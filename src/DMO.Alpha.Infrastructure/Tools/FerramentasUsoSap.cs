using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// A utilização das ferramentas pelo SAP sobre o <see cref="DmoDbContext"/>.
/// Cada linha do SAP vai para a ferramenta com o mesmo nome técnico e lote:
/// grava-se uma leitura nova quando a ferramenta tem o lembrete aberto, ainda
/// não tem leitura ou a percentagem mudou, e o lembrete fecha-se com ela. Ler
/// o mesmo ficheiro outra vez não acrescenta nada.
/// </summary>
public sealed class FerramentasUsoSap(DmoDbContext db, ICurrentAccountContext account) : IFerramentasUsoSap
{
    public async Task<ImportarUsoSapOutcome> ImportarAsync(string? conteudo, string? ficheiro, DateTime? ficheiroModificadoEmUtc, CancellationToken cancellationToken = default)
    {
        var leitura = UsoSapExportacao.Ler(conteudo);
        if (!leitura.CabecalhoEncontrado)
        {
            return new ImportarUsoSapOutcome(ImportarUsoSapResultado.NaoELista, 0, 0, []);
        }

        await using var transacao = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        var ferramentas = await db.Tools.AsNoTracking()
            .Where(t => t.TechnicalName != null && t.TechnicalName != "" && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.TechnicalName, t.Lot })
            .ToListAsync(cancellationToken);
        // O código que o script procurou liga a linha à ferramenta (a tabela
        // dos códigos de procura e o «Código SAP», DMO-585); sem ele, o nome
        // técnico + lote como sempre.
        var porProcura = (await CodigosPorFerramentaAsync(db.Tools.Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId), cancellationToken))
            .ToLookup(c => c.Codigo, c => c.ToolId, StringComparer.Ordinal);
        var agora = DateTime.UtcNow;
        var autor = Autor();
        var nome = ficheiro is { Length: > 0 } f ? f[..Math.Min(f.Length, FerramentaUtilizacao.FicheiroMaxLength)] : null;
        var semFerramenta = new List<string>();
        var novas = new List<(FerramentaUtilizacao Leitura, List<FerramentaLembreteUtilizacao> Lembretes)>();
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        var aLer = new List<(UsoSapLinha Linha, UsoSapInformacao Info, string ToolId)>();
        foreach (var (linha, indice) in leitura.Linhas.Select((linha, indice) => (linha, indice)))
        {
            var daLinha = ferramentas.Where(t => UsoSapCodigo.Corresponde(linha.CodigoEquipamento, t.TechnicalName, t.Lot)).Select(t => t.ToolId).ToList();
            if (daLinha.Count == 0)
            {
                daLinha = porProcura[linha.Procura ?? linha.CodigoEquipamento].Concat(porProcura[linha.CodigoEquipamento]).Distinct(StringComparer.Ordinal).ToList();
            }

            if (daLinha.Count == 0)
            {
                semFerramenta.Add(linha.CodigoEquipamento);
                continue;
            }

            aLer.AddRange(daLinha.Where(id => vistas.Add(id)).Select(id => (linha, leitura.Informacao[indice], id)));
        }

        // Os lembretes abertos e a última percentagem de todas as ferramentas
        // do ficheiro em duas consultas, seja qual for o número de linhas
        // (antes eram duas por ferramenta; vistoria ao backend, 27/10/2026).
        var ids = aLer.Select(a => a.ToolId).ToList();
        var retratos = db.Database.IsNpgsql()
            ? await db.Tools.FromSqlInterpolated($"SELECT *, xmin FROM \"Tools\" WHERE \"ToolId\" = ANY({ids.ToArray()}) ORDER BY \"ToolId\" FOR UPDATE").ToDictionaryAsync(t => t.ToolId, cancellationToken)
            : await db.Tools.Where(t => ids.Contains(t.ToolId)).ToDictionaryAsync(t => t.ToolId, cancellationToken);
        List<FerramentaLembreteUtilizacao> abertos = ids.Count == 0
            ? []
            : await db.FerramentaLembretesUtilizacao
                .Where(l => ids.Contains(l.ToolId) && l.FechadoEmUtc == null)
                .ToListAsync(cancellationToken);
        var ultimas = ids.Count == 0
            ? new Dictionary<string, FerramentaUtilizacao>()
            : await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => ids.Contains(u.ToolId)
                    && u.Id == db.FerramentaUtilizacoes.Where(o => o.ToolId == u.ToolId).Max(o => o.Id))
                .ToDictionaryAsync(u => u.ToolId, StringComparer.Ordinal, cancellationToken);
        var propostas = 0;
        foreach (var (linha, info, toolId) in aLer)
        {
            var lembretes = abertos.Where(l => l.ToolId == toolId).ToList();
            var retrato = retratos[toolId];
            var ultimaLeitura = ultimas.GetValueOrDefault(toolId);
            decimal? ultima = ultimaLeitura?.Percentagem;
            var mudou = retrato.SapPercentagem != linha.PercentagemUso;
            var mudouDados = (info.Pecas is not null && retrato.SapPecas != info.Pecas)
                || (info.Fornecedor is not null && retrato.SapFornecedor != info.Fornecedor)
                || (info.DataAquisicao is not null && retrato.SapDataAquisicao != info.DataAquisicao);
            retrato.SapPercentagem = linha.PercentagemUso;
            // Exportações antigas sem estas colunas não apagam informação já lida.
            if (info.Pecas is { } pecas) retrato.SapPecas = pecas;
            if (info.Fornecedor is { } fornecedor) retrato.SapFornecedor = fornecedor;
            if (info.DataAquisicao is { } compra) retrato.SapDataAquisicao = compra;
            if (ultimaLeitura?.Origem == FerramentaUtilizacao.OrigemManual)
            {
                if ((mudou && ultima != linha.PercentagemUso) || mudouDados)
                {
                    retrato.SapAvisosAlteradosEmUtc = agora;
                    propostas++;
                }
                // A origem continua manual até à escolha «Sim»; fecha-se o lembrete
                // porque o SAP já respondeu, sem fabricar uma leitura aplicada.
                foreach (var lembrete in lembretes)
                {
                    lembrete.FechadoEmUtc = agora;
                    lembrete.UtilizacaoId = ultimaLeitura.Id;
                }
                continue;
            }
            if (lembretes.Count == 0 && ultima == linha.PercentagemUso)
            {
                if (mudouDados) propostas++;
                continue;
            }

            var nova = new FerramentaUtilizacao
            {
                ToolId = toolId,
                Percentagem = linha.PercentagemUso,
                Origem = FerramentaUtilizacao.OrigemSap,
                RegistadaEmUtc = agora,
                RegistadoPor = autor,
                CodigoEquipamento = linha.CodigoEquipamento,
                Consumo = linha.Consumo,
                VidaPrevista = linha.VidaPrevista,
                Ficheiro = nome,
                FicheiroModificadoEmUtc = ficheiroModificadoEmUtc
            };
            db.FerramentaUtilizacoes.Add(nova);
            novas.Add((nova, lembretes));
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var (nova, lembretes) in novas)
        {
            foreach (var lembrete in lembretes)
            {
                lembrete.FechadoEmUtc = agora;
                lembrete.UtilizacaoId = nova.Id;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        if (transacao is not null)
        {
            await transacao.CommitAsync(cancellationToken);
        }

        return new ImportarUsoSapOutcome(ImportarUsoSapResultado.Gravado, leitura.Linhas.Count, novas.Count + propostas, semFerramenta.Distinct().ToList());
    }

    public Task<IReadOnlyList<string>> CodigosAsync(CancellationToken cancellationToken = default) =>
        CodigosDeAsync(db.Tools.Where(t => db.FerramentaLembretesUtilizacao.Any(l => l.ToolId == t.ToolId && l.FechadoEmUtc == null)), cancellationToken);

    public Task<IReadOnlyList<string>> TodosOsCodigosAsync(CancellationToken cancellationToken = default) =>
        CodigosDeAsync(db.Tools.Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId), cancellationToken);

    public Task<IReadOnlyList<string>> CodigosDaReferenciaAsync(string referencia, ToolType? tipo, CancellationToken cancellationToken = default)
    {
        var texto = (referencia ?? string.Empty).Trim().ToUpperInvariant();
        if (texto.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var ferramentas = db.Tools.Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId
            && (t.Reference.ToUpper().Contains(texto) || (t.AssociatedMfReference != null && t.AssociatedMfReference.ToUpper().Contains(texto))));
        if (tipo is { } t1)
        {
            ferramentas = ferramentas.Where(t => t.Type == t1);
        }

        return CodigosDeAsync(ferramentas, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> CodigosDeAsync(IQueryable<Tool> ferramentasPedidas, CancellationToken cancellationToken) =>
        (await CodigosPorFerramentaAsync(ferramentasPedidas, cancellationToken))
            .Select(c => c.Codigo)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    // O código de cada ferramenta pela tabela dos códigos de procura do
    // Admin › SAP (DMO-585) e pelo «Código SAP» escrito na ferramenta.
    private async Task<List<(string ToolId, string Codigo)>> CodigosPorFerramentaAsync(IQueryable<Tool> ferramentasPedidas, CancellationToken cancellationToken)
    {
        var regras = await SapProcuraRegras.LerDeAsync(db, cancellationToken);
        var ferramentas = await ferramentasPedidas.AsNoTracking()
            .Select(t => new { t.ToolId, t.Type, t.TechnicalName, t.Reference, t.Lot, t.SapCodigo })
            .ToListAsync(cancellationToken);
        var comLinhas = regras.Any(r => r.Linha.Length > 0);
        var ids = ferramentas.Select(f => f.ToolId).ToList();
        var linhas = comLinhas
            ? (await db.ToolMachineLinks.AsNoTracking()
                .Where(l => ids.Contains(l.ToolId))
                .Select(l => new { l.ToolId, l.Machine.Code })
                .ToListAsync(cancellationToken))
                .ToLookup(l => l.ToolId, l => l.Code)
            : null;
        return ferramentas
            .Select(f => (f.ToolId, Codigo: SapProcura.Codigo(regras, f.Type, linhas?[f.ToolId] ?? [], f.SapCodigo, f.TechnicalName, f.Reference, f.Lot)))
            .Where(c => c.Codigo is not null)
            .Select(c => (c.ToolId, c.Codigo!))
            .ToList();
    }

    public async Task<UsoSapUltimaLeitura?> UltimaLeituraAsync(CancellationToken cancellationToken = default)
    {
        var ultima = await db.FerramentaUtilizacoes.AsNoTracking()
            .Where(u => u.Origem == FerramentaUtilizacao.OrigemSap)
            .OrderByDescending(u => u.Id)
            .Select(u => new { u.RegistadaEmUtc, u.FicheiroModificadoEmUtc, u.Ficheiro })
            .FirstOrDefaultAsync(cancellationToken);
        return ultima is null ? null : new UsoSapUltimaLeitura(ultima.RegistadaEmUtc, ultima.FicheiroModificadoEmUtc, ultima.Ficheiro);
    }

    public async Task<FerramentaUtilizacaoAtual> DaFerramentaAsync(string toolId, CancellationToken cancellationToken = default)
    {
        var ultima = await db.FerramentaUtilizacoes.AsNoTracking()
            .Where(u => u.ToolId == toolId)
            .OrderByDescending(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var lembrete = await db.FerramentaLembretesUtilizacao.AsNoTracking()
            .Where(l => l.ToolId == toolId && l.FechadoEmUtc == null)
            .Select(l => (DateTime?)l.CriadoEmUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return new FerramentaUtilizacaoAtual(ultima, lembrete);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> UltimasPercentagensAsync(IReadOnlyCollection<string> toolIds, CancellationToken cancellationToken = default)
    {
        if (toolIds.Count == 0) return new Dictionary<string, decimal>();
        return (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => toolIds.Contains(u.ToolId))
                .Select(u => new { u.ToolId, u.Id, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => g.MaxBy(u => u.Id)!.Percentagem);
    }

    public async Task<bool> PedirAsync(string toolId, CancellationToken cancellationToken = default)
    {
        if (!await db.Tools.AnyAsync(t => t.ToolId == toolId && t.ToolId != ToolRegistryConstants.RemovedToolId, cancellationToken))
        {
            return false;
        }

        if (!await db.FerramentaLembretesUtilizacao.AnyAsync(l => l.ToolId == toolId && l.FechadoEmUtc == null, cancellationToken))
        {
            db.FerramentaLembretesUtilizacao.Add(new FerramentaLembreteUtilizacao { ToolId = toolId, CriadoEmUtc = DateTime.UtcNow, CriadoPor = Autor() });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Dois pedidos ao mesmo tempo: o outro já o abriu (o índice
                // único); sem nenhum aberto, o erro é outro.
                db.ChangeTracker.Clear();
                if (!await db.FerramentaLembretesUtilizacao.AsNoTracking().AnyAsync(l => l.ToolId == toolId && l.FechadoEmUtc == null, cancellationToken))
                {
                    throw;
                }
            }
        }

        return true;
    }

    public async Task<bool> RegistarPercentagemManualAsync(string toolId, decimal percentagem, CancellationToken cancellationToken = default)
    {
        if (percentagem < 0 || !await db.Tools.AnyAsync(t => t.ToolId == toolId && t.ToolId != ToolRegistryConstants.RemovedToolId, cancellationToken))
        {
            return false;
        }

        await using var transacao = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var tool = db.Database.IsNpgsql()
            ? await db.Tools.FromSqlInterpolated($"SELECT *, xmin FROM \"Tools\" WHERE \"ToolId\" = {toolId} FOR UPDATE").SingleAsync(cancellationToken)
            : await db.Tools.SingleAsync(t => t.ToolId == toolId, cancellationToken);
        // A ficha pode já ter esta Tool em tracking desde o Editar. Depois
        // do trinco, recarregar também o xmin e o retrato que outra importação gravou.
        if (db.Database.IsNpgsql()) await db.Entry(tool).ReloadAsync(cancellationToken);

        var anterior = await db.FerramentaUtilizacoes.AsNoTracking()
            .Where(u => u.ToolId == toolId)
            .OrderByDescending(u => u.Id)
            .Select(u => (decimal?)u.Percentagem)
            .FirstOrDefaultAsync(cancellationToken);
        // SAP-B06 (sweep .230): comparar o valor exato, não arredondado — uma
        // correção manual igual à parte inteira da última leitura SAP
        // (90,2 → 90) é uma correção real e deve gravar.
        if (anterior is { } a && a == percentagem)
        {
            return false;
        }

        var agora = DateTime.UtcNow;
        tool.SapAvisosAlteradosEmUtc = agora;
        var nova = new FerramentaUtilizacao
        {
            ToolId = toolId,
            Percentagem = percentagem,
            Origem = FerramentaUtilizacao.OrigemManual,
            RegistadaEmUtc = agora,
            RegistadoPor = Autor()
        };
        db.FerramentaUtilizacoes.Add(nova);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var lembrete in await db.FerramentaLembretesUtilizacao.Where(l => l.ToolId == toolId && l.FechadoEmUtc == null).ToListAsync(cancellationToken))
        {
            lembrete.FechadoEmUtc = agora;
            lembrete.UtilizacaoId = nova.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (transacao is not null)
        {
            await transacao.CommitAsync(cancellationToken);
        }

        return true;
    }

    public async Task<IReadOnlyList<FerramentaUsoPorAtualizar>> PorAtualizarAsync(int maximo, CancellationToken cancellationToken = default) =>
        await (from l in db.FerramentaLembretesUtilizacao.AsNoTracking()
               join t in db.Tools.AsNoTracking() on l.ToolId equals t.ToolId
               where l.FechadoEmUtc == null && t.ToolId != ToolRegistryConstants.RemovedToolId
               orderby l.CriadoEmUtc, l.Id
               select new FerramentaUsoPorAtualizar(t.ToolId, t.Type, t.Reference, t.Lot, l.CriadoEmUtc))
            .Take(maximo)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// A Entrada no Armazém vinda da Produção abre o lembrete (na mesma
    /// gravação do movimento); com um aberto, fica esse.
    /// </summary>
    public static async Task LembrarAsync(DmoDbContext db, string toolId, int movimentoId, string? autor, DateTime agora, CancellationToken cancellationToken)
    {
        if (db.FerramentaLembretesUtilizacao.Local.Any(l => l.ToolId == toolId && l.FechadoEmUtc == null)
            || await db.FerramentaLembretesUtilizacao.AnyAsync(l => l.ToolId == toolId && l.FechadoEmUtc == null, cancellationToken))
        {
            return;
        }

        db.FerramentaLembretesUtilizacao.Add(new FerramentaLembreteUtilizacao { ToolId = toolId, CriadoEmUtc = agora, MovimentoId = movimentoId, CriadoPor = autor });
    }

    private string? Autor()
    {
        var actor = account.Current;
        return actor.IsAuthenticated
            ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName)
            : null;
    }
}

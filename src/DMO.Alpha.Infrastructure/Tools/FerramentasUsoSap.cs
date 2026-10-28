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

        var ferramentas = await db.Tools.AsNoTracking()
            .Where(t => t.TechnicalName != null && t.TechnicalName != "" && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.TechnicalName, t.Lot })
            .ToListAsync(cancellationToken);
        var agora = DateTime.UtcNow;
        var autor = Autor();
        var nome = ficheiro is { Length: > 0 } f ? f[..Math.Min(f.Length, FerramentaUtilizacao.FicheiroMaxLength)] : null;
        var semFerramenta = new List<string>();
        var novas = new List<(FerramentaUtilizacao Leitura, List<FerramentaLembreteUtilizacao> Lembretes)>();
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        var aLer = new List<(UsoSapLinha Linha, string ToolId)>();
        foreach (var linha in leitura.Linhas)
        {
            var daLinha = ferramentas.Where(t => UsoSapCodigo.Corresponde(linha.CodigoEquipamento, t.TechnicalName, t.Lot)).ToList();
            if (daLinha.Count == 0)
            {
                semFerramenta.Add(linha.CodigoEquipamento);
                continue;
            }

            aLer.AddRange(daLinha.Where(t => vistas.Add(t.ToolId)).Select(t => (linha, t.ToolId)));
        }

        // Os lembretes abertos e a última percentagem de todas as ferramentas
        // do ficheiro em duas consultas, seja qual for o número de linhas
        // (antes eram duas por ferramenta; vistoria ao backend, 27/10/2026).
        var ids = aLer.Select(a => a.ToolId).ToList();
        List<FerramentaLembreteUtilizacao> abertos = ids.Count == 0
            ? []
            : await db.FerramentaLembretesUtilizacao
                .Where(l => ids.Contains(l.ToolId) && l.FechadoEmUtc == null)
                .ToListAsync(cancellationToken);
        var ultimas = ids.Count == 0
            ? new Dictionary<string, decimal>()
            : await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => ids.Contains(u.ToolId)
                    && u.Id == db.FerramentaUtilizacoes.Where(o => o.ToolId == u.ToolId).Max(o => o.Id))
                .ToDictionaryAsync(u => u.ToolId, u => u.Percentagem, StringComparer.Ordinal, cancellationToken);
        foreach (var (linha, toolId) in aLer)
        {
            var lembretes = abertos.Where(l => l.ToolId == toolId).ToList();
            decimal? ultima = ultimas.TryGetValue(toolId, out var percentagem) ? percentagem : null;
            if (lembretes.Count == 0 && ultima == linha.PercentagemUso)
            {
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

        await using var transacao = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
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

        return new ImportarUsoSapOutcome(ImportarUsoSapResultado.Gravado, leitura.Linhas.Count, novas.Count, semFerramenta.Distinct().ToList());
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

    private static async Task<IReadOnlyList<string>> CodigosDeAsync(IQueryable<Tool> ferramentasPedidas, CancellationToken cancellationToken)
    {
        var ferramentas = await ferramentasPedidas.AsNoTracking()
            .Where(t => t.TechnicalName != null && t.TechnicalName != "")
            .Select(t => new { t.TechnicalName, t.Lot })
            .ToListAsync(cancellationToken);
        return ferramentas
            .Select(t => UsoSapCodigo.Da(t.TechnicalName, t.Lot))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
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

        var anterior = await db.FerramentaUtilizacoes.AsNoTracking()
            .Where(u => u.ToolId == toolId)
            .OrderByDescending(u => u.Id)
            .Select(u => (decimal?)u.Percentagem)
            .FirstOrDefaultAsync(cancellationToken);
        if (anterior is { } a && Math.Round(a, 0, MidpointRounding.AwayFromZero) == percentagem)
        {
            return false;
        }

        var agora = DateTime.UtcNow;
        var nova = new FerramentaUtilizacao
        {
            ToolId = toolId,
            Percentagem = percentagem,
            Origem = FerramentaUtilizacao.OrigemManual,
            RegistadaEmUtc = agora,
            RegistadoPor = Autor()
        };
        db.FerramentaUtilizacoes.Add(nova);
        await using var transacao = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
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

using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// O fabrico pelo SAP sobre o <see cref="DmoDbContext"/>: cada linha do
/// relatório fica uma vez (material, linha, início e fim); lida outra vez,
/// atualiza as produções. As produções de uma ferramenta são os Job On em que
/// ela foi a CM, o MF ou a BQ (também os em que foi trocada), com a soma das
/// linhas do SAP da mesma referência, máquina e datas.
/// </summary>
public sealed class FerramentasFabricoSap(DmoDbContext db, ICurrentAccountContext account) : IFerramentasFabricoSap
{
    public async Task<ImportarFabricoSapOutcome> ImportarAsync(IReadOnlyList<FabricoSapLinha> linhas, string? ficheiro, DateTime? ficheiroModificadoEmUtc,
        IReadOnlyList<FabricoSapDetalhe>? detalhes = null, CancellationToken cancellationToken = default)
    {
        var detalheDe = (detalhes ?? [])
            .GroupBy(d => (d.Material, d.Linha, d.InicioEm, d.FimEm))
            .ToDictionary(g => g.Key, g => g.Last());
        var agora = DateTime.UtcNow;
        var actor = account.Current;
        var autor = actor.IsAuthenticated ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName) : null;
        var nome = ficheiro is { Length: > 0 } f ? f[..Math.Min(f.Length, FerramentaUtilizacao.FicheiroMaxLength)] : null;
        var unicas = linhas
            .GroupBy(l => (l.Material, l.Linha, l.InicioEm, l.FimEm))
            .Select(g => g.Last())
            .ToList();
        var materiais = unicas.Select(l => l.Material).Distinct().ToList();
        var existentes = await db.SapFabricos
            .Where(s => materiais.Contains(s.Material))
            .ToListAsync(cancellationToken);
        var novas = 0;
        foreach (var linha in unicas)
        {
            var existente = existentes.FirstOrDefault(s => s.Material == linha.Material && s.Linha == linha.Linha && s.InicioEm == linha.InicioEm && s.FimEm == linha.FimEm);
            if (existente is null)
            {
                existente = new SapFabrico { Material = linha.Material, Linha = linha.Linha, InicioEm = linha.InicioEm, FimEm = linha.FimEm };
                db.SapFabricos.Add(existente);
                novas++;
            }

            // SAP-B02: um relatório parcial (só com Net Prod., ou sem a coluna
            // Avg weight) não tem estes campos; um `null` dele é «não sei», não
            // «apaga» o que um relatório anterior já tinha lido.
            if (linha.ProducaoBruta is { } bruta)
            {
                existente.ProducaoBruta = bruta;
            }

            if (linha.ProducaoLiquida is { } liquida)
            {
                existente.ProducaoLiquida = liquida;
            }

            if (linha.PesoMedioGramas is { } peso)
            {
                existente.PesoMedioGramas = peso;
            }

            if (linha.DiasProducao is { } dias)
            {
                existente.DiasProducao = dias;
            }

            if (detalheDe.TryGetValue((linha.Material, linha.Linha, linha.InicioEm, linha.FimEm), out var detalhe))
            {
                existente.Cadencia = detalhe.Cadencia;
            }

            existente.LidoEmUtc = agora;
            existente.LidoPor = autor;
            existente.Ficheiro = nome;
            existente.FicheiroModificadoEmUtc = ficheiroModificadoEmUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportarFabricoSapOutcome(unicas.Count, novas);
    }

    public Task<DateTime?> UltimoFicheiroAsync(CancellationToken cancellationToken = default) =>
        db.SapFabricos.AsNoTracking().MaxAsync(s => s.FicheiroModificadoEmUtc, cancellationToken);

    public async Task<CadenciaDoJobOn?> CadenciaDoJobOnAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var atual = await db.JobOns.AsNoTracking()
            .Where(j => j.Id == jobOnId)
            .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
            .FirstOrDefaultAsync(cancellationToken);
        if (atual is null)
        {
            return null;
        }

        // A própria produção; sem cadência, as anteriores da mesma referência
        // pela regra única de «anterior» (a do Peso), a mais recente primeiro.
        var anteriores = await db.JobOns.AsNoTracking()
            .Where(j => j.Reference == atual.Reference && j.Id != atual.Id)
            .SoAnteriores(j => j, atual.Id, atual.InicioEm)
            .MaisRecentePrimeiro(j => j)
            .Take(IProductionPickerQuery.MaxResults)
            .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
            .ToListAsync(cancellationToken);
        var producoes = anteriores.Prepend(atual).Where(j => j.InicioEm.HasValue).ToList();
        if (producoes.Count == 0)
        {
            return null;
        }

        var maquinas = producoes.Select(j => j.Maquina.ToUpperInvariant()).Distinct().ToList();
        var de = producoes.Min(j => j.InicioEm!.Value);
        var sap = await db.SapFabricos.AsNoTracking()
            .Where(s => maquinas.Contains(s.Linha) && s.FimEm >= de && s.Cadencia != null)
            .ToListAsync(cancellationToken);
        var iniciosPorMaquina = await IniciosPorMaquinaAsync(maquinas, cancellationToken);
        foreach (var j in producoes)
        {
            var proximo = ProximoNaMaquina(iniciosPorMaquina, j.Maquina, j.InicioEm!.Value);
            var ordens = sap.Where(s => FabricoSapExportacao.Corresponde(s.Material, s.Linha, s.InicioEm, s.FimEm, j.Reference, j.Maquina, j.InicioEm, j.FimEm, proximo));
            if (FabricoSapCadencia.Resumo(ordens, j.InicioEm!.Value, j.FimEm) is { } resumo)
            {
                return j.Id == atual.Id
                    ? new CadenciaDoJobOn(resumo)
                    : new CadenciaDoJobOn(resumo, j.Id, j.ProductionNumber);
            }
        }

        return null;
    }

    public async Task<IReadOnlyDictionary<int, PesoSapResumo>> PesoDosJobOnsAsync(IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default)
    {
        var jobOns = await db.JobOns.AsNoTracking()
            .Where(j => jobOnIds.Contains(j.Id) && j.InicioEm != null)
            .Select(j => new { j.Id, j.Reference, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
            .ToListAsync(cancellationToken);
        var resumos = new Dictionary<int, PesoSapResumo>();
        if (jobOns.Count == 0)
        {
            return resumos;
        }

        var maquinas = jobOns.Select(j => j.Maquina.ToUpperInvariant()).Distinct().ToList();
        var de = jobOns.Min(j => j.InicioEm!.Value);
        var sap = await db.SapFabricos.AsNoTracking()
            .Where(s => maquinas.Contains(s.Linha) && s.FimEm >= de && s.PesoMedioGramas != null)
            .ToListAsync(cancellationToken);
        var iniciosPorMaquina = await IniciosPorMaquinaAsync(maquinas, cancellationToken);
        foreach (var j in jobOns)
        {
            var proximo = ProximoNaMaquina(iniciosPorMaquina, j.Maquina, j.InicioEm!.Value);
            var ordens = sap.Where(s => FabricoSapExportacao.Corresponde(s.Material, s.Linha, s.InicioEm, s.FimEm, j.Reference, j.Maquina, j.InicioEm, j.FimEm, proximo));
            if (FabricoSapPeso.Resumo(ordens, j.InicioEm!.Value, j.FimEm) is { } resumo)
            {
                resumos[j.Id] = resumo;
            }
        }

        return resumos;
    }

    public async Task<FerramentaProducoes> ProducoesAsync(string toolId, int? pagina, int porPagina, CancellationToken cancellationToken = default)
    {
        var cm = db.CmContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId);
        var mf = db.MfContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId);
        var bq = db.BqContexts.Where(c => c.ToolId == toolId).Select(c => c.JobOnId ?? c.ReplacedJobOnId);
        var ids = (await cm.Concat(mf).Concat(bq).ToListAsync(cancellationToken))
            .OfType<int>()
            .Concat(await JobOnsPeloArmazemAsync(toolId, cancellationToken))
            .Distinct()
            .ToList();
        // Uma só consulta a JobOns: as da própria ferramenta, mais (SAP-B01)
        // todas as outras da mesma máquina com início, para saber onde para
        // uma produção sem fim — sem gastar uma viagem à base a mais por
        // ferramenta (o «antes» da comparação do Histórico conta isto).
        var maquinasDaFerramenta = db.JobOns.Where(j => ids.Contains(j.Id) && j.InicioEm != null).Select(j => j.MachineId);
        var todos = await db.JobOns.AsNoTracking()
            .Where(j => ids.Contains(j.Id) || (j.InicioEm != null && maquinasDaFerramenta.Contains(j.MachineId)))
            .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
            .ToListAsync(cancellationToken);
        var jobOns = todos.Where(j => ids.Contains(j.Id))
            .OrderByDescending(j => j.InicioEm.HasValue)
            .ThenByDescending(j => j.InicioEm)
            .ThenByDescending(j => j.Id)
            .ToList();
        var comDatas = jobOns.Where(j => j.InicioEm.HasValue).ToList();
        var sap = new List<SapFabrico>();
        var iniciosPorMaquina = new Dictionary<string, List<DateOnly>>(StringComparer.Ordinal);
        if (comDatas.Count > 0)
        {
            var maquinas = comDatas.Select(j => j.Maquina.ToUpperInvariant()).Distinct().ToList();
            var de = comDatas.Min(j => j.InicioEm!.Value);
            // Sem fim, o Job On ainda está na máquina: conta tudo desde o início.
            var ate = comDatas.Max(j => j.FimEm is { } f ? (f >= j.InicioEm!.Value ? f : j.InicioEm!.Value) : DateOnly.MaxValue);
            sap = await db.SapFabricos.AsNoTracking()
                .Where(s => maquinas.Contains(s.Linha) && s.InicioEm <= ate && s.FimEm >= de)
                .ToListAsync(cancellationToken);
            iniciosPorMaquina = todos
                .Where(j => j.InicioEm != null)
                .GroupBy(j => j.Maquina.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Select(j => j.InicioEm!.Value).OrderBy(d => d).ToList(), StringComparer.Ordinal);
        }

        // As leituras da utilização, pela data em Portugal.
        var leituras = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => u.ToolId == toolId)
                .OrderBy(u => u.RegistadaEmUtc)
                .ThenBy(u => u.Id)
                .Select(u => new { u.RegistadaEmUtc, u.Percentagem })
                .ToListAsync(cancellationToken))
            .Select(u => (Dia: DateOnly.FromDateTime(DmoBusinessTime.ToLocal(u.RegistadaEmUtc)), u.Percentagem))
            .ToList();

        var inicios = jobOns.Select(j => j.InicioEm).ToList();
        var itens = jobOns.Select(j => Montar(j.Id, j.Reference, j.ProductionNumber, j.Maquina, j.InicioEm, j.FimEm, sap, leituras, ProximoInicio(inicios, j.InicioEm),
            j.InicioEm is { } de ? ProximoNaMaquina(iniciosPorMaquina, j.Maquina, de) : null)).ToList();
        return new FerramentaProducoes(
            PagedList<FerramentaProducao>.FromLoaded(itens, pagina, porPagina),
            itens.Any(p => p.ProducaoBruta.HasValue) ? itens.Sum(p => p.ProducaoBruta ?? 0) : null,
            itens.Any(p => p.ProducaoLiquida.HasValue) ? itens.Sum(p => p.ProducaoLiquida ?? 0) : null);
    }

    /// <summary>
    /// Os Job On em que a ferramenta esteve pela história do Armazém (Diogo,
    /// 28/10/2026: «deve aparecer o que a tool_id tem e não o Job On»): cada
    /// Saída para a Produção numa Linha, até ao movimento seguinte da
    /// ferramenta (sem ele, ainda lá está), dá os Job On dessa Linha com datas
    /// dentro desse tempo, de qualquer artigo. O Job On do próprio movimento
    /// também conta.
    /// </summary>
    private async Task<IReadOnlyList<int>> JobOnsPeloArmazemAsync(string toolId, CancellationToken cancellationToken) =>
        (await JobOnsPeloArmazemAsync([toolId], cancellationToken)).Select(r => r.JobOnId).ToList();

    // O mesmo para várias ferramentas (o Histórico): uma leitura dos
    // movimentos e, só com Saídas para uma Linha, uma dos Job On.
    private async Task<IReadOnlyList<(string ToolId, int JobOnId)>> JobOnsPeloArmazemAsync(IReadOnlyCollection<string> toolIds, CancellationToken cancellationToken)
    {
        var movimentos = await db.ArmazemMovimentos.AsNoTracking()
            .Where(m => toolIds.Contains(m.ToolId))
            .OrderBy(m => m.OcorridoEmUtc).ThenBy(m => m.Id)
            .Select(m => new { m.ToolId, m.Tipo, m.Destino, m.MachineId, m.JobOnId, m.OcorridoEmUtc })
            .ToListAsync(cancellationToken);
        var ids = new List<(string, int)>();
        var periodos = new List<(string ToolId, int Maquina, DateOnly De, DateOnly? Ate)>();
        foreach (var daFerramenta in movimentos.GroupBy(m => m.ToolId))
        {
            var lista = daFerramenta.ToList();
            for (var i = 0; i < lista.Count; i++)
            {
                var m = lista[i];
                if (m.Tipo != Core.Armazem.ArmazemMovimentoTipo.Saida || m.Destino != Core.Armazem.ArmazemDestino.Producao)
                {
                    continue;
                }

                if (m.JobOnId is { } jobOnId)
                {
                    ids.Add((m.ToolId, jobOnId));
                }

                if (m.MachineId is { } maquina)
                {
                    var de = DateOnly.FromDateTime(DmoBusinessTime.ToLocal(m.OcorridoEmUtc));
                    DateOnly? ate = i + 1 < lista.Count ? DateOnly.FromDateTime(DmoBusinessTime.ToLocal(lista[i + 1].OcorridoEmUtc)) : null;
                    periodos.Add((m.ToolId, maquina, de, ate));
                }
            }
        }

        if (periodos.Count > 0)
        {
            var maquinas = periodos.Select(p => p.Maquina).Distinct().ToList();
            var desde = periodos.Min(p => p.De);
            var candidatos = await db.JobOns.AsNoTracking()
                .Where(j => maquinas.Contains(j.MachineId) && j.InicioEm != null && (j.FimEm == null || j.FimEm >= desde))
                .Select(j => new { j.Id, j.MachineId, j.InicioEm, j.FimEm })
                .ToListAsync(cancellationToken);
            foreach (var p in periodos)
            {
                ids.AddRange(candidatos
                    .Where(j => p.Maquina == j.MachineId
                        && (p.Ate is not { } ate || j.InicioEm!.Value <= ate)
                        && (j.FimEm ?? DateOnly.MaxValue) >= p.De)
                    .Select(j => (p.ToolId, j.Id)));
            }
        }

        return ids;
    }

    /// <summary>
    /// As produções de várias ferramentas de uma vez (o Histórico): as mesmas
    /// de <see cref="ProducoesAsync"/> para cada uma, na mesma ordem, mas com um
    /// número fixo de leituras e só os Job On que cabem na linha e nos dias do
    /// filtro. A ferramenta sem produções fica de fora.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<FerramentaProducao>>> ProducoesDeFerramentasAsync(
        IReadOnlyCollection<string> toolIds, HistoricoFiltro? filtro, CancellationToken cancellationToken = default)
    {
        var resultado = new Dictionary<string, IReadOnlyList<FerramentaProducao>>(StringComparer.Ordinal);
        if (toolIds.Count == 0)
        {
            return resultado;
        }

        var relacoes = (await db.CmContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => new { c.ToolId, JobOnId = c.JobOnId ?? c.ReplacedJobOnId }).ToListAsync(cancellationToken))
            .Concat(await db.MfContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => new { c.ToolId, JobOnId = c.JobOnId ?? c.ReplacedJobOnId }).ToListAsync(cancellationToken))
            .Concat(await db.BqContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => new { c.ToolId, JobOnId = c.JobOnId ?? c.ReplacedJobOnId }).ToListAsync(cancellationToken))
            .Where(c => c.JobOnId.HasValue)
            .Select(c => (c.ToolId, JobOnId: c.JobOnId!.Value))
            // E os da Linha para onde saiu do Armazém, como na ficha (DMO-571).
            .Concat(await JobOnsPeloArmazemAsync(toolIds, cancellationToken))
            .Distinct()
            .ToList();
        if (relacoes.Count == 0)
        {
            return resultado;
        }

        var ids = relacoes.Select(r => r.JobOnId).Distinct().ToList();
        // Todos os Job On das ferramentas (o filtro aplica-se a seguir): a
        // leitura «depois» de uma produção precisa de saber quando começou a
        // seguinte, mesmo que essa fique fora do filtro. Numa só consulta
        // (SAP-B01): também as outras da mesma máquina com início, para saber
        // onde para uma produção sem fim — sem uma viagem à base a mais.
        var maquinasDasFerramentas = db.JobOns.Where(j => ids.Contains(j.Id) && j.InicioEm != null).Select(j => j.MachineId);
        var todosGeral = await db.JobOns.AsNoTracking()
            .Where(j => ids.Contains(j.Id) || (j.InicioEm != null && maquinasDasFerramentas.Contains(j.MachineId)))
            .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm, j.FimEm })
            .ToListAsync(cancellationToken);
        var todosJobOns = todosGeral.Where(j => ids.Contains(j.Id))
            .OrderByDescending(j => j.InicioEm.HasValue)
            .ThenByDescending(j => j.InicioEm)
            .ThenByDescending(j => j.Id)
            .ToList();
        var linhaFiltro = filtro?.Linha is { } linha && !string.IsNullOrWhiteSpace(linha) ? linha.Trim() : null;
        var jobOns = todosJobOns
            .Where(j => (linhaFiltro is null || string.Equals(j.Maquina, linhaFiltro, StringComparison.OrdinalIgnoreCase))
                && (filtro?.De is not { } de || j.FimEm is null || j.FimEm >= de)
                && (filtro?.Ate is not { } ate || (j.InicioEm is { } inicio && inicio <= ate)))
            .ToList();
        if (jobOns.Count == 0)
        {
            return resultado;
        }

        var sap = new List<SapFabrico>();
        var iniciosPorMaquina = new Dictionary<string, List<DateOnly>>(StringComparer.Ordinal);
        var comDatas = jobOns.Where(j => j.InicioEm.HasValue).ToList();
        if (comDatas.Count > 0)
        {
            var maquinas = comDatas.Select(j => j.Maquina.ToUpperInvariant()).Distinct().ToList();
            var de = comDatas.Min(j => j.InicioEm!.Value);
            var ate = comDatas.Max(j => j.FimEm is { } f ? (f >= j.InicioEm!.Value ? f : j.InicioEm!.Value) : DateOnly.MaxValue);
            sap = await db.SapFabricos.AsNoTracking()
                .Where(s => maquinas.Contains(s.Linha) && s.InicioEm <= ate && s.FimEm >= de)
                .ToListAsync(cancellationToken);
            iniciosPorMaquina = todosGeral
                .Where(j => j.InicioEm != null)
                .GroupBy(j => j.Maquina.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Select(j => j.InicioEm!.Value).OrderBy(d => d).ToList(), StringComparer.Ordinal);
        }

        var comProducao = relacoes.Select(r => r.ToolId).Distinct().ToList();
        var leituras = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => comProducao.Contains(u.ToolId))
                .OrderBy(u => u.RegistadaEmUtc).ThenBy(u => u.Id)
                .Select(u => new { u.ToolId, u.RegistadaEmUtc, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => g.Select(u => (Dia: DateOnly.FromDateTime(DmoBusinessTime.ToLocal(u.RegistadaEmUtc)), u.Percentagem)).ToList(), StringComparer.Ordinal);

        var porFerramenta = relacoes.ToLookup(r => r.ToolId, r => r.JobOnId);
        foreach (var toolId in comProducao)
        {
            var seus = porFerramenta[toolId].ToHashSet();
            var l = leituras.TryGetValue(toolId, out var dela) ? dela : [];
            var inicios = todosJobOns.Where(j => seus.Contains(j.Id)).Select(j => j.InicioEm).ToList();
            var itens = jobOns.Where(j => seus.Contains(j.Id))
                .Select(j => Montar(j.Id, j.Reference, j.ProductionNumber, j.Maquina, j.InicioEm, j.FimEm, sap, l, ProximoInicio(inicios, j.InicioEm),
                    j.InicioEm is { } de ? ProximoNaMaquina(iniciosPorMaquina, j.Maquina, de) : null))
                .ToList();
            if (itens.Count > 0)
            {
                resultado[toolId] = itens;
            }
        }

        return resultado;
    }

    // A leitura «depois» de uma produção tem de ser de antes de a ferramenta
    // entrar na produção seguinte: uma leitura de hoje não é o uso no fim de
    // uma produção de há meses (Diogo, 27/10/2026: «sempre 16% uso?»).
    private static DateOnly? ProximoInicio(IEnumerable<DateOnly?> inicios, DateOnly? inicio) => inicio is { } de
        ? inicios.Where(i => i > de).Min()
        : null;

    // SAP-B01: para uma produção sem fim, o início da próxima produção na
    // mesma máquina (de qualquer referência) limita qual linha do SAP lhe
    // pode pertencer — senão engolia também as linhas das produções
    // seguintes na mesma máquina, contando-as a dobrar.
    private async Task<Dictionary<string, List<DateOnly>>> IniciosPorMaquinaAsync(IReadOnlyCollection<string> maquinasMaiusculas, CancellationToken cancellationToken)
    {
        var todos = await db.JobOns.AsNoTracking()
            .Where(j => j.InicioEm != null && maquinasMaiusculas.Contains(j.Machine.Code.ToUpper()))
            .Select(j => new { Maquina = j.Machine.Code, j.InicioEm })
            .ToListAsync(cancellationToken);
        return todos
            .GroupBy(j => j.Maquina.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Select(j => j.InicioEm!.Value).OrderBy(d => d).ToList(), StringComparer.Ordinal);
    }

    private static DateOnly? ProximoNaMaquina(Dictionary<string, List<DateOnly>> iniciosPorMaquina, string maquina, DateOnly inicio) =>
        iniciosPorMaquina.TryGetValue(maquina.ToUpperInvariant(), out var lista)
            ? lista.Where(d => d > inicio).Select(d => (DateOnly?)d).DefaultIfEmpty(null).Min()
            : null;

    private static FerramentaProducao Montar(int id, string reference, string producao, string maquina, DateOnly? inicio, DateOnly? fim,
        IReadOnlyList<SapFabrico> sap, IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras, DateOnly? proximoInicio, DateOnly? proximoInicioNaMaquina = null)
    {
        var linhas = sap.Where(s => FabricoSapExportacao.Corresponde(s.Material, s.Linha, s.InicioEm, s.FimEm, reference, maquina, inicio, fim, proximoInicioNaMaquina)).ToList();
        return new FerramentaProducao(
            id,
            reference,
            producao,
            maquina,
            inicio,
            fim,
            linhas.Any(s => s.ProducaoBruta.HasValue) ? linhas.Sum(s => s.ProducaoBruta ?? 0) : null,
            linhas.Any(s => s.ProducaoLiquida.HasValue) ? linhas.Sum(s => s.ProducaoLiquida ?? 0) : null,
            linhas.Count,
            UsoAntes(leituras, inicio),
            UsoDepois(leituras, inicio, fim, proximoInicio),
            inicio is { } de ? FabricoSapCadencia.Resumo(linhas, de, fim) : null);
    }

    // Antes: a última leitura até ao dia do início (a desse dia é o ponto de
    // partida). Depois: a primeira a partir do dia do fim; sem fim (ainda na
    // máquina), a última desde o início. Com a produção seguinte, só até ao dia
    // em que ela começa. As leituras pela ordem, com o dia em Portugal.
    public static decimal? UsoAntes(IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras, DateOnly? inicio) => inicio is { } de
        ? leituras.Where(u => u.Dia <= de).Select(u => (decimal?)u.Percentagem).LastOrDefault()
        : null;

    public static decimal? UsoDepois(IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras, DateOnly? inicio, DateOnly? fim, DateOnly? proximoInicio = null) => (inicio, fim) switch
    {
        (null, _) => null,
        (_, { } ate) => leituras.Where(u => u.Dia >= ate && (proximoInicio is not { } seguinte || u.Dia <= seguinte)).Select(u => (decimal?)u.Percentagem).FirstOrDefault(),
        ({ } de, null) => leituras.Where(u => u.Dia >= de).Select(u => (decimal?)u.Percentagem).LastOrDefault()
    };

    /// <summary>A % de uso numa produção: depois − antes (a mesma conta da ficha).</summary>
    public static decimal? UsoNaProducao(IReadOnlyList<(DateOnly Dia, decimal Percentagem)> leituras, DateOnly? inicio, DateOnly? fim) =>
        UsoAntes(leituras, inicio) is { } antes && UsoDepois(leituras, inicio, fim) is { } depois ? depois - antes : null;
}

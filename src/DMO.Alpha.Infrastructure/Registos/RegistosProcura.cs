using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Registos;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Registos;

/// <summary>
/// A procura dos Registos sobre o <see cref="DmoDbContext"/>. Só lê. O texto
/// encontra Job On (referência, produção, linha), ferramentas (referência,
/// lote) e pessoas (operador, reparador); o que uma pessoa fez liga-a aos Job
/// On e às ferramentas dos registos dela.
/// </summary>
public sealed class RegistosProcura(DmoDbContext db, IRegistosDosUtilizadores registos) : IRegistosProcura
{
    /// <summary>Quantos registos cada módulo lê quando a procura filtra depois de ler.</summary>
    public const int LimiteLargo = 2000;

    public async Task<ProcuraProducoes> ProducoesAsync(ProcuraFiltro filtro, CancellationToken cancellationToken = default)
    {
        if (!filtro.ComFiltro)
        {
            return new ProcuraProducoes([], false);
        }

        var jobOns = db.JobOns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.Linha))
        {
            var linha = filtro.Linha.Trim().ToUpperInvariant();
            jobOns = jobOns.Where(j => j.Machine.Code.ToUpper() == linha);
        }

        if (filtro.De is { } de)
        {
            jobOns = jobOns.Where(j => j.FimEm == null || j.FimEm >= de);
        }

        if (filtro.Ate is { } ate)
        {
            jobOns = jobOns.Where(j => j.InicioEm != null && j.InicioEm <= ate);
        }

        if (filtro.JobOnId is { } jobOnId)
        {
            jobOns = jobOns.Where(j => j.Id == jobOnId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.ToolId))
        {
            var daFerramenta = JobOnsDaFerramenta([filtro.ToolId]);
            jobOns = jobOns.Where(j => daFerramenta.Contains(j.Id));
        }

        // A pessoa (escolhida ou escrita): os Job On dos registos dela.
        if (filtro.OperadorId is not null || !string.IsNullOrWhiteSpace(filtro.Reparador))
        {
            var dela = (await DetalhadosAsync(filtro with { Texto = null, ToolId = null, JobOnId = null }, cancellationToken))
                .Select(r => r.JobOnId).OfType<int>().Distinct().ToList();
            jobOns = jobOns.Where(j => dela.Contains(j.Id));
        }

        if (Texto(filtro.Texto) is { } texto)
        {
            var ferramentas = FerramentasDoTexto(texto).Select(t => t.ToolId);
            var daFerramenta = db.CmContexts.Where(c => ferramentas.Contains(c.ToolId)).Select(c => (c.JobOnId ?? c.ReplacedJobOnId)!.Value)
                .Concat(db.MfContexts.Where(c => ferramentas.Contains(c.ToolId)).Select(c => (c.JobOnId ?? c.ReplacedJobOnId)!.Value))
                .Concat(db.BqContexts.Where(c => ferramentas.Contains(c.ToolId)).Select(c => (c.JobOnId ?? c.ReplacedJobOnId)!.Value));
            var dasPessoas = await JobOnsDasPessoasAsync(texto, filtro, cancellationToken);
            jobOns = jobOns.Where(j => j.Reference.ToUpper().Contains(texto) || j.ProductionNumber.ToUpper().Contains(texto)
                || j.Machine.Code.ToUpper() == texto || daFerramenta.Contains(j.Id) || dasPessoas.Contains(j.Id));
        }

        var encontrados = await jobOns
            .OrderByDescending(j => j.InicioEm.HasValue).ThenByDescending(j => j.InicioEm).ThenByDescending(j => j.Id)
            .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm, j.FimEm, BqContextId = j.BqContext != null ? (int?)j.BqContext.Id : null })
            .Take(IRegistosProcura.MaxProducoes + 1)
            .ToListAsync(cancellationToken);
        var cortada = encontrados.Count > IRegistosProcura.MaxProducoes;
        var linhas = await LinhasAsync(encontrados.Take(IRegistosProcura.MaxProducoes)
            .Select(j => new ProducaoLinha(j.Id, j.Reference, j.ProductionNumber, j.Maquina, j.InicioEm, j.FimEm, [], j.BqContextId)).ToList(), cancellationToken);
        return new ProcuraProducoes(linhas, cortada);
    }

    public async Task<PagedList<RegistoDetalhado>> RegistosAsync(ProcuraFiltro filtro, int? pagina, CancellationToken cancellationToken = default) =>
        filtro.ComFiltro
            ? PagedList<RegistoDetalhado>.FromLoaded(await DetalhadosAsync(filtro, cancellationToken), pagina)
            : PagedList<RegistoDetalhado>.Empty;

    public async Task<FerramentaResumo?> FerramentaAsync(string toolId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId) || toolId == ToolRegistryConstants.RemovedToolId)
        {
            return null;
        }

        var tool = await db.Tools.AsNoTracking().Where(t => t.ToolId == toolId)
            .Select(t => new HistoricoFerramenta(t.ToolId, t.Type, t.Reference, t.Lot)).SingleOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            return null;
        }

        tool = FerramentaHistoricoQuery.ParteDaBq(tool);
        var producoes = await JobOnsDaFerramenta([toolId]).Distinct().CountAsync(cancellationToken);
        var ultimo = await db.FerramentaUtilizacoes.AsNoTracking().Where(u => u.ToolId == toolId)
            .OrderByDescending(u => u.RegistadaEmUtc).ThenByDescending(u => u.Id)
            .Select(u => new { u.Percentagem, u.RegistadaEmUtc }).FirstOrDefaultAsync(cancellationToken);
        return new FerramentaResumo(tool.ToolId, tool.Tipo, tool.Referencia, tool.Lote, producoes, ultimo?.Percentagem, ultimo?.RegistadaEmUtc);
    }

    public async Task<ProducaoLinha?> ProducaoAsync(int jobOnId, CancellationToken cancellationToken = default)
    {
        var j = await db.JobOns.AsNoTracking().Where(j => j.Id == jobOnId)
            .Select(j => new ProducaoLinha(j.Id, j.Reference, j.ProductionNumber, j.Machine.Code, j.InicioEm, j.FimEm, new List<ProducaoFerramenta>(), j.BqContext != null ? (int?)j.BqContext.Id : null))
            .SingleOrDefaultAsync(cancellationToken);
        return j is null ? null : (await LinhasAsync([j], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<RegistoDetalhado>> UltimosAsync(string? toolId, int? jobOnId, int quantos, CancellationToken cancellationToken = default) =>
        (await DetalhadosAsync(new ProcuraFiltro(null, null, null, null, null, null, null, toolId, jobOnId), cancellationToken)).Take(quantos).ToList();

    /// <summary>Quantas sugestões de cada tipo.</summary>
    public const int SugestoesPorTipo = 5;

    public async Task<IReadOnlyList<Sugestao>> SugestoesAsync(string texto, CancellationToken cancellationToken = default)
    {
        if (Texto(texto) is not { Length: >= 2 } t)
        {
            return [];
        }

        var sugestoes = new List<Sugestao>();
        var referencias = await db.JobOns.AsNoTracking().Where(j => j.Reference.ToUpper().Contains(t))
            .GroupBy(j => j.Reference).Select(g => new { Referencia = g.Key, Producoes = g.Count() })
            .OrderBy(r => r.Referencia).Take(SugestoesPorTipo).ToListAsync(cancellationToken);
        sugestoes.AddRange(referencias.Select(r => new Sugestao(SugestaoTipo.Referencia, r.Referencia, r.Referencia, $"{r.Producoes} {(r.Producoes == 1 ? "produção" : "produções")}")));

        var jobOns = await db.JobOns.AsNoTracking().Where(j => j.ProductionNumber.ToUpper().Contains(t) || j.Reference.ToUpper().Contains(t))
            .OrderByDescending(j => j.InicioEm.HasValue).ThenByDescending(j => j.InicioEm).ThenByDescending(j => j.Id)
            .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Maquina = j.Machine.Code, j.InicioEm })
            .Take(SugestoesPorTipo).ToListAsync(cancellationToken);
        sugestoes.AddRange(jobOns.Select(j => new Sugestao(SugestaoTipo.JobOn, j.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"{j.Reference} · {j.ProductionNumber}", $"linha {j.Maquina}{(j.InicioEm is { } d ? " · " + d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : "")}")));

        var ferramentas = await FerramentasDoTexto(t).OrderBy(f => f.Type).ThenBy(f => f.Reference).ThenBy(f => f.Lot)
            .Select(f => new HistoricoFerramenta(f.ToolId, f.Type, f.Reference, f.Lot)).Take(SugestoesPorTipo).ToListAsync(cancellationToken);
        sugestoes.AddRange(ferramentas.Select(FerramentaHistoricoQuery.ParteDaBq)
            .Select(f => new Sugestao(SugestaoTipo.Ferramenta, f.ToolId, $"{f.Tipo.ToString().ToUpperInvariant()} {f.Referencia} · Lote {LoteExibido.Mostrar(f.Lote)}", null)));

        var operadores = await db.Users.AsNoTracking()
            .Where(u => u.Name.ToUpper().Contains(t) || (u.OperatorId != null && u.OperatorId.ToUpper().Contains(t)))
            .OrderBy(u => u.Name).Select(u => new { u.Id, u.Name, u.OperatorId }).Take(SugestoesPorTipo).ToListAsync(cancellationToken);
        sugestoes.AddRange(operadores.Select(u => new Sugestao(SugestaoTipo.Operador, u.Id.ToString(), u.OperatorId is null ? u.Name : $"{u.Name} ({u.OperatorId})", null)));

        sugestoes.AddRange((await ReparadoresAsync(cancellationToken)).Where(n => Contem(n, t)).Take(SugestoesPorTipo)
            .Select(n => new Sugestao(SugestaoTipo.Reparador, n, n, null)));
        return sugestoes;
    }

    public async Task<IReadOnlyList<string>> ReparadoresAsync(CancellationToken cancellationToken = default)
    {
        var armazem = await db.Reparadores.AsNoTracking().Select(r => r.Nome).ToListAsync(cancellationToken);
        var boquilhas = await db.BqRepairers.AsNoTracking().Select(r => r.Name).ToListAsync(cancellationToken);
        return armazem.Concat(boquilhas).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Os registos do filtro: o utilizador, o módulo e os dias no servidor; o resto depois de ler.</summary>
    private async Task<IReadOnlyList<RegistoDetalhado>> DetalhadosAsync(ProcuraFiltro filtro, CancellationToken cancellationToken)
    {
        var texto = Texto(filtro.Texto);
        var depois = texto is not null || filtro.ToolId is not null || filtro.JobOnId is not null
            || !string.IsNullOrWhiteSpace(filtro.Linha) || !string.IsNullOrWhiteSpace(filtro.Reparador);
        var lidos = await registos.DetalhadosAsync(new RegistosFiltro(filtro.OperadorId, filtro.Modulo, filtro.De, filtro.Ate),
            depois ? LimiteLargo : IRegistosDosUtilizadores.LimitePorModulo, cancellationToken);
        IEnumerable<RegistoDetalhado> linhas = lidos;
        if (filtro.JobOnId is { } jobOnId)
        {
            linhas = linhas.Where(r => r.JobOnId == jobOnId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.ToolId))
        {
            linhas = linhas.Where(r => r.ToolIds.Contains(filtro.ToolId, StringComparer.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Linha))
        {
            linhas = linhas.Where(r => string.Equals(r.Linha, filtro.Linha.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Reparador))
        {
            linhas = linhas.Where(r => Contem(r.Reparador, filtro.Reparador.Trim()));
        }

        if (texto is not null)
        {
            var ferramentas = (await FerramentasDoTexto(texto).Select(t => t.ToolId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            var jobOns = (await db.JobOns.AsNoTracking()
                    .Where(j => j.Reference.ToUpper().Contains(texto) || j.ProductionNumber.ToUpper().Contains(texto) || j.Machine.Code.ToUpper() == texto)
                    .Select(j => j.Id).ToListAsync(cancellationToken)).ToHashSet();
            linhas = linhas.Where(r => (r.JobOnId is { } id && jobOns.Contains(id))
                || r.ToolIds.Any(ferramentas.Contains)
                || Contem(r.Utilizador, texto) || Contem(r.Reparador, texto) || Contem(r.Detalhe, texto) || Contem(r.Acao, texto));
        }

        return linhas.ToList();
    }

    /// <summary>Os Job On de quem tem o texto no nome ou no nº (operador) ou no nome de reparador.</summary>
    private async Task<List<int>> JobOnsDasPessoasAsync(string texto, ProcuraFiltro filtro, CancellationToken cancellationToken)
    {
        var operadores = await db.Users.AsNoTracking()
            .Where(u => u.Name.ToUpper().Contains(texto) || (u.OperatorId != null && u.OperatorId.ToUpper() == texto))
            .Select(u => u.Id).Take(20).ToListAsync(cancellationToken);
        var reparadores = (await ReparadoresAsync(cancellationToken)).Where(n => Contem(n, texto)).ToList();
        if (operadores.Count == 0 && reparadores.Count == 0)
        {
            return [];
        }

        var todos = await registos.DetalhadosAsync(new RegistosFiltro(null, filtro.Modulo, filtro.De, filtro.Ate), LimiteLargo, cancellationToken);
        return todos
            .Where(r => (r.UserId is { } u && operadores.Contains(u)) || reparadores.Any(n => Contem(r.Reparador, n)))
            .Select(r => r.JobOnId).OfType<int>().Distinct().ToList();
    }

    private IQueryable<int> JobOnsDaFerramenta(IReadOnlyList<string> toolIds) =>
        db.CmContexts.Where(c => toolIds.Contains(c.ToolId) && (c.JobOnId ?? c.ReplacedJobOnId) != null).Select(c => (c.JobOnId ?? c.ReplacedJobOnId)!.Value)
            .Concat(db.MfContexts.Where(c => toolIds.Contains(c.ToolId) && (c.JobOnId ?? c.ReplacedJobOnId) != null).Select(c => (c.JobOnId ?? c.ReplacedJobOnId)!.Value))
            .Concat(db.BqContexts.Where(c => toolIds.Contains(c.ToolId) && (c.JobOnId ?? c.ReplacedJobOnId) != null).Select(c => (c.JobOnId ?? c.ReplacedJobOnId)!.Value));

    /// <summary>
    /// As ferramentas do texto: «5447», «CM 5447», «5447 · Lote 3» ou «5447 3».
    /// A referência pode ser parte; o lote, quando vem, é exato.
    /// </summary>
    private IQueryable<Tool> FerramentasDoTexto(string texto)
    {
        var partes = texto.Replace("·", " ").Replace("LOTE", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        ToolType? tipo = partes.FirstOrDefault() switch { "CM" => ToolType.Cm, "MF" => ToolType.Mf, "BQ" => ToolType.Bq, _ => null };
        if (tipo is not null)
        {
            partes.RemoveAt(0);
        }

        var ferramentas = db.Tools.AsNoTracking().Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId);
        if (partes.Count == 0)
        {
            return ferramentas.Where(_ => false);
        }

        var referencia = partes[0];
        ferramentas = ferramentas.Where(t => t.Reference.ToUpper().Contains(referencia));
        if (tipo is { } t1)
        {
            ferramentas = ferramentas.Where(t => t.Type == t1);
        }

        if (partes.Count > 1)
        {
            var lote = partes[1];
            ferramentas = ferramentas.Where(t => t.Lot.ToUpper() == lote);
        }

        return ferramentas;
    }

    /// <summary>As ferramentas de cada Job On, com a % de uso pela mesma conta da ficha.</summary>
    private async Task<List<ProducaoLinha>> LinhasAsync(IReadOnlyList<ProducaoLinha> jobOns, CancellationToken cancellationToken)
    {
        if (jobOns.Count == 0)
        {
            return [];
        }

        var ids = jobOns.Select(j => (int?)j.JobOnId).ToList();
        var contextos = (await db.CmContexts.AsNoTracking().Where(c => ids.Contains(c.JobOnId ?? c.ReplacedJobOnId))
                .Select(c => new Contexto((c.JobOnId ?? c.ReplacedJobOnId)!.Value, c.ToolId, c.JobOnId != null, c.Id, ToolType.Cm)).ToListAsync(cancellationToken))
            .Concat(await db.MfContexts.AsNoTracking().Where(c => ids.Contains(c.JobOnId ?? c.ReplacedJobOnId))
                .Select(c => new Contexto((c.JobOnId ?? c.ReplacedJobOnId)!.Value, c.ToolId, c.JobOnId != null, c.Id, ToolType.Mf)).ToListAsync(cancellationToken))
            .Concat(await db.BqContexts.AsNoTracking().Where(c => ids.Contains(c.JobOnId ?? c.ReplacedJobOnId))
                .Select(c => new Contexto((c.JobOnId ?? c.ReplacedJobOnId)!.Value, c.ToolId, c.JobOnId != null, c.Id, ToolType.Bq)).ToListAsync(cancellationToken))
            .ToList();
        var toolIds = contextos.Select(c => c.ToolId).Distinct().ToList();
        var nomes = await db.Tools.AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new HistoricoFerramenta(t.ToolId, t.Type, t.Reference, t.Lot))
            .ToDictionaryAsync(t => t.ToolId, FerramentaHistoricoQuery.ParteDaBq, StringComparer.Ordinal, cancellationToken);
        var leituras = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => toolIds.Contains(u.ToolId))
                .OrderBy(u => u.RegistadaEmUtc).ThenBy(u => u.Id)
                .Select(u => new { u.ToolId, u.RegistadaEmUtc, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<(DateOnly Dia, decimal Percentagem)>)g
                .Select(u => (DateOnly.FromDateTime(DmoBusinessTime.ToLocal(u.RegistadaEmUtc)), u.Percentagem)).ToList(), StringComparer.Ordinal);

        return jobOns.Select(j => j with
        {
            Ferramentas = contextos
                .Where(c => c.JobOnId == j.JobOnId && nomes.ContainsKey(c.ToolId))
                .OrderBy(c => c.Tipo).ThenByDescending(c => c.Atual).ThenBy(c => c.Id)
                .DistinctBy(c => c.ToolId)
                .Select(c => new ProducaoFerramenta(c.ToolId, c.Tipo, nomes[c.ToolId].Referencia, nomes[c.ToolId].Lote,
                    leituras.TryGetValue(c.ToolId, out var l) ? FerramentasFabricoSap.UsoNaProducao(l, j.InicioEm, j.FimEm) : null, !c.Atual))
                .ToList()
        }).ToList();
    }

    private sealed record Contexto(int JobOnId, string ToolId, bool Atual, int Id, ToolType Tipo);

    private static string? Texto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim().ToUpperInvariant();

    private static bool Contem(string? onde, string texto) =>
        onde is not null && onde.Contains(texto, StringComparison.CurrentCultureIgnoreCase);
}

using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// Lê os rastos que cada módulo já grava (quem e quando) e junta-os numa só
/// lista, os mais recentes primeiro. Cada módulo é uma consulta curta, com o
/// filtro no servidor e no máximo <see cref="IRegistosDosUtilizadores.LimitePorModulo"/>
/// linhas; a página faz-se depois de juntar.
/// </summary>
public sealed class RegistosDosUtilizadores(DmoDbContext db) : IRegistosDosUtilizadores
{
    private const int Limite = IRegistosDosUtilizadores.LimitePorModulo;

    public async Task<PagedList<RegistoDeUtilizador>> ListarAsync(RegistosFiltro filtro, int? pagina, CancellationToken cancellationToken = default)
    {
        var todas = await DetalhadosAsync(filtro, Limite, cancellationToken, incluirCorrecoes: false);
        return PagedList<RegistoDeUtilizador>.FromLoaded(todas.Select(r => r.Simples()).ToList(), pagina);
    }

    public Task<IReadOnlyList<RegistoDetalhado>> DetalhadosAsync(RegistosFiltro filtro, int limitePorModulo, CancellationToken cancellationToken = default) =>
        DetalhadosAsync(filtro, limitePorModulo, cancellationToken, incluirCorrecoes: true);

    private async Task<IReadOnlyList<RegistoDetalhado>> DetalhadosAsync(RegistosFiltro filtro, int limite, CancellationToken cancellationToken, bool incluirCorrecoes)
    {
        var desde = filtro.De is { } de ? DmoBusinessTime.ToUtc(de.ToDateTime(TimeOnly.MinValue)) : DateTime.MinValue;
        var ate = filtro.Ate is { } a ? DmoBusinessTime.ToUtc(a.AddDays(1).ToDateTime(TimeOnly.MinValue)) : DateTime.MaxValue;
        var user = filtro.UserId;
        bool Inclui(RegistoModulo modulo) => filtro.Modulo is null || filtro.Modulo == modulo;

        var linhas = new List<Fonte>();

        if (Inclui(RegistoModulo.JobOn))
        {
            var mudancas = await db.JobOnChanges.AsNoTracking()
                .Where(c => (user == null ? c.ChangedByUserId != null : c.ChangedByUserId == user) && c.ChangedAtUtc >= desde && c.ChangedAtUtc < ate)
                .OrderByDescending(c => c.ChangedAtUtc).Take(limite)
                .Select(c => new { c.ChangedAtUtc, c.Field, c.OldValueFrozen, c.NewValueFrozen, c.ChangedByUserId, c.JobOnId, c.JobOn!.Reference, c.JobOn.ProductionNumber, Maquina = c.JobOn.Machine.Code })
                .ToListAsync(cancellationToken);
            linhas.AddRange(mudancas.Select(c => new Fonte(c.ChangedAtUtc, RegistoModulo.JobOn, incluirCorrecoes ? $"Alterou · {c.Field}" : "Alterou",
                $"{c.Reference} · {c.ProductionNumber} · {c.Field}: {c.OldValueFrozen ?? "—"} → {c.NewValueFrozen ?? "—"}", c.ChangedByUserId)
            {
                JobOnId = c.JobOnId, Linha = c.Maquina, Sobre = $"{c.Reference} · {c.ProductionNumber}", Antes = c.OldValueFrozen ?? "—", Depois = c.NewValueFrozen ?? "—"
            }));
            var removidas = await db.JobOnChanges.AsNoTracking()
                .Where(c => c.RemovedAtUtc != null && (user == null ? c.RemovedByUserId != null : c.RemovedByUserId == user) && c.RemovedAtUtc >= desde && c.RemovedAtUtc < ate)
                .OrderByDescending(c => c.RemovedAtUtc).Take(limite)
                .Select(c => new { c.RemovedAtUtc, c.Field, c.RemovedByUserId, c.JobOnId, c.JobOn!.Reference, c.JobOn.ProductionNumber, Maquina = c.JobOn.Machine.Code })
                .ToListAsync(cancellationToken);
            linhas.AddRange(removidas.Select(c => new Fonte(c.RemovedAtUtc!.Value, RegistoModulo.JobOn, "Removeu uma alteração",
                $"{c.Reference} · {c.ProductionNumber} · {c.Field}", c.RemovedByUserId) { JobOnId = c.JobOnId, Linha = c.Maquina, Sobre = $"{c.Reference} · {c.ProductionNumber}" }));
        }

        if (Inclui(RegistoModulo.ReparacaoInterna))
        {
            var registos = await (
                    from r in db.ReparacoesInternas.AsNoTracking()
                    join m in db.Machines.AsNoTracking() on r.MachineId equals m.Id
                    where (user == null ? r.RegistadoPorUserId != null : r.RegistadoPorUserId == user) && r.RegistadoEmUtc >= desde && r.RegistadoEmUtc < ate
                    orderby r.RegistadoEmUtc descending
                    select new
                    {
                        r.RegistadoEmUtc, r.Tipo, r.Numero, r.RegistadoPorUserId, Linha = m.Code, r.JobOnId, r.AssociadaToolId,
                        CmTool = db.CmContexts.Where(c => c.Id == r.CmContextId).Select(c => c.ToolId).FirstOrDefault(),
                        MfTool = db.MfContexts.Where(c => c.Id == r.MfContextId).Select(c => c.ToolId).FirstOrDefault()
                    })
                .Take(limite).ToListAsync(cancellationToken);
            linhas.AddRange(registos.Select(r => new Fonte(r.RegistadoEmUtc, RegistoModulo.ReparacaoInterna, "Registou",
                $"{(r.Tipo == ReparacaoTipo.Cm ? "CM" : "MF")} nº {r.Numero} · linha {r.Linha}", r.RegistadoPorUserId)
            {
                JobOnId = r.JobOnId, Linha = r.Linha, Ferramentas = Ids(r.AssociadaToolId, r.CmTool, r.MfTool)
            }));
            var rasto = await (
                    from t in db.ReparacoesInternasRasto.AsNoTracking()
                    join r in db.ReparacoesInternas.AsNoTracking() on t.ReparacaoId equals r.Id
                    join m in db.Machines.AsNoTracking() on r.MachineId equals m.Id
                    where (user == null ? t.PorUserId != null : t.PorUserId == user) && t.EmUtc >= desde && t.EmUtc < ate
                    orderby t.EmUtc descending
                    select new { t.EmUtc, t.Acao, t.NumeroAntes, t.NumeroDepois, t.PorUserId, r.JobOnId, Linha = m.Code, r.AssociadaToolId, t.ToolIdAssociado })
                .Take(limite).ToListAsync(cancellationToken);
            linhas.AddRange(rasto.Select(t => new Fonte(t.EmUtc, RegistoModulo.ReparacaoInterna,
                t.Acao switch { ReparacaoAcao.Corrigido => "Corrigiu", ReparacaoAcao.Anulado => "Anulou", _ => "Associou" },
                t.Acao == ReparacaoAcao.Corrigido ? $"nº {t.NumeroAntes} → {t.NumeroDepois}" : $"nº {t.NumeroAntes}", t.PorUserId)
            {
                JobOnId = t.JobOnId, Linha = t.Linha, Ferramentas = Ids(t.AssociadaToolId, t.ToolIdAssociado),
                Antes = t.Acao == ReparacaoAcao.Corrigido ? t.NumeroAntes : null, Depois = t.Acao == ReparacaoAcao.Corrigido ? t.NumeroDepois : null
            }));
        }

        if (Inclui(RegistoModulo.Armazem))
        {
            var movimentos = await (
                    from m in db.ArmazemMovimentos.AsNoTracking()
                    join t in db.Tools.AsNoTracking() on m.ToolId equals t.ToolId into ts
                    from t in ts.DefaultIfEmpty()
                    join q in db.Machines.AsNoTracking() on m.MachineId equals q.Id into qs
                    from q in qs.DefaultIfEmpty()
                    where (user == null ? m.RegistadoPorUserId != null : m.RegistadoPorUserId == user) && m.RegistadoEmUtc >= desde && m.RegistadoEmUtc < ate
                    orderby m.RegistadoEmUtc descending
                    select new
                    {
                        m.RegistadoEmUtc, m.Tipo, m.RegistadoPorUserId, m.ToolId, m.JobOnId, Linha = q != null ? q.Code : null, Reparador = m.ReparadorNome ?? m.EntradaReparadorNome,
                        Type = t != null ? (ToolType?)t.Type : null, Reference = m.ToolReferenceFrozen ?? (t != null ? t.Reference : null), Lot = m.ToolLotFrozen ?? (t != null ? t.Lot : null)
                    })
                .Take(limite).ToListAsync(cancellationToken);
            linhas.AddRange(movimentos.Select(m => new Fonte(m.RegistadoEmUtc, RegistoModulo.Armazem,
                m.Tipo switch { ArmazemMovimentoTipo.Entrada => "Entrada", ArmazemMovimentoTipo.Saida => "Saída", _ => "Corrigiu a localização" },
                Ferramenta(m.Type, m.Reference, m.Lot), m.RegistadoPorUserId)
            {
                JobOnId = m.JobOnId, Linha = m.Linha, Reparador = m.Reparador, Ferramentas = Ids(m.ToolId)
            }));
        }

        if (Inclui(RegistoModulo.Boquilhas))
        {
            var movimentos = await db.BqMovements.AsNoTracking()
                .Where(m => (user == null ? m.RegisteredByUserId != null : m.RegisteredByUserId == user) && m.CreatedAtUtc >= desde && m.CreatedAtUtc < ate)
                .OrderByDescending(m => m.CreatedAtUtc).Take(limite)
                .Select(m => new
                {
                    m.CreatedAtUtc, m.Type, m.Quantity, m.MachineCodeFrozen, m.RegisteredByUserId, m.BqRepairerNameFrozen, m.Trace.ToolId,
                    JobOnId = m.Trace.BqContext != null ? m.Trace.BqContext.JobOnId ?? m.Trace.BqContext.ReplacedJobOnId : null,
                    Reference = m.Trace.ToolReferenceFrozen ?? (m.Trace.Tool != null ? m.Trace.Tool.Reference : null), Lot = m.Trace.ToolLotFrozen ?? (m.Trace.Tool != null ? m.Trace.Tool.Lot : null)
                })
                .ToListAsync(cancellationToken);
            linhas.AddRange(movimentos.Select(m => new Fonte(m.CreatedAtUtc, RegistoModulo.Boquilhas,
                m.Type switch { BqMovementType.Saida => "Saída", BqMovementType.Entrada => "Entrada", _ => "Entrada sem reparação" },
                $"{Ferramenta(ToolType.Bq, m.Reference, m.Lot)} · {m.Quantity}{(m.MachineCodeFrozen is { Length: > 0 } linha ? " · linha " + linha : "")}", m.RegisteredByUserId)
            {
                JobOnId = m.JobOnId, Linha = m.MachineCodeFrozen, Reparador = m.BqRepairerNameFrozen, Ferramentas = Ids(m.ToolId)
            }));
        }

        if (Inclui(RegistoModulo.Controlo))
        {
            var pesos = await db.Pesos.AsNoTracking()
                .Where(p => p.RegistadoEmUtc != null && (user == null ? p.RegistadoPorUserId != null : p.RegistadoPorUserId == user) && p.RegistadoEmUtc >= desde && p.RegistadoEmUtc < ate)
                .OrderByDescending(p => p.RegistadoEmUtc).Take(limite)
                .Select(p => new
                {
                    p.RegistadoEmUtc, p.RegistadoPorUserId, p.CmToolId, p.BqToolId, JobOnId = p.Controlo != null ? (int?)p.Controlo.JobOnId : null, Linha = p.Controlo != null ? p.Controlo.JobOn.Machine.Code : null,
                    Cm = p.CmToolReferenceFrozen ?? p.CmTool.Reference, Bq = p.BqToolReferenceFrozen ?? p.BqTool.Reference
                })
                .ToListAsync(cancellationToken);
            linhas.AddRange(pesos.Select(p => new Fonte(p.RegistadoEmUtc!.Value, RegistoModulo.Controlo, "Registou o Peso", $"CM {p.Cm} · BQ {p.Bq}", p.RegistadoPorUserId)
            {
                JobOnId = p.JobOnId, Linha = p.Linha, Ferramentas = Ids(p.CmToolId, p.BqToolId)
            }));
            var pegamentos = await db.Pegamentos.AsNoTracking()
                .Where(p => p.RegistadoEmUtc != null && (user == null ? p.RegistadoPorUserId != null : p.RegistadoPorUserId == user) && p.RegistadoEmUtc >= desde && p.RegistadoEmUtc < ate)
                .OrderByDescending(p => p.RegistadoEmUtc).Take(limite)
                .Select(p => new
                {
                    p.RegistadoEmUtc, p.RegistadoPorUserId, p.CmToolId, p.BqToolId, p.MfToolId, JobOnId = p.Controlo != null ? (int?)p.Controlo.JobOnId : null, Linha = p.Controlo != null ? p.Controlo.JobOn.Machine.Code : null,
                    Cm = p.CmToolReferenceFrozen ?? (p.CmTool != null ? p.CmTool.Reference : null)
                })
                .ToListAsync(cancellationToken);
            linhas.AddRange(pegamentos.Select(p => new Fonte(p.RegistadoEmUtc!.Value, RegistoModulo.Controlo, "Registou os Pegamentos", p.Cm is null ? "—" : $"CM {p.Cm}", p.RegistadoPorUserId)
            {
                JobOnId = p.JobOnId, Linha = p.Linha, Ferramentas = Ids(p.CmToolId, p.BqToolId, p.MfToolId)
            }));
            var decisoes = await (
                    from d in db.ControloDecisoes.AsNoTracking()
                    join c in db.Controlos.AsNoTracking() on d.ControloId equals c.Id
                    where (user == null ? d.DecididoPorUserId != null : d.DecididoPorUserId == user) && d.EmUtc >= desde && d.EmUtc < ate
                    orderby d.EmUtc descending
                    select new { d.EmUtc, d.Parte, d.Aprovado, d.DecididoPorUserId, c.JobOnId, c.JobOn.Reference, c.JobOn.ProductionNumber, Linha = c.JobOn.Machine.Code })
                .Take(limite).ToListAsync(cancellationToken);
            linhas.AddRange(decisoes.Select(d => new Fonte(d.EmUtc, RegistoModulo.Controlo, d.Aprovado ? "Aprovou" : "Não aprovou",
                $"{d.Parte} · {d.Reference} · {d.ProductionNumber}", d.DecididoPorUserId) { JobOnId = d.JobOnId, Linha = d.Linha }));

            if (incluirCorrecoes)
            {
                await CorrecoesAsync(linhas, user, desde, ate, limite, cancellationToken);
            }
        }

        if (Inclui(RegistoModulo.Ferramentas))
        {
            var usos = await (
                    from u in db.FerramentaUtilizacoes.AsNoTracking()
                    join t in db.Tools.AsNoTracking() on u.ToolId equals t.ToolId
                    where (user == null ? u.RegistadoPorUserId != null : u.RegistadoPorUserId == user) && u.RegistadaEmUtc >= desde && u.RegistadaEmUtc < ate
                    orderby u.RegistadaEmUtc descending
                    select new { u.RegistadaEmUtc, u.Percentagem, u.RegistadoPorUserId, t.ToolId, t.Type, t.Reference, t.Lot })
                .Take(limite).ToListAsync(cancellationToken);
            linhas.AddRange(usos.Select(u => new Fonte(u.RegistadaEmUtc, RegistoModulo.Ferramentas, "Registou o % de uso",
                $"{Ferramenta(u.Type, u.Reference, u.Lot)} · {Percentagem(u.Percentagem)} %", u.RegistadoPorUserId)
            {
                Ferramentas = Ids(u.ToolId), Depois = $"{Percentagem(u.Percentagem)} %"
            }));
        }

        var ids = linhas.Select(l => l.UserId).OfType<Guid>().Distinct().ToList();
        var nomes = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.OperatorId })
            .ToDictionaryAsync(u => u.Id, u => $"{u.Name} ({u.OperatorId})", cancellationToken);

        return linhas
            .OrderByDescending(l => l.EmUtc)
            .Select(l => new RegistoDetalhado(l.EmUtc, l.Modulo, l.Acao, l.Detalhe, l.UserId,
                l.UserId is { } id && nomes.TryGetValue(id, out var nome) ? nome : null,
                l.JobOnId, l.Ferramentas, l.Linha, l.Reparador,
                l.Antes, l.Depois, l.Sobre))
            .ToList();
    }

    /// <summary>As correções do Peso e dos Pegamentos e o OK/NOK das áreas (só leitura dos rastos).</summary>
    private async Task CorrecoesAsync(List<Fonte> linhas, Guid? user, DateTime desde, DateTime ate, int limite, CancellationToken cancellationToken)
    {
        var pesos = await (
                from h in db.PesoMeasurementHistorico.AsNoTracking()
                join p in db.Pesos.AsNoTracking() on h.PesoId equals p.Id
                where (user == null ? h.PorUserId != null : h.PorUserId == user) && h.EmUtc >= desde && h.EmUtc < ate
                orderby h.EmUtc descending
                select new
                {
                    h.EmUtc, h.PorUserId, h.Numero, h.Apagada, h.WaterWeightAntesGrams, h.WaterWeightDepoisGrams, p.CmToolId, p.BqToolId,
                    JobOnId = p.Controlo != null ? (int?)p.Controlo.JobOnId : null, Linha = p.Controlo != null ? p.Controlo.JobOn.Machine.Code : null,
                    Cm = p.CmToolReferenceFrozen ?? p.CmTool.Reference
                })
            .Take(limite).ToListAsync(cancellationToken);
        linhas.AddRange(pesos.Select(h => new Fonte(h.EmUtc, RegistoModulo.Controlo,
            h.Apagada ? $"Apagou a leitura nº {h.Numero} do Peso" : $"Corrigiu a leitura nº {h.Numero} do Peso", $"CM {h.Cm}", h.PorUserId)
        {
            JobOnId = h.JobOnId, Linha = h.Linha, Ferramentas = Ids(h.CmToolId, h.BqToolId),
            Antes = $"{Gramas(h.WaterWeightAntesGrams)} g", Depois = h.WaterWeightDepoisGrams is { } g ? $"{Gramas(g)} g" : "—"
        }));

        var pegamentos = await (
                from h in db.PegamentosMeasurementHistorico.AsNoTracking()
                join p in db.Pegamentos.AsNoTracking() on h.PegamentosId equals p.Id
                where (user == null ? h.PorUserId != null : h.PorUserId == user) && h.EmUtc >= desde && h.EmUtc < ate
                orderby h.EmUtc descending
                select new
                {
                    h.EmUtc, h.PorUserId, h.Numero, h.Apagada, h.CosturaAntesMm, h.ContraCosturaAntesMm, h.CosturaDepoisMm, h.ContraCosturaDepoisMm,
                    p.CmToolId, p.BqToolId, p.MfToolId,
                    JobOnId = p.Controlo != null ? (int?)p.Controlo.JobOnId : null, Linha = p.Controlo != null ? p.Controlo.JobOn.Machine.Code : null,
                    Cm = p.CmToolReferenceFrozen ?? (p.CmTool != null ? p.CmTool.Reference : null)
                })
            .Take(limite).ToListAsync(cancellationToken);
        linhas.AddRange(pegamentos.Select(h => new Fonte(h.EmUtc, RegistoModulo.Controlo,
            h.Apagada ? $"Apagou a leitura nº {h.Numero} dos Pegamentos" : $"Corrigiu a leitura nº {h.Numero} dos Pegamentos", h.Cm is null ? "—" : $"CM {h.Cm}", h.PorUserId)
        {
            JobOnId = h.JobOnId, Linha = h.Linha, Ferramentas = Ids(h.CmToolId, h.BqToolId, h.MfToolId),
            Antes = Milimetros(h.CosturaAntesMm, h.ContraCosturaAntesMm), Depois = h.CosturaDepoisMm is { } c ? Milimetros(c, h.ContraCosturaDepoisMm) : "—"
        }));

        var areas = await (
                from h in db.ControloAreaStateHistorico.AsNoTracking()
                join c in db.Controlos.AsNoTracking() on h.ControloId equals c.Id
                where (user == null ? h.PorUserId != null : h.PorUserId == user) && h.EmUtc >= desde && h.EmUtc < ate
                orderby h.EmUtc descending
                select new { h.EmUtc, h.PorUserId, h.Area, h.Status, c.JobOnId, c.JobOn.Reference, c.JobOn.ProductionNumber, Linha = c.JobOn.Machine.Code })
            .Take(limite).ToListAsync(cancellationToken);
        linhas.AddRange(areas.Select(h => new Fonte(h.EmUtc, RegistoModulo.Controlo, $"OK/NOK da área {h.Area}", $"{h.Reference} · {h.ProductionNumber}", h.PorUserId)
        {
            JobOnId = h.JobOnId, Linha = h.Linha, Depois = h.Status ?? "—"
        }));
    }

    private sealed record Fonte(DateTime EmUtc, RegistoModulo Modulo, string Acao, string Detalhe, Guid? UserId)
    {
        public int? JobOnId { get; init; }

        public IReadOnlyList<string> Ferramentas { get; init; } = [];

        public string? Linha { get; init; }

        public string? Reparador { get; init; }

        public string? Sobre { get; init; }

        public string? Antes { get; init; }

        public string? Depois { get; init; }
    }

    private static IReadOnlyList<string> Ids(params string?[] ids) =>
        ids.Where(i => !string.IsNullOrWhiteSpace(i) && i != ToolRegistryConstants.RemovedToolId).Select(i => i!).Distinct(StringComparer.Ordinal).ToList();

    private static string Gramas(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Milimetros(decimal costura, decimal? contra) =>
        contra is { } c ? $"{Gramas(costura)} / {Gramas(c)} mm" : $"{Gramas(costura)} mm";

    private static string Ferramenta(ToolType? tipo, string? referencia, string? lote) =>
        referencia is null
            ? "—"
            : $"{(tipo is { } t ? t.ToString().ToUpperInvariant() + " " : "")}{referencia}{(string.IsNullOrWhiteSpace(lote) ? "" : " · Lote " + LoteExibido.Mostrar(lote))}";

    // Números: inteiros sem casas, os outros com 2 casas e ponto.
    private static string Percentagem(decimal valor) =>
        valor.ToString(valor % 1 == 0 ? "0" : "0.00", CultureInfo.InvariantCulture);
}

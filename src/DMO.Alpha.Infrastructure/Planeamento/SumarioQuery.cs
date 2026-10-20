using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Planeamento;

/// <summary>
/// Implementação de <see cref="ISumarioQuery"/>: uma consulta por tipo pedido,
/// cada uma limitada ao período. As regras vêm dos sítios onde já vivem: o
/// estado da aprovação (<see cref="ControloAprovacao"/> e a Revisão), o
/// texto das alterações (<see cref="JobOnChangeFields.Describe"/>) e o
/// registo de visão do Resumo (<see cref="ControloViewing"/>).
/// </summary>
public sealed class SumarioQuery(DmoDbContext db, IControloRevisaoQuery revisao) : ISumarioQuery
{
    /// <summary>O máximo de linhas por tipo num período (o calendário pede um mês).</summary>
    private const int MaxPorTipo = 500;

    public async Task<IReadOnlyList<SumarioItem>> PeriodAsync(
        DateOnly from,
        DateOnly toExclusive,
        IReadOnlyCollection<SumarioTipo> tipos,
        CancellationToken cancellationToken = default)
    {
        var fromUtc = Utc(from);
        var toUtc = Utc(toExclusive);
        var items = new List<SumarioItem>();

        if (tipos.Contains(SumarioTipo.ProducaoComeca))
        {
            items.AddRange(await ComecamAsync(from, toExclusive, cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.JobOnCriado) || tipos.Contains(SumarioTipo.JobOnPorVer))
        {
            items.AddRange(await CriadosAsync(from, toExclusive, fromUtc, toUtc, tipos, cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.JobOnAlterado))
        {
            items.AddRange(await AlteracoesAsync(fromUtc, toUtc, cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.ControloPorAprovar))
        {
            var pendentes = await revisao.PeriodAsync(from, toExclusive, null, ControloRevisaoEstado.Pendente, null, MaxPorTipo, cancellationToken);
            items.AddRange(pendentes.Select(p => new SumarioItem(
                SumarioTipo.ControloPorAprovar,
                Dia(p.RegistadoEmUtc!.Value),
                p.RegistadoEmUtc,
                Producao(p.Reference, p.ProductionNumber),
                null,
                p.RegistadoPor) { JobOnId = p.JobOnId, Maquina = p.Machine }));
        }

        if (tipos.Contains(SumarioTipo.ControloNaoAprovado))
        {
            items.AddRange(await NaoAprovadosAsync(fromUtc, toUtc, cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.BqMovimentoApagado))
        {
            items.AddRange(await BqApagadosAsync(fromUtc, toUtc, cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.FerramentaDadosEmFalta))
        {
            items.AddRange(await DadosEmFaltaAsync(from, toExclusive, cancellationToken));
        }

        return items
            .OrderByDescending(i => i.Dia)
            .ThenBy(i => i.Tipo)
            .ThenByDescending(i => i.QuandoUtc)
            .ToList();
    }

    /// <summary>As produções que começam no período.</summary>
    private async Task<IEnumerable<SumarioItem>> ComecamAsync(DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken)
    {
        var rows = await db.JobOns.AsNoTracking()
            .Where(j => j.InicioEm >= from && j.InicioEm < toExclusive)
            .OrderBy(j => j.InicioEm)
            .Take(MaxPorTipo)
            .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Machine = j.Machine.Code, Inicio = j.InicioEm!.Value })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SumarioItem(
            SumarioTipo.ProducaoComeca, r.Inicio, null, Producao(r.Reference, r.ProductionNumber), null, null) { JobOnId = r.Id, Maquina = r.Machine });
    }

    /// <summary>
    /// Os Job Ons gravados no período (Diogo, 19/10/2026: «um jobon deve ser
    /// anunciado quando criado e não apenas no dia») e, para o Controlo, os
    /// que ainda ninguém abriu no Resumo (sem controlo ou sem registo de
    /// visão). Um Job On gravado antes de a app guardar a data de criação
    /// conta pelo dia de início, só nos por ver.
    /// </summary>
    private async Task<IEnumerable<SumarioItem>> CriadosAsync(
        DateOnly from,
        DateOnly toExclusive,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyCollection<SumarioTipo> tipos,
        CancellationToken cancellationToken)
    {
        var rows = await db.JobOns.AsNoTracking()
            .Where(j => (j.CriadoEmUtc >= fromUtc && j.CriadoEmUtc < toUtc)
                || (j.CriadoEmUtc == null && j.InicioEm >= from && j.InicioEm < toExclusive))
            .OrderByDescending(j => j.CriadoEmUtc)
            .Take(MaxPorTipo)
            .Select(j => new
            {
                j.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = j.Machine.Code,
                j.InicioEm,
                j.CriadoEmUtc,
                Visto = db.ControloViewings.Any(v => v.Controlo!.JobOnId == j.Id)
            })
            .ToListAsync(cancellationToken);

        var items = new List<SumarioItem>();
        foreach (var r in rows)
        {
            var titulo = Producao(r.Reference, r.ProductionNumber);
            var dia = r.CriadoEmUtc is { } criado ? Dia(criado) : r.InicioEm!.Value;
            if (tipos.Contains(SumarioTipo.JobOnCriado) && r.CriadoEmUtc is not null)
            {
                var detalhe = r.InicioEm is { } inicio ? $"Começa a {inicio:dd/MM/yyyy}" : "Sem data de início";
                items.Add(new SumarioItem(SumarioTipo.JobOnCriado, dia, r.CriadoEmUtc, titulo, detalhe, null) { JobOnId = r.Id, Maquina = r.Machine });
            }

            if (tipos.Contains(SumarioTipo.JobOnPorVer) && !r.Visto)
            {
                items.Add(new SumarioItem(SumarioTipo.JobOnPorVer, dia, r.CriadoEmUtc, titulo, "Ainda ninguém abriu o Resumo", null) { JobOnId = r.Id, Maquina = r.Machine });
            }
        }

        return items;
    }

    /// <summary>
    /// As alterações a Job Ons feitas no período, também antes de a produção
    /// começar (Diogo, 19/10/2026: «se existir alterações mostrar também»).
    /// </summary>
    private async Task<IEnumerable<SumarioItem>> AlteracoesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = await db.JobOnChanges.AsNoTracking()
            .Where(c => c.ChangedAtUtc >= fromUtc && c.ChangedAtUtc < toUtc)
            .OrderByDescending(c => c.ChangedAtUtc)
            .Take(MaxPorTipo)
            .Select(c => new
            {
                c.JobOnId,
                c.Field,
                c.OldValueFrozen,
                c.NewValueFrozen,
                c.ChangedByNameFrozen,
                c.ChangedAtUtc,
                c.JobOn!.Reference,
                c.JobOn.ProductionNumber,
                Machine = c.JobOn.Machine.Code
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(c => new SumarioItem(
                SumarioTipo.JobOnAlterado,
                Dia(c.ChangedAtUtc),
                c.ChangedAtUtc,
                Producao(c.Reference, c.ProductionNumber),
                JobOnChangeFields.Describe(c.Field, c.OldValueFrozen, c.NewValueFrozen),
                c.ChangedByNameFrozen) { JobOnId = c.JobOnId, Maquina = c.Machine });
    }

    /// <summary>Os controlos não aprovados no período (pelo dia da decisão).</summary>
    private async Task<IEnumerable<SumarioItem>> NaoAprovadosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = await (
                from e in ControloAprovacao.Estados(db)
                where e.NotApprovedAtUtc >= fromUtc && e.NotApprovedAtUtc < toUtc
                join j in db.JobOns.AsNoTracking() on e.JobOnId equals j.Id
                join m in db.Machines.AsNoTracking() on j.MachineId equals m.Id
                select new
                {
                    e.JobOnId,
                    j.Reference,
                    j.ProductionNumber,
                    Machine = m.Code,
                    Em = e.NotApprovedAtUtc!.Value,
                    e.ReviewNote,
                    Por = db.Users.Where(u => u.OperatorId == e.NotApprovedByOperatorId).Select(u => u.Name).FirstOrDefault()
                        ?? e.NotApprovedByOperatorId
                })
            .OrderByDescending(r => r.Em)
            .Take(MaxPorTipo)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SumarioItem(
            SumarioTipo.ControloNaoAprovado,
            Dia(r.Em),
            r.Em,
            Producao(r.Reference, r.ProductionNumber),
            string.IsNullOrWhiteSpace(r.ReviewNote) ? null : r.ReviewNote,
            r.Por) { JobOnId = r.JobOnId, Maquina = r.Machine });
    }

    /// <summary>Os movimentos de boquilhas apagados no período (o rasto do apagar).</summary>
    private async Task<IEnumerable<SumarioItem>> BqApagadosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = await (
                from d in db.BqMovementDeletions.AsNoTracking()
                where d.DeletedAtUtc >= fromUtc && d.DeletedAtUtc < toUtc
                join t in db.BqRepairTraces.AsNoTracking() on d.BqRepairTraceId equals t.Id into traces
                from t in traces.DefaultIfEmpty()
                select new
                {
                    d.Type,
                    d.Quantity,
                    d.MachineCodeFrozen,
                    d.DeletedByNameFrozen,
                    d.DeletedAtUtc,
                    Reference = t == null ? null : t.ToolReferenceFrozen ?? t.Tool!.Reference,
                    Lot = t == null ? null : t.ToolLotFrozen ?? t.Tool!.Lot,
                    BqContextId = t == null ? null : t.BqContextId
                })
            .OrderByDescending(r => r.DeletedAtUtc)
            .Take(MaxPorTipo)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SumarioItem(
            SumarioTipo.BqMovimentoApagado,
            Dia(r.DeletedAtUtc),
            r.DeletedAtUtc,
            r.Reference is null ? "Boquilha" : $"{r.Reference}{(string.IsNullOrEmpty(r.Lot) ? null : $" · lote {r.Lot}")}",
            $"{BqMovementLabels.Of(r.Type)} de {r.Quantity}",
            r.DeletedByNameFrozen) { BqContextId = r.BqContextId, Maquina = r.MachineCodeFrozen });
    }

    /// <summary>
    /// As ferramentas das produções que começam no período a que faltam dados
    /// técnicos (Diogo, 19/10/2026: «dados técnicos em falta faz mais
    /// sentido»), uma linha por ferramenta e produção, no dia do início.
    /// </summary>
    private async Task<IEnumerable<SumarioItem>> DadosEmFaltaAsync(DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken)
    {
        var producoes = await db.JobOns.AsNoTracking()
            .Where(j => j.InicioEm >= from && j.InicioEm < toExclusive)
            .OrderBy(j => j.InicioEm)
            .Take(MaxPorTipo)
            .Select(j => new
            {
                j.Reference,
                j.ProductionNumber,
                Machine = j.Machine.Code,
                Inicio = j.InicioEm!.Value,
                Cm = j.CmContext == null ? null : j.CmContext.ToolId,
                Mf = j.MfContext == null ? null : j.MfContext.ToolId,
                Bq = j.BqContext == null ? null : j.BqContext.ToolId
            })
            .ToListAsync(cancellationToken);

        var toolIds = producoes.SelectMany(p => new[] { p.Cm, p.Mf, p.Bq }).OfType<string>().Distinct().ToList();
        var tools = await db.Tools.AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId))
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .ToDictionaryAsync(t => t.ToolId, cancellationToken);
        var technical = await db.ToolTechnicalContexts.AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId))
            .ToDictionaryAsync(t => t.ToolId, cancellationToken);

        var items = new List<SumarioItem>();
        foreach (var p in producoes)
        {
            foreach (var toolId in new[] { p.Cm, p.Mf, p.Bq }.OfType<string>())
            {
                if (!tools.TryGetValue(toolId, out var tool))
                {
                    continue;
                }

                var falta = ToolTechnicalMissing.For(tool.Type, technical.GetValueOrDefault(toolId));
                if (falta.Count == 0)
                {
                    continue;
                }

                items.Add(new SumarioItem(
                    SumarioTipo.FerramentaDadosEmFalta,
                    p.Inicio,
                    null,
                    $"{tool.Type.ToString().ToUpperInvariant()} {tool.Reference} · lote {tool.Lot}",
                    $"Falta: {string.Join(", ", falta)} · {Producao(p.Reference, p.ProductionNumber)}",
                    null) { ToolId = toolId, Maquina = p.Machine });
            }
        }

        return items;
    }

    // A referência e a produção como nas linhas do Job On (5547T173 202601);
    // a máquina vai à parte, em SumarioItem.Maquina, com a cor dela.
    private static string Producao(string reference, string productionNumber) =>
        $"{reference} {productionNumber}";

    private static DateOnly Dia(DateTime utc) =>
        DateOnly.FromDateTime(DmoBusinessTime.ToLocal(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

    private static DateTime Utc(DateOnly day) =>
        DateTime.SpecifyKind(DmoBusinessTime.ToUtc(day.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc);
}

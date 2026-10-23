using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Planeamento;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using System.Linq.Expressions;
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
            items.AddRange(await DadosEmFaltaAsync(j => j.InicioEm >= from && j.InicioEm < toExclusive, cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.UsoAtualizado))
        {
            items.AddRange(await UsoAtualizadoAsync(j => j.FimEm >= from && j.FimEm < toExclusive, cancellationToken));
        }

        return items
            .OrderByDescending(i => i.Dia)
            .ThenBy(i => i.Tipo)
            .ThenByDescending(i => i.QuandoUtc)
            .ToList();
    }

    /// <summary>
    /// Quantos dias para trás se procuram os por resolver que não têm estado
    /// próprio no fim (os por ver, os dados em falta e o % de uso de produções
    /// antigas deixam de pedir ação).
    /// </summary>
    public const int DiasPorResolver = 60;

    /// <summary>
    /// Os movimentos de boquilhas apagados ficam no topo 7 dias: não há um
    /// estado de «visto» do apagar (Diogo, 23/10/2026).
    /// </summary>
    public const int DiasBqApagados = 7;

    // OS IMPORTANTES ATÉ RESOLVER (Diogo, 23/10/2026: «ficam sempre no topo
    // até resolver»). Saem pelo estado que já existe: os por aprovar quando
    // há decisão (a Revisão), os não aprovados quando a decisão passa a
    // aprovado, os por ver quando alguém abre o Resumo, os dados em falta
    // quando a ficha os tem, o % de uso quando há a leitura do fim. Os dois
    // primeiros não têm limite de dias; os outros olham para os últimos
    // DiasPorResolver dias e só para produções que não acabaram antes de
    // hoje (os por ver e os dados em falta).
    public async Task<IReadOnlyList<SumarioItem>> PorResolverAsync(
        DateOnly hoje,
        IReadOnlyCollection<SumarioTipo> tipos,
        CancellationToken cancellationToken = default)
    {
        var amanha = hoje.AddDays(1);
        var desde = hoje.AddDays(-DiasPorResolver);
        var desdeUtc = Utc(desde);
        var items = new List<SumarioItem>();

        if (tipos.Contains(SumarioTipo.ControloPorAprovar))
        {
            var pendentes = await revisao.PeriodAsync(new DateOnly(2000, 1, 1), amanha, null, ControloRevisaoEstado.Pendente, null, MaxPorTipo, cancellationToken);
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
            items.AddRange(await NaoAprovadosAsync(Utc(new DateOnly(2000, 1, 1)), Utc(amanha), cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.JobOnPorVer))
        {
            var rows = await db.JobOns.AsNoTracking()
                .Where(j => (j.FimEm == null || j.FimEm >= hoje)
                    && (j.CriadoEmUtc >= desdeUtc || (j.CriadoEmUtc == null && j.InicioEm >= desde && j.InicioEm <= hoje))
                    && !db.ControloViewings.Any(v => v.Controlo!.JobOnId == j.Id))
                .OrderByDescending(j => j.CriadoEmUtc)
                .Take(MaxPorTipo)
                .Select(j => new { j.Id, j.Reference, j.ProductionNumber, Machine = j.Machine.Code, j.InicioEm, j.CriadoEmUtc })
                .ToListAsync(cancellationToken);
            items.AddRange(rows.Select(r => new SumarioItem(
                SumarioTipo.JobOnPorVer,
                r.CriadoEmUtc is { } criado ? Dia(criado) : r.InicioEm!.Value,
                r.CriadoEmUtc,
                Producao(r.Reference, r.ProductionNumber),
                "Ainda ninguém abriu o Resumo",
                null) { JobOnId = r.Id, Maquina = r.Machine }));
        }

        if (tipos.Contains(SumarioTipo.BqMovimentoApagado))
        {
            items.AddRange(await BqApagadosAsync(Utc(hoje.AddDays(1 - DiasBqApagados)), Utc(amanha), cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.FerramentaDadosEmFalta))
        {
            items.AddRange(await DadosEmFaltaAsync(
                j => j.InicioEm >= desde && j.InicioEm <= hoje && (j.FimEm == null || j.FimEm >= hoje), cancellationToken));
        }

        if (tipos.Contains(SumarioTipo.UsoAtualizado))
        {
            items.AddRange((await UsoAtualizadoAsync(j => j.FimEm >= desde && j.FimEm <= hoje, cancellationToken)).Where(i => !i.Resolvido));
        }

        return items
            .OrderByDescending(i => i.InstanteUtc)
            .ThenBy(i => i.Tipo)
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
            // As removidas por engano não entram (Diogo, 23/10/2026).
            .Where(JobOnChangeFields.Active)
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
                c.ChangedByNameFrozen) { JobOnId = c.JobOnId, Maquina = c.Machine })
            // A mesma informação não se repete (Diogo, 23/10/2026: «se a informação já existe não duplicar no sumário»):
            // a mesma alteração do mesmo Job On, pela mesma pessoa, no mesmo minuto, fica numa só linha.
            .DistinctBy(i => (i.JobOnId, i.Detalhe, i.Por, (i.QuandoUtc ?? default).Ticks / TimeSpan.TicksPerMinute));
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
            r.Reference is null ? "Boquilha" : $"{r.Reference}{(string.IsNullOrEmpty(r.Lot) ? null : $" · Lote {Lote(r.Lot)}")}",
            $"{BqMovementLabels.Of(r.Type)} de {r.Quantity}",
            r.DeletedByNameFrozen) { BqContextId = r.BqContextId, Maquina = r.MachineCodeFrozen });
    }

    /// <summary>
    /// As ferramentas das produções que começam no período a que faltam dados
    /// técnicos (Diogo, 19/10/2026: «dados técnicos em falta faz mais
    /// sentido»), uma linha por ferramenta e produção, no dia do início.
    /// </summary>
    private async Task<IEnumerable<SumarioItem>> DadosEmFaltaAsync(Expression<Func<JobOn, bool>> quais, CancellationToken cancellationToken)
    {
        var producoes = await db.JobOns.AsNoTracking()
            .Where(quais)
            .Where(j => j.InicioEm != null)
            .OrderBy(j => j.InicioEm)
            .Take(MaxPorTipo)
            .Select(j => new
            {
                j.Id,
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
                    $"{tool.Type.ToString().ToUpperInvariant()} {tool.Reference} · Lote {Lote(tool.Lot)}",
                    $"Falta: {string.Join(", ", falta)} · {Producao(p.Reference, p.ProductionNumber)}",
                    null) { ToolId = toolId, JobOnId = p.Id, Maquina = p.Machine });
            }
        }

        return items;
    }

    /// <summary>
    /// O % de uso atualizado (Diogo, 23/10/2026): as CM, MF e BQ dos Job Ons
    /// que acabaram no período, uma linha por ferramenta e produção, no dia
    /// do fim. A conta é a da ficha e do Histórico
    /// (<see cref="Tools.FerramentasFabricoSap.UsoNaProducao"/>: a leitura do
    /// dia do fim menos a do início); sem a leitura do fim, a linha diz que
    /// falta, porque é isso que fica por fazer na ficha.
    /// </summary>
    private async Task<IEnumerable<SumarioItem>> UsoAtualizadoAsync(Expression<Func<JobOn, bool>> quais, CancellationToken cancellationToken)
    {
        var producoes = await db.JobOns.AsNoTracking()
            .Where(quais)
            .Where(j => j.FimEm != null)
            .OrderBy(j => j.FimEm)
            .Take(MaxPorTipo)
            .Select(j => new
            {
                j.Id,
                j.Reference,
                j.ProductionNumber,
                Machine = j.Machine.Code,
                j.InicioEm,
                Fim = j.FimEm!.Value,
                Cm = j.CmContext == null ? null : j.CmContext.ToolId,
                Mf = j.MfContext == null ? null : j.MfContext.ToolId,
                Bq = j.BqContext == null ? null : j.BqContext.ToolId
            })
            .ToListAsync(cancellationToken);

        var toolIds = producoes.SelectMany(p => new[] { p.Cm, p.Mf, p.Bq }).OfType<string>().Distinct().ToList();
        var tools = await db.Tools.AsNoTracking()
            .Where(t => toolIds.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .ToDictionaryAsync(t => t.ToolId, cancellationToken);
        var leituras = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => toolIds.Contains(u.ToolId))
                .OrderBy(u => u.RegistadaEmUtc).ThenBy(u => u.Id)
                .Select(u => new { u.ToolId, u.RegistadaEmUtc, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<(DateOnly Dia, decimal Percentagem)>)g
                .Select(u => (Dia(u.RegistadaEmUtc), u.Percentagem)).ToList(), StringComparer.Ordinal);

        var items = new List<SumarioItem>();
        foreach (var p in producoes)
        {
            foreach (var toolId in new[] { p.Cm, p.Mf, p.Bq }.OfType<string>().Distinct())
            {
                if (!tools.TryGetValue(toolId, out var tool))
                {
                    continue;
                }

                var l = leituras.GetValueOrDefault(toolId) ?? [];
                var depois = Tools.FerramentasFabricoSap.UsoDepois(l, p.InicioEm, p.Fim);
                var naProducao = Tools.FerramentasFabricoSap.UsoNaProducao(l, p.InicioEm, p.Fim);
                var detalhe = depois is { } agora
                    ? (naProducao is { } delta ? $"{(delta >= 0 ? "+" : null)}{Percentagem(delta)} nesta produção · agora {Percentagem(agora)}" : $"Agora {Percentagem(agora)}")
                    : "Falta a leitura do % de uso depois do fim";
                items.Add(new SumarioItem(
                    SumarioTipo.UsoAtualizado,
                    p.Fim,
                    null,
                    $"{tool.Type.ToString().ToUpperInvariant()} {tool.Reference} · Lote {Lote(tool.Lot)}",
                    $"{detalhe} · {Producao(p.Reference, p.ProductionNumber)}",
                    null) { ToolId = toolId, JobOnId = p.Id, Maquina = p.Machine, Resolvido = depois is not null });
            }
        }

        return items;
    }

    private static string Percentagem(decimal valor) =>
        valor.ToString("0.##", DmoNumberFormat.Culture) + " %";

    // A referência e a produção como nas linhas do Job On (5547T173 202601);
    // a máquina vai à parte, em SumarioItem.Maquina, com a cor dela.
    private static string Producao(string reference, string productionNumber) =>
        $"{reference} {productionNumber}";

    private static DateOnly Dia(DateTime utc) =>
        DateOnly.FromDateTime(DmoBusinessTime.ToLocal(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

    private static DateTime Utc(DateOnly day) =>
        DateTime.SpecifyKind(DmoBusinessTime.ToUtc(day.ToDateTime(TimeOnly.MinValue)), DateTimeKind.Utc);

    // O lote como se mostra: sem zeros à esquerda («Lote 2», nunca «Lote 02»; Diogo, 22/10/2026), como o LoteTexto da Web.
    private static string Lote(string? lote)
    {
        var texto = lote?.Trim() ?? string.Empty;
        if (texto.Length == 0 || !texto.All(char.IsAsciiDigit))
        {
            return texto;
        }

        var semZeros = texto.TrimStart('0');
        return semZeros.Length == 0 ? "0" : semZeros;
    }
}

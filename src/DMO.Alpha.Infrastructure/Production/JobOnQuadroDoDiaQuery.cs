using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// The day board that used to be composed inside the Job On page. Same reads,
/// in the same order, and the same rows as before.
/// </summary>
public sealed class JobOnQuadroDoDiaQuery(
    IJobOnListQuery jobOnList,
    JobOnLotNoteHandler lotNotes,
    JobOnChangeQuery changes) : IJobOnQuadroDoDiaQuery
{
    public async Task<IReadOnlyList<DayMachineRow>> QuadroAsync(
        IReadOnlyList<MachineOption> machines,
        IReadOnlyList<ProductionContextReadModel> jobOns,
        int ano,
        int mes,
        int dia,
        bool diaEscolhido,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DayMachineRow> dayRows = ComporDayRows(machines, jobOns, ano, mes, dia);

        // As mudanças de lote do dia entram na linha da máquina, no espaço
        // entre a referência e o número (o proprietário, 16/10/2026).
        var doDia = dayRows.SelectMany(r => r.Productions).Select(p => p.JobOnId).ToList();
        var notasDoDia = await lotNotes.ForDayAsync(new DateOnly(ano, mes, dia), doDia, cancellationToken);
        // E as alterações de cada produção: a linha mostra qual foi (o Diogo,
        // 17/10/2026), e a ficha tem o detalhe em «O que mudou».
        var alteracoes = await changes.AfterStartAsync(doDia, cancellationToken);
        if (notasDoDia.Count > 0 || alteracoes.Count > 0)
        {
            dayRows = dayRows
                .Select(r => r with
                {
                    Productions = r.Productions
                        .Select(p => p with
                        {
                            LotNotes = notasDoDia.Where(n => n.JobOnId == p.JobOnId).Select(n => n.Text).ToList(),
                            ChangeCount = alteracoes.TryGetValue(p.JobOnId, out var mudou) ? mudou.Count : 0,
                            ChangeSummary = mudou is { Count: > 0 } ? JobOnChangeFields.Summarize(mudou) : null,
                            ChangeDetails = mudou?.Select(c => c.Text).ToList() ?? []
                        })
                        .ToList()
                })
                .ToList();
        }

        // A próxima mudança em cada máquina, depois do dia escolhido (o
        // proprietário, 17/10/2026): a produção que entra a seguir ou a troca
        // de um componente da produção atual, a que vier primeiro; no mesmo
        // dia, a produção nova. Vai para a frente da linha.
        if (diaEscolhido)
        {
            var data = new DateOnly(ano, mes, dia);
            var proximas = await jobOnList.ListNextPerMachineAsync(data, cancellationToken);
            var trocas = await lotNotes.NextAfterAsync(data, doDia, cancellationToken);
            if (proximas.Count > 0 || trocas.Count > 0)
            {
                dayRows = dayRows
                    .Select(r =>
                    {
                        var entra = proximas.FirstOrDefault(p => p.Machine == r.MachineCode) is { InicioEm: { } inicio } p
                            ? new NextChange(inicio, $"{p.Reference} {p.ProductionNumber}", true, JobOnId: p.JobOnId)
                            : null;
                        var troca = trocas
                            .Where(t => r.Productions.Any(x => x.JobOnId == t.JobOnId))
                            .OrderBy(t => t.Dia)
                            .Select(t => new NextChange(t.Dia, t.Texto, false, ToolId: t.NewToolId))
                            .FirstOrDefault();
                        var next = entra is null ? troca
                            : troca is null || entra.Dia <= troca.Dia ? entra
                            : troca;
                        return next is null ? r : r with { Next = next };
                    })
                    .ToList();
            }
        }

        return dayRows;
    }

    private static IReadOnlyList<DayMachineRow> ComporDayRows(
        IReadOnlyList<MachineOption> machines, IReadOnlyList<ProductionContextReadModel> jobOns, int ano, int mes, int? diaSelecionado)
    {
        // A procura não filtra o dia (Diogo, 17/10/2026: não é um filtro
        // global); as linhas das máquinas mostram sempre o dia escolhido.
        var cobre = new List<ProductionContextReadModel>();
        // O DIA CONSULTADO (o painel do dia do dmo-html): o dia clicado no
        // calendário — na omissão, o dia inicial da página (o hoje no mês
        // corrente). A produção «até aviso» (o FimEm null) COBRE o dia a
        // partir do início (a regra do corte B: as datas do Job On mandam).
        var data = new DateOnly(ano, mes, Math.Clamp(diaSelecionado ?? 1, 1, DateTime.DaysInMonth(ano, mes)));
        // Uma produção por linha: a que entrou por último (Diogo, 27/10/2026).
        // A experiência que entra e sai no mesmo dia fica ao lado dela.
        cobre.AddRange(ProducaoEmCurso.DoDia(jobOns, data));
        var rows = new List<DayMachineRow>();
        foreach (var machine in machines)
        {
            var doDia = cobre.Where(j => j.Machine == machine.Code).OrderBy(j => j.Reference).ToList();
            // Sem sobreposição entre produções da linha: só as BQ a têm
            // (Diogo, 25/10/2026).
            const bool cruzam = false;
            rows.Add(new(machine.Code, doDia.Select(x => new DayProduction(x.JobOnId, x.Reference, x.ProductionNumber, x.Experiencia)).ToList())
            {
                HasOverlap = cruzam
            });
        }
        return rows;
    }
}

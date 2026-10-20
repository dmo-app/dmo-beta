using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Search;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Search;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Deep sweep 2 (18/10/2026, F-2): a regra da procura de uma referência (o
/// início dela ou da parte BQ, nunca a meio) traduzida no PostgreSQL por cada
/// consulta que a usa. O InMemory avalia o LINQ em C# e não apanha uma
/// expressão que o Npgsql não traduz. Os dados têm um prefixo de algarismos
/// ao acaso, para a base partilhada pelos testes não interferir.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ProcuraPeloInicio_TodasAsConsultas_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        // A máquina e o nº da produção «a meio» só com letras: o nº, a máquina
        // e o lote continuam a procurar-se por parte do texto, e não podem
        // conter o prefixo.
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var r1 = $"{p}1Y{p}";       // começa pelo prefixo; parte BQ «Y…»
        var r2 = $"{p}2T{p}";
        var rMeio = $"9{p}5Z{p}";   // o prefixo só a meio; parte BQ «Z…»
        var n1 = $"{p}01";
        var n2 = $"{p}02";
        var nMeio = $"M{letras}";
        var machine = new Machine { Code = $"PG-{letras}" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = $"BQ-INI-{p}", Type = ToolType.Bq, Reference = $"Y{p}", Lot = "1" },
            new Tool { ToolId = $"CM-INI-{p}", Type = ToolType.Cm, Reference = p, Lot = "2" },
            new Tool { ToolId = $"CM-INIM-{p}", Type = ToolType.Cm, Reference = $"9{p}", Lot = "3" });
        await db.SaveChangesAsync();
        // Por esta ordem (os ids crescem): o mesmo nº n1 na r1 e na r2.
        var j1 = new JobOn { Reference = r1, ProductionNumber = n1, MachineId = machine.Id };
        var j2 = new JobOn { Reference = r2, ProductionNumber = n2, MachineId = machine.Id };
        var j3 = new JobOn { Reference = r2, ProductionNumber = n1, MachineId = machine.Id };
        var jMeio = new JobOn { Reference = rMeio, ProductionNumber = nMeio, MachineId = machine.Id };
        foreach (var jobOn in new[] { j1, j2, j3, jMeio })
        {
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
        }
        var bq1 = new BqContext { JobOnId = j1.Id, ToolId = $"BQ-INI-{p}" };
        var bqMeio = new BqContext { JobOnId = jMeio.Id, ToolId = $"BQ-INI-{p}" };
        db.BqContexts.AddRange(bq1, bqMeio);
        await db.SaveChangesAsync();
        var trace1 = new BqRepairTrace { BqContextId = bq1.Id };
        var traceMeio = new BqRepairTrace { BqContextId = bqMeio.Id };
        db.BqRepairTraces.AddRange(trace1, traceMeio);
        await db.SaveChangesAsync();
        var occurred = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        db.BqMovements.AddRange(
            new BqMovement { BqRepairTraceId = trace1.Id, Type = BqMovementType.Saida, Quantity = 1, OccurredAtUtc = occurred, CreatedAtUtc = occurred },
            new BqMovement { BqRepairTraceId = traceMeio.Id, Type = BqMovementType.Saida, Quantity = 1, OccurredAtUtc = occurred, CreatedAtUtc = occurred });
        var controlo1 = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = j1.Id };
        var controloMeio = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jMeio.Id };
        db.Controlos.AddRange(controlo1, controloMeio);
        await db.SaveChangesAsync();
        Peso NovoPeso(int controloId, string cmToolId) => new()
        {
            CmToolId = cmToolId, BqToolId = $"BQ-INI-{p}", Process = "NNPB",
            WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m,
            GlassDensityGramsPerCm3 = 2.5m, ControloId = controloId
        };
        db.Pesos.AddRange(NovoPeso(controlo1.Id, $"CM-INI-{p}"), NovoPeso(controloMeio.Id, $"CM-INIM-{p}"));
        await db.SaveChangesAsync();

        try
        {
            // As sugestões.
            var suggestions = new SuggestionQuery(db);
            async Task<string[]> Sugere(SuggestionSource fonte, string texto, ToolType? tipo = null) =>
                (await suggestions.SuggestAsync(fonte, texto, tipo)).Select(s => s.Value).ToArray();
            Assert.Equal(new[] { r1, r2 }, await Sugere(SuggestionSource.Referencias, p));
            Assert.Equal(new[] { r1 }, await Sugere(SuggestionSource.Referencias, $"y{p}"));
            Assert.Equal(new[] { rMeio }, await Sugere(SuggestionSource.Referencias, $"z{p}"));
            Assert.Equal(new[] { r2, r2, r1 }, await Sugere(SuggestionSource.Producoes, p));
            Assert.Equal(new[] { n1, n2, n1 }, await Sugere(SuggestionSource.NumerosProducao, p));
            Assert.Equal(new[] { p }, await Sugere(SuggestionSource.Ferramentas, p));
            Assert.Equal(new[] { $"Y{p}" }, await Sugere(SuggestionSource.Ferramentas, $"y{p}", ToolType.Bq));
            Assert.Equal(new[] { r1, $"Y{p}" }, await Sugere(SuggestionSource.ReferenciasEBq, $"y{p}"));

            // O segundo passo da escolha: o início da referência ou da parte BQ.
            var picker = new ProductionPickerQuery(db);
            Assert.Equal(j1.Id, Assert.Single(await picker.ListByReferenceAsync(r1.ToLowerInvariant())).JobOnId);
            Assert.Equal(new[] { j3.Id, j2.Id, j1.Id }, (await picker.ListByReferenceAsync(p)).Select(i => i.JobOnId));
            Assert.Equal(jMeio.Id, Assert.Single(await picker.ListByReferenceAsync($"z{p}")).JobOnId);
            Assert.Empty(await picker.ListByReferenceAsync($"{p}5Z"));
            // O dropdown do Job On: só a referência completa (Diogo, 20/10/2026).
            Assert.Equal(j1.Id, Assert.Single(await picker.ListOfReferenceAsync(r1.ToLowerInvariant())).JobOnId);
            Assert.Empty(await picker.ListOfReferenceAsync(p));
            Assert.Empty(await picker.ListOfReferenceAsync($"z{p}"));

            // A lista do Job On.
            var list = new JobOnListQuery(db);
            Assert.Equal(j1.Id, Assert.Single(await list.ListByReferenceAsync(r1.ToLowerInvariant(), null)).JobOnId);
            Assert.Equal(2, (await list.ListPageOfReferenceAsync($" {r2.ToLowerInvariant()} ", null, 10)).TotalCount);
            Assert.Equal(
                new[] { (n1, r2), (n2, r2), (n1, r1) },
                (await list.ListProductionsOfReferenceAsync(p, machine.Id)).Select(o => (o.ProductionNumber, o.Reference)));
            Assert.Equal(2, (await list.SearchHistoryPageAsync(new JobOnHistoryFilter(p, null, n1, null, null), null)).TotalCount);
            Assert.Equal(j3.Id, Assert.Single((await list.SearchHistoryPageAsync(
                new JobOnHistoryFilter(p, null, n1, null, null, r2.ToLowerInvariant()), null)).Items).JobOnId);
            // «{p}5Z» só existe a meio da rMeio.
            Assert.Empty((await list.SearchHistoryPageAsync(new JobOnHistoryFilter($"{p}5Z", null, null, null, null), null)).Items);

            // O Resumo: a referência ou o nº pelo início.
            var resumo = new ResumoProductionQuery(db);
            Assert.Equal(new[] { j3.Id, j2.Id, j1.Id }, (await resumo.ListAsync(p, 1)).Items.Select(i => i.JobOnId));
            Assert.Equal(jMeio.Id, Assert.Single((await resumo.ListAsync($"z{p}", 1)).Items).JobOnId);
            Assert.Equal(jMeio.Id, Assert.Single((await resumo.ListAsync(nMeio, 1)).Items).JobOnId);

            // As ferramentas: a procura do catálogo e a das Ferramentas.
            var tools = new ToolCandidateQuery(db);
            Assert.Equal(new[] { $"CM-INI-{p}" }, (await tools.FindCandidatesAsync(new ToolCandidateFilter(Search: p))).Select(t => t.ToolId));
            Assert.Equal(new[] { $"BQ-INI-{p}" }, (await tools.FindCandidatesAsync(new ToolCandidateFilter(Search: $"y{p}"))).Select(t => t.ToolId));
            Assert.Equal(new[] { $"CM-INI-{p}" }, (await tools.FindCandidatesAsync(new ToolCandidateFilter(ReferenceContains: p))).Select(t => t.ToolId));

            // O Histórico das Boquilhas: a referência da produção pelo início; a BQ pela parte BQ.
            var history = new BqToolHistoryQuery(db);
            Assert.Equal(new[] { trace1.Id }, (await history.ListMovementsAsync(null, null, null, null, null, p)).Select(m => m.BqRepairTraceId));
            Assert.Equal(
                new[] { trace1.Id, traceMeio.Id }.OrderBy(id => id),
                (await history.ListMovementsAsync(null, null, null, null, null, $"y{p}")).Select(m => m.BqRepairTraceId).OrderBy(id => id));

            // A Revisão: as produções sem data de registo.
            var revisao = new ControloRevisaoQuery(db);
            Assert.Equal(j1.Id, Assert.Single(await revisao.UndatedAsync(p, null)).JobOnId);
            Assert.Equal(jMeio.Id, Assert.Single(await revisao.UndatedAsync($"z{p}", null)).JobOnId);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.Pesos.Where(x => x.ControloId == controlo1.Id || x.ControloId == controloMeio.Id).ExecuteDeleteAsync();
            await db.Controlos.Where(c => c.JobOnId == j1.Id || c.JobOnId == jMeio.Id).ExecuteDeleteAsync();
            await db.BqMovements.Where(m => m.BqRepairTraceId == trace1.Id || m.BqRepairTraceId == traceMeio.Id).ExecuteDeleteAsync();
            await db.BqRepairTraces.Where(t => t.Id == trace1.Id || t.Id == traceMeio.Id).ExecuteDeleteAsync();
            await db.BqContexts.Where(c => c.ToolId == $"BQ-INI-{p}").ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId.EndsWith(p)).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == machine.Id).ExecuteDeleteAsync();
        }
    }
}

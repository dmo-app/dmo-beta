using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Deep sweep 2 (18/10/2026, F-4, F-5, F-6 e F-18): a procura do Histórico
/// do Controlo traduzida no PostgreSQL — a referência pelo início dela ou da
/// parte BQ, o nº com a referência da produção escolhida, a data do registo
/// dos Pesos e dos Pegamentos no dia de Lisboa, a ordem pelas datas com os
/// sem data no fim (também ao paginar), o «Sem Job On associado» com a
/// referência e a data, e a produção pedida por link. O InMemory avalia o
/// LINQ em C# e não apanha uma expressão que o Npgsql não traduz. Os dados
/// têm um prefixo de algarismos ao acaso e cada procura leva a referência
/// dele, para a base partilhada pelos testes não interferir.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task HistoricoDoControlo_ReferenciaDataEProducao_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var r1 = $"{p}1Y{p}";       // começa pelo prefixo
        var r2 = $"{p}2T{p}";       // começa pelo prefixo; o mesmo nº da r1
        var rMeio = $"9{p}5Z{p}";   // o prefixo só a meio; parte BQ «Z…»
        var n1 = $"{p}01";
        var n2 = $"{p}02";
        var cmId = $"CM-HC-{p}";
        var bqId = $"BQ-HC-{p}";
        var machine = new Machine { Code = $"HC-{letras}" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = cmId, Type = ToolType.Cm, Reference = p, Lot = "1" },
            new Tool { ToolId = bqId, Type = ToolType.Bq, Reference = $"Y{p}", Lot = "2" });
        await db.SaveChangesAsync();
        var j1 = new JobOn { Reference = r1, ProductionNumber = n1, MachineId = machine.Id };
        var j2 = new JobOn { Reference = r2, ProductionNumber = n1, MachineId = machine.Id };
        var jMeio = new JobOn { Reference = rMeio, ProductionNumber = n2, MachineId = machine.Id };
        foreach (var jobOn in new[] { j1, j2, jMeio })
        {
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
        }
        db.CmContexts.Add(new CmContext { JobOnId = j1.Id, ToolId = cmId });
        var c1 = new DMO.Alpha.Core.Controlo.Controlo
        {
            JobOnId = j1.Id,
            ApprovedAtUtc = new DateTime(2020, 3, 16, 9, 0, 0, DateTimeKind.Utc),
            ApprovedByOperatorId = "4221"
        };
        var c2 = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = j2.Id };
        var cMeio = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jMeio.Id };
        db.Controlos.AddRange(c1, c2, cMeio);
        await db.SaveChangesAsync();
        Peso NovoPeso(int? controloId, DateTime? registadoEm) => new()
        {
            CmToolId = cmId, BqToolId = bqId, Process = "NNPB",
            WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m,
            GlassDensityGramsPerCm3 = 2.5m, ControloId = controloId, RegistadoEmUtc = registadoEm
        };
        var peso1 = NovoPeso(c1.Id, new DateTime(2026, 10, 16, 23, 30, 0, DateTimeKind.Utc)); // 17/10 00:30 em Lisboa
        peso1.CmToolReferenceFrozen = p;
        var peso2 = NovoPeso(c2.Id, null);                                                     // anterior ao carimbo
        var pesoMeio = NovoPeso(cMeio.Id, new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc));
        var pesoSolto = NovoPeso(null, new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc));
        pesoSolto.CmToolReferenceFrozen = p;
        pesoSolto.BqToolReferenceFrozen = $"Y{p}";
        db.Pesos.AddRange(peso1, peso2, pesoMeio, pesoSolto);
        var g1 = new Pegamentos
        {
            ControloId = c1.Id, CmToolId = cmId, CmToolReferenceFrozen = p, ToleranceMm = 0.2m,
            RegistadoEmUtc = new DateTime(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc)
        };
        var gSolto = new Pegamentos { MfToolReferenceFrozen = $"8{p}", ToleranceMm = 0.2m };
        db.Pegamentos.AddRange(g1, gSolto);
        await db.SaveChangesAsync();
        var comparacao = new PesoComparacao
        {
            ControloId = c1.Id, PesoInicialId = peso1.Id, CmToolId = cmId, CmToolReferenceFrozen = p,
            WaterTemperatureC = 20, WaterDensityGramsPerCm3 = 0.99717m, GlassDensityGramsPerCm3 = 2.5m,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m,
            CreatedAtUtc = new DateTime(2026, 10, 17, 7, 0, 0, DateTimeKind.Utc)
        };
        db.PesoComparacoes.Add(comparacao);
        await db.SaveChangesAsync();

        try
        {
            var query = new ControloHistorySearchQuery(db);
            async Task<(ControloHistoryKind, int)[]> Procura(
                string? referencia, string? producao = null, string? ferramenta = null, DateOnly? data = null,
                bool semJobOn = false, string? referenciaDaProducao = null) =>
                (await query.SearchPageAsync(referencia, producao, ferramenta, null, data, 1, 100,
                    semJobOn: semJobOn, productionReference: referenciaDaProducao))
                .Items.Select(i => (i.Kind, i.Id)).ToArray();

            // A referência pelo início: a r1 e a r2, nunca a rMeio. Pelas
            // datas do registo, o mais recente primeiro; os sem data no fim.
            var peloInicio = new[]
            {
                (ControloHistoryKind.Pegamentos, g1.Id),
                (ControloHistoryKind.Comparacao, comparacao.Id),
                (ControloHistoryKind.Peso, peso1.Id),
                (ControloHistoryKind.Resumo, j1.Id),
                (ControloHistoryKind.Peso, peso2.Id),
                (ControloHistoryKind.Resumo, j2.Id)
            };
            Assert.Equal(peloInicio, await Procura(p));
            // A parte BQ, a meio da referência.
            Assert.Equal(
                new[] { (ControloHistoryKind.Peso, pesoMeio.Id), (ControloHistoryKind.Resumo, jMeio.Id) },
                await Procura($"z{p}"));
            // As páginas são a lista inteira cortada, também com as datas fora da ordem dos Ids.
            var paginas = new List<(ControloHistoryKind, int)>();
            for (var pagina = 1; pagina <= 3; pagina++)
            {
                var page = await query.SearchPageAsync(p, null, null, null, null, pagina, 2);
                Assert.Equal(6, page.TotalCount);
                paginas.AddRange(page.Items.Select(i => (i.Kind, i.Id)));
            }
            Assert.Equal(peloInicio, paginas);

            // O nº com a referência da produção escolhida: só essa produção.
            Assert.Equal(6, (await Procura(p, n1)).Length);
            Assert.Equal(
                new[] { (ControloHistoryKind.Peso, peso2.Id), (ControloHistoryKind.Resumo, j2.Id) },
                await Procura(p, n1, referenciaDaProducao: r2.ToLowerInvariant()));
            Assert.Empty(await Procura(p, p));

            // A data: o dia de Lisboa, pela data gravada de cada tipo.
            Assert.Equal(peloInicio[..3], await Procura(p, data: new DateOnly(2026, 10, 17)));
            Assert.Empty(await Procura(p, data: new DateOnly(2026, 10, 16)));
            Assert.Equal(new[] { (ControloHistoryKind.Resumo, j1.Id) }, await Procura(p, data: new DateOnly(2020, 3, 16)));

            // A ferramenta pelo início: a CM congelada ou a corrente; o Resumo pela CM do Job On.
            Assert.Equal(
                new[] { (ControloHistoryKind.Pegamentos, g1.Id), (ControloHistoryKind.Comparacao, comparacao.Id),
                    (ControloHistoryKind.Peso, peso1.Id), (ControloHistoryKind.Resumo, j1.Id), (ControloHistoryKind.Peso, peso2.Id) },
                await Procura(p, ferramenta: p));
            Assert.Empty(await Procura(p, ferramenta: p[1..]));

            // Sem Job On: a referência nas ferramentas do registo e a data do registo.
            Assert.Equal(new[] { (ControloHistoryKind.Peso, pesoSolto.Id) }, await Procura($"y{p}", semJobOn: true));
            Assert.Equal(new[] { (ControloHistoryKind.Pegamentos, gSolto.Id) }, await Procura($"8{p}", semJobOn: true));
            Assert.Equal(new[] { (ControloHistoryKind.Peso, pesoSolto.Id) }, await Procura(p, data: new DateOnly(2026, 10, 17), semJobOn: true));
            Assert.Empty(await Procura($"8{p}", data: new DateOnly(2026, 10, 17), semJobOn: true));

            // A produção pedida por link: a mais recente com o nº, ou a da referência dada.
            Assert.Equal(new JobOnProductionOption(n1, r2), await query.ProducaoDaReferenciaAsync(p, n1, null));
            Assert.Equal(new JobOnProductionOption(n1, r1), await query.ProducaoDaReferenciaAsync(p, n1, r1.ToLowerInvariant()));
            Assert.Null(await query.ProducaoDaReferenciaAsync($"z{p}", n1, null));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.PesoComparacoes.Where(x => x.Id == comparacao.Id).ExecuteDeleteAsync();
            await db.Pegamentos.Where(g => g.Id == g1.Id || g.Id == gSolto.Id).ExecuteDeleteAsync();
            await db.Pesos.Where(x => x.CmToolId == cmId).ExecuteDeleteAsync();
            await db.Controlos.Where(c => c.Id == c1.Id || c.Id == c2.Id || c.Id == cMeio.Id).ExecuteDeleteAsync();
            await db.CmContexts.Where(c => c.ToolId == cmId).ExecuteDeleteAsync();
            await db.JobOns.Where(j => j.MachineId == machine.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == cmId || t.ToolId == bqId).ExecuteDeleteAsync();
            await db.Machines.Where(m => m.Id == machine.Id).ExecuteDeleteAsync();
        }
    }
}

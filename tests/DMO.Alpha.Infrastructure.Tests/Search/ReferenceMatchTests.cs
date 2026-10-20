using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Search;

/// <summary>
/// Deep sweep 2 (18/10/2026, F-2): a regra única da procura de uma referência
/// (Diogo, 18/10/2026) — pelo início da referência ou da parte BQ, nunca um
/// carácter a meio, sem distinguir maiúsculas: «0» encontra a 0132T173 e
/// nunca a 1075; «Y2» encontra a 6018Y232; «T17» a 5447T173; «018» nada. A
/// regra em memória e a da base dão o mesmo, e as procuras que a usam
/// (Resumo, Revisão, Histórico das Boquilhas) seguem-na.
/// </summary>
public sealed class ReferenceMatchTests : IDisposable
{
    private readonly DmoDbContext _db;

    public ReferenceMatchTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("0132T173", "0", true)]
    [InlineData("1075", "0", false)]
    [InlineData("6018Y232", "Y2", true)]
    [InlineData("6018Y232", " y2 ", true)]
    [InlineData("6018Y232", "018", false)]
    [InlineData("6018Y232", "6018y", true)]
    [InlineData("5447T173", "T17", true)]
    [InlineData("5447T173", "17", false)]
    [InlineData("3540Y079", "35", true)]
    [InlineData("4996T350", "35", false)]
    [InlineData("2459T035", "35", false)]
    [InlineData("T194", "t19", true)]
    [InlineData("T194", "19", false)]
    [InlineData(null, "0", false)]
    [InlineData("0132T173", "   ", false)]
    [InlineData("0132T173", null, false)]
    public void Matches_OInicioDaReferenciaOuDaParteBq(string? value, string? term, bool expected)
    {
        Assert.Equal(expected, ReferenceMatch.Matches(value, term));
    }

    [Fact]
    public void Predicate_DaOMesmoQueMatches()
    {
        string?[] values = ["0132T173", "1075", "6018Y232", "5447T173", "3540Y079", "4996T350", "T194", "ST100", null];
        string[] terms = ["0", "35", "Y2", "y2", "T17", "018", "1", "s", "T1", "6018Y232"];
        foreach (var term in terms)
        {
            var predicate = ReferenceMatch.Predicate<Holder>(h => h.Value, term).Compile();
            foreach (var value in values)
            {
                Assert.Equal(ReferenceMatch.Matches(value, term), predicate(new Holder(value)));
            }
        }
    }

    [Fact]
    public void Predicate_SemTexto_Recusa()
    {
        Assert.Throws<ArgumentException>(() => ReferenceMatch.Predicate<Holder>(h => h.Value, "  "));
        Assert.Null(ReferenceMatch.Normalize(" "));
        Assert.Equal("Y2", ReferenceMatch.Normalize(" y2 "));
    }

    private sealed record Holder(string? Value);

    private async Task<Machine> SeedJobOnsAsync()
    {
        var machine = new Machine { Code = "RM1" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        foreach (var (reference, number) in new[]
        {
            ("3540Y079", "2026006"), ("4996T350", "2026153"), ("2459T035", "2026138"),
            ("1638P052", "2026135"), ("0132T173", "2026001"), ("1075C142", "2026002"),
            ("6018Y232", "2026003")
        })
        {
            _db.JobOns.Add(new JobOn { Reference = reference, ProductionNumber = number, MachineId = machine.Id });
        }
        await _db.SaveChangesAsync();
        return machine;
    }

    // F-13 (a parte da referência): «Procurar resumo» encontrava «35» a meio
    // da referência (4996T350, 2459T035) e do nº (1638P052 · 2026135).
    [Theory]
    [InlineData("35", new[] { "3540Y079" })]
    [InlineData("0", new[] { "0132T173" })]
    [InlineData("Y2", new[] { "6018Y232" })]
    [InlineData("y2", new[] { "6018Y232" })]
    [InlineData("018", new string[0])]
    [InlineData("135", new string[0])]
    [InlineData("2026135", new[] { "1638P052" })]
    public async Task Resumo_PeloInicioDaReferenciaOuDoNumero(string texto, string[] expected)
    {
        await SeedJobOnsAsync();

        var page = await new ResumoProductionQuery(_db).ListAsync(texto, 1);

        Assert.Equal(expected, page.Items.Select(i => i.Reference));
        Assert.Equal(expected.Length, page.TotalCount);
    }

    [Fact]
    public async Task Resumo_PeloInicioDoNumero_ContaEPagina()
    {
        await SeedJobOnsAsync();

        var page = await new ResumoProductionQuery(_db).ListAsync("2026", 2);

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    private async Task SeedRevisaoAsync()
    {
        // Os nºs sem algarismos: o nº continua a procurar-se a meio, e um
        // «0» a meio de todos os 2026… esconderia a regra das referências.
        var machine = new Machine { Code = "RM1" };
        _db.Machines.Add(machine);
        _db.Tools.AddRange(
            new Tool { ToolId = "RM-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "RM-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "1235" });
        await _db.SaveChangesAsync();
        foreach (var (reference, number) in new[]
        {
            ("3540Y079", "RA"), ("4996T350", "RB"), ("2459T035", "RC"), ("0132T173", "RD"), ("6018Y232", "RE")
        })
        {
            var jobOn = new JobOn { Reference = reference, ProductionNumber = number, MachineId = machine.Id };
            _db.JobOns.Add(jobOn);
            await _db.SaveChangesAsync();
            var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
            _db.Controlos.Add(controlo);
            await _db.SaveChangesAsync();
            var propria = reference == "3540Y079";
            _db.Pesos.Add(new Peso
            {
                CmToolId = "RM-CM", BqToolId = "RM-BQ", Process = "NNPB",
                WaterTemperatureC = 20, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 70m,
                GlassDensityGramsPerCm3 = 2.5m, ControloId = controlo.Id,
                CmToolReferenceFrozen = propria ? "5447" : "9999",
                BqToolReferenceFrozen = propria ? "T194" : "Y999",
                BqToolLotFrozen = propria ? "1235" : "7"
            });
        }
        await _db.SaveChangesAsync();
    }

    // A Revisão: as referências da produção, da CM e da BQ pelo início (ou
    // a parte BQ); o lote, o nº e a máquina por parte do texto.
    [Theory]
    [InlineData("35", new[] { "3540Y079" })]   // nunca a 4996T350 nem a 2459T035 (35 a meio)
    [InlineData("0", new[] { "0132T173" })]
    [InlineData("y2", new[] { "6018Y232" })]
    [InlineData("54", new[] { "3540Y079" })]   // a CM 5447
    [InlineData("t19", new[] { "3540Y079" })]  // a BQ T194
    [InlineData("19", new string[0])]          // nunca a meio da T194
    [InlineData("018", new string[0])]
    [InlineData("23", new[] { "3540Y079" })]   // o lote 1235, por parte do texto
    [InlineData("rb", new[] { "4996T350" })]   // o nº, por parte do texto
    public async Task Revisao_ProcuraPeloInicioDasReferencias(string texto, string[] expected)
    {
        await SeedRevisaoAsync();

        var found = await new ControloRevisaoQuery(_db).UndatedAsync(texto, null);

        Assert.Equal(expected.OrderBy(r => r), found.Select(i => i.Reference).OrderBy(r => r));
    }

    // O Histórico das Boquilhas: a BQ e a referência da produção pelo início
    // (ou a parte BQ); o lote, o nº, a máquina e o reparador por parte do texto.
    [Theory]
    [InlineData("35", new[] { "3540Y079" })]
    [InlineData("T17", new[] { "0132T173" })]
    [InlineData("17", new string[0])]
    [InlineData("y2", new[] { "6018Y232" })]
    [InlineData("018", new string[0])]
    [InlineData("88", new[] { "6018Y232" })]                        // o lote 1880
    public async Task HistoricoBoquilhas_PesquisaPeloInicioDasReferencias(string texto, string[] expected)
    {
        var machine = new Machine { Code = "RM2" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        foreach (var (reference, bq, lot, number) in new[]
        {
            ("3540Y079", "Y079", "1", "PN-A"), ("4996T350", "T350", "2", "PN-B"),
            ("0132T173", "T173", "3", "PN-C"), ("6018Y232", "Y232", "1880", "PN-D")
        })
        {
            var tool = new Tool { ToolId = $"RM-{bq}", Type = ToolType.Bq, Reference = bq, Lot = lot };
            var jobOn = new JobOn { Reference = reference, ProductionNumber = number, MachineId = machine.Id };
            var context = new BqContext { JobOn = jobOn, ToolId = tool.ToolId };
            var trace = new BqRepairTrace { BqContext = context };
            _db.AddRange(tool, jobOn, context, trace);
            await _db.SaveChangesAsync();
            _db.BqMovements.Add(new BqMovement
            {
                BqRepairTraceId = trace.Id, Type = BqMovementType.Saida, Quantity = 1,
                OccurredAtUtc = DateTime.UtcNow.AddDays(-1), CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        var found = await new BqToolHistoryQuery(_db).ListMovementsAsync(null, null, null, null, null, texto);

        Assert.Equal(expected.OrderBy(r => r), found.Select(m => m.Reference!).OrderBy(r => r));
    }
}

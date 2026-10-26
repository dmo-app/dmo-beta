using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// DMO-075 — a procura em dois passos: sem referência nada é lido; com ela,
/// só as produções dessa referência, da mais recente para a mais antiga e
/// no máximo <see cref="IProductionPickerQuery.MaxResults"/>.
/// </summary>
public sealed class ProductionPickerQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly ProductionPickerQuery _query;

    public ProductionPickerQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new ProductionPickerQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Machine> SeedMachineAsync()
    {
        var machine = new Machine { Code = "PK1" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SemReferencia_NaoDevolveNada(string? reference)
    {
        var machine = await SeedMachineAsync();
        _db.JobOns.Add(new JobOn { Reference = "5932T173", ProductionNumber = "202601", MachineId = machine.Id });
        await _db.SaveChangesAsync();

        Assert.Empty(await _query.ListByReferenceAsync(reference));
    }

    [Fact]
    public async Task ComReferencia_SoAsDela_MaisRecentePrimeiro_SemDistinguirMaiusculas()
    {
        var machine = await SeedMachineAsync();
        _db.JobOns.AddRange(
            new JobOn { Reference = "5932T173", ProductionNumber = "202601", MachineId = machine.Id, InicioEm = new DateOnly(2026, 10, 1) },
            new JobOn { Reference = "6018Y232", ProductionNumber = "202602", MachineId = machine.Id },
            new JobOn { Reference = "5932T173", ProductionNumber = "202603", MachineId = machine.Id });
        await _db.SaveChangesAsync();

        var items = await _query.ListByReferenceAsync(" 5932t173 ");

        Assert.Equal(new[] { "202603", "202601" }, items.Select(i => i.ProductionNumber));
        Assert.Equal("5932T173 · 202601 · PK1 · 01/10/2026", items[1].Label);
        Assert.Equal("5932T173 · 202603 · PK1", items[0].Label);
    }

    // Deep sweep 2 (18/10/2026, F-1/F-3): o segundo passo vai pelo início da
    // referência ou da parte BQ, nunca a meio (ReferenceMatch). Antes, «35»
    // trazia as da 4996T350, da 2459T035 e da 1985T350 (35 a meio) e nenhuma
    // da 3540Y079. O início já enche o dropdown (Diogo, beta.208: «6018»).
    [Theory]
    [InlineData("35", new[] { "2026001" })]
    [InlineData("0", new[] { "2026005" })]
    [InlineData("y2", new[] { "2026006" })]
    [InlineData("T350", new[] { "2026004", "2026002" })]
    [InlineData("3540y079", new[] { "2026001" })]
    [InlineData("018", new string[0])]
    [InlineData("350", new string[0])]
    public async Task ComParteDaReferencia_PeloInicioOuPelaParteBq(string reference, string[] expected)
    {
        var machine = await SeedMachineAsync();
        foreach (var (referencia, numero) in new[]
        {
            ("3540Y079", "2026001"), ("4996T350", "2026002"), ("2459T035", "2026003"),
            ("1985T350", "2026004"), ("0132T173", "2026005"), ("6018Y232", "2026006")
        })
        {
            _db.JobOns.Add(new JobOn { Reference = referencia, ProductionNumber = numero, MachineId = machine.Id });
        }
        await _db.SaveChangesAsync();

        Assert.Equal(expected, (await _query.ListByReferenceAsync(reference)).Select(i => i.ProductionNumber));
    }

    [Fact]
    public async Task ComReferencia_OMesmoNumeroNoutraReferencia_CadaUmaComOSeuJobOn()
    {
        // O mesmo nº em duas referências: cada item leva o seu jobon_id e a sua
        // referência, e «2847» só traz a da 2847Y171.
        var machine = await SeedMachineAsync();
        var escolhida = new JobOn { Reference = "2847Y171", ProductionNumber = "2026999", MachineId = machine.Id };
        var outra = new JobOn { Reference = "4526Y171", ProductionNumber = "2026999", MachineId = machine.Id };
        _db.JobOns.AddRange(escolhida, outra);
        await _db.SaveChangesAsync();

        var item = Assert.Single(await _query.ListByReferenceAsync("2847"));
        Assert.Equal((escolhida.Id, "2847Y171"), (item.JobOnId, item.Reference));
        Assert.Equal(
            new[] { (outra.Id, "4526Y171"), (escolhida.Id, "2847Y171") },
            (await _query.ListByReferenceAsync("Y171")).Select(i => (i.JobOnId, i.Reference)));
    }

    [Fact]
    public async Task ComReferencia_PeloInicioOuPelaParteBq_NuncaPeloMeio()
    {
        // Diogo, 18/10/2026; deep sweep 2, F-1: «2847» trazia também
        // referências com 2847 no meio.
        var machine = await SeedMachineAsync();
        _db.JobOns.AddRange(
            new JobOn { Reference = "2847Y171", ProductionNumber = "2026999", MachineId = machine.Id },
            new JobOn { Reference = "4526Y171", ProductionNumber = "2026999", MachineId = machine.Id },
            new JobOn { Reference = "12847Y17", ProductionNumber = "2026998", MachineId = machine.Id });
        await _db.SaveChangesAsync();

        Assert.Equal(new[] { "2847Y171" }, (await _query.ListByReferenceAsync("2847")).Select(i => i.Reference));
        Assert.Equal(
            new[] { "4526Y171", "2847Y171" },
            (await _query.ListByReferenceAsync("y171")).Select(i => i.Reference));
    }

    [Fact]
    public async Task ComReferencia_CortaNoMaximo()
    {
        var machine = await SeedMachineAsync();
        for (var i = 0; i < IProductionPickerQuery.MaxResults + 5; i++)
        {
            _db.JobOns.Add(new JobOn { Reference = "5932T173", ProductionNumber = $"P{i:000}", MachineId = machine.Id });
        }

        await _db.SaveChangesAsync();

        var items = await _query.ListByReferenceAsync("5932T173");

        Assert.Equal(IProductionPickerQuery.MaxResults, items.Count);
        Assert.Equal($"P{IProductionPickerQuery.MaxResults + 4:000}", items[0].ProductionNumber);
    }

    [Fact]
    public async Task FindAsync_DevolveAProducaoOuNull()
    {
        var machine = await SeedMachineAsync();
        var jobOn = new JobOn { Reference = "5932T173", ProductionNumber = "202601", MachineId = machine.Id };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();

        var found = await _query.FindAsync(jobOn.Id);

        Assert.NotNull(found);
        Assert.Equal("5932T173", found!.Reference);
        Assert.Null(await _query.FindAsync(jobOn.Id + 999));
    }

    [Fact]
    public async Task ProducoesDaReferencia_SoACompleta_ComAsFerramentas()
    {
        var machine = await SeedMachineAsync();
        _db.Tools.Add(new DMO.Alpha.Core.Tools.Tool { ToolId = "BQ-1", Type = DMO.Alpha.Core.Tools.ToolType.Bq, Reference = "T173", Lot = "77" });
        var comBq = new JobOn { Reference = "5932T173", ProductionNumber = "202601", MachineId = machine.Id };
        _db.JobOns.Add(comBq);
        _db.JobOns.Add(new JobOn { Reference = "5932T173X", ProductionNumber = "202602", MachineId = machine.Id });
        await _db.SaveChangesAsync();
        _db.BqContexts.Add(new BqContext { JobOnId = comBq.Id, ToolId = "BQ-1" });
        await _db.SaveChangesAsync();

        Assert.Empty(await _query.ListOfReferenceAsync(null));
        var found = await _query.ListOfReferenceAsync("5932t173");
        var producao = Assert.Single(found);
        Assert.Equal("T173 · lote 77", producao.Bq);
        Assert.Null(producao.Cm);
    }

    // Diogo, 20/10/2026: «primeiro campo filtra apenas referências completas,
    // segundo aparece dropdown das produções 202601 202602». O início
    // («6018», «Y2», «0») fica para as sugestões por baixo do campo.
    [Fact]
    public async Task Procura_SoAReferenciaCompleta()
    {
        var machine = await SeedMachineAsync();
        _db.JobOns.Add(new JobOn { Reference = "6018Y232", ProductionNumber = "202616", MachineId = machine.Id });
        _db.JobOns.Add(new JobOn { Reference = "0132T173", ProductionNumber = "202617", MachineId = machine.Id });
        _db.JobOns.Add(new JobOn { Reference = "1075", ProductionNumber = "202618", MachineId = machine.Id });
        await _db.SaveChangesAsync();

        async Task<IEnumerable<string>> Numeros(string texto) =>
            (await _query.ListOfReferenceAsync(texto)).Select(p => p.ProductionNumber);
        Assert.Equal(["202616"], await Numeros(" 6018y232 "));
        Assert.Equal(["202617"], await Numeros("0132T173"));
        Assert.Equal(["202618"], await Numeros("1075"));
        Assert.Empty(await Numeros("6018"));
        Assert.Empty(await Numeros("y2"));
        Assert.Empty(await Numeros("0"));
        Assert.Empty(await Numeros("T17"));
        Assert.Empty(await Numeros("018"));
        Assert.Empty(await Numeros("232"));
        Assert.Empty(await Numeros("17"));
    }

    [Fact]
    public async Task Procura_AsUltimas5Producoes()
    {
        var machine = await SeedMachineAsync();
        for (var i = 1; i <= 12; i++)
        {
            _db.JobOns.Add(new JobOn { Reference = "6018Y232", ProductionNumber = $"2026{i:00}", MachineId = machine.Id });
            _db.JobOns.Add(new JobOn { Reference = $"6018R{i:00}", ProductionNumber = $"2027{i:00}", MachineId = machine.Id });
        }
        await _db.SaveChangesAsync();

        var found = await _query.ListOfReferenceAsync("6018Y232");
        Assert.Equal(5, IProductionPickerQuery.ProductionsOfReferenceLimit);
        Assert.Equal(IProductionPickerQuery.ProductionsOfReferenceLimit, found.Count);
        Assert.Equal("202612", found[0].ProductionNumber);
        Assert.Equal("202608", found[^1].ProductionNumber);
    }

    // Diogo, 26/10/2026: «deveria aparecer o mais recente primeiro» (as importadas
    // entraram fora de ordem: 202603, 202604, 202605, 202602, 202601).
    [Fact]
    public async Task Procura_AMaisRecentePrimeiro_PeloInicioEDepoisPeloNumero()
    {
        var machine = await SeedMachineAsync();
        foreach (var (numero, inicio) in new (string, DateOnly?)[]
                 {
                     ("202603", new DateOnly(2026, 3, 1)), ("202604", new DateOnly(2026, 4, 1)), ("202605", new DateOnly(2026, 5, 1)),
                     ("202602", new DateOnly(2026, 2, 1)), ("202601", null)
                 })
        {
            _db.JobOns.Add(new JobOn { Reference = "9262T288", ProductionNumber = numero, MachineId = machine.Id, InicioEm = inicio });
        }
        await _db.SaveChangesAsync();

        var found = await _query.ListOfReferenceAsync("9262T288");

        Assert.Equal(["202605", "202604", "202603", "202602", "202601"], found.Select(p => p.ProductionNumber));
    }
}

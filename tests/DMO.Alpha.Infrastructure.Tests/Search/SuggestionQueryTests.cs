using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Search;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Search;

/// <summary>
/// DMO-044 — as sugestões ao escrever: sem distinção de maiúsculas, no
/// máximo 10. As referências pelo início delas ou da parte BQ, a partir de 1
/// carácter (Diogo, 18/10/2026: «0» sugere a 0132T173); os reparadores por
/// parte do nome, a partir de 2.
/// </summary>
public sealed class SuggestionQueryTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly SuggestionQuery _query;

    public SuggestionQueryTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _query = new SuggestionQuery(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedProductionsAsync()
    {
        var machine = new Machine { Code = "ZC2" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        _db.JobOns.AddRange(
            new JobOn { Reference = "6018Y232", ProductionNumber = "202616", MachineId = machine.Id },
            new JobOn { Reference = "5932T173", ProductionNumber = "202601", MachineId = machine.Id },
            new JobOn { Reference = "5932T173", ProductionNumber = "202605", MachineId = machine.Id });
        await _db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyTerm_ReturnsNothing(string? term)
    {
        await SeedProductionsAsync();
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.Producoes, term));
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.Referencias, term));
    }

    [Fact]
    public async Task OneCharacter_IsEnough_ForReferences()
    {
        await SeedProductionsAsync();
        Assert.Equal(["5932T173"], (await _query.SuggestAsync(SuggestionSource.Referencias, " 5 ")).Select(s => s.Value));
    }

    private async Task SeedOwnerExamplesAsync()
    {
        var machine = new Machine { Code = "ZB1" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        foreach (var (reference, number) in new[]
        {
            ("0132T173", "2026001"), ("1075C142", "2026002"), ("6018Y232", "2026003"),
            ("5447T173", "2026004"), ("2459T035", "2026135"), ("3540Y079", "2026006"),
            ("4996T350", "2026007"), ("7018C002", "2026008"), ("1985T350", "2026009")
        })
        {
            _db.JobOns.Add(new JobOn { Reference = reference, ProductionNumber = number, MachineId = machine.Id });
        }
        await _db.SaveChangesAsync();
    }

    // A regra do Diogo (18/10/2026): o início da referência ou da parte BQ,
    // nunca um carácter a meio.
    [Theory]
    [InlineData("0", new[] { "0132T173" })]
    [InlineData("35", new[] { "3540Y079" })]
    [InlineData("y2", new[] { "6018Y232" })]
    [InlineData("Y2", new[] { "6018Y232" })]
    [InlineData("t17", new[] { "0132T173", "5447T173" })]
    [InlineData("018", new string[0])]
    [InlineData("232", new string[0])]
    [InlineData("02", new string[0])]
    public async Task Referencias_PeloInicioDaReferenciaOuDaParteBq(string term, string[] expected)
    {
        await SeedOwnerExamplesAsync();

        Assert.Equal(expected, (await _query.SuggestAsync(SuggestionSource.Referencias, term)).Select(s => s.Value));
    }

    [Fact]
    public async Task Producoes_UmNumeroAMeioNaoDevolveAReferencia()
    {
        await SeedOwnerExamplesAsync();

        // «0»: a 0132T173 pelo início da referência; os nºs 2026… não começam por 0.
        var zero = await _query.SuggestAsync(SuggestionSource.Producoes, "0");
        Assert.Equal("0132T173", Assert.Single(zero).Value);
        // «35»: a 3540Y079; nem a 4996T350 nem o nº 2026135 (35 a meio).
        Assert.Equal(["3540Y079"], (await _query.SuggestAsync(SuggestionSource.Producoes, "35")).Select(s => s.Value));
        // «02»: o 02 a meio do nº 2026002 e da 7018C002 não conta.
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.Producoes, "02"));
        // O nº pelo início: o valor é o nº.
        var numero = await _query.SuggestAsync(SuggestionSource.Producoes, "2026135");
        Assert.Equal("2026135", Assert.Single(numero).Value);
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.NumerosProducao, "135"));
    }

    [Fact]
    public async Task ReferenciasEBq_JuntaAsReferenciasDeProducaoEAsBq()
    {
        await SeedOwnerExamplesAsync();
        _db.Tools.AddRange(
            new Tool { ToolId = "bq-t173", Type = ToolType.Bq, Reference = "T173", Lot = "1" },
            new Tool { ToolId = "bq-t173b", Type = ToolType.Bq, Reference = "T173", Lot = "2" },
            new Tool { ToolId = "cm-t173", Type = ToolType.Cm, Reference = "T1730", Lot = "1" },
            new Tool { ToolId = "bq-y079", Type = ToolType.Bq, Reference = "Y079", Lot = "1" });
        await _db.SaveChangesAsync();

        // «3540»: a referência da produção (antes, só BQ: «Sem sugestões»).
        Assert.Equal(["3540Y079"], (await _query.SuggestAsync(SuggestionSource.ReferenciasEBq, "3540")).Select(s => s.Value));

        // «t17»: as referências de produção e a BQ T173 (uma vez, marcada BQ); a CM fica de fora.
        var t17 = await _query.SuggestAsync(SuggestionSource.ReferenciasEBq, "t17");
        Assert.Equal(["0132T173", "5447T173", "T173"], t17.Select(s => s.Value));
        Assert.Equal("BQ T173", t17[^1].Label);
        Assert.Equal("5447T173", t17[1].Label);

        // «17»: nem a parte BQ a meio, nem o 17 a meio da BQ.
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.ReferenciasEBq, "17"));
    }

    [Fact]
    public async Task ReferenciasRegisto_SempreAReferenciaToda_EACmComONomeOriginal()
    {
        await SeedOwnerExamplesAsync();
        _db.Tools.AddRange(
            new Tool { ToolId = "bq-1", Type = ToolType.Bq, Reference = "1075C142", Lot = "1" },
            new Tool { ToolId = "cm-st100", Type = ToolType.Cm, Reference = "ST100", AssociatedMfReference = "1075C142", Lot = "1" },
            new Tool { ToolId = "mf-5447", Type = ToolType.Mf, Reference = "5447T173", Lot = "1" });
        await _db.SaveChangesAsync();

        // Diogo, 19/10/2026: «C142» e «1075» mostram a referência toda, e escolher escreve-a toda.
        Assert.Equal(["1075C142"], (await _query.SuggestAsync(SuggestionSource.ReferenciasRegisto, "c142", ToolType.Bq)).Select(s => s.Value));
        Assert.Equal(["1075C142"], (await _query.SuggestAsync(SuggestionSource.ReferenciasRegisto, "1075", ToolType.Mf)).Select(s => s.Value));

        // Na CM, a ST100 da 1075C142 aparece com o nome original, pela ST100 ou pela 1075C142.
        var st = Assert.Single(await _query.SuggestAsync(SuggestionSource.ReferenciasRegisto, "st1", ToolType.Cm));
        Assert.Equal(("ST100", "ST100 · 1075C142"), (st.Value, st.Label));
        var cm = await _query.SuggestAsync(SuggestionSource.ReferenciasRegisto, "1075", ToolType.Cm);
        Assert.Equal(["ST100", "1075C142"], cm.Select(s => s.Value));

        // Nunca a meio.
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.ReferenciasRegisto, "17", ToolType.Bq));
    }

    [Fact]
    public async Task ReferenciasEBq_ComMuitasDasDuas_MetadeDeCada()
    {
        var machine = new Machine { Code = "ZB2" };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        for (var i = 0; i < 12; i++)
        {
            _db.JobOns.Add(new JobOn { Reference = $"10{i:00}T1{i:00}", ProductionNumber = $"P{i}", MachineId = machine.Id });
            _db.Tools.Add(new Tool { ToolId = $"bq-{i}", Type = ToolType.Bq, Reference = $"T1{i:00}", Lot = "1" });
        }
        await _db.SaveChangesAsync();

        var items = await _query.SuggestAsync(SuggestionSource.ReferenciasEBq, "T1");

        Assert.Equal(ISuggestionQuery.MaxResults, items.Count);
        Assert.Equal(ISuggestionQuery.MaxResults / 2, items.Count(s => s.Label.StartsWith("BQ ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Producoes_MatchReferenceIgnoringCase_ValueIsTheReference()
    {
        await SeedProductionsAsync();

        var items = await _query.SuggestAsync(SuggestionSource.Producoes, "y23");

        var item = Assert.Single(items);
        Assert.Equal("6018Y232", item.Value);
        Assert.Equal("6018Y232 · 202616 · ZC2", item.Label);
    }

    [Fact]
    public async Task Producoes_MatchProductionNumber_ValueIsTheNumber()
    {
        await SeedProductionsAsync();

        var items = await _query.SuggestAsync(SuggestionSource.Producoes, "202605");

        Assert.Equal("202605", Assert.Single(items).Value);
    }

    [Fact]
    public async Task Referencias_AreDistinct()
    {
        await SeedProductionsAsync();

        var items = await _query.SuggestAsync(SuggestionSource.Referencias, "t17");

        Assert.Equal("5932T173", Assert.Single(items).Value);
    }

    [Fact]
    public async Task NumerosProducao_IgnoreReferences()
    {
        await SeedProductionsAsync();

        Assert.Empty(await _query.SuggestAsync(SuggestionSource.NumerosProducao, "5932"));
        Assert.Equal(3, (await _query.SuggestAsync(SuggestionSource.NumerosProducao, "2026")).Count);
    }

    [Fact]
    public async Task Ferramentas_FilterByType_ExcludeRemoved_AtMostTen()
    {
        for (var i = 0; i < 12; i++)
        {
            _db.Tools.Add(new Tool { ToolId = $"cm-{i}", Type = ToolType.Cm, Reference = $"54{i:D2}", Lot = "1" });
        }
        _db.Tools.Add(new Tool { ToolId = "bq-1", Type = ToolType.Bq, Reference = "T194", Lot = "1" });
        _db.Tools.Add(new Tool { ToolId = "bq-2", Type = ToolType.Bq, Reference = "T194", Lot = "2" });
        if (!await _db.Tools.AnyAsync(t => t.ToolId == ToolRegistryConstants.RemovedToolId))
        {
            _db.Tools.Add(new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Bq, Reference = "T19REMOVED", Lot = "0" });
        }
        await _db.SaveChangesAsync();

        Assert.Equal(ISuggestionQuery.MaxResults, (await _query.SuggestAsync(SuggestionSource.Ferramentas, "54")).Count);

        var bq = await _query.SuggestAsync(SuggestionSource.Ferramentas, "t19", ToolType.Bq);
        var item = Assert.Single(bq);
        Assert.Equal("T194", item.Value);
        Assert.Equal("BQ T194", item.Label);
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.Ferramentas, "t19", ToolType.Cm));
        // O início da referência: «19» já não encontra a T194 pelo meio, e
        // «0» não encontra as 54xx com um 0 no meio.
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.Ferramentas, "19", ToolType.Bq));
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.Ferramentas, "0"));
    }

    [Fact]
    public async Task Reparadores_MatchName()
    {
        _db.BqRepairers.AddRange(new BqRepairer { Name = "Vidroart" }, new BqRepairer { Name = "Moldes Lda" });
        await _db.SaveChangesAsync();

        Assert.Equal("Vidroart", Assert.Single(await _query.SuggestAsync(SuggestionSource.Reparadores, "VIDRO")).Value);
        Assert.Equal("Vidroart", Assert.Single(await _query.SuggestAsync(SuggestionSource.Reparadores, "dro")).Value);
        // Os nomes procuram-se a meio, por isso continuam a pedir 2 caracteres.
        Assert.Empty(await _query.SuggestAsync(SuggestionSource.Reparadores, "v"));
    }
}

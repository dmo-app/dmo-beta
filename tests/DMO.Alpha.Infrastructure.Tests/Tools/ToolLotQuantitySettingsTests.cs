using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

public sealed class ToolLotQuantitySettingsTests : IDisposable
{
    private readonly DmoDbContext _db = new(new DbContextOptionsBuilder<DmoDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RepeatedMachineCode_ListsOneRow_SavesAndSuggestsForBoth()
    {
        // O código da máquina não é único na base. Antes, duas máquinas «B1»
        // rebentavam a lista e a gravação com uma chave repetida (500 nas
        // Definições).
        var first = new Machine { Code = "B1" };
        var second = new Machine { Code = "B1" };
        _db.Machines.AddRange(first, second);
        await _db.SaveChangesAsync();
        var settings = new ToolLotQuantitySettings(_db);

        var list = await settings.ListAsync();
        Assert.Single(list, d => d.Type == ToolType.Bq && d.MachineCode == "B1");

        var saved = await settings.SaveAsync(new Dictionary<string, string?> { [ToolLotQuantityKeys.For(ToolType.Bq, "B1")] = "120" });
        Assert.Equal(SaveToolLotQuantitiesResult.Success, saved.Result);
        Assert.Equal(120, (await settings.SuggestAsync(ToolType.Bq, new[] { first.Id })).Quantity);
        Assert.Equal(120, (await settings.SuggestAsync(ToolType.Bq, new[] { second.Id })).Quantity);
        Assert.Equal(120, (await settings.SuggestAsync(ToolType.Bq, new[] { first.Id, second.Id })).Quantity);
    }

    [Fact]
    public async Task Defaults_AreIndependentByTypeAndMachine_ConflictsNeedChoice_ManualValueWins()
    {
        var b1 = new Machine { Code = "B1" };
        var c3 = new Machine { Code = "C3" };
        _db.Machines.AddRange(b1, c3);
        await _db.SaveChangesAsync();
        var settings = new ToolLotQuantitySettings(_db);
        var saved = await settings.SaveAsync(new Dictionary<string, string?>
        {
            [ToolLotQuantityKeys.For(ToolType.Bq, "B1")] = "120",
            [ToolLotQuantityKeys.For(ToolType.Bq, "C3")] = "90",
            [ToolLotQuantityKeys.For(ToolType.Cm, "B1")] = "24"
        });
        Assert.Equal(SaveToolLotQuantitiesResult.Success, saved.Result);
        Assert.Empty(saved.Ignored);
        Assert.Equal(120, (await settings.SuggestAsync(ToolType.Bq, new[] { b1.Id })).Quantity);
        Assert.Equal(24, (await settings.SuggestAsync(ToolType.Cm, new[] { b1.Id })).Quantity);
        Assert.Null((await settings.SuggestAsync(ToolType.Mf, new[] { b1.Id })).Quantity);
        Assert.Null((await settings.SuggestAsync(ToolType.Bq, new[] { b1.Id, c3.Id })).Quantity);
        var create = new CreateToolHandler(_db, settings);
        var automatic = await create.HandleAsync(new CreateTool("BQ", "Y232", "1", MachineIds: new[] { b1.Id }));
        var manual = await create.HandleAsync(new CreateTool("BQ", "Y232", "2", BqAccountedQuantity: 110, MachineIds: new[] { b1.Id }));
        var conflict = await create.HandleAsync(new CreateTool("BQ", "Y232", "3", MachineIds: new[] { b1.Id, c3.Id }));
        var cleared = await create.HandleAsync(new CreateTool("BQ", "Y232", "4", MachineIds: new[] { b1.Id }, UseLotQuantityDefault: false));
        Assert.Equal(120, (await _db.Tools.FindAsync(automatic.Tool!.ToolId))!.BqAccountedQuantity);
        Assert.Equal(110, (await _db.Tools.FindAsync(manual.Tool!.ToolId))!.BqAccountedQuantity);
        Assert.Null((await _db.Tools.FindAsync(conflict.Tool!.ToolId))!.BqAccountedQuantity);
        Assert.Null((await _db.Tools.FindAsync(cleared.Tool!.ToolId))!.BqAccountedQuantity);
        await settings.SaveAsync(new Dictionary<string, string?> { [ToolLotQuantityKeys.For(ToolType.Bq, "B1")] = "130" });
        // Mudar a definição não mexe na quantidade de um lote já registado
        // (registá-lo outra vez é recusado: ferramenta repetida, 18/10/2026).
        await RegistosAntigos.EnvelhecerAsync(_db);
        var existing = await create.HandleAsync(new CreateTool("BQ", "Y232", "1", MachineIds: new[] { b1.Id }));
        Assert.Equal(CreateToolResult.ToolAlreadyExists, existing.Result);
        Assert.Equal(120, (await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == automatic.Tool.ToolId)).BqAccountedQuantity);
    }

    [Fact]
    public async Task BadQuantities_AreIgnoredWithWarning_ValidOnesSave_EmptyClearsOnlyTheSubmittedDefinition()
    {
        // Decisão do proprietário (16/10/2026): uma quantidade má não recusa
        // as outras. A ilegível e a negativa ficam de fora com o valor
        // anterior; as válidas gravam; o resultado diz quais foram ignoradas.
        _db.Machines.Add(new Machine { Code = "B1" });
        await _db.SaveChangesAsync();
        var settings = new ToolLotQuantitySettings(_db);
        var cm = ToolLotQuantityKeys.For(ToolType.Cm, "B1");
        var mf = ToolLotQuantityKeys.For(ToolType.Mf, "B1");
        var bq = ToolLotQuantityKeys.For(ToolType.Bq, "B1");
        await settings.SaveAsync(new Dictionary<string, string?> { [cm] = "24", [mf] = "6", [bq] = "120" });

        var partial = await settings.SaveAsync(new Dictionary<string, string?> { [cm] = "30", [mf] = "-4", [bq] = "bad" });

        Assert.Equal(SaveToolLotQuantitiesResult.Success, partial.Result);
        Assert.Equal("30", (await _db.ModuleSettings.FindAsync(cm))!.SettingValue);
        Assert.Equal("6", (await _db.ModuleSettings.FindAsync(mf))!.SettingValue);
        Assert.Equal("120", (await _db.ModuleSettings.FindAsync(bq))!.SettingValue);
        Assert.Equal(2, partial.Ignored.Count);
        Assert.Contains(partial.Ignored, i => i.Type == ToolType.Mf && i.MachineCode == "B1" && i.Submitted == "-4");
        Assert.Contains(partial.Ignored, i => i.Type == ToolType.Bq && i.MachineCode == "B1" && i.Submitted == "bad");

        // Uma definição de máquina inexistente continua a recusar tudo.
        var unknown = await settings.SaveAsync(new Dictionary<string, string?> { [cm] = "31", ["Boquilhas:DayChangeHour"] = "2" });
        Assert.Equal(SaveToolLotQuantitiesResult.UnknownDefinition, unknown.Result);
        Assert.Equal("30", (await _db.ModuleSettings.FindAsync(cm))!.SettingValue);

        await settings.SaveAsync(new Dictionary<string, string?> { [cm] = "" });
        Assert.Null(await _db.ModuleSettings.FindAsync(cm));
        Assert.Equal("120", (await _db.ModuleSettings.FindAsync(bq))!.SettingValue);
    }

    // Teste do agente de 19/10/2026 (T2945d11): «Criar sem “Usar sugestão”»
    // com B1 (120) e uma máquina sem total: a quantidade vem preenchida.
    [Fact]
    public async Task Suggestion_IgnoresAMachineWithoutTotal_ButNotDifferentTotals()
    {
        var b1 = new Machine { Code = "B1" };
        var b2 = new Machine { Code = "B2" };
        var c3 = new Machine { Code = "C3" };
        _db.Machines.AddRange(b1, b2, c3);
        await _db.SaveChangesAsync();
        var settings = new ToolLotQuantitySettings(_db);
        await settings.SaveAsync(new Dictionary<string, string?>
        {
            [ToolLotQuantityKeys.For(ToolType.Bq, "B1")] = "120",
            [ToolLotQuantityKeys.For(ToolType.Bq, "C3")] = "90"
        });

        var withMissing = await settings.SuggestAsync(ToolType.Bq, new[] { b1.Id, b2.Id });
        Assert.Equal(120, withMissing.Quantity);
        Assert.Contains(withMissing.Machines, m => m.MachineCode == "B2" && m.Quantity is null);
        Assert.Null((await settings.SuggestAsync(ToolType.Bq, new[] { b2.Id })).Quantity);
        Assert.Null((await settings.SuggestAsync(ToolType.Bq, new[] { b1.Id, b2.Id, c3.Id })).Quantity);
        var created = await new CreateToolHandler(_db, settings)
            .HandleAsync(new CreateTool("BQ", "Y233", "1", MachineIds: new[] { b1.Id, b2.Id }));
        Assert.Equal(120, (await _db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == created.Tool!.ToolId)).BqAccountedQuantity);
    }
}


using System.IO;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// A gravação das definições POR MÓDULO: a hora de viragem das Boquilhas (um
/// inteiro 0–23 — o dia operacional conta dessa hora, nunca 0h); o upsert
/// por chave canónica é idempotente.
/// </summary>
public sealed class SaveModuleSettingHandlerTests
{
    private static DmoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-save-module-setting-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new DmoDbContext(options);
    }

    [Fact]
    public async Task Save_RetiredControloOutputDirectoryKey_IsUnknown_AndNeverTouchesTheDisk()
    {
        // A chave antiga Controlo:OutputDirectory foi retirada: os PDFs são
        // gerados em memória e descarregados pelo utilizador, por isso o
        // servidor já não valida directórios. Linhas antigas na tabela são
        // inofensivas (ninguém as lê).
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        var missing = Path.Combine(Path.GetTempPath(), "dmo-missing-" + Guid.NewGuid().ToString("N"));
        var outcome = await handler.HandleAsync(
            new SaveModuleSetting("Controlo:OutputDirectory", missing));

        Assert.Equal(SaveModuleSettingResult.UnknownSettingKey, outcome.Result);
        Assert.False(Directory.Exists(missing));
        Assert.Empty(await db.ModuleSettings.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Save_BoquilhasDayChangeHour_ValidHour_Saves()
    {
        // A hora de viragem do Owner: «se meter as 8, às 8 da manhã o
        // módulo boquilhas passa a mostrar T173 na B1» — um inteiro 0–23.
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        var outcome = await handler.HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.BoquilhasDayChangeHour, "8"));

        Assert.Equal(SaveModuleSettingResult.Success, outcome.Result);
        var stored = await db.ModuleSettings.AsNoTracking()
            .SingleAsync(s => s.SettingKey == ModuleSettingKeys.BoquilhasDayChangeHour);
        Assert.Equal("8", stored.SettingValue);
    }

    [Theory]
    [InlineData("24")]
    [InlineData("-1")]
    [InlineData("8h")]
    [InlineData("manhã")]
    public async Task Save_BoquilhasDayChangeHour_InvalidValue_Refuses(string value)
    {
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        var outcome = await handler.HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.BoquilhasDayChangeHour, value));

        Assert.Equal(SaveModuleSettingResult.BoquilhasInvalidHour, outcome.Result);
    }

    [Fact]
    public async Task Save_UnknownKey_Refuses()
    {
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        var outcome = await handler.HandleAsync(new SaveModuleSetting("ModuloInexistente:Chave", "x"));

        Assert.Equal(SaveModuleSettingResult.UnknownSettingKey, outcome.Result);
    }

    // ------------------------------------------------------------------
    // O MAPA DO ENCAMINHAMENTO POR MÁQUINA (Boquilhas:RepairerB1..C3):
    // o valor é o Id de uma ENTIDADE REPARADORA existente; o valor
    // VAZIO LIMPA a rota (a máquina sem reparador — estado válido); o Id
    // que não resolve é recusa tipada.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Save_MachineRoute_ExistingRepairerId_Saves()
    {
        // A ROTA por máquina: B1 → o Id do reparador (o dropdown da Admin
        // oferece apenas os existentes).
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);
        var repairer = new DMO.Alpha.Core.Boquilhas.BqRepairer { Name = "Vidroart" };
        db.BqRepairers.Add(repairer);
        await db.SaveChangesAsync();

        var outcome = await handler.HandleAsync(
            new SaveModuleSetting(
                DMO.Alpha.Core.Boquilhas.BqRepairerRouting.SettingKeyFor("B1"),
                repairer.Id.ToString()));

        Assert.Equal(SaveModuleSettingResult.Success, outcome.Result);
        var stored = await db.ModuleSettings.AsNoTracking()
            .SingleAsync(s => s.SettingKey == "Boquilhas:RepairerB1");
        Assert.Equal(repairer.Id.ToString(), stored.SettingValue);
    }

    [Fact]
    public async Task Save_MachineRoute_EmptyValue_ClearsTheRoute()
    {
        // O VALOR VAZIO LIMPA a rota da máquina (o «— sem reparador —» do
        // mapa): a saída de uma máquina sem rota grava na mesma, sem
        // reparador — truthful, a app nunca bloqueia.
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);
        var repairer = new DMO.Alpha.Core.Boquilhas.BqRepairer { Name = "Vidroart" };
        db.BqRepairers.Add(repairer);
        await db.SaveChangesAsync();
        await handler.HandleAsync(new SaveModuleSetting(
            DMO.Alpha.Core.Boquilhas.BqRepairerRouting.SettingKeyFor("C3"),
            repairer.Id.ToString()));

        var cleared = await handler.HandleAsync(new SaveModuleSetting(
            DMO.Alpha.Core.Boquilhas.BqRepairerRouting.SettingKeyFor("C3"),
            string.Empty));

        Assert.Equal(SaveModuleSettingResult.Success, cleared.Result);
        Assert.Empty(await db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == "Boquilhas:RepairerC3").ToListAsync());

        // Limpar o que já está limpo é idempotente — nunca bloqueia.
        var again = await handler.HandleAsync(new SaveModuleSetting(
            DMO.Alpha.Core.Boquilhas.BqRepairerRouting.SettingKeyFor("C3"),
            "  "));
        Assert.Equal(SaveModuleSettingResult.Success, again.Result);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0,5")]
    public async Task Save_MachineRoute_MalformedValue_RefusesTyped(string value)
    {
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        var outcome = await handler.HandleAsync(new SaveModuleSetting(
            DMO.Alpha.Core.Boquilhas.BqRepairerRouting.SettingKeyFor("B2"), value));

        Assert.Equal(SaveModuleSettingResult.BoquilhasInvalidRepairerId, outcome.Result);
        Assert.Empty(await db.ModuleSettings.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Save_MachineRoute_MissingRepairerId_RefusesTyped()
    {
        // O Id que não resolve a nenhuma entidade (a entidade desapareceu
        // entre o GET e o POST): recusa tipada, informativa — o operador
        // volta a escolher um existente.
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        var outcome = await handler.HandleAsync(new SaveModuleSetting(
            DMO.Alpha.Core.Boquilhas.BqRepairerRouting.SettingKeyFor("B2"), "9999"));

        Assert.Equal(SaveModuleSettingResult.BoquilhasInvalidRepairerId, outcome.Result);
        Assert.Empty(await db.ModuleSettings.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Save_MachineRoute_MachineOutsideTheSixCodes_RefusesUnknownKey()
    {
        // O vocabulário fechado das 6 máquinas (B1..C3): uma chave fora
        // dele (B4) é UnknownSettingKey — nunca texto livre nas chaves.
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        var outcome = await handler.HandleAsync(new SaveModuleSetting(
            "Boquilhas:RepairerB4", "1"));

        Assert.Equal(SaveModuleSettingResult.UnknownSettingKey, outcome.Result);
    }

    [Fact]
    public async Task Save_Twice_Upserts_TheSameKey()
    {
        using var db = CreateContext();
        var handler = new SaveModuleSettingHandler(db);

        await handler.HandleAsync(new SaveModuleSetting(ModuleSettingKeys.BoquilhasDayChangeHour, "8"));
        await handler.HandleAsync(new SaveModuleSetting(ModuleSettingKeys.BoquilhasDayChangeHour, "6"));

        var rows = await db.ModuleSettings.AsNoTracking().ToListAsync();
        var setting = Assert.Single(rows);
        Assert.Equal("6", setting.SettingValue);
    }
}

/// <summary>
/// O DIA OPERACIONAL das Boquilhas (a decisão do Owner 2026-10-11: «não à
/// meia-noite... se meter as 8, às 8 da manhã o módulo boquilhas passa a
/// mostrar T173 na B1»): o dia começa à hora de viragem — antes dela
/// ainda é o dia operacional de ONTEM.
/// </summary>
public sealed class BoquilhasDayChangeTests
{
    [Fact]
    public void BeforeDayChangeHour_StillYesterdaysOperationalDay()
    {
        // 16/10 às 07:59, viragem às 8 → o dia operacional ainda é 15/10:
        // a B1 continua a mostrar a referência ANTERIOR até às 8.
        var day = BoquilhasDayChange.ResolveOperationalDay(
            new DateTime(2026, 10, 16, 7, 59, 0), dayChangeHour: 8);

        Assert.Equal(new DateOnly(2026, 10, 15), day);
    }

    [Fact]
    public void AtDayChangeHour_TheNewDayBegins()
    {
        // 16/10 às 08:00, viragem às 8 → o dia operacional passa a 16/10:
        // a B1 passa a mostrar a 5447T173.
        var day = BoquilhasDayChange.ResolveOperationalDay(
            new DateTime(2026, 10, 16, 8, 0, 0), dayChangeHour: 8);

        Assert.Equal(new DateOnly(2026, 10, 16), day);
    }

    [Fact]
    public void LateNight_BelongsToTheSameOperationalDay()
    {
        // 17/10 às 03:00, viragem às 8 → o dia operacional ainda é 16/10
        // (a noite pertence ao dia anterior — nunca à meia-noite).
        var day = BoquilhasDayChange.ResolveOperationalDay(
            new DateTime(2026, 10, 17, 3, 0, 0), dayChangeHour: 8);

        Assert.Equal(new DateOnly(2026, 10, 16), day);
    }

    [Fact]
    public void Midnight_WithZeroChangeHour_BehavesAsCalendarDay()
    {
        // A hora 0 é válida (a fábrica pode querer o dia natural) — a
        // meia-noite o dia muda.
        var day = BoquilhasDayChange.ResolveOperationalDay(
            new DateTime(2026, 10, 17, 0, 0, 0), dayChangeHour: 0);

        Assert.Equal(new DateOnly(2026, 10, 17), day);
    }
}

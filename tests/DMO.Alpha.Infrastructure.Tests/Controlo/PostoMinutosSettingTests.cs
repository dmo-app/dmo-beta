using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Os minutos do posto da Reparação Interna (Diogo, 27/10/2026: «3min auto e
/// opções nas definições para aumentar»): só se grava uma das opções do
/// select; sem nada gravado, ou com um valor estranho, ficam os 3 minutos.
/// </summary>
public sealed class PostoMinutosSettingTests
{
    private static DmoDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-posto-minutos-" + Guid.NewGuid().ToString("N"))
            .Options);

    [Theory]
    [InlineData("3")]
    [InlineData("10")]
    [InlineData("30")]
    public async Task UmaDasOpcoes_Grava(string minutos)
    {
        using var db = CreateContext();
        var outcome = await new SaveModuleSettingHandler(db).HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.ReparacaoInternaPostoMinutos, minutos));

        Assert.Equal(SaveModuleSettingResult.Success, outcome.Result);
        var gravado = await db.ModuleSettings.AsNoTracking()
            .SingleAsync(s => s.SettingKey == ModuleSettingKeys.ReparacaoInternaPostoMinutos);
        Assert.Equal(minutos, gravado.SettingValue);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("600")]
    [InlineData("-3")]
    [InlineData("três")]
    public async Task ForaDasOpcoes_ERecusado(string minutos)
    {
        using var db = CreateContext();
        var outcome = await new SaveModuleSettingHandler(db).HandleAsync(
            new SaveModuleSetting(ModuleSettingKeys.ReparacaoInternaPostoMinutos, minutos));

        Assert.Equal(SaveModuleSettingResult.PostoMinutosInvalidos, outcome.Result);
        Assert.Empty(await db.ModuleSettings.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("", 3)]
    [InlineData("7", 3)]
    [InlineData("abc", 3)]
    [InlineData("5", 5)]
    [InlineData(" 15 ", 15)]
    public void OsMinutosLidos_SemValorValidoSao3(string? valor, int esperado) =>
        Assert.Equal(esperado, PostoRiRegras.Minutos(valor));
}

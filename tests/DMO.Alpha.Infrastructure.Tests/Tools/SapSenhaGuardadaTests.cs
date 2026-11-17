using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

/// <summary>
/// Diogo, 02/11/2026 (DMO-705): a «Senha do SAP» dizia «guardada» e não estava
/// (a senha cifrada com outra chave não se lê). «guardada» só quando se lê mesmo.
/// </summary>
public sealed class SapSenhaGuardadaTests
{
    private static DmoDbContext NovaBase() => new(new DbContextOptionsBuilder<DmoDbContext>()
        .UseInMemoryDatabase("dmo-sap-senha-" + Guid.NewGuid().ToString("N"))
        .Options);

    [Fact]
    public async Task ComASenhaLegivel_DizGuardada()
    {
        await using var db = NovaBase();
        var sap = new SapDefinicoes(db, new EphemeralDataProtectionProvider());

        Assert.Equal(SapGravacaoOk(), await sap.GravarAsync("USERX", "Ab9zQ", 18, 120));

        Assert.True((await sap.LerAsync()).TemSenha);
    }

    [Fact]
    public async Task ComASenhaIlegivel_NaoDizGuardada()
    {
        await using var db = NovaBase();
        await new SapDefinicoes(db, new EphemeralDataProtectionProvider()).GravarAsync("USERX", "Ab9zQ", 18, 120);

        // Outra chave de cifra (outra instalação): a senha gravada já não se lê.
        var outra = new SapDefinicoes(db, new EphemeralDataProtectionProvider());

        Assert.False((await outra.LerAsync()).TemSenha);
        Assert.Null(await outra.SenhaAsync());
    }

    [Fact]
    public async Task SemSenha_NaoDizGuardada()
    {
        await using var db = NovaBase();
        var sap = new SapDefinicoes(db, new EphemeralDataProtectionProvider());
        await sap.GravarAsync("USERX", null, 18, 120);

        Assert.False((await sap.LerAsync()).TemSenha);
    }

    private static DMO.Alpha.Core.Tools.SapGravacao SapGravacaoOk() => DMO.Alpha.Core.Tools.SapGravacao.Gravado;
}

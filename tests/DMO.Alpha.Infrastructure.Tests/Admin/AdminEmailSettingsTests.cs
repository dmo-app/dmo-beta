using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// O primeiro login por email só reivindica o campo do ADMIN quando está
/// VAZIO: um segundo login (ou um login simultâneo que perdeu a corrida)
/// nunca substitui o email já gravado.
/// </summary>
public sealed class AdminEmailSettingsTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly AdminEmailSettings _settings;

    public AdminEmailSettingsTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _settings = new AdminEmailSettings(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task TryClaim_EmptyField_ClaimsIt()
    {
        Assert.True(await _settings.TryClaimAdminEmailAsync(" admin@example.test "));
        Assert.Equal("admin@example.test", await _settings.GetAdminEmailAsync());
    }

    [Fact]
    public async Task TryClaim_FieldAlreadySet_NeverReplacesIt()
    {
        Assert.True(await _settings.TryClaimAdminEmailAsync("admin@example.test"));

        Assert.False(await _settings.TryClaimAdminEmailAsync("intruso@example.test"));
        Assert.Equal("admin@example.test", await _settings.GetAdminEmailAsync());
        Assert.Single(_db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == ModuleSettingKeys.AdminEmail));
    }

    [Fact]
    public async Task TryClaim_InvalidEmail_ClaimsNothing()
    {
        Assert.False(await _settings.TryClaimAdminEmailAsync("sem-arroba"));
        Assert.Null(await _settings.GetAdminEmailAsync());
    }

    [Fact]
    public async Task Save_SoUmEmailComConta_OAtualOuODeUmUtilizador()
    {
        // Sweep de 19/10/2026, AD-1: um email sem conta deixava toda a gente
        // fora do Admin. Aceita-se o ADMIN atual e os utilizadores, sem
        // distinguir maiúsculas nem espaços, como o login.
        Assert.True(await _settings.TryClaimAdminEmailAsync("admin@example.test"));
        _db.Users.Add(new DMO.Alpha.Core.Identity.User
        {
            Id = Guid.NewGuid(),
            Name = "Colega",
            OperatorId = "4800",
            ProviderUserId = "provider-colega",
            Email = " Colega@Example.test "
        });
        await _db.SaveChangesAsync();

        Assert.Equal(DMO.Alpha.Core.Admin.SaveAdminEmailResult.NoAccount,
            await _settings.SaveAdminEmailAsync("intruso@example.test"));
        Assert.Equal("admin@example.test", await _settings.GetAdminEmailAsync());

        Assert.Equal(DMO.Alpha.Core.Admin.SaveAdminEmailResult.Success,
            await _settings.SaveAdminEmailAsync("ADMIN@example.test"));
        Assert.Equal("ADMIN@example.test", await _settings.GetAdminEmailAsync());

        Assert.Equal(DMO.Alpha.Core.Admin.SaveAdminEmailResult.Success,
            await _settings.SaveAdminEmailAsync("colega@example.test "));
        Assert.Equal("colega@example.test", await _settings.GetAdminEmailAsync());
    }
}

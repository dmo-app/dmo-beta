using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Infrastructure.Authentication.Local;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DMO.Alpha.Infrastructure.Tests.Authentication;

/// <summary>
/// O login próprio da app (modo Local, sem Supabase): criar, entrar, mudar,
/// repor e apagar, com as palavras-passe só cifradas na base.
/// </summary>
public sealed class LocalAuthenticationProviderTests
{
    private static LocalAuthenticationProvider NewProvider(out DmoDbContext db)
    {
        db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        return new LocalAuthenticationProvider(db, NullLogger<LocalAuthenticationProvider>.Instance);
    }

    [Fact]
    public async Task Criar_EntraComATemporariaEPedeMudanca()
    {
        var provider = NewProvider(out var db);

        var created = await provider.CreateAsync("Ana@Fabrica.pt", "1234");
        var login = await provider.AuthenticateAsync("ana@fabrica.pt", "1234");

        Assert.Equal(UserProvisioningCreateStatus.Success, created.Status);
        Assert.True(login.Success);
        Assert.Equal(created.ProviderUserId, login.ProviderUserId);
        Assert.True(login.IsTemporaryPassword);
        Assert.DoesNotContain("1234", db.LocalCredentials.Single().PasswordHash);
    }

    [Fact]
    public async Task PalavraPasseErrada_OuEmailDesconhecido_Falha()
    {
        var provider = NewProvider(out _);
        await provider.CreateAsync("ana@fabrica.pt", "1234");

        Assert.False((await provider.AuthenticateAsync("ana@fabrica.pt", "4321")).Success);
        Assert.False((await provider.AuthenticateAsync("rui@fabrica.pt", "1234")).Success);
    }

    [Fact]
    public async Task EmailRepetido_EhRecusado()
    {
        var provider = NewProvider(out _);
        await provider.CreateAsync("ana@fabrica.pt", "1234");

        var again = await provider.CreateAsync("ANA@fabrica.pt", "9999");

        Assert.Equal(UserProvisioningCreateStatus.EmailExists, again.Status);
    }

    [Fact]
    public async Task Mudar_TiraATemporariaEAAntigaDeixaDeServir()
    {
        var provider = NewProvider(out _);
        var id = (await provider.CreateAsync("ana@fabrica.pt", "1234")).ProviderUserId!;

        Assert.Equal(ChangePasswordResult.Success, await provider.ChangePasswordAsync(id, "nova-palavra"));
        var login = await provider.AuthenticateAsync("ana@fabrica.pt", "nova-palavra");

        Assert.True(login.Success);
        Assert.False(login.IsTemporaryPassword);
        Assert.False((await provider.AuthenticateAsync("ana@fabrica.pt", "1234")).Success);
    }

    [Fact]
    public async Task Repor_VoltaATemporaria()
    {
        var provider = NewProvider(out _);
        var id = (await provider.CreateAsync("ana@fabrica.pt", "1234")).ProviderUserId!;
        await provider.ChangePasswordAsync(id, "nova-palavra");

        var reset = await provider.ResetAsync(id, "5678");
        var login = await provider.AuthenticateAsync("ana@fabrica.pt", "5678");

        Assert.Equal(UserProvisioningResetStatus.Success, reset.Status);
        Assert.True(login.IsTemporaryPassword);
        Assert.Equal(UserProvisioningResetStatus.IdentityNotFound, (await provider.ResetAsync("nao-existe", "5678")).Status);
    }

    [Fact]
    public async Task Apagar_TiraOAcesso()
    {
        var provider = NewProvider(out _);
        var id = (await provider.CreateAsync("ana@fabrica.pt", "1234")).ProviderUserId!;

        Assert.Equal(UserProvisioningDeleteStatus.Success, (await provider.DeleteAsync(id)).Status);
        Assert.False((await provider.AuthenticateAsync("ana@fabrica.pt", "1234")).Success);
        Assert.False(await provider.IdentityExistsAsync(id));
        Assert.Equal(UserProvisioningDeleteStatus.IdentityNotFound, (await provider.DeleteAsync(id)).Status);
    }

    [Fact]
    public void Cifra_TemSalDiferenteEVerificaSoACerta()
    {
        var a = LocalPasswordHasher.Hash("1234");
        var b = LocalPasswordHasher.Hash("1234");

        Assert.NotEqual(a, b);
        Assert.True(LocalPasswordHasher.Verify("1234", a));
        Assert.False(LocalPasswordHasher.Verify("1235", a));
        Assert.False(LocalPasswordHasher.Verify("1234", "lixo"));
    }

    [Fact]
    public async Task CincoFalhasSeguidas_BloqueiamMesmoComACerta_EDepoisVoltaAEntrar()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var relogio = new Relogio();
        var provider = new LocalAuthenticationProvider(db, NullLogger<LocalAuthenticationProvider>.Instance, relogio);
        await provider.CreateAsync("ana@fabrica.pt", "1234");

        for (var i = 0; i < 5; i++)
        {
            await provider.AuthenticateAsync("ana@fabrica.pt", "errada");
        }

        Assert.False((await provider.AuthenticateAsync("ana@fabrica.pt", "1234")).Success);

        relogio.Agora += TimeSpan.FromSeconds(31);
        Assert.True((await provider.AuthenticateAsync("ana@fabrica.pt", "1234")).Success);
        Assert.Equal(0, db.LocalCredentials.Single().FailedAttempts);
    }

    [Fact]
    public void Espera_DobraAteQuinzeMinutos()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), LocalAuthenticationProvider.LockFor(5));
        Assert.Equal(TimeSpan.FromSeconds(60), LocalAuthenticationProvider.LockFor(6));
        Assert.Equal(TimeSpan.FromMinutes(15), LocalAuthenticationProvider.LockFor(40));
    }

    [Fact]
    public async Task PalavraPasseDoSupabase_Entra_EFicaCifradaNoFormatoDaApp()
    {
        var provider = NewProvider(out var db);
        db.LocalCredentials.Add(new LocalCredential
        {
            ProviderUserId = "11111111-2222-3333-4444-555555555555",
            Email = "rui@fabrica.pt",
            // Como o Supabase guarda: bcrypt da palavra-passe com o prefixo das curtas.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Dmo#1:4821", 4),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        Assert.True((await provider.AuthenticateAsync("rui@fabrica.pt", "4821")).Success);
        Assert.StartsWith("pbkdf2-sha256$", db.LocalCredentials.Single().PasswordHash);
        Assert.True((await provider.AuthenticateAsync("rui@fabrica.pt", "4821")).Success);
        Assert.False((await provider.AuthenticateAsync("rui@fabrica.pt", "4822")).Success);
    }
}

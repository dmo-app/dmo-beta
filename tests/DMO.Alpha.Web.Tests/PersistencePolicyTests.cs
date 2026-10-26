using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Authentication;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Online BETA: the database is an application deployment setting.
/// No Setup wizard or installation-database file chooses the active database.
/// </summary>
public sealed class PersistencePolicyTests
{
    private static string TempKeyRing() =>
        Path.Combine(Path.GetTempPath(), "dmo-alpha-web-tests", Guid.NewGuid().ToString("N"));

    private static WebApplicationFactory<Program> CreateUnconfiguredFactory(
        string environment, string provider) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Dmo:AuthenticationProvider", provider);
            builder.UseSetting("ConnectionStrings:DmoDatabase", "");
            builder.UseSetting("Dmo:DataProtectionKeyRingPath", TempKeyRing());
            builder.UseSetting("Dmo:DevConnectionProbe", "false");
            builder.UseSetting("Dmo:SupabaseUrl", "");
            builder.UseSetting("Dmo:SupabasePublishableKey", "");
            builder.UseSetting("Dmo:SupabaseSecretKey", "");
        });

    [Theory]
    [InlineData("Production", "")]
    [InlineData("Production", "Local")]
    [InlineData("Staging", "")]
    public void NonDevelopment_SemFornecedorOuLocal_UsaOLoginProprio(
        string environment, string provider)
    {
        // O servidor do portátil (26/10/2026): sem escolha, o login é o da app.
        using var factory = CreateUnconfiguredFactory(environment, provider);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        Assert.IsType<DMO.Alpha.Infrastructure.Authentication.Local.LocalAuthenticationProvider>(
            scope.ServiceProvider.GetRequiredService<IAuthenticationProvider>());
        Assert.IsType<DMO.Alpha.Infrastructure.Authentication.Local.LocalAuthenticationProvider>(
            scope.ServiceProvider.GetRequiredService<IUserProvisioningProvider>());
    }

    [Theory]
    [InlineData("Production", "InMemory")]
    [InlineData("Production", "Supabse")]
    [InlineData("Production", "Supabase")]
    [InlineData("Staging", "InMemory")]
    public void NonDevelopment_AlwaysRegistersRealAuthenticationAndProvisioning(
        string environment, string provider)
    {
        using var factory = CreateUnconfiguredFactory(environment, provider);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        Assert.IsType<SupabaseAuthenticationProvider>(
            scope.ServiceProvider.GetRequiredService<IAuthenticationProvider>());
        Assert.IsType<SupabaseUserProvisioningProvider>(
            scope.ServiceProvider.GetRequiredService<IUserProvisioningProvider>());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task NonDevelopment_WithoutDatabase_ServesLoginButCannotSaveVolatileData(string environment)
    {
        using var factory = CreateUnconfiguredFactory(environment, "Supabase");
        using var client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var login = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("name=\"password\"", await login.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Machines.Add(new Machine { Code = "NO-VOLATILE-WRITE" });
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("ConnectionString", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);

        using var error = await client.GetAsync("/Error");
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Contains("Referência do erro", await error.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("InMemory")]
    public async Task Development_WithoutDatabase_PreservesLocalAuthenticationAndWriteSeam(string provider)
    {
        using var factory = CreateUnconfiguredFactory("Development", provider);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        Assert.IsType<InMemoryAuthenticationProvider>(
            scope.ServiceProvider.GetRequiredService<IAuthenticationProvider>());
        Assert.IsType<InMemoryUserProvisioningProvider>(
            scope.ServiceProvider.GetRequiredService<IUserProvisioningProvider>());

        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "LOCAL-DEV-ONLY" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        Assert.True(await db.Machines.AnyAsync(m => m.Id == machine.Id));
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", db.Database.ProviderName);
        db.Machines.Remove(machine);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Beta_NoDatabaseConnection_StillServesNormalLogin_NotSetup()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Dmo:AuthenticationProvider", "InMemory");
            builder.UseSetting("Dmo:DataProtectionKeyRingPath", TempKeyRing());
            builder.UseSetting("ConnectionStrings:DmoDatabase", "");
            builder.UseSetting("Dmo:InstallationDatabasePath", "obsolete-setup-do-not-read.json");
        });

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.AbsolutePath);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", db.Database.ProviderName);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void Beta_WithExplicitDatabaseConnection_UsesNpgsql(string environment)
    {
        const string connection = "Host=localhost;Database=dmo_beta_test;Username=dmo;Password=not-real";
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Dmo:AuthenticationProvider", "InMemory");
            builder.UseSetting("Dmo:DataProtectionKeyRingPath", TempKeyRing());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DmoDatabase"] = connection
                }));
        });

        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
    }
}

using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Authentication;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemoryAuthenticationProvider AuthProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Keep every test-run DataProtection keyring OUT of the source tree
        // (the app's durable default lives next to the content root).
        // UseSetting — not ConfigureAppConfiguration — so Program.cs's eager
        // keyring-path read sees it in minimal hosting.
        builder.UseSetting(
            "Dmo:DataProtectionKeyRingPath",
            Path.Combine(Path.GetTempPath(), "dmo-alpha-web-tests", Guid.NewGuid().ToString("N")));

        // Os avisos do Armazém no Job On estão em espera na aplicação (Diogo,
        // 22/10/2026); os testes que os verificam correm com eles ligados.
        builder.UseSetting("Dmo:Avisos:ArmazemSemRegisto", "true");

        builder.ConfigureServices(services =>
        {
            // Replace the authentication provider with the deterministic seam.
            services.AddSingleton<IAuthenticationProvider>(AuthProvider);

            // Replace the provisioning seam with the deterministic in-memory
            // one over the SAME account set as the authentication seam —
            // users created through the application (Admin → Users) are
            // immediately usable by the login surface, so the worked
            // examples (create → temporary → first login forces the private
            // change) run end-to-end without any real Supabase project.
            services.AddSingleton<IUserProvisioningProvider>(
                _ => new InMemoryUserProvisioningProvider(AuthProvider));

            // Replace EF configuration with an in-memory database instance
            // that is reset between tests. Uma base por fábrica: com um nome
            // fixo, todas as classes de testes partilhavam a mesma base em
            // paralelo e o ResetState de uma apagava os dados de outra (e
            // ids semeados à mão colidiam; DMO-312, 22/10/2026).
            var databaseName = _databaseName;
            // O carimbo do registo (quando e quem gravou o Peso/Pegamentos),
            // como na aplicação.
            services.AddScoped(sp =>
                new DbContextOptionsBuilder<DmoDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .AddInterceptors(sp.GetRequiredService<DMO.Alpha.Infrastructure.Controlo.RegistoCarimboInterceptor>())
                    .Options);

            services.AddScoped(sp =>
                new DmoDbContext(sp.GetRequiredService<DbContextOptions<DmoDbContext>>()));
        });
    }

    // Os WithWebHostBuilder derivados desta fábrica usam a mesma base.
    private readonly string _databaseName = "DmoAlphaTestDatabase-" + Guid.NewGuid().ToString("N");

    public void ResetState()
    {
        AuthProvider.Clear();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmoDbContext>();

        // The identity surface reset keeps unconfigured/configured scenarios
        // isolated between tests. The User↔Template assignments (Cascade on
        // Users, Restrict on Templates) disappear BEFORE both of their
        // principals. O email do ADMIN (chave Admin:Email) é limpo entre
        // testes: os cenários de bootstrap/ADMIN ficam isolados.
        var adminEmailSetting = dbContext.ModuleSettings
            .FirstOrDefault(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
        if (adminEmailSetting is not null)
        {
            dbContext.ModuleSettings.Remove(adminEmailSetting);
            dbContext.SaveChanges();
        }


        dbContext.TemplateAssignments.RemoveRange(dbContext.TemplateAssignments);
        dbContext.Users.RemoveRange(dbContext.Users);
        dbContext.Templates.RemoveRange(dbContext.Templates);
        dbContext.SaveChanges();
    }
}

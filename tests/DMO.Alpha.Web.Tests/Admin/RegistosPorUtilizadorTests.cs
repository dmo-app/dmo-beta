using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Admin;

/// <summary>
/// Os registos por utilizador no Admin (Diogo, 2026-10: «logs por utilizador
/// que o Admin poderá ver, com filtro»): só o ADMIN entra; a lista abre vazia
/// e aparece ao escolher um filtro; o utilizador e o módulo reduzem a lista.
/// </summary>
public sealed class RegistosPorUtilizadorTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string AdminEmail = "registos-admin@example.test";
    private const string AdminPassword = "RegistosAdmin1!";
    private readonly CustomWebApplicationFactory _factory;
    private readonly Guid _ana = Guid.NewGuid();
    private readonly Guid _rui = Guid.NewGuid();

    public RegistosPorUtilizadorTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using (var limpar = _factory.Services.CreateScope())
        {
            var antes = limpar.ServiceProvider.GetRequiredService<DmoDbContext>();
            antes.ArmazemMovimentos.RemoveRange(antes.ArmazemMovimentos.Where(m => m.ToolId == "reg-cm"));
            antes.Tools.RemoveRange(antes.Tools.Where(t => t.ToolId == "reg-cm"));
            antes.Users.RemoveRange(antes.Users.Where(u => u.OperatorId == "7501" || u.OperatorId == "7502" || u.OperatorId == "7503"));
            antes.SaveChanges();
        }
        _factory.AuthProvider.AddAccount(AdminEmail, "provider-registos-admin", AdminPassword, AdminEmail);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = AdminEmail });
        db.Users.AddRange(
            new User { Id = _ana, OperatorId = "7501", Name = "Ana Registos", ProviderUserId = "provider-7501" },
            new User { Id = _rui, OperatorId = "7502", Name = "Rui Registos", ProviderUserId = "provider-7502" });
        var machine = new Machine { Code = "R1" };
        db.Machines.Add(machine);
        db.Tools.Add(new Tool { ToolId = "reg-cm", Type = ToolType.Cm, Reference = "5447", Lot = "03" });
        db.SaveChanges();
        var job = new JobOn { Reference = "5447T173", ProductionNumber = "202699", MachineId = machine.Id };
        db.JobOns.Add(job);
        db.SaveChanges();
        db.JobOnChanges.Add(new JobOnChange { JobOnId = job.Id, Field = JobOnChangeFields.Calote, OldValueFrozen = "12", NewValueFrozen = "14", ChangedByUserId = _ana, ChangedByNameFrozen = "Ana Registos", ChangedAtUtc = DateTime.UtcNow.AddHours(-2) });
        db.ArmazemMovimentos.Add(new ArmazemMovimento { Tipo = ArmazemMovimentoTipo.Entrada, ToolId = "reg-cm", RegistadoPorUserId = _rui, RegistadoEmUtc = DateTime.UtcNow.AddHours(-1), OcorridoEmUtc = DateTime.UtcNow.AddHours(-1), OperacaoId = Guid.NewGuid() });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", AdminEmail),
            ("password", AdminPassword),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task Registos_AbremVazios_EFiltramPorUtilizadorEModulo()
    {
        using var client = await LoginAdminAsync();

        var vazio = await client.GetStringAsync("/Admin/Registos");
        Assert.Contains("Filtrar registos", vazio);
        Assert.DoesNotContain("dmrResultados", vazio);
        Assert.Contains(">Registos</a>", vazio);

        var ana = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Registos?utilizador=7501"));
        Assert.Contains("5447T173 · 202699 · Calote: 12 → 14", ana);
        Assert.Contains("Ana Registos (7501)", ana);
        Assert.DoesNotContain("CM 5447 · Lote 3", ana);

        var armazem = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Registos?modulo=Armazem"));
        Assert.Contains("CM 5447 · Lote 3", armazem);
        Assert.Contains("Rui Registos (7502)", armazem);
        Assert.DoesNotContain("Calote: 12", armazem);
    }

    [Fact]
    public async Task Registos_SoOAdminEntra()
    {
        _factory.AuthProvider.AddAccount("7503@dmo.local", "provider-7503", "RegistosOp1!", "7503@dmo.local");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Add(new User { Id = Guid.NewGuid(), OperatorId = "7503", Name = "Operador", ProviderUserId = "provider-7503" });
            db.SaveChanges();
        }
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "7503"), ("password", "RegistosOp1!"), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));

        var resposta = await client.GetAsync("/Admin/Registos?utilizador=7501");

        Assert.NotEqual(HttpStatusCode.OK, resposta.StatusCode);
    }
}

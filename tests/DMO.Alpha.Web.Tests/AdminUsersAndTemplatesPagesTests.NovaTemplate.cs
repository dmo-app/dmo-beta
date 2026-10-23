using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class AdminUsersAndTemplatesPagesTests
{
    // Diogo, 24/10/2026: criar uma template e depois outra gravava a segunda
    // por cima da primeira. Depois de criar, a página abre a criada para
    // editar; a «Nova template» volta ao formulário vazio do Criar.
    [Fact]
    public async Task AdminTemplates_CriarDuasSeguidas_FicamDuas_APrimeiraNaoMuda()
    {
        SeedConfiguredAdmin("tpl-duas@example.test", "tpl-duas-provider", "TplDuas12345!");
        using var admin = await LoginAdminAsync(_factory, "tpl-duas@example.test", "TplDuas12345!");
        var nomeA = "A " + Guid.NewGuid().ToString("N")[..8];
        var nomeB = "B " + Guid.NewGuid().ToString("N")[..8];

        var criarA = await PostAsync(admin, "/Admin/Templates?handler=Create", ("name", nomeA), ("capabilities", DmoCapabilities.JobOnView));
        var keyA = LocationQueryValue(criarA, "template");
        var aberta = await (await admin.GetAsync(criarA.Headers.Location!.OriginalString)).Content.ReadAsStringAsync();

        // A página da criada tem a «Nova template», que abre o Criar vazio.
        var nova = Regex.Match(aberta, "<a class=\"dmo-button\" id=\"dmtNova\" href=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal("/Admin/Templates", nova);
        var criarHtml = await (await admin.GetAsync(nova)).Content.ReadAsStringAsync();
        Assert.Contains("id=\"dmtCriarForm\"", criarHtml);
        Assert.DoesNotContain("id=\"dmtEditarForm\"", criarHtml);
        Assert.DoesNotContain(keyA!, Regex.Match(criarHtml, "<form id=\"dmtCriarForm\".*?</form>", RegexOptions.Singleline).Value);

        var criarB = await PostAsync(admin, "/Admin/Templates?handler=Create", ("name", nomeB), ("capabilities", DmoCapabilities.ControloCreate));
        Assert.Equal(HttpStatusCode.Redirect, criarB.StatusCode);
        Assert.NotEqual(keyA, LocationQueryValue(criarB, "template"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var a = await db.Templates.AsNoTracking().SingleAsync(t => t.TemplateKey == keyA);
        Assert.Equal(nomeA, a.Name);
        Assert.Equal([DmoCapabilities.JobOnView], a.Capabilities);
        Assert.Equal(1, await db.Templates.AsNoTracking().CountAsync(t => t.Name == nomeB));
    }

    // O outro lado: guardar uma template aberta continua a atualizá-la (não cria outra).
    [Fact]
    public async Task AdminTemplates_GuardarAAberta_AtualizaEssa()
    {
        SeedConfiguredAdmin("tpl-guardar@example.test", "tpl-guardar-provider", "TplGuardar123!");
        using var admin = await LoginAdminAsync(_factory, "tpl-guardar@example.test", "TplGuardar123!");
        var nome = "G " + Guid.NewGuid().ToString("N")[..8];
        var criar = await PostAsync(admin, "/Admin/Templates?handler=Create", ("name", nome), ("capabilities", DmoCapabilities.JobOnView));
        var key = LocationQueryValue(criar, "template")!;

        var guardar = await PostAsync(admin, "/Admin/Templates?handler=Edit", ("template_key", key), ("name", nome + " v2"), ("capabilities", DmoCapabilities.Ferramentas));
        Assert.Equal(key, LocationQueryValue(guardar, "template"));
        Assert.Equal("1", LocationQueryValue(guardar, "saved"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var t = await db.Templates.AsNoTracking().SingleAsync(x => x.TemplateKey == key);
        Assert.Equal(nome + " v2", t.Name);
        Assert.Equal([DmoCapabilities.Ferramentas], t.Capabilities);
        Assert.Equal(0, await db.Templates.AsNoTracking().CountAsync(x => x.Name == nome));
    }
}

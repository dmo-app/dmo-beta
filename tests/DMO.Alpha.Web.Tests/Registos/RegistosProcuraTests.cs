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

namespace DMO.Alpha.Web.Tests.Registos;

/// <summary>
/// Os Registos (Diogo, 27/10/2026): um só campo Procurar com sugestões
/// (Job On, referências, ferramentas, operadores, reparadores); o que se
/// escolhe vira o filtro. As Produções mostram CM, MF e BQ com a %; os
/// Registos mostram quem fez o quê, antes → depois; os painéis da
/// ferramenta («Filtrar») e do Job On («Opções»). Só leitura.
/// </summary>
public sealed class RegistosProcuraTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string AdminEmail = "procura-admin@example.test";
    private const string AdminPassword = "ProcuraAdmin1!";
    private readonly CustomWebApplicationFactory _factory;
    private readonly Guid _ana = Guid.NewGuid();
    private int _jobOnId;
    private int _outroJobOnId;

    public RegistosProcuraTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using (var limpar = _factory.Services.CreateScope())
        {
            var antes = limpar.ServiceProvider.GetRequiredService<DmoDbContext>();
            antes.ArmazemMovimentos.RemoveRange(antes.ArmazemMovimentos.Where(m => m.ToolId.StartsWith("rp-")));
            antes.JobOnChanges.RemoveRange(antes.JobOnChanges.Where(c => c.JobOn!.Reference.StartsWith("9810T")));
            antes.CmContexts.RemoveRange(antes.CmContexts.Where(c => c.ToolId.StartsWith("rp-")));
            antes.MfContexts.RemoveRange(antes.MfContexts.Where(c => c.ToolId.StartsWith("rp-")));
            antes.BqContexts.RemoveRange(antes.BqContexts.Where(c => c.ToolId.StartsWith("rp-")));
            antes.JobOns.RemoveRange(antes.JobOns.Where(j => j.Reference.StartsWith("9810T")));
            antes.Tools.RemoveRange(antes.Tools.Where(t => t.ToolId.StartsWith("rp-")));
            antes.Users.RemoveRange(antes.Users.Where(u => u.OperatorId == "7601" || u.OperatorId == "7602"));
            antes.Reparadores.RemoveRange(antes.Reparadores.Where(r => r.Nome == "Moldes Sul"));
            antes.SaveChanges();
        }

        _factory.AuthProvider.AddAccount(AdminEmail, "provider-procura-admin", AdminPassword, AdminEmail);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = AdminEmail });
        db.Users.Add(new User { Id = _ana, OperatorId = "7601", Name = "Ana Procura", ProviderUserId = "provider-7601" });
        db.Reparadores.Add(new Reparador { Nome = "Moldes Sul" });
        var machine = new Machine { Code = "P1" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = "rp-cm", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "rp-mf", Type = ToolType.Mf, Reference = "3301", Lot = "1" },
            new Tool { ToolId = "rp-bq", Type = ToolType.Bq, Reference = "2210", Lot = "7" });
        db.SaveChanges();
        var job = new JobOn { Reference = "9810T188", ProductionNumber = "202602", MachineId = machine.Id, InicioEm = new DateOnly(2026, 10, 20) };
        var outro = new JobOn { Reference = "9810T190", ProductionNumber = "202575", MachineId = machine.Id, InicioEm = new DateOnly(2026, 8, 25), FimEm = new DateOnly(2026, 9, 1) };
        db.JobOns.AddRange(job, outro);
        db.SaveChanges();
        _jobOnId = job.Id;
        _outroJobOnId = outro.Id;
        db.CmContexts.AddRange(new CmContext { JobOnId = job.Id, ToolId = "rp-cm" }, new CmContext { JobOnId = outro.Id, ToolId = "rp-cm" });
        db.MfContexts.Add(new MfContext { JobOnId = job.Id, ToolId = "rp-mf" });
        db.BqContexts.Add(new BqContext { JobOnId = job.Id, ToolId = "rp-bq" });
        db.JobOnChanges.Add(new JobOnChange { JobOnId = job.Id, Field = JobOnChangeFields.Calote, OldValueFrozen = "12", NewValueFrozen = "14", ChangedByUserId = _ana, ChangedByNameFrozen = "Ana Procura", ChangedAtUtc = DateTime.UtcNow.AddHours(-2) });
        db.ArmazemMovimentos.Add(new ArmazemMovimento { Tipo = ArmazemMovimentoTipo.Saida, ToolId = "rp-cm", ReparadorNome = "Moldes Sul", RegistadoPorUserId = _ana, RegistadoEmUtc = DateTime.UtcNow.AddHours(-1), OcorridoEmUtc = DateTime.UtcNow.AddHours(-1), OperacaoId = Guid.NewGuid() });
        db.SaveChanges();
    }

    private async Task<HttpClient> EntrarAsync(string identificador, string palavraPasse)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", identificador),
            ("password", palavraPasse),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        return client;
    }

    [Fact]
    public async Task Procura_AbreVazia_ESugereDeTodosOsTipos()
    {
        using var client = await EntrarAsync(AdminEmail, AdminPassword);

        var vazia = await client.GetStringAsync("/Registos");
        Assert.Contains("id=\"regConvite\"", vazia);
        Assert.DoesNotContain("Exportar", vazia);

        var referencia = System.Text.RegularExpressions.Regex.Unescape(await client.GetStringAsync("/Registos?handler=Sugestoes&q=9810T18"));
        Assert.Contains("\"grupo\":\"Referências\"", referencia);
        Assert.Contains("\"grupo\":\"Job On\"", referencia);
        Assert.Contains("9810T188 · 202602", referencia);

        var pessoas = System.Text.RegularExpressions.Regex.Unescape(await client.GetStringAsync("/Registos?handler=Sugestoes&q=Procura"));
        Assert.Contains("Ana Procura (7601)", pessoas);
        var reparador = System.Text.RegularExpressions.Regex.Unescape(await client.GetStringAsync("/Registos?handler=Sugestoes&q=Moldes"));
        Assert.Contains("\"tipo\":\"Reparador\"", reparador);
        var ferramenta = System.Text.RegularExpressions.Regex.Unescape(await client.GetStringAsync("/Registos?handler=Sugestoes&q=CM 5447"));
        Assert.Contains("\"valor\":\"rp-cm\"", ferramenta);
    }

    [Fact]
    public async Task Producoes_DaReferencia_EDaFerramenta()
    {
        using var client = await EntrarAsync(AdminEmail, AdminPassword);

        var referencia = WebUtility.HtmlDecode(await client.GetStringAsync("/Registos?q=9810T188&tipo=Referencia&valor=9810T188"));
        Assert.Contains("id=\"regProducoes\"", referencia);
        Assert.Contains("202602", referencia);
        Assert.Contains("5447 · L3", referencia);
        Assert.Contains("3301 · L1", referencia);
        Assert.Contains("2210 · L7", referencia);
        Assert.DoesNotContain("202575", referencia);

        // «Filtrar» no painel da ferramenta: todos os Job On onde trabalhou, de qualquer referência.
        var daFerramenta = WebUtility.HtmlDecode(await client.GetStringAsync("/Registos?tipo=Ferramenta&valor=rp-cm&q=CM%205447&voltar=%2FRegistos%3Fq%3D9810T188"));
        Assert.Contains("202602", daFerramenta);
        Assert.Contains("202575", daFerramenta);
        Assert.Contains("id=\"regVoltar\" href=\"/Registos?q=9810T188\"", daFerramenta);
    }

    [Fact]
    public async Task Registos_DoOperadorEDoReparador()
    {
        using var client = await EntrarAsync(AdminEmail, AdminPassword);

        var ana = WebUtility.HtmlDecode(await client.GetStringAsync($"/Registos?tipo=Operador&valor={_ana}&q=Ana&vista=registos"));
        Assert.Contains("id=\"regRegistos\"", ana);
        Assert.Contains("Ana Procura (7601)", ana);
        Assert.Contains("Calote: 12", ana);
        Assert.Contains("Saída", ana);

        var reparador = WebUtility.HtmlDecode(await client.GetStringAsync("/Registos?tipo=Reparador&valor=Moldes%20Sul&q=Moldes%20Sul&vista=registos"));
        Assert.Contains("Saída", reparador);
        Assert.DoesNotContain("Calote: 12", reparador);
    }

    [Fact]
    public async Task Paineis_DaFerramentaEDoJobOn()
    {
        using var client = await EntrarAsync(AdminEmail, AdminPassword);

        var ferramenta = WebUtility.HtmlDecode(await client.GetStringAsync("/Registos?handler=Ferramenta&tool_id=rp-cm&voltar=%2FRegistos%3Fq%3D9810"));
        Assert.Contains("data-reg-titulo=\"CM 5447 · Lote 3\"", ferramenta);
        Assert.Contains("id=\"regFiltrar\"", ferramenta);
        Assert.Contains("Últimos registos", ferramenta);
        Assert.Contains("Saída", ferramenta);

        var jobOn = WebUtility.HtmlDecode(await client.GetStringAsync($"/Registos?handler=JobOn&id={_jobOnId}"));
        Assert.Contains("data-reg-titulo=\"9810T188 · 202602\"", jobOn);
        Assert.Contains("id=\"regOpcoes\"", jobOn);
        Assert.Contains($"/Controlo/Resumo?jobon_id={_jobOnId}", jobOn);
        Assert.Contains("Alterou · Calote · 12 → 14", jobOn);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Registos?handler=JobOn&id=999999")).StatusCode);
        _ = _outroJobOnId;
    }

    [Fact]
    public async Task SoEntraOAdminOuQuemTemAChave()
    {
        _factory.AuthProvider.AddAccount("7602@dmo.local", "provider-7602", "ProcuraOp1!", "7602@dmo.local");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Users.Add(new User { Id = Guid.NewGuid(), OperatorId = "7602", Name = "Sem Registos", ProviderUserId = "provider-7602" });
            db.SaveChanges();
        }

        using var client = await EntrarAsync("7602", "ProcuraOp1!");
        var resposta = await client.GetAsync("/Registos?q=9810T188");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.False((resposta.Headers.Location?.ToString() ?? "").StartsWith("/Registos", StringComparison.Ordinal));
    }
}

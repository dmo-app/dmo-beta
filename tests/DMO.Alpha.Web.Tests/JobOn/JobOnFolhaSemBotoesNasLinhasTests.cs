using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// «Não quero botões em rows por todo o lado» (Diogo, 27/10/2026): em «O que
/// mudou» da folha do Job On, as alterações deixaram de ter cada uma o seu
/// botão. A ação do módulo («Verificar o Peso», «Rever o Controlo») é UM
/// botão no canto, para a alteração escolhida; a mais recente com ação vem
/// escolhida e cada linha diz qual é a sua ação (o clique troca-a).
/// </summary>
public class JobOnFolhaSemBotoesNasLinhasTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;

    public JobOnFolhaSemBotoesNasLinhasTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        ResetDomainState();
    }

    public void Dispose() => ResetDomainState();

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.JobOnChanges.RemoveRange(db.JobOnChanges);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        const string password = "LinhasPass1!";
        _factory.AuthProvider.AddAccount(email, "provider-" + operatorId, password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Planeador " + operatorId, ProviderUserId = "provider-" + operatorId };
            var template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-linhas-" + operatorId, Name = "Linhas " + operatorId, Capabilities = capabilities };
            db.Templates.Add(template);
            db.Users.Add(user);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(login)!;
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId), ("password", password), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>Três alterações de hoje: a folha (sem ação), a BQ (Rever o Controlo) e a CM, a mais recente (Verificar o Peso).</summary>
    private async Task<int> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var maquina = new Machine { Code = "L1" };
        db.Machines.Add(maquina);
        db.Tools.Add(new Tool { ToolId = "CM-L", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = "MF-L", Type = ToolType.Mf, Reference = "5447", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "BQ-L", Type = ToolType.Bq, Reference = "T194", Lot = "13" });
        var jobOn = new JobOn
        {
            Reference = "5447T194", ProductionNumber = "202688", Machine = maquina,
            InicioEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-2)
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-L" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-L" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-L" });
        var agora = DateTime.UtcNow;
        db.JobOnChanges.AddRange(
            new JobOnChange
            {
                JobOn = jobOn, Field = JobOnChangeFields.Folha, OldValueFrozen = null, NewValueFrozen = "Notas",
                ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora.AddMinutes(-3)
            },
            new JobOnChange
            {
                JobOn = jobOn, Field = JobOnChangeFields.Bq, OldValueFrozen = "T194 · lote 12", NewValueFrozen = "T194 · lote 13",
                ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora.AddMinutes(-2)
            },
            new JobOnChange
            {
                JobOn = jobOn, Field = JobOnChangeFields.Cm, OldValueFrozen = "5447 · lote 2", NewValueFrozen = "5447 · lote 3",
                ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora.AddMinutes(-1)
            });
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    [Fact]
    public async Task OQueMudou_UmSoBotaoNoCanto_ParaAAlteracaoEscolhida_NenhumNasLinhas()
    {
        var jobOnId = await SeedAsync();
        using var client = await LoginAsync("7301", DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate);

        var response = await client.GetAsync($"/JobOn/Folha?jobon_id={jobOnId}&vista=alteracoes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var folha = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var mudancas = Regex.Match(folha, "<details class=\"jobon-mudancas\" id=\"fichaMudancas\"[\\s\\S]*?</details>").Value;
        Assert.NotEmpty(mudancas);

        // Nenhum botão nas linhas.
        var lista = Regex.Match(mudancas, "<ul class=\"jobon-resumo\"[\\s\\S]*?</ul>").Value;
        Assert.Equal(3, Regex.Matches(lista, "<li ").Count);
        Assert.DoesNotContain("dmo-button", lista);
        Assert.DoesNotContain("<button", lista);
        Assert.DoesNotContain("<a ", lista);

        // Um só botão, antes da lista, com a ação da mais recente (a CM), que vem escolhida.
        var botoes = Regex.Matches(mudancas, "<a class=\"dmo-button\"[^>]*>[^<]*</a>");
        Assert.Single(botoes);
        Assert.Equal($"<a class=\"dmo-button\" href=\"/Controlo/Peso?jobon_id={jobOnId}\">Verificar o Peso</a>", botoes[0].Value);
        Assert.True(mudancas.IndexOf("data-jof-resumo-acao", StringComparison.Ordinal) < mudancas.IndexOf("<ul class=\"jobon-resumo\"", StringComparison.Ordinal));
        var linhas = Regex.Matches(lista, "<li [^>]*>").Select(m => m.Value).ToList();
        Assert.Contains("class=\"selected\" aria-current=\"true\"", linhas[0]);
        Assert.DoesNotContain("selected", linhas[1]);

        // Cada linha diz a sua ação; a da folha não tem nenhuma.
        Assert.Contains($"data-jof-acao-href=\"/Controlo/Peso?jobon_id={jobOnId}\" data-jof-acao-rotulo=\"Verificar o Peso\"", linhas[0]);
        Assert.Contains($"data-jof-acao-href=\"/Controlo/Resumo?jobon_id={jobOnId}\" data-jof-acao-rotulo=\"Rever o Controlo\"", linhas[1]);
        Assert.Contains("data-jof-acao-href=\"\" data-jof-acao-rotulo=\"\"", linhas[2]);
        Assert.All(linhas, l => Assert.Contains("tabindex=\"0\"", l));
    }

    [Fact]
    public async Task OQueMudou_SemOModuloDaAcao_NaoHaBotaoNoCanto()
    {
        var jobOnId = await SeedAsync();
        using var client = await LoginAsync("7302", DmoCapabilities.JobOnCreate);

        var folha = WebUtility.HtmlDecode(await (await client.GetAsync($"/JobOn/Folha?jobon_id={jobOnId}&vista=alteracoes")).Content.ReadAsStringAsync());
        var mudancas = Regex.Match(folha, "<details class=\"jobon-mudancas\" id=\"fichaMudancas\"[\\s\\S]*?</details>").Value;
        Assert.NotEmpty(mudancas);
        Assert.DoesNotContain("data-jof-resumo-acao", mudancas);
        Assert.DoesNotContain("dmo-button", mudancas);
        Assert.DoesNotContain("data-jof-acao-href=\"/", mudancas);
    }
}

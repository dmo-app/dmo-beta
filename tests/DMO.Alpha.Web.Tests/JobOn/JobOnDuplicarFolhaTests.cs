using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O Duplicar do Job On com a folha da fábrica (Job On, peça 4, regras do
/// Diogo): a folha da produção de origem vem copiada no formulário e o Guardar
/// grava-a com a produção nova, na mesma escrita; as datas pedem-se; a
/// máquina, as ferramentas e as utilizações copiadas levam a marca
/// «Confirmar», sem bloquear; e a folha tem o sítio da contagem dos campos
/// alterados.
/// </summary>
public class JobOnDuplicarFolhaTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string PagePath = "/JobOn";

    private readonly CustomWebApplicationFactory _factory;

    public JobOnDuplicarFolhaTests(CustomWebApplicationFactory factory)
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
        db.JobOnFolhaCalLinhas.RemoveRange(db.JobOnFolhaCalLinhas);
        db.JobOnFolhas.RemoveRange(db.JobOnFolhas);
        db.JobOnChanges.RemoveRange(db.JobOnChanges);
        db.JobOnLotNotes.RemoveRange(db.JobOnLotNotes);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private async Task<HttpClient> EntrarAsync(string operatorId)
    {
        var password = "DuplicarPass1!";
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, "provider-duplicar-" + operatorId, password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                TemplateKey = "tpl-test-duplicar-" + operatorId,
                Name = "Planeamento de teste — Duplicar " + operatorId,
                Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate }
            };
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Planeamento", ProviderUserId = "provider-duplicar-" + operatorId };
            db.Templates.Add(template);
            db.Users.Add(user);
            db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await client.GetAsync("/login");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await login.Content.ReadAsStringAsync())!;
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", operatorId), ("password", password), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>
    /// A produção 202601 em B1 com o MF 5810; com <paramref name="comFolha"/>,
    /// a folha tem o cabeçalho, as utilizações da CM e do MF, uma nota e uma
    /// linha CAL escritas.
    /// </summary>
    private async Task<int> SeedOrigemAsync(bool comFolha)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b1 = new Machine { Code = "B1" };
        db.Machines.Add(b1);
        db.Tools.Add(new Tool { ToolId = "MF-DUP", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        var jobOn = new JobOn
        {
            Reference = "1075C142",
            ProductionNumber = "202601",
            Machine = b1,
            InicioEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-2),
            FimEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(5)
        };
        db.JobOns.Add(jobOn);
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-DUP" });
        await db.SaveChangesAsync();
        if (comFolha)
        {
            db.JobOnFolhas.Add(new JobOnFolha
            {
                JobOnId = jobOn.Id,
                CabSeccoes = "11",
                CmUtilizacao = "80",
                CmQtdStock = "12",
                MfUtilizacao = "65",
                CmNotas = "Ver costuras"
            });
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha { JobOnId = jobOn.Id, Ordem = 0, Designacao = "Bucha Marcada", Valor = "8151", QtdMaq = "3" });
            await db.SaveChangesAsync();
        }

        return jobOn.Id;
    }

    private static async Task<string> HtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<T> LerAsync<T>(Func<DmoDbContext, Task<T>> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return await ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    /// <summary>A tag de um input pelo id; falha quando o input não existe.</summary>
    private static string Input(string html, string id)
    {
        var input = Regex.Match(html, $"<input\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>");
        Assert.True(input.Success, $"A página não tem o campo #{id}.");
        return input.Value;
    }

    /// <summary>O value de um input pelo id (null sem value).</summary>
    private static string? ValorDoCampo(string html, string id)
    {
        var valor = Regex.Match(Input(html, id), "\\bvalue=\"(?<v>[^\"]*)\"");
        return valor.Success ? WebUtility.HtmlDecode(valor.Groups["v"].Value) : null;
    }

    /// <summary>O rótulo de um campo da folha pelo id do campo, com o que tiver dentro.</summary>
    private static string Rotulo(string html, string id)
    {
        var rotulo = Regex.Match(html, $"<label for=\"{Regex.Escape(id)}\">(?<r>.*?)</label>", RegexOptions.Singleline);
        Assert.True(rotulo.Success, $"A página não tem o rótulo de #{id}.");
        return rotulo.Groups["r"].Value;
    }

    private const string MarcaConfirmar = "<span class=\"jobon-confirmar\">Confirmar</span>";

    [Fact]
    public async Task Duplicar_TrazAFolhaDaOrigem_EOGuardarGravaANaProducaoNova()
    {
        var origemId = await SeedOrigemAsync(comFolha: true);
        using var client = await EntrarAsync("4751");

        var duplicar = await HtmlAsync(client, $"{PagePath}?jobon_id={origemId}&duplicar=1");

        // A folha da origem no formulário de criação, com a CAL gravada.
        Assert.Contains("Duplicada da produção 202601", duplicar);
        Assert.Equal("11", ValorDoCampo(duplicar, "jobon-cab-seccoes"));
        Assert.Equal("80", ValorDoCampo(duplicar, "jobon-cm-utilizacao"));
        Assert.Equal("12", ValorDoCampo(duplicar, "jobon-cm-qtd-stock"));
        Assert.Equal("Bucha Marcada", ValorDoCampo(duplicar, "jobon-cal-0-designacao"));
        Assert.Equal("8151", ValorDoCampo(duplicar, "jobon-cal-0-valor"));
        Assert.Null(ValorDoCampo(duplicar, "jobon-cal-1-designacao")); // a linha vazia para acrescentar
        Assert.Contains("Ver costuras", duplicar);
        // A ficha em leitura não aparece no Duplicar.
        Assert.DoesNotContain("id=\"fichaFolha\"", duplicar);

        // O nº e as datas são da produção nova: vazios. As datas são livres
        // (só avisos, nunca bloqueios): não ficam obrigatórias.
        Assert.Null(ValorDoCampo(duplicar, "lightProduction"));
        Assert.Null(ValorDoCampo(duplicar, "lightStart"));
        Assert.DoesNotContain("required", Input(duplicar, "lightStart"));
        Assert.DoesNotContain("required", Input(duplicar, "lightEnd"));

        var (acao, campos) = EnvioDoFormulario.Preencher(duplicar,
            ("lightProduction", "202650"),
            ("lightStart", "2030-02-01"),
            ("lightEnd", "2030-02-10"),
            ("jobon-mf-utilizacao", "70"));
        Assert.Equal($"{PagePath}?handler=Create", acao);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);

        // A produção nova tem a folha copiada, com o que se mudou.
        var novaId = await LerAsync(db => db.JobOns.Where(j => j.ProductionNumber == "202650").Select(j => j.Id).SingleAsync());
        var gravada = await LerAsync(db => db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == novaId));
        Assert.Equal(("11", "80", "12", "70", "Ver costuras"),
            (gravada.CabSeccoes, gravada.CmUtilizacao, gravada.CmQtdStock, gravada.MfUtilizacao, gravada.CmNotas));
        var linha = Assert.Single(await LerAsync(db => db.JobOnFolhaCalLinhas.AsNoTracking().Where(l => l.JobOnId == novaId).ToListAsync()));
        Assert.Equal(("Bucha Marcada", "8151", "3"), (linha.Designacao, linha.Valor, linha.QtdMaq));
        // A criação não é uma alteração, e a origem fica como estava.
        Assert.Equal(0, await LerAsync(db => db.JobOnChanges.CountAsync()));
        var origem = await LerAsync(db => db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == origemId));
        Assert.Equal("65", origem.MfUtilizacao);
    }

    [Fact]
    public async Task Duplicar_MarcaConfirmarNaMaquinaNasFerramentasENasUtilizacoesCopiadas()
    {
        var origemId = await SeedOrigemAsync(comFolha: true);
        using var client = await EntrarAsync("4752");

        var duplicar = await HtmlAsync(client, $"{PagePath}?jobon_id={origemId}&duplicar=1");

        // As utilizações escritas levam a marca; a vazia (BQ) e os outros campos não.
        Assert.Contains(MarcaConfirmar, Rotulo(duplicar, "jobon-cm-utilizacao"));
        Assert.Contains(MarcaConfirmar, Rotulo(duplicar, "jobon-mf-utilizacao"));
        Assert.DoesNotContain(MarcaConfirmar, Rotulo(duplicar, "jobon-bq-utilizacao"));
        Assert.DoesNotContain(MarcaConfirmar, Rotulo(duplicar, "jobon-cm-qtd-stock"));

        // A máquina copiada e o MF copiado; a CM e a BQ não vieram.
        Assert.Matches("Máquina / linha\\s*<span class=\"jobon-confirmar\">Confirmar</span>", duplicar);
        Assert.Matches("<strong>Molde final</strong>\\s*<span class=\"jobon-confirmar\">Confirmar</span>", duplicar);
        Assert.DoesNotMatch("<strong>Contra molde</strong>\\s*<span class=\"jobon-confirmar\">", duplicar);
        Assert.DoesNotMatch("<strong>Boquilha</strong>\\s*<span class=\"jobon-confirmar\">", duplicar);

        // A contagem dos campos alterados: o sítio vazio, que o script preenche.
        Assert.Contains("<span class=\"jobon-alterados\" id=\"lightAlterados\" role=\"status\" aria-live=\"polite\"></span>", duplicar);
    }

    [Fact]
    public async Task Duplicar_SemFolhaNaOrigem_AbreAsMedidasPorOmissao_SemMarcasNaFolha()
    {
        var origemId = await SeedOrigemAsync(comFolha: false);
        using var client = await EntrarAsync("4753");

        var duplicar = await HtmlAsync(client, $"{PagePath}?jobon_id={origemId}&duplicar=1");

        Assert.Equal("Bucha Marcada", ValorDoCampo(duplicar, "jobon-cal-0-designacao"));
        Assert.Equal("Pinças", ValorDoCampo(duplicar, "jobon-cal-7-designacao"));
        Assert.Null(ValorDoCampo(duplicar, "jobon-cm-utilizacao"));
        Assert.DoesNotContain(MarcaConfirmar, Rotulo(duplicar, "jobon-cm-utilizacao"));
    }

    [Fact]
    public async Task NovoEEditar_NaoTemMarcasDoDuplicar()
    {
        var origemId = await SeedOrigemAsync(comFolha: true);
        using var client = await EntrarAsync("4754");

        foreach (var url in new[] { $"{PagePath}?novo=1", $"{PagePath}?jobon_id={origemId}&editar=1" })
        {
            var html = await HtmlAsync(client, url);
            Assert.DoesNotContain("required", Input(html, "lightStart"));
            Assert.DoesNotContain("required", Input(html, "lightEnd"));
            Assert.DoesNotContain("jobon-confirmar\">", html);
            Assert.DoesNotContain("id=\"lightAlterados\"", html);
        }
    }
}

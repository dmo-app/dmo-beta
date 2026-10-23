using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// Entrada | Saída primeiro no «Registar …» (Diogo, 23/10/2026: «No armazém
/// deveria primeiro aparecer entrada saída porque isso ajuda a filtrar o que
/// existe para entrar e sair»): a Entrada mostra só as que estão fora do
/// armazém (e o lote novo); a Saída, só as que estão numa posição; o modo vai
/// no endereço e segue na procura.
/// </summary>
public sealed class ArmazemModoEntradaSaidaPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Fora = "arm-modo-fora";
    private const string Dentro = "arm-modo-dentro";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemModoEntradaSaidaPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Fora || t.ToolId == Dentro || t.Reference.StartsWith("9311")));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = Fora, Type = ToolType.Cm, Reference = "9311A001", Lot = "1" },
            new Tool { ToolId = Dentro, Type = ToolType.Cm, Reference = "9311A002", Lot = "2" });
        var posicao = new ArmazemPosicao { Codigo = "0077", CriadaEmUtc = DateTime.UtcNow };
        db.ArmazemPosicoes.Add(posicao);
        db.SaveChanges();
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = Dentro, PosicaoId = posicao.Id, MovimentoId = 1, DesdeUtc = DateTime.UtcNow });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Armazem{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armmodo-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = [DmoCapabilities.ArmazemGestao] };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static string Linha(string id) => $"data-dmo-ferramenta=\"{id}\"";

    [Fact]
    public async Task Entrada_MostraSoAsDeFora_ComOLoteNovo_EAbreAEntrada()
    {
        using var client = await LoginAsync("6301");

        var pagina = await client.GetStringAsync("/Armazem/Movimentos?tipo=CM&modo=entrada&q=9311");
        var parcial = await client.GetStringAsync("/Armazem/Movimentos?handler=Procurar&tipo=CM&modo=entrada&q=9311");

        Assert.Contains(Linha(Fora), pagina);
        Assert.DoesNotContain(Linha(Dentro), pagina);
        Assert.Contains($"ferramenta={Fora}&amp;acao=entrada", pagina);
        Assert.Contains("1 ferramenta está numa posição: escolha «Saída».", pagina);
        Assert.Contains(Linha(Fora), parcial);
        Assert.DoesNotContain(Linha(Dentro), parcial);
        // O lote novo só na Entrada.
        Assert.Contains("id=\"armLoteNovo\"", await client.GetStringAsync("/Armazem/Movimentos?tipo=CM&modo=entrada&q=8888"));
    }

    [Fact]
    public async Task Saida_MostraSoAsDasPosicoes_SemLoteNovo_EAPosicaoProcuraOQueLaEsta()
    {
        using var client = await LoginAsync("6302");

        var pagina = await client.GetStringAsync("/Armazem/Movimentos?tipo=CM&modo=saida&q=9311");
        var parcial = await client.GetStringAsync("/Armazem/Movimentos?handler=Procurar&tipo=CM&modo=saida&q=9311");
        var pelaPosicao = await client.GetStringAsync("/Armazem/Movimentos?tipo=CM&modo=saida&pos=0077");
        var vazia = await client.GetStringAsync("/Armazem/Movimentos?tipo=CM&modo=saida&q=8888");

        Assert.Contains(Linha(Dentro), pagina);
        Assert.DoesNotContain(Linha(Fora), pagina);
        Assert.Contains($"ferramenta={Dentro}&amp;acao=saida", pagina);
        Assert.Contains("1 ferramenta está fora do armazém: escolha «Entrada».", pagina);
        Assert.Contains(Linha(Dentro), parcial);
        Assert.DoesNotContain(Linha(Fora), parcial);
        Assert.Contains(Linha(Dentro), pelaPosicao);
        Assert.DoesNotContain("id=\"armLoteNovo\"", vazia);
        Assert.Contains("Nenhuma CM numa posição do armazém.", vazia);
    }

    [Fact]
    public async Task SemModo_EAEntrada_EOModoSegueNaProcura()
    {
        using var client = await LoginAsync("6303");

        var semModo = await client.GetStringAsync("/Armazem/Movimentos?tipo=CM&q=9311");
        var saida = await client.GetStringAsync("/Armazem/Movimentos?tipo=CM&modo=saida&q=9311&pos=0077");

        Assert.Contains(Linha(Fora), semModo);
        Assert.DoesNotContain(Linha(Dentro), semModo);
        Assert.Contains("id=\"armProcuraModoEntrada\" aria-pressed=\"true\"", semModo);
        Assert.Contains("<input type=\"hidden\" name=\"modo\" value=\"entrada\" />", semModo);
        // Saída: o formulário da procura e a procura ao escrever levam o modo; os links levam a procura.
        Assert.Contains("id=\"armProcuraModoSaida\" aria-pressed=\"true\"", saida);
        Assert.Contains("<input type=\"hidden\" name=\"modo\" value=\"saida\" />", saida);
        Assert.Matches("data-dmo-procura=\"[^\"]*modo=saida", saida);
        Assert.Contains("href=\"/Armazem/Movimentos?tipo=CM&amp;modo=entrada&amp;q=9311&amp;pos=0077\"", saida);
    }
}

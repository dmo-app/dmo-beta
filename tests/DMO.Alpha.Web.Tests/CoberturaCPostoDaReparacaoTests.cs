using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O posto da Reparação Interna com o PIN a sério (DMO-429): o ADMIN guarda o
/// PIN de cada pessoa na ficha do utilizador, o hash fica na base e o posto
/// (com a implementação real, não uma falsa) deixa entrar quem tem esse PIN.
/// E o botão «Reparação» do login que lá leva (DMO-436).
/// </summary>
public sealed class CoberturaCPostoDaReparacaoTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string MensagemPinInvalido = "O PIN tem de ter de 4 a 12 algarismos.";

    private readonly CustomWebApplicationFactory _factory;

    public CoberturaCPostoDaReparacaoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.UserSettings.RemoveRange(db.UserSettings);
        db.SaveChanges();
    }

    private async Task<string?> HashDoPinAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.UserSettings.AsNoTracking()
            .Where(s => s.UserId == userId && s.Key == PostoRiRegras.Chave)
            .Select(s => s.Value)
            .SingleOrDefaultAsync();
    }

    /// <summary>O ADMIN escreve o PIN na ficha do utilizador e carrega em «Guardar»; devolve o aviso que a ficha mostra.</summary>
    private static async Task<string> GuardarPinNaFichaAsync(HttpClient admin, string operatorId, string pin)
    {
        var ficha = await CoberturaCApoio.PaginaAsync(admin, $"/Admin/Users/Ficha?operator_id={operatorId}");
        var (acao, campos) = EnvioDoFormulario.Preencher(ficha, ("dfu-pin", pin));
        using var guardou = await admin.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, guardou.StatusCode);
        var depois = WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(admin, guardou.Headers.Location!.OriginalString));
        var aviso = Regex.Match(depois, "<div class=\"dfu-nota\" role=\"status\">([^<]*)</div>");
        Assert.True(aviso.Success, "a ficha não mostrou o resultado de guardar o PIN");
        return aviso.Groups[1].Value;
    }

    private async Task<string> PostoAsync(HttpClient anonimo) => await CoberturaCApoio.PaginaAsync(anonimo, "/Posto");

    private static async Task<HttpResponseMessage> EntrarNoPostoAsync(HttpClient anonimo, string paginaDoPosto, Guid userId, string pin) =>
        await CoberturaCApoio.EnviarAsync(anonimo, paginaDoPosto, "/Posto", ("utilizador", userId.ToString()), ("pin", pin));

    [Fact]
    public async Task OPinGuardadoPeloAdmin_FicaEmHash_ESoComEleOReparadorEntraNoPosto()
    {
        var reparadora = CoberturaCApoio.CriarOperador(_factory, "7801", "Reparadora Posto", DmoCapabilities.ReparacaoInterna);
        var admin = await CoberturaCApoio.AdminAsync(_factory, "cobc-posto-admin@example.test", "CobCAdmin4!");
        using var anonimo = CoberturaCApoio.Cliente(_factory);

        // Sem PIN ninguém a escolhe no posto (a lista só tem quem já tem PIN).
        var semPin = await PostoAsync(anonimo);
        Assert.Contains("Escolha o seu nome", semPin);
        Assert.DoesNotContain("Reparadora Posto", semPin);
        Assert.Contains("PIN (ainda não tem)", WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(admin, "/Admin/Users/Ficha?operator_id=7801")));

        Assert.Equal("PIN do posto guardado.", await GuardarPinNaFichaAsync(admin, "7801", "4821"));
        var hash = await HashDoPinAsync(reparadora);
        Assert.NotNull(hash);
        Assert.StartsWith("v1.", hash);
        Assert.DoesNotContain("4821", hash);
        Assert.Contains("PIN novo (já tem PIN)", WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(admin, "/Admin/Users/Ficha?operator_id=7801")));

        var comPin = await PostoAsync(anonimo);
        Assert.Equal(reparadora.ToString(), EnvioDoFormulario.Opcao(comPin, "posto-nome", "Reparadora Posto"));

        // Um PIN errado recusa sem dizer qual dos dois falhou, e não entra.
        using (var errado = await EntrarNoPostoAsync(anonimo, comPin, reparadora, "0000"))
        {
            Assert.Equal(HttpStatusCode.OK, errado.StatusCode);
            Assert.Contains("Nome ou PIN errado.", await errado.Content.ReadAsStringAsync());
        }

        using var certo = await EntrarNoPostoAsync(anonimo, comPin, reparadora, "4821");
        Assert.Equal(HttpStatusCode.Redirect, certo.StatusCode);
        Assert.StartsWith("/ReparacaoInterna", certo.Headers.Location!.OriginalString);
        using var ri = await anonimo.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.OK, ri.StatusCode);
    }

    [Fact]
    public async Task UmPinNovoSubstituiOAnterior_ENaoSeAceitaUmPinForaDosQuatroAosDozeAlgarismos()
    {
        var reparador = CoberturaCApoio.CriarOperador(_factory, "7802", "Reparador Troca", DmoCapabilities.ReparacaoInternaView);
        var admin = await CoberturaCApoio.AdminAsync(_factory, "cobc-posto-troca@example.test", "CobCAdmin5!");
        using var anonimo = CoberturaCApoio.Cliente(_factory);

        // Um campo inválido de cada vez: três algarismos, treze e uma letra no meio. Nada se grava.
        foreach (var invalido in new[] { "123", "1234567890123", "12a4" })
        {
            Assert.Equal(MensagemPinInvalido, await GuardarPinNaFichaAsync(admin, "7802", invalido));
            Assert.Null(await HashDoPinAsync(reparador));
        }

        Assert.DoesNotContain("Reparador Troca", await PostoAsync(anonimo));

        // Nos dois limites (4 e 12 algarismos) grava; o novo substitui o anterior.
        Assert.Equal("PIN do posto guardado.", await GuardarPinNaFichaAsync(admin, "7802", "1234"));
        var primeiro = await HashDoPinAsync(reparador);
        Assert.Equal("PIN do posto guardado.", await GuardarPinNaFichaAsync(admin, "7802", "123456789012"));
        var segundo = await HashDoPinAsync(reparador);
        Assert.NotEqual(primeiro, segundo);

        var posto = await PostoAsync(anonimo);
        using (var antigo = await EntrarNoPostoAsync(anonimo, posto, reparador, "1234"))
        {
            Assert.Equal(HttpStatusCode.OK, antigo.StatusCode);
            Assert.Contains("Nome ou PIN errado.", await antigo.Content.ReadAsStringAsync());
        }

        using var novo = await EntrarNoPostoAsync(anonimo, posto, reparador, "123456789012");
        Assert.Equal(HttpStatusCode.Redirect, novo.StatusCode);
        Assert.StartsWith("/ReparacaoInterna", novo.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task QuemNaoTemAReparacaoInterna_TemPinMasNaoApareceNoPosto()
    {
        var semRi = CoberturaCApoio.CriarOperador(_factory, "7803", "Sem Reparação", DmoCapabilities.Boquilhas);
        CoberturaCApoio.CriarOperador(_factory, "7804", "Com Reparação", DmoCapabilities.ReparacaoInterna);
        var admin = await CoberturaCApoio.AdminAsync(_factory, "cobc-posto-sem@example.test", "CobCAdmin6!");
        using var anonimo = CoberturaCApoio.Cliente(_factory);

        Assert.Equal("PIN do posto guardado.", await GuardarPinNaFichaAsync(admin, "7803", "5555"));
        Assert.Equal("PIN do posto guardado.", await GuardarPinNaFichaAsync(admin, "7804", "6666"));
        Assert.NotNull(await HashDoPinAsync(semRi));

        var posto = WebUtility.HtmlDecode(await PostoAsync(anonimo));
        Assert.Contains("Com Reparação", posto);
        Assert.DoesNotContain("Sem Reparação", posto);
    }

    [Fact]
    public async Task OBotaoReparacaoDoLogin_AbreOPosto_EOPostoTemOVoltarAoLogin()
    {
        using var anonimo = CoberturaCApoio.Cliente(_factory);

        var login = await CoberturaCApoio.PaginaAsync(anonimo, "/login");

        // Só «Reparação», depois do «Entrar», e leva ao posto.
        var botao = Regex.Match(login, "<a[^>]*id=\"login-posto\"[^>]*>([^<]*)</a>");
        Assert.True(botao.Success, "o login não tem o botão do posto");
        Assert.Equal("Reparação", WebUtility.HtmlDecode(botao.Groups[1].Value));
        Assert.Contains("href=\"/Posto\"", botao.Value);
        Assert.True(Posicao(login, "data-login-submit") < Posicao(login, "id=\"login-posto\""));

        var posto = await CoberturaCApoio.PaginaAsync(anonimo, "/Posto");
        Assert.Contains("id=\"posto-title\"", posto);
        Assert.Contains("Escolha o seu nome", posto);
        Assert.Matches("<a[^>]*id=\"posto-voltar\"[^>]*href=\"/login\"|<a[^>]*href=\"/login\"[^>]*id=\"posto-voltar\"", posto);
    }
}

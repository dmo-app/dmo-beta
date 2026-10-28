using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O que a auditoria de cobertura (C) achou por provar nas Definições
/// pessoais, na ficha do utilizador e no topo: a duração da sessão
/// escolhida em Definições → A minha conta (DMO-398), a «A minha conta» sem o
/// email inventado (DMO-480), o «← Voltar» da ficha (DMO-480) e o utilizador
/// no topo (DMO-414).
/// </summary>
public sealed class CoberturaCContaETopoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CoberturaCContaETopoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.UserSettings.RemoveRange(db.UserSettings);
        db.SaveChanges();
    }

    private static string SelectDaSessao(string html) =>
        Regex.Match(html, "<select name=\"horas\"[\\s\\S]*?</select>").Value;

    private static string? HorasEscolhidas(string html)
    {
        var escolhida = Regex.Match(SelectDaSessao(html), "<option value=\"(\\d+)\" selected");
        return escolhida.Success ? escolhida.Groups[1].Value : null;
    }

    private async Task<string?> HorasGravadasAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.UserSettings.AsNoTracking()
            .Where(s => s.UserId == userId && s.Key == UserSettingKeys.SessaoHoras)
            .Select(s => s.Value)
            .SingleOrDefaultAsync();
    }

    private static async Task<HttpResponseMessage> EscolherHorasAsync(HttpClient cliente, int horas)
    {
        var pagina = await CoberturaCApoio.PaginaAsync(cliente, "/Definicoes");
        var acao = WebUtility.HtmlDecode(Regex.Match(pagina, "<form[^>]*action=\"([^\"]*handler=Sessao[^\"]*)\"").Groups[1].Value);
        Assert.NotEmpty(acao);
        return await CoberturaCApoio.EnviarAsync(cliente, pagina, acao, ("horas", horas.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Quando a sessão que o login acabou de abrir expira (lido do bilhete do cookie).</summary>
    private DateTimeOffset ExpiraAs(HttpResponseMessage entrada)
    {
        var cookie = entrada.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("DmoAuth=", StringComparison.Ordinal));
        var valor = Uri.UnescapeDataString(cookie["DmoAuth=".Length..].Split(';')[0]);
        var opcoes = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var bilhete = opcoes.TicketDataFormat.Unprotect(valor);
        Assert.NotNull(bilhete);
        var expira = bilhete!.Properties.ExpiresUtc;
        Assert.True(expira.HasValue, "o bilhete da sessão não tem hora de fim");
        return expira!.Value;
    }

    private async Task<HttpResponseMessage> EntrarDeNovoAsync(string operatorId)
    {
        var cliente = CoberturaCApoio.Cliente(_factory);
        var entrada = await CoberturaCApoio.EntrarAsync(cliente, $"{operatorId}@dmo.local", CoberturaCApoio.PalavraPasseDe(operatorId));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        return entrada;
    }

    [Fact]
    public async Task ADuracaoDaSessao_OferecePeloMenosUmaHoraAteVinteEQuatro_ESoGravaAsPermitidas_SoDeQuemAEscolhe()
    {
        var (ana, anaId) = await CoberturaCApoio.OperadorAsync(_factory, "7701", "Ana Sessão", DmoCapabilities.Boquilhas);
        var (_, ruiId) = await CoberturaCApoio.OperadorAsync(_factory, "7702", "Rui Sessão", DmoCapabilities.Boquilhas);

        var inicial = await CoberturaCApoio.PaginaAsync(ana, "/Definicoes");
        var opcoes = Regex.Matches(SelectDaSessao(inicial), "<option value=\"(\\d+)\"[^>]*>([^<]+)</option>")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToArray();
        Assert.Equal(new[] { ("1", "1 hora"), ("2", "2 horas"), ("4", "4 horas"), ("8", "8 horas"), ("12", "12 horas"), ("24", "24 horas") }, opcoes);
        Assert.Equal("1", HorasEscolhidas(inicial));
        Assert.Null(await HorasGravadasAsync(anaId));

        // Os dois limites (1 e 24) e uma do meio gravam-se, e a página volta com a escolha.
        foreach (var horas in new[] { 24, 8, 1 })
        {
            using var gravou = await EscolherHorasAsync(ana, horas);
            Assert.Equal(HttpStatusCode.Redirect, gravou.StatusCode);
            Assert.Equal(horas.ToString(System.Globalization.CultureInfo.InvariantCulture), await HorasGravadasAsync(anaId));
            Assert.Equal(horas.ToString(System.Globalization.CultureInfo.InvariantCulture), HorasEscolhidas(await CoberturaCApoio.PaginaAsync(ana, "/Definicoes")));
        }

        // Uma duração que não está na lista (logo abaixo, entre duas e logo acima) é recusada e nada muda.
        using (var certa = await EscolherHorasAsync(ana, 4))
        {
            Assert.Equal(HttpStatusCode.Redirect, certa.StatusCode);
        }

        foreach (var invalida in new[] { 0, 3, 25 })
        {
            using var recusada = await EscolherHorasAsync(ana, invalida);
            Assert.Equal(HttpStatusCode.Redirect, recusada.StatusCode);
            Assert.Contains("geral=sem-permissao", recusada.Headers.Location!.OriginalString);
            Assert.Equal("4", await HorasGravadasAsync(anaId));
        }

        // A escolha é só de quem a fez.
        Assert.Null(await HorasGravadasAsync(ruiId));
    }

    [Fact]
    public async Task ASessaoNova_DuraAsHorasQueOUtilizadorEscolheu_ESemEscolhaDuraUmaHora()
    {
        var (ana, anaId) = await CoberturaCApoio.OperadorAsync(_factory, "7703", "Ana Entra", DmoCapabilities.Boquilhas);
        CoberturaCApoio.CriarOperador(_factory, "7704", "Rui Entra", DmoCapabilities.Boquilhas);

        using (var primeira = await EscolherHorasAsync(ana, 8))
        {
            Assert.Equal("8", await HorasGravadasAsync(anaId));
        }

        // Vale a partir da entrada seguinte, e só para quem escolheu.
        using var deAna = await EntrarDeNovoAsync("7703");
        using var deRui = await EntrarDeNovoAsync("7704");
        var agora = DateTimeOffset.UtcNow;
        AssertDuracao(TimeSpan.FromHours(8), ExpiraAs(deAna) - agora);
        AssertDuracao(TimeSpan.FromHours(1), ExpiraAs(deRui) - agora);

        using (var maxima = await EscolherHorasAsync(ana, 24))
        {
            Assert.Equal("24", await HorasGravadasAsync(anaId));
        }

        using var deAnaOutraVez = await EntrarDeNovoAsync("7703");
        AssertDuracao(TimeSpan.FromHours(24), ExpiraAs(deAnaOutraVez) - DateTimeOffset.UtcNow);
    }

    private static void AssertDuracao(TimeSpan esperada, TimeSpan obtida) =>
        Assert.True(Math.Abs((obtida - esperada).TotalMinutes) < 5,
            $"a sessão devia durar cerca de {esperada}, durou {obtida}");

    [Fact]
    public async Task AMinhaConta_EscondeOEmailInventadoDoLoginPorNumero_EMostraOEmailRealDoAdmin()
    {
        var (operador, _) = await CoberturaCApoio.OperadorAsync(_factory, "7705", "Ana Conta", DmoCapabilities.Boquilhas);
        var admin = await CoberturaCApoio.AdminAsync(_factory, "cobc-conta-admin@example.test", "CobCAdmin1!");

        var deOperador = WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(operador, "/Definicoes"));
        // O nº aparece e o email de «7705@dmo.local» (que a app inventou) fica escondido.
        Assert.Matches("<div><dt>Número</dt><dd data-dmo-conta-numero>7705</dd></div>", deOperador);
        var emailDoOperador = Regex.Match(deOperador, "<div([^>]*)><dt>Email</dt>");
        Assert.True(emailDoOperador.Success, "a conta não tem a linha do email");
        Assert.Contains("hidden", emailDoOperador.Groups[1].Value);

        var doAdmin = WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(admin, "/Definicoes"));
        var emailDoAdmin = Regex.Match(doAdmin, "<div([^>]*)><dt>Email</dt><dd data-dmo-conta-email>cobc-conta-admin@example.test</dd>");
        Assert.True(emailDoAdmin.Success, "o email real do ADMIN devia aparecer");
        Assert.DoesNotContain("hidden", emailDoAdmin.Groups[1].Value);
    }

    [Fact]
    public async Task AFichaDoUtilizador_TemOVoltarPorCimaDoTitulo_ENaoRepeteONumeroDeOperadorNosFactos()
    {
        CoberturaCApoio.CriarOperador(_factory, "7711", "Ficha Voltar", DmoCapabilities.Boquilhas);
        var admin = await CoberturaCApoio.AdminAsync(_factory, "cobc-ficha-admin@example.test", "CobCAdmin2!");

        var html = WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(admin, "/Admin/Users/Ficha?operator_id=7711"));

        var voltar = Regex.Match(html, "<a[^>]*id=\"dfuVoltar\"[^>]*>");
        Assert.True(voltar.Success, "a ficha não tem o «← Voltar»");
        Assert.Contains("href=\"/Admin/Users\"", voltar.Value);
        Assert.Contains("data-dmo-voltar=\"ficha-utilizador\"", voltar.Value);
        Assert.Contains("Voltar</a>", html[voltar.Index..]);
        Assert.True(Posicao(html, "id=\"dfuVoltar\"") < Posicao(html, "id=\"dfu-title\""), "o «← Voltar» devia estar por cima do título");

        // O nº de operador está no título do cartão e já não se repete nos factos ao lado.
        Assert.Contains("Ficha Voltar — nº 7711</h3>", html);
        Assert.Contains("Mudança de palavra-passe", html);
        Assert.DoesNotContain("Nº de operador", html);
    }

    /// <summary>O bloco do topo: do nome do utilizador ao «Sair».</summary>
    private static string BlocoDaSessao(string html)
    {
        var bloco = Regex.Match(html, "<div class=\"dmo-shell-conta\">[\\s\\S]*?>Sair</a>");
        Assert.True(bloco.Success, "a página não tem o bloco da sessão no topo");
        return bloco.Value;
    }

    [Fact]
    public async Task OTopo_MostraONomeEOTituloDoOperadorAntesDoSair_ESemTituloOAdminSoTemONome()
    {
        var (operador, _) = await CoberturaCApoio.OperadorAsync(_factory, "7721", "Ana Topo", DmoCapabilities.Boquilhas);
        var admin = await CoberturaCApoio.AdminAsync(_factory, "cobc-topo-admin@example.test", "CobCAdmin3!");

        var deOperador = BlocoDaSessao(WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(operador, "/Definicoes")));
        Assert.Contains("<strong data-user-profile-name>Ana Topo</strong>", deOperador);
        Assert.True(Posicao(deOperador, "data-user-profile-name") < Posicao(deOperador, "data-user-profile-title"));
        Assert.True(Posicao(deOperador, "data-user-profile-title") < Posicao(deOperador, ">Sair<"));

        // O ADMIN não tem template: só o nome e o Sair, sem um título vazio (e sem o «·» que o acompanha).
        var doAdmin = BlocoDaSessao(WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(admin, "/Definicoes")));
        Assert.Contains("data-user-profile-name", doAdmin);
        Assert.DoesNotContain("data-user-profile-title", doAdmin);
        Assert.True(Posicao(doAdmin, "data-user-profile-name") < Posicao(doAdmin, ">Sair<"));
    }
}

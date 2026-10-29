using DMO.Alpha.Core.Controlo;
using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A página /instalar com o login próprio (DMO-382): o formulário que o
/// operador vê, a recusa de cada campo mal preenchido sem criar nada, o ADMIN
/// criado com o código certo e a página a deixar de servir depois.
/// </summary>
public sealed class CoberturaCInstalarPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Codigo = "ABCD-2345";
    private const string Email = "diogo@fabrica.pt";

    private readonly CustomWebApplicationFactory _factory;

    public CoberturaCInstalarPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.LocalCredentials.RemoveRange(db.LocalCredentials);
        db.SaveChanges();
    }

    private WebApplicationFactory<Program> ComLoginProprio() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Dmo:AuthenticationProvider", "Local");
            builder.UseSetting("Dmo:CodigoDeInstalacao", Codigo);
        });

    private static async Task<HttpResponseMessage> InstalarAsync(
        HttpClient cliente, string pagina, string codigo, string email, string palavraPasse, string confirmar) =>
        await CoberturaCApoio.EnviarAsync(cliente, pagina, "/instalar",
            ("codigo", codigo), ("email", email), ("palavraPasse", palavraPasse), ("confirmar", confirmar));

    private async Task<(int Credenciais, string? AdminEmail)> EstadoDaBaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return (await db.LocalCredentials.CountAsync(),
            await db.ModuleSettings.AsNoTracking().Where(s => s.SettingKey == ModuleSettingKeys.AdminEmail).Select(s => s.SettingValue).SingleOrDefaultAsync());
    }

    [Fact]
    public async Task SemOLoginProprio_NaoHaPrimeiraInstalacao_ELevaAoLogin()
    {
        using var cliente = CoberturaCApoio.Cliente(_factory);

        using var resposta = await cliente.GetAsync("/instalar");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/login", resposta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ABaseSemAdmin_MostraOFormularioDaPrimeiraInstalacao()
    {
        using var fabrica = ComLoginProprio();
        using var cliente = CoberturaCApoio.Cliente(fabrica);

        var html = await CoberturaCApoio.PaginaAsync(cliente, "/instalar");

        Assert.Contains("Primeira instalação", WebUtility.HtmlDecode(html));
        foreach (var campo in new[] { "codigo", "email", "palavraPasse", "confirmar" })
        {
            Assert.Matches($"<input[^>]*id=\"{campo}\"[^>]*name=\"{campo}\"", html);
        }

        Assert.Matches("<input[^>]*id=\"palavraPasse\"[^>]*type=\"password\"", html);
        Assert.Matches("<input[^>]*id=\"confirmar\"[^>]*type=\"password\"", html);
        Assert.Contains("Criar ADMIN", html);
        Assert.DoesNotContain("role=\"alert\"", html);
    }

    [Theory]
    [InlineData("ZZZZ-9999", Email, "4321", "4321", "O código de instalação não está certo.")]
    [InlineData(Codigo, "isto-nao-e-um-email", "4321", "4321", "O email indicado não é utilizável.")]
    [InlineData(Codigo, Email, "", "", "Indique e confirme a palavra-passe.")]
    [InlineData(Codigo, Email, "4321", "4322", "As palavras-passe não coincidem.")]
    [InlineData(Codigo, Email, "123", "123", "A palavra-passe deve ter pelo menos 4 caracteres.")]
    public async Task UmCampoMalPreenchido_VoltaComOAviso_GuardaOEmailEscrito_ENadaSeCria(
        string codigo, string email, string palavraPasse, string confirmar, string aviso)
    {
        using var fabrica = ComLoginProprio();
        using var cliente = CoberturaCApoio.Cliente(fabrica);
        var pagina = await CoberturaCApoio.PaginaAsync(cliente, "/instalar");

        using var resposta = await InstalarAsync(cliente, pagina, codigo, email, palavraPasse, confirmar);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());
        Assert.Matches($"<div class=\"alert alert-danger form-message\" role=\"alert\">{Regex.Escape(aviso)}</div>", html);
        Assert.Matches($"<input[^>]*id=\"email\"[^>]*value=\"{Regex.Escape(email)}\"", html);
        Assert.Equal((0, (string?)null), await EstadoDaBaseAsync());
    }

    [Fact]
    public async Task ComOCodigoCerto_CriaOAdmin_VaiParaOLogin_ADepoisDeFeitaJaNaoServe()
    {
        using var fabrica = ComLoginProprio();
        using var cliente = CoberturaCApoio.Cliente(fabrica);
        var pagina = await CoberturaCApoio.PaginaAsync(cliente, "/instalar");

        using var feita = await InstalarAsync(cliente, pagina, "abcd 2345", " " + Email + " ", "4321", "4321");

        Assert.Equal(HttpStatusCode.Redirect, feita.StatusCode);
        Assert.Equal("/login?instalado=true", feita.Headers.Location!.OriginalString);
        Assert.Equal((1, Email), await EstadoDaBaseAsync());
        using (var scope = _factory.Services.CreateScope())
        {
            var hash = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().LocalCredentials.AsNoTracking()
                .Select(c => c.PasswordHash).SingleAsync();
            Assert.False(string.IsNullOrWhiteSpace(hash));
            Assert.DoesNotContain("4321", hash);
        }

        var login = WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(cliente, feita.Headers.Location!.OriginalString));
        Assert.Contains("ADMIN criado. Entre com o email e a palavra-passe que escolheu.", login);

        // Feita, a página deixa de servir (GET e POST), mesmo com o código certo.
        using var outraVez = await cliente.GetAsync("/instalar");
        Assert.Equal(HttpStatusCode.Redirect, outraVez.StatusCode);
        Assert.Equal("/login", outraVez.Headers.Location!.OriginalString);
        using var tentativa = await InstalarAsync(cliente, login, Codigo, "outro@fabrica.pt", "9999", "9999");
        Assert.Equal(HttpStatusCode.Redirect, tentativa.StatusCode);
        Assert.Equal("/login", tentativa.Headers.Location!.OriginalString);
        Assert.Equal((1, Email), await EstadoDaBaseAsync());
    }
}

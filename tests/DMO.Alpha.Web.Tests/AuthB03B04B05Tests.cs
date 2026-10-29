using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A 2.ª vistoria de segurança (Codex/Qoder, 29/10/2026) sobre o login e as
/// sessões — AUTH-B03 (DMO-655, A-0397), AUTH-B04 (DMO-656, A-0398) e AUTH-B05
/// (DMO-657, A-0399).
/// </summary>
public sealed class AuthB03B04B05Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthB03B04B05Tests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<string> TokenAsync(HttpClient client, string path)
    {
        var html = await (await client.GetAsync(path)).Content.ReadAsStringAsync();
        return HtmlFormHelpers.ExtractRequestVerificationToken(html)
            ?? throw new InvalidOperationException($"No antiforgery token on {path}.");
    }

    private async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, params (string name, string value)[] fields)
    {
        var token = await TokenAsync(client, path.Split('?')[0]);
        return await client.PostAsync(path, HtmlFormHelpers.BuildFormPost(fields.Append(("__RequestVerificationToken", token)).ToArray()));
    }

    private void SeedConfiguredAdmin(string email, string providerUserId, string password)
    {
        _factory.AuthProvider.AddAccount(email, providerUserId, password, email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAdminAsync(string email, string password)
    {
        var client = CreateClient();
        var login = await PostAsync(client, "/login", ("identifier", email), ("password", password), ("returnUrl", "/Admin/Users"));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> EntrarNoPostoAsync(HttpClient client, Guid userId, string pin)
    {
        var pagina = await client.GetAsync("/Posto");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await pagina.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("No antiforgery token on /Posto.");
        return await client.PostAsync("/Posto", HtmlFormHelpers.BuildFormPost(
            ("utilizador", userId.ToString()),
            ("pin", pin),
            ("__RequestVerificationToken", token)));
    }

    /// <summary>
    /// AUTH-B03 (DMO-655): repor o acesso de um utilizador apaga também o
    /// PIN do posto que ele tinha — o PIN antigo deixa de entrar e já não
    /// aparece na lista do posto (sem PIN, ninguém o escolhe lá).
    /// </summary>
    [Fact]
    public async Task ResetDeAcesso_ApagaOPinDoPosto_OPinAntigoJaNaoEntra()
    {
        SeedConfiguredAdmin("authb03-admin@example.test", "authb03-admin-provider", "AuthB03Admin1!");
        using var admin = await LoginAdminAsync("authb03-admin@example.test", "AuthB03Admin1!");

        var userId = CoberturaCApoio.CriarOperador(_factory, "9001", "Reparador AuthB03", DmoCapabilities.ReparacaoInterna);

        var ficha = await (await admin.GetAsync("/Admin/Users/Ficha?operator_id=9001")).Content.ReadAsStringAsync();
        var (acao, campos) = EnvioDoFormulario.Preencher(ficha, ("dfu-pin", "3344"));
        var pinGravado = await admin.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, pinGravado.StatusCode);

        // O PIN funciona antes do reset.
        using (var anonimo = CreateClient())
        {
            var antes = await EntrarNoPostoAsync(anonimo, userId, "3344");
            Assert.Equal(HttpStatusCode.Redirect, antes.StatusCode);
        }

        // O ADMIN repõe o acesso.
        var reset = await PostAsync(admin, "/Admin/Users?handler=Reset", ("operator_id", "9001"));
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);

        // O PIN já não está gravado.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var pin = await db.UserSettings.AsNoTracking()
                .Where(s => s.UserId == userId && s.Key == PostoRiRegras.Chave)
                .Select(s => s.Value)
                .SingleOrDefaultAsync();
            Assert.Null(pin);
        }

        // O reparador já não aparece na lista do posto (sem PIN).
        using (var anonimo = CreateClient())
        {
            var posto = await (await anonimo.GetAsync("/Posto")).Content.ReadAsStringAsync();
            Assert.DoesNotContain("Reparador AuthB03", posto);

            // E o PIN antigo, mesmo indicado à força, não entra.
            var depois = await EntrarNoPostoAsync(anonimo, userId, "3344");
            Assert.Equal(HttpStatusCode.OK, depois.StatusCode);
            Assert.Contains("Escolha o seu nome", await depois.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// AUTH-B03 (DMO-655), defesa em profundidade na entrada: com
    /// RequiresPasswordChange=true (o estado logo depois de um reset), a
    /// entrada pelo posto recusa mesmo que, por algum caminho, o PIN ainda
    /// confira (o caminho normal já o impede ao apagar o PIN; este é o
    /// cinturão e suspensórios pedido pela auditoria).
    /// </summary>
    [Fact]
    public async Task ComRequiresPasswordChange_AEntradaPeloPostoRecusaMesmoComOPinCerto()
    {
        var userId = CoberturaCApoio.CriarOperador(_factory, "9002", "Reparador RPC", DmoCapabilities.ReparacaoInterna);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var posto = scope.ServiceProvider.GetRequiredService<IPostoRi>();
            Assert.Equal(PinGravacao.Gravado, await posto.DefinirPinAsync(userId, "5566"));
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.RequiresPasswordChange = true;
            await db.SaveChangesAsync();
        }

        using var anonimo = CreateClient();
        var entrada = await EntrarNoPostoAsync(anonimo, userId, "5566");
        Assert.Equal(HttpStatusCode.OK, entrada.StatusCode);
        // O PIN confere (ConfereAsync), mas o sign-in do posto recusa pelo
        // RequiresPasswordChange — a mensagem é a do SignInPostoAsync, não
        // a do PIN errado.
        Assert.Contains("Credenciais inv", await entrada.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTH-B03 (DMO-655): um PIN novo sobe a versão da sessão — uma sessão
    /// do posto aberta com o PIN anterior deixa de valer no pedido seguinte.
    /// </summary>
    [Fact]
    public async Task MudarOPin_FechaAsSessoesDoPostoAbertasComOPinAnterior()
    {
        SeedConfiguredAdmin("authb03b-admin@example.test", "authb03b-admin-provider", "AuthB03BAdmin1!");
        using var admin = await LoginAdminAsync("authb03b-admin@example.test", "AuthB03BAdmin1!");

        var userId = CoberturaCApoio.CriarOperador(_factory, "9003", "Reparador Troca Pin", DmoCapabilities.ReparacaoInterna);
        using (var scope = _factory.Services.CreateScope())
        {
            var posto = scope.ServiceProvider.GetRequiredService<IPostoRi>();
            Assert.Equal(PinGravacao.Gravado, await posto.DefinirPinAsync(userId, "1111"));
        }

        using var sessao = CreateClient();
        var entrada = await EntrarNoPostoAsync(sessao, userId, "1111");
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sessao.GetAsync("/ReparacaoInterna")).StatusCode);

        // O ADMIN grava um PIN novo (a mesma sessão continua aberta no cliente).
        var ficha = await (await admin.GetAsync("/Admin/Users/Ficha?operator_id=9003")).Content.ReadAsStringAsync();
        var (acao, campos) = EnvioDoFormulario.Preencher(ficha, ("dfu-pin", "2222"));
        var guardou = await admin.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, guardou.StatusCode);

        // A cookie antiga do posto já não vale — tem de entrar de novo.
        var depois = await sessao.GetAsync("/ReparacaoInterna");
        Assert.Equal(HttpStatusCode.Redirect, depois.StatusCode);
        Assert.Contains("/login", depois.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AUTH-B04 (DMO-656): arquivar e depois reativar sobe a versão da
    /// sessão nas duas transições — uma cookie emitida ANTES de arquivar
    /// continua recusada depois de reativar, sem novo login.
    /// </summary>
    [Fact]
    public async Task ArquivarEReativar_ACookieAntigaContinuaRecusadaDepoisDeReativar()
    {
        SeedConfiguredAdmin("authb04-admin@example.test", "authb04-admin-provider", "AuthB04Admin1!");
        using var admin = await LoginAdminAsync("authb04-admin@example.test", "AuthB04Admin1!");

        var operatorId = "9004";
        CoberturaCApoio.CriarOperador(_factory, operatorId, "Operador AuthB04", DMO.Alpha.Core.Admin.DmoCapabilities.JobOnCreate);
        using var sessao = CreateClient();
        var entrada = await PostAsync(sessao, "/login",
            ("identifier", $"{operatorId}@dmo.local"),
            ("password", CoberturaCApoio.PalavraPasseDe(operatorId)),
            ("returnUrl", "/"));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sessao.GetAsync("/JobOn")).StatusCode);

        // Arquiva: a cookie A deixa de valer.
        var arquivar = await PostAsync(admin, "/Admin/Users?handler=StandBy", ("operator_id", operatorId), ("stand_by", "1"));
        Assert.Equal(HttpStatusCode.Redirect, arquivar.StatusCode);
        var depoisDeArquivar = await sessao.GetAsync("/JobOn");
        Assert.Equal(HttpStatusCode.Redirect, depoisDeArquivar.StatusCode);

        // Reativa: SEM novo login, a MESMA cookie A continua recusada — a
        // falha do AUTH-B04 era exatamente deixá-la valer outra vez aqui.
        var reativar = await PostAsync(admin, "/Admin/Users?handler=StandBy", ("operator_id", operatorId), ("stand_by", "0"));
        Assert.Equal(HttpStatusCode.Redirect, reativar.StatusCode);
        var depoisDeReativar = await sessao.GetAsync("/JobOn");
        Assert.Equal(HttpStatusCode.Redirect, depoisDeReativar.StatusCode);
        Assert.Contains("/login", depoisDeReativar.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        // Um login novo, sim, volta a entrar normalmente.
        using var novaSessao = CreateClient();
        var novoLogin = await PostAsync(novaSessao, "/login",
            ("identifier", $"{operatorId}@dmo.local"),
            ("password", CoberturaCApoio.PalavraPasseDe(operatorId)),
            ("returnUrl", "/"));
        Assert.Equal(HttpStatusCode.Redirect, novoLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await novaSessao.GetAsync("/JobOn")).StatusCode);
    }
}

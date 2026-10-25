using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// AS DEFINIÇÕES NUM SÓ SÍTIO (Diogo, 22/10/2026): um separador na barra de
/// cima, ao nível dos módulos; a parte «Pessoal» (só de quem a muda) e a
/// parte «Geral» (para todos, com cadeado e só de leitura para quem não tem
/// a permissão); uma secção por módulo ativo; as páginas antigas levam lá.
/// </summary>
public sealed class DefinicoesNumSoSitioTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DefinicoesNumSoSitioTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.BqRepairers.RemoveRange(db.BqRepairers);
        db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey == PinDasDefinicoesRegras.Chave));
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = PalavraPasseDe(operatorId);
        _factory.AuthProvider.AddAccount(email, $"provider-sitio-{operatorId}", password, email);
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-sitio-{operatorId}", Name = $"Sítio {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-sitio-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
            userId = user.Id;
        }

        return (await LoginAsync(email, password), userId);
    }

    private static string PalavraPasseDe(string operatorId) => $"UmSitio{operatorId}!";

    private async Task<HttpClient> LoginAdminAsync()
    {
        const string email = "sitio-admin@example.test";
        _factory.AuthProvider.AddAccount(email, "sitio-admin-provider", "SitioAdmin1!", email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            db.SaveChanges();
        }

        return await LoginAsync(email, "SitioAdmin1!");
    }

    private static async Task<string> GetOkAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string BarraDeCima(string html) =>
        Regex.Match(html, "<nav class=\"dmo-primary-nav\"[\\s\\S]*?</nav>").Value;

    private static string BarraDoModulo(string html) =>
        Regex.Match(html, "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>").Value;

    private static string Secao(string html, string chave) =>
        Regex.Match(html, $"<section id=\"sec-{chave}\"[^>]*>").Value;

    private static string Menu(string html) =>
        Regex.Match(html, "<nav class=\"def-menu\"[\\s\\S]*?</nav>").Value;

    [Fact]
    public async Task ASDefinicoes_EstaoNaBarraDeCima_EJaNaoNaBarraDosModulos()
    {
        var (client, _) = await LoginOperatorAsync("7301", DmoCapabilities.Boquilhas, DmoCapabilities.Ferramentas, DmoCapabilities.ControloCreate);
        foreach (var url in new[] { "/Boquilhas", "/Ferramentas", "/Controlo/Resumo" })
        {
            var html = await GetOkAsync(client, url);
            Assert.Contains("href=\"/Definicoes\"", BarraDeCima(html));
            Assert.DoesNotContain("Definições", BarraDoModulo(html));
        }

        var definicoes = await GetOkAsync(client, "/Definicoes");
        Assert.Matches("<a href=\"/Definicoes\"[^>]*aria-current=\"page\">Definições</a>", BarraDeCima(definicoes));
        Assert.Contains("<title>Definições", definicoes);
    }

    [Fact]
    public async Task OMenu_PessoalEOsModulosAtivos_CadaEntradaMostraSoASuaSeccao()
    {
        var (client, _) = await LoginOperatorAsync("7310", DmoCapabilities.Boquilhas, DmoCapabilities.ControloCreate);
        var html = await GetOkAsync(client, "/Definicoes");
        var menu = Menu(html);
        var entradas = Regex.Matches(menu, "data-dmo-def-entrada=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(new[] { "pessoal", "boquilhas" }, entradas);
        Assert.Contains("href=\"/Definicoes?secao=boquilhas\"", menu);
        // O Controlo é um grupo no menu, fechado enquanto não está aberto.
        Assert.Contains("data-dmo-def-grupo=\"controlo\"", menu);
        Assert.Contains("aria-expanded=\"false\"", menu);
        Assert.Contains("<div class=\"def-menu-sub\" id=\"defMenu-controlo\" hidden=\"hidden\">", menu);

        // Por omissão, Pessoal: a única secção à vista, marcada no menu.
        Assert.Matches("<a href=\"/Definicoes\\?secao=pessoal\" data-dmo-def-entrada=\"pessoal\" class=\"active\" aria-current=\"page\">", menu);
        Assert.DoesNotContain("hidden", Secao(html, "pessoal"));
        Assert.Contains("hidden", Secao(html, "boquilhas"));
        Assert.Contains("hidden", Secao(html, "controlo"));

        // Sem JavaScript, o link abre a entrada.
        var boquilhas = await GetOkAsync(client, "/Definicoes?secao=boquilhas");
        Assert.DoesNotContain("hidden", Secao(boquilhas, "boquilhas"));
        Assert.Contains("hidden", Secao(boquilhas, "pessoal"));
        Assert.Matches("data-dmo-def-entrada=\"boquilhas\" class=\"active\" aria-current=\"page\"", Menu(boquilhas));

        // Uma entrada que não é do utilizador abre Pessoal.
        Assert.DoesNotContain("hidden", Secao(await GetOkAsync(client, "/Definicoes?secao=armazem"), "pessoal"));
    }

    [Fact]
    public async Task OMenu_TemOCadeadoNosModulos_QuandoOGeralESoDeLeitura()
    {
        var (semPermissao, _) = await LoginOperatorAsync("7311", DmoCapabilities.Boquilhas);
        var menu = Menu(await GetOkAsync(semPermissao, "/Definicoes"));
        Assert.Matches("data-dmo-def-entrada=\"boquilhas\"[^>]*><span>Boquilhas</span><span class=\"def-menu-cadeado\" data-dmo-menu-cadeado", menu);
        Assert.DoesNotMatch("data-dmo-def-entrada=\"pessoal\"[^>]*><span>Pessoal</span><span class=\"def-menu-cadeado\"", menu);

        var (comPermissao, _) = await LoginOperatorAsync("7312", DmoCapabilities.Boquilhas);
        await DefinicoesPin.DesbloquearAsync(_factory.Services, comPermissao);
        Assert.DoesNotContain("data-dmo-menu-cadeado", Menu(await GetOkAsync(comPermissao, "/Definicoes")));
    }

    [Fact]
    public async Task Armazem_SemCadeado_ESemTextoComAListaVazia()
    {
        var (client, _) = await LoginOperatorAsync("7319", DmoCapabilities.ArmazemGestao, DmoCapabilities.Boquilhas);
        var html = await GetOkAsync(client, "/Definicoes?secao=armazem");
        var armazem = Regex.Match(html, "<section id=\"sec-armazem\"[\\s\\S]*?(?=<section id=\"sec-|</main>)").Value;
        Assert.Contains("armDefReparadores", armazem);
        Assert.DoesNotContain("data-dmo-cadeado", armazem);
        Assert.DoesNotContain("def-geral-campos", armazem);
        Assert.DoesNotContain("disabled", armazem);
        Assert.DoesNotContain("Ainda não há reparadores", armazem);
        Assert.DoesNotContain("data-dmo-list=\"Reparadores de CM e MF\"", armazem);
        Assert.DoesNotMatch("data-dmo-def-entrada=\"armazem\"[^>]*><span>[^<]*</span><span class=\"def-menu-cadeado\"", Menu(html));
    }

    [Theory]
    [InlineData("/Boquilhas/Definicoes", "/Definicoes?secao=boquilhas")]
    [InlineData("/Ferramentas/Definicoes", "/Definicoes?secao=ferramentas")]
    [InlineData("/Controlo/Definicoes?save=ok-tolerancia", "/Definicoes?save=ok-tolerancia&secao=controlo")]
    [InlineData("/Armazem/Definicoes", "/Definicoes?secao=armazem")]
    public async Task AsPaginasAntigas_AbremAEntradaDoModulo(string antiga, string nova)
    {
        var (client, _) = await LoginOperatorAsync("7302", DmoCapabilities.Boquilhas, DmoCapabilities.Ferramentas, DmoCapabilities.ControloCreate, DmoCapabilities.ArmazemGestao);
        var response = await client.GetAsync(antiga);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(nova, response.Headers.Location!.OriginalString);
        var chave = nova[(nova.IndexOf("secao=", StringComparison.Ordinal) + 6)..];
        Assert.DoesNotContain("hidden", Secao(await GetOkAsync(client, nova), chave));
    }

    [Fact]
    public async Task SemAPermissao_OGeralLeSeComCadeado_ENaoGrava()
    {
        var (client, _) = await LoginOperatorAsync("7303", DmoCapabilities.Boquilhas);
        var html = await GetOkAsync(client, "/Definicoes?secao=boquilhas");

        Assert.Contains("data-dmo-cadeado", html);
        Assert.Contains("data-dmo-geral=\"leitura\"", html);
        Assert.Matches("<fieldset class=\"def-geral-campos\" disabled=\"disabled\">", html);
        // Ler nunca é bloqueado: a secção das Boquilhas aparece.
        Assert.Contains("id=\"bqDefReparadores\"", html);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var post = await client.PostAsync("/Boquilhas/Definicoes?handler=CreateRepairer",
            HtmlFormHelpers.BuildFormPost(("name", "Sem Permissão"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal("/Definicoes?geral=sem-permissao&secao=boquilhas", post.Headers.Location!.OriginalString);
        Assert.Contains("Abra o cadeado com o PIN das Definições", await GetOkAsync(client, post.Headers.Location!.OriginalString));

        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DmoDbContext>().BqRepairers.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ComOPin_OGeralEditaSe_SemCadeado_EVoltaAEntrada()
    {
        var (client, _) = await LoginOperatorAsync("7304", DmoCapabilities.Boquilhas);
        await DefinicoesPin.DesbloquearAsync(_factory.Services, client);
        var html = await GetOkAsync(client, "/Definicoes");
        Assert.DoesNotContain("data-dmo-cadeado", html);
        Assert.Contains("data-dmo-geral=\"edicao\"", html);
        Assert.Matches("<fieldset class=\"def-geral-campos\">", html);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var post = await client.PostAsync("/Boquilhas/Definicoes?handler=CreateRepairer",
            HtmlFormHelpers.BuildFormPost(("name", "Com Permissão"), ("__RequestVerificationToken", token)));
        Assert.Equal("/Definicoes?save=ok-reparador&secao=boquilhas", post.Headers.Location!.OriginalString);
        var depois = await GetOkAsync(client, post.Headers.Location!.OriginalString);
        Assert.Contains("Com Permissão", depois);
        Assert.DoesNotContain("hidden", Secao(depois, "boquilhas"));
    }

    [Fact]
    public async Task OAdmin_SoVeAsDefinicoesDoAdmin_ENaoDosModulos()
    {
        using var client = await LoginAdminAsync();
        var html = await GetOkAsync(client, "/Definicoes");
        Assert.DoesNotContain("data-dmo-cadeado", html);
        // Diogo, 25/10/2026: «no admin só deve ter definições importantes»; cada módulo só a quem o tem.
        foreach (var id in new[] { "id=\"controlo\"", "id=\"boquilhas\"", "id=\"ferramentas\"", "id=\"armazem\"", "id=\"reparacao\"" })
        {
            Assert.DoesNotContain(id, html);
        }
        Assert.Contains("data-dmo-def-entrada=\"admin\"", html);

        Assert.Matches("<a href=\"/Definicoes\"[^>]*aria-current=\"page\">Definições</a>", html);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var post = await client.PostAsync("/Boquilhas/Definicoes?handler=CreateRepairer",
            HtmlFormHelpers.BuildFormPost(("name", "Do Admin"), ("__RequestVerificationToken", token)));
        Assert.Equal("/Definicoes?save=ok-reparador&secao=boquilhas", post.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task UmModuloInativo_NaoTemSeccao()
    {
        var (client, _) = await LoginOperatorAsync("7305", DmoCapabilities.Boquilhas);
        var html = await GetOkAsync(client, "/Definicoes");
        Assert.Contains("id=\"boquilhas\"", html);
        Assert.DoesNotContain("id=\"controlo\"", html);
        Assert.DoesNotContain("id=\"ferramentas\"", html);
        Assert.DoesNotContain("id=\"armazem\"", html);
        Assert.DoesNotContain("data-dmo-def-entrada=\"controlo\"", html);
        Assert.Contains("data-dmo-def-entrada=\"boquilhas\"", html);
    }

    [Fact]
    public async Task Pessoal_SoTemAContaAPalavraPasseEAAparencia()
    {
        var (client, _) = await LoginOperatorAsync("7313", DmoCapabilities.Boquilhas, DmoCapabilities.ControloCreate, DmoCapabilities.Ferramentas);
        var html = await GetOkAsync(client, "/Definicoes");
        var pessoal = Regex.Match(html, "<section id=\"sec-pessoal\"[\\s\\S]*?(?=<section id=\"sec-controlo\")").Value;
        var cartoes = Regex.Matches(pessoal, "<section id=\"(\\w+)\" class=\"dmo-card").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(new[] { "defConta", "defPalavraPasse" }, cartoes);
        // A aparência é um botão no canto do título de Pessoal; o «Mudar» está no título do cartão.
        Assert.Matches("<div class=\"def-pessoal-topo\">[\\s\\S]*?id=\"defTemaBotao\"", pessoal);
        Assert.Matches("<div class=\"dmo-card-topo-acoes\"><button type=\"submit\" class=\"dmo-button\" id=\"defPalavraGuardar\" form=\"defPalavraPasseForm\">", pessoal);

        var conta = WebUtility.HtmlDecode(Regex.Match(pessoal, "<section id=\"defConta\"[\\s\\S]*?</section>").Value);
        Assert.Contains("<dd data-dmo-conta-nome>Operador 7313</dd>", conta);
        Assert.Contains("<dd data-dmo-conta-email>7313@dmo.local</dd>", conta);
        Assert.Contains("<dd data-dmo-conta-numero>7313</dd>", conta);
        Assert.Contains("<dd data-dmo-conta-modulos>Controlo, Boquilhas, Ferramentas</dd>", conta);
        Assert.DoesNotContain("<input", conta);
    }

    [Fact]
    public async Task AsPastas_SaoGerais_SoAsAcoesFicamParaTodos()
    {
        var (client, _) = await LoginOperatorAsync("7314", DmoCapabilities.Boquilhas, DmoCapabilities.ControloCreate, DmoCapabilities.Ferramentas);
        var html = WebUtility.HtmlDecode(await GetOkAsync(client, "/Definicoes"));

        // Ferramentas: «Utilização pela produção» no Geral; só «Ler agora» fica ativo.
        var producao = Regex.Match(html, "<section id=\"ferrDefSap\"[\\s\\S]*?</section>").Value;
        Assert.Contains("Utilização pela produção", producao);
        Assert.DoesNotContain("SAP<", producao);
        Assert.Contains("data-ferr-sap-pasta disabled=\"disabled\"", producao);
        Assert.Contains("data-ferr-sap-codigos disabled=\"disabled\"", producao);
        Assert.Matches("data-ferr-sap-ler>Ler agora", producao);
        Assert.True(html.IndexOf("id=\"ferrDefSap\"", StringComparison.Ordinal) > html.IndexOf("<div class=\"def-parte\" id=\"ferramentas\">", StringComparison.Ordinal));

        // Boquilhas: a pasta e o ficheiro com cadeado; «Atualizar» fica.
        var excel = Regex.Match(html, "<section id=\"bqDefExcel\"[\\s\\S]*?</section>").Value;
        Assert.Contains("data-bq-excel-pasta disabled=\"disabled\"", excel);
        Assert.Contains("data-bq-excel-escolher disabled=\"disabled\"", excel);
        Assert.DoesNotMatch("data-bq-excel-atualizar[^>]*disabled", excel);

        // Controlo: o nome da pasta é Geral; «Associar» (neste computador) fica.
        var pasta = Regex.Match(html, "<section id=\"ctlDefPastaPdf\"[\\s\\S]*?</section>").Value;
        Assert.Matches("<input id=\"pastaPdfNome\"[^>]*disabled=\"disabled\"", pasta);
        Assert.DoesNotMatch("data-dmo-pasta-pdf-escolher[^>]*disabled", pasta);
        Assert.True(html.IndexOf("id=\"ctlDefPastaPdf\"", StringComparison.Ordinal) > html.IndexOf("<div class=\"def-parte\" id=\"controlo\">", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APastaDosPdfs_GravaSeParaTodos_ComAPermissao()
    {
        var (client, _) = await LoginOperatorAsync("7315", DmoCapabilities.ControloCreate);
        await DefinicoesPin.DesbloquearAsync(_factory.Services, client);
        var html = await GetOkAsync(client, "/Definicoes?secao=controlo");
        Assert.DoesNotMatch("<input id=\"pastaPdfNome\"[^>]*disabled", html);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var post = await client.PostAsync("/Controlo/Definicoes?handler=SavePastaPdf",
            HtmlFormHelpers.BuildFormPost(("pasta", " Controlo 2026 "), ("__RequestVerificationToken", token)));
        Assert.Equal("/Definicoes?pasta=ok&secao=controlo", post.Headers.Location!.OriginalString);
        var depois = await GetOkAsync(client, post.Headers.Location!.OriginalString);
        Assert.Contains("data-dmo-pasta-pdf-esperada=\"Controlo 2026\"", depois);
        Assert.Contains("Pasta dos PDFs guardada.", depois);

        var (outro, _) = await LoginOperatorAsync("7316", DmoCapabilities.ControloCreate);
        var outroHtml = await GetOkAsync(outro, "/Definicoes?secao=controlo");
        Assert.Contains("data-dmo-pasta-pdf-esperada=\"Controlo 2026\"", outroHtml);
        var recusa = await outro.PostAsync("/Controlo/Definicoes?handler=SavePastaPdf",
            HtmlFormHelpers.BuildFormPost(("pasta", "Outra"), ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(outroHtml)!)));
        Assert.Equal("/Definicoes?geral=sem-permissao&secao=controlo", recusa.Headers.Location!.OriginalString);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal("Controlo 2026", (await db.ModuleSettings.AsNoTracking().SingleAsync(s => s.SettingKey == ModuleSettingKeys.ControloPastaPdf)).SettingValue);
        db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey == ModuleSettingKeys.ControloPastaPdf));
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> AbrirCadeadoAsync(HttpClient client, string pin)
    {
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetOkAsync(client, "/Definicoes?secao=boquilhas"))!;
        return await client.PostAsync("/Definicoes?handler=Desbloquear", HtmlFormHelpers.BuildFormPost(
            ("pin", pin), ("secao", "boquilhas"), ("__RequestVerificationToken", token)));
    }

    [Fact]
    public async Task OCadeado_SemPin_PedeAoAdminParaOCriar()
    {
        var (client, _) = await LoginOperatorAsync("7320", DmoCapabilities.Boquilhas);
        var html = WebUtility.HtmlDecode(await GetOkAsync(client, "/Definicoes?secao=boquilhas"));
        Assert.Contains("data-dmo-dialog=\"defPinJanela\"", html);
        Assert.Contains("<dialog id=\"defPinJanela\"", html);
        Assert.Contains("data-dmo-sem-pin>Ainda não há PIN das Definições. Peça ao ADMIN para o criar em Definições → Admin → App.", html);
        Assert.DoesNotContain("id=\"defPinForm\"", html);
        var resposta = await AbrirCadeadoAsync(client, "1234");
        Assert.Equal("/Definicoes?secao=boquilhas&pin=sem-pin", resposta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task OCadeado_AbreComOPin_AteSair()
    {
        await DefinicoesPin.CriarAsync(_factory.Services);
        var (client, _) = await LoginOperatorAsync("7321", DmoCapabilities.Boquilhas);
        var html = await GetOkAsync(client, "/Definicoes?secao=boquilhas");
        Assert.Contains("id=\"defPinForm\"", html);
        Assert.Matches("<fieldset class=\"def-geral-campos\" disabled=\"disabled\">", html);

        var errado = await AbrirCadeadoAsync(client, "0000");
        Assert.Equal("/Definicoes?secao=boquilhas&pin=errado", errado.Headers.Location!.OriginalString);
        Assert.Contains("data-dmo-cadeado", await GetOkAsync(client, "/Definicoes?secao=boquilhas"));

        var certo = await AbrirCadeadoAsync(client, DefinicoesPin.Pin);
        Assert.Equal("/Definicoes?secao=boquilhas&pin=ok", certo.Headers.Location!.OriginalString);
        var aberto = await GetOkAsync(client, certo.Headers.Location!.OriginalString);
        Assert.DoesNotContain("data-dmo-cadeado", aberto);
        Assert.Matches("<fieldset class=\"def-geral-campos\">", aberto);
        Assert.Contains("Cadeado aberto", aberto);

        // Sair fecha o cadeado; ao entrar outra vez está fechado.
        await client.GetAsync("/logout");
        var deNovo = await LoginAsync("7321@dmo.local", PalavraPasseDe("7321"));
        Assert.Contains("data-dmo-cadeado", await GetOkAsync(deNovo, "/Definicoes?secao=boquilhas"));
    }

    [Fact]
    public async Task OCadeado_DepoisDe5PinsErrados_Espera()
    {
        await DefinicoesPin.CriarAsync(_factory.Services);
        var (client, _) = await LoginOperatorAsync("7322", DmoCapabilities.Boquilhas);
        for (var i = 0; i < 5; i++)
        {
            Assert.EndsWith("pin=errado", (await AbrirCadeadoAsync(client, "9999")).Headers.Location!.OriginalString);
        }

        // Mesmo o PIN certo espera: a sexta tentativa não é verificada.
        var espera = await AbrirCadeadoAsync(client, DefinicoesPin.Pin);
        Assert.EndsWith("pin=espera", espera.Headers.Location!.OriginalString);
        Assert.Contains("Demasiadas tentativas erradas", await GetOkAsync(client, espera.Headers.Location!.OriginalString));
    }

    [Fact]
    public async Task OAdmin_CriaOPinNasDefinicoesApp_SoOHashFicaGuardado()
    {
        using var admin = await LoginAdminAsync();
        var setup = await GetOkAsync(admin, "/Definicoes?secao=admin&sub=app");
        Assert.Contains("id=\"admPin\"", setup);
        Assert.Contains("Ainda não há PIN", setup);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(setup)!;

        var diferente = await admin.PostAsync("/Admin/Definicoes?handler=SavePin", HtmlFormHelpers.BuildFormPost(
            ("pin", "1357"), ("pin_confirmar", "1358"), ("__RequestVerificationToken", token)));
        Assert.EndsWith("pinapp=diferente", diferente.Headers.Location!.OriginalString);
        var curto = await admin.PostAsync("/Admin/Definicoes?handler=SavePin", HtmlFormHelpers.BuildFormPost(
            ("pin", "12"), ("pin_confirmar", "12"), ("__RequestVerificationToken", token)));
        Assert.EndsWith("pinapp=invalido", curto.Headers.Location!.OriginalString);

        var gravado = await admin.PostAsync("/Admin/Definicoes?handler=SavePin", HtmlFormHelpers.BuildFormPost(
            ("pin", "1357"), ("pin_confirmar", "1357"), ("__RequestVerificationToken", token)));
        Assert.EndsWith("pinapp=ok", gravado.Headers.Location!.OriginalString);
        Assert.Contains("O PIN está criado.", await GetOkAsync(admin, "/Definicoes?secao=admin&sub=app"));

        using (var scope = _factory.Services.CreateScope())
        {
            var valor = (await scope.ServiceProvider.GetRequiredService<DmoDbContext>().ModuleSettings.AsNoTracking()
                .SingleAsync(s => s.SettingKey == PinDasDefinicoesRegras.Chave)).SettingValue;
            Assert.DoesNotContain("1357", valor);
            Assert.StartsWith("v1.", valor);
        }

        // Um operador abre o cadeado com o PIN novo.
        var (client, _) = await LoginOperatorAsync("7323", DmoCapabilities.Boquilhas);
        Assert.EndsWith("pin=ok", (await AbrirCadeadoAsync(client, "1357")).Headers.Location!.OriginalString);
    }

    private static async Task<HttpResponseMessage> MudarPalavraPasseAsync(HttpClient client, string atual, string nova, string confirmar)
    {
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetOkAsync(client, "/Definicoes"))!;
        return await client.PostAsync("/Definicoes?handler=PalavraPasse", HtmlFormHelpers.BuildFormPost(
            ("atual", atual), ("nova", nova), ("confirmar", confirmar), ("__RequestVerificationToken", token)));
    }

    [Fact]
    public async Task APalavraPasse_MudaComAAtualCerta()
    {
        var (client, _) = await LoginOperatorAsync("7317", DmoCapabilities.Boquilhas);
        var resposta = await MudarPalavraPasseAsync(client, PalavraPasseDe("7317"), "4321", "4321");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Definicoes?secao=pessoal&palavra-passe=ok", resposta.Headers.Location!.OriginalString);
        Assert.Contains("Palavra-passe alterada.", await GetOkAsync(client, resposta.Headers.Location!.OriginalString));
        Assert.True((await _factory.AuthProvider.AuthenticateAsync("7317@dmo.local", "4321")).Success);
        Assert.False((await _factory.AuthProvider.AuthenticateAsync("7317@dmo.local", PalavraPasseDe("7317"))).Success);
    }

    [Fact]
    public async Task APalavraPasse_ComAAtualErrada_AvisaENaoMudaNada()
    {
        var (client, _) = await LoginOperatorAsync("7318", DmoCapabilities.Boquilhas);
        var resposta = await MudarPalavraPasseAsync(client, "errada", "4321", "4321");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Definicoes?secao=pessoal", resposta.Headers.Location!.OriginalString);
        var html = WebUtility.HtmlDecode(await GetOkAsync(client, resposta.Headers.Location!.OriginalString));
        Assert.Contains("data-dmo-palavra-passe-aviso>A palavra-passe atual não está certa. Nada foi alterado.", html);
        Assert.True((await _factory.AuthProvider.AuthenticateAsync("7318@dmo.local", PalavraPasseDe("7318"))).Success);
        Assert.False((await _factory.AuthProvider.AuthenticateAsync("7318@dmo.local", "4321")).Success);

        // As regras do PIN valem também aqui: curta ou diferente não grava.
        var curta = await MudarPalavraPasseAsync(client, PalavraPasseDe("7318"), "12", "12");
        Assert.Contains("pelo menos 4 caracteres", WebUtility.HtmlDecode(await GetOkAsync(client, curta.Headers.Location!.OriginalString)));
        Assert.True((await _factory.AuthProvider.AuthenticateAsync("7318@dmo.local", PalavraPasseDe("7318"))).Success);
    }

    [Fact]
    public async Task OModoEscuro_GuardaSeSoParaQuemOMuda()
    {
        var (ana, anaId) = await LoginOperatorAsync("7306", DmoCapabilities.Boquilhas);
        var (rui, ruiId) = await LoginOperatorAsync("7307", DmoCapabilities.Boquilhas);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await GetOkAsync(ana, "/Definicoes"))!;
        var post = await ana.PostAsync("/Definicoes?handler=Tema",
            HtmlFormHelpers.BuildFormPost(("tema", "dark"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var linha = await db.UserSettings.AsNoTracking().SingleAsync(s => s.UserId == anaId);
            Assert.Equal(UserSettingKeys.Tema, linha.Key);
            Assert.Equal("dark", linha.Value);
            Assert.False(await db.UserSettings.AnyAsync(s => s.UserId == ruiId));
        }

        var deAna = await GetOkAsync(ana, "/Definicoes");
        Assert.Contains("data-dmo-tema-pessoal=\"dark\"", deAna);
        Assert.Contains("<input type=\"hidden\" name=\"tema\" value=\"light\" />", deAna);
        Assert.Contains("localStorage.setItem(\"dmo-tema\",\"dark\")", await GetOkAsync(ana, "/Boquilhas"));
        var deRui = await GetOkAsync(rui, "/Definicoes");
        Assert.Contains("data-dmo-tema-pessoal=\"light\"", deRui);
        Assert.DoesNotContain("localStorage.setItem(\"dmo-tema\",\"dark\")", deRui);

        // Gravar outra vez muda a mesma linha (uma por utilizador e chave).
        var post2 = await ana.PostAsync("/Definicoes?handler=Tema",
            HtmlFormHelpers.BuildFormPost(("tema", "light"), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, post2.StatusCode);
        using var scope2 = _factory.Services.CreateScope();
        Assert.Equal("light", (await scope2.ServiceProvider.GetRequiredService<DmoDbContext>().UserSettings.AsNoTracking().SingleAsync(s => s.UserId == anaId)).Value);
    }

    [Fact]
    public async Task OCadeado_Depois4PinsErrados_OCertoAindaAbre()
    {
        await DefinicoesPin.CriarAsync(_factory.Services);
        var (client, _) = await LoginOperatorAsync("7323", DmoCapabilities.Boquilhas);
        for (var i = 0; i < 4; i++)
        {
            Assert.EndsWith("pin=errado", (await AbrirCadeadoAsync(client, "9999")).Headers.Location!.OriginalString);
        }

        // Só a quinta errada faz esperar: com quatro, o PIN certo abre.
        var certo = await AbrirCadeadoAsync(client, DefinicoesPin.Pin);
        Assert.EndsWith("pin=ok", certo.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AFaixaEmDesenvolvimento_SoNosModulosEmDesenvolvimento()
    {
        var (client, _) = await LoginOperatorAsync("7324",
            DmoCapabilities.Boquilhas, DmoCapabilities.ArmazemGestao, DmoCapabilities.ReparacaoInterna);
        Assert.Contains("data-dmo-em-desenvolvimento", await GetOkAsync(client, "/Armazem/Movimentos"));
        Assert.Contains("data-dmo-em-desenvolvimento", await GetOkAsync(client, "/ReparacaoInterna"));
        Assert.DoesNotContain("data-dmo-em-desenvolvimento", await GetOkAsync(client, "/Boquilhas"));
    }

    [Fact]
    public async Task Controlo_FuncoesNoMenuLateral_UmBotaoPorCartao()
    {
        // Diogo, 22/10/2026: «usa apenas sidepanel, Controlo expande, fica como título, por baixo as funções».
        var (client, _) = await LoginOperatorAsync("7325", DmoCapabilities.ControloCreate);
        var html = await GetOkAsync(client, "/Definicoes?secao=controlo&sub=pegamentos");
        var menu = Menu(html);
        Assert.Contains("data-dmo-def-grupo=\"controlo\" data-aberto>", menu);
        Assert.Contains("aria-expanded=\"true\"", menu);
        Assert.Contains("class=\"dmo-caret\"", menu);
        Assert.Contains("<div class=\"def-menu-sub\" id=\"defMenu-controlo\">", menu);
        Assert.DoesNotContain("def-subtabs", html);
        foreach (var sub in new[] { "peso", "pegamentos", "puncoes", "email", "pdfs" })
        {
            Assert.Contains($"href=\"/Definicoes?secao=controlo&amp;sub={sub}\" data-dmo-ctl-tab=\"{sub}\"", html);
        }

        Assert.Contains("data-dmo-ctl-tab=\"pegamentos\" data-dmo-grupo=\"controlo\" class=\"active\"", html);
        Assert.Contains("data-dmo-ctl-sub=\"pegamentos\" data-dmo-grupo=\"controlo\">", html);
        Assert.Contains("data-dmo-ctl-sub=\"peso\" data-dmo-grupo=\"controlo\" hidden=\"hidden\"", html);
        // Cada área no seu separador.
        Assert.True(html.IndexOf("data-dmo-ctl-sub=\"peso\"", StringComparison.Ordinal) < html.IndexOf("id=\"ctlDefDensidade\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("data-dmo-ctl-sub=\"pdfs\"", StringComparison.Ordinal) < html.IndexOf("id=\"ctlDefPastaPdf\"", StringComparison.Ordinal));
        // Sem «Guardar» ao lado dos campos nem por linha.
        Assert.Contains("form=\"ctlDefPastaForm\"", html);
        Assert.Contains("form=\"ctlDefCriarListaForm\"", html);
        Assert.DoesNotContain("form=\"ctlDefSeccoes-", html);
        // Sem sub, um resultado abre o separador da sua área.
        Assert.Contains("data-dmo-ctl-tab=\"peso\" data-dmo-grupo=\"controlo\" class=\"active\"", await GetOkAsync(client, "/Definicoes?secao=controlo&densidade=ok"));
    }

    [Fact]
    public async Task PuNnpb_UmGuardarParaB1EC3_AsOutrasLinhasFicamComoEstao()
    {
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DmoDbContext>();
            foreach (var codigo in new[] { "B1", "B2", "C3" }.Where(c => !db.Machines.Any(m => m.Code == c)).ToArray())
            {
                db.Machines.Add(new DMO.Alpha.Core.Production.Machine { Code = codigo });
            }

            db.SaveChanges();
        }

        var (client, _) = await LoginOperatorAsync("7326", DmoCapabilities.ControloCreate);
        await DefinicoesPin.CriarAsync(_factory.Services);
        await DefinicoesPin.DesbloquearAsync(_factory.Services, client);
        var html = await GetOkAsync(client, "/Definicoes?secao=controlo&sub=puncoes");
        Assert.Contains("data-dmo-linha=\"B1\"", html);
        Assert.Contains("data-dmo-linha=\"C3\"", html);
        Assert.DoesNotContain("data-dmo-linha=\"B2\"", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "id=\"ctlDefSeccoesGuardar\""));
        int b2Antes;
        using (var antes = _factory.Services.CreateScope())
        {
            b2Antes = await antes.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Controlo.PuncoesLinhaDefinicoes>().SeccoesAsync("B2");
        }

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        var r = await client.PostAsync("/Controlo/Definicoes?handler=SavePuncoesSeccoesTodas", HtmlFormHelpers.BuildFormPost(
            ("maquina", "B1"), ("seccoes", "3"), ("maquina", "C3"), ("seccoes", "4"), ("maquina", "B2"), ("seccoes", "9"),
            ("__RequestVerificationToken", token)));
        Assert.Contains("seccoes=Gravado", r.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var linhas = scope.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Controlo.PuncoesLinhaDefinicoes>();
        Assert.Equal(3, await linhas.SeccoesAsync("B1"));
        Assert.Equal(4, await linhas.SeccoesAsync("C3"));
        // A B2 não está no cartão: o que vier no pedido não a muda.
        Assert.Equal(b2Antes, await linhas.SeccoesAsync("B2"));
    }
}

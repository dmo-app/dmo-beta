using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

/// <summary>
/// Enviar o PDF do Peso por email (Diogo, 19/10/2026, D9: «os pesos devem ter
/// uma opção para enviar… escolher o template B ou C e escolher a lista»; e
/// «essa opção deve existir no resumo do controlo»). As listas e os modelos
/// vivem nas Definições do Controlo; o «Enviar» está no Resumo; o PDF é feito
/// na hora e nunca fica no servidor; fica só o registo do envio. O email sai
/// pelo Outlook do PC (Diogo, 21/10/2026): o «Enviar» devolve um .eml.
/// </summary>
public sealed partial class EnvioEmailPesoPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly CustomWebApplicationFactory _base;
    public EnvioEmailPesoPageTests(CustomWebApplicationFactory factory)
    {
        _base = factory;
        _factory = factory.WithWebHostBuilder(_ => { });
        _base.ResetState();
        ResetDomainState();
    }

    public void Dispose()
    {
        ResetDomainState();
        _factory.Dispose();
    }

    private void ResetDomainState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.EmailEnvios.RemoveRange(db.EmailEnvios);
        db.EmailModelos.RemoveRange(db.EmailModelos);
        db.EmailListas.RemoveRange(db.EmailListas);
        db.PesoMeasurements.RemoveRange(db.PesoMeasurements);
        db.Pesos.RemoveRange(db.Pesos);
        db.Controlos.RemoveRange(db.Controlos);
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.GlassDensities.RemoveRange(db.GlassDensities);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Envio{operatorId}!";
        _base.AuthProvider.AddAccount(email, $"provider-envio-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template
            {
                TemplateKey = $"tpl-envio-{operatorId}",
                Name = $"Envio {operatorId}",
                Capabilities = new[] { DmoCapabilities.ControloCreate }
            };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Operador Envio", ProviderUserId = $"provider-envio-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email), ("password", password), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(login)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await DefinicoesPin.DesbloquearAsync(_factory.Services, client);
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string pageForToken, string url, params (string, string)[] fields)
    {
        var html = await (await client.GetAsync(pageForToken)).Content.ReadAsStringAsync();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        if (url.Contains("handler=RemoverPessoa", StringComparison.Ordinal))
        {
            var id = fields.Single(f => f.Item1 == "lista_id").Item2;
            var (_, atuais) = EnvioDoFormulario.Mudar(html, $"ctl-lista-nome-{id}", "");
            fields = fields.Append(("versao", atuais.Single(c => c.Nome == "versao").Valor)).ToArray();
        }
        return await client.PostAsync(url, HtmlFormHelpers.BuildFormPost(fields.Append(("__RequestVerificationToken", token)).ToArray()));
    }

    private sealed record Producao(int JobOnId, int ControloId);

    /// <summary>A produção 202605 da referência 5447T173 na B1, com a CM T8 e a BQ BQ4.</summary>
    private async Task<Producao> SeedProducaoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var machine = new Machine { Code = "B1" };
        db.Machines.Add(machine);
        db.Tools.AddRange(
            new Tool { ToolId = "T8", Type = ToolType.Cm, Reference = "5447", Lot = "8" },
            new Tool { ToolId = "BQ4", Type = ToolType.Bq, Reference = "T173", Lot = "4" },
            new Tool { ToolId = "MF3", Type = ToolType.Mf, Reference = "M88", Lot = "3" });
        db.ToolTechnicalContexts.AddRange(
            new ToolTechnicalContext { ToolId = "T8", Process = CmProcess.Nnpb, VolumePuncaoCm3 = 8.9m, NominalWeightGrams = 176m },
            new ToolTechnicalContext { ToolId = "BQ4", VolumeMarisaCm3 = 12m });
        db.GlassDensities.Add(new GlassDensity { Process = CmProcess.Nnpb, DensityGramsPerCm3 = 2.4027m });
        var jobOn = new JobOn { Reference = "5447T173", ProductionNumber = "202605", Machine = machine };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "T8" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF3" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ4" });
        await db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        db.Controlos.Add(controlo);
        await db.SaveChangesAsync();
        return new Producao(jobOn.Id, controlo.Id);
    }

    private static async Task CriarPesoAsync(HttpClient client, Producao producao)
    {
        var folha = $"/Controlo/Peso?controlo_id={producao.ControloId}";
        using var criado = await PostAsync(client, folha, "/Controlo/Peso?handler=CreateForControlo",
            ("controlo_id", producao.ControloId.ToString()), ("water_temperature", "24"));
        Assert.Equal(HttpStatusCode.Redirect, criado.StatusCode);
    }

    private (int ListaId, int ModeloId) SeedModelo(string? listaProposta = "Produção")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var lista = new EmailLista { Nome = "Produção", Destinatarios = "producao@fabrica.pt\nchefe@fabrica.pt" };
        db.EmailListas.Add(lista);
        db.SaveChanges();
        var modelo = new EmailModelo
        {
            Nome = "Peso para a produção",
            Remetente = "controlo@fabrica.pt",
            Assunto = "Peso {referencia} · {producao}",
            Corpo = "Segue o Peso da produção {producao} na máquina {maquina}.",
            ListaId = listaProposta is null ? null : lista.Id
        };
        db.EmailModelos.Add(modelo);
        db.SaveChanges();
        return (lista.Id, modelo.Id);
    }

    private List<EmailEnvio> Envios()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailEnvios.AsNoTracking().ToList();
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient client, Producao producao, int modeloId, int listaId) =>
        PostAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}", "/Controlo/EnviarPeso",
            ("jobon_id", producao.JobOnId.ToString()), ("modelo_id", modeloId.ToString()), ("lista_id", listaId.ToString()));

    private static string ZonaDoEnvio(string html)
    {
        var zona = Regex.Match(html, "<div id=\"resumoEnvio\"[\\s\\S]*?</dialog>").Value;
        Assert.False(string.IsNullOrEmpty(zona), "A zona do «Enviar» não está no Resumo.");
        return zona;
    }

    [Fact]
    public async Task Enviar_DevolveOEmailParaOOutlook_ComALista_OAssunto_OCorpo_EOPdf_EFicaORegisto()
    {
        var producao = await SeedProducaoAsync();
        var (listaId, modeloId) = SeedModelo();
        using var client = await LoginAsync("4801");
        await CriarPesoAsync(client, producao);

        using var enviado = await EnviarAsync(client, producao, modeloId, listaId);

        Assert.Equal(HttpStatusCode.OK, enviado.StatusCode);
        Assert.Equal("message/rfc822", enviado.Content.Headers.ContentType!.MediaType);
        Assert.Equal("PESO-5447T173-202605.eml", enviado.Content.Headers.ContentDisposition!.FileNameStar ?? enviado.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        var eml = Encoding.ASCII.GetString(await enviado.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("X-Unsent: 1\r\n", eml);
        Assert.Contains("\r\nFrom: controlo@fabrica.pt\r\n", eml);
        Assert.Contains("\r\nTo: producao@fabrica.pt, chefe@fabrica.pt\r\n", eml);
        Assert.Contains("\r\nSubject: =?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Peso 5447T173 · 202605")) + "?=\r\n", eml);
        var partes = Regex.Matches(eml, "\r\n\r\n([A-Za-z0-9+/=\r\n]+?)--dmo-");
        Assert.Equal(2, partes.Count);
        Assert.Equal("Segue o Peso da produção 202605 na máquina B1.", Encoding.UTF8.GetString(Convert.FromBase64String(partes[0].Groups[1].Value.Replace("\r\n", ""))));
        Assert.Contains("Content-Disposition: attachment; filename=\"PESO-5447T173-202605.pdf\"", eml);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(Convert.FromBase64String(partes[1].Groups[1].Value.Replace("\r\n", "")), 0, 4));

        var envio = Assert.Single(Envios());
        Assert.Equal(producao.ControloId, envio.ControloId);
        Assert.Equal("PESO-5447T173-202605.pdf", envio.Ficheiro);
        Assert.Equal("Peso para a produção", envio.ModeloNome);
        Assert.Equal("Produção", envio.ListaNome);
        Assert.Equal("4801", envio.EnviadoPor);

        var resumo = await GetAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        var zona = ZonaDoEnvio(resumo);
        Assert.Contains("O email abre no Outlook", zona);
        Assert.Contains("data-dmo-dialog-fecha-ao-enviar", zona);
        Assert.Contains("Último email:", zona);
        Assert.Contains("· Produção · por 4801", zona);
    }

    [Fact]
    public async Task Enviar_ComPesoNoAssunto_OAssuntoDoEmailTrazOPesoMedioDaProducao()
    {
        // Diogo, 21/10/2026: «no assunto meto Peso Referencia Maquina Producao
        // e o peso que deu». O {peso} do modelo chega ao Subject do .eml com
        // o peso médio do vidro desta produção, como na folha («NNN,NN g»).
        var producao = await SeedProducaoAsync();
        var (listaId, modeloId) = SeedModelo();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.EmailModelos.Single(m => m.Id == modeloId).Assunto = "Peso {referencia} · {maquina} · {producao} · {peso}";
            db.SaveChanges();
        }
        using var client = await LoginAsync("4809");
        await CriarPesoAsync(client, producao);
        int pesoId;
        using (var scope = _factory.Services.CreateScope())
        {
            pesoId = scope.ServiceProvider.GetRequiredService<DmoDbContext>().Pesos.AsNoTracking()
                .Single(p => p.ControloId == producao.ControloId).Id;
        }
        var folha = $"/Controlo/Peso?controlo_id={producao.ControloId}";
        foreach (var (cm, agua) in new[] { ("1", "176,4"), ("2", "177,1") })
        {
            using var leitura = await PostAsync(client, folha, "/Controlo/Peso?handler=RegisterMeasurement",
                ("peso_id", pesoId.ToString()), ("cm_subject", cm), ("water_weight", agua));
            Assert.Equal(HttpStatusCode.Redirect, leitura.StatusCode);
        }
        decimal media;
        using (var scope = _factory.Services.CreateScope())
        {
            var lido = await scope.ServiceProvider.GetRequiredService<IPesoQuery>().FindPesoAsync(pesoId, CancellationToken.None);
            media = Assert.IsType<decimal>(lido!.Averages?.AverageGlassWeightGrams);
        }
        var pesoTexto = media.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")) + " g";
        Assert.Matches(@"^\d+,\d\d g$", pesoTexto);

        using var enviado = await EnviarAsync(client, producao, modeloId, listaId);

        Assert.Equal(HttpStatusCode.OK, enviado.StatusCode);
        var eml = Encoding.ASCII.GetString(await enviado.Content.ReadAsByteArrayAsync());
        var assunto = "Peso 5447T173 · B1 · 202605 · " + pesoTexto;
        Assert.Contains("\r\nSubject: =?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(assunto)) + "?=\r\n", eml);
        Assert.Equal(assunto, Assert.Single(Envios()).Assunto);
    }

    [Fact]
    public async Task Enviar_SemListaOuSemModelo_VoltaAoResumo_SemEmailNemRegisto_EDizPorque()
    {
        var producao = await SeedProducaoAsync();
        var (listaId, modeloId) = SeedModelo();
        using var client = await LoginAsync("4802");
        await CriarPesoAsync(client, producao);

        using var semLista = await EnviarAsync(client, producao, modeloId, listaId + 1000);
        using var semModelo = await EnviarAsync(client, producao, modeloId + 1000, listaId);

        Assert.Equal($"/Controlo/Resumo?jobon_id={producao.JobOnId}&envio=SemLista#resumoEnvio", semLista.Headers.Location!.ToString());
        Assert.EndsWith("envio=SemModelo#resumoEnvio", semModelo.Headers.Location!.ToString());
        Assert.Empty(Envios());
        var depois = await GetAsync(client, semLista.Headers.Location!.ToString());
        var zona = ZonaDoEnvio(depois);
        Assert.Contains("Escolha uma lista de destinatários. Nenhum email foi preparado.", zona);
        Assert.Contains("href=\"/Definicoes?secao=controlo\"", zona);
    }

    [Fact]
    public async Task Enviar_SemPeso_NaoPreparaEmail()
    {
        var producao = await SeedProducaoAsync();
        var (listaId, modeloId) = SeedModelo();
        using var client = await LoginAsync("4803");

        using var enviado = await EnviarAsync(client, producao, modeloId, listaId);

        Assert.EndsWith("envio=SemPeso#resumoEnvio", enviado.Headers.Location!.ToString());
        Assert.Empty(Envios());
    }

    [Fact]
    public async Task Enviar_SoApareceComPeso_ESemModelosAvisaComLinkParaAsDefinicoes()
    {
        var producao = await SeedProducaoAsync();
        using var client = await LoginAsync("4804");

        var semPeso = await GetAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        Assert.Contains("resumo-sheet-intro", semPeso);
        Assert.DoesNotContain("id=\"resumoEnvio\"", semPeso);

        await CriarPesoAsync(client, producao);
        var comPeso = await GetAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        var zona = ZonaDoEnvio(comPeso);
        Assert.Contains("data-dmo-envio-sem-definicoes", zona);
        Assert.Contains("href=\"/Definicoes?secao=controlo\"", zona);
    }

    [Fact]
    public async Task Definicoes_CriarLista_GuardaOsEmails_EAvisaDosQueNaoSaoEmails()
    {
        using var client = await LoginAsync("4805");

        using var criada = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", " Produção "), ("destinatarios", "producao@fabrica.pt; chefe@fabrica.pt, nao-e-email producao@fabrica.pt"));

        Assert.Equal(HttpStatusCode.Redirect, criada.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var lista = Assert.Single(scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking());
            Assert.Equal("Produção", lista.Nome);
            Assert.Equal("producao@fabrica.pt\nchefe@fabrica.pt", lista.Destinatarios);
        }

        var html = await GetAsync(client, criada.Headers.Location!.ToString());
        Assert.Contains("Lista gravada. Ficaram de fora, por não serem emails: nao-e-email.", html);
        // Uma pessoa por linha, as listas de antes sem nome («—»).
        Assert.Contains("name=\"email\" value=\"producao@fabrica.pt\"", html);
        Assert.Contains("name=\"email\" value=\"chefe@fabrica.pt\"", html);
    }

    [Fact]
    public async Task Definicoes_ListaSemNenhumEmail_NaoGrava_EONomeDeUmaQueExisteJuntaAPessoa()
    {
        using var client = await LoginAsync("4806");
        SeedModelo();

        using var semEmails = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "Qualidade"), ("destinatarios", "qualidade"));
        // Um só botão no cartão (Diogo, 23/10/2026): o nome de uma lista que já existe junta-lhe a pessoa.
        using var junta = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "produção"), ("pessoa", "Outro Nome"), ("destinatarios", "outro@fabrica.pt"));

        Assert.EndsWith("envio=SemEnderecos&secao=controlo", semEmails.Headers.Location!.ToString());
        Assert.Contains("envio=ok-lista", junta.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var lista = Assert.Single(scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking());
        Assert.Equal("Produção", lista.Nome);
        Assert.EndsWith("\nOutro Nome <outro@fabrica.pt>", lista.Destinatarios);
    }

    [Fact]
    public async Task Definicoes_MudarOsEmailsNaLinha_GravaEDevolveOValor()
    {
        using var client = await LoginAsync("4807");
        var (listaId, _) = SeedModelo();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync("/Definicoes")).Content.ReadAsStringAsync())!;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Controlo/Definicoes?handler=MudarLista")
        {
            Content = HtmlFormHelpers.BuildFormPost(("__RequestVerificationToken", token), ("lista_id", listaId.ToString()), ("versao", VersaoDaLista(listaId)), ("destinatarios", "novo@fabrica.pt x"))
        };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"valor\":\"novo@fabrica.pt\"", json);
        Assert.Contains("Ficaram de fora, por não serem emails: x.", System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("aviso").GetString());
        using var scope = _factory.Services.CreateScope();
        var lista = scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking().Single();
        Assert.Equal("Produção", lista.Nome);
        Assert.Equal("novo@fabrica.pt", lista.Destinatarios);
    }

    [Fact]
    public async Task Definicoes_CriarEMudarUmModelo_ERemetenteQueNaoEEmailNaoGrava()
    {
        using var client = await LoginAsync("4808");
        var (listaId, _) = SeedModelo();

        using var criado = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=GuardarModelo",
            ("nome", "Qualidade"), ("remetente", ""), ("assunto", "Peso {producao}"), ("corpo", "Linha 1\r\nLinha 2"), ("lista_id", listaId.ToString()));
        using var recusado = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=GuardarModelo",
            ("nome", "Outro"), ("remetente", "controlo"), ("assunto", ""), ("corpo", ""), ("lista_id", ""));

        Assert.EndsWith("envio=ok-modelo&secao=controlo", criado.Headers.Location!.ToString());
        Assert.EndsWith("envio=RemetenteInvalido&secao=controlo", recusado.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var modelo = scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailModelos.AsNoTracking().Single(m => m.Nome == "Qualidade");
        Assert.Null(modelo.Remetente);
        Assert.Equal("Linha 1\nLinha 2", modelo.Corpo);
        Assert.Equal(listaId, modelo.ListaId);
        Assert.False(scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailModelos.Any(m => m.Nome == "Outro"));

        var html = await GetAsync(client, "/Definicoes");
        Assert.Contains("data-dmo-dialog=\"ctl-modelo-" + modelo.Id + "\">Qualidade</button>", html);
    }

    // AS LISTAS COM NOMES (Diogo, 23/10/2026): colar a lista do Outlook guarda
    // cada pessoa com o seu nome (os nomes com vírgula entre aspas não partem);
    // grava-se «Nome <email>» por linha, na mesma coluna.
    [Fact]
    public async Task Definicoes_ColarListaDoOutlook_GuardaNomesEEmails()
    {
        using var client = await LoginAsync("4821");

        using var criada = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "Qualidade"), ("destinatarios", "\"Leal, Sergio\" <sleal@baglass.com>; \"Marinho, Carlos\" <cmarinho@baglass.com>; plain@x.pt"));

        var location = criada.Headers.Location!.ToString();
        Assert.Contains("envio=ok-lista", location);
        using (var scope = _factory.Services.CreateScope())
        {
            var lista = Assert.Single(scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking());
            Assert.Equal("Leal, Sergio <sleal@baglass.com>\nMarinho, Carlos <cmarinho@baglass.com>\nplain@x.pt", lista.Destinatarios);
            Assert.Contains($"lista={lista.Id}", location);
        }

        var html = await GetAsync(client, location);
        Assert.Contains("name=\"nome\" value=\"Leal, Sergio\" placeholder=\"—\"", html);
        Assert.Contains("name=\"email\" value=\"cmarinho@baglass.com\"", html);
        Assert.Contains("name=\"nome\" placeholder=\"—\"", html);
        Assert.Contains(">3 pessoas</span>", html);
        // Depois de lhe mexer, a lista volta aberta.
        Assert.Matches("<details class=\"ctl-def-lista\" data-dmo-lista-email=\"\\d+\" open=\"open\">", html);
    }

    [Fact]
    public async Task Definicoes_ColarComUmEmailErrado_AvisaENaoOGrava()
    {
        using var client = await LoginAsync("4822");

        using var criada = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "Qualidade"), ("destinatarios", "\"Leal, Sergio\" <sleal@baglass.com>; \"Errado, Ze\" <nao-e-email>"));

        Assert.Contains("envio=aviso-lista", criada.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var lista = Assert.Single(scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking());
        Assert.Equal("Leal, Sergio <sleal@baglass.com>", lista.Destinatarios);
        var html = await GetAsync(client, criada.Headers.Location!.ToString());
        Assert.Contains("Ficaram de fora, por não serem emails: \"Errado, Ze\" <nao-e-email>.", html);
    }

    [Fact]
    public async Task Definicoes_PessoaDaLista_MudarNomeEEmail_Tirar_EJuntar()
    {
        using var client = await LoginAsync("4823");
        var (listaId, _) = SeedModelo();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync("/Definicoes")).Content.ReadAsStringAsync())!;
        async Task<System.Text.Json.JsonElement> MudarAsync(string pessoa, string campo, string valor)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Controlo/Definicoes?handler=MudarPessoa")
            {
                Content = HtmlFormHelpers.BuildFormPost(("__RequestVerificationToken", token), ("lista_id", listaId.ToString()), ("versao", VersaoDaLista(listaId)), ("pessoa", pessoa), (campo, valor))
            };
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await client.SendAsync(request);
            return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        }
        string Destinatarios()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking().Single().Destinatarios;
        }

        var nome = await MudarAsync("1", "nome", "Chefe de Turno");
        Assert.True(nome.GetProperty("ok").GetBoolean());
        Assert.Equal("producao@fabrica.pt\nChefe de Turno <chefe@fabrica.pt>", Destinatarios());

        var email = await MudarAsync("0", "email", "fabrico@fabrica.pt");
        Assert.True(email.GetProperty("ok").GetBoolean());
        Assert.Equal("fabrico@fabrica.pt", email.GetProperty("valor").GetString());

        var errado = await MudarAsync("0", "email", "nao-e-email");
        Assert.False(errado.GetProperty("ok").GetBoolean());
        Assert.Equal("«nao-e-email» não é um email. Não foi gravado.", errado.GetProperty("aviso").GetString());
        var repetido = await MudarAsync("0", "email", "chefe@fabrica.pt");
        Assert.False(repetido.GetProperty("ok").GetBoolean());
        Assert.Equal("fabrico@fabrica.pt\nChefe de Turno <chefe@fabrica.pt>", Destinatarios());

        using var tirada = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=RemoverPessoa",
            ("lista_id", listaId.ToString()), ("pessoa", "0"));
        Assert.Contains("envio=ok-lista", tirada.Headers.Location!.ToString());
        Assert.Equal("Chefe de Turno <chefe@fabrica.pt>", Destinatarios());
        // A última pessoa não sai: a lista ficava sem emails.
        using var ultima = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=RemoverPessoa",
            ("lista_id", listaId.ToString()), ("pessoa", "0"));
        Assert.Contains("envio=SemEnderecos", ultima.Headers.Location!.ToString());

        using var junta = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "Produção"), ("pessoa", "Ana Silva"), ("destinatarios", "asilva@fabrica.pt"));
        Assert.Equal("Chefe de Turno <chefe@fabrica.pt>\nAna Silva <asilva@fabrica.pt>", Destinatarios());
    }

    [Fact]
    public async Task Definicoes_JuntarAUmaLista_UmEmailQueJaLaEsta_NaoDuplica_EAvisa()
    {
        using var client = await LoginAsync("4825");
        var (listaId, _) = SeedModelo();
        string Destinatarios()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking().Single().Destinatarios;
        }
        var antes = Destinatarios();

        using var junta = await PostAsync(client, "/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "Produção"), ("pessoa", "Chefe"), ("destinatarios", "chefe@fabrica.pt"));

        Assert.Equal(antes, Destinatarios());
        Assert.Contains("envio=EmailRepetido", junta.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Definicoes_ListasDeAntes_SemNomes_MostramSeFechadasComOTraco()
    {
        using var client = await LoginAsync("4824");
        var (listaId, _) = SeedModelo();

        var html = await GetAsync(client, "/Definicoes?secao=controlo&sub=email");
        Assert.Contains($"<details class=\"ctl-def-lista\" data-dmo-lista-email=\"{listaId}\">", html);
        Assert.Contains("<summary><span class=\"dmo-caret\" aria-hidden=\"true\"></span><span class=\"ctl-def-lista-nome\">Produção</span>", html);
        Assert.Contains("name=\"email\" value=\"producao@fabrica.pt\"", html);
        Assert.Contains("name=\"nome\" placeholder=\"—\"", html);
        // O espaço entre os campos e as listas é o do cartão.
        Assert.Contains(".ctl-def-listas{display:grid;gap:var(--dmo-space-2);margin-top:var(--dmo-space-4)}", html);
    }
}

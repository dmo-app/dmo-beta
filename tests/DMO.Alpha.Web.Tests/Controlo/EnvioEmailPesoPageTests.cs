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
public sealed class EnvioEmailPesoPageTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
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
        return client;
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string pageForToken, string url, params (string, string)[] fields)
    {
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync(pageForToken)).Content.ReadAsStringAsync())!;
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
        Assert.Contains("href=\"/Controlo/Definicoes\"", zona);
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
        Assert.Contains("href=\"/Controlo/Definicoes\"", zona);
    }

    [Fact]
    public async Task Definicoes_CriarLista_GuardaOsEmails_EAvisaDosQueNaoSaoEmails()
    {
        using var client = await LoginAsync("4805");

        using var criada = await PostAsync(client, "/Controlo/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
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
        Assert.Contains("value=\"producao@fabrica.pt, chefe@fabrica.pt\"", html);
    }

    [Fact]
    public async Task Definicoes_ListaSemNenhumEmail_OuComNomeRepetido_NaoGrava()
    {
        using var client = await LoginAsync("4806");
        SeedModelo();

        using var semEmails = await PostAsync(client, "/Controlo/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "Qualidade"), ("destinatarios", "qualidade"));
        using var repetida = await PostAsync(client, "/Controlo/Definicoes", "/Controlo/Definicoes?handler=CriarLista",
            ("nome", "produção"), ("destinatarios", "outro@fabrica.pt"));

        Assert.EndsWith("envio=SemEnderecos", semEmails.Headers.Location!.ToString());
        Assert.EndsWith("envio=NomeRepetido", repetida.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        Assert.Equal(new[] { "Produção" }, scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailListas.AsNoTracking().Select(l => l.Nome).ToArray());
    }

    [Fact]
    public async Task Definicoes_MudarOsEmailsNaLinha_GravaEDevolveOValor()
    {
        using var client = await LoginAsync("4807");
        var (listaId, _) = SeedModelo();
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync("/Controlo/Definicoes")).Content.ReadAsStringAsync())!;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Controlo/Definicoes?handler=MudarLista")
        {
            Content = HtmlFormHelpers.BuildFormPost(("__RequestVerificationToken", token), ("lista_id", listaId.ToString()), ("destinatarios", "novo@fabrica.pt x"))
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

        using var criado = await PostAsync(client, "/Controlo/Definicoes", "/Controlo/Definicoes?handler=GuardarModelo",
            ("nome", "Qualidade"), ("remetente", ""), ("assunto", "Peso {producao}"), ("corpo", "Linha 1\r\nLinha 2"), ("lista_id", listaId.ToString()));
        using var recusado = await PostAsync(client, "/Controlo/Definicoes", "/Controlo/Definicoes?handler=GuardarModelo",
            ("nome", "Outro"), ("remetente", "controlo"), ("assunto", ""), ("corpo", ""), ("lista_id", ""));

        Assert.EndsWith("envio=ok-modelo", criado.Headers.Location!.ToString());
        Assert.EndsWith("envio=RemetenteInvalido", recusado.Headers.Location!.ToString());
        using var scope = _factory.Services.CreateScope();
        var modelo = scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailModelos.AsNoTracking().Single(m => m.Nome == "Qualidade");
        Assert.Null(modelo.Remetente);
        Assert.Equal("Linha 1\nLinha 2", modelo.Corpo);
        Assert.Equal(listaId, modelo.ListaId);
        Assert.False(scope.ServiceProvider.GetRequiredService<DmoDbContext>().EmailModelos.Any(m => m.Nome == "Outro"));

        var html = await GetAsync(client, "/Controlo/Definicoes");
        Assert.Contains("data-dmo-dialog=\"ctl-modelo-" + modelo.Id + "\">Qualidade</button>", html);
    }
}

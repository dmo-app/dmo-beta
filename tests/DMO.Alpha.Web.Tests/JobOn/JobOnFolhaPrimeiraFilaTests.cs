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
/// A folha do Job On arrumada pelo Diogo (23/10/2026): CM, MF e BQ na
/// primeira fila; o Processo NNPB|PS com o da CM sugerido; «Modo Editar» no
/// lugar da procura e da produção; as notas em todos os cartões, mesmo
/// vazias, para os cartões terem a mesma altura; «Referência» e «Produção» por
/// cima das caixas, sem «Ficha de moldes». Exporta o HTML que o
/// tests/visual/jobon_folha_browser.py abre no Chromium.
/// </summary>
public class JobOnFolhaPrimeiraFilaTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;

    public JobOnFolhaPrimeiraFilaTests(CustomWebApplicationFactory factory)
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
        db.BqContexts.RemoveRange(db.BqContexts);
        db.MfContexts.RemoveRange(db.MfContexts);
        db.CmContexts.RemoveRange(db.CmContexts);
        db.JobOns.RemoveRange(db.JobOns);
        db.ToolTechnicalContexts.RemoveRange(db.ToolTechnicalContexts);
        db.Tools.RemoveRange(db.Tools);
        db.Machines.RemoveRange(db.Machines);
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = "PrimeiraFila1!";
        _factory.AuthProvider.AddAccount(email, "provider-pf-" + operatorId, password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { Id = Guid.NewGuid(), TemplateKey = "tpl-pf-" + operatorId, Name = "Primeira fila " + operatorId, Capabilities = capabilities };
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Planeador " + operatorId, ProviderUserId = "provider-pf-" + operatorId };
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

    /// <summary>CM 5809 lote 1 (NNPB) com nota, MF 5810, BQ T188; duas alterações de hoje, a primeira removida por engano.</summary>
    private async Task<int> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var b2 = new Machine { Code = "B2" };
        db.Machines.Add(b2);
        db.Tools.Add(new Tool { ToolId = "CM-PF", Type = ToolType.Cm, Reference = "5809", Lot = "1" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "CM-PF", Process = CmProcess.Nnpb });
        db.Tools.Add(new Tool { ToolId = "MF-PF", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        db.Tools.Add(new Tool { ToolId = "BQ-PF", Type = ToolType.Bq, Reference = "T188", Lot = "21" });
        var jobOn = new JobOn
        {
            Reference = "5810T188", ProductionNumber = "202602", Machine = b2,
            InicioEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(-2),
            FimEm = DateOnly.FromDateTime(DmoBusinessTime.Now).AddDays(5), CaloteMm = 3.5m
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "CM-PF" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "MF-PF" });
        db.BqContexts.Add(new BqContext { JobOn = jobOn, ToolId = "BQ-PF" });
        var agora = DateTime.UtcNow;
        db.JobOnChanges.AddRange(
            new JobOnChange
            {
                JobOn = jobOn, Field = JobOnChangeFields.Bq, OldValueFrozen = "T188 · lote 20", NewValueFrozen = "T188 · lote 21",
                ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora.AddMinutes(-3),
                RemovedAtUtc = agora.AddMinutes(-1), RemovedByNameFrozen = "Diogo"
            },
            new JobOnChange
            {
                JobOn = jobOn, Field = JobOnChangeFields.End, OldValueFrozen = null, NewValueFrozen = "31/12/2026",
                ChangedByNameFrozen = "Diogo", ChangedAtUtc = agora.AddMinutes(-2)
            });
        await db.SaveChangesAsync();
        db.JobOnFolhas.Add(new JobOnFolha { JobOnId = jobOn.Id, CmNotas = "CX rodar justa 1/4 de volta.", MfCorpo = "54.02/52.02/54.02/44.10/45.20", MfCorpoTol = "0.05", MfGargalo = "21.5" });
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    private static async Task<string> PageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Cartao(string html, string familia)
    {
        var inicio = html.IndexOf($"data-jof-familia=\"{familia}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"A folha não tem o cartão {familia}.");
        var fim = html.IndexOf("data-jof-familia=", inicio + 20, StringComparison.Ordinal);
        return fim < 0 ? html[inicio..] : html[inicio..fim];
    }

    [Fact]
    public async Task Folha_PrimeiraFila_CmMfBq_NotasEmTodosOsCartoes_RotulosSemFichaDeMoldes()
    {
        var jobOnId = await SeedAsync();
        using var client = await LoginAsync("7301", DmoCapabilities.JobOnCreate);

        var html = await PageAsync(client, $"/JobOn/Folha?jobon_id={jobOnId}&vista=alteracoes");
        ExportarParaOBrowser.Gravar("jobon-folha.html", html);

        // CM, MF e BQ (com o AN) na primeira fila, por esta ordem.
        var moldes = html.IndexOf("id=\"gMoldes\"", StringComparison.Ordinal);
        Assert.True(moldes >= 0);
        var fila = html[moldes..html.IndexOf("</section>", moldes, StringComparison.Ordinal)];
        var cm = fila.IndexOf("data-jof-familia=\"CM\"", StringComparison.Ordinal);
        var mf = fila.IndexOf("data-jof-familia=\"MF\"", StringComparison.Ordinal);
        var bq = fila.IndexOf("data-jof-familia=\"BQ\"", StringComparison.Ordinal);
        Assert.True(cm >= 0 && cm < mf && mf < bq, $"A primeira fila não é CM, MF, BQ ({cm}, {mf}, {bq}).");

        // As notas em cada cartão, mesmo vazias (o MF não tem nota escrita).
        Assert.Contains("CX rodar justa 1/4 de volta.", Cartao(html, "CM"));
        Assert.Contains("class=\"note-box note-view\" data-jof-campo=\"cm_notas\" data-jof-nota", html);
        foreach (var familia in new[] { "CM", "MF", "BQ", "TP", "CS" })
        {
            Assert.Contains("data-jof-nota", Cartao(html, familia));
        }
        Assert.Contains("class=\"note-box note-view is-empty\" data-jof-campo=\"mf_notas\" data-jof-nota", html);

        // «O que mudou» no fim da folha, depois do último cartão (Diogo, 23/10/2026).
        var resumo = html.IndexOf("id=\"fichaMudancas\"", StringComparison.Ordinal);
        var ultimoCartao = html.LastIndexOf("data-jof-familia=", StringComparison.Ordinal);
        Assert.True(resumo >= 0 && ultimoCartao >= 0, $"Falta o resumo ({resumo}) ou os cartões ({ultimoCartao}).");
        Assert.True(resumo > ultimoCartao, $"O resumo ({resumo}) não vem depois do último cartão ({ultimoCartao}).");

        // A ordem dos cartões (Diogo, 23/10/2026): TP e CS logo a seguir aos
        // moldes, depois os Calibres; o PI na coluna da direita, depois das Notas gerais.
        var tp = html.IndexOf("data-jof-familia=\"TP\"", StringComparison.Ordinal);
        var cs = html.IndexOf("data-jof-familia=\"CS\"", StringComparison.Ordinal);
        var pu = html.IndexOf("data-jof-familia=\"PU\"", StringComparison.Ordinal);
        var cal = html.IndexOf("cal-card", StringComparison.Ordinal);
        var notasGerais = html.IndexOf("id=\"notesTitle\"", StringComparison.Ordinal);
        var piCartao = html.IndexOf("data-jof-familia=\"PI\"", StringComparison.Ordinal);
        Assert.True(new[] { bq, tp, cs, pu, cal, notasGerais, piCartao }.All(i => i >= 0), "Falta um dos cartões.");
        var mfHtml = html.IndexOf("data-jof-familia=\"MF\"", StringComparison.Ordinal);
        var bqHtml = html.IndexOf("data-jof-familia=\"BQ\"", StringComparison.Ordinal);
        Assert.True(tp < mfHtml && mfHtml < cs && cs < bqHtml && bqHtml < pu && pu < cal, $"A ordem não é CM+TP, MF+CS, BQ+AN, PU+ARR, Calibres ({tp}, {mfHtml}, {cs}, {bqHtml}, {pu}, {cal}).");
        Assert.True(notasGerais < piCartao, $"O PI não vem depois das Notas gerais ({notasGerais}, {piCartao}).");
        var lado = html.IndexOf("<aside class=\"side\">", StringComparison.Ordinal);
        Assert.True(lado >= 0 && lado < piCartao && piCartao < html.IndexOf("</aside>", lado, StringComparison.Ordinal), "O PI não está na coluna da direita.");

        // A imagem do artigo.
        Assert.Contains("data-jof-artigo", html);

        // «Referência» e «Produção» por cima das caixas; «Ficha de moldes» saiu; sem «Modo Editar».
        Assert.Contains("<label class=\"pick-rotulo\" for=\"jofReferencia\">Referência</label>", html);
        Assert.Contains("<label class=\"pick-rotulo\" for=\"jofProducoes\">Produção</label>", html);
        Assert.DoesNotContain("Ficha de moldes", html);
        Assert.DoesNotContain("data-jof-modo-editar", html);
        Assert.Contains($"href=\"/JobOn/Folha?jobon_id={jobOnId}&amp;editar=1\" data-jof-editar>Editar</a>", html);
    }

    [Fact]
    public async Task Folha_CartoesNaoColapsam_ComLarguraMinimaEMedidasSemScroll()
    {
        // Um cartão (PU, ARR…) ficou uma faixa estreita com uma letra por linha (Diogo, 26/10/2026):
        // cada cartão tem largura mínima, a grelha tem 4 colunas iguais e as medidas não têm scroll.
        var css = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "DMO.Alpha.Web", "wwwroot", "css", "dmo-jobon-folha.css"));
        Assert.Contains(".board>.card.sec{min-width:150px", css);
        Assert.Contains(".zones>.fcol{display:flex;flex-direction:column", css);
        Assert.Contains("grid-template-columns:repeat(4,minmax(0,1fr))", css);
        Assert.Contains(".more-body .table-wrap{overflow-x:visible}", css);
    }

    [Fact]
    public async Task Folha_TpMostraACaloteGravada_NuncaAPalavraBacia()
    {
        var jobOnId = await SeedAsync();
        using var client = await LoginAsync("7303", DmoCapabilities.JobOnCreate);

        var html = await PageAsync(client, $"/JobOn/Folha?jobon_id={jobOnId}");

        var tp = Cartao(html, "TP");
        Assert.Contains("3.50", tp);
        Assert.DoesNotContain("bacia", tp, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Editar_ModoEditarNoLugarDaProcura_EOProcessoNnpbPs_ComODaCmSugerido()
    {
        var jobOnId = await SeedAsync();
        using var client = await LoginAsync("7302", DmoCapabilities.JobOnCreate);

        var html = await PageAsync(client, $"/JobOn/Folha?jobon_id={jobOnId}&editar=1");
        ExportarParaOBrowser.Gravar("jobon-folha-editar.html", html);

        Assert.Contains("data-jof-modo-editar>Modo Editar</div>", html);
        Assert.DoesNotContain("id=\"jofReferencia\"", html);
        Assert.DoesNotContain("id=\"jofProducoes\"", html);
        Assert.DoesNotContain("Ficha de moldes", html);

        var processo = Regex.Match(html, "<select\\b[^>]*data-edit=\"cab_processo\"[^>]*>(?<o>.*?)</select>", RegexOptions.Singleline);
        Assert.True(processo.Success, "O Processo não é uma lista no Editar.");
        Assert.Contains("data-jof-processo-sugerido=\"cm\"", processo.Value);
        var opcoes = Regex.Matches(processo.Groups["o"].Value, "<option value=\"(?<v>[^\"]*)\"(?<s> selected=\"selected\")?")
            .Select(m => (m.Groups["v"].Value, m.Groups["s"].Success)).ToList();
        Assert.Equal([("", false), ("NNPB", true), ("PS", false)], opcoes);
    }

    [Fact]
    public async Task SoVer_NaoTemEditar_NemModoEditar_MesmoPedindoOEditar()
    {
        var jobOnId = await SeedAsync();
        using var client = await LoginAsync("7303", DmoCapabilities.JobOnView);

        var vista = await PageAsync(client, $"/JobOn/Folha?jobon_id={jobOnId}");
        Assert.DoesNotContain("data-jof-editar", vista);
        Assert.DoesNotContain("data-jof-modo-editar", vista);

        var pedido = await PageAsync(client, $"/JobOn/Folha?jobon_id={jobOnId}&editar=1");
        Assert.DoesNotContain("data-jof-modo-editar", pedido);
        Assert.DoesNotContain("data-edit=\"cab_processo\"", pedido);
        Assert.Contains("id=\"jofReferencia\"", pedido);
    }

    // Os Dias ao centro, entre as barras (Diogo, 25/10/2026): fim − início + 1; sem fim, «—».
    [Theory]
    [InlineData(0, 0, "1")]
    [InlineData(-2, 0, "3")]
    [InlineData(-2, null, "—")]
    public async Task Dias_AoCentro_FimMenosInicioMaisUm(int inicio, int? fim, string esperado)
    {
        var jobOnId = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var jobOn = db.JobOns.Single(j => j.Id == jobOnId);
            var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
            jobOn.InicioEm = hoje.AddDays(inicio);
            jobOn.FimEm = fim is int f ? hoje.AddDays(f) : null;
            await db.SaveChangesAsync();
        }
        using var client = await LoginAsync("7304", DmoCapabilities.JobOnCreate);

        var html = await PageAsync(client, $"/JobOn/Folha?jobon_id={jobOnId}");
        var dias = Regex.Match(html, "<div class=\"fact-col fact-dias\">\\s*<span class=\"fl\">Dias</span><span class=\"fv\"><strong[^>]*>(?<v>[^<]*)</strong></span>\\s*</div>");
        Assert.True(dias.Success, "Os Dias não estão sozinhos na sua coluna (ao centro, sem a linha vazia).");
        Assert.Equal(esperado, dias.Groups["v"].Value);
    }

    // Os números da folha com duas casas decimais no máximo e, quando têm casas, com ponto e
    // duas casas (Diogo, 23/10/2026: «quando não tem fica sem os 00»; 26/10: «2.5 fica 2.50»).
    [Theory]
    [InlineData("365.000", "365")]
    [InlineData("36.850", "36.85")]
    [InlineData("2,50", "2.50")]
    [InlineData("12.345", "12.35")]
    [InlineData("12", "12")]
    [InlineData("1/2 volta", "1/2 volta")]
    public async Task Peso_DuasCasasDecimaisNoMaximo_ComPonto(string gravado, string esperado)
    {
        var jobOnId = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.JobOnFolhas.Single(f => f.JobOnId == jobOnId).CabPeso = gravado;
            await db.SaveChangesAsync();
        }
        using var client = await LoginAsync("7305", DmoCapabilities.JobOnCreate);

        var html = await PageAsync(client, $"/JobOn/Folha?jobon_id={jobOnId}");
        var peso = Regex.Match(html, "<span class=\"fl\">Peso</span>\\s*<span class=\"fv\">\\s*<strong>(?<v>[^<]*)</strong>");
        Assert.True(peso.Success, "Sem o Peso no cabeçalho.");
        Assert.Equal(esperado, System.Net.WebUtility.HtmlDecode(peso.Groups["v"].Value));
    }
}

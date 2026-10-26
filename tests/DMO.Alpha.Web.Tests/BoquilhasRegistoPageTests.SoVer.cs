using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// «Boquilhas · ver» (Diogo, 22/10/2026: «para ele é ver apenas os
/// registos de forma simples»): quem só vê entra no Histórico, em leitura,
/// com o painel das linhas; «Boquilhas · criar» fica como estava.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    private const string ViewHome = "/Boquilhas?historico=1&vista=movimentos";

    private void SeedViewOnlyUser(string operatorId, string providerUserId, string password)
    {
        SeedUser(operatorId, "Chefe", providerUserId, password, withOperationalAccess: false);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var user = db.Users.Single(u => u.OperatorId == operatorId);
        var template = new Template { TemplateKey = "tpl-bq-ver-" + operatorId, Name = "Chefe", Capabilities = [DmoCapabilities.BoquilhasView] };
        db.Templates.Add(template);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }

    private static string HistoryZone(string html)
    {
        var start = html.IndexOf("<section id=\"historico\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "a página não tem o Histórico");
        var end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        Assert.True(end > start, "o Histórico não fecha");
        // O painel das linhas fica por baixo do título, dentro da secção (mockups, 26/10/2026), mas não
        // faz parte do Histórico: verifica-se à parte. Tira-se da zona, como quando estava ao lado.
        return System.Text.RegularExpressions.Regex.Replace(html[start..end], "<aside class=\"beta-light-rail beta-production-rail\"[\\s\\S]*?</aside>", "");
    }

    [Fact]
    public async Task Quem_so_ve_as_Boquilhas_ve_os_registos_no_Historico_sem_formularios_de_registo()
    {
        await SeedToolAsync("BQ-VER-1", "RGT771", "4");
        var trace = await SeedTraceAsync("BQ-VER-1");
        await SeedMovementAsync(trace.Id, "saida", 3);
        await SeedProductionAsync("BQ-VER-1", "REF-VER", "PRD-VER-1", "B1");
        SeedViewOnlyUser("4801", "provider-bq-ver-1", "VerPass1!");
        using var client = await LoginClientAsync("4801", "VerPass1!");

        // A entrada do módulo é o Histórico.
        var entry = await client.GetAsync(PagePath);
        Assert.Equal(HttpStatusCode.Redirect, entry.StatusCode);
        Assert.Equal(ViewHome, entry.Headers.Location?.OriginalString);

        var html = await GetPageHtmlAsync(client, ViewHome);
        ExportarParaOBrowser.Gravar("boquilhas-ver.html", html);
        var zone = HistoryZone(html);
        Assert.Contains("RGT771 · Lote 4", zone);
        Assert.Contains("data-dmo-row", zone);
        Assert.DoesNotContain("method=\"post\"", zone);
        Assert.DoesNotContain("data-dmo-open", zone);
        Assert.Contains("<label for=\"maquina\">Linha</label>", zone);
        Assert.Contains("Um clique num dia do calendário ou numa linha do painel filtra a lista.", zone);
        Assert.DoesNotContain("abre a ficha", zone);
        Assert.DoesNotContain("abre-a com", zone);
        Assert.DoesNotContain("RegisterMovement", html);
        Assert.DoesNotContain("RegistarSemJobOn", html);
        Assert.DoesNotContain("href=\"/Boquilhas/Definicoes\"", html);
        Assert.Contains("<h2>Movimentos<", zone);
        Assert.Contains(">Movimentos</a>", html);
        Assert.DoesNotContain(">Histórico</a>", html);
        Assert.DoesNotContain("href=\"/Boquilhas?ficheiros=1\"", html);

        // As outras vistas e páginas das Boquilhas voltam ao Histórico.
        foreach (var path in new[] { $"/Boquilhas?trace_id={trace.Id}", "/Boquilhas?ficheiros=1", "/Boquilhas/Definicoes" })
        {
            var refused = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal(ViewHome, refused.Headers.Location?.OriginalString);
        }
    }

    [Fact]
    public async Task Quem_so_ve_as_Boquilhas_e_recusado_ao_registar_e_nada_fica_gravado()
    {
        await SeedToolAsync("BQ-VER-2", "RGT772", "1");
        var trace = await SeedTraceAsync("BQ-VER-2");
        SeedViewOnlyUser("4802", "provider-bq-ver-2", "VerPass2!");
        using var client = await LoginClientAsync("4802", "VerPass2!");

        var token = await GetVerificationTokenAsync(client, "/login");
        foreach (var handler in new[] { "RegisterMovement", "RegistarSemJobOn" })
        {
            var response = await client.PostAsync($"{PagePath}?handler={handler}", HtmlFormHelpers.BuildFormPost(
                ("trace_id", trace.Id.ToString()),
                ("tool_id", "BQ-VER-2"),
                ("movement_type", "saida"),
                ("quantity", "2"),
                ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Empty(await ReadMovementsAsync(trace.Id));
    }

    [Fact]
    public async Task Quem_cria_nas_Boquilhas_continua_a_registar_e_a_abrir_os_registos()
    {
        await SeedToolAsync("BQ-VER-3", "RGT773", "1");
        var trace = await SeedTraceAsync("BQ-VER-3");
        await SeedMovementAsync(trace.Id, "saida", 3);
        SeedUser("4803", "Criar", "provider-bq-ver-3", "VerPass3!");
        using var client = await LoginClientAsync("4803", "VerPass3!");

        var registo = await GetPageHtmlAsync(client, $"/Boquilhas?trace_id={trace.Id}");
        Assert.Contains("RegisterMovement", registo);
        Assert.Contains("href=\"/Boquilhas?ficheiros=1\"", registo);

        var historico = HistoryZone(await GetPageHtmlAsync(client, ViewHome + "&bq_ferramenta=BQ-VER-3"));
        Assert.Contains("RGT773 · Lote 1", historico);
        Assert.Contains("data-dmo-open", historico);

        var response = await PostMovementAsync(client, $"/Boquilhas?trace_id={trace.Id}", trace.Id, "entrada", 3);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(2, (await ReadMovementsAsync(trace.Id)).Count);
    }

    [Fact]
    public async Task O_painel_de_quem_so_ve_abre_os_movimentos_da_producao_escolhida_e_so_esses()
    {
        await SeedToolAsync("BQ-VER-A", "RGT781", "1");
        await SeedToolAsync("BQ-VER-B", "RGT782", "2");
        var producaoA = await SeedProductionAsync("BQ-VER-A", "REF-A", "PRD-A-10", "B2");
        var producaoB = await SeedProductionAsync("BQ-VER-B", "REF-B", "PRD-A-100", "C3");
        var traceA = await SeedTraceAsync("BQ-VER-A", producaoA.Id);
        var traceB = await SeedTraceAsync("BQ-VER-B", producaoB.Id);
        await SeedMovementAsync(traceA.Id, "saida", 2);
        await SeedMovementAsync(traceA.Id, "entrada", 2);
        await SeedMovementAsync(traceB.Id, "saida", 5);
        SeedViewOnlyUser("4804", "provider-bq-ver-4", "VerPass4!");
        using var client = await LoginClientAsync("4804", "VerPass4!");

        // O link vem do painel que a página desenha, não montado à mão.
        var home = await GetPageHtmlAsync(client, ViewHome);
        var painelInicio = home.IndexOf("id=\"bqCurrentLines\"", StringComparison.Ordinal);
        Assert.True(painelInicio >= 0, "a página não tem o painel das linhas");
        var painel = home[painelInicio..home.IndexOf("</aside>", painelInicio, StringComparison.Ordinal)];
        Assert.Contains("PRD-A-10", painel);
        var links = System.Text.RegularExpressions.Regex.Matches(painel, "data-dmo-open=\"([^\"]*jobon_id=[0-9]+[^\"]*)\"")
            .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        var jobOnA = producaoA.JobOnId!.Value;
        var linkA = Assert.Single(links, l => l.EndsWith($"jobon_id={jobOnA}", StringComparison.Ordinal));
        Assert.Equal($"{ViewHome}&jobon_id={jobOnA}", linkA);

        var zone = HistoryZone(await GetPageHtmlAsync(client, linkA));
        Assert.Contains("Produção PRD-A-10 · REF-A · Linha B2", zone);
        var lista = zone[zone.IndexOf("data-dmo-list=\"Movimentos detalhados\"", StringComparison.Ordinal)..];
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(lista, "data-dmo-row").Count);
        Assert.Contains("RGT781 · Lote 1", lista);
        Assert.DoesNotContain("RGT782", lista);
        Assert.DoesNotContain("PRD-A-100", lista);
    }
}

using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Uma folha de Pegamentos por combinação de ferramentas (
/// 18/10/2026). Antes: depois de trocar a CM a meio da produção, a leitura
/// seguinte ia para a folha da CM antiga, medida contra a CM nova. Agora a
/// primeira leitura começa uma folha nova e a página diz porquê; a folha
/// anterior abre com as ferramentas dela, sem linha nova, e as leituras
/// dela ainda se corrigem; o Resumo, a Revisão e o histórico mostram as
/// duas, e o alerta da segunda não fica escondido.
/// </summary>
public partial class PegamentosPageTests
{
    /// <summary>
    /// O J10 com uma leitura de CM dentro do corredor na T3 (nominal 70.00)
    /// e, depois de trocar a CM pela T4 (5447 lote 4, nominal 70.50), uma
    /// leitura fora (71.00) na folha nova. Devolve os ids.
    /// </summary>
    private async Task<(int ControloId, int JobOnId, int OldSheetId, int OldReadingId)> SeedTwoSheetsAsync(HttpClient client)
    {
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (var first = await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00"))
        {
            Assert.Equal(url, first.Headers.Location!.ToString());
        }

        var (jobOnId, oldSheetId, oldReadingId) = await SwapCmAsync(controloId);
        using var after = await PostMeasurementAsync(client, url, controloId, "CM", "71.00", "71.00");
        Assert.Equal($"{url}&folha=nova", after.Headers.Location!.ToString());
        return (controloId, jobOnId, oldSheetId, oldReadingId);
    }

    /// <summary>Troca a CM do J10 pela T4 no Editar do Job On; devolve a folha e a leitura que já existiam.</summary>
    private async Task<(int JobOnId, int SheetId, int ReadingId)> SwapCmAsync(int controloId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = "T4", Type = ToolType.Cm, Reference = "5447", Lot = "4" });
        db.ToolTechnicalContexts.Add(new ToolTechnicalContext { ToolId = "T4", Process = CmProcess.Nnpb, DiametroGargaloMm = 70.50m });
        await db.SaveChangesAsync();
        var jobOn = await db.Controlos.AsNoTracking().Where(c => c.Id == controloId)
            .Select(c => new { c.JobOnId, c.JobOn!.MachineId }).SingleAsync();
        var swapped = await scope.ServiceProvider.GetRequiredService<IUpdateJobOnHandler>().HandleAsync(
            new UpdateJobOn(jobOn.JobOnId, jobOn.MachineId, "T4", "MF-TOOL-9", "BQ-1"));
        Assert.Equal(UpdateJobOnResult.Success, swapped.Result);
        var sheet = await db.Pegamentos.AsNoTracking().SingleAsync(p => p.ControloId == controloId);
        var reading = await db.PegamentosMeasurements.AsNoTracking().SingleAsync(m => m.PegamentosId == sheet.Id);
        return (jobOn.JobOnId, sheet.Id, reading.Id);
    }

    private static string Section(string html, string marker)
    {
        var section = System.Text.RegularExpressions.Regex.Match(html, marker + "[\\s\\S]*?</section>");
        Assert.True(section.Success, $"a página não tem a secção {marker}");
        return section.Value;
    }

    [Fact]
    public async Task SingleSheet_PageLooksAsBefore_NoSheetsCard()
    {
        SeedUser("4801", "Pegamentos Operador", "provider-peg-folha-1", "PegFolha1!");
        using var client = await LoginClientAsync("4801", "PegFolha1!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00")) { }

        var html = await GetPageHtmlAsync(client, url);

        Assert.Contains("<div id=\"pegFolhas\" class=\"dmo-zona\" data-dmo-regiao>", html);
        Assert.DoesNotContain("Folhas desta produção", html);
        Assert.Contains("congelada no registo desta produção", html);
    }

    [Fact]
    public async Task CmChanged_PageIsReadyForANewSheet_TheFirstReadingStartsIt_AndThePageSaysSo()
    {
        SeedUser("4802", "Pegamentos Operador", "provider-peg-folha-2", "PegFolha2!");
        using var client = await LoginClientAsync("4802", "PegFolha2!");
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";
        using (await PostMeasurementAsync(client, url, controloId, "CM", "70.10", "69.90", "70.00")) { }
        // A página aberta antes da troca (outro separador) mostra a folha da T3.
        var stale = await GetPageHtmlAsync(client, url);
        var (_, oldSheetId, _) = await SwapCmAsync(controloId);

        // Antes da primeira leitura: a folha nova está pronta, com a T4.
        var pending = await GetPageHtmlAsync(client, url);
        Assert.Contains("As ferramentas mudaram: a próxima leitura começa uma folha nova com estas ferramentas.", pending);
        Assert.Contains("Folhas desta produção", pending);
        Assert.Contains("começa na próxima leitura · aberta", pending);
        Assert.Contains("<dt>Lote</dt><dd>4</dd>", Section(pending, "data-peg-component=\"CM\""));

        // Registar a partir da página antiga: a leitura vai para a folha da
        // T4 e a página abre inteira nessa folha, com o aviso.
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(stale)!;
        using var posted = await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("component", "CM"), ("costura", "71.00"),
            ("folha_vista", oldSheetId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal($"{url}&folha=nova", posted.Headers.Location!.ToString());

        var html = await GetPageHtmlAsync(client, posted.Headers.Location!.ToString());
        Assert.Contains("data-dmo-toast=\"aviso\"", html);
        Assert.Contains("A CM mudou: esta leitura começou uma folha nova. A folha anterior ficou como estava.", html);
        var folhas = System.Text.RegularExpressions.Regex.Match(html, "data-dmo-list=\"Folhas desta produção\"[\\s\\S]*?</table>").Value;
        Assert.Contains($"/Controlo/Pegamentos?controlo_id={controloId}&amp;pegamentos_id={oldSheetId}", folhas);
        Assert.Contains("Ferramentas anteriores", folhas);
        Assert.Contains("Ferramentas atuais · aberta", folhas);
        Assert.Contains("1 · 1 fora", folhas);
        Assert.Contains("<dt>Lote</dt><dd>4</dd>", Section(html, "data-peg-component=\"CM\""));
        Assert.Contains("congelada nesta folha", html);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var sheets = await db.Pegamentos.AsNoTracking().Where(p => p.ControloId == controloId).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(new[] { "T3", "T4" }, sheets.Select(s => s.CmToolId));
        Assert.Equal(1, await db.PegamentosMeasurements.CountAsync(m => m.PegamentosId == oldSheetId));
    }

    [Fact]
    public async Task OldSheet_OpensWithItsTools_NoNewReadingRow_CorrectionReturnsToIt_PdfNamesFollowTheSheet()
    {
        SeedUser("4803", "Pegamentos Operador", "provider-peg-folha-3", "PegFolha3!");
        using var client = await LoginClientAsync("4803", "PegFolha3!");
        var (controloId, _, oldSheetId, oldReadingId) = await SeedTwoSheetsAsync(client);
        var url = $"{PagePath}?controlo_id={controloId}";
        var oldUrl = $"{url}&pegamentos_id={oldSheetId}";

        var html = await GetPageHtmlAsync(client, oldUrl);

        var card = Section(html, "data-peg-component=\"CM\"");
        Assert.Contains("<dt>Lote</dt><dd>3</dd>", card);
        Assert.Contains("70.10", card);
        Assert.DoesNotContain("id=\"costura-Cm\"", html);
        Assert.DoesNotContain("data-peg-mais-linha", html);
        Assert.Contains("Esta folha é de ferramentas que já não estão na produção: não recebe leituras novas.", html);
        Assert.Contains($"<a class=\"dmo-button\" href=\"/Controlo/Pegamentos?controlo_id={controloId}\">Abrir a folha atual</a>", html);
        Assert.Contains($"name=\"pegamentos_id\" value=\"{oldSheetId}\"", html);

        // Corrigir uma leitura da folha antiga volta a ela.
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        using var edited = await client.PostAsync($"{PagePath}?handler=EditMeasurement", HtmlFormHelpers.BuildFormPost(
            ("controlo_id", controloId.ToString()), ("pegamentos_id", oldSheetId.ToString()),
            ("measurement_id", oldReadingId.ToString()), ("costura", "70.15"), ("contra_costura", "69.95"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(oldUrl, edited.Headers.Location!.ToString());
        Assert.Contains("70.15", Section(await GetPageHtmlAsync(client, oldUrl), "data-peg-component=\"CM\""));

        // O PDF de cada folha: a primeira com o nome de sempre, a segunda com «-2».
        async Task<string?> PdfNameAsync(params (string, string)[] fields)
        {
            var pdfToken = await GetVerificationTokenAsync(client, url);
            using var response = await client.PostAsync($"{PagePath}?handler=GeneratePdf",
                HtmlFormHelpers.BuildFormPost(fields.Append(("__RequestVerificationToken", pdfToken)).ToArray()));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        }
        Assert.Equal("PEGAMENTOS-5810-J10.pdf", await PdfNameAsync(("controlo_id", controloId.ToString()), ("pegamentos_id", oldSheetId.ToString())));
        Assert.Equal("PEGAMENTOS-5810-J10-2.pdf", await PdfNameAsync(("controlo_id", controloId.ToString())));
    }

    [Fact]
    public async Task EscolhaDoJobOn_DepoisDeTrocarACm_AbreAFolhaDasFerramentasAtuais()
    {
        // A escolha do Job On numa linha e sem recarregar (deep sweep 2,
        // 18/10/2026, F-8) com as folhas por combinação de ferramentas
        // (beta.236): escolher a produção na escolha do Job On abre, como
        // antes, a folha da produção pelo controlo_id, que é a das
        // ferramentas atuais (a T4); a folha da T3 abre-se pelo cartão
        // «Folhas desta produção».
        SeedUser("4806", "Pegamentos Operador", "provider-peg-folha-6", "PegFolha6!");
        using var client = await LoginClientAsync("4806", "PegFolha6!");
        var (controloId, jobOnId, oldSheetId, _) = await SeedTwoSheetsAsync(client);

        var escolha = await GetPageHtmlAsync(client, $"{PagePath}?modo=jobon&referencia=5810");
        var producao = Controlo.PesoZonas.Zona(escolha, "pegJobOnProducaoZona");
        Assert.Contains("<select id=\"pegJobOn\" name=\"jobon_id\" form=\"pegJobOnEscolha\" required data-dmo-autosubmit=\"valor\" data-dmo-depende=\"pegJobOnReferencia\" data-dmo-depende-valor=\"5810\">", producao);
        Assert.Contains($"<option value=\"{jobOnId}\">J10 · B1</option>", producao);
        var abrir = System.Text.RegularExpressions.Regex.Match(escolha, "<form [^>]*id=\"pegJobOnEscolha\"[^>]*>").Value;
        Assert.Contains("handler=AbrirJobOn", abrir);
        Assert.Contains("data-dmo-nativo", abrir);

        var token = HtmlFormHelpers.ExtractRequestVerificationToken(escolha)!;
        using var aberto = await client.PostAsync($"{PagePath}?handler=AbrirJobOn", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, aberto.StatusCode);
        Assert.Equal($"{PagePath}?controlo_id={controloId}", aberto.Headers.Location!.ToString());

        var html = await GetPageHtmlAsync(client, aberto.Headers.Location!.ToString());
        Assert.Contains("<dt>Lote</dt><dd>4</dd>", Section(html, "data-peg-component=\"CM\""));
        Assert.Contains("id=\"costura-Cm\"", html);
        var folhas = System.Text.RegularExpressions.Regex.Match(html, "data-dmo-list=\"Folhas desta produção\"[\\s\\S]*?</table>").Value;
        Assert.Contains("Ferramentas atuais · aberta", folhas);
        Assert.Contains($"/Controlo/Pegamentos?controlo_id={controloId}&amp;pegamentos_id={oldSheetId}", folhas);
    }

    [Fact]
    public async Task ReviewerScreens_ShowBothSheets_TheAlertOfTheSecondIsNotHidden()
    {
        SeedUser("4804", "Pegamentos Operador", "provider-peg-folha-4", "PegFolha4!");
        using var client = await LoginClientAsync("4804", "PegFolha4!");
        var (controloId, jobOnId, oldSheetId, _) = await SeedTwoSheetsAsync(client);

        // O Resumo: o tom do cartão é o da pior folha e cada folha tem a sua linha.
        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}");
        Assert.Contains("resumo-linked-card resumo-linked-peg nok", resumo);
        Assert.Contains("Existem medições fora das tolerâncias", resumo);
        var linhas = System.Text.RegularExpressions.Regex.Match(resumo, "<ul class=\"resumo-peg-folhas\"[\\s\\S]*?</ul>").Value;
        Assert.Contains($"pegamentos_id={oldSheetId}\">Folha 1</a>", linhas);
        Assert.Contains($"controlo_id={controloId}\">Folha 2</a>", linhas);
        Assert.Contains("CM 5447 · BQ T194 · MF 5810", linhas);
        Assert.Contains("1 em alerta", linhas);

        // O histórico: cada linha abre a sua folha.
        using var aberta = await client.GetAsync($"/Controlo/Peso?historico=1&abrir=Pegamentos:{oldSheetId}:{controloId}");
        Assert.Equal(HttpStatusCode.Redirect, aberta.StatusCode);
        Assert.Equal($"{PagePath}?controlo_id={controloId}&pegamentos_id={oldSheetId}", aberta.Headers.Location!.ToString());

        // A Revisão: a linha soma as duas folhas e cada folha tem a sua.
        SeedApprover("4805", "provider-peg-folha-5", "PegFolha5!");
        using var chefe = await LoginClientAsync("4805", "PegFolha5!");
        var approve = await GetPageHtmlAsync(chefe, $"/Controlo/Approve?jobon_id={jobOnId}");
        // As duas folhas têm um alerta cada: a 1.ª pela ovalização +0,20 (F8).
        Assert.Contains("2 medição(ões) · <span class=\"revisao-alerta\">2 alerta(s) dimensionais</span> · 2 folhas", approve);
        Assert.Contains("Folha 2 · CM 5447 · BQ T194 · MF 5810 · 1 medição(ões) · <span class=\"revisao-alerta\">1 alerta(s)</span>", approve);

        var detalhe = await GetPageHtmlAsync(chefe, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos");
        Assert.Contains("Folha 1 · ", detalhe);
        Assert.Contains("ferramentas anteriores", detalhe);
        Assert.Contains("Folha 2 · ", detalhe);
        Assert.Contains("Lote 3", detalhe);
        Assert.Contains("Lote 4", detalhe);
        Assert.Contains("<span class=\"peg-state-bad\">fora</span>", detalhe);
    }

    /// <summary>Um chefe de aprovação (Controlo Aprovar, sem criar).</summary>
    private void SeedApprover(string operatorId, string providerUserId, string password)
    {
        _factory.AuthProvider.AddAccount($"{operatorId}@dmo.local", providerUserId, password, $"{operatorId}@dmo.local");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var template = db.Templates.FirstOrDefault(t => t.TemplateKey == "tpl-teste-chefe-folhas");
        if (template is null)
        {
            template = new Template
            {
                TemplateKey = "tpl-teste-chefe-folhas",
                Name = "Chefe de aprovação",
                Capabilities = new[] { DmoCapabilities.JobOnCreate, DmoCapabilities.ControloApprove }
            };
            db.Templates.Add(template);
            db.SaveChanges();
        }

        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Chefe Folhas", ProviderUserId = providerUserId };
        db.Users.Add(user);
        db.SaveChanges();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }
}

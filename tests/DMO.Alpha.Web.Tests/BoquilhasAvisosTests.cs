using System.Net;
using DMO.Alpha.Core.Boquilhas;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Regra do proprietário de 16/10/2026 («na app toda não devem existir
/// blocks… tentar sempre warning, desde que não parta nada»), nas
/// Boquilhas: a hora da mudança de hora passa para a hora seguinte e avisa;
/// o «Associar Job On» encontra a produção pelo nº e só avisa se a
/// referência for diferente; a quantidade ilegível continua recusada, agora
/// com o texto da causa.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    private static string Unescaped(HttpResponseMessage response) =>
        Uri.UnescapeDataString(response.Headers.Location?.OriginalString ?? string.Empty);

    // Só o formulário «Associar Job On»: a página mostra as produções da BQ noutros cards.
    private static string AssociateForm(string html)
    {
        var form = System.Text.RegularExpressions.Regex.Match(html,
            "<form id=\"bqAssociarJobOn\" method=\"post\"[^>]*handler=AssociateJobOn[^>]*>(.*?)</form>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(form.Success, "a página não tem o formulário «Associar Job On»");
        return form.Groups[1].Value;
    }

    [Fact]
    public async Task PostMovement_TimeInTheDstGap_SavesAtTheNextHourAndWarns()
    {
        await SeedToolAsync("BQ-AVISO-1", "AVS101", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-1");
        SeedUser("4130", "Avisos", "provider-aviso-1", "AvisoPass1!");
        using var client = await LoginClientAsync("4130", "AvisoPass1!");

        // 29/03/2026 01:30 não existe em Lisboa (01:00 passa a 02:00).
        using var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "entrada", 5, "2026-03-29T01:30");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Unescaped(response);
        Assert.DoesNotContain("movement_refusal", location);
        Assert.Contains("hora_indicada=2026-03-29T01:30", location);
        Assert.Contains("hora_gravada=2026-03-29T02:30", location);

        // Gravado às 02:30 de Lisboa (01:30 UTC), com a quantidade indicada.
        var persisted = Assert.Single(await ReadMovementsAsync(trace.Id));
        Assert.Equal(5, persisted.Quantity);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), persisted.OccurredAtUtc);

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains(
            "<div data-dmo-toast=\"aviso\" role=\"status\">A hora indicada (29/03/2026 01:30) não existe em Portugal continental por causa da mudança de hora. " +
            "O movimento foi gravado às 02:30 de 29/03/2026. Corrija o movimento se a hora real for outra. <a class=\"dmo-aviso-link\" href=\"",
            html);
        Assert.DoesNotContain("role=\"alert\">A hora indicada", html);
    }

    [Fact]
    public async Task PostMovement_ValidTime_HasNoWarning()
    {
        await SeedToolAsync("BQ-AVISO-2", "AVS102", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-2");
        SeedUser("4131", "Avisos", "provider-aviso-2", "AvisoPass2!");
        using var client = await LoginClientAsync("4131", "AvisoPass2!");

        using var response = await PostMovementAsync(client, TraceUrl(trace.Id), trace.Id, "saida", 2, "2026-03-29T03:00");

        // A data indicada volta ao form (lançar guias seguidas); sem aviso de mudança de hora.
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}&data_antiga=2026-03-29T03%3A00", response.Headers.Location?.OriginalString);
        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.DoesNotContain("mudança de hora", html);
    }

    [Fact]
    public async Task PostMovement_UnreadableQuantity_ShowsTheCauseInsteadOfAnEmptyBox()
    {
        await SeedToolAsync("BQ-AVISO-3", "AVS103", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-3");
        SeedUser("4132", "Avisos", "provider-aviso-3", "AvisoPass3!");
        using var client = await LoginClientAsync("4132", "AvisoPass3!");

        var token = await GetVerificationTokenAsync(client, TraceUrl(trace.Id));
        using var response = await client.PostAsync(
            $"{PagePath}?handler=RegisterMovement",
            HtmlFormHelpers.BuildFormPost(
                ("trace_id", trace.Id.ToString()),
                ("movement_type", "saida"),
                ("quantity", "abc"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("movement_refusal=InvalidQuantity", response.Headers.Location!.OriginalString);
        Assert.Empty(await ReadMovementsAsync(trace.Id));

        var html = await GetPageHtmlAsync(client, response.Headers.Location.OriginalString);
        Assert.Contains(
            "<div data-dmo-toast=\"erro\" class=\"result\" role=\"alert\">Indique a quantidade como um número inteiro igual ou superior a 1. Nenhum movimento foi registado.</div>",
            html);
        Assert.DoesNotContain("class=\"result\" role=\"alert\"></div>", html);
    }

    [Fact]
    public async Task EditMovement_TimeInTheDstGap_SavesAtTheNextHourAndWarns()
    {
        await SeedToolAsync("BQ-AVISO-4", "AVS104", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-4");
        await SeedMovementAsync(trace.Id, "saida", 3);
        var movementId = (await ReadMovementsAsync(trace.Id)).Single().Id;
        SeedUser("4133", "Avisos", "provider-aviso-4", "AvisoPass4!");
        using var client = await LoginClientAsync("4133", "AvisoPass4!");

        using var response = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "saida"),
            ("quantity", "3"),
            ("occurred_at", "2026-03-29T01:15"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Unescaped(response);
        Assert.Contains("editar_mov=1", location);
        Assert.DoesNotContain("edit_refusal", location);
        Assert.Contains("hora_indicada=2026-03-29T01:15", location);
        Assert.Contains("hora_gravada=2026-03-29T02:15", location);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 15, 0, DateTimeKind.Utc),
            Assert.Single(await ReadMovementsAsync(trace.Id)).OccurredAtUtc);

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("O movimento foi gravado às 02:15 de 29/03/2026.", html);
        // Em modo de correção, o aviso aparece uma vez (junto da correção).
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "foi gravado às 02:15"));
        // É o resultado da gravação: vai para o canto e não empurra a janela (regra 32).
        Assert.Contains("<div data-dmo-toast=\"aviso\" role=\"status\">A hora indicada (29/03/2026 01:15)", html);
    }

    [Fact]
    public async Task EditMovement_UnreadableDate_IsRefusedWithAReadableCause()
    {
        await SeedToolAsync("BQ-AVISO-5", "AVS105", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-5");
        await SeedMovementAsync(trace.Id, "saida", 3);
        var movementId = (await ReadMovementsAsync(trace.Id)).Single().Id;
        SeedUser("4134", "Avisos", "provider-aviso-5", "AvisoPass5!");
        using var client = await LoginClientAsync("4134", "AvisoPass5!");

        using var response = await PostCorrectionAsync(client, "EditMovement", EditModeUrl(trace.Id, movementId),
            ("trace_id", trace.Id.ToString()),
            ("movement_id", movementId.ToString()),
            ("movement_type", "saida"),
            ("quantity", "3"),
            ("occurred_at", "ontem"));

        Assert.Contains("edit_refusal=InvalidOccurrenceTime", response.Headers.Location!.OriginalString);
        var html = await GetPageHtmlAsync(client, response.Headers.Location.OriginalString);
        Assert.Contains("A data e hora indicadas não são legíveis.", html);
    }

    [Fact]
    public async Task PostAssociateJobOn_ByNumberWithADifferentReference_AssociatesAndWarns()
    {
        await SeedToolAsync("BQ-AVISO-6", "AVS106", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-6");
        await SeedMovementAsync(trace.Id, "saida", 2);
        var bq = await SeedProductionAsync("BQ-AVISO-6", "ZQREF-AV6", "PN-551106", "B1");
        SeedUser("4135", "Avisos", "provider-aviso-6", "AvisoPass6!");
        using var client = await LoginClientAsync("4135", "AvisoPass6!");

        using var response = await PostAssociateJobOnAsync(
            client, TraceUrl(trace.Id), trace.Id, "PN-551106", "ZQREF-ERRADA");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Unescaped(response);
        Assert.DoesNotContain("association_refusal", location);
        Assert.Contains("referencia_indicada=ZQREF-ERRADA", location);

        // Associado pela produção do nº; os movimentos mantêm-se.
        Assert.Equal(bq.Id, (await ReadTraceAsync(trace.Id))!.BqContextId);
        Assert.Single(await ReadMovementsAsync(trace.Id));

        var html = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains(
            "<div data-dmo-toast=\"aviso\" role=\"status\">A referência indicada (ZQREF-ERRADA) não é a referência da produção PN-551106 (ZQREF-AV6). " +
            "O registo foi associado pelo número de produção. Se a produção não é esta, contacte o ADMIN.</div>",
            html);
    }

    [Fact]
    public async Task PostAssociateJobOn_SameNumberInSeveralReferences_PicksTheProductionWithThisBq()
    {
        // O mesmo nº existe em várias referências (17/10/2026): ganha a
        // produção que tem a BQ do registo e, entre essas, a da referência
        // indicada.
        await SeedToolAsync("BQ-AVISO-9", "AVS109", "1");
        await SeedToolAsync("BQ-AVISO-10", "AVS110", "1");
        var onlyOneHasBq = await SeedTraceAsync("BQ-AVISO-9");
        var bothHaveBq = await SeedTraceAsync("BQ-AVISO-10");
        var bq9 = await SeedProductionAsync("BQ-AVISO-9", "ZQREF-AV9", "PN-551109", "BX1");
        await SeedProductionAsync("BQ-AVISO-10", "ZQREF-OUTRA", "PN-551109", "BX2");
        var bq10 = await SeedProductionAsync("BQ-AVISO-10", "ZQREF-AV10", "PN-551109", "BX3");
        SeedUser("4137", "Avisos", "provider-aviso-9", "AvisoPass9!");
        using var client = await LoginClientAsync("4137", "AvisoPass9!");

        using var byBq = await PostAssociateJobOnAsync(
            client, TraceUrl(onlyOneHasBq.Id), onlyOneHasBq.Id, "PN-551109", "");
        Assert.Equal($"/Boquilhas?trace_id={onlyOneHasBq.Id}", byBq.Headers.Location?.OriginalString);
        Assert.Equal(bq9.Id, (await ReadTraceAsync(onlyOneHasBq.Id))!.BqContextId);

        using var byReference = await PostAssociateJobOnAsync(
            client, TraceUrl(bothHaveBq.Id), bothHaveBq.Id, "PN-551109", "zqref-av10");
        Assert.Equal($"/Boquilhas?trace_id={bothHaveBq.Id}", byReference.Headers.Location?.OriginalString);
        Assert.Equal(bq10.Id, (await ReadTraceAsync(bothHaveBq.Id))!.BqContextId);
    }

    [Fact]
    public async Task AssociateForm_SendsTheChosenReference_AndOnlyItsProductionIsAssociated()
    {
        // Deep sweep (18/10/2026): o formulário da lista de produções leva a
        // própria produção escolhida (o nº só é único junto com a
        // referência). Antes só levava o nº, e com o mesmo nº noutra
        // referência com esta BQ associava a produção mais recente dessa
        // outra referência, sem volta. Um POST só com o nº e a referência
        // continua a escolher pela referência.
        await SeedToolAsync("BQ-AVISO-11", "AVS111", "1");
        await SeedToolAsync("BQ-AVISO-12", "AVS112", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-11");
        var escolhida = await SeedProductionAsync("BQ-AVISO-11", "ZQREF-AV11", "PN-551111", "BX4");
        await SeedProductionAsync("BQ-AVISO-11", "ZQREF-OUTRA11", "PN-551111", "BX5");
        // A produção escolhida sem a BQ do registo é recusada com a causa,
        // em vez de associar a da outra referência que a tem.
        var semBq = await SeedTraceAsync("BQ-AVISO-11");
        await SeedProductionAsync("BQ-AVISO-12", "ZQREF-AV12", "PN-551112", "BX6");
        await SeedProductionAsync("BQ-AVISO-11", "ZQREF-OUTRA12", "PN-551112", "BX7");
        SeedUser("4138", "Avisos", "provider-aviso-11", "AvisoPass11!");
        using var client = await LoginClientAsync("4138", "AvisoPass11!");

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=ZQREF-AV11");
        var form = AssociateForm(html);
        Assert.Contains($"<option value=\"{escolhida.JobOnId}\">ZQREF-AV11 · PN-551111 · BX4</option>", form);
        Assert.DoesNotContain("name=\"job_reference\"", form);

        using var associated = await PostAssociateJobOnAsync(
            client, TraceUrl(trace.Id), trace.Id, "PN-551111", "ZQREF-AV11");
        Assert.Equal(HttpStatusCode.Redirect, associated.StatusCode);
        Assert.Equal(escolhida.Id, (await ReadTraceAsync(trace.Id))!.BqContextId);

        using var withoutBq = await PostAssociateJobOnAsync(
            client, TraceUrl(semBq.Id), semBq.Id, "PN-551112", "ZQREF-AV12");
        Assert.Equal(HttpStatusCode.Redirect, withoutBq.StatusCode);
        Assert.Contains("association_refusal=BqNotInProduction", Unescaped(withoutBq));
        Assert.Null((await ReadTraceAsync(semBq.Id))!.BqContextId);
    }

    [Fact]
    public async Task PostAssociateJobOn_ByNumberOnlyOrSameReference_AssociatesWithoutWarning()
    {
        await SeedToolAsync("BQ-AVISO-7", "AVS107", "1");
        await SeedToolAsync("BQ-AVISO-8", "AVS108", "1");
        var withoutReference = await SeedTraceAsync("BQ-AVISO-7");
        var sameReference = await SeedTraceAsync("BQ-AVISO-8");
        var bq7 = await SeedProductionAsync("BQ-AVISO-7", "ZQREF-AV7", "PN-551107", "B2");
        var bq8 = await SeedProductionAsync("BQ-AVISO-8", "ZQREF-AV8", "PN-551108", "B3");
        SeedUser("4136", "Avisos", "provider-aviso-7", "AvisoPass7!");
        using var client = await LoginClientAsync("4136", "AvisoPass7!");

        // Só o nº: a referência é opcional.
        using var byNumber = await PostAssociateJobOnAsync(
            client, TraceUrl(withoutReference.Id), withoutReference.Id, "PN-551107", "");
        Assert.Equal($"/Boquilhas?trace_id={withoutReference.Id}", byNumber.Headers.Location?.OriginalString);
        Assert.Equal(bq7.Id, (await ReadTraceAsync(withoutReference.Id))!.BqContextId);

        // A mesma referência (sem distinguir maiúsculas): nenhum aviso.
        using var bySame = await PostAssociateJobOnAsync(
            client, TraceUrl(sameReference.Id), sameReference.Id, "PN-551108", "zqref-av8");
        Assert.Equal($"/Boquilhas?trace_id={sameReference.Id}", bySame.Headers.Location?.OriginalString);
        Assert.Equal(bq8.Id, (await ReadTraceAsync(sameReference.Id))!.BqContextId);

        var html = await GetPageHtmlAsync(client, bySame.Headers.Location!.OriginalString);
        Assert.DoesNotContain("A referência indicada", html);
    }

    [Fact]
    public async Task AssociateJobOnForm_ReferenceFirst_ThenOnlyItsProductions()
    {
        // Dois passos (16/10/2026): primeiro a referência; o select só traz
        // as produções dela e só depois de a indicar.
        await SeedToolAsync("BQ-AVISO-9", "AVS109", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-9");
        var daReferencia = await SeedProductionAsync("BQ-AVISO-9", "ZQREF-AV9", "PN-551109", "B1");
        await SeedProductionAsync("BQ-AVISO-9", "ZQREF-OUTRA9", "PN-551119", "B2");
        SeedUser("4137", "Avisos", "provider-aviso-9", "AvisoPass9!");
        using var client = await LoginClientAsync("4137", "AvisoPass9!");

        // Sem referência: só o campo, nenhuma produção carregada.
        var vazio = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        Assert.Contains("name=\"associar_ref\"", vazio);
        Assert.Contains("data-dmo-suggest=\"referencias\" data-dmo-suggest-submit", vazio);
        Assert.DoesNotContain("name=\"job_on_id\"", vazio);
        Assert.DoesNotContain("id=\"bqAssociarJobOn\"", vazio);

        // Com a referência: o select só com as produções dela.
        var comReferencia = await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=zqref-av9");
        // Escolher não associa: associa o botão «Associar» (pergunta 1 do
        // deep sweep, 18/10/2026).
        Assert.Contains("<select id=\"jobNumber\" name=\"job_on_id\" required aria-label", comReferencia);
        Assert.DoesNotContain("data-dmo-autosubmit", comReferencia.Substring(comReferencia.IndexOf("id=\"jobNumber\"", StringComparison.Ordinal)).Split('>')[0]);
        Assert.Contains("<button type=\"submit\" form=\"bqAssociarJobOn\" class=\"btn primary\">Associar</button>", comReferencia);
        Assert.Contains("<form id=\"bqAssociarJobOn\" method=\"post\"", comReferencia);
        Assert.Contains($"<option value=\"{daReferencia.JobOnId}\">ZQREF-AV9 · PN-551109 · B1</option>", comReferencia);
        var formulario = AssociateForm(comReferencia);
        Assert.Contains("ZQREF-AV9 · PN-551109 · B1", formulario);
        Assert.DoesNotContain("PN-551119", formulario);
        Assert.Contains("handler=AssociateJobOn", comReferencia);

        // Referência sem produções: aviso âmbar, sem select.
        var semProducoes = System.Net.WebUtility.HtmlDecode(
            await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=NAOEXISTE"));
        Assert.Contains("Sem produções para «NAOEXISTE».", semProducoes);
        Assert.Contains("Sem produções para «NAOEXISTE». <a class=\"dmo-aviso-link\" href=\"/JobOn?novo=1&reference=NAOEXISTE#lightSheet\">Criar o Job On</a></div>", semProducoes);
        Assert.DoesNotContain("name=\"job_on_id\"", semProducoes);
    }

    [Fact]
    public async Task AssociateForm_WithTheStartOfTheReference_AssociatesTheChosenProduction_NotAnotherWithTheSameNumber()
    {
        // Deep sweep 2, F-1 (18/10/2026): com «2847» escrito e a
        // 2847Y171 · 2026999 escolhida, associava a 4526Y171 · 2026999 (o
        // mesmo nº, também com esta BQ, mais recente). A lista vai pelo
        // início da referência e cada produção leva o seu Job On.
        await SeedToolAsync("BQ-AVISO-13", "AVS113", "1");
        var trace = await SeedTraceAsync("BQ-AVISO-13");
        var escolhida = await SeedProductionAsync("BQ-AVISO-13", "92847Y171", "PN-551113", "BX8");
        var outra = await SeedProductionAsync("BQ-AVISO-13", "84526Y171", "PN-551113", "BX9");
        await SeedProductionAsync("BQ-AVISO-13", "192847Y17", "PN-551114", "BX10");
        SeedUser("4139", "Avisos", "provider-aviso-13", "AvisoPass13!");
        using var client = await LoginClientAsync("4139", "AvisoPass13!");

        var form = AssociateForm(await GetPageHtmlAsync(client, TraceUrl(trace.Id) + "&associar_ref=92847"));
        Assert.Contains($"<option value=\"{escolhida.JobOnId}\">92847Y171 · PN-551113 · BX8</option>", form);
        Assert.DoesNotContain("84526Y171", form);
        // Nunca um pedaço do meio da referência.
        Assert.DoesNotContain("192847Y17", form);

        using var associated = await PostAssociateChosenJobOnAsync(
            client, TraceUrl(trace.Id), trace.Id, escolhida.JobOnId!.Value);
        Assert.Equal($"/Boquilhas?trace_id={trace.Id}", associated.Headers.Location?.OriginalString);
        Assert.Equal(escolhida.Id, (await ReadTraceAsync(trace.Id))!.BqContextId);
        Assert.NotEqual(outra.Id, escolhida.Id);

        // Um Job On que já não existe: recusa com a causa, nada associado.
        var outroRegisto = await SeedTraceAsync("BQ-AVISO-13");
        using var missing = await PostAssociateChosenJobOnAsync(
            client, TraceUrl(outroRegisto.Id), outroRegisto.Id, outra.JobOnId!.Value + 999);
        Assert.Contains("association_refusal=ProductionNotFound", Unescaped(missing));
        Assert.Null((await ReadTraceAsync(outroRegisto.Id))!.BqContextId);
    }
}

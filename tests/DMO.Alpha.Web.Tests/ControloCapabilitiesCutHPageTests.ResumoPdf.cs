using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Sweep de 19/10/2026, A-4: o PDF do Resumo sai de um só serviço, que o
    // Resumo e a Revisão usam. Antes a Revisão criava a página inteira do
    // Resumo para o gerar. O mesmo papel nos dois sítios, e uma recusa da
    // Revisão volta à Revisão.
    [Fact]
    public async Task PdfDoResumo_NaRevisaoENoResumo_MesmoFicheiroEMesmaPasta()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedUserWithTemplate("4861", "Cria e Aprova", "provider-resumo-pdf-servico", "ResumoPdf1!",
            "tpl-teste-resumo-pdf", "Cria e aprova",
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]);
        using var client = await LoginClientAsync("4861", "ResumoPdf1!");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(
            await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}"))!;

        foreach (var apenasResumo in new[] { "true", "false" })
        {
            using var resumo = await client.PostAsync("/Controlo/Resumo?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("apenas_resumo", apenasResumo), ("__RequestVerificationToken", token)));
            using var revisao = await client.PostAsync("/Controlo/Approve?handler=ResumoPdf", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("apenas_resumo", apenasResumo), ("__RequestVerificationToken", token)));

            Assert.Equal(HttpStatusCode.OK, resumo.StatusCode);
            Assert.Equal(HttpStatusCode.OK, revisao.StatusCode);
            Assert.Equal("application/pdf", revisao.Content.Headers.ContentType?.MediaType);
            Assert.Equal(resumo.Content.Headers.ContentDisposition?.FileName, revisao.Content.Headers.ContentDisposition?.FileName);
            Assert.Equal(
                Assert.Single(resumo.Headers.GetValues(ControloDocumentPaths.PdfFolderHeader)),
                Assert.Single(revisao.Headers.GetValues(ControloDocumentPaths.PdfFolderHeader)));
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await revisao.Content.ReadAsByteArrayAsync())[..4]));
        }

        // Uma produção que já não existe: cada página volta para si.
        using var semResumo = await client.PostAsync("/Controlo/Resumo?handler=GeneratePdf", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", "999999"), ("__RequestVerificationToken", token)));
        Assert.Equal("/Controlo/Resumo?jobon_id=999999&pdf_refusal=sem-producao", semResumo.Headers.Location?.ToString());
        using var semRevisao = await client.PostAsync("/Controlo/Approve?handler=ResumoPdf", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", "999999"), ("__RequestVerificationToken", token)));
        Assert.Equal("/Controlo/Approve?jobon_id=999999", semRevisao.Headers.Location?.ToString());
    }
}

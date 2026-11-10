using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A GOTA PELO NOME TÉCNICO (Diogo, 28/10/2026: «ver se o jobon já tem em
/// consideração nome técnico para mudar gota e meter número correto»): a Gota
/// da folha, do painel e do Imprimir vem do nome técnico da CM (senão do MF),
/// quando não há Gota escrita à mão; a escrita ganha sempre (Diogo, 10/11/2026).
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_GotaVemDoNomeTecnicoDaCm_SemEscrita()
    {
        SeedUser("4602", "Chefe gota", "provider-folha-gota", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        await GotaEscritaENomesAsync(id, "", cm: "M580955MPPSX", mf: "M58104TMFPSV");
        using var client = await LoginClientAsync("4602", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"));
        Assert.Contains("<strong title=\"Do nome técnico da CM: M580955MPPSX\" data-jof-gota-nome-tecnico>2</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<div><span>Gota</span><b>2</b></div>", html, StringComparison.Ordinal);

        var painel = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&painel=1"));
        Assert.Contains("<div><dt>Gota</dt><dd>2</dd></div>", painel, StringComparison.Ordinal);
    }

    // Diogo, 10/11/2026: «tem de ter sempre forma de alterar» (C3 com 4 gotas
    // por duplicar da B1). A escrita à mão ganha ao nome técnico, e no Editar
    // a Gota escreve-se sempre.
    [Fact]
    public async Task Folha_GotaEscritaGanhaAoNomeTecnico_EEditarDeixaEscrever()
    {
        SeedUser("4604", "Chefe gota escrita", "provider-folha-gota-escrita", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        await GotaEscritaENomesAsync(id, "", cm: "M580995MPPSX", mf: null);
        using var client = await LoginClientAsync("4604", "Folha1234!");

        var editar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1"));
        Assert.Contains("id=\"jobon-cab-gota\"", editar, StringComparison.Ordinal);
        var (acao, campos) = EnvioDoFormulario.Mudar(editar, "jobon-cab-gota", "3");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"));
        Assert.DoesNotContain("data-jof-gota-nome-tecnico", html, StringComparison.Ordinal);
        Assert.Contains("<div><span>Gota</span><b>3</b></div>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Folha_SemNomeNaCm_GotaDoMf_ESemNenhum_AEscrita()
    {
        SeedUser("4603", "Chefe gota MF", "provider-folha-gota-mf", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        await GotaEscritaENomesAsync(id, "", cm: null, mf: "M581095MFPSV");
        using var client = await LoginClientAsync("4603", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"));
        Assert.Contains("<strong title=\"Do nome técnico do MF: M581095MFPSV\" data-jof-gota-nome-tecnico>4</strong>", html, StringComparison.Ordinal);

        await GotaEscritaENomesAsync(id, "1", cm: null, mf: null);
        html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"));
        Assert.DoesNotContain("data-jof-gota-nome-tecnico", html, StringComparison.Ordinal);
        Assert.Contains("<div><span>Gota</span><b>1</b></div>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"jobon-cab-gota\"", WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1")), StringComparison.Ordinal);
    }

    private async Task GotaEscritaENomesAsync(int jobOnId, string gota, string? cm, string? mf)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Single(t => t.ToolId == "F-CM").TechnicalName = cm;
        db.Tools.Single(t => t.ToolId == "F-MF").TechnicalName = mf;
        var folha = await db.JobOnFolhas.SingleOrDefaultAsync(f => f.JobOnId == jobOnId);
        if (folha is null)
        {
            folha = new JobOnFolha { JobOnId = jobOnId };
            db.JobOnFolhas.Add(folha);
        }
        folha.DefinirValor("cab_gota", gota);
        await db.SaveChangesAsync();
    }
}

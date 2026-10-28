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
/// por cima da escrita à mão. Sem nome técnico que a diga, fica a escrita.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task Folha_GotaVemDoNomeTecnicoDaCm_PorCimaDaEscrita()
    {
        SeedUser("4602", "Chefe gota", "provider-folha-gota", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        await GotaEscritaENomesAsync(id, "1", cm: "M580955MPPSX", mf: "M58104TMFPSV");
        using var client = await LoginClientAsync("4602", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}"));
        Assert.Contains("<strong title=\"Do nome técnico da CM: M580955MPPSX\" data-jof-gota-nome-tecnico>2</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<div><span>Gota</span><b>2</b></div>", html, StringComparison.Ordinal);

        var painel = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&painel=1"));
        Assert.Contains("<div><dt>Gota</dt><dd>2</dd></div>", painel, StringComparison.Ordinal);

        // No Editar não se escreve: mostra a do nome técnico e a escrita vai como está.
        var editar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={id}&editar=1"));
        Assert.DoesNotContain("id=\"jobon-cab-gota\"", editar, StringComparison.Ordinal);
        Assert.Contains("data-jof-gota-nome-tecnico>2</strong>", editar, StringComparison.Ordinal);
        var (acao, campos) = EnvioDoFormulario.Mudar(editar, "jobon-cab-seccoes", "12");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.Equal("1", scope.ServiceProvider.GetRequiredService<DmoDbContext>().JobOnFolhas.AsNoTracking().Single(f => f.JobOnId == id).CabGota);
    }

    [Fact]
    public async Task Folha_SemNomeNaCm_GotaDoMf_ESemNenhum_AEscrita()
    {
        SeedUser("4603", "Chefe gota MF", "provider-folha-gota-mf", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        await GotaEscritaENomesAsync(id, "1", cm: null, mf: "M581095MFPSV");
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

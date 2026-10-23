using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PuncoesPageTests
{
    // Diogo, 24/10/2026: a mesma punção (o mesmo número e o mesmo lote) não
    // gravava e não dizia nada. Continua recusada, com a mensagem junto ao
    // formulário; o mesmo número com outro lote grava-se.
    [Fact]
    public async Task MesmoNumeroEMesmoLote_ERecusada_ComAMensagem_EOutroLoteGrava()
    {
        var (_, controloId) = await SeedProductionAsync(CmProcess.Nnpb);
        SeedUser("4315", "Operador Repetida", "provider-puncoes-repetida", "PuncoesRep1!");
        using var client = await LoginClientAsync("4315", "PuncoesRep1!");
        var primeira = await PostAsync(client, controloId, "Add", ("numero", "12"), ("referencia", "1075"), ("lote", "13"));
        Assert.DoesNotContain("recusa", primeira.Headers.Location!.ToString());

        var repetida = await PostAsync(client, controloId, "Add", ("numero", "12"), ("referencia", "1075"), ("lote", "13"), ("lote_puncao", "13"));
        Assert.Equal(HttpStatusCode.Redirect, repetida.StatusCode);
        Assert.Contains("recusa=DuplicatePunch", repetida.Headers.Location!.ToString());
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, repetida.Headers.Location!.ToString()));
        Assert.Contains("<div data-dmo-toast=\"erro\" class=\"pu-aviso\" role=\"alert\" data-pu-repetida>A punção 12 do lote 13 já está registada.</div>", html);
        // Junto ao formulário das punções, não no cartão de cima.
        Assert.True(html.IndexOf("data-pu-repetida", StringComparison.Ordinal) > html.IndexOf("id=\"puncoes-lista\"", StringComparison.Ordinal));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "class=\"pu-aviso\""));
        Assert.Contains("<details class=\"pu-id-editar\">", html);

        // Sem «Lote desta punção»: o lote de cima, o mesmo — também recusada.
        var semLote = await PostAsync(client, controloId, "Add", ("numero", "12"), ("referencia", "1075"), ("lote", "13"));
        Assert.Contains("recusa=DuplicatePunch", semLote.Headers.Location!.ToString());

        var outroLote = await PostAsync(client, controloId, "Add", ("numero", "12"), ("referencia", "1075"), ("lote", "13"), ("lote_puncao", "14"));
        Assert.DoesNotContain("recusa", outroLote.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var linhas = await db.PuncaoInspecoes.AsNoTracking().OrderBy(i => i.Position).Select(i => new { i.Number, i.Lot }).ToListAsync();
        Assert.Equal(new[] { ("12", "13"), ("12", "14") }, linhas.Select(l => (l.Number, l.Lot)));
    }
}

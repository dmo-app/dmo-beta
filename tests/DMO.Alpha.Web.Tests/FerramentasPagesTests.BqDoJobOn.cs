using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// «Criar BQ» a partir do Job On (Diogo, 22/10/2026): escolhida a referência
/// total (5612T173), a referência fica toda e o cartão do Job On mostra-a
/// toda; sem lote, a BQ é recusada e nada é gravado («Precisamos do lote
/// para tool_id»).
/// </summary>
public partial class FerramentasPagesTests
{
    private const string RegressoJobOnBq = "/JobOn?novo=1&reference=5612T173&retoma=1&bq_tool_id={tool_id}#lightSheet";

    private async Task<string> FormularioCriarBqDoJobOnAsync(HttpClient client) =>
        await GetPageHtmlAsync(client, $"{IndexPath}?criar=1&type=BQ&return={Uri.EscapeDataString(RegressoJobOnBq)}");

    [Fact]
    public async Task CriarBqDoJobOn_ComAReferenciaTotal_GuardaETornaAMostrarAReferenciaToda()
    {
        SeedUser("4491", "Ferramentas Owner", "provider-ferr-bq-total", "FerrBq91!");
        using var client = await LoginClientAsync("4491", "FerrBq91!");

        var html = await FormularioCriarBqDoJobOnAsync(client);
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("ferr-new-reference", "5612T173"), ("ferr-new-lot", "3"));
        var response = await client.PostAsync(acao, HtmlFormHelpers.BuildFormPost(campos.ToArray()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/JobOn?novo=1&reference=5612T173", location, StringComparison.Ordinal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var bq = await db.Tools.AsNoTracking().SingleAsync();
            Assert.Equal(ToolType.Bq, bq.Type);
            Assert.Equal("5612T173", bq.Reference);
            Assert.Equal("3", bq.Lot);
            Assert.Contains($"bq_tool_id={bq.ToolId}", location, StringComparison.Ordinal);
        }

        // O cartão BQ do Job On mostra a referência toda, não só a T173.
        var jobOn = await GetPageHtmlAsync(client, location.Split('#')[0]);
        Assert.Contains("5612T173 · Lote 3", jobOn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CriarBqDoJobOn_SemLote_ERecusada_ENadaEGravado(string lote)
    {
        SeedUser("4492", "Ferramentas Owner", "provider-ferr-bq-semlote", "FerrBq92!");
        using var client = await LoginClientAsync("4492", "FerrBq92!");

        var html = await FormularioCriarBqDoJobOnAsync(client);
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("ferr-new-reference", "5612T173"), ("ferr-new-lot", lote));
        var response = await client.PostAsync(acao, HtmlFormHelpers.BuildFormPost(campos.ToArray()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("create_refusal=MissingLot", location, StringComparison.Ordinal);

        var recusa = await GetPageHtmlAsync(client, location);
        Assert.Contains("Indique o lote da boquilha: é preciso para identificar a ferramenta.", recusa);
        // O formulário volta com a referência toda escrita.
        Assert.Contains("value=\"5612T173\"", recusa);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.False(await db.Tools.AsNoTracking().AnyAsync());
    }
}

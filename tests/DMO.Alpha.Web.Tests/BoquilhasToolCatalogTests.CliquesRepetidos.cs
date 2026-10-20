using System.Net;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

// Cliques repetidos não mostravam mensagens (Diogo, 18/10/2026; deep sweep,
// B-boquilhas-4) desde que «Guardar ficha» em «+ Criar boquilha / lote»
// passou pela fila da página. Esse formulário curto saiu (Diogo, 18/10/2026,
// pergunta 6: «1 página e funciona com todas»): a boquilha regista-se nas
// Ferramentas, que já tratam os cliques repetidos, e volta aqui com a
// mensagem na zona dos avisos.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task ToolCatalog_SemOFormularioCurto_OPostAntigoNaoGravaEAbreORegisto_EAGuardadaMostraAMensagemNaZona()
    {
        SeedUser("4923", "Cliques BQ", "catalog-cliques", "CatalogPass1!");
        using var client = await LoginClientAsync("4923", "CatalogPass1!");

        var html = (await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1")).Replace("\r\n", "\n");
        Assert.Contains("<section id=\"boquilhas\" class=\"view active\" data-dmo-troca>", html);
        Assert.DoesNotContain("bq-tool-create", html);
        Assert.DoesNotContain("bqCriarErro", html);
        Assert.DoesNotContain("bq-tool-catalog.js", html);
        Assert.Matches("<div id=\"bqCatalogoAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*</div>", html);
        // Sem o módulo Ferramentas não há «Criar boquilha»: o registo
        // mandava-o para o primeiro módulo dele, sem aviso.
        Assert.Contains("<div id=\"bqCriarBoquilha\" class=\"dmo-card-topo-acoes\" data-dmo-regiao></div>", html);
        Assert.DoesNotContain("Criar boquilha</a>", html);

        var guardada = (await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&saved=created")).Replace("\r\n", "\n");
        Assert.Matches("<div id=\"bqCatalogoAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*<p data-dmo-toast=\"ok\" class=\"result\" role=\"status\">Boquilha guardada\\. A ficha está disponível abaixo\\.</p>\\s*</div>", guardada);
        // As Ferramentas recusam a boquilha repetida (beta.229): o aviso
        // «já estavam registados» deixou de ter caminho e saiu.
        var existente = (await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&saved=existing")).Replace("\r\n", "\n");
        Assert.Matches("<div id=\"bqCatalogoAvisos\" class=\"dmo-zona\" data-dmo-regiao>\\s*</div>", existente);
        Assert.DoesNotContain("já estavam registados", existente);

        // Uma página antiga ainda aberta com o formulário curto: o POST já
        // não grava nada e abre o registo das Ferramentas, em vez de um erro.
        // O catálogo já não tem formulários POST; o token vem da ficha de uma
        // boquilha, que os tem (é o mesmo na sessão).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = "BQ-CLIQUES-1", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        await db.SaveChangesAsync();
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?ficheiros=1&ferramenta=BQ-CLIQUES-1");
        using var antigo = await client.PostAsync($"{PagePath}?handler=CreateTool", HtmlFormHelpers.BuildFormPost(
            ("Draft.Reference", "AGT5"), ("Draft.Lot", "1"), ("Draft.Quantity", "12"), ("Draft.QuantityManual", "1"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, antigo.StatusCode);
        Assert.Equal("/Ferramentas?criar=1&type=BQ&return=%2FBoquilhas%3Fficheiros%3D1%26ferramenta%3D%7Btool_id%7D%26saved%3Dcreated%23bq-tool-sheet",
            antigo.Headers.Location!.OriginalString);
        db.ChangeTracker.Clear();
        Assert.DoesNotContain(db.Tools, t => t.Reference == "AGT5");
    }
}

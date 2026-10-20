using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Teste do agente (19/10/2026, T4a2a27b): no Peso de uma produção, antes de
    // existir Peso, o Processo aparecia «Ps» (o nome do enum) em vez de «PS».
    [Theory]
    [InlineData(CmProcess.Ps, "PS")]
    [InlineData(CmProcess.Nnpb, "NNPB")]
    public async Task PesoDaProducaoSemPeso_MostraOProcessoComoNasFolhas(CmProcess processo, string esperado)
    {
        var (_, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.ToolTechnicalContexts.Single(t => t.ToolId == "T3").Process = processo;
            await db.SaveChangesAsync();
        }

        SeedOperacional("4793", "provider-processo-peso", "ProcessoPeso1!");
        using var client = await LoginClientAsync("4793", "ProcessoPeso1!");
        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Peso?controlo_id={controloId}"));

        var contexto = Regex.Match(html, "<div class=\"peso-facts peso-context-grid\">[\\s\\S]*?</div>\\s*</div>").Value;
        Assert.NotEmpty(contexto);
        Assert.Contains($"<span>Processo</span><strong>{esperado}</strong>", contexto);
    }
}

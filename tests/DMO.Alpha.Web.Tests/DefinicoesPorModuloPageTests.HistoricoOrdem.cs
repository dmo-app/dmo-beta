using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O Histórico de produções da ficha numera as produções da ferramenta (Diogo,
/// 29/10/2026: «202603 referência B1 depois 1, e por aí fora»): a 1 é a mais
/// antiga, mesmo noutro artigo; uma produção sem início não leva número.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OHistoricoDeProducoes_NumeraAsProducoesDaFerramenta_A1EAMaisAntiga()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var linha = db.Machines.First(m => m.Code == maquina);
            var hoje = DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.Now);
            var outroArtigo = new JobOn { Reference = "7018ORD", ProductionNumber = "209906", Machine = linha, InicioEm = hoje.AddDays(-1), FimEm = hoje.AddDays(6) };
            var semInicio = new JobOn { Reference = referencia, ProductionNumber = "209907", Machine = linha };
            db.JobOns.AddRange(outroArtigo, semInicio);
            db.CmContexts.Add(new CmContext { JobOn = outroArtigo, ToolId = toolId });
            db.CmContexts.Add(new CmContext { JobOn = semInicio, ToolId = toolId });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7198", DmoCapabilities.Ferramentas);
        var historico = WebUtility.HtmlDecode(ZonaUsoSap(await (await client.GetAsync($"/Ferramentas/Ficha?tool_id={toolId}")).Content.ReadAsStringAsync(), "ferrHistoricoProducoes"));

        Assert.Contains($"7018ORD · {maquina}</small><small class=\"ferr-producao-maquina ferr-producao-ordem\"> · 2</small>", historico, StringComparison.Ordinal);
        Assert.Contains($"{referencia} · {maquina}</small><small class=\"ferr-producao-maquina ferr-producao-ordem\"> · 1</small>", historico, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(historico, "ferr-producao-ordem").Count);
        Assert.Contains("209907", historico, StringComparison.Ordinal);
    }
}

using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Ferramentas;

public sealed partial class ReparacaoProgramadaPaginaTests
{
    /// <summary>
    /// Garante (DMO-674, Diogo 29/10/2026: «em frente à ferramenta mete referência
    /// completa»): na lista e na procura do Registar Reparação, a seguir ao nome da
    /// ferramenta vem a referência completa da última produção em que trabalhou.
    /// A página vai para o Chromium (tests/visual/cobertura_a2_browser.py), que mede
    /// o mesmo espaço entre os botões («espaço entre os botões não está uniforme»).
    /// </summary>
    [Fact]
    public async Task ListaEProcura_MostramAReferenciaCompletaAFrenteDaFerramenta()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var maquina = new Machine { Code = "RP" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant() };
            db.JobOns.AddRange(
                new JobOn { Machine = maquina, Reference = "5447T100", ProductionNumber = "202601", InicioEm = new DateOnly(2026, 1, 5), FimEm = new DateOnly(2026, 1, 9) },
                new JobOn { Machine = maquina, Reference = "5447T173", ProductionNumber = "202602", InicioEm = new DateOnly(2026, 3, 2), FimEm = new DateOnly(2026, 3, 9) });
            db.SaveChanges();
            foreach (var j in db.JobOns.Where(j => j.Machine!.Code == maquina.Code).ToList())
                db.CmContexts.Add(new CmContext { JobOnId = j.Id, ToolId = Cm });
            db.SaveChanges();
            Assert.True((await scope.ServiceProvider.GetRequiredService<IReparacaoProgramada>().AssociarAsync([Cm], null, ReparadorId())).Associou);
        }

        using var chefia = await LoginAsync("7674", DmoCapabilities.Ferramentas);
        var html = await chefia.GetStringAsync($"{Pagina}?referencia=5447RP");

        // Na lista e na procura: a da última produção (5447T173), não a mais antiga.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "<small class=\"dmo-ferr-ref\">5447RP</small><small class=\"rp-artigo\"> (?:·|&#xB7;) 5447T173</small>").Count);
        Assert.DoesNotContain("5447T100", html, StringComparison.Ordinal);
        Assert.Contains("id=\"rpImprimir\"", html);
        ExportarParaOBrowser.Gravar("rp-lista.html", html);
    }
}

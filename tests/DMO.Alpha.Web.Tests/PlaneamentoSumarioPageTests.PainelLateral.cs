using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

public sealed partial class PlaneamentoSumarioPageTests
{
    /// <summary>A página sem o painel lateral, que mostra as entradas da semana.</summary>
    private static string SemPainelLateral(string html) =>
        Regex.Replace(html, "<aside class=\"beta-light-rail beta-production-rail\"[\\s\\S]*?</aside>", string.Empty);

    [Fact]
    public async Task Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas()
    {
        // «Na página do sumário não temos sidepanel do planeamento» (Diogo,
        // 21/10/2026): o mesmo «O que vai entrar» do Job On, com as produções
        // que entram desta semana. A que entrou ontem e não tem fim está em
        // curso e vem primeiro (Diogo, 22/10/2026: «fica tudo a depender da
        // data de entrada e saída»).
        var entraHoje = await JobOnAsync("S9P001", Hoje);
        var entrouOntem = await JobOnAsync("S9P002", Hoje.AddDays(-1));
        using var client = await LoginAsync("5310", DmoCapabilities.JobOnCreate);

        var html = await GetAsync(client);
        var painel = Regex.Match(html, "<aside class=\"beta-light-rail beta-production-rail\"[\\s\\S]*?</aside>").Value;
        // O título é «Planeamento», um link para o calendário do Job On (Diogo, 26/10/2026).
        Assert.Contains("<a class=\"beta-production-rail-title\" href=\"/JobOn\">Planeamento</a>", painel);
        // Cada produção abre a folha nova (Diogo, 23/10/2026: a ficha antiga deixou de existir).
        Assert.Contains($"data-dmo-open=\"/JobOn/Folha?jobon_id={entraHoje}\"", painel);
        Assert.Contains("S9P001", painel);
        Assert.Contains($"data-dmo-open=\"/JobOn/Folha?jobon_id={entrouOntem}\"", painel);
        // A referência e a produção numa linha, o estado noutra (Diogo, 26/10/2026).
        Assert.Contains("<span>S9P002 · Produção 7</span>", painel);
        Assert.Contains($"<span>Em curso desde {Hoje.AddDays(-1):dd/MM}</span>", painel);
        Assert.True(painel.IndexOf($"jobon_id={entrouOntem}\"", StringComparison.Ordinal)
            < painel.IndexOf($"jobon_id={entraHoje}\"", StringComparison.Ordinal));
        // O painel fica por baixo do título do Sumário, dentro da página, como no Job On (mockups, 26/10/2026).
        Assert.Matches("<main [^>]*sumario-page[\\s\\S]*?id=\"sumario-title\"[\\s\\S]*?<aside class=\"beta-light-rail beta-production-rail\"", html);
    }
}

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
        Assert.Contains("O que vai entrar", painel);
        Assert.Contains($"data-dmo-open=\"/JobOn?jobon_id={entraHoje}\"", painel);
        Assert.Contains("S9P001", painel);
        Assert.Contains($"data-dmo-open=\"/JobOn?jobon_id={entrouOntem}\"", painel);
        Assert.Contains($"S9P002 · Produção 7 · em curso desde {Hoje.AddDays(-1):dd/MM}", painel);
        Assert.True(painel.IndexOf($"jobon_id={entrouOntem}\"", StringComparison.Ordinal)
            < painel.IndexOf($"jobon_id={entraHoje}\"", StringComparison.Ordinal));
        // O painel fica ao lado do Sumário, no mesmo layout do Job On.
        Assert.Matches("<div class=\"beta-light-layout\">\\s*<aside class=\"beta-light-rail beta-production-rail\"", html);
    }
}

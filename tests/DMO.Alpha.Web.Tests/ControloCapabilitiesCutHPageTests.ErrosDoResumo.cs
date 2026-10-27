using System.Net;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Os erros do Resumo (Diogo, 27/10/2026): o cabeçalho num só bloco com o
    // texto curto, o «PDF Pegamentos», o «Sem link do mcaliper» na área sem
    // link, o «Registar» que abre a página (fora do painel lateral) para quem
    // tem o Criar e o «← Voltar» da página de registo para o Resumo.
    [Fact]
    public async Task Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4795", "provider-erros-resumo", "ErrosResumo1!");
        using var client = await LoginClientAsync("4795", "ErrosResumo1!");

        var resumo = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"/Controlo/Resumo?jobon_id={jobOnId}"));
        Assert.Contains("<div class=\"resumo-cabecalho\">", resumo);
        Assert.Contains("<strong>Por aprovar</strong>", resumo);
        Assert.DoesNotContain("nada bloqueia", resumo);
        Assert.True(resumo.IndexOf("<span>Aprovação</span>", StringComparison.Ordinal)
            < resumo.IndexOf("Controlos associados", StringComparison.Ordinal));
        // Sem medições, sem o «PDF Pegamentos» (Diogo, 27/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraB4cWtFy2LfgzGWkBMpzRuk).
        Assert.DoesNotContain(">PDF Pegamentos</button>", resumo);

        // Sem link e sem o Aprovar: só o texto, sem a ida à Revisão.
        Assert.Contains("<span class=\"resumo-area-link vazio\" data-resumo-mcaliper-sem=\"CM\">Sem link do mcaliper</span>", resumo);
        Assert.DoesNotContain("data-resumo-mcaliper-sem=\"PU\"", resumo);
        Assert.DoesNotContain("associar na Revisão", resumo);

        // Com o Criar, os botões de registo dos cartões saem do painel lateral.
        Assert.Contains("<div class=\"resumo-linked-grid\" data-dmo-sem-painel=\".resumo-card-actions a.dmo-button:not([href*='comparacao=1'])\">", resumo);
        var suporte = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "DMO.Alpha.Web", "wwwroot", "js", "dmo-support.js"));
        Assert.Contains("closest?.('[data-dmo-sem-painel]')", suporte);

        foreach (var pagina in new[] { "Peso", "Pegamentos", "Puncoes" })
        {
            var html = await GetPageHtmlAsync(client, $"/Controlo/{pagina}?controlo_id={controloId}");
            Assert.Contains($"<a class=\"dmo-voltar-texto\" href=\"/Controlo/Resumo?jobon_id={jobOnId}\" data-dmo-voltar=\"resumo\"", html);
        }
    }
}

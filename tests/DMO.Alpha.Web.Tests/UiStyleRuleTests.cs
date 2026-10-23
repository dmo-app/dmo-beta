namespace DMO.Alpha.Web.Tests;

// A regra única de estilo (Diogo, 16/10/2026): os botões são azuis com letra
// branca e o hover inverte; as linhas das tabelas são brancas, o hover é
// azul-claro e a linha selecionada inverte (fundo azul, letra branca), sem
// círculo de seleção visível. Estes testes guardam o contrato no CSS e no
// markup partilhado; a aparência em si só se confirma no browser.
public sealed class UiStyleRuleTests
{
    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "DMO.Alpha.Web");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([WebRoot(), .. parts]));

    [Fact]
    public void LinksNosMiniCartoes_TemACorDoValor_SemSublinhado_NemNoHover()
    {
        // Diogo, 24/10/2026: o link da produção num mini-cartão tinha a cor e o
        // sublinhado do browser. A regra partilhada vale para toda a app.
        var css = Read("wwwroot", "css", "dmo-normalize.css");
        const string cartoes = ":is(.dmo-fact,.dmo-facts,.dmo-fact-stack,.pu-fact,.pu-stat,.peso-fact,.peso-facts,.revisao-facts,.facts,.fact-col,.lot-data,.resumo-stats,.jobon-production-facts,.job-tool-card-facts) a:not(.dmo-button):not(.btn)";
        Assert.Contains($"html body {cartoes}{{color:inherit;text-decoration:none;cursor:pointer;", css, StringComparison.Ordinal);
        Assert.Contains($"html body {cartoes}:is(:hover,:focus-visible){{color:var(--dmo-brand-700);text-decoration:none}}", css, StringComparison.Ordinal);
        // O hover é um token que o modo escuro redefine (não uma cor fixa).
        var tokens = Read("wwwroot", "0_ASSET_CANONICAL_DESIGN_SYSTEM.css");
        Assert.Matches(":root\\[data-theme=\"dark\"\\]\\{[^}]*--dmo-brand-700:", tokens);
    }

    [Fact]
    public void Buttons_HaveOneLook_BlueWithWhiteText_HoverInverts()
    {
        var css = Read("wwwroot", "css", "dmo-normalize.css");
        Assert.Contains(
            "html body :is(.dmo-button,.button,.btn){background:var(--button-color,var(--dmo-brand-600));color:var(--dmo-text-on-color)",
            css, StringComparison.Ordinal);
        Assert.Contains(
            "html body :is(.dmo-button,.button,.btn):is(:hover,:focus-visible){background:var(--dmo-card);color:var(--button-color,var(--dmo-brand-600))",
            css, StringComparison.Ordinal);
        // A variante branca («ghost») deixou de existir no CSS e no markup.
        Assert.DoesNotContain(".ghost", css, StringComparison.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(WebRoot(), "Pages"), "*.cshtml", SearchOption.AllDirectories))
            Assert.DoesNotContain("ghost\"", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void TableRows_AreWhite_HoverLightBlue_SelectedInverted_RoundedInsideTheCard_WithoutVisibleRadio()
    {
        var css = Read("wwwroot", "css", "dmo-normalize.css");
        Assert.Contains("html body .dmo-table tbody tr{background:var(--dmo-card)}", css, StringComparison.Ordinal);
        // O destaque desenha-se nas células, com cantos arredondados e barra
        // fina à esquerda, dentro do cartão (Diogo, 17/10/2026).
        Assert.Contains(":hover>td{background:var(--dmo-row-hover)}", css, StringComparison.Ordinal);
        Assert.Contains(
            ":is(.selected,[aria-selected=\"true\"],:has(>td>input[type=radio]:checked))>td{background:var(--dmo-brand-600)}",
            css, StringComparison.Ordinal);
        Assert.Contains("{border-radius:7px 0 0 7px;box-shadow:inset 3px 0 0 var(--dmo-brand-600)}", css, StringComparison.Ordinal);
        Assert.Contains("html body .dmo-table-wrap:has(>.dmo-table tbody [data-dmo-row]){padding:4px}", css, StringComparison.Ordinal);
        Assert.Contains(
            "html body .dmo-table td>input[type=radio]{position:absolute;opacity:0;width:0;height:0;margin:0;pointer-events:none}",
            css, StringComparison.Ordinal);
    }

    // A paleta do Diogo (17/10/2026) é o template de cores da app: as cinco cores
    // ficam definidas num só sítio e os azuis antigos já não aparecem soltos no CSS.
    [Fact]
    public void Palette_IsDefinedOnce_AndDrivesButtonsRowsAndHover()
    {
        var css = Read("wwwroot", "0_ASSET_CANONICAL_DESIGN_SYSTEM.css");
        Assert.Contains(
            ":root{--dmo-palette-sky:#afd1f2;--dmo-palette-mist:#92b4cc;--dmo-palette-steel:#6494b4;--dmo-palette-ocean:#4c7c9c;--dmo-palette-slate:#44647c}",
            css, StringComparison.Ordinal);
        Assert.Contains("--dmo-brand-600:var(--dmo-palette-slate);", css, StringComparison.Ordinal);
        Assert.Contains("--dmo-row-hover:var(--dmo-brand-200);", css, StringComparison.Ordinal);
        Assert.Contains("--dmo-brand-200:var(--dmo-palette-sky);", css, StringComparison.Ordinal);

        string[] oldBlues = ["#3c73a8", "#315d88", "#376da1", "#3973a8", "#174a80", "#065c99", "#087bc1", "#4094c5", "#214963", "#e8eff7"];
        var cssFiles = Directory.EnumerateFiles(Path.Combine(WebRoot(), "wwwroot"), "*.css", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        foreach (var file in cssFiles)
        {
            // As cores de recurso dentro de var(--x,#...) não contam: só se usam se o token faltar.
            var text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(file).ToLowerInvariant(), @",\s*#[0-9a-f]{3,8}", "");
            foreach (var blue in oldBlues)
                Assert.False(text.Contains(blue, StringComparison.Ordinal), $"{Path.GetFileName(file)} ainda usa {blue} solto");
        }
    }

    // As cores também não ficam soltas nos scripts (deep sweep de 18/10/2026,
    // regra 109): o dmo-live.js pintava as suas mensagens a #1f3a5f, #8a5a00 e
    // #8a1c1c, fora da paleta, e o teste acima só lê o CSS. Um script que
    // precise de cor usa as classes e os tokens do CSS.
    [Fact]
    public void Scripts_DoNotCarryLooseColors()
    {
        var jsFiles = Directory.EnumerateFiles(Path.Combine(WebRoot(), "wwwroot"), "*.js", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToList();
        Assert.Contains(jsFiles, f => Path.GetFileName(f) == "dmo-live.js");
        var cor = new System.Text.RegularExpressions.Regex(@"(?<![\w&])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b|\brgba?\(\s*\d");
        foreach (var file in jsFiles)
        {
            var match = cor.Match(File.ReadAllText(file));
            Assert.False(match.Success, $"{Path.GetFileName(file)} tem a cor solta {match.Value}");
        }
    }

    // Modo escuro e mais tons (Diogo, 21/10/2026: «está demasiado branco»): o
    // modo escuro só redefine os tokens, por isso nenhuma folha pode pintar
    // fundos a branco solto; o cabeçalho tem o botão e o <head> aplica a escolha.
    [Fact]
    public void DarkMode_RedefinesTheTokens_AndNoSheetPaintsLooseWhite()
    {
        var css = Read("wwwroot", "0_ASSET_CANONICAL_DESIGN_SYSTEM.css");
        Assert.Contains("@media screen{:root[data-theme=\"dark\"]{color-scheme:dark;--dmo-page:", css, StringComparison.Ordinal);
        Assert.Contains("--dmo-page:#e4ebf2;", css, StringComparison.Ordinal);
        var branco = new System.Text.RegularExpressions.Regex(@"background(-color)?\s*:\s*(#fff\b|#ffffff\b|white\b|rgb\(\s*255\s*,\s*255\s*,\s*255\s*\))", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // As folhas e também os <style> e style="" das páginas.
        var cssFiles = Directory.EnumerateFiles(Path.Combine(WebRoot(), "wwwroot"), "*.css", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Concat(Directory.EnumerateFiles(Path.Combine(WebRoot(), "Pages"), "*.cshtml", SearchOption.AllDirectories));
        foreach (var file in cssFiles)
        {
            // A impressão sai sempre em branco: o @media print pode pintar #fff.
            var text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(file), @"@media print\{(?:[^{}]|\{[^{}]*\})*\}", "");
            var match = branco.Match(text);
            Assert.False(match.Success, $"{Path.GetFileName(file)} pinta {match.Value}: use var(--dmo-card)");
        }
        // O modo escuro saiu do cabeçalho (Diogo, 22/10/2026): vive em Definições → Pessoal.
        var cabecalho = Read("Pages", "Shared", "_DmoSessionUtilities.cshtml");
        Assert.DoesNotContain("data-dmo-tema", cabecalho, StringComparison.Ordinal);
        Assert.DoesNotContain("Modo escuro", cabecalho, StringComparison.Ordinal);
        // Sem o link «Admin» (Diogo, 23/10/2026: «remover o link admin, deixar o sair»): só o «Sair» por baixo do utilizador.
        Assert.DoesNotContain("href=\"/Admin\"", cabecalho, StringComparison.Ordinal);
        Assert.DoesNotContain(">Admin<", cabecalho, StringComparison.Ordinal);
        Assert.True(cabecalho.IndexOf("data-user-profile-name", StringComparison.Ordinal) < cabecalho.IndexOf(">Sair<", StringComparison.Ordinal));
        Assert.Contains("id=\"defTemaBotao\"", Read("Pages", "Definicoes", "Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_DmoTemaInicial\" />", Read("Pages", "Shared", "_DmoShellLayout.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_DmoTemaInicial\" />", Read("Pages", "Shared", "_DmoAccountLayout.cshtml"), StringComparison.Ordinal);
    }

    // Os PDFs seguem a mesma paleta: os azuis antigos já não aparecem nos geradores.
    // Os botões de um card no canto superior direito, na linha do título
    // (Diogo, 18/10/2026). As classes são partilhadas e genéricas: flex, o
    // espaçamento da casa, sem larguras nem alturas fixas e sem cores.
    [Fact]
    public void CardTopo_PutsTheCardButtonsTopRight_WithTheHouseSpacing()
    {
        var css = Read("wwwroot", "0_ASSET_CANONICAL_DESIGN_SYSTEM.css");
        Assert.Contains("/* Cabeçalho de card com ações no canto superior direito (regra do Diogo, 18/10) */", css);
        Assert.Contains(".dmo-card-topo{display:flex;flex-wrap:wrap;justify-content:space-between;align-items:center;gap:var(--dmo-space-3)}", css);
        Assert.Contains(".dmo-card-topo-acoes{display:flex;flex-wrap:wrap;align-items:center;gap:var(--dmo-space-3);margin-left:auto}", css);
        var bloco = css[css.IndexOf("/* Cabeçalho de card com ações", StringComparison.Ordinal)..];
        var regras = string.Join("\n", bloco.Split('\n').Where(linha => !linha.StartsWith("/*", StringComparison.Ordinal)));
        Assert.DoesNotMatch("(?<![-a-z])(min-|max-)?(width|height)\\s*:\\s*0*[1-9]", regras);
        Assert.DoesNotContain("#", regras);
        Assert.DoesNotContain("ferr", regras);
    }

    [Fact]
    public void PdfGenerators_UseThePalette_NotTheOldBlues()
    {
        var infra = Path.Combine(Path.GetDirectoryName(WebRoot())!, "DMO.Alpha.Infrastructure");
        Assert.Contains("public const string Slate = \"#44647c\";", File.ReadAllText(Path.Combine(infra, "Pdf", "DmoPdfPalette.cs")), StringComparison.Ordinal);
        string[] oldBlues = ["#3c73a8", "#087bc1", "#0066a0", "#0089d7", "#174a80", "#16365c", "#193046", "#d9e6f2", "#d0e0f1"];
        foreach (var file in Directory.EnumerateFiles(infra, "*PdfGenerator.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file).ToLowerInvariant();
            foreach (var blue in oldBlues)
                Assert.False(text.Contains(blue, StringComparison.Ordinal), $"{Path.GetFileName(file)} ainda usa {blue}");
        }
    }

    [Fact]
    public void Pills_CenterTheirText_AndLogoutIsJustTheWord()
    {
        var css = Read("wwwroot", "css", "dmo-normalize.css");
        Assert.Contains(
            "html body :is(.dmo-pill,.status){display:inline-flex;align-items:center;justify-content:center;text-align:center",
            css, StringComparison.Ordinal);
        Assert.Contains(
            "html body .dmo-shell-header .dmo-shell-logout{min-height:0;margin-left:18px;padding:4px 2px;border:0;border-radius:4px;background:none",
            css, StringComparison.Ordinal);
    }

    // Diogo, 17/10/2026: um só tamanho de botão, igual aos campos de filtragem;
    // «Anterior»/«Seguinte» passam a só setas; o texto dos pills fica ao centro.
    [Fact]
    public void Buttons_ShareTheFieldHeight_PagerUsesArrowsOnly_PillsTrimText()
    {
        var css = Read("wwwroot", "css", "dmo-normalize.css");
        Assert.Contains(
            "html body :is(.dmo-button,.button,.btn){box-sizing:border-box;height:var(--dmo-control-height);min-height:var(--dmo-control-height);padding:0 16px;white-space:nowrap;font-family:inherit;font-size:var(--dmo-fs-base)",
            css, StringComparison.Ordinal);
        Assert.Contains("html body :is(.dmo-button,.button,.btn).dmo-arrow,", css, StringComparison.Ordinal);
        Assert.Contains("text-box:trim-both cap alphabetic", css, StringComparison.Ordinal);
        foreach (var file in new[] { Read("Pages", "Shared", "_ListPager.cshtml"), Read("Pages", "Ferramentas", "_FerramentasResultados.cshtml"), Read("Pages", "Shared", "_BqToolCatalog.cshtml") })
        {
            Assert.DoesNotContain("← Anterior", file, StringComparison.Ordinal);
            Assert.DoesNotContain("Seguinte →", file, StringComparison.Ordinal);
            Assert.Contains("dmo-arrow", file, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ButtonAndFieldText_IsCentered_WithPinnedFontMetrics()
    {
        var css = Read("wwwroot", "css", "dmo-normalize.css");
        Assert.Contains("letter-spacing:0;line-height:1;text-box:trim-both cap alphabetic}", css, StringComparison.Ordinal);
        Assert.Contains("ascent-override:96.875%;descent-override:24.121%;line-gap-override:0%", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledPagerButtons_DoNotFadeInline()
    {
        Assert.DoesNotContain("opacity:", Read("Pages", "Shared", "_ListPager.cshtml"), StringComparison.Ordinal);
    }

    // Login (Diogo, 17/10/2026): ao carregar em «Entrar» a letra das credenciais
    // mudava de tamanho. O Chrome desenha a sugestão das credenciais guardadas com
    // font:-webkit-small-control !important; o valor preenchido usa a mesma letra
    // para não mudar no primeiro clique.
    [Fact]
    public void AutofilledFields_UseTheSameFontAsChromePreview()
    {
        var css = Read("wwwroot", "css", "dmo-normalize.css");
        Assert.Contains(
            "html body input:-webkit-autofill:is(:hover,:focus,:active){-webkit-text-fill-color:var(--dmo-text);caret-color:var(--dmo-text);-webkit-box-shadow:0 0 0 1000px var(--dmo-card) inset;box-shadow:0 0 0 1000px var(--dmo-card) inset;font:-webkit-small-control !important;line-height:1.2 !important;",
            css, StringComparison.Ordinal);
        Assert.DoesNotContain("-webkit-autofill::first-line", css, StringComparison.Ordinal);
    }
}

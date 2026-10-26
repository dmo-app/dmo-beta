using System.Text.RegularExpressions;
using DMO.Alpha.Web.Pages.Shared;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests;

// Os componentes partilhados (Diogo, 17/10/2026: «um ficheiro para podermos
// usar como templates»): o calendário é só o partial _DmoCalendar montado por
// DmoCalendarPresentation.ForMonth, e as tabelas são só a classe dmo-table.
// O modelo de cada um está em docs/ui-reference/componentes-partilhados.md.
public sealed class SharedComponentsTests
{
    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "DMO.Alpha.Web");
    }

    private static IEnumerable<string> PageFiles(string pattern) =>
        Directory.EnumerateFiles(Path.Combine(WebRoot(), "Pages"), pattern, SearchOption.AllDirectories);

    // Sem flash ao mudar de separador (Diogo, 18/10/2026): o browser só desenha
    // a página nova quando chega ao marcador do fim, depois dos scripts; até lá
    // fica a anterior. Com o marcador antes dos scripts, voltava a aparecer
    // meia página.
    [Fact]
    public void ShellLayout_RendersOnlyWhenTheWholePageArrived()
    {
        var html = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Shared", "_DmoShellLayout.cshtml"));
        var head = html[..html.IndexOf("</head>", StringComparison.Ordinal)];
        Assert.Contains("<link rel=\"expect\" href=\"#dmoFim\" blocking=\"render\">", head);
        var fim = html.IndexOf("<div id=\"dmoFim\" hidden></div>", StringComparison.Ordinal);
        Assert.True(fim > html.LastIndexOf("<script", StringComparison.Ordinal), "o #dmoFim tem de vir depois dos scripts");
        // Os scripts de cada página (a secção só se desenha uma vez).
        Assert.True(fim > Posicao(html, "RenderSectionAsync(\"Scripts\""));
        Assert.Equal("</body>", html[(fim + "<div id=\"dmoFim\" hidden></div>".Length)..].Trim()[..7]);
    }

    [Fact]
    public void Calendar_MarkupLivesOnlyInTheSharedPartial()
    {
        foreach (var file in PageFiles("*.cshtml"))
        {
            if (Path.GetFileName(file) == "_DmoCalendar.cshtml")
                continue;
            var html = File.ReadAllText(file);
            Assert.False(html.Contains("dmo-calendar-days", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} desenha um calendário próprio; usar <partial name=\"_DmoCalendar\" />.");
        }
    }

    [Fact]
    public void Calendar_DaysAreBuiltOnlyByForMonth()
    {
        foreach (var file in PageFiles("*.cs"))
        {
            if (Path.GetFileName(file) == "DmoCalendarPresentation.cs")
                continue;
            var code = File.ReadAllText(file);
            Assert.False(Regex.IsMatch(code, @"new\s+List<[\w.]*DmoCalendarDay>"),
                $"{Path.GetFileName(file)} monta os dias à mão; usar DmoCalendarPresentation.ForMonth.");
        }
    }

    [Fact]
    public void Tables_UseOnlyTheSharedClass_WithoutPerModuleSelectionClasses()
    {
        var table = new Regex("<table\\b[^>]*>");
        foreach (var file in PageFiles("*.cshtml"))
        {
            foreach (Match m in table.Matches(File.ReadAllText(file)))
            {
                Assert.True(Regex.IsMatch(m.Value, "class=\"dmo-table[\" ]"),
                    $"{Path.GetFileName(file)}: {m.Value} não usa a classe dmo-table.");
                Assert.False(m.Value.Contains("row-select", StringComparison.Ordinal),
                    $"{Path.GetFileName(file)}: {m.Value} tem uma classe de seleção própria; a dmo-table já trata a linha selecionável.");
            }
        }
    }

    // A dica de ajuda (Diogo, 17/10/2026): um triângulo pequeno que inverte a
    // cor ao passar o rato e mostra o texto. Só o partial _DmoHelp a desenha.
    [Fact]
    public void Caret_UmaRegraParaTudo_AEsquerdaDoTexto()
    {
        // «Sempre do lado esquerdo e mais perto do font», «uma regra para tudo»
        // (Diogo, 22/10/2026): só .dmo-caret em dmo-normalize.css desenha o triângulo.
        const string caret = "<span class=\"dmo-caret\" aria-hidden=\"true\"></span>";
        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "dmo-normalize.css"));
        Assert.Contains("html body .dmo-caret{display:inline-block;flex:none;width:6px;height:9px;margin-right:.25em;", css, StringComparison.Ordinal);
        Assert.Contains("clip-path:polygon(0 0,100% 50%,0 100%)", css, StringComparison.Ordinal);
        Assert.Contains("html body :is(details[open]>summary,[aria-expanded=\"true\"]) .dmo-caret{transform:rotate(90deg)}", css, StringComparison.Ordinal);

        // Nenhuma outra folha de estilo nem página define o seu triângulo.
        var estilos = Directory.GetFiles(Path.Combine(WebRoot(), "wwwroot"), "*.css", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".min.css", StringComparison.Ordinal));
        var paginas = Directory.GetFiles(Path.Combine(WebRoot(), "Pages"), "*.cshtml", SearchOption.AllDirectories);
        foreach (var f in estilos.Concat(paginas))
        {
            var text = File.ReadAllText(f).Replace("caret-color", "", StringComparison.Ordinal);
            Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex(@"caret(?<!dmo-caret)"), text);
            if (!f.EndsWith("dmo-normalize.css", StringComparison.Ordinal))
                Assert.DoesNotContain(".dmo-caret", text, StringComparison.Ordinal);
        }

        // Cada <summary> e cada grupo do painel das Definições abre com o triângulo partilhado.
        foreach (var f in paginas)
        {
            var text = File.ReadAllText(f);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"(?<!/// )<summary\b[^>]*>(?<corpo>[\s\S]*?)</summary>"))
                Assert.True(m.Groups["corpo"].Value.Contains(caret, StringComparison.Ordinal), $"{Path.GetFileName(f)}: {m.Value[..Math.Min(80, m.Value.Length)]}");
        }
        var definicoes = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Definicoes", "Index.cshtml"));
        Assert.Contains("data-dmo-def-grupo-titulo", definicoes, StringComparison.Ordinal);
        Assert.Contains(caret + "<span>@nome</span>", definicoes, StringComparison.Ordinal);
    }

    // UM SÓ TRIÂNGULO E O INVERTIDO BRANCO (Diogo, 26/10/2026: «dois triângulos»;
    // «invertido deveria ser branco em todos»; os tipos de movimento das BQ
    // «iguais aos botões, o escolhido invertido»).
    [Fact]
    public void CardDobravel_UmSoTriangulo_EBotaoInvertido_BrancoNoEscuro()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "dmo-normalize.css"));
        Assert.Contains("html body summary.dmo-card-topo>:first-child::before{content:none}", css, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] body :is(.dmo-button,.button,.btn):is(:hover,:focus-visible){background:var(--dmo-invertido)}", css, StringComparison.Ordinal);

        var bq = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "dmo-boquilhas-prototype.css"));
        foreach (var raiz in new[] { ".beta-bq-layout", ".bq-sem-jobon-janela" })
        {
            Assert.Contains(raiz + " .bq-movement-types span { background:var(--dmo-brand-600);border-color:var(--dmo-brand-600);color:var(--dmo-text-on-color); }", bq, StringComparison.Ordinal);
            Assert.Contains(raiz + " .bq-movement-types :is(label:hover input, input:checked) + span { background:var(--dmo-invertido);border-color:var(--dmo-brand-600);color:var(--dmo-brand-600); }", bq, StringComparison.Ordinal);
            Assert.Contains(raiz + " .bq-movement-types input:focus-visible + span { outline:0;", bq, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial()
    {
        var partial = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Shared", "_DmoHelp.cshtml"));
        Assert.Contains("class=\"dmo-help-mark\"", partial, StringComparison.Ordinal);
        // Círculo vazio, sem «?» (Diogo, 22/10/2026); o triângulo fica só para recolher.
        Assert.Contains("<circle cx=\"5\" cy=\"5\" r=\"4.2\" />", partial, StringComparison.Ordinal);
        Assert.DoesNotContain("<text", partial, StringComparison.Ordinal);
        Assert.DoesNotContain("<path", partial, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "dmo-normalize.css"));
        Assert.Contains(".dmo-help-mark circle{fill:transparent;", css, StringComparison.Ordinal);
        // Sempre no mesmo sítio em relação ao texto (Diogo, 22/10/2026): uma caixa
        // da linha de base à altura das maiúsculas (em em), com o círculo em cima,
        // em linha ou num flex alinhado pela linha de base.
        // Um só tamanho e um só sítio (Diogo, 23/10/2026): 8px, a 3px da palavra, elevado 4px acima do topo das maiúsculas.
        Assert.Contains(".dmo-help{position:relative;display:inline-block;width:8px;height:.72em;font-size:inherit;vertical-align:baseline;align-self:baseline;margin-left:3px;", css, StringComparison.Ordinal);
        Assert.Contains("html body :is(h1,h2,h3,h4,label,legend,summary):has(>:is(.dmo-help,.dmo-com-ajuda)){align-items:baseline!important}", css, StringComparison.Ordinal);
        // Um elemento com a sua ajuda (a etiqueta «Só de leitura») fica junto: o gap do título não os separa.
        Assert.Contains("html body .dmo-com-ajuda{display:inline-flex;align-items:baseline}", css, StringComparison.Ordinal);
        Assert.Contains("<span class=\"dmo-com-ajuda\"><button type=\"button\" class=\"def-cadeado\"", File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Definicoes", "_DefinicoesCadeado.cshtml")), StringComparison.Ordinal);
        Assert.Contains(".dmo-help-mark{position:absolute;left:0;top:-4px;display:block;overflow:visible;width:8px;height:8px}", css, StringComparison.Ordinal);
        // Ao lado de um botão, a mesma regra.
        Assert.Contains(":is(.dmo-button,button,.dmo-options)+.dmo-help{margin-left:3px}", css, StringComparison.Ordinal);
        Assert.DoesNotContain("margin-top:-5px", css, StringComparison.Ordinal);
        Assert.Contains(".dmo-help:hover .dmo-help-mark circle,.dmo-help:focus .dmo-help-mark circle{fill:var(--dmo-palette-ocean)}", css, StringComparison.Ordinal);

        foreach (var file in PageFiles("*.cshtml"))
        {
            if (Path.GetFileName(file) == "_DmoHelp.cshtml")
                continue;
            var html = File.ReadAllText(file);
            Assert.DoesNotContain("caret\" aria-hidden=\"true\">▾<", html, StringComparison.Ordinal);
            Assert.False(html.Contains("dmo-help-tip", StringComparison.Ordinal) || html.Contains("dmo-help-mark", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} desenha uma dica própria; usar <partial name=\"_DmoHelp\" />.");
        }
    }

    // Os avisos do separador Issues do Chrome (Diogo, 17/10/2026): nada
    // focável dentro de um <summary> (a dica entra com EmSumario) e nenhum
    // <label> sem campo; um rótulo sem campo é um <span class="dmo-label">.
    [Fact]
    public void Markup_HasNoInteractiveSummaryContent_NorLabelsWithoutField()
    {
        foreach (var file in PageFiles("*.cshtml"))
        {
            var name = Path.GetFileName(file);
            var html = File.ReadAllText(file);
            foreach (Match summary in Regex.Matches(html, @"<summary\b[^>]*>(.*?)</summary>", RegexOptions.Singleline))
            {
                var body = summary.Groups[1].Value;
                Assert.False(Regex.IsMatch(body, @"<(?:a|button|input|select|textarea)\b|tabindex="),
                    $"{name}: {summary.Value} tem um elemento interativo dentro do <summary>.");
                Assert.False(body.Contains("_DmoHelp", StringComparison.Ordinal) && !body.Contains("EmSumario", StringComparison.Ordinal),
                    $"{name}: a dica dentro de um <summary> leva view-data com EmSumario.");
            }
            foreach (Match label in Regex.Matches(html, @"<label\b([^>]*)>(.*?)</label>", RegexOptions.Singleline))
            {
                Assert.True(label.Groups[1].Value.Contains("for=", StringComparison.Ordinal)
                        || Regex.IsMatch(label.Groups[2].Value, @"<(?:input|select|textarea)\b"),
                    $"{name}: {label.Value} não está associado a um campo; usar for= ou <span class=\"dmo-label\">.");
            }
        }
    }

    // As páginas já passadas para a dica não voltam a ter textos explicativos
    // escritos por baixo dos campos ou dos títulos.
    [Theory]
    [InlineData("Boquilhas", "Index.cshtml")]
    [InlineData("Shared", "_BqToolSheet.cshtml")]
    [InlineData("Shared", "_BqToolCatalog.cshtml")]
    [InlineData("Ferramentas", "Ficha.cshtml")]
    [InlineData("Admin/Templates", "Index.cshtml")]
    [InlineData("Admin/Users", "Index.cshtml")]
    [InlineData("Admin/Users", "Ficha.cshtml")]
    [InlineData("Admin/Definicoes", "_DefinicoesApp.cshtml")]
    [InlineData("ReparacaoInterna", "_DefinicoesGeral.cshtml")]
    [InlineData("Boquilhas", "Definicoes.cshtml")]
    [InlineData("Ferramentas", "Definicoes.cshtml")]
    [InlineData("Ferramentas", "Index.cshtml")]
    [InlineData("JobOn", "Index.cshtml")]
    [InlineData("Controlo", "Approve.cshtml")]
    [InlineData("Controlo", "ApproveDetalhe.cshtml")]
    public void HelpTexts_LiveInTheTip_NotWrittenOnTheScreen(string folder, string name)
    {
        var html = File.ReadAllText(Path.Combine(WebRoot(), "Pages", folder, name));
        foreach (Match match in Regex.Matches(html, @"<(?:p|span|div)\s+class=""[^""]*(?:muted|hint|note)[^""]*""[^>]*>([^<]*)|<small[^>]*>([^<]*)"))
        {
            var text = match.Groups[1].Value + match.Groups[2].Value;
            Assert.False(text.Contains("clique", StringComparison.OrdinalIgnoreCase),
                $"{name}: «{text.Trim()}» é um texto de ajuda escrito no ecrã; passar para <partial name=\"_DmoHelp\" />.");
        }
    }

    [Fact]
    public void InfoCards_UseOnlyTheSharedClass()
    {
        // Os cartões de informação são só dmo-fact (Diogo, 17/10/2026). O Peso
        // passa quando a folha dele for refeita.
        foreach (var file in PageFiles("*.cshtml"))
        {
            if (Path.GetFileName(file) == "Peso.cshtml")
                continue;
            var html = File.ReadAllText(file);
            Assert.False(Regex.IsMatch(html, "class=\"(ferr|peg|dfu)-fact"),
                $"{Path.GetFileName(file)} tem cartões próprios; usar dmo-facts/dmo-fact.");
        }
    }

    [Fact]
    public void Dropdowns_ApplyOnChoose_WithoutAnApplyButton()
    {
        // Escolher uma opção num dropdown aplica logo (Diogo, 17/10/2026): o
        // select leva data-dmo-autosubmit e não há «Aplicar» nem handlers
        // próprios por página. O Peso passa na thread dele.
        var form = new Regex("<form\\b[^>]*>(.*?)</form>", RegexOptions.Singleline);
        foreach (var file in PageFiles("*.cshtml"))
        {
            if (Path.GetFileName(file) == "Peso.cshtml")
                continue;
            var html = File.ReadAllText(file);
            var name = Path.GetFileName(file);
            Assert.False(html.Contains("onchange=\"this.form", StringComparison.Ordinal), $"{name}: usar data-dmo-autosubmit.");
            Assert.False(html.Contains("data-auto-submit", StringComparison.Ordinal), $"{name}: usar data-dmo-autosubmit.");
            Assert.False(Regex.IsMatch(html, @">\s*Aplicar[^<]*</button>"), $"{name} tem um botão «Aplicar».");
            foreach (Match m in form.Matches(html))
            {
                var body = m.Groups[1].Value;
                // Um formulário só com dropdowns (sem texto a escrever) não tem botão.
                var writes = Regex.IsMatch(body, "<textarea|<input\\b(?![^>]*type=\"(hidden|radio|checkbox)\")");
                if (!body.Contains("data-dmo-autosubmit", StringComparison.Ordinal) || writes)
                    continue;
                Assert.False(Regex.IsMatch(body, "<button(?![^>]*formaction)[^>]*type=\"submit\""),
                    $"{name}: o formulário só com dropdowns ainda tem botão de submeter.");
            }
        }
    }

    [Fact]
    public void Buttons_HaveOnlyTheirThinBorder_NoOutlineAround()
    {
        // Sem a segunda borda à volta dos botões (Diogo, 17/10/2026): o hover
        // e o foco não desenham outline; o foco do teclado é um halo suave.
        var rule = new Regex(@"([^{}]+)\{([^{}]*)\}");
        var css = Directory.EnumerateFiles(Path.Combine(WebRoot(), "wwwroot"), "*.css", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        foreach (var file in css)
        {
            foreach (Match m in rule.Matches(File.ReadAllText(file)))
            {
                var selector = m.Groups[1].Value;
                if (!Regex.IsMatch(selector, @"\.(dmo-button|btn|button)\b|dmo-calendar-nav button") || !selector.Contains(":hover", StringComparison.Ordinal))
                    continue;
                Assert.False(Regex.IsMatch(m.Groups[2].Value, @"outline:\s*\d+px"),
                    $"{Path.GetFileName(file)}: {selector.Trim()} desenha uma borda extra à volta do botão.");
            }
        }
    }

    [Fact]
    public void Buttons_PressShowsALighterBlue()
    {
        // Todos os botões iguais (Diogo, 19/10/2026): ao carregar ficam com um
        // azul mais claro da paleta, nos dois ficheiros de estilo partilhados.
        foreach (var file in new[] { Path.Combine("css", "dmo-normalize.css"), "0_ASSET_CANONICAL_DESIGN_SYSTEM.css" })
        {
            var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", file));
            Assert.Contains("html body :is(.dmo-button,.button,.btn):active:not(:disabled):not([aria-disabled=\"true\"]){background:var(--button-press,var(--dmo-palette-steel))", css);
        }
    }

    [Fact]
    public void ForMonth_BuildsTheWholeMonth_WithCountsSelectionAndQuery()
    {
        var marcado = new DateOnly(2026, 2, 10);
        var cal = DmoCalendarPresentation.ForMonth(
            2026, 2,
            d => d == marcado ? 3 : 0,
            selected: new DateOnly(2026, 2, 12),
            navigationPage: "/JobOn/Index",
            preservedQuery: new Dictionary<string, string> { ["historico"] = "1" });

        Assert.Equal(28, cal.Days.Count);
        Assert.Equal(Enumerable.Range(1, 28), cal.Days.Select(d => d.Day));
        Assert.Equal([10], cal.Days.Where(d => d.Has).Select(d => d.Day));
        Assert.Equal(3, cal.Days[9].Count);
        Assert.Equal([12], cal.Days.Where(d => d.Selected).Select(d => d.Day));
        Assert.Equal("/JobOn/Index", cal.NavigationPage);
        Assert.Equal("1", cal.PreservedQuery["historico"]);
        Assert.Equal(DmoCalendarPresentation.MonthTitleFor(2026, 2), cal.MonthTitle);
    }

    [Fact]
    public void ForMonth_HasRuleCanDifferFromTheCount()
    {
        // A Revisão conta todos os controlos do dia, mas o ponto só acende
        // nos dias com controlos por aprovar.
        var cal = DmoCalendarPresentation.ForMonth(
            2026, 4,
            _ => 2,
            selected: null,
            navigationPage: "/Controlo/Approve",
            hasForDay: d => d.Day == 5);

        Assert.All(cal.Days, d => Assert.Equal(2, d.Count));
        Assert.Equal([5], cal.Days.Where(d => d.Has).Select(d => d.Day));
        Assert.DoesNotContain(cal.Days, d => d.Selected);
        Assert.Empty(cal.PreservedQuery);
    }

    // Um clique abre (Diogo, 17/10/2026: «vamos fazer 1 click, não há
    // necessidade para dois»). A lista do dia do Job On também abre com um
    // clique e outro clique na mesma linha tira-a (18/10/2026); nenhuma
    // página volta ao duplo clique para abrir.
    [Fact]
    public void Lists_OpenWithOneClick()
    {
        foreach (var file in PageFiles("*.cshtml"))
        {
            var html = File.ReadAllText(file);
            var name = Path.GetRelativePath(Path.Combine(WebRoot(), "Pages"), file).Replace('\\', '/');
            Assert.False(html.Contains("data-dmo-seleciona", StringComparison.Ordinal),
                $"{name}: as listas abrem com um clique.");
            // Seleciona primeiro: a lista do dia do Job On, a lista das
            // Ferramentas quando se escolhe uma ferramenta para outra página e
            // os «Pesos sem produção» do Peso (escolher para associar, com o
            // botão no canto do cartão, deep sweep 18/10/2026).
            // O Resumo muda o texto na thread dele.
            if (name is "JobOn/Index.cshtml" or "Ferramentas/Index.cshtml"
                or "Ferramentas/_FerramentasResultados.cshtml" or "Controlo/Resumo.cshtml"
                or "Controlo/Peso.cshtml")
                continue;
            Assert.False(Regex.IsMatch(html, "duplo clique (ou Enter )?abre", RegexOptions.IgnoreCase),
                $"{name} ainda diz que o duplo clique abre; agora abre com um clique.");
        }

        var jobOn = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "JobOn", "Index.cshtml"));
        Assert.Contains("id=\"lightProductionList\" class=\"beta-light-list\" data-dmo-list=\"Produções do dia\"", jobOn);
        Assert.Contains("data-dmo-fechar=", jobOn);

        var script = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "0_ASSET_CANONICAL_INTERACTIONS.js"));
        Assert.Contains("if (!selectsOnly(list) && row.dataset.dmoOpen) openRow(list, row);", script);
        Assert.Contains("if (row.dataset.dmoFechar && row.classList.contains(SELECTED))", script);
    }

    // Procurar sem botão (Diogo, 17/10/2026: «se existe um botão associado,
    // abrir a partir do filtro»): os filtros procuram ao escrever ou ao
    // escolher. Faltam o histórico e o «Associar Job On» das Boquilhas, e o
    // Peso, que mudam a seguir.
    [Fact]
    public void SearchFields_HaveNoSearchButton()
    {
        var pending = Array.Empty<string>();
        foreach (var file in PageFiles("*.cshtml"))
        {
            var name = Path.GetRelativePath(Path.Combine(WebRoot(), "Pages"), file).Replace('\\', '/');
            if (pending.Contains(name))
                continue;
            var html = File.ReadAllText(file);
            Assert.False(Regex.IsMatch(html, @">\s*(Procurar|Pesquisar)\s*</button>"),
                $"{name} ainda tem um botão «Procurar»; o filtro procura ao escrever.");
        }

        foreach (var (page, target, results) in new[]
                 {
                     ("Ferramentas/Index.cshtml", "ferrResultados", "Ferramentas/_FerramentasResultados.cshtml"),
                     ("Shared/_BqToolCatalog.cshtml", "bq-tool-results", "Shared/_BqToolCatalog.cshtml"),
                     ("Controlo/Approve.cshtml", "revisaoResultados", "Controlo/_RevisaoResultados.cshtml"),
                 })
        {
            var html = File.ReadAllText(Path.Combine(WebRoot(), "Pages", page));
            Assert.Contains($"data-dmo-ao-escrever=\"{target}\"", html);
            Assert.Contains($"id=\"{target}\"", File.ReadAllText(Path.Combine(WebRoot(), "Pages", results)));
            if (page != results) Assert.Contains($"<partial name=\"{Path.GetFileNameWithoutExtension(results)}\"", html);
        }

        // Sem botão, as datas e a vista do Histórico das Boquilhas aplicam logo e
        // o Enter num campo de texto submete mesmo com vários campos no formulário.
        var boquilhas = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Boquilhas", "Index.cshtml"));
        foreach (var id in new[] { "desde", "ate" })
            Assert.Matches($"<input id=\"{id}\"[^>]*data-dmo-autosubmit", boquilhas);
        Assert.Matches("<select id=\"vista\"[^>]*data-dmo-autosubmit", boquilhas);
        Assert.Contains("input[type=month]", File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "0_ASSET_CANONICAL_INTERACTIONS.js")));
        var suggest = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "js", "dmo-suggest.js"));
        Assert.Contains("if (event.key !== \"Enter\" || event.defaultPrevented || !input.form) return;", suggest);
    }

    // Dia selecionado + rato por cima (Diogo, 17/10/2026): o hover dos dias tem
    // mais peso que o selecionado e punha o fundo claro por baixo do texto
    // branco. O selecionado com hover mantém o fundo slate e o texto branco.
    [Fact]
    public void Calendar_SelectedDayHover_KeepsSlateBackground()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "beta-calendar.css"));
        var hover = css.IndexOf(":not(.empty):hover,", StringComparison.Ordinal);
        var rule = ".dmo-calendar .dmo-calendar-days>:is(button,.day,.dmo-calendar-day):is(.selected,.is-selected):not(.empty):is(:hover,:focus-visible){background:var(--dmo-palette-slate);border-color:var(--dmo-palette-slate);color:var(--dmo-text-on-color);";
        var selectedHover = css.IndexOf(rule, StringComparison.Ordinal);
        Assert.True(hover >= 0 && selectedHover > hover, "O dia selecionado com o rato por cima tem de manter o fundo slate depois da regra do hover.");
    }

    // O dia de hoje (Diogo, 18/10/2026, «bug no 18»): só o negrito; a
    // moldura escura fazia-o parecer um segundo dia escolhido.
    [Fact]
    public void Calendar_Today_IsOnlyBold()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "beta-calendar.css"));
        var today = Regex.Matches(css, @"\.today\{([^}]*)\}").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["font-weight:800"], today);
    }

    // Mensagens de resultado (Diogo 17/10/2026: «quando eliminamos um template
    // esse notice aparece e empurra o layout»): saem do fluxo da página pelo
    // estilo partilhado e dmo-live.js junta-as numa pilha com «×». Três tipos:
    // «ok» fecha sozinha; «erro» (uma recusa) e «aviso» (gravou, mas há uma
    // coisa a ver, como a contra costura ilegível nos Pegamentos) ficam até se
    // fecharem. Só «erro» conta como recusa para o DmoTroca.
    [Fact]
    public void ResultMessages_FloatAboveThePage()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "dmo-normalize.css"));
        Assert.Matches(@"\[data-dmo-toast\]\{position:fixed;", css);
        Assert.Contains("html body [data-dmo-toast=\"aviso\"]{border-left-color:var(--dmo-warning);background:var(--dmo-warning-soft)}", css, StringComparison.Ordinal);
        var js = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "dmo-live.js"));
        Assert.Contains("[data-dmo-toast]", js, StringComparison.Ordinal);
        // Só as «ok» fecham sozinhas.
        Assert.Contains("if (vida === null && mensagem.getAttribute(\"data-dmo-toast\") === \"ok\") vida = 6000;", js, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(js, "setTimeout\\(fechar"));
        var templates = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Admin", "Templates", "Index.cshtml"));
        foreach (var nota in new[] { "Template criada.", "Template guardada.", "Template eliminada." })
            Assert.Contains($"data-dmo-toast=\"ok\" class=\"dmt-nota\" role=\"status\">{nota}", templates, StringComparison.Ordinal);
        foreach (var file in PageFiles("*.cshtml"))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "data-dmo-toast=\"([^\"]*)\""))
                Assert.True(m.Groups[1].Value is "ok" or "erro" or "aviso" || m.Groups[1].Value.StartsWith('@'),
                    $"{Path.GetFileName(file)}: data-dmo-toast só aceita «ok», «erro» ou «aviso».");
    }

    // Utilizadores do Admin: o aviso do PIN (alto, só fecha no «×») tapava
    // «Criar utilizador» no canto do cartão; nesta página a pilha fica em
    // baixo (B-admin-2, deep sweep 18/10/2026).
    [Fact]
    public void AdminUsersToasts_StayClearOfTheCreateButton()
    {
        var page = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Admin", "Users", "Index.cshtml"));
        Assert.Contains("html body:has(.dmu-page) .dmo-toast-pilha,html body #dmuAvisos [data-dmo-toast]{top:auto!important;bottom:16px}", page, StringComparison.Ordinal);
        Assert.Contains("<button type=\"submit\" class=\"dmo-button\" id=\"dmuCriarBotao\" form=\"dmuCriarForm\">Criar utilizador</button>", page, StringComparison.Ordinal);
    }

    // Registar ferramenta: a nota do nome técnico aparece ao escrever; com a
    // linha reservada, as máquinas por baixo não descem (regra 2, deep sweep
    // 18/10/2026).
    [Fact]
    public void FerramentasTechnicalNotice_KeepsItsLine()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "dmo-ferramentas.css"));
        Assert.Contains("#ferr-technical-notice,#ferr-technical-notice:empty{display:block;min-height:1lh}", css, StringComparison.Ordinal);
        var page = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Ferramentas", "_FerramentaFactos.cshtml"));
        Assert.Contains("<p class=\"ferr-hint\" id=\"ferr-technical-notice\"", page, StringComparison.Ordinal);
    }

    // A ficha de uma ferramenta: os avisos âmbar depois de gravar (lote vazio,
    // quantidade ou valor técnico ilegível, lote já registado, tipo mudado
    // numa ferramenta já usada) vão para o canto; na grelha empurravam os
    // cartões (beta.199). O teste no browser
    // (tests/visual/ficha_avisos_browser.py) mede que nada se mexe.
    [Fact]
    public void FichaWarnings_GoToTheCorner()
    {
        var ficha = File.ReadAllText(Path.Combine(WebRoot(), "Pages", "Ferramentas", "Ficha.cshtml"));
        var avisos = Regex.Matches(ficha, "<div[^>]*class=\"ferr-aviso warn\"[^>]*>");
        Assert.Equal(7, avisos.Count); // + o % uso do Editar (22/10/2026)
        Assert.All(avisos, m => Assert.Contains("data-dmo-toast=\"aviso\"", m.Value, StringComparison.Ordinal));
        // O aviso de ferramenta repetida (18/10/2026) não é de gravar: fica no
        // seu cartão, já na página ao abrir, até se limpar.
        Assert.Contains("<div class=\"ferr-aviso warn ferr-repetida-texto\" role=\"status\">", ficha, StringComparison.Ordinal);
    }
}

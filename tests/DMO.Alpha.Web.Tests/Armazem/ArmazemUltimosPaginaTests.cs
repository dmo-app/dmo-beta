using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// Os Movimentos do Armazém (Diogo, 22/10/2026): a função vem logo a seguir
/// ao Registo e substitui o Histórico; à esquerda o que está fora, em
/// reparação (só CM e MF), com a linha a abrir a ficha a quem tem as
/// Ferramentas; à direita as últimas entradas e saídas (todas, BQ
/// incluídas), com o reparador, a posição na Entrada e os filtros que
/// aplicam ao escolher e trocam só esse cartão. Os testes da página do
/// Histórico (ArmazemHistoricoPaginaTests) vieram para aqui.
/// </summary>
public sealed class ArmazemUltimosPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-rep-cm";
    private const string Mf = "arm-rep-mf";
    private const string Bq = "arm-rep-bq";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemUltimosPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados);
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Reparadores.RemoveRange(db.Reparadores);
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == Mf || t.ToolId == Bq));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "6012A118", Lot = "7" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Codigo = "0042", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Codigo = "0043", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Codigo = "5001", CriadaEmUtc = DateTime.UtcNow });
        var reparador = new Reparador { Nome = "Moldes Lda" };
        db.Reparadores.Add(reparador);
        var bqReparador = db.BqRepairers.FirstOrDefault(r => r.Name == "LE");
        if (bqReparador is null)
        {
            bqReparador = new BqRepairer { Name = "LE" };
            db.BqRepairers.Add(bqReparador);
        }

        db.SaveChanges();
        var s = new ArmazemMovimentos(db, new OperacoesRegistadas(db), new Sessao());
        void Ok(ArmazemMovimentoSaida r) => Assert.True(r.Gravou, r.Resultado.ToString());
        Ok(s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, D(10), null)).GetAwaiter().GetResult());
        Ok(s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Mf, "0043", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, D(10), null)).GetAwaiter().GetResult());
        Ok(s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Bq, "5001", ArmazemOrigem.Nova, FerramentaEstadoTipo.Novo, D(10), null)).GetAwaiter().GetResult());
        Ok(s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Cm, ArmazemDestino.Reparacao, reparador.Id, null, null, D(11), null)).GetAwaiter().GetResult());
        Ok(s.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "0042", ArmazemOrigem.Reparacao, FerramentaEstadoTipo.Reparado, D(14), null)).GetAwaiter().GetResult());
        Ok(s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Mf, ArmazemDestino.Reparacao, reparador.Id, null, null, D(12), null)).GetAwaiter().GetResult());
        Ok(s.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Bq, ArmazemDestino.Reparacao, bqReparador.Id, null, null, D(13), null)).GetAwaiter().GetResult());
    }

    private static DateTime D(int dia) => new(2026, 10, dia, 9, 0, 0, DateTimeKind.Utc);

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Armazem{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armult-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var response = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", email),
            ("password", password),
            ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>O elemento que abre com este atributo, até ao seu fecho. Falha se não existir.</summary>
    private static string Elemento(string html, string atributo)
    {
        var inicio = html.IndexOf(atributo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"a página não tem {atributo}");
        inicio = html.LastIndexOf('<', inicio);
        var tag = Regex.Match(html[(inicio + 1)..], "^[a-z][a-z0-9]*").Value;
        var nivel = 0;
        foreach (Match m in Regex.Matches(html[inicio..], $"<{tag}\\b|</{tag}>"))
        {
            nivel += m.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (nivel == 0)
            {
                return html.Substring(inicio, m.Index + m.Length);
            }
        }

        throw new InvalidOperationException($"{atributo} ficou por fechar");
    }

    private static string Texto(string html) =>
        System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", " "), "\\s+", " ")).Trim();

    [Fact]
    public async Task SemAChave_NaoEntra()
    {
        using var client = await LoginOperatorAsync("6601", DmoCapabilities.Ferramentas);

        using var response = await client.GetAsync("/Armazem/Ultimos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Armazem", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AsFuncoes_RegistoDepoisMovimentos_SemHistorico()
    {
        using var client = await LoginOperatorAsync("6602", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Ultimos");
        using var antigo = await client.GetAsync("/Armazem/Historico");
        using var mapa = await client.GetAsync("/Armazem/Mapa");

        Assert.Matches("<a href=\"/Armazem/Ultimos\"[^>]*aria-current=\"page\">Movimentos</a>", html);
        Assert.Contains("<title>Movimentos", html);
        var registo = html.IndexOf(">Registo</a>", StringComparison.Ordinal);
        var movimentos = html.IndexOf(">Movimentos</a>", StringComparison.Ordinal);
        // As Definições saíram da barra do módulo para a barra de cima (Diogo, 22/10/2026).
        Assert.True(registo >= 0 && registo < movimentos, "Registo, Movimentos");
        Assert.DoesNotContain("href=\"/Armazem/Definicoes\"", html);
        Assert.DoesNotContain(">Próximos Job On</a>", html);
        Assert.DoesNotContain(">Posições</a>", html);
        // Diogo, 22/10/2026: o Mapa já não faz falta.
        Assert.DoesNotContain(">Mapa</a>", html);
        Assert.DoesNotContain(">Histórico</a>", html);
        Assert.DoesNotContain("/Armazem/Historico", html);
        Assert.Equal(HttpStatusCode.NotFound, antigo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mapa.StatusCode);
        Assert.DoesNotContain("Máquina", html);
    }

    [Fact]
    public async Task Fora_SoCmEMf_ComReparadorEDia_BqNao()
    {
        using var client = await LoginOperatorAsync("6603", DmoCapabilities.ArmazemGestao, DmoCapabilities.Ferramentas);

        var html = await client.GetStringAsync("/Armazem/Ultimos");

        var fora = Elemento(html, "id=\"armRepForaLista\"");
        var linha = Assert.Single(Regex.Matches(fora, "<tr data-dmo-row[^>]*>"));
        Assert.Contains($"data-dmo-fora=\"{Mf}\"", linha.Value);
        Assert.Contains($"data-dmo-open=\"/Ferramentas/Ficha?tool_id={Mf}\"", linha.Value);
        Assert.Contains("MF 6012 · Lote 7 Todas — Moldes Lda 12/10/2026", Texto(fora));
        Assert.DoesNotContain(Bq, fora);
        Assert.DoesNotContain(Cm, fora);
        Assert.DoesNotContain("<button", fora);
    }

    [Fact]
    public async Task Fora_SemFerramentas_NaoAbreAFicha()
    {
        using var client = await LoginOperatorAsync("6604", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Ultimos");

        var fora = Elemento(html, "id=\"armRepForaLista\"");
        Assert.Contains($"data-dmo-fora=\"{Mf}\"", fora);
        Assert.DoesNotContain("data-dmo-open", fora);
        Assert.DoesNotContain("/Ferramentas/Ficha", fora);
    }

    [Fact]
    public async Task Ultimas_Todas_DoMaisRecente_BqIncluida_PosicaoSoNaEntrada()
    {
        using var client = await LoginOperatorAsync("6605", DmoCapabilities.ArmazemGestao);

        var html = await client.GetStringAsync("/Armazem/Ultimos");
        ExportarParaOBrowser.Gravar("armazem-movimentos.html", html);

        var cartao = Elemento(html, "id=\"armMovimentos\"");
        Assert.Contains("data-dmo-regiao", cartao[..200]);
        Assert.Contains("data-dmo-parcial", cartao[..200]);
        var lista = Elemento(cartao, "id=\"armMovLista\"");
        var linhas = Regex.Matches(lista, "<tr data-dmo-movimento=\"\\d+\"[^>]*>(.*?)</tr>", RegexOptions.Singleline)
            .Select(m => Texto(m.Groups[1].Value)).ToArray();
        Assert.Equal(
            [
                "14/10/2026 CM 5447 · Lote 3 Todas — Entrada Moldes Lda 0042",
                "13/10/2026 BQ T173 · Lote 01 Todas — Saída · Reparação LE —",
                "12/10/2026 MF 6012 · Lote 7 Todas — Saída · Reparação Moldes Lda —",
                "11/10/2026 CM 5447 · Lote 3 Todas — Saída · Reparação Moldes Lda —",
                "10/10/2026 BQ T173 · Lote 01 Todas — Entrada — 5001",
                "10/10/2026 MF 6012 · Lote 7 Todas — Entrada — 0043",
                "10/10/2026 CM 5447 · Lote 3 Todas — Entrada — 0042"
            ],
            linhas);
        Assert.DoesNotContain("<button", lista);
        Assert.Contains("dmo-list-pager", cartao);
        var filtros = Elemento(cartao, "id=\"armMovFiltros\"");
        Assert.DoesNotContain("<button", filtros);
        Assert.Equal(5, Regex.Matches(filtros, "data-dmo-autosubmit").Count);
        foreach (var nome in new[] { "de", "ate", "tipo", "referencia", "lote", "linha", "reparador" })
        {
            Assert.Contains($"name=\"{nome}\"", filtros);
        }

        Assert.DoesNotContain("id=\"armMovLimpar\"", html);
    }

    [Fact]
    public async Task Ultimas_OsFiltros()
    {
        using var client = await LoginOperatorAsync("6606", DmoCapabilities.ArmazemGestao);

        var dias = await client.GetStringAsync("/Armazem/Ultimos?de=2026-10-12&ate=2026-10-13");
        var bq = await client.GetStringAsync("/Armazem/Ultimos?tipo=BQ");
        var referencia = await client.GetStringAsync("/Armazem/Ultimos?referencia=5447");
        var lote = await client.GetStringAsync("/Armazem/Ultimos?lote=7");
        var reparador = await client.GetStringAsync("/Armazem/Ultimos?reparador=LE");
        var nada = await client.GetStringAsync("/Armazem/Ultimos?de=2026-11-01");

        int Linhas(string html) => Regex.Matches(Elemento(html, "id=\"armMovLista\""), "<tr data-dmo-movimento").Count;
        Assert.Equal(2, Linhas(dias));
        Assert.Matches("<input id=\"armMovDe\"[^>]*value=\"2026-10-12\"", dias);
        Assert.Contains("id=\"armMovLimpar\"", dias);
        Assert.Equal(2, Linhas(bq));
        Assert.Matches("<option value=\"BQ\" selected", bq);
        Assert.Equal(3, Linhas(referencia));
        Assert.Equal(2, Linhas(lote));
        Assert.Equal(1, Linhas(reparador));
        Assert.Contains("<option value=\"Moldes Lda\">", reparador);
        Assert.Contains("Nenhuma entrada ou saída com esse filtro.", Elemento(nada, "id=\"armMovVazio\""));
        // Os filtros só mexem na direita: a esquerda continua a mostrar o MF.
        Assert.Contains($"data-dmo-fora=\"{Mf}\"", nada);
    }

    [Fact]
    public async Task AFicha_MostraALocalizacaoEOsMovimentos_SoAQuemTemOArmazem()
    {
        using var comArmazem = await LoginOperatorAsync("6607", DmoCapabilities.Ferramentas, DmoCapabilities.ArmazemGestao);
        using var semArmazem = await LoginOperatorAsync("6608", DmoCapabilities.Ferramentas);

        var ficha = await comArmazem.GetStringAsync($"/Ferramentas/Ficha?tool_id={Cm}");
        var fichaBq = await comArmazem.GetStringAsync($"/Ferramentas/Ficha?tool_id={Bq}");
        var fichaSem = await semArmazem.GetStringAsync($"/Ferramentas/Ficha?tool_id={Cm}");

        Assert.Contains("Armazém · posição 0042", Texto(Elemento(ficha, "id=\"ferrLocalizacao\"")));
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Cm}\"", Elemento(ficha, "id=\"ferrLocalizacao\""));
        Assert.Contains("Reparado", Texto(Elemento(ficha, "id=\"ferrEstadoArmazem\"")));
        Assert.Contains("Reparação · LE", Texto(Elemento(fichaBq, "id=\"ferrLocalizacao\"")));
        Assert.Contains($"href=\"/Armazem/Ultimos?ferramenta={Cm}\"", ficha);
        Assert.DoesNotContain("/Armazem/Historico", ficha);
        // A posição e o estado a todos (Diogo, 22/10/2026); o link só a quem tem o Armazém.
        Assert.Contains("0042", Texto(Elemento(ficha, "id=\"ferrPosicao\"")));
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Cm}\"", Elemento(ficha, "id=\"ferrEstadoArmazem\""));
        Assert.Contains("0042", Texto(Elemento(fichaSem, "id=\"ferrPosicao\"")));
        Assert.Contains("Reparado", Texto(Elemento(fichaSem, "id=\"ferrEstadoArmazem\"")));
        Assert.Contains("—", Texto(Elemento(fichaBq, "id=\"ferrPosicao\"")));
        Assert.DoesNotContain("ferrLocalizacao", fichaSem);
        Assert.DoesNotContain("/Armazem/", fichaSem);

        var movimentos = await comArmazem.GetStringAsync($"/Armazem/Ultimos?ferramenta={Cm}");
        Assert.Contains("Só CM 5447T173 · lote 3.", Texto(Elemento(movimentos, "id=\"armMovFerramenta\"")));
        Assert.Equal(3, Regex.Matches(Elemento(movimentos, "id=\"armMovLista\""), "<tr data-dmo-movimento").Count);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

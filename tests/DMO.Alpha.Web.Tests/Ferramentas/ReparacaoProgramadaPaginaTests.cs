using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Ferramentas;

/// <summary>
/// A Reparação Programada, separador das Ferramentas (Diogo, 22/10/2026):
/// só quem tem as Ferramentas entra; a procura abre vazia; associar cria a
/// lista do reparador; a Entrada no Armazém de uma ferramenta da lista vem
/// com «Reparado» e o aviso de mudar; depois da Entrada a linha fica cinza
/// com o dia, a posição e quem deu entrada; «Imprimir» abre a vista do
/// browser.
/// </summary>
public sealed partial class ReparacaoProgramadaPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "rp-cm-5447-3";
    private const string Pagina = "/Ferramentas/ReparacaoProgramada";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoProgramadaPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ReparacaoProgramadaItens.RemoveRange(db.ReparacaoProgramadaItens);
        db.ReparacaoProgramadaListas.RemoveRange(db.ReparacaoProgramadaListas);
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados);
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Reparadores.RemoveRange(db.Reparadores);
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm));
        db.SaveChanges();
        db.Tools.Add(new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447RP", Lot = "3" });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Codigo = "1033", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Codigo = "4152", CriadaEmUtc = DateTime.UtcNow });
        db.Reparadores.Add(new Reparador { Nome = "Moldin" });
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Programada{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-rp-{operatorId}-{Guid.NewGuid():N}", Name = $"Programada {operatorId}", Capabilities = capabilities };
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

    private T NaBase<T>(Func<DmoDbContext, T> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    private async Task NoArmazemAsync(Func<IArmazemMovimentos, Task<ArmazemMovimentoSaida>> movimento)
    {
        using var scope = _factory.Services.CreateScope();
        var saida = await movimento(scope.ServiceProvider.GetRequiredService<IArmazemMovimentos>());
        Assert.True(saida.Gravou, saida.Resultado.ToString());
    }

    private int ReparadorId() => NaBase(db => db.Reparadores.Single().Id);

    private static async Task<HttpResponseMessage> AssociarAsync(HttpClient client, string html, params (string Nome, string Valor)[] campos)
    {
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        return await client.PostAsync($"{Pagina}?handler=Associar",
            HtmlFormHelpers.BuildFormPost([.. campos, ("__RequestVerificationToken", token)]));
    }

    /// <summary>
    /// DMO-591 (Diogo, 29/10/2026): «Talamolde · 29/10/2026» não cabia no
    /// campo «Lista» e o dia saía por cima do «Reparador»; e ao escrever 1075
    /// na Referência «nao aparece nada» (só procurava com Enter). O campo
    /// mostra só o reparador e a lista aberta o dia; a procura corre ao
    /// escrever e o servidor devolve só a zona dos resultados, com o
    /// «Adicionar» a apontar para a lista escolhida. O Chromium a 1290×646
    /// prova-se em tests/visual/reparacao_listas_browser.py.
    /// </summary>
    [Fact]
    public async Task Lista_MostraSoOReparador_EAProcuraCorreAoEscrever()
    {
        var listaId = NaBase(db =>
        {
            var l = new ReparacaoProgramadaLista { ReparadorNome = "Talamolde", CriadaEmUtc = new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc) };
            db.ReparacaoProgramadaListas.Add(l);
            db.SaveChanges();
            return l.Id;
        });
        using var chefia = await LoginAsync("7409", DmoCapabilities.Ferramentas);

        var html = await chefia.GetStringAsync(Pagina);
        var escolha = Regex.Match(html, "<select id=\"rpListaEscolha\"[\\s\\S]*?</select>").Value;
        Assert.Contains("<button type=\"button\"><selectedcontent></selectedcontent></button>", escolha);
        Assert.Contains($"<option value=\"{listaId}\" selected=\"selected\"><span>Talamolde</span><span class=\"rp-dia\"> · 29/10/2026</span></option>", escolha);
        Assert.Contains("#rpListaEscolha selectedcontent .rp-dia{display:none}", html);
        var filtros = Regex.Match(html, "<form id=\"rpFiltros\"[^>]*>").Value;
        Assert.Contains("data-dmo-ao-escrever=\"rpResultados\"", filtros);
        Assert.Contains("data-dmo-handler=\"Resultados\"", filtros);
        Assert.Contains("data-dmo-fica", filtros);
        Assert.Contains("<div id=\"rpResultados\" data-dmo-regiao>", html);
        Assert.Contains("data-dmo-filtros=\"rpFiltros tipo referencia lote linha estado\"", html);
        Assert.Contains("id=\"rpLimpar\" data-dmo-completa", html);

        var resposta = await chefia.GetAsync($"{Pagina}?handler=Resultados&referencia=5447");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("rpResultados", resposta.Headers.GetValues("X-Dmo-Region").Single());
        var zona = (await resposta.Content.ReadAsStringAsync()).Trim();
        Assert.StartsWith("<div id=\"rpResultados\" data-dmo-regiao>", zona);
        Assert.DoesNotContain("<html", zona);
        Assert.Contains($"data-dmo-candidata=\"{Cm}\"", zona);
        Assert.Contains($"name=\"destino\" value=\"{listaId}\"", zona);
        Assert.Contains("name=\"referencia\" value=\"5447\"", zona);

        var nada = await chefia.GetStringAsync($"{Pagina}?handler=Resultados&referencia=1075");
        Assert.Contains("id=\"rpProcuraNada\"", nada);
    }

    [Fact]
    public async Task SemFerramentas_NaoEntra_NemAssocia_NemImprime()
    {
        using var armazem = await LoginAsync("7401", DmoCapabilities.ArmazemGestao);

        var pagina = await armazem.GetAsync(Pagina);
        var imprimir = await armazem.GetAsync("/Ferramentas/ReparacaoProgramada/Imprimir?lista=1");
        var armazemHtml = await armazem.GetStringAsync("/Armazem/Movimentos");
        var associar = await AssociarAsync(armazem, armazemHtml, ("ferramenta", Cm), ("reparador", ReparadorId().ToString()));

        Assert.Equal(HttpStatusCode.Redirect, pagina.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, imprimir.StatusCode);
        // Recusado antes de chegar à página (o antiforgery ou o módulo): nada grava.
        Assert.Contains(associar.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.BadRequest });
        Assert.DoesNotContain(Pagina, associar.Headers.Location?.OriginalString ?? string.Empty);
        Assert.Empty(NaBase(db => db.ReparacaoProgramadaListas.ToList()));
    }

    [Fact]
    public async Task Ferramentas_VeOSeparador_AProcuraAbreVazia_EAssociaAUmaListaNova()
    {
        await NoArmazemAsync(m => m.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "1033", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.PorReparar, null, null)));
        using var chefia = await LoginAsync("7402", DmoCapabilities.Ferramentas);

        var vazia = await chefia.GetStringAsync(Pagina);
        Assert.Contains("Registar Reparação", vazia);
        // A ajuda do «Histórico» encostada ao botão (Diogo, 24/10/2026); o Chromium mede-a.
        Assert.Contains("<span class=\"dmo-com-ajuda\"><a class=\"dmo-button\" id=\"rpModoHistorico\"", vazia);
        ExportarParaOBrowser.Gravar("rp-ajuda.html", vazia);
        Assert.Contains("href=\"/Ferramentas/ReparacaoProgramada\"", vazia);
        Assert.DoesNotContain("Escolha um filtro e procure.", vazia);
        Assert.DoesNotContain("id=\"rpCandidatas\"", vazia);
        Assert.DoesNotContain("Nenhuma lista ativa", vazia);

        var procura = await chefia.GetStringAsync($"{Pagina}?estado=PorReparar&referencia=5447RP");
        Assert.Contains($"data-dmo-candidata=\"{Cm}\"", procura);
        Assert.Matches("<span class=\"rp-pos\">1033</span>", procura);
        // Sem botões nas linhas: a linha abre a ficha.
        Assert.Contains($"data-dmo-open=\"/Ferramentas/Ficha?tool_id={Cm}\"", procura);
        Assert.DoesNotMatch(new Regex("<tbody>(?:(?!</tbody>).)*<button", RegexOptions.Singleline), procura);

        using var associada = await AssociarAsync(chefia, procura, ("ferramenta", Cm), ("destino", ""), ("reparador", ReparadorId().ToString()), ("estado", "PorReparar"));
        Assert.Equal(HttpStatusCode.Redirect, associada.StatusCode);
        var lista = await chefia.GetStringAsync(associada.Headers.Location!.OriginalString);

        Assert.Contains("1 ferramenta associada.", lista);
        Assert.Contains("id=\"rpListaAtiva\"", lista);
        Assert.Contains("<strong>Moldin</strong>", lista);
        // O nome da lista nova (Diogo, 23/10/2026: «nova lista fica com que nome?»): o reparador e o dia de Lisboa em que foi criada.
        var criada = NaBase(db => db.ReparacaoProgramadaListas.Single());
        var dia = DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(criada.CriadaEmUtc).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(DateTime.UtcNow).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture), dia);
        Assert.Matches($"<option value=\"{criada.Id}\" selected=\"selected\"><span>Moldin</span><span class=\"rp-dia\"> · {Regex.Escape(dia)}</span></option>", lista);
        Assert.Matches("data-dmo-progresso[^>]*>Por retirar<", lista);
        Assert.Contains("id=\"rpImprimir\"", lista);
        var item = NaBase(db => db.ReparacaoProgramadaItens.Single());
        Assert.Equal((Cm, "5447RP", "3"), (item.ToolId, item.Referencia, item.Lote));
    }

    [Fact]
    public async Task ArmazemEntrada_DeFerramentaNaLista_SugereReparado_AvisaAoMudar_EALinhaFicaCinza()
    {
        await NoArmazemAsync(m => m.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "1033", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.PorReparar, null, null)));
        using (var scope = _factory.Services.CreateScope())
        {
            var associada = await scope.ServiceProvider.GetRequiredService<IReparacaoProgramada>().AssociarAsync([Cm], null, ReparadorId());
            Assert.True(associada.Associou);
        }

        await NoArmazemAsync(m => m.SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), Cm, ArmazemDestino.Reparacao, ReparadorId(), null, null, null, null)));
        using var chefia = await LoginAsync("7403", DmoCapabilities.Ferramentas);
        var depoisDaSaida = await chefia.GetStringAsync(Pagina);
        Assert.Matches("data-dmo-progresso class=\"rp-saiu\">Saída \\d\\d/\\d\\d<", depoisDaSaida);
        Assert.Contains("Reparação · Moldin", depoisDaSaida);

        using var armazem = await LoginAsync("7404", DmoCapabilities.ArmazemGestao);
        var entrada = await armazem.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        Assert.Contains("id=\"armEntProgramada\"", entrada);
        Assert.Matches("<option value=\"2\" selected=\"selected\">Reparado</option>", entrada);
        Assert.Matches("id=\"armEntEstadoAviso\"[^>]*hidden", entrada);
        Assert.Contains("data-arm-sugerido=\"2\"", entrada);

        // Muda para «Por reparar»: grava (nunca bloqueia) e avisa.
        var (acao, campos) = EnvioDoFormulario.Enviar(entrada, [("armEntPosicao", "4152"), ("armEntEstado", "3")], idDoBotao: "armEntRegistar");
        using var gravada = await armazem.PostAsync(WebUtility.HtmlDecode(acao), EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, gravada.StatusCode);
        var depois = await armazem.GetStringAsync(gravada.Headers.Location!.OriginalString);
        Assert.Contains("Reparação Programada de Moldin", WebUtility.HtmlDecode(depois));

        var listaId = NaBase(db => db.ReparacaoProgramadaListas.Single().Id);
        // No Histórico a lista abre com um clique na linha (só para ver).
        var historico = await chefia.GetStringAsync($"{Pagina}?vista=fechadas&hist_reparador=Moldin");
        Assert.Contains($"data-dmo-historico=\"{listaId}\"", historico);
        var lista = await chefia.GetStringAsync($"{Pagina}?vista=fechadas&hist_reparador=Moldin&lista={listaId}");
        Assert.Contains("id=\"rpListaFechada\"", lista);
        Assert.Contains("class=\"rp-entrou\"", lista);
        Assert.Matches("Entrada \\d\\d/\\d\\d/\\d{4} · Posição 4152 · Operador 7404", WebUtility.HtmlDecode(lista));

        var impressao = await chefia.GetStringAsync($"/Ferramentas/ReparacaoProgramada/Imprimir?lista={listaId}");
        Assert.Contains("window.print()", impressao);
        Assert.Contains("@media print", impressao);
        Assert.Contains("Reparação Programada · Moldin", WebUtility.HtmlDecode(impressao));
    }
}

using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Armazem;

/// <summary>
/// A página dos Movimentos do Armazém (0.2.0-beta.9): procurar a ferramenta,
/// ver onde está, dar Entrada (com a posição nova criada por «Criar e
/// registar»), Saída (o destino escolhe os campos) e Corrigir localização no
/// «Opções», sempre com o formulário que a página desenha. A Entrada vem com a
/// origem e o estado da última Saída escolhidos; os avisos ficam no cartão
/// da ferramenta; o mesmo envio duas vezes grava uma; as Posições mostram
/// quem lá está.
/// </summary>
public sealed class ArmazemMovimentosPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-pag-cm";
    private const string Bq = "arm-pag-bq";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemMovimentosPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados);
        db.FerramentaUtilizacoes.RemoveRange(db.FerramentaUtilizacoes.Where(u => u.ToolId == Cm || u.ToolId == Bq));
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas);
        db.ArmazemPosicoes.RemoveRange(db.ArmazemPosicoes);
        db.Reparadores.RemoveRange(db.Reparadores);
        // A base em memória é a mesma para todas as classes: tirar também as
        // ferramentas de outras classes com as mesmas referências, senão a procura muda.
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == Bq || t.Reference == "5447T173" || t.Reference == "T173"));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Bq, Type = ToolType.Bq, Reference = "T173", Lot = "01" });
        db.ArmazemPosicoes.Add(new ArmazemPosicao { Codigo = "0042", CriadaEmUtc = DateTime.UtcNow });
        db.Reparadores.Add(new Reparador { Nome = "Moldes Lda" });
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Armazem{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-armmov-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Operador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            db.SaveChanges();
        }

        var client = CreateClient();
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

    /// <summary>Envia o formulário que a página desenha (pelo botão, quando é dado) e devolve a resposta.</summary>
    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string html, string? botao, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        var (acao, campos) = EnvioDoFormulario.Enviar(html, preenchidos, idDoBotao: botao);
        return await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
    }

    /// <summary>Envia e segue o redirect, como o browser; devolve a página a seguir.</summary>
    private static async Task<string> GravarAsync(HttpClient client, string html, string botao, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        using var response = await EnviarAsync(client, html, botao, preenchidos);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await client.GetStringAsync(response.Headers.Location!.OriginalString);
    }

    /// <summary>O elemento que abre com este atributo (id="…" ou data-…="…"), até ao seu fecho. Falha se não existir.</summary>
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

    /// <summary>O texto da opção escolhida de um select da página; null quando é a opção vazia («Escolha…») ou nenhuma.</summary>
    private static string? Escolhida(string html, string idDoSelect)
    {
        var select = Elemento(html, $"id=\"{idDoSelect}\"");
        var opcao = Regex.Match(select, "<option(?<a>[^>]*)selected=\"selected\"(?<b>[^>]*)>(?<t>[^<]*)</option>");
        return opcao.Success && !(opcao.Groups["a"].Value + opcao.Groups["b"].Value).Contains("value=\"\"", StringComparison.Ordinal)
            ? System.Net.WebUtility.HtmlDecode(opcao.Groups["t"].Value)
            : null;
    }

    /// <summary>A etiqueta &lt;input&gt; com este id (um campo não tem fecho). Falha se não existir.</summary>
    private static string Campo(string html, string id)
    {
        var campo = Regex.Match(html, $"<input\\b[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>");
        Assert.True(campo.Success, $"a página não tem o campo #{id}");
        return campo.Value;
    }

    [Fact]
    public async Task SemAChave_NaoEntra()
    {
        using var client = await LoginOperatorAsync("6201", DmoCapabilities.Ferramentas);

        using var response = await client.GetAsync("/Armazem/Movimentos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/Armazem", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Procurar_MostraOndeEstaEAbreAFerramenta()
    {
        using var client = await LoginOperatorAsync("6202", DmoCapabilities.ArmazemGestao);

        var pagina = await client.GetStringAsync("/Armazem/Movimentos?q=5447");
        var lista = await client.GetStringAsync("/Armazem/Movimentos?handler=Procurar&q=t17");

        Assert.Matches("<a href=\"/Armazem/Movimentos\"[^>]*aria-current=\"page\">Movimentos</a>", pagina);
        var linha = Elemento(pagina, $"data-dmo-ferramenta=\"{Cm}\"");
        Assert.Contains("CM 5447T173", linha);
        Assert.Contains("Fabrico", linha);
        Assert.Contains($"/Armazem/Movimentos?ferramenta={Cm}", linha);
        Assert.Contains($"data-dmo-ferramenta=\"{Bq}\"", lista);
        Assert.DoesNotContain("<main", lista);
        Assert.Contains("Nenhuma ferramenta encontrada.", Elemento(await client.GetStringAsync("/Armazem/Movimentos?q=9999"), "id=\"armProcuraVazia\""));
    }

    [Fact]
    public async Task Procurar_MostraOUsoDaUltimaLeitura_ESemLeituraUmTraco()
    {
        // Diogo, 21/10/2026: «posição, referência, lote, estado, máquinas, % uso».
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.AddRange(
                new FerramentaUtilizacao { ToolId = Cm, Percentagem = 40m, RegistadaEmUtc = DateTime.UtcNow.AddDays(-9) },
                new FerramentaUtilizacao { ToolId = Cm, Percentagem = 47.5m, RegistadaEmUtc = DateTime.UtcNow });
            db.SaveChanges();
        }
        using var client = await LoginOperatorAsync("6209", DmoCapabilities.ArmazemGestao);

        var pagina = await client.GetStringAsync("/Armazem/Movimentos?q=t173");

        Assert.Contains("<th>Estado</th><th>Máquinas</th><th>% uso</th>", Elemento(pagina, "id=\"armProcuraResultados\""));
        // A última célula de cada linha é o % de uso.
        string Uso(string linha) => System.Text.RegularExpressions.Regex.Matches(linha, "<td>([^<]*)</td>").Last().Groups[1].Value;
        Assert.Equal("47.5 %", Uso(Elemento(pagina, $"data-dmo-ferramenta=\"{Cm}\"")));
        Assert.Equal("—", Uso(Elemento(pagina, $"data-dmo-ferramenta=\"{Bq}\"")));
    }

    [Fact]
    public async Task Entrada_PeloFormulario_PoeNaPosicaoEPassaASaida()
    {
        using var client = await LoginOperatorAsync("6203", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        // Sem saída anterior não há proposta: a pessoa escolhe.
        Assert.Null(Escolhida(pagina, "armEntOrigem"));
        Assert.Null(Escolhida(pagina, "armEntEstado"));

        var depois = await GravarAsync(client, pagina, "armEntRegistar",
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Já estava no armazém")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Reparado")));

        Assert.Contains("Entrada registada na posição 0042.", Elemento(depois, "id=\"armMovAvisos\""));
        Assert.Contains("Armazém · posição 0042", Elemento(depois, "data-dmo-onde"));
        Assert.Contains("Reparado", Elemento(depois, "data-dmo-estado"));
        Assert.Contains("Entrada na posição 0042 · Já estava no armazém · Reparado", Elemento(depois, "id=\"armUltimos\""));
        Assert.Contains("id=\"armSaiDestino\"", depois);
        Assert.DoesNotContain("id=\"armEntrada\"", depois);
        var movimento = NaBase(db => db.ArmazemMovimentos.AsNoTracking().Single());
        Assert.Equal((Cm, "Operador 6203"), (movimento.ToolId, movimento.RegistadoPor));
    }

    [Fact]
    public async Task Entrada_NumaPosicaoQueNaoExiste_PerguntaECriaComCriarERegistar()
    {
        using var client = await LoginOperatorAsync("6204", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        (string, string)[] campos =
        [
            ("armEntPosicao", "7001"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Nova")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Novo"))
        ];

        using var pergunta = await EnviarAsync(client, pagina, "armEntRegistar", campos);
        var recusada = await pergunta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, pergunta.StatusCode);
        Assert.Contains("A posição 7001 não existe. Criar?", Elemento(recusada, "id=\"armMovAvisos\""));
        // O formulário volta com o que a pessoa escreveu e o botão «Criar e registar».
        Assert.Contains("value=\"7001\"", Campo(recusada, "armEntPosicao"));
        Assert.Equal("Nova", Escolhida(recusada, "armEntOrigem"));
        Assert.Contains("Criar e registar", Elemento(recusada, "id=\"armEntCriar\""));
        Assert.Empty(NaBase(db => db.ArmazemMovimentos.AsNoTracking().ToList()));

        var depois = await GravarAsync(client, recusada, "armEntCriar");

        Assert.Contains("Entrada registada na posição 7001.", Elemento(depois, "id=\"armMovAvisos\""));
        Assert.Equal(1, NaBase(db => db.ArmazemPosicoes.Count(p => p.Codigo == "7001")));
    }

    [Fact]
    public async Task Saida_ODestinoEscolheOsCamposEDepoisAEntradaPropoe()
    {
        using var client = await LoginOperatorAsync("6205", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        pagina = await GravarAsync(client, pagina, "armEntRegistar",
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Nova")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Novo")));
        Assert.DoesNotContain("id=\"armSaida\"", pagina);

        var reparacao = EnvioDoFormulario.Opcao(pagina, "armSaiDestino", "Reparação");
        var comDestino = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}&destino={reparacao}");
        Assert.DoesNotContain("id=\"armSaiFabrica\"", comDestino);
        var depois = await GravarAsync(client, comDestino, "armSaiRegistar",
            ("armSaiReparador", EnvioDoFormulario.Opcao(comDestino, "armSaiReparador", "Moldes Lda")),
            ("armSaiData", DMO.Alpha.Core.Runtime.DmoBusinessTime.Now.AddDays(2).ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Contains("Saída registada.", Elemento(depois, "id=\"armMovAvisos\""));
        Assert.Contains("é no futuro", Elemento(depois, "data-dmo-aviso"));
        Assert.Contains("A reparar · Moldes Lda", Elemento(depois, "data-dmo-onde"));
        // Depois da Reparação, a Entrada vem com a origem e o estado escolhidos (D11).
        Assert.Equal("Reparação", Escolhida(depois, "armEntOrigem"));
        Assert.Equal("Reparado", Escolhida(depois, "armEntEstado"));
    }

    [Fact]
    public async Task Saida_ParaReparacaoSemReparador_RecusaComOFormularioPreenchido()
    {
        using var client = await LoginOperatorAsync("6206", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        pagina = await GravarAsync(client, pagina, "armEntRegistar",
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Nova")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Novo")));
        var comDestino = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}&destino={EnvioDoFormulario.Opcao(pagina, "armSaiDestino", "Reparação")}");

        using var resposta = await EnviarAsync(client, comDestino, "armSaiRegistar", ("armSaiObs", "enviado para LE"));
        var recusada = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Escolha o reparador.", Elemento(recusada, "id=\"armMovAvisos\""));
        Assert.Contains("value=\"enviado para LE\"", Campo(recusada, "armSaiObs"));
        Assert.Equal(1, NaBase(db => db.ArmazemMovimentos.Count()));
    }

    [Fact]
    public async Task OMesmoEnvioDuasVezes_GravaUmaVez()
    {
        using var client = await LoginOperatorAsync("6207", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        (string, string)[] campos =
        [
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Nova")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Novo"))
        ];

        await GravarAsync(client, pagina, "armEntRegistar", campos);
        var segunda = await GravarAsync(client, pagina, "armEntRegistar", campos);

        Assert.Contains("Este movimento já estava registado.", Elemento(segunda, "id=\"armMovAvisos\""));
        Assert.Equal(1, NaBase(db => db.ArmazemMovimentos.Count()));
    }

    [Fact]
    public async Task CorrigirLocalizacao_NoOpcoesTiraDoArmazem()
    {
        using var client = await LoginOperatorAsync("6208", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        pagina = await GravarAsync(client, pagina, "armEntRegistar",
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Nova")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Novo")));
        Assert.Contains("Corrigir localização", Elemento(pagina, "data-dmo-dialog=\"arm-corrigir-dialog\""));

        var depois = await GravarAsync(client, pagina, "armCorGravar", ("armCorObs", "não estava lá"));

        Assert.Contains("Localização corrigida.", Elemento(depois, "id=\"armMovAvisos\""));
        Assert.Contains("Fabrico", Elemento(depois, "data-dmo-onde"));
        Assert.Contains("Corrigida: não está no armazém", Elemento(depois, "id=\"armUltimos\""));
        Assert.Contains("id=\"armEntrada\"", depois);
    }

    [Fact]
    public async Task Posicoes_MostramQuemLaEstaOuLivre()
    {
        using var client = await LoginOperatorAsync("6209", DmoCapabilities.ArmazemGestao);
        NaBase(db =>
        {
            db.ArmazemPosicoes.Add(new ArmazemPosicao { Codigo = "0043", CriadaEmUtc = DateTime.UtcNow });
            return db.SaveChanges();
        });
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Bq}");
        await GravarAsync(client, pagina, "armEntRegistar",
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Nova")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Novo")));

        var posicoes = await client.GetStringAsync("/Armazem/Posicoes");

        var ocupada = Elemento(Elemento(posicoes, "data-dmo-posicao=\"0042\""), "data-dmo-ocupantes");
        Assert.Contains("BQ T173 · lote 01", ocupada);
        Assert.Contains($"/Armazem/Movimentos?ferramenta={Bq}", ocupada);
        Assert.Contains("Livre", Elemento(Elemento(posicoes, "data-dmo-posicao=\"0043\""), "data-dmo-ocupantes"));
    }
}

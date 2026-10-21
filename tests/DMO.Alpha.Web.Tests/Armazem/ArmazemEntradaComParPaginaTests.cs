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
/// A Entrada como no Access (0.2.0-beta.11), pelo formulário da página: a CM
/// mostra o seu MF e pode entrar com ele (a escolha vem desmarcada, é raro; sem ela, entra
/// só a CM), e o campo da posição sugere só as posições livres.
/// </summary>
public sealed class ArmazemEntradaComParPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Cm = "arm-par-cm";
    private const string Mf = "arm-par-mf";
    private const string Outra = "arm-par-outra";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemEntradaComParPaginaTests(CustomWebApplicationFactory factory)
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
        // A base em memória é a mesma para todas as classes: tirar também as
        // ferramentas de outras classes com a mesma referência, senão o par é outro.
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId == Cm || t.ToolId == Mf || t.ToolId == Outra || t.Reference == "5447T173"));
        db.SaveChanges();
        db.Tools.AddRange(
            new Tool { ToolId = Cm, Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Mf, Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = Outra, Type = ToolType.Cm, Reference = "1075C142", Lot = "2" });
        var ocupada = new ArmazemPosicao { Codigo = "0043", CriadaEmUtc = DateTime.UtcNow };
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Codigo = "0042", CriadaEmUtc = DateTime.UtcNow },
            ocupada,
            new ArmazemPosicao { Codigo = "0099", Ativa = false, CriadaEmUtc = DateTime.UtcNow });
        db.SaveChanges();
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = Outra, PosicaoId = ocupada.Id, MovimentoId = 1, DesdeUtc = DateTime.UtcNow });
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
            var template = new Template { TemplateKey = $"tpl-armpar-{operatorId}-{Guid.NewGuid():N}", Name = $"Armazém {operatorId}", Capabilities = capabilities };
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
    public async Task ACm_MostraOMfEAsPosicoesLivres()
    {
        using var client = await LoginOperatorAsync("6401", DmoCapabilities.ArmazemGestao);

        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");

        var par = Elemento(pagina, "data-dmo-armazem-par");
        Assert.Contains($"href=\"/Armazem/Movimentos?ferramenta={Mf}\">MF</a>", par);
        Assert.Contains("fora do armazém", par);
        // Guardar a CM e o MF juntos é raro (Diogo 21/10, AR-02): a escolha vem desmarcada.
        var escolha = Campo(pagina, "armEntPar");
        Assert.DoesNotContain("checked", escolha);
        Assert.Contains("Entrar também MF 5447T173 · lote 3, na mesma posição", System.Net.WebUtility.HtmlDecode(Elemento(pagina, "for=\"armEntPar\"")));
        Assert.Contains("list=\"armEntLivres\"", Campo(pagina, "armEntPosicao"));
        // Sugere só as livres: a 0043 tem uma ferramenta e a 0099 está arquivada.
        Assert.Equal(["0042"], Regex.Matches(Elemento(pagina, "id=\"armEntLivres\""), "value=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray());
    }

    [Fact]
    public async Task ComOParMarcado_ACmEOMfEntramJuntos()
    {
        using var client = await LoginOperatorAsync("6402", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");

        var (acao, campos) = EnvioDoFormulario.Enviar(pagina,
        [
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Reparação")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Reparado"))
        ], idDoBotao: "armEntRegistar");
        // A pessoa marca «Entrar também o MF».
        using var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo([.. campos, ("com_par", "1")]));
        var depois = await client.GetStringAsync(resposta.Headers.Location!.OriginalString);

        Assert.Contains("Entrada da CM e do MF registada na posição 0042.", Elemento(depois, "id=\"armMovAvisos\""));
        Assert.Contains("posição 0042", Elemento(depois, "data-dmo-armazem-par"));
        var doMf = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Mf}");
        Assert.Contains("Armazém · posição 0042", Elemento(doMf, "data-dmo-onde"));
        Assert.Contains("Reparado", Elemento(doMf, "data-dmo-estado"));
        Assert.Contains("Entrada na posição 0042 · Reparação · Reparado", Elemento(doMf, "id=\"armUltimos\""));
        Assert.Equal([Cm, Mf], NaBase(db => db.ArmazemMovimentos.AsNoTracking().OrderBy(m => m.Id).Select(m => m.ToolId).ToArray()));
    }

    [Fact]
    public async Task ComOParDesmarcado_EntraSoACm()
    {
        using var client = await LoginOperatorAsync("6403", DmoCapabilities.ArmazemGestao);
        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Cm}");
        var (acao, campos) = EnvioDoFormulario.Enviar(pagina,
        [
            ("armEntPosicao", "0042"),
            ("armEntOrigem", EnvioDoFormulario.Opcao(pagina, "armEntOrigem", "Reparação")),
            ("armEntEstado", EnvioDoFormulario.Opcao(pagina, "armEntEstado", "Reparado"))
        ], idDoBotao: "armEntRegistar");
        Assert.DoesNotContain(campos, c => c.Nome == "com_par");

        using var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        var depois = await client.GetStringAsync(resposta.Headers.Location!.OriginalString);

        Assert.Contains("Entrada registada na posição 0042.", Elemento(depois, "id=\"armMovAvisos\""));
        Assert.Contains("fora do armazém", Elemento(depois, "data-dmo-armazem-par"));
        Assert.Equal([Cm], NaBase(db => db.ArmazemMovimentos.AsNoTracking().Select(m => m.ToolId).ToArray()));
    }

    [Fact]
    public async Task UmaFerramentaSemPar_NaoMostraAEscolha()
    {
        using var client = await LoginOperatorAsync("6404", DmoCapabilities.ArmazemGestao);
        NaBase(db =>
        {
            db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
            return db.SaveChanges();
        });

        var pagina = await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={Outra}");

        Assert.DoesNotContain("id=\"armEntPar\"", pagina);
        Assert.DoesNotContain("data-dmo-armazem-par", pagina);
    }
}

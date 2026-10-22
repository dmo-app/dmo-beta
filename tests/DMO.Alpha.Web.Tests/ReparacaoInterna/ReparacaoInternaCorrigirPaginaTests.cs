using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Presentation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.ReparacaoInterna;

/// <summary>
/// Corrigir e anular na página da Reparação Interna (peça 2): na tabela não
/// há botões; o número abre a janela de corrigir e o «×» a de anular, uma de
/// cada, fora das zonas, que o ri-corrigir.js enche com a linha. Quem tem a
/// chave ri e não registou também corrige e anula, e o rasto diz quem. O
/// mesmo envio duas vezes aplica-se uma; depois de anular, a linha sai. A
/// base em memória é partilhada entre classes: cada teste usa as suas
/// máquinas, e o construtor limpa só as desta classe.
/// </summary>
public sealed class ReparacaoInternaCorrigirPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RICOR";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoInternaCorrigirPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var maquinas = db.Machines.Where(m => m.Code.StartsWith(Marca)).Select(m => m.Id).ToList();
        var jobOns = db.JobOns.Where(j => maquinas.Contains(j.MachineId)).Select(j => j.Id).ToList();
        var reparacoes = db.ReparacoesInternas.Where(r => maquinas.Contains(r.MachineId)).ToList();
        var ids = reparacoes.Select(r => r.Id).ToList();
        var resultados = ids.Select(id => (int?)id).ToList();
        db.ReparacoesInternasRasto.RemoveRange(db.ReparacoesInternasRasto.Where(r => ids.Contains(r.ReparacaoId)));
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas.Where(o => o.ResultadoTipo == "Reparacao" && resultados.Contains(o.ResultadoId)));
        db.ReparacoesInternas.RemoveRange(reparacoes);
        db.CmContexts.RemoveRange(db.CmContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.MfContexts.RemoveRange(db.MfContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.JobOns.RemoveRange(db.JobOns.Where(j => jobOns.Contains(j.Id)));
        db.Machines.RemoveRange(db.Machines.Where(m => maquinas.Contains(m.Id)));
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId.StartsWith(Marca)));
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Corrigir{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-ricor-{operatorId}-{Guid.NewGuid():N}", Name = $"RI {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Reparador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            return db.SaveChanges();
        });
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

    private Guid Utilizador(string operatorId) => NaBase(db => db.Users.AsNoTracking().Single(u => u.OperatorId == operatorId).Id);

    /// <summary>Uma máquina desta classe com uma produção que cobre hoje e ontem, com a CM e o MF.</summary>
    private (string Codigo, int JobOn) Maquina(string sufixo) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var maquina = new Machine { Code = codigo };
        var referencia = $"77{sufixo}T324";
        db.Machines.Add(maquina);
        db.Tools.AddRange(
            new Tool { ToolId = $"{codigo}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "1" },
            new Tool { ToolId = $"{codigo}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "1" });
        var jobOn = new JobOn { Machine = maquina, Reference = referencia, ProductionNumber = $"{sufixo}01", InicioEm = hoje.AddDays(-2), FimEm = hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        db.SaveChanges();
        db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm" });
        db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf" });
        db.SaveChanges();
        return (codigo, jobOn.Id);
    });

    /// <summary>Regista os números pela página (Enter no campo) e devolve os ids, pela ordem.</summary>
    private async Task<int[]> RegistarAsync(HttpClient client, string codigo, int jobOn, params string[] numeros)
    {
        foreach (var numero in numeros)
        {
            var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
            var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", numero));
            using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
            Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        }

        return NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOn).OrderBy(r => r.Id).Select(r => r.Id).ToArray());
    }

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

    private static string Atributo(string elemento, string nome) =>
        WebUtility.HtmlDecode(Regex.Match(elemento, $"{nome}=\"([^\"]*)\"").Groups[1].Value);

    /// <summary>
    /// O que o ri-corrigir.js faz ao clicar no número ou no «×» da linha:
    /// enche a janela com a reparação e o número da linha, a máquina, a
    /// produção e a página da tabela e um envio novo; a pessoa escreve o
    /// resto e envia.
    /// </summary>
    private static (string Acao, IReadOnlyList<(string Nome, string Valor)> Campos) Janela(
        string html, int reparacaoId, string prefixo, Guid envio, params (string IdDoCampo, string Valor)[] escritos)
    {
        // A tabela do cartão onde está a linha (cada máquina tem a sua).
        var linhaEm = html.IndexOf($"data-dmo-reparacao=\"{reparacaoId}\"", StringComparison.Ordinal);
        var tabela = Elemento(html[html.LastIndexOf("<table", linhaEm, StringComparison.Ordinal)..], "data-ri-maquina");
        var ligacao = Regex.Match(Elemento(html, $"data-dmo-reparacao=\"{reparacaoId}\""), $"<a[^>]*data-ri-abre=\"{prefixo}Janela\"[^>]*>").Value;
        Assert.NotEmpty(ligacao);
        var preenchidos = new List<(string, string)>
        {
            ($"{prefixo}Operacao", envio.ToString()),
            ($"{prefixo}Id", Atributo(ligacao, "data-ri-reparacao")),
            ($"{prefixo}Maquina", Atributo(tabela, "data-ri-maquina")),
            ($"{prefixo}Producao", Atributo(tabela, "data-ri-producao")),
            ($"{prefixo}Tipo", Atributo(tabela, "data-ri-tipo")),
            ($"{prefixo}Pagina", Atributo(tabela, "data-ri-pagina"))
        };
        if (prefixo == "riCorrigir")
        {
            preenchidos.Add(("riCorrigirNumero", Atributo(ligacao, "data-ri-numero")));
        }

        foreach (var escrito in escritos)
        {
            preenchidos.RemoveAll(p => p.Item1 == escrito.IdDoCampo);
            preenchidos.Add(escrito);
        }

        return EnvioDoFormulario.Preencher(html, [.. preenchidos]);
    }

    [Fact]
    public async Task ATabela_SemBotoes_ONumeroAbreACorrecao_EOXisAAnulacao()
    {
        var (codigo, jobOn) = Maquina("A");
        using var client = await LoginOperatorAsync("6701", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "0457");

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        var linha = Elemento(html, $"data-dmo-reparacao=\"{ids[0]}\"");
        Assert.DoesNotContain("<button", linha);
        Assert.DoesNotContain("<button", Elemento(html, $"id=\"riLista-{codigo}\""));
        // Clicar no número corrige; o «×» anula.
        Assert.Matches($"<a href=\"#riCorrigirJanela\" class=\"ri-valor\"[^>]*data-ri-reparacao=\"{ids[0]}\" data-ri-numero=\"0457\"[^>]*><strong>0457</strong></a>", linha);
        Assert.Matches($"<a href=\"#riAnularJanela\" class=\"ri-x\"[^>]*data-ri-reparacao=\"{ids[0]}\"[^>]*>×</a>", linha);
        // As janelas: fora das zonas, com o motivo opcional e os botões em baixo.
        foreach (var janela in new[] { "riCorrigirJanela", "riAnularJanela" })
        {
            var dialog = Elemento(html, $"id=\"{janela}\"");
            Assert.Contains("class=\"dmo-dialog ri-janela\"", dialog);
            Assert.Contains("Motivo (opcional)", dialog);
            Assert.DoesNotMatch("id=\"ri(Corrigir|Anular)Motivo\"[^>]*required", dialog);
            Assert.Matches(new Regex("<div class=\"dmo-dialog-actions\">(?:(?!<div).)*</div>\\s*(?:<input name=\"__RequestVerificationToken\"[^>]*>\\s*)?</form>\\s*</dialog>$", RegexOptions.Singleline), dialog);
            foreach (var zona in new[] { "riAvisos", $"riCard-{codigo}" })
            {
                Assert.DoesNotContain($"id=\"{janela}\"", Elemento(html, $"id=\"{zona}\""));
            }
        }

        Assert.Contains("/js/ri-corrigir.js", html);
    }

    [Fact]
    public async Task OutraPessoaComOModulo_CorrigeONumero_ComRasto_EOMesmoEnvioAplicaUmaVez()
    {
        var (codigo, jobOn) = Maquina("B");
        using var autor = await LoginOperatorAsync("6702", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(autor, codigo, jobOn, "0457", "88");
        using var outra = await LoginOperatorAsync("6703", DmoCapabilities.ReparacaoInterna);
        var html = await outra.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var envio = Guid.NewGuid();
        var (acao, campos) = Janela(html, ids[0], "riCorrigir", envio, ("riCorrigirNumero", " 0475 "), ("riCorrigirMotivo", "trocou os algarismos"));

        using var corrigir = await outra.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        using var outraVez = await outra.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, corrigir.StatusCode);
        var destino = corrigir.Headers.Location!.OriginalString;
        Assert.Contains($"maquina={codigo}", destino);
        Assert.Contains("tipo=cm", destino);
        var depois = await outra.GetStringAsync(destino);
        Assert.Contains("Número corrigido para 0475.", Elemento(depois, "id=\"riAvisos\""));
        Assert.Contains("data-dmo-toast=\"ok\"", Elemento(depois, "id=\"riAvisos\""));
        var linha = Elemento(depois, $"data-dmo-reparacao=\"{ids[0]}\"");
        Assert.Contains("<strong>0475</strong>", linha);
        // Quem registou continua a ser o autor; o rasto diz quem corrigiu.
        Assert.Contains("Reparador 6702", linha);
        Assert.Contains("<strong>88</strong>", Elemento(depois, $"data-dmo-reparacao=\"{ids[1]}\""));
        Assert.Equal(HttpStatusCode.Redirect, outraVez.StatusCode);
        var rasto = Assert.Single(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(r => ids.Contains(r.ReparacaoId)).ToList()));
        Assert.Equal((ReparacaoAcao.Corrigido, "0457", "0475", (Guid?)Utilizador("6703"), "trocou os algarismos"),
            (rasto.Acao, rasto.NumeroAntes, rasto.NumeroDepois, rasto.PorUserId, rasto.Motivo));
    }

    [Fact]
    public async Task OutraPessoaComOModulo_Anula_ALinhaSai_EFicaNaBase()
    {
        var (codigo, jobOn) = Maquina("C");
        using var autor = await LoginOperatorAsync("6704", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(autor, codigo, jobOn, "1", "2", "3");
        using var outra = await LoginOperatorAsync("6705", DmoCapabilities.ReparacaoInterna);
        var html = await outra.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = Janela(html, ids[1], "riAnular", Guid.NewGuid(), ("riAnularMotivo", "máquina errada"));

        using var anular = await outra.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, anular.StatusCode);
        var depois = await outra.GetStringAsync(anular.Headers.Location!.OriginalString);
        Assert.Contains("Reparação do nº 2 anulada.", Elemento(depois, "id=\"riAvisos\""));
        Assert.DoesNotContain($"data-dmo-reparacao=\"{ids[1]}\"", depois);
        Assert.Contains($"data-dmo-reparacao=\"{ids[0]}\"", depois);
        Assert.Contains($"data-dmo-reparacao=\"{ids[2]}\"", depois);
        var anulada = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[1]));
        Assert.Equal("2", anulada.Numero);
        Assert.NotNull(anulada.AnuladaEmUtc);
        var rasto = Assert.Single(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(r => ids.Contains(r.ReparacaoId)).ToList()));
        Assert.Equal((ids[1], ReparacaoAcao.Anulado, "2", (string?)null, (Guid?)Utilizador("6705"), "máquina errada"),
            (rasto.ReparacaoId, rasto.Acao, rasto.NumeroAntes, rasto.NumeroDepois, rasto.PorUserId, rasto.Motivo));
    }

    [Fact]
    public async Task AnuladaEntretanto_CorrigirOuAnularAvisa_ENadaMuda()
    {
        var (codigo, jobOn) = Maquina("D");
        using var client = await LoginOperatorAsync("6706", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "5");
        // As duas janelas abertas na mesma página; outra pessoa anula primeiro.
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acaoAnular, camposAnular) = Janela(html, ids[0], "riAnular", Guid.NewGuid());
        var (acaoCorrigir, camposCorrigir) = Janela(html, ids[0], "riCorrigir", Guid.NewGuid(), ("riCorrigirNumero", "6"));
        using (var primeira = await client.PostAsync(acaoAnular, EnvioDoFormulario.Corpo(camposAnular)))
        {
            Assert.Equal(HttpStatusCode.Redirect, primeira.StatusCode);
        }

        using var corrigir = await client.PostAsync(acaoCorrigir, EnvioDoFormulario.Corpo(camposCorrigir));
        var (acaoOutra, camposOutra) = Janela(html, ids[0], "riAnular", Guid.NewGuid());
        using var anularOutraVez = await client.PostAsync(acaoOutra, EnvioDoFormulario.Corpo(camposOutra));

        foreach (var resposta in new[] { corrigir, anularOutraVez })
        {
            var depois = await client.GetStringAsync(resposta.Headers.Location!.OriginalString);
            Assert.Contains("A reparação do nº 5 já tinha sido anulada. Nada mudou.", Elemento(depois, "id=\"riAvisos\""));
            Assert.Contains("data-dmo-toast=\"erro\"", Elemento(depois, "id=\"riAvisos\""));
        }

        Assert.Equal("5", NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[0]).Numero));
        Assert.Single(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(r => r.ReparacaoId == ids[0]).ToList()));
    }

    [Fact]
    public async Task UmNumeroLongo_Recusa_ENadaMuda()
    {
        var (codigo, jobOn) = Maquina("E");
        using var client = await LoginOperatorAsync("6707", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "9");
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = Janela(html, ids[0], "riCorrigir", Guid.NewGuid(), ("riCorrigirNumero", new string('9', Reparacao.NumeroMaxLength + 1)));

        using var corrigir = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        var depois = await client.GetStringAsync(corrigir.Headers.Location!.OriginalString);
        Assert.Contains($"O número tem até {Reparacao.NumeroMaxLength} caracteres. Nada mudou.", Elemento(depois, "id=\"riAvisos\""));
        Assert.Equal("9", NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[0]).Numero));
    }

    [Fact]
    public async Task SemAChave_NaoCorrigeNemAnula()
    {
        var (codigo, jobOn) = Maquina("F");
        using var autor = await LoginOperatorAsync("6708", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(autor, codigo, jobOn, "4");
        var html = await autor.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = Janela(html, ids[0], "riAnular", Guid.NewGuid());
        using var semChave = await LoginOperatorAsync("6709", DmoCapabilities.Ferramentas);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await semChave.GetStringAsync("/Ferramentas"))!;

        using var anular = await semChave.PostAsync(acao, EnvioDoFormulario.Corpo(
            [.. campos.Where(c => c.Nome != "__RequestVerificationToken"), ("__RequestVerificationToken", token)]));

        Assert.NotEqual(HttpStatusCode.OK, anular.StatusCode);
        Assert.DoesNotContain("/ReparacaoInterna", anular.Headers.Location?.ToString() ?? string.Empty);
        Assert.Null(NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[0]).AnuladaEmUtc));
    }
}

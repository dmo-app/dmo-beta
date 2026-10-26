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
/// cada, fora das zonas, que o ri-corrigir.js enche com a linha. Só quem
/// registou e a chefia (a Consulta, ri.view) corrigem e anulam (DS-PERM-01,
/// Diogo 26/10/2026); o rasto diz quem. O
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
    /// O que o ri-corrigir.js faz com o duplo clique na linha (ou «Editar»):
    /// enche a janela da reparação com a reparação e o número da linha, a
    /// linha, a produção, o dia e a página da tabela e um envio novo; a
    /// pessoa escreve o resto e carrega Guardar (riCorrigir) ou Anular
    /// (riAnular, o botão com o formaction). Os campos escritos podem vir
    /// com o prefixo riAnular (o motivo): é a mesma janela.
    /// </summary>
    private static (string Acao, IReadOnlyList<(string Nome, string Valor)> Campos) Janela(
        string html, int reparacaoId, string prefixo, Guid envio, params (string IdDoCampo, string Valor)[] escritos)
    {
        var linhaEm = html.IndexOf($"data-dmo-reparacao=\"{reparacaoId}\"", StringComparison.Ordinal);
        Assert.True(linhaEm >= 0, $"a página não tem a reparação {reparacaoId}");
        var tabela = Elemento(html[html.LastIndexOf("<table", linhaEm, StringComparison.Ordinal)..], "data-ri-maquina");
        var linha = Regex.Match(Elemento(html, $"data-dmo-reparacao=\"{reparacaoId}\""), "<tr[^>]*>").Value;
        var preenchidos = new List<(string, string)>
        {
            ("riCorrigirOperacao", envio.ToString()),
            ("riCorrigirId", Atributo(linha, "data-ri-reparacao")),
            ("riCorrigirMaquina", Atributo(tabela, "data-ri-maquina")),
            ("riCorrigirProducao", Atributo(tabela, "data-ri-producao")),
            ("riCorrigirTipo", Atributo(tabela, "data-ri-tipo")),
            ("riCorrigirDia", Atributo(tabela, "data-ri-dia")),
            ("riCorrigirPagina", Atributo(tabela, "data-ri-pagina")),
            ("riCorrigirNumero", Atributo(linha, "data-ri-numero"))
        };
        foreach (var (id, valor) in escritos)
        {
            var campo = id.Replace("riAnular", "riCorrigir", StringComparison.Ordinal);
            preenchidos.RemoveAll(p => p.Item1 == campo);
            preenchidos.Add((campo, valor));
        }

        return EnvioDoFormulario.Enviar(html, [.. preenchidos], idDoBotao: prefixo == "riAnular" ? "riAnularBotao" : "riGuardarBotao");
    }

    /// <summary>O mesmo envio feito por outra pessoa (o token dela; RI-04: quem tem o módulo pode corrigir).</summary>
    private static IReadOnlyList<(string Nome, string Valor)> ComTokenDe(IReadOnlyList<(string Nome, string Valor)> campos, string htmlDoOutro) =>
        [.. campos.Where(c => c.Nome != "__RequestVerificationToken"), ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(htmlDoOutro)!)];

    [Fact]
    public async Task ATabela_SemBotoes_ODuploCliqueAbreAReparacao_ComCorrigirEAnular()
    {
        var (codigo, jobOn) = Maquina("A");
        using var client = await LoginOperatorAsync("6701", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "457");

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        var linha = Elemento(html, $"data-dmo-reparacao=\"{ids[0]}\"");
        Assert.DoesNotContain("<button", linha);
        Assert.DoesNotContain("<a ", linha);
        Assert.DoesNotContain("<button", Elemento(html, $"id=\"riLista-{codigo}\""));
        // Um clique escolhe, o duplo clique abre (a lista tem ações fora, data-dmo-for).
        Assert.Matches($"<tr data-dmo-row data-id=\"{ids[0]}\" data-dmo-reparacao=\"{ids[0]}\" data-ri-reparacao=\"{ids[0]}\" data-ri-numero=\"457\"", linha);
        // «Editar» no canto do cartão abre a linha escolhida.
        Assert.Contains($"data-ri-editar=\"riLog-{codigo}\"", Elemento(html, $"data-dmo-for=\"riLog-{codigo}\""));
        // A janela: uma, fora das zonas, com o motivo opcional e as opções em baixo.
        var dialog = Elemento(html, "id=\"riCorrigirJanela\"");
        Assert.Contains("class=\"dmo-dialog ri-janela\"", dialog);
        Assert.Contains("Motivo (opcional)", dialog);
        Assert.DoesNotMatch("id=\"riCorrigirMotivo\"[^>]*required", dialog);
        Assert.Matches("id=\"riAnularBotao\" formaction=\"/ReparacaoInterna\\?handler=Anular\" formnovalidate", dialog);
        Assert.Matches(new Regex("<div class=\"dmo-dialog-actions\">(?:(?!<div).)*</div>\\s*(?:<input name=\"__RequestVerificationToken\"[^>]*>\\s*)?</form>\\s*</dialog>$", RegexOptions.Singleline), dialog);
        Assert.DoesNotContain("id=\"riAnularJanela\"", html);
        foreach (var zona in new[] { "riAvisos", "riRegisto", "riLinhas" })
        {
            Assert.DoesNotContain("id=\"riCorrigirJanela\"", Elemento(html, $"id=\"{zona}\""));
        }

        Assert.Contains("/js/ri-corrigir.js", html);
    }

    [Fact]
    public async Task AChefia_CorrigeONumeroDeOutraPessoa_ComRasto_EOMesmoEnvioAplicaUmaVez()
    {
        var (codigo, jobOn) = Maquina("B");
        using var autor = await LoginOperatorAsync("6702", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(autor, codigo, jobOn, "457", "88");
        using var outra = await LoginOperatorAsync("6703", DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView);
        // O registo de cada um é dele: a reparação vem da página do autor,
        // e a chefia (a Consulta) corrige-a (DS-PERM-01).
        var html = await autor.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var envio = Guid.NewGuid();
        var (acao, doAutor) = Janela(html, ids[0], "riCorrigir", envio, ("riCorrigirNumero", " 475 "), ("riCorrigirMotivo", "trocou os algarismos"));
        var campos = ComTokenDe(doAutor, await outra.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm"));

        using var corrigir = await outra.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        using var outraVez = await outra.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, corrigir.StatusCode);
        var destino = corrigir.Headers.Location!.OriginalString;
        Assert.Contains($"maquina={codigo}", destino);
        Assert.Contains("tipo=cm", destino);
        var depois = await outra.GetStringAsync(destino);
        Assert.Contains("Reparação do nº 475 corrigida.", Elemento(depois, "id=\"riAvisos\""));
        Assert.Contains("data-dmo-toast=\"ok\"", Elemento(depois, "id=\"riAvisos\""));
        // Quem registou continua a ser o autor (está no registo dele); o rasto diz quem corrigiu.
        Assert.DoesNotContain($"data-dmo-reparacao=\"{ids[0]}\"", depois);
        var doAutorDepois = await autor.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        Assert.Contains("<strong>475</strong>", Elemento(doAutorDepois, $"data-dmo-reparacao=\"{ids[0]}\""));
        Assert.Contains("<strong>88</strong>", Elemento(doAutorDepois, $"data-dmo-reparacao=\"{ids[1]}\""));
        Assert.Equal((Guid?)Utilizador("6702"), NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[0]).RegistadoPorUserId));
        Assert.Equal(HttpStatusCode.Redirect, outraVez.StatusCode);
        var rasto = Assert.Single(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(r => ids.Contains(r.ReparacaoId)).ToList()));
        Assert.Equal((ReparacaoAcao.Corrigido, "457", "475", (Guid?)Utilizador("6703"), "trocou os algarismos"),
            (rasto.Acao, rasto.NumeroAntes, rasto.NumeroDepois, rasto.PorUserId, rasto.Motivo));
    }

    [Fact]
    public async Task AChefia_AnulaADeOutraPessoa_ALinhaSai_EFicaNaBase()
    {
        var (codigo, jobOn) = Maquina("C");
        using var autor = await LoginOperatorAsync("6704", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(autor, codigo, jobOn, "1", "2", "3");
        using var outra = await LoginOperatorAsync("6705", DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView);
        var html = await autor.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, doAutor) = Janela(html, ids[1], "riAnular", Guid.NewGuid(), ("riAnularMotivo", "máquina errada"));
        var campos = ComTokenDe(doAutor, await outra.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm"));

        using var anular = await outra.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, anular.StatusCode);
        Assert.Contains("Reparação do nº 2 anulada.", Elemento(await outra.GetStringAsync(anular.Headers.Location!.OriginalString), "id=\"riAvisos\""));
        var depois = await autor.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
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
    public async Task OutroOperadorSemAChefia_NaoCorrigeNemAnula_ENadaMuda()
    {
        var (codigo, jobOn) = Maquina("Q");
        using var autor = await LoginOperatorAsync("6713", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(autor, codigo, jobOn, "31");
        using var outro = await LoginOperatorAsync("6714", DmoCapabilities.ReparacaoInterna);
        var html = await autor.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var paginaDoOutro = await outro.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        // Na página do outro a reparação nem aparece: só vê as suas.
        Assert.DoesNotContain($"data-dmo-reparacao=\"{ids[0]}\"", paginaDoOutro);
        var (corrigirAcao, corrigirCampos) = Janela(html, ids[0], "riCorrigir", Guid.NewGuid(), ("riCorrigirNumero", "13"));
        var (anularAcao, anularCampos) = Janela(html, ids[0], "riAnular", Guid.NewGuid());

        using var corrigir = await outro.PostAsync(corrigirAcao, EnvioDoFormulario.Corpo(ComTokenDe(corrigirCampos, paginaDoOutro)));
        using var anular = await outro.PostAsync(anularAcao, EnvioDoFormulario.Corpo(ComTokenDe(anularCampos, paginaDoOutro)));

        Assert.Equal(HttpStatusCode.Redirect, corrigir.StatusCode);
        Assert.Contains("A reparação do nº 31 é de outra pessoa: só quem a registou e a chefia a corrigem ou anulam. Nada mudou.",
            Elemento(await outro.GetStringAsync(corrigir.Headers.Location!.OriginalString), "id=\"riAvisos\""));
        Assert.Equal(HttpStatusCode.Redirect, anular.StatusCode);
        var reparacao = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[0]));
        Assert.Equal("31", reparacao.Numero);
        Assert.Null(reparacao.AnuladaEmUtc);
        Assert.Empty(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(r => r.ReparacaoId == ids[0]).ToList()));
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
        // Agora recusado já pela regra dos algarismos (até 3), antes do limite da base.
        Assert.Contains("O número tem só algarismos, até 3. Nada mudou.", Elemento(depois, "id=\"riAvisos\""));
        Assert.Equal("9", NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[0]).Numero));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("A1")]
    public async Task CorrigirComNumeroQueNaoESoAlgarismosAte3_Recusa_ENadaMuda(string escrito)
    {
        // Diogo, 24/10/2026: o Corrigir aceita o mesmo que o registo.
        var (codigo, jobOn) = Maquina(escrito == "A1" ? "G" : "H");
        using var client = await LoginOperatorAsync(escrito == "A1" ? "6711" : "6712", DmoCapabilities.ReparacaoInterna);
        var ids = await RegistarAsync(client, codigo, jobOn, "9");
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = Janela(html, ids[0], "riCorrigir", Guid.NewGuid(), ("riCorrigirNumero", escrito));

        using var corrigir = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        var depois = await client.GetStringAsync(corrigir.Headers.Location!.OriginalString);
        Assert.Contains("O número tem só algarismos, até 3. Nada mudou.", Elemento(depois, "id=\"riAvisos\""));
        Assert.Equal("9", NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Id == ids[0]).Numero));
        Assert.Empty(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(r => r.ReparacaoId == ids[0]).ToList()));
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

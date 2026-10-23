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
/// O lote novo do Armazém (Diogo, 22/10/2026: «chega CM 5447 Lote 13 ... nao
/// existe o registo no armazem. o operador regista com esses dados aparece um
/// notice quem tem modulo ferramentas»). O Registo guarda o lote «por criar»
/// sem criar a ferramenta; o Sumário do Planeamento avisa só quem tem o
/// módulo Ferramentas; o aviso abre o formulário das Ferramentas preenchido
/// e, ao registar (ou ao ligar à que já existe), a entrada fica na posição
/// guardada e o aviso desaparece.
/// </summary>
public sealed class ArmazemLoteNovoPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Referencia = "8813LN";

    private readonly CustomWebApplicationFactory _factory;

    public ArmazemLoteNovoPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ArmazemLotesNovos.RemoveRange(db.ArmazemLotesNovos);
        db.FerramentaEstados.RemoveRange(db.FerramentaEstados);
        db.FerramentaLocalizacoes.RemoveRange(db.FerramentaLocalizacoes);
        db.ArmazemOcupacoes.RemoveRange(db.ArmazemOcupacoes);
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas);
        db.Tools.RemoveRange(db.Tools.Where(t => t.Reference == Referencia));
        db.SaveChanges();
    }

    private async Task<HttpClient> LoginAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"LoteNovo{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var template = new Template { TemplateKey = $"tpl-lotenovo-{operatorId}-{Guid.NewGuid():N}", Name = $"Lote novo {operatorId}", Capabilities = capabilities };
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

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string html, string botao, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        var (acao, campos) = EnvioDoFormulario.Enviar(html, preenchidos, idDoBotao: botao);
        return await client.PostAsync(WebUtility.HtmlDecode(acao), EnvioDoFormulario.Corpo(campos));
    }

    /// <summary>O Armazém regista CM <see cref="Referencia"/> lote 13 na posição 1033 pelo formulário da página.</summary>
    private async Task<string> RegistarLoteNovoAsync(HttpClient armazem)
    {
        var vazia = await armazem.GetStringAsync($"/Armazem/Movimentos?tipo=CM&q={Referencia}");
        Assert.Contains("id=\"armProcuraVazia\"", vazia);
        Assert.Contains("id=\"armLoteNovo\"", vazia);
        // A referência procurada já vem escrita; o tipo é o do cartão.
        Assert.Matches($"<input id=\"armLoteRef\"[^>]*value=\"{Referencia}\"", vazia);
        using var response = await EnviarAsync(armazem, vazia, "armLoteNovoRegistar", ("armLoteLote", "13"), ("armLotePosicao", "1033"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await armazem.GetStringAsync(response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Registar_GuardaPorCriar_SemCriarFerramenta_EMostraNoLog()
    {
        using var armazem = await LoginAsync("7301", DmoCapabilities.ArmazemGestao);

        var depois = await RegistarLoteNovoAsync(armazem);

        Assert.Contains("Lote novo registado para a posição 1033.", depois);
        Assert.Empty(NaBase(db => db.Tools.Where(t => t.Reference == Referencia).ToList()));
        var registo = Assert.Single(NaBase(db => db.ArmazemLotesNovos.ToList()));
        Assert.Equal((ToolType.Cm, Referencia, "13", "1033", ArmazemLoteNovoEstado.PorCriar), (registo.Tipo, registo.Referencia, registo.Lote, registo.Posicao, registo.Estado));
        var linha = Regex.Match(depois, $"<tr[^>]*data-dmo-lote-novo=\"{registo.Id}\"[\\s\\S]*?</tr>").Value;
        Assert.Contains("Por criar", linha);
        Assert.Contains("1033", linha);
        Assert.DoesNotContain("<button", linha);
        // No log da MF não aparece.
        Assert.DoesNotContain("data-dmo-lote-novo", await armazem.GetStringAsync("/Armazem/Movimentos?tipo=MF"));
    }

    [Fact]
    public async Task Registar_SoRecusaAPosicaoSemQuatroAlgarismosEOQueFalta()
    {
        using var armazem = await LoginAsync("7302", DmoCapabilities.ArmazemGestao);
        var vazia = await armazem.GetStringAsync($"/Armazem/Movimentos?tipo=CM&q={Referencia}");

        using var recusa = await EnviarAsync(armazem, vazia, "armLoteNovoRegistar", ("armLoteLote", "13"), ("armLotePosicao", "103"));
        var html = await recusa.Content.ReadAsStringAsync();
        using var semLote = await EnviarAsync(armazem, vazia, "armLoteNovoRegistar", ("armLotePosicao", "1033"));

        Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
        Assert.Contains("Escreva a posição com os 4 algarismos", html);
        // O que a pessoa escreveu volta no formulário.
        Assert.Matches("<input id=\"armLoteLote\"[^>]*value=\"13\"", html);
        Assert.Contains("Escreva o lote.", await semLote.Content.ReadAsStringAsync());
        Assert.Empty(NaBase(db => db.ArmazemLotesNovos.ToList()));
    }

    [Fact]
    public async Task Sumario_AvisaSoQuemTemFerramentas_ECriarResolveEDaAEntrada()
    {
        using var armazem = await LoginAsync("7303", DmoCapabilities.ArmazemGestao);
        await RegistarLoteNovoAsync(armazem);
        var id = NaBase(db => db.ArmazemLotesNovos.Single().Id);
        using var semFerramentas = await LoginAsync("7304", DmoCapabilities.JobOnCreate);
        using var ferramentas = await LoginAsync("7305", DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas);

        Assert.DoesNotContain("Ferramenta nova registada no Armazém", await semFerramentas.GetStringAsync("/Planeamento/Sumario"));
        var sumario = await ferramentas.GetStringAsync("/Planeamento/Sumario");
        // Uma linha do grupo «Importante» do Sumário (Diogo, 23/10/2026), já não um aviso à parte.
        var aviso = Regex.Match(sumario, $"<li [^>]*data-dmo-lote-novo=\"{id}\"[^>]*>[\\s\\S]*?</li>").Value;
        Assert.Contains($">CM {Referencia} · Lote 13</a>", aviso);
        Assert.Contains("Ferramenta nova registada no Armazém, posição 1033", aviso);
        var href = WebUtility.HtmlDecode(Regex.Match(aviso, "href=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal($"/Ferramentas?criar=1&type=CM&reference={Referencia}&lot=13&lote_novo={id}", href);

        var criar = await ferramentas.GetStringAsync(href);
        Assert.Contains("id=\"ferrLoteNovo\"", criar);
        Assert.Contains($"<input type=\"hidden\" name=\"lote_novo\" value=\"{id}\" />", criar);
        Assert.DoesNotContain("id=\"ferrLoteNovoLigar\"", criar);
        var (acao, campos) = EnvioDoFormulario.Enviar(criar, [("ferr-new-lot", "13")]);
        Assert.Contains(campos, c => c is ("reference", Referencia));
        Assert.Contains(campos, c => c is ("lot", "13"));
        using var gravou = await ferramentas.PostAsync(WebUtility.HtmlDecode(acao), EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, gravou.StatusCode);
        var ficha = await ferramentas.GetStringAsync(gravou.Headers.Location!.OriginalString);

        Assert.Contains("a entrada ficou registada na posição 1033", ficha);
        var tool = NaBase(db => db.Tools.Single(t => t.Reference == Referencia));
        var movimento = NaBase(db => db.ArmazemMovimentos.Single(m => m.ToolId == tool.ToolId));
        Assert.Equal((ArmazemMovimentoTipo.Entrada, ArmazemOrigem.Nova), (movimento.Tipo, movimento.Origem));
        var registo = NaBase(db => db.ArmazemLotesNovos.Single());
        Assert.Equal((ArmazemLoteNovoEstado.Resolvido, tool.ToolId), (registo.Estado, registo.ResolvedToolId));
        Assert.DoesNotContain("Ferramenta nova registada no Armazém", await ferramentas.GetStringAsync("/Planeamento/Sumario"));
        // O Registo mostra agora a entrada, na posição 1033, e já não o «Por criar».
        var log = await armazem.GetStringAsync("/Armazem/Movimentos?tipo=CM");
        Assert.DoesNotContain("data-dmo-lote-novo", log);
        Assert.Contains($"/Armazem/Movimentos?ferramenta={tool.ToolId}", log);
        Assert.Equal("1033", NaBase(db => db.ArmazemPosicoes.Single(p => p.Id == movimento.PosicaoId).Codigo));
    }

    [Fact]
    public async Task FerramentaQueJaExiste_LigaAoRegistoEmVezDeCriar()
    {
        using var armazem = await LoginAsync("7306", DmoCapabilities.ArmazemGestao);
        await RegistarLoteNovoAsync(armazem);
        var id = NaBase(db => db.ArmazemLotesNovos.Single().Id);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Tools.Add(new Tool { ToolId = "lote-novo-existente", Type = ToolType.Cm, Reference = Referencia, Lot = "13" });
            db.SaveChanges();
        }

        using var ferramentas = await LoginAsync("7307", DmoCapabilities.JobOnCreate, DmoCapabilities.Ferramentas);
        var criar = await ferramentas.GetStringAsync($"/Ferramentas?criar=1&type=CM&reference={Referencia}&lot=13&lote_novo={id}");
        Assert.Contains("id=\"ferrLoteNovoExiste\"", criar);
        using var ligou = await EnviarAsync(ferramentas, criar, "ferrLoteNovoLigar");

        Assert.Equal(HttpStatusCode.Redirect, ligou.StatusCode);
        Assert.Contains("tool_id=lote-novo-existente", ligou.Headers.Location!.OriginalString);
        Assert.Equal(1, NaBase(db => db.Tools.Count(t => t.Reference == Referencia)));
        var registo = NaBase(db => db.ArmazemLotesNovos.Single());
        Assert.Equal((ArmazemLoteNovoEstado.Resolvido, "lote-novo-existente"), (registo.Estado, registo.ResolvedToolId));
        Assert.Equal("lote-novo-existente", NaBase(db => db.ArmazemOcupacoes.Single(o => o.LibertadaEmUtc == null).ToolId));
    }
}

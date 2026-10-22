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
/// O painel da Reparação Interna (Diogo, 22/10/2026: «um sistema similar ao
/// das boquilhas»): mudar o dia do registo (as datas são livres, só avisa)
/// e a tab «Histórico», a página de cada reparador: só as reparações desta
/// pessoa, pela linha, os dias, a referência e a produção, sem filtro de
/// quem; abre vazio; o duplo clique abre a reparação e volta à vista. A
/// base é partilhada entre classes: cada teste usa as suas máquinas.
/// </summary>
public sealed class ReparacaoInternaHistoricoPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RIHIS";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoInternaHistoricoPaginaTests(CustomWebApplicationFactory factory)
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

    private async Task<HttpClient> LoginOperatorAsync(string operatorId)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Historico{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-rihis-{operatorId}-{Guid.NewGuid():N}", Name = $"RI {operatorId}", Capabilities = [DmoCapabilities.ReparacaoInterna] };
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

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Data(DateOnly dia) => dia.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Uma máquina desta classe com uma produção de há 5 dias a daqui a 2, com a CM e o MF.</summary>
    private (string Codigo, int JobOn) Maquina(string sufixo) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var maquina = new Machine { Code = codigo };
        var referencia = $"88{sufixo}T324";
        db.Machines.Add(maquina);
        db.Tools.AddRange(
            new Tool { ToolId = $"{codigo}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "1" },
            new Tool { ToolId = $"{codigo}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "1" });
        var jobOn = new JobOn { Machine = maquina, Reference = referencia, ProductionNumber = $"{sufixo}01", InicioEm = Hoje.AddDays(-5), FimEm = Hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        db.SaveChanges();
        db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm" });
        db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf" });
        db.SaveChanges();
        return (codigo, jobOn.Id);
    });

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

    private static async Task RegistarAsync(HttpClient client, string codigo, string numero, DateOnly? dia = null)
    {
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm" + (dia is { } d ? $"&dia={Data(d)}" : string.Empty));
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", numero));
        using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
    }

    [Fact]
    public async Task MudarODia_RegistaNesseDia_ESoAvisa()
    {
        var (codigo, jobOn) = Maquina("A");
        using var client = await LoginOperatorAsync("6901");
        var ontem = Hoje.AddDays(-3);

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm&dia={Data(ontem)}");

        var card = Elemento(html, $"id=\"riCard-{codigo}\"");
        Assert.Contains($"value=\"{Data(ontem)}\"", Regex.Match(card, $"<input[^>]*id=\"riDia-{codigo}\"[^>]*>").Value);
        Assert.Contains("data-dmo-autosubmit", Regex.Match(card, $"<input[^>]*id=\"riDia-{codigo}\"[^>]*>").Value);
        Assert.Contains("não no de hoje", Elemento(card, $"id=\"riOutroDia-{codigo}\""));
        Assert.Contains("Registo de " + ontem.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture), card);

        await RegistarAsync(client, codigo, "D1", ontem);

        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Numero == "D1" && x.JobOnId == jobOn));
        Assert.Equal(ontem, r.DiaOperacional);
        // No dia de hoje não aparece; no dia escolhido sim.
        var hoje = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}");
        Assert.DoesNotContain("<strong>D1</strong>", Elemento(hoje, $"id=\"riLista-{codigo}\""));
        Assert.DoesNotContain($"id=\"riOutroDia-{codigo}\"", hoje);
        var nesse = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&dia={Data(ontem)}");
        Assert.Contains("<strong>D1</strong>", Elemento(nesse, $"id=\"riLista-{codigo}\""));
    }

    [Fact]
    public async Task OHistorico_AbreVazio_SemFiltroDeQuem_EMostraSoAsDestaPessoa()
    {
        var (codigo, _) = Maquina("B");
        var (outra, _) = Maquina("C");
        using var eu = await LoginOperatorAsync("6902");
        using var colega = await LoginOperatorAsync("6903");
        await RegistarAsync(eu, codigo, "H1");
        await RegistarAsync(eu, codigo, "H2", Hoje.AddDays(-4));
        await RegistarAsync(eu, outra, "H3");
        await RegistarAsync(colega, codigo, "X9");

        var vazio = await eu.GetStringAsync("/ReparacaoInterna?historico=1");

        Assert.Matches("<a href=\"/ReparacaoInterna\\?historico=1\"[^>]*aria-current=\"page\">Hist(ó|&#xF3;)rico</a>", vazio);
        Assert.Contains("id=\"riHistConvite\"", Elemento(vazio, "id=\"riHistResultados\""));
        Assert.DoesNotContain("<table", Elemento(vazio, "id=\"riHistResultados\""));
        var filtros = Elemento(vazio, "id=\"riHistFiltros\"");
        foreach (var campo in new[] { "riHistLinha", "riHistDe", "riHistAte", "riHistReferencia", "riHistProducao" })
        {
            Assert.Contains($"id=\"{campo}\"", filtros);
        }

        Assert.DoesNotContain("Quem", filtros);
        Assert.DoesNotContain("name=\"pessoa\"", filtros);
        Assert.DoesNotContain("id=\"riLinhasRail\"", vazio);
        Assert.Contains("id=\"riHistLimpar\"", vazio);

        // A linha: só as minhas, de todos os dias.
        var porLinha = Elemento(await eu.GetStringAsync($"/ReparacaoInterna?historico=1&maquina={codigo}"), "id=\"riHistLista\"");
        Assert.Contains("<strong>H1</strong>", porLinha);
        Assert.Contains("<strong>H2</strong>", porLinha);
        Assert.DoesNotContain("<strong>H3</strong>", porLinha);
        Assert.DoesNotContain("<strong>X9</strong>", porLinha);
        Assert.DoesNotContain("<button", porLinha);
        Assert.Contains("data-ri-editar=\"riHistLista\"", Elemento(await eu.GetStringAsync($"/ReparacaoInterna?historico=1&maquina={codigo}"), "data-dmo-for=\"riHistLista\""));

        // Os dias (de / até): só os do intervalo.
        var dias = Elemento(await eu.GetStringAsync($"/ReparacaoInterna?historico=1&de={Data(Hoje.AddDays(-5))}&ate={Data(Hoje.AddDays(-2))}"), "id=\"riHistLista\"");
        Assert.Contains("<strong>H2</strong>", dias);
        Assert.DoesNotContain("<strong>H1</strong>", dias);
        Assert.DoesNotContain("<strong>H3</strong>", dias);

        // A referência: as produções dela no dropdown e as reparações delas.
        var porReferencia = await eu.GetStringAsync("/ReparacaoInterna?historico=1&referencia=88ct324");
        Assert.Contains("<strong>H3</strong>", Elemento(porReferencia, "id=\"riHistLista\""));
        Assert.DoesNotContain("<strong>H1</strong>", Elemento(porReferencia, "id=\"riHistLista\""));
        Assert.Contains("data-dmo-autosubmit", Regex.Match(Elemento(porReferencia, "id=\"riHistProducaoZona\""), "<select[^>]*>").Value);
    }

    [Fact]
    public async Task CorrigirNoHistorico_VoltaAoHistoricoComOsFiltros()
    {
        var (codigo, jobOn) = Maquina("D");
        using var client = await LoginOperatorAsync("6904");
        await RegistarAsync(client, codigo, "V1");
        var id = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.JobOnId == jobOn).Id);
        var vista = $"/ReparacaoInterna?historico=1&maquina={codigo}";
        var html = await client.GetStringAsync(vista);
        var tabela = Regex.Match(Elemento(html, "id=\"riHistLista\""), "<table[^>]*>").Value;
        Assert.Equal(vista, WebUtility.HtmlDecode(Regex.Match(tabela, "data-ri-volta=\"([^\"]*)\"").Groups[1].Value));

        var (acao, campos) = EnvioDoFormulario.Enviar(html,
            [("riCorrigirOperacao", Guid.NewGuid().ToString()), ("riCorrigirId", id.ToString()), ("riCorrigirNumero", "V2"), ("riCorrigirVolta", vista)],
            idDoBotao: "riGuardarBotao");
        using var corrigir = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, corrigir.StatusCode);
        var destino = corrigir.Headers.Location!.OriginalString;
        Assert.StartsWith("/ReparacaoInterna?historico=1", destino);
        Assert.Contains($"maquina={codigo}", destino);
        var depois = await client.GetStringAsync(destino);
        Assert.Contains("Número corrigido para V2.", Elemento(depois, "id=\"riAvisos\""));
        Assert.Contains("<strong>V2</strong>", Elemento(depois, "id=\"riHistLista\""));

        // Outro sítio que não esta página não serve de volta.
        var (acaoFora, camposFora) = EnvioDoFormulario.Enviar(html,
            [("riCorrigirOperacao", Guid.NewGuid().ToString()), ("riCorrigirId", id.ToString()), ("riCorrigirNumero", "V3"), ("riCorrigirVolta", "https://exemplo.test/ReparacaoInterna?x=1")],
            idDoBotao: "riGuardarBotao");
        using var fora = await client.PostAsync(acaoFora, EnvioDoFormulario.Corpo(camposFora));
        Assert.StartsWith("/ReparacaoInterna?", fora.Headers.Location!.OriginalString);
        Assert.DoesNotContain("exemplo.test", fora.Headers.Location!.OriginalString);
    }
}

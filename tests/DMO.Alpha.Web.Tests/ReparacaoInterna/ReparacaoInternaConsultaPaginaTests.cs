using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.ReparacaoInterna;

/// <summary>
/// A página Consultar da Reparação Interna (peça 3, RI-05): só quem tem a
/// chave ri entra; o segundo separador ao lado do Registar; abre vazia (sem
/// filtro a tabela não lê) e mostra as reparações depois de filtrar, sem
/// botões nas linhas e com a ferramenta pela referência e o lote (nunca o
/// nome técnico). A procura em dois passos: a referência pelo início e
/// depois as últimas 5 produções dela. Filtrar troca só as zonas. A base em
/// memória é partilhada entre classes: cada teste usa as suas máquinas,
/// ferramentas e pessoas (com a marca) e limpa-as no construtor.
/// </summary>
public sealed class ReparacaoInternaConsultaPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RICON";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoInternaConsultaPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        // Só o que é desta classe (as máquinas, ferramentas e produções com a marca).
        var maquinas = db.Machines.Where(m => m.Code.StartsWith(Marca)).Select(m => m.Id).ToList();
        var jobOns = db.JobOns.Where(j => maquinas.Contains(j.MachineId)).Select(j => j.Id).ToList();
        db.ReparacoesInternas.RemoveRange(db.ReparacoesInternas.Where(r => maquinas.Contains(r.MachineId)));
        db.CmContexts.RemoveRange(db.CmContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.MfContexts.RemoveRange(db.MfContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.JobOns.RemoveRange(db.JobOns.Where(j => jobOns.Contains(j.Id)));
        db.Machines.RemoveRange(db.Machines.Where(m => maquinas.Contains(m.Id)));
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId.StartsWith(Marca)));
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
        var password = $"Consulta{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-ricon-{operatorId}-{Guid.NewGuid():N}", Name = $"RI consulta {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Chefe {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            return db.SaveChanges();
        });
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

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Uma máquina desta classe com uma produção (CM e MF com nome técnico) e
    /// duas pessoas que registaram: a Ana (CM, hoje), o Rui (MF, ontem) e uma
    /// do Rui anulada. Devolve o código, o jobon_id, a referência e as pessoas.
    /// </summary>
    private (string Codigo, int JobOnId, string Referencia, Guid Ana, Guid Rui) Cenario(string sufixo) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var referencia = $"71{sufixo}0T324";
        var maquina = new Machine { Code = codigo };
        db.Machines.Add(maquina);
        db.Tools.AddRange(
            new Tool { ToolId = $"{codigo}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "2", TechnicalName = "Molde técnico secreto" },
            new Tool { ToolId = $"{codigo}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "3", TechnicalName = "Molde técnico secreto" });
        var jobOn = new JobOn { Machine = maquina, Reference = referencia, ProductionNumber = $"{sufixo}01", InicioEm = Hoje.AddDays(-2), FimEm = Hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        var ana = new User { Id = Guid.NewGuid(), OperatorId = $"ricon-a-{sufixo}", Name = $"Ana {sufixo}", ProviderUserId = $"ricon-a-{sufixo}" };
        var rui = new User { Id = Guid.NewGuid(), OperatorId = $"ricon-r-{sufixo}", Name = $"Rui {sufixo}", ProviderUserId = $"ricon-r-{sufixo}" };
        db.Users.AddRange(ana, rui);
        db.SaveChanges();
        var cm = new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm" };
        var mf = new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf" };
        db.CmContexts.Add(cm);
        db.MfContexts.Add(mf);
        db.SaveChanges();
        Reparacao Rep(ReparacaoTipo tipo, string numero, Guid quem, DateOnly dia, bool anulada = false) => new()
        {
            JobOnId = jobOn.Id, MachineId = maquina.Id, Tipo = tipo,
            CmContextId = tipo == ReparacaoTipo.Cm ? cm.Id : null, MfContextId = tipo == ReparacaoTipo.Mf ? mf.Id : null,
            Numero = numero, RegistadoPorUserId = quem, RegistadoEmUtc = dia.ToDateTime(new TimeOnly(10, 15), DateTimeKind.Utc),
            DiaOperacional = dia, AnuladaEmUtc = anulada ? DateTime.UtcNow : null
        };
        db.ReparacoesInternas.AddRange(
            Rep(ReparacaoTipo.Mf, $"{sufixo}-RUI", rui.Id, Hoje.AddDays(-1)),
            Rep(ReparacaoTipo.Cm, $"{sufixo}-ANULADA", rui.Id, Hoje, anulada: true),
            Rep(ReparacaoTipo.Cm, $"{sufixo}-ANA", ana.Id, Hoje));
        db.SaveChanges();
        return (codigo, jobOn.Id, referencia, ana.Id, rui.Id);
    });

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

    /// <summary>Um campo (input, sem fecho) pelo id.</summary>
    private static string Campo(string html, string id) =>
        Regex.Match(html, $"<input[^>]*id=\"{id}\"[^>]*>").Value;

    [Fact]
    public async Task SemAChave_NaoEntra()
    {
        using var client = await LoginOperatorAsync("6701", DmoCapabilities.Ferramentas, DmoCapabilities.JobOnCreate);

        using var response = await client.GetAsync("/ReparacaoInterna/Consultar");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/ReparacaoInterna", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task OsDoisSeparadores_RegistarEConsultar()
    {
        using var client = await LoginOperatorAsync("6702", DmoCapabilities.ReparacaoInterna);

        var consultar = await client.GetStringAsync("/ReparacaoInterna/Consultar");
        var registar = await client.GetStringAsync("/ReparacaoInterna");

        Assert.Matches("<a href=\"/ReparacaoInterna/Consultar\"[^>]*aria-current=\"page\">Consultar</a>", consultar);
        Assert.Matches("<a href=\"/ReparacaoInterna\"[^>]*>Registar</a>", consultar);
        Assert.DoesNotMatch("<a href=\"/ReparacaoInterna\"[^>]*aria-current=\"page\">Registar</a>", consultar);
        Assert.Matches("<a href=\"/ReparacaoInterna\"[^>]*aria-current=\"page\">Registar</a>", registar);
        Assert.Matches("<a href=\"/ReparacaoInterna/Consultar\"[^>]*>Consultar</a>", registar);
    }

    [Fact]
    public async Task AbreVazia_SoComOsFiltros()
    {
        var (codigo, _, _, _, _) = Cenario("A");
        using var client = await LoginOperatorAsync("6703", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync("/ReparacaoInterna/Consultar");

        // Há reparações na base, mas sem filtro a tabela não lê nada.
        var resultados = Elemento(html, "id=\"riConsultaResultados\"");
        Assert.Contains("id=\"riConsultaConvite\"", resultados);
        Assert.DoesNotContain("<table", resultados);
        Assert.DoesNotContain("A-ANA", html);
        // Os filtros numa linha: a máquina, os dias, quem, CM ou MF, a referência e a produção.
        var filtros = Elemento(html, "id=\"riConsultaFiltros\"");
        Assert.Contains($"<option value=\"{codigo}\">{codigo}</option>", Elemento(filtros, "id=\"riConsultaMaquina\""));
        Assert.Contains("type=\"date\"", Campo(filtros, "riConsultaDe"));
        Assert.Contains("type=\"date\"", Campo(filtros, "riConsultaAte"));
        Assert.Contains(">Ana A</option>", Elemento(filtros, "id=\"riConsultaPessoa\""));
        Assert.Contains("<option value=\"mf\">MF</option>", Elemento(filtros, "id=\"riConsultaTipo\""));
        Assert.Contains("data-dmo-suggest=\"referencias\"", Campo(filtros, "riConsultaReferencia"));
        Assert.Contains("disabled", Elemento(filtros, "id=\"riConsultaProducao\""));
        Assert.Contains("Limpar filtros", Elemento(html, "id=\"riConsultaLimpar\""));
        // Atualizar só o que muda: a raiz, as zonas e os filtros que ficam.
        Assert.Contains("data-dmo-troca", html);
        Assert.Contains("data-dmo-regiao", Elemento(html, "id=\"riConsultaResultados\""));
        Assert.Contains("data-dmo-regiao", Elemento(html, "id=\"riConsultaProducaoZona\""));
        Assert.Contains("data-dmo-fica data-dmo-parcial", html);
    }

    [Fact]
    public async Task DepoisDeFiltrar_AsReparacoes_SemBotoes_ENuncaONomeTecnico()
    {
        var (codigo, jobOnId, referencia, _, _) = Cenario("B");
        using var client = await LoginOperatorAsync("6704", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}");

        var tabela = Elemento(html, "id=\"riConsultaLista\"");
        Assert.Contains("class=\"dmo-table\"", tabela);
        Assert.Contains("<th>Dia</th><th>Hora</th><th>Linha</th><th>Produção</th><th>CM/MF</th><th>Ferramenta</th><th>Número</th><th>Quem</th>", tabela);
        var ids = NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOnId).ToDictionary(r => r.Numero, r => r.Id));
        var ana = Elemento(tabela, $"data-dmo-reparacao=\"{ids["B-ANA"]}\"");
        Assert.Contains($"<td>{Hoje:dd/MM/yyyy}</td>", ana);
        Assert.Contains($"<td>{codigo}</td>", ana);
        Assert.Contains($"{referencia} · B01", ana);
        Assert.Contains("<td>CM</td>", ana);
        Assert.Contains($"<td>{referencia} · lote 2</td>", ana);
        Assert.Contains("<strong>B-ANA</strong>", ana);
        Assert.Contains("<td>Ana B</td>", ana);
        Assert.Contains($"<td>{referencia} · lote 3</td>", Elemento(tabela, $"data-dmo-reparacao=\"{ids["B-RUI"]}\""));
        // A mais recente por cima; a anulada fica de fora.
        Assert.True(tabela.IndexOf("B-ANA", StringComparison.Ordinal) < tabela.IndexOf("B-RUI", StringComparison.Ordinal));
        Assert.DoesNotContain("B-ANULADA", html);
        Assert.DoesNotContain("<button", Elemento(html, "id=\"riConsultaResultados\""));
        Assert.DoesNotContain("Molde técnico secreto", html);
        // Sem o Job On, a produção é só texto.
        Assert.DoesNotContain("/JobOn?jobon_id=", tabela);
        Assert.Contains("1 de 1", Elemento(html, "class=\"dmo-list-pager\""));
    }

    [Fact]
    public async Task ComOJobOn_AProducaoAbreAFicha()
    {
        var (codigo, jobOnId, _, _, _) = Cenario("C");
        using var client = await LoginOperatorAsync("6705", DmoCapabilities.ReparacaoInterna, DmoCapabilities.JobOnCreate);

        var tabela = Elemento(await client.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}&tipo=cm"), "id=\"riConsultaLista\"");

        Assert.Contains($"<a href=\"/JobOn?jobon_id={jobOnId}\" data-dmo-completa>", tabela);
        Assert.Contains("C-ANA", tabela);
        Assert.DoesNotContain("C-RUI", tabela);
    }

    [Fact]
    public async Task AsPessoasEOsDias_Filtram()
    {
        var (codigo, _, _, ana, rui) = Cenario("D");
        using var client = await LoginOperatorAsync("6706", DmoCapabilities.ReparacaoInterna);

        var doRui = Elemento(await client.GetStringAsync($"/ReparacaoInterna/Consultar?pessoa={rui}"), "id=\"riConsultaResultados\"");
        var daAnaHoje = await client.GetStringAsync($"/ReparacaoInterna/Consultar?pessoa={ana}&de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}");
        var ontem = Elemento(await client.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}&ate={Hoje.AddDays(-1):yyyy-MM-dd}"), "id=\"riConsultaResultados\"");
        var amanha = Elemento(await client.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}&de={Hoje.AddDays(1):yyyy-MM-dd}"), "id=\"riConsultaResultados\"");

        Assert.Contains("D-RUI", doRui);
        Assert.DoesNotContain("D-ANA", doRui);
        Assert.Contains("D-ANA", Elemento(daAnaHoje, "id=\"riConsultaResultados\""));
        Assert.DoesNotContain("D-RUI", Elemento(daAnaHoje, "id=\"riConsultaResultados\""));
        // O filtro fica escolhido no dropdown e nas datas.
        Assert.Contains($"<option value=\"{ana}\" selected=\"selected\">Ana D</option>", Elemento(daAnaHoje, "id=\"riConsultaPessoa\""));
        Assert.Contains($"value=\"{Hoje:yyyy-MM-dd}\"", Campo(daAnaHoje, "riConsultaDe"));
        Assert.Contains("D-RUI", ontem);
        Assert.DoesNotContain("D-ANA", ontem);
        Assert.Contains("Nenhuma reparação com estes filtros.", amanha);
    }

    [Fact]
    public async Task AProducaoEmDoisPassos_AReferenciaDepoisAsUltimas5()
    {
        var (codigo, jobOnId, referencia, _, _) = Cenario("E");
        // Mais seis produções da mesma referência (sem reparações): o dropdown só mostra as últimas 5.
        var outras = NaBase(db =>
        {
            var maquina = db.Machines.Single(m => m.Code == codigo);
            var novas = Enumerable.Range(2, 6).Select(i => new JobOn { MachineId = maquina.Id, Reference = referencia, ProductionNumber = $"E0{i}" }).ToList();
            db.JobOns.AddRange(novas);
            db.SaveChanges();
            return novas.Select(j => j.Id).ToList();
        });
        using var client = await LoginOperatorAsync("6707", DmoCapabilities.ReparacaoInterna);

        // O início da referência (como «60» traz todas as que começam por 60).
        var html = await client.GetStringAsync($"/ReparacaoInterna/Consultar?referencia={referencia[..4].ToLowerInvariant()}");

        var producao = Elemento(html, "id=\"riConsultaProducao\"");
        Assert.DoesNotContain("disabled", producao);
        Assert.Contains("data-dmo-depende=\"riConsultaReferencia\"", producao);
        Assert.Equal(6, Regex.Matches(producao, "<option").Count);
        Assert.DoesNotContain($"value=\"{jobOnId}\"", producao);
        Assert.Contains($"value=\"{outras[^1]}\"", producao);
        // Só com a referência, as reparações das produções dela.
        Assert.Contains("E-ANA", Elemento(html, "id=\"riConsultaResultados\""));

        // Escolher uma produção sem reparações: a tabela fica vazia.
        var escolhida = await client.GetStringAsync($"/ReparacaoInterna/Consultar?referencia={referencia}&producao={outras[^1]}");
        Assert.Contains($"<option value=\"{outras[^1]}\" selected=\"selected\">", Elemento(escolhida, "id=\"riConsultaProducao\""));
        Assert.Contains("Nenhuma reparação com estes filtros.", Elemento(escolhida, "id=\"riConsultaResultados\""));

        // A produção com reparações, por um link guardado (fora das últimas 5), continua no dropdown.
        var guardada = await client.GetStringAsync($"/ReparacaoInterna/Consultar?referencia={referencia}&producao={jobOnId}");
        Assert.Contains($"<option value=\"{jobOnId}\" selected=\"selected\">", Elemento(guardada, "id=\"riConsultaProducao\""));
        Assert.Contains("E-ANA", Elemento(guardada, "id=\"riConsultaResultados\""));

        // As sugestões da referência também respondem a quem só tem a Reparação Interna.
        var sugestoes = await client.GetStringAsync($"/Sugestoes?fonte=referencias&q={referencia[..4]}");
        Assert.Contains(referencia, sugestoes);
    }

    /// <summary>A raiz data-dmo-troca sem as zonas, sem os filtros que ficam e sem o token.</summary>
    private static string ForaDasZonas(string html)
    {
        var raiz = Elemento(html, "data-dmo-troca");
        raiz = raiz.Replace(Elemento(raiz, "data-dmo-fica"), string.Empty, StringComparison.Ordinal);
        raiz = raiz.Replace(Elemento(raiz, "id=\"riConsultaResultados\""), string.Empty, StringComparison.Ordinal);
        return Regex.Replace(raiz, "name=\"__RequestVerificationToken\"[^>]*", string.Empty);
    }

    [Fact]
    public async Task Filtrar_SoMudamAsZonas()
    {
        var (codigo, _, referencia, _, _) = Cenario("F");
        using var client = await LoginOperatorAsync("6708", DmoCapabilities.ReparacaoInterna);

        var vazia = await client.GetStringAsync("/ReparacaoInterna/Consultar");
        var filtrada = await client.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}&referencia={referencia}&pagina=1");

        Assert.Equal(ForaDasZonas(vazia), ForaDasZonas(filtrada));
        Assert.Contains("data-dmo-parcial", Elemento(filtrada, "id=\"riConsultaResultados\""));
    }
}

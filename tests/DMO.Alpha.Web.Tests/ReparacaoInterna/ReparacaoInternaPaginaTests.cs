using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
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
/// A página Registar da Reparação Interna (peça 1): só quem tem a chave ri
/// entra; escolher a máquina traz a produção do dia (a ferramenta pela
/// referência e o lote, nunca o nome técnico); escrever o número e Enter
/// grava, volta com o campo vazio e com o cursor e a reparação no topo da
/// tabela; o mesmo envio duas vezes grava uma. Duas produções avisam e a de
/// maior jobon_id vem escolhida; sem produção nada se pode gravar. A base em
/// memória é partilhada entre classes: cada teste usa as suas máquinas.
/// </summary>
public sealed class ReparacaoInternaPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RIPAG";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoInternaPaginaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        // Só o que é desta classe (as máquinas, ferramentas e produções com a marca).
        var maquinas = db.Machines.Where(m => m.Code.StartsWith(Marca)).Select(m => m.Id).ToList();
        var jobOns = db.JobOns.Where(j => maquinas.Contains(j.MachineId)).Select(j => j.Id).ToList();
        var reparacoes = db.ReparacoesInternas.Where(r => maquinas.Contains(r.MachineId)).ToList();
        var resultados = reparacoes.Select(r => (int?)r.Id).ToList();
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas.Where(o => o.ResultadoTipo == "Reparacao" && resultados.Contains(o.ResultadoId)));
        db.ReparacoesInternas.RemoveRange(reparacoes);
        db.CmContexts.RemoveRange(db.CmContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.MfContexts.RemoveRange(db.MfContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)));
        db.JobOns.RemoveRange(db.JobOns.Where(j => jobOns.Contains(j.Id)));
        db.Machines.RemoveRange(db.Machines.Where(m => maquinas.Contains(m.Id)));
        db.Tools.RemoveRange(db.Tools.Where(t => t.ToolId.StartsWith(Marca)));
        db.ModuleSettings.RemoveRange(db.ModuleSettings.Where(s => s.SettingKey == ModuleSettingKeys.ReparacaoInternaDayChangeHour));
        db.SaveChanges();
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
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

    private async Task<HttpClient> LoginOperatorAsync(string operatorId, params string[] capabilities)
    {
        var email = $"{operatorId}@dmo.local";
        var password = $"Reparacao{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-ri-{operatorId}-{Guid.NewGuid():N}", Name = $"RI {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Reparador {operatorId}", ProviderUserId = $"provider-{operatorId}" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            return db.SaveChanges();
        });
        return await LoginAsync(email, password);
    }

    private T NaBase<T>(Func<DmoDbContext, T> ler)
    {
        using var scope = _factory.Services.CreateScope();
        return ler(scope.ServiceProvider.GetRequiredService<DmoDbContext>());
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Uma máquina desta classe com produções que cobrem hoje e ontem (o dia
    /// operacional antes da viragem), cada uma com a CM e o MF. Devolve o
    /// código da máquina e os jobon_id pela ordem dada.
    /// </summary>
    private (string Codigo, int[] JobOns) Maquina(string sufixo, int producoes, bool comDatas = true) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var maquina = new Machine { Code = codigo };
        db.Machines.Add(maquina);
        var ids = new List<int>();
        for (var i = 1; i <= producoes; i++)
        {
            var referencia = $"55{sufixo}{i}T324";
            db.Tools.AddRange(
                new Tool { ToolId = $"{codigo}-cm{i}", Type = ToolType.Cm, Reference = referencia, Lot = $"{i + 1}", TechnicalName = "Molde técnico secreto" },
                new Tool { ToolId = $"{codigo}-mf{i}", Type = ToolType.Mf, Reference = referencia, Lot = $"{i + 1}" });
            var jobOn = new JobOn
            {
                Machine = maquina, Reference = referencia, ProductionNumber = $"{sufixo}0{i}",
                InicioEm = comDatas ? Hoje.AddDays(-2) : null, FimEm = comDatas ? Hoje.AddDays(2) : null
            };
            db.JobOns.Add(jobOn);
            db.SaveChanges();
            db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm{i}" });
            db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf{i}" });
            db.SaveChanges();
            ids.Add(jobOn.Id);
        }

        return (codigo, ids.ToArray());
    });

    /// <summary>Envia o formulário que a página desenha, com os campos mudados (como o browser).</summary>
    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string html, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        var (acao, campos) = EnvioDoFormulario.Preencher(html, preenchidos);
        return await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
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

    private static string Campo(string html, string id) =>
        Regex.Match(html, $"<input[^>]*id=\"{id}\"[^>]*>").Value;

    [Fact]
    public async Task SemAChave_NaoEntra()
    {
        using var client = await LoginOperatorAsync("6601", DmoCapabilities.Ferramentas, DmoCapabilities.ArmazemGestao);

        using var response = await client.GetAsync("/ReparacaoInterna");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("/ReparacaoInterna", response.Headers.Location!.ToString());
        Assert.DoesNotContain("Reparação Interna", await client.GetStringAsync("/Ferramentas"));
    }

    [Fact]
    public async Task ComAChave_AbreComOMenuEAsZonas()
    {
        var (codigo, _) = Maquina("A", 1);
        using var client = await LoginOperatorAsync("6602", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync("/ReparacaoInterna");

        // Dentro das páginas diz só «Reparação»; o módulo continua «Reparação Interna» (Diogo, 23/10/2026).
        Assert.Equal("<h1 id=\"ri-title\">Reparação</h1>", Elemento(html, "id=\"ri-title\""));
        Assert.Contains(">Reparação Interna<", html);
        Assert.Matches("<a href=\"/ReparacaoInterna\"[^>]*aria-current=\"page\">Registar</a>", html);
        // As tabs: Registar, Histórico (o desta pessoa) e Consultar.
        Assert.Contains("href=\"/ReparacaoInterna?historico=1\"", html);
        // O painel lateral das Boquilhas (o rail único) com as linhas; sem
        // linha escolhida, o registo pede-a (Diogo, 22/10/2026).
        var painel = Elemento(html, "id=\"riLinhasRail\"");
        Assert.Contains("beta-production-rail", painel);
        Assert.Contains($"<strong>{codigo}</strong>", painel);
        Assert.Contains($"data-dmo-open=\"/ReparacaoInterna?maquina={codigo}\"", painel);
        Assert.Contains("55A1T324 · A01", painel);
        Assert.Contains("CM lote 2 · MF lote 2", painel);
        Assert.DoesNotContain("Máquina", painel);
        Assert.Contains("Escolha a linha no painel lateral para registar.", Elemento(html, "id=\"riRegisto\""));
        Assert.DoesNotContain($"id=\"riCard-{codigo}\"", html);
        // Atualizar só o que muda: a raiz, as zonas e os cliques parciais.
        Assert.Contains("data-dmo-troca", html);
        foreach (var zona in new[] { "riAvisos", "riRegisto", "riLinhas" })
        {
            Assert.Contains("data-dmo-regiao", Elemento(html, $"id=\"{zona}\""));
        }

        Assert.Contains("data-dmo-parcial", Elemento(html, "id=\"riLinhas\""));
        Assert.Contains("data-dmo-parcial", Elemento(html, "id=\"riRegisto\""));

        // Escolhida, a linha fica marcada no painel e abre o registo dela.
        var escolhida = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}");
        Assert.Contains($"Linha {codigo}</h3>", Elemento(escolhida, $"id=\"riCard-{codigo}\""));
        Assert.Matches($"<button type=\"button\" class=\"selected\"[^>]*data-id=\"{codigo}\"", Elemento(escolhida, "id=\"riLinhasRail\""));
        // Depois do login, quem só tem a Reparação Interna abre-a.
        using var inicio = await client.GetAsync("/");
        Assert.Equal("/ReparacaoInterna", inicio.Headers.Location?.ToString());
    }

    [Fact]
    public async Task EscreverONumeroEEnter_Grava_OCampoVoltaVazio_EALinhaApareceNoTopo()
    {
        var (codigo, jobOns) = Maquina("B", 1);
        using var client = await LoginOperatorAsync("6603", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var contexto = Elemento(html, $"id=\"riCard-{codigo}\"");
        Assert.Contains("CM 55B1T324 · lote 2", Elemento(contexto, "data-dmo-ferramenta=\"cm\""));
        Assert.Contains("B01 · 55B1T324", Elemento(contexto, $"data-dmo-producao=\"{jobOns[0]}\""));
        Assert.DoesNotContain("Molde técnico secreto", html);
        Assert.DoesNotContain("dmo-aviso", contexto);
        Assert.Contains("Ainda não registou reparações nesta linha neste dia.", Elemento(html, $"id=\"riLista-{codigo}\""));
        // O OK no cartão, dentro do formulário; o campo com o cursor.
        Assert.Contains(">OK</button>", Elemento(html, $"id=\"riRegistar-{codigo}\""));
        Assert.Contains("autofocus", Campo(html, $"riNumero-{codigo}"));

        using var gravar = await EnviarAsync(client, html, ($"riNumero-{codigo}", "  457  "));
        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        var destino = gravar.Headers.Location!.OriginalString;
        Assert.Contains($"maquina={codigo}", destino);
        Assert.Contains("tipo=cm", destino);

        var depois = await client.GetStringAsync(destino);
        Assert.Contains("Reparação do nº 457 registada.", Elemento(depois, "id=\"riAvisos\""));
        Assert.DoesNotContain("value=", Campo(depois, $"riNumero-{codigo}"));
        Assert.Contains("autofocus", Campo(depois, $"riNumero-{codigo}"));
        var id = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.JobOnId == jobOns[0]).Id);
        var linha = Elemento(depois, $"data-dmo-reparacao=\"{id}\"");
        Assert.Contains("<strong>457</strong>", linha);
        Assert.Contains("<td>CM</td>", linha);
        // O registo é desta pessoa: sem a coluna de quem; a linha abre com duplo clique.
        Assert.DoesNotContain("Reparador 6603", linha);
        Assert.Contains("data-dmo-row", linha);
        Assert.Contains($"data-dmo-for=\"riLog-{codigo}\"", depois);
        Assert.DoesNotContain("<button", Elemento(depois, $"id=\"riLista-{codigo}\""));
        Assert.Contains("class=\"dmo-table\"", Elemento(depois, $"id=\"riLista-{codigo}\""));

        // O MF a seguir, e o mais recente fica por cima.
        var mf = await client.GetStringAsync(destino.Replace("tipo=cm", "tipo=mf"));
        using (var segunda = await EnviarAsync(client, mf, ($"riNumero-{codigo}", "12")))
        {
            var tabela = Elemento(await client.GetStringAsync(segunda.Headers.Location!.OriginalString), $"id=\"riLista-{codigo}\"");
            Assert.True(tabela.IndexOf("<strong>12</strong>", StringComparison.Ordinal) < tabela.IndexOf("<strong>457</strong>", StringComparison.Ordinal));
            Assert.Contains("<td>MF</td>", tabela);
        }

        var gravadas = NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOns[0]).OrderBy(r => r.Id).ToList());
        Assert.Equal(new[] { ("457", ReparacaoTipo.Cm), ("12", ReparacaoTipo.Mf) }, gravadas.Select(r => (r.Numero, r.Tipo)));
        Assert.NotNull(gravadas[0].CmContextId);
        Assert.NotNull(gravadas[1].MfContextId);
    }

    /// <summary>A raiz data-dmo-troca sem as zonas e sem o token: o que tem de ficar igual para só as zonas trocarem.</summary>
    private static string ForaDasZonas(string html)
    {
        var raiz = Elemento(html, "data-dmo-troca");
        foreach (var zona in new[] { "riAvisos", "riRegisto", "riLinhas" })
        {
            raiz = raiz.Replace(Elemento(raiz, $"id=\"{zona}\""), string.Empty, StringComparison.Ordinal);
        }

        return Regex.Replace(raiz, "name=\"__RequestVerificationToken\"[^>]*", string.Empty);
    }

    [Fact]
    public async Task EscolherAMaquinaEGravar_SoMudamAsZonas()
    {
        var (codigo, _) = Maquina("G", 1);
        using var client = await LoginOperatorAsync("6608", DmoCapabilities.ReparacaoInterna);

        var vazia = await client.GetStringAsync("/ReparacaoInterna");
        var escolhida = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        using var gravar = await EnviarAsync(client, escolhida, ($"riNumero-{codigo}", "3"));
        var depois = await client.GetStringAsync(gravar.Headers.Location!.OriginalString);

        Assert.Equal(ForaDasZonas(vazia), ForaDasZonas(escolhida));
        Assert.Equal(ForaDasZonas(escolhida), ForaDasZonas(depois));
        // O formulário de registo é um envio parcial e cada página leva um envio novo.
        Assert.Contains("data-dmo-parcial", Elemento(depois, $"id=\"riCard-{codigo}\""));
        Assert.NotEqual(
            Regex.Match(escolhida, "name=\"operacao_id\" value=\"([^\"]+)\"").Groups[1].Value,
            Regex.Match(depois, "name=\"operacao_id\" value=\"([^\"]+)\"").Groups[1].Value);
    }

    [Fact]
    public async Task OMesmoEnvioDuasVezes_GravaUmaVez()
    {
        var (codigo, jobOns) = Maquina("C", 1);
        using var client = await LoginOperatorAsync("6604", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=mf");
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", "77"));

        using var primeira = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        using var segunda = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, segunda.StatusCode);
        Assert.Single(NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOns[0]).ToList()));
    }

    // O número reparado: só algarismos, até 3 (Diogo, 23/10/2026). Os dois lados do limite.
    [Theory]
    [InlineData("1000")]
    [InlineData("12a")]
    [InlineData("1.5")]
    public async Task UmaRecusa_VoltaComONumeroEscrito_ENadaSeGrava(string numero)
    {
        var (codigo, jobOns) = Maquina("D", 1);
        using var client = await LoginOperatorAsync("6605", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        using var recusa = await EnviarAsync(client, html, ($"riNumero-{codigo}", numero));

        Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
        var pagina = await recusa.Content.ReadAsStringAsync();
        Assert.Contains("O número tem só algarismos, até 3.", Elemento(pagina, "id=\"riAvisos\""));
        Assert.Contains($"value=\"{numero}\"", Campo(pagina, $"riNumero-{codigo}"));
        Assert.Contains("aria-invalid=\"true\"", Campo(pagina, $"riNumero-{codigo}"));
        Assert.Empty(NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOns[0]).ToList()));
    }

    [Fact]
    public async Task ONumero_TresAlgarismos_Grava_EOCampoSoAceitaAlgarismos()
    {
        var (codigo, jobOns) = Maquina("D", 1);
        using var client = await LoginOperatorAsync("6606", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var campo = Campo(html, $"riNumero-{codigo}");
        Assert.Contains("inputmode=\"numeric\"", campo);
        Assert.Contains("maxlength=\"3\"", campo);
        Assert.Contains("pattern=\"[0-9]{1,3}\"", campo);

        using var gravar = await EnviarAsync(client, html, ($"riNumero-{codigo}", "999"));

        Assert.Equal(HttpStatusCode.Found, gravar.StatusCode);
        Assert.Equal("999", NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.JobOnId == jobOns[0]).Numero));
    }

    [Fact]
    public async Task DuasProducoes_AvisaComAsCandidatas_ADeMaiorIdEscolhida()
    {
        var (codigo, jobOns) = Maquina("E", 2);
        using var client = await LoginOperatorAsync("6606", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        // Duas produções: o dropdown pequeno no cartão, com as duas.
        var producao = Elemento(html, $"id=\"riProducao-{codigo}\"");
        Assert.Contains("E01 · 55E1T324", producao);
        Assert.Contains("E02 · 55E2T324", producao);
        Assert.Contains($"<option value=\"{jobOns[1]}\" selected=\"selected\">", producao);
        Assert.Contains("CM 55E2T324 · lote 3", Elemento(Elemento(html, $"id=\"riCard-{codigo}\""), "data-dmo-ferramenta=\"cm\""));

        // Escolher a outra (o dropdown aplica ao escolher) e gravar nela.
        var outra = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm&producao={jobOns[0]}");
        Assert.Contains("CM 55E1T324 · lote 2", Elemento(Elemento(outra, $"id=\"riCard-{codigo}\""), "data-dmo-ferramenta=\"cm\""));
        using var gravar = await EnviarAsync(client, outra, ($"riNumero-{codigo}", "5"));
        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        Assert.Equal(jobOns[0], NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(r => r.Numero == "5" && r.JobOnId != null && jobOns.Contains(r.JobOnId.Value)).JobOnId));
    }

    [Fact]
    public async Task SemProducao_AvisaEOFormularioPedeAReferenciaEOLote()
    {
        var (codigo, _) = Maquina("F", 1, comDatas: false);
        using var client = await LoginOperatorAsync("6607", DmoCapabilities.ReparacaoInterna, DmoCapabilities.JobOnCreate);

        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        // Sem Job On regista-se com a referência e o lote escritos (Diogo,
        // 21/10/2026): o aviso só avisa e continua a levar ao Job On.
        Assert.Contains("Pode registar sem Job On", Elemento(html, $"id=\"riSemProducao-{codigo}\""));
        Assert.Contains("href=\"/JobOn?novo=1#lightSheet\"", Elemento(html, $"id=\"riSemProducao-{codigo}\""));
        Assert.Contains($"id=\"riRegistar-{codigo}\"", html);
        Assert.Contains($"id=\"riOk-{codigo}\"", html);
        Assert.Contains("required", Campo(html, $"riReferencia-{codigo}"));
        Assert.Contains("required", Campo(html, $"riLote-{codigo}"));
        Assert.DoesNotContain($"id=\"riOutra-{codigo}\"", html);
    }

    [Fact]
    public async Task AdminTemplatesECanal_AChaveEAHoraDaReparacaoInterna()
    {
        const string email = "ri-admin@example.test";
        _factory.AuthProvider.AddAccount(email, "ri-admin-provider", "RiAdmin1!", email);
        NaBase(db =>
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
            return db.SaveChanges();
        });
        using var admin = await LoginAsync(email, "RiAdmin1!");
        var boquilhas = NaBase(db => db.ModuleSettings.AsNoTracking().FirstOrDefault(s => s.SettingKey == ModuleSettingKeys.BoquilhasDayChangeHour)?.SettingValue);

        var templates = await admin.GetStringAsync("/Admin/Templates");
        Assert.Contains("Reparação Interna", templates);
        Assert.Contains($"value=\"{DmoCapabilities.ReparacaoInterna}\"", templates);

        using var chefe = await LoginOperatorAsync("6604", DmoCapabilities.BoquilhasView);
        var canal = await chefe.GetStringAsync("/Definicoes?secao=reparacao");
        // O ADMIN só vê as definições do Admin (Diogo, 25/10/2026): o cartão é de «Boquilhas · ver».
        Assert.DoesNotContain("id=\"riDefViragem\"", await admin.GetStringAsync("/Definicoes?secao=reparacao"));
        var cartao = Elemento(canal, "id=\"riDefViragem\"");
        Assert.Contains("Hora de viragem do dia", cartao);
        Assert.Contains("<option value=\"8\" selected=\"selected\">8:00</option>", cartao);

        var adminHtml = await admin.GetStringAsync("/Definicoes");
        using var gravar = await admin.PostAsync("/ReparacaoInterna/Definicoes?handler=SaveDayChangeHour", HtmlFormHelpers.BuildFormPost(
            ("dayChangeHour", "6"), ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(adminHtml)!)));
        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        var depois = await chefe.GetStringAsync(gravar.Headers.Location!.OriginalString);
        Assert.Contains("Hora de viragem gravada.", Elemento(depois, "id=\"riDefViragem\""));
        // A das Boquilhas fica como estava.
        Assert.Equal(("6", boquilhas), NaBase(db => (
            db.ModuleSettings.AsNoTracking().Single(s => s.SettingKey == ModuleSettingKeys.ReparacaoInternaDayChangeHour).SettingValue,
            db.ModuleSettings.AsNoTracking().FirstOrDefault(s => s.SettingKey == ModuleSettingKeys.BoquilhasDayChangeHour)?.SettingValue)));
    }

    [Theory]
    [InlineData("G", 1, "Perde-se também: 1 reparação.")]
    [InlineData("H", 3, "Perde-se também: 3 reparações.")]
    public async Task EliminarJobOnComReparacoes_AJanelaDizQuantasSePerdem(string sufixo, int quantas, string esperado)
    {
        var (_, jobOns) = Maquina(sufixo, 1);
        NaBase(db =>
        {
            var jobOn = db.JobOns.AsNoTracking().Single(j => j.Id == jobOns[0]);
            var cm = db.CmContexts.AsNoTracking().Single(c => c.JobOnId == jobOn.Id);
            for (var i = 1; i <= quantas; i++)
            {
                db.ReparacoesInternas.Add(new Reparacao
                {
                    JobOnId = jobOn.Id, MachineId = jobOn.MachineId, Tipo = ReparacaoTipo.Cm, CmContextId = cm.Id,
                    Numero = $"{sufixo}{i}", RegistadoEmUtc = DateTime.UtcNow, DiaOperacional = Hoje
                });
            }

            return db.SaveChanges();
        });
        using var client = await LoginOperatorAsync($"661{quantas}", DmoCapabilities.JobOnCreate);

        var ficha = WebUtility.HtmlDecode(await client.GetStringAsync($"/JobOn/Folha?jobon_id={jobOns[0]}"));
        var inicio = ficha.IndexOf("<dialog id=\"jobon-delete-dialog\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a janela do Eliminar não está na ficha");
        var janela = ficha[inicio..(ficha.IndexOf("</dialog>", inicio, StringComparison.Ordinal) + 9)];

        Assert.Contains(esperado, janela);
    }

    [Fact]
    public void EliminarAProducao_AvisaQueSePerdemAsReparacoes()
    {
        var preview = new JobOnDeletionPreview(1, "5538T324", "202511", null, false, 0, 0, 0, 0, 0, false) { ReparacoesInternas = 3 };

        var aviso = JobOnEliminarAviso.From(preview, veControlo: false);

        Assert.Equal("Perde-se também: 3 reparações.", aviso.Perde);
    }
}

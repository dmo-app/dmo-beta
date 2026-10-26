using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Presentation;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DMO.Alpha.Web.Tests.ReparacaoInterna;

/// <summary>
/// PERF-09: a Reparação Interna responde só com as zonas pedidas pelo
/// cabeçalho X-Dmo-Zonas (riLinhas, riAvisos, riRegisto na vista Registar;
/// riAvisos, riHistResultados no Histórico), com os mesmos partials da página
/// completa; sem o cabeçalho, ou com uma zona que a vista não tem, responde
/// com a página inteira como antes. A autorização é a da página.
/// </summary>
public sealed class ReparacaoInternaZonasPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RIZON";

    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _saida;

    public ReparacaoInternaZonasPaginaTests(CustomWebApplicationFactory factory, ITestOutputHelper saida)
    {
        _factory = factory;
        _saida = saida;
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
        var password = $"Zonas{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-rizon-{operatorId}-{Guid.NewGuid():N}", Name = $"RI {operatorId}", Capabilities = [DmoCapabilities.ReparacaoInterna] };
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

    private (string Codigo, int JobOn) Maquina(string sufixo) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var maquina = new Machine { Code = codigo };
        var referencia = $"77{sufixo}T324";
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

    private static async Task RegistarAsync(HttpClient client, string codigo, string numero)
    {
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", numero));
        using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
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

    // O que muda de resposta para resposta sem ser da zona: as fichas
    // antifalsificação, o identificador do envio (um novo em cada desenho), a
    // hora de agora no campo e o espaço entre etiquetas.
    private static string Normalizar(string html)
    {
        html = Regex.Replace(html, "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*", "$1");
        html = Regex.Replace(html, "(name=\"operacao_id\"[^>]*value=\")[^\"]*", "$1");
        html = Regex.Replace(html, "(type=\"time\"[^>]*value=\")[^\"]*", "$1");
        return Regex.Replace(html.Trim(), ">\\s+<", "><");
    }

    private static async Task<HttpResponseMessage> ZonasAsync(HttpClient client, string url, string? zonas)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, url);
        if (zonas is not null)
        {
            pedido.Headers.Add("X-Dmo-Zonas", zonas);
        }

        return await client.SendAsync(pedido);
    }

    // As zonas pedidas são as da página inteira, pela ordem pedida e iguais.
    private static async Task AssertZonasIguaisAsync(HttpClient client, string url, params (string Id, string Atributo)[] zonas)
    {
        var inteira = await client.GetStringAsync(url);
        using var resposta = await ZonasAsync(client, url, string.Join(' ', zonas.Select(z => z.Id)));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(string.Join(' ', zonas.Select(z => z.Id)), Assert.Single(resposta.Headers.GetValues("X-Dmo-Region")));
        Assert.Contains("X-Dmo-Zonas", resposta.Headers.GetValues("Vary").SelectMany(v => v.Split(',')).Select(v => v.Trim()));
        var parcial = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", parcial);
        Assert.Equal(
            Normalizar(string.Concat(zonas.Select(z => Elemento(inteira, z.Atributo)))),
            Normalizar(parcial));
    }

    [Fact]
    public async Task DepoisDeGravar_RegistoEAvisos_SaoOsMesmosPartialsDaPaginaInteira()
    {
        var (codigo, _) = Maquina("A");
        Maquina("B");
        using var client = await LoginOperatorAsync("7001");
        await RegistarAsync(client, codigo, "11");
        await RegistarAsync(client, codigo, "12");

        var vista = $"/ReparacaoInterna?maquina={codigo}&tipo=cm";
        // O GET do redirect que segue o «OK»: a mensagem e o registo.
        await AssertZonasIguaisAsync(client, vista + "&gravacao=ok&numero=12", ("riRegisto", "id=\"riRegisto\""), ("riAvisos", "id=\"riAvisos\""));
        await AssertZonasIguaisAsync(client, vista, ("riAvisos", "id=\"riAvisos\""), ("riRegisto", "id=\"riRegisto\""));
        // O formulário do registo declara a mensagem como zona que muda com ele (a do registo é a dele).
        var inteira = await client.GetStringAsync(vista);
        Assert.Matches("<div id=\"riRegisto\"[^>]*data-dmo-zonas=\"riAvisos\"", inteira);
        Assert.Contains("<strong>12</strong>", Elemento(inteira, "id=\"riRegisto\""));
        using var parcial = await ZonasAsync(client, vista + "&gravacao=ok&numero=12", "riRegisto riAvisos");
        var html = await parcial.Content.ReadAsStringAsync();
        Assert.Contains("Reparação do nº 12 registada.", Elemento(html, "id=\"riAvisos\""));
        Assert.DoesNotContain("riLinhasRail", html);
        Assert.DoesNotContain("<main", html);
    }

    [Fact]
    public async Task OPainelDasLinhas_PedidoSozinho_ELinhaSemEscolher_SaoOsMesmosPartials()
    {
        var (codigo, _) = Maquina("C");
        Maquina("D");
        using var client = await LoginOperatorAsync("7002");

        await AssertZonasIguaisAsync(client, $"/ReparacaoInterna?maquina={codigo}", ("riLinhas", "id=\"riLinhas\""));
        // Sem linha escolhida: o convite, e o painel sem nenhuma selecionada.
        await AssertZonasIguaisAsync(client, "/ReparacaoInterna", ("riRegisto", "id=\"riRegisto\""), ("riLinhas", "id=\"riLinhas\""));
        await AssertZonasIguaisAsync(client, "/ReparacaoInterna?maquina=NAO-EXISTE", ("riRegisto", "id=\"riRegisto\""));
        // Noutro dia e com mais de uma zona do painel.
        await AssertZonasIguaisAsync(client, $"/ReparacaoInterna?maquina={codigo}&dia={Hoje.AddDays(-3):yyyy-MM-dd}", ("riLinhas", "id=\"riLinhas\""), ("riRegisto", "id=\"riRegisto\""), ("riAvisos", "id=\"riAvisos\""));
    }

    [Fact]
    public async Task OHistorico_ZonaDosResultados_ESoAsMinhasReparacoes_SaoOsMesmosPartials()
    {
        var (codigo, _) = Maquina("E");
        using var eu = await LoginOperatorAsync("7003");
        using var colega = await LoginOperatorAsync("7004");
        await RegistarAsync(eu, codigo, "21");
        await RegistarAsync(colega, codigo, "29");

        await AssertZonasIguaisAsync(eu, "/ReparacaoInterna?historico=1", ("riHistResultados", "id=\"riHistResultados\""));
        await AssertZonasIguaisAsync(eu, $"/ReparacaoInterna?historico=1&maquina={codigo}", ("riHistResultados", "id=\"riHistResultados\""), ("riAvisos", "id=\"riAvisos\""));
        await AssertZonasIguaisAsync(eu, "/ReparacaoInterna?historico=1&referencia=77e", ("riAvisos", "id=\"riAvisos\""), ("riHistResultados", "id=\"riHistResultados\""));
        using var resposta = await ZonasAsync(eu, $"/ReparacaoInterna?historico=1&maquina={codigo}", "riHistResultados");
        var html = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("<strong>21</strong>", html);
        Assert.DoesNotContain("<strong>29</strong>", html);
    }

    [Theory]
    [InlineData("/ReparacaoInterna", "riInexistente")]
    [InlineData("/ReparacaoInterna", "riRegisto riInexistente")]
    [InlineData("/ReparacaoInterna", "riRegisto riRegisto")]
    [InlineData("/ReparacaoInterna", "riHistResultados")]
    [InlineData("/ReparacaoInterna?historico=1", "riLinhas")]
    [InlineData("/ReparacaoInterna?historico=1", "riRegisto riAvisos")]
    public async Task ZonaDesconhecidaOuDeOutraVista_RespondeComAPaginaInteira(string url, string zonas)
    {
        Maquina("F");
        using var client = await LoginOperatorAsync("7005");

        using var resposta = await ZonasAsync(client, url, zonas);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False(resposta.Headers.Contains("X-Dmo-Region"));
        var html = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("<html", html);
        Assert.Equal(Normalizar(await client.GetStringAsync(url)), Normalizar(html));
    }

    [Fact]
    public async Task SemOCabecalho_AResposta_EAPaginaInteiraDeSempre()
    {
        var (codigo, _) = Maquina("G");
        using var client = await LoginOperatorAsync("7006");
        await RegistarAsync(client, codigo, "31");

        var inteira = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}");

        Assert.Contains("<html", inteira);
        Assert.Contains("id=\"riLinhasRail\"", inteira);
        Assert.Contains("id=\"riAvisos\"", inteira);
        Assert.Contains("<strong>31</strong>", Elemento(inteira, "id=\"riRegisto\""));
        Assert.Contains("id=\"riLinhas\"", inteira);
    }

    [Theory]
    [InlineData("/ReparacaoInterna?maquina=X")]
    [InlineData("/ReparacaoInterna?historico=1")]
    public async Task AAutorizacaoEADaPagina_ComECemOCabecalho(string url)
    {
        // Sem sessão: vai para o login, com ou sem zonas.
        var anonimo = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var semSessao = await ZonasAsync(anonimo, url, url.Contains("historico", StringComparison.Ordinal) ? "riHistResultados" : "riRegisto riAvisos");
        Assert.Equal(HttpStatusCode.Redirect, semSessao.StatusCode);
        Assert.Contains("/login", semSessao.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.False(semSessao.Headers.Contains("X-Dmo-Region"));

        // Com sessão mas sem a chave ri: o mesmo que sem cabeçalho.
        _factory.AuthProvider.AddAccount("7007@dmo.local", "provider-7007", "ZonasSemChave7007!", "7007@dmo.local");
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-rizon-7007-{Guid.NewGuid():N}", Name = "Sem RI", Capabilities = [] };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = "7007", Name = "Sem chave", ProviderUserId = "provider-7007" };
            db.Users.Add(user);
            db.SaveChanges();
            db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            return db.SaveChanges();
        });
        var sem = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await sem.GetAsync("/login")).Content.ReadAsStringAsync();
        await sem.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "7007@dmo.local"), ("password", "ZonasSemChave7007!"), ("returnUrl", "/"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
        using var semCabecalho = await ZonasAsync(sem, url, null);
        using var comCabecalho = await ZonasAsync(sem, url, "riAvisos");
        Assert.Equal(semCabecalho.StatusCode, comCabecalho.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, comCabecalho.StatusCode);
        Assert.Equal(semCabecalho.Headers.Location?.OriginalString, comCabecalho.Headers.Location?.OriginalString);
        Assert.False(comCabecalho.Headers.Contains("X-Dmo-Region"));
    }

    // PERF-09, as leituras à base (PostgreSQL, como na aplicação: o InMemory
    // não envia comandos). Antes, o GET que segue uma gravação desenhava a
    // página inteira; agora lê só o que as zonas pedidas mostram. Os números
    // vão para a saída do teste.
    [SkippableFact]
    public async Task OsPedidosComZonas_LeemMenosDaBaseQueAPaginaInteira_NoPostgres()
    {
        var ligacao = Environment.GetEnvironmentVariable("DMO_POSTGRES_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(ligacao), "DMO_POSTGRES_CONNECTION is not configured (disposable PostgreSQL test database).");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(ligacao!) { Pooling = false };
        Skip.IfNot(
            builder.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true,
            "DMO_POSTGRES_CONNECTION must name a disposable database containing «test».");
        var conta = new ContaConsultas();
        using var fabrica = _factory.WithWebHostBuilder(web => web.ConfigureServices(services =>
        {
            services.AddScoped(_ => new DbContextOptionsBuilder<DmoDbContext>().UseNpgsql(builder.ConnectionString).AddInterceptors(conta).Options);
            services.AddScoped(sp => new DmoDbContext(sp.GetRequiredService<DbContextOptions<DmoDbContext>>()));
        }));
        var marca = "RQ" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var operatorId = Random.Shared.Next(8000, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var email = $"{operatorId}@dmo.local";
        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<DmoDbContext>();
        await db.Database.MigrateAsync();
        var maquinas = Enumerable.Range(0, 6).Select(i => new Machine { Code = $"{marca}{i}" }).ToList();
        db.Machines.AddRange(maquinas);
        var template = new Template { TemplateKey = $"tpl-{marca}", Name = marca, Capabilities = [DmoCapabilities.ReparacaoInterna] };
        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = marca, ProviderUserId = $"provider-{marca}" };
        db.Templates.Add(template);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
        var ferramentas = new List<string>();
        foreach (var m in maquinas)
        {
            var referencia = $"{marca}T";
            ferramentas.AddRange([$"{m.Code}-cm", $"{m.Code}-mf"]);
            db.Tools.AddRange(
                new Tool { ToolId = $"{m.Code}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "1" },
                new Tool { ToolId = $"{m.Code}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "1" });
            var jobOn = new JobOn { Machine = m, Reference = referencia, ProductionNumber = $"{m.Code}-1", InicioEm = Hoje.AddDays(-5), FimEm = Hoje.AddDays(2) };
            db.JobOns.Add(jobOn);
            await db.SaveChangesAsync();
            db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = $"{m.Code}-cm" });
            db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = $"{m.Code}-mf" });
        }

        await db.SaveChangesAsync();
        _factory.AuthProvider.AddAccount(email, $"provider-{marca}", "ZonasPg9999!", email);
        using var client = fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        try
        {
            var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
            using var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
                ("identifier", email), ("password", "ZonasPg9999!"), ("returnUrl", "/"),
                ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(loginHtml)!)));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            var codigo = maquinas[0].Code;
            await RegistarAsync(client, codigo, "41");
            var registo = $"/ReparacaoInterna?maquina={codigo}&tipo=cm&gravacao=ok&numero=41";
            var historico = $"/ReparacaoInterna?historico=1&maquina={codigo}";

            async Task<(int Leituras, int Linhas)> LeiturasAsync(string url, string? zonas)
            {
                await ZonasAsync(client, url, zonas); // aquece (o que se guarda em memória)
                conta.Zerar();
                using var resposta = await ZonasAsync(client, url, zonas);
                Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
                await resposta.Content.ReadAsStringAsync();
                return (conta.Vezes, conta.LinhasDasProducoes);
            }

            var inteira = await LeiturasAsync(registo, null);
            var comRegisto = await LeiturasAsync(registo, "riRegisto riAvisos");
            var soAvisos = await LeiturasAsync(registo, "riAvisos");
            var soPainel = await LeiturasAsync(registo, "riLinhas");
            var historicoInteiro = await LeiturasAsync(historico, null);
            var historicoZona = await LeiturasAsync(historico, "riHistResultados");
            _saida.WriteLine($"Registar, página inteira: {inteira.Leituras} leituras, produções de {inteira.Linhas} linhas");
            _saida.WriteLine($"Registar, riRegisto riAvisos (depois de gravar): {comRegisto.Leituras} leituras, produções de {comRegisto.Linhas} linha");
            _saida.WriteLine($"Registar, riAvisos: {soAvisos.Leituras} leituras");
            _saida.WriteLine($"Registar, riLinhas: {soPainel.Leituras} leituras, produções de {soPainel.Linhas} linhas");
            _saida.WriteLine($"Histórico, página inteira: {historicoInteiro.Leituras} leituras");
            _saida.WriteLine($"Histórico, riHistResultados: {historicoZona.Leituras} leituras");

            // O registo precisa do log e das produções da linha aberta, por isso
            // as leituras são as mesmas; deixa de ler as produções das outras linhas.
            Assert.True(inteira.Linhas >= maquinas.Count, $"a página inteira lê as produções de todas as linhas ({inteira.Linhas})");
            Assert.Equal(1, comRegisto.Linhas);
            Assert.True(comRegisto.Leituras <= inteira.Leituras, $"registo {comRegisto.Leituras} <= inteira {inteira.Leituras}");
            Assert.True(soAvisos.Leituras < inteira.Leituras, $"avisos {soAvisos.Leituras} < inteira {inteira.Leituras}");
            Assert.True(soPainel.Leituras < inteira.Leituras, $"painel {soPainel.Leituras} < inteira {inteira.Leituras}");
            Assert.True(historicoZona.Leituras < historicoInteiro.Leituras, $"histórico {historicoZona.Leituras} < inteiro {historicoInteiro.Leituras}");
        }
        finally
        {
            db.ChangeTracker.Clear();
            var ids = maquinas.Select(m => m.Id).ToList();
            var jobOns = await db.JobOns.Where(j => ids.Contains(j.MachineId)).Select(j => j.Id).ToListAsync();
            var reparacoes = await db.ReparacoesInternas.Where(r => ids.Contains(r.MachineId)).Select(r => r.Id).ToListAsync();
            var resultados = reparacoes.Select(r => (int?)r).ToList();
            await db.ReparacoesInternasRasto.Where(r => reparacoes.Contains(r.ReparacaoId)).ExecuteDeleteAsync();
            await db.OperacoesRegistadas.Where(o => o.ResultadoTipo == "Reparacao" && resultados.Contains(o.ResultadoId)).ExecuteDeleteAsync();
            await db.ReparacoesInternas.Where(r => ids.Contains(r.MachineId)).ExecuteDeleteAsync();
            await db.CmContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)).ExecuteDeleteAsync();
            await db.MfContexts.Where(c => c.JobOnId != null && jobOns.Contains(c.JobOnId.Value)).ExecuteDeleteAsync();
            await db.JobOns.Where(j => ids.Contains(j.MachineId)).ExecuteDeleteAsync();
            await db.Tools.Where(t => ferramentas.Contains(t.ToolId)).ExecuteDeleteAsync();
            await db.Machines.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync();
            await db.TemplateAssignments.Where(a => a.UserId == user.Id).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
            await db.Templates.Where(t => t.Id == template.Id).ExecuteDeleteAsync();
        }
    }

    /// <summary>Conta as leituras (SELECT) enviadas à base.</summary>
    private sealed class ContaConsultas : DbCommandInterceptor
    {
        private int _vezes;

        public int Vezes => Volatile.Read(ref _vezes);

        /// <summary>As linhas da leitura das produções do dia (0 se não leu).</summary>
        public int LinhasDasProducoes { get; private set; }

        public void Zerar()
        {
            Volatile.Write(ref _vezes, 0);
            LinhasDasProducoes = 0;
        }

        private void Ver(DbCommand command)
        {
            Interlocked.Increment(ref _vezes);
            if (command.CommandText.Contains("\"CmId\"", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Select(p => p.Value).OfType<System.Collections.ICollection>().FirstOrDefault() is { } linhas)
            {
                LinhasDasProducoes = linhas.Count;
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Ver(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Ver(command);
            return result;
        }
    }
}

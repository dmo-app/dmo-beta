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
/// A Reparação Interna, peça 4 (Diogo, 21/10/2026): no Registar, «É outra
/// CM/MF» mostra a referência e o lote e grava sem o contexto, com o aviso
/// de que falta associar; sem produção para o dia, a página avisa e grava
/// sempre assim, sem Job On, e a tabela mostra as da máquina no dia. Na
/// Consultar, as por associar levam a marca «Por associar» (que abre a
/// janela «Associar ferramenta»), a escrita na coluna Ferramenta e o filtro
/// «Só por associar»; associar grava uma vez por envio. Sem botões nas
/// linhas. A base em memória é partilhada entre classes: cada teste usa as
/// suas máquinas e ferramentas (com a marca) e o construtor limpa-as.
/// </summary>
public sealed class ReparacaoInternaOutraFerramentaPaginaTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Marca = "RIOUT";

    private readonly CustomWebApplicationFactory _factory;

    public ReparacaoInternaOutraFerramentaPaginaTests(CustomWebApplicationFactory factory)
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
        db.OperacoesRegistadas.RemoveRange(db.OperacoesRegistadas.Where(o => o.ResultadoTipo == "Reparacao" && resultados.Contains(o.ResultadoId)));
        db.ReparacoesInternasRasto.RemoveRange(db.ReparacoesInternasRasto.Where(r => ids.Contains(r.ReparacaoId)));
        db.ReparacoesInternas.RemoveRange(reparacoes);
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
        var password = $"Outra{operatorId}!";
        _factory.AuthProvider.AddAccount(email, $"provider-{operatorId}", password, email);
        NaBase(db =>
        {
            var template = new Template { TemplateKey = $"tpl-riout-{operatorId}-{Guid.NewGuid():N}", Name = $"RI outra {operatorId}", Capabilities = capabilities };
            db.Templates.Add(template);
            var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = $"Pessoa {operatorId}", ProviderUserId = $"provider-{operatorId}" };
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

    /// <summary>A referência das ferramentas «trocadas» de cada teste (só desta classe).</summary>
    private static string Trocada(string sufixo) => $"{Marca}{sufixo}T173";

    /// <summary>
    /// Uma máquina desta classe: com produção (que cobre hoje e ontem, com a
    /// CM e o MF) ou sem nenhuma; e a CM trocada registada no lote 4.
    /// </summary>
    private (string Codigo, int? JobOnId) Maquina(string sufixo, bool comProducao = true) => NaBase(db =>
    {
        var codigo = Marca + sufixo;
        var maquina = new Machine { Code = codigo };
        db.Machines.Add(maquina);
        db.Tools.Add(new Tool { ToolId = $"{codigo}-x4", Type = ToolType.Cm, Reference = Trocada(sufixo), Lot = "4", TechnicalName = "Molde técnico secreto" });
        db.SaveChanges();
        if (!comProducao)
        {
            return (codigo, (int?)null);
        }

        var referencia = $"55{sufixo}T324";
        db.Tools.AddRange(
            new Tool { ToolId = $"{codigo}-cm", Type = ToolType.Cm, Reference = referencia, Lot = "2" },
            new Tool { ToolId = $"{codigo}-mf", Type = ToolType.Mf, Reference = referencia, Lot = "2" });
        var jobOn = new JobOn { Machine = maquina, Reference = referencia, ProductionNumber = $"{sufixo}01", InicioEm = Hoje.AddDays(-2), FimEm = Hoje.AddDays(2) };
        db.JobOns.Add(jobOn);
        db.SaveChanges();
        db.CmContexts.Add(new CmContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-cm" });
        db.MfContexts.Add(new MfContext { JobOnId = jobOn.Id, ToolId = $"{codigo}-mf" });
        db.SaveChanges();
        return (codigo, (int?)jobOn.Id);
    });

    /// <summary>Uma reparação com a ferramenta escrita (por associar), gravada direto na base.</summary>
    private int Escrita(string codigo, int? jobOnId, string numero, string referencia, string lote) => NaBase(db =>
    {
        var maquina = db.Machines.AsNoTracking().Single(m => m.Code == codigo);
        var reparacao = new Reparacao
        {
            JobOnId = jobOnId, MachineId = maquina.Id, Tipo = ReparacaoTipo.Cm, Numero = numero,
            ReferenciaEscrita = referencia, LoteEscrito = lote, RegistadoEmUtc = DateTime.UtcNow, DiaOperacional = Hoje
        };
        db.ReparacoesInternas.Add(reparacao);
        db.SaveChanges();
        return reparacao.Id;
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

    private static string Campo(string html, string id) =>
        Regex.Match(html, $"<input[^>]*id=\"{id}\"[^>]*>").Value;

    /// <summary>O envio do número com «É outra CM/MF» marcado (o browser junta a caixa marcada).</summary>
    private static async Task<HttpResponseMessage> RegistarOutraAsync(HttpClient client, string codigo, string html, string numero, string referencia, string lote)
    {
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ($"riNumero-{codigo}", numero), ($"riReferencia-{codigo}", referencia), ($"riLote-{codigo}", lote));
        return await client.PostAsync(acao, EnvioDoFormulario.Corpo([.. campos, ("outra", "1")]));
    }

    [Fact]
    public async Task OCartaoDoNumero_TemAMarcaOutraCm_EOsCamposCurtos_ODaProducaoFicaIgual()
    {
        var (codigo, _) = Maquina("A");
        using var client = await LoginOperatorAsync("6801", DmoCapabilities.ReparacaoInterna);

        var cm = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        var mf = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=mf");

        var registo = Elemento(cm, $"id=\"riCard-{codigo}\"");
        Assert.Contains("É outra CM/MF</label>", registo);
        Assert.Contains($"<input type=\"checkbox\" id=\"riOutra-{codigo}\" class=\"ri-outra-check\" name=\"outra\" value=\"1\" />", registo);
        // O botão CM/MF mostra o ativo: no pedido com MF vem marcado o MF.
        Assert.Contains("checked=\"checked\"", Campo(cm, $"riTipoCm-{codigo}"));
        Assert.Contains("checked=\"checked\"", Campo(mf, $"riTipoMf-{codigo}"));
        Assert.DoesNotContain("checked", Campo(mf, $"riTipoCm-{codigo}"));
        // A referência e o lote: curtos, com as sugestões da referência; não obrigatórios sem a marca.
        Assert.Contains("data-dmo-suggest=\"referencias\"", Campo(cm, $"riReferencia-{codigo}"));
        Assert.Contains("class=\"ri-referencia\"", Campo(cm, $"riReferencia-{codigo}"));
        Assert.Contains("class=\"ri-lote\"", Campo(cm, $"riLote-{codigo}"));
        Assert.DoesNotContain("required", Campo(cm, $"riReferencia-{codigo}"));
        Assert.Contains("maxlength=\"40\"", Campo(cm, $"riReferencia-{codigo}"));
        Assert.Contains("maxlength=\"12\"", Campo(cm, $"riLote-{codigo}"));
        // A ferramenta do Job On continua a vir sozinha.
        Assert.Contains($"CM 55AT324 · lote 2", Elemento(registo, "data-dmo-ferramenta=\"cm\""));
        Assert.DoesNotContain("Molde técnico secreto", cm);
    }

    [Fact]
    public async Task GravarComOutraCm_AvisaQueFaltaAssociar_EOCampoVoltaVazioEDesmarcado()
    {
        var (codigo, jobOn) = Maquina("B");
        using var client = await LoginOperatorAsync("6802", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        using var gravar = await RegistarOutraAsync(client, codigo, html, " 0457 ", " 5447t173 ", " 4 ");

        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        var depois = await client.GetStringAsync(gravar.Headers.Location!.OriginalString);
        var aviso = Elemento(depois, "id=\"riAvisos\"");
        Assert.Contains("Reparação gravada com outra CM: 5447T173 · lote 4. Falta associar.", aviso);
        Assert.Contains("data-dmo-toast=\"aviso\"", aviso);
        Assert.DoesNotContain("value=", Campo(depois, $"riNumero-{codigo}"));
        Assert.Contains("autofocus", Campo(depois, $"riNumero-{codigo}"));
        // Volta a desmarcada, sem a referência e o lote.
        Assert.DoesNotContain("checked", Campo(depois, $"riOutra-{codigo}"));
        Assert.DoesNotContain("value=", Campo(depois, $"riReferencia-{codigo}"));
        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.JobOnId == jobOn));
        Assert.Equal(((int?)null, "5447T173", "4", "0457"), (r.CmContextId, r.ReferenciaEscrita, r.LoteEscrito, r.Numero));
        var linha = Elemento(depois, $"data-dmo-reparacao=\"{r.Id}\"");
        Assert.Contains("Outra: 5447T173 · lote 4", linha);
        Assert.DoesNotContain("<button", Elemento(depois, $"id=\"riLista-{codigo}\""));
    }

    [Fact]
    public async Task OutraSemAReferencia_RecusaNoCampo_ENadaSeGrava()
    {
        var (codigo, jobOn) = Maquina("C");
        using var client = await LoginOperatorAsync("6803", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");

        using var recusa = await RegistarOutraAsync(client, codigo, html, "12", "  ", "4");

        Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
        var pagina = await recusa.Content.ReadAsStringAsync();
        Assert.Contains("Escreva a referência da CM.", Elemento(pagina, "id=\"riAvisos\""));
        Assert.Contains("aria-invalid=\"true\"", Campo(pagina, $"riReferencia-{codigo}"));
        Assert.Contains("checked", Campo(pagina, $"riOutra-{codigo}"));
        Assert.Contains("value=\"12\"", Campo(pagina, $"riNumero-{codigo}"));
        Assert.Contains("value=\"4\"", Campo(pagina, $"riLote-{codigo}"));
        Assert.Empty(NaBase(db => db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOn).ToList()));
    }

    [Fact]
    public async Task SemProducao_GravaSemJobOn_AvisaEATabelaMostraAsDoDia()
    {
        var (codigo, _) = Maquina("D", comProducao: false);
        using var client = await LoginOperatorAsync("6804", DmoCapabilities.ReparacaoInterna);
        var html = await client.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        Assert.Contains("Ainda não há reparações sem Job On hoje.", Elemento(html, $"id=\"riLista-{codigo}\""));

        using var gravar = await EnviarAsync(client, html, ($"riNumero-{codigo}", "31"), ($"riReferencia-{codigo}", "5447t173"), ($"riLote-{codigo}", "4"));

        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        var depois = await client.GetStringAsync(gravar.Headers.Location!.OriginalString);
        Assert.Contains("Reparação gravada sem Job On, com a CM 5447T173 · lote 4. Falta associar.", Elemento(depois, "id=\"riAvisos\""));
        // Sem Job On a referência e o lote ficam para a seguinte; o número volta vazio.
        Assert.Contains("value=\"5447T173\"", Campo(depois, $"riReferencia-{codigo}"));
        Assert.Contains("value=\"4\"", Campo(depois, $"riLote-{codigo}"));
        Assert.DoesNotContain("value=", Campo(depois, $"riNumero-{codigo}"));
        var lista = Elemento(depois, $"id=\"riLista-{codigo}\"");
        Assert.Contains("Último: <strong>31</strong>", lista);
        Assert.Contains("<strong>31</strong>", lista);
        Assert.Contains("Outra: 5447T173 · lote 4", lista);
        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Numero == "31" && x.ReferenciaEscrita == "5447T173" && x.JobOnId == null));
        Assert.Equal(((int?)null, (int?)null), (r.CmContextId, r.MfContextId));

        // Sem a referência: recusa no campo, nada se grava.
        using var recusa = await EnviarAsync(client, depois, ($"riNumero-{codigo}", "32"), ($"riReferencia-{codigo}", ""));
        Assert.Contains("Escreva a referência da CM.", Elemento(await recusa.Content.ReadAsStringAsync(), "id=\"riAvisos\""));
        Assert.False(NaBase(db => db.ReparacoesInternas.Any(x => x.Numero == "32" && x.JobOnId == null && x.ReferenciaEscrita == "5447T173")));
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string html, params (string IdDoCampo, string Valor)[] preenchidos)
    {
        var (acao, campos) = EnvioDoFormulario.Preencher(html, preenchidos);
        return await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
    }

    [Fact]
    public async Task Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas()
    {
        var (codigo, jobOn) = Maquina("E");
        var (semJobOn, _) = Maquina("F", comProducao: false);
        var primeira = Escrita(codigo, jobOn, "E1", Trocada("E"), "4");
        var segunda = Escrita(semJobOn, null, "F1", Trocada("E"), "9");
        using var client = await LoginOperatorAsync("6805", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}");
        var filtro = Elemento(html, "class=\"ri-so-por-associar\"");
        Assert.Contains("Só por associar", filtro);
        Assert.Contains("type=\"checkbox\" id=\"riConsultaPorAssociar\" name=\"por_associar\" value=\"true\"", filtro);
        Assert.Contains("data-dmo-autosubmit", filtro);
        var linha = Elemento(html, $"data-dmo-reparacao=\"{primeira}\"");
        Assert.Contains($"{Trocada("E")} · lote 4 (escrita)", linha);
        Assert.Contains($"<a href=\"#riAssociar-{primeira}\" class=\"ri-por-associar\" data-dmo-dialog=\"riAssociar-{primeira}\"", linha);
        Assert.Contains(">Por associar</a>", linha);

        // Só por associar: as duas, também a sem Job On («Sem Job On» na produção).
        var porAssociar = await client.GetStringAsync("/ReparacaoInterna/Consultar?por_associar=true");
        Assert.Contains("checked=\"checked\"", Campo(porAssociar, "riConsultaPorAssociar"));
        var tabela = Elemento(porAssociar, "id=\"riConsultaLista\"");
        Assert.Contains($"data-dmo-reparacao=\"{primeira}\"", tabela);
        Assert.Contains("Sem Job On", Elemento(tabela, $"data-dmo-reparacao=\"{segunda}\""));
        Assert.Contains(">Por associar</a>", Elemento(tabela, $"data-dmo-reparacao=\"{segunda}\""));
        Assert.DoesNotContain("<button", tabela);
        Assert.Contains("class=\"dmo-table\"", tabela);
        Assert.DoesNotContain("Molde técnico secreto", porAssociar);
    }

    [Fact]
    public async Task Consultar_AJanelaAssociar_GravaUmaVez_EALinhaFicaComAFerramenta()
    {
        var (codigo, jobOn) = Maquina("G");
        var id = Escrita(codigo, jobOn, "G1", Trocada("G"), "4");
        using var client = await LoginOperatorAsync("6806", DmoCapabilities.ReparacaoInterna);
        var url = $"/ReparacaoInterna/Consultar?maquina={codigo}";
        var html = await client.GetStringAsync(url);

        var janela = Elemento(html, $"id=\"riAssociar-{id}\"");
        Assert.Contains("class=\"dmo-dialog ri-janela\"", janela);
        Assert.Contains("Associar ferramenta", janela);
        Assert.Contains($"<option value=\"{codigo}-x4\">CM {Trocada("G")} · lote 4</option>", janela);
        // Os botões no fundo da janela, Guardar por último.
        Assert.Matches("<div class=\"dmo-dialog-actions\">\\s*<button type=\"button\"[^>]*>Cancelar</button>\\s*<button type=\"submit\" class=\"dmo-button\">Guardar</button>\\s*</div>", janela);
        Assert.True(janela.IndexOf("dmo-dialog-actions", StringComparison.Ordinal) > janela.IndexOf("<select", StringComparison.Ordinal));
        Assert.Contains($"maquina={codigo}", Regex.Match(janela, "<form[^>]*action=\"([^\"]+)\"").Groups[1].Value);

        var (acao, campos) = EnvioDoFormulario.Preencher(janela, ($"riAssociarFerramenta-{id}", $"{codigo}-x4"));
        using var gravar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        using var outraVez = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, gravar.StatusCode);
        var destino = gravar.Headers.Location!.OriginalString;
        Assert.Contains($"maquina={codigo}", destino);
        var depois = await client.GetStringAsync(destino);
        Assert.Contains($"Reparação do nº G1 associada a {Trocada("G")} · lote 4.", Elemento(depois, "id=\"riConsultaAvisos\""));
        var linha = Elemento(depois, $"data-dmo-reparacao=\"{id}\"");
        Assert.Contains($"{Trocada("G")} · lote 4", linha);
        Assert.DoesNotContain("(escrita)", linha);
        Assert.DoesNotContain("Por associar", linha);
        Assert.DoesNotContain($"id=\"riAssociar-{id}\"", depois);
        var r = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Id == id));
        Assert.Equal(($"{codigo}-x4", true), (r.AssociadaToolId, r.AssociadaPorUserId is not null));
        var rasto = Assert.Single(NaBase(db => db.ReparacoesInternasRasto.AsNoTracking().Where(x => x.ReparacaoId == id).ToList()));
        Assert.Equal((ReparacaoAcao.Associado, $"{codigo}-x4"), (rasto.Acao, rasto.ToolIdAssociado));
    }

    [Fact]
    public async Task Consultar_SemFerramentaQueSirva_AJanelaAvisaComOLinkParaRegistar()
    {
        var (codigo, jobOn) = Maquina("H");
        var id = Escrita(codigo, jobOn, "H1", $"{Marca}H0000", "1");
        using var client = await LoginOperatorAsync("6807", DmoCapabilities.ReparacaoInterna, DmoCapabilities.Ferramentas);

        var html = await client.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}");

        var janela = Elemento(html, $"id=\"riAssociar-{id}\"");
        Assert.Contains($"Não há nenhuma CM {Marca}H0000 registada.", janela);
        Assert.Contains($"href=\"/Ferramentas?criar=1&amp;type=CM&amp;reference={Marca}H0000\"", janela);
        Assert.Contains(">Registar a ferramenta</a>", janela);
        Assert.DoesNotContain("<select", janela);
        Assert.DoesNotContain("Guardar", janela);
    }

    [Fact]
    public async Task AFerramentaMostrada_LevaAFicha_SoComAsFerramentas_AEscritaFicaTexto()
    {
        var (codigo, jobOn) = Maquina("J");
        var escrita = Escrita(codigo, jobOn, "J1", Trocada("J"), "4");
        var associada = Escrita(codigo, jobOn, "J2", Trocada("J"), "4");
        NaBase(db =>
        {
            var r = db.ReparacoesInternas.Single(x => x.Id == associada);
            r.AssociadaToolId = $"{codigo}-x4";
            r.AssociadaEmUtc = DateTime.UtcNow;
            var cm = db.CmContexts.AsNoTracking().Single(c => c.JobOnId == jobOn);
            db.ReparacoesInternas.Add(new Reparacao
            {
                JobOnId = jobOn, MachineId = r.MachineId, Tipo = ReparacaoTipo.Cm, CmContextId = cm.Id, Numero = "J3",
                RegistadoEmUtc = DateTime.UtcNow, DiaOperacional = Hoje
            });
            return db.SaveChanges();
        });
        using var comFicha = await LoginOperatorAsync("6808", DmoCapabilities.ReparacaoInterna, DmoCapabilities.Ferramentas);
        using var semFicha = await LoginOperatorAsync("6809", DmoCapabilities.ReparacaoInterna);
        var doJobOn = NaBase(db => db.ReparacoesInternas.AsNoTracking().Single(x => x.Numero == "J3" && x.JobOnId == jobOn).Id);

        var consulta = await comFicha.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}");
        Assert.Contains($"href=\"/Ferramentas/Ficha?tool_id={codigo}-cm\"", Elemento(consulta, $"data-dmo-reparacao=\"{doJobOn}\""));
        Assert.Contains($"href=\"/Ferramentas/Ficha?tool_id={codigo}-x4\"", Elemento(consulta, $"data-dmo-reparacao=\"{associada}\""));
        Assert.DoesNotContain("/Ferramentas/Ficha", Elemento(consulta, $"data-dmo-reparacao=\"{escrita}\""));
        var registar = await comFicha.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm");
        Assert.Contains($"<a class=\"ri-ficha\" href=\"/Ferramentas/Ficha?tool_id={codigo}-cm\">CM 55JT324 · lote 2</a>", Elemento(Elemento(registar, $"id=\"riCard-{codigo}\""), "data-dmo-ferramenta=\"cm\""));

        var semConsulta = await semFicha.GetStringAsync($"/ReparacaoInterna/Consultar?maquina={codigo}");
        Assert.DoesNotContain("/Ferramentas/Ficha", Elemento(semConsulta, "id=\"riConsultaLista\""));
        Assert.Contains("55JT324 · lote 2", Elemento(semConsulta, $"data-dmo-reparacao=\"{doJobOn}\""));
        var semRegistar = Elemento(Elemento(await semFicha.GetStringAsync($"/ReparacaoInterna?maquina={codigo}&tipo=cm"), $"id=\"riCard-{codigo}\""), "data-dmo-ferramenta=\"cm\"");
        Assert.DoesNotContain("<a", semRegistar);
        Assert.Contains("CM 55JT324 · lote 2", semRegistar);
    }

    [Fact]
    public async Task OsCartoes_BAntesDeC_ATabelaMinimizadaComOUltimo_EditarAbre()
    {
        var (codigo, jobOn) = Maquina("K");
        NaBase(db =>
        {
            var m = db.Machines.AsNoTracking().Single(x => x.Code == codigo);
            var cm = db.CmContexts.AsNoTracking().Single(c => c.JobOnId == jobOn);
            foreach (var (n, min) in new[] { ("K1", 10), ("K2", 5) })
            {
                db.ReparacoesInternas.Add(new Reparacao
                {
                    JobOnId = jobOn, MachineId = m.Id, Tipo = ReparacaoTipo.Cm, CmContextId = cm.Id, Numero = n,
                    RegistadoEmUtc = DateTime.UtcNow.AddMinutes(-min), DiaOperacional = Hoje
                });
                db.SaveChanges();
            }

            return 0;
        });
        using var client = await LoginOperatorAsync("6810", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync("/ReparacaoInterna");
        var codigos = System.Text.RegularExpressions.Regex.Matches(html, "data-ri-cartao=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Contains(codigo, codigos);
        // As B antes das C.
        var b = codigos.Select((c, i) => (c, i)).Where(x => x.c.StartsWith('B')).Select(x => x.i).DefaultIfEmpty(-1).Max();
        var c = codigos.Select((x, i) => (x, i)).Where(x => x.x.StartsWith('C')).Select(x => x.i).DefaultIfEmpty(int.MaxValue).Min();
        Assert.True(b < c);
        // Fechada: só o último registo à vista.
        var lista = Elemento(html, $"id=\"riLista-{codigo}\"");
        Assert.DoesNotContain("open", Regex.Match(lista, "<details[^>]*>").Value);
        Assert.Contains("Último: <strong>K2</strong>", lista);
        // «Editar» abre a tabela, com o número para corrigir e o «×».
        var editar = Elemento(html, $"id=\"riEditar-{codigo}\"");
        Assert.Contains("class=\"dmo-button\"", editar);
        var aberta = await client.GetStringAsync(WebUtility.HtmlDecode(Regex.Match(editar, "href=\"([^\"]+)\"").Groups[1].Value));
        var tabela = Elemento(aberta, $"id=\"riLista-{codigo}\"");
        Assert.Contains("open", Regex.Match(tabela, "<details[^>]*>").Value);
        Assert.Contains("data-ri-abre=\"riCorrigirJanela\"", tabela);
        Assert.Contains("data-ri-abre=\"riAnularJanela\"", tabela);
        Assert.DoesNotContain("<button", tabela);
    }

    [Fact]
    public async Task CorrigirParaOMesmoNumero_NadaMuda_ESemAviso()
    {
        using var client = await LoginOperatorAsync("6811", DmoCapabilities.ReparacaoInterna);

        var html = await client.GetStringAsync("/ReparacaoInterna?correcao=igual&numero=5");

        Assert.DoesNotContain("data-dmo-toast", Elemento(html, "id=\"riAvisos\""));
    }
}

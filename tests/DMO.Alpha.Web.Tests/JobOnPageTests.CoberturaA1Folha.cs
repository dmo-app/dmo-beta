using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Cobertura A1 da folha do Job On e do Planeamento: regras que a auditoria
/// marcou «de raspão» ou «não cobre» (DMO-454, 478, 430, 521, 395, 557, 506).
/// </summary>
public partial class JobOnPageTests
{
    private void CoberturaA1SeedUserCom(string operatorId, string providerUserId, params string[] capacidades)
    {
        var email = $"{operatorId}@dmo.local";
        _factory.AuthProvider.AddAccount(email, providerUserId, "Folha1234!", email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var user = new User { Id = Guid.NewGuid(), OperatorId = operatorId, Name = "Cobertura A1 " + operatorId, ProviderUserId = providerUserId };
        db.Users.Add(user);
        var template = new Template
        {
            Id = Guid.NewGuid(), TemplateKey = "tpl-cobertura-a1-" + operatorId, Name = "Cobertura A1 " + operatorId, Capabilities = capacidades
        };
        db.Templates.Add(template);
        db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
        db.SaveChanges();
    }

    private static string CoberturaA1Bloco(string html, string inicio, string fim)
    {
        var de = html.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(de >= 0, $"A página não tem {inicio}.");
        var ate = html.IndexOf(fim, de, StringComparison.Ordinal);
        Assert.True(ate >= 0, $"Depois de {inicio} não há {fim}.");
        return html[de..ate];
    }

    // DMO-454: o fim de sessão e o botão de desbloquear saíram dos ecrãs.
    [Fact]
    public async Task Folha_NaoTemOContadorDeSessaoNemOBotaoDeDesbloquearDoPainel()
    {
        SeedUser("9101", "Chefe limpeza", "provider-a1-limpeza", "Folha1234!");
        var jobOnId = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("9101", "Folha1234!");

        var html = await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}");

        // Aparece o que o painel de apoio carrega e a folha tem, para o «não aparece» valer.
        Assert.Contains("js/dmo-support.js", html, StringComparison.Ordinal);
        Assert.Contains("data-jof-imprimir>Imprimir</button>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dmo-session.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dmo-sessao-fim", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-support-unlock", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-imprimir-lista", html, StringComparison.Ordinal);
    }

    // DMO-478: o Imprimir leva as Pinças do cartão PI inteiras, em vez de só «Latão» ou «Grafite».
    [Fact]
    public async Task Imprimir_UsaAsPincasDoCartaoPiInteiras_ComODiametro_SemALinhaTipo()
    {
        SeedUser("9102", "Chefe pinças", "provider-a1-pincas", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var folha = await db.JobOnFolhas.SingleAsync(f => f.JobOnId == jobOnId);
            folha.DefinirValor("pi_pincas", "Latão duplo com anel");
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("9102", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        var impressao = Zona(html, "data-jof-impressao");

        // O bloco das Pinças do Job-ON Moldes: «Pinças» antes do «Diâmetro», e nenhuma linha «Tipo».
        var pincas = CoberturaA1Bloco(impressao, "<b>Pinças</b>", "</table>");
        Assert.Matches("<tr><th>Pinças</th><td>Latão duplo com anel</td></tr><tr><th>Diâmetro</th><td>59[.,]3\\d*</td></tr>", pincas);
        Assert.DoesNotContain("<th>Tipo</th>", pincas, StringComparison.Ordinal);
        // No Trabalho de Equipa, a pinça leva o diâmetro e, por baixo, as pinças como escritas.
        Assert.Matches(
            "<span>PI</span> Pinça</div>\\s*<div class=\"jfc-ref\"><b>Ø 59[.,]3\\d*</b></div>\\s*<div class=\"jfc-obs\">Latão duplo com anel</div>",
            impressao);
    }

    // DMO-430: o lote do AN é, por omissão, o da BQ; só muda se alguém o escrever à mão.
    private static string CoberturaA1CartaoAn(string html) =>
        CoberturaA1Bloco(html, "data-jof-familia=\"AN\"", "</article>");

    private static string CoberturaA1LoteDoAnNaImpressao(string html)
    {
        var impressao = Zona(html, "data-jof-impressao");
        return CoberturaA1Bloco(impressao, "<b>Anel</b>", "</table>");
    }

    private async Task CoberturaA1TrocarABqAsync(int jobOnId, string toolId, string lote)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = toolId, Type = ToolType.Bq, Reference = "T194", Lot = lote });
        await db.SaveChangesAsync();
        var bqId = await db.BqContexts.Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
        var alterar = scope.ServiceProvider.GetRequiredService<IAlterarLoteBq>();
        Assert.Equal(AlterarLoteResultado.Alterado, (await alterar.AlterarAsync(new AlterarLote(bqId, toolId, "Chefe anel"))).Resultado);
    }

    [Fact]
    public async Task Folha_LoteDoAnSemEscrever_ESempreODaBq_NoEcraENaImpressao_MesmoDepoisDeTrocarAsBq()
    {
        SeedUser("9103", "Chefe anel", "provider-a1-anel", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using var client = await LoginClientAsync("9103", "Folha1234!");

        async Task<string> LerAsync() => WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));

        // A BQ é a T194 lote 198 e o AN não tem lote escrito: herda o 198.
        var antes = await LerAsync();
        Assert.Matches("<small>Lote</small><b>\\s*198\\s*</b>", CoberturaA1CartaoAn(antes));
        Assert.Contains("<th>Lote</th><td>198</td>", CoberturaA1LoteDoAnNaImpressao(antes), StringComparison.Ordinal);

        // Trocada a BQ, o AN acompanha-a, uma segunda e uma terceira vez.
        await CoberturaA1TrocarABqAsync(jobOnId, "A1-BQ-205", "205");
        var depois = await LerAsync();
        Assert.Matches("<small>Lote</small><b>\\s*205\\s*</b>", CoberturaA1CartaoAn(depois));
        Assert.DoesNotMatch("<small>Lote</small><b>\\s*198\\s*</b>", CoberturaA1CartaoAn(depois));
        Assert.Contains("<th>Lote</th><td>205</td>", CoberturaA1LoteDoAnNaImpressao(depois), StringComparison.Ordinal);

        await CoberturaA1TrocarABqAsync(jobOnId, "A1-BQ-210", "210");
        var terceira = await LerAsync();
        Assert.Matches("<small>Lote</small><b>\\s*210\\s*</b>", CoberturaA1CartaoAn(terceira));
        Assert.Contains("<th>Lote</th><td>210</td>", CoberturaA1LoteDoAnNaImpressao(terceira), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Folha_LoteDoAnEscritoAMao_FicaEMantemSeQuandoATrocaDeBq()
    {
        SeedUser("9104", "Chefe anel manual", "provider-a1-anel-manual", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            (await db.JobOnFolhas.SingleAsync(f => f.JobOnId == jobOnId)).DefinirValor("an_lote", "77");
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("9104", "Folha1234!");

        var antes = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        Assert.Matches("<small>Lote</small><b>\\s*77\\s*</b>", CoberturaA1CartaoAn(antes));
        Assert.Contains("<th>Lote</th><td>77</td>", CoberturaA1LoteDoAnNaImpressao(antes), StringComparison.Ordinal);

        await CoberturaA1TrocarABqAsync(jobOnId, "A1-BQ-305", "305");
        var depois = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        Assert.Matches("<small>Lote</small><b>\\s*77\\s*</b>", CoberturaA1CartaoAn(depois));
        Assert.DoesNotMatch("<small>Lote</small><b>\\s*305\\s*</b>", CoberturaA1CartaoAn(depois));
        var naImpressao = CoberturaA1LoteDoAnNaImpressao(depois);
        Assert.Contains("<th>Lote</th><td>77</td>", naImpressao, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>305</td>", naImpressao, StringComparison.Ordinal);

        using var scope2 = _factory.Services.CreateScope();
        Assert.Equal("77", scope2.ServiceProvider.GetRequiredService<DmoDbContext>().JobOnFolhas.AsNoTracking().Single(f => f.JobOnId == jobOnId).AnLote);
    }

    [Fact]
    public async Task Editar_LoteDoAnVazioMostraODaBqComoSugestao_EGuardarOutraCoisaNaoOCopiaParaABase()
    {
        SeedUser("9105", "Chefe anel editar", "provider-a1-anel-editar", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using var client = await LoginClientAsync("9105", "Folha1234!");

        var editar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}&editar=1"));
        var campo = Regex.Match(editar, "<input[^>]*id=\"jobon-an-lote\"[^>]*>").Value;
        Assert.NotEqual(string.Empty, campo);
        Assert.Contains("placeholder=\"198\"", campo, StringComparison.Ordinal);
        Assert.DoesNotMatch("value=\"[^\"]+\"", campo);

        // Guardar outra coisa não grava o 198 no AN: ele continua a seguir a BQ.
        var (acao, campos) = EnvioDoFormulario.Preencher(editar, ("jobon-cm-folga2", "0.02"), ("jobon-mf-folga2", "0.02"), ("jobon-mf-corpo", "71.7"), ("jobon-cm-notas", "Nota escrita no Editar."));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var folha = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == jobOnId);
            Assert.Equal("Nota escrita no Editar.", folha.CmNotas);
            Assert.True(string.IsNullOrEmpty(folha.AnLote), "O lote da BQ foi copiado para o AN ao guardar.");
        }

        await CoberturaA1TrocarABqAsync(jobOnId, "A1-BQ-405", "405");
        var ver = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        Assert.Matches("<small>Lote</small><b>\\s*405\\s*</b>", CoberturaA1CartaoAn(ver));
    }

    [Fact]
    public async Task Editar_LoteDoAnEscritoNoEditar_GravaSeEFicaComoLoteDoAn()
    {
        SeedUser("9106", "Chefe anel escrever", "provider-a1-anel-escrever", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using var client = await LoginClientAsync("9106", "Folha1234!");

        var editar = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}&editar=1"));
        var (acao, campos) = EnvioDoFormulario.Preencher(editar, ("jobon-cm-folga2", "0.02"), ("jobon-mf-folga2", "0.02"), ("jobon-mf-corpo", "71.7"), ("jobon-an-lote", "88"));
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos))).StatusCode);

        using var scope = _factory.Services.CreateScope();
        Assert.Equal("88", scope.ServiceProvider.GetRequiredService<DmoDbContext>().JobOnFolhas.AsNoTracking().Single(f => f.JobOnId == jobOnId).AnLote);
        var ver = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        Assert.Matches("<small>Lote</small><b>\\s*88\\s*</b>", CoberturaA1CartaoAn(ver));
    }

    // DMO-521: no painel deslizante a folha é um resumo para consulta rápida, com «Abrir folha».
    [Fact]
    public async Task Painel_FolhaNoPainel_EUmResumoComAbrirFolha_SemOsCartoesDaFolha()
    {
        SeedUser("9107", "Chefe painel", "provider-a1-painel", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using var client = await LoginClientAsync("9107", "Folha1234!");

        var painel = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}&painel=1"));

        var abrir = CoberturaA1Bloco(painel, "<main class=\"jof-painel\"", "</main>");
        // O botão leva à folha inteira, fora do painel.
        Assert.Contains($"<a class=\"dmo-button\" href=\"/JobOn/Folha?jobon_id={jobOnId}\" target=\"_top\" data-jof-abrir-folha>Abrir folha</a>", abrir, StringComparison.Ordinal);
        // A produção: referência, nº e linha.
        Assert.Contains("<b>9395T194</b><span>202601</span>", abrir, StringComparison.Ordinal);
        Assert.Contains(">B2</span>", abrir, StringComparison.Ordinal);
        Assert.Contains("15/10/2025 → 26/10/2025", abrir, StringComparison.Ordinal);
        // A faixa: secções, gota, processo e peso.
        Assert.Contains("<div><dt>Secções</dt><dd>12</dd></div>", abrir, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Gota</dt><dd>3</dd></div>", abrir, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Processo</dt><dd>PS</dd></div>", abrir, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Peso</dt><dd>161 g</dd></div>", abrir, StringComparison.Ordinal);
        // As peças: referência e lote da CM, do MF e da BQ, e a referência do PU.
        Assert.Matches("<th scope=\"row\">CM</th>\\s*<td><b>9395</b></td>\\s*<td>34</td>", abrir);
        Assert.Matches("<th scope=\"row\">MF</th>\\s*<td><b>9395</b></td>\\s*<td>23</td>", abrir);
        Assert.Matches("<th scope=\"row\">BQ</th>\\s*<td><b>T194</b></td>\\s*<td>198</td>", abrir);
        Assert.Contains("<th scope=\"row\">PU</th><td><b>9395</b></td>", abrir, StringComparison.Ordinal);
        // As notas gerais.
        Assert.Contains("2.º fabrico na linha B2", abrir, StringComparison.Ordinal);
        // E não é a folha inteira: sem os cartões nem o Editar.
        Assert.DoesNotContain("data-jof-familia=\"CM\"", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-editar", painel, StringComparison.Ordinal);

        // Fora do painel a mesma produção abre a folha inteira.
        var inteira = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        Assert.Contains("data-jof-familia=\"CM\"", inteira, StringComparison.Ordinal);
        Assert.DoesNotContain("data-jof-abrir-folha", inteira, StringComparison.Ordinal);
    }

    // DMO-395: sem calote da produção, o TP mostra a Bacia PS da folha; com ela, a calote manda.
    [Fact]
    public async Task Folha_CaloteDoTp_SemCaloteDaProducaoMostraABaciaPs_ComElaMandaACalote()
    {
        SeedUser("9108", "Chefe calote", "provider-a1-calote", "Folha1234!");
        var jobOnId = await SeedJobOn9395T194Async();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            (await db.JobOnFolhas.SingleAsync(f => f.JobOnId == jobOnId)).DefinirValor("tp_bacia_ps", "7.20");
            (await db.JobOns.SingleAsync(j => j.Id == jobOnId)).CaloteMm = null;
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("9108", "Folha1234!");

        var semCalote = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        var tpSem = CoberturaA1Bloco(semCalote, "data-jof-familia=\"TP\"", "</article>");
        Assert.Matches("Calote</dt>\\s*<dd>\\s*7\\.20\\s*</dd>", tpSem);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            (await db.JobOns.SingleAsync(j => j.Id == jobOnId)).CaloteMm = 6.5m;
            await db.SaveChangesAsync();
        }
        var comCalote = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?jobon_id={jobOnId}"));
        var tpCom = CoberturaA1Bloco(comCalote, "data-jof-familia=\"TP\"", "</article>");
        Assert.Matches("Calote</dt>\\s*<dd>\\s*6\\.50\\s*</dd>", tpCom);
        Assert.DoesNotContain("7.20", tpCom, StringComparison.Ordinal);
    }

    // DMO-395: o Duplicar traz a folha da origem (PU, TP, calote) e grava-a com a produção nova.
    [Fact]
    public async Task Duplicar_TrazOCabecalhoDaOrigem_OPuEACalote_EGravaAProducaoNovaComEles()
    {
        SeedUser("9109", "Chefe duplicar PU", "provider-a1-duplicar", "Folha1234!");
        var origem = await SeedJobOn9395T194Async();
        using var client = await LoginClientAsync("9109", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, $"{FolhaPath}?duplicar={origem}&inicio_em=2026-11-05"));
        Assert.Contains(">Novo Job On · duplicado de 202601</span>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"reference\" type=\"text\" value=\"9395T194\"", html, StringComparison.Ordinal);
        var pu = Regex.Match(html, "<input[^>]*id=\"jobon-pu-referencia\"[^>]*>").Value;
        Assert.Contains("value=\"9395\"", pu, StringComparison.Ordinal);
        var calote = Regex.Match(html, "<input[^>]*id=\"lightCalote\"[^>]*>").Value;
        Assert.Matches("value=\"6[.,]5", calote);

        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("jobon-cm-folga2", "0.02"), ("jobon-mf-folga2", "0.02"), ("jobon-mf-corpo", "71.7"), ("lightProduction", "202699"));
        var guardar = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, guardar.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var nova = await db.JobOns.AsNoTracking().SingleAsync(j => j.ProductionNumber == "202699");
        Assert.NotEqual(origem, nova.Id);
        Assert.Equal(6.5m, nova.CaloteMm);
        var folha = await db.JobOnFolhas.AsNoTracking().SingleAsync(f => f.JobOnId == nova.Id);
        Assert.Equal("9395", folha.Valor("pu_referencia"));
        Assert.Equal("9395", folha.Valor("arr_referencia"));
        Assert.Matches("^34[.,]950*$", folha.Valor("tp_diametro_ps"));
    }

    // DMO-557: a posição do Armazém leva ao Armazém da ferramenta, a quem lá pode entrar.
    private async Task<int> CoberturaA1SeedCmNoArmazemAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.Tools.Add(new Tool { ToolId = "A1-CM", Type = ToolType.Cm, Reference = "8326", Lot = "7" });
        db.Tools.Add(new Tool { ToolId = "A1-MF", Type = ToolType.Mf, Reference = "8326", Lot = "6" });
        var jobOn = new JobOn
        {
            Reference = "8326T557",
            ProductionNumber = "202607",
            Machine = db.Machines.FirstOrDefault(m => m.Code == "C1") ?? new Machine { Code = "C1" },
            InicioEm = new DateOnly(2026, 8, 5),
        };
        db.JobOns.Add(jobOn);
        db.CmContexts.Add(new CmContext { JobOn = jobOn, ToolId = "A1-CM" });
        db.MfContexts.Add(new MfContext { JobOn = jobOn, ToolId = "A1-MF" });
        var posicao = new ArmazemPosicao { Codigo = "5252", CriadaEmUtc = DateTime.UtcNow };
        db.ArmazemPosicoes.Add(posicao);
        var entrada = new ArmazemMovimento
        {
            Tipo = ArmazemMovimentoTipo.Entrada, ToolId = "A1-CM",
            OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid()
        };
        db.ArmazemMovimentos.Add(entrada);
        await db.SaveChangesAsync();
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = "A1-CM", PosicaoId = posicao.Id, MovimentoId = entrada.Id, DesdeUtc = DateTime.UtcNow });
        db.FerramentaEstados.Add(new FerramentaEstado { ToolId = "A1-CM", Estado = FerramentaEstadoTipo.Reparado, RegistadoEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return jobOn.Id;
    }

    [Fact]
    public async Task Folha_PosicaoDoArmazem_ELinkParaOArmazemDaFerramenta_SoParaQuemTemOArmazem()
    {
        var jobOnId = await CoberturaA1SeedCmNoArmazemAsync();
        CoberturaA1SeedUserCom("9110", "provider-a1-armazem", DmoCapabilities.JobOnCreate, DmoCapabilities.ArmazemGestao);
        CoberturaA1SeedUserCom("9111", "provider-a1-sem-armazem", DmoCapabilities.JobOnCreate);

        using (var comArmazem = await LoginClientAsync("9110", "Folha1234!"))
        {
            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(comArmazem, $"{FolhaPath}?jobon_id={jobOnId}"));
            var cm = CoberturaA1Bloco(html, "data-jof-familia=\"CM\"", "data-jof-familia=\"MF\"");
            Assert.Contains(
                "<a class=\"jof-onde-pos\" href=\"/Armazem/Movimentos?ferramenta=A1-CM\" title=\"Armazém · posição 5252: dar entrada ou saída no Armazém\"><b>5252</b></a>",
                cm, StringComparison.Ordinal);
            Assert.Contains("<b>Reparado</b>", cm, StringComparison.Ordinal);
        }

        using var semArmazem = await LoginClientAsync("9111", "Folha1234!");
        var semLink = WebUtility.HtmlDecode(await GetPageHtmlAsync(semArmazem, $"{FolhaPath}?jobon_id={jobOnId}"));
        var cmSem = CoberturaA1Bloco(semLink, "data-jof-familia=\"CM\"", "data-jof-familia=\"MF\"");
        Assert.Contains("<b>5252</b>", cmSem, StringComparison.Ordinal);
        Assert.DoesNotContain("jof-onde-pos", cmSem, StringComparison.Ordinal);
        Assert.DoesNotContain("/Armazem/Movimentos", cmSem, StringComparison.Ordinal);
    }

    // DMO-506: a mesma faixa «Planeamento» na página do Job On: só o que entra, de hoje a uma semana.
    [Fact]
    public async Task Planeamento_FaixaDoJobOn_TemOQueEntraEstaSemana_ENaoOQueJaEntrou()
    {
        SeedUser("9112", "Chefe faixa", "provider-a1-faixa", "Folha1234!");
        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        int entraHoje, entraEm3Dias, jaEntrou;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b1 = new Machine { Code = "B1" };
            var b2 = new Machine { Code = "B2" };
            var b3 = new Machine { Code = "B3" };
            var a = new JobOn { Machine = b1, Reference = "A1R001", ProductionNumber = "7", InicioEm = hoje };
            var b = new JobOn { Machine = b2, Reference = "A1R002", ProductionNumber = "8", InicioEm = hoje.AddDays(3) };
            var c = new JobOn { Machine = b3, Reference = "A1R003", ProductionNumber = "9", InicioEm = hoje.AddDays(-1) };
            db.JobOns.AddRange(a, b, c);
            await db.SaveChangesAsync();
            (entraHoje, entraEm3Dias, jaEntrou) = (a.Id, b.Id, c.Id);
        }
        using var client = await LoginClientAsync("9112", "Folha1234!");

        var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, PagePath));

        var faixa = Regex.Match(html, "<aside class=\"beta-light-rail beta-production-rail\"[\\s\\S]*?</aside>").Value;
        Assert.NotEqual(string.Empty, faixa);
        Assert.Contains("href=\"/JobOn\">Planeamento</a>", faixa, StringComparison.Ordinal);
        Assert.Contains($"data-dmo-open=\"/JobOn/Folha?jobon_id={entraHoje}\"", faixa, StringComparison.Ordinal);
        Assert.Contains("A1R001", faixa, StringComparison.Ordinal);
        Assert.Contains($"<span>Entra a {hoje:dd/MM}</span>", faixa, StringComparison.Ordinal);
        Assert.Contains($"data-dmo-open=\"/JobOn/Folha?jobon_id={entraEm3Dias}\"", faixa, StringComparison.Ordinal);
        Assert.Contains($"<span>Entra a {hoje.AddDays(3):dd/MM}</span>", faixa, StringComparison.Ordinal);
        // Pela ordem em que entram.
        Assert.True(faixa.IndexOf("A1R001", StringComparison.Ordinal) < faixa.IndexOf("A1R002", StringComparison.Ordinal));
        // A que entrou ontem já não aparece (DMO-506).
        Assert.DoesNotContain($"jobon_id={jaEntrou}\"", faixa, StringComparison.Ordinal);
        Assert.DoesNotContain("A1R003", faixa, StringComparison.Ordinal);
        Assert.DoesNotContain("Em curso desde", faixa, StringComparison.Ordinal);
    }
}

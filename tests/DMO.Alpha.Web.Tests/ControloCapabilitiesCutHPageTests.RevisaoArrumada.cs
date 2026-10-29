using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Diogo, 19/10/2026, sobre a Revisão: os botões no primeiro cartão, sem o
    // título repetido, o bloco da produção chama-se «Resumo do Controlo», os
    // Pegamentos com o gráfico e sem aprovar, o aviso das ferramentas sem
    // leituras, a tabela CM a CM do Peso e o Responsável é quem entrou.
    [Fact]
    public async Task Revisao_ArrumadaComoODiogoPediu()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            // A produção anterior da mesma referência, com o seu Peso.
            var anterior = new JobOn { Reference = "5810", ProductionNumber = "J09", MachineId = (await db.Machines.FirstAsync()).Id };
            db.JobOns.Add(anterior);
            await db.SaveChangesAsync();
            var controloAnterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anterior.Id };
            db.Controlos.Add(controloAnterior);
            await db.SaveChangesAsync();
            var pesoAnterior = NovoPeso(controloAnterior.Id, null, null);
            pesoAnterior.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "41", WaterWeightGrams = 250m });
            db.Pesos.Add(pesoAnterior);
            await db.SaveChangesAsync();

            var peso = NovoPeso(controloId, controloAnterior.Id, 258m);
            peso.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "61", WaterWeightGrams = 251m });
            db.Pesos.Add(peso);
            var folha = new Pegamentos { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", MfToolId = "MF-TOOL-9", ToleranceMm = 0.20m };
            folha.Measurements.Add(new PegamentosMeasurement { RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 30m, NominalGargaloMm = 30m });
            db.Pegamentos.Add(folha);
            await db.SaveChangesAsync();
        }

        SeedUserWithTemplate("4802", "Chefe Completo", "provider-revisao-arrumada", "RevisaoArrumada1!",
            "tpl-teste-revisao-arrumada", "Chefe completo", [DmoCapabilities.ControloApprove, DmoCapabilities.ControloCreate]);
        using var client = await LoginClientAsync("4802", "RevisaoArrumada1!");
        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");

        // Os botões no primeiro cartão, numa zona própria; sem título repetido.
        var filtros = html[html.IndexOf("id=\"revisaoFiltros\"", StringComparison.Ordinal)..html.IndexOf("id=\"revisaoDetalhe\"", StringComparison.Ordinal)];
        Assert.Contains("id=\"revisaoAcoes\" data-dmo-regiao", filtros);
        // As consultas saem da barra dos filtros para o Resumo do Controlo,
        // em texto com link (Diogo, 19/10/2026: «remover daqui»).
        Assert.DoesNotContain("Consultar", filtros);
        var consultas = Regex.Match(html, "<nav class=\"revisao-consultas\" id=\"revisaoConsultas\"[\\s\\S]*?</nav>").Value;
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=controlo\">Resumo do controlo</a>", consultas);
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso\">Peso</a>", consultas);
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos\">Pegamentos</a>", consultas);
        Assert.DoesNotContain("dmo-button", consultas);
        Assert.Contains(">PDF do Resumo</button>", filtros);
        Assert.Contains(">Limpar filtros</a>", filtros);
        // Diogo, 19/10/2026: os botões à direita, na linha dos filtros
        // (tests/visual/nok_agente_browser.py mede-os no Chromium).
        ExportarParaOBrowser.Gravar("revisao-filtros.html", html);
        Assert.DoesNotContain("tipo=ferramentas", filtros);
        Assert.DoesNotContain("approve-production-title", html);
        Assert.Contains("<h3 class=\"revisao-section\">Resumo do Controlo</h3>", html);
        Assert.DoesNotContain("Identificação da produção", html);

        // Os Pegamentos: o gráfico e o resumo, o aviso das ferramentas sem
        // leituras, e nenhum Aprovar.
        Assert.Contains("Mapa de limites", html);
        Assert.Contains("Todas as medições permanecem dentro do corredor", html);
        Assert.Contains("Sem leituras de BQ, MF na folha atual.", html);
        Assert.Contains($"href=\"/Controlo/Pegamentos?controlo_id={controloId}\"", html);
        Assert.DoesNotContain("revisaoAprovar-pegamentos", html);
        Assert.Contains("id=\"revisaoAprovar-peso\"", html);

        // O Peso com a tabela CM a CM do controlo anterior.
        Assert.Contains("data-revisao-peso-comparacao", html);
        Assert.Contains("CM anterior · J09", html);
        Assert.Contains("<td>41</td>", html);

        // O Responsável é quem entrou, sem caixa para escrever.
        Assert.Contains("Responsável: <b>Chefe Completo</b>", html);
        Assert.DoesNotContain("id=\"revisao-responsavel\" value=", html);

        // A consulta do Peso: a tabela, os quadros da produção e a aprovação
        // no fim; a dos Pegamentos sem aprovação.
        var consulta = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso");
        // As leituras e a comparação CM a CM numa só tabela (20/10/2026).
        Assert.Contains("Leituras por CM e comparação com o controlo anterior", consulta);
        Assert.DoesNotContain("Comparação CM a CM", consulta);
        Assert.Single(Regex.Matches(consulta, "data-revisao-peso-comparacao"));
        Assert.Contains("<div><span>Produção</span><strong>J10</strong></div>", consulta);
        Assert.True(consulta.IndexOf("data-revisao-peso-comparacao", StringComparison.Ordinal) < consulta.IndexOf("id=\"consultaDecisao\"", StringComparison.Ordinal));
        // «Produção» e não «SAP» (Diogo, 21/10/2026: «sempre que for
        // referência a produção usar produção e não SAP»): o delta da Revisão,
        // o quadro da consulta do Peso e o Resumo do controlo.
        Assert.Matches("<span>Delta à produção anterior</span><strong>[^<]*</strong><small>Produção 258[.,]00 g</small>", html);
        Assert.DoesNotContain("SAP", TextoVisivel(html));
        Assert.Contains("<span>Peso da produção anterior</span>", consulta);
        Assert.DoesNotContain("SAP", TextoVisivel(consulta));
        var resumoDoControlo = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=controlo");
        Assert.Contains("<span>Peso da produção anterior</span>", resumoDoControlo);
        Assert.DoesNotContain("SAP", TextoVisivel(resumoDoControlo));
        var pegamentos = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos");
        Assert.DoesNotContain("consultaDecisao", pegamentos);
        Assert.Contains("<h3 class=\"revisao-section\" id=\"approve-detail-production\">Pegamentos</h3>", pegamentos);
    }

    // Diogo, 29/10/2026: «controlo aprove faltou diferença da produção, o
    // delta como tem no nominal» (DMO-595): o «Peso da produção anterior» leva
    // por baixo a diferença em g e em % ao peso médio atual, como o nominal.
    [Fact]
    public async Task Approve_PesoDaProducaoAnteriorMostraAhDiferenca()
    {
        var (_, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            // A produção anterior da mesma referência, com o seu Peso.
            var anterior = new JobOn { Reference = "5810", ProductionNumber = "J09", MachineId = (await db.Machines.FirstAsync()).Id };
            db.JobOns.Add(anterior);
            await db.SaveChangesAsync();
            var controloAnterior = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = anterior.Id };
            db.Controlos.Add(controloAnterior);
            await db.SaveChangesAsync();
            var pesoAnterior = NovoPeso(controloAnterior.Id, null, null);
            pesoAnterior.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "41", WaterWeightGrams = 250m });
            db.Pesos.Add(pesoAnterior);
            await db.SaveChangesAsync();

            var peso = NovoPeso(controloId, controloAnterior.Id, 258m);
            peso.Measurements.Add(new PesoMeasurement { RowPosition = 1, CmSubject = "61", WaterWeightGrams = 251m });
            db.Pesos.Add(peso);
            var folha = new Pegamentos { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", MfToolId = "MF-TOOL-9", ToleranceMm = 0.20m };
            folha.Measurements.Add(new PegamentosMeasurement { RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 30m, NominalGargaloMm = 30m });
            db.Pegamentos.Add(folha);
            await db.SaveChangesAsync();
        }

        SeedUserWithTemplate("4852", "Chefe Delta", "provider-approve-delta", "ApproveDelta1!",
            "tpl-teste-approve-delta", "Chefe delta", [DmoCapabilities.ControloApprove, DmoCapabilities.ControloCreate]);
        using var client = await LoginClientAsync("4852", "ApproveDelta1!");
        var padrao = "Peso da produção anterior</span><strong>[^<]*</strong><small>(?:&#x2B;|[−-])[^<]* g \\((?:&#x2B;|[−-])[^<]* %\\)</small>";
        var consulta = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso");
        Assert.Matches(padrao, consulta);
        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=controlo");
        Assert.Matches(padrao, resumo);
    }

    // Diogo, 29/10/2026: «remover pendente daqui, já aparece em baixo»
    // (DMO-597): a barra dos filtros não repete a etiqueta «Pendente».
    [Fact]
    public async Task Approve_BarraDosFiltrosNaoRepeteAEtiquetaPendente()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedUserWithTemplate("4862", "Chefe Filtros", "provider-filtros-pendente", "FiltrosPend1!",
            "tpl-teste-filtros-pendente", "Chefe filtros", [DmoCapabilities.ControloApprove, DmoCapabilities.ControloCreate]);
        using var client = await LoginClientAsync("4862", "FiltrosPend1!");
        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        var filtros = html[html.IndexOf("id=\"revisaoFiltros\"", StringComparison.Ordinal)..html.IndexOf("id=\"revisaoDetalhe\"", StringComparison.Ordinal)];
        Assert.Contains("id=\"revisaoAcoes\"", filtros);
        Assert.DoesNotContain("revisao-pill", filtros);
    }

    // Sem Peso não há nada para aprovar: nem botões nem nota, e o aviso diz
    // onde se regista (Diogo, 19/10/2026: «isto é uma aprovaçao do peso?»).
    [Fact]
    public async Task Revisao_SemPeso_NaoOfereceAprovar()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedChefe("4803", "provider-revisao-sem-peso", "RevisaoSemPeso1!");
        using var client = await LoginClientAsync("4803", "RevisaoSemPeso1!");

        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.Contains("Ainda não há Peso registado nesta produção.", html);
        Assert.DoesNotContain("revisaoAprovar-peso", html);
        Assert.DoesNotContain("revisao-nota", html);

        var consulta = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso");
        Assert.Contains("Ainda não existe Peso registado nesta produção.", consulta);
        Assert.DoesNotContain("consultaDecisao", consulta);
        Assert.DoesNotContain("revisaoAprovar-peso", consulta);
    }

    // O Resumo do controlo é um separador ao lado da Revisão (Diogo,
    // 19/10/2026: «onde podemos procurar pelos Job Ons já feitos, e o chefe
    // ver tipo histórico»): abre vazio, procura a referência, mostra as
    // produções dela e, escolhida uma, o controlo inteiro, sem botões de
    // aprovar e com as consultas em link.
    [Fact]
    public async Task ResumoDoControlo_SeparadorComProcura_MostraOControloInteiro()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(NovoPeso(controloId, null, null));
            var folha = new Pegamentos { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", MfToolId = "MF-TOOL-9", ToleranceMm = 0.20m };
            folha.Measurements.Add(new PegamentosMeasurement { RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 30m, NominalGargaloMm = 30m });
            db.Pegamentos.Add(folha);
            db.ControloAreaStates.Add(new ControloAreaState { ControloId = controloId, Area = "BQ", Status = "OK", Comment = "Boquilha limpa", UpdatedBy = "4804", UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        SeedChefe("4804", "provider-resumo-controlo", "ResumoControlo1!");
        using var client = await LoginClientAsync("4804", "ResumoControlo1!");

        var revisao = await GetPageHtmlAsync(client, "/Controlo/Approve");
        var nav = Regex.Match(revisao, "<nav class=\"tabs dmo-secondary-nav\"[\\s\\S]*?</nav>").Value;
        Assert.Contains("href=\"/Controlo/Approve/Detalhe?tipo=controlo\"", nav);
        Assert.Contains("Resumo do controlo", nav);

        var vazio = await GetPageHtmlAsync(client, "/Controlo/Approve/Detalhe?tipo=controlo");
        Assert.Contains("id=\"resumoControloProcura\"", vazio);
        // Com o espaçamento dos filtros da Revisão e a ajuda na dica, não no ecrã.
        Assert.Contains("<section class=\"dmo-card revisao-filtros-card\" aria-label=\"Procurar a produção\">", vazio);
        Assert.DoesNotContain("<p class=\"resumo-hint\">Escreva a referência", vazio);
        Assert.DoesNotContain("resumoControloProducao", vazio);
        Assert.DoesNotContain("approve-detail-production", vazio);

        var procura = await GetPageHtmlAsync(client, "/Controlo/Approve/Detalhe?tipo=controlo&referencia=5810");
        Assert.Contains($"<option value=\"{jobOnId}\">5810 · J10", procura);
        Assert.DoesNotContain("approve-detail-production", procura);

        var resumo = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?tipo=controlo&referencia=5810&jobon_id={jobOnId}");
        Assert.Contains($"<option value=\"{jobOnId}\" selected=\"selected\">5810 · J10", resumo);
        Assert.Contains("<h3 class=\"revisao-section\" id=\"approve-detail-production\">Resumo do controlo</h3>", resumo);
        // Com o toque da paleta (Diogo, 19/10/2026: «página muito branca»).
        Assert.Contains("class=\"dmo-card revisao-detail revisao-consulta revisao-resumo\"", resumo);
        foreach (var parte in new[] { "Mapa de limites", "<h3 class=\"revisao-section\">PU NNPB</h3>", "Ainda não há registo das punções", "Ferramentas e OK / NOK", "Aprovação do Peso" })
            Assert.Contains(parte, resumo);
        // Curto (Diogo, 20/10/2026: «página tem muita tabela»): o Peso só com
        // a comparação ao controlo anterior, os Pegamentos com a média por
        // componente e o gráfico, sem tabelas nem contadores; as tabelas
        // ficam nas consultas.
        Assert.DoesNotContain("<table", resumo);
        Assert.DoesNotContain("Dados do Peso", resumo);
        Assert.DoesNotMatch("\\d+ medi(ção|ções) ·", resumo);
        Assert.Contains("<span>Média do controlo anterior</span>", resumo);
        Assert.Contains("data-peg-componente=\"CM\"", resumo);
        Assert.Contains("<strong>30.00 mm</strong>", resumo);
        // As áreas em cartões, com o nome de quem gravou e as notas que abrem
        // numa janela; quem só aprova vê, não grava.
        var bq = Regex.Match(resumo, "<div class=\"resumo-area ok\" data-revisao-area=\"BQ\">[\\s\\S]*?Por [^<]*").Value;
        Assert.Contains("<strong>OK</strong>", bq);
        Assert.Contains("Boquilha limpa", bq);
        Assert.Contains("Lote 12", bq);
        Assert.Contains("Por Chefe Aprovação", bq);
        Assert.DoesNotContain("handler=AreaState", resumo);
        Assert.Contains("Por aprovar.", resumo);
        Assert.DoesNotContain("revisaoAprovar-peso", resumo);
        Assert.DoesNotContain("Voltar à revisão", resumo);
        var links = Regex.Match(resumo, "<nav class=\"revisao-consultas\" id=\"resumoControloConsultas\"[\\s\\S]*?</nav>").Value;
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso\">Peso</a>", links);
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=pegamentos\">Pegamentos</a>", links);
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=puncoes\">PU NNPB</a>", links);
    }

    // Quem regista também grava nos cartões do Resumo do controlo (Diogo,
    // 20/10/2026: «cards para poder associar link mcaliper e um campo de
    // notas»): o OK/NOK, as notas e o link voltam ao Resumo do controlo.
    [Fact]
    public async Task ResumoDoControlo_CartoesDasAreas_GravamEVoltamAoResumo()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedUserWithTemplate("4806", "Chefe Completo", "provider-resumo-cartoes", "ResumoCartoes1!",
            "tpl-teste-resumo-cartoes", "Registo e aprovação",
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]);
        using var client = await LoginClientAsync("4806", "ResumoCartoes1!");
        var pagina = $"/Controlo/Approve/Detalhe?tipo=controlo&jobon_id={jobOnId}";

        var html = await GetPageHtmlAsync(client, pagina);
        Assert.Contains("name=\"voltar\" value=\"resumo-controlo\"", html);
        // O link do mcaliper cola-se num campo da linha (DMO-524, A-0254).
        Assert.Contains("placeholder=\"Colar o link do mcaliper\" aria-label=\"Link do mcaliper CM\"", html);
        var token = await GetVerificationTokenAsync(client, pagina);

        var estado = await client.PostAsync("/Controlo/Resumo?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("estado", "NOK"), ("voltar", "resumo-controlo"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.Redirect, estado.StatusCode);
        Assert.Equal($"{pagina}#resumoControloAreas", estado.Headers.Location!.ToString());
        var notas = await client.PostAsync("/Controlo/Resumo?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("comentario", "Riscada na zona 3"), ("voltar", "resumo-controlo"),
            ("__RequestVerificationToken", token)));
        Assert.Equal($"{pagina}#resumoControloAreas", notas.Headers.Location!.ToString());
        var link = await client.PostAsync("/Controlo/Resumo?handler=Mcaliper", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("url", "https://mcaliper.example/5447"), ("voltar", "resumo-controlo"),
            ("__RequestVerificationToken", token)));
        Assert.Equal($"{pagina}#resumoControloAreas", link.Headers.Location!.ToString());
        var recusado = await client.PostAsync("/Controlo/Resumo?handler=Mcaliper", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("url", "ftp://mcaliper"), ("voltar", "resumo-controlo"),
            ("__RequestVerificationToken", token)));
        Assert.Equal($"/Controlo/Approve/Detalhe?tipo=controlo&jobon_id={jobOnId}&mcaliper=invalido#resumoControloAreas", recusado.Headers.Location!.ToString());

        var depois = await GetPageHtmlAsync(client, pagina);
        var cm = Regex.Match(depois, "<div class=\"resumo-area nok\" data-revisao-area=\"CM\">[\\s\\S]*?Por [^<]*").Value;
        Assert.Contains("<option value=\"NOK\" selected=\"selected\">NOK</option>", cm);
        Assert.Contains("Riscada na zona 3", cm);
        Assert.Contains("href=\"https://mcaliper.example/5447\" target=\"_blank\" rel=\"noopener noreferrer\" data-resumo-mcaliper-abrir=\"CM\">Medições</a>", cm);
        Assert.Contains("Por Chefe Completo", cm);
        // Sem a gravação vir daqui, o Resumo da produção continua a voltar a si.
        var semVoltar = await client.PostAsync("/Controlo/Resumo?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("estado", "OK"), ("__RequestVerificationToken", token)));
        Assert.StartsWith("/Controlo/Resumo?jobon_id=", semVoltar.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ResumoDoControlo_PuECs_EscrevemAReferenciaECmNao()
    {
        // O PU e o CS não têm ferramenta: escreve-se a referência e o lote no
        // cartão (Diogo, 20/10/2026). A CM tem a ferramenta, não se escreve.
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedUserWithTemplate("4807", "Chefe Escreve", "provider-resumo-pucs", "ResumoPuCs1!",
            "tpl-teste-resumo-pucs", "Registo e aprovação",
            [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove]);
        using var client = await LoginClientAsync("4807", "ResumoPuCs1!");
        var pagina = $"/Controlo/Approve/Detalhe?tipo=controlo&jobon_id={jobOnId}";
        var token = await GetVerificationTokenAsync(client, pagina);

        var pu = await client.PostAsync("/Controlo/Resumo?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "PU"), ("identificacao", "  PU-778 Lote 4  "), ("voltar", "resumo-controlo"),
            ("__RequestVerificationToken", token)));
        Assert.Equal($"{pagina}#resumoControloAreas", pu.Headers.Location!.ToString());
        await client.PostAsync("/Controlo/Resumo?handler=AreaState", HtmlFormHelpers.BuildFormPost(
            ("jobon_id", jobOnId.ToString()), ("area", "CM"), ("identificacao", "CM-nao-pode"), ("voltar", "resumo-controlo"),
            ("__RequestVerificationToken", token)));

        var html = await GetPageHtmlAsync(client, pagina);
        var cartaoPu = Regex.Match(html, "data-revisao-area=\"PU\">[\\s\\S]*?resumo-area-identificacao[\\s\\S]*?</form>").Value;
        Assert.Contains("name=\"identificacao\" value=\"PU-778 Lote 4\"", cartaoPu);
        var cartaoCs = Regex.Match(html, "data-revisao-area=\"CS\">[\\s\\S]*?resumo-area-identificacao[\\s\\S]*?</form>").Value;
        Assert.Contains("name=\"identificacao\"", cartaoCs);
        Assert.DoesNotContain("CM-nao-pode", html);
        // A barra do resumo leva os links dentro dela.
        Assert.Matches("<div class=\"revisao-parte-topo revisao-topo-principal\">\\s*<h3[^>]*>Resumo do controlo</h3>\\s*<nav class=\"revisao-consultas\"", html);
    }

    // O texto que se vê: sem as etiquetas, os atributos e os scripts.
    private static string TextoVisivel(string html) =>
        System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<(script|style)[\\s\\S]*?</\\1>", " "), "<[^>]+>", " "));

    private static Peso NovoPeso(int controloId, int? anteriorControloId, decimal? sap) => new()
    {
        ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB", WaterTemperatureC = 20,
        GlassDensityGramsPerCm3 = 2.5m, VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m,
        ProducaoAnteriorControloId = anteriorControloId, ProducaoAnteriorPesoGramas = sap,
        RegistadoEmUtc = DateTime.UtcNow
    };
}

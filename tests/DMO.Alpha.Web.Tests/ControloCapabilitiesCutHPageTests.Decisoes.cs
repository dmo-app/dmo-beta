using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed partial class ControloCapabilitiesCutHPageTests
{
    // Diogo, 19/10/2026: «remover aprovaçao do spegamentos, manter apenas no
    // peso». Só o Peso tem Aprovar / Não aprovar; o controlo é o do Peso.
    // Nunca bloqueia nada.
    [Fact]
    public async Task Revisao_SoOPesoSeAprova_EDecideOControlo()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }

        SeedChefe("4793", "provider-revisao-partes", "RevisaoPartes1!");
        using var client = await LoginClientAsync("4793", "RevisaoPartes1!");
        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.Contains("id=\"revisaoAprovar-peso\"", html);
        Assert.DoesNotContain("revisaoAprovar-pegamentos", html);
        Assert.DoesNotContain("revisaoAprovar-ferramentas", html);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        Task<HttpResponseMessage> Post(string handler, params (string, string)[] mais) =>
            client.PostAsync($"/Controlo/Approve?handler={handler}", HtmlFormHelpers.BuildFormPost(
                [("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token), .. mais]));
        async Task<(DMO.Alpha.Core.Controlo.Controlo Controlo, List<ControloDecisao> Decisoes)> EstadoAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            return (await db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId),
                await db.ControloDecisoes.AsNoTracking().Where(d => d.ControloId == controloId).ToListAsync());
        }

        // Aprovar o Peso: o controlo fica aprovado.
        Assert.Equal(HttpStatusCode.Redirect, (await Post("Approve", ("parte", "peso"))).StatusCode);
        var (controlo, decisoes) = await EstadoAsync();
        Assert.Equal(ControloParte.Peso, Assert.Single(decisoes).Parte);
        Assert.Equal("4793", controlo.ApprovedByOperatorId);

        // Não aprovar o Peso sem nota: nada muda e a página diz a parte.
        var semNota = await Post("NaoAprovar", ("parte", "peso"), ("nota", " "));
        Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}&nota_em_falta=1&parte=peso", semNota.Headers.Location?.ToString());
        Assert.Equal("4793", (await EstadoAsync()).Controlo.ApprovedByOperatorId);

        // Não aprovar o Peso com nota: o controlo fica não aprovado.
        await Post("NaoAprovar", ("parte", "peso"), ("nota", "Repetir o Peso."));
        (controlo, decisoes) = await EstadoAsync();
        Assert.Null(controlo.ApprovedAtUtc);
        Assert.Equal("4793", controlo.NotApprovedByOperatorId);
        Assert.Equal("Repetir o Peso.", controlo.ReviewNote);
        Assert.False(Assert.Single(decisoes).Aprovado);

        var pagina = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.Contains("Não aprovado por Chefe Aprovação", pagina);
        Assert.Contains("Repetir o Peso.", pagina);
        Assert.Contains(">Aprovar</button>", pagina);
        Assert.DoesNotContain(">Reaprovar</button>", pagina);

        // Sem parte (o Aprovar do Resumo): o Peso fica aprovado.
        await Post("Approve");
        (controlo, decisoes) = await EstadoAsync();
        Assert.True(Assert.Single(decisoes).Aprovado);
        Assert.Equal("4793", controlo.ApprovedByOperatorId);
        Assert.Contains(">Reaprovar</button>", await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}"));
    }

    // Aprovar e Não aprovar são do Controlo Aprovar: quem só tem o Controlo
    // Criar volta ao seu Controlo sem decidir nada. O mesmo envio, feito por
    // quem aprova, grava (a recusa vem da permissão, não do envio).
    [Fact]
    public async Task Decisao_SemAPermissaoDeAprovar_ERecusada_ENadaFicaGravado()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        SeedOperacional("4799", "provider-decisao-sem-aprovar", "SemAprovar1!");
        SeedChefe("4800", "provider-decisao-com-aprovar", "ComAprovar1!");
        Task<HttpResponseMessage> Post(HttpClient client, string token, string handler, params (string, string)[] mais) =>
            client.PostAsync($"/Controlo/Approve?handler={handler}", HtmlFormHelpers.BuildFormPost(
                [("jobon_id", jobOnId.ToString()), ("__RequestVerificationToken", token), .. mais]));
        async Task<(DMO.Alpha.Core.Controlo.Controlo Controlo, List<ControloDecisao> Decisoes)> EstadoAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            return (await db.Controlos.AsNoTracking().SingleAsync(c => c.Id == controloId),
                await db.ControloDecisoes.AsNoTracking().Where(d => d.ControloId == controloId).ToListAsync());
        }

        using (var operacional = await LoginClientAsync("4799", "SemAprovar1!"))
        {
            var token = await GetVerificationTokenAsync(operacional, $"/Controlo/Resumo?jobon_id={jobOnId}");
            using var aprovar = await Post(operacional, token, "Approve", ("parte", "pegamentos"));
            using var naoAprovar = await Post(operacional, token, "NaoAprovar", ("parte", "peso"), ("nota", "Repetir a pesagem."));
            foreach (var recusa in new[] { aprovar, naoAprovar })
            {
                Assert.Equal(HttpStatusCode.Redirect, recusa.StatusCode);
                Assert.Equal("/Controlo/Resumo", recusa.Headers.Location?.ToString());
            }
        }

        var (controlo, decisoes) = await EstadoAsync();
        Assert.Empty(decisoes);
        Assert.Null(controlo.ApprovedByOperatorId);
        Assert.Null(controlo.NotApprovedByOperatorId);
        Assert.Null(controlo.ReviewNote);

        using var chefe = await LoginClientAsync("4800", "ComAprovar1!");
        var chefeToken = await GetVerificationTokenAsync(chefe, $"/Controlo/Approve?jobon_id={jobOnId}");
        using (var aprovado = await Post(chefe, chefeToken, "Approve", ("parte", "pegamentos")))
            Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}", aprovado.Headers.Location?.ToString());
        using (var naoAprovado = await Post(chefe, chefeToken, "NaoAprovar", ("parte", "peso"), ("nota", "Repetir a pesagem.")))
            Assert.Equal($"/Controlo/Approve?jobon_id={jobOnId}", naoAprovado.Headers.Location?.ToString());
        (controlo, decisoes) = await EstadoAsync();
        Assert.Equal("4800", Assert.Single(decisoes).OperatorId);
        Assert.Equal("4800", controlo.NotApprovedByOperatorId);
        Assert.Equal("Repetir a pesagem.", controlo.ReviewNote);
    }

    // Diogo, 19/10/2026: «merter aprovar tambem nas paginas dos mesmos». A
    // Consulta do Peso tem o Aprovar / Não aprovar e volta à consulta; os
    // Pegamentos, o PU NNPB e o Resumo das ferramentas não se aprovam.
    [Fact]
    public async Task RevisaoConsultas_AprovarSoNoPeso()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            await db.SaveChangesAsync();
        }

        SeedChefe("4796", "provider-revisao-consulta-aprovar", "RevisaoConsulta1!");
        using var client = await LoginClientAsync("4796", "RevisaoConsulta1!");

        foreach (var tipo in new[] { "peso" })
        {
            var html = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo={tipo}");
            Assert.Contains("<main data-dmo-troca", html);
            Assert.Contains("id=\"consultaDecisao\" data-dmo-regiao", html);
            Assert.Contains($"id=\"revisaoAprovar-{tipo}\"", html);
            Assert.Contains($"<input type=\"hidden\" name=\"voltar\" value=\"{tipo}\" />", html);
            var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;

            var semNota = await client.PostAsync("/Controlo/Approve?handler=NaoAprovar", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("controlo_id", controloId.ToString()), ("voltar", tipo),
                ("parte", tipo), ("nota", ""), ("__RequestVerificationToken", token)));
            Assert.Equal($"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo={tipo}&nota_em_falta=1", semNota.Headers.Location?.ToString());

            var aprovar = await client.PostAsync("/Controlo/Approve?handler=Approve", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("controlo_id", controloId.ToString()), ("voltar", tipo),
                ("parte", tipo), ("__RequestVerificationToken", token)));
            Assert.Equal($"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo={tipo}", aprovar.Headers.Location?.ToString());
            var depois = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo={tipo}");
            Assert.Contains("Aprovado por", depois);
        }

        foreach (var tipo in new[] { "pegamentos", "puncoes", "ferramentas" })
        {
            var html = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo={tipo}");
            Assert.DoesNotContain("revisaoAprovar-", html);
            Assert.DoesNotContain("consultaDecisao", html);
        }
    }

    // O resumo do PU NNPB na Revisão mostra só o total verificado; a consulta mostra tudo.
    [Fact]
    public async Task Revisao_PuNnpb_TotalVerificados_EConsulta()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var registo = new Puncoes { ControloId = controloId, PunchReference = "PU-77", Lot = "4", UpdatedAtUtc = DateTime.UtcNow };
            registo.Inspecoes.Add(new PuncaoInspecao { Position = 1, Number = "101", Lot = "4", PunchSeen = true, CoolerSeen = true, RegisteredAtUtc = DateTime.UtcNow });
            registo.Inspecoes.Add(new PuncaoInspecao { Position = 2, Number = "102", Lot = "4", PunchSeen = true, CoolerSeen = false, RegisteredAtUtc = DateTime.UtcNow });
            db.Puncoes.Add(registo);
            await db.SaveChangesAsync();
        }

        SeedChefe("4797", "provider-revisao-pu", "RevisaoPu1!");
        using var client = await LoginClientAsync("4797", "RevisaoPu1!");
        var revisao = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.Contains("<div><span>Total verificados</span><strong>1 de 2</strong></div>", revisao);
        Assert.Contains($"href=\"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=puncoes\">PU NNPB</a>", revisao);
        Assert.DoesNotContain("Punções vistas", revisao);

        var consulta = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=puncoes");
        Assert.Contains("Consulta do PU NNPB", consulta);
        Assert.Contains("PU-77", consulta);
        Assert.Contains("<td>102</td>", consulta);
        Assert.Contains("Punções vistas", consulta);
    }

    // Diogo, 19/10/2026: «controlo aprove também deveria receber resumo».
    [Fact]
    public async Task Revisao_ExportaOResumo_ComoOResumo()
    {
        var (jobOnId, _) = await SeedRealProductionAsync();
        SeedChefe("4794", "provider-revisao-resumo-pdf", "RevisaoResumo1!");
        using var client = await LoginClientAsync("4794", "RevisaoResumo1!");
        var html = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.Contains(">PDF do Resumo</button>", html);
        Assert.Contains(">PDF completo</button>", html);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(html)!;
        foreach (var apenasResumo in new[] { "true", "false" })
        {
            var pdf = await client.PostAsync("/Controlo/Approve?handler=ResumoPdf", HtmlFormHelpers.BuildFormPost(
                ("jobon_id", jobOnId.ToString()), ("apenas_resumo", apenasResumo), ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        }
    }

    // O que é do Peso fica na Consulta do Peso; o Resumo das ferramentas tem a sua consulta.
    [Fact]
    public async Task RevisaoConsultas_ComparacaoNoPeso_EResumoDasFerramentas()
    {
        var (jobOnId, controloId) = await SeedRealProductionAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Pesos.Add(new Peso { ControloId = controloId, CmToolId = "T3", BqToolId = "BQ-1", Process = "NNPB",
                WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m });
            db.ControloAreaStates.Add(new ControloAreaState { ControloId = controloId, Area = "CM", Status = "NOK", Comment = "Riscada", UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        SeedChefe("4795", "provider-revisao-consultas", "RevisaoConsultas1!");
        using var client = await LoginClientAsync("4795", "RevisaoConsultas1!");
        var revisao = await GetPageHtmlAsync(client, $"/Controlo/Approve?jobon_id={jobOnId}");
        Assert.DoesNotContain("Comparação global de peso", revisao);
        Assert.DoesNotContain("Temperatura da água", revisao);
        Assert.Contains("<strong class=\"revisao-alerta\">NOK</strong><small>Riscada</small>", revisao);

        var peso = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=peso");
        Assert.Contains("Comparação global de peso", peso);
        Assert.Contains("Temperatura da água", peso);

        var ferramentas = await GetPageHtmlAsync(client, $"/Controlo/Approve/Detalhe?controlo_id={controloId}&tipo=ferramentas");
        Assert.Contains("Consulta do Resumo das ferramentas", ferramentas);
        Assert.Contains("<div><span>CM</span><strong>5447</strong><small>Lote 3</small></div>", ferramentas);
        Assert.Contains("Riscada", ferramentas);
    }
}

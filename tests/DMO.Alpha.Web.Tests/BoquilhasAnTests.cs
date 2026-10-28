using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DMO.Alpha.Web.Tests;

// OS AN (Diogo, 28/10/2026: «vamos passar a registar AN. Aplicar um filtro
// para os movimentos das BQ para sabermos onde param os AN de cada lote»):
// a ficha da BQ tem a quantidade de AN do lote, ao lado da das BQ; cada
// movimento pode levar AN; o Registo mostra os AN por baixo das BQ e os
// Movimentos filtram só os que têm AN.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task An_NaFichaDaBq_GravaEMostraBqEAn()
    {
        var tool = await SeedToolAsync("BQ-AN-FICHA", "AN173", "4");
        SeedUser("4381", "AN Ficha", "provider-an-ficha", "AnFicha1!", withFerramentas: true);
        using var client = await LoginClientAsync("4381", "AnFicha1!");

        var ficha = await GetPageHtmlAsync(client, $"/Ferramentas/Ficha?tool_id={tool.ToolId}");
        Assert.Contains("<label for=\"ferr-edit-an\">Qtd. AN</label>", ficha);

        var (acao, campos) = EnvioDoFormulario.Enviar(ficha,
            [("ferr-edit-qty", "120"), ("ferr-edit-an", "180")]);
        Assert.Contains("handler=EditFacts", acao);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("facts_refusal", response.Headers.Location!.OriginalString);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var gravada = await db.Tools.AsNoTracking().SingleAsync(t => t.ToolId == tool.ToolId);
            Assert.Equal(120, gravada.BqAccountedQuantity);
            Assert.Equal(180, gravada.AnQuantity);
        }

        var depois = await GetPageHtmlAsync(client, $"/Ferramentas/Ficha?tool_id={tool.ToolId}");
        Assert.Contains("<span>Quantidade</span><strong>120 BQ · 180 AN</strong>", depois);
        Assert.Contains("id=\"ferr-edit-an\" name=\"an_quantity\" type=\"text\" inputmode=\"numeric\" value=\"180\"", depois);

        // A ficha da BQ nas Boquilhas também diz os AN do lote.
        var boquilhas = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&ferramenta={tool.ToolId}");
        Assert.Contains("<span>Quantidade do lote</span><strong>120 BQ · 180 AN</strong>", boquilhas);
    }

    [Fact]
    public async Task An_NoRegisto_GravaComOMovimento_EContaOsAnFora()
    {
        await SeedToolAsync("BQ-AN-REG", "AN174", "7");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var tool = await db.Tools.SingleAsync(t => t.ToolId == "BQ-AN-REG");
            tool.BqAccountedQuantity = 24;
            tool.AnQuantity = 30;
            await db.SaveChangesAsync();
        }
        var bq = await SeedProductionAsync("BQ-AN-REG", "5447AN174", "202640", "B1");
        var trace = await SeedTraceAsync(null, bq.Id);
        SeedUser("4382", "AN Registo", "provider-an-registo", "AnRegisto1!");
        using var client = await LoginClientAsync("4382", "AnRegisto1!");

        async Task RegistarAsync(string tipo, string bqs, string an)
        {
            var page = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
            var (acao, campos) = EnvioDoFormulario.Enviar(page,
                [("quantity", bqs), ("quantityAn", an)],
                radios: [("movement_type", tipo)]);
            Assert.Contains("handler=RegisterMovement", acao);
            using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.DoesNotContain("movement_refusal", response.Headers.Location!.OriginalString);
        }

        // Saem 4 BQ com 6 AN; voltam 4 BQ com 2 AN; sai 1 BQ sem AN.
        await RegistarAsync("saida", "4", "6");
        await RegistarAsync("entrada", "4", "2");
        await RegistarAsync("saida", "1", "");

        var movimentos = await ReadMovementsAsync(trace.Id);
        Assert.Equal([6, 2, null], movimentos.Select(m => m.AnQuantity));

        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        // A tabela tem a coluna dos AN.
        Assert.Contains("<th>Qtd. BQ</th><th>Qtd. AN</th>", html);
        // Os valores: as BQ e, por baixo, os AN (30 do lote, 4 fora, 26 na fábrica).
        Assert.Contains("<div class=\"lot-data bq-com-an\">", html);
        Assert.Contains("<span>Quantidade do lote</span><strong>24<small class=\"bq-peca\">BQ</small><span class=\"bq-an\">30<small class=\"bq-peca\">AN</small></span></strong>", html);
        Assert.Contains("<span>Fabricação</span><strong>23<small class=\"bq-peca\">BQ</small><span class=\"bq-an\">26<small class=\"bq-peca\">AN</small></span></strong>", html);
        Assert.Contains("<span>Em reparação</span><strong>1<small class=\"bq-peca\">BQ</small><span class=\"bq-an\">4<small class=\"bq-peca\">AN</small></span></strong>", html);
    }

    [Fact]
    public async Task An_IlegivelOuNegativo_NaoGravaNada_EOFormularioVoltaComOQueSeEscreveu()
    {
        await SeedToolAsync("BQ-AN-MAU", "AN175", "1");
        var bq = await SeedProductionAsync("BQ-AN-MAU", "5447AN175", "202641", "B2");
        var trace = await SeedTraceAsync(null, bq.Id);
        SeedUser("4383", "AN Recusa", "provider-an-recusa", "AnRecusa1!");
        using var client = await LoginClientAsync("4383", "AnRecusa1!");

        foreach (var an in new[] { "abc", "-3" })
        {
            var token = await GetVerificationTokenAsync(client, TraceUrl(trace.Id));
            using var response = await client.PostAsync($"{PagePath}?handler=RegisterMovement", HtmlFormHelpers.BuildFormPost(
                ("trace_id", trace.Id.ToString()), ("movement_type", "saida"), ("quantity", "2"),
                ("quantity_an", an), ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location!.OriginalString;
            Assert.Contains("movement_refusal=InvalidAnQuantity", location);

            var html = WebUtility.HtmlDecode(await GetPageHtmlAsync(client, location));
            Assert.Contains("Indique a quantidade de AN como um número inteiro, ou deixe em branco. Nenhum movimento foi registado.", html);
            Assert.Contains($"id=\"quantityAn\" name=\"quantity_an\" type=\"number\" lang=\"en\" min=\"0\" placeholder=\"Ex.: 1\" value=\"{an}\"", html);
        }

        Assert.Empty(await ReadMovementsAsync(trace.Id));
    }

    [Fact]
    public async Task An_RegistarSemJobOn_GravaOsAn_EAFichaMostraAColuna()
    {
        await SeedToolAsync("BQ-AN-SEMJOB", "AN176", "2");
        SeedUser("4384", "AN Sem Job", "provider-an-semjob", "AnSemJob1!");
        using var client = await LoginClientAsync("4384", "AnSemJob1!");

        var page = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1");
        Assert.Contains("<label for=\"bqSemJobOnAn\">Qtd. AN</label>", page);
        var (acao, campos) = EnvioDoFormulario.Enviar(page,
            [("bqSemJobOnBq", "BQ-AN-SEMJOB"), ("bqSemJobOnQtd", "3"), ("bqSemJobOnAn", "5"), ("bqSemJobOnData", "2026-10-02T09:00")],
            radios: [("movement_type", "saida")]);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("sem_jobon=ok", response.Headers.Location!.OriginalString);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var movimento = await db.BqMovements.AsNoTracking().SingleAsync(m => m.Trace.ToolId == "BQ-AN-SEMJOB");
            Assert.Equal(3, movimento.Quantity);
            Assert.Equal(5, movimento.AnQuantity);
        }

        var depois = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("<th>Qtd.</th><th>Qtd. AN</th><th>Linha</th>", depois);
        Assert.Contains("<td>02/10/2026 09:00</td><td>Saída</td><td>3</td><td>5</td>", depois);
    }

    [Fact]
    public async Task An_NosMovimentos_OFiltroPecaAn_MostraSoOsMovimentosComAn()
    {
        await SeedToolAsync("BQ-AN-HIST", "AN177", "3");
        var bq = await SeedProductionAsync("BQ-AN-HIST", "5447AN177", "202642", "B3");
        var trace = await SeedTraceAsync(null, bq.Id);
        using (var scope = _factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IRegisterBqMovementHandler>();
            Assert.Equal(RegisterBqMovementResult.Success,
                (await handler.HandleAsync(new RegisterBqMovement(trace.Id, "saida", 2, AnQuantity: 8))).Result);
            Assert.Equal(RegisterBqMovementResult.Success,
                (await handler.HandleAsync(new RegisterBqMovement(trace.Id, "saida", 1))).Result);
        }
        SeedUser("4385", "AN Movimentos", "provider-an-hist", "AnHist1!");
        using var client = await LoginClientAsync("4385", "AnHist1!");

        var todos = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&vista=movimentos&pesquisa=AN177");
        Assert.Contains("<label for=\"peca\">Peça</label>", todos);
        Assert.Contains("<th>Qtd.</th><th>Qtd. AN</th>", todos);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(todos, "<td>AN177 · Lote 3</td>").Count);

        var soAn = await GetPageHtmlAsync(client, $"{PagePath}?historico=1&vista=movimentos&pesquisa=AN177&peca=an");
        Assert.Contains("<option value=\"an\" selected=\"selected\">AN</option>", soAn);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(soAn, "<td>AN177 · Lote 3</td>"));
        Assert.Contains("<td>2</td>\n                          <td>8</td>", soAn.Replace("\r\n", "\n"));
    }
}

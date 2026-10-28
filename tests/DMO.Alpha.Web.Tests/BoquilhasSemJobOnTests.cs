using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DMO.Alpha.Web.Tests;

// «Registar sem Job On» (Diogo, 22/10/2026): na página Boquilhas, uma janela
// com a BQ e o lote, o movimento, a quantidade, a data e hora e «Registar».
// O movimento fica na ficha da BQ, nos «Movimentos sem Job On»; o Registo é
// só para os registos de um Job On.
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task RegistarSemJobOn_NaPaginaBoquilhas_GravaEMostraOMovimento()
    {
        await SeedToolAsync("BQ-SEMJOB-1", "QS173", "5");
        await SeedToolAsync("BQ-SEMJOB-2", "QS173", "6");
        int c2Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var c2 = new DMO.Alpha.Core.Production.Machine { Code = "C2" };
            db.Machines.AddRange(new DMO.Alpha.Core.Production.Machine { Code = "B1" }, c2);
            await db.SaveChangesAsync();
            c2Id = c2.Id;
        }
        SeedUser("4291", "Registo Sem Job", "provider-registo-semjob", "RegistoSemJob1!");
        using var client = await LoginClientAsync("4291", "RegistoSemJob1!");

        // O Registo não tem a janela; a página Boquilhas tem.
        Assert.DoesNotContain("bqSemJobOnDialog", await GetPageHtmlAsync(client, PagePath));
        var page = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1");
        Assert.Contains("<button type=\"button\" class=\"bq-sem-jobon-link\" data-dmo-dialog=\"bqSemJobOnDialog\">Registar sem Job On</button>", page);
        // Uma só escolha: cada opção é uma BQ (tool_id) com o que a identifica.
        Assert.Contains("<select id=\"bqSemJobOnBq\" name=\"tool_id\" data-bq-escolha required>", page);
        Assert.Contains("<option value=\"BQ-SEMJOB-1\" data-procura=\"qs173 · lote 5\">QS173 · Lote 5</option>", page);
        Assert.Contains("<option value=\"BQ-SEMJOB-2\" data-procura=\"qs173 · lote 6\">QS173 · Lote 6</option>", page);
        Assert.DoesNotContain("bqSemJobOnLote", page);
        // A linha (Diogo, 22/10/2026): ajuda a distinguir a BQ.
        Assert.Contains($"<option value=\"{c2Id}\">C2</option>", page);

        var (acao, campos) = EnvioDoFormulario.Enviar(page,
            [("bqSemJobOnBq", "BQ-SEMJOB-2"), ("bqSemJobOnLinha", c2Id.ToString()), ("bqSemJobOnQtd", "4"), ("bqSemJobOnData", "2026-10-01T08:00")],
            radios: [("movement_type", "entrada")]);
        Assert.Contains("handler=RegistarSemJobOn", acao);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains("ficheiros=1", location);
        Assert.Contains("ferramenta=BQ-SEMJOB-2", location);
        Assert.DoesNotContain("trace_id", location);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var movimento = await db.BqMovements.AsNoTracking().Include(m => m.Trace).SingleAsync();
            Assert.Equal("BQ-SEMJOB-2", movimento.Trace.ToolId);
            Assert.Null(movimento.Trace.BqContextId);
            Assert.Equal(BqMovementType.Entrada, movimento.Type);
            Assert.Equal(4, movimento.Quantity);
            Assert.Equal("C2", movimento.MachineCodeFrozen);
        }

        var depois = await GetPageHtmlAsync(client, location);
        Assert.Contains("Movimento registado sem Job On.", depois);
        Assert.Contains("<td>01/10/2026 08:00</td><td>Entrada</td><td>4</td><td>C2</td>", depois);
        // Com um movimento, a opção passa a mostrar a última linha dela.
        Assert.Contains("<option value=\"BQ-SEMJOB-2\" data-procura=\"qs173 · lote 6 · última linha c2\" selected=\"selected\">QS173 · Lote 6 · Última linha C2</option>", depois);
    }

    [Fact]
    public async Task RegistarSemJobOn_ALinhaSoTemAsDaFichaDaBq_EVemJaEscolhida()
    {
        // Diogo, 28/10/2026: «escolhi uma para C1 mete logo C1; se for C1 B2,
        // aparecer C1 e depois apenas poder filtrar pela C1 ou B2».
        await SeedToolAsync("BQ-LINHAS-1", "QS301", "1");
        await SeedToolAsync("BQ-LINHAS-2", "QS302", "1");
        int b2Id, c1Id, c3Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b2 = new DMO.Alpha.Core.Production.Machine { Code = "B2" };
            var c1 = new DMO.Alpha.Core.Production.Machine { Code = "C1" };
            var c3 = new DMO.Alpha.Core.Production.Machine { Code = "C3" };
            db.Machines.AddRange(b2, c1, c3);
            await db.SaveChangesAsync();
            (b2Id, c1Id, c3Id) = (b2.Id, c1.Id, c3.Id);
            db.ToolMachineLinks.AddRange(
                new DMO.Alpha.Core.Tools.ToolMachineLink { ToolId = "BQ-LINHAS-1", MachineId = c1Id },
                new DMO.Alpha.Core.Tools.ToolMachineLink { ToolId = "BQ-LINHAS-2", MachineId = c1Id },
                new DMO.Alpha.Core.Tools.ToolMachineLink { ToolId = "BQ-LINHAS-2", MachineId = b2Id });
            await db.SaveChangesAsync();
        }
        SeedUser("4295", "Registo Linhas", "provider-registo-linhas", "RegistoLinhas1!");
        using var client = await LoginClientAsync("4295", "RegistoLinhas1!");

        // Cada BQ diz as linhas da ficha (o JavaScript filtra a Linha ao escolher).
        var semEscolha = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1");
        Assert.Contains("data-linhas=\"C1\"", semEscolha);
        Assert.Contains("data-linhas=\"B2 C1\"", semEscolha);
        Assert.Contains("data-bq-linhas-da=\"bqSemJobOnBq\"", semEscolha);

        // Com a BQ escolhida: só C1, já escolhida.
        var soC1 = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&ferramenta=BQ-LINHAS-1");
        Assert.Contains($"<option value=\"{c1Id}\" selected=\"selected\">C1</option>", soC1);
        Assert.Contains($"<option value=\"{b2Id}\" hidden=\"hidden\" disabled=\"disabled\">B2</option>", soC1);
        Assert.Contains($"<option value=\"{c3Id}\" hidden=\"hidden\" disabled=\"disabled\">C3</option>", soC1);

        // C1 e B2: as duas, com a primeira já escolhida; a C3 não.
        var duas = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&ferramenta=BQ-LINHAS-2");
        Assert.Contains($"<option value=\"{b2Id}\" selected=\"selected\">B2</option>", duas);
        Assert.Contains($"<option value=\"{c1Id}\">C1</option>", duas);
        Assert.Contains($"<option value=\"{c3Id}\" hidden=\"hidden\" disabled=\"disabled\">C3</option>", duas);
    }

    [Fact]
    public void RegistarSemJobOn_ALinhaPropostaEAUltimaOndeTeveMovimentos_SeForDaFicha()
    {
        Assert.Equal(["C1", "B2"], DMO.Alpha.Web.Pages.Boquilhas.IndexModel.LinhasPelaUltima(["B2", "C1"], "C1"));
        Assert.Equal(["B2", "C1"], DMO.Alpha.Web.Pages.Boquilhas.IndexModel.LinhasPelaUltima(["B2", "C1"], "C3"));
        Assert.Equal(["B2", "C1"], DMO.Alpha.Web.Pages.Boquilhas.IndexModel.LinhasPelaUltima(["B2", "C1"], null));
    }

    [Fact]
    public async Task RegistarSemJobOn_NumJobOnAnterior_FicaNoRegistoDesseJobOn_EODaEntradaDizQuemRegistou()
    {
        // Diogo, 28/10/2026: «boquilhas que chegaram atrasadas e não consigo
        // registar num Job On já feito»; «no reparador meter nome do user que dá entrada».
        await SeedToolAsync("BQ-ANT-1", "QS501", "1");
        await SeedToolAsync("BQ-ANT-2", "QS502", "1");
        var feito = await SeedProductionAsync("BQ-ANT-1", "8326T501", "P-777", "C7");
        var doOutro = await SeedProductionAsync("BQ-ANT-2", "9999T502", "P-888", "C8");
        int c7Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            c7Id = await db.Machines.Where(m => m.Code == "C7").Select(m => m.Id).SingleAsync();
        }
        SeedUser("4296", "Registo Anterior", "provider-registo-anterior", "RegistoAnterior1!");
        using var client = await LoginClientAsync("4296", "RegistoAnterior1!");

        // Com a BQ escolhida, as referências em que trabalhou; a produção abre ao escolher a referência.
        var page = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&ferramenta=BQ-ANT-1");
        Assert.Contains("<option value=\"8326T501\" data-bq=\"BQ-ANT-1\">8326T501</option>", page);
        Assert.Contains("<option value=\"9999T502\" data-bq=\"BQ-ANT-2\" hidden=\"hidden\" disabled=\"disabled\">9999T502</option>", page);
        Assert.Contains($"<option value=\"{feito.Id}\" data-bq=\"BQ-ANT-1\" data-referencia=\"8326T501\" data-linha=\"{c7Id}\" hidden>P-777 · C7</option>", page);
        Assert.Contains("<select id=\"bqSemJobOnProducao\" name=\"bq_id\" disabled data-bq-producoes>", page);

        async Task<string> RegistarAsync(int bqId)
        {
            var (acao, campos) = EnvioDoFormulario.Enviar(page,
                [("bqSemJobOnBq", "BQ-ANT-1"), ("bqSemJobOnLinha", c7Id.ToString()), ("bqSemJobOnQtd", "6"), ("bqSemJobOnData", "2026-10-27T15:59")],
                radios: [("movement_type", "entrada")]);
            using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo([.. campos, ("bq_id", bqId.ToString())]));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return response.Headers.Location!.OriginalString;
        }

        // Um Job On de outra BQ é recusado e não grava nada.
        Assert.Contains("sem_jobon=JobOnErrado", await RegistarAsync(doOutro.Id));
        var location = await RegistarAsync(feito.Id);
        Assert.Contains("sem_jobon=ok_jobon", location);
        Assert.Contains("Movimento registado no Job On da produção P-777.", await GetPageHtmlAsync(client, location));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var movimento = await db.BqMovements.AsNoTracking().Include(m => m.Trace).SingleAsync();
            Assert.Equal(feito.Id, movimento.Trace.BqContextId);
            Assert.Equal(BqMovementType.Entrada, movimento.Type);
            Assert.Equal(6, movimento.Quantity);
        }

        // No registo do Job On, o Reparador da entrada é quem a registou.
        var registo = await GetPageHtmlAsync(client, $"{PagePath}?bq_id={feito.Id}");
        Assert.Contains("<td>Entrada</td><td>6</td><td>—</td><td>C7</td><td>Registo Anterior</td>", registo.Replace("\n", "").Replace("                ", ""));
    }

    [Fact]
    public async Task RegistarSemJobOn_SemQuantidade_ReabreAJanelaComAFrase()
    {
        await SeedToolAsync("BQ-SEMJOB-3", "QS174", "1");
        SeedUser("4292", "Registo Sem Job 2", "provider-registo-semjob-2", "RegistoSemJob2!");
        using var client = await LoginClientAsync("4292", "RegistoSemJob2!");
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?ficheiros=1");
        using var response = await client.PostAsync($"{PagePath}?handler=RegistarSemJobOn", HtmlFormHelpers.BuildFormPost(
            ("tool_id", "BQ-SEMJOB-3"), ("movement_type", "saida"), ("quantity", "0"), ("__RequestVerificationToken", token)));
        var depois = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains("data-dmo-dialog-aberta=\"1\"", depois);
        Assert.Contains("Indique a quantidade como um número inteiro igual ou superior a 1.", depois);
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<DmoDbContext>().BqMovements.AnyAsync());
    }

    [Fact]
    public void SemJobOnTool_Label_MostraOQueIdentificaABq_PelaReferencia()
    {
        var tool = new DMO.Alpha.Web.Pages.Boquilhas.SemJobOnTool("BQ-X", "QS173", "6", 120, "C1 · C2", "5447T173", "C2");
        Assert.Equal("QS173 · Lote 6 · Qtd 120 · Máq. C1 · C2 · MF 5447T173 · Última linha C2", tool.Label);
        Assert.Equal("QS173 · Lote 5", new DMO.Alpha.Web.Pages.Boquilhas.SemJobOnTool("BQ-Y", "QS173", "5").Label);
    }
}

using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// DMO-572 (A-0263): em «Registar sem Job On» a Linha já vem com a última
/// onde a BQ teve movimentos, se for da ficha; e uma BQ sem linhas na ficha
/// continua a mostrar todas.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task RegistarSemJobOn_ALinhaVemJaEscolhidaComAUltimaOndeABqTeveMovimentos()
    {
        await SeedToolAsync("BQ-ULT-1", "QU301", "1");
        int b2Id, c1Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b2 = new Machine { Code = "B2" };
            var c1 = new Machine { Code = "C1" };
            db.Machines.AddRange(b2, c1);
            await db.SaveChangesAsync();
            (b2Id, c1Id) = (b2.Id, c1.Id);
            db.ToolMachineLinks.AddRange(
                new ToolMachineLink { ToolId = "BQ-ULT-1", MachineId = b2Id },
                new ToolMachineLink { ToolId = "BQ-ULT-1", MachineId = c1Id });
            await db.SaveChangesAsync();
        }
        SeedUser("4296", "Registo Ultima", "provider-registo-ultima", "RegistoUltima1!");
        using var client = await LoginClientAsync("4296", "RegistoUltima1!");

        // Sem movimentos, a primeira da ficha (B2) vem escolhida.
        var antes = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&ferramenta=BQ-ULT-1");
        Assert.Contains($"<option value=\"{b2Id}\" selected=\"selected\">B2</option>", antes);
        Assert.Contains($"<option value=\"{c1Id}\">C1</option>", antes);
        // A janela vai para o Chromium (tests/visual/cobertura_a2_browser.py), que mede a disposição dos campos.
        ExportarParaOBrowser.Gravar("cobertura-a2-bq-sem-jobon.html", antes);

        // Um movimento na C1 (a segunda da ficha).
        var (acao, campos) = EnvioDoFormulario.Enviar(antes,
            [("bqSemJobOnBq", "BQ-ULT-1"), ("bqSemJobOnLinha", c1Id.ToString()), ("bqSemJobOnQtd", "2"), ("bqSemJobOnData", "2026-10-01T08:00")],
            radios: [("movement_type", "saida")]);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Agora a C1 vem escolhida (a última com movimentos) e a B2 continua à escolha.
        var depois = await GetPageHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains($"<option value=\"{c1Id}\" selected=\"selected\">C1</option>", depois);
        Assert.Contains($"<option value=\"{b2Id}\">B2</option>", depois);
        Assert.DoesNotContain($"<option value=\"{b2Id}\" selected", depois);
    }

    [Fact]
    public async Task RegistarSemJobOn_UmaBqSemLinhasNaFicha_MostraTodasAsLinhasSemNenhumaEscolhida()
    {
        await SeedToolAsync("BQ-SEMLIN-1", "QL301", "1");
        int b2Id, c1Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b2 = new Machine { Code = "B2" };
            var c1 = new Machine { Code = "C1" };
            db.Machines.AddRange(b2, c1);
            await db.SaveChangesAsync();
            (b2Id, c1Id) = (b2.Id, c1.Id);
        }
        SeedUser("4297", "Registo Sem Linhas", "provider-registo-semlinhas", "RegistoSemLin1!");
        using var client = await LoginClientAsync("4297", "RegistoSemLin1!");

        var html = await GetPageHtmlAsync(client, $"{PagePath}?ficheiros=1&ferramenta=BQ-SEMLIN-1");

        var janela = html[html.IndexOf("id=\"bqSemJobOnLinha\"", StringComparison.Ordinal)..];
        janela = janela[..janela.IndexOf("</select>", StringComparison.Ordinal)];
        Assert.Contains($"<option value=\"{b2Id}\">B2</option>", janela);
        Assert.Contains($"<option value=\"{c1Id}\">C1</option>", janela);
        Assert.DoesNotContain("disabled", janela);
        Assert.DoesNotContain("selected", janela);
    }
}

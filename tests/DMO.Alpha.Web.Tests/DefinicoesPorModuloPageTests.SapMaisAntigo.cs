using System.Text.Json;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O «Ler pasta» envia todos os ficheiros da pasta do SAP (Diogo,
/// 27/10/2026: só o mais recente deixava o das produções por ler). Um
/// ficheiro mais antigo do que o último lido do mesmo tipo não grava: uma
/// lista velha não põe uma % antiga por cima da de hoje, nem um relatório
/// velho as garrafas de antes. O mesmo ficheiro outra vez continua a ler-se.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task UmFicheiroDoSapMaisAntigoDoQueOUltimoLido_NaoGrava()
    {
        var nome = "MUS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPPSX";
        var toolId = SeedToolComNomeTecnico(nome, "11");
        var (_, referencia, maquina) = SeedJobOnComCm();
        var client = await LoginOperatorAsync("7111", DmoCapabilities.Ferramentas);
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(await (await client.GetAsync("/Definicoes")).Content.ReadAsStringAsync())!;
        // Bem depois dos outros testes: 2099 é o último lido, 2098 é mais antigo.
        const string Novo = "4070908800000";
        const string Velho = "4039372800000";
        async Task<(bool Gravado, string Mensagem)> Enviar(string conteudo, string ficheiro, string modificado)
        {
            var resposta = await client.PostAsync($"{FerramentasPath}?handler=ImportarUsoSap", HtmlFormHelpers.BuildFormPost(
                ("conteudo", conteudo), ("ficheiro", ficheiro), ("modificado", modificado), ("__RequestVerificationToken", token)));
            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            return (json.RootElement.GetProperty("gravado").GetBoolean(), json.RootElement.GetProperty("mensagem").GetString()!);
        }

        try
        {
            Assert.True((await Enviar(ListaDoSap(nome + "11", "40,5"), "dmo-uso-sap.csv", Novo)).Gravado);
            Assert.True((await Enviar(RelatorioDeFabrico(referencia + "BRA", maquina), "dmo-fabrico-sap.txt", Novo)).Gravado);

            Assert.Equal((false, "O ficheiro «velho.html» é mais antigo do que o último lido do mesmo tipo: nada foi gravado."),
                await Enviar(ListaDoSap(nome + "11", "12,0"), "velho.html", Velho));
            Assert.Equal((false, "O ficheiro «producoes-velhas.txt» é mais antigo do que o último lido do mesmo tipo: nada foi gravado."),
                await Enviar(RelatorioDeFabrico(referencia + "BRA", maquina).Replace("1.404.234", "9.999", StringComparison.Ordinal), "producoes-velhas.txt", Velho));
            // O mesmo ficheiro outra vez lê-se como antes.
            Assert.Equal((true, "Fabrico lido do SAP: 1 produção, nenhuma nova."),
                await Enviar(RelatorioDeFabrico(referencia + "BRA", maquina), "dmo-fabrico-sap.txt", Novo));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.Equal(new[] { 40.5m }, await db.FerramentaUtilizacoes.Where(u => u.ToolId == toolId).Select(u => u.Percentagem).ToListAsync());
            Assert.Equal(1_404_234L, (await db.SapFabricos.SingleAsync(s => s.Material == referencia + "BRA")).ProducaoBruta);
        }
        finally
        {
            // Os outros testes enviam ficheiros de 2026: sem isto seriam «mais antigos».
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.RemoveRange(db.FerramentaUtilizacoes.Where(u => u.ToolId == toolId));
            db.SapFabricos.RemoveRange(db.SapFabricos.Where(s => s.Material == referencia + "BRA"));
            await db.SaveChangesAsync();
        }
    }
}

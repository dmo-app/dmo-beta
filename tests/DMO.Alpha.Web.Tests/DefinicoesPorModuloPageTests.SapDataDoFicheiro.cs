using System.Net;
using System.Text.Json;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Audit externo, 16/11/2026 (DMO-772): o /sap/enviar gravava a hora da
/// receção como data do ficheiro, e a proteção «ficheiro mais antigo» nunca
/// valia no envio do script. Agora o script manda a hora (de Lisboa) em que
/// o ficheiro foi escrito, e um ficheiro mais antigo do que o último lido não
/// grava. Sem a data (script antigo) conta a hora da receção, como antes.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task EnvioDoSap_FicheiroMaisAntigoDoQueOUltimo_NaoGrava()
    {
        var nome = "MEN" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() + "MPNNX";
        var toolId = SeedToolComNomeTecnico(nome, "4");
        string chave;
        using (var scope = _factory.Services.CreateScope())
        {
            chave = await scope.ServiceProvider.GetRequiredService<ISapDefinicoes>().ChaveDeEnvioAsync();
        }

        var anonimo = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        async Task<(bool Gravado, string Mensagem)> Enviar(string percentagem, string? modificado)
        {
            var lista = "search,equipment,percent_use,last_production,batch_description,status,error\r\n"
                + $"{nome}04,{nome}04,{percentagem},,,OK,\r\n";
            var url = "/sap/enviar?ficheiro=dmo-uso-sap.csv" + (modificado is null ? "" : "&modificado=" + modificado);
            using var pedido = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(System.Text.Encoding.Latin1.GetBytes(lista)) };
            pedido.Headers.Add("X-DMO-SAP", chave);
            using var resposta = await anonimo.SendAsync(pedido);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            return (json.RootElement.GetProperty("gravado").GetBoolean(), json.RootElement.GetProperty("mensagem").GetString()!);
        }

        decimal? Percentagem()
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<DmoDbContext>().FerramentaUtilizacoes.Where(u => u.ToolId == toolId).OrderByDescending(u => u.Id).First().Percentagem;
        }

        var hoje = DMO.Alpha.Core.Runtime.DmoBusinessTime.Now.Date;
        var novo = await Enviar("70", hoje.AddHours(-20).ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(novo.Gravado, novo.Mensagem);

        var antigo = await Enviar("40", hoje.AddDays(-3).ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(antigo.Gravado);
        Assert.Contains("é mais antigo do que o último lido", antigo.Mensagem);
        Assert.Equal(70m, Percentagem());

        // Sem a data (o script antigo), conta a hora da receção, como antes.
        var semData = await Enviar("75", null);
        Assert.True(semData.Gravado, semData.Mensagem);
        Assert.Equal(75m, Percentagem());
    }
}

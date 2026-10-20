using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Fase base dos testes (19/10/2026): criar o Peso e registar uma leitura
/// pelo formulário que a página desenha (o botão «Criar Peso» está fora do
/// &lt;form&gt;, ligado por form="pesoCreate"). Os outros testes enviam os
/// nomes que escolhem, por isso um name trocado na página deixava o CI verde.
/// </summary>
public partial class PesoToolScopedPageTests
{
    [Fact]
    public async Task PesoELeituraPeloFormularioDaPagina_GravamOQueOOperadorEscreveu()
    {
        await SeedWorkedExampleAsync();
        SeedUser("4231", "Peso Formulário", "provider-peso-formulario", "PesoForm1!");
        using var client = await LoginClientAsync("4231", "PesoForm1!");

        // Criar: a temperatura e os valores técnicos em falta, escritos no Novo Peso.
        var html = await GetPageHtmlAsync(client, $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9");
        var (acao, campos) = EnvioDoFormulario.Preencher(html,
            ("waterTemperature", "21"),
            ("cmVolumePuncao", "30.00"),
            ("cmNominalWeight", "200.00"),
            ("bqVolumeMarisa", "10.00"));
        int pesoId;
        using (var criado = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, criado.StatusCode);
            var destino = criado.Headers.Location?.ToString() ?? string.Empty;
            var match = Regex.Match(destino, @"peso_id=(\d+)");
            Assert.True(match.Success, $"O Peso não foi criado: voltou a {destino}.");
            pesoId = int.Parse(match.Groups[1].Value);
        }

        // Registar uma leitura no Peso aberto.
        html = await GetPageHtmlAsync(client, $"{PagePath}?peso_id={pesoId}");
        (acao, campos) = EnvioDoFormulario.Preencher(html, ("cmSubject", "12"), ("waterWeight", "199.64"));
        using (var leitura = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, leitura.StatusCode);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var peso = await db.Pesos.AsNoTracking().Include(p => p.Measurements).SingleAsync();
        Assert.Equal(pesoId, peso.Id);
        Assert.Equal("T3", peso.CmToolId);
        Assert.Equal("BQ-TOOL-9", peso.BqToolId);
        Assert.Equal(21, peso.WaterTemperatureC);
        Assert.Equal(30.00m, peso.VolumePuncaoCm3);
        Assert.Equal(200.00m, peso.NominalWeightGrams);
        Assert.Equal(10.00m, peso.VolumeMarisaCm3);
        var gravada = Assert.Single(peso.Measurements);
        Assert.Equal("12", gravada.CmSubject);
        Assert.Equal(199.64m, gravada.WaterWeightGrams);
    }
}

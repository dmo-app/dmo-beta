using System.Net;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PesoToolScopedPageTests
{
    [Fact]
    public async Task RegisterMeasurement_OMesmoEnvioDepoisDeUmReinicio_NaoGravaOutraVez_EUmEnvioNovoIgualGrava()
    {
        // Diogo, 27/10/2026: a memória dos envios ([EnvioUnico]) perde-se num
        // reinício. Aqui cada pedido começa sem ela, como depois de um
        // reinício; a operação registada na base é que impede o duplicado.
        await SeedWorkedExampleAsync();
        SeedUser("4233", "Peso Operador", "provider-peso-um-envio", "PesoUmEnvio1!");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped(_ => new EnviosGravados())));
        using var client = await LoginClientAsync("4233", "PesoUmEnvio1!", factory);

        var created = await PostCreateAsync(client, $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9", "T3", "BQ-TOOL-9", "20",
            volumePuncao: "30.00", volumeMarisa: "10.00");
        var pesoUrl = created.Headers.Location!.ToString();
        var pesoId = int.Parse(System.Text.RegularExpressions.Regex.Match(pesoUrl, @"peso_id=(\d+)").Groups[1].Value);
        var token = await GetVerificationTokenAsync(client, pesoUrl);

        async Task<HttpResponseMessage> EnviarAsync(string chave)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{PagePath}?handler=RegisterMeasurement")
            {
                Content = HtmlFormHelpers.BuildFormPost(
                    ("peso_id", pesoId.ToString()),
                    ("cm_subject", "12"), ("water_weight", "199,64"),
                    ("cm_subject", "21"), ("water_weight", "99.82"),
                    ("__RequestVerificationToken", token))
            };
            request.Headers.Add(EnvioUnicoFilter.Cabecalho, chave);
            return await client.SendAsync(request);
        }

        async Task<string[]> LeiturasAsync()
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PesoMeasurements.AsNoTracking()
                .Where(m => m.PesoId == pesoId).OrderBy(m => m.RowPosition)
                .Select(m => m.RowPosition + ":" + m.CmSubject).ToArrayAsync();
        }

        var primeiro = await EnviarAsync("envio-um-0000000001");
        var repetido = await EnviarAsync("envio-um-0000000001");

        Assert.Equal(HttpStatusCode.Redirect, repetido.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        Assert.DoesNotContain("measurement_refusal", repetido.Headers.Location!.ToString());
        Assert.Equal(["1:12", "2:21"], await LeiturasAsync());

        // As mesmas leituras escritas outra vez são um envio novo: gravam.
        await EnviarAsync("envio-dois-000000002");
        Assert.Equal(["1:12", "2:21", "3:12", "4:21"], await LeiturasAsync());
    }
}

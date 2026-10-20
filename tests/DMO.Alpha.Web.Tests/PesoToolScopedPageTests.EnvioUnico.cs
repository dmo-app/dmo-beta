using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PesoToolScopedPageTests
{
    [Fact]
    public async Task RegisterMeasurement_SameEnvioTwice_SavesOnce()
    {
        // Deep sweep, 18/10/2026, B-peso-3: a ligação caía depois de o
        // servidor gravar a leitura e um novo «Registar leitura» gravava-a
        // outra vez. O mesmo envio (a mesma chave X-Dmo-Envio) grava uma vez e
        // recebe o mesmo redirect.
        await SeedWorkedExampleAsync();
        SeedUser("4621", "Peso Envio", "provider-peso-envio", "PesoEnvio1!");
        using var client = await LoginClientAsync("4621", "PesoEnvio1!");
        var created = await PostCreateAsync(client, $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9", "T3", "BQ-TOOL-9", "20",
            volumePuncao: "30.00", volumeMarisa: "10.00");
        var pesoUrl = created.Headers.Location!.ToString();
        var pesoId = int.Parse(System.Text.RegularExpressions.Regex.Match(pesoUrl, @"peso_id=(\d+)").Groups[1].Value);
        var token = await GetVerificationTokenAsync(client, pesoUrl);
        var chave = EnvioUnicoPedidos.NovaChave();
        (string, string)[] campos = [("peso_id", pesoId.ToString()), ("cm_subject", "52"), ("water_weight", "131,2"), ("__RequestVerificationToken", token)];

        var primeiro = await client.PostComChaveAsync($"{PagePath}?handler=RegisterMeasurement", chave, campos);
        var repetido = await client.PostComChaveAsync($"{PagePath}?handler=RegisterMeasurement", chave, campos);

        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        using var scope = _factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PesoMeasurements.AsNoTracking()
            .Where(m => m.PesoId == pesoId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(131.2m, rows[0].WaterWeightGrams);
    }
}

using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    [Fact]
    public async Task PostMeasurement_SameEnvioTwice_SavesOnce()
    {
        // Deep sweep, 18/10/2026, B-pegamentos-3: a ligação caía depois de o
        // servidor gravar, o valor ficava na linha nova e um novo Enter gravava
        // a mesma leitura duas vezes. O mesmo envio (a mesma chave
        // X-Dmo-Envio) grava uma vez e recebe o mesmo redirect.
        SeedUser("4611", "Pegamentos Envio", "provider-peg-envio", "PegEnvio1!");
        using var client = await LoginClientAsync("4611", "PegEnvio1!");
        var controloId = await SeedRealProductionAsync();
        var token = await GetVerificationTokenAsync(client, $"{PagePath}?controlo_id={controloId}");
        var chave = EnvioUnicoPedidos.NovaChave();
        (string, string)[] campos =
        [
            ("controlo_id", controloId.ToString()), ("component", "CM"), ("gargalo", "70.00"),
            ("costura", "70.27"), ("contra_costura", "70.11"), ("__RequestVerificationToken", token)
        ];

        var primeiro = await client.PostComChaveAsync($"{PagePath}?handler=RegisterMeasurement", chave, campos);
        var repetido = await client.PostComChaveAsync($"{PagePath}?handler=RegisterMeasurement", chave, campos);

        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        using var scope = _factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements.AsNoTracking().ToListAsync();
        Assert.Single(rows);
        Assert.Equal(70.27m, rows[0].CosturaMm);
    }
}

using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Fase base dos testes (19/10/2026): o tipo e a data e hora reais do
/// movimento, enviados pelo formulário que a página desenha. Um name trocado
/// no radio do tipo ou no campo da data deixava os outros testes verdes,
/// porque esses enviam os nomes que o próprio teste escolhe.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task EntradaComADataReal_PeloFormularioDaPagina_GravaOTipoEAHoraDeLisboaEmUtc()
    {
        await SeedToolAsync("BQ-FORM-4742", "RGT742", "3");
        var trace = await SeedTraceAsync("BQ-FORM-4742");
        SeedUser("4742", "Registo Entrada", "provider-registo-entrada", "RegistoEntrada1!");
        using var client = await LoginClientAsync("4742", "RegistoEntrada1!");

        // Um registo atrasado de verão (Lisboa UTC+1): o operador marca
        // «Entrada», escreve a quantidade e a data e hora reais.
        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        var (acao, campos) = EnvioDoFormulario.Enviar(html,
            [("quantity", "4"), ("occurredAt", "2026-07-15T09:30")],
            radios: [("movement_type", "entrada")]);
        using (var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        // E uma «Entrada sem reparação» de inverno (Lisboa UTC+0).
        html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        (acao, campos) = EnvioDoFormulario.Enviar(html,
            [("quantity", "2"), ("occurredAt", "2026-01-20T14:05")],
            radios: [("movement_type", "entrada_sem_reparacao")]);
        using (var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos)))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var gravados = await db.BqMovements.AsNoTracking()
            .Where(m => m.BqRepairTraceId == trace.Id)
            .OrderBy(m => m.Quantity)
            .ToListAsync();
        Assert.Equal(2, gravados.Count);

        Assert.Equal(BqMovementType.EntradaSemReparacao, gravados[0].Type);
        Assert.Equal(2, gravados[0].Quantity);
        Assert.Equal(new DateTime(2026, 1, 20, 14, 5, 0, DateTimeKind.Utc), gravados[0].OccurredAtUtc);

        Assert.Equal(BqMovementType.Entrada, gravados[1].Type);
        Assert.Equal(4, gravados[1].Quantity);
        Assert.Equal(new DateTime(2026, 7, 15, 8, 30, 0, DateTimeKind.Utc), gravados[1].OccurredAtUtc);
    }
}

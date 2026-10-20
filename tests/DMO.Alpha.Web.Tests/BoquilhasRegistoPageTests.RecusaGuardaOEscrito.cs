using System.Net;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Teste do agente de 19/10/2026 (Tc95ce11): uma recusa do movimento (a
/// máquina indicada não existe) mostrava a mensagem, mas o formulário voltava
/// limpo: Saída, sem quantidade e com a hora de agora. O que o operador
/// escreveu nunca se perde sem aviso (REGRAS-DE-CODIGO, 2).
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task RecusaDoMovimento_OFormularioVoltaComOQueSeEscreveu_ENadaGrava()
    {
        await SeedToolAsync("BQ-RECUSA-4742", "RGT742", "3");
        var trace = await SeedTraceAsync("BQ-RECUSA-4742");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.Machines.Add(new Machine { Code = "B3" });
            await db.SaveChangesAsync();
        }
        SeedUser("4742", "Registo Recusa", "provider-registo-recusa", "RegistoRecusa1!");
        using var client = await LoginClientAsync("4742", "RegistoRecusa1!");

        // Entrada, 7, uma hora antiga e uma máquina que entretanto deixou de existir.
        var html = await GetPageHtmlAsync(client, TraceUrl(trace.Id));
        var (acao, campos) = EnvioDoFormulario.Enviar(html,
            [("quantity", "7"), ("occurredAt", "2026-10-15T08:30"), ("movementMachine", "999999")],
            radios: [("movement_type", "entrada")]);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains("movement_refusal=MachineNotFound", location);

        var depois = await GetPageHtmlAsync(client, location);
        Assert.Contains("A máquina indicada não existe", depois);
        // O que o formulário desenhado de novo enviaria: o que o operador escreveu.
        var (_, deNovo) = EnvioDoFormulario.Enviar(depois, [("movementMachine", "999999")]);
        Assert.Contains(("movement_type", "entrada"), deNovo);
        Assert.Contains(("quantity", "7"), deNovo);
        Assert.Contains(("occurred_at", "2026-10-15T08:30"), deNovo);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            Assert.False(await db.BqMovements.AsNoTracking().AnyAsync());
        }
    }
}

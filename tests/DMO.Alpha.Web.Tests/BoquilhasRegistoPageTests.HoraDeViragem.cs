using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O «hoje» das Boquilhas (sweep de 19/10/2026, B-1): o painel muda de
/// produção à hora de viragem (8h), mas a procura e os registos de uma BQ
/// mudavam à meia-noite. No dia da troca, entre as 0h e as 8h, a procura
/// marcava «Em produção» na produção seguinte e o clique abria o registo dela.
/// Agora os três usam o dia operacional.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task DeNoiteAntesDaViragem_AProcuraEOsRegistosSeguemAProducaoEmMaquina()
    {
        await SeedToolAsync("BQ-VIRAGEM", "QV194", "3");
        int contexto1, contexto2, registo1, registo2;
        using (var scope = _factory.Services.CreateScope())
        {
            // A B2 faz a P1 até 10/03 e a P2 a partir de 11/03, com a mesma BQ.
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b2 = new Machine { Code = "B2" };
            var p1 = new BqContext
            {
                ToolId = "BQ-VIRAGEM",
                JobOn = new JobOn
                {
                    Reference = "5447QV194", ProductionNumber = "PN-VIR-1", Machine = b2,
                    InicioEm = new DateOnly(2030, 3, 4), FimEm = new DateOnly(2030, 3, 10)
                }
            };
            var p2 = new BqContext
            {
                ToolId = "BQ-VIRAGEM",
                JobOn = new JobOn
                {
                    Reference = "5447QV194", ProductionNumber = "PN-VIR-2", Machine = b2,
                    InicioEm = new DateOnly(2030, 3, 11), FimEm = new DateOnly(2030, 3, 17)
                }
            };
            var t1 = new BqRepairTrace { BqContext = p1 };
            var t2 = new BqRepairTrace { BqContext = p2 };
            db.AddRange(p1, p2, t1, t2);
            await db.SaveChangesAsync();
            (contexto1, contexto2, registo1, registo2) = (p1.Id, p2.Id, t1.Id, t2.Id);
        }

        SeedUser("4197", "Registo Viragem", "provider-registo-viragem", "RegistoViragem1!");

        // 11/03/2030 às 03:00 em Lisboa (inverno, UTC+0): o dia operacional
        // ainda é 10/03, a B2 está na P1.
        using (var deNoite = FactoryWithBoquilhasClock(new DateTime(2030, 3, 11, 3, 0, 0, DateTimeKind.Utc)))
        {
            using var client = await LoginClientAsync("4197", "RegistoViragem1!", deNoite);
            var registos = RegistosDaBq(await GetPageHtmlAsync(client, "/Boquilhas?ficheiros=1&ferramenta=BQ-VIRAGEM"));
            Assert.Contains(">Produção em máquina</td>", Linha(registos, registo1.ToString()));
            Assert.Contains(">Produção programada</td>", Linha(registos, registo2.ToString()));
        }

        // Às 08:00 passa à P2, como o painel.
        using (var deManha = FactoryWithBoquilhasClock(new DateTime(2030, 3, 11, 8, 0, 0, DateTimeKind.Utc)))
        {
            using var client = await LoginClientAsync("4197", "RegistoViragem1!", deManha);
            var registos = RegistosDaBq(await GetPageHtmlAsync(client, "/Boquilhas?ficheiros=1&ferramenta=BQ-VIRAGEM"));
            Assert.Contains(">Produção anterior</td>", Linha(registos, registo1.ToString()));
            Assert.Contains(">Produção em máquina</td>", Linha(registos, registo2.ToString()));
        }
    }

    /// <summary>A app com o relógio do «hoje» das Boquilhas fixo.</summary>
    private WebApplicationFactory<Program> FactoryWithBoquilhasClock(DateTime utcNow)
    {
        var clock = new FixedClock(utcNow);
        return _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IBoquilhasCurrentQuery>(provider =>
                ActivatorUtilities.CreateInstance<BoquilhasCurrentQuery>(provider, clock))));
    }

    /// <summary>A tabela dos registos da BQ na ficha dela.</summary>
    private static string RegistosDaBq(string html)
    {
        var inicio = html.IndexOf("id=\"bqSheetRegisters\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "A tabela dos registos da BQ não aparece.");
        return html[inicio..];
    }

    /// <summary>A linha de um registo numa tabela (pelo data-id).</summary>
    private static string Linha(string html, string id)
    {
        var inicio = html.IndexOf($"data-id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"A linha de {id} não aparece.");
        var fim = html.IndexOf("</tr>", inicio, StringComparison.Ordinal);
        return html[inicio..fim];
    }
}

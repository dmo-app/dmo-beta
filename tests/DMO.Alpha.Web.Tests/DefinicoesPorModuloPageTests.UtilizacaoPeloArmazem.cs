using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A janela «Utilização» mostra o que a ferramenta fez, não só os Job On onde
/// ficou escrita (Diogo, 28/10/2026: «deve aparecer o que a tool_id tem e não
/// o Job On»): uma Saída do Armazém para a Produção numa Linha conta os Job On
/// dessa Linha até ela voltar.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task AJanelaDaUtilizacao_MostraOsJobOnDaLinhaEnquantoAFerramentaLaEsteve()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var linha = db.Machines.First(m => m.Code == maquina);
            // Outro artigo na mesma Linha, sem a ferramenta escrita no Job On; e um depois de ela voltar.
            db.JobOns.AddRange(
                new JobOn { Reference = referencia + "X", ProductionNumber = "202502", Machine = linha, InicioEm = new DateOnly(2025, 2, 10), FimEm = new DateOnly(2025, 2, 20) },
                new JobOn { Reference = referencia + "Y", ProductionNumber = "202509", Machine = linha, InicioEm = new DateOnly(2025, 4, 1), FimEm = new DateOnly(2025, 4, 10) });
            db.ArmazemMovimentos.AddRange(
                new ArmazemMovimento { Tipo = ArmazemMovimentoTipo.Saida, Destino = ArmazemDestino.Producao, ToolId = toolId, MachineId = linha.Id, OcorridoEmUtc = new DateTime(2025, 2, 8, 10, 0, 0, DateTimeKind.Utc), RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid() },
                new ArmazemMovimento { Tipo = ArmazemMovimentoTipo.Entrada, Origem = ArmazemOrigem.Producao, ToolId = toolId, MachineId = linha.Id, OcorridoEmUtc = new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc), RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid() });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7187", DmoCapabilities.Ferramentas);
        var resposta = await client.GetAsync($"/Ferramentas/Utilizacao?tool_id={toolId}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var quadro = ZonaUsoSap(WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync()), "ferrProducoes");

        Assert.Contains("<strong>202501</strong>", quadro, StringComparison.Ordinal);
        Assert.Contains("<strong>202502</strong>", quadro, StringComparison.Ordinal);
        Assert.Contains($"{referencia}X · {maquina}", quadro, StringComparison.Ordinal);
        Assert.DoesNotContain("202509", quadro, StringComparison.Ordinal);
        Assert.Contains("Total · 2 produções", quadro, StringComparison.Ordinal);
    }
}

using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O «% uso» abre o histórico do uso da ferramenta (Diogo, 27/10/2026: «qualquer
/// % uso de uma ferramenta abre um slide dessa tool_id com esse historial»): a
/// página /Ferramentas/Utilizacao, com as produções, o Job On, o produzido e o
/// uso no fim de cada uma.
/// </summary>
public sealed partial class DefinicoesPorModuloPageTests
{
    [Fact]
    public async Task OHistoricoDoUso_MostraAsProducoesEOUsoQueFoiAcumulando()
    {
        var (toolId, referencia, maquina) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var seguinte = new JobOn
            {
                Reference = referencia, ProductionNumber = "202502", Machine = db.Machines.First(m => m.Code == maquina),
                InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 10)
            };
            db.JobOns.Add(seguinte);
            db.CmContexts.Add(new CmContext { JobOn = seguinte, ToolId = toolId });
            db.SapFabricos.AddRange(
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 1, 21), FimEm = new DateOnly(2025, 2, 4), ProducaoBruta = 1_000_000, LidoEmUtc = DateTime.UtcNow },
                new SapFabrico { Material = referencia + "BRA", Linha = maquina, InicioEm = new DateOnly(2025, 3, 1), FimEm = new DateOnly(2025, 3, 10), ProducaoBruta = 3_000_000, LidoEmUtc = DateTime.UtcNow });
            // Antes da 1.ª 40 %, entre as duas 47,5 %; a 2.ª sem leitura depois: estima-se pelo produzido.
            db.FerramentaUtilizacoes.AddRange(
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 40m, RegistadaEmUtc = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc) },
                new FerramentaUtilizacao { ToolId = toolId, Percentagem = 47.5m, RegistadaEmUtc = new DateTime(2025, 2, 6, 12, 0, 0, DateTimeKind.Utc) });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7193", DmoCapabilities.Ferramentas);
        var resposta = await client.GetAsync($"/Ferramentas/Utilizacao?tool_id={toolId}");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var pagina = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());
        var quadro = ZonaUsoSap(pagina, "ferrProducoes");

        Assert.Contains("202501", quadro, StringComparison.Ordinal);
        Assert.Contains("202502", quadro, StringComparison.Ordinal);
        Assert.Matches("<a class=\"ferr-armazem-link\" href=\"/JobOn\\?jobon_id=\\d+\"><strong>202502</strong></a>", quadro);
        // A 1.ª medida (+7.5 %, fica em 47.5 %); a 2.ª sem leitura depois: a parte do % de hoje
        // pelo que se produziu até ao fim dela (4 de 4 milhões), ≈ 47.5 %.
        Assert.Contains("+7.5 %", quadro, StringComparison.Ordinal);
        Assert.Contains("<td>47.5 %</td>", quadro, StringComparison.Ordinal);
        Assert.Contains("<td>≈ 47.5 %</td>", quadro, StringComparison.Ordinal);
        Assert.Contains("Total · 2 produções", ZonaUsoSap(quadro, "ferrProducoesTotal"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OHistoricoDoUso_DeUmaFerramentaQueNaoExiste_DaNaoEncontrada()
    {
        var client = await LoginOperatorAsync("7194", DmoCapabilities.Ferramentas);
        var resposta = await client.GetAsync("/Ferramentas/Utilizacao?tool_id=nao-existe-9999");
        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task OUsoNaFichaDoArmazem_AbreOHistoricoDoUso()
    {
        var (toolId, _, _) = SeedJobOnComCm();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = toolId, Percentagem = 12.5m, RegistadaEmUtc = DateTime.UtcNow });
            db.SaveChanges();
        }

        var client = await LoginOperatorAsync("7195", DmoCapabilities.ArmazemGestao);
        var pagina = WebUtility.HtmlDecode(await client.GetStringAsync($"/Armazem/Movimentos?ferramenta={toolId}"));

        Assert.Contains($"<a class=\"dmo-uso-link\" href=\"/Ferramentas/Utilizacao?tool_id={toolId}\"", ZonaUsoSap(pagina, "armUso"), StringComparison.Ordinal);
        Assert.Contains("12.5 %</a>", ZonaUsoSap(pagina, "armUso"), StringComparison.Ordinal);
    }
}

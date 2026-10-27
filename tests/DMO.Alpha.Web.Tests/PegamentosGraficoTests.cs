using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Pages.Controlo;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O Mapa de limites no ecrã (Diogo, 27/10/2026: «arranjar o gráfico»): a
/// escala das medições e dos limites, com margem; uma linha por componente
/// medido; os componentes sem medições numa nota, nunca numa linha vazia.
/// </summary>
public class PegamentosGraficoTests
{
    private static PegamentosPdfGenerator.MeasurementRow Linha(int posicao, decimal media, decimal nominal, bool fora = false) =>
        new(posicao, media, media, 0m, media, fora, false, nominal, nominal - .20m, nominal + .20m);

    private static PegamentosPdfGenerator.ComponentTable Tabela(string componente, params PegamentosPdfGenerator.MeasurementRow[] linhas) =>
        new(componente, linhas, linhas.Length, linhas.Count(l => l.IsAlert), null, null, null, linhas.Average(l => l.MediaMm));

    private static PegamentosPdfGenerator.Input Entrada(params PegamentosPdfGenerator.ComponentTable[] tabelas) => new(
        "202604", "1075C142", "B1",
        [new("CM", "ST100", "8", 24.80m), new("BQ", "T200", "3", 24.95m), new("MF", "5810", "1", 25.10m)],
        tabelas, .20m);

    [Fact]
    public void Escala_DasMedicoesEDosLimites_ComMargem_SemLinhasVazias()
    {
        var grafico = PegamentosGrafico.From(Entrada(Tabela("CM", Linha(1, 24.82m, 24.80m), Linha(2, 24.87m, 24.80m), Linha(3, 26.74m, 24.80m, fora: true))));

        Assert.Equal(["CM"], grafico.Linhas.Select(l => l.Componente));
        Assert.Equal(["BQ", "MF"], grafico.SemMedicoes);
        var cm = grafico.Linhas[0];
        Assert.Equal("24.80 ± 0.20", cm.Nominal);
        // Tudo dentro do gráfico, com margem dos dois lados.
        var xs = cm.Pontos.Select(p => p.X).Append(cm.Corredores[0].Inicio).Append(cm.Corredores[0].Inicio + cm.Corredores[0].Largura).ToArray();
        Assert.All(xs, x => Assert.InRange(x, 3, 97));
        Assert.Equal("fora", cm.Pontos[2].Estado);
        // Poucos valores na escala (4 a 8), sem um intervalo fixo.
        Assert.InRange(grafico.Escala.Count, 4, 9);
        Assert.All(grafico.Escala, m => Assert.InRange(m.X, 0, 100));
        Assert.Equal(2, grafico.LimitesDeFora.Count);
    }

    [Fact]
    public void CadaComponenteMedido_NaSuaLinha_PelaOrdemDaFolha()
    {
        var grafico = PegamentosGrafico.From(Entrada(
            Tabela("CM", Linha(1, 24.80m, 24.80m)),
            Tabela("BQ", Linha(1, 24.96m, 24.95m)),
            Tabela("MF", Linha(1, 25.08m, 25.10m))));

        Assert.Equal(["CM", "BQ", "MF"], grafico.Linhas.Select(l => l.Componente));
        Assert.Empty(grafico.SemMedicoes);
        Assert.True(grafico.Linhas[0].Corredores[0].Nominal < grafico.Linhas[2].Corredores[0].Nominal);
    }

    [Fact]
    public void SemMedicoes_NenhumaLinha()
    {
        var grafico = PegamentosGrafico.From(Entrada());
        Assert.False(grafico.TemLinhas);
        Assert.Equal(["CM", "BQ", "MF"], grafico.SemMedicoes);
    }
}

using System.Globalization;
using System.Text.Json;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Pages.Controlo;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Sweep de 19/10/2026, 3.12 (B-pegamentos-6): os casos de
/// tests/ShellProof/pegamentos-previsao.casos.json verificam o cálculo do
/// servidor (PegamentosCalculation e o que o cartão mostra) e, no
/// pegamentos-previsao.test.cjs, a pré-visualização no browser. Se a regra
/// mudar num lado, o teste do outro falha: a pré-visualização é o que fica
/// gravado.
/// </summary>
public sealed class PegamentosPrevisaoCasosTests
{
    public sealed record Caso(
        string Nome,
        string Nominal,
        string Tolerancia,
        string LimiteInferior,
        string LimiteSuperior,
        string Costura,
        string Contra,
        string Oval,
        string Media,
        bool ForaCostura,
        bool ForaContra,
        bool ForaMedia,
        bool OvalVermelha);

    private sealed record Ficheiro(string Nota, IReadOnlyList<Caso> Casos);

    public static TheoryData<string> Nomes()
    {
        var nomes = new TheoryData<string>();
        foreach (var caso in Casos()) nomes.Add(caso.Nome);
        return nomes;
    }

    private static IReadOnlyList<Caso> Casos()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var json = File.ReadAllText(Path.Combine(dir!.FullName, "tests", "ShellProof", "pegamentos-previsao.casos.json"));
        return JsonSerializer.Deserialize<Ficheiro>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Casos;
    }

    // Como o servidor lê uma medição (MeasurementDecimal): vírgula ou ponto.
    private static decimal? Ler(string texto) =>
        decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var valor) ? valor : null;

    [Theory]
    [MemberData(nameof(Nomes))]
    public void OServidorDaOMesmoQueAPrevisualizacao(string nome)
    {
        var caso = Casos().Single(c => c.Nome == nome);
        var nominal = Ler(caso.Nominal)!.Value;
        var tolerancia = Ler(caso.Tolerancia)!.Value;

        // O corredor que o cartão escreve para o browser.
        Assert.Equal(caso.LimiteInferior, PegamentosVista.Exato(PegamentosCalculation.CorridorLowerMm(nominal, tolerancia)));
        Assert.Equal(caso.LimiteSuperior, PegamentosVista.Exato(PegamentosCalculation.CorridorUpperMm(nominal, tolerancia)));

        var avaliacao = PegamentosCalculation.EvaluateRow(nominal, tolerancia, Ler(caso.Costura)!.Value, Ler(caso.Contra));
        Assert.Equal(caso.Oval, PegamentosVista.FmtSigned(avaliacao.OvalizacaoMm));
        Assert.Equal(caso.Media, PegamentosVista.Fmt(avaliacao.MediaMm));
        Assert.Equal(caso.ForaCostura, PegamentosVista.AxisAlert(avaliacao, PegamentosEvaluatedAxis.Costura));
        Assert.Equal(caso.ForaContra, PegamentosVista.AxisAlert(avaliacao, PegamentosEvaluatedAxis.ContraCostura));
        Assert.Equal(caso.ForaMedia, PegamentosVista.AxisAlert(avaliacao, PegamentosEvaluatedAxis.Media));
        Assert.Equal(caso.OvalVermelha, PegamentosVista.OvalAlert(avaliacao, tolerancia));
    }
}

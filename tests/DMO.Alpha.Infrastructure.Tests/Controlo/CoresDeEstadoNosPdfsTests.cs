using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using UglyToad.PdfPig;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// As cores de estado nos PDFs do Controlo (sweep de 19/10/2026, 3.16): o
/// mesmo estado sai sempre com a mesma cor. «Não avaliável» sai a cinzento,
/// como no ecrã dos Pegamentos (saía a âmbar, a cor de «por verificar»), e as
/// mesmas punções saem com as mesmas cores no PDF do PU NNPB e no do Resumo
/// («Por verificar» saía no castanho do erro num e a âmbar no outro). Todas
/// as cores vêm da DmoPdfPalette: um gerador com uma cor solta falha.
/// </summary>
public sealed class CoresDeEstadoNosPdfsTests
{
    private const string Verde = "#15803d";
    private const string Ambar = "#a16207";

    // A cor das letras de cada palavra «palavra» escrita no PDF.
    private static string[] CoresDe(byte[] pdf, string palavra)
    {
        using var document = PdfDocument.Open(pdf);
        var cores = document.GetPages()
            .SelectMany(page => page.GetWords())
            .Where(word => word.Text == palavra)
            .SelectMany(word => word.Letters)
            .Select(letter =>
            {
                var (r, g, b) = letter.Color.ToRGBValues();
                return $"#{(int)Math.Round(r * 255):x2}{(int)Math.Round(g * 255):x2}{(int)Math.Round(b * 255):x2}";
            })
            .Distinct()
            .ToArray();
        Assert.NotEmpty(cores);
        return cores;
    }

    private static ResumoPdfGenerator.Input Resumo() =>
        new("202605", "5447T173", "B1", null, null, [], null, null, [], null);

    [Fact]
    public void ResumoPdf_NaoAvaliaveis_SaemACinzento()
    {
        var umaFolha = Resumo() with { Pegamentos = new ResumoPdfGenerator.PegamentosSummary(3, 0, .20m, NotEvaluableCount: 2) };
        Assert.Equal([PegamentosLimitMap.NotEvaluableColor], CoresDe(new ResumoPdfGenerator().GenerateBytes(umaFolha), "avaliáveis"));

        var duasFolhas = Resumo() with
        {
            Pegamentos = new ResumoPdfGenerator.PegamentosSummary(5, 0, .20m, NotEvaluableCount: 2)
            {
                Sheets =
                [
                    new("CM 5447 · BQ T173 · MF 5447", 3, 0, 0, .20m, null),
                    new("CM 5448 · BQ T173 · MF 5447", 2, 0, 2, .20m, null)
                ]
            }
        };
        var pdf = new ResumoPdfGenerator().GenerateBytes(duasFolhas);
        Assert.Equal([PegamentosLimitMap.NotEvaluableColor], CoresDe(pdf, "avaliáveis"));
        Assert.Equal([PegamentosLimitMap.NotEvaluableColor], CoresDe(pdf, "avaliável(eis)"));
    }

    [Fact]
    public void PegamentosPdf_NaoAvaliavel_SaiACinzento_ComoNoMapa()
    {
        var sheet = PdfRecoveryTests.ReferenceSheet();
        var semNominal = sheet.Pegamentos!.Measurements[0] with
        {
            NominalGargaloMm = null,
            Evaluation = PegamentosCalculation.EvaluateRow(null, .20m, 34.55m, 34.50m)
        };
        var input = PegamentosPdfGenerator.FromSheet(sheet with { Pegamentos = sheet.Pegamentos with { Measurements = [semNominal] } });

        Assert.Equal([PegamentosLimitMap.NotEvaluableColor], CoresDe(new PegamentosPdfGenerator().GenerateBytes(input), "avaliáveis"));
    }

    [Fact]
    public void Puncoes_SaemComAsCoresDeEstadoNoResumo()
    {
        // O PDF das punções é a tabela das posições (22/10/2026), sem estados;
        // as cores do «por verificar» e do «vistos» ficam no cartão do Resumo.
        var porVerificar = new ResumoPdfGenerator().GenerateBytes(Resumo() with { Puncoes = new(2, 1, 1) });
        Assert.Equal([Ambar], CoresDe(porVerificar, "verificar"));
        var vistas = new ResumoPdfGenerator().GenerateBytes(Resumo() with { Puncoes = new(2, 2, 2) });
        Assert.Equal([Verde], CoresDe(vistas, "vistos"));
    }

    [Fact]
    public void OsGeradoresDePdf_NaoTemCoresSoltas()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var geradores = Directory.EnumerateFiles(Path.Combine(dir!.FullName, "src", "DMO.Alpha.Infrastructure"),
            "*PdfGenerator.cs", SearchOption.AllDirectories).ToArray();
        Assert.NotEmpty(geradores);
        foreach (var file in geradores)
        {
            var soltas = Regex.Matches(File.ReadAllText(file), "#[0-9a-fA-F]{6}\\b").Select(m => m.Value).ToArray();
            Assert.True(soltas.Length == 0, $"{Path.GetFileName(file)} tem cores soltas: {string.Join(", ", soltas)}");
        }
    }
}

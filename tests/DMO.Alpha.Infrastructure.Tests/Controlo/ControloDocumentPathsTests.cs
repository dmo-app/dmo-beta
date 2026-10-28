using System.IO;
using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O RESOLVER de caminhos dos documentos do Controlo (o corte PDF, a
/// estrutura do Owner 2026-10-11): &lt;dir&gt;/&lt;referência&gt;/&lt;nº de
/// produção&gt;/ com os nomes canónicos PESO-/PEGAMENTOS-/RESUMO-&lt;ref&gt;-&lt;prod&gt;.pdf
/// — o exemplo do Owner: .dir/5447T173/202601/PESO-5447T173-202601.pdf.
/// </summary>
public sealed class ControloDocumentPathsTests
{
    [Fact]
    public void FileNames_AreCanonical_PesoPegamentosResumo()
    {
        // Os nomes do Owner: PESO-5447T173-202601, PEGAMENTOS-…, RESUMO-…
        Assert.Equal(
            "PESO-5447T173-202601.pdf",
            ControloDocumentPaths.GetPesoFileName("5447T173", "202601"));
        Assert.Equal(
            "PEGAMENTOS-5447T173-202601.pdf",
            ControloDocumentPaths.GetPegamentosFileName("5447T173", "202601"));
        Assert.Equal(
            "RESUMO-5447T173-202601.pdf",
            ControloDocumentPaths.GetResumoFileName("5447T173", "202601"));
    }

    [Fact]
    public void PegamentosFileName_SecondSheetOnwards_EndsWithItsNumber()
    {
        // Uma produção com mais do que uma folha de Pegamentos (a CM, a BQ
        // ou o MF mudou a meio; 18/10/2026): a primeira fica com o
        // nome de sempre, as outras levam «-2», «-3»…
        Assert.Equal("PEGAMENTOS-5447T173-202601.pdf", ControloDocumentPaths.GetPegamentosFileName("5447T173", "202601", 1));
        Assert.Equal("PEGAMENTOS-5447T173-202601-2.pdf", ControloDocumentPaths.GetPegamentosFileName("5447T173", "202601", 2));
        Assert.Equal("PEGAMENTOS-5447T173-202601-3.pdf", ControloDocumentPaths.GetPegamentosFileName("5447T173", "202601", 3));
    }

    [Fact]
    public void PesoFileName_SecondPesoOnwards_EndsWithItsNumber()
    {
        // Uma produção com mais do que um Peso (a CM ou a BQ mudou a meio;
        // resposta R16-9 do especialista, 18/10/2026): o primeiro fica com o
        // nome de sempre, os outros levam «-2», «-3»…
        Assert.Equal("PESO-5447T173-202601.pdf", ControloDocumentPaths.GetPesoFileName("5447T173", "202601", 1));
        Assert.Equal("PESO-5447T173-202601-2.pdf", ControloDocumentPaths.GetPesoFileName("5447T173", "202601", 2));
        Assert.Equal("PESO-5447T173-202601-3.pdf", ControloDocumentPaths.GetPesoFileName("5447T173", "202601", 3));
    }

    [Fact]
    public void Segments_WithPathSeparators_NeverBecomeStructure()
    {
        // Um separador no valor do operador nunca vira estrutura: a app
        // decide a estrutura, o documento carrega o nome (recusar é teatro).
        Assert.Equal(
            "PESO-.._5447T173-2026_01.pdf",
            ControloDocumentPaths.GetPesoFileName(@"../5447T173", @"2026/01"));
    }

    [Fact]
    public void ProductionRelativeFolder_IsReferenceThenProduction_SafeForWindowsShares()
    {
        // A pasta escolhida no computador recebe <referência>/<produção>
        // (Diogo, 17/10/2026), e a da produção leva também a referência
        // (Diogo, 28/10/2026); os caracteres que o Windows recusa viram «_».
        Assert.Equal("5447T173/202601 5447T173", ControloDocumentPaths.GetProductionRelativeFolder("5447T173", "202601"));
        Assert.Equal("A_B_C_/2026_01 A_B_C_", ControloDocumentPaths.GetProductionRelativeFolder("A:B*C?", "2026|01"));
        Assert.Equal("sem-referencia/sem-producao", ControloDocumentPaths.GetProductionRelativeFolder("", " "));
        Assert.Equal("5447T173/Sem Job On 5447T173", ControloDocumentPaths.GetProductionRelativeFolder("5447T173", "Sem Job On"));
    }
}

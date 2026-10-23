using DMO.Alpha.Web.Pages.Ferramentas;
using DMO.Alpha.Web.Presentation;

namespace DMO.Alpha.Web.Tests;

/// <summary>O lote mostra-se sem zeros à esquerda (Diogo, 22/10/2026); o gravado não muda.</summary>
public sealed class LoteTextoTests
{
    [Theory]
    [InlineData("02", "2")]
    [InlineData("2", "2")]
    [InlineData("010", "10")]
    [InlineData(" 03 ", "3")]
    [InlineData("0", "0")]
    [InlineData("00", "0")]
    [InlineData("02A", "02A")]
    [InlineData("B1", "B1")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void OLote_SemZerosAEsquerda(string? gravado, string mostrado)
    {
        Assert.Equal(mostrado, LoteTexto.Mostrar(gravado));
    }

    [Fact]
    public void ONomeDoHistorico_UsaOLoteSemZero()
    {
        var bq = new DMO.Alpha.Core.Tools.HistoricoFerramenta("t", DMO.Alpha.Core.Tools.ToolType.Bq, "T173", "02");

        Assert.Equal("BQ T173 · Lote 2", HistoricoModel.Nome(bq));
        Assert.Equal("02", bq.Lote);
    }
}

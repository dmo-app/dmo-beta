using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Sem «.00» num número inteiro (Diogo, 23/10/2026: «100.00 g» é «100 g»); com parte
/// decimal ficam as duas casas.
/// </summary>
public class DmoNumberFormatTests
{
    [Theory]
    [InlineData("100", "100")]
    [InlineData("197.00", "197")]
    [InlineData("100.5", "100.50")]
    [InlineData("181.67", "181.67")]
    [InlineData("99.999", "100")]
    [InlineData("0.004", "0")]
    [InlineData("-3.69", "-3.69")]
    public void Decimal2_SemZerosNumInteiro_DuasCasasComDecimais(string valor, string esperado)
    {
        Assert.Equal(esperado, DmoNumberFormat.Decimal2(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)));
    }

    // A folha do Job On (Diogo, 26/10/2026): ponto em vez de vírgula e duas casas quando há
    // casas, também dentro do texto; um inteiro fica inteiro e o texto fica como está.
    [Theory]
    [InlineData("365.000", "365")]
    [InlineData("2,5", "2.50")]
    [InlineData("36.85", "36.85")]
    [InlineData("70/69,4", "70/69.40")]
    [InlineData("56,4 × 43,3", "56.40 × 43.30")]
    [InlineData("0/+0,03", "0/+0.03")]
    [InlineData("0,005", "0.01")]
    [InlineData("± 0,005 mm", "± 0.005 mm")]
    [InlineData("6 1/4\"", "6 1/4\"")]
    [InlineData("21.10.2026", "21.10.2026")]
    [InlineData("Abertos 4 / Fechados 2", "Abertos 4 / Fechados 2")]
    [InlineData("  ", null)]
    public void FolhaTexto_PontoEDuasCasas_TambemDentroDoTexto(string valor, string? esperado)
    {
        Assert.Equal(esperado, DmoNumberFormat.FolhaTexto(valor));
    }
}

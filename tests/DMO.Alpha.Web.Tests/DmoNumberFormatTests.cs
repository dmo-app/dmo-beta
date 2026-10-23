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
}

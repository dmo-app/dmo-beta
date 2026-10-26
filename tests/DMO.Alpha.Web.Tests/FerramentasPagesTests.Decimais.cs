using DMO.Alpha.Core.Runtime;
using Xunit;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Diogo, 25/10/2026: «.00 duas decimais mas só quando for necessário,
/// números inteiros não aparece .00» — o Peso nominal aparecia «140.000» no campo da ficha.
/// </summary>
public sealed class FichaDecimaisTests
{
    [Theory]
    [InlineData("140.000", "140")]
    [InlineData("36.850", "36.85")]
    [InlineData("4.500", "4.50")]
    [InlineData("1.478", "1.48")]
    [InlineData("70.10", "70.10")]
    public void Decimal2_InteirosSemCasas_DecimaisComDuas(string valor, string esperado) =>
        Assert.Equal(esperado, DmoNumberFormat.Decimal2(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)));
}

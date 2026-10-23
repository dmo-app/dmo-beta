using DMO.Alpha.Core.Production;
using Xunit;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

public sealed class CsFurosTextoTests
{
    [Theory]
    [InlineData("Abertos 4 / Fechados 2", true, "4", "2")]
    [InlineData("abertos 0 / fechados 9", true, "0", "9")]
    [InlineData("Abertos 3", true, "3", null)]
    [InlineData("Fechados 1", true, null, "1")]
    [InlineData("4 × 4", false, null, null)]
    [InlineData("Abertos 12 / Fechados 2", false, null, null)]
    [InlineData(null, false, null, null)]
    public void Ler(string? texto, bool lido, string? abertos, string? fechados)
        => Assert.Equal((lido, abertos, fechados), CsFurosTexto.Ler(texto));

    [Theory]
    [InlineData("4", "2", "Abertos 4 / Fechados 2")]
    [InlineData("4", null, "Abertos 4")]
    [InlineData(null, "2", "Fechados 2")]
    [InlineData("12", "x", "")]
    [InlineData(null, null, "")]
    public void Escrever(string? abertos, string? fechados, string texto)
        => Assert.Equal(texto, CsFurosTexto.Escrever(abertos, fechados));
}

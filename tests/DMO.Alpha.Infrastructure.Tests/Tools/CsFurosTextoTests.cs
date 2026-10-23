using DMO.Alpha.Core.Production;
using Xunit;

namespace DMO.Alpha.Infrastructure.Tests.Tools;

public sealed class CsFurosTextoTests
{
    [Theory]
    [InlineData("Fechado · 4", "Fechado", "4")]
    [InlineData("aberto · 12", "Aberto", "12")]
    [InlineData("Fechado", "Fechado", null)]
    [InlineData("4 × 4", null, null)]
    [InlineData(null, null, null)]
    public void Ler(string? texto, string? estado, string? numero)
        => Assert.Equal((estado, numero), CsFurosTexto.Ler(texto));

    [Theory]
    [InlineData("Fechado", "4", "Fechado · 4")]
    [InlineData("Aberto", null, "Aberto")]
    [InlineData(null, null, "")]
    public void Escrever(string? estado, string? numero, string texto)
        => Assert.Equal(texto, CsFurosTexto.Escrever(estado, numero));
}

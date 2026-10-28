using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// As FOLGAS, um número por espaço (Diogo, 28/10/2026: «MF tem dois no primeiro
/// espaço, deveria ser 0.12 - tolerância - 0.16 no último»).
/// </summary>
public sealed class JobOnFolgasTests
{
    [Theory]
    [InlineData("0.12 / 0.16", null, null, "0.12", null, "0.16")]
    [InlineData("0.12/0.16", "0.02", "", "0.12", "0.02", "0.16")]
    [InlineData(null, "0.02", "0.12 / 0.16", "0.12", "0.02", "0.16")]
    [InlineData("0.12", "0.02", "0.16", "0.12", "0.02", "0.16")]
    [InlineData("0.12 / 0.16", null, "0.20", "0.12 / 0.16", null, "0.20")]
    [InlineData("0.1/0.2/0.3", null, null, "0.1/0.2/0.3", null, null)]
    [InlineData(null, null, null, null, null, null)]
    public void UmNumeroPorEspaco(string? f1, string? f2, string? f3, string? e1, string? e2, string? e3)
    {
        Assert.Equal(new[] { e1, e2, e3 }, JobOnFolgas.UmaPorEspaco(f1, f2, f3));
    }
}

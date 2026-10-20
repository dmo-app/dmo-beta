using DMO.Alpha.Infrastructure.Search;

namespace DMO.Alpha.Infrastructure.Tests.Search;

/// <summary>
/// Sweep de 19/10/2026, DB-8: um texto que começa por letra procura o início
/// da parte BQ, que é a que começa na primeira letra. «T1» encontrava a
/// ST100 pelo T a meio; agora só encontra o que vem logo a seguir aos
/// algarismos do início (0132T173) ou uma referência que começa por ele.
/// </summary>
public sealed class ReferenceMatchParteBqTests
{
    [Theory]
    [InlineData("ST100", "T1", false)]
    [InlineData("ST100", "T", false)]
    [InlineData("ST100", "S", true)]
    [InlineData("ST100", "st1", true)]
    [InlineData("0132T173", "T1", true)]
    [InlineData("1075C142", "C1", true)]
    [InlineData("1075C142", "142", false)]
    [InlineData("5447T173A", "A", false)]
    [InlineData("T194", "T1", true)]
    [InlineData("0132T173", "T1.", false)]
    // Continua a encontrar pelo início da referência e da parte BQ.
    [InlineData("5447T173", "T173", true)]
    [InlineData("5447T173", "t17", true)]
    [InlineData("5447T173", "5447", true)]
    [InlineData("5447T173", "5447T173", true)]
    [InlineData("5447T173", "447", false)]
    public void Matches_UmTextoComLetra_ComecaNaParteBq(string value, string term, bool expected)
    {
        Assert.Equal(expected, ReferenceMatch.Matches(value, term));
        Assert.Equal(expected, ReferenceMatch.Predicate<Holder>(h => h.Value, term).Compile()(new Holder(value)));
    }

    private sealed record Holder(string? Value);
}

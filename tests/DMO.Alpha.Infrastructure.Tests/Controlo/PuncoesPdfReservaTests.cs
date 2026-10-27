using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O PDF DA PU SAI SEMPRE (Diogo, 27/10/2026: «não consigo extrair PU»): com
/// a reserva cheia o PDF passava da largura da folha e não se gerava. Os
/// números da reserva ficam inteiros numa só linha.
/// </summary>
public class PuncoesPdfReservaTests
{
    [Theory]
    [InlineData(12, 4, 60)]
    [InlineData(12, 3, 200)]
    [InlineData(1, 4, 150)]
    public void Pdf_ComAReservaCheia_GeraSe_ComOsNumerosInteiros(int seccoes, int gotas, int total)
    {
        var rows = Enumerable.Range(0, total)
            .Select(i => new PuncaoReadModel(i + 1, i + 1, (1000 + i).ToString(), "12", true, true, false, "4301", DateTime.UtcNow))
            .ToList();
        var grelha = PuncoesPosicoes.Distribuir(rows, seccoes, gotas);
        var text = PdfText.Of(new PuncoesPosicoesPdfGenerator().GenerateBytes(new PuncoesPosicoesPdfGenerator.Input(
            "1075C142", "B1", "202604", null, null, "4301", grelha)));

        Assert.Contains($"Reserva {grelha.Reserva.Count}", text);
        Assert.Contains((1000 + total - 1).ToString(), text);
    }
}

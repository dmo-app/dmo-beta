using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O PDF das punções (DMO-508, DMO-511, DMO-516): cabe numa só folha A4 e
/// as linhas da folha seguem as gotas da linha, lidas pelo texto.
/// </summary>
public class PuncoesPosicoesPdfCoberturaBTests
{
    private static byte[] Pdf(int seccoes, int gotas, int total)
    {
        var rows = Enumerable.Range(0, total)
            .Select(i => new PuncaoReadModel(i + 1, i + 1, (1000 + i).ToString(), "12", true, true, false, "4301", DateTime.UtcNow))
            .ToList();
        var grelha = PuncoesPosicoes.Distribuir(rows, seccoes, gotas);
        return new PuncoesPosicoesPdfGenerator().GenerateBytes(new PuncoesPosicoesPdfGenerator.Input(
            "1075C142", "B1", "202604", null, null, "4301", grelha));
    }

    [Theory]
    [InlineData(12, 4, 20)]
    [InlineData(12, 4, 60)]
    [InlineData(12, 3, 200)]
    [InlineData(1, 4, 150)]
    [InlineData(40, 4, 160)]
    public void PdfDasPuncoes_SaiNumaSoFolhaA4_ComAReservaCheiaOuNao(int seccoes, int gotas, int total)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(Pdf(seccoes, gotas, total));

        Assert.Equal(1, document.NumberOfPages);
        var page = document.GetPage(1);
        // A4 deitada (297 x 210 mm), em pontos.
        Assert.InRange(page.Width, 841.0, 843.0);
        Assert.InRange(page.Height, 594.0, 596.0);
    }

    [Fact]
    public void PdfDasPuncoes_AsLinhasSaoAsGotasDaLinha_ETresGotasDeixamMaisNaReserva()
    {
        // 2 secções: com 3 gotas a linha leva 6 e 2 das 8 vão para a reserva;
        // com 4 gotas leva 8 e a reserva fica vazia; a linha D só existe com 4.
        var comTres = PdfText.Of(Pdf(2, 3, 8));
        var comQuatro = PdfText.Of(Pdf(2, 4, 8));

        Assert.Contains("Punções verificados 8", comTres);
        Assert.Contains("Reserva 2", comTres);
        Assert.Contains("1007", comTres);
        Assert.Contains("Reserva 0", comQuatro);

        var palavrasComTres = comTres.Split(' ', '\n');
        var palavrasComQuatro = comQuatro.Split(' ', '\n');
        foreach (var letra in new[] { "A", "B", "C" })
        {
            Assert.Contains(letra, palavrasComTres);
        }

        Assert.DoesNotContain("D", palavrasComTres);
        Assert.Contains("D", palavrasComQuatro);
    }
}

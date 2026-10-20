using UglyToad.PdfPig;

namespace DMO.Alpha.Infrastructure.Tests;

/// <summary>
/// O texto que está escrito num PDF gerado em memória, palavra a palavra e
/// página a página, para os testes verificarem o conteúdo e não só que o
/// ficheiro é um PDF (varredura de 18/10/2026, F1).
/// </summary>
public static class PdfText
{
    public static string Of(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join("\n", document.GetPages()
            .Select(page => string.Join(" ", page.GetWords().Select(word => word.Text))));
    }
}

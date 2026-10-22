using System.IO;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O RESOLVER de caminhos dos documentos do Controlo (o corte PDF, a
/// estrutura decidida pelo Owner 2026-10-11):
///
///   &lt;dir configurado&gt;/&lt;referência da produção&gt;/&lt;nº de produção&gt;/
///       PESO-&lt;referência&gt;-&lt;nº de produção&gt;.pdf
///       PEGAMENTOS-&lt;referência&gt;-&lt;nº de produção&gt;.pdf
///       RESUMO-&lt;referência&gt;-&lt;nº de produção&gt;.pdf
///
/// Exemplo (o do Owner):
///   D:\ControloDMO\5447T173\202601\PESO-5447T173-202601.pdf
///
/// Puro e determinístico: recebe o dir configurado, a referência e o nº de
/// produção; devolve o caminho da pasta da produção e os nomes canónicos
/// dos ficheiros. O caminho usa o separador do sistema; a referência e
/// o nº de produção são LIMPOS de caracteres inválidos de path (a app
/// nunca recusa por causa do separador no nome).
/// </summary>
public static class ControloDocumentPaths
{
    public static string GetProductionFolder(string outputDirectory, string reference, string productionNumber)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        ArgumentException.ThrowIfNullOrEmpty(reference);
        ArgumentException.ThrowIfNullOrEmpty(productionNumber);

        return Path.Combine(
            outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\'),
            SanitizeSegment(reference),
            SanitizeSegment(productionNumber));
    }

    public static string GetPesoFileName(string reference, string productionNumber) =>
        $"PESO-{SanitizeSegment(reference)}-{SanitizeSegment(productionNumber)}.pdf";

    /// <summary>
    /// O Peso N de uma produção com mais do que um (depois de trocar a CM ou
    /// a BQ): «-2», «-3»… no fim do nome; o primeiro fica com o nome de
    /// sempre (resposta R16-9 do especialista, 18/10/2026).
    /// </summary>
    public static string GetPesoFileName(string reference, string productionNumber, int pesoNumber) =>
        pesoNumber > 1
            ? $"PESO-{SanitizeSegment(reference)}-{SanitizeSegment(productionNumber)}-{pesoNumber}.pdf"
            : GetPesoFileName(reference, productionNumber);

    public static string GetPegamentosFileName(string reference, string productionNumber) =>
        $"PEGAMENTOS-{SanitizeSegment(reference)}-{SanitizeSegment(productionNumber)}.pdf";

    /// <summary>
    /// A folha N dos Pegamentos de uma produção com mais do que uma (depois
    /// de trocar a CM, a BQ ou o MF): «-2», «-3»… no fim do nome; a primeira
    /// fica com o nome de sempre (18/10/2026).
    /// </summary>
    public static string GetPegamentosFileName(string reference, string productionNumber, int sheetNumber) =>
        sheetNumber > 1
            ? $"PEGAMENTOS-{SanitizeSegment(reference)}-{SanitizeSegment(productionNumber)}-{sheetNumber}.pdf"
            : GetPegamentosFileName(reference, productionNumber);

    public static string GetPuncoesFileName(string reference, string productionNumber) =>
        $"PU-NNPB-{SanitizeSegment(reference)}-{SanitizeSegment(productionNumber)}.pdf";

    public static string GetResumoFileName(string reference, string productionNumber) =>
        $"RESUMO-{SanitizeSegment(reference)}-{SanitizeSegment(productionNumber)}.pdf";

    /// <summary>
    /// A subpasta da produção dentro da pasta escolhida no computador
    /// (Diogo, 17/10/2026: «eu crio a pasta Controlo 2026, e a app por si
    /// cria o resto dos diretórios dependendo da referência e produção»):
    /// «&lt;referência&gt;/&lt;nº de produção&gt;», com «/» como separador e
    /// os caracteres que o Windows não aceita num nome trocados por «_».
    /// Vai no cabeçalho <see cref="PdfFolderHeader"/> da resposta do PDF,
    /// codificada para URL; o browser cria as pastas e grava lá o ficheiro.
    /// </summary>
    public static string GetProductionRelativeFolder(string reference, string productionNumber)
    {
        // Sem referência ou sem produção (registos sem Job On) a pasta diz
        // isso mesmo, em vez de recusar o PDF.
        var referenceSegment = string.IsNullOrWhiteSpace(reference) ? "sem-referencia" : SanitizePortableSegment(reference);
        var productionSegment = string.IsNullOrWhiteSpace(productionNumber) ? "sem-producao" : SanitizePortableSegment(productionNumber);
        return $"{referenceSegment}/{productionSegment}";
    }

    /// <summary>O cabeçalho HTTP com a subpasta da produção do PDF.</summary>
    public const string PdfFolderHeader = "X-Dmo-Pdf-Pasta";

    /// <summary>
    /// O PDF que acompanha este (Diogo, 22/10/2026: o PDF das punções ao lado
    /// do PDF do Controlo): o endereço do POST que o gera, com os campos na
    /// query. O browser pede-o a seguir e grava-o na mesma pasta, ou
    /// descarrega-o sem pasta escolhida.
    /// </summary>
    public const string PdfCompanionHeader = "X-Dmo-Pdf-Acompanha";

    private static string SanitizePortableSegment(string value)
    {
        const string invalid = "<>:\"/\\|?*";
        var chars = SanitizeSegment(value).ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.IndexOf(chars[i]) >= 0 || char.IsControl(chars[i]))
            {
                chars[i] = '_';
            }
        }

        var segment = new string(chars).Trim().TrimEnd('.');
        return segment.Length == 0 ? "_" : segment;
    }

    /// <summary>
    /// Um segmento de path seguro: os caracteres inválidos do SO saem
    /// (o path sepaarador nunca vira estrutura — a app decide a estrutura;
    /// o documento carrega o nome que o operador escreveu, sem o recusar).
    /// </summary>
    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars).TrimEnd('.');
    }
}

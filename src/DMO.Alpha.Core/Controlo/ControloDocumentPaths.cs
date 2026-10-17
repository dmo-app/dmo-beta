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
/// dos ficheiros. A criação da pasta (EnsureDirectory) usa o separador do
/// sistema; a referência e o nº de produção são LIMPOS de caracteres
/// inválidos de path (a app nunca recusa por causa do separador no nome).
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

    public static string GetPegamentosFileName(string reference, string productionNumber) =>
        $"PEGAMENTOS-{SanitizeSegment(reference)}-{SanitizeSegment(productionNumber)}.pdf";

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
    /// Garante a pasta da produção (cria &lt;dir&gt;/&lt;ref&gt;/&lt;produção&gt; —
    /// e os pais — se ainda não existirem; idempotente) e devolve o
    /// caminho. A referência e o nº entram limpos no segmento.
    /// </summary>
    public static string EnsureProductionFolder(string outputDirectory, string reference, string productionNumber)
    {
        var folder = GetProductionFolder(outputDirectory, reference, productionNumber);
        Directory.CreateDirectory(folder);
        return folder;
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

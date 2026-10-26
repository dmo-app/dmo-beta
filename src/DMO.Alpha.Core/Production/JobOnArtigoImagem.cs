namespace DMO.Alpha.Core.Production;

/// <summary>
/// A IMAGEM DO ARTIGO (Diogo, 26/10/2026: «clicar no artigo deveria abrir
/// para associar imagem»): uma imagem por referência completa (MF + BQ, ex.
/// 9389T194), guardada na base do servidor e mostrada em todos os Job On
/// dessa referência. Trocar substitui; retirar apaga a linha (a folha volta
/// à silhueta).
/// </summary>
public sealed class JobOnArtigoImagem
{
    public const int ReferenciaMaxLength = 100;

    /// <summary>O limite de tamanho, em bytes.</summary>
    public const int TamanhoMaximo = 5 * 1024 * 1024;

    /// <summary>Os tipos aceites (sem SVG, que pode levar scripts).</summary>
    public static readonly IReadOnlyList<string> TiposAceites = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    /// <summary>A referência completa, em maiúsculas (a chave).</summary>
    public string ReferenciaTotal { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public byte[] Dados { get; set; } = null!;

    public DateTime AtualizadoEmUtc { get; set; }

    /// <summary>Quem associou a imagem (nome do operador ou do ADMIN).</summary>
    public string? AtualizadoPor { get; set; }

    public static string Normalizar(string? referencia) => (referencia ?? string.Empty).Trim().ToUpperInvariant();
}

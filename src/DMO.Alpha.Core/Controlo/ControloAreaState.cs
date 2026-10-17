namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O estado de uma área do documento do controlo (CM/BQ/MF/PU/CS): OK/NOK e
/// um comentário opcional, registados pelo operador. Uma linha por área; a
/// última gravação vence. Nunca é gate: é a informação do documento.
/// </summary>
public sealed class ControloAreaState
{
    public static readonly IReadOnlyList<string> Areas = ["CM", "BQ", "MF", "PU", "CS"];

    public const int CommentMaxLength = 500;

    public int Id { get; set; }

    public int ControloId { get; set; }

    public Controlo? Controlo { get; set; }

    /// <summary>A área do documento: CM, BQ, MF, PU ou CS.</summary>
    public string Area { get; set; } = null!;

    /// <summary>«OK», «NOK» ou null quando a área ainda não foi avaliada.</summary>
    public string? Status { get; set; }

    public string? Comment { get; set; }

    /// <summary>Quem gravou por último (nº de operador ou email do ADMIN).</summary>
    public string? UpdatedBy { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

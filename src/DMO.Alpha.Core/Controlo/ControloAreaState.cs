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

    public const int IdentificacaoMaxLength = 120;

    /// <summary>As áreas sem ferramenta na produção: a referência e o lote escrevem-se à mão.</summary>
    public static readonly IReadOnlyList<string> AreasSemFerramenta = ["PU", "CS"];

    public int Id { get; set; }

    public int ControloId { get; set; }

    public Controlo? Controlo { get; set; }

    /// <summary>A área do documento: CM, BQ, MF, PU ou CS.</summary>
    public string Area { get; set; } = null!;

    /// <summary>«OK», «NOK» ou null quando a área ainda não foi avaliada.</summary>
    public string? Status { get; set; }

    public string? Comment { get; set; }

    /// <summary>
    /// A referência e o lote escritos à mão no PU e no CS, que não têm
    /// ferramenta na produção (Diogo, 20/10/2026: «se for PU, CS deixar campo
    /// para escrever»). Null nas áreas com ferramenta.
    /// </summary>
    public string? Identificacao { get; set; }

    /// <summary>Quem gravou por último (nº de operador ou email do ADMIN).</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>O utilizador de <see cref="UpdatedBy"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? UpdatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

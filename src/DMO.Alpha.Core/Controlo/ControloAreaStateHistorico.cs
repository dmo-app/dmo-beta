namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Uma gravação do OK/NOK de uma área do controlo (Diogo, 19/10/2026, D13:
/// «se for do mesmo [controlo], sim. Existir sempre forma de mudar, e a
/// última mudança tem efeito»). Cada mudança do OK/NOK acrescenta uma linha
/// (quem, quando, o valor); a que vale é a de <see cref="ControloAreaState"/>,
/// a última. Os outros controlos não entram. Só se acrescenta, nunca se muda.
/// </summary>
public sealed class ControloAreaStateHistorico
{
    public int Id { get; set; }

    public int ControloId { get; set; }

    public Controlo? Controlo { get; set; }

    /// <summary>A área do documento: CM, BQ, MF, PU ou CS.</summary>
    public string Area { get; set; } = null!;

    /// <summary>«OK», «NOK» ou null quando a área voltou a «—».</summary>
    public string? Status { get; set; }

    /// <summary>Quem gravou (nº de operador ou email do ADMIN).</summary>
    public string? Por { get; set; }

    public DateTime EmUtc { get; set; }
}

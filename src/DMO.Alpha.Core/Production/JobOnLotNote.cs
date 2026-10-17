namespace DMO.Alpha.Core.Production;

/// <summary>
/// Uma MUDANÇA DE LOTE durante a produção (o proprietário, 16/10/2026: o
/// «Duplicar → Lote» «não muda a produção, deixa apenas uma nota»): num dia,
/// uma ferramenta da produção (CM, MF ou BQ) passa a outro lote registado no
/// Ferramentas. A produção, os seus contextos e o Controlo ficam como estão;
/// a nota aparece na linha da máquina em «Produções do dia» desse dia.
/// </summary>
public sealed class JobOnLotNote
{
    public static readonly IReadOnlyList<string> Slots = ["CM", "MF", "BQ"];

    public const int NoteMaxLength = 200;

    public int Id { get; set; }

    public int JobOnId { get; set; }

    public JobOn? JobOn { get; set; }

    /// <summary>O dia da mudança.</summary>
    public DateOnly Dia { get; set; }

    /// <summary>A ferramenta que muda de lote: CM, MF ou BQ.</summary>
    public string Slot { get; set; } = null!;

    /// <summary>A Tool registada do lote novo.</summary>
    public string NewToolId { get; set; } = null!;

    /// <summary>A referência da ferramenta, congelada ao gravar.</summary>
    public string ToolReferenceFrozen { get; set; } = null!;

    /// <summary>O lote em uso antes da mudança (null quando a produção não tinha esta ferramenta).</summary>
    public string? PreviousLotFrozen { get; set; }

    /// <summary>O lote novo, congelado ao gravar.</summary>
    public string NewLotFrozen { get; set; } = null!;

    /// <summary>Texto livre opcional.</summary>
    public string? Note { get; set; }

    /// <summary>Quem registou (nº de operador ou email do ADMIN).</summary>
    public string? CreatedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>O texto da nota: «Muda BQ lote 6 para 7», mais o texto livre quando existe.</summary>
    public static string Describe(string slot, string? previousLot, string newLot, string? note)
    {
        var change = previousLot is null
            ? $"Muda {slot} para o lote {newLot}"
            : $"Muda {slot} lote {previousLot} para {newLot}";
        return string.IsNullOrWhiteSpace(note) ? change : $"{change} · {note}";
    }
}

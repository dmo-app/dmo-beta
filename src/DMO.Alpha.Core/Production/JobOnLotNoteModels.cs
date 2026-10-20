namespace DMO.Alpha.Core.Production;

/// <summary>Uma mudança de lote registada, pronta a mostrar.</summary>
public sealed record LotNoteView(int JobOnId, DateOnly Dia, string Slot, string Text, string? CreatedBy);

/// <summary>A próxima troca de lote de uma produção: o dia e o que muda.</summary>
public sealed record NextLotChange(int JobOnId, DateOnly Dia, string Slot, string? PreviousLot, string NewLot)
{
    /// <summary>«BQ lote 165 para 166», ou «BQ para o lote 166» sem lote anterior.</summary>
    public string Texto => PreviousLot is null ? $"{Slot} para o lote {NewLot}" : $"{Slot} lote {PreviousLot} para {NewLot}";
}

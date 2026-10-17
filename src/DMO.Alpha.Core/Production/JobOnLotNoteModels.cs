namespace DMO.Alpha.Core.Production;

/// <summary>Resultado tipado de registar uma mudança de lote.</summary>
public enum AddLotNoteResult
{
    Success,

    /// <summary>A produção indicada não existe.</summary>
    JobOnNotFound,

    /// <summary>Falta o dia da mudança.</summary>
    MissingDay,

    /// <summary>O dia fica fora da janela da produção.</summary>
    DayOutsideProduction,

    /// <summary>O lote escolhido não é um lote registado de uma ferramenta desta produção.</summary>
    LotNotFound,

    /// <summary>O lote escolhido já é o que está em uso nesse dia.</summary>
    SameLot,

    /// <summary>O texto livre passa do limite.</summary>
    NoteTooLong
}

/// <summary>Um lote que a produção pode passar a usar: outra Tool registada com a mesma referência e tipo de uma das ferramentas dela.</summary>
public sealed record LotCandidate(string Slot, string ToolId, string Reference, string Lot);

/// <summary>Uma mudança de lote registada, pronta a mostrar.</summary>
public sealed record LotNoteView(int JobOnId, DateOnly Dia, string Slot, string Text, string? CreatedBy);

/// <summary>A próxima troca de lote de uma produção: o dia e o que muda.</summary>
public sealed record NextLotChange(int JobOnId, DateOnly Dia, string Slot, string? PreviousLot, string NewLot)
{
    /// <summary>«BQ lote 165 para 166», ou «BQ para o lote 166» sem lote anterior.</summary>
    public string Texto => PreviousLot is null ? $"{Slot} para o lote {NewLot}" : $"{Slot} lote {PreviousLot} para {NewLot}";
}

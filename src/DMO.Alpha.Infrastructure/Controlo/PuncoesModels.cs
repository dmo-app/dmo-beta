
namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>De onde vem o NNPB da produção.</summary>
public enum PuncoesNnpbSource
{
    /// <summary>Sem processo na ficha da CM e sem «NN» no nome técnico.</summary>
    Nenhuma = 0,

    /// <summary>O «Processo» da ficha técnica da CM.</summary>
    Ficha = 1,

    /// <summary>O «NN» do nome técnico da CM (a ficha sem processo).</summary>
    NomeTecnico = 2
}

public enum PuncaoSeenField
{
    Puncao = 1,

    Arrefecedor = 2
}

public enum PuncoesResult
{
    Success = 0,
    ControloNotFound,
    NotNnpb,
    MissingPunchReference,
    InvalidLot,
    MissingNumber,
    InspecaoNotFound,
    MissingReference,
    RegistoNotFound,
    AlreadyAssociated,
    ControloHasRegisto,
    /// <summary>Já há uma punção com este número e este lote: é a mesma punção (Diogo, 24/10/2026).</summary>
    DuplicatePunch
}

/// <summary>
/// A folha onde se escreve: a produção (controlo_id) ou um registo pelo
/// seu puncoes_id (o registo sem Job On).
/// </summary>
public readonly record struct PuncoesTarget(int? ControloId, int? PuncoesId)
{
    public static PuncoesTarget ForControlo(int controloId) => new(controloId, null);

    public static PuncoesTarget ForRegisto(int puncoesId) => new(null, puncoesId);
}

/// <summary>Um registo aberto pelo puncoes_id; ControloId null enquanto não estiver associado.</summary>
public sealed record PuncoesStandaloneSheet(
    int PuncoesId,
    string Reference,
    int? ControloId,
    PuncoesReadModel Registo)
{
    /// <summary>O modelo da referência total, a sugestão da referência da punção.</summary>
    public string? DefaultPunchReference => PuncoesHandler.DefaultPunchReference(Reference);
}

/// <summary>Uma linha da lista dos registos sem Job On de uma referência.</summary>
public sealed record PuncoesStandaloneSummary(
    int PuncoesId,
    string Reference,
    string PunchReference,
    string Lot,
    int Total,
    string? UpdatedBy,
    DateTime UpdatedAtUtc);

/// <summary>A folha das punções de um controlo: a produção, a CM e o registo quando existe.</summary>
public sealed record PuncoesSheet(
    int ControloId,
    int JobOnId,
    string Reference,
    string ProductionNumber,
    string Machine,
    string? CmReference,
    string? CmLot,
    /// <summary>O «Processo» da ficha da CM (NNPB/PS); null quando por preencher.</summary>
    string? CmProcess,
    bool IsNnpb,
    PuncoesNnpbSource NnpbSource,
    string? DefaultPunchReference,
    PuncoesReadModel? Registo)
{
    /// <summary>A ferramenta CM da produção, para o aviso do processo em falta levar à ficha dela; null sem CM.</summary>
    public string? CmToolId { get; init; }

    /// <summary>As datas da produção no Job On (início e fim), para o PDF das posições.</summary>
    public DateOnly? InicioEm { get; init; }

    public DateOnly? FimEm { get; init; }
}

public sealed record PuncoesReadModel(
    string PunchReference,
    string Lot,
    IReadOnlyList<PuncaoReadModel> Rows,
    int PunchSeenCount,
    int CoolerSeenCount,
    string? UpdatedBy,
    DateTime UpdatedAtUtc)
{
    public int Total => Rows.Count;
}

public sealed record PuncaoReadModel(
    int Id,
    int Position,
    string Number,
    string Lot,
    bool PunchSeen,
    bool CoolerSeen,
    bool IsAlteracao,
    string? RegisteredBy,
    DateTime RegisteredAtUtc);

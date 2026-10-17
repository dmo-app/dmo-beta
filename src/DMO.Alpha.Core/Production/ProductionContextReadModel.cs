namespace DMO.Alpha.Core.Production;

/// <summary>
/// Read model purpose-specific para o contexto mínimo de uma produção.
/// Não é uma entidade nem um aggregate genérico; é uma projecção plana
/// devolvida pela query <see cref="IProductionContextQuery"/>.
/// </summary>
public sealed record ProductionContextReadModel(
    int JobOnId,
    string Reference,
    string ProductionNumber,
    string Machine,
    int? CmId,
    string? CmToolId,
    int? MfId,
    string? MfToolId,
    int? BqId,
    string? BqToolId,
    /// <summary>A janela temporal da produção (o corte B): o dia em que entra em máquina; null = em aberto.</summary>
    DateOnly? InicioEm = null,
    /// <summary>O dia em que sai de máquina; null = até aviso.</summary>
    DateOnly? FimEm = null,
    /// <summary>A CALOTE DESTA PRODUÇÃO (mm) — o campo j-calote da ficha do dmo-html: null = a referência da Tool manda (o apoio TP/calote do Peso usa-a automaticamente — a B1). Aditivo por posição: os consumidores existentes só leem os campos que lhes dizem respeito.</summary>
    decimal? CaloteMm = null);

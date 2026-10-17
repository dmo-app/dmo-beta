namespace DMO.Alpha.Core.Production;

/// <summary>
/// Read model purpose-specific do browser «Procurar resumo» (Resumo do
/// Controlo): uma produção encontrável com os factos mínimos de
/// identificação — jobon_id, nº de produção, referência e máquina. Não é
/// uma entidade nem um aggregate; é uma projeção plana devolvida pela
/// <see cref="IResumoProductionQuery"/> para o utilizador escolher
/// explicitamente qual produção abrir.
/// </summary>
public sealed record ResumoProductionItem(
    int JobOnId,
    string ProductionNumber,
    string Reference,
    string Machine);

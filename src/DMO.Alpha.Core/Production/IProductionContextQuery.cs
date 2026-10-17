namespace DMO.Alpha.Core.Production;

/// <summary>
/// Query purpose-specific: resolve o contexto mínimo de um Job On.
/// </summary>
public interface IProductionContextQuery
{
    /// <summary>
    /// Devolve o contexto mínimo do Job On identificado por <paramref name="jobOnId"/>.
    /// </summary>
    /// <returns>Read model com os campos essenciais, ou <c>null</c> se o Job On não existir.</returns>
    Task<ProductionContextReadModel?> GetAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devolve o contexto mínimo da produção do controlo dado (o corte C —
    /// o Peso production-scoped chega pelo controlo_id). Null quando o
    /// controlo não existe.
    /// </summary>
    Task<ProductionContextReadModel?> GetByControloAsync(int controloId, CancellationToken cancellationToken = default);
}

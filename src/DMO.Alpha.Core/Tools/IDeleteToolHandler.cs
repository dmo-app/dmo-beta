namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface de escrita canónica: eliminar uma Tool numa transação canónica
/// (backfill congelados → reatribuição Pesos/Pegamentos à DUMMY → traces
/// não resolvidos → NULL → apagar links/contexto técnico/tool). A
/// eliminação nunca destrói histórico: os factos congelados e os
/// contextos de produção permanecem.
/// </summary>
public interface IDeleteToolHandler
{
    Task<DeleteToolOutcome> HandleAsync(
        DeleteTool command,
        CancellationToken cancellationToken = default);
}

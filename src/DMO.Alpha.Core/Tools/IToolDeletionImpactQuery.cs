namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Surface só de leitura do aviso de eliminação: o impacto corrente de
/// eliminar uma Tool (produções que a referem + contagens dos registos de
/// consumo que serão reatribuídos à DUMMY ou cuja âncora será limpa).
/// Devolve null quando o tool_id não existe (ou é a DUMMY) — nenhum
/// impacto é inventado.
/// </summary>
public interface IToolDeletionImpactQuery
{
    Task<ToolDeletionImpactReadModel?> FindByToolIdAsync(
        string toolId,
        CancellationToken cancellationToken = default);
}

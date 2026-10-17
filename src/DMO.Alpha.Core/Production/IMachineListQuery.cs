namespace DMO.Alpha.Core.Production;

/// <summary>
/// Surface só de leitura das máquinas de produção persistidas, para
/// seleção humana explícita no fluxo de criação do Job On. Não é um
/// repositório genérico: devolve a lista das máquinas registadas
/// (ordenada por code, determinística) — nunca inventa máquinas, nunca
/// auto-seleciona. A lista vazia é o estado truthful de um registo
/// ainda não povoado, não um erro.
/// </summary>
public interface IMachineListQuery
{
    /// <summary>
    /// Devolve as máquinas de produção atualmente persistidas, projetadas
    /// dos factos reais e ordenadas por code.
    /// </summary>
    Task<IReadOnlyList<MachineOption>> ListAsync(CancellationToken cancellationToken = default);
}

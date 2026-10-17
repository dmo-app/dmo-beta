using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Leitura canónica da configuração do Controlo: a densidade do vidro
/// configurada por processo de fabrico. Contrato de leitura apenas — a
/// edição das definições pertence à superfície de administração; o Peso
/// consome o valor aplicável ao processo da CM e preserva-o como evidência
/// histórica no Peso criado.
/// </summary>
public interface IGlassDensityConfiguration
{
    /// <summary>
    /// Devolve a densidade do vidro (g/cm³) configurada para o processo, ou
    /// null quando o processo ainda não tem densidade configurada.
    /// </summary>
    Task<decimal?> FindByProcessAsync(
        CmProcess process,
        CancellationToken cancellationToken = default);
}

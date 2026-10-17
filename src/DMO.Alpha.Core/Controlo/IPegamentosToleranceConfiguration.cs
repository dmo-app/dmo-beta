namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Leitura canónica da configuração do Controlo: a tolerância do corredor
/// de Pegamentos configurada (CONFIGURATION.md L20). Contrato de leitura
/// apenas — a edição pertence à superfície de administração; a criação de
/// um registo de Pegamentos consome o valor efetivo (configurada quando
/// existir, default do Owner na sua falta) e preserva-o congelado no
/// registo como evidência histórica.
/// </summary>
public interface IPegamentosToleranceConfiguration
{
    /// <summary>
    /// Devolve a tolerância (mm) configurada para o corredor de Pegamentos,
    /// ou null quando ainda não há configuração registada (o default
    /// canónico ±0,20 do Owner aplica-se — nunca locked no cálculo).
    /// </summary>
    Task<decimal?> FindConfiguredAsync(CancellationToken cancellationToken = default);
}

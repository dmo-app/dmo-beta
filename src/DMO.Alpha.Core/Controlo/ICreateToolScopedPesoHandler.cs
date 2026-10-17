namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Handler do comando de criação de um Peso tool-scoped. Cria o peso_id
/// durável ancorado às Tools reais, congela os valores consumidos como
/// evidência histórica e completa na Tool os valores técnicos em falta
/// fornecidos (regra Ferramentas). Nada é escrito no Job On nem em qualquer
/// contexto de produção — nenhuma identidade de produção é fabricada.
/// </summary>
public interface ICreateToolScopedPesoHandler
{
    Task<CreateToolScopedPesoOutcome> HandleAsync(
        CreateToolScopedPeso command,
        CancellationToken cancellationToken = default);
}

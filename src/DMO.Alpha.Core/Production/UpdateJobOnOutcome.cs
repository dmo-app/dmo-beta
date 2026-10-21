namespace DMO.Alpha.Core.Production;

/// <summary>
/// Resultado da edição: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific do contexto de produção re-estabelecido — o
/// MESMO <see cref="ProductionContextReadModel"/> que a query canónica
/// serve, com o jobon_id INALTERADO (a identidade da produção é
/// imutável na edição) e os contextos aplicáveis ancorados às Tools
/// recém-selecionadas. O <see cref="OverlapWarning"/> é o AVISO
/// INFORMATIVO da sobreposição da janela RE-PLANEADA na mesma máquina
/// (a produção está gravada na mesma — nunca bloqueio): as produções em
/// conflito, as janelas, o período exato e como resolver; null sem
/// conflito.
/// </summary>
public sealed record UpdateJobOnOutcome(
    UpdateJobOnResult Result,
    ProductionContextReadModel? JobOn = null,
    OverlapWarning? OverlapWarning = null,
    // A chave da folha que passou o máximo (a recusa TextoLongo); null nas outras.
    string? ChaveRecusada = null);

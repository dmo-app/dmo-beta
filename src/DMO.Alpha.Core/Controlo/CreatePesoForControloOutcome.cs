namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Outcome do Peso production-scoped: o resultado tipado e, em caso de
/// sucesso, o read model do Peso do controlo (criado agora ou aberto do
/// já existente — o mesmo shape do tool-scoped).
/// </summary>
public sealed record CreatePesoForControloOutcome(
    CreatePesoForControloResult Result,
    CreatedPeso? Peso = null);

/// <summary>
/// Interface do handler de criação/abertura do Peso production-scoped.
/// </summary>
public interface ICreatePesoForControloHandler
{
    Task<CreatePesoForControloOutcome> HandleAsync(
        CreatePesoForControlo command,
        CancellationToken cancellationToken = default);
}

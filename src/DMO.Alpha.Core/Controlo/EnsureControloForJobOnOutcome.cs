namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado do ensure: o outcome tipado e, em caso de sucesso, o
/// controlo_id canónico da produção — o MESMO id em re-execuções
/// (get-or-create por jobon_id; abertura idempotente).
/// </summary>
public sealed record EnsureControloForJobOnOutcome(
    EnsureControloResult Result,
    int? ControloId = null);

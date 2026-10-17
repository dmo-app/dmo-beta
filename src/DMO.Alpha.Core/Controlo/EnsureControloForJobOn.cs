namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Comando: assegurar o controlo_id de uma produção (get-or-create
/// idempotente por jobon_id). Não é um ato de criação do utilizador — é
/// bookkeeping idempotente do registo durável que a spec associa a cada
/// Job On desde a criação. O jobon_id é a âncora canónica persistida; o
/// backend devolve o MESMO controlo_id em re-execuções.
/// </summary>
public sealed record EnsureControloForJobOn(int JobOnId);

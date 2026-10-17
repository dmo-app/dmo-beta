namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Comando: criar (ou abrir) o Peso PRODUCTION-SCOPED de um controlo — o
/// corte C (spec PESO.md «Production-scoped Peso»). O controlo_id resolve o
/// contexto de produção estabelecido (o Job On e as suas Tools canónicas);
/// as ferramentas NUNCA são escolhidas aqui — a CM e a BQ vêm do contexto
/// (a CM pesada e a BQ/marisa cujo volume_marisa o cálculo consome). O
/// backend é get-or-open idempotente: se o controlo já tem o seu Peso
/// baseline, o MESMO peso_id é devolvido (a associação é 1:1 — um novo Peso
/// exigiria uma nova produção; a associação consome o Peso).
/// Os campos opcionais de contexto técnico transportam APENAS valores
/// Tool-owned EM FALTA coletados no fluxo (regra Ferramentas): gravam no
/// tool_id owner, nunca como facto privado do Peso.
/// </summary>
public sealed record CreatePesoForControlo(
    int ControloId,
    decimal WaterTemperatureC,
    string? CmProcessToken = null,
    decimal? CmVolumePuncaoCm3 = null,
    decimal? CmNominalWeightGrams = null,
    decimal? BqVolumeMarisaCm3 = null);

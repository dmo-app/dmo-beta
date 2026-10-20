namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Comando: criar (ou abrir) o Peso PRODUCTION-SCOPED de um controlo — o
/// corte C (spec PESO.md «Production-scoped Peso»). O controlo_id resolve o
/// contexto de produção estabelecido (o Job On e as suas Tools canónicas);
/// as ferramentas NUNCA são escolhidas aqui — a CM e a BQ vêm do contexto
/// (a CM pesada e a BQ/marisa cujo volume_marisa o cálculo consome). O
/// backend é get-or-open idempotente para a CM e a BQ que a produção tem
/// agora: se o controlo já tem o Peso dessa combinação, o MESMO peso_id é
/// devolvido. Depois de trocar a CM ou a BQ a meio da produção abre um Peso
/// novo, que traz a produção anterior (o peso do SAP) do Peso que a produção
/// já tinha, ainda editável; voltar a uma combinação já usada reabre o Peso
/// dela (respostas R15, R16-2, R16-5 e R16-7 do especialista, 18/10/2026).
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

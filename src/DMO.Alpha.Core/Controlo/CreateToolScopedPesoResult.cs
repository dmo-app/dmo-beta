namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado tipado da criação de um Peso tool-scoped.
/// </summary>
public enum CreateToolScopedPesoResult
{
    Success,

    /// <summary>O tool_id da CM não resolve a Tool canónica no registo Ferramentas.</summary>
    CmToolNotFound,

    /// <summary>O tool_id da BQ não resolve a Tool canónica no registo Ferramentas.</summary>
    BqToolNotFound,

    /// <summary>
    /// O processo da CM continua em falta: nem o contexto técnico da Tool o
    /// tem registado nem foi fornecido para completar.
    /// </summary>
    MissingCmProcess,

    /// <summary>O token do processo fornecido não é um dos autorizados (NNPB, PS).</summary>
    UnknownProcessToken,

    /// <summary>volume_puncao da CM em falta após a tentativa de completar.</summary>
    MissingCmVolumePuncao,

    /// <summary>volume_marisa da BQ em falta após a tentativa de completar.</summary>
    MissingBqVolumeMarisa,

    /// <summary>
    /// A densidade do vidro do processo aplicável não está configurada na
    /// configuração do Controlo.
    /// </summary>
    GlassDensityNotConfigured,

    /// <summary>
    /// A temperatura da água, arredondada ao grau, está fora do intervalo
    /// suportado 5–35 °C (a tabela canónica não a resolve).
    /// </summary>
    InvalidWaterTemperature,

    /// <summary>
    /// Um valor técnico fornecido é fisicamente inválido (o P2 do
    /// stresstest 2026-10-12: volume_puncao/marisa ou peso nominal
    /// NEGATIVOS ou zero eram ACEITES calados): os volumes (cm³) e o
    /// peso nominal (g) são grandezas físicas positivas — zero ou
    /// negativo é lixo de input, nunca um facto observável.
    /// </summary>
    InvalidTechnicalValue,

    /// <summary>
    /// Peso nominal preenchido com texto que não representa um decimal.
    /// Desde a beta.65 a página já não recusa por isto (o nominal é
    /// opcional: o Peso é criado sem ele e um aviso explica); o valor
    /// fica no vocabulário para os links antigos.
    /// </summary>
    InvalidNominalWeight,

    /// <summary>A temperatura da água veio vazia ou com texto que não é um número (não é «fora do intervalo»).</summary>
    UnreadableWaterTemperature,

    /// <summary>O volume punção da CM foi escrito mas é ilegível (não é «não registado»).</summary>
    UnreadableCmVolumePuncao,

    /// <summary>O volume marisa da BQ foi escrito mas é ilegível (não é «não registado»).</summary>
    UnreadableBqVolumeMarisa,

    /// <summary>
    /// O Peso para o qual se cria um Peso só para comparação não existe, não
    /// é de uma produção ou é ele próprio só para comparação.
    /// </summary>
    ComparacaoPesoNotFound
}

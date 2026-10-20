namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado tipado da criação/abertura do Peso production-scoped. As
/// recusas de criação delegadas (Missing*/NotConfigured/Invalid) usam os
/// MESMOS nomes do slice tool-scoped: a causa real é a mesma, só a origem
/// das ferramentas muda (o contexto de produção em vez do input).
/// </summary>
public enum CreatePesoForControloResult
{
    /// <summary>O Peso da combinação CM+BQ atual do controlo foi criado — ou já existia e foi aberto (get-or-open).</summary>
    Success,

    /// <summary>O controlo_id não resolve nenhum controlo.</summary>
    ControloNotFound,

    /// <summary>O Job On da produção não tem contexto CM — o peso não tem ferramenta para pesar.</summary>
    ContextMissingCm,

    /// <summary>
    /// O Job On da produção não tem contexto BQ — o cálculo principal
    /// consome o volume_marisa da BQ (marisa) usada na pesagem.
    /// </summary>
    ContextMissingBq,

    /// <summary>O tool_id da CM herdado do contexto não resolve a Tool canónica.</summary>
    CmToolNotFound,

    /// <summary>O tool_id da BQ herdado do contexto não resolve a Tool canónica.</summary>
    BqToolNotFound,

    /// <summary>O processo da CM continua em falta (nem o técnico da Tool nem o input do fluxo).</summary>
    MissingCmProcess,

    /// <summary>O token do processo fornecido não é um dos autorizados (NNPB, PS).</summary>
    UnknownProcessToken,

    /// <summary>volume_puncao da CM em falta após a tentativa de completar.</summary>
    MissingCmVolumePuncao,

    /// <summary>volume_marisa da BQ em falta após a tentativa de completar.</summary>
    MissingBqVolumeMarisa,

    /// <summary>A densidade do vidro do processo aplicável não está configurada.</summary>
    GlassDensityNotConfigured,

    /// <summary>A temperatura da água (grau inteiro) está fora de 5–35 °C.</summary>
    InvalidWaterTemperature,

    /// <summary>Um valor técnico fornecido é fisicamente inválido (volumes/peso nominal ≤ 0).</summary>
    InvalidTechnicalValue,

    /// <summary>
    /// Peso nominal preenchido com texto que não representa um decimal.
    /// Desde a beta.65 a página já não recusa por isto (o Peso é criado
    /// sem nominal e um aviso explica); fica para os links antigos.
    /// </summary>
    InvalidNominalWeight,

    /// <summary>A temperatura da água veio vazia ou com texto que não é um número.</summary>
    UnreadableWaterTemperature,

    /// <summary>O volume punção da CM foi escrito mas é ilegível.</summary>
    UnreadableCmVolumePuncao,

    /// <summary>O volume marisa da BQ foi escrito mas é ilegível.</summary>
    UnreadableBqVolumeMarisa
}

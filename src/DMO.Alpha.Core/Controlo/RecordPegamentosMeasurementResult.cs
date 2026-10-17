namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado tipado do registo de uma medição de Pegamentos.
/// </summary>
public enum RecordPegamentosMeasurementResult
{
    Success,

    /// <summary>O controlo_id não existe — nenhum registo nem medição são criados.</summary>
    ControloNotFound,

    /// <summary>O token do componente não é um dos canónicos (CM/BQ/MF).</summary>
    UnknownComponent,

    /// <summary>
    /// O componente pedido não existe no contexto desta produção (por
    /// exemplo MF numa produção sem MF) — a tool nunca é inventada e
    /// nenhuma medição é registada.
    /// </summary>
    ComponentNotInProduction,

    /// <summary>
    /// Sem nominal do gargalo: nem a ferramenta o tem nem foi escrito um valor legível. Nada é
    /// gravado (Diogo, 17/10/2026: «não dá para fazer Pegamentos sem nominal do gargalo»).
    /// </summary>
    MissingGargalo,

    /// <summary>Costura em falta ou ilegível (a contra costura ilegível grava-se vazia, com aviso).</summary>
    InvalidMeasurementValue,

    /// <summary>O pegamentos_id indicado não existe: nenhuma medição foi registada.</summary>
    PegamentosNotFound
}

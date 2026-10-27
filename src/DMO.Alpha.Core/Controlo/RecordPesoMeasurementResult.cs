namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Resultado tipado do registo de uma leitura de Peso.
/// </summary>
public enum RecordPesoMeasurementResult
{
    Success,

    /// <summary>O peso_id não existe.</summary>
    PesoNotFound,

    /// <summary>
    /// Com esta massa de água a capacidade ou o peso do vidro dão 0 ou menos:
    /// fisicamente impossível (Diogo, 18/10/2026). Nenhuma leitura é gravada.
    /// </summary>
    ResultNonPositive,

    /// <summary>
    /// O Peso indicado é de uma CM ou de uma BQ que a produção já não tem (a
    /// CM ou a BQ mudou a meio da produção): não recebe leituras novas, que
    /// vão para o Peso das ferramentas atuais. O que já tem fica como estava
    /// (resposta R16-3 do especialista, 18/10/2026). Nenhuma leitura é
    /// gravada.
    /// </summary>
    PesoNotCurrent,

    /// <summary>
    /// O mesmo envio já tinha sido gravado (a ligação caiu depois de gravar,
    /// a app reiniciou pelo meio): nada é gravado outra vez.
    /// </summary>
    AlreadyRecorded,

    /// <summary>
    /// O identificador do envio já foi usado por outra pessoa, noutra
    /// operação ou com outras leituras: recusado, nada é gravado.
    /// </summary>
    OperationConflict
}

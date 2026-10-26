namespace DMO.Alpha.Core.Production;

/// <summary>
/// Comando: criar um Job On — uma ocorrência real de produção. Input do
/// utilizador: o nº de produção, a referência e a máquina (âncora canónica
/// persistida em Machines), e os tool_ids canónicos EXPLICITAMENTE
/// selecionados para os contextos aplicáveis (CM/MF/BQ). Um slot null
/// significa «não aplicável»: nenhum contexto desse tipo é criado (spec
/// Job On: «Only applicable component contexts are created»). O backend
/// emite o jobon_id, os cm_id/mf_id/bq_id e o controlo_id associado
/// (spec Job On L33) numa única escrita atómica — o contexto de produção
/// que os módulos seguintes consomem. O registo Ferramentas nunca é
/// mutado: o Job On apenas consome os tool_ids canónicos selecionados.
/// </summary>
public sealed record CreateJobOn(
    string ProductionNumber,
    string Reference,
    int MachineId,
    string? CmToolId = null,
    string? MfToolId = null,
    string? BqToolId = null,
    // O dia em que a produção entra em máquina (o corte B); null = ainda sem janela.
    DateOnly? InicioEm = null,
    // O dia em que sai de máquina; null = até aviso.
    DateOnly? FimEm = null,
    // A CALOTE DESTA PRODUÇÃO (mm) — o campo j-calote da ficha do
    // dmo-html («Calote desta produção (mm)», placeholder «Usar referência
    // da Tool»): a calote efetiva da produção quando existe; null = a
    // referência da Tool manda. Consumida pelo apoio TP/calote do Peso
    // (a B1) — nunca um gate. Negativa é recusa tipada (InvalidCalote).
    decimal? CaloteMm = null,
    // A FOLHA DA FÁBRICA: os campos escritos por chave do catálogo
    // (JobOnFolhaCampos) e as linhas CAL, pela ordem. Null = o envio não traz
    // a folha (nada se grava nela). Gravam na mesma escrita do Job On.
    IReadOnlyDictionary<string, string?>? Folha = null,
    IReadOnlyList<LinhaCalEnvio>? Cal = null,
    // O diâmetro D (mm) do tampão desta produção (Diogo, 23/10/2026); zero ou
    // negativo é recusa tipada (InvalidTampao); null = ainda não registado.
    decimal? TampaoDiametroMm = null,
    // Experiência em vez de produção (Diogo, 26/10/2026): número AAAA00 e entra e sai no mesmo dia.
    bool Experiencia = false);

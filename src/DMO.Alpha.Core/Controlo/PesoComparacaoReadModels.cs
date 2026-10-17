namespace DMO.Alpha.Core.Controlo;

/// <summary>Uma leitura de contra-molde submetida (o número/posição CM e a massa de água em g).</summary>
public sealed record PesoComparacaoLeituraInput(string CmSubject, decimal WaterWeightGrams);

/// <summary>
/// O resultado do cálculo da comparação (a comparação ao vivo e a criação do
/// registo partilham o MESMO vocabulário fechado): Sucesso e as recusas
/// tipadas em PT na apresentação (o peso inicial não selecionado, o
/// contra-molde por escolher, a temperatura fora da tabela, a massa de água
/// não utilizável, sem leituras, o volume punção indisponível e o resultado
/// não positivo). Nenhuma recusa bloqueia o Peso — são recusas do próprio
/// ato de comparar, com a causa explicada.
/// </summary>
public enum PesoComparacaoResultado
{
    Sucesso = 0,
    PesoInicialNaoSelecionado,
    PesoInicialNaoEncontrado,
    ContraMoldePorEscolher,
    TemperaturaInvalida,
    MassaAguaIlegivel,
    MassaAguaNaoPositiva,
    SemLeituras,
    VolumePuncaoIndisponivel,
    ResultadoNaoPositivo,

    /// <summary>A temperatura da água veio vazia ou ilegível (não é «fora do intervalo»).</summary>
    TemperaturaIlegivel
}

/// <summary>
/// Porque uma leitura ficou de fora da comparação (a regra do proprietário,
/// 16/10/2026: «tentar sempre warning, desde que não parta nada» — uma
/// leitura má já não anula a comparação toda; as outras comparam-se e o
/// aviso diz qual ficou de fora).
/// </summary>
public enum PesoComparacaoMotivoIgnorada
{
    /// <summary>A massa de água foi escrita mas não é um número.</summary>
    MassaIlegivel,

    /// <summary>O nº de CM foi escrito sem massa de água.</summary>
    SemMassa,

    /// <summary>A massa de água é zero ou negativa.</summary>
    MassaNaoPositiva,

    /// <summary>O peso calculado da leitura é zero ou negativo.</summary>
    ResultadoNaoPositivo
}

/// <summary>
/// Uma leitura deixada de fora da comparação: a posição dela no pedido
/// (1-based), o nº de CM, a massa (quando legível) e o motivo.
/// </summary>
public sealed record PesoComparacaoLeituraIgnorada(
    int Indice,
    string CmSubject,
    decimal? WaterWeightGrams,
    PesoComparacaoMotivoIgnorada Motivo);

/// <summary>
/// O pedido da comparação das novas leituras dos contra-moldes contra o
/// Peso inicial (a superfície própria «Comparação de Peso» do dmo-html): o
/// peso inicial (peso_id), o contra-molde (opcional — na falta, o CM do
/// próprio Peso inicial, o fallback do html), a temperatura da água e as
/// leituras.
/// </summary>
public sealed record PesoComparacaoLeiturasPedido(
    int PesoInicialId,
    string? CmToolId,
    decimal WaterTemperatureC,
    IReadOnlyList<PesoComparacaoLeituraInput> Leituras);

/// <summary>Uma leitura calculada: o facto observado e os derivados do backend.</summary>
public sealed record PesoComparacaoLeituraReadModel(
    int RowPosition,
    string CmSubject,
    decimal WaterWeightGrams,
    decimal CapacityCm3,
    decimal GlassWeightGrams);

/// <summary>
/// O resultado da comparação das novas leituras (calculado PELO BACKEND pela
/// <see cref="PesoCalculation"/> + <see cref="PesoComparacaoCalculation"/>):
/// a base consumida (para a exibição e o congelamento no registo), as
/// leituras com os valores derivados, as médias e os pares por posição do
/// CM com o «Sem contraparte». Em recusa, só o <see cref="Refusal"/> vem
/// preenchido — nenhum valor é inventado.
/// </summary>
public sealed record PesoComparacaoLeiturasResultado(
    PesoComparacaoResultado Refusal,
    int PesoInicialId,
    string? CmToolId,
    string? CmToolReference,
    string? CmToolLot,
    string? PesoInicialCmReference,
    string? PesoInicialCmLot,
    int PesoInicialLeituras,
    decimal? PesoInicialPesoMedioGrams,
    int WaterTemperatureC,
    decimal? WaterDensityGramsPerCm3,
    decimal? GlassDensityGramsPerCm3,
    decimal? VolumeMarisaCm3,
    decimal? VolumePuncaoCm3,
    /// <summary>O aviso do html: a referência ou as máquinas possíveis do contra-molde diferem das do CM inicial — a comparação continua manualmente, sem bloqueio.</summary>
    bool FerramentaDiferente,
    IReadOnlyList<PesoComparacaoLeituraReadModel> Leituras,
    PesoAveragesReadModel? Medias,
    PesoComparacaoPares? Pares,
    /// <summary>As leituras deixadas de fora (massa ≤ 0 ou resultado ≤ 0) — o aviso diz quais; vazio quando todas entraram.</summary>
    IReadOnlyList<PesoComparacaoLeituraIgnorada>? Ignoradas = null);

/// <summary>
/// Uma opção de Peso guardado para os selects da comparação (o «Peso inicial
/// desta produção» e os «Primeiro/Segundo Peso» do histórico): os FATOS
/// HUMANOS apenas — nunca um id como facto da folha (o id viaja no POST/URL).
/// O CmToolId é a âncora do POST do contra-molde (o fallback do html: o CM
/// do peso inicial), nunca um facto exibido.
/// </summary>
public sealed record PesoComparacaoPesoOpcao(
    int PesoId,
    string? Reference,
    string? ProductionNumber,
    string? Machine,
    string? CmToolId,
    string? CmToolReference,
    string? CmToolLot,
    int Leituras,
    decimal? PesoMedioGrams,
    IReadOnlyList<string>? CmMachines = null);

/// <summary>
/// A vista da comparação entre DOIS Pesos guardados do histórico (o
/// «Comparar Pesos guardados» do dmo-html): os dois lados identificados por
/// factos humanos, a compatibilidade das ferramentas (a mesma referência e
/// máquinas possíveis em comum — sem compatibilidade é «Comparação manual,
/// sem bloqueio») e os pares por posição do CM.
/// </summary>
public sealed record PesoComparacaoGuardadosView(
    int PrimeiroPesoId,
    int SegundoPesoId,
    string PrimeiroRotulo,
    string SegundoRotulo,
    decimal? PrimeiroPesoMedioGrams,
    decimal? SegundoPesoMedioGrams,
    int PrimeiroLeituras,
    int SegundoLeituras,
    bool Compativel,
    PesoComparacaoPares Pares,
    IReadOnlyList<PesoComparacaoLado>? PrimeiroLinhas = null,
    IReadOnlyList<PesoComparacaoLado>? SegundoLinhas = null,
    PesoAveragesReadModel? PrimeiroMedias = null,
    PesoAveragesReadModel? SegundoMedias = null);

/// <summary>
/// O read model de um registo de comparação guardado (o detalhe do
/// histórico): os factos da produção e das ferramentas congelados, as
/// leituras com os derivados calculados a partir dos inputs congelados, as
/// médias e os pares contra o Peso inicial.
/// </summary>
public sealed record PesoComparacaoReadModel(
    int ComparacaoId,
    int ControloId,
    string Reference,
    string ProductionNumber,
    string Machine,
    int PesoInicialId,
    string? CmToolReference,
    string? CmToolLot,
    int WaterTemperatureC,
    decimal WaterDensityGramsPerCm3,
    decimal GlassDensityGramsPerCm3,
    decimal VolumeMarisaCm3,
    decimal VolumePuncaoCm3,
    IReadOnlyList<PesoComparacaoLeituraReadModel> Leituras,
    PesoAveragesReadModel? Medias,
    PesoComparacaoPares Pares,
    DateTime CreatedAtUtc,
    string? Note);

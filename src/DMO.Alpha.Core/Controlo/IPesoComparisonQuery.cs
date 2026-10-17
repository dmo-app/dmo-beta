namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Interface da Comparação_B (corte C, spec PESO.md «Comparação_B —
/// diferencial do Peso baseline»): os candidatos históricos da mesma
/// referência de produção (a app PROPÕE — o último da mesma MÁQUINA em
/// primeiro, a regra do Owner do desgaste por máquina — mas a SELEÇÃO é
/// sempre humana explícita) e o diferencial entre o Peso atual e um
/// histórico explicitamente selecionado (matching das leituras pela
/// POSIÇÃO física; contagens desiguais são válidas — as linhas sem
/// contraparte ficam de fora; sem contrapartes nenhuma, a comparação
/// avisa e nunca bloqueia o fluxo do Peso).
/// </summary>
public interface IPesoComparisonQuery
{
    /// <summary>
    /// Os Pesos baseline das produções anteriores COM A MESMA referência
    /// (a mesma base do conjunto) que a produção do controlo dado — exceto o
    /// próprio. O ranking é AJUDA (a mesma máquina primeiro, depois o mais
    /// recente); nunca uma regra de elegibilidade escondida.
    /// </summary>
    Task<IReadOnlyList<PesoComparisonCandidate>> FindCandidatesAsync(
        int controloId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O diferencial lado a lado: o Peso atual e o histórico selecionado,
    /// as médias e as diferenças (massa de água, capacidade, peso do vidro)
    /// e as leituras correspondentes pela posição física.
    /// </summary>
    Task<PesoComparisonView?> CompareAsync(
        int currentPesoId,
        int historicalPesoId,
        CancellationToken cancellationToken = default);

    /// <summary>O mesmo diferencial a partir do Peso atual já lido pela página.</summary>
    Task<PesoComparisonView?> CompareAsync(
        PesoReadModel current,
        int historicalPesoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// OS PESOS GUARDADOS de uma produção (o «Peso inicial desta produção»
    /// do dmo-html): os Pesos production-scoped do controlo da produção,
    /// do mais antigo para o mais recente (o html ordena por created_at —
    /// o primeiro é o peso inicial da produção), com os factos humanos para
    /// o select.
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarIniciaisAsync(
        int jobOnId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TODOS os Pesos guardados do histórico (os selects «Primeiro Peso» /
    /// «Segundo Peso» do histórico do dmo-html), do mais recente para o mais
    /// antigo, com os factos humanos para os selects.
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosGuardadosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os Pesos guardados de UMA referência (a procura em dois passos do
    /// «Comparar Pesos guardados», 16/10/2026): os da produção cuja
    /// referência contém o texto e os sem produção cuja CM o contém, do mais
    /// recente para o mais antigo, no máximo 100. Sem referência: lista vazia.
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosPorReferenciaAsync(
        string? referencia,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A procura ao escrever do «Comparar Pesos guardados» (Diogo,
    /// 17/10/2026: «queremos comparar pesos que foram usados numa produção,
    /// e o filtro mostra a produção, lote e máquinas onde trabalhou»; «a
    /// entrada dos filtros deve poder partir de qualquer informação
    /// relacionada»): os Pesos cuja produção, referência, máquina, lote ou
    /// referência da CM, ou uma máquina da CM, contêm o texto (só os da
    /// referência indicada, quando há uma), do mais recente
    /// para o mais antigo, no máximo <paramref name="limite"/>. Cada opção
    /// traz as máquinas da CM (as ligações da ferramenta).
    /// </summary>
    /// <summary>
    /// As opções (com as máquinas da CM) dos Pesos indicados: o texto dos
    /// campos da procura quando a página reabre com os Pesos escolhidos.
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> OpcoesDosPesosAsync(
        IReadOnlyCollection<int> pesoIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ProcurarPesosAsync(
        string? termo,
        string? referencia,
        int limite,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A referência que reabre os selects de um Peso: a da produção ou, sem
    /// produção, a da CM. Null quando o Peso não existe.
    /// </summary>
    Task<string?> ReferenciaDoPesoAsync(
        int pesoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os Pesos guardados que fazem sentido comparar com <paramref name="pesoId"/>
    /// (o «Peso anterior para comparar» do Resumo e do PDF do Peso, DMO-045):
    /// os da MESMA referência de produção ou da MESMA referência de CM, sem o
    /// próprio, do mais recente para o mais antigo. Antes eram todos os Pesos,
    /// e escolher outra referência dava uma diferença sem significado.
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosComparaveisAsync(
        int pesoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A COMPARAÇÃO DAS NOVAS LEITURAS dos contra-moldes contra o Peso
    /// inicial (a superfície própria «Comparação de Peso»): a base consumida
    /// (a densidade da água pela tabela, a densidade do vidro da CM ou — na
    /// sua falta — a congelada do Peso inicial; o volume_marisa do Peso
    /// inicial congelado; o volume_punção da CM ou, na falta, o congelado do
    /// Peso inicial), o cálculo de cada leitura pelo
    /// <see cref="PesoCalculation"/> e os pares pela
    /// <see cref="PesoComparacaoCalculation"/>. Zero escrita: nenhuma
    /// recusa nem resultado grava nada.
    /// </summary>
    Task<PesoComparacaoLeiturasResultado> CompararLeiturasAsync(
        PesoComparacaoLeiturasPedido pedido,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A comparação entre DOIS Pesos guardados (o «Comparar Pesos guardados»
    /// do histórico), pelo id dos dois. Null quando algum não existe (a
    /// recusa tipada vive na apresentação); os pares emparelham pelo
    /// número/posição do CM com o «Sem contraparte». O MESMO Peso dos dois
    /// lados é válido (beta.65): cada par mostra Δ = 0.
    /// </summary>
    Task<PesoComparacaoGuardadosView?> CompararGuardadosAsync(
        int primeiroPesoId,
        int segundoPesoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O registo de comparação guardado pelo id (o detalhe do histórico):
    /// os inputs congelados e os derivados calculados à leitura pela fonte
    /// única. Null quando o registo não existe.
    /// </summary>
    Task<PesoComparacaoReadModel?> ObterComparacaoGuardadaAsync(
        int comparacaoId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Um candidato da Comparação_B: um Peso baseline de uma produção anterior
/// da mesma referência, com os factos que AJUDAM a escolher (a máquina, a
/// recência, as médias) — a decisão é sempre do operador.
/// </summary>
public sealed record PesoComparisonCandidate(
    int PesoId,
    string ProductionNumber,
    string Reference,
    string Machine,
    bool SameMachine,
    int ReadingCount,
    decimal? AverageWaterWeightGrams,
    decimal? AverageCapacityCm3,
    decimal? AverageGlassWeightGrams,
    string? CmLot = null,
    IReadOnlyList<string>? CompatibleMachines = null);

/// <summary>
/// A vista do diferencial (o alvo 22): as médias atuais, as do histórico
/// selecionado e as diferenças calculadas pelo backend (peso do vidro,
/// volume/capacidade); as leituras correspondentes pela posição.
/// </summary>
public sealed record PesoComparisonView(
    int CurrentPesoId,
    int HistoricalPesoId,
    string HistoricalProductionNumber,
    string HistoricalMachine,
    PesoAveragesReadModel? CurrentAverages,
    PesoAveragesReadModel? HistoricalAverages,
    PesoComparisonDeltas Deltas,
    IReadOnlyList<PesoComparisonRow> Rows,
    DateOnly? HistoricalInicioEm = null,
    DateOnly? HistoricalFimEm = null,
    int? HistoricalProductionDays = null);

/// <summary>
/// As diferenças das médias (atual − histórico): massa de água (g),
/// capacidade (cm³) e peso do vidro (g), com a variação percentual do peso
/// do vidro face ao histórico (o −0,37% do alvo).
/// </summary>
public sealed record PesoComparisonDeltas(
    decimal? WaterWeightDifferenceGrams,
    decimal? CapacityDifferenceCm3,
    decimal? GlassWeightDifferenceGrams,
    decimal? GlassWeightVariationPercent);

/// <summary>
/// Uma correspondência de leitura pela POSIÇÃO física (a regra da spec:
/// «comparação feita pela posição da leitura na tabela do controlo
/// anterior — o número de CM é apenas indicativo»). Só posições presentes
/// nos DOIS Pesos aparecem aqui. O VALOR comparado é O PESO do vidro da
/// CM — a fórmula total (capacidade + marisa − punção) × densidade (o
/// Owner 2026-10-12: «na tabela massa actual deveria ser o peso actual,
/// peso é a formula total para cada CM... de momento estamos a comparar
/// massa de agua com volume que é praticamente o mesmo» — a massa e o
/// volume são quase o mesmo número, o que muda de verdade é o PESO).
/// </summary>
public sealed record PesoComparisonRow(
    int Position,
    string CurrentCmSubject,
    decimal CurrentGlassWeightGrams,
    string HistoricalCmSubject,
    decimal HistoricalGlassWeightGrams,
    decimal? CurrentCapacityCm3 = null,
    decimal? HistoricalCapacityCm3 = null,
    decimal? GlassWeightDifferenceGrams = null,
    decimal? CapacityDifferenceCm3 = null,
    decimal? GlassWeightVariationPercent = null);

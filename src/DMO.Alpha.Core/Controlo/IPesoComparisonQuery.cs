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
    /// próprio —, das máquinas permitidas pela ficha da CM. Anteriores com o
    /// critério da «Produção anterior»: as que começaram antes da atual (no
    /// mesmo dia, as registadas antes); sem início na atual, as registadas
    /// antes dela, com as sem início no fim. As mais recentes primeiro, no
    /// máximo <see cref="DMO.Alpha.Core.Production.IProductionPickerQuery.MaxResults"/>
    /// (as últimas 5, deep sweep 2, F-12). O <paramref name="compararPesoId"/>
    /// pedido fica na lista, no fim, mesmo mais antigo ou posterior, se for da
    /// mesma referência e de uma máquina permitida.
    /// </summary>
    Task<IReadOnlyList<PesoComparisonCandidate>> FindCandidatesAsync(
        int controloId,
        int? compararPesoId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os mesmos candidatos para o Peso aberto: as máquinas permitidas vêm da
    /// ficha da CM DESTE Peso (congelada nele), não da CM que a produção tem
    /// agora — um Peso de antes de uma troca da CM compara-se com a sua CM
    /// (18/10/2026). Vazia para um Peso sem produção. O <paramref name="compararPesoId"/>
    /// pedido fica na lista como no outro.
    /// </summary>
    Task<IReadOnlyList<PesoComparisonCandidate>> FindCandidatesAsync(
        PesoReadModel peso,
        int? compararPesoId = null,
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
    /// do mais antigo para o mais recente, com os factos humanos para o
    /// select. Com mais do que um (a CM ou a BQ mudou a meio da produção),
    /// cada opção diz a BQ e se é o Peso das ferramentas atuais, que é o que
    /// a página escolhe por omissão (senão o mais recente).
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarIniciaisAsync(
        int jobOnId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os Pesos guardados de UMA referência (a procura em dois passos do
    /// «Comparar Pesos guardados», 16/10/2026): os da produção cuja
    /// referência começa pelo texto e os sem produção cuja CM começa por ele,
    /// ou cuja parte BQ começa por ele (sem distinção de maiúsculas; nunca um
    /// pedaço do meio, a regra única de 18/10/2026), do mais recente para o
    /// mais antigo, no máximo 100. Sem referência: lista vazia.
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> ListarPesosPorReferenciaAsync(
        string? referencia,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// As opções (com as máquinas da CM) dos Pesos indicados: o texto dos
    /// campos da procura quando a página reabre com os Pesos escolhidos.
    /// </summary>
    Task<IReadOnlyList<PesoComparacaoPesoOpcao>> OpcoesDosPesosAsync(
        IReadOnlyCollection<int> pesoIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A procura ao escrever do «Comparar Pesos guardados» (Diogo,
    /// 17/10/2026: «queremos comparar pesos que foram usados numa produção,
    /// e o filtro mostra a produção, lote e máquinas onde trabalhou»; «a
    /// entrada dos filtros deve poder partir de qualquer informação
    /// relacionada»): os Pesos cuja referência da produção ou da CM começa
    /// pelo texto (ou a parte BQ dela; nunca um pedaço do meio, a regra única
    /// de 18/10/2026), ou cujo nº de produção, máquina, lote ou máquina da CM
    /// contém o texto (só os da referência indicada, pelo início, quando há
    /// uma), do mais recente para o mais antigo, no máximo
    /// <paramref name="limite"/>. Cada opção traz as máquinas da CM (as
    /// ligações da ferramenta).
    /// </summary>
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

using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// Surface nativo Peso (Controlo Create) para o corte tool-scoped: uma
/// pesagem real de uma ferramenta quando ainda não existe produção nem Job
/// On. O fluxo da página segue a spec: escolher a tool (resolução canónica
/// por tool_id exato), criar o peso_id, registar as leituras reais e ver o
/// cálculo principal — tudo calculado pelo backend — e o histórico de Pesos
/// da ferramenta, para que um Peso tomado antes da produção permaneça
/// diretamente consultável a partir dela. Nenhuma identidade de produção é
/// inventada; valores técnicos Tool-owned em falta são completados no fluxo
/// e gravados no tool_id owner. Mutação nativa exige sessão autenticada
/// ([Authorize] + Challenge nos POST); recusas tipadas voltam por query
/// (PRG) com vocabulário fechado dos enums, nunca texto livre.
///
/// CORTE C — o modo production-scoped (?controlo_id): o Peso baseline da
/// produção chega pelo Resumo do Controlo; as ferramentas (CM e BQ/marisa)
/// são HERDADAS do contexto do Job On — nunca escolhidas nesta folha — e a
/// Comparação_B (os Pesos da mesma referência, o último da mesma MÁQUINA
/// proposto em primeiro — a regra do Owner do desgaste por máquina) é
/// seleção humana explícita com o diferencial pela POSIÇÃO física das
/// leituras (spec PESO.md).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class PesoModel : PageModel
{
    private readonly IToolLookupQuery _toolLookup;
    private readonly IToolTechnicalQuery _toolTechnical;
    private readonly IGlassDensityConfiguration _glassDensityConfiguration;
    private readonly ICreateToolScopedPesoHandler _createPeso;
    private readonly IRecordPesoMeasurementHandler _recordMeasurement;
    private readonly IPesoQuery _pesoQuery;
    private readonly ShellStateFactory _shellState;
    private readonly ICreatePesoForControloHandler _createPesoForControlo;
    private readonly IPesoComparisonQuery _pesoComparison;
    private readonly IProductionContextQuery _productionContext;
    private readonly PesoPdfGenerator _pdfGenerator;
    private readonly IModuleSettingsQuery _moduleSettings;
    private readonly ControloApprovalHandler _approvalHandler;
    private readonly DmoDbContext _db;
    private readonly IToolCandidateQuery _toolCandidates;
    private readonly IAssociatePesoToControloHandler _associatePeso;
    private readonly IPesoAssociationQuery _pesoAssociation;
    private readonly IEnsureControloForJobOnHandler _ensureControlo;
    private readonly IJobOnListQuery _jobOnList;

    public PesoModel(
        IToolLookupQuery toolLookup,
        IToolTechnicalQuery toolTechnical,
        IGlassDensityConfiguration glassDensityConfiguration,
        ICreateToolScopedPesoHandler createPeso,
        IRecordPesoMeasurementHandler recordMeasurement,
        IPesoQuery pesoQuery,
        ShellStateFactory shellState,
        ICreatePesoForControloHandler createPesoForControlo,
        IPesoComparisonQuery pesoComparison,
        IProductionContextQuery productionContext,
        PesoPdfGenerator pdfGenerator,
        IModuleSettingsQuery moduleSettings,
        ControloApprovalHandler approvalHandler,
        DmoDbContext db,
        IToolCandidateQuery toolCandidates,
        IAssociatePesoToControloHandler associatePeso,
        IPesoAssociationQuery pesoAssociation,
        IEnsureControloForJobOnHandler ensureControlo,
        IJobOnListQuery jobOnList)
    {
        _jobOnList = jobOnList;
        _associatePeso = associatePeso;
        _pesoAssociation = pesoAssociation;
        _ensureControlo = ensureControlo;
        _toolLookup = toolLookup;
        _toolTechnical = toolTechnical;
        _glassDensityConfiguration = glassDensityConfiguration;
        _createPeso = createPeso;
        _recordMeasurement = recordMeasurement;
        _pesoQuery = pesoQuery;
        _shellState = shellState;
        _createPesoForControlo = createPesoForControlo;
        _pesoComparison = pesoComparison;
        _productionContext = productionContext;
        _pdfGenerator = pdfGenerator;
        _moduleSettings = moduleSettings;
        _approvalHandler = approvalHandler;
        _db = db;
        _toolCandidates = toolCandidates;
    }

    /// <summary>
    /// A query do apoio TP/calote, instanciada sobre o DbContext scoped. A
    /// classe é stateless e só de leitura; o seu registo DI cabe ao corte
    /// que consumir a ficha do Job On (a mesma fonte do cálculo).
    /// </summary>
    private PesoCapSupportQuery CapSupportQuery => new(_db);

    /// <summary>As ferramentas herdadas da produção numa só consulta.</summary>
    private PesoToolFactsQuery ToolFactsQuery => new(_db);

    /// <summary>As alterações do Job On, para o aviso no Peso aberto.</summary>
    private JobOnChangeQuery ChangeQuery => new(_db);

    /// <summary>O aviso quando o Job On mudou depois de o Peso ser gravado; null sem alterações.</summary>
    public JobOnChangeNoticeView? JobOnNotice { get; private set; }

    private async Task LoadJobOnNoticeAsync()
    {
        if (Peso is not null)
        {
            JobOnNotice = JobOnChangeNoticeView.From(
                "O Job On mudou depois de este Peso ser gravado.",
                await ChangeQuery.ForPesoAsync(Peso.PesoId, HttpContext.RequestAborted));
        }
    }

    /// <summary>A query das comparações do Peso (a Comparação_B + as comparações todas — o mesmo serviço, sem registo DI próprio).</summary>
    private PesoComparisonQuery ComparacaoQuery => new(_db, _pesoQuery);

    /// <summary>A produção anterior do Peso (o Owner 2026-10-17): Job On e peso do SAP.</summary>
    private PesoProducaoAnteriorQuery ProducaoAnteriorQuery => new(_db);

    /// <summary>O handler de criação do registo de comparação (o cálculo vem da fonte única acima).</summary>
    private CreatePesoComparacaoHandler ComparacaoHandler => new(_db, ComparacaoQuery);

    /// <summary>A procura unificada do histórico do Controlo (os três tipos).</summary>
    private ControloHistorySearchQuery HistoricoControloQuery => new(_db);

    /// <summary>As produções de uma referência (a procura em dois passos, 16/10/2026).</summary>
    private DMO.Alpha.Infrastructure.Production.ProductionPickerQuery ProductionPicker => new(_db);

    /// <summary>Pesos sem produção da CM do Job On (modo controlo_id, sem Peso ainda).</summary>
    public IReadOnlyList<UnassociatedPesoCandidate> AssociationCandidates { get; private set; } = Array.Empty<UnassociatedPesoCandidate>();

    /// <summary>Produções cuja CM usa o tool_id do Peso aberto sem produção.</summary>
    public IReadOnlyList<PesoProductionCandidate> ProductionCandidates { get; private set; } = Array.Empty<PesoProductionCandidate>();

    /// <summary>True depois de uma associação bem-sucedida (PRG).</summary>
    public bool PesoAssociated { get; private set; }

    /// <summary>Recusa tipada da associação posterior (PRG).</summary>
    public AssociatePesoToControloResult? AssociationRefusal { get; private set; }

    /// <summary>Mensagem da recusa da associação, apenas apresentação.</summary>
    public string AssociationRefusalMessage => AssociationRefusal switch
    {
        AssociatePesoToControloResult.ControloNotFound => "A produção indicada não existe. Nenhum Peso foi associado.",
        AssociatePesoToControloResult.PesoNotFound => "O Peso indicado não existe. Nada foi associado.",
        AssociatePesoToControloResult.ContextMissingCm => "O Job On desta produção não tem CM. Nenhum Peso foi associado.",
        AssociatePesoToControloResult.CmMismatch => "A CM deste Peso não é a CM do Job On. Nenhum Peso foi associado.",
        AssociatePesoToControloResult.PesoAlreadyAssociated => "Este Peso já pertence a outra produção. Nada foi alterado.",
        AssociatePesoToControloResult.ControloAlreadyHasPeso => "Esta produção já tem o seu Peso. Nada foi alterado.",
        _ => string.Empty
    };

    /// <summary>Factos descritivos da CM escolhida (null enquanto não resolvida).</summary>
    public ToolLookupResult? CmTool { get; private set; }

    /// <summary>Contexto técnico atual da CM escolhida (null quando ainda não existe).</summary>
    public ToolTechnicalReadModel? CmTechnical { get; private set; }

    /// <summary>Factos descritivos da BQ escolhida (null enquanto não resolvida).</summary>
    public ToolLookupResult? BqTool { get; private set; }

    /// <summary>Contexto técnico atual da BQ escolhida (null quando ainda não existe).</summary>
    public ToolTechnicalReadModel? BqTechnical { get; private set; }

    /// <summary>Factos descritivos do MF herdado do contexto (a referência, não o uuid).</summary>
    public ToolLookupResult? MfTool { get; private set; }

    /// <summary>
    /// Densidade do vidro configurada para o processo atual da CM; null
    /// quando o processo ainda não está registado ou não tem densidade
    /// configurada.
    /// </summary>
    public decimal? ConfiguredGlassDensity { get; private set; }

    /// <summary>Read model do Peso aberto (?peso_id); null fora desse contexto.</summary>
    public PesoReadModel? Peso { get; private set; }

    /// <summary>Valor submetido do tool_id da CM (contexto de escolha de ferramentas).</summary>
    public string? RequestedCmToolId { get; private set; }

    /// <summary>Valor submetido do tool_id da BQ (contexto de escolha de ferramentas).</summary>
    public string? RequestedBqToolId { get; private set; }

    /// <summary>True quando um tool_id da CM foi submetido e não resolve no registo.</summary>
    public bool CmToolRequestedNotFound { get; private set; }

    /// <summary>True quando um tool_id da BQ foi submetido e não resolve no registo.</summary>
    public bool BqToolRequestedNotFound { get; private set; }

    /// <summary>Recusa tipada do último POST de criação, transportada por query (PRG).</summary>
    public CreateToolScopedPesoResult? CreateRefusal { get; private set; }

    /// <summary>Recusa tipada do último POST de leitura, transportada por query (PRG).</summary>
    public RecordPesoMeasurementResult? MeasurementRefusal { get; private set; }

    /// <summary>Mensagem da recusa de criação, apenas apresentação.</summary>
    public string CreateRefusalMessage => CreateRefusal switch
    {
        CreateToolScopedPesoResult.CmToolNotFound =>
            "A ferramenta CM indicada não está registada no Ferramentas. Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.BqToolNotFound =>
            "A boquilha (BQ) indicada não está registada no Ferramentas. Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.MissingCmProcess =>
            "O processo de fabrico da CM não está registado nem foi indicado. Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.UnknownProcessToken =>
            "Processo desconhecido. Use NNPB ou PS. Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.MissingCmVolumePuncao =>
            "O volume punção da CM não está registado nem foi indicado. Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.MissingBqVolumeMarisa =>
            "O volume marisa da BQ não está registado nem foi indicado. Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.GlassDensityNotConfigured =>
            "A densidade do vidro do processo não está configurada no Controlo. " +
            "Nenhum Peso foi criado e nenhum valor foi inventado.",
        CreateToolScopedPesoResult.InvalidWaterTemperature =>
            "Temperatura da água fora do intervalo suportado (5–35 °C). Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.InvalidNominalWeight =>
            "O peso nominal preenchido é ilegível (ex.: 200,00 ou 200.00). Reveja o valor ou deixe o campo opcional vazio.",
        CreateToolScopedPesoResult.InvalidTechnicalValue =>
            "Um valor técnico fornecido é fisicamente inválido: os volumes (cm³) e o peso nominal (g) têm de ser positivos. Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.UnreadableWaterTemperature =>
            UnreadableTemperatureMessage + " Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.UnreadableCmVolumePuncao =>
            UnreadablePuncaoMessage + " Nenhum Peso foi criado.",
        CreateToolScopedPesoResult.UnreadableBqVolumeMarisa =>
            UnreadableMarisaMessage + " Nenhum Peso foi criado.",
        _ => string.Empty
    };

    // As recusas de um valor ESCRITO mas ilegível dizem «ilegível» (antes
    // diziam «não está registado» ou «fora do intervalo», o que não era a
    // causa) e dão os dois exemplos, com vírgula e com ponto.
    private const string UnreadableTemperatureMessage =
        "A temperatura da água está vazia ou ilegível — escreva um número, por exemplo 20,4 ou 20.4.";

    private const string UnreadablePuncaoMessage =
        "O volume punção da CM escrito é ilegível — escreva um número, por exemplo 70,10 ou 70.10.";

    private const string UnreadableMarisaMessage =
        "O volume marisa da BQ escrito é ilegível — escreva um número, por exemplo 10,50 ou 10.50.";

    /// <summary>Mensagem da recusa de leitura, apenas apresentação.</summary>
    public string MeasurementRefusalMessage => MeasurementRefusal switch
    {
        RecordPesoMeasurementResult.PesoNotFound =>
            "O Peso indicado não existe. Nenhuma leitura foi registada.",
        _ => string.Empty
    };

    // ---------------------------------------------------------------
    // CORTE C — o modo production-scoped (?controlo_id).
    // ---------------------------------------------------------------

    /// <summary>Recusa tipada da criação/abertura do Peso da produção (PRG por query).</summary>
    public CreatePesoForControloResult? ProductionRefusal { get; private set; }

    /// <summary>Mensagem da recusa production-scoped, apenas apresentação.</summary>
    public string ProductionRefusalMessage => ProductionRefusal switch
    {
        CreatePesoForControloResult.ControloNotFound =>
            "O controlo indicado não existe. Nenhum Peso foi aberto.",
        CreatePesoForControloResult.ContextMissingCm =>
            "Esta produção não tem ferramenta CM no contexto do Job On. O Peso precisa da CM a pesar.",
        CreatePesoForControloResult.ContextMissingBq =>
            "Esta produção não tem boquilha (marisa) no contexto do Job On. O cálculo consume o volume marisa da BQ.",
        CreatePesoForControloResult.CmToolNotFound =>
            "A ferramenta CM desta produção não está registada no Ferramentas.",
        CreatePesoForControloResult.BqToolNotFound =>
            "A boquilha (marisa) desta produção não está registada no Ferramentas.",
        CreatePesoForControloResult.MissingCmProcess =>
            "O processo de fabrico da CM não está registado nem foi indicado.",
        CreatePesoForControloResult.UnknownProcessToken =>
            "Processo desconhecido. Use NNPB ou PS.",
        CreatePesoForControloResult.MissingCmVolumePuncao =>
            "O volume punção da CM não está registado nem foi indicado.",
        CreatePesoForControloResult.MissingBqVolumeMarisa =>
            "O volume marisa da BQ não está registado nem foi indicado.",
        CreatePesoForControloResult.GlassDensityNotConfigured =>
            "A densidade do vidro do processo não está configurada no Controlo. Nenhum valor foi inventado.",
        CreatePesoForControloResult.InvalidWaterTemperature =>
            "Temperatura da água fora do intervalo suportado (5–35 °C).",
        CreatePesoForControloResult.InvalidNominalWeight =>
            "O peso nominal preenchido é ilegível (ex.: 200,00 ou 200.00). Reveja o valor ou deixe o campo opcional vazio.",
        CreatePesoForControloResult.InvalidTechnicalValue =>
            "Um valor técnico fornecido é fisicamente inválido: os volumes (cm³) e o peso nominal (g) têm de ser positivos.",
        CreatePesoForControloResult.UnreadableWaterTemperature => UnreadableTemperatureMessage,
        CreatePesoForControloResult.UnreadableCmVolumePuncao => UnreadablePuncaoMessage,
        CreatePesoForControloResult.UnreadableBqVolumeMarisa => UnreadableMarisaMessage,
        _ => string.Empty
    };

    /// <summary>O contexto da produção (fatos humanos: nº, referência, máquina, as tools herdadas).</summary>
    public ProductionContextReadModel? ProductionContext { get; private set; }

    /// <summary>Produção do Peso aberto pelo histórico, sem trocar o peso_id selecionado.</summary>
    public ProductionContextReadModel? OpenedPesoProduction { get; private set; }

    /// <summary>O controlo_id do modo production-scoped (para os links da Comparação_B).</summary>
    public int? ControloIdFromQuery { get; private set; }

    /// <summary>Os candidatos da Comparação_B (a mesma referência, a produção mais recente primeiro; a escolha é tua).</summary>
    public IReadOnlyList<PesoComparisonCandidate> ComparisonCandidates { get; private set; } = Array.Empty<PesoComparisonCandidate>();

    /// <summary>O diferencial da Comparação_B (o histórico selecionado por ti).</summary>
    public PesoComparisonView? Comparison { get; private set; }

    /// <summary>A produção do Peso aberto (pela folha da produção ou pelo histórico) — o lado atual da comparação.</summary>
    public ProductionContextReadModel? ProducaoDaComparacao => ProductionContext ?? OpenedPesoProduction;

    /// <summary>A calote do tampão que o Peso aberto tem (a do Job On ou a h_ref da CM) — o ponto de partida do «supomos».</summary>
    public PesoCapSupportReadModel? CaloteDaComparacao { get; private set; }

    /// <summary>O texto submetido no «supomos» (ecoado no campo).</summary>
    public string? SuporCaloteTexto { get; private set; }

    /// <summary>O resultado do «supomos» da calote (só exibição; nunca gravado).</summary>
    public PesoSuposicaoCalote? Suposicao { get; private set; }

    /// <summary>True quando o valor do «supomos» foi submetido mas não é utilizável.</summary>
    public bool SuposicaoIlegivel { get; private set; }

    /// <summary>O caminho do PDF gravado (transportado por query, apenas apresentação).</summary>
    public string? PdfSavedPath { get; private set; }

    /// <summary>Recusa tipada da geração do PDF, apenas apresentação.</summary>
    public string? PdfRefusal { get; private set; }

    /// <summary>A TAB do Histórico (?historico=1) — a procura do TOTAL; a folha da produção fica só com o fundo (a referência).</summary>
    public bool HistoricoTab { get; private set; }

    /// <summary>Os filtros da procura do histórico (persistem no form após submeter).</summary>
    public string? HistoryReference { get; private set; }

    public string? HistoryProduction { get; private set; }

    public string PdfRefusalMessage => PdfRefusal switch
    {
        "sem-peso" => "Esta produção ainda não tem pesagem — registe as leituras antes de gerar o PDF.",
        _ => string.Empty
    };

    // ---------------------------------------------------------------
    // A AUDITORIA DO CODEX (achado 2): as recusas do Peso REAPRESENTAM
    // OS DADOS QUE O OPERADOR ESCREVEU (o padrão da casa: a causa em PT
    // + o estado preservado para a correção) — o operador preenche tudo,
    // a recusa vem, e o form volta COM os valores, nunca vazio.
    // ---------------------------------------------------------------

    /// <summary>O echo da temperatura submetida na recusa de criação (o form volta com o que foi escrito).</summary>
    public string? EchoTemperature { get; private set; }

    /// <summary>O echo do processo submetido na recusa de criação.</summary>
    public string? EchoCmProcess { get; private set; }

    /// <summary>O echo do volume punção submetido na recusa de criação.</summary>
    public string? EchoVolumePuncao { get; private set; }

    /// <summary>O echo do peso nominal submetido na recusa de criação.</summary>
    public string? EchoNominalWeight { get; private set; }

    /// <summary>O echo do volume marisa submetido na recusa de criação.</summary>
    public string? EchoVolumeMarisa { get; private set; }

    /// <summary>
    /// A5 (beta.65): o peso nominal escrito era ilegível ou não era maior
    /// que 0. O nominal é opcional, por isso o Peso foi criado SEM ele e
    /// este aviso explica o que ficou de fora (antes a criação era recusada).
    /// </summary>
    public bool NominalIlegivelAviso { get; private set; }

    /// <summary>True quando o nominal era legível mas ≤ 0 (o aviso diz isso em vez de «ilegível»).</summary>
    public bool NominalNaoPositivo { get; private set; }

    /// <summary>O texto do nominal deixado de fora (o que foi escrito), para o aviso.</summary>
    public string? NominalIlegivelValor { get; private set; }

    /// <summary>Mensagem do aviso do nominal deixado de fora, apenas apresentação.</summary>
    public string NominalIlegivelMessage =>
        $"O peso nominal escrito{(NominalIlegivelValor is { } valor ? $" («{valor}»)" : string.Empty)} " +
        (NominalNaoPositivo ? "não é maior que 0" : "é ilegível") + ": o Peso foi criado sem ele. " +
        "Pode indicá-lo depois na ficha da ferramenta CM, por exemplo 200,00 ou 200.00.";

    /// <summary>A recusa tipada da leitura (a massa de água não utilizável — o typo não desaparece em silêncio).</summary>
    public bool LeituraRefusal { get; private set; }

    /// <summary>True quando a massa foi escrita mas é ilegível (a recusa diz «ilegível», não «≤ 0»).</summary>
    public bool LeituraIlegivel { get; private set; }

    /// <summary>Mensagem da recusa da leitura, apenas apresentação.</summary>
    public string LeituraRefusalMessage => LeituraIlegivel
        ? "A massa de água escrita é ilegível — escreva um número, por exemplo 199,64 ou 199.64. Nenhuma leitura foi registada; o form preserva o que escreveu."
        : "A massa de água tem de ser maior que 0 (por exemplo 199,64 ou 199.64). Nenhuma leitura foi registada; o form preserva o que escreveu.";

    /// <summary>O echo do nº de CM submetido na recusa da leitura.</summary>
    public string? EchoLeituraSubject { get; private set; }

    /// <summary>O echo da massa de água submetida na recusa da leitura.</summary>
    public string? EchoLeituraWater { get; private set; }

    // ---------------------------------------------------------------
    // O APOIO TP/CALOTE (o «TP / calote · Apoio separado» do dmo-html):
    // cálculo SEPARADO do Peso (PesoCalculation.cs:6-7), nunca um gate —
    // o painel de factos mostra-o sempre e o Peso funciona 100% intacto
    // sem calote nem TP.
    // ---------------------------------------------------------------

    /// <summary>
    /// O apoio TP/calote do contexto atual (a calote efetiva do Job On quando
    /// existe, senão a h_ref da Tool), calculado PELO BACKEND pela query
    /// canónica; null quando não há CM a que se aplique.
    /// </summary>
    public PesoCapSupportReadModel? CapSupport { get; private set; }

    /// <summary>A calote simulada submetida (o campo do apoio; nunca é gravada).</summary>
    public decimal? CapSimulada { get; private set; }

    /// <summary>
    /// True quando um valor de calote simulada foi submetido mas não é
    /// utilizável (texto ilegível ou negativo): aviso honesto, o apoio segue
    /// com o h efetivo — nunca bloqueia.
    /// </summary>
    public bool CapSimuladaIlegivel { get; private set; }

    /// <summary>
    /// O apoio abre-se expandido quando há uma simulação ativa (o reload do
    /// GET do form de simulação mantém o painel aberto).
    /// </summary>
    public bool CapAberto => CapSimulada is not null || CapSimuladaIlegivel;

    // ---------------------------------------------------------------
    // A COMPARAÇÃO DE PESO (a superfície própria do dmo-html — o tab
    // ?comparacao=1 do Peso): as novas leituras dos contra-moldes contra o
    // Peso inicial da produção, os pares por posição do CM, o Δ peso médio
    // dos pares e o «Sem contraparte» — calculado PELO BACKEND (a fonte
    // única: PesoCalculation + PesoComparacaoCalculation). O registo
    // guardado (Guardar comparação) vive no histórico tipo Comparação.
    // ---------------------------------------------------------------

    /// <summary>True no tab da comparação (?comparacao=1) — a folha do Peso fica de fora (o padrão do tab do histórico).</summary>
    public bool ComparacaoTab { get; private set; }

    /// <summary>
    /// O 1.º passo da comparação: a referência escrita. Só com ela se lê o
    /// select «Produção / Job On» (16/10/2026: nunca todas as produções).
    /// </summary>
    public string? ReferenciaComparacao { get; private set; }

    /// <summary>A referência escrita não tem produções: aviso âmbar, sem bloqueio.</summary>
    public bool ReferenciaComparacaoSemProducoes { get; private set; }

    /// <summary>As produções da referência escrita (o select «Produção / Job On»).</summary>
    public IReadOnlyList<ProductionPickItem> Producoes { get; private set; } = Array.Empty<ProductionPickItem>();
    public IReadOnlyList<ProductionContextReadModel> ProducoesJobOn { get; private set; } = Array.Empty<ProductionContextReadModel>();

    /// <summary>O jobon_id da produção selecionada (valor do select).</summary>
    public int? ProducaoSelecionadaJobOnId { get; private set; }

    /// <summary>Os Pesos guardados da produção selecionada (o select «Peso inicial»).</summary>
    public IReadOnlyList<PesoComparacaoPesoOpcao> Iniciais { get; private set; } = Array.Empty<PesoComparacaoPesoOpcao>();

    /// <summary>A base da comparação: o Peso inicial escolhido, CM a CM (peso e volume).</summary>
    public PesoReadModel? BaseDaComparacao { get; private set; }

    /// <summary>Quanto cada CM da base puxa a média.</summary>
    public IReadOnlyList<PesoComparacaoInfluencia> InfluenciaDaBase { get; private set; } = Array.Empty<PesoComparacaoInfluencia>();

    /// <summary>Quanto cada CM pesada agora mexe na média da base (a comparação ao vivo ou a guardada).</summary>
    public IReadOnlyList<PesoComparacaoInfluencia> InfluenciaDasNovas { get; private set; } = Array.Empty<PesoComparacaoInfluencia>();

    /// <summary>O peso inicial selecionado (valor do select).</summary>
    public int? PesoInicialSelecionado { get; private set; }

    /// <summary>Os candidatos CM (o select «Contra-molde a pesar» — a mesma referência em primeiro, o padrão do html).</summary>
    public IReadOnlyList<ToolCandidate> ContraMoldes { get; private set; } = Array.Empty<ToolCandidate>();

    /// <summary>O contra-molde selecionado (valor do select — o tool_id canónico no POST, nunca um facto da folha).</summary>
    public string? ContraMoldeSelecionado { get; private set; }

    /// <summary>As linhas submetidas (o re-render do form após um POST/recusa — o N.º CM e a massa como texto).</summary>
    public IReadOnlyList<(string? Subject, string? Water)> ComparacaoLinhas { get; private set; } = Array.Empty<(string?, string?)>();

    /// <summary>A temperatura submetida (o echo do form).</summary>
    public string? TemperaturaSubmetida { get; private set; }

    /// <summary>A nota submetida (o echo do form).</summary>
    public string? NotaSubmetida { get; private set; }

    /// <summary>O resultado da comparação ao vivo (o POST «Comparar pesos» — sem escrita).</summary>
    public PesoComparacaoLeiturasResultado? ResultadoComparacao { get; private set; }

    /// <summary>O registo de comparação guardado (?comparacao_id — o resultado a partir do PERSISTIDO).</summary>
    public PesoComparacaoReadModel? ComparacaoGuardada { get; private set; }

    /// <summary>A recusa tipada da comparação (transportada por query no PRG do Guardar).</summary>
    public PesoComparacaoResultado? ComparacaoRefusal { get; private set; }

    /// <summary>Mensagem da recusa da comparação, apenas apresentação.</summary>
    public string ComparacaoRefusalMessage => ComparacaoRefusal switch
    {
        PesoComparacaoResultado.PesoInicialNaoSelecionado =>
            "Selecione o Peso inicial desta produção. Nenhuma comparação foi feita.",
        PesoComparacaoResultado.PesoInicialNaoEncontrado =>
            "O Peso inicial indicado não existe. Nenhuma comparação foi feita.",
        PesoComparacaoResultado.ContraMoldePorEscolher =>
            "Selecione o contra-molde para medir. Nenhuma comparação foi feita.",
        PesoComparacaoResultado.TemperaturaInvalida =>
            "Temperatura da água fora do intervalo suportado (5–35 °C). Nenhuma comparação foi feita.",
        PesoComparacaoResultado.MassaAguaIlegivel =>
            "Nenhuma leitura tem massa de água legível — escreva um número, por exemplo 199,64 ou 199.64. Nenhuma comparação foi feita.",
        PesoComparacaoResultado.MassaAguaNaoPositiva =>
            "Nenhuma leitura tem massa de água maior que 0. Nenhuma comparação foi feita.",
        PesoComparacaoResultado.SemLeituras =>
            "Registe pelo menos uma leitura de contra-molde. Nenhuma comparação foi feita.",
        PesoComparacaoResultado.VolumePuncaoIndisponivel =>
            "O volume punção não está registado nem na ferramenta nem no Peso inicial. Nenhuma comparação foi feita.",
        PesoComparacaoResultado.ResultadoNaoPositivo =>
            "Nenhuma leitura deu um peso positivo — verifique as leituras e o volume punção. Nada foi gravado.",
        PesoComparacaoResultado.TemperaturaIlegivel =>
            UnreadableTemperatureMessage + " Nenhuma comparação foi feita.",
        _ => string.Empty
    };

    /// <summary>
    /// A3/A4 (beta.65): as leituras que ficaram DE FORA da comparação (massa
    /// ilegível, nº de CM sem massa, massa ≤ 0 ou peso calculado ≤ 0). As
    /// outras comparam-se (e gravam-se); o aviso diz quais ficaram de fora.
    /// </summary>
    public IReadOnlyList<ComparacaoLeituraForaAviso> ComparacaoLeiturasFora { get; private set; } = Array.Empty<ComparacaoLeituraForaAviso>();

    /// <summary>True quando o aviso vem de uma comparação GUARDADA (as leituras de fora não foram gravadas).</summary>
    public bool ComparacaoLeiturasForaGuardada { get; private set; }

    /// <summary>Uma leitura deixada de fora da comparação: a linha do form, o nº de CM, o valor escrito e o motivo.</summary>
    public sealed record ComparacaoLeituraForaAviso(
        int Linha,
        string? CmSubject,
        string? Valor,
        PesoComparacaoMotivoIgnorada Motivo)
    {
        public string Message
        {
            get
            {
                var quem = string.IsNullOrWhiteSpace(CmSubject)
                    ? $"Leitura {Linha} (sem nº de CM)"
                    : $"Leitura {Linha} (CM {CmSubject})";
                return Motivo switch
                {
                    PesoComparacaoMotivoIgnorada.MassaIlegivel =>
                        $"{quem}: a massa de água «{Valor}» é ilegível (escreva por exemplo 199,64 ou 199.64).",
                    PesoComparacaoMotivoIgnorada.SemMassa =>
                        $"{quem}: falta a massa de água.",
                    PesoComparacaoMotivoIgnorada.MassaNaoPositiva =>
                        $"{quem}: a massa de água {Valor} g não é maior que 0.",
                    _ =>
                        $"{quem}: o peso calculado com a massa {Valor} g não é positivo."
                };
            }
        }
    }

    // ---------------------------------------------------------------
    // O HISTÓRICO DO CONTROLO (a secção history do dmo-html): a procura
    // pelos três tipos (Peso, Pegamentos, Comparação) com os filtros da
    // oficina, o «Comparar Pesos guardados» e o detalhe do registo de
    // comparação guardado.
    // ---------------------------------------------------------------

    /// <summary>O filtro da ferramenta (a referência da CM — parcial conta).</summary>
    public string? HistoryTool { get; private set; }

    /// <summary>O filtro do tipo (Peso/Pegamentos/Comparação; null = todos).</summary>
    public ControloHistoryKind? HistoryTipo { get; private set; }

    /// <summary>O filtro «Sem Job On associado»: só os Pesos e Pegamentos ainda por associar.</summary>
    public bool HistorySemJobOn { get; private set; }

    /// <summary>As produções da referência procurada (o dropdown do segundo passo).</summary>
    public IReadOnlyList<string> HistoryProducoes { get; private set; } = Array.Empty<string>();

    /// <summary>O filtro da data (as comparações — as únicas com data registada).</summary>
    public DateOnly? HistoryData { get; private set; }

    /// <summary>True quando há pelo menos um filtro do histórico; sem filtros a lista não é lida.</summary>
    public bool HistoricoComProcura =>
        HistorySemJobOn
        || HistoryReference is not null
        || HistoryProduction is not null
        || HistoryTool is not null
        || HistoryTipo is not null
        || HistoryData is not null;

    /// <summary>Os resultados da procura unificada do histórico (null sem procura).</summary>
    public IReadOnlyList<ControloHistorySearchItem>? HistoryResults { get; private set; }

    /// <summary>A paginação do histórico (25 por página, feita na query).</summary>
    public ListPager? HistoryPager { get; private set; }

    /// <summary>
    /// O 1.º passo do «Comparar Pesos guardados»: a referência (da produção
    /// ou da CM). Só com ela se leem os Pesos dos selects.
    /// </summary>
    public string? PesoReferencia { get; private set; }

    /// <summary>A referência escrita não tem Pesos guardados: aviso âmbar, sem bloqueio.</summary>
    public bool PesoReferenciaSemPesos { get; private set; }

    /// <summary>Os Pesos guardados da referência, para os selects do «Comparar Pesos guardados».</summary>
    public IReadOnlyList<PesoComparacaoPesoOpcao> PesoOpcoes { get; private set; } = Array.Empty<PesoComparacaoPesoOpcao>();

    /// <summary>Quantos Pesos a procura ao escrever do «Comparar Pesos guardados» mostra.</summary>
    public const int PesoProcuraLimite = 10;

    /// <summary>
    /// O rótulo de um Peso na procura (Diogo, 17/10/2026: «o filtro mostra a
    /// produção, lote e máquinas onde trabalhou»): a produção (ou «Sem Job
    /// On»), a referência, o lote da CM e as máquinas da CM.
    /// </summary>
    public static string RotuloProcura(PesoComparacaoPesoOpcao opcao)
    {
        var partes = new List<string> { opcao.ProductionNumber ?? "Sem Job On" };
        var referencia = opcao.Reference ?? opcao.CmToolReference;
        if (!string.IsNullOrEmpty(referencia)) partes.Add(referencia);
        if (!string.IsNullOrEmpty(opcao.CmToolLot)) partes.Add($"Lote {opcao.CmToolLot}");
        if (opcao.CmMachines is { Count: > 0 } maquinas) partes.Add(string.Join(" ", maquinas));
        return string.Join(" · ", partes);
    }

    /// <summary>Os Pesos já escolhidos nos campos da procura (para o texto ao reabrir a página).</summary>
    public IReadOnlyList<PesoComparacaoPesoOpcao> PesosEscolhidos { get; private set; } = Array.Empty<PesoComparacaoPesoOpcao>();

    /// <summary>O rótulo do Peso já escolhido (o texto do campo ao reabrir a página).</summary>
    public string? RotuloEscolhido(int? pesoId) =>
        pesoId is int id && PesosEscolhidos.FirstOrDefault(o => o.PesoId == id) is { } opcao
            ? RotuloProcura(opcao)
            : null;

    /// <summary>
    /// A procura ao escrever do «Comparar Pesos guardados»
    /// (?handler=ProcurarPesos&amp;q=&amp;peso_ref=): os Pesos usados numa
    /// produção cujo texto contém o escrito, só da referência quando ela está
    /// preenchida. Devolve só a lista; um clique escolhe.
    /// </summary>
    public async Task<IActionResult> OnGetProcurarPesosAsync(
        string? q,
        [FromQuery(Name = "peso_ref")] string? pesoRef = null)
    {
        var opcoes = await ComparacaoQuery.ProcurarPesosAsync(
            q, Sanitize(pesoRef), PesoProcuraLimite, HttpContext.RequestAborted);
        return Partial("_PesoProcura", opcoes);
    }

    /// <summary>O primeiro Peso selecionado (o echo do select).</summary>
    public int? Peso1 { get; private set; }

    /// <summary>O segundo Peso selecionado (o echo do select).</summary>
    public int? Peso2 { get; private set; }

    /// <summary>A recusa tipada do comparar guardados (escolher | diferentes).</summary>
    public string? CompararRefusal { get; private set; }

    /// <summary>Mensagem da recusa do comparar guardados, apenas apresentação.</summary>
    public string CompararRefusalMessage => "Escolha dois Pesos guardados no Histórico.";

    /// <summary>
    /// A18 (beta.65): o mesmo Peso escolhido nos dois selects já não é
    /// recusa — a comparação mostra Δ = 0 e este aviso explica porquê.
    /// </summary>
    public bool CompararMesmoPeso { get; private set; }

    /// <summary>A comparação de dois Pesos guardados (o resultado do backend).</summary>
    public PesoComparacaoGuardadosView? PesoComparacaoGuardados { get; private set; }

    /// <summary>O registo de comparação guardado aberto pelo histórico (?comparacao_id — o detalhe inline).</summary>
    public PesoComparacaoReadModel? HistoricoComparacao { get; private set; }

    /// <summary>O valor de ?modo que abre a escolha do Job On no Novo Peso.</summary>
    public const string ModoJobOnValor = "jobon";

    /// <summary>True no Novo Peso associado a um Job On (?modo=jobon): a folha mostra a escolha da produção em vez das ferramentas.</summary>
    public bool ModoJobOn { get; private set; }

    /// <summary>A referência procurada na escolha do Job On (primeiro passo).</summary>
    public string? JobOnReferencia { get; private set; }

    /// <summary>A máquina procurada na escolha do Job On; null = todas.</summary>
    public string? JobOnMaquina { get; private set; }

    /// <summary>As máquinas do filtro da escolha do Job On.</summary>
    public IReadOnlyList<string> Maquinas { get; private set; } = Array.Empty<string>();

    /// <summary>A recusa da escolha do Job On (PRG do AbrirJobOn); null sem recusa.</summary>
    public string? JobOnEscolhaRefusal { get; private set; }

    /// <summary>
    /// A produção anterior guardada no Peso (o Owner 2026-10-17): o contexto
    /// do Job On anterior e o peso que essa produção usou, tirado do SAP.
    /// </summary>
    public PesoProducaoAnteriorReadModel? ProducaoAnterior { get; private set; }

    /// <summary>Os outros Job Ons da mesma referência, os mais recentes primeiro.</summary>
    public IReadOnlyList<PesoProducaoAnteriorOpcao> ProducaoAnteriorOpcoes { get; private set; } = Array.Empty<PesoProducaoAnteriorOpcao>();

    /// <summary>
    /// O Job On mostrado como produção anterior: o escolhido no select, o
    /// guardado, ou o mais recente da mesma referência.
    /// </summary>
    public PesoProducaoAnteriorOpcao? ProducaoAnteriorEscolhida { get; private set; }

    /// <summary>A recusa ao guardar a produção anterior (PRG); null sem recusa.</summary>
    public string? ProducaoAnteriorRefusal { get; private set; }

    /// <summary>O peso do SAP ilegível, devolvido ao campo depois da recusa.</summary>
    public string? ProducaoAnteriorEcho { get; private set; }

    /// <summary>O aviso de que a produção anterior foi guardada (PRG).</summary>
    public bool ProducaoAnteriorGuardada { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "cm_tool_id")] string? cmToolId,
        [FromQuery(Name = "bq_tool_id")] string? bqToolId,
        [FromQuery(Name = "peso_id")] int? pesoId,
        [FromQuery(Name = "controlo_id")] int? controloId,
        [FromQuery(Name = "comparar")] int? compararPesoId,
        [FromQuery(Name = "create_refusal")] string? createRefusal,
        [FromQuery(Name = "production_refusal")] string? productionRefusal,
        [FromQuery(Name = "measurement_refusal")] string? measurementRefusal,
        [FromQuery(Name = "pdf")] string? pdfSaved,
        [FromQuery(Name = "pdf_refusal")] string? pdfRefusal,
        [FromQuery(Name = "historico")] string? historico,
        [FromQuery(Name = "referencia")] string? referencia,
        [FromQuery(Name = "producao")] string? producao,
        [FromQuery(Name = "cap_h")] string? capH,
        [FromQuery(Name = "supor_calote")] string? suporCalote,
        [FromQuery(Name = "comparacao")] string? comparacao,
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "peso_inicial")] int? pesoInicialId,
        [FromQuery(Name = "comparacao_id")] int? comparacaoId,
        [FromQuery(Name = "comparacao_refusal")] string? comparacaoRefusal,
        [FromQuery(Name = "ferramenta")] string? ferramenta,
        [FromQuery(Name = "tipo")] string? tipo,
        [FromQuery(Name = "data")] string? data,
        [FromQuery(Name = "pagina")] int? pagina,
        [FromQuery(Name = "abrir")] string? abrir,
        [FromQuery(Name = "peso_1")] int? peso1,
        [FromQuery(Name = "peso_2")] int? peso2,
        [FromQuery(Name = "echo_temperature")] string? echoTemperature,
        [FromQuery(Name = "echo_process")] string? echoProcess,
        [FromQuery(Name = "echo_puncao")] string? echoPuncao,
        [FromQuery(Name = "echo_nominal")] string? echoNominal,
        [FromQuery(Name = "echo_marisa")] string? echoMarisa,
        [FromQuery(Name = "leitura_refusal")] string? leituraRefusal,
        [FromQuery(Name = "leitura_cm")] string? leituraCm,
        [FromQuery(Name = "leitura_water")] string? leituraWater,
        [FromQuery(Name = "associado")] string? associado = null,
        [FromQuery(Name = "associacao_refusal")] string? associacaoRefusal = null,
        [FromQuery(Name = "aviso")] string? aviso = null,
        [FromQuery(Name = "ignoradas")] string? ignoradas = null,
        [FromQuery(Name = "peso_ref")] string? pesoRef = null,
        [FromQuery(Name = "modo")] string? modo = null,
        [FromQuery(Name = "jobon_refusal")] string? jobOnRefusal = null,
        [FromQuery(Name = "maquina")] string? maquina = null,
        [FromQuery(Name = "sem_jobon")] string? semJobOnFiltro = null,
        [FromQuery(Name = "anterior")] int? anteriorJobOnId = null,
        [FromQuery(Name = "anterior_recusa")] string? anteriorRecusa = null,
        [FromQuery(Name = "anterior_peso")] string? anteriorPeso = null,
        [FromQuery(Name = "anterior_ok")] string? anteriorOk = null)
    {
        ViewData["Title"] = "Peso — Controlo";

        ProducaoAnteriorRefusal = anteriorRecusa switch
        {
            "peso" => "O peso da produção anterior tem de ser um número maior que 0. Nada foi guardado.",
            "jobon" => "A produção anterior escolhida já não existe. Nada foi guardado.",
            _ => null
        };
        ProducaoAnteriorEcho = ProducaoAnteriorRefusal is null ? null : Sanitize(anteriorPeso);
        ProducaoAnteriorGuardada = anteriorOk == "1";

        // A associação posterior (PRG): o aviso de sucesso ou a recusa tipada.
        PesoAssociated = associado == "1";
        if (associacaoRefusal is not null
            && Enum.TryParse(associacaoRefusal, out AssociatePesoToControloResult parsedAssociation)
            && parsedAssociation != AssociatePesoToControloResult.Success)
        {
            AssociationRefusal = parsedAssociation;
        }

        // A calote SIMULADA (o campo do apoio TP/calote): parse flexível
        // (vírgula OU ponto); presente mas ilegível ou negativa é aviso
        // honesto sem bloqueio — o apoio segue com o h efetivo. Nunca é
        // gravada: vive apenas nesta exibição.
        var capParsed = ParseOptionalDecimal(capH);
        decimal? capSimulada;
        bool capIlegivel;
        if (capParsed is { } capValor && capValor >= 0m)
        {
            capSimulada = capValor;
            capIlegivel = false;
        }
        else
        {
            capSimulada = null;
            capIlegivel = Sanitize(capH) is not null;
        }

        CapSimulada = capSimulada;
        CapSimuladaIlegivel = capIlegivel;

        // O CORTE H: a barra do módulo é SEMPRE completa (o
        // ShellStateFactory — o mapeamento num só sítio; nada de
        // capability aqui).
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = historico == "1" ? "historico" : comparacao == "1" ? "comparacao" : "peso",
            Functions = ShellStateFactory.ControloFunctions(
                historico == "1" ? "historico" : comparacao == "1" ? "comparacao" : "peso",
                jobOnId, controloId)
        };

        // O HISTÓRICO DO CONTROLO (o Owner 2026-10-12: «procurar pesos pela
        // referencia e produçao» — ao uso do dmo-html: os TRÊS tipos de
        // registo (Peso, Pegamentos, Comparação) com os filtros da oficina,
        // o «Comparar Pesos guardados» e o detalhe do registo de comparação).
        // A procura vive NA TAB (?historico=1 — o TOTAL de todas); a folha da
        // produção fica só com o fundo (a referência) — nunca em dois lados
        // (o Owner 2026-10-13: «o historico de pesos esta em dois lados»).
        HistoricoTab = historico == "1";
        if (HistoricoTab)
        {
            // O ABRIR do registo selecionado (a linha escolhida — o padrão
            // da casa: o radio escolhe, o botão FORA age): o valor transporta
            // o tipo e o id; o roteamento por tipo é do backend.
            if (ParseAbrir(abrir) is { } abrirAlvo)
            {
                var (abrirKind, abrirId, abrirControloId) = abrirAlvo;
                return abrirKind switch
                {
                    ControloHistoryKind.Peso => RedirectToPage("/Controlo/Peso", new { peso_id = abrirId }),
                    ControloHistoryKind.Pegamentos => abrirControloId is int pegamentosControlo
                        ? RedirectToPage("/Controlo/Pegamentos", new { controlo_id = pegamentosControlo })
                        : RedirectToPage("/Controlo/Pegamentos", new { pegamentos_id = abrirId }),
                    ControloHistoryKind.Resumo => RedirectToPage("/Controlo/Resumo", new { jobon_id = abrirId }),
                    _ => RedirectToPage("/Controlo/Peso", new { historico = "1", comparacao_id = abrirId })
                };
            }

            HistoryReference = Sanitize(referencia);
            HistoryProduction = Sanitize(producao);
            HistoryTool = Sanitize(ferramenta);
            HistoryTipo = ParseTipo(tipo);
            HistoryData = ParseData(data);
            HistorySemJobOn = semJobOnFiltro == "1";

            // A procura em dois passos: com a referência escrita, o nº de
            // produção escolhe-se num dropdown só com as produções dela.
            if (HistoryReference is { } referenciaHistorico && !HistorySemJobOn)
            {
                HistoryProducoes = (await _jobOnList.ListByReferenceAsync(
                        referenciaHistorico, null, HttpContext.RequestAborted))
                    .Select(p => p.ProductionNumber)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                // Uma produção de outra referência (a referência mudou) não filtra.
                if (HistoryProduction is { } producaoEscolhida && !HistoryProducoes.Contains(producaoEscolhida))
                {
                    HistoryProduction = null;
                }
            }

            // O histórico abre vazio: só procura com pelo menos um filtro.
            if (HistoricoComProcura)
            {
                var historyPage = await HistoricoControloQuery.SearchPageAsync(
                    HistoryReference, HistoryProduction, HistoryTool, HistoryTipo, HistoryData,
                    pagina, cancellationToken: HttpContext.RequestAborted, semJobOn: HistorySemJobOn);
                HistoryResults = historyPage.Items;
                HistoryPager = ListPager.For(historyPage);
            }

            // O detalhe do registo de comparação guardado (?comparacao_id —
            // o open do tipo Comparação renderiza o resultado aqui mesmo).
            if (comparacaoId is int detalheId)
            {
                HistoricoComparacao = await ComparacaoQuery.ObterComparacaoGuardadaAsync(
                    detalheId, HttpContext.RequestAborted);
            }

            // O «Comparar Pesos guardados» (os selects primeiro/segundo peso
            // do html): os Pesos guardados para os selects e a comparação
            // pelos ids (o endpoint por id no backend).
            // Dois passos (16/10/2026): a referência primeiro e só depois os
            // Pesos dela nos selects. Um link com peso_1 e sem referência
            // reabre os selects na referência desse Peso.
            PesoReferencia = Sanitize(pesoRef);
            if (PesoReferencia is null && (peso1 ?? peso2) is int pesoDoLink)
            {
                PesoReferencia = await ComparacaoQuery.ReferenciaDoPesoAsync(
                    pesoDoLink, HttpContext.RequestAborted);
            }

            if (PesoReferencia is not null)
            {
                PesoOpcoes = await ComparacaoQuery.ListarPesosPorReferenciaAsync(
                    PesoReferencia, HttpContext.RequestAborted);
                PesoReferenciaSemPesos = PesoOpcoes.Count == 0;
            }

            Peso1 = peso1;
            Peso2 = peso2;
            var escolhidos = new[] { peso1, peso2 }.OfType<int>().Distinct().ToList();
            PesosEscolhidos = await ComparacaoQuery.OpcoesDosPesosAsync(escolhidos, HttpContext.RequestAborted);
            if (peso1 is int primeiro && peso2 is int segundo)
            {
                // O mesmo Peso nos dois lados compara-se (Δ = 0) com aviso.
                PesoComparacaoGuardados = await ComparacaoQuery.CompararGuardadosAsync(
                    primeiro, segundo, HttpContext.RequestAborted);
                if (PesoComparacaoGuardados is null)
                {
                    CompararRefusal = "escolher";
                }
                else
                {
                    CompararMesmoPeso = primeiro == segundo;
                }
            }
            else if (peso1 is not null || peso2 is not null)
            {
                CompararRefusal = "escolher";
            }

            return Page();
        }

        // A AUDITORIA DO CODEX (achado 2): o ECHO dos valores submetidos —
        // apenas quando a recusa correspondente está presente (nunca um
        // valor velho a prefilar um form fresco).
        var comRecusaDeCriacao = (createRefusal is not null
            && Enum.TryParse(createRefusal, out CreateToolScopedPesoResult parsedEcho)
            && parsedEcho != CreateToolScopedPesoResult.Success)
            || (productionRefusal is not null
            && Enum.TryParse(productionRefusal, out CreatePesoForControloResult parsedProductionEcho)
            && parsedProductionEcho != CreatePesoForControloResult.Success);
        if (comRecusaDeCriacao)
        {
            EchoTemperature = Sanitize(echoTemperature);
            EchoCmProcess = Sanitize(echoProcess);
            EchoVolumePuncao = Sanitize(echoPuncao);
            EchoNominalWeight = ParseOptionalDecimal(echoNominal)?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                ?? Sanitize(echoNominal);
            EchoVolumeMarisa = Sanitize(echoMarisa);
        }

        // A5: o Peso foi criado sem o nominal ilegível — o aviso diz o que ficou de fora.
        NominalNaoPositivo = aviso == AvisoNominalNaoPositivo;
        NominalIlegivelAviso = aviso == AvisoNominalIlegivel || NominalNaoPositivo;
        if (NominalIlegivelAviso)
        {
            NominalIlegivelValor = Sanitize(echoNominal);
        }

        LeituraIlegivel = leituraRefusal == "ilegivel";
        LeituraRefusal = leituraRefusal == "massa" || LeituraIlegivel;
        if (LeituraRefusal)
        {
            EchoLeituraSubject = Sanitize(leituraCm);
            EchoLeituraWater = Sanitize(leituraWater);
        }

        // A COMPARAÇÃO DE PESO (o tab ?comparacao=1 — a superfície própria
        // do dmo-html): a produção, o Peso inicial, os contra-moldes e as
        // novas leituras; o resultado é sempre do backend. A folha do Peso
        // fica de fora (o padrão do tab do histórico).
        ComparacaoTab = comparacao == "1";
        if (ComparacaoTab)
        {
            // A produção pedida: o jobon_id do GET ou o da produção do
            // controlo (o atalho da folha: ?controlo_id=N&comparacao=1
            // aterra com a produção e o peso inicial pré-selecionados — o
            // mesmo pré-select do html ao sair do Resumo).
            int? producaoPedida = jobOnId;
            if (producaoPedida is null && controloId is int controloDaComparacao)
            {
                producaoPedida = await _db.Controlos.AsNoTracking()
                    .Where(c => c.Id == controloDaComparacao)
                    .Select(c => (int?)c.JobOnId)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);
            }

            // Dois passos (16/10/2026): a referência primeiro; o select só
            // traz as produções dela. Sem referência escrita, vale a da
            // produção pedida; sem nenhuma das duas, o select fica vazio.
            await CarregarProducoesDaReferenciaAsync(
                Sanitize(referencia), producaoPedida, HttpContext.RequestAborted);

            if (ProducaoSelecionadaJobOnId is int producaoEscolhida)
            {
                Iniciais = await ComparacaoQuery.ListarIniciaisAsync(
                    producaoEscolhida, HttpContext.RequestAborted);

                // O peso inicial: o selecionado (se é desta produção) ou o
                // PRIMEIRO da produção (o html auto-seleciona o primeiro — o
                // peso inicial é o mais antigo da produção).
                PesoInicialSelecionado = pesoInicialId is int pedido && Iniciais.Any(i => i.PesoId == pedido)
                    ? pedido
                    : Iniciais.FirstOrDefault()?.PesoId;
            }

            // O contra-molde: o selecionado ou o CM do peso inicial (o
            // fallback do html).
            ContraMoldeSelecionado = Sanitize(cmToolId)
                ?? Iniciais.FirstOrDefault(i => i.PesoId == PesoInicialSelecionado)?.CmToolId;

            // As linhas por omissão (o html começa com UMA leitura; o form
            // nativo sem JS renderiza quatro — as linhas em branco são
            // ignoradas pelo backend).
            if (ComparacaoLinhas.Count == 0)
            {
                ComparacaoLinhas = Enumerable.Range(0, 4).Select(_ => ((string?)null, (string?)null)).ToList();
            }

            // O resultado do registo guardado (?comparacao_id — o PRG do
            // Guardar aterra com o resultado a partir do PERSISTIDO).
            if (comparacaoId is int guardadaId)
            {
                ComparacaoGuardada = await ComparacaoQuery.ObterComparacaoGuardadaAsync(
                    guardadaId, HttpContext.RequestAborted);
            }

            await CarregarBaseAsync(HttpContext.RequestAborted);

            if (comparacaoRefusal is not null
                && Enum.TryParse(comparacaoRefusal, out PesoComparacaoResultado parsedComparacao)
                && parsedComparacao != PesoComparacaoResultado.Sucesso)
            {
                ComparacaoRefusal = parsedComparacao;
            }

            // Os candidatos CM só quando o formulário das leituras aparece.
            if (PesoInicialSelecionado is not null || ComparacaoRefusal is not null)
            {
                await CarregarContraMoldesAsync(HttpContext.RequestAborted);
            }

            // A3/A4: as leituras que ficaram de fora da comparação guardada
            // (PRG do Guardar) — o aviso diz quais; não foram gravadas.
            if (ComparacaoGuardada is not null)
            {
                ComparacaoLeiturasFora = DecodeLeiturasFora(ignoradas);
                ComparacaoLeiturasForaGuardada = ComparacaoLeiturasFora.Count > 0;
            }

            // Comparação tem tab própria e preserva o Job On da produção.
            if (ViewData["Shell"] is ShellPresentation shell)
            {
                ViewData["Shell"] = shell with
                {
                    Functions = ShellStateFactory.ControloFunctions(
                        "comparacao", ProducaoSelecionadaJobOnId, controloId)
                };
            }
            return Page();
        }

        // Recusas tipadas do POST anterior, transportadas por query (PRG)
        // com o vocabulário fechado dos enums.
        if (createRefusal is not null
            && Enum.TryParse(createRefusal, out CreateToolScopedPesoResult parsedCreate)
            && parsedCreate != CreateToolScopedPesoResult.Success)
        {
            CreateRefusal = parsedCreate;
        }

        if (measurementRefusal is not null
            && Enum.TryParse(measurementRefusal, out RecordPesoMeasurementResult parsedMeasurement)
            && parsedMeasurement != RecordPesoMeasurementResult.Success)
        {
            MeasurementRefusal = parsedMeasurement;
        }

        // Recusa tipada do POST production-scoped anterior (PRG por query).
        if (productionRefusal is not null
            && Enum.TryParse(productionRefusal, out CreatePesoForControloResult parsedProduction)
            && parsedProduction != CreatePesoForControloResult.Success)
        {
            ProductionRefusal = parsedProduction;
        }

        // O resultado da geração do PDF (PRG por query): o caminho do
        // ficheiro gravado ou a recusa tipada.
        PdfSavedPath = pdfSaved;
        PdfRefusal = pdfRefusal;

        // CORTE C — o modo production-scoped (?controlo_id): o contexto da
        // produção resolve as ferramentas (HERDADAS — o operador nunca as
        // escolhe nesta folha); o Peso baseline do controlo é aberto quando
        // já existe (get-or-open: sem Peso, o formulário abaixo pede a
        // temperatura medida); os candidatos da Comparação_B e o diferencial
        // do histórico selecionado (?comparar) seguem a spec.
        if (controloId is int canonicalControloId)
        {
            ControloIdFromQuery = canonicalControloId;
            ProductionContext = await _productionContext.GetByControloAsync(
                canonicalControloId, HttpContext.RequestAborted);

            // Os factos técnicos atuais das ferramentas HERDADAS do contexto
            // (não do Peso!): sem o peso ainda, o formulário precisa deles
            // para saber o que já está registado (só a temperatura e os
            // técnicos EM FALTA pedem input humano).
            // CM, BQ e MF (o MF só pela referência) com o técnico e a
            // densidade do vidro numa só consulta.
            if (ProductionContext is not null)
            {
                var ferramentas = await ToolFactsQuery.GetAsync(
                    ProductionContext.CmToolId,
                    ProductionContext.BqToolId,
                    ProductionContext.MfToolId,
                    HttpContext.RequestAborted);
                CmTool = ferramentas.CmTool;
                CmTechnical = ferramentas.CmTechnical;
                ConfiguredGlassDensity = ferramentas.CmGlassDensity;
                BqTool = ferramentas.BqTool;
                BqTechnical = ferramentas.BqTechnical;
                MfTool = ferramentas.MfTool;
            }

            // O APOIO TP/CALOTE (o painel de factos): a calote efetiva é a do
            // Job On quando existe, senão a h_ref da Tool — calculado PELO
            // BACKEND pela query canónica (fonte única; o browser nunca
            // calcula física). A simulação (cap_h) sobrepõe-se na exibição.
            if (ProductionContext?.CmToolId is { } capContextCmToolId)
            {
                // O técnico da CM e a calote do Job On já foram lidos.
                CapSupport = capContextCmToolId.Length > 0 && capContextCmToolId.Trim() == capContextCmToolId
                    ? PesoCapSupportQuery.FromKnown(CmTechnical, ProductionContext.CaloteMm, capSimulada)
                    : await CapSupportQuery.GetAsync(
                        new PesoCapSupportRequest(capContextCmToolId, canonicalControloId, capSimulada),
                        HttpContext.RequestAborted);
            }

            Peso = await _pesoQuery.FindByControloAsync(canonicalControloId, HttpContext.RequestAborted);

            // A ASSOCIAÇÃO POSTERIOR (o Owner 2026-10-16): sem Peso ainda,
            // os Pesos sem produção da MESMA CM (tool_id) do Job On são
            // propostos — associar é sempre o botão explícito.
            if (Peso is null)
            {
                AssociationCandidates = await _pesoAssociation.FindCandidatesForControloAsync(
                    canonicalControloId, HttpContext.RequestAborted);
            }

            if (Peso is not null)
            {
                ComparisonCandidates = await _pesoComparison.FindCandidatesAsync(
                    canonicalControloId, HttpContext.RequestAborted);

                // O DROPDOWN em tempo real (o Owner 2026-10-12: «existir um
                // dropdown em que conseguimos escolher a produçao em tempo
                // real para comparar. chefe recebe peso a comparar com
                // produçao 202602, mas se quiser comparar com 202601 pode
                // o fazer»): SEM escolha explícita, a PROPOSTA da app — a
                // última da MESMA MÁQUINA (o primeiro candidato, o ranking
                // do desgaste) — carrega de imediato; o dropdown muda quando
                // o operador quiser.
                // Depois (o Owner 2026-10-17): «ao procurar aparecer as
                // diferenças … e depois ao escolher aí sim a tabela com
                // detalhes» — a procura já mostra as médias de cada produção;
                // a tabela só aparece com uma escolha explícita.
                var effectiveComparisonId = compararPesoId;

                if (effectiveComparisonId is int historicalPesoId && historicalPesoId != Peso.PesoId)
                {
                    Comparison = await _pesoComparison.CompareAsync(
                        Peso, historicalPesoId, HttpContext.RequestAborted);
                }

                await CarregarSuposicaoAsync(Peso, canonicalControloId, suporCalote);
                await LoadProducaoAnteriorAsync(Peso.PesoId, ProductionContext?.JobOnId, anteriorJobOnId);
            }

            // O histórico da referência vive só na tab (?historico=1): a
            // folha não o apresenta, por isso não o lê.

            if (ViewData["Shell"] is ShellPresentation shell)
            {
                ViewData["Shell"] = shell with
                {
                    Functions = ShellStateFactory.ControloFunctions(
                        "peso", ProductionContext?.JobOnId, canonicalControloId)
                };
            }
            await LoadJobOnNoticeAsync();
            return Page();
        }

        if (pesoId is int canonicalPesoId)
        {
            // Contexto do Peso aberto: o read model completo (leituras com
            // os valores derivados e médias). O histórico da ferramenta
            // vive na tab Histórico e não é lido aqui.
            Peso = await _pesoQuery.FindPesoAsync(canonicalPesoId, HttpContext.RequestAborted);
            if (Peso is not null)
            {
                // Peso sem produção: as produções cuja CM usa este tool_id e
                // ainda sem Peso — a app pergunta; nunca associa sozinha.
                if (Peso.ControloId is null)
                {
                    ProductionCandidates = await _pesoAssociation.FindProductionsForPesoAsync(
                        canonicalPesoId, HttpContext.RequestAborted);
                }

                // Abrir por peso_id (histórico/PRG) não perde a produção
                // desse registo. Resolver apresentação/links: não associar
                // Pesos tool-scoped nem substituir o Peso selecionado.
                if (Peso.ControloId is int pesoControloId
                    && ViewData["Shell"] is ShellPresentation shell)
                {
                    OpenedPesoProduction = await _productionContext.GetByControloAsync(
                        pesoControloId, HttpContext.RequestAborted);
                    ViewData["Shell"] = shell with
                    {
                        Functions = ShellStateFactory.ControloFunctions(
                            "peso", OpenedPesoProduction?.JobOnId, pesoControloId)
                    };
                }

                // COMPARAR NA FOLHA (o Diogo, 16/10: «comparar peso deveria
                // ser uma opção dentro de uma folha de peso na app, não no
                // pdf»): o Peso aberto pelo histórico compara-se aqui mesmo
                // com outra produção da mesma referência — a lista já vem
                // filtrada pela referência do Peso; o resultado só aparece
                // quando uma produção é escolhida no dropdown.
                if (Peso.ControloId is int controloDoPeso && Peso.Averages is not null)
                {
                    ComparisonCandidates = await _pesoComparison.FindCandidatesAsync(
                        controloDoPeso, HttpContext.RequestAborted);
                    if (compararPesoId is int escolhido
                        && escolhido != Peso.PesoId
                        && ComparisonCandidates.Any(c => c.PesoId == escolhido))
                    {
                        Comparison = await _pesoComparison.CompareAsync(
                            Peso, escolhido, HttpContext.RequestAborted);
                    }

                    await CarregarSuposicaoAsync(Peso, controloDoPeso, suporCalote);
                }
            }

            await LoadJobOnNoticeAsync();
            return Page();
        }

        // Novo Peso associado a um Job On (?modo=jobon), em dois passos:
        // procura-se a referência (e a máquina), depois escolhe-se a
        // produção dessa referência; a folha abre pelo controlo_id dela
        // (POST AbrirJobOn). As ferramentas vêm do Job On.
        ModoJobOn = modo == ModoJobOnValor;
        if (ModoJobOn)
        {
            JobOnReferencia = Sanitize(referencia);
            JobOnMaquina = Sanitize(maquina);
            Maquinas = await _db.Machines.AsNoTracking()
                .OrderBy(m => m.Code)
                .Select(m => m.Code)
                .ToListAsync(HttpContext.RequestAborted);
            if (JobOnReferencia is { } referenciaProcurada)
            {
                ProducoesJobOn = await _jobOnList.ListByReferenceAsync(
                    referenciaProcurada, JobOnMaquina, HttpContext.RequestAborted);
            }
            JobOnEscolhaRefusal = jobOnRefusal switch
            {
                "sem-escolha" => "Escolha um Job On para associar o Peso.",
                "inexistente" => "O Job On escolhido não existe. Nenhum Peso foi criado.",
                _ => null
            };
            return Page();
        }

        // Contexto de escolha de ferramentas (P2-5): o operador escreve o
        // que vê nas folhas — A REFERÊNCIA (ST100, C142). A resolução
        // canónica é PELA REFERÊNCIA primeiro (a mais recente Tool com
        // ela — o lote corrente), do tipo certo do slot (CM na CM, BQ na
        // BQ: uma referência que resolva noutro tipo é «não registada»
        // para este slot — honesto, nunca um swap silencioso); o tool_id
        // opaco continua a resolver como fallback (os links profundos e
        // o PRG antigo), mas nenhuma folha o pede.
        RequestedCmToolId = Sanitize(cmToolId);
        RequestedBqToolId = Sanitize(bqToolId);

        if (!string.IsNullOrWhiteSpace(cmToolId))
        {
            CmTool = await ResolveToolAsync(cmToolId, ToolType.Cm, HttpContext.RequestAborted);
            CmToolRequestedNotFound = CmTool is null;
            if (CmTool is not null)
            {
                CmTechnical = await _toolTechnical.FindByToolIdAsync(CmTool.ToolId, HttpContext.RequestAborted);
                if (CmTechnical?.Process is { } process)
                {
                    ConfiguredGlassDensity = await _glassDensityConfiguration.FindByProcessAsync(
                        process,
                        HttpContext.RequestAborted);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(bqToolId))
        {
            BqTool = await ResolveToolAsync(bqToolId, ToolType.Bq, HttpContext.RequestAborted);
            BqToolRequestedNotFound = BqTool is null;
            if (BqTool is not null)
            {
                BqTechnical = await _toolTechnical.FindByToolIdAsync(BqTool.ToolId, HttpContext.RequestAborted);
            }
        }

        // O apoio TP/calote no modo tool-scoped (pesagem antes da produção):
        // sem Job On, a calote efetiva cai na h_ref da Tool (o html: Tool CM).
        if (CmTool is not null)
        {
            // O técnico da CM já foi lido para o mesmo tool_id.
            CapSupport = CmTool.ToolId.Length > 0 && CmTool.ToolId.Trim() == CmTool.ToolId
                ? PesoCapSupportQuery.FromKnown(CmTechnical, null, capSimulada)
                : await CapSupportQuery.GetAsync(
                    new PesoCapSupportRequest(CmTool.ToolId, null, capSimulada),
                    HttpContext.RequestAborted);
        }

        return Page();
    }

    /// <summary>
    /// O «SUPOMOS» da calote do tampão (o Diogo, 16/10: «quão diferente seria
    /// se a calote do tampão do 202601 fosse 5,2 mm em vez de 3 mm»): a calote
    /// atual do Peso (a do Job On, senão a h_ref da CM) e, quando há um valor
    /// suposto, o peso médio e a diferença para a produção comparada com essa
    /// calote. Só leitura: o Peso e o Job On nunca são alterados.
    /// </summary>
    private async Task CarregarSuposicaoAsync(PesoReadModel peso, int controloId, string? suporCalote)
    {
        if (peso.Averages is null)
        {
            return;
        }

        CaloteDaComparacao = await CapSupportQuery.GetAsync(
            new PesoCapSupportRequest(peso.CmToolId, controloId), HttpContext.RequestAborted);

        SuporCaloteTexto = Sanitize(suporCalote);
        if (SuporCaloteTexto is null)
        {
            return;
        }

        if (ParseOptionalDecimal(SuporCaloteTexto) is not { } suposta || suposta < 0m)
        {
            SuposicaoIlegivel = true;
            return;
        }

        if (CaloteDaComparacao is not { TemReferencia: true, DiametroMm: { } diametro, AlturaEfetivaMm: { } atual })
        {
            return;
        }

        Suposicao = PesoSuposicaoCalculation.Calote(
            diametro,
            atual,
            suposta,
            peso.GlassDensityGramsPerCm3,
            peso.Averages.AverageGlassWeightGrams,
            Comparison?.HistoricalAverages?.AverageGlassWeightGrams);
    }

    /// <summary>
    /// A resolução do slot (P2-5): pela REFERÊNCIA primeiro (a Tool mais
    /// recente com ela — o lote corrente), exigindo o TIPO do slot; o
    /// tool_id canónico continua a resolver como fallback (o fluxo
    /// profundo de sempre). Null quando nada do pedido resolve no
    /// registo do tipo certo.
    /// </summary>
    private async Task<ToolLookupResult?> ResolveToolAsync(
        string requested,
        ToolType expectedType,
        CancellationToken cancellationToken)
    {
        var byReference = await _toolLookup.FindByReferenceAsync(requested, expectedType, cancellationToken);
        if (byReference is not null)
        {
            return byReference;
        }

        var byToolId = await _toolLookup.FindByToolIdAsync(requested, cancellationToken);
        return byToolId is { Type: var toolIdType } && toolIdType == expectedType
            ? byToolId
            : null;
    }

    /// <summary>
    /// POST nativo: criar um Peso tool-scoped. O browser submete os
    /// tool_ids canónicos, a temperatura da água medida e — apenas para os
    /// valores técnicos Tool-owned EM FALTA — os valores completados no
    /// fluxo (gravados no tool_id owner, nunca como facto do Peso).
    /// Sucesso volta ao Peso criado por redirect (PRG); a recusa é tipada e
    /// volta por query com o contexto das ferramentas escolhidas. A seam
    /// de conta é exigida pelo [Authorize] da página: sem sessão
    /// autenticada o pedido é desafiado antes de qualquer mutação.
    /// </summary>
    public async Task<IActionResult> OnPostCreateAsync(
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "bq_tool_id")] string? bqToolId,
        [FromForm(Name = "water_temperature")] string? waterTemperature,
        [FromForm(Name = "cm_process")] string? cmProcess,
        [FromForm(Name = "cm_volume_puncao")] string? cmVolumePuncao,
        [FromForm(Name = "cm_nominal_weight")] string? cmNominalWeight,
        [FromForm(Name = "bq_volume_marisa")] string? bqVolumeMarisa)
    {
        var sanitizedCmToolId = Sanitize(cmToolId);
        var sanitizedBqToolId = Sanitize(bqToolId);

        // O cálculo principal é do backend: a temperatura medida chega como
        // texto e é parseada FLEXIVELMENTE (o stresstest 2026-10-12: a
        // oficina escreve VÍRGULA decimal — «20,4», como o placeholder
        // promete; ponto OU vírgula valem); sem valor utilizável a recusa é
        // a tipada da temperatura (nunca zero inventado).
        var parsedTemperature = ParseOptionalDecimal(waterTemperature);
        if (parsedTemperature is null)
        {
            // Vazia ou ilegível: a recusa diz «ilegível» (não «fora do
            // intervalo», que é a recusa do backend para 5–35 °C).
            return CreateRefused(
                sanitizedCmToolId,
                sanitizedBqToolId,
                CreateToolScopedPesoResult.UnreadableWaterTemperature,
                waterTemperature, cmProcess, cmVolumePuncao, cmNominalWeight, bqVolumeMarisa);
        }

        var temperature = parsedTemperature.Value;

        // Valores técnicos em falta fornecidos: o parse é flexível (ponto OU
        // vírgula); o input PRESENTE mas ILEGÍVEL em campos requeridos é
        // RECUSADO tipadamente (o stresstest: «abc» passava calado como
        // não-fornecido — o typo do operador desaparecia em silêncio e o
        // valor da Tool era usado sem ele saber; agora a recusa Missing*
        // explica o que falta).
        var parsedPuncao = ParseOptionalDecimal(cmVolumePuncao);
        if (Sanitize(cmVolumePuncao) is not null && parsedPuncao is null)
        {
            return CreateRefused(
                sanitizedCmToolId,
                sanitizedBqToolId,
                CreateToolScopedPesoResult.UnreadableCmVolumePuncao,
                waterTemperature, cmProcess, cmVolumePuncao, cmNominalWeight, bqVolumeMarisa);
        }

        var parsedMarisa = ParseOptionalDecimal(bqVolumeMarisa);
        if (Sanitize(bqVolumeMarisa) is not null && parsedMarisa is null)
        {
            return CreateRefused(
                sanitizedCmToolId,
                sanitizedBqToolId,
                CreateToolScopedPesoResult.UnreadableBqVolumeMarisa,
                waterTemperature, cmProcess, cmVolumePuncao, cmNominalWeight, bqVolumeMarisa);
        }

        // A5 (beta.65): o peso nominal é OPCIONAL — escrito mas ilegível ou ≤ 0
        // já não recusa o Peso; cria-se sem ele e um aviso explica.
        var (parsedNominal, avisoNominal) = NominalOpcional(cmNominalWeight);

        var outcome = await _createPeso.HandleAsync(
            new CreateToolScopedPeso(
                sanitizedCmToolId ?? string.Empty,
                sanitizedBqToolId ?? string.Empty,
                temperature,
                Sanitize(cmProcess),
                parsedPuncao,
                parsedNominal,
                parsedMarisa),
            HttpContext.RequestAborted);

        if (outcome.Result == CreateToolScopedPesoResult.Success)
        {
            // Permanece no contexto canónico do Peso criado e re-carrega o
            // estado persistido a partir do servidor.
            return avisoNominal is not null
                ? RedirectToPage("/Controlo/Peso", new
                {
                    peso_id = outcome.Peso!.PesoId,
                    aviso = avisoNominal,
                    echo_nominal = Sanitize(cmNominalWeight)
                })
                : RedirectToPage("/Controlo/Peso", new { peso_id = outcome.Peso!.PesoId });
        }

        return CreateRefused(sanitizedCmToolId, sanitizedBqToolId, outcome.Result,
            waterTemperature, cmProcess, cmVolumePuncao, cmNominalWeight, bqVolumeMarisa);
    }

    /// <summary>
    /// POST nativo: ASSOCIAR um Peso pré-produção a uma produção (spec
    /// PESO.md «Later association»; o Owner 2026-10-16: «quando existir um
    /// jobon com CM_ID que usa o tool_id perguntar se quero associar»). A
    /// produção chega pelo controlo_id (folha da produção) ou pelo
    /// jobon_id (folha do Peso sem produção — o controlo é assegurado de
    /// forma idempotente). Exige controlo.create; a recusa é tipada por PRG
    /// e volta à folha de origem.
    /// </summary>
    public async Task<IActionResult> OnPostAssociateAsync(
        [FromForm(Name = "peso_id")] int pesoId,
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "jobon_id")] int? jobOnId)
    {
        IActionResult BackWith(object query) => controloId is int originControlo
            ? RedirectToPage("/Controlo/Peso", new RouteValueDictionary(query) { ["controlo_id"] = originControlo })
            : RedirectToPage("/Controlo/Peso", new RouteValueDictionary(query) { ["peso_id"] = pesoId });

        var targetControloId = controloId;
        if (targetControloId is null && jobOnId is int canonicalJobOnId)
        {
            var ensured = await _ensureControlo.HandleAsync(
                new EnsureControloForJobOn(canonicalJobOnId), HttpContext.RequestAborted);
            targetControloId = ensured.ControloId;
        }

        if (targetControloId is not int resolvedControloId)
        {
            return BackWith(new { associacao_refusal = AssociatePesoToControloResult.ControloNotFound.ToString() });
        }

        var outcome = await _associatePeso.HandleAsync(
            new AssociatePesoToControlo(resolvedControloId, pesoId), HttpContext.RequestAborted);
        if (outcome.Result != AssociatePesoToControloResult.Success)
        {
            return BackWith(new { associacao_refusal = outcome.Result.ToString() });
        }

        return RedirectToPage("/Controlo/Peso", new { controlo_id = resolvedControloId, associado = "1" });
    }

    /// <summary>
    /// POST nativo do Novo Peso associado a um Job On: o jobon_id escolhido
    /// resolve o seu controlo_id (get-or-create idempotente, o mesmo do
    /// Associate) e a folha abre no modo da produção, onde CM, BQ e calote
    /// vêm do contexto do Job On. Nenhum Peso é criado aqui.
    /// </summary>
    public async Task<IActionResult> OnPostAbrirJobOnAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId)
    {
        if (jobOnId is not int escolhido)
        {
            return RedirectToPage("/Controlo/Peso", new { modo = ModoJobOnValor, jobon_refusal = "sem-escolha" });
        }

        var ensured = await _ensureControlo.HandleAsync(
            new EnsureControloForJobOn(escolhido), HttpContext.RequestAborted);
        if (ensured.ControloId is not int controloId)
        {
            return RedirectToPage("/Controlo/Peso", new { modo = ModoJobOnValor, jobon_refusal = "inexistente" });
        }

        return RedirectToPage("/Controlo/Peso", new { controlo_id = controloId });
    }

    /// <summary>
    /// POST nativo (CORTE C): criar o Peso baseline da produção pelo
    /// controlo_id — as ferramentas são HERDADAS do contexto do Job On
    /// (nunca submetidas pelo form); a temperatura da água medida e os
    /// valores técnicos em falta (regra Ferramentas: gravam no tool_id
    /// owner) são os únicos inputs humanos. O get-or-open é idempotente:
    /// um controlo com Peso devolve sempre o MESMO peso_id.
    /// </summary>
    public async Task<IActionResult> OnPostCreateForControloAsync(
        [FromForm(Name = "controlo_id")] int controloId,
        [FromForm(Name = "water_temperature")] string? waterTemperature,
        [FromForm(Name = "cm_process")] string? cmProcess,
        [FromForm(Name = "cm_volume_puncao")] string? cmVolumePuncao,
        [FromForm(Name = "cm_nominal_weight")] string? cmNominalWeight,
        [FromForm(Name = "bq_volume_marisa")] string? bqVolumeMarisa)
    {
        // O parse FLEXÍVEL da temperatura (vírgula OU ponto — a oficina
        // escreve «20,4»): sem valor utilizável a recusa é a tipada. A
        // recusa leva os VALORES submetidos de volta (a auditoria do Codex:
        // o form nunca volta vazio).
        var parsedTemperature = ParseOptionalDecimal(waterTemperature);
        if (parsedTemperature is null)
        {
            return RedirectToPage("/Controlo/Peso", new
            {
                controlo_id = controloId,
                production_refusal = CreatePesoForControloResult.UnreadableWaterTemperature.ToString(),
                echo_temperature = waterTemperature,
                echo_process = cmProcess,
                echo_puncao = cmVolumePuncao,
                echo_nominal = cmNominalWeight,
                echo_marisa = bqVolumeMarisa
            });
        }

        // O input PRESENTE mas ILEGÍVEL em campos requeridos é RECUSADO
        // tipadamente (o stresstest: «abc» passava calado — o typo do
        // operador desaparecia em silêncio e o valor da Tool era usado).
        var parsedPuncao = ParseOptionalDecimal(cmVolumePuncao);
        if (Sanitize(cmVolumePuncao) is not null && parsedPuncao is null)
        {
            return RedirectToPage("/Controlo/Peso", new
            {
                controlo_id = controloId,
                production_refusal = CreatePesoForControloResult.UnreadableCmVolumePuncao.ToString(),
                echo_temperature = waterTemperature,
                echo_process = cmProcess,
                echo_puncao = cmVolumePuncao,
                echo_nominal = cmNominalWeight,
                echo_marisa = bqVolumeMarisa
            });
        }

        var parsedMarisa = ParseOptionalDecimal(bqVolumeMarisa);
        if (Sanitize(bqVolumeMarisa) is not null && parsedMarisa is null)
        {
            return RedirectToPage("/Controlo/Peso", new
            {
                controlo_id = controloId,
                production_refusal = CreatePesoForControloResult.UnreadableBqVolumeMarisa.ToString(),
                echo_temperature = waterTemperature,
                echo_process = cmProcess,
                echo_puncao = cmVolumePuncao,
                echo_nominal = cmNominalWeight,
                echo_marisa = bqVolumeMarisa
            });
        }

        // A5 (beta.65): o nominal ilegível ou ≤ 0 já não recusa — o Peso é criado
        // sem ele e o aviso explica o que ficou de fora.
        var (parsedNominal, avisoNominal) = NominalOpcional(cmNominalWeight);

        var outcome = await _createPesoForControlo.HandleAsync(
            new CreatePesoForControlo(
                controloId,
                parsedTemperature.Value,
                Sanitize(cmProcess),
                parsedPuncao,
                parsedNominal,
                parsedMarisa),
            HttpContext.RequestAborted);

        if (outcome.Result == CreatePesoForControloResult.Success)
        {
            return avisoNominal is not null
                ? RedirectToPage("/Controlo/Peso", new
                {
                    controlo_id = controloId,
                    aviso = avisoNominal,
                    echo_nominal = Sanitize(cmNominalWeight)
                })
                : RedirectToPage("/Controlo/Peso", new { controlo_id = controloId });
        }

        return RedirectToPage("/Controlo/Peso", new
        {
            controlo_id = controloId,
            production_refusal = outcome.Result.ToString(),
            echo_temperature = waterTemperature,
            echo_process = cmProcess,
            echo_puncao = cmVolumePuncao,
            echo_nominal = cmNominalWeight,
            echo_marisa = bqVolumeMarisa
        });
    }

    /// <summary>
    /// Descarrega o PDF completo de Peso, com as duas comparações explicitamente selecionadas e valores do backend.
    /// Geração em memória: não grava PDFs no servidor nem altera dados persistidos.
    /// </summary>
    public async Task<IActionResult> OnPostGeneratePdfAsync(
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "peso_id")] int? pesoId,
        [FromForm(Name = "comparar")] int? compararPesoId,
        [FromForm(Name = "comparar_peso")] int? pesoAnteriorId = null)
    {
        PesoReadModel? peso;
        ProductionContextReadModel? context;
        if (controloId is int canonicalControloId)
        {
            peso = await _pesoQuery.FindByControloAsync(canonicalControloId, HttpContext.RequestAborted);
            if (peso is null)
            {
                return RedirectToPage("/Controlo/Peso", new { controlo_id = canonicalControloId, pdf_refusal = "sem-peso" });
            }

            context = await _productionContext.GetByControloAsync(canonicalControloId, HttpContext.RequestAborted);
        }
        else if (pesoId is int canonicalPesoId)
        {
            // O histórico: o PDF da folha de uma pesagem passada (reimprimir
            // o papel que a produção pede outra vez). O contexto vem do
            // controlo do próprio peso quando ele é production-scoped.
            peso = await _pesoQuery.FindPesoAsync(canonicalPesoId, HttpContext.RequestAborted);
            if (peso is null)
            {
                return RedirectToPage("/Controlo/Peso", new { peso_id = canonicalPesoId, pdf_refusal = "sem-peso" });
            }

            context = peso.ControloId is { } pesoControloId
                ? await _productionContext.GetByControloAsync(pesoControloId, HttpContext.RequestAborted)
                : null;
        }
        else
        {
            return RedirectToPage("/Controlo/Peso", new { pdf_refusal = "sem-peso" });
        }


        PesoComparisonView? comparison = null;
        if (compararPesoId is int historicalPesoId && historicalPesoId != peso.PesoId)
        {
            comparison = await _pesoComparison.CompareAsync(
                peso.PesoId, historicalPesoId, HttpContext.RequestAborted);
        }

        PesoComparisonView? previousPesoComparison = null;
        if (pesoAnteriorId is int selectedPesoId && selectedPesoId != peso.PesoId)
        {
            previousPesoComparison = await _pesoComparison.CompareAsync(
                peso.PesoId, selectedPesoId, HttpContext.RequestAborted);
        }

        var reference = context?.Reference ?? peso.CmToolReference ?? peso.CmToolId;
        // Uma pesagem antes da produção (tool-scoped) descarrega como
        // «sem-producao» — o nome é honesto, nunca um travessão.
        var productionNumber = context?.ProductionNumber ?? "sem-producao";
        var fileName = ControloDocumentPaths.GetPesoFileName(reference, productionNumber);

        var readings = peso.Measurements
            .Select(m => new PesoPdfGenerator.Reading(
                m.RowPosition,
                m.CmSubject,
                m.WaterWeightGrams,
                m.CapacityCm3,
                m.GlassWeightGrams))
            .ToList();

        // O SELO DE APROVAÇÃO no papel (o corte D — o alvo do peso real
        // tem o bloco «Aprovado por»): o controlo do contexto; um peso
        // tool-scoped (sem controlo) simplesmente não tem selo.
        var approval = (controloId ?? peso.ControloId) is { } approvalControloId
            ? await _approvalHandler.GetAsync(approvalControloId, HttpContext.RequestAborted)
            : null;

        // QUEM GERA O PAPEL (o rodapé do alvo: «Diogo Oliveira
        // 28/07/2026, 14:52:28») — o nome humano da sessão.
        var generatedBy = User.FindFirst(DmoClaimTypes.DisplayName)?.Value
            ?? User.FindFirst(DmoClaimTypes.Email)?.Value;

        // O volume do tampão desta produção (a calote do Job On; sem controlo, a referência da CM).
        var cap = await CapSupportQuery.GetAsync(
            new PesoCapSupportRequest(peso.CmToolId, controloId ?? peso.ControloId), HttpContext.RequestAborted);

        var bytes = _pdfGenerator.GenerateBytes(new PesoPdfGenerator.Input(
            productionNumber,
            reference,
            context?.Machine ?? "—",
            peso.CmToolId,
            peso.CmToolReference,
            peso.CmToolLot,
            peso.Process.ToString().ToUpperInvariant(),
            peso.WaterTemperatureC,
            peso.WaterDensityGramsPerCm3,
            peso.GlassDensityGramsPerCm3,
            peso.VolumeMarisaCm3,
            peso.VolumePuncaoCm3,
            peso.NominalWeightGrams,
            readings,
            peso.Averages,
            comparison,
            "",
            Approval: approval,
            GeneratedBy: generatedBy,
            PreviousPesoComparison: previousPesoComparison,
            InicioEm: context?.InicioEm, FimEm: context?.FimEm,
            VolumeTampaoCm3: cap.VolumeEfetivoCm3, CaloteMm: cap.AlturaEfetivaMm,
            ProducaoAnterior: await ProducaoAnteriorQuery.FindAsync(peso.PesoId, HttpContext.RequestAborted)));

        Response.Headers[ControloDocumentPaths.PdfFolderHeader] = Uri.EscapeDataString(ControloDocumentPaths.GetProductionRelativeFolder(reference, productionNumber));
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>
    /// A produção anterior da folha: o registo guardado no Peso e os outros
    /// Job Ons da mesma referência. A escolhida é a do select (anterior),
    /// senão a guardada, senão a mais recente.
    /// </summary>
    private async Task LoadProducaoAnteriorAsync(int pesoId, int? currentJobOnId, int? requestedJobOnId)
    {
        ProducaoAnterior = await ProducaoAnteriorQuery.FindAsync(pesoId, HttpContext.RequestAborted);
        if (currentJobOnId is not int jobOnId)
        {
            return;
        }

        ProducaoAnteriorOpcoes = await ProducaoAnteriorQuery.ListOpcoesAsync(jobOnId, HttpContext.RequestAborted);
        var wanted = requestedJobOnId ?? ProducaoAnterior?.JobOnId;
        ProducaoAnteriorEscolhida = ProducaoAnteriorOpcoes.FirstOrDefault(o => o.JobOnId == wanted)
            ?? (ProducaoAnterior?.JobOnId is int saved && saved != jobOnId && ProducaoAnterior.ProductionNumber is { } savedNumber
                ? new PesoProducaoAnteriorOpcao(saved, savedNumber, ProducaoAnterior.Machine ?? "—", ProducaoAnterior.InicioEm, ProducaoAnterior.FimEm)
                : ProducaoAnteriorOpcoes.FirstOrDefault());
    }

    /// <summary>
    /// POST nativo: guardar a produção anterior do Peso (o Owner
    /// 2026-10-17): o Job On escolhido e o peso que usou, tirado do SAP.
    /// Um peso vazio apaga o valor; ilegível ou ≤ 0 é recusa com o echo.
    /// </summary>
    public async Task<IActionResult> OnPostGuardarProducaoAnteriorAsync(
        [FromForm(Name = "peso_id")] int pesoId,
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "comparar")] int? compararPesoId,
        [FromForm(Name = "anterior")] int? anteriorJobOnId,
        [FromForm(Name = "anterior_peso")] string? anteriorPeso)
    {
        var pesoTexto = Sanitize(anteriorPeso);
        decimal? pesoGramas = null;
        if (pesoTexto is not null)
        {
            if (ParseOptionalDecimal(pesoTexto) is not { } parsed || parsed <= 0m)
            {
                return RedirectToPage("/Controlo/Peso", new
                {
                    controlo_id = controloId,
                    peso_id = controloId is null ? pesoId : (int?)null,
                    comparar = compararPesoId,
                    anterior = anteriorJobOnId,
                    anterior_recusa = "peso",
                    anterior_peso = pesoTexto
                });
            }

            pesoGramas = parsed;
        }

        // O contexto vem do controlo da produção anterior: o get-or-create
        // do Job On escolhido (o mesmo que abre a folha dessa produção).
        int? anteriorControloId = null;
        if (anteriorJobOnId is int escolhidoJobOnId)
        {
            var ensured = await _ensureControlo.HandleAsync(
                new EnsureControloForJobOn(escolhidoJobOnId), HttpContext.RequestAborted);
            if (ensured.Result != EnsureControloResult.Success)
            {
                return RedirectToPage("/Controlo/Peso", new
                {
                    controlo_id = controloId,
                    peso_id = controloId is null ? pesoId : (int?)null,
                    comparar = compararPesoId,
                    anterior_recusa = "jobon"
                });
            }

            anteriorControloId = ensured.ControloId;
        }

        var result = await ProducaoAnteriorQuery.SaveAsync(
            pesoId, anteriorControloId, pesoGramas, HttpContext.RequestAborted);
        return RedirectToPage("/Controlo/Peso", new
        {
            controlo_id = controloId,
            peso_id = controloId is null ? pesoId : (int?)null,
            comparar = compararPesoId,
            anterior = anteriorJobOnId,
            anterior_recusa = result switch
            {
                GuardarProducaoAnteriorResult.JobOnNotFound => "jobon",
                GuardarProducaoAnteriorResult.PesoInvalido => "peso",
                _ => null
            },
            anterior_ok = result == GuardarProducaoAnteriorResult.Success ? "1" : null
        });
    }

    /// <summary>
    /// POST nativo: registar uma leitura real no peso_id aberto. O browser
    /// submete apenas o número/posição CM visível e a massa de água
    /// observada; a posição física e os valores derivados são do backend.
    /// </summary>
    public async Task<IActionResult> OnPostRegisterMeasurementAsync(
        [FromForm(Name = "peso_id")] int pesoId,
        [FromForm(Name = "cm_subject")] string?[]? cmSubjects,
        [FromForm(Name = "water_weight")] string?[]? waterWeights,
        [FromForm(Name = "controlo_id")] int? controloId = null,
        [FromForm(Name = "comparar")] int? compararPesoId = null)
    {
        // Volta à folha de onde veio: a da produção (controlo_id) mantém a
        // comparação e a produção anterior visíveis.
        object Back(object? extra = null)
        {
            var values = new RouteValueDictionary(extra ?? new { });
            if (controloId is int c)
            {
                values["controlo_id"] = c;
                if (compararPesoId is int cmp) values["comparar"] = cmp;
            }
            else
            {
                values["peso_id"] = pesoId;
            }
            return values;
        }

        // As linhas do formulário (o «+» acrescenta linhas): só contam as que
        // têm massa de água; uma linha vazia é ignorada.
        var subjects = cmSubjects ?? Array.Empty<string?>();
        var weights = waterWeights ?? Array.Empty<string?>();
        var rows = new List<(string? CmSubject, decimal Water)>();
        for (var i = 0; i < weights.Length; i++)
        {
            var cmSubject = i < subjects.Length ? Sanitize(subjects[i]) : null;
            // A massa de água: o parse FLEXÍVEL (vírgula OU ponto — a oficina).
            // PRESENTE mas ilegível ou não positiva é RECUSA TIPADA com o ECHO
            // dos valores (a auditoria do Codex: o typo do operador não
            // desaparece em silêncio nem o form volta vazio). Nenhuma linha é
            // registada se uma delas for recusada.
            var waterSanitizada = Sanitize(weights[i]);
            if (waterSanitizada is null)
            {
                continue;
            }

            if (ParseOptionalDecimal(waterSanitizada) is not { } massaUtilizavel || massaUtilizavel <= 0m)
            {
                // Ilegível diz «ilegível»; ≤ 0 continua recusa (protege o cálculo).
                return RedirectToPage("/Controlo/Peso", Back(new
                {
                    leitura_refusal = ParseOptionalDecimal(waterSanitizada) is null ? "ilegivel" : "massa",
                    leitura_cm = cmSubject,
                    leitura_water = waterSanitizada
                }));
            }

            rows.Add((cmSubject, massaUtilizavel));
        }

        foreach (var (cmSubject, water) in rows)
        {
            var outcome = await _recordMeasurement.HandleAsync(
                new RecordPesoMeasurement(pesoId, cmSubject, water),
                HttpContext.RequestAborted);

            if (outcome.Result != RecordPesoMeasurementResult.Success)
            {
                return RedirectToPage("/Controlo/Peso", Back(new
                {
                    measurement_refusal = outcome.Result.ToString()
                }));
            }
        }

        return RedirectToPage("/Controlo/Peso", Back());
    }

    /// <summary>
    /// Endpoint READ-ONLY do design pass F1 (§2.4): preview em tempo real.
    /// Recebe o peso_id aberto e as linhas em preparação (texto) e resolve o
    /// contexto PERSISTIDO do peso_id pela MESMA query canónica do surface
    /// (IPesoQuery.FindPesoAsync — mesma resolução do PesoQuery), calculando
    /// com o MESMO motor do backend (PesoCalculation). O browser nunca
    /// calcula física (PESO.md L180). Zero escrita: nenhum handler de
    /// mutação é invocado. Recusas tipadas no vocabulário dos enums
    /// (PesoNotFound) e do baseline §8 (RESULT_NON_POSITIVE — recusa antes
    /// de qualquer valor exibido). Sem JS este endpoint nunca é chamado: o
    /// registo nativo por POST continua intacto (progressive enhancement).
    /// </summary>
    public sealed record PesoPreviewLineReadModel(
        int Index,
        string? CmSubject,
        string Status,
        decimal? WaterWeightGrams,
        decimal? CapacityCm3,
        decimal? GlassWeightGrams,
        decimal? DesvioCm3,
        decimal? DesvioPercent);

    public sealed record PesoPreviewTotalsReadModel(
        int SampleCount,
        decimal? AverageWaterWeightGrams,
        decimal? AverageCapacityCm3,
        decimal? AverageGlassWeightGrams,
        decimal? DesvioNominalG);

    public sealed record PesoPreviewContextReadModel(
        string Process,
        int WaterTemperatureC,
        decimal WaterDensityGramsPerCm3,
        decimal GlassDensityGramsPerCm3,
        decimal VolumeMarisaCm3,
        decimal VolumePuncaoCm3,
        decimal? NominalWeightGrams);

    public sealed record PesoPreviewReadModel(
        int PesoId,
        PesoPreviewContextReadModel Context,
        IReadOnlyList<PesoPreviewLineReadModel> Lines,
        PesoPreviewTotalsReadModel Totals,
        PesoPreviewTotalsReadModel Persisted);

    /// <summary>Opções de serialização do preview: contrato JSON camelCase para o peso-live.js.</summary>
    private static readonly System.Text.Json.JsonSerializerOptions PreviewJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// O PREVIEW do apoio TP/calote — ENDPOINT READ-ONLY DO BACKEND (a fonte
    /// única de verdade, o mesmo padrão do Preview do Peso): o browser nunca
    /// calcula física — a matemática é a do Core (PesoCapCalculation) pela
    /// MESMA query canónica que o render do servidor usa
    /// (IPesoCapSupportQuery). Resolve o contexto pelo controlo_id (o Job On
    /// manda na calote), pelo peso_id (a folha do histórico) ou pelo
    /// cm_tool_id (pesagem antes da produção) e devolve o cálculo completo
    /// para a calote pedida (a efetiva, ou a simulada no campo do apoio —
    /// a simulação NUNCA é gravada). Zero escrita; recusas não existem —
    /// sem referência TP devolve o estado «Referência por completar»
    /// (temReferencia=false), nunca um bloqueio.
    /// </summary>
    public async Task<IActionResult> OnGetCapSupportAsync(
        [FromQuery(Name = "controlo_id")] int? controloId,
        [FromQuery(Name = "peso_id")] int? pesoId,
        [FromQuery(Name = "cm_tool_id")] string? cmToolId,
        [FromQuery(Name = "calote")] string? calote)
    {
        // Parse flexível da calote pedida (vírgula OU ponto — a oficina);
        // ilegível ou negativa = sem simulação (o apoio segue com o efetivo).
        var caloteSimulada = ParseOptionalDecimal(calote);
        if (caloteSimulada is { } negativa && negativa < 0m)
        {
            caloteSimulada = null;
        }

        var toolId = string.Empty;
        int? controloDoJobOn = null;

        if (controloId is int canonicalControloId)
        {
            var context = await _productionContext.GetByControloAsync(
                canonicalControloId, HttpContext.RequestAborted);
            toolId = context?.CmToolId ?? string.Empty;
            if (context is not null)
            {
                controloDoJobOn = canonicalControloId;
            }
        }
        else if (pesoId is int canonicalPesoId)
        {
            var peso = await _pesoQuery.FindPesoAsync(canonicalPesoId, HttpContext.RequestAborted);
            toolId = peso?.CmToolId ?? string.Empty;
            controloDoJobOn = peso?.ControloId;
        }
        else
        {
            toolId = Sanitize(cmToolId) ?? string.Empty;
        }

        var readModel = await CapSupportQuery.GetAsync(
            new PesoCapSupportRequest(toolId, controloDoJobOn, caloteSimulada),
            HttpContext.RequestAborted);

        return new JsonResult(readModel, PreviewJsonOptions);
    }

    // -------------------------------------------------------------------
    // A COMPARAÇÃO DE PESO (a superfície própria do dmo-html): o POST
    // «Comparar pesos» calcula AO VIVO sem escrever nada (a fonte única no
    // backend); o POST «Guardar comparação» persiste o registo do histórico
    // tipo Comparação. As recusas são tipadas em PT; o form PRESERVA os
    // valores submetidos na recusa (o padrão do Job On: o formulário
    // fica como estava, a BD inalterada).
    // -------------------------------------------------------------------

    /// <summary>
    /// O shell do render do POST (os handlers que devolvem Page() têm de o
    /// compor como o OnGet — o layout exige-o).
    /// </summary>
    private async Task ConfigurarShellDaComparacaoAsync()
    {
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = "peso",
            Functions = ShellStateFactory.ControloFunctions("peso")
        };
    }

    /// <summary>
    /// POST nativo: comparar as leituras em preparação contra o Peso inicial
    /// — ZERO escrita (o resultado vive só nesta exibição, como no html). O
    /// cálculo é da fonte única no backend (PesoCalculation +
    /// PesoComparacaoCalculation); o browser nunca calcula física.
    /// </summary>
    public async Task<IActionResult> OnPostCompararAsync(
        [FromForm(Name = "jobon_id")] int jobOnId,
        [FromForm(Name = "peso_inicial")] int? pesoInicialId,
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "water_temperature")] string? waterTemperature,
        [FromForm(Name = "cm_subject")] string[]? cmSubjects,
        [FromForm(Name = "water_weight")] string[]? waterWeights)
    {
        ComparacaoTab = true;

        await ConfigurarShellDaComparacaoAsync();

        var (refusal, leituras, temperatura) = await PrepararComparacaoAsync(
            jobOnId, pesoInicialId, cmToolId, waterTemperature, null, cmSubjects, waterWeights,
            HttpContext.RequestAborted);
        if (leituras is null)
        {
            ComparacaoRefusal = refusal;
            await CarregarBaseAsync(HttpContext.RequestAborted);
            return Page();
        }

        ResultadoComparacao = await ComparacaoQuery.CompararLeiturasAsync(
            new PesoComparacaoLeiturasPedido(pesoInicialId!.Value, Sanitize(cmToolId), temperatura, leituras),
            HttpContext.RequestAborted);
        JuntarLeiturasForaDoBackend(ResultadoComparacao.Ignoradas);
        if (ResultadoComparacao.Refusal != PesoComparacaoResultado.Sucesso)
        {
            // A recusa da fonte única (a temperatura fora da tabela, o
            // resultado não positivo) apresenta-se como as outras — o
            // resultado não é exibido como facto.
            ComparacaoRefusal = ResultadoComparacao.Refusal;
            ResultadoComparacao = null;
        }

        await CarregarBaseAsync(HttpContext.RequestAborted);
        return Page();
    }

    /// <summary>
    /// POST nativo: guardar o registo de comparação (o histórico tipo
    /// Comparação do html — ligado ao controlo_id da produção). O cálculo é
    /// o MESMO da comparação ao vivo (a fonte única); os inputs consumidos
    /// são congelados no registo. Sucesso volta por PRG ao resultado a
    /// partir do PERSISTIDO; a recusa preserva o formulário (nada escrito).
    /// </summary>
    public async Task<IActionResult> OnPostGuardarComparacaoAsync(
        [FromForm(Name = "jobon_id")] int jobOnId,
        [FromForm(Name = "peso_inicial")] int? pesoInicialId,
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "water_temperature")] string? waterTemperature,
        [FromForm(Name = "cm_subject")] string[]? cmSubjects,
        [FromForm(Name = "water_weight")] string[]? waterWeights,
        [FromForm(Name = "comparacao_note")] string? comparacaoNote)
    {
        ComparacaoTab = true;
        await ConfigurarShellDaComparacaoAsync();

        var (refusal, leituras, temperatura) = await PrepararComparacaoAsync(
            jobOnId, pesoInicialId, cmToolId, waterTemperature, comparacaoNote, cmSubjects, waterWeights,
            HttpContext.RequestAborted);
        if (leituras is null)
        {
            ComparacaoRefusal = refusal;
            await CarregarBaseAsync(HttpContext.RequestAborted);
            return Page();
        }

        var outcome = await ComparacaoHandler.HandleAsync(
            new CreatePesoComparacao(
                pesoInicialId!.Value,
                Sanitize(cmToolId),
                temperatura,
                leituras,
                Sanitize(comparacaoNote)),
            HttpContext.RequestAborted);

        JuntarLeiturasForaDoBackend(outcome.Ignoradas);
        if (outcome.Result != PesoComparacaoResultado.Sucesso || outcome.Comparacao is null)
        {
            ComparacaoRefusal = outcome.Result;
            await CarregarBaseAsync(HttpContext.RequestAborted);
            return Page();
        }

        // A4: a comparação foi gravada com as leituras utilizáveis; as que
        // ficaram de fora seguem no PRG para o aviso (não foram gravadas).
        return RedirectToPage("/Controlo/Peso", new
        {
            comparacao = "1",
            jobon_id = jobOnId,
            peso_inicial = pesoInicialId.Value,
            comparacao_id = outcome.Comparacao.ComparacaoId,
            ignoradas = EncodeLeiturasFora(ComparacaoLeiturasFora)
        });
    }

    /// <summary>
    /// O estado do tab da comparação para o re-render do POST: os selects
    /// (produções, iniciais, contra-moldes) e o echo dos valores submetidos.
    /// </summary>
    private async Task CarregarEstadoComparacaoAsync(
        int jobOnId,
        int? pesoInicialId,
        string? cmToolId,
        string? waterTemperature,
        string? comparacaoNote,
        CancellationToken cancellationToken)
    {
        await CarregarProducoesDaReferenciaAsync(null, jobOnId > 0 ? jobOnId : null, cancellationToken);
        TemperaturaSubmetida = waterTemperature;
        NotaSubmetida = Sanitize(comparacaoNote);

        if (ProducaoSelecionadaJobOnId is int producaoEscolhida)
        {
            Iniciais = await ComparacaoQuery.ListarIniciaisAsync(producaoEscolhida, cancellationToken);
        }

        PesoInicialSelecionado = pesoInicialId;
        await CarregarContraMoldesAsync(cancellationToken);
        ContraMoldeSelecionado = Sanitize(cmToolId);
    }

    /// <summary>
    /// A BASE da comparação (o Diogo, 16/10: «usar esse peso como baseline,
    /// peso e volume dos CM que foram medidos»): o Peso inicial CM a CM, o
    /// quanto cada CM puxa a média e, com novas leituras (ao vivo ou
    /// guardadas), o quanto cada CM pesada agora mexeria na média da base.
    /// Só leitura.
    /// </summary>
    private async Task CarregarBaseAsync(CancellationToken cancellationToken)
    {
        var baseId = ComparacaoGuardada?.PesoInicialId ?? PesoInicialSelecionado;
        if (baseId is not int pesoBaseId)
        {
            return;
        }

        BaseDaComparacao = await _pesoQuery.FindPesoAsync(pesoBaseId, cancellationToken);
        if (BaseDaComparacao is not { Averages: { } mediasBase } baseLida)
        {
            return;
        }

        InfluenciaDaBase = PesoComparacaoInfluenciaCalculation.DaBase(
            baseLida.Measurements.OrderBy(m => m.RowPosition)
                .Select(m => (m.CmSubject, m.GlassWeightGrams)).ToList());

        var pares = ResultadoComparacao?.Pares ?? ComparacaoGuardada?.Pares;
        if (pares is not null)
        {
            InfluenciaDasNovas = PesoComparacaoInfluenciaCalculation.Novas(
                pares.Rows, mediasBase.AverageGlassWeightGrams, baseLida.Measurements.Count);
        }
    }

    /// <summary>
    /// O select «Produção / Job On» em dois passos: as produções da
    /// referência escrita ou, sem ela, da referência da produção pedida.
    /// A produção pedida só fica escolhida se é dessa referência; uma
    /// referência sem produções é ignorada com aviso.
    /// </summary>
    private async Task CarregarProducoesDaReferenciaAsync(
        string? referenciaEscrita,
        int? producaoPedida,
        CancellationToken cancellationToken)
    {
        var pedida = producaoPedida is int pedidaId
            ? await ProductionPicker.FindAsync(pedidaId, cancellationToken)
            : null;
        ReferenciaComparacao = referenciaEscrita ?? pedida?.Reference;
        if (ReferenciaComparacao is null)
        {
            return;
        }

        var producoes = (await ProductionPicker.ListByReferenceAsync(ReferenciaComparacao, cancellationToken)).ToList();
        ReferenciaComparacaoSemProducoes = producoes.Count == 0;

        // A produção pedida desta referência fica no select mesmo além das
        // mais recentes (o atalho da folha numa produção antiga).
        if (pedida is not null
            && producoes.All(p => p.JobOnId != pedida.JobOnId)
            && pedida.Reference.Contains(ReferenciaComparacao, StringComparison.OrdinalIgnoreCase))
        {
            producoes.Add(pedida);
        }

        Producoes = producoes;
        ProducaoSelecionadaJobOnId = pedida is not null && producoes.Any(p => p.JobOnId == pedida.JobOnId)
            ? pedida.JobOnId
            : null;
    }

    /// <summary>
    /// Os candidatos CM (o contra-molde a pesar): a MESMA referência do CM
    /// do peso inicial em primeiro (o padrão do html), depois a referência
    /// alfabeticamente.
    /// </summary>
    private async Task CarregarContraMoldesAsync(CancellationToken cancellationToken)
    {
        var referenciaDoInicial = Iniciais
            .FirstOrDefault(i => i.PesoId == PesoInicialSelecionado)?.CmToolReference;
        ContraMoldes = (await _toolCandidates.FindCandidatesAsync(
                new ToolCandidateFilter(ToolType.Cm), cancellationToken))
            .OrderBy(t => referenciaDoInicial is { } referencia && t.Reference == referencia ? 0 : 1)
            .ThenBy(t => t.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// O parse das inputs da comparação (o form preserva o que foi
    /// submetido): as linhas em branco ignoram-se; o nº de CM sem massa de
    /// água e a massa presente mas ilegível ficam DE FORA com aviso (A3,
    /// beta.65 — antes anulavam a comparação toda); a massa não positiva
    /// segue para o backend, que também a deixa de fora com aviso. Só sem
    /// nenhuma leitura utilizável a comparação é recusada (nada a comparar).
    /// A temperatura é flexível (ponto OU vírgula).
    /// </summary>
    private async Task<(PesoComparacaoResultado? Refusal, List<PesoComparacaoLeituraInput>? Leituras, decimal Temperatura)> PrepararComparacaoAsync(
        int jobOnId,
        int? pesoInicialId,
        string? cmToolId,
        string? waterTemperature,
        string? comparacaoNote,
        string[]? cmSubjects,
        string[]? waterWeights,
        CancellationToken cancellationToken)
    {
        await CarregarEstadoComparacaoAsync(jobOnId, pesoInicialId, cmToolId, waterTemperature, comparacaoNote, cancellationToken);

        // O echo das linhas submetidas (o re-render do form com os valores).
        var subjects = cmSubjects ?? Array.Empty<string>();
        var waters = waterWeights ?? Array.Empty<string>();
        var count = Math.Max(subjects.Length, waters.Length);
        var echo = new List<(string?, string?)>();
        var leituras = new List<PesoComparacaoLeituraInput>();
        var fora = new List<ComparacaoLeituraForaAviso>();
        _linhasDasLeituras.Clear();
        for (var i = 0; i < count; i++)
        {
            var subject = i < subjects.Length ? subjects[i]?.Trim() : null;
            var waterRaw = i < waters.Length ? waters[i]?.Trim() : null;
            var subjectTem = !string.IsNullOrEmpty(subject);
            var waterTem = !string.IsNullOrEmpty(waterRaw);
            if (!subjectTem && !waterTem)
            {
                continue; // linha em branco: nada a comparar
            }

            echo.Add((subject, waterRaw));
            if (waterTem && ParseOptionalDecimal(waterRaw) is { } water)
            {
                leituras.Add(new PesoComparacaoLeituraInput(subject ?? string.Empty, water));
                _linhasDasLeituras.Add((echo.Count, subject, waterRaw));
            }
            else
            {
                // Nº de CM sem massa de água, ou massa presente mas ilegível:
                // a linha fica de fora (nenhum facto é inventado) e o aviso
                // diz qual — as outras leituras comparam-se.
                fora.Add(new ComparacaoLeituraForaAviso(
                    echo.Count,
                    subject,
                    waterRaw,
                    waterTem ? PesoComparacaoMotivoIgnorada.MassaIlegivel : PesoComparacaoMotivoIgnorada.SemMassa));
            }
        }

        ComparacaoLinhas = echo.Count > 0 ? echo : ComparacaoLinhas;
        ComparacaoLeiturasFora = fora;

        if (pesoInicialId is not { } inicialId || inicialId <= 0)
        {
            return (PesoComparacaoResultado.PesoInicialNaoSelecionado, null, 0m);
        }

        if (ParseOptionalDecimal(waterTemperature) is not { } temperatura)
        {
            // Vazia ou ilegível diz «ilegível»; fora de 5–35 °C é do backend.
            return (PesoComparacaoResultado.TemperaturaIlegivel, null, 0m);
        }

        if (leituras.Count == 0)
        {
            // Nenhuma leitura utilizável: nada a comparar. Com linhas
            // escritas mas ilegíveis o motivo é esse (o aviso diz quais).
            return (fora.Count > 0 ? PesoComparacaoResultado.MassaAguaIlegivel : PesoComparacaoResultado.SemLeituras, null, 0m);
        }

        return (null, leituras, temperatura);
    }

    /// <summary>O valor do parâmetro «aviso» quando o Peso foi criado sem o nominal ilegível.</summary>
    private const string AvisoNominalIlegivel = "nominal-ilegivel";

    /// <summary>O valor do parâmetro «aviso» quando o Peso foi criado sem o nominal ≤ 0.</summary>
    private const string AvisoNominalNaoPositivo = "nominal-nao-positivo";

    /// <summary>
    /// O peso nominal OPCIONAL (A5): legível e maior que 0 segue para o
    /// handler; escrito mas ilegível ou ≤ 0 fica de fora (null) e devolve o
    /// aviso a mostrar depois de criar — nunca recusa o Peso.
    /// </summary>
    private static (decimal? Nominal, string? Aviso) NominalOpcional(string? raw)
    {
        if (Sanitize(raw) is null)
        {
            return (null, null);
        }

        return ParseOptionalDecimal(raw) switch
        {
            null => (null, AvisoNominalIlegivel),
            <= 0m => (null, AvisoNominalNaoPositivo),
            { } nominal => (nominal, null)
        };
    }

    /// <summary>
    /// A linha do form (1-based, a ordem das linhas reapresentadas), o nº
    /// de CM e o texto escrito de cada leitura enviada ao backend, pela
    /// ordem do pedido — para o aviso apontar a linha certa quando o
    /// backend deixa uma de fora.
    /// </summary>
    private readonly List<(int Linha, string? CmSubject, string? Valor)> _linhasDasLeituras = new();

    /// <summary>Junta ao aviso as leituras que o backend deixou de fora (massa ≤ 0 ou resultado ≤ 0).</summary>
    private void JuntarLeiturasForaDoBackend(IReadOnlyList<PesoComparacaoLeituraIgnorada>? ignoradas)
    {
        if (ignoradas is not { Count: > 0 })
        {
            return;
        }

        var todas = ComparacaoLeiturasFora.ToList();
        foreach (var ignorada in ignoradas)
        {
            var origem = ignorada.Indice >= 1 && ignorada.Indice <= _linhasDasLeituras.Count
                ? _linhasDasLeituras[ignorada.Indice - 1]
                : (Linha: ignorada.Indice, CmSubject: ignorada.CmSubject, Valor: ignorada.WaterWeightGrams?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            todas.Add(new ComparacaoLeituraForaAviso(origem.Linha, origem.CmSubject, origem.Valor, ignorada.Motivo));
        }

        ComparacaoLeiturasFora = todas.OrderBy(a => a.Linha).ToList();
    }

    /// <summary>
    /// O aviso das leituras de fora atravessa o PRG do Guardar num só
    /// parâmetro: uma linha por leitura, «linha|motivo|valor|cm» (o motivo
    /// é do vocabulário fechado; o valor e o nº de CM são o que o operador
    /// escreveu, limitados e sem os separadores).
    /// </summary>
    private static string? EncodeLeiturasFora(IReadOnlyList<ComparacaoLeituraForaAviso> fora)
    {
        if (fora.Count == 0)
        {
            return null;
        }

        static string Limpo(string? valor)
        {
            var texto = (valor ?? string.Empty).Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ').Trim();
            return texto.Length > 40 ? texto[..40] : texto;
        }

        return string.Join('\n', fora.Take(50).Select(a => $"{a.Linha}|{a.Motivo}|{Limpo(a.Valor)}|{Limpo(a.CmSubject)}"));
    }

    private static IReadOnlyList<ComparacaoLeituraForaAviso> DecodeLeiturasFora(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return Array.Empty<ComparacaoLeituraForaAviso>();
        }

        var avisos = new List<ComparacaoLeituraForaAviso>();
        foreach (var linha in encoded.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(50))
        {
            var partes = linha.Split('|', 4);
            if (partes.Length == 4
                && int.TryParse(partes[0], out var numero)
                && Enum.TryParse(partes[1], out PesoComparacaoMotivoIgnorada motivo)
                && Enum.IsDefined(motivo))
            {
                avisos.Add(new ComparacaoLeituraForaAviso(numero, Sanitize(partes[3]), Sanitize(partes[2]), motivo));
            }
        }

        return avisos;
    }

    /// <summary>O filtro do tipo (o vocabulário fechado do select do histórico).</summary>
    private static ControloHistoryKind? ParseTipo(string? tipo) => tipo switch
    {
        "Peso" => ControloHistoryKind.Peso,
        "Pegamentos" => ControloHistoryKind.Pegamentos,
        "Comparacao" => ControloHistoryKind.Comparacao,
        "Resumo" => ControloHistoryKind.Resumo,
        _ => null
    };

    private static DateOnly? ParseData(string? data) =>
        DateOnly.TryParse(data, out var parsed) ? parsed : null;

    /// <summary>O alvo do «Abrir registo selecionado»: o tipo, o id e o controlo da produção (o roteamento é do backend).</summary>
    private static (ControloHistoryKind Kind, int Id, int? ControloId)? ParseAbrir(string? abrir)
    {
        var parts = Sanitize(abrir)?.Split(':');
        if (parts is not { Length: 3 }
            || !Enum.TryParse(parts[0], out ControloHistoryKind kind)
            || !int.TryParse(parts[1], out var id))
        {
            return null;
        }

        return (kind, id, int.TryParse(parts[2], out var controloId) ? controloId : null);
    }

    public async Task<IActionResult> OnPostPreviewAsync(
        [FromForm(Name = "peso_id")] int pesoId,
        [FromForm(Name = "lines")] string? linesJson)
    {
        var peso = await _pesoQuery.FindPesoAsync(pesoId, HttpContext.RequestAborted);
        if (peso is null)
        {
            return new JsonResult(new { refusal = RecordPesoMeasurementResult.PesoNotFound.ToString() }, PreviewJsonOptions);
        }

        // Amostras válidas = leituras persistidas (factos históricos) +
        // linhas em preparação válidas (preview apenas — nada é escrito).
        var samples = new List<(decimal Water, decimal Capacity, decimal Glass)>();
        foreach (var measurement in peso.Measurements)
        {
            samples.Add((measurement.WaterWeightGrams, measurement.CapacityCm3, measurement.GlassWeightGrams));
        }

        var persistedCount = samples.Count;
        var persistedTotals = BuildTotals(samples, peso.NominalWeightGrams);

        var drafts = ParseDraftLines(linesJson);
        var parsed = new List<(string? CmSubject, decimal Water)>();
        foreach (var draft in drafts)
        {
            if (ParseOptionalDecimal(draft.WaterWeight) is not { } water
                || water == 0m)
            {
                continue; // sem massa utilizável: sem facto a projetar (linhas inválidas ficam pending, sem bloquear)
            }

            parsed.Add((draft.CmSubject, water));
        }

        // Recusa tipada RESULT_NON_POSITIVE (baseline §8): qualquer resultado
        // não estritamente positivo invalida a linha — nada é exibido como
        // facto e a linha não entra nas médias (função antes de proteção:
        // as outras linhas continuam).
        var validLines = new List<(string? CmSubject, decimal Water, decimal Capacity, decimal Glass)>();
        foreach (var (cmSubject, water) in parsed)
        {
            var capacity = PesoCalculation.CapacityCm3(water, peso.WaterDensityGramsPerCm3);
            var glass = PesoCalculation.GlassWeightGrams(
                capacity, peso.VolumeMarisaCm3, peso.VolumePuncaoCm3, peso.GlassDensityGramsPerCm3);
            if (capacity <= 0m || glass <= 0m)
            {
                continue;
            }

            validLines.Add((cmSubject, water, capacity, glass));
            samples.Add((water, capacity, glass));
        }

        var totals = BuildTotals(samples, peso.NominalWeightGrams);

        var lines = new List<PesoPreviewLineReadModel>();
        var index = 0;
        foreach (var draft in drafts)
        {
            index++;
            if (ParseOptionalDecimal(draft.WaterWeight) is not { } water
                || water == 0m)
            {
                lines.Add(new PesoPreviewLineReadModel(
                    index, draft.CmSubject, "invalid_water_weight",
                    null, null, null, null, null));
                continue;
            }

            var capacity = PesoCalculation.CapacityCm3(water, peso.WaterDensityGramsPerCm3);
            var glass = PesoCalculation.GlassWeightGrams(
                capacity, peso.VolumeMarisaCm3, peso.VolumePuncaoCm3, peso.GlassDensityGramsPerCm3);
            if (capacity <= 0m || glass <= 0m)
            {
                lines.Add(new PesoPreviewLineReadModel(
                    index, draft.CmSubject, "result_non_positive",
                    water, capacity, glass, null, null));
                continue;
            }

            decimal? desvio = null;
            decimal? desvioPercent = null;
            if (totals.AverageCapacityCm3 is { } averageCapacity)
            {
                desvio = capacity - averageCapacity;
                desvioPercent = averageCapacity != 0m ? desvio / averageCapacity * 100m : null;
            }
            lines.Add(new PesoPreviewLineReadModel(
                index, draft.CmSubject, "ok", water, capacity, glass, desvio, desvioPercent));
        }

        return new JsonResult(new PesoPreviewReadModel(
            peso.PesoId,
            new PesoPreviewContextReadModel(
                peso.Process,
                peso.WaterTemperatureC,
                peso.WaterDensityGramsPerCm3,
                peso.GlassDensityGramsPerCm3,
                peso.VolumeMarisaCm3,
                peso.VolumePuncaoCm3,
                peso.NominalWeightGrams),
            lines,
            totals,
            persistedTotals), PreviewJsonOptions);
    }

    private static PesoPreviewTotalsReadModel BuildTotals(
        IReadOnlyList<(decimal Water, decimal Capacity, decimal Glass)> samples,
        decimal? nominalWeightGrams)
    {
        if (samples.Count == 0)
        {
            return new PesoPreviewTotalsReadModel(0, null, null, null, null);
        }

        var averageWater = samples.Average(s => s.Water);
        var averageCapacity = samples.Average(s => s.Capacity);
        var averageGlass = samples.Average(s => s.Glass);
        var desvioNominal = nominalWeightGrams is { } nominal ? averageGlass - nominal : (decimal?)null;
        return new PesoPreviewTotalsReadModel(samples.Count, averageWater, averageCapacity, averageGlass, desvioNominal);
    }

    private static IReadOnlyList<(string? CmSubject, string WaterWeight)> ParseDraftLines(string? linesJson)
    {
        if (string.IsNullOrWhiteSpace(linesJson))
        {
            return Array.Empty<(string?, string)>();
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(linesJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return Array.Empty<(string?, string)>();
            }

            var result = new List<(string?, string)>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var cmSubject = item.TryGetProperty("cm_subject", out var cm) && cm.ValueKind == System.Text.Json.JsonValueKind.String
                    ? cm.GetString()
                    : null;
                var water = item.TryGetProperty("water_weight", out var weight) && weight.ValueKind == System.Text.Json.JsonValueKind.String
                    ? weight.GetString()
                    : null;
                result.Add((string.IsNullOrWhiteSpace(cmSubject) ? null : cmSubject!.Trim(), water ?? string.Empty));
            }

            return result;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<(string?, string)>(); // request malformado: sem linhas a projetar (nunca um facto inventado)
        }
    }

    private RedirectToPageResult CreateRefused(
        string? cmToolId,
        string? bqToolId,
        CreateToolScopedPesoResult result,
        string? waterTemperature = null,
        string? cmProcess = null,
        string? cmVolumePuncao = null,
        string? cmNominalWeight = null,
        string? bqVolumeMarisa = null) =>
        RedirectToPage("/Controlo/Peso", new
        {
            cm_tool_id = cmToolId,
            bq_tool_id = bqToolId,
            create_refusal = result.ToString(),
            // A AUDITORIA DO CODEX (achado 2): a recusa leva os VALORES
            // submetidos de volta (o form volta com o que o operador
            // escreveu, nunca vazio).
            echo_temperature = waterTemperature,
            echo_process = cmProcess,
            echo_puncao = cmVolumePuncao,
            echo_nominal = cmNominalWeight,
            echo_marisa = bqVolumeMarisa
        });

    private static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// O parse decimal FLEXÍVEL (o stresstest 2026-10-12: a oficina escreve
    /// VÍRGULA decimal («20,4» — o placeholder promete!): ponto ou vírgula,
    /// sem interpretar separadores como agrupamento de milhares. Nulo quando
    /// vazio ou ilegível.
    /// </summary>
    private static decimal? ParseOptionalDecimal(string? value) => MeasurementDecimal.Parse(value);
}

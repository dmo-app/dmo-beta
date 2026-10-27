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
    private readonly IProductionContextQuery _productionContext;
    private readonly PesoPdfDocumentBuilder _pdfDocument;
    private readonly IModuleSettingsQuery _moduleSettings;
    private readonly IPesoEscolhasQuery _escolhas;
    private readonly IPesoComparacaoEstadoQuery _comparacaoEstado;
    private readonly IAssociatePesoToControloHandler _associatePeso;
    private readonly IPesoAssociationQuery _pesoAssociation;
    private readonly IEnsureControloForJobOnHandler _ensureControlo;
    private readonly IJobOnListQuery _jobOnList;
    private readonly IControloHistoricoProcura _historicoProcura;
    private readonly IPegamentosEscolhasQuery _lotes;

    public PesoModel(
        IToolLookupQuery toolLookup,
        IToolTechnicalQuery toolTechnical,
        IGlassDensityConfiguration glassDensityConfiguration,
        ICreateToolScopedPesoHandler createPeso,
        IRecordPesoMeasurementHandler recordMeasurement,
        IPesoQuery pesoQuery,
        ShellStateFactory shellState,
        ICreatePesoForControloHandler createPesoForControlo,
        IProductionContextQuery productionContext,
        PesoPdfDocumentBuilder pdfDocument,
        IModuleSettingsQuery moduleSettings,
        IPesoEscolhasQuery escolhas,
        IPesoComparacaoEstadoQuery comparacaoEstado,
        IAssociatePesoToControloHandler associatePeso,
        IPesoAssociationQuery pesoAssociation,
        IEnsureControloForJobOnHandler ensureControlo,
        IJobOnListQuery jobOnList,
        IPesoCapSupportQuery capSupportQuery,
        IPesoFolhaQuery folhaQuery,
        JobOnChangeQuery changeQuery,
        PesoComparisonQuery comparacaoQuery,
        PesoProducaoAnteriorQuery producaoAnteriorQuery,
        ICreatePesoComparacaoHandler comparacaoHandler,
        IControloHistoricoProcura historicoProcura,
        DMO.Alpha.Core.Tools.IFerramentasFabricoSap fabricoSap,
        IPegamentosEscolhasQuery lotes)
    {
        _lotes = lotes;
        _fabricoSap = fabricoSap;
        _jobOnList = jobOnList;
        CapSupportQuery = capSupportQuery;
        FolhaQuery = folhaQuery;
        ChangeQuery = changeQuery;
        ComparacaoQuery = comparacaoQuery;
        ProducaoAnteriorQuery = producaoAnteriorQuery;
        ComparacaoHandler = comparacaoHandler;
        _historicoProcura = historicoProcura;
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
        _productionContext = productionContext;
        _pdfDocument = pdfDocument;
        _moduleSettings = moduleSettings;
        _escolhas = escolhas;
        _comparacaoEstado = comparacaoEstado;
    }

    /// <summary>A query do apoio TP/calote (a mesma fonte do cálculo da ficha do Job On).</summary>
    private IPesoCapSupportQuery CapSupportQuery { get; }

    /// <summary>The initial production sheet, composed without writes.</summary>
    private IPesoFolhaQuery FolhaQuery { get; }

    /// <summary>As alterações do Job On, para o aviso no Peso aberto.</summary>
    private JobOnChangeQuery ChangeQuery { get; }

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

    /// <summary>A query das comparações do Peso (a Comparação_B + as comparações todas — o mesmo serviço).</summary>
    private PesoComparisonQuery ComparacaoQuery { get; }

    /// <summary>A produção anterior do Peso (o Owner 2026-10-17): Job On e peso do SAP.</summary>
    private PesoProducaoAnteriorQuery ProducaoAnteriorQuery { get; }

    private readonly DMO.Alpha.Core.Tools.IFerramentasFabricoSap _fabricoSap;

    /// <summary>
    /// O peso do SAP de cada produção anterior que se pode escolher (Diogo,
    /// 21/10/2026): a média pelos dias e a moda das ordens do relatório de
    /// fabrico com a mesma referência, máquina e datas.
    /// </summary>
    public IReadOnlyDictionary<int, DMO.Alpha.Core.Tools.PesoSapResumo> ProducaoAnteriorSap { get; private set; } = new Dictionary<int, DMO.Alpha.Core.Tools.PesoSapResumo>();

    /// <summary>O handler de criação do registo de comparação (o cálculo vem da fonte única acima).</summary>
    private ICreatePesoComparacaoHandler ComparacaoHandler { get; }

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
        AssociatePesoToControloResult.CmMismatch => "A CM deste Peso não é a CM do Job On, nem uma que a produção teve antes. Nenhum Peso foi associado.",
        AssociatePesoToControloResult.ContextMissingBq => "O Job On desta produção não tem boquilha (BQ). Nenhum Peso foi associado.",
        AssociatePesoToControloResult.BqMismatch => "A boquilha (BQ) deste Peso não é a do Job On, nem uma que a produção teve antes. Nenhum Peso foi associado.",
        AssociatePesoToControloResult.PesoAlreadyAssociated => "Este Peso já pertence a outra produção. Nada foi alterado.",
        AssociatePesoToControloResult.ControloAlreadyHasPeso => "Esta produção já tem um Peso com esta CM e esta boquilha (BQ). Nada foi alterado.",
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

    /// <summary>Os lotes da CM escrita quando são vários: o operador escolhe o lote (como nos Pegamentos).</summary>
    public IReadOnlyList<ToolLookupResult> CmLotes { get; private set; } = Array.Empty<ToolLookupResult>();

    /// <summary>
    /// Os lotes da BQ escrita, só quando é preciso escolher: a marisa é a
    /// mesma em todos os lotes da BQ (Diogo, 27/10/2026: «o vol da BQ é sempre
    /// igual, independentemente se é lote 6 ou 59»), por isso o «BQ — lote»
    /// só aparece quando os lotes têm marisas diferentes.
    /// </summary>
    public IReadOnlyList<ToolLookupResult> BqLotes { get; private set; } = Array.Empty<ToolLookupResult>();

    /// <summary>True quando os lotes da BQ escrita têm marisas diferentes e o lote tem de ser escolhido.</summary>
    public bool BqMarisasDiferentes { get; private set; }

    /// <summary>O lote escolhido da CM (cm_lote); null sem escolha.</summary>
    public string? ChosenCmLote { get; private set; }

    /// <summary>O lote escolhido da BQ (bq_lote); null sem escolha.</summary>
    public string? ChosenBqLote { get; private set; }

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
            "A densidade do vidro do processo não está configurada: guarde-a nas Definições do Controlo. " +
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
        CreateToolScopedPesoResult.ComparacaoPesoNotFound =>
            "O Peso a comparar já não existe ou não é de uma produção. Nenhum Peso foi criado.",
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
        RecordPesoMeasurementResult.PesoNotCurrent =>
            "Este Peso é de uma CM ou de uma boquilha (BQ) que já não está na produção. A leitura não foi gravada. " +
            "As leituras novas vão para o Peso das ferramentas atuais.",
        RecordPesoMeasurementResult.OperationConflict =>
            "Este envio não corresponde ao que foi gravado antes com a mesma identificação. Nenhuma leitura foi registada; escreva a leitura outra vez.",
        _ => string.Empty
    };

    // ---------------------------------------------------------------
    // VÁRIOS PESOS NA PRODUÇÃO (18/10/2026; respostas R15, R16-2, R16-3 e
    // R16-5 do especialista): depois de trocar a CM ou a BQ a meio da
    // produção, as ferramentas novas têm um Peso novo; o anterior fica como
    // estava, sem leituras novas. Com um só Peso a folha fica como sempre.
    // ---------------------------------------------------------------

    /// <summary>Os Pesos da produção, pela ordem de gravação (vazia sem produção).</summary>
    public IReadOnlyList<PesoReadModel> Pesos { get; private set; } = Array.Empty<PesoReadModel>();

    /// <summary>O controlo dos Pesos listados (o da folha ou o do Peso aberto).</summary>
    public int? PesosControloId { get; private set; }

    /// <summary>
    /// True quando a produção tem outro Peso além do mostrado: o cartão
    /// «Pesos desta produção» lista-os. Com um só, a folha fica como sempre.
    /// </summary>
    public bool ShowPesos =>
        PesosControloId is not null && Pesos.Any(p => p.PesoId != Peso?.PesoId);

    /// <summary>
    /// True quando o Peso mostrado é de antes de uma troca da CM ou da BQ:
    /// mostra-se sem a leitura nova e com «Abrir o Peso atual».
    /// </summary>
    public bool PesoAnterior => Peso is { IsCurrent: false, ControloId: not null };

    /// <summary>
    /// O Peso das ferramentas que a produção tem agora, quando já existe: o
    /// «Abrir o Peso atual» de um Peso anterior leva a ele.
    /// </summary>
    public PesoReadModel? PesoAtual => Pesos.FirstOrDefault(p => p.IsCurrent);

    /// <summary>
    /// O peso_id que a folha da produção leva nos endereços e nos
    /// formulários: só o de um Peso anterior (o atual abre pelo controlo_id,
    /// como sempre).
    /// </summary>
    public int? PesoIdNoEndereco => PesoAnterior && ControloIdFromQuery is not null ? Peso!.PesoId : null;

    /// <summary>
    /// O endereço de um Peso da produção: o atual abre pelo controlo_id, como
    /// sempre; um de antes de uma troca, pelo controlo_id e o peso_id dele.
    /// </summary>
    public static string PesoHref(int controloId, PesoReadModel peso) =>
        peso.IsCurrent
            ? $"/Controlo/Peso?controlo_id={controloId}"
            : $"/Controlo/Peso?controlo_id={controloId}&peso_id={peso.PesoId}";

    /// <summary>O número do Peso na produção (1, 2, …), pela ordem de gravação; null fora da lista.</summary>
    public int? PesoNumber(PesoReadModel? peso)
    {
        var index = peso is null ? -1 : Pesos.ToList().FindIndex(p => p.PesoId == peso.PesoId);
        return index < 0 ? null : index + 1;
    }

    /// <summary>
    /// «A CM mudou», «A BQ mudou» ou «A CM e a BQ mudaram»: o que difere entre
    /// o último Peso da produção e as ferramentas que ela tem agora; null
    /// quando a produção ainda não tem Peso.
    /// </summary>
    public string? FerramentasMudaramTexto
    {
        get
        {
            if (Pesos.Count == 0 || ProductionContext is not { } contexto)
            {
                return null;
            }

            var ultimo = Pesos[^1];
            var cm = contexto.CmToolId is { } cmAtual && cmAtual != ultimo.CmToolId;
            var bq = contexto.BqToolId is { } bqAtual && bqAtual != ultimo.BqToolId;
            return (cm, bq) switch
            {
                (true, true) => "A CM e a BQ mudaram",
                (true, false) => "A CM mudou",
                (false, true) => "A BQ mudou",
                _ => "As ferramentas mudaram"
            };
        }
    }

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
            "A densidade do vidro do processo não está configurada: guarde-a nas Definições do Controlo. Nenhum valor foi inventado.",
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

    /// <summary>
    /// Os candidatos da Comparação_B: os Pesos das produções da mesma
    /// referência anteriores a esta, a mais recente primeiro, no máximo 5, e
    /// o pedido no fim quando fica de fora deles (deep sweep 2, F-12); a
    /// escolha é tua.
    /// </summary>
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

    /// <summary>
    /// A calote suposta que os formulários da folha levam de volta (registar
    /// uma leitura, guardar a produção anterior, escolher a comparação,
    /// «Calcular»), para o «Supomos» aberto não desaparecer. Um valor ilegível
    /// não segue: a recusa já foi mostrada.
    /// </summary>
    public string? SuporCaloteParaLevar => SuposicaoIlegivel ? null : SuporCaloteTexto;

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

    /// <summary>True quando, com a massa escrita, o peso do vidro dá 0 ou menos (fisicamente impossível).</summary>
    public bool LeituraVidroNaoPositivo { get; private set; }

    /// <summary>Mensagem da recusa da leitura, apenas apresentação.</summary>
    public string LeituraRefusalMessage => LeituraIlegivel
        ? "A massa de água escrita é ilegível — escreva um número, por exemplo 199,64 ou 199.64. Nenhuma leitura foi registada; o form preserva o que escreveu."
        : LeituraVidroNaoPositivo
            ? "Com esta massa de água o peso do vidro dá 0 ou menos, o que é impossível. Confirme a massa (por exemplo 199,64 ou 199.64). Nenhuma leitura foi registada; o form preserva o que escreveu."
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
    /// A PRODUÇÃO FIXA (Diogo, 27/10/2026: «é sempre comparado à mesma
    /// produção que saiu»): aberta pelo Resumo (?controlo_id=N&amp;comparacao=1),
    /// a comparação fica na produção desse controlo, sem os campos da
    /// referência e da produção; só se escolhe o Peso inicial, quando há
    /// mais de um. Segue no formulário e no PRG como producao_fixa=1.
    /// </summary>
    public bool ProducaoFixa { get; private set; }

    /// <summary>O nome da produção fixa («1075C142 · 202601 · B1»).</summary>
    public string? ProducaoFixaRotulo => Producoes.FirstOrDefault(p => p.JobOnId == ProducaoSelecionadaJobOnId)?.Label;

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

    /// <summary>O filtro da ferramenta (a referência da CM, pelo início dela ou da parte BQ).</summary>
    public string? HistoryTool { get; private set; }

    /// <summary>O lote escrito no Histórico (Diogo, 27/10/2026: «falta lote»): com a referência, por exemplo ST100 lote 8.</summary>
    public string? HistoryLote { get; private set; }

    /// <summary>O filtro do tipo (Peso/Pegamentos/Comparação; null = todos).</summary>
    public ControloHistoryKind? HistoryTipo { get; private set; }

    /// <summary>O filtro «Sem Job On associado»: só os Pesos e Pegamentos ainda por associar.</summary>
    public bool HistorySemJobOn { get; private set; }

    /// <summary>
    /// As produções das referências procuradas (o dropdown do segundo passo),
    /// pela regra da tabela, cada uma com a sua referência: o mesmo nº pode
    /// existir em duas referências.
    /// </summary>
    public IReadOnlyList<JobOnProductionOption> HistoryProducoes { get; private set; } = Array.Empty<JobOnProductionOption>();

    /// <summary>A referência da produção escolhida no dropdown (o nº só é único dentro da referência).</summary>
    public string? HistoryProductionReference { get; private set; }

    /// <summary>
    /// Entre o nº e a referência no valor de uma opção do dropdown
    /// («2026001|5447T173»).
    /// </summary>
    public const char HistoryProducaoSeparador = ControloHistoricoValores.ProducaoSeparador;

    /// <summary>True quando o dropdown das produções junta mais do que uma referência (o texto é o início de várias).</summary>
    public bool HistoryProducoesVariasReferencias =>
        HistoryProducoes.Select(p => p.Reference).Distinct(StringComparer.Ordinal).Skip(1).Any();

    /// <summary>
    /// O valor de uma opção do dropdown: sempre o nº e a referência, para a
    /// escolha filtrar só essa produção (deep sweep 2, F-6: o mesmo nº noutra
    /// referência não entra). Também com uma só referência no dropdown: a
    /// escolha vai nos pedidos seguintes, e com o texto da referência
    /// alargado («1075C142» → «1075») um valor só com o nº passava para a
    /// mais recente com esse nº noutra referência.
    /// </summary>
    public string HistoryProducaoValor(JobOnProductionOption opcao) =>
        $"{opcao.ProductionNumber}{HistoryProducaoSeparador}{opcao.Reference}";

    /// <summary>A opção escolhida: o nº e a referência da produção que filtra.</summary>
    public bool HistoryProducaoEscolhida(JobOnProductionOption opcao) =>
        opcao.ProductionNumber == HistoryProduction && opcao.Reference == HistoryProductionReference;

    /// <summary>O filtro da data (o dia de Lisboa, pela data gravada de cada registo).</summary>
    public DateOnly? HistoryData { get; private set; }

    /// <summary>True quando há pelo menos um filtro do histórico; sem filtros a lista não é lida.</summary>
    public bool HistoricoComProcura => _historico?.ComProcura ?? false;

    private ControloHistoricoResultado? _historico;

    /// <summary>Os resultados da procura unificada do histórico (null sem procura).</summary>
    public IReadOnlyList<ControloHistorySearchItem>? HistoryResults { get; private set; }

    /// <summary>A paginação do histórico (25 por página, feita na query).</summary>
    public ListPager? HistoryPager { get; private set; }

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

    /// <summary>
    /// As últimas 5 produções da mesma referência anteriores a esta, as mais
    /// recentes primeiro, e a guardada quando fica de fora delas.
    /// </summary>
    public IReadOnlyList<PesoProducaoAnteriorOpcao> ProducaoAnteriorOpcoes { get; private set; } = Array.Empty<PesoProducaoAnteriorOpcao>();

    /// <summary>
    /// O Job On mostrado como produção anterior: o escolhido no select, o
    /// guardado, ou a anterior mais recente da mesma referência.
    /// </summary>
    public PesoProducaoAnteriorOpcao? ProducaoAnteriorEscolhida { get; private set; }

    /// <summary>A recusa ao guardar a produção anterior (PRG); null sem recusa.</summary>
    public string? ProducaoAnteriorRefusal { get; private set; }

    /// <summary>O peso do SAP ilegível, devolvido ao campo depois da recusa.</summary>
    public string? ProducaoAnteriorEcho { get; private set; }

    /// <summary>O aviso de que a produção anterior foi guardada (PRG).</summary>
    public bool ProducaoAnteriorGuardada { get; private set; }

    /// <summary>
    /// O Peso (de uma produção) para o qual o Novo Peso é criado só para
    /// comparação (?comparacao_de; o Diogo, 22/10/2026); null num Novo Peso normal.
    /// </summary>
    public int? ComparacaoDePesoId { get; private set; }

    /// <summary>
    /// Num Peso só para comparação, o endereço do Peso que ele compara, com a
    /// comparação escolhida; null nos outros.
    /// </summary>
    public string? ComparacaoVoltarUrl { get; private set; }

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
        [FromQuery(Name = "producao_fixa")] string? producaoFixa,
        [FromQuery(Name = "comparacao_id")] int? comparacaoId,
        [FromQuery(Name = "comparacao_refusal")] string? comparacaoRefusal,
        [FromQuery(Name = "ferramenta")] string? ferramenta,
        [FromQuery(Name = "lote")] string? lote,
        [FromQuery(Name = "tipo")] string? tipo,
        [FromQuery(Name = "data")] string? data,
        [FromQuery(Name = "pagina")] int? pagina,
        [FromQuery(Name = "abrir")] string? abrir,
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
        [FromQuery(Name = "modo")] string? modo = null,
        [FromQuery(Name = "jobon_refusal")] string? jobOnRefusal = null,
        [FromQuery(Name = "maquina")] string? maquina = null,
        [FromQuery(Name = "sem_jobon")] string? semJobOnFiltro = null,
        [FromQuery(Name = "anterior")] int? anteriorJobOnId = null,
        [FromQuery(Name = "anterior_recusa")] string? anteriorRecusa = null,
        [FromQuery(Name = "anterior_peso")] string? anteriorPeso = null,
        [FromQuery(Name = "anterior_ok")] string? anteriorOk = null,
        [FromQuery(Name = "comparacao_de")] int? comparacaoDe = null)
    {
        ViewData["Title"] = "Peso — Controlo";

        // Só o jobon_id (o «Verificar o Peso» de «O que mudou» no Job On,
        // 18/10/2026): a folha da produção abre pelo controlo_id dela, como
        // no AbrirJobOn. Sem controlo ainda, o Resumo da produção, que o
        // assegura e propõe o Peso. Antes caía no Novo Peso sem Job On.
        if (jobOnId is int soJobOnId && controloId is null && pesoId is null
            && modo is null && historico != "1" && comparacao != "1")
        {
            var controloDaProducao = await _escolhas.ControloDaProducaoAsync(soJobOnId, HttpContext.RequestAborted);
            return controloDaProducao is int abrirControloId
                ? RedirectToPage("/Controlo/Peso", new { controlo_id = abrirControloId })
                : RedirectToPage("/Controlo/Resumo", new { jobon_id = soJobOnId });
        }

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
            ActiveFunctionKey = historico == "1" ? "historico" : comparacao == "1" ? "resumo" : "peso",
            Functions = ShellStateFactory.ControloFunctions(
                historico == "1" ? "historico" : comparacao == "1" ? "resumo" : "peso",
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
                    // Cada linha é uma folha: abre essa folha (18/10/2026).
                    ControloHistoryKind.Pegamentos => abrirControloId is int pegamentosControlo
                        ? RedirectToPage("/Controlo/Pegamentos", new { controlo_id = pegamentosControlo, pegamentos_id = abrirId })
                        : RedirectToPage("/Controlo/Pegamentos", new { pegamentos_id = abrirId }),
                    ControloHistoryKind.Resumo => RedirectToPage("/Controlo/Resumo", new { jobon_id = abrirId }),
                    _ => RedirectToPage("/Controlo/Peso", new { historico = "1", comparacao_id = abrirId })
                };
            }

            // A procura do histórico (os filtros, o dropdown das produções e
            // a página de resultados) é da IControloHistoricoProcura.
            var procura = await _historicoProcura.ProcurarAsync(
                new ControloHistoricoPedido(
                    Sanitize(referencia), Sanitize(producao), Sanitize(ferramenta), Sanitize(lote),
                    tipo, data, semJobOnFiltro == "1", pagina),
                HttpContext.RequestAborted);
            _historico = procura;
            HistoryReference = procura.Referencia;
            HistoryProduction = procura.Producao;
            HistoryProductionReference = procura.ProducaoReferencia;
            HistoryTool = procura.Ferramenta;
            HistoryLote = procura.Lote;
            HistoryTipo = procura.Tipo;
            HistoryData = procura.Data;
            HistorySemJobOn = procura.SemJobOn;
            HistoryProducoes = procura.Producoes;
            if (procura.Pagina is { } historyPage)
            {
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
        LeituraVidroNaoPositivo = leituraRefusal == "vidro";
        LeituraRefusal = leituraRefusal == "massa" || LeituraIlegivel || LeituraVidroNaoPositivo;
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
                producaoPedida = await _escolhas.ProducaoDoControloAsync(controloDaComparacao, HttpContext.RequestAborted);
            }

            // Pelo Resumo a produção fica fixa: a referência escrita não conta.
            ProducaoFixa = producaoPedida is not null && (controloId is not null || producaoFixa == "1");

            // Dois passos (16/10/2026): a referência primeiro; o select só
            // traz as produções dela. Sem referência escrita, vale a da
            // produção pedida; sem nenhuma das duas, o select fica vazio.
            await CarregarProducoesDaReferenciaAsync(
                ProducaoFixa ? null : Sanitize(referencia), producaoPedida, HttpContext.RequestAborted);
            ProducaoFixa &= ProducaoSelecionadaJobOnId is not null;

            if (ProducaoSelecionadaJobOnId is int producaoEscolhida)
            {
                Iniciais = await ComparacaoQuery.ListarIniciaisAsync(
                    producaoEscolhida, HttpContext.RequestAborted);

                // O peso inicial: o selecionado (se é desta produção) ou o
                // PRIMEIRO da produção (o html auto-seleciona o primeiro — o
                // peso inicial é o mais antigo da produção). Com vários Pesos
                // (a CM ou a BQ mudou a meio; 18/10/2026), o das ferramentas
                // atuais, senão o mais recente.
                PesoInicialSelecionado = pesoInicialId is int pedido && Iniciais.Any(i => i.PesoId == pedido)
                    ? pedido
                    : (Iniciais.FirstOrDefault(i => i.IsCurrent) ?? Iniciais.LastOrDefault())?.PesoId;
            }

            // O contra-molde: o selecionado ou o CM do peso inicial (o
            // fallback do html).
            ContraMoldeSelecionado = Sanitize(cmToolId)
                ?? Iniciais.FirstOrDefault(i => i.PesoId == PesoInicialSelecionado)?.CmToolId;

            // As linhas por omissão: UMA leitura; «+», «−» e o «×» da linha
            // acrescentam e tiram (as em branco são ignoradas pelo backend).
            if (ComparacaoLinhas.Count == 0)
            {
                ComparacaoLinhas = new List<(string?, string?)> { (null, null) };
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

            // A Comparação vive no Resumo da produção (Diogo, 21/10/2026):
            // sem separador próprio, o Resumo fica ativo e com o Job On.
            if (ViewData["Shell"] is ShellPresentation shell)
            {
                ViewData["Shell"] = shell with
                {
                    Functions = ShellStateFactory.ControloFunctions(
                        "resumo", ProducaoSelecionadaJobOnId, controloId)
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
            var folha = await FolhaQuery.CarregarAsync(
                canonicalControloId, pesoId, capSimulada, HttpContext.RequestAborted);
            ProductionContext = folha.ProductionContext;
            Pesos = folha.Pesos;
            PesosControloId = canonicalControloId;
            Peso = folha.Peso;
            CmTool = folha.ToolFacts?.CmTool;
            CmTechnical = folha.ToolFacts?.CmTechnical;
            ConfiguredGlassDensity = folha.ToolFacts?.CmGlassDensity;
            BqTool = folha.ToolFacts?.BqTool;
            BqTechnical = folha.ToolFacts?.BqTechnical;
            MfTool = folha.ToolFacts?.MfTool;
            CapSupport = folha.CapSupport;
            AssociationCandidates = folha.AssociationCandidates;

            if (Peso is not null)
            {
                // As produções a comparar vêm da ficha da CM do Peso aberto
                // (a de antes da troca num Peso anterior); o «comparar» pedido
                // fica no dropdown mesmo fora das 5 anteriores, mais antigo ou
                // posterior (deep sweep 2, F-12): a tabela abaixo é dele.
                ComparisonCandidates = await ComparacaoQuery.FindCandidatesAsync(
                    Peso, compararPesoId, HttpContext.RequestAborted);

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
                // Sem escolha, o Peso criado só para comparar com este (o
                // Diogo, 22/10/2026): foi feito para isso, e a comparação
                // aparece logo.
                var effectiveComparisonId = compararPesoId
                    ?? ComparisonCandidates
                        .Where(c => c.ProductionNumber == PesoComparisonQuery.RotuloParaComparacao)
                        .Select(c => (int?)c.PesoId)
                        .FirstOrDefault();

                if (effectiveComparisonId is int historicalPesoId && historicalPesoId != Peso.PesoId)
                {
                    Comparison = await ComparacaoQuery.CompareAsync(
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
                if (Peso.ControloId is null && Peso.ComparacaoDoPesoId is null)
                {
                    ProductionCandidates = await _pesoAssociation.FindProductionsForPesoAsync(
                        canonicalPesoId, HttpContext.RequestAborted);
                }

                // Um Peso só para comparação volta ao Peso que compara, com
                // ele escolhido na comparação (o Diogo, 22/10/2026).
                if (Peso.ComparacaoDoPesoId is int comparado
                    && await _pesoQuery.FindPesoAsync(comparado, HttpContext.RequestAborted) is { ControloId: int controloComparado } pesoComparado)
                {
                    ComparacaoVoltarUrl = Url.Page("/Controlo/Peso", new
                    {
                        controlo_id = controloComparado,
                        peso_id = pesoComparado.IsCurrent ? (int?)null : pesoComparado.PesoId,
                        comparar = Peso.PesoId
                    });
                }

                // Abrir por peso_id (histórico/PRG) não perde a produção
                // desse registo. Resolver apresentação/links: não associar
                // Pesos tool-scoped nem substituir o Peso selecionado.
                if (Peso.ControloId is int pesosDoControlo)
                {
                    // Os outros Pesos da produção (a CM ou a BQ mudou a meio).
                    Pesos = await _pesoQuery.ListByControloAsync(pesosDoControlo, HttpContext.RequestAborted);
                    PesosControloId = pesosDoControlo;
                }

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
                // filtrada pela referência do Peso e só com as anteriores à
                // dele (deep sweep 2, F-12: um Peso antigo aberto pelo
                // histórico já não oferece as posteriores); o resultado só
                // aparece quando uma produção é escolhida no dropdown.
                if (Peso.ControloId is int controloDoPeso && Peso.Averages is not null)
                {
                    ComparisonCandidates = await ComparacaoQuery.FindCandidatesAsync(
                        Peso, compararPesoId, HttpContext.RequestAborted);
                    if (compararPesoId is int escolhido
                        && escolhido != Peso.PesoId
                        && ComparisonCandidates.Any(c => c.PesoId == escolhido))
                    {
                        Comparison = await ComparacaoQuery.CompareAsync(
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
            Maquinas = await _escolhas.CodigosDeMaquinasAsync(HttpContext.RequestAborted);
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
        ComparacaoDePesoId = comparacaoDe;

        if (!string.IsNullOrWhiteSpace(cmToolId))
        {
            ChosenCmLote = Sanitize(Request.Query["cm_lote"].ToString());
            (CmTool, CmLotes) = await ResolveSlotAsync(cmToolId, ToolType.Cm, ChosenCmLote, HttpContext.RequestAborted);
            CmToolRequestedNotFound = CmTool is null && CmLotes.Count <= 1;
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
            ChosenBqLote = Sanitize(Request.Query["bq_lote"].ToString());
            (BqTool, BqLotes) = await ResolveSlotAsync(bqToolId, ToolType.Bq, ChosenBqLote, HttpContext.RequestAborted);
            BqToolRequestedNotFound = BqTool is null && BqLotes.Count <= 1;
            if (BqTool is null && BqLotes.Count > 1)
            {
                // Sem lote escolhido: com a mesma marisa em todos os lotes,
                // vale o primeiro que a tem (ou o primeiro) e o campo sai.
                var marisas = new List<(ToolLookupResult Lote, decimal? Marisa)>();
                foreach (var bqLote in BqLotes)
                {
                    var tecnico = await _toolTechnical.FindByToolIdAsync(bqLote.ToolId, HttpContext.RequestAborted);
                    marisas.Add((bqLote, tecnico?.VolumeMarisaCm3));
                }
                BqMarisasDiferentes = marisas.Where(m => m.Marisa is not null).Select(m => m.Marisa).Distinct().Count() > 1;
                if (!BqMarisasDiferentes)
                {
                    BqTool = marisas.FirstOrDefault(m => m.Marisa is not null).Lote ?? BqLotes[0];
                    BqLotes = Array.Empty<ToolLookupResult>();
                }
            }
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
    /// <summary>
    /// O slot da CM ou da BQ com vários lotes (Diogo, 26/10/2026: «ao criar
    /// comparação não me deixa escolher o lote»): sem lote escolhido não há
    /// ferramenta e a página mostra a escolha; com um só lote, ou sem a
    /// referência nos lotes, segue a resolução de sempre.
    /// </summary>
    private async Task<(ToolLookupResult? Tool, IReadOnlyList<ToolLookupResult> Lotes)> ResolveSlotAsync(
        string requested,
        ToolType expectedType,
        string? chosenToolId,
        CancellationToken cancellationToken)
    {
        var lotes = await _lotes.LotesDaReferenciaAsync(requested.Trim(), expectedType, cancellationToken);
        if (lotes.Count > 1)
        {
            return (lotes.FirstOrDefault(t => t.ToolId == chosenToolId), lotes);
        }

        return (await ResolveToolAsync(requested, expectedType, cancellationToken), Array.Empty<ToolLookupResult>());
    }

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
        [FromForm(Name = "bq_volume_marisa")] string? bqVolumeMarisa,
        [FromForm(Name = "comparacao_de")] int? comparacaoDe = null)
    {
        ComparacaoDePesoId = comparacaoDe;
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
                parsedMarisa,
                ComparacaoDoPesoId: comparacaoDe),
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

        // Um Peso das ferramentas de antes de uma troca (R16-6) abre pelo
        // peso_id dele; o das ferramentas atuais, pelo controlo_id.
        var atual = await _pesoQuery.FindCurrentByControloAsync(resolvedControloId, HttpContext.RequestAborted);
        return atual?.PesoId == pesoId
            ? RedirectToPage("/Controlo/Peso", new { controlo_id = resolvedControloId, associado = "1" })
            : RedirectToPage("/Controlo/Peso", new { controlo_id = resolvedControloId, peso_id = pesoId, associado = "1" });
    }

    /// <summary>
    /// POST nativo do Novo Peso associado a um Job On: o jobon_id escolhido
    /// resolve o seu controlo_id (get-or-create idempotente, o mesmo do
    /// Associate) e a folha abre no modo da produção, onde CM, BQ e calote
    /// vêm do contexto do Job On. Nenhum Peso é criado aqui.
    /// </summary>
    public async Task<IActionResult> OnPostAbrirJobOnAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "anterior")] int? producaoAnterior = null)
    {
        // Na Comparação, o «Abrir o Peso desta produção» leva a produção anterior escolhida.
        jobOnId ??= producaoAnterior;
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
        // QUEM GERA O PAPEL (o rodapé do alvo: «Diogo Oliveira
        // 28/07/2026, 14:52:28») — o nome humano da sessão.
        var generatedBy = User.FindFirst(DmoClaimTypes.DisplayName)?.Value
            ?? User.FindFirst(DmoClaimTypes.Email)?.Value;
        var documento = await _pdfDocument.BuildAsync(
            controloId, pesoId, compararPesoId, pesoAnteriorId, generatedBy, HttpContext.RequestAborted);
        if (documento is null)
        {
            return controloId is int semPesoControloId
                ? RedirectToPage("/Controlo/Peso", new { controlo_id = semPesoControloId, pdf_refusal = "sem-peso" })
                : pesoId is int semPesoId
                    ? RedirectToPage("/Controlo/Peso", new { peso_id = semPesoId, pdf_refusal = "sem-peso" })
                    : RedirectToPage("/Controlo/Peso", new { pdf_refusal = "sem-peso" });
        }

        Response.Headers[ControloDocumentPaths.PdfFolderHeader] = Uri.EscapeDataString(documento.Folder);
        return File(documento.Bytes, "application/pdf", documento.FileName);
    }

    /// <summary>
    /// A produção anterior da folha: o registo guardado no Peso e as últimas
    /// 5 produções da mesma referência anteriores a esta. A escolhida é a do
    /// select (anterior), senão a guardada, senão a anterior mais recente.
    /// </summary>
    private async Task LoadProducaoAnteriorAsync(int pesoId, int? currentJobOnId, int? requestedJobOnId)
    {
        ProducaoAnterior = await ProducaoAnteriorQuery.FindAsync(pesoId, HttpContext.RequestAborted);
        if (currentJobOnId is not int jobOnId)
        {
            return;
        }

        var opcoes = (await ProducaoAnteriorQuery.ListOpcoesAsync(jobOnId, HttpContext.RequestAborted)).ToList();

        // A guardada fica no select mesmo fora das últimas 5 (deep sweep 2,
        // F-11): o select só envia as suas opções, e o «Guardar» trocava-a
        // sem aviso pela primeira, ou apagava-a com o select vazio.
        if (ProducaoAnterior is { JobOnId: int saved, ProductionNumber: { } savedNumber } guardada
            && saved != jobOnId
            && opcoes.All(o => o.JobOnId != saved))
        {
            opcoes.Add(new PesoProducaoAnteriorOpcao(saved, savedNumber, guardada.Machine ?? "—", guardada.InicioEm, guardada.FimEm));
        }

        ProducaoAnteriorOpcoes = opcoes;
        ProducaoAnteriorSap = await _fabricoSap.PesoDosJobOnsAsync(opcoes.Select(o => o.JobOnId).ToList(), HttpContext.RequestAborted);
        var wanted = requestedJobOnId ?? ProducaoAnterior?.JobOnId;
        ProducaoAnteriorEscolhida = opcoes.FirstOrDefault(o => o.JobOnId == wanted)
            ?? opcoes.FirstOrDefault(o => o.JobOnId == ProducaoAnterior?.JobOnId)
            ?? opcoes.FirstOrDefault();
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
        [FromForm(Name = "anterior_peso")] string? anteriorPeso,
        [FromForm(Name = "supor_calote")] string? suporCalote = null,
        [FromForm(Name = "peso_anterior")] string? pesoAnteriorAberto = null)
    {
        // O «Supomos» aberto volta com a folha (gravar sem recarregar troca a
        // zona dele pelo que o endereço traz). Um Peso de antes de uma troca
        // volta pelo peso_id dele (18/10/2026).
        var suporTexto = Sanitize(suporCalote);
        int? pesoNoEndereco = controloId is null || pesoAnteriorAberto == "1" ? pesoId : null;
        var pesoTexto = Sanitize(anteriorPeso);
        decimal? pesoGramas = null;
        if (pesoTexto is not null)
        {
            if (ParseOptionalDecimal(pesoTexto) is not { } parsed || parsed <= 0m)
            {
                return RedirectToPage("/Controlo/Peso", new
                {
                    controlo_id = controloId,
                    peso_id = pesoNoEndereco,
                    comparar = compararPesoId,
                    supor_calote = suporTexto,
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
                    peso_id = pesoNoEndereco,
                    comparar = compararPesoId,
                    supor_calote = suporTexto,
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
            peso_id = pesoNoEndereco,
            comparar = compararPesoId,
            supor_calote = suporTexto,
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
    // O mesmo envio outra vez (a ligação caiu depois de gravar) não grava
    // duas vezes (deep sweep, 18/10/2026, B-peso-3).
    [EnvioUnico]
    public async Task<IActionResult> OnPostRegisterMeasurementAsync(
        [FromForm(Name = "peso_id")] int pesoId,
        [FromForm(Name = "cm_subject")] string?[]? cmSubjects,
        [FromForm(Name = "water_weight")] string?[]? waterWeights,
        [FromForm(Name = "controlo_id")] int? controloId = null,
        [FromForm(Name = "comparar")] int? compararPesoId = null,
        [FromForm(Name = "supor_calote")] string? suporCalote = null)
    {
        // Volta à folha de onde veio: a da produção (controlo_id) ou o Peso
        // aberto pelo Histórico (peso_id), com a comparação escolhida e o
        // «Supomos» aberto, que a leitura gravada atualiza.
        // Um Peso que deixou de ser o atual (a CM ou a BQ mudou com a folha
        // aberta) volta pelo peso_id dele, para a recusa aparecer nele.
        object Back(object? extra = null, bool comPeso = false)
        {
            var values = new RouteValueDictionary(extra ?? new { });
            if (controloId is int c)
            {
                values["controlo_id"] = c;
                if (comPeso)
                {
                    values["peso_id"] = pesoId;
                }
            }
            else
            {
                values["peso_id"] = pesoId;
            }
            if (compararPesoId is int cmp) values["comparar"] = cmp;
            if (Sanitize(suporCalote) is { } supor) values["supor_calote"] = supor;
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

        if (rows.Count == 0)
        {
            return RedirectToPage("/Controlo/Peso", Back());
        }

        // As linhas todas numa só escrita: um pedido cortado a meio (o
        // operador sai da página, a rede cai) não deixa só as primeiras, e
        // reenviar não as duplica. O pedido já chegou inteiro: grava até ao
        // fim mesmo que o browser se vá embora (deep sweep 18/10/2026).
        var outcome = await _recordMeasurement.HandleAsync(
            new RecordPesoMeasurement(
                pesoId,
                rows[0].CmSubject,
                rows[0].Water,
                rows.Skip(1).Select(r => new PesoMeasurementRow(r.CmSubject, r.Water)).ToList(),
                EnvioUnicoFilter.OperacaoDoEnvio(Request)),
            CancellationToken.None);

        // Peso do vidro 0 ou menos: a recusa volta com o que se escreveu
        // (a primeira linha), como a massa ≤ 0.
        if (outcome.Result == RecordPesoMeasurementResult.ResultNonPositive)
        {
            return RedirectToPage("/Controlo/Peso", Back(new
            {
                leitura_refusal = "vidro",
                leitura_cm = rows[0].CmSubject,
                leitura_water = Sanitize(weights.FirstOrDefault(w => Sanitize(w) is not null))
            }));
        }

        // O mesmo envio já gravado (a ligação caiu, a app reiniciou): volta
        // como se tivesse gravado agora, sem gravar outra vez.
        if (outcome.Result is not (RecordPesoMeasurementResult.Success or RecordPesoMeasurementResult.AlreadyRecorded))
        {
            return RedirectToPage("/Controlo/Peso", Back(new
            {
                measurement_refusal = outcome.Result.ToString()
            }, comPeso: outcome.Result == RecordPesoMeasurementResult.PesoNotCurrent));
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

            // Um Peso de antes de uma troca da CM: o apoio é o da CM dele
            // (18/10/2026).
            if (pesoId is int pesoDaFolha
                && await _pesoQuery.FindPesoAsync(pesoDaFolha, HttpContext.RequestAborted) is { } pesoAberto
                && pesoAberto.ControloId == canonicalControloId)
            {
                toolId = pesoAberto.CmToolId;
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
    /// POST (pelo DmoTroca desde o deep sweep, 18/10/2026; sem JS, nativo):
    /// comparar as leituras em preparação contra o Peso inicial
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
        [FromForm(Name = "water_weight")] string[]? waterWeights,
        [FromForm(Name = "comparacao_note")] string? comparacaoNote = null,
        [FromForm(Name = "producao_fixa")] string? producaoFixa = null)
    {
        ComparacaoTab = true;

        await ConfigurarShellDaComparacaoAsync();

        // A nota escrita volta no formulário (só eco, nada se grava): o
        // formulário trocado pela resposta já não a apaga (deep sweep,
        // 18/10/2026, B-peso-4).
        var (refusal, leituras, temperatura) = await PrepararComparacaoAsync(
            jobOnId, pesoInicialId, cmToolId, waterTemperature, comparacaoNote, cmSubjects, waterWeights,
            HttpContext.RequestAborted);
        ProducaoFixa = producaoFixa == "1" && ProducaoSelecionadaJobOnId is not null;
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
    /// POST (pelo DmoTroca desde o deep sweep, 18/10/2026; sem JS, nativo):
    /// guardar o registo de comparação (o histórico tipo
    /// Comparação do html — ligado ao controlo_id da produção). O cálculo é
    /// o MESMO da comparação ao vivo (a fonte única); os inputs consumidos
    /// são congelados no registo. Sucesso volta por PRG ao resultado a
    /// partir do PERSISTIDO; a recusa preserva o formulário (nada escrito).
    /// </summary>
    // O mesmo envio outra vez (a ligação caiu depois de guardar) não guarda
    // duas vezes, nem depois da janela dos repetidos (deep sweep, 18/10/2026,
    // B-peso-4).
    [EnvioUnico]
    public async Task<IActionResult> OnPostGuardarComparacaoAsync(
        [FromForm(Name = "jobon_id")] int jobOnId,
        [FromForm(Name = "peso_inicial")] int? pesoInicialId,
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "water_temperature")] string? waterTemperature,
        [FromForm(Name = "cm_subject")] string[]? cmSubjects,
        [FromForm(Name = "water_weight")] string[]? waterWeights,
        [FromForm(Name = "comparacao_note")] string? comparacaoNote,
        [FromForm(Name = "comparacao_guardada")] int? comparacaoGuardadaId = null,
        [FromForm(Name = "ignoradas")] string? ignoradasGuardadas = null,
        [FromForm(Name = "producao_fixa")] string? producaoFixa = null)
    {
        ComparacaoTab = true;
        var fixa = producaoFixa == "1" ? "1" : null;

        // Um clique repetido em «Guardar comparação» que chega já à página da
        // comparação acabada de guardar: o formulário dela vem sem leituras.
        // Não é uma comparação nova nem uma recusa a mostrar: volta à
        // guardada, como estava (cliques repetidos não mostram mensagens,
        // Diogo; B-peso-2, deep sweep 18/10/2026).
        if (comparacaoGuardadaId is int guardadaId
            && pesoInicialId is int inicialGuardado
            && (cmSubjects ?? []).All(string.IsNullOrWhiteSpace)
            && (waterWeights ?? []).All(string.IsNullOrWhiteSpace)
            && await _escolhas.ComparacaoGuardadaExisteAsync(guardadaId, inicialGuardado, HttpContext.RequestAborted))
        {
            return RedirectToPage("/Controlo/Peso", new
            {
                comparacao = "1",
                jobon_id = jobOnId,
                peso_inicial = inicialGuardado,
                producao_fixa = fixa,
                comparacao_id = guardadaId,
                ignoradas = Sanitize(ignoradasGuardadas)
            });
        }

        await ConfigurarShellDaComparacaoAsync();

        var (refusal, leituras, temperatura) = await PrepararComparacaoAsync(
            jobOnId, pesoInicialId, cmToolId, waterTemperature, comparacaoNote, cmSubjects, waterWeights,
            HttpContext.RequestAborted);
        ProducaoFixa = producaoFixa == "1" && ProducaoSelecionadaJobOnId is not null;
        if (leituras is null)
        {
            ComparacaoRefusal = refusal;
            await CarregarBaseAsync(HttpContext.RequestAborted);
            return Page();
        }

        // O pedido já chegou inteiro: grava até ao fim mesmo que o browser o
        // largue (um clique repetido larga o anterior). Os repetidos abrem a
        // comparação do primeiro (ver CreatePesoComparacaoHandler).
        var outcome = await ComparacaoHandler.HandleAsync(
            new CreatePesoComparacao(
                pesoInicialId!.Value,
                Sanitize(cmToolId),
                temperatura,
                leituras,
                Sanitize(comparacaoNote)),
            CancellationToken.None);

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
            producao_fixa = fixa,
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

        var lida = await _comparacaoEstado.BaseAsync(
            pesoBaseId, ResultadoComparacao?.Pares ?? ComparacaoGuardada?.Pares, cancellationToken);
        BaseDaComparacao = lida.Base;
        InfluenciaDaBase = lida.InfluenciaDaBase;
        InfluenciaDasNovas = lida.InfluenciaDasNovas;
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
        var lidas = await _comparacaoEstado.ProducoesDaReferenciaAsync(
            referenciaEscrita, producaoPedida, cancellationToken);
        ReferenciaComparacao = lidas.Referencia;
        ReferenciaComparacaoSemProducoes = lidas.SemProducoes;
        Producoes = lidas.Producoes;
        ProducaoSelecionadaJobOnId = lidas.SelecionadaJobOnId;
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
        ContraMoldes = await _comparacaoEstado.ContraMoldesAsync(referenciaDoInicial, cancellationToken);
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
                // Um item que não é um objeto ([1], ["x"]) não é uma linha e
                // fica de fora, como o JSON malformado: o TryGetProperty num
                // número lançava e o preview dava erro 500 (sweep do Codex,
                // 19/10/2026, ponto 2). A página envia sempre objetos.
                if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    continue;
                }

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
            echo_marisa = bqVolumeMarisa,
            comparacao_de = ComparacaoDePesoId
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

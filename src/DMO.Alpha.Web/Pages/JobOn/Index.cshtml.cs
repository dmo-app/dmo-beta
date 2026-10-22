using DMO.Alpha.Web.Authorization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Search;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// Planeamento · Job On: o rail da semana, o calendário do mês, o quadro do
/// dia, a ficha da produção e a folha de criação/edição. Tudo é desenhado
/// pelo servidor. Criar e editar validam no backend com recusas tipadas por
/// query (PRG); o MF é obrigatório; a sobreposição e a calote ilegível
/// avisam, não bloqueiam.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnCreate)]
public sealed class IndexModel : PageModel
{
    private const string PagePath = "/JobOn";

    private readonly ICreateJobOnHandler _createJobOn;
    private readonly IUpdateJobOnHandler _updateJobOn;
    private readonly IJobOnListQuery _jobOnList;
    private readonly IProductionContextQuery _productionContext;
    private readonly IMachineListQuery _machineList;
    private readonly IToolCandidateQuery _toolCandidates;
    private readonly IJobOnToolFactsQuery _toolFacts;
    private readonly IProductionPickerQuery _productionPicker;
    private readonly DmoDbContext _db;
    private readonly ShellStateFactory _shellState;
    private readonly CurrentUserCapabilities _capabilities;
    private readonly JobOnLotNoteHandler _lotNotes;
    private readonly JobOnChangeQuery _changes;
    private readonly IDeleteJobOnHandler _deleteJobOn;
    private readonly IJobOnFolhaQuery _folha;

    public IndexModel(
        ICreateJobOnHandler createJobOn,
        IUpdateJobOnHandler updateJobOn,
        IJobOnListQuery jobOnList,
        IProductionContextQuery productionContext,
        IMachineListQuery machineList,
        IToolCandidateQuery toolCandidates,
        IJobOnToolFactsQuery toolFacts,
        IProductionPickerQuery productionPicker,
        DmoDbContext db,
        ShellStateFactory shellState,
        CurrentUserCapabilities capabilities,
        JobOnLotNoteHandler lotNotes,
        JobOnChangeQuery changes,
        IDeleteJobOnHandler deleteJobOn,
        IJobOnFolhaQuery folha)
    {
        _createJobOn = createJobOn;
        _updateJobOn = updateJobOn;
        _jobOnList = jobOnList;
        _productionContext = productionContext;
        _machineList = machineList;
        _toolCandidates = toolCandidates;
        _toolFacts = toolFacts;
        _productionPicker = productionPicker;
        _db = db;
        _shellState = shellState;
        _capabilities = capabilities;
        _lotNotes = lotNotes;
        _changes = changes;
        _deleteJobOn = deleteJobOn;
        _folha = folha;
    }

    /// <summary>
    /// A FOLHA DA FÁBRICA no formulário (os campos escritos por famílias e a
    /// CAL): a gravada no Editar, vazia no Novo e, depois de uma recusa que
    /// volta à página a partir do envio, o que se escreveu.
    /// </summary>
    public FolhaDoJobOnVista Folha { get; private set; } = FolhaDoJobOnVista.Vazia();

    /// <summary>
    /// A folha da fábrica na ficha do Job On, em leitura, só com o que está
    /// escrito; null sem ficha (sem Job On aberto ou no Duplicar).
    /// </summary>
    public FichaDaFolhaVista? FichaFolha { get; private set; }

    // Uma recusa com a folha escrita volta à página a partir do envio (a folha
    // não cabe no endereço): o envio, a chave do texto longo demais e a versão
    // com que a folha de edição tinha aberto.
    private EnvioDaFolha? _envioDaFolha;
    private string? _chaveRecusada;
    private uint? _versaoDoEnvio;

    /// <summary>
    /// O link dos avisos dos módulos (?jobon_id=N&amp;vista=alteracoes): a ficha
    /// abre com o resumo «O que mudou» já aberto; sem ele, o resumo vem fechado
    /// (o proprietário, 17/10/2026).
    /// </summary>
    public bool VistaAlteracoes { get; private set; }

    /// <summary>O resumo «O que mudou» da produção aberta, do mais recente para o mais antigo.</summary>
    public IReadOnlyList<JobOnSummaryEntry> FichaResumo { get; private set; } = Array.Empty<JobOnSummaryEntry>();

    /// <summary>As mudanças de lote já registadas na produção aberta.</summary>
    public IReadOnlyList<LotNoteView> FichaLotNotes { get; private set; } = Array.Empty<LotNoteView>();

    /// <summary>Contexto de produção estabelecido do Job On aberto (?jobon_id); null fora desse contexto.</summary>
    public ProductionContextReadModel? OpenJobOn { get; private set; }

    /// <summary>A confirmação do «Eliminar Job On» da ficha (Diogo, 19/10/2026, D2).</summary>
    public JobOnEliminarAviso? FichaEliminar { get; private set; }

    /// <summary>O Job On acabou de ser eliminado (?eliminado=1).</summary>
    public bool Eliminado { get; private set; }

    /// <summary>
    /// Só quem tem um módulo do Controlo (Controlo ou Controlo Aprovar) vê a
    /// informação do Controlo no Job On (Diogo, 19/10/2026): a opção «Controlo»
    /// da ficha e o que o Eliminar conta. Quem só aprova entra no Controlo dele
    /// (ModuleEntry leva-o à Revisão da mesma produção).
    /// </summary>
    public bool VeControlo { get; private set; }

    /// <summary>
    /// A FICHA DA PRODUÇÃO (o job-sheet do dmo-html): os fatos técnicos das
    /// ferramentas da produção + os avisos informativos (a compatibilidade
    /// da máquina registada e o uso paralelo noutra produção ativa —
    /// nunca um bloqueio), calculados PELO BACKEND pela
    /// <see cref="IJobOnToolFactsQuery"/>. Null fora da ficha.
    /// </summary>
    public IReadOnlyList<JobOnToolFacts> FichaFacts { get; private set; } = Array.Empty<JobOnToolFacts>();

    /// <summary>
    /// O MODO DUPLICAR (?jobon_id=N&duplicar=1): o html duplica o Job On
    /// INTEIRO para uma produção nova (os dados copiados — mudar
    /// datas/máquina sem voltar a criar; o nº e as datas ficam por
    /// preencher, o mesmo espírito do duplicar-anterior). O jobon de
    /// origem; o draft composto no ScriptData abre a folha em CRIAÇÃO.
    /// </summary>
    public int? DuplicarDe { get; private set; }

    /// <summary>A calote submetida (o echo do POST recusado — o estado preservado).</summary>
    public string? RequestedCaloteMm { get; private set; }

    /// <summary>True quando foi pedido um jobon_id que não existe: recusa limpa, sem inventar contexto.</summary>
    public bool JobOnRequestedNotFound { get; private set; }

    /// <summary>
    /// O AVISO INFORMATIVO da sobreposição de janelas na mesma máquina
    /// (?overlap=1 — o PRG do POST que GRAVOU a produção com conflito): a
    /// verdade persistida recomposta pela MESMA calculadora dos handlers
    /// — as produções em conflito, as janelas, o período exato e como
    /// resolver. O banner amarelo veste a folha DEPOIS do redirect
    /// (nunca vermelho, nunca bloqueio: a produção está gravada); null
    /// quando não há sobreposição real.
    /// </summary>
    public OverlapWarning? OverlapWarning { get; private set; }

    /// <summary>
    /// A calote escrita que não foi gravada (?calote_ignorada — o PRG do POST
    /// que gravou o Job On sem ela): ilegível ou negativa. Ao criar, a
    /// produção fica sem calote e vale a referência da ferramenta; ao
    /// editar, mantém-se a calote que já estava gravada. O aviso amarelo diz
    /// qual foi o valor ignorado (decisão do proprietário, 16/10/2026:
    /// avisar em vez de recusar, sem perder dados).
    /// </summary>
    public string? CaloteIgnorada { get; private set; }

    /// <summary>
    /// A mensagem do aviso da calote ignorada, apenas apresentação. Lê a
    /// calote gravada da produção aberta: com valor, foi uma edição que o
    /// manteve; sem valor, a produção ficou sem calote.
    /// </summary>
    public string? CaloteIgnoradaMessage => CaloteIgnorada is not { } valor
        ? null
        : OpenJobOn?.CaloteMm is { } mantida
            ? $"Job On guardado: «{valor}» não é um número em mm igual ou superior a 0 e foi ignorado; mantém-se a calote anterior ({mantida.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} mm)."
            : $"Job On guardado sem calote: «{valor}» não é um número em mm igual ou superior a 0 e foi ignorado. Vale a calote de referência da ferramenta; para a indicar, use Editar.";

    /// <summary>
    /// O Job On foi gravado com o fim antes do início (?fim_antes_inicio=1, o
    /// PRG do POST). Grava e avisa (Diogo, 19/10/2026: «as datas têm de ser
    /// livres»; na beta.228 era recusado).
    /// </summary>
    public bool FimAntesDoInicio { get; private set; }

    /// <summary>
    /// A mensagem do aviso do fim antes do início, com as datas gravadas da
    /// produção aberta; null quando as datas já não estão trocadas.
    /// </summary>
    public string? FimAntesDoInicioMessage => FimAntesDoInicio
        && OpenJobOn is { InicioEm: { } inicio, FimEm: { } fim } && fim < inicio
        ? $"Job On guardado com o fim ({fim.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}) antes do início ({inicio.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}). " +
          "Enquanto o fim for antes do início, a produção não aparece em nenhum dia do calendário. Para corrigir as datas, use Editar."
        : null;

    /// <summary>A TAB do Histórico (?historico=1 — o padrão do Controlo/Peso): as produções anteriores e os sem dia.</summary>
    public bool ShowHistory { get; private set; }

    /// <summary>A página do histórico (?pagina — 25 produções por página), paginada na base; só lida depois de uma procura.</summary>
    [BindProperty(SupportsGet = true, Name = "pagina")] public int? HistoryPageNumber { get; set; }
    public PagedList<ProductionContextReadModel> HistoryPage { get; private set; } = PagedList<ProductionContextReadModel>.Empty;

    /// <summary>Os filtros do histórico (?referencia, ?maquina, ?producao, ?desde, ?ate).</summary>
    public string? HistoryReference { get; private set; }
    public int? HistoryMachineId { get; private set; }
    public string? HistoryProduction { get; private set; }

    /// <summary>A referência da produção escolhida no dropdown: o mesmo nº pode existir noutra referência.</summary>
    public string? HistoryProductionReference { get; private set; }
    public DateOnly? HistoryDesde { get; private set; }
    public DateOnly? HistoryAte { get; private set; }

    /// <summary>As produções da referência procurada: o dropdown do histórico.</summary>
    public IReadOnlyList<JobOnProductionOption> HistoryProductions { get; private set; } = Array.Empty<JobOnProductionOption>();

    /// <summary>
    /// Entre o nº e a referência no valor de uma opção do dropdown, quando o
    /// dropdown tem produções de mais do que uma referência («2026001|5447T173»).
    /// </summary>
    public const char HistoryProductionSeparator = '|';

    /// <summary>True quando o dropdown das produções junta mais do que uma referência (o texto é o início de várias).</summary>
    public bool HistoryProductionsSpanReferences =>
        HistoryProductions.Select(p => p.Reference).Distinct(StringComparer.Ordinal).Skip(1).Any();

    /// <summary>
    /// O valor de uma opção do dropdown: o nº; com várias referências, o nº e
    /// a referência, para a escolha filtrar só essa produção (deep sweep 2,
    /// F-17: o mesmo nº em duas referências filtrava as duas).
    /// </summary>
    public string HistoryProductionValue(JobOnProductionOption option) => HistoryProductionsSpanReferences
        ? $"{option.ProductionNumber}{HistoryProductionSeparator}{option.Reference}"
        : option.ProductionNumber;

    /// <summary>A opção escolhida: o nº e a referência da produção que filtra.</summary>
    public bool IsHistoryProductionSelected(JobOnProductionOption option) =>
        option.ProductionNumber == HistoryProduction && option.Reference == HistoryProductionReference;

    /// <summary>True quando há referência, máquina ou data; sem isso o histórico fica em branco e não lê nada.</summary>
    public bool HistorySearchApplied { get; private set; }

    /// <summary>Datas ilegíveis ignoradas ou trocadas (aviso amarelo, nunca recusa).</summary>
    public IReadOnlyList<string> HistoryDateWarnings { get; private set; } = Array.Empty<string>();

    public const string HistoryEmptyStateMessage = "Pesquise por referência ou data para ver as produções.";

    /// <summary>
    /// Sem dia escolhido (dia=0, o duplo clique no dia já escolhido do
    /// calendário): o quadro do dia fica vazio, só com a indicação.
    /// </summary>
    public bool NoDaySelected { get; private set; }

    /// <summary>
    /// A FOLHA server-rendered (o referencial do Owner — sem js para
    /// abrir): ?editar=1&jobon_id=N abre-a EM EDIÇÃO com os campos
    /// preenchidos pela produção (a identidade read-only); ?novo=1 abre-a
    /// em CRIAÇÃO (vazia); ?jobon_id=N&duplicar=1 abre-a em criação COM OS
    /// DADOS COPIADOS (o html: o nº e as datas por preencher).
    /// </summary>
    public bool FolhaAberta { get; private set; }

    /// <summary>True quando a folha aberta é MODO EDIÇÃO (o jobon_edit_id segue no POST).</summary>
    public bool FolhaEdicao => OpenJobOn is not null && !DuplicarDe.HasValue && FolhaAberta;

    /// <summary>A calote pré-preenchida na folha (o valor da produção em edição/duplicar).</summary>
    public string? FolhaCalote { get; private set; }

    /// <summary>
    /// A versão do Job On quando a folha de edição abriu (vai escondida no
    /// POST): se outra pessoa gravar entretanto, nada se grava por cima. Ao
    /// voltar do «Criar CM/MF/BQ» é a da folha que se estava a editar.
    /// </summary>
    public uint FolhaVersao { get; private set; }

    /// <summary>O mês inicial do calendário (deep link ?ano&mes; omissão = hoje).</summary>
    public int Year { get; private set; }

    public int Month { get; private set; }

    /// <summary>Dia validado, comum ao calendário e ao painel do dia.</summary>
    public int SelectedDay { get; private set; }

    /// <summary>Pesquisa submetida por GET, sem depender de script externo.</summary>
    public string? SearchTerm { get; private set; }

    /// <summary>
    /// A procura do Job On em dois campos (Diogo, 17/10/2026): a referência
    /// escrita e, à frente, o dropdown com as últimas produções dela. Sem
    /// script, o Enter no campo enche o dropdown pelo servidor. Null sem procura.
    /// </summary>
    public JobOnProcura? SearchResults { get; private set; }

    /// <summary>
    /// A tabela de todas as produções da referência do Job On aberto
    /// (?producoes_ref=1, na «Opções» da folha), paginada na base e lida só
    /// quando se abre; null fechada.
    /// </summary>
    public PagedList<ProductionContextReadModel>? ReferenceProductionsPage { get; private set; }

    /// <summary>A página da tabela das produções da referência (?pagina_ref).</summary>
    [BindProperty(SupportsGet = true, Name = "pagina_ref")] public int? ReferenceProductionsPageNumber { get; set; }

    /// <summary>As linhas por página da tabela das produções da referência.</summary>
    public const int ReferenceProductionsPageSize = 10;

    /// <summary>O QUADRO COMPLETO DAS LINHAS do dia (o shared element por Razor — o Owner: «quadro do dia pode ir»): as máquinas B1..C3 sempre visíveis, as com produção mostram-na, as livres ficam vazias.</summary>
    public IReadOnlyList<DMO.Alpha.Web.Presentation.DayMachineRow> DayRows { get; private set; } = Array.Empty<DMO.Alpha.Web.Presentation.DayMachineRow>();
    public IReadOnlyList<ProductionContextReadModel> JobOns { get; private set; } = Array.Empty<ProductionContextReadModel>();

    /// <summary>
    /// O SIDEPANEL (a semântica do Owner 14/10: «o sidepanel mostra o que
    /// vai entrar»): as próximas produções a entrar nas linhas — as
    /// produções cujo InicioEm ainda não chegou (a fila à frente),
    /// ordenadas pela data de entrada. Os factos humanos apenas; o clique
    /// abre a ficha.
    /// </summary>
    public IReadOnlyList<ProductionContextReadModel> ProximasEntradas { get; private set; } = Array.Empty<ProductionContextReadModel>();

    /// <summary>Máquinas persistidas para seleção explícita; lista vazia = registo ainda não povoado.</summary>
    public IReadOnlyList<MachineOption> Machines { get; private set; } = Array.Empty<MachineOption>();

    /// <summary>
    /// Só as ferramentas que a página mostra: as escolhidas na folha e as do
    /// histórico desta página. A escolha de outra é uma procura ao escrever
    /// (?handler=ToolCandidates); o catálogo nunca é carregado inteiro.
    /// </summary>
    public IReadOnlyList<ToolCandidate> KnownTools { get; private set; } = Array.Empty<ToolCandidate>();

    /// <summary>Quantas sugestões a procura de ferramentas devolve de cada vez.</summary>
    public const int ToolSuggestionLimit = 20;

    /// <summary>Valor submetido do nº de produção (estado do formulário preservado).</summary>
    public string? RequestedProductionNumber { get; private set; }

    /// <summary>Valor submetido da referência (estado do formulário preservado).</summary>
    public string? RequestedReference { get; private set; }

    /// <summary>Máquina submetida (estado do formulário preservado); null quando não escolhida.</summary>
    public int? RequestedMachineId { get; private set; }

    /// <summary>tool_id da CM explicitamente selecionado (null = não aplicável).</summary>
    public string? RequestedCmToolId { get; private set; }

    /// <summary>tool_id da MF explicitamente selecionado (null = não aplicável).</summary>
    public string? RequestedMfToolId { get; private set; }

    /// <summary>tool_id da BQ explicitamente selecionado (null = não aplicável).</summary>
    public string? RequestedBqToolId { get; private set; }

    /// <summary>
    /// O «Criar CM/MF/BQ» do cartão da ferramenta: abre as Ferramentas e volta
    /// à MESMA folha com a ferramenta criada no seu lugar (o Diogo, 17/10/2026:
    /// criar uma CM nova para trocar durante o fabrico abria um Job On novo e
    /// pedia o número da produção). No Editar volta à edição deste Job On; o
    /// resto do que estava escrito volta com ?retoma=1. O script da página
    /// refaz o link com o que estiver escrito no momento do clique.
    /// </summary>
    public string CriarFerramentaHref(string tipo)
    {
        var slot = tipo.ToLowerInvariant() + "_tool_id";
        var valores = new List<(string Nome, string? Valor)>();
        if (FolhaEdicao)
        {
            valores.Add(("jobon_id", OpenJobOn!.JobOnId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            valores.Add(("editar", "1"));
        }
        else
        {
            valores.Add(("novo", "1"));
            valores.Add(("reference", RequestedReference));
            valores.Add(("production_number", RequestedProductionNumber));
        }

        valores.Add(("retoma", "1"));
        valores.Add(("machine_id", RequestedMachineId?.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        valores.Add(("inicio_em", RequestedInicioEm?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        valores.Add(("fim_em", RequestedFimEm?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        valores.Add(("calote_mm", FolhaCalote));
        foreach (var (outro, id) in new[] { ("cm_tool_id", RequestedCmToolId), ("mf_tool_id", RequestedMfToolId), ("bq_tool_id", RequestedBqToolId) })
        {
            if (outro != slot) valores.Add((outro, id));
        }

        // A versão da folha em edição volta com ela (ver FolhaVersao).
        if (FolhaEdicao)
        {
            valores.Add(("versao", FolhaVersao.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        // O {tool_id} fica por codificar: as Ferramentas substituem-no pelo id criado.
        var regresso = "/JobOn?" + string.Join("&", valores
            .Where(v => !string.IsNullOrEmpty(v.Valor))
            .Select(v => v.Nome + "=" + Uri.EscapeDataString(v.Valor!)))
            + "&" + slot + "={tool_id}#lightSheet";
        // A máquina da produção já vem escolhida no registo (Diogo,
        // 18/10/2026: «estamos no jobon 6018Y232 C2, mudar boquilha já vem
        // com C2 BQ filtrado»).
        var maquina = RequestedMachineId is int machineId
            ? "&machine_ids=" + machineId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "";
        return $"/Ferramentas?criar=1&type={tipo}{maquina}&return={Uri.EscapeDataString(regresso)}";
    }

    /// <summary>O dia em que a produção entra em máquina (o corte B; null = em aberto).</summary>
    public DateOnly? RequestedInicioEm { get; private set; }

    /// <summary>O dia em que sai de máquina (null = até aviso).</summary>
    public DateOnly? RequestedFimEm { get; private set; }

    /// <summary>Recusa tipada do último POST de criação, transportada por query (PRG).</summary>
    public CreateJobOnResult? CreateRefusal { get; private set; }

    /// <summary>Recusa tipada do último POST de EDIÇÃO, transportada por query (PRG).</summary>
    public UpdateJobOnResult? UpdateRefusal { get; private set; }

    /// <summary>Mensagem da recusa tipada, apenas apresentação.</summary>
    public string CreateRefusalMessage => CreateRefusal switch
    {
        CreateJobOnResult.MissingProductionNumber =>
            "O nº de produção está em branco. Nenhum Job On foi criado.",
        CreateJobOnResult.MissingReference =>
            "A referência está em branco. Nenhum Job On foi criado.",
        CreateJobOnResult.MachineNotFound =>
            "A máquina indicada não existe. Nenhum Job On foi criado.",
        CreateJobOnResult.MissingMfTool =>
            "O MF (molde final) é obrigatório — sem ele não se fazem garrafas nem frascos. Selecione o MF do desenho. Nenhum Job On foi criado.",
        CreateJobOnResult.MissingCmTool =>
            "Falta a CM (contra molde): um Job On precisa de CM, BQ e MF. Associe ou crie a CM. Nenhum Job On foi criado.",
        CreateJobOnResult.MissingBqTool =>
            "Falta a BQ (boquilha): um Job On precisa de CM, BQ e MF. Associe ou crie a BQ. Nenhum Job On foi criado.",
        CreateJobOnResult.CmToolNotFound =>
            "A ferramenta CM selecionada não está registada no Ferramentas. Nenhum Job On foi criado.",
        CreateJobOnResult.MfToolNotFound =>
            "A ferramenta MF selecionada não está registada no Ferramentas. Nenhum Job On foi criado.",
        CreateJobOnResult.BqToolNotFound =>
            "A boquilha (BQ) selecionada não está registada no Ferramentas. Nenhum Job On foi criado.",
        CreateJobOnResult.DuplicateProductionNumber =>
            "Já existe um Job On desta referência com este nº de produção — uma produção nova tem nº próprio. Nenhum Job On foi criado.",
        CreateJobOnResult.InvalidCalote =>
            "A calote desta produção não é utilizável — escreve um número em mm (0 ou mais; vazio usa a referência da Tool). Nenhum Job On foi criado.",
        CreateJobOnResult.TextoLongo =>
            Folha.MensagemTextoLongo("Nenhum Job On foi criado.") ?? "Um texto da folha é longo demais. Nenhum Job On foi criado.",
        _ => string.Empty
    };

    /// <summary>Mensagem da recusa tipada da edição, apenas apresentação.</summary>
    public string UpdateRefusalMessage => UpdateRefusal switch
    {
        UpdateJobOnResult.JobOnNotFound =>
            "A produção indicada não existe. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.MachineNotFound =>
            "A máquina indicada não existe. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.MissingMfTool =>
            "O MF (molde final) é obrigatório — sem ele não se fazem garrafas nem frascos. Selecione o MF do desenho. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.MissingCmTool =>
            "Falta a CM (contra molde): um Job On precisa de CM, BQ e MF. Associe ou crie a CM. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.MissingBqTool =>
            "Falta a BQ (boquilha): um Job On precisa de CM, BQ e MF. Associe ou crie a BQ. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.CmToolNotFound =>
            "A ferramenta CM selecionada não está registada no Ferramentas. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.MfToolNotFound =>
            "A ferramenta MF selecionada não está registada no Ferramentas. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.BqToolNotFound =>
            "A boquilha (BQ) selecionada não está registada no Ferramentas. Nenhuma alteração foi guardada.",
        UpdateJobOnResult.InvalidCalote =>
            "A calote desta produção não é utilizável — escreve um número em mm (0 ou mais; vazio usa a referência da Tool). Nenhuma alteração foi guardada.",
        UpdateJobOnResult.ChangedByAnotherUser =>
            "Outra pessoa gravou esta produção depois de a abrires. Nenhuma alteração foi guardada: a folha mostra agora o que ficou gravado; volta a fazer a tua alteração.",
        UpdateJobOnResult.TextoLongo =>
            Folha.MensagemTextoLongo("Nenhuma alteração foi guardada.") ?? "Um texto da folha é longo demais. Nenhuma alteração foi guardada.",
        _ => string.Empty
    };

    /// <summary>A calote da ficha duplicada (o echo para o form; null quando não há).</summary>
    public string? FichaCaloteEcho { get; private set; }

    private static readonly JsonSerializerOptions ScriptJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Default,
        Converters = { new JsonStringEnumConverter() }
    };

    private int InitialDay()
    {
        var today = DateOnly.FromDateTime(DmoBusinessTime.Now);
        return today.Year == Year && today.Month == Month ? today.Day : 1;
    }

    /// <summary>Um record do calendário: os factos humanos da produção com as tools pelos nomes com lote.</summary>

    /// <summary>O SHARED ELEMENT do calendário (o Owner 2026-10-14) — o viewmodel comum servido pelo partial _DmoCalendar; os dias com produção pela janela temporal.</summary>
    public DMO.Alpha.Web.Pages.Shared.DmoCalendarPresentation? Calendar { get; private set; }

        private DMO.Alpha.Web.Pages.Shared.DmoCalendarPresentation ComporJobOnCalendar(int year, int month, IReadOnlyList<ProductionContextReadModel> jobOns, int? diaSelecionado)
    {
        // A LINHA DO TEMPO COMPLETA (o Owner 14/10: «o calendário mostra
        // tudo o que está a produzir, o que vai entrar — e ver o que
        // esteve no passado naquele específico dia»): o dia marca com
        // produção EM QUALQUER TEMPO — o passado (entrou e saiu), o
        // presente e o futuro (a entrar); a produção «até aviso» (o
        // FimEm null) cobre a partir do início.
        // O SINAL SÓ NAS MUDANÇAS (Diogo, 19/10/2026: «deve apenas mostrar
        // quando existem mudanças numa linha senão o calendário fica
        // carregado»): o dia acende quando uma produção entra numa linha ou
        // sai dela (o dia a seguir ao FimEm); os dias em que tudo continua
        // ficam limpos. A contagem continua a ser a de tudo o que está a produzir.
        // O partial gere ano/mes; repetir estes parâmetros invalida a navegação.
        return DMO.Alpha.Web.Pages.Shared.DmoCalendarPresentation.ForMonth(
            year, month,
            data => jobOns.Count(j => j.InicioEm <= data && (j.FimEm == null || data <= j.FimEm)),
            diaSelecionado is { } dia && dia >= 1 && dia <= DateTime.DaysInMonth(year, month) ? new DateOnly(year, month, dia) : null,
            "/JobOn/Index",
            hasForDay: data => jobOns.Any(j => j.InicioEm == data || j.FimEm?.AddDays(1) == data)) with { LegendLabel = "Mudanças numa linha" };
    }

    private static IReadOnlyList<DMO.Alpha.Web.Presentation.DayMachineRow> ComporDayRows(IReadOnlyList<MachineOption> machines, IReadOnlyList<ProductionContextReadModel> jobOns, int ano, int mes, int? diaSelecionado)
    {
        // A procura não filtra o dia (Diogo, 17/10/2026: não é um filtro
        // global); as linhas das máquinas mostram sempre o dia escolhido.
        var cobre = new System.Collections.Generic.List<ProductionContextReadModel>();
        // O DIA CONSULTADO (o painel do dia do dmo-html): o dia clicado no
        // calendário — na omissão, o dia inicial da página (o hoje no mês
        // corrente). A produção «até aviso» (o FimEm null) COBRE o dia a
        // partir do início (a regra do corte B: as datas do Job On mandam).
        var data = new DateOnly(ano, mes, Math.Clamp(diaSelecionado ?? 1, 1, DateTime.DaysInMonth(ano, mes)));
        cobre.AddRange(jobOns.Where(j => j.InicioEm <= data && (j.FimEm == null || data <= j.FimEm)));
        var rows = new System.Collections.Generic.List<DMO.Alpha.Web.Presentation.DayMachineRow>();
        foreach (var machine in machines)
        {
            var doDia = cobre.Where(j => j.Machine == machine.Code).OrderBy(j => j.Reference).ToList();
            // A sobreposição: duas produções que se cruzam, pela regra do aviso
            // ao gravar (a que sai no dia em que a outra entra não conta).
            var cruzam = doDia.Any(a => doDia.Any(b => a.JobOnId < b.JobOnId
                && DMO.Alpha.Core.Production.JobOnOverlap.Crosses(a.InicioEm, a.FimEm, b.InicioEm, b.FimEm)));
            rows.Add(new(machine.Code, doDia.Select(x => new DMO.Alpha.Web.Presentation.DayProduction(x.JobOnId, x.Reference, x.ProductionNumber)).ToList())
            {
                HasOverlap = cruzam
            });
        }
        return rows;
    }

    /// <summary>
    /// O HISTÓRICO abre em branco (decisão do proprietário, 16/10/2026): só
    /// lê produções depois de uma procura pela referência, máquina ou data.
    /// A procura de uma produção faz-se em dois passos: a referência (e a
    /// máquina, se quiser) e depois o dropdown só com as produções dela,
    /// lido só para ela. Uma data ilegível é ignorada com um aviso amarelo,
    /// não recusada.
    /// </summary>
    private async Task ComposeHistoryAsync(string? referencia, int? maquina, string? producao, string? desde, string? ate)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var warnings = new List<string>();
        HistoryReference = Sanitize(referencia);
        // Uma máquina que não existe não filtra.
        HistoryMachineId = maquina is int id && Machines.Any(m => m.MachineId == id) ? id : null;
        HistoryDesde = ParseHistoryDate(desde, "Desde", warnings);
        HistoryAte = ParseHistoryDate(ate, "Até", warnings);
        if (HistoryDesde is { } d && HistoryAte is { } a && d > a)
        {
            (HistoryDesde, HistoryAte) = (a, d);
            warnings.Add("«Desde» era depois de «Até»: as datas foram trocadas.");
        }
        HistoryDateWarnings = warnings;

        if (HistoryReference is { } reference)
        {
            HistoryProductions = await _jobOnList.ListProductionsOfReferenceAsync(reference, HistoryMachineId, cancellationToken);
            // A produção pedida: o nº e, quando o dropdown junta várias
            // referências, a referência dela. Um link antigo só com o nº
            // escolhe a mais recente com esse nº, nunca as duas juntas. Uma
            // produção que já não é desta referência (a referência mudou) não filtra.
            var (wanted, wantedReference) = ParseHistoryProduction(Sanitize(producao));
            var escolhida = wanted is null
                ? null
                : HistoryProductions.FirstOrDefault(p => p.ProductionNumber == wanted
                    && (wantedReference is null || p.Reference == wantedReference));
            // O dropdown só tem as últimas produções; uma mais antiga pedida
            // por link continua a filtrar e entra no dropdown.
            if (wanted is not null && escolhida is null)
            {
                var antigas = _db.JobOns.AsNoTracking()
                    .Where(ReferenceMatch.Predicate<DMO.Alpha.Core.Production.JobOn>(j => j.Reference, reference))
                    .Where(j => j.ProductionNumber == wanted);
                if (wantedReference is not null)
                {
                    antigas = antigas.Where(j => j.Reference == wantedReference);
                }
                if (HistoryMachineId is int maquinaPedida)
                {
                    antigas = antigas.Where(j => j.MachineId == maquinaPedida);
                }
                escolhida = await antigas
                    .OrderByDescending(j => j.Id)
                    .Select(j => new JobOnProductionOption(j.ProductionNumber, j.Reference))
                    .FirstOrDefaultAsync(cancellationToken);
                if (escolhida is not null)
                {
                    HistoryProductions = [.. HistoryProductions, escolhida];
                }
            }
            HistoryProduction = escolhida?.ProductionNumber;
            HistoryProductionReference = escolhida?.Reference;
        }

        var filter = new JobOnHistoryFilter(
            HistoryReference, HistoryMachineId, HistoryProduction, HistoryDesde, HistoryAte, HistoryProductionReference);
        HistorySearchApplied = !filter.IsEmpty;
        if (HistorySearchApplied)
        {
            HistoryPage = await _jobOnList.SearchHistoryPageAsync(
                filter, HistoryPageNumber, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// O valor pedido do dropdown das produções: «nº» (os links antigos e o
    /// dropdown de uma só referência) ou «nº|referência».
    /// </summary>
    private static (string? Number, string? Reference) ParseHistoryProduction(string? value)
    {
        if (value is null)
        {
            return (null, null);
        }
        var separator = value.IndexOf(HistoryProductionSeparator);
        if (separator < 0)
        {
            return (value, null);
        }
        var number = value[..separator].Trim();
        var reference = value[(separator + 1)..].Trim().ToUpperInvariant();
        return (number.Length == 0 ? null : number, reference.Length == 0 ? null : reference);
    }

    /// <summary>Uma página do histórico, sem compor calendário, dia ou rail.</summary>
    public async Task<IActionResult> OnGetHistoricoResultadosAsync(
        [FromQuery(Name = "referencia")] string? referencia,
        [FromQuery(Name = "maquina")] int? maquina,
        [FromQuery(Name = "producao")] string? producao,
        [FromQuery(Name = "desde")] string? desde,
        [FromQuery(Name = "ate")] string? ate)
    {
        Machines = await _machineList.ListAsync(HttpContext.RequestAborted);
        await ComposeHistoryAsync(referencia, maquina, producao, desde, ate);
        var shownToolIds = HistoryPage.Items
            .SelectMany(j => new[] { j.CmToolId, j.MfToolId, j.BqToolId })
            .Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).Distinct().ToList();
        if (shownToolIds.Count > 0)
            KnownTools = await _toolCandidates.FindCandidatesAsync(
                new ToolCandidateFilter(ToolIds: shownToolIds), HttpContext.RequestAborted);
        Response.Headers["X-Dmo-Region"] = "jobonHistoricoResultados";
        return Partial("_JobOnHistoricoResultados", this);
    }

    private static DateOnly? ParseHistoryDate(string? value, string label, List<string> warnings)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        if (DateOnly.TryParseExact(text, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }
        warnings.Add($"«{label}» ignorada: «{text}» não é uma data válida.");
        return null;
    }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "create_refusal")] string? createRefusal,
        [FromQuery(Name = "update_refusal")] string? updateRefusal,
        [FromQuery(Name = "production_number")] string? productionNumber,
        [FromQuery(Name = "reference")] string? reference,
        [FromQuery(Name = "machine_id")] int? machineId,
        [FromQuery(Name = "cm_tool_id")] string? cmToolId,
        [FromQuery(Name = "mf_tool_id")] string? mfToolId,
        [FromQuery(Name = "bq_tool_id")] string? bqToolId,
        [FromQuery(Name = "inicio_em")] string? inicioEm,
        [FromQuery(Name = "fim_em")] string? fimEm,
        [FromQuery(Name = "historico")] string? historico,
        [FromQuery(Name = "ano")] int? ano,
        [FromQuery(Name = "mes")] int? mes,
        [FromQuery(Name = "overlap")] string? overlap,
        [FromQuery(Name = "dia")] int? diaSelected,
        [FromQuery(Name = "procura")] string? procura,
        [FromQuery(Name = "procura_ref")] string? procuraRef,
        [FromQuery(Name = "producoes_ref")] string? producoesRef,
        [FromQuery(Name = "duplicar")] string? duplicar,
        [FromQuery(Name = "calote_mm")] string? caloteMm,
        [FromQuery(Name = "calote_ignorada")] string? caloteIgnorada,
        [FromQuery(Name = "fim_antes_inicio")] string? fimAntesInicio,
        [FromQuery(Name = "editar")] string? editar,
        [FromQuery(Name = "novo")] string? novo,
        [FromQuery(Name = "retoma")] string? retoma,
        [FromQuery(Name = "versao")] string? versao,
        [FromQuery(Name = "referencia")] string? historicoReferencia,
        [FromQuery(Name = "maquina")] int? historicoMaquina,
        [FromQuery(Name = "producao")] string? historicoProducao,
        [FromQuery(Name = "desde")] string? historicoDesde,
        [FromQuery(Name = "ate")] string? historicoAte,
        [FromQuery(Name = "vista")] string? vista = null,
        [FromQuery(Name = "eliminado")] string? eliminado = null)
    {
        ViewData["Title"] = "Job On";
        // O JOB ON É UMA FUNÇÃO DO PLANEAMENTO (o Owner: «o jobon fica no
        // tab do planeamento, aparece no segundo tab») — a barra principal
        // seleciona o PLANEAMENTO; as funções: Job On · Histórico (o
        // padrão ?historico=1 do Controlo/Peso).
        ShowHistory = historico == "1";
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("planeamento", "Planeamento"))) with
        {
            ActiveFunctionKey = ShowHistory ? "historico" : "jobon",
            Functions = ShellStateFactory.PlanningFunctions(ShowHistory ? "historico" : "jobon")
        };

        // Recusa tipada do POST anterior, transportada por query (PRG) com o
        // vocabulário fechado do enum — nunca texto livre.
        if (createRefusal is not null
            && Enum.TryParse(createRefusal, out CreateJobOnResult parsed)
            && parsed != CreateJobOnResult.Success)
        {
            CreateRefusal = parsed;
        }

        // Recusa tipada do POST de EDIÇÃO anterior, o mesmo transporte por
        // query (PRG) com o vocabulário fechado do seu enum.
        if (updateRefusal is not null
            && Enum.TryParse(updateRefusal, out UpdateJobOnResult parsedUpdate)
            && parsedUpdate != UpdateJobOnResult.Success)
        {
            UpdateRefusal = parsedUpdate;
        }

        // Estado do formulário preservado (o POST recusado volta por query
        // com os valores que o utilizador já tinha introduzido).
        RequestedProductionNumber = Sanitize(productionNumber);
        RequestedReference = Sanitize(reference);
        RequestedMachineId = machineId;
        RequestedCmToolId = Sanitize(cmToolId);
        RequestedMfToolId = Sanitize(mfToolId);
        RequestedBqToolId = Sanitize(bqToolId);
        RequestedInicioEm = DateOnly.TryParse(inicioEm, out var inicioValue) ? inicioValue : null;
        RequestedFimEm = DateOnly.TryParse(fimEm, out var fimValue) ? fimValue : null;

        CaloteIgnorada = Sanitize(caloteIgnorada);
        FimAntesDoInicio = fimAntesInicio == "1";
        Eliminado = eliminado == "1";

        // O mês inicial do calendário (deep link ?ano&mes; omissão = hoje).
        var today = DateOnly.FromDateTime(DmoBusinessTime.Now);
        Year = ano is int y and > 1999 and < 3000 ? y : today.Year;
        Month = mes is int m and >= 1 and <= 12 ? m : today.Month;
        NoDaySelected = diaSelected == 0;
        SelectedDay = diaSelected is int chosenDay && chosenDay >= 1 && chosenDay <= DateTime.DaysInMonth(Year, Month)
            ? chosenDay : InitialDay();
        SearchTerm = (string.IsNullOrWhiteSpace(procuraRef) ? procura : procuraRef)?.Trim();

        // Job On aberto: o contexto de produção estabelecido, resolvido pela
        // query canónica existente (a mesma que os módulos seguintes usam).
        if (jobOnId is int canonicalJobOnId)
        {
            OpenJobOn = await _productionContext.GetAsync(canonicalJobOnId, HttpContext.RequestAborted);
            JobOnRequestedNotFound = OpenJobOn is null;
        }

        // A procura ao escrever, desenhada pelo servidor quando chega pelo Enter.
        if (!string.IsNullOrEmpty(SearchTerm))
        {
            SearchResults = await ProcurarProducoesAsync(
                procura, procuraRef, Year, Month, NoDaySelected ? 0 : SelectedDay);
        }

        if (producoesRef == "1" && OpenJobOn is { } fichaReferencia)
        {
            ReferenceProductionsPage = await _jobOnList.ListPageOfReferenceAsync(
                fichaReferencia.Reference, ReferenceProductionsPageNumber, ReferenceProductionsPageSize, HttpContext.RequestAborted);
        }

        // O AVISO DO OVERLAP (?overlap=1 — o PRG do POST que gravou a
        // produção com sobreposição na máquina): a verdade persistida
        // RECOMPOSTA pela MESMA calculadora dos handlers (determinística
        // sobre os factos — um estado já resolvido simplesmente não
        // gera aviso). O banner amarelo veste a folha; o detalhe (as
        // produções em conflito, as janelas, o período exato, como
        // resolver) vem todo no <see cref="OverlapWarning.Message"/>.
        if (overlap == "1" && OpenJobOn is { } overlapOpen)
        {
            var overlapMachineId = await _db.JobOns
                .AsNoTracking()
                .Where(j => j.Id == overlapOpen.JobOnId)
                .Select(j => j.MachineId)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            if (overlapMachineId != 0)
            {
                OverlapWarning = await DMO.Alpha.Infrastructure.Production.JobOnOverlapQuery.EvaluateAsync(
                    _db, overlapOpen.JobOnId, overlapMachineId, overlapOpen.Machine,
                    overlapOpen.InicioEm, overlapOpen.FimEm, HttpContext.RequestAborted);
            }
        }

        // Só as produções que o calendário do mês, o dia e o rail da semana
        // mostram; o histórico pagina na base e só é lido na sua tab.
        var primeiroDia = new DateOnly(Year, Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);
        var domingo = hoje.AddDays((7 - (int)hoje.DayOfWeek) % 7);
        JobOns = await _jobOnList.ListWindowAsync(
            primeiroDia < hoje ? primeiroDia : hoje,
            ultimoDia > domingo ? ultimoDia : domingo,
            HttpContext.RequestAborted);

        // Máquinas persistidas (o select da linha). As ferramentas não se
        // carregam aqui: cada campo procura ao escrever.
        Machines = await _machineList.ListAsync(HttpContext.RequestAborted);
        if (ShowHistory)
        {
            await ComposeHistoryAsync(historicoReferencia, historicoMaquina, historicoProducao, historicoDesde, historicoAte);
        }

        // A FICHA DA PRODUÇÃO (?jobon_id — o job-sheet do dmo-html): os
        // fatos técnicos das ferramentas da produção e os avisos
        // informativos (a compatibilidade registada e o uso paralelo noutra
        // produção ativa — NUNCA um bloqueio), calculados PELO BACKEND pela
        // MESMA query que o endpoint do vivo usa. O MODO DUPLICAR
        // (?jobon_id=N&duplicar=1) compõe o draft da produção nova (o html:
        // o nº e as datas por preencher, as tools e a calote copiadas).
        if (OpenJobOn is { } loteJobOn)
        {
            FichaLotNotes = await _lotNotes.ForJobOnAsync(loteJobOn.JobOnId, HttpContext.RequestAborted);
            var resumo = JobOnSummaryEntry.Compose(
                loteJobOn.JobOnId,
                loteJobOn.InicioEm,
                await _changes.ForJobOnAsync(loteJobOn.JobOnId, HttpContext.RequestAborted),
                FichaLotNotes);
            var permitidas = new HashSet<string>();
            foreach (var capability in resumo.Select(e => e.ActionCapability).Distinct())
            {
                if (await _capabilities.HasAsync(capability, HttpContext.RequestAborted)) permitidas.Add(capability);
            }
            FichaResumo = JobOnSummaryEntry.ForCapabilities(resumo, permitidas.Contains);
            VistaAlteracoes = vista == "alteracoes";
        }

        if (OpenJobOn is { } eliminarJobOn && duplicar != "1")
        {
            VeControlo = await _capabilities.HasAsync(DmoCapabilities.ControloCreate, HttpContext.RequestAborted)
                || await _capabilities.HasAsync(DmoCapabilities.ControloApprove, HttpContext.RequestAborted);
            if (await _deleteJobOn.PreviewAsync(eliminarJobOn.JobOnId, today, HttpContext.RequestAborted) is { } preview)
            {
                FichaEliminar = JobOnEliminarAviso.From(preview, VeControlo);
            }
        }

        if (OpenJobOn is { } fichaJobOn)
        {
            FichaCaloteEcho = fichaJobOn.CaloteMm?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            if (duplicar == "1")
            {
                DuplicarDe = fichaJobOn.JobOnId;
                RequestedReference = fichaJobOn.Reference;
                RequestedMachineId = Machines.FirstOrDefault(m => m.Code == fichaJobOn.Machine)?.MachineId;
                RequestedCmToolId = fichaJobOn.CmToolId;
                RequestedMfToolId = fichaJobOn.MfToolId;
                RequestedBqToolId = fichaJobOn.BqToolId;
                RequestedCaloteMm = FichaCaloteEcho;
                FolhaAberta = true;
                FolhaCalote = FichaCaloteEcho;
            }
            else if (editar == "1")
            {
                // A folha em edição. Depois de uma recusa, a máquina, as
                // ferramentas, as datas e a calote são as submetidas; a
                // referência e o nº não se editam e vêm da produção.
                // Também ao voltar do «Criar CM/MF/BQ» (?retoma=1): a folha
                // volta com o que estava escrito e a ferramenta acabada de
                // criar, e o Guardar regista a troca em «O que mudou».
                FolhaAberta = true;
                RequestedReference = fichaJobOn.Reference;
                RequestedProductionNumber = fichaJobOn.ProductionNumber;
                FolhaVersao = _versaoDoEnvio
                    ?? (retoma == "1" && Ferramentas.FichaModel.ParseVersion(versao) is { } aberta
                        ? aberta
                        : fichaJobOn.Version);
                // Gravada por outra pessoa: a folha volta com o que ficou gravado.
                if ((UpdateRefusal is null or UpdateJobOnResult.ChangedByAnotherUser) && retoma != "1")
                {
                    RequestedMachineId = Machines.FirstOrDefault(m => m.Code == fichaJobOn.Machine)?.MachineId;
                    RequestedCmToolId = fichaJobOn.CmToolId;
                    RequestedMfToolId = fichaJobOn.MfToolId;
                    RequestedBqToolId = fichaJobOn.BqToolId;
                    RequestedInicioEm = fichaJobOn.InicioEm;
                    RequestedFimEm = fichaJobOn.FimEm;
                    RequestedCaloteMm = FichaCaloteEcho;
                    FolhaCalote = FichaCaloteEcho;
                }
                else
                {
                    RequestedCaloteMm = Sanitize(caloteMm);
                    FolhaCalote = RequestedCaloteMm;
                }
            }

            if (DuplicarDe is null)
            {
                var fichaToolIds = new[] { fichaJobOn.CmToolId, fichaJobOn.MfToolId, fichaJobOn.BqToolId }
                    .Where(id => id is not null)
                    .Select(id => id!)
                    .ToList();
                if (fichaToolIds.Count > 0)
                {
                    FichaFacts = await _toolFacts.FindAsync(
                        fichaToolIds, fichaJobOn.Machine, fichaJobOn.JobOnId,
                        fichaJobOn.InicioEm, fichaJobOn.FimEm, HttpContext.RequestAborted);
                }
            }
        }

        // O echo da calote submetida — apenas com recusa presente ou ao
        // voltar do «Criar CM/MF/BQ» (nunca um valor velho a prefilar um
        // form fresco).
        if ((CreateRefusal is not null || UpdateRefusal is not null || retoma == "1")
            && Sanitize(caloteMm) is { } caloteEcho)
        {
            RequestedCaloteMm = caloteEcho;
            FolhaAberta = true;
            FolhaCalote = caloteEcho;
        }

        // O [Criar Job On]: a folha abre em CRIAÇÃO (server-render). Depois
        // de uma recusa a folha reabre com o que foi submetido.
        if (novo == "1" || CreateRefusal is not null || UpdateRefusal is not null)
        {
            FolhaAberta = true;
        }

        // A FOLHA DA FÁBRICA gravada, lida uma vez para a ficha em leitura, para
        // o Editar e para o Duplicar (a ficha aparece sempre que há Job On
        // aberto, menos no Duplicar).
        var folhaGravada = OpenJobOn is { } folhaJobOn
            ? await _folha.ForJobOnAsync(folhaJobOn.JobOnId, HttpContext.RequestAborted)
            : null;
        FichaFolha = folhaGravada is null || DuplicarDe is not null ? null : FichaDaFolhaVista.DaLida(folhaGravada);

        // No formulário: o que se escreveu (depois de uma recusa que volta a
        // partir do envio), a gravada no Editar, a da origem no Duplicar (o
        // Guardar grava-a com a produção nova, na mesma escrita), ou vazia.
        if (FolhaAberta)
        {
            Folha = _envioDaFolha is { } envioDaFolha
                ? FolhaDoJobOnVista.DoEnvio(envioDaFolha, _chaveRecusada)
                : folhaGravada is null
                    ? FolhaDoJobOnVista.Vazia()
                    : FolhaEdicao
                        ? FolhaDoJobOnVista.DaLida(folhaGravada)
                        : DuplicarDe is not null
                            ? FolhaDoJobOnVista.DoDuplicar(folhaGravada)
                            : FolhaDoJobOnVista.Vazia();
        }

        // Os factos das ferramentas que a página mostra (a folha e o
        // histórico), lidos por tool_id.
        var shownToolIds = new[] { RequestedCmToolId, RequestedMfToolId, RequestedBqToolId }
            .Concat(HistoryPage.Items.SelectMany(j => new[] { j.CmToolId, j.MfToolId, j.BqToolId }))
            .Concat((ReferenceProductionsPage?.Items ?? []).SelectMany(j => new[] { j.CmToolId, j.MfToolId, j.BqToolId }))
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();
        if (shownToolIds.Count > 0)
        {
            KnownTools = await _toolCandidates.FindCandidatesAsync(
                new ToolCandidateFilter(ToolIds: shownToolIds), HttpContext.RequestAborted);
        }

        DayRows = ComporDayRows(Machines, JobOns, Year, Month, SelectedDay);

        // As mudanças de lote do dia entram na linha da máquina, no espaço
        // entre a referência e o número (o proprietário, 16/10/2026).
        var doDia = DayRows.SelectMany(r => r.Productions).Select(p => p.JobOnId).ToList();
        var notasDoDia = await _lotNotes.ForDayAsync(new DateOnly(Year, Month, SelectedDay), doDia, HttpContext.RequestAborted);
        // E as alterações de cada produção: a linha mostra qual foi (o Diogo,
        // 17/10/2026), e a ficha tem o detalhe em «O que mudou».
        var alteracoes = await _changes.AfterStartAsync(doDia, HttpContext.RequestAborted);
        if (notasDoDia.Count > 0 || alteracoes.Count > 0)
        {
            DayRows = DayRows
                .Select(r => r with
                {
                    Productions = r.Productions
                        .Select(p => p with
                        {
                            LotNotes = notasDoDia.Where(n => n.JobOnId == p.JobOnId).Select(n => n.Text).ToList(),
                            ChangeCount = alteracoes.TryGetValue(p.JobOnId, out var mudou) ? mudou.Count : 0,
                            ChangeSummary = mudou is { Count: > 0 } ? JobOnChangeFields.Summarize(mudou) : null,
                            ChangeDetails = mudou?.Select(c => c.Text).ToList() ?? []
                        })
                        .ToList()
                })
                .ToList();
        }

        // A próxima mudança em cada máquina, depois do dia escolhido (o
        // proprietário, 17/10/2026): a produção que entra a seguir ou a troca
        // de um componente da produção atual, a que vier primeiro; no mesmo
        // dia, a produção nova. Vai para a frente da linha.
        if (!NoDaySelected)
        {
            var dia = new DateOnly(Year, Month, SelectedDay);
            var proximas = await _jobOnList.ListNextPerMachineAsync(dia, HttpContext.RequestAborted);
            var trocas = await _lotNotes.NextAfterAsync(dia, doDia, HttpContext.RequestAborted);
            if (proximas.Count > 0 || trocas.Count > 0)
            {
                DayRows = DayRows
                    .Select(r =>
                    {
                        var entra = proximas.FirstOrDefault(p => p.Machine == r.MachineCode) is { InicioEm: { } inicio } p
                            ? new DMO.Alpha.Web.Presentation.NextChange(inicio, $"{p.Reference} {p.ProductionNumber}", true, JobOnId: p.JobOnId)
                            : null;
                        var troca = trocas
                            .Where(t => r.Productions.Any(x => x.JobOnId == t.JobOnId))
                            .OrderBy(t => t.Dia)
                            .Select(t => new DMO.Alpha.Web.Presentation.NextChange(t.Dia, t.Texto, false, ToolId: t.NewToolId))
                            .FirstOrDefault();
                        var next = entra is null ? troca
                            : troca is null || entra.Dia <= troca.Dia ? entra
                            : troca;
                        return next is null ? r : r with { Next = next };
                    })
                    .ToList();
            }
        }

        // O SIDEPANEL (o Owner: «o sidepanel mostra o que vai entrar»): a
        // fila das próximas entradas da SEMANA CORRENTE (o renderRail em mode
        // current-week), pela ordem em que entram.
        ProximasEntradas = ProximasEntradasDaSemana(JobOns, hoje);
        Calendar = ComporJobOnCalendar(Year, Month, JobOns, NoDaySelected ? null : SelectedDay);
        return Page();
    }

    private static DateOnly hoje => DateOnly.FromDateTime(DmoBusinessTime.Now);

    /// <summary>
    /// O sidepanel: primeiro as produções em curso (início antes de hoje, sem
    /// fim ou com fim de hoje em diante), depois as que entram de hoje até ao
    /// domingo da semana corrente (Seg..Dom, como o calendário). O que entra
    /// depois de domingo fica para a semana seguinte.
    /// </summary>
    public static IReadOnlyList<ProductionContextReadModel> ProximasEntradasDaSemana(
        IEnumerable<ProductionContextReadModel> jobOns,
        DateOnly dia)
    {
        var domingo = dia.AddDays((7 - (int)dia.DayOfWeek) % 7);
        // As produções em curso (começaram antes de hoje e ainda não acabaram)
        // vêm primeiro: as que já corriam quando se lançaram na app também
        // aparecem (Diogo, 22/10/2026: «se criar um jobon passado não aparece
        // na lista ao lado do calendário»).
        var emCurso = jobOns
            .Where(j => j.InicioEm is { } inicio && inicio < dia && (j.FimEm is not { } fim || fim >= dia))
            .OrderBy(j => j.InicioEm);
        var entradas = jobOns
            .Where(j => j.InicioEm is { } inicioEntrada && inicioEntrada >= dia && inicioEntrada <= domingo)
            .OrderBy(j => j.InicioEm);
        return emCurso.Concat(entradas).ToList();
    }

    /// <summary>
    /// POST nativo: criar um Job On. O browser submete os factos da
    /// produção (nº, referência, máquina) e os tool_ids canónicos
    /// explicitamente selecionados por slot; um slot sem seleção é não
    /// aplicável. O backend emite todas as identidades (jobon_id e os
    /// cm_id/mf_id/bq_id aplicáveis). Sucesso REDIRECIONA AO RESUMO da
    /// produção (o comportamento canónico submit do js — o Guardar
    /// leva ao resumo); a recusa é tipada e volta por query com os
    /// valores já introduzidos preservados. A seam de conta é exigida
    /// pelo [Authorize] da página: sem sessão autenticada o pedido é
    /// desafiado antes de qualquer mutação.
    /// </summary>
    /// <summary>
    /// A procura ao escrever (?handler=ProcurarProducao&amp;q=), em dois campos
    /// (Diogo, 17/10/2026: «ao escrever a referência na primeira, a segunda mostra
    /// produção e máquina»; sem lista por baixo do campo): quando o texto é uma
    /// referência que existe, devolve o dropdown com as últimas
    /// <see cref="IProductionPickerQuery.ProductionsOfReferenceLimit"/> produções
    /// dela; senão o dropdown vazio e desativado. &amp;procura_ref= é o mesmo.
    /// </summary>
    public async Task<IActionResult> OnGetProcurarProducaoAsync(
        [FromQuery(Name = "q")] string? search,
        [FromQuery(Name = "procura_ref")] string? procuraRef,
        [FromQuery(Name = "ano")] int? ano,
        [FromQuery(Name = "mes")] int? mes,
        [FromQuery(Name = "dia")] int? dia)
    {
        var resultados = await ProcurarProducoesAsync(search, procuraRef, ano, mes, dia);
        return Partial("_JobOnProcuraProducoes", resultados);
    }

    private async Task<JobOnProcura> ProcurarProducoesAsync(string? search, string? procuraRef, int? ano, int? mes, int? dia)
    {
        // Só a referência completa (sem distinção de maiúsculas); o início fica para as sugestões.
        var referencia = (string.IsNullOrWhiteSpace(procuraRef) ? search : procuraRef)?.Trim() ?? string.Empty;
        var producoes = referencia.Length == 0
            ? Array.Empty<ProductionSearchItem>()
            : await _productionPicker.ListOfReferenceAsync(referencia, HttpContext.RequestAborted);
        // Escolher uma produção abre a folha no mesmo mês e dia.
        return new JobOnProcura(
            referencia,
            producoes.Select(item => new JobOnProcuraLinha(
                    item,
                    Url.Page("/JobOn/Index", null, new { ano, mes, dia, jobon_id = item.JobOnId }, null, null, "lightFicha")!))
                .ToList());
    }

    /// <summary>
    /// A procura de um campo de ferramenta (CM, MF ou BQ) enquanto se escreve.
    /// Só leitura e no máximo <see cref="ToolSuggestionLimit"/> sugestões.
    /// Sem «Mostrar todas», os dados da folha filtram: a máquina e a
    /// referência da produção; na CM, com uma MF escolhida, a máquina e a
    /// referência dessa MF (se nada corresponder, volta à referência da
    /// produção). Na MF, com uma CM escolhida, só as MF que trabalham com
    /// essa CM: a referência MF associada da CM ou, sem ela, a referência
    /// da CM; com máquina, também só as dessa máquina (o Diogo, 17/10/2026).
    /// Com «Mostrar todas», procura no catálogo inteiro. Nunca muda a
    /// ferramenta já escolhida.
    /// </summary>
    public async Task<IActionResult> OnGetToolCandidatesAsync(
        [FromQuery(Name = "type")] string? type,
        [FromQuery(Name = "q")] string? search,
        [FromQuery(Name = "machine_id")] int? machineId,
        [FromQuery(Name = "reference")] string? reference,
        [FromQuery(Name = "mf_tool_id")] string? mfToolId,
        [FromQuery(Name = "cm_tool_id")] string? cmToolId,
        [FromQuery(Name = "all")] bool showAll = false)
    {
        ToolType? toolType = Sanitize(type)?.ToUpperInvariant() switch
        {
            "CM" => ToolType.Cm,
            "MF" => ToolType.Mf,
            "BQ" => ToolType.Bq,
            _ => null
        };
        if (toolType is not { } slot)
        {
            return BadRequest();
        }

        var term = Sanitize(search);
        ToolCandidatePage? page = null;
        string filtro;
        if (showAll)
        {
            filtro = "Catálogo completo";
            page = await _toolCandidates.FindPageAsync(
                new ToolCandidateFilter(Type: slot, Search: term), 1, ToolSuggestionLimit, HttpContext.RequestAborted);
        }
        else
        {
            filtro = machineId is null ? "Filtrado pela referência" : "Filtrado por máquina e referência";
            if (slot == ToolType.Cm && Sanitize(mfToolId) is { } mfId)
            {
                var mfReference = await _db.Tools.AsNoTracking()
                    .Where(tool => tool.ToolId == mfId && tool.Type == ToolType.Mf)
                    .Select(tool => tool.Reference)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);
                if (mfReference is not null)
                {
                    var byMf = await _toolCandidates.FindPageAsync(
                        new ToolCandidateFilter(Type: slot, MachineId: machineId, MfReference: mfReference, Search: term),
                        1, ToolSuggestionLimit, HttpContext.RequestAborted);
                    if (byMf.TotalCount > 0)
                    {
                        page = byMf;
                        filtro = (machineId is null ? "Filtrado pela MF " : "Filtrado por máquina e pela MF ") + mfReference;
                    }
                }
            }
            if (slot == ToolType.Mf && Sanitize(cmToolId) is { } cmId)
            {
                // Sem volta à referência da produção: com a CM escolhida, a
                // lista mostra só as MF que encaixam nela.
                var cmMfReference = await _db.Tools.AsNoTracking()
                    .Where(tool => tool.ToolId == cmId && tool.Type == ToolType.Cm)
                    .Select(tool => tool.AssociatedMfReference ?? tool.Reference)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);
                if (cmMfReference is not null)
                {
                    page = await _toolCandidates.FindPageAsync(
                        new ToolCandidateFilter(Type: slot, MachineId: machineId, MfReference: cmMfReference, Search: term),
                        1, ToolSuggestionLimit, HttpContext.RequestAborted);
                    filtro = (machineId is null ? "Filtrado pela CM " : "Filtrado por máquina e pela CM ") + cmMfReference;
                }
            }
            page ??= await _toolCandidates.FindPageAsync(
                new ToolCandidateFilter(Type: slot, MachineId: machineId, ProductionReference: Sanitize(reference), Search: term),
                1, ToolSuggestionLimit, HttpContext.RequestAborted);
        }

        return new JsonResult(new { items = page.Items, total = page.TotalCount, filtro });
    }

    public async Task<IActionResult> OnPostCreateAsync(
        [FromForm(Name = "production_number")] string? productionNumber,
        [FromForm(Name = "reference")] string? reference,
        [FromForm(Name = "machine_id")] string? machineId,
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "mf_tool_id")] string? mfToolId,
        [FromForm(Name = "bq_tool_id")] string? bqToolId,
        [FromForm(Name = "inicio_em")] string? inicioEm,
        [FromForm(Name = "fim_em")] string? fimEm,
        [FromForm(Name = "calote_mm")] string? caloteMm)
    {
        var sanitizedProductionNumber = Sanitize(productionNumber);
        var sanitizedReference = Sanitize(reference)?.ToUpperInvariant();
        var sanitizedCmToolId = Sanitize(cmToolId);
        var sanitizedMfToolId = Sanitize(mfToolId);
        var sanitizedBqToolId = Sanitize(bqToolId);

        // A JANELA TEMPORAL (o corte B): os dias em que a produção
        // entra/sai de máquina — inputs date nativos, DateOnly no
        // comando; sem valor = a janela fica em aberto.
        var parsedInicioEm = DateOnly.TryParse(inicioEm, out var inicioValue) ? inicioValue : (DateOnly?)null;
        var parsedFimEm = DateOnly.TryParse(fimEm, out var fimValue) ? fimValue : (DateOnly?)null;

        // A máquina chega como texto do select: um valor não utilizável não
        // resolve âncora canónica nenhuma (MachineId 0 nunca existe) e a
        // recusa tipada explica — nunca é auto-selecionada outra máquina.
        var parsedMachineId = int.TryParse(machineId?.Trim(), out var machineIdValue)
            ? machineIdValue
            : 0;

        // A CALOTE DESTA PRODUÇÃO (o campo j-calote da ficha): o parse
        // FLEXÍVEL (ponto OU vírgula); vazia = a referência da Tool manda.
        // Ilegível ou negativa já não recusa o Job On (decisão do
        // proprietário, 16/10/2026): grava sem calote e o aviso diz qual foi
        // o valor ignorado (0 é válido).
        var parsedCalote = ParseCalote(caloteMm, out var caloteIgnorada);

        // O fim antes do início grava e a folha avisa (Diogo, 19/10/2026).
        var fimAntesDoInicio = parsedInicioEm is { } inicioDia && parsedFimEm is { } fimDia && fimDia < inicioDia;

        // Um Job On tem sempre CM, BQ e MF (Diogo, 22/10/2026: bloqueio
        // autorizado). Sem MF, a recusa do MF (a do handler) vem primeiro.
        if (sanitizedMfToolId is not null && MissingCmOrBq(sanitizedCmToolId, sanitizedBqToolId) is { } faltaCriar)
        {
            return CreateRefused(
                sanitizedProductionNumber, sanitizedReference, parsedMachineId,
                sanitizedCmToolId, sanitizedMfToolId, sanitizedBqToolId,
                parsedInicioEm, parsedFimEm,
                faltaCriar == "CM" ? CreateJobOnResult.MissingCmTool : CreateJobOnResult.MissingBqTool,
                caloteMm);
        }

        // A FOLHA DA FÁBRICA: os campos escritos e as linhas CAL do envio.
        var envio = LerEnvioDaFolha();

        var outcome = await _createJobOn.HandleAsync(
            new CreateJobOn(
                sanitizedProductionNumber ?? string.Empty,
                sanitizedReference ?? string.Empty,
                parsedMachineId,
                sanitizedCmToolId,
                sanitizedMfToolId,
                sanitizedBqToolId,
                parsedInicioEm,
                parsedFimEm,
                parsedCalote,
                envio.Folha,
                envio.Cal),
            HttpContext.RequestAborted);

        if (outcome.Result == CreateJobOnResult.Success)
        {
            // O OVERLAP NÃO BLOQUEIA (o Owner: «overlap da maquina deve
            // dar um warning, nao deve bloquear»): a produção está
            // GRAVADA — com sobreposição na máquina ou com a calote
            // ignorada, o redirect fica na FOLHA do Job On para o aviso
            // amarelo vestir a página; sem aviso, o GUARDAR LEVA AO RESUMO
            // (o comportamento canónico submit do js) a quem tem o
            // Controlo Criar.
            return await SavedRedirectAsync(
                outcome.JobOn!.JobOnId,
                outcome.OverlapWarning is not null,
                caloteIgnorada,
                fimAntesDoInicio);
        }

        // Duplo clique (Diogo, 18/10/2026): o segundo envio chega com os
        // mesmos dados que o primeiro acabou de gravar. Não é uma produção
        // repetida: abre-se a que ficou gravada, como se fosse um clique.
        if (outcome.Result == CreateJobOnResult.DuplicateProductionNumber
            && await IdenticalJobOnIdAsync(
                sanitizedReference, sanitizedProductionNumber, parsedMachineId,
                sanitizedCmToolId, sanitizedMfToolId, sanitizedBqToolId,
                parsedInicioEm, parsedFimEm, parsedCalote, envio) is int gravado)
        {
            return await SavedRedirectAsync(gravado, false, caloteIgnorada, fimAntesDoInicio);
        }

        // A FOLHA ESCRITA NÃO SE PERDE: o texto longo demais, ou outra recusa
        // com a folha escrita, volta à página a partir do envio (como no
        // DMO-FULL), com tudo o que se escreveu; o resto volta como antes,
        // pelo endereço.
        if (outcome.Result == CreateJobOnResult.TextoLongo || envio.TemEscrito)
        {
            _envioDaFolha = envio;
            _chaveRecusada = outcome.ChaveRecusada;
            return await OnGetAsync(
                jobOnId: null, createRefusal: outcome.Result.ToString(), updateRefusal: null,
                productionNumber: sanitizedProductionNumber, reference: sanitizedReference,
                machineId: parsedMachineId > 0 ? parsedMachineId : null,
                cmToolId: sanitizedCmToolId, mfToolId: sanitizedMfToolId, bqToolId: sanitizedBqToolId,
                inicioEm: parsedInicioEm?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                fimEm: parsedFimEm?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                historico: null, ano: null, mes: null, overlap: null, diaSelected: null,
                procura: null, procuraRef: null, producoesRef: null, duplicar: null,
                caloteMm: caloteMm, caloteIgnorada: null, fimAntesInicio: null,
                editar: null, novo: "1", retoma: null, versao: null,
                historicoReferencia: null, historicoMaquina: null, historicoProducao: null,
                historicoDesde: null, historicoAte: null);
        }

        return CreateRefused(
            sanitizedProductionNumber,
            sanitizedReference,
            parsedMachineId,
            sanitizedCmToolId,
            sanitizedMfToolId,
            sanitizedBqToolId,
            parsedInicioEm,
            parsedFimEm,
            outcome.Result,
            caloteMm);
    }

    /// <summary>
    /// O Job On com esta referência e nº que tem exatamente os mesmos dados
    /// (máquina, ferramentas, datas, calote e folha da fábrica); null quando
    /// algum difere.
    /// </summary>
    private async Task<int?> IdenticalJobOnIdAsync(
        string? reference, string? productionNumber, int machineId,
        string? cmToolId, string? mfToolId, string? bqToolId,
        DateOnly? inicioEm, DateOnly? fimEm, decimal? caloteMm, EnvioDaFolha envio)
    {
        if (string.IsNullOrEmpty(reference) || string.IsNullOrEmpty(productionNumber))
        {
            return null;
        }

        var existing = await _db.JobOns
            .AsNoTracking()
            .Where(j => j.ProductionNumber == productionNumber && j.Reference.ToUpper() == reference)
            .Select(j => new
            {
                j.Id, j.MachineId, j.InicioEm, j.FimEm, j.CaloteMm,
                Cm = j.CmContext == null ? null : j.CmContext.ToolId,
                Mf = j.MfContext == null ? null : j.MfContext.ToolId,
                Bq = j.BqContext == null ? null : j.BqContext.ToolId
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (existing is null
            || existing.MachineId != machineId
            || existing.Cm != cmToolId || existing.Mf != mfToolId || existing.Bq != bqToolId
            || existing.InicioEm != inicioEm || existing.FimEm != fimEm
            || existing.CaloteMm != caloteMm)
        {
            return null;
        }

        var folhaGravada = await _folha.ForJobOnAsync(existing.Id, HttpContext.RequestAborted);
        return JobOnFolhaRegras.Alteracoes(
                folhaGravada,
                envio.Folha is null ? null : JobOnFolhaRegras.NormalizarFolha(envio.Folha),
                envio.Cal?.Select(JobOnFolhaRegras.NormalizarLinha).ToList()).Count == 0
            ? existing.Id
            : null;
    }

    /// <summary>A folha da fábrica do envio do formulário (sem formulário, nada da folha).</summary>
    private EnvioDaFolha LerEnvioDaFolha() =>
        Request.HasFormContentType ? EnvioDaFolha.Ler(Request.Form) : new EnvioDaFolha(null, null);

    /// <summary>
    /// POST nativo: EDITAR um Job On existente — a produção re-planeada
    /// (a janela temporal, a linha e as tools) SEM criar outra produção.
    /// O browser submete o jobon_edit_id (a folha em modo edição: o nº de
    /// produção e a referência vão read-only e o POST não os precisa — a
    /// identidade é imutável na edição), a máquina e os tool_ids
    /// canônicos explícitos por slot. Sucesso REDIRECIONA AO RESUMO da
    /// MESMA produção (o padrão do Create); a recusa é tipada e volta
    /// por query com os valores submetidos preservados e o jobon_id — a
    /// folha reabre em modo edição nos valores que o utilizador tentou
    /// guardar. A seam de conta é exigida pelo [Authorize] da página.
    /// </summary>
    public async Task<IActionResult> OnPostUpdateAsync(
        [FromForm(Name = "jobon_edit_id")] string? jobOnEditId,
        [FromForm(Name = "machine_id")] string? machineId,
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "mf_tool_id")] string? mfToolId,
        [FromForm(Name = "bq_tool_id")] string? bqToolId,
        [FromForm(Name = "inicio_em")] string? inicioEm,
        [FromForm(Name = "fim_em")] string? fimEm,
        [FromForm(Name = "calote_mm")] string? caloteMm,
        [FromForm(Name = "versao")] string? versao)
    {
        var sanitizedCmToolId = Sanitize(cmToolId);
        var sanitizedMfToolId = Sanitize(mfToolId);
        var sanitizedBqToolId = Sanitize(bqToolId);

        // A janela temporal da produção re-planeada — os mesmos inputs
        // date nativos do Create; sem valor = a janela em aberto.
        var parsedInicioEm = DateOnly.TryParse(inicioEm, out var inicioValue) ? inicioValue : (DateOnly?)null;
        var parsedFimEm = DateOnly.TryParse(fimEm, out var fimValue) ? fimValue : (DateOnly?)null;

        // A máquina chega como texto do select (o padrão do Create): um
        // valor não utilizável não resolve âncora canônica nenhuma.
        var parsedMachineId = int.TryParse(machineId?.Trim(), out var machineIdValue)
            ? machineIdValue
            : 0;

        // O jobon a editar: um valor não utilizável não resolve produção
        // nenhuma — a recusa tipada JobOnNotFound explica (limpa, nunca
        // um 500, nunca uma linha inventada).
        var parsedJobOnId = int.TryParse(jobOnEditId?.Trim(), out var jobOnIdValue)
            ? jobOnIdValue
            : 0;

        // A CALOTE DESTA PRODUÇÃO (o campo j-calote da ficha — editável):
        // o mesmo parse flexível do Create; vazia = limpar (a referência
        // da Tool manda). Ilegível ou negativa grava o resto da edição,
        // MANTÉM a calote já gravada (não se perde um dado por um erro de
        // escrita) e avisa qual foi o valor ignorado (decisão do
        // proprietário, 16/10/2026). O handler recebe o valor guardado,
        // que já é válido.
        var parsedCalote = ParseCalote(caloteMm, out var caloteIgnorada);
        if (caloteIgnorada is not null)
        {
            parsedCalote = await _db.JobOns
                .AsNoTracking()
                .Where(j => j.Id == parsedJobOnId)
                .Select(j => j.CaloteMm)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
        }

        if (sanitizedMfToolId is not null && MissingCmOrBq(sanitizedCmToolId, sanitizedBqToolId) is { } faltaEditar)
        {
            return UpdateRefused(
                parsedJobOnId, parsedMachineId,
                sanitizedCmToolId, sanitizedMfToolId, sanitizedBqToolId,
                parsedInicioEm, parsedFimEm,
                faltaEditar == "CM" ? UpdateJobOnResult.MissingCmTool : UpdateJobOnResult.MissingBqTool,
                caloteMm);
        }

        // A FOLHA DA FÁBRICA: os campos escritos e as linhas CAL do envio.
        var envio = LerEnvioDaFolha();
        var versaoAberta = Ferramentas.FichaModel.ParseVersion(versao);

        var outcome = await _updateJobOn.HandleAsync(
            new UpdateJobOn(
                parsedJobOnId,
                parsedMachineId,
                sanitizedCmToolId,
                sanitizedMfToolId,
                sanitizedBqToolId,
                parsedInicioEm,
                parsedFimEm,
                parsedCalote,
                CurrentActorName(),
                versaoAberta,
                envio.Folha,
                envio.Cal),
            HttpContext.RequestAborted);

        if (outcome.Result == UpdateJobOnResult.Success)
        {
            // O OVERLAP NÃO BLOQUEIA (a edição gravou na mesma): o mesmo
            // destino do Create; o jobon_id não muda na edição. O fim antes
            // do início também grava e a folha avisa (Diogo, 19/10/2026).
            return await SavedRedirectAsync(
                outcome.JobOn!.JobOnId,
                outcome.OverlapWarning is not null,
                caloteIgnorada,
                parsedInicioEm is { } inicioDia && parsedFimEm is { } fimDia && fimDia < inicioDia);
        }

        // Gravada por outra pessoa: reabre a edição com o que ficou gravado
        // (os valores escritos não voltam, para não parecerem os gravados).
        if (outcome.Result == UpdateJobOnResult.ChangedByAnotherUser)
        {
            return RedirectToPage("/JobOn/Index", new
            {
                jobon_id = parsedJobOnId,
                editar = "1",
                update_refusal = outcome.Result.ToString()
            });
        }

        // A FOLHA ESCRITA NÃO SE PERDE: o texto longo demais, ou outra recusa
        // com a folha escrita, volta à página a partir do envio, na edição e
        // com a versão com que abriu; o resto volta como antes, pelo endereço.
        if (parsedJobOnId > 0
            && outcome.Result != UpdateJobOnResult.JobOnNotFound
            && (outcome.Result == UpdateJobOnResult.TextoLongo || envio.TemEscrito))
        {
            _envioDaFolha = envio;
            _chaveRecusada = outcome.ChaveRecusada;
            _versaoDoEnvio = versaoAberta;
            return await OnGetAsync(
                jobOnId: parsedJobOnId, createRefusal: null, updateRefusal: outcome.Result.ToString(),
                productionNumber: null, reference: null,
                machineId: parsedMachineId > 0 ? parsedMachineId : null,
                cmToolId: sanitizedCmToolId, mfToolId: sanitizedMfToolId, bqToolId: sanitizedBqToolId,
                inicioEm: parsedInicioEm?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                fimEm: parsedFimEm?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                historico: null, ano: null, mes: null, overlap: null, diaSelected: null,
                procura: null, procuraRef: null, producoesRef: null, duplicar: null,
                caloteMm: caloteMm, caloteIgnorada: null, fimAntesInicio: null,
                editar: "1", novo: null, retoma: null, versao: null,
                historicoReferencia: null, historicoMaquina: null, historicoProducao: null,
                historicoDesde: null, historicoAte: null);
        }

        return UpdateRefused(
            parsedJobOnId,
            parsedMachineId,
            sanitizedCmToolId,
            sanitizedMfToolId,
            sanitizedBqToolId,
            parsedInicioEm,
            parsedFimEm,
            outcome.Result,
            caloteMm);
    }

    /// <summary>
    /// POST do «Duplicar → Lote»: regista a mudança de lote como nota da
    /// produção, sem mudar o Job On. Sucesso volta ao dia da mudança, onde a
    /// nota aparece na linha da máquina; a recusa reabre o formulário.
    /// </summary>
    /// <summary>O nome de quem edita: o nome do utilizador, senão o nº de operador, senão o email.</summary>
    private string? CurrentActorName() => ActorName(User);

    internal static string? ActorName(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(DmoClaimTypes.DisplayName)?.Value is { Length: > 0 } name
            ? name
            : user.FindFirst(DmoClaimTypes.OperatorId)?.Value
                ?? user.FindFirst(DmoClaimTypes.Email)?.Value;

    /// <summary>
    /// O destino depois de GRAVAR um Job On: a folha da produção gravada, no
    /// Planeamento, com os avisos (sobreposição na linha, calote ignorada ou
    /// fim antes do início) quando os há.
    /// </summary>
    private Task<IActionResult> SavedRedirectAsync(int jobOnId, bool overlap, string? caloteIgnorada, bool fimAntesDoInicio)
    {
        // Fica sempre no Job On do Planeamento (Diogo, 22/10/2026): gravar
        // já não leva ao Resumo do Controlo.
        return Task.FromResult<IActionResult>(RedirectToPage("/JobOn/Index", new
        {
            jobon_id = jobOnId,
            overlap = overlap ? "1" : null,
            calote_ignorada = caloteIgnorada,
            fim_antes_inicio = fimAntesDoInicio ? "1" : null
        }));
    }

    /// <summary>«CM» ou «BQ» quando falta essa ferramenta; null quando estão as duas.</summary>
    internal static string? MissingCmOrBq(string? cmToolId, string? bqToolId) =>
        cmToolId is null ? "CM" : bqToolId is null ? "BQ" : null;

    private RedirectToPageResult CreateRefused(
        string? productionNumber,
        string? reference,
        int machineId,
        string? cmToolId,
        string? mfToolId,
        string? bqToolId,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        CreateJobOnResult result,
        string? caloteMm = null)
    {
        // Os valores introduzidos voltam por query para o formulário se
        // reabrir no estado em que estavam (preservação de estado — a
        // auditoria do Codex: o form nunca volta vazio).
        return RedirectToPage("/JobOn/Index", new
        {
            production_number = productionNumber,
            reference,
            machine_id = machineId > 0 ? machineId : (int?)null,
            cm_tool_id = cmToolId,
            mf_tool_id = mfToolId,
            bq_tool_id = bqToolId,
            inicio_em = inicioEm?.ToString("yyyy-MM-dd"),
            fim_em = fimEm?.ToString("yyyy-MM-dd"),
            calote_mm = caloteMm,
            create_refusal = result.ToString()
        });
    }

    private RedirectToPageResult UpdateRefused(
        int jobOnId,
        int machineId,
        string? cmToolId,
        string? mfToolId,
        string? bqToolId,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        UpdateJobOnResult result,
        string? caloteMm = null)
    {
        // Os valores submetidos voltam por query com o jobon_id pedido:
        // a folha reabre em MODO EDIÇÃO nos valores que o utilizador
        // tentou guardar (a janela temporal incluída — a edição mexe
        // nela), nunca nos persistidos que a recusa impediu de gravar.
        // O JobOnNotFound não leva jobon_id: a produção não existe —
        // não há folha de edição a reabrer (o banner tipado explica).
        var reabre = jobOnId > 0 && result != UpdateJobOnResult.JobOnNotFound;
        return RedirectToPage("/JobOn/Index", new
        {
            jobon_id = reabre ? jobOnId : (int?)null,
            editar = reabre ? "1" : null,
            machine_id = machineId > 0 ? machineId : (int?)null,
            cm_tool_id = cmToolId,
            mf_tool_id = mfToolId,
            bq_tool_id = bqToolId,
            inicio_em = inicioEm?.ToString("yyyy-MM-dd"),
            fim_em = fimEm?.ToString("yyyy-MM-dd"),
            calote_mm = caloteMm,
            update_refusal = result.ToString()
        });
    }

    /// <summary>
    /// O parse FLEXÍVEL da calote (ponto OU vírgula — a oficina): vazia =
    /// null (a referência da Tool manda); presente mas ilegível ou negativa
    /// também é null e devolve em <paramref name="ignorada"/> o texto
    /// escrito, para o aviso (0 é válido: calote plana). O handler continua
    /// a recusar uma calote negativa se for chamado diretamente; a página
    /// nunca lha envia.
    /// </summary>
    private static decimal? ParseCalote(string? caloteMm, out string? ignorada)
    {
        ignorada = null;
        var raw = Sanitize(caloteMm);
        if (raw is null)
        {
            return null;
        }

        // Decimal inputs accept either separator, but never grouping. The
        // default Number style treats "2,5" as 25 in InvariantCulture.
        if (decimal.TryParse(raw.Replace(',', '.'),
            System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            if (parsed < 0m)
            {
                ignorada = raw;
                return null;
            }

            return parsed;
        }

        ignorada = raw;
        return null;
    }

    /// <summary>
    /// O PREVIEW dos fatos das ferramentas na escolha — ENDPOINT READ-ONLY
    /// DO BACKEND (o padrão da B1): o showToolFacts do dmo-html ao vivo —
    /// os fatos técnicos das tools selecionadas (o processo, os volumes, o
    /// gargalo, os valores TP — o que o peso/pegamentos consomem) e os
    /// avisos INFORMATIVOS (a compatibilidade registada e o uso paralelo
    /// noutra produção nas mesmas datas) calculados pela MESMA query do render do
    /// servidor. O browser nunca calcula regra de domínio; a escolha
    /// nunca é bloqueada. Zero escrita.
    /// </summary>
    public async Task<IActionResult> OnGetToolFactsAsync(
        [FromQuery(Name = "cm_tool_id")] string? cmToolId,
        [FromQuery(Name = "mf_tool_id")] string? mfToolId,
        [FromQuery(Name = "bq_tool_id")] string? bqToolId,
        [FromQuery(Name = "machine_id")] int? machineId,
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "inicio_em")] DateOnly? inicioEm,
        [FromQuery(Name = "fim_em")] DateOnly? fimEm)
    {
        var machineCode = machineId is int id
            ? await _db.Machines
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => (string?)m.Code)
                .FirstOrDefaultAsync(HttpContext.RequestAborted)
            : null;

        var toolIds = new[] { Sanitize(cmToolId), Sanitize(mfToolId), Sanitize(bqToolId) }
            .Where(toolId => toolId is not null)
            .Select(toolId => toolId!)
            .ToList();

        var facts = toolIds.Count == 0
            ? Array.Empty<JobOnToolFacts>()
            : await _toolFacts.FindAsync(toolIds, machineCode, jobOnId, inicioEm, fimEm, HttpContext.RequestAborted);

        // Os avisos com os links para onde se resolvem, só os que esta
        // pessoa pode abrir (o mesmo que o partial _DmoAvisoLink faz no
        // servidor).
        var capabilities = await _capabilities.GetAsync(HttpContext.RequestAborted);
        var resposta = new System.Text.Json.Nodes.JsonArray();
        foreach (var fact in facts)
        {
            var node = JsonSerializer.SerializeToNode(fact, ScriptJsonOptions)!.AsObject();
            node["avisos"] = JsonSerializer.SerializeToNode(
                AvisosDasFerramentas(fact).Select(aviso => new
                {
                    texto = aviso.Texto,
                    links = aviso.Links
                        .Where(link => link.PodeAbrir(capabilities))
                        .Select(link => new { texto = link.Texto, href = link.Href })
                }),
                ScriptJsonOptions);
            resposta.Add(node);
        }

        return new JsonResult(resposta);
    }

    /// <summary>
    /// Os avisos de uma ferramenta escolhida na folha, cada um com os links
    /// para onde se resolve (Diogo, 19/10/2026): a compatibilidade leva à
    /// ficha da ferramenta (as máquinas registadas), o uso paralelo a cada
    /// uma das outras produções, o Armazém (não registada ou sem estado) aos
    /// movimentos da ferramenta. Abrem noutro separador: a folha pode estar
    /// por gravar.
    /// </summary>
    public static IReadOnlyList<JobOnToolAviso> AvisosDasFerramentas(JobOnToolFacts fact)
    {
        var avisos = new List<JobOnToolAviso>();
        if (fact.AvisoCompatibilidade is { } compatibilidade)
        {
            avisos.Add(new JobOnToolAviso(compatibilidade,
            [
                Shared.DmoAvisoLink.FichaFerramenta(fact.ToolId, $"Abrir a ficha da {fact.Type}") with { NovoSeparador = true }
            ]));
        }

        if (fact.AvisoUsoParalelo is { } paralelo)
        {
            avisos.Add(new JobOnToolAviso(paralelo, fact.UsosParalelos
                .Select(uso => Shared.DmoAvisoLink.JobOn(uso.JobOnId, $"Abrir {uso.ProductionNumber} ({uso.Machine})") with { NovoSeparador = true })
                .ToList()));
        }

        if (fact.AvisoArmazem is { } armazem)
        {
            avisos.Add(new JobOnToolAviso(armazem,
            [
                Shared.DmoAvisoLink.FichaFerramenta(fact.ToolId, "Registar na ferramenta") with { NovoSeparador = true }
            ]));
        }

        return avisos;
    }

    private static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

/// <summary>Um aviso de uma ferramenta da folha do Job On e os links para onde se resolve.</summary>
public sealed record JobOnToolAviso(string Texto, IReadOnlyList<DMO.Alpha.Web.Pages.Shared.DmoAvisoLink> Links);

/// <summary>
/// A procura ao escrever no Job On: a referência escrita e as últimas
/// produções dela (vazio quando a referência não existe).
/// </summary>
public sealed record JobOnProcura(
    string Referencia,
    IReadOnlyList<JobOnProcuraLinha> Linhas)
{
    /// <summary>Sem procura: o dropdown vazio e desativado.</summary>
    public static JobOnProcura Vazia { get; } = new(string.Empty, Array.Empty<JobOnProcuraLinha>());
}

/// <summary>Uma produção encontrada e o endereço da folha que um clique abre.</summary>
public sealed record JobOnProcuraLinha(ProductionSearchItem Item, string Abrir);

/// <summary>
/// O formato das listas de produções do Job On (a procura ao escrever e o
/// Histórico): as datas e as ferramentas escritas da mesma maneira.
/// </summary>
public static class JobOnProcuraFormato
{
    /// <summary>«02/03/2026 – 09/03/2026», só o início, ou «Sem dia associado».</summary>
    public static string Datas(DateOnly? inicio, DateOnly? fim) => inicio switch
    {
        null => "Sem dia associado",
        { } i when fim is { } f => $"{i:dd/MM/yyyy} – {f:dd/MM/yyyy}",
        { } i => $"{i:dd/MM/yyyy}"
    };

    /// <summary>«5447 · lote 2»; sem lote, só a referência.</summary>
    public static string Ferramenta(string reference, string? lot) =>
        string.IsNullOrWhiteSpace(lot) ? reference : $"{reference} · lote {lot}";
}

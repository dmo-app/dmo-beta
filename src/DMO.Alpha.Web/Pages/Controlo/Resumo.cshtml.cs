using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// Surface nativo do Resumo do Controlo (nome canónico; «Folha» é o nome
/// legado da MESMA superfície — nunca uma identidade separada): a
/// superfície frontend do controlo_id de uma produção. Browser «Procurar
/// resumo» com filtro por referência de IGUALDADE EXATA aplicada no
/// servidor (query purpose-specific IResumoProductionQuery; o
/// contains/auto-open do mockup é regra de protótipo, não copiada) e
/// tabela de produções (jobon_id, nº, referência, máquina; recentes
/// primeiro) com «Abrir» — a produção é sempre EXPLICITAMENTE selecionada
/// pelo link GET ?jobon_id=N (padrão «Abrir» da página Job On). A folha
/// da produção aberta mostra o contexto pela query canónica EXISTENTE
/// IProductionContextQuery (a superfície que o Peso production-scoped
/// consumirá) e assegura o controlo_id: get-or-create idempotente por
/// jobon_id (abertura = navegação; ensure = bookkeeping idempotente, NUNCA
/// um ato de criação do utilizador; o controlo_id já nasce na criação do
/// Job On — o ensure cobre o backfill de produções pré-corte). Estado do
/// documento: OK/NOK e comentário por área CM/BQ/MF/PU/CS, registados pelo
/// operador; sem registo a área fica «não registado» (nenhum OK/NOK
/// inventado); datas «where available»: o Job On não tem datas,
/// portanto não são mostradas. Mutação nativa exige sessão autenticada
/// ([Authorize]: o anónimo é desafiado ANTES de qualquer leitura/escrita).
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class ResumoModel : PageModel
{
    private readonly IResumoProductionQuery _resumoProductions;
    private readonly IProductionContextQuery _productionContext;
    private readonly IEnsureControloForJobOnHandler _ensureControlo;
    private readonly ShellStateFactory _shellState;
    private readonly ControloViewingHandler _viewingHandler;
    private readonly IToolLookupQuery _toolLookup;
    private readonly ResumoPdfDocumentBuilder _pdfDocument;
    private readonly DMO.Alpha.Core.Controlo.IPegamentosQuery _pegamentosQuery;
    private readonly DMO.Alpha.Core.Controlo.IPesoQuery _pesoQuery;
    private readonly DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler _approvalHandler;
    private readonly DMO.Alpha.Infrastructure.Controlo.ControloAreaStateHandler _areaStateHandler;
    private readonly IPesoAssociationQuery _pesoAssociation;
    private readonly DMO.Alpha.Infrastructure.Controlo.PuncoesHandler _puncoesHandler;
    private readonly DMO.Alpha.Infrastructure.Production.JobOnChangeQuery _changes;
    private readonly CurrentUserCapabilities _capabilities;
    private readonly DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks _mcaliper;
    private readonly IControloRevisaoQuery _revisaoQuery;
    private readonly PesoProducaoAnteriorQuery _producaoAnteriorQuery;

    public ResumoModel(
        IResumoProductionQuery resumoProductions,
        IProductionContextQuery productionContext,
        IEnsureControloForJobOnHandler ensureControlo,
        ShellStateFactory shellState,
        ControloViewingHandler viewingHandler,
        IToolLookupQuery toolLookup,
        ResumoPdfDocumentBuilder pdfDocument,
        DMO.Alpha.Core.Controlo.IPegamentosQuery pegamentosQuery,
        DMO.Alpha.Core.Controlo.IPesoQuery pesoQuery,
        DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler approvalHandler,
        DMO.Alpha.Infrastructure.Controlo.ControloAreaStateHandler areaStateHandler,
        IPesoAssociationQuery pesoAssociation,
        DMO.Alpha.Infrastructure.Controlo.PuncoesHandler puncoesHandler,
        DMO.Alpha.Infrastructure.Production.JobOnChangeQuery changes,
        CurrentUserCapabilities capabilities,
        DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks mcaliper,
        IControloRevisaoQuery revisaoQuery,
        PesoProducaoAnteriorQuery producaoAnteriorQuery)
    {
        _revisaoQuery = revisaoQuery;
        _producaoAnteriorQuery = producaoAnteriorQuery;
        _mcaliper = mcaliper;
        _capabilities = capabilities;
        _puncoesHandler = puncoesHandler;
        _changes = changes;
        _pesoAssociation = pesoAssociation;
        _approvalHandler = approvalHandler;
        _areaStateHandler = areaStateHandler;
        _resumoProductions = resumoProductions;
        _productionContext = productionContext;
        _ensureControlo = ensureControlo;
        _shellState = shellState;
        _viewingHandler = viewingHandler;
        _toolLookup = toolLookup;
        _pdfDocument = pdfDocument;
        _pegamentosQuery = pegamentosQuery;
        _pesoQuery = pesoQuery;
    }

    /// <summary>Valor submetido do filtro por referência (estado de apresentação preservado).</summary>
    public string? RequestedReference { get; private set; }

    /// <summary>Produções do browser (jobon_id, nº, referência, máquina), mais recentes primeiro.</summary>
    public IReadOnlyList<ResumoProductionItem> Productions { get; private set; } = Array.Empty<ResumoProductionItem>();

    /// <summary>A contagem total das produções do filtro (para a paginação: 5 por página).</summary>
    public int TotalProductions { get; private set; }

    /// <summary>Contexto canónico da produção explicitamente selecionada (?jobon_id); null fora desse contexto.</summary>
    public ProductionContextReadModel? SelectedProduction { get; private set; }

    /// <summary>
    /// O SELO DE APROVAÇÃO (o corte D): o operador que aprovou e o momento
    /// — null quando ainda não aprovado (nunca bloqueia nada).
    /// </summary>
    public DMO.Alpha.Infrastructure.Controlo.ControloApprovalReadModel? Approval { get; private set; }

    /// <summary>O ESTADO DO DOCUMENTO: OK/NOK e comentário por área (CM/BQ/MF/PU/CS).</summary>
    public IReadOnlyList<DMO.Alpha.Infrastructure.Controlo.ControloAreaStateReadModel> AreaStates { get; private set; } =
        Array.Empty<DMO.Alpha.Infrastructure.Controlo.ControloAreaStateReadModel>();

    /// <summary>controlo_id assegurado da produção selecionada (idempotente: reabrir devolve o MESMO id).</summary>
    public int? ControloId { get; private set; }

    /// <summary>True quando foi pedido um jobon_id que não existe: aviso limpo, sem contexto nem controlo inventados.</summary>
    public bool JobOnRequestedNotFound { get; private set; }

    /// <summary>A página corrente da lista (as últimas 5 por página; 1 por omissão).</summary>
    public int PageNumber { get; private set; } = 1;

    /// <summary>
    /// O REGISTO DE VISÃO da folha (o approve-mínimo, a decisão GATE 4):
    /// quem abriu esta folha e quando — a última visita de cada operador.
    /// NUNCA é gate: a lista registra; sem visitas, a folha mostra
    /// «ninguém viu» e o resto segue igual.
    /// </summary>
    public IReadOnlyList<ControloViewingReadModel> Viewings { get; private set; } = Array.Empty<ControloViewingReadModel>();

    /// <summary>As REFERÊNCIAS das ferramentas herdadas (fatos humanos; nunca os tool_id da BD).</summary>
    public ToolLookupResult? CmTool { get; private set; }
    public ToolLookupResult? MfTool { get; private set; }
    public ToolLookupResult? BqTool { get; private set; }

    /// <summary>
    /// O Peso da produção para os valores do cartão: o das ferramentas que a
    /// produção tem agora, senão (depois de uma troca da CM ou da BQ, sem o
    /// Peso novo ainda) o mais recente; null quando ainda não existe.
    /// </summary>
    public PesoReadModel? LinkedPeso { get; private set; }

    /// <summary>O controlo anterior da mesma referência com Peso (peso médio para o delta no cartão do Peso); null sem ele.</summary>
    public ControloRevisaoItem? ControloAnterior { get; private set; }

    /// <summary>A produção anterior guardada no Peso (peso da produção anterior); null sem ela.</summary>
    public PesoProducaoAnteriorReadModel? ProducaoAnterior { get; private set; }

    /// <summary>
    /// Todos os Pesos da produção, pela ordem de gravação: um por CM+BQ
    /// (18/10/2026). Com mais do que um, o cartão lista-os todos.
    /// </summary>
    public IReadOnlyList<PesoReadModel> Pesos { get; private set; } = Array.Empty<PesoReadModel>();

    /// <summary>True quando a produção já tem o Peso da CM e da BQ que tem agora.</summary>
    public bool HasCurrentPeso => Pesos.Any(p => p.IsCurrent);

    /// <summary>
    /// O endereço de um Peso da produção: o atual pelo controlo_id, como
    /// sempre; um de antes de uma troca, também pelo peso_id dele.
    /// </summary>
    public string PesoHref(PesoReadModel peso) => peso.IsCurrent
        ? $"/Controlo/Peso?controlo_id={ControloId}"
        : $"/Controlo/Peso?controlo_id={ControloId}&peso_id={peso.PesoId}";

    /// <summary>
    /// Pesos sem produção que se podem associar (só quando a produção ainda
    /// não tem o Peso das ferramentas atuais).
    /// </summary>
    public IReadOnlyList<UnassociatedPesoCandidate> PesoCandidates { get; private set; } = Array.Empty<UnassociatedPesoCandidate>();

    /// <summary>
    /// O utilizador tem o Controlo Aprovar: o cartão «Aprovação» leva-o à
    /// revisão desta produção (Diogo, 17/10/2026).
    /// </summary>
    public bool CanApprove { get; private set; }

    /// <summary>
    /// O link do mcaliper por área com ferramenta (CM, MF, BQ), lido da ficha
    /// técnica da Tool da produção. Só as áreas com link.
    /// </summary>
    public IReadOnlyDictionary<string, string> McaliperLinks { get; private set; } = new Dictionary<string, string>();

    /// <summary>As áreas do estado do documento que têm ferramenta e, por isso, link do mcaliper.</summary>
    public static bool HasMcaliper(string area) => area is "CM" or "MF" or "BQ";

    /// <summary>A mensagem da recusa do link (?mcaliper=invalido).</summary>
    public bool McaliperRefused { get; private set; }

    /// <summary>
    /// Quem fez um registo, como no «Visto por»: «Operador 4302» para um
    /// número de operador; o email ou o nome tal como foram guardados.
    /// </summary>
    public string PersonLabel(string? who) => ResumoPdfDocumentBuilder.PersonLabel(who);

    /// <summary>
    /// As folhas de Pegamentos da produção, pela ordem de gravação (mais do
    /// que uma quando a CM, a BQ ou o MF mudou a meio; 18/10/2026);
    /// vazia antes da primeira medição.
    /// </summary>
    public IReadOnlyList<PegamentosReadModel> PegamentosSheets { get; private set; } = Array.Empty<PegamentosReadModel>();

    /// <summary>A primeira folha de Pegamentos da produção; null quando ainda não existe.</summary>
    public PegamentosReadModel? PegamentosRecord => PegamentosSheets.FirstOrDefault();

    /// <summary>A folha PU NNPB da produção (o NNPB da CM e o registo, quando existe).</summary>
    public DMO.Alpha.Infrastructure.Controlo.PuncoesSheet? Puncoes { get; private set; }

    /// <summary>O aviso quando o Job On mudou depois da aprovação; null sem alterações ou sem aprovação.</summary>
    public JobOnChangeNoticeView? JobOnNotice { get; private set; }

    /// <summary>O cartão PU NNPB aparece quando a CM é NNPB ou já há registo.</summary>
    public bool ShowPuncoes => Puncoes is { IsNnpb: true } || Puncoes?.Registo is not null;


    /// <summary>A zona da mensagem de uma recusa do estado do documento.</summary>
    public const string ZonaAvisos = "resumoEstadoAvisos";

    /// <summary>A zona do «Visto por».</summary>
    public const string ZonaVistoPor = "resumoVistoPor";

    /// <summary>A zona do cartão de uma área do estado do documento.</summary>
    public static string ZonaArea(string area) => $"resumo-area-{area}";

    private static bool ZonaConhecida(string zona) =>
        zona is ZonaAvisos or ZonaVistoPor
        || DMO.Alpha.Core.Controlo.ControloAreaState.Areas.Any(area => ZonaArea(area) == zona);

    /// <summary>As zonas pedidas pelo GET que segue uma gravação (E11); vazio na página inteira.</summary>
    public IReadOnlyList<string> ZonasPedidas { get; private set; } = Array.Empty<string>();

    /// <summary>As áreas do estado do documento: as registadas, ou as cinco por registar.</summary>
    public IReadOnlyList<DMO.Alpha.Infrastructure.Controlo.ControloAreaStateReadModel> AreasMostradas =>
        AreaStates.Count > 0
            ? AreaStates
            : DMO.Alpha.Core.Controlo.ControloAreaState.Areas
                .Select(a => new DMO.Alpha.Infrastructure.Controlo.ControloAreaStateReadModel(a, null, null, null, null))
                .ToList();

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "reference")] string? reference,
        [FromQuery(Name = "pagina")] int? pagina,
        [FromQuery(Name = "mcaliper")] string? mcaliper = null)
    {
        ViewData["Title"] = "Resumo do Controlo";
        McaliperRefused = mcaliper == "invalido";

        // Depois de gravar uma área (E11, beta.189): só as zonas pedidas, sem
        // a procura, a combinação, os controlos associados nem a aprovação.
        if (jobOnId is int zonasJobOnId
            && DmoZonasPedidas.Ler(Request, ZonaConhecida) is { } zonas
            && await ZonasAsync(zonasJobOnId, zonas) is { } parcial)
        {
            return parcial;
        }

        // O CORTE H: a barra do módulo é SEMPRE completa (o
        // ShellStateFactory — o mapeamento num só sítio; nada de
        // capability aqui).
        ViewData["Shell"] = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = "resumo",
            Functions = ShellStateFactory.ControloFunctions("resumo")
        };

        // Browser «Procurar resumo» (o Owner 2026-10-13: «deveriamos
        // escrever a referencia e depois de selecionar a referencia aparecer
        // as ultimas produçoes em baixo, por exemplo uma lista com ultimas
        // 5» + «ter opçao de procurar na lista por paginaçao»): as últimas
        // 5 da referência escrita (ou de todas), com a paginação para as
        // mais antigas. Nenhum auto-open: a produção a abrir é sempre
        // escolha explícita.
        RequestedReference = Sanitize(reference);
        PageNumber = pagina is int p && p >= 1 ? p : 1;
        // Sem referência a lista não é mostrada, por isso não se consulta.
        if (RequestedReference is not null)
        {
            var pageResult = await _resumoProductions.ListAsync(
                RequestedReference, PageNumber, HttpContext.RequestAborted);
            Productions = pageResult.Items;
            TotalProductions = pageResult.TotalCount;
            PageNumber = Math.Min(PageNumber, Math.Max(1, (int)Math.Ceiling(TotalProductions / 5.0)));
        }

        // A folha só existe sobre uma produção EXPLICITAMENTE selecionada
        // (?jobon_id pelo «Abrir»): a página nunca escolhe nem abre
        // sozinha — nem com uma única produção no resultado.
        if (jobOnId is int canonicalJobOnId)
        {
            // Contexto pela query canónica EXISTENTE, por jobon_id — a
            // mesma que a página Job On e os módulos seguintes consomem.
            SelectedProduction = await _productionContext.GetAsync(canonicalJobOnId, HttpContext.RequestAborted);

            // As REFERÊNCIAS das ferramentas herdadas (o Owner 2026-10-12:
            // os uuid da BD não ficam na folha — a referência é o fato
            // humano). As três numa só consulta.
            if (SelectedProduction is { } toolsProduction)
            {
                var tools = await _toolLookup.FindByToolIdsAsync(
                    new[] { toolsProduction.CmToolId, toolsProduction.MfToolId, toolsProduction.BqToolId },
                    HttpContext.RequestAborted);
                await LoadMcaliperLinksAsync(toolsProduction);
                CmTool = toolsProduction.CmToolId is { } cmId ? tools.GetValueOrDefault(cmId) : null;
                MfTool = toolsProduction.MfToolId is { } mfId ? tools.GetValueOrDefault(mfId) : null;
                BqTool = toolsProduction.BqToolId is { } bqId ? tools.GetValueOrDefault(bqId) : null;
            }

            if (SelectedProduction is null)
            {
                // jobon_id inexistente: aviso limpo. O ensure só corre
                // sobre uma produção que existe — nunca é criado um
                // controlo para um jobon_id inexistente.
                JobOnRequestedNotFound = true;
            }
            else
            {
                // Ensure do controlo_id: get-or-create idempotente por
                // jobon_id — bookkeeping, nunca um ato de criação do
                // utilizador. Produções criadas antes deste corte ganham
                // aqui o seu controlo_id UMA vez (backfill); reabrir a
                // folha devolve o MESMO controlo_id, sem segunda escrita.
                // O controlo existente e o selo vêm numa só leitura; o
                // ensure só corre quando a produção ainda não tem controlo.
                var existing = await _approvalHandler.FindByJobOnAsync(
                    canonicalJobOnId, HttpContext.RequestAborted);
                if (existing is { } found)
                {
                    ControloId = found.ControloId;
                }
                else
                {
                    var ensured = await _ensureControlo.HandleAsync(
                        new EnsureControloForJobOn(canonicalJobOnId),
                        HttpContext.RequestAborted);
                    ControloId = ensured.Result == EnsureControloResult.Success
                        ? ensured.ControloId
                        : null;
                }

                // O REGISTO DE VISÃO (a decisão GATE 4): abrir a folha é
                // VER — a visita do operador logado fica registada (o
                // get-or-mark idempotente: uma linha por operador; reabrir
                // atualiza o momento). Nunca bloqueia nada: é o histórico
                // de que a informação existiu e foi vista.
                if (ControloId is { } viewedControloId)
                {
                    Viewings = await _viewingHandler.MarkAndGetViewingsOfExistingAsync(
                        viewedControloId,
                        User.FindFirst(DmoClaimTypes.OperatorId)?.Value,
                        HttpContext.RequestAborted);

                    // O SELO DE APROVAÇÃO (o corte D): o estado corrente —
                    // apenas leitura na folha; o botão (do chefe) sela.
                    Approval = existing?.Approval
                        ?? await _approvalHandler.GetAsync(viewedControloId, HttpContext.RequestAborted);

                    AreaStates = await _areaStateHandler.GetAsync(
                        viewedControloId, HttpContext.RequestAborted);

                    // O Job On mudou depois da aprovação: só informa, a
                    // aprovação mantém-se (o proprietário, 17/10/2026).
                    JobOnNotice = JobOnChangeNoticeView.From(
                        "O Job On mudou depois da aprovação. A aprovação mantém-se.",
                        await _changes.ForControloApprovalAsync(viewedControloId, HttpContext.RequestAborted));

                    await LoadLinkedControlsAsync(viewedControloId, SelectedProduction);

                    CanApprove = (await _capabilities.GetAsync(HttpContext.RequestAborted))
                        .Contains(DmoCapabilities.ControloApprove);
                }
            }
        }

        // Contexto canónico das tabs: ao sair da folha não perder a produção.
        if (ViewData["Shell"] is ShellPresentation shell)
        {
            ViewData["Shell"] = shell with
            {
                Functions = ShellStateFactory.ControloFunctions(
                    "resumo", SelectedProduction?.JobOnId, ControloId)
            };
        }

        return Page();
    }

    /// <summary>
    /// O link do mcaliper por área (CM, MF, BQ), lido da ficha técnica das
    /// ferramentas da produção.
    /// </summary>
    private async Task LoadMcaliperLinksAsync(ProductionContextReadModel production)
    {
        var links = await _mcaliper.GetAsync(
            new[] { production.CmToolId, production.MfToolId, production.BqToolId },
            HttpContext.RequestAborted);
        McaliperLinks = new[] { ("CM", production.CmToolId), ("MF", production.MfToolId), ("BQ", production.BqToolId) }
            .Where(slot => slot.Item2 is { } id && links.ContainsKey(id))
            .ToDictionary(slot => slot.Item1, slot => links[slot.Item2!]);
    }

    /// <summary>
    /// Só as zonas pedidas pelo GET que segue uma gravação (E11, beta.189):
    /// os cartões das áreas (o contexto da produção para o jobon_id e os
    /// links do mcaliper, e os estados), a mensagem da recusa e o «Visto
    /// por». A visita fica registada como na página inteira (abrir é ver).
    /// Não lê as ferramentas, a aprovação, as alterações do Job On nem os
    /// controlos associados, que não estão nessas zonas. Sem produção ou sem
    /// controlo (o ensure da página inteira), null: a página inteira.
    /// </summary>
    private async Task<IActionResult?> ZonasAsync(int jobOnId, IReadOnlyList<string> zonas)
    {
        var ct = HttpContext.RequestAborted;
        SelectedProduction = await _productionContext.GetAsync(jobOnId, ct);
        if (SelectedProduction is null
            || await _approvalHandler.FindByJobOnAsync(jobOnId, ct) is not { } existing)
        {
            SelectedProduction = null;
            return null;
        }

        ControloId = existing.ControloId;
        Viewings = await _viewingHandler.MarkAndGetViewingsOfExistingAsync(
            existing.ControloId, User.FindFirst(DmoClaimTypes.OperatorId)?.Value, ct);
        if (zonas.Any(zona => zona is not ZonaAvisos and not ZonaVistoPor))
        {
            await LoadMcaliperLinksAsync(SelectedProduction);
            AreaStates = await _areaStateHandler.GetAsync(existing.ControloId, ct);
        }

        ZonasPedidas = zonas;
        DmoZonasPedidas.Marcar(Response, zonas);
        return Partial("_ResumoZonas", this);
    }

    /// <summary>
    /// POST nativo: grava o ESTADO DO DOCUMENTO de uma área (OK/NOK ou o
    /// comentário). Nunca é gate de nada.
    /// </summary>
    public async Task<IActionResult> OnPostAreaStateAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "area")] string? area,
        [FromForm(Name = "estado")] string? status,
        [FromForm(Name = "comentario")] string? comment,
        [FromForm(Name = "voltar")] string? voltar = null,
        [FromForm(Name = "identificacao")] string? identificacao = null)
    {
        if (jobOnId is not int canonicalJobOnId)
        {
            return RedirectToPage("/Controlo/Resumo", new { jobon_id = jobOnId });
        }

        var ensured = await _ensureControlo.HandleAsync(
            new EnsureControloForJobOn(canonicalJobOnId), HttpContext.RequestAborted);
        if (ensured.ControloId is { } controloId)
        {
            var updatedBy = User.FindFirst(DmoClaimTypes.OperatorId)?.Value
                ?? User.FindFirst(DmoClaimTypes.Email)?.Value
                ?? User.FindFirst(DmoClaimTypes.DisplayName)?.Value;
            // Cada formulário só envia o seu campo (o OK/NOK, o comentário ou,
            // no PU e no CS, a referência escrita); o que não vem fica como
            // está na base.
            await _areaStateHandler.SetAsync(
                controloId, area, status, comment, updatedBy,
                setStatus: Request.Form.ContainsKey("estado"),
                setComment: Request.Form.ContainsKey("comentario"),
                cancellationToken: HttpContext.RequestAborted,
                identificacao: identificacao,
                setIdentificacao: Request.Form.ContainsKey("identificacao"));
        }

        return voltar == ResumoDoControlo
            ? Redirect(ResumoDoControloUrl(canonicalJobOnId))
            : RedirectToPage("/Controlo/Resumo", null, new { jobon_id = canonicalJobOnId }, "estado-documento");
    }

    /// <summary>
    /// O OK/NOK, o comentário e o link do mcaliper também se gravam nos
    /// cartões das áreas do Resumo do controlo (Diogo, 20/10/2026); com
    /// voltar=resumo-controlo, a gravação volta lá, às áreas.
    /// </summary>
    public const string ResumoDoControlo = "resumo-controlo";

    private static string ResumoDoControloUrl(int jobOnId, bool mcaliperRecusado = false) =>
        $"/Controlo/Approve/Detalhe?tipo=controlo&jobon_id={jobOnId}{(mcaliperRecusado ? "&mcaliper=invalido" : null)}#resumoControloAreas";

    /// <summary>
    /// Os controlos associados que a folha mostra: o Peso (com as opções de
    /// comparação, ou os Pesos sem produção da mesma CM) e o registo de
    /// Pegamentos. Cada secção só é lida quando é mostrada.
    /// </summary>
    private async Task LoadLinkedControlsAsync(int controloId, ProductionContextReadModel production)
    {
        var ct = HttpContext.RequestAborted;
        // Os Pesos do controlo, um por CM+BQ (18/10/2026): o cartão mostra o
        // das ferramentas atuais, senão o mais recente.
        Pesos = await _pesoQuery.ListByControloAsync(controloId, ct);
        LinkedPeso = Pesos.FirstOrDefault(p => p.IsCurrent) ?? Pesos.LastOrDefault();
        if (LinkedPeso is { } pesoLigado)
        {
            ControloAnterior = await _revisaoQuery.FindPreviousWithPesoAsync(production.JobOnId, ct);
            ProducaoAnterior = await _producaoAnteriorQuery.FindAsync(pesoLigado.PesoId, ct);
        }
        if (!HasCurrentPeso && production.CmToolId is not null)
        {
            // Sem o Peso das ferramentas atuais (ainda sem Peso, ou depois de
            // uma troca da CM ou da BQ): os Pesos sem produção que a
            // associação aceita — a CM e a BQ deste Job On, agora ou antes
            // da troca (resposta R16-6 do especialista), os mesmos da folha
            // de Peso.
            PesoCandidates = await _pesoAssociation.FindCandidatesForControloAsync(controloId, ct);
        }

        PegamentosSheets = await _pegamentosQuery.ListSheetsByControloAsync(controloId, ct);
        Puncoes = await _puncoesHandler.GetSheetAsync(controloId, ct);
    }

    /// <summary>
    /// Associa o link do mcaliper à ferramenta da área (CM, MF ou BQ) desta
    /// produção, na ficha técnica da Tool (Diogo, 17/10/2026). Campo vazio
    /// retira o link; um endereço que não é http(s) é recusado.
    /// </summary>
    public async Task<IActionResult> OnPostMcaliperAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "area")] string? area,
        [FromForm(Name = "url")] string? url,
        [FromForm(Name = "voltar")] string? voltar = null)
    {
        if (jobOnId is not int canonicalJobOnId)
        {
            return RedirectToPage("/Controlo/Resumo");
        }

        var production = await _productionContext.GetAsync(canonicalJobOnId, HttpContext.RequestAborted);
        var toolId = area switch
        {
            "CM" => production?.CmToolId,
            "MF" => production?.MfToolId,
            "BQ" => production?.BqToolId,
            _ => null,
        };
        var saved = toolId is not null
            && await _mcaliper.SetAsync(toolId, url, HttpContext.RequestAborted);
        if (voltar == ResumoDoControlo)
            return Redirect(ResumoDoControloUrl(canonicalJobOnId, mcaliperRecusado: !saved));
        return RedirectToPage("/Controlo/Resumo", null,
            saved ? new { jobon_id = canonicalJobOnId } : new { jobon_id = canonicalJobOnId, mcaliper = "invalido" },
            "estado-documento");
    }

    /// <summary>
    /// A procura ao escrever (?handler=Procurar&amp;q=, Diogo 17/10/2026): as
    /// últimas 5 produções do texto escrito, que a página mostra por baixo do
    /// campo. Um clique numa linha abre a produção.
    /// </summary>
    public async Task<IActionResult> OnGetProcurarAsync(string? q, int? pagina)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var term = Sanitize(q);
        if (term is null)
        {
            return Content(string.Empty, "text/html");
        }

        var page = pagina is int requested && requested >= 1 ? requested : 1;
        var result = await _resumoProductions.ListAsync(term, page, HttpContext.RequestAborted);
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling(result.TotalCount / 5.0)));
        return Partial("_ResumoProcura", new ResumoProcuraView(term, result.Items, result.TotalCount, page));
    }

    private static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Descarrega o Resumo separado ou o documento composto pelos mesmos geradores completos de Peso e Pegamentos.
    /// Geração em memória: não grava PDFs no servidor nem altera dados persistidos.
    /// </summary>
    public async Task<IActionResult> OnPostGeneratePdfAsync(
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "comparar")] int? previousControlPesoId = null,
        [FromForm(Name = "comparar_peso")] int? previousPesoId = null,
        [FromForm(Name = "apenas_resumo")] bool summaryOnly = false)
    {
        if (jobOnId is not int canonicalJobOnId)
        {
            return RedirectToPage("/Controlo/Resumo", new { pdf_refusal = "sem-producao" });
        }

        var documento = await _pdfDocument.BuildAsync(
            canonicalJobOnId, previousControlPesoId, previousPesoId, summaryOnly,
            GeradoPor(User), HttpContext.RequestAborted);
        if (documento is null)
        {
            return RedirectToPage("/Controlo/Resumo", new { jobon_id = canonicalJobOnId, pdf_refusal = "sem-producao" });
        }

        Response.Headers[ControloDocumentPaths.PdfFolderHeader] = Uri.EscapeDataString(documento.Folder);
        if (documento.PuncoesControloId is int puncoesControloId)
        {
            Response.Headers[ControloDocumentPaths.PdfCompanionHeader] = $"/Controlo/Puncoes?handler=GeneratePdf&controlo_id={puncoesControloId}";
        }

        return File(documento.Bytes, "application/pdf", documento.FileName);
    }

    /// <summary>
    /// Quem gera o papel (o rodapé do alvo: «Diogo Oliveira 28/07/2026,
    /// 14:52:28»): o nome humano da sessão.
    /// </summary>
    public static string? GeradoPor(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(DmoClaimTypes.DisplayName)?.Value
        ?? user.FindFirst(DmoClaimTypes.Email)?.Value;
}

/// <summary>Uma página de produções da procura do Resumo (o texto, as 5 linhas e o total).</summary>
public sealed record ResumoProcuraView(
    string Term,
    IReadOnlyList<ResumoProductionItem> Items,
    int Total,
    int Page);

/// <summary>Uma área do estado do documento na sua zona (_ResumoArea).</summary>
public sealed record ResumoAreaZona(
    ResumoModel Pagina,
    DMO.Alpha.Infrastructure.Controlo.ControloAreaStateReadModel Estado);

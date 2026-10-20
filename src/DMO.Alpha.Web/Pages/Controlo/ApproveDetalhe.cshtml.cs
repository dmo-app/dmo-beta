using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

[Authorize]
[ModuleEntry(DmoCapabilities.ControloApprove)]
public sealed class ApproveDetalheModel : PageModel
{
    private readonly ShellStateFactory _shell;
    private readonly IProductionContextQuery _production;
    private readonly IPesoQuery _peso;
    private readonly IPegamentosQuery _pegamentos;
    private readonly IControloRevisaoQuery _revisao;
    private readonly DMO.Alpha.Infrastructure.Controlo.ControloAreaStateHandler _areas;
    private readonly DMO.Alpha.Core.Tools.IToolLookupQuery _tools;
    private readonly DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler _approval;
    private readonly DMO.Alpha.Infrastructure.Controlo.PuncoesHandler _puncoes;
    private readonly DMO.Alpha.Infrastructure.Controlo.RevisaoControloAnteriorQuery _anteriores;
    private readonly DMO.Alpha.Infrastructure.Controlo.PegamentosHistoricoQuery _historico;
    private readonly CurrentUserCapabilities _capabilities;
    private readonly IProductionPickerQuery _picker;
    private readonly IEnsureControloForJobOnHandler _ensure;
    private readonly DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks _mcaliper;

    public ApproveDetalheModel(
        ShellStateFactory shell,
        IProductionContextQuery production, IPesoQuery peso,
        IPegamentosQuery pegamentos,
        IControloRevisaoQuery revisao,
        DMO.Alpha.Infrastructure.Controlo.ControloAreaStateHandler areas,
        DMO.Alpha.Core.Tools.IToolLookupQuery tools,
        DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler approval,
        DMO.Alpha.Infrastructure.Controlo.PuncoesHandler puncoes,
        DMO.Alpha.Infrastructure.Controlo.RevisaoControloAnteriorQuery anteriores,
        DMO.Alpha.Infrastructure.Controlo.PegamentosHistoricoQuery historico,
        CurrentUserCapabilities capabilities,
        IProductionPickerQuery picker,
        IEnsureControloForJobOnHandler ensure,
        DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks mcaliper)
    {
        _mcaliper = mcaliper;
        _picker = picker;
        _ensure = ensure;
        _capabilities = capabilities;
        _historico = historico;
        _anteriores = anteriores;
        _approval = approval;
        _puncoes = puncoes;
        _areas = areas;
        _tools = tools;
        _revisao = revisao;
        _shell = shell;
        _production = production;
        _peso = peso;
        _pegamentos = pegamentos;
    }

    public ProductionContextReadModel? Production { get; private set; }
    /// <summary>O Peso para um valor só: o das ferramentas atuais, senão o mais recente.</summary>
    public PesoReadModel? Peso { get; private set; }

    /// <summary>
    /// Todos os Pesos da produção, um por CM+BQ (a CM ou a BQ mudou a meio;
    /// 18/10/2026), cada um com as suas ferramentas.
    /// </summary>
    public IReadOnlyList<PesoReadModel> Pesos { get; private set; } = [];

    /// <summary>
    /// O controlo anterior de cada Peso, pelo peso_id: o peso do SAP e a
    /// produção escolhida no Peso, senão só a sugestão (C6).
    /// </summary>
    public IReadOnlyDictionary<int, DMO.Alpha.Infrastructure.Controlo.RevisaoControloAnterior> Anteriores { get; private set; }
        = new Dictionary<int, DMO.Alpha.Infrastructure.Controlo.RevisaoControloAnterior>();
    public PegamentosSheetReadModel? Pegamentos { get; private set; }

    /// <summary>
    /// As leituras dos Pegamentos corrigidas ou apagadas, da mais recente para
    /// a mais antiga (4.6): o valor antigo, o novo, quem e quando.
    /// </summary>
    public IReadOnlyList<PegamentosMeasurementHistorico> Correcoes { get; private set; } = [];
    public string Tipo { get; private set; } = "peso";

    /// <summary>
    /// O Resumo do controlo (tipo=controlo; Diogo, 19/10/2026: «resume tudo
    /// sobre controlo, a versão do PDF mas na UI»): as consultas todas, uma a
    /// seguir à outra. Cada parte mostra-se na sua consulta e no Resumo.
    /// </summary>
    public bool Mostra(string parte) => Tipo == parte || Tipo == "controlo";

    /// <summary>O link «Registar» do aviso sem Peso: a página do Peso é do Controlo Criar.</summary>
    public bool TemControloCriar { get; private set; }
    public int? ControloId { get; private set; }

    /// <summary>
    /// A procura do Resumo do controlo, em dois passos: a referência, depois
    /// as últimas 5 produções dela. Abre vazio e só procura depois de se
    /// escrever a referência.
    /// </summary>
    public string? Referencia { get; private set; }
    public IReadOnlyList<ProductionPickItem> Producoes { get; private set; } = [];
    /// <summary>A produção escolhida ainda não tem controlo (nada registado).</summary>
    public ProductionPickItem? SemControlo { get; private set; }


    /// <summary>O Resumo das ferramentas (tipo=ferramentas): o OK/NOK de cada área e as ferramentas da produção.</summary>
    public IReadOnlyList<DMO.Alpha.Infrastructure.Controlo.ControloAreaStateReadModel> Areas { get; private set; } = [];
    public IReadOnlyList<(string Area, DMO.Alpha.Core.Tools.ToolLookupResult Tool)> Tools { get; private set; } = [];

    /// <summary>
    /// O link do mcaliper por área (CM, BQ, MF), da ficha técnica da
    /// ferramenta: os cartões do OK/NOK no Resumo do controlo associam-no e
    /// copiam-no (Diogo, 20/10/2026). O link nunca aparece escrito no ecrã.
    /// </summary>
    public IReadOnlyDictionary<string, string> McaliperLinks { get; private set; } = new Dictionary<string, string>();

    /// <summary>O link do mcaliper foi recusado (?mcaliper=invalido): não é um endereço http(s).</summary>
    public bool McaliperRefused { get; private set; }

    /// <summary>O registo das punções NNPB (tipo=puncoes): tudo, sem aprovar.</summary>
    public DMO.Alpha.Infrastructure.Controlo.PuncoesSheet? Puncoes { get; private set; }

    /// <summary>
    /// A decisão desta parte (Peso ou Pegamentos): o Aprovar / Não aprovar
    /// também na consulta (Diogo, 19/10/2026: «merter aprovar tambem nas
    /// paginas dos mesmos»). Null no PU NNPB e no Resumo das ferramentas.
    /// </summary>
    public RevisaoParteVista? Parte { get; private set; }

    public string Reviewer { get; private set; } = "";
    public bool NoteMissing { get; private set; }
    public bool ApprovalRefusedNoIdentity { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "controlo_id")] int? controloId,
        [FromQuery(Name = "tipo")] string? tipo,
        [FromQuery(Name = "jobon_id")] int? jobOnId = null,
        [FromQuery(Name = "referencia")] string? referencia = null,
        [FromQuery(Name = "nota_em_falta")] string? noteMissing = null,
        [FromQuery(Name = "aprovacao_recusada")] string? approvalRefusal = null,
        [FromQuery(Name = "mcaliper")] string? mcaliper = null)
    {
        McaliperRefused = mcaliper == "invalido";
        Tipo = tipo is "pegamentos" or "ferramentas" or "puncoes" or "controlo" ? tipo : "peso";
        NoteMissing = noteMissing == "1";
        ApprovalRefusedNoIdentity = approvalRefusal == "sem-identidade";
        Reviewer = User.FindFirst(DMO.Alpha.Web.Services.DmoClaimTypes.DisplayName)?.Value
            ?? User.FindFirst(DMO.Alpha.Web.Services.DmoClaimTypes.Email)?.Value
            ?? User.Identity?.Name
            ?? "";
        if (Tipo == "controlo")
        {
            Referencia = string.IsNullOrWhiteSpace(referencia) ? null : referencia.Trim().ToUpperInvariant();
            if (controloId is null && jobOnId is int escolhida)
            {
                controloId = await _ensure.FindExistingIdAsync(escolhida, HttpContext.RequestAborted);
                if (controloId is null)
                    SemControlo = await _picker.FindAsync(escolhida, HttpContext.RequestAborted);
                Referencia ??= SemControlo?.Reference;
            }
        }
        ControloId = controloId;
        ViewData["Title"] = Tipo switch
        {
            "peso" => "Revisão de Peso",
            "pegamentos" => "Revisão de Pegamentos",
            "puncoes" => "Revisão do PU NNPB",
            "controlo" => "Resumo do controlo",
            _ => "Revisão do Resumo das ferramentas"
        };
        var separador = Tipo == "controlo" ? "resumo-controlo" : "revisao";
        ViewData["Shell"] = await _shell.ForControloReviewAsync(activeKey: separador);

        if (controloId is int procurado)
        {
            Production = await _production.GetByControloAsync(procurado, HttpContext.RequestAborted);
            if (Tipo == "controlo")
                Referencia ??= Production?.Reference;
        }
        if (Tipo == "controlo" && Referencia is { } procura)
            Producoes = await _picker.ListByReferenceAsync(procura, HttpContext.RequestAborted);

        if (controloId is not int id || Production is null)
            return Page();

        ViewData["Shell"] = await _shell.ForControloReviewAsync(Production.JobOnId, separador);
        TemControloCriar = (await _capabilities.GetAsync(HttpContext.RequestAborted))
            .Contains(DmoCapabilities.ControloCreate, StringComparer.Ordinal);

        if (Mostra("peso"))
        {
            Pesos = await _peso.ListByControloAsync(id, HttpContext.RequestAborted);
            Peso = Pesos.FirstOrDefault(p => p.IsCurrent) ?? Pesos.LastOrDefault();
            // O controlo anterior de cada Peso: o que o operador escolheu no
            // Peso, senão só a sugestão (Diogo, 19/10/2026, C6).
            Anteriores = await _anteriores.ParaPesosAsync(Production.JobOnId, Pesos, HttpContext.RequestAborted);
        }
        if (Mostra("pegamentos"))
        {
            Pegamentos = await _pegamentos.FindSheetByControloAsync(id, cancellationToken: HttpContext.RequestAborted);
            Correcoes = await _historico.ListByControloAsync(id, HttpContext.RequestAborted);
        }
        if (Mostra("puncoes"))
            Puncoes = await _puncoes.GetSheetAsync(id, HttpContext.RequestAborted);
        if (Mostra("ferramentas"))
        {
            Areas = await _areas.GetAsync(id, HttpContext.RequestAborted);
            var tools = new List<(string, DMO.Alpha.Core.Tools.ToolLookupResult)>();
            foreach (var (area, toolId) in new[] { ("CM", Production.CmToolId), ("BQ", Production.BqToolId), ("MF", Production.MfToolId) })
            {
                if (toolId is not null && await _tools.FindByToolIdAsync(toolId, HttpContext.RequestAborted) is { } tool)
                    tools.Add((area, tool));
            }
            Tools = tools;
            var links = await _mcaliper.GetAsync(
                [Production.CmToolId, Production.BqToolId, Production.MfToolId], HttpContext.RequestAborted);
            McaliperLinks = new[] { ("CM", Production.CmToolId), ("BQ", Production.BqToolId), ("MF", Production.MfToolId) }
                .Where(x => x.Item2 is not null && links.ContainsKey(x.Item2))
                .ToDictionary(x => x.Item1, x => links[x.Item2!]);
        }

        // Só o Peso se aprova (Diogo, 19/10/2026); a consulta dos Pegamentos não
        // tem aprovar. O Resumo do controlo mostra a decisão, sem os botões.
        if (Mostra("peso"))
        {
            var partes = await ApproveModel.PartesAsync(_approval, _revisao, id, HttpContext.RequestAborted);
            Parte = ApproveModel.Vista(partes, ControloParte.Peso, Pesos.Count > 0,
                await _approval.RegistosAsync(id, HttpContext.RequestAborted));
        }

        return Page();
    }
}

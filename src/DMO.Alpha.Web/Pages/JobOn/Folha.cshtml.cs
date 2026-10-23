using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// A FOLHA NOVA DO JOB ON (Diogo, 22/10/2026, mockup Job_On_v2): numa página
/// própria, com o painel lateral do Planeamento e o link de regresso. A
/// informação vem agrupada para o chefe (Moldes, Gargalo, Sopro e
/// calibres, Artigo e Notas gerais); o primeiro ecrã tem só o essencial e a
/// informação técnica recolhe no triângulo. Abre bloqueada (só leitura);
/// «Editar» desbloqueia-a para quem tem o Planeamento (jobon.create). Quem
/// só tem «Job On ver» (jobon.view) vê a folha sem «Editar». Guardar usa o
/// mesmo POST do Planeamento (Update) e volta aqui.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.JobOnView, DmoCapabilities.JobOnCreate)]
public sealed class FolhaModel(
    IProductionContextQuery productionContext,
    IJobOnToolFactsQuery toolFacts,
    IJobOnFolhaQuery folha,
    IJobOnListQuery jobOnList,
    IMachineListQuery machineList,
    IProductionPickerQuery productionPicker,
    ShellStateFactory shellState,
    CurrentUserCapabilities capabilities) : PageModel
{
    /// <summary>A produção aberta; null sem jobon_id ou quando não existe.</summary>
    public ProductionContextReadModel? JobOn { get; private set; }

    /// <summary>Pediu-se uma produção que não existe.</summary>
    public bool NaoExiste { get; private set; }

    /// <summary>A folha está desbloqueada (Editar).</summary>
    public bool Editar { get; private set; }

    /// <summary>Quem vê pode editar (jobon.create).</summary>
    public bool PodeEditar { get; private set; }

    /// <summary>Quem vê tem um módulo do Controlo (a opção «Controlo»).</summary>
    public bool VeControlo { get; private set; }

    /// <summary>Tem a chave das Boquilhas: vê «BQs» nas Opções (os movimentos da BQ deste Job On).</summary>
    public bool VeBoquilhas { get; private set; }

    /// <summary>A folha da fábrica gravada.</summary>
    public FolhaDoJobOnVista Folha { get; private set; } = FolhaDoJobOnVista.Vazia();

    /// <summary>Os factos das ferramentas da produção (referência, lote, % de uso).</summary>
    public IReadOnlyList<JobOnToolFacts> Facts { get; private set; } = [];

    /// <summary>As linhas (o select do Editar).</summary>
    public IReadOnlyList<MachineOption> Machines { get; private set; } = [];

    /// <summary>O painel lateral: as produções em curso e as que entram esta semana.</summary>
    public IReadOnlyList<ProductionContextReadModel> ProximasEntradas { get; private set; } = [];

    /// <summary>A mensagem depois de guardar (e os avisos que vieram com a gravação).</summary>
    public IReadOnlyList<string> AvisosGravacao { get; private set; } = [];

    public bool Gravado { get; private set; }

    /// <summary>A referência escrita na procura do cabeçalho.</summary>
    public string? ReferenciaProcurada { get; private set; }

    /// <summary>A referência procurada não tem produções.</summary>
    public bool ReferenciaSemProducoes { get; private set; }

    /// <summary>As últimas produções da mesma referência (o dropdown do Planeamento): escolher uma abre a folha dela.</summary>
    public IReadOnlyList<ProductionSearchItem> Producoes { get; private set; } = [];

    /// <summary>O lote sem zeros à esquerda («Lote 1»).</summary>
    public static string Lote(string? lote) => DMO.Alpha.Web.Presentation.LoteTexto.Mostrar(lote);

    public JobOnToolFacts? Fact(string? toolId) => toolId is null ? null : Facts.FirstOrDefault(f => f.ToolId == toolId);

    /// <summary>
    /// A referência total da produção: a referência do MF seguida da da BQ
    /// (5810 + T188 = 5810T188). Sem MF ou sem BQ, a gravada.
    /// </summary>
    public string ReferenciaTotal =>
        JobOn is null ? string.Empty
        : Fact(JobOn.MfToolId) is { } mf && Fact(JobOn.BqToolId) is { } bq ? mf.Reference + bq.Reference
        : JobOn.Reference;

    /// <summary>O aviso quando a referência gravada não é MF + BQ (só aviso).</summary>
    public string? AvisoReferencia =>
        JobOn is not null && !string.Equals(ReferenciaTotal, JobOn.Reference, StringComparison.OrdinalIgnoreCase)
            ? $"A referência da produção é {JobOn.Reference}, mas MF + BQ dá {ReferenciaTotal}. Confirme as ferramentas."
            : null;

    /// <summary>O que falta para o Job On ser possível (CM, MF e BQ). Só aviso na leitura; o Guardar recusa sem eles.</summary>
    public IReadOnlyList<string> Faltam =>
        JobOn is null ? []
        : new[] { ("CM", JobOn.CmToolId), ("MF", JobOn.MfToolId), ("BQ", JobOn.BqToolId) }
            .Where(t => t.Item2 is null).Select(t => t.Item1).ToList();

    public int? Dias => JobOn is { InicioEm: { } i, FimEm: { } f } && f >= i ? f.DayNumber - i.DayNumber + 1 : null;

    public string Href(bool editar = false) =>
        JobOn is null ? "/JobOn/Folha" : $"/JobOn/Folha?jobon_id={JobOn.JobOnId}{(editar ? "&editar=1" : "")}";

    /// <summary>O regresso ao Planeamento, no dia de início da produção.</summary>
    public string PlaneamentoHref =>
        !PodeEditar ? "/JobOn/Folha"
        : JobOn is { InicioEm: { } inicio } aberto ? $"/JobOn?ano={inicio.Year}&mes={inicio.Month}&dia={inicio.Day}&jobon_id={aberto.JobOnId}#lightFicha"
        : JobOn is { } semDia ? $"/JobOn?jobon_id={semDia.JobOnId}#lightFicha"
        : "/JobOn";

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "editar")] string? editar,
        [FromQuery(Name = "gravado")] string? gravado,
        [FromQuery(Name = "overlap")] string? overlap,
        [FromQuery(Name = "calote_ignorada")] string? caloteIgnorada,
        [FromQuery(Name = "fim_antes_inicio")] string? fimAntesInicio,
        [FromQuery(Name = "referencia")] string? referencia = null)
    {
        ViewData["Title"] = "Job On";
        ViewData["Shell"] = (await shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("planeamento", "Planeamento"))) with
        {
            ActiveFunctionKey = "jobon",
            Functions = await shellState.PlanningFunctionsAsync("jobon")
        };
        var ct = HttpContext.RequestAborted;
        PodeEditar = await capabilities.HasAsync(DmoCapabilities.JobOnCreate, ct);
        VeControlo = await capabilities.HasAsync(DmoCapabilities.ControloCreate, ct)
            || await capabilities.HasAsync(DmoCapabilities.ControloApprove, ct);
        VeBoquilhas = await capabilities.HasAsync(DmoCapabilities.Boquilhas, ct);

        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var domingo = hoje.AddDays((7 - (int)hoje.DayOfWeek) % 7);
        ProximasEntradas = IndexModel.ProximasEntradasDaSemana(await jobOnList.ListWindowAsync(hoje, domingo, ct), hoje);

        // A procura pela referência (o campo do cabeçalho): abre a última produção dela.
        if (jobOnId is null && referencia?.Trim() is { Length: > 0 } procurada)
        {
            ReferenciaProcurada = procurada;
            var dela = await productionPicker.ListOfReferenceAsync(procurada, ct);
            if (dela.Count > 0)
            {
                var ultima = dela.OrderByDescending(p => p.ProductionNumber, StringComparer.Ordinal).First();
                return Redirect($"/JobOn/Folha?jobon_id={ultima.JobOnId}");
            }
            ReferenciaSemProducoes = true;
        }

        if (jobOnId is int id)
        {
            JobOn = await productionContext.GetAsync(id, ct);
            NaoExiste = JobOn is null;
        }

        if (JobOn is { } aberto)
        {
            // Só quem tem o Planeamento desbloqueia: o «Job On ver» abre sempre bloqueada.
            Editar = PodeEditar && editar == "1";
            Producoes = (await productionPicker.ListOfReferenceAsync(aberto.Reference, ct))
                .OrderByDescending(p => p.ProductionNumber, StringComparer.Ordinal).ToList();
            Folha = FolhaDoJobOnVista.DaLida(await folha.ForJobOnAsync(aberto.JobOnId, ct));
            var ids = new[] { aberto.CmToolId, aberto.MfToolId, aberto.BqToolId }.OfType<string>().ToList();
            if (ids.Count > 0)
            {
                Facts = await toolFacts.FindAsync(ids, aberto.Machine, aberto.JobOnId, aberto.InicioEm, aberto.FimEm, ct);
            }
            if (Editar)
            {
                Machines = await machineList.ListAsync(ct);
            }

            Gravado = gravado == "1";
            var avisos = new List<string>();
            if (overlap == "1")
            {
                avisos.Add($"Há outra produção na {aberto.Machine} nas mesmas datas. Veja no Planeamento.");
            }
            if (caloteIgnorada is { Length: > 0 })
            {
                avisos.Add($"A calote «{caloteIgnorada}» não é um número e ficou a anterior.");
            }
            if (fimAntesInicio == "1")
            {
                avisos.Add("O fim é antes do início: a produção não aparece no calendário.");
            }
            AvisosGravacao = avisos;
        }

        return Page();
    }
}

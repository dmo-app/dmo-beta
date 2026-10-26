using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
/// CRIAR E DUPLICAR NA FOLHA NOVA (Diogo, 23/10/2026: «esta versão do Job On
/// já não deveria existir»): ?novo=1 abre a folha vazia em Editar e
/// ?duplicar=N abre-a com os dados da produção N, sem datas nem nº (o início
/// é o dia escolhido no calendário, quando há). Guardar usa o mesmo POST
/// canónico do Planeamento (Create, com as mesmas recusas tipadas) e volta
/// aqui; uma recusa volta a esta folha com tudo o que se escreveu (OnPostRecusa
/// só desenha, não grava).
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
    CurrentUserCapabilities capabilities,
    JobOnLotNoteHandler lotNotes,
    JobOnChangeQuery changes,
    IDeleteJobOnHandler deleteJobOn,
    IJobOnPaginaQuery jobOnPagina,
    IConfiguration configuration,
    DMO.Alpha.Core.Tools.IFerramentasUsoSap usoSap) : PageModel
{
    /// <summary>O aviso «não está registada no Armazém» (o mesmo interruptor do Planeamento, Dmo:Avisos:ArmazemSemRegisto).</summary>
    public bool MostrarAvisoArmazem { get; } = configuration.GetValue(IndexModel.AvisoArmazemChave, false);

    /// <summary>
    /// Os avisos depois de gravar, os mesmos do Planeamento (a ficha antiga
    /// deixou de existir, Diogo 23/10/2026): a sobreposição na linha com as
    /// produções em conflito, a calote ignorada e o fim antes do início.
    /// </summary>
    public OverlapWarning? AvisoSobreposicao { get; private set; }

    public string? AvisoCalote { get; private set; }

    public string? AvisoFimAntesDoInicio { get; private set; }

    /// <summary>
    /// «O que mudou» desde o início, no topo da folha bloqueada: veio da
    /// ficha antiga do Planeamento, que deixou de existir (Diogo, 23/10/2026).
    /// </summary>
    public IReadOnlyList<JobOnSummaryEntry> Resumo { get; private set; } = [];

    /// <summary>As entradas ativas de «O que mudou» (sem as removidas por engano): a contagem do título.</summary>
    public int ResumoAtivas => Resumo.Count(e => !e.IsRemoved);

    /// <summary>Depois do «Remover por engano»: a confirmação.</summary>
    public bool AlteracaoRemovida { get; private set; }

    /// <summary>O link dos avisos dos módulos (?vista=alteracoes): «O que mudou» já aberto.</summary>
    public bool VistaAlteracoes { get; private set; }

    /// <summary>A confirmação do «Eliminar Job On» (nas Opções; veio da ficha antiga).</summary>
    public JobOnEliminarAviso? Eliminar { get; private set; }

    /// <summary>A produção aberta; null sem jobon_id ou quando não existe.</summary>
    public ProductionContextReadModel? JobOn { get; private set; }

    /// <summary>Pediu-se uma produção que não existe.</summary>
    public bool NaoExiste { get; private set; }

    /// <summary>A folha está desbloqueada (Editar).</summary>
    public bool Editar { get; private set; }

    /// <summary>O Job On novo antes da referência: o template vazio, sem Editar; só a Referência e a Produção.</summary>
    public bool Escolher { get; private set; }

    /// <summary>A folha é de um Job On novo (Criar ou Duplicar): guarda pelo Create.</summary>
    public bool Criar { get; private set; }

    /// <summary>No Duplicar, a produção de origem.</summary>
    public int? DuplicarDe { get; private set; }

    /// <summary>O nº da produção de origem do Duplicar (o título).</summary>
    public string? DuplicarDeNumero { get; private set; }

    /// <summary>A mensagem da recusa tipada do Create (a folha volta com o que se escreveu).</summary>
    public string? RecusaCriar { get; private set; }

    /// <summary>O dia escolhido no calendário do Planeamento (o início do Duplicar).</summary>
    public DateOnly? DiaPlaneamento { get; private set; }

    /// <summary>O «Duplicar» da folha: a folha nova com os dados desta produção.</summary>
    public string DuplicarHref => JobOn is null ? "/JobOn/Folha?novo=1" + NovoDestino
        : $"/JobOn/Folha?duplicar={JobOn.JobOnId}{NovoDestino}";

    /// <summary>
    /// A linha e o dia escolhidos no painel do Planeamento (uma linha vazia,
    /// Diogo 23/10/2026): a folha abre em branco, sem Editar, para procurar um
    /// Job On antigo; o Duplicar e o Criar levam já essa linha e esse início.
    /// </summary>
    public int? NovoMachineId { get; private set; }
    public DateOnly? NovoInicio { get; private set; }
    public string? NovoLinha { get; private set; }

    /// <summary>O «&amp;machine_id=…&amp;inicio_em=…» que segue de página em página.</summary>
    public string NovoDestino =>
        (NovoMachineId is int m ? $"&machine_id={m}" : "")
        + ((NovoInicio ?? DiaPlaneamento) is { } d ? $"&inicio_em={d:yyyy-MM-dd}" : "");

    /// <summary>O Cancelar: no Editar a folha bloqueada; no Duplicar a de origem; no Criar o Planeamento.</summary>
    public string CancelarHref => !Criar ? Href() : DuplicarDe is { } origem ? $"/JobOn/Folha?jobon_id={origem}" : "/JobOn";

    /// <summary>Quem vê pode editar (jobon.create).</summary>
    public bool PodeEditar { get; private set; }

    /// <summary>Quem vê tem um módulo do Controlo (a opção «Controlo»).</summary>
    public bool VeControlo { get; private set; }

    /// <summary>Tem a chave das Boquilhas: vê «BQs» nas Opções (os movimentos da BQ deste Job On).</summary>
    public bool VeBoquilhas { get; private set; }
    /// <summary>A capacidade de abrir a ficha da ferramenta (o link do aviso do Armazém).</summary>
    public IReadOnlyCollection<string> Capacidades { get; private set; } = [];

    /// <summary>A folha da fábrica gravada.</summary>
    public FolhaDoJobOnVista Folha { get; private set; } = FolhaDoJobOnVista.Vazia();

    /// <summary>Os factos das ferramentas da produção (referência, lote, % de uso).</summary>
    public IReadOnlyList<JobOnToolFacts> Facts { get; private set; } = [];

    /// <summary>As linhas (o select do Editar).</summary>
    public IReadOnlyList<MachineOption> Machines { get; private set; } = [];

    /// <summary>O painel lateral: as produções em curso e as que entram esta semana.</summary>
    public IReadOnlyList<ProductionContextReadModel> ProximasEntradas { get; private set; } = [];


    public bool Gravado { get; private set; }

    /// <summary>O nº de produção escrito no Criar (vazio no Duplicar; o escrito depois de uma recusa).</summary>
    public string? NumeroEscrito { get; private set; }

    /// <summary>A referência escrita na procura do cabeçalho.</summary>
    public string? ReferenciaProcurada { get; private set; }

    /// <summary>A referência procurada não tem produções.</summary>
    public bool ReferenciaSemProducoes { get; private set; }

    /// <summary>As últimas produções da mesma referência (o dropdown do Planeamento): escolher uma abre a folha dela.</summary>
    public IReadOnlyList<ProductionSearchItem> Producoes { get; private set; } = [];

    /// <summary>O lote sem zeros à esquerda («Lote 1»).</summary>
    public static string Lote(string? lote) => DMO.Alpha.Web.Presentation.LoteTexto.Mostrar(lote);

    /// <summary>O % de uso da última leitura de cada ferramenta da folha.</summary>
    public IReadOnlyDictionary<string, decimal> Usos { get; private set; } = new Dictionary<string, decimal>();

    /// <summary>A classe da ferramenta com o uso cheio (só cor ligeira), ou null.</summary>
    public string? UsoCheioDe(string? toolId) =>
        toolId is not null && Usos.TryGetValue(toolId, out var uso) ? UsoCheio.ClasseDe(uso) : null;

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
        JobOn is not null && !Criar && !string.Equals(ReferenciaTotal, JobOn.Reference, StringComparison.OrdinalIgnoreCase)
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

    /// <summary>
    /// O regresso ao Planeamento: o cabeçalho do painel lateral é só
    /// «Planeamento», um link para /JobOn (Diogo, 23/10/2026).
    /// </summary>
    public const string PlaneamentoHref = "/JobOn";

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "jobon_id")] int? jobOnId,
        [FromQuery(Name = "editar")] string? editar,
        [FromQuery(Name = "gravado")] string? gravado,
        [FromQuery(Name = "overlap")] string? overlap,
        [FromQuery(Name = "calote_ignorada")] string? caloteIgnorada,
        [FromQuery(Name = "fim_antes_inicio")] string? fimAntesInicio,
        [FromQuery(Name = "referencia")] string? referencia = null,
        [FromQuery(Name = "novo")] string? novo = null,
        [FromQuery(Name = "duplicar")] int? duplicar = null,
        [FromQuery(Name = "machine_id")] int? machineId = null,
        [FromQuery(Name = "inicio_em")] string? inicioEm = null,
        [FromQuery(Name = "dia")] string? dia = null,
        [FromQuery(Name = "vista")] string? vista = null,
        [FromQuery(Name = "removida")] string? removida = null,
        [FromQuery(Name = "etapa")] string? etapa = null,
        [FromQuery(Name = "producao")] string? producao = null)
    {
        VistaAlteracoes = vista == "alteracoes";
        AlteracaoRemovida = removida == "1";
        await ComporPaginaAsync();
        var ct = HttpContext.RequestAborted;
        DiaPlaneamento = DateOnly.TryParseExact(dia, "yyyy-MM-dd", out var diaValor) ? diaValor : null;
        NovoMachineId = machineId;
        NovoInicio = DateOnly.TryParseExact(inicioEm, "yyyy-MM-dd", out var novoInicio) ? novoInicio : null;
        if (machineId is not null && novo != "1" && duplicar is null)
        {
            NovoLinha = (await machineList.ListAsync(ct)).FirstOrDefault(m => m.MachineId == machineId)?.Code;
        }

        // Criar e Duplicar abrem a folha em Editar; só quem tem o Planeamento.
        if (novo == "1" || duplicar is not null)
        {
            if (!PodeEditar)
            {
                return Redirect(duplicar is int origemSemEditar ? $"/JobOn/Folha?jobon_id={origemSemEditar}" : "/JobOn/Folha");
            }
            var inicio = DateOnly.TryParseExact(inicioEm, "yyyy-MM-dd", out var inicioValor) ? inicioValor : (DateOnly?)null;
            Machines = await machineList.ListAsync(ct);
            // O JOB ON NOVO (Diogo, 23/10/2026: «primeiro procura-se ou escreve-se a
            // referência e depois sim criar/editar»; «template vazio, deixa escrever
            // referência e produção, simples»): o template vazio à vista, sem Editar;
            // escrita a referência, abre a última dela para ver e duplicar («muitas vezes
            // procuram a partir do template e só depois fazem duplicar»), ou em branco em Editar.
            var numeroNovo = producao?.Trim() is { Length: > 0 } pn ? pn : null;
            if (duplicar is null && etapa == "criar" && referencia?.Trim().ToUpperInvariant() is { Length: > 0 } refNova)
            {
                var dela = await productionPicker.ListOfReferenceAsync(refNova, ct);
                if (dela.Count > 0)
                {
                    var ultima = dela.OrderByDescending(p => p.ProductionNumber, StringComparer.Ordinal).First();
                    return Redirect($"/JobOn/Folha?jobon_id={ultima.JobOnId}{NovoDestino}");
                }
                var maquinaNova = Machines.FirstOrDefault(m => m.MachineId == machineId)?.Code ?? string.Empty;
                await AbrirCriarAsync(
                    new ProductionContextReadModel(0, refNova, string.Empty, maquinaNova, null, null, null, null, null, null, inicio),
                    FolhaDoJobOnVista.Vazia(), numero: numeroNovo, origemId: null, origemNumero: null, recusa: null);
                return Page();
            }
            if (duplicar is int origemId)
            {
                if (await productionContext.GetAsync(origemId, ct) is not { } origem)
                {
                    NaoExiste = true;
                    return Page();
                }
                // Os dados copiados; o nº e as datas da produção nova ficam por
                // preencher (o início é o dia escolhido no calendário).
                // Vindo de uma linha do painel, a produção nova vai para essa linha.
                var linhaEscolhida = Machines.FirstOrDefault(m => m.MachineId == machineId)?.Code;
                await AbrirCriarAsync(
                    origem with { JobOnId = 0, ProductionNumber = string.Empty, InicioEm = inicio, FimEm = null, Version = 0, Machine = linhaEscolhida ?? origem.Machine },
                    FolhaDoJobOnVista.DoDuplicar(await folha.ForJobOnAsync(origem.JobOnId, ct)),
                    numero: numeroNovo, origem.JobOnId, origem.ProductionNumber, recusa: null);
                return Page();
            }
            var maquina = Machines.FirstOrDefault(m => m.MachineId == machineId)?.Code ?? string.Empty;
            await AbrirCriarAsync(
                new ProductionContextReadModel(0, string.Empty, string.Empty, maquina, null, null, null, null, null, null, inicio),
                FolhaDoJobOnVista.Vazia(), numero: null, origemId: null, origemNumero: null, recusa: null);
            if (etapa != "criar")
            {
                Editar = false;
                Escolher = true;
            }
            return Page();
        }

        // A procura pela referência (o campo do cabeçalho): abre a última produção dela.
        if (jobOnId is null && referencia?.Trim() is { Length: > 0 } procurada)
        {
            ReferenciaProcurada = procurada;
            var dela = await productionPicker.ListOfReferenceAsync(procurada, ct);
            if (dela.Count > 0)
            {
                var ultima = dela.OrderByDescending(p => p.ProductionNumber, StringComparer.Ordinal).First();
                return Redirect($"/JobOn/Folha?jobon_id={ultima.JobOnId}{NovoDestino}");
            }
            ReferenciaSemProducoes = true;
        }

        return await AbrirAsync(jobOnId, editar, gravado, overlap, caloteIgnorada, fimAntesInicio);
    }

    /// <summary>
    /// A recusa do Create pedido a partir desta folha (o Planeamento reenvia o
    /// mesmo formulário com 307): só desenha a folha em Criar com tudo o que
    /// se escreveu e a mensagem da recusa. Não grava nada.
    /// </summary>
    public async Task<IActionResult> OnPostRecusaAsync(
        [FromQuery(Name = "create_refusal")] string? createRefusal,
        [FromQuery(Name = "chave")] string? chave,
        [FromForm(Name = "production_number")] string? productionNumber,
        [FromForm(Name = "reference")] string? reference,
        [FromForm(Name = "machine_id")] string? machineId,
        [FromForm(Name = "cm_tool_id")] string? cmToolId,
        [FromForm(Name = "mf_tool_id")] string? mfToolId,
        [FromForm(Name = "bq_tool_id")] string? bqToolId,
        [FromForm(Name = "inicio_em")] string? inicioEm,
        [FromForm(Name = "fim_em")] string? fimEm,
        [FromForm(Name = "calote_mm")] string? caloteMm,
        [FromForm(Name = "duplicar_de")] int? duplicarDe)
    {
        await ComporPaginaAsync();
        if (!PodeEditar)
        {
            return Redirect("/JobOn/Folha");
        }
        var ct = HttpContext.RequestAborted;
        Machines = await machineList.ListAsync(ct);
        var resultado = Enum.TryParse(createRefusal, out CreateJobOnResult r) && r != CreateJobOnResult.Success ? r : CreateJobOnResult.MissingReference;
        var folhaEscrita = FolhaDoJobOnVista.DoEnvio(EnvioDaFolha.Ler(Request.Form), string.IsNullOrWhiteSpace(chave) ? null : chave);
        static string? Limpo(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        var maquina = int.TryParse(machineId, out var mid) ? Machines.FirstOrDefault(m => m.MachineId == mid)?.Code : null;
        decimal? calote = decimal.TryParse(caloteMm?.Replace(',', '.'), System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : null;
        var escrito = new ProductionContextReadModel(
            0, (Limpo(reference) ?? string.Empty).ToUpperInvariant(), string.Empty, maquina ?? string.Empty,
            null, Limpo(cmToolId), null, Limpo(mfToolId), null, Limpo(bqToolId),
            DateOnly.TryParse(inicioEm, out var i) ? i : null,
            DateOnly.TryParse(fimEm, out var f) ? f : null,
            calote);
        var origem = duplicarDe is int o ? await productionContext.GetAsync(o, ct) : null;
        await AbrirCriarAsync(escrito, folhaEscrita, Limpo(productionNumber), origem?.JobOnId, origem?.ProductionNumber,
            IndexModel.MensagemRecusaCriar(resultado, folhaEscrita));
        return Page();
    }

    private async Task AbrirCriarAsync(
        ProductionContextReadModel novo, FolhaDoJobOnVista folhaNova, string? numero,
        int? origemId, string? origemNumero, string? recusa)
    {
        Criar = true;
        Editar = true;
        JobOn = novo;
        Folha = folhaNova;
        NumeroEscrito = numero;
        DuplicarDe = origemId;
        DuplicarDeNumero = origemNumero;
        RecusaCriar = recusa;
        var ids = new[] { novo.CmToolId, novo.MfToolId, novo.BqToolId }.OfType<string>().ToList();
        if (ids.Count > 0)
        {
            Facts = await toolFacts.FindAsync(ids, novo.Machine.Length == 0 ? null : novo.Machine, null, novo.InicioEm, novo.FimEm, HttpContext.RequestAborted);
            Usos = await usoSap.UltimasPercentagensAsync(ids, HttpContext.RequestAborted);
        }
    }

    private async Task ComporPaginaAsync()
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
        Capacidades = await capabilities.GetAsync();

        var hoje = DateOnly.FromDateTime(DmoBusinessTime.Now);
        var domingo = hoje.AddDays((7 - (int)hoje.DayOfWeek) % 7);
        ProximasEntradas = IndexModel.ProximasEntradasDaSemana(await jobOnList.ListWindowAsync(hoje, domingo, ct), hoje);
    }

    private async Task<IActionResult> AbrirAsync(int? jobOnId, string? editar, string? gravado, string? overlap, string? caloteIgnorada, string? fimAntesInicio)
    {
        var ct = HttpContext.RequestAborted;
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
                Usos = await usoSap.UltimasPercentagensAsync(ids, ct);
            }
            if (Editar)
            {
                Machines = await machineList.ListAsync(ct);
            }
            else if (PodeEditar)
            {
                // O que mudou e o Eliminar, como na ficha antiga, só a quem tem o Planeamento.
                var notas = await lotNotes.ForJobOnAsync(aberto.JobOnId, ct);
                var resumo = JobOnSummaryEntry.Compose(aberto.JobOnId, aberto.InicioEm, await changes.ForJobOnAsync(aberto.JobOnId, ct), notas);
                var permitidas = new HashSet<string>();
                foreach (var capability in resumo.Select(e => e.ActionCapability).Distinct())
                {
                    if (await capabilities.HasAsync(capability, ct)) permitidas.Add(capability);
                }
                Resumo = JobOnSummaryEntry.ForCapabilities(resumo, permitidas.Contains);
                if (await deleteJobOn.PreviewAsync(aberto.JobOnId, DateOnly.FromDateTime(DmoBusinessTime.Now), ct) is { } preview)
                {
                    Eliminar = JobOnEliminarAviso.From(preview, VeControlo);
                }
            }

            Gravado = gravado == "1";
            if (overlap == "1")
            {
                AvisoSobreposicao = await jobOnPagina.AvisoDeSobreposicaoAsync(
                    aberto.JobOnId, aberto.Machine, aberto.InicioEm, aberto.FimEm, ct);
            }
            if (caloteIgnorada?.Trim() is { Length: > 0 } valorIgnorado)
            {
                AvisoCalote = IndexModel.MensagemCaloteIgnorada(valorIgnorado, aberto.CaloteMm);
            }
            if (fimAntesInicio == "1" && aberto is { InicioEm: { } inicio, FimEm: { } fim } && fim < inicio)
            {
                AvisoFimAntesDoInicio = IndexModel.MensagemFimAntesDoInicio(inicio, fim);
            }
        }

        return Page();
    }
}

using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// A folha PU NNPB (o Owner 17/10/2026): o registo das punções de uma
/// produção NNPB, ancorado ao controlo_id como os Pegamentos. A
/// identificação (referência da punção e o lote dos próximos registos), a
/// lista das punções com «punção visto» e «arrefecedor visto» (marcados
/// por omissão) e os três totais. Como no Peso, o registo também nasce
/// sem Job On (?puncoes_id, a referência total escrita à mão) e é
/// associado depois à mão a uma produção NNPB. Cada gesto é um POST
/// nativo com PRG; as recusas voltam por query com o vocabulário fechado
/// do enum.
/// </summary>
[Authorize]
[ModuleEntry(DmoCapabilities.ControloCreate)]
public sealed class PuncoesModel : PageModel
{
    /// <summary>O valor de ?modo que abre a escolha do Job On.</summary>
    public const string ModoJobOnValor = "jobon";

    private readonly PuncoesHandler _handler;
    private readonly PuncoesPdfGenerator _pdfGenerator;
    private readonly ShellStateFactory _shellState;
    private readonly IJobOnListQuery _jobOnList;
    private readonly IEnsureControloForJobOnHandler _ensureControlo;

    public PuncoesModel(
        PuncoesHandler handler,
        PuncoesPdfGenerator pdfGenerator,
        ShellStateFactory shellState,
        IJobOnListQuery jobOnList,
        IEnsureControloForJobOnHandler ensureControlo)
    {
        _handler = handler;
        _pdfGenerator = pdfGenerator;
        _shellState = shellState;
        _jobOnList = jobOnList;
        _ensureControlo = ensureControlo;
    }

    /// <summary>A folha do controlo aberto (?controlo_id); null fora desse contexto.</summary>
    public PuncoesSheet? Sheet { get; private set; }

    /// <summary>O registo sem Job On aberto (?puncoes_id); null fora desse contexto.</summary>
    public PuncoesStandaloneSheet? Standalone { get; private set; }

    public int? RequestedControloId { get; private set; }

    public int? RequestedPuncoesId { get; private set; }

    /// <summary>True quando foi pedido um controlo_id que não existe.</summary>
    public bool ControloRequestedNotFound { get; private set; }

    /// <summary>True quando foi pedido um puncoes_id que não existe.</summary>
    public bool RegistoRequestedNotFound { get; private set; }

    /// <summary>True na escolha do Job On (?modo=jobon) sem folha aberta.</summary>
    public bool ModoJobOn { get; private set; }

    /// <summary>A referência procurada (primeiro passo da procura).</summary>
    public string? ReferenciaProcurada { get; private set; }

    /// <summary>As produções da referência procurada (o segundo passo).</summary>
    public IReadOnlyList<ProductionContextReadModel> Producoes { get; private set; } = Array.Empty<ProductionContextReadModel>();

    /// <summary>Os registos sem Job On da referência procurada (ou da produção aberta).</summary>
    public IReadOnlyList<PuncoesStandaloneSummary> RegistosSemJobOn { get; private set; } = Array.Empty<PuncoesStandaloneSummary>();

    /// <summary>Recusa do último POST (PRG).</summary>
    public PuncoesResult? Refusal { get; private set; }

    /// <summary>Recusa da escolha do Job On («sem-escolha» ou «inexistente»).</summary>
    public string? JobOnRefusal { get; private set; }

    /// <summary>Os valores escritos que a recusa devolve ao formulário.</summary>
    public string? EchoNumber { get; private set; }
    public string? EchoPunchReference { get; private set; }
    public string? EchoLot { get; private set; }
    public string? EchoReference { get; private set; }

    public string RefusalMessage => Refusal switch
    {
        PuncoesResult.ControloNotFound => "O controlo indicado não existe. Nada foi registado.",
        PuncoesResult.NotNnpb => "Esta produção não é NNPB: o registo das punções só existe quando a CM é NNPB. Nada foi registado.",
        PuncoesResult.MissingPunchReference => "Indique a referência da punção. Nada foi registado.",
        PuncoesResult.InvalidLot => "Indique o lote com um número inteiro (1, 2, 3…). Nada foi registado.",
        PuncoesResult.MissingNumber => "Indique o número da punção. Nada foi registado.",
        PuncoesResult.InspecaoNotFound => "Essa punção já não existe neste registo. Nada foi alterado.",
        PuncoesResult.MissingReference => "Indique a referência total. Nada foi registado.",
        PuncoesResult.RegistoNotFound => "Esse registo já não existe. Nada foi alterado.",
        PuncoesResult.AlreadyAssociated => "Este registo já está associado a uma produção. Nada foi alterado.",
        PuncoesResult.ControloHasRegisto => "Essa produção já tem um registo das punções. Nada foi associado.",
        _ => string.Empty
    };

    public string? JobOnRefusalMessage => JobOnRefusal switch
    {
        "sem-escolha" => "Escolha a produção.",
        "inexistente" => "A produção escolhida não existe. Nada foi alterado.",
        _ => null
    };

    /// <summary>O valor mostrado num campo: o eco da recusa, o registo, ou a sugestão.</summary>
    public string? ShownPunchReference => EchoPunchReference
        ?? Sheet?.Registo?.PunchReference ?? Sheet?.DefaultPunchReference
        ?? Standalone?.Registo.PunchReference;
    public string? ShownLot => EchoLot ?? Sheet?.Registo?.Lot ?? Standalone?.Registo.Lot;
    public string? ShownReference => EchoReference ?? Standalone?.Reference;

    /// <summary>O registo mostrado na lista: o da produção ou o registo sem Job On.</summary>
    public PuncoesReadModel? Registo => Sheet?.Registo ?? Standalone?.Registo;

    /// <summary>A folha aceita registos: produção NNPB, um registo da produção, ou um registo sem Job On.</summary>
    public bool CanRegister => Sheet is { IsNnpb: true } || Sheet?.Registo is not null || Standalone is not null;

    /// <summary>O campo escondido que diz a folha aos POSTs: controlo_id ou puncoes_id.</summary>
    public (string Name, int Value)? TargetField => Sheet is { } sheet
        ? ("controlo_id", sheet.ControloId)
        : Standalone is { } standalone ? ("puncoes_id", standalone.PuncoesId) : null;

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "controlo_id")] int? controloId,
        [FromQuery(Name = "puncoes_id")] int? puncoesId,
        [FromQuery(Name = "modo")] string? modo,
        [FromQuery(Name = "procura")] string? procura,
        [FromQuery(Name = "recusa")] string? refusal,
        [FromQuery(Name = "jobon_recusa")] string? jobOnRefusal,
        [FromQuery(Name = "numero")] string? number,
        [FromQuery(Name = "referencia")] string? punchReference,
        [FromQuery(Name = "lote")] string? lot,
        [FromQuery(Name = "referencia_total")] string? reference)
    {
        ViewData["Title"] = "PU NNPB — Controlo";
        var shell = (await _shellState.ForDestinationAsync("ModuleFunctions", new ModulePresentation("controlo", "Controlo"))) with
        {
            ActiveFunctionKey = "puncoes",
            Functions = ShellStateFactory.ControloFunctions("puncoes", controloId: controloId)
        };
        ViewData["Shell"] = shell;

        if (refusal is not null && Enum.TryParse(refusal, out PuncoesResult parsed) && parsed != PuncoesResult.Success)
        {
            Refusal = parsed;
            EchoNumber = Limit(number, PuncaoInspecao.NumberMaxLength);
            EchoPunchReference = Limit(punchReference, DMO.Alpha.Core.Controlo.Puncoes.PunchReferenceMaxLength);
            EchoLot = Limit(lot, DMO.Alpha.Core.Controlo.Puncoes.LotMaxLength);
            EchoReference = Limit(reference, DMO.Alpha.Core.Controlo.Puncoes.ReferenceMaxLength);
        }

        JobOnRefusal = jobOnRefusal;
        RequestedControloId = controloId;
        RequestedPuncoesId = puncoesId;
        ReferenciaProcurada = PuncoesHandler.CanonicalReference(procura);

        if (controloId is int canonicalControloId)
        {
            Sheet = await _handler.GetSheetAsync(canonicalControloId, HttpContext.RequestAborted);
            ControloRequestedNotFound = Sheet is null;
            if (Sheet is { Registo: null, IsNnpb: true } sheet)
            {
                // A produção NNPB ainda sem registo pode receber à mão um
                // registo sem Job On da mesma referência.
                RegistosSemJobOn = await _handler.ListStandaloneAsync(sheet.Reference, HttpContext.RequestAborted);
            }

            ViewData["Shell"] = shell with
            {
                Functions = ShellStateFactory.ControloFunctions("puncoes", Sheet?.JobOnId, controloId)
            };
            return Page();
        }

        if (puncoesId is int canonicalPuncoesId)
        {
            Standalone = await _handler.GetStandaloneAsync(canonicalPuncoesId, HttpContext.RequestAborted);
            RegistoRequestedNotFound = Standalone is null;
            if (Standalone?.ControloId is int associado)
            {
                return RedirectToPage("/Controlo/Puncoes", new { controlo_id = associado });
            }

            if (Standalone is { } standalone)
            {
                // A associação à mão: procura-se a referência (por omissão a
                // do registo) e escolhe-se a produção.
                ReferenciaProcurada ??= standalone.Reference;
                Producoes = await _jobOnList.ListByReferenceAsync(ReferenciaProcurada, null, HttpContext.RequestAborted);
            }

            return Page();
        }

        ModoJobOn = modo == ModoJobOnValor;
        if (ReferenciaProcurada is { } procurada)
        {
            if (ModoJobOn)
            {
                Producoes = await _jobOnList.ListByReferenceAsync(procurada, null, HttpContext.RequestAborted);
            }
            else
            {
                RegistosSemJobOn = await _handler.ListStandaloneAsync(procurada, HttpContext.RequestAborted);
            }
        }

        return Page();
    }

    /// <summary>Começa um registo sem Job On e abre-o pelo puncoes_id.</summary>
    public async Task<IActionResult> OnPostCreateAsync(
        [FromForm(Name = "referencia_total")] string? reference,
        [FromForm(Name = "referencia")] string? punchReference,
        [FromForm(Name = "lote")] string? lot)
    {
        var (result, puncoesId) = await _handler.CreateStandaloneAsync(reference, punchReference, lot, Operator(), HttpContext.RequestAborted);
        if (result == PuncoesResult.Success && puncoesId is int created)
        {
            return RedirectToPage("/Controlo/Puncoes", null, new { puncoes_id = created }, "puncoes-lista");
        }

        return RedirectToPage("/Controlo/Puncoes", null, new
        {
            procura = Limit(reference, DMO.Alpha.Core.Controlo.Puncoes.ReferenceMaxLength),
            recusa = result.ToString(),
            referencia = Limit(punchReference, DMO.Alpha.Core.Controlo.Puncoes.PunchReferenceMaxLength),
            lote = Limit(lot, DMO.Alpha.Core.Controlo.Puncoes.LotMaxLength),
            referencia_total = Limit(reference, DMO.Alpha.Core.Controlo.Puncoes.ReferenceMaxLength)
        }, "puncoes-novo");
    }

    /// <summary>Abre a folha da produção escolhida (o controlo_id nasce do Job On, como no Peso).</summary>
    public async Task<IActionResult> OnPostAbrirJobOnAsync([FromForm(Name = "jobon_id")] int? jobOnId)
    {
        if (jobOnId is not int escolhido)
        {
            return RedirectToPage("/Controlo/Puncoes", new { modo = ModoJobOnValor, jobon_recusa = "sem-escolha" });
        }

        var ensured = await _ensureControlo.HandleAsync(new EnsureControloForJobOn(escolhido), HttpContext.RequestAborted);
        return ensured.ControloId is int controloId
            ? RedirectToPage("/Controlo/Puncoes", new { controlo_id = controloId })
            : RedirectToPage("/Controlo/Puncoes", new { modo = ModoJobOnValor, jobon_recusa = "inexistente" });
    }

    /// <summary>
    /// Associa à mão o registo sem Job On à produção escolhida (pelo
    /// jobon_id, que dá o controlo_id) ou a uma produção já aberta. Uma
    /// recusa no registo sem Job On volta com a referência procurada
    /// (procura): a zona do «Associar» troca sem perder a procura.
    /// </summary>
    public async Task<IActionResult> OnPostAssociateAsync(
        [FromForm(Name = "puncoes_id")] int puncoesId,
        [FromForm(Name = "jobon_id")] int? jobOnId,
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "origem")] string? origem,
        [FromForm(Name = "procura")] string? procura = null)
    {
        procura = PuncoesHandler.CanonicalReference(procura);
        var back = origem == "controlo" && controloId is int origemControlo
            ? (object)new { controlo_id = origemControlo }
            : new { puncoes_id = puncoesId, procura };

        if (controloId is not int destino)
        {
            if (jobOnId is not int escolhido)
            {
                return RedirectToPage("/Controlo/Puncoes", null, new { puncoes_id = puncoesId, procura, jobon_recusa = "sem-escolha" }, "puncoes-associar");
            }

            var ensured = await _ensureControlo.HandleAsync(new EnsureControloForJobOn(escolhido), HttpContext.RequestAborted);
            if (ensured.ControloId is not int ensuredControlo)
            {
                return RedirectToPage("/Controlo/Puncoes", null, new { puncoes_id = puncoesId, procura, jobon_recusa = "inexistente" }, "puncoes-associar");
            }

            destino = ensuredControlo;
        }

        var result = await _handler.AssociateAsync(puncoesId, destino, Operator(), HttpContext.RequestAborted);
        if (result == PuncoesResult.Success)
        {
            return RedirectToPage("/Controlo/Puncoes", null, new { controlo_id = destino }, "puncoes-lista");
        }

        var refused = new RouteValueDictionary(back) { ["recusa"] = result.ToString() };
        return RedirectToPage("/Controlo/Puncoes", null, refused, origem == "controlo" ? "puncoes-identificacao" : "puncoes-associar");
    }

    public async Task<IActionResult> OnPostIdentificationAsync(
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "puncoes_id")] int? puncoesId,
        [FromForm(Name = "referencia")] string? punchReference,
        [FromForm(Name = "lote")] string? lot,
        [FromForm(Name = "referencia_total")] string? reference)
    {
        var target = Target(controloId, puncoesId);
        var result = await _handler.SaveIdentificationAsync(target, punchReference, lot, Operator(), HttpContext.RequestAborted,
            target.PuncoesId is null ? null : reference ?? string.Empty);
        return result == PuncoesResult.Success
            ? Back(target, "puncoes-identificacao")
            : Refused(target, result, null, punchReference, lot, reference);
    }

    // O mesmo envio outra vez (a ligação caiu depois de gravar) não grava
    // duas vezes (deep sweep, 18/10/2026, B-pegamentos-3).
    [EnvioUnico]
    public async Task<IActionResult> OnPostAddAsync(
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "puncoes_id")] int? puncoesId,
        [FromForm(Name = "numero")] string? number,
        [FromForm(Name = "referencia")] string? punchReference,
        [FromForm(Name = "lote")] string? lot,
        [FromForm(Name = "lote_puncao")] string? punchLot)
    {
        var target = Target(controloId, puncoesId);
        var result = await _handler.AddAsync(target, number, punchReference, lot, Operator(), HttpContext.RequestAborted, punchLot);
        return result == PuncoesResult.Success
            ? Back(target, "puncoes-lista")
            : Refused(target, result, number, punchReference, lot);
    }

    public async Task<IActionResult> OnPostSeenAsync(
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "puncoes_id")] int? puncoesId,
        [FromForm(Name = "inspecao_id")] int inspecaoId,
        [FromForm(Name = "campo")] string? field,
        [FromForm(Name = "visto")] bool seen)
    {
        var target = Target(controloId, puncoesId);
        var parsedField = field == "arrefecedor" ? PuncaoSeenField.Arrefecedor : PuncaoSeenField.Puncao;
        var result = await _handler.SetSeenAsync(target, inspecaoId, parsedField, seen, Operator(), HttpContext.RequestAborted);
        return result == PuncoesResult.Success ? Back(target, "puncoes-lista") : Refused(target, result);
    }

    public async Task<IActionResult> OnPostRemoveAsync(
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "puncoes_id")] int? puncoesId,
        [FromForm(Name = "inspecao_id")] int inspecaoId)
    {
        var target = Target(controloId, puncoesId);
        var result = await _handler.RemoveAsync(target, inspecaoId, Operator(), HttpContext.RequestAborted);
        return result == PuncoesResult.Success ? Back(target, "puncoes-lista") : Refused(target, result);
    }

    /// <summary>O PDF da folha, gerado em memória e descarregado; nunca grava no servidor.</summary>
    public async Task<IActionResult> OnPostGeneratePdfAsync(
        [FromForm(Name = "controlo_id")] int? controloId,
        [FromForm(Name = "puncoes_id")] int? puncoesId)
    {
        var target = Target(controloId, puncoesId);
        var generatedBy = User.FindFirst(DmoClaimTypes.DisplayName)?.Value ?? User.FindFirst(DmoClaimTypes.Email)?.Value;

        if (target.PuncoesId is int registoId)
        {
            var standalone = await _handler.GetStandaloneAsync(registoId, HttpContext.RequestAborted);
            if (standalone is null || standalone.ControloId is not null)
            {
                return Back(target, null);
            }

            var standaloneBytes = _pdfGenerator.GenerateBytes(PuncoesPdfGenerator.FromStandalone(standalone, generatedBy));
            Response.Headers[ControloDocumentPaths.PdfFolderHeader] = Uri.EscapeDataString(ControloDocumentPaths.GetProductionRelativeFolder(standalone.Reference, $"SEM-JOBON-{standalone.PuncoesId}"));
            return File(standaloneBytes, "application/pdf",
                ControloDocumentPaths.GetPuncoesFileName(standalone.Reference, $"SEM-JOBON-{standalone.PuncoesId}"));
        }

        var sheet = await _handler.GetSheetAsync(target.ControloId ?? 0, HttpContext.RequestAborted);
        if (sheet?.Registo is not { } registo)
        {
            return Back(target, null);
        }

        var bytes = _pdfGenerator.GenerateBytes(PuncoesPdfGenerator.FromRegisto(sheet, registo, generatedBy));
        Response.Headers[ControloDocumentPaths.PdfFolderHeader] = Uri.EscapeDataString(ControloDocumentPaths.GetProductionRelativeFolder(sheet.Reference, sheet.ProductionNumber));
        return File(bytes, "application/pdf", ControloDocumentPaths.GetPuncoesFileName(sheet.Reference, sheet.ProductionNumber));
    }

    private static PuncoesTarget Target(int? controloId, int? puncoesId) =>
        puncoesId is int registo ? PuncoesTarget.ForRegisto(registo) : PuncoesTarget.ForControlo(controloId ?? 0);

    private string? Operator() =>
        User.FindFirst(DmoClaimTypes.OperatorId)?.Value
        ?? User.FindFirst(DmoClaimTypes.Email)?.Value
        ?? User.FindFirst(DmoClaimTypes.DisplayName)?.Value;

    private static RouteValueDictionary TargetRoute(PuncoesTarget target) =>
        target.PuncoesId is int registo
            ? new RouteValueDictionary { ["puncoes_id"] = registo }
            : new RouteValueDictionary { ["controlo_id"] = target.ControloId };

    private IActionResult Back(PuncoesTarget target, string? fragment) =>
        RedirectToPage("/Controlo/Puncoes", null, TargetRoute(target), fragment);

    private IActionResult Refused(PuncoesTarget target, PuncoesResult result,
        string? number = null, string? punchReference = null, string? lot = null, string? reference = null)
    {
        var route = TargetRoute(target);
        route["recusa"] = result.ToString();
        route["numero"] = Limit(number, PuncaoInspecao.NumberMaxLength);
        route["referencia"] = Limit(punchReference, DMO.Alpha.Core.Controlo.Puncoes.PunchReferenceMaxLength);
        route["lote"] = Limit(lot, DMO.Alpha.Core.Controlo.Puncoes.LotMaxLength);
        route["referencia_total"] = Limit(reference, DMO.Alpha.Core.Controlo.Puncoes.ReferenceMaxLength);
        return RedirectToPage("/Controlo/Puncoes", null, route, "puncoes-identificacao");
    }

    private static string? Limit(string? value, int max)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}

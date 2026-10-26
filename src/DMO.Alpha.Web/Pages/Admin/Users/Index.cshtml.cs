using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.Users;

/// <summary>
/// A folha Admin → Utilizadores do corte I (USERS.md §1-§5): a lista dos
/// Users com FACTOS DE APLICAÇÃO — Nome, Nº Operador, Template (rótulo
/// corrente, resolvido pela relação persistida), Estado (Ativo/Arquivado) e
/// COM coluna Email (o Owner 2026-10-13: [o email tem de
/// (o email é a identidade do provider, vivida por pass-through — nunca
/// um facto da folha) e SEM ids visíveis (nenhum Guid/providerUserId —
/// as ações usam o nº de operador, o facto humano único). O email e a
/// password temporária aparecem UMA VEZ no resultado do criar/reset
/// (TempData — gestão do ADMIN, USERS.md §3), nunca na lista.
///
/// Criação na app (§2): invariáveis tipadas antes de qualquer escrita,
/// password temporária mintada pelo backend, provisionamento no Auth
/// provider (DMO nunca é password store) e persistência atómica do User +
/// atribuição opcional. Reset (§5) e arquivar (§1) por linha, com recusas
/// tipadas (o ADMIN corrente nunca é alvo). A atribuição de Template é o
/// select por linha («Sem template» = remover) — a MESMA relação
/// persistida que a vista Templates edita (TEMPLATES.md L18: uma relação,
/// duas vistas).
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public sealed class IndexModel : PageModel
{
    private readonly IUserAdminService _userAdminService;
    private readonly ITemplateAdminService _templateAdminService;
    private readonly ShellStateFactory _shellStateFactory;

    public IndexModel(
        IUserAdminService userAdminService,
        ITemplateAdminService templateAdminService,
        ShellStateFactory shellStateFactory)
    {
        _userAdminService = userAdminService;
        _templateAdminService = templateAdminService;
        _shellStateFactory = shellStateFactory;
    }

    /// <summary>A lista de Users (factos de aplicação, ordenados por nº de operador).</summary>
    public IReadOnlyList<UserAdminSummary> Users { get; private set; } = [];

    /// <summary>A paginação da lista (25 por página, feita na query).</summary>
    public ListPager UsersPager { get; private set; } = new(1, 1, 0);

    /// <summary>As Templates existentes (select por linha — a relação é a mesma).</summary>
    public IReadOnlyList<TemplateSummary> Templates { get; private set; } = [];

    /// <summary>Factos submetidos da criação, preservados pela volta PRG das recusas.</summary>
    public string? RequestedName { get; private set; }
    public string? RequestedOperatorId { get; private set; }
    public string? RequestedEmail { get; private set; }
    public string? RequestedTemplateKey { get; private set; }

    /// <summary>Recusa tipada do último POST de criação (transportada por query — PRG).</summary>
    public CreateDmoUserResult? CreateRefusal { get; private set; }

    /// <summary>Recusa tipada do último POST de atribuição de Template (PRG).</summary>
    public SetCurrentUserTemplateResult? TemplateRefusal { get; private set; }

    /// <summary>Recusa tipada do último POST de arquivar (PRG).</summary>
    public SetUserStandByResult? StandByRefusal { get; private set; }

    /// <summary>Recusa tipada do último POST de reset de acesso (PRG).</summary>
    public ResetUserAccessResult? ResetRefusal { get; private set; }

    /// <summary>
    /// O resultado UMA-VEZ do criar: o email (nunca persistido) e a
    /// password temporária mintada pelo backend — o token de entrega do
    /// ADMIN ao utilizador (TempData; não sobrevive a um refresh).
    /// </summary>
    public string? CreatedUserEmail { get; private set; }
    public string? CreatedUserTemporaryPassword { get; private set; }
    public string? CreatedUserName { get; private set; }
    public string? CreatedUserOperatorId { get; private set; }

    /// <summary>O resultado UMA-VEZ do reset: a NOVA password temporária (TempData).</summary>
    public string? ResetOperatorId { get; private set; }
    public string? ResetTemporaryPassword { get; private set; }

    /// <summary>Dono do email repetido (TempData UMA-VEZ, só em EmailInUseByUser).</summary>
    public string? EmailOwnerName { get; private set; }
    public string? EmailOwnerOperatorId { get; private set; }

    public string CreateRefusalMessage => CreateRefusal switch
    {
        CreateDmoUserResult.MissingName =>
            "O nome é obrigatório. Nenhum utilizador foi criado.",
        CreateDmoUserResult.MissingOperator =>
            "O nº de operador é obrigatório (até 4 dígitos). Nenhum utilizador foi criado.",
        CreateDmoUserResult.InvalidOperator =>
            "O nº de operador contém caracteres que não são dígitos. Nenhum utilizador foi criado.",
        CreateDmoUserResult.OperatorTooLong =>
            "O nº de operador tem mais de 4 dígitos. Nenhum utilizador foi criado.",
        CreateDmoUserResult.DuplicateOperator =>
            "Já existe um utilizador com esse nº de operador. Nenhum utilizador foi criado.",
        CreateDmoUserResult.MissingEmail =>
            "O email é obrigatório. Nenhum utilizador foi criado.",
        CreateDmoUserResult.UnknownTemplate =>
            "A template indicada não existe. Nenhum utilizador foi criado.",
        CreateDmoUserResult.EmailInUseByUser => EmailOwnerName is not null
            ? $"Esse email já é do utilizador {EmailOwnerName} (nº {EmailOwnerOperatorId}). Nenhum utilizador foi criado."
            : "Esse email já é de outro utilizador da lista. Nenhum utilizador foi criado.",
        CreateDmoUserResult.EmailExists =>
            "Esse email já existe no login, mas não é de nenhum utilizador da app. Nenhum utilizador foi criado.",
        CreateDmoUserResult.ProviderFailure =>
            "O provedor de autenticação recusou a criação. Nenhum utilizador foi criado.",
        CreateDmoUserResult.AdminUserRefused =>
            "Este email é o do ADMIN, que não é criado como utilizador. Nenhum utilizador foi criado.",
        _ => string.Empty
    };

    /// <summary>
    /// O link da recusa de criar para onde se resolve (Diogo, 19/10/2026):
    /// o nº de operador ou o email que já são de outro utilizador levam à
    /// ficha desse utilizador.
    /// </summary>
    public DMO.Alpha.Web.Pages.Shared.DmoAvisoLink? CreateRefusalLink => CreateRefusal switch
    {
        CreateDmoUserResult.DuplicateOperator when RequestedOperatorId is { } numero =>
            new($"Abrir a ficha do nº {numero}", $"/Admin/Users/Ficha?operator_id={Uri.EscapeDataString(numero)}"),
        CreateDmoUserResult.EmailInUseByUser when EmailOwnerOperatorId is { } dono =>
            new($"Abrir a ficha de {EmailOwnerName ?? $"nº {dono}"}", $"/Admin/Users/Ficha?operator_id={Uri.EscapeDataString(dono)}"),
        _ => null
    };

    public string TemplateRefusalMessage => TemplateRefusalText(TemplateRefusal);

    /// <summary>A recusa da template em texto (a lista e a ficha).</summary>
    internal static string TemplateRefusalText(SetCurrentUserTemplateResult? refusal) => refusal switch
    {
        SetCurrentUserTemplateResult.UserNotFound => "O utilizador indicado já não existe.",
        SetCurrentUserTemplateResult.AdminRefused =>
            "O ADMIN nunca tem template de acesso — a atribuição foi recusada.",
        SetCurrentUserTemplateResult.UnknownTemplate => "A template indicada não existe.",
        _ => string.Empty
    };

    public string StandByRefusalMessage => StandByRefusalText(StandByRefusal);

    /// <summary>A recusa de arquivar em texto (a lista e a ficha).</summary>
    internal static string StandByRefusalText(SetUserStandByResult? refusal) => refusal switch
    {
        SetUserStandByResult.UserNotFound => "O utilizador indicado já não existe.",
        SetUserStandByResult.AdminRefused => "O ADMIN não pode ser arquivado.",
        _ => string.Empty
    };

    public DeleteUserResult? DeleteRefusal { get; private set; }

    public string DeleteRefusalMessage => DeleteRefusal switch
    {
        DeleteUserResult.AdminRefused => "A identidade ADMIN não pode ser eliminada.",
        DeleteUserResult.UserNotFound => "O utilizador já não existe.",
        DeleteUserResult.ProviderFailure =>
            "O login não confirmou a eliminação. O utilizador DMO foi mantido.",
        DeleteUserResult.PersistenceFailure =>
            "O acesso ao login foi apagado, mas o utilizador DMO não foi removido. Repita a eliminação.",
        _ => string.Empty
    };

    /// <summary>Resultado da eliminação anterior (PRG): «1» ou «sem-identidade».</summary>
    public string? Deleted { get; private set; }

    public string DeletedMessage => Deleted switch
    {
        "1" => "Utilizador eliminado.",
        "sem-identidade" => "Utilizador eliminado. O acesso ao login já não existia.",
        _ => string.Empty
    };

    public string ResetRefusalMessage => ResetRefusalText(ResetRefusal);

    /// <summary>A recusa de repor o acesso em texto (a lista e a ficha).</summary>
    internal static string ResetRefusalText(ResetUserAccessResult? refusal) => refusal switch
    {
        ResetUserAccessResult.UserNotFound => "O utilizador indicado já não existe.",
        ResetUserAccessResult.AdminRefused => "Não é possível repor o acesso do ADMIN aqui.",
        ResetUserAccessResult.IdentityNotFound =>
            "A identidade de autenticação está em falta. Preencha o email na ficha do utilizador e volte a repor o acesso.",
        ResetUserAccessResult.EmailAlreadyInUse =>
            "O email já pertence a uma identidade de autenticação. A associação do utilizador não foi alterada.",
        ResetUserAccessResult.ProviderFailure =>
            "O provedor de autenticação recusou a reposição. O utilizador não foi alterado.",
        _ => string.Empty
    };

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "name")] string? name,
        [FromQuery(Name = "operator_id")] string? operatorId,
        [FromQuery(Name = "email")] string? email,
        [FromQuery(Name = "template_key")] string? templateKey,
        [FromQuery(Name = "create_refusal")] string? createRefusal,
        [FromQuery(Name = "template_refusal")] string? templateRefusal,
        [FromQuery(Name = "standby_refusal")] string? standbyRefusal,
        [FromQuery(Name = "reset_refusal")] string? resetRefusal,
        [FromQuery(Name = "delete_refusal")] string? deleteRefusal,
        [FromQuery(Name = "pagina")] int? pagina)
    {
        ViewData["Title"] = "Utilizadores";
        var shell = (await _shellStateFactory.ForDestinationAsync("admin", new ModulePresentation("admin", "Administração")));
        ViewData["Shell"] = shell with
        {
            ActiveFunctionKey = AdminAreaNavigation.UsersFunctionKey,
            Functions = AdminAreaNavigation.Functions(
                AdminAreaNavigation.UsersFunctionKey,
                Url.Page("/Admin/Users/Index"),
                Url.Page("/Admin/Templates/Index")),
            Planeamento = shell.Planeamento with { Visible = false }
        };

        // Resultado UMA-VEZ do criar/reset (TempData — gestão do ADMIN):
        // lido aqui e consumido; um refresh já não o mostra.
        CreatedUserEmail = TempData["AdminUserCreatedEmail"] as string;
        CreatedUserTemporaryPassword = TempData["AdminUserCreatedPassword"] as string;
        CreatedUserName = TempData["AdminUserCreatedName"] as string;
        CreatedUserOperatorId = TempData["AdminUserCreatedOperatorId"] as string;
        ResetOperatorId = TempData["AdminUserResetOperatorId"] as string;
        ResetTemporaryPassword = TempData["AdminUserResetPassword"] as string;
        EmailOwnerName = TempData["AdminUserEmailOwnerName"] as string;
        EmailOwnerOperatorId = TempData["AdminUserEmailOwnerOperatorId"] as string;

        // Recusas tipadas dos POSTs anteriores (PRG), pelo vocabulário
        // fechado dos enums — texto livre nunca volta das recusas.
        if (createRefusal is not null
            && Enum.TryParse(createRefusal, out CreateDmoUserResult parsedCreate)
            && parsedCreate != CreateDmoUserResult.Success)
        {
            CreateRefusal = parsedCreate;
        }

        if (templateRefusal is not null
            && Enum.TryParse(templateRefusal, out SetCurrentUserTemplateResult parsedTemplate)
            && parsedTemplate != SetCurrentUserTemplateResult.Success)
        {
            TemplateRefusal = parsedTemplate;
        }

        if (standbyRefusal is not null
            && Enum.TryParse(standbyRefusal, out SetUserStandByResult parsedStandBy)
            && parsedStandBy != SetUserStandByResult.Success)
        {
            StandByRefusal = parsedStandBy;
        }

        if (resetRefusal is not null
            && Enum.TryParse(resetRefusal, out ResetUserAccessResult parsedReset)
            && parsedReset != ResetUserAccessResult.Success)
        {
            ResetRefusal = parsedReset;
        }

        if (deleteRefusal is not null
            && Enum.TryParse(deleteRefusal, out DeleteUserResult parsedDelete)
            && parsedDelete is not (DeleteUserResult.Success or DeleteUserResult.SuccessIdentityAlreadyMissing))
            DeleteRefusal = parsedDelete;

        Deleted = Request.Query["deleted"].ToString() is "1" or "sem-identidade"
            ? Request.Query["deleted"].ToString()
            : null;

        // Factos submetidos (refrescam o form de criação após uma recusa).
        RequestedName = Sanitize(name);
        RequestedOperatorId = Sanitize(operatorId);
        RequestedEmail = Sanitize(email);
        RequestedTemplateKey = Sanitize(templateKey);

        // A LISTA SÓ TEM UTILIZADORES GERIDOS (o Owner 2026-10-16: «admin
        // nao deve aparecer na lista»): a conta ADMIN é o boundary, não um
        // utilizador desta folha — a sua linha não é listada. O serviço continua a devolver a projeção
        // completa (a FICHA usa-a para renderizar o boundary quando o nº é
        // aberto diretamente); o corte é da LISTA, não da autoridade.
        // A lista é paginada na query (25 por página); o corte do ADMIN
        // também, para a contagem bater certo.
        var usersPage = await _userAdminService.ListManagedUsersPageAsync(
            pagina, cancellationToken: HttpContext.RequestAborted);
        Users = usersPage.Items;
        UsersPager = ListPager.For(usersPage);
        Templates = await _templateAdminService.ListTemplatesAsync(HttpContext.RequestAborted);

        return Page();
    }

    /// <summary>
    /// POST: criar um utilizador na app (USERS.md §2 — o ato canónico do
    /// ADMIN). O form submete os factos humanos (nome, nº de operador) e
    /// a identidade a provisionar (email); a password temporária é SEMPRE
    /// mintada pelo backend. Sucesso → TempData UMA-VEZ (email + token de
    /// entrega) e lista; recusa tipada → volta por query (PRG) com os
    /// factos preservados.
    /// </summary>
    public async Task<IActionResult> OnPostCreateAsync(
        [FromForm(Name = "name")] string? name,
        [FromForm(Name = "operator_id")] string? operatorId,
        [FromForm(Name = "email")] string? email,
        [FromForm(Name = "template_key")] string? templateKey)
    {
        var outcome = await _userAdminService.CreateDmoUserAsync(
            new CreateDmoUser(
                name ?? string.Empty,
                operatorId ?? string.Empty,
                email ?? string.Empty,
                Sanitize(templateKey)),
            HttpContext.RequestAborted);

        if (outcome.Result == CreateDmoUserResult.Success && outcome.User is { } created)
        {
            // UMA vez: o token de entrega do ADMIN. Nunca volta a ser
            // mostrado (TempData consumido no GET seguinte) e nunca é
            // persistido pelo DMO.
            TempData["AdminUserCreatedEmail"] = created.Email;
            TempData["AdminUserCreatedPassword"] = created.TemporaryPassword;
            TempData["AdminUserCreatedName"] = created.Name;
            TempData["AdminUserCreatedOperatorId"] = created.OperatorId;
            return RedirectToPage("/Admin/Users/Index");
        }

        if (outcome.EmailOwner is { } owner)
        {
            TempData["AdminUserEmailOwnerName"] = owner.Name;
            TempData["AdminUserEmailOwnerOperatorId"] = owner.OperatorId;
        }

        return RedirectToPage("/Admin/Users/Index", new
        {
            name = Sanitize(name),
            operator_id = Sanitize(operatorId),
            email = Sanitize(email),
            template_key = Sanitize(templateKey),
            create_refusal = outcome.Result.ToString()
        });
    }

    /// <summary>
    /// POST: editar a atribuição corrente de Template de um User (select
    /// por linha; «Sem template» = remover). A MESMA relação persistida
    /// da vista Templates. Vindo da ficha (origem=ficha), volta à ficha.
    /// </summary>
    public async Task<IActionResult> OnPostTemplateAsync(
        [FromForm(Name = "operator_id")] string? operatorId,
        [FromForm(Name = "template_key")] string? templateKey,
        [FromForm(Name = "origem")] string? origem = null)
    {
        var outcome = await _userAdminService.SetCurrentUserTemplateAsync(
            new SetCurrentUserTemplate(
                operatorId ?? string.Empty,
                Sanitize(templateKey)),
            HttpContext.RequestAborted);

        var refusal = outcome.Result == SetCurrentUserTemplateResult.Success ? null : outcome.Result.ToString();
        if (origem == OrigemFicha)
        {
            return VoltarAFicha(operatorId, "template_refusal", refusal);
        }

        return refusal is null
            ? RedirectToPage("/Admin/Users/Index")
            : RedirectToPage("/Admin/Users/Index", new { template_refusal = refusal });
    }

    /// <summary>
    /// POST: arquivar/reativar um User (USERS.md §1 — o User, a
    /// atribuição e o histórico ficam preservados; apenas o acesso
    /// operacional corrente desliga).
    /// </summary>
    public async Task<IActionResult> OnPostStandByAsync(
        [FromForm(Name = "operator_id")] string? operatorId,
        [FromForm(Name = "stand_by")] string? standBy,
        [FromForm(Name = "origem")] string? origem = null)
    {
        var outcome = await _userAdminService.SetUserStandByAsync(
            new SetUserStandBy(operatorId ?? string.Empty, standBy == "1"),
            HttpContext.RequestAborted);

        var refusal = outcome.Result == SetUserStandByResult.Success ? null : outcome.Result.ToString();
        if (origem == OrigemFicha)
        {
            return VoltarAFicha(operatorId, "standby_refusal", refusal);
        }

        return refusal is null
            ? RedirectToPage("/Admin/Users/Index")
            : RedirectToPage("/Admin/Users/Index", new { standby_refusal = refusal });
    }

    /// <summary>
    /// POST: reset do acesso de um User (USERS.md §5) — nova password
    /// temporária (UMA vez, TempData), a anterior deixa de valer e a
    /// mudança para a password privada volta a ser obrigatória no login
    /// seguinte. Nada mais muda (sem histórico, sem configuração).
    /// </summary>
    public async Task<IActionResult> OnPostResetAsync(
        [FromForm(Name = "operator_id")] string? operatorId,
        [FromForm(Name = "origem")] string? origem = null)
    {
        var outcome = await _userAdminService.ResetUserAccessAsync(
            new ResetUserAccess(operatorId ?? string.Empty),
            HttpContext.RequestAborted);

        if (outcome.Result == ResetUserAccessResult.Success)
        {
            TempData["AdminUserResetOperatorId"] = outcome.OperatorId;
            TempData["AdminUserResetPassword"] = outcome.TemporaryPassword;
            return origem == OrigemFicha
                ? VoltarAFicha(operatorId, "reset_refusal", null)
                : RedirectToPage("/Admin/Users/Index");
        }

        return origem == OrigemFicha
            ? VoltarAFicha(operatorId, "reset_refusal", outcome.Result.ToString())
            : RedirectToPage("/Admin/Users/Index", new { reset_refusal = outcome.Result.ToString() });
    }

    /// <summary>
    /// As ações da ficha (template, arquivar e repor) gravam e voltam à
    /// ficha, com a recusa no endereço (deep sweep, 18/10/2026): na ficha,
    /// trocam só as zonas que mudam. Eliminar continua a voltar à lista.
    /// </summary>
    internal const string OrigemFicha = "ficha";

    private RedirectToPageResult VoltarAFicha(string? operatorId, string refusalName, string? refusal)
    {
        var values = new Microsoft.AspNetCore.Routing.RouteValueDictionary { ["operator_id"] = Sanitize(operatorId) };
        if (refusal is not null)
        {
            values[refusalName] = refusal;
        }

        return RedirectToPage("/Admin/Users/Ficha", values);
    }

    /// <summary>
    /// POST: ELIMINAR o User (o Owner 2026-10-13: «sim mas eu preciso de
    /// apagar os que estao la» — o delete REAL com os factos congelados, o
    /// mesmo princípio das Tools): a linha sai da lista; os registos
    /// passados mantêm o nº como texto congelado. A confirmação em 2
    /// passos (o padrão ?confirm_delete=1 das ferramentas) — o clique
    /// primeiro VESTE o aviso, o segundo apaga.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(
        [FromForm(Name = "operator_id")] string? operatorId,
        [FromForm(Name = "confirm_delete")] string? confirmDelete)
    {
        if (confirmDelete != "1")
        {
            return RedirectToPage("/Admin/Users/Ficha", new { operator_id = operatorId, confirm_delete = "1" });
        }

        var outcome = await _userAdminService.DeleteUserAsync(
            new DeleteUser(operatorId ?? string.Empty),
            HttpContext.RequestAborted);

        if (outcome.Result == DeleteUserResult.Success)
        {
            return RedirectToPage("/Admin/Users/Index", new { deleted = "1" });
        }

        if (outcome.Result == DeleteUserResult.SuccessIdentityAlreadyMissing)
        {
            return RedirectToPage("/Admin/Users/Index", new { deleted = "sem-identidade" });
        }

        return RedirectToPage("/Admin/Users/Index", new { delete_refusal = outcome.Result.ToString() });
    }

    internal static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

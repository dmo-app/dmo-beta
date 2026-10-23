using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.Templates;

/// <summary>
/// A folha Admin → Templates de acesso do corte I (TEMPLATES.md): criar
/// e editar Templates — Nome + as TRÊS capabilities Beta como checkboxes
/// (jobon.create / controlo.create / controlo.approve; o matrix de
/// permissões por módulo do mockup NÃO é vocabulário Beta — recusa
/// tipada UnknownCapability). A TemplateKey é MINTADA pelo backend
/// («tpl-» + 8 hex) e exibida no detalhe como âncora ESTÁVEL: renomear o
/// rótulo nunca muda o acesso de ninguém. Zero capabilities é permitido
/// («Sem capabilities»). A atribuição a utilizadores edita-se na folha
/// Utilizadores — UMA relação persistida, duas vistas (TEMPLATES.md L18).
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public sealed class IndexModel : PageModel
{
    private readonly ITemplateAdminService _templateAdminService;
    private readonly IUserAdminService _userAdminService;
    private readonly ShellStateFactory _shellStateFactory;

    public IndexModel(
        ITemplateAdminService templateAdminService,
        IUserAdminService userAdminService,
        ShellStateFactory shellStateFactory)
    {
        _templateAdminService = templateAdminService;
        _userAdminService = userAdminService;
        _shellStateFactory = shellStateFactory;
    }

    /// <summary>As Templates persistidas, ordenadas pela identidade estável.</summary>
    public IReadOnlyList<TemplateSummary> Templates { get; private set; } = [];

    /// <summary>A Template aberta no detalhe (resolvida pela key estável) — null = nenhuma.</summary>
    public TemplateSummary? SelectedTemplate { get; private set; }

    /// <summary>Quantos utilizadores têm cada template atribuída (pela TemplateKey).</summary>
    public IReadOnlyDictionary<string, int> UserCounts { get; private set; } = new Dictionary<string, int>();

    public int UserCount(string templateKey) => UserCounts.TryGetValue(templateKey, out var count) ? count : 0;

    /// <summary>Os utilizadores da template escolhida (o pill da contagem leva a esta lista).</summary>
    public IReadOnlyList<UserAdminSummary> SelectedTemplateUsers { get; private set; } = [];

    /// <summary>Factos submetidos da criação, preservados pela volta PRG das recusas.</summary>
    public string? RequestedName { get; private set; }
    public IReadOnlyList<string> RequestedCapabilities { get; private set; } = [];

    /// <summary>Recusa tipada do último POST de criação (PRG por query).</summary>
    public CreateTemplateResult? CreateRefusal { get; private set; }

    /// <summary>Recusa tipada do último POST de edição (PRG por query).</summary>
    public EditTemplateResult? EditRefusal { get; private set; }

    /// <summary>Nota PRG: template criada (o detalhe abre com a key mintada).</summary>
    public bool CreatedNote { get; private set; }

    /// <summary>Nota PRG: template guardada.</summary>
    public bool SavedNote { get; private set; }

    /// <summary>Nota PRG: template eliminada.</summary>
    public bool DeletedNote { get; private set; }

    /// <summary>Recusa tipada do último POST de eliminação (PRG por query).</summary>
    public DeleteTemplateResult? DeleteRefusal { get; private set; }

    public string DeleteRefusalMessage => DeleteRefusal switch
    {
        DeleteTemplateResult.UnknownTemplate =>
            "A template indicada já não existe.",
        DeleteTemplateResult.InUse =>
            "A template ainda tem utilizadores. Mude primeiro a template deles; nada foi eliminado.",
        _ => string.Empty
    };

    public string CreateRefusalMessage => CreateRefusal switch
    {
        CreateTemplateResult.MissingName =>
            "O nome é obrigatório. Nenhuma template foi criada.",
        CreateTemplateResult.UnknownCapability =>
            "Um dos módulos indicados não existe. Nenhuma template foi criada.",
        _ => string.Empty
    };

    public string EditRefusalMessage => EditRefusal switch
    {
        EditTemplateResult.UnknownTemplate =>
            "A template indicada já não existe.",
        EditTemplateResult.MissingName =>
            "O nome é obrigatório. A template não foi guardada.",
        EditTemplateResult.UnknownCapability =>
            "Um dos módulos indicados não existe. A template não foi guardada.",
        _ => string.Empty
    };

    /// <summary>
    /// OS TABS ORGANIZADOS POR MÓDULO (o Owner 2026-10-12: «no menu onde
    /// criamos templates, ter uma forma de organizar os tabs pela ordem»):
    /// as capabilities agrupadas pelo módulo que as possui (o princípio
    /// do gate 3: a autorização é MODULE-OWNED), na ordem da navegação
    /// dos módulos — nunca a lista plana.
    /// </summary>
    public static IReadOnlyList<(string Module, IReadOnlyList<string> Capabilities)> BetaCapabilityGroups =>
        new (string, IReadOnlyList<string>)[]
        {
            ("Planeamento · Job On", new[]
            {
                DmoCapabilities.JobOnCreate,
                DmoCapabilities.JobOnView
            }),
            ("Controlo (Criar: Resumo · Peso · Pegamentos · Histórico; Aprovar: Revisão)", new[]
            {
                DmoCapabilities.ControloCreate,
                DmoCapabilities.ControloApprove
            }),
            ("Boquilhas (Criar: Registo · Boquilhas · Movimentos · Definições; Ver: só os Movimentos)", new[]
            {
                DmoCapabilities.Boquilhas,
                DmoCapabilities.BoquilhasView
            }),
            ("Ferramentas", new[]
            {
                DmoCapabilities.Ferramentas
            }),
            ("Armazém (Gestão: Movimentos · Mapa · Histórico · Próximos Job On · Posições · Definições)", new[]
            {
                DmoCapabilities.ArmazemGestao
            }),
            ("Reparação Interna (Registar)", new[]
            {
                DmoCapabilities.ReparacaoInterna
            })
        };

    /// <summary>Rótulo pt-PT da capability (o token canónico é a autoridade; o rótulo é display).</summary>
    public static string CapabilityLabel(string capability) => capability switch
    {
        DmoCapabilities.JobOnCreate => "Planeamento",
        DmoCapabilities.JobOnView => "Job On ver",
        DmoCapabilities.ControloCreate => "Controlo — Criar",
        DmoCapabilities.ControloApprove => "Controlo — Aprovar",
        DmoCapabilities.Boquilhas => "Boquilhas · criar",
        DmoCapabilities.BoquilhasView => "Boquilhas · ver",
        DmoCapabilities.Ferramentas => "Ferramentas",
        DmoCapabilities.ArmazemGestao => "Armazém — Gestão",
        DmoCapabilities.ReparacaoInterna => "Reparação Interna",
        _ => capability
    };

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "template")] string? templateKey,
        [FromQuery(Name = "name")] string? name,
        [FromQuery(Name = "capabilities")] List<string>? capabilities,
        [FromQuery(Name = "create_refusal")] string? createRefusal,
        [FromQuery(Name = "edit_refusal")] string? editRefusal,
        [FromQuery(Name = "created")] string? created,
        [FromQuery(Name = "saved")] string? saved,
        [FromQuery(Name = "deleted")] string? deleted,
        [FromQuery(Name = "delete_refusal")] string? deleteRefusal)
    {
        ViewData["Title"] = "Templates de acesso";
        var shell = (await _shellStateFactory.ForDestinationAsync("admin", new ModulePresentation("admin", "Administração")));
        ViewData["Shell"] = shell with
        {
            ActiveFunctionKey = AdminAreaNavigation.TemplatesFunctionKey,
            Functions = AdminAreaNavigation.Functions(
                AdminAreaNavigation.TemplatesFunctionKey,
                Url.Page("/Admin/Users/Index"),
                Url.Page("/Admin/Templates/Index")),
            Planeamento = shell.Planeamento with { Visible = false }
        };

        // Recusas tipadas (PRG) pelo vocabulário fechado.
        if (createRefusal is not null
            && Enum.TryParse(createRefusal, out CreateTemplateResult parsedCreate)
            && parsedCreate != CreateTemplateResult.Success)
        {
            CreateRefusal = parsedCreate;
        }

        if (editRefusal is not null
            && Enum.TryParse(editRefusal, out EditTemplateResult parsedEdit)
            && parsedEdit != EditTemplateResult.Success)
        {
            EditRefusal = parsedEdit;
        }

        if (deleteRefusal is not null
            && Enum.TryParse(deleteRefusal, out DeleteTemplateResult parsedDelete)
            && parsedDelete != DeleteTemplateResult.Success)
        {
            DeleteRefusal = parsedDelete;
        }

        CreatedNote = created == "1";
        SavedNote = saved == "1";
        DeletedNote = deleted == "1";

        RequestedName = Sanitize(name);
        RequestedCapabilities = capabilities?
            .Select(Sanitize)
            .Where(c => c is not null)
            .Select(c => c!)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

        Templates = await _templateAdminService.ListTemplatesAsync(HttpContext.RequestAborted);
        var users = await _userAdminService.ListUsersAsync(HttpContext.RequestAborted);
        UserCounts = users
            .Where(user => user.CurrentTemplateKey is not null)
            .GroupBy(user => user.CurrentTemplateKey!)
            .ToDictionary(group => group.Key, group => group.Count());

        // O detalhe resolve SEMPRE pela identidade estável (TemplateKey).
        var requestedTemplate = Sanitize(templateKey);
        SelectedTemplate = requestedTemplate is null
            ? null
            : Templates.FirstOrDefault(t => t.TemplateKey == requestedTemplate);
        SelectedTemplateUsers = SelectedTemplate is null
            ? []
            : users
                .Where(user => user.CurrentTemplateKey == SelectedTemplate.TemplateKey)
                .OrderBy(user => user.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        return Page();
    }

    /// <summary>
    /// POST: criar uma template. A TemplateKey NÃO vem do form — é mintada
    /// pelo backend («tpl-» + 8 hex, re-mint). Sucesso → PRG para o
    /// detalhe da template criada (a key mintada é a âncora exibida).
    /// </summary>
    public async Task<IActionResult> OnPostCreateAsync(
        [FromForm(Name = "name")] string? name,
        [FromForm(Name = "capabilities")] List<string>? capabilities)
    {
        var outcome = await _templateAdminService.CreateTemplateAsync(
            new CreateTemplate(
                name ?? string.Empty,
                capabilities ?? []),
            HttpContext.RequestAborted);

        if (outcome.Result == CreateTemplateResult.Success && outcome.Template is { } created)
        {
            return RedirectToPage("/Admin/Templates/Index", new { template = created.TemplateKey, created = "1" });
        }

        return RedirectToPage("/Admin/Templates/Index", new
        {
            name = Sanitize(name),
            capabilities = capabilities ?? [],
            create_refusal = outcome.Result.ToString()
        });
    }

    /// <summary>
    /// POST: guardar a edição de uma template (resolvida pela key estável
    /// — o rótulo pode ter sido renomeado, a key nunca muda). Sucesso →
    /// PRG de volta ao detalhe com a nota de guardado.
    /// </summary>
    public async Task<IActionResult> OnPostEditAsync(
        [FromForm(Name = "template_key")] string? templateKey,
        [FromForm(Name = "name")] string? name,
        [FromForm(Name = "capabilities")] List<string>? capabilities)
    {
        var outcome = await _templateAdminService.EditTemplateAsync(
            new EditTemplate(
                templateKey ?? string.Empty,
                name ?? string.Empty,
                capabilities ?? []),
            HttpContext.RequestAborted);

        if (outcome.Result == EditTemplateResult.Success && outcome.Template is { } saved)
        {
            return RedirectToPage("/Admin/Templates/Index", new { template = saved.TemplateKey, saved = "1" });
        }

        return RedirectToPage("/Admin/Templates/Index", new
        {
            template = Sanitize(templateKey),
            name = Sanitize(name),
            capabilities = capabilities ?? [],
            edit_refusal = outcome.Result.ToString()
        });
    }

    /// <summary>
    /// POST: eliminar uma template (pela key estável). Só se apaga quando
    /// nenhum utilizador a tem; a janela de confirmação já o diz antes.
    /// Sucesso → PRG para a lista sem template aberta.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(
        [FromForm(Name = "template_key")] string? templateKey)
    {
        var outcome = await _templateAdminService.DeleteTemplateAsync(
            templateKey ?? string.Empty,
            HttpContext.RequestAborted);

        if (outcome.Result == DeleteTemplateResult.Success)
        {
            return RedirectToPage("/Admin/Templates/Index", new { deleted = "1" });
        }

        return RedirectToPage("/Admin/Templates/Index", new
        {
            template = Sanitize(templateKey),
            delete_refusal = outcome.Result.ToString()
        });
    }

    internal static string? Sanitize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Presentation;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DMO.Alpha.Web.Pages.Admin.Users;

/// <summary>
/// A FICHA DO UTILIZADOR — a folha de destino do DUPLO CLIQUE da lista
/// (a decisão do Owner 2026-10-12, verbatim: «botoes vivem sempre fora,
/// inverter cor ao selecionar um row, e duplo click na row abre a funçao
/// se existir, no caso dos users abre a folha do user»). A GRAMÁTICA
/// GLOBAL das tabelas: a linha da lista seleciona (o radio escondido, a
/// cor invertida), o DUPLO abre esta ficha — e é AQUI, na folha, que o
/// trabalho vive: os forms das ações que moravam na coluna «Ações» da
/// tabela (contra a regra global: nas tabelas não existem botões).
///
/// Os FACTOS HUMANOS (nome, nº de operador, template corrente pelo
/// NOME, estado, CreatedAt) e as AÇÕES — o select do template +
/// [Guardar template], [Ativar/Arquivar] (o toggle) e [Reset de
/// acesso] — postando aos MESMOS handlers do Index (os testes postam-nos
/// direto), cada um com o PRG e as recusas tipadas atuais preservadas.
/// Vindas da ficha (origem=ficha), a template, arquivar e repor voltam à
/// ficha, com a recusa ou o PIN temporário na pilha do canto, e trocam só
/// o que muda (deep sweep, 18/10/2026); eliminar volta à lista.
/// O EMAIL NÃO EXISTE no DMO (nunca guardado) —
/// não é procurado nem mostrado; o utilizador resolve-se pelo nº de
/// operador, o facto humano único. Nenhum nº que não exista abre
/// ficha: o aviso limpo, nunca uma folha inventada.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public sealed class FichaModel : PageModel
{
    private readonly IUserAdminService _userAdminService;
    private readonly ITemplateAdminService _templateAdminService;
    private readonly ShellStateFactory _shellStateFactory;
    private readonly IPostoRi _postoRi;

    public FichaModel(
        IUserAdminService userAdminService,
        ITemplateAdminService templateAdminService,
        ShellStateFactory shellStateFactory,
        IPostoRi postoRi)
    {
        _postoRi = postoRi;
        _userAdminService = userAdminService;
        _templateAdminService = templateAdminService;
        _shellStateFactory = shellStateFactory;
    }

    /// <summary>O nº de operador pedido (?operator_id — o facto humano único).</summary>
    public string? RequestedOperatorId { get; private set; }

    /// <summary>O utilizador da ficha; null quando o nº não resolve nenhum.</summary>
    public UserAdminSummary? SelectedUser { get; private set; }

    /// <summary>As Templates existentes (o select da atribuição — a MESMA relação).</summary>
    public IReadOnlyList<TemplateSummary> Templates { get; private set; } = [];

    /// <summary>A confirmação do delete vestida (?confirm_delete=1 — o 2.º passo).</summary>
    public bool ConfirmDelete { get; private set; }

    /// <summary>
    /// As recusas das ações da ficha (template, arquivar, repor), que voltam
    /// à ficha pelo endereço (PRG, o vocabulário fechado dos enums), e o PIN
    /// temporário de um repor (TempData, mostrado uma vez). Na pilha do canto.
    /// </summary>
    public string? TemplateRefusalMessage { get; private set; }
    public string? StandByRefusalMessage { get; private set; }
    public string? ResetRefusalMessage { get; private set; }
    public string? ResetOperatorId { get; private set; }
    public string? ResetTemporaryPassword { get; private set; }

    /// <summary>O utilizador da ficha já tem PIN do posto de Reparação.</summary>
    public bool TemPinPosto { get; private set; }

    /// <summary>O resultado de guardar o PIN do posto (TempData, uma vez).</summary>
    public string? PinMensagem { get; private set; }

    /// <summary>Guarda o PIN do posto de Reparação de um utilizador (só o ADMIN).</summary>
    public async Task<IActionResult> OnPostPinPostoAsync(
        [FromForm(Name = "operator_id")] string? operatorId,
        [FromForm(Name = "pin")] string? pin)
    {
        var id = IndexModel.Sanitize(operatorId);
        var user = id is null ? null : (await _userAdminService.ListUsersAsync(HttpContext.RequestAborted))
            .FirstOrDefault(u => string.Equals(u.OperatorId, id, StringComparison.Ordinal));
        if (user is null || user.IsAdmin)
        {
            return RedirectToPage(new { operator_id = id });
        }

        var resultado = await _postoRi.DefinirPinAsync(user.Id, pin, HttpContext.RequestAborted);
        TempData["AdminUserPinPosto"] = resultado == PinGravacao.Gravado
            ? "PIN do posto guardado."
            : "O PIN tem de ter de 4 a 12 algarismos.";
        return RedirectToPage(new { operator_id = user.OperatorId });
    }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "operator_id")] string? operatorId,
        [FromQuery(Name = "confirm_delete")] string? confirmDelete,
        [FromQuery(Name = "template_refusal")] string? templateRefusal = null,
        [FromQuery(Name = "standby_refusal")] string? standbyRefusal = null,
        [FromQuery(Name = "reset_refusal")] string? resetRefusal = null)
    {
        ConfirmDelete = confirmDelete == "1";
        if (Enum.TryParse(templateRefusal, out SetCurrentUserTemplateResult template) && template != SetCurrentUserTemplateResult.Success)
            TemplateRefusalMessage = NullIfEmpty(IndexModel.TemplateRefusalText(template));
        if (Enum.TryParse(standbyRefusal, out SetUserStandByResult standBy) && standBy != SetUserStandByResult.Success)
            StandByRefusalMessage = NullIfEmpty(IndexModel.StandByRefusalText(standBy));
        if (Enum.TryParse(resetRefusal, out ResetUserAccessResult reset) && reset != ResetUserAccessResult.Success)
            ResetRefusalMessage = NullIfEmpty(IndexModel.ResetRefusalText(reset));
        ResetOperatorId = TempData["AdminUserResetOperatorId"] as string;
        ResetTemporaryPassword = TempData["AdminUserResetPassword"] as string;
        PinMensagem = TempData["AdminUserPinPosto"] as string;
        ViewData["Title"] = "Ficha de utilizador";
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

        RequestedOperatorId = IndexModel.Sanitize(operatorId);

        // A ficha resolve-se pelo nº de operador ÚNICO (o facto humano —
        // os ids de backend nunca saem nas folhas). Não existe → fica
        // null: a folha mostra o aviso limpo, nada é inventado.
        if (RequestedOperatorId is not null)
        {
            SelectedUser = (await _userAdminService.ListUsersAsync(HttpContext.RequestAborted))
                .FirstOrDefault(u => string.Equals(u.OperatorId, RequestedOperatorId, StringComparison.Ordinal));
        }

        Templates = await _templateAdminService.ListTemplatesAsync(HttpContext.RequestAborted);
        TemPinPosto = SelectedUser is { } selecionado && await _postoRi.TemPinAsync(selecionado.Id, HttpContext.RequestAborted);

        return Page();
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
}

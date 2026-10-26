using DMO.Alpha.Web.Presentation;

namespace DMO.Alpha.Web.Pages.Admin;

/// <summary>
/// Entradas da navegação de administração: Utilizadores, Templates de
/// acesso e Registos. O Canal Jobon e a App (email do ADMIN e PIN) vivem nas Definições
/// do ADMIN (Diogo, 22/10/2026: «canal jobon e app ficam dentro das
/// definições do admin, admin tem definições diferentes dos users»).
/// </summary>
internal static class AdminAreaNavigation
{
    internal const string UsersFunctionKey = "users";
    internal const string TemplatesFunctionKey = "templates";
    internal const string RegistosFunctionKey = "registos";

    internal static FunctionPresentation[] Functions(
        string activeKey,
        string? usersHref,
        string? templatesHref,
        string? registosHref = null)
    {
        return
        [
            new FunctionPresentation(UsersFunctionKey, "Utilizadores", usersHref, null, activeKey == UsersFunctionKey, true, true),
            new FunctionPresentation(TemplatesFunctionKey, "Templates de acesso", templatesHref, null, activeKey == TemplatesFunctionKey, true, true),
            // Os registos por utilizador, com filtro (Diogo, 2026-10).
            new FunctionPresentation(RegistosFunctionKey, "Registos", registosHref ?? "/Admin/Registos", null, activeKey == RegistosFunctionKey, true, true)
        ];
    }
}

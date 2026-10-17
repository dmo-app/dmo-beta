using DMO.Alpha.Web.Presentation;

namespace DMO.Alpha.Web.Pages.Admin;

/// <summary>
/// Entradas da navegação de administração. O shell apresenta-as na primeira
/// barra, juntamente com Definições, sem uma segunda barra de funções. As
/// Ferramentas (os totais de lote por máquina) têm página própria desde a
/// beta.110 (o proprietário, 17/10/2026).
/// </summary>
internal static class AdminAreaNavigation
{
    internal const string UsersFunctionKey = "users";
    internal const string TemplatesFunctionKey = "templates";
    internal const string FerramentasFunctionKey = "ferramentas";
    internal const string FerramentasHref = "/Admin/Ferramentas";

    internal static FunctionPresentation[] Functions(
        string activeKey,
        string? usersHref,
        string? templatesHref)
    {
        return
        [
            new FunctionPresentation(UsersFunctionKey, "Utilizadores", usersHref, null, activeKey == UsersFunctionKey, true, true),
            new FunctionPresentation(TemplatesFunctionKey, "Templates de acesso", templatesHref, null, activeKey == TemplatesFunctionKey, true, true),
            new FunctionPresentation(FerramentasFunctionKey, "Ferramentas", FerramentasHref, null, activeKey == FerramentasFunctionKey, true, true)
        ];
    }
}

using DMO.Alpha.Web.Presentation;

namespace DMO.Alpha.Web.Pages.Admin;

/// <summary>
/// Entradas da navegação de administração. O shell apresenta-as na primeira
/// barra, juntamente com o Setup (o email do ADMIN), sem uma segunda barra de
/// funções. O Canal Jobon junta as definições que ligam o Job On aos módulos,
/// a começar pela hora de viragem das Boquilhas (Diogo, 17/10/2026: «hora
/// viragem precisa de outra página onde depois podemos adicionar mais
/// módulos. Podemos chamar Canal Jobon»). Os totais de lote saíram para as
/// Definições das Ferramentas.
/// </summary>
internal static class AdminAreaNavigation
{
    internal const string UsersFunctionKey = "users";
    internal const string TemplatesFunctionKey = "templates";
    internal const string CanalJobonFunctionKey = "canal-jobon";
    internal const string CanalJobonHref = "/Admin/CanalJobon";

    internal static FunctionPresentation[] Functions(
        string activeKey,
        string? usersHref,
        string? templatesHref)
    {
        return
        [
            new FunctionPresentation(UsersFunctionKey, "Utilizadores", usersHref, null, activeKey == UsersFunctionKey, true, true),
            new FunctionPresentation(TemplatesFunctionKey, "Templates de acesso", templatesHref, null, activeKey == TemplatesFunctionKey, true, true),
            new FunctionPresentation(CanalJobonFunctionKey, "Canal Jobon", CanalJobonHref, null, activeKey == CanalJobonFunctionKey, true, true)
        ];
    }
}

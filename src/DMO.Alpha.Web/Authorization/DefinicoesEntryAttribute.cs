using DMO.Alpha.Core.Admin;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DMO.Alpha.Web.Authorization;

/// <summary>
/// Os handlers que mudam uma definição GERAL (a que vale para todos). Só quem
/// pode editar a parte Geral das Definições os corre
/// (<see cref="DefinicoesAcesso.PodeEditarGeralAsync"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DefinicaoGeralAttribute : Attribute;

/// <summary>
/// As Definições num só sítio (Diogo, 22/10/2026). Quem pode o quê:
/// ler as definições de um módulo é de quem tem o módulo (e do ADMIN);
/// mudar a parte Geral é do ADMIN e de quem abriu o cadeado com o PIN das Definições.
/// </summary>
public static class DefinicoesAcesso
{
    public static bool EAdmin(HttpContext context) =>
        context.User.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString);

    public static async Task<bool> PodeEditarGeralAsync(HttpContext context)
    {
        if (EAdmin(context))
        {
            return true;
        }

        return await context.RequestServices.GetRequiredService<DesbloqueioDasDefinicoes>().AbertoAsync(context);
    }

    /// <summary>True quando a secção do módulo aparece: o módulo está ativo para o utilizador (o ADMIN vê todas).</summary>
    public static async Task<bool> VeModuloAsync(HttpContext context, params string[] modulos)
    {
        if (EAdmin(context))
        {
            return true;
        }

        var granted = await context.RequestServices.GetRequiredService<CurrentUserCapabilities>().GetAsync(context.RequestAborted);
        return modulos.Any(m => granted.Contains(m, StringComparer.Ordinal));
    }

    /// <summary>
    /// O endereço novo de uma página antiga de definições (/Boquilhas/Definicoes…):
    /// a página única, com a mesma query e a âncora na secção do módulo.
    /// </summary>
    public static string Nova(HttpRequest request, string seccao) =>
        request.QueryString.HasValue
            ? $"/Definicoes{request.QueryString}&secao={seccao}"
            : $"/Definicoes?secao={seccao}";
}

/// <summary>
/// A entrada nas páginas de definições de um módulo, que agora só têm os
/// handlers (o GET leva à página única /Definicoes). Quem não tem o módulo
/// nem é ADMIN vai para o seu primeiro módulo; um handler
/// <see cref="DefinicaoGeralAttribute"/> sem permissão de editar volta às
/// Definições com o aviso do cadeado.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DefinicoesEntryAttribute(string secao, params string[] modulos) : Attribute, IAsyncPageFilter
{
    /// <summary>A entrada do menu das Definições deste módulo (?secao=…).</summary>
    public string Secao { get; } = secao;

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!await DefinicoesAcesso.VeModuloAsync(http, modulos))
        {
            var granted = await http.RequestServices.GetRequiredService<CurrentUserCapabilities>().GetAsync(http.RequestAborted);
            context.Result = new RedirectResult(ModuleEntryAttribute.HomeFor(http.User, granted));
            return;
        }

        var geral = context.HandlerMethod?.MethodInfo.IsDefined(typeof(DefinicaoGeralAttribute), false) == true;
        if (DefinicoesAcesso.EAdmin(http) && !geral && !HttpMethods.IsGet(http.Request.Method))
        {
            // O ADMIN não faz atos operacionais (o Excel, a leitura do SAP).
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        if (geral && !await DefinicoesAcesso.PodeEditarGeralAsync(http))
        {
            if (http.Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
            {
                context.Result = new JsonResult(new { ok = false, valor = (string?)null, aviso = DefinicoesMensagens.SemPermissao }) { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }

            context.Result = new RedirectResult($"/Definicoes?geral=sem-permissao&secao={Secao}");
            return;
        }

        // Depois de gravar, as Definições abrem na entrada deste módulo.
        var executado = await next();
        if (executado.Result is RedirectToPageResult { PageName: "/Definicoes/Index" } volta)
        {
            volta.RouteValues ??= new Microsoft.AspNetCore.Routing.RouteValueDictionary();
            volta.RouteValues["secao"] = Secao;
        }
    }
}

public static class DefinicoesMensagens
{
    public const string SemPermissao = "As definições gerais estão fechadas. Abra o cadeado com o PIN das Definições.";
}

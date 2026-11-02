using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// Lê, para a conta com sessão, os avisos do Job On que ela desligou nas
/// Definições (<see cref="AvisosPessoais"/>); sem conta, vê todos.
/// </summary>
public static class AvisosPessoaisDaConta
{
    public static async Task<IReadOnlySet<string>> LerAsync(HttpContext http)
    {
        var account = http.RequestServices.GetService<ICurrentAccountContext>();
        var pessoais = http.RequestServices.GetService<IDefinicoesPessoais>();
        if (account?.Current.ActorId is not { } userId || pessoais is null)
        {
            return new HashSet<string>();
        }

        return AvisosPessoais.Ler(await pessoais.LerAsync(userId, UserSettingKeys.AvisosEscondidos, http.RequestAborted));
    }
}

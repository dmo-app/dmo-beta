using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Runtime;

namespace DMO.Alpha.Web.Pages.JobOn;

/// <summary>
/// Lê, para a conta com sessão, os avisos do Job On que ela desligou nas
/// Definições (<see cref="AvisosPessoais"/>); sem conta, vê todos.
/// </summary>
public static class AvisosPessoaisDaConta
{
    public static async Task<IReadOnlySet<string>> LerAsync(
        ICurrentAccountContext account, IDefinicoesPessoais pessoais, CancellationToken cancellationToken)
    {
        if (account.Current.ActorId is not { } userId)
        {
            return new HashSet<string>();
        }

        return AvisosPessoais.Ler(await pessoais.LerAsync(userId, UserSettingKeys.AvisosEscondidos, cancellationToken));
    }
}

using System.Security.Claims;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// Mudar a palavra-passe, num só sítio: a mudança obrigatória
/// (/change-password) e a de Definições → Pessoal (que pede também a atual).
/// As regras são as mesmas: o mínimo de 4 caracteres (um PIN serve), a
/// confirmação igual, o provedor (Supabase) muda-a, as outras sessões
/// terminam e esta sessão é renovada com a nova.
/// </summary>
public sealed class MudancaDePalavraPasse(
    IAuthenticationProvider provider,
    ICurrentAccountContext accountContext,
    DmoDbContext dbContext,
    DmoSignInService signInService,
    IAdminEmailSettings adminEmailSettings,
    IHttpContextAccessor httpContextAccessor)
{
    public const int MinimumLength = 4;

    public const string AtualErrada = "A palavra-passe atual não está certa. Nada foi alterado.";

    /// <summary>Erro (nada mudou, ou a sessão não renovou) ou o destino depois de mudar.</summary>
    public sealed record Resultado(string? Erro, string? Destino, bool Mudou = false);

    public async Task<Resultado> MudarAsync(
        ClaimsPrincipal user, bool pedirAtual, string? atual, string? nova, string? confirmar, string destino)
    {
        if (pedirAtual && string.IsNullOrEmpty(atual))
        {
            return new("Indique a palavra-passe atual.", null);
        }

        if (string.IsNullOrWhiteSpace(nova) || string.IsNullOrWhiteSpace(confirmar))
        {
            return new("Indique e confirme a nova palavra-passe.", null);
        }

        if (!string.Equals(nova, confirmar, StringComparison.Ordinal))
        {
            return new("As palavras-passe não coincidem.", null);
        }

        // Mínimo de 4 caracteres (pedido do Diogo de 17/10/2026: sai o
        // mínimo de 6, um PIN de 4 números serve). O Supabase continua a
        // exigir 6; SupabasePassword trata das curtas.
        if (nova.Length < MinimumLength)
        {
            return new($"A nova palavra-passe deve ter pelo menos {MinimumLength} caracteres.", null);
        }

        var actor = accountContext.Current;
        var providerUserId = user.FindFirst(DmoClaimTypes.ProviderUserId)?.Value;
        if (!actor.IsAuthenticated || string.IsNullOrWhiteSpace(providerUserId))
        {
            return new("Não foi possível identificar o utilizador.", null);
        }

        var identifier = user.FindFirst(DmoClaimTypes.Email)?.Value
            ?? (actor.IsAdmin ? null : actor.OperatorId);

        if (pedirAtual)
        {
            var email = user.FindFirst(DmoClaimTypes.Email)?.Value;
            var confere = !string.IsNullOrWhiteSpace(email)
                && (await provider.AuthenticateAsync(email, atual!)).Success;
            if (!confere)
            {
                return new(AtualErrada, null);
            }
        }

        var changeResult = await provider.ChangePasswordAsync(providerUserId, nova);
        if (changeResult != ChangePasswordResult.Success)
        {
            return new(changeResult switch
            {
                ChangePasswordResult.IdentityNotFound =>
                    "A identidade desta conta já não existe no provedor de autenticação.",
                ChangePasswordResult.PolicyViolation =>
                    "A palavra-passe foi recusada pela política de segurança. Escolhe outra.",
                _ => "O provedor de autenticação não pôde alterar a palavra-passe. Tenta novamente em instantes."
            }, null);
        }

        if (actor.IsAdmin)
        {
            // O ADMIN não tem linha em Users: a versão da sessão dele vive
            // nas definições. As outras sessões do ADMIN terminam; a
            // renovação logo a seguir entra já com a versão nova.
            await adminEmailSettings.BumpAdminSessionVersionAsync(CancellationToken.None);
        }
        else
        {
            var row = await dbContext.Users.FirstOrDefaultAsync(u => u.OperatorId == actor.OperatorId);
            if (row is not null)
            {
                row.RequiresPasswordChange = false;
                // As outras sessões deste User terminam; a renovação logo a
                // seguir entra já com a versão nova.
                row.SessionVersion++;
                await dbContext.SaveChangesAsync();
            }
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return new("Não foi possível refazer a autenticação.", null, Mudou: true);
        }

        var signInResult = await signInService.RefreshSignInAsync(identifier, nova, destino);
        if (!signInResult.Succeeded)
        {
            var http = httpContextAccessor.HttpContext;
            if (http is not null)
            {
                await http.SignOutAsync();
            }

            return new("Palavra-passe alterada, mas a sessão não pôde ser renovada. Entre novamente.", null, Mudou: true);
        }

        return new(null, signInResult.RedirectUrl, Mudou: true);
    }
}

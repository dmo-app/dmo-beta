using System.Security.Claims;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Web.Services;

public sealed class DmoSignInService
{
    private readonly IAuthenticationProvider _provider;
    private readonly IAdminAuthorizationService _authorization;
    private readonly IAdminEmailSettings _adminEmailSettings;
    private readonly DmoDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // As recusas antes de a palavra-passe ser verificada dizem todas o mesmo:
    // o ecrã de entrada não revela que números de operador existem.
    private const string InvalidCredentials = "Credenciais inválidas.";
    private const string ArchivedAccount = "Esta conta está arquivada. Fale com o ADMIN.";
    // Sem resposta do serviço de autenticação, a palavra-passe pode estar
    // certa: o ecrã não diz que está errada (sweep de 19/10/2026, 5.1).
    private const string ProviderUnavailable = "O serviço de autenticação não respondeu. Tente de novo dentro de alguns minutos.";

    public DmoSignInService(
        IAuthenticationProvider provider,
        IAdminAuthorizationService authorization,
        IAdminEmailSettings adminEmailSettings,
        DmoDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _provider = provider;
        _authorization = authorization;
        _adminEmailSettings = adminEmailSettings;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<SignInOutcome> SignInAsync(string identifier, string password, string? returnUrl = null)
        => SignInAsync(identifier, password, returnUrl, passwordJustChanged: false);

    private async Task<SignInOutcome> SignInAsync(string identifier, string password, string? returnUrl, bool passwordJustChanged)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context.");

        // Espaços colados (autopreenchimento, copiar) e maiúsculas no email
        // não podem falhar a entrada: o Supabase não tira os espaços.
        identifier = identifier.Trim();
        var isEmail = identifier.Contains('@');
        if (isEmail)
        {
            identifier = identifier.ToLowerInvariant();
        }

        // Operational numbers identify a DMO User, not a Supabase email.
        // Accounts created by Admin use their real email; keep @dmo.local
        // only for legacy accounts that predate persisted User.Email.
        User? operatorUser = null;
        if (!isEmail)
        {
            operatorUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.OperatorId == identifier);

            if (operatorUser is null)
            {
                return SignInOutcome.Failed(InvalidCredentials);
            }
        }

        var providerIdentifier = isEmail
            ? identifier
            : !string.IsNullOrWhiteSpace(operatorUser!.Email)
                ? operatorUser.Email.Trim()
                : MapOperatorIdToProviderIdentifier(identifier);

        var authResult = await _provider.AuthenticateAsync(providerIdentifier, password);

        if (!authResult.Success)
        {
            // P2-6 da auditoria: a mensagem de credenciais inválidas EM
            // PORTUGUÊS, sempre — o ErrorMessage do provider é o detalhe
            // técnico (logs), o que o utilizador lê é a nossa frase (antes:
            // «Invalid credentials.» vinha CHEIO do provider e o fallback
            // nunca disparava).
            return SignInOutcome.Failed(authResult.ProviderUnavailable ? ProviderUnavailable : InvalidCredentials);
        }

        List<Claim> claims;
        bool requiresPasswordChange;
        string displayName;

        if (isEmail)
        {
            // O ADMIN é o email guardado nas Definições: quem autentica
            // com esse email entra como ADMIN.
            // Num ambiente novo (campo ainda vazio), o primeiro login por
            // email com sucesso que NÃO seja um User registado assume o
            // campo e fica ADMIN — as contas operacionais criadas pelo
            // ADMIN nunca o reivindicam.
            var adminEmail = await _adminEmailSettings.GetAdminEmailAsync();
            var loginEmail = (authResult.Email ?? identifier).Trim();

            // A resolução do User pela identidade canónica do provider —
            // a MESMA que o ramo normal usa (nunca pelo email guardado:
            // USERS.md §1 — a credencial pertence ao provider).
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ProviderUserId == authResult.ProviderUserId);

            if (adminEmail is null && user is null)
            {
                adminEmail = await _adminEmailSettings.TryClaimAdminEmailAsync(loginEmail)
                    ? loginEmail
                    : await _adminEmailSettings.GetAdminEmailAsync();
            }

            if (AdminEmailRule.IsAdminEmail(loginEmail, adminEmail))
            {
                displayName = adminEmail;
                requiresPasswordChange = false;

                claims =
                [
                    new Claim(DmoClaimTypes.DisplayName, adminEmail),
                    new Claim(DmoClaimTypes.IsAdmin, bool.TrueString),
                    new Claim(DmoClaimTypes.Email, adminEmail),
                    new Claim(DmoClaimTypes.ProviderUserId, authResult.ProviderUserId!),
                    new Claim(DmoClaimTypes.SessionVersion,
                        (await _adminEmailSettings.GetAdminSessionAsync()).SessionVersion
                            .ToString(System.Globalization.CultureInfo.InvariantCulture))
                ];
            }
            else
            {
                // A normal User of the app, resolved by the provider's own
                // identity — never by stored email (USERS.md §1: the
                // identifier is application data, the credential belongs
                // to the provider).
                if (user is null)
                {
                    return SignInOutcome.Failed("Este email não tem acesso à aplicação.");
                }

                if (user.IsStandBy)
                {
                    return SignInOutcome.Failed(ArchivedAccount);
                }

                displayName = user.Name;
                requiresPasswordChange = user.RequiresPasswordChange || authResult.IsTemporaryPassword;

                claims =
                [
                    new Claim(DmoClaimTypes.ActorId, user.Id.ToString()),
                    new Claim(DmoClaimTypes.DisplayName, user.Name),
                    new Claim(DmoClaimTypes.IsAdmin, bool.FalseString),
                    new Claim(DmoClaimTypes.OperatorId, user.OperatorId),
                    // The session carries the email claim from the
                    // authentication result — NEVER a persisted value
                    // (DMO stores no User email; the change-password
                    // re-sign-in prefers this claim).
                    new Claim(DmoClaimTypes.Email, authResult.Email ?? identifier),
                    new Claim(DmoClaimTypes.ProviderUserId, authResult.ProviderUserId!),
                    SessionVersionClaim(user)
                ];

                if (!string.IsNullOrWhiteSpace(user.TemplateName))
                {
                    claims.Add(new Claim(DmoClaimTypes.ProfileLabel, user.TemplateName));
                }
            }
        }
        else
        {
            // The lookup happened before contacting Supabase, so the exact
            // same registered account is checked against the Auth identity.
            var user = operatorUser!;

            if (!string.Equals(user.ProviderUserId, authResult.ProviderUserId, StringComparison.Ordinal))
            {
                return SignInOutcome.Failed(InvalidCredentials);
            }

            // Só depois de a palavra-passe estar certa: a conta arquivada
            // não se revela a quem não a sabe.
            if (user.IsStandBy)
            {
                return SignInOutcome.Failed(ArchivedAccount);
            }

            displayName = user.Name;
            requiresPasswordChange = user.RequiresPasswordChange || authResult.IsTemporaryPassword;

            claims =
            [
                new Claim(DmoClaimTypes.ActorId, user.Id.ToString()),
                new Claim(DmoClaimTypes.DisplayName, user.Name),
                new Claim(DmoClaimTypes.IsAdmin, bool.FalseString),
                new Claim(DmoClaimTypes.OperatorId, user.OperatorId),
                new Claim(DmoClaimTypes.Email, authResult.Email ?? providerIdentifier),
                new Claim(DmoClaimTypes.ProviderUserId, authResult.ProviderUserId!),
                SessionVersionClaim(user)
            ];

            if (!string.IsNullOrWhiteSpace(user.TemplateName))
            {
                claims.Add(new Claim(DmoClaimTypes.ProfileLabel, user.TemplateName));
            }
        }

        if (requiresPasswordChange)
        {
            claims.Add(new Claim(DmoClaimTypes.RequiresPasswordChange, bool.TrueString));
        }
        else if (passwordJustChanged)
        {
            // Marca a sessão renovada pela mudança: um segundo «Guardar»
            // enviado com o formulário antigo já não reabre o formulário
            // (ExpiredPasswordFormFilter).
            claims.Add(new Claim(DmoClaimTypes.PasswordChangedAt,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);
        // Uma sessão nova começa com o cadeado das Definições fechado.
        DesbloqueioDasDefinicoes.Fechar(httpContext);

        // O modo claro/escuro guardado na conta (Definições → Pessoal) vem
        // para este computador ao entrar; as páginas leem-no do cookie, sem
        // ir à base em cada pedido.
        if (Guid.TryParse(claimsPrincipal.FindFirst(DmoClaimTypes.ActorId)?.Value, out var actorId))
        {
            var tema = await _dbContext.UserSettings.AsNoTracking()
                .Where(s => s.UserId == actorId && s.Key == DMO.Alpha.Core.Admin.UserSettingKeys.Tema)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            TemaPessoal.Escrever(httpContext, tema);
        }

        if (requiresPasswordChange)
        {
            return SignInOutcome.RedirectTo("/change-password");
        }

        return SignInOutcome.RedirectTo(await ResolveLandingAsync(claimsPrincipal, returnUrl));
    }

    /// <summary>The page a signed-in session lands on (by role and template).</summary>
    public async Task<string> ResolveLandingAsync(ClaimsPrincipal principal, string? returnUrl = null)
    {
        // O DESTINO POR PAPEL (o G1 da varredura 2026-10-12 + o Owner
        // 2026-10-13: «ja disse varias vezes admin so tem pagina de admin» —
        // A REGRA É ABSOLUTA: o ADMIN nunca aterra fora da admin, NEM POR
        // RETURNURL (o challenge de /JobOn traz ReturnUrl=%2FJobOn e o admin
        // caía lá dentro); os operacionais entram no trabalho (o /JobOn) e
        // honram o returnUrl das páginas protegidas.
        if (principal.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString))
        {
            // O Owner 2026-10-13: «deveria entrar nos utilizadores nao
            // definiçoes» — o destino do ADMIN são OS UTILIZADORES (o
            // trabalho mais comum da admin), não as definições.
            return "/Admin/Users";
        }

        // Route operational users to a module allowed by their assigned template,
        // instead of assuming everybody has Job On access. Keep explicit deep links.
        var effective = Guid.TryParse(principal.FindFirst(DmoClaimTypes.ActorId)?.Value, out var actorId)
            ? await _authorization.ResolveEffectiveAccessAsync(actorId)
            : null;
        var capabilities = effective?.Capabilities ?? Array.Empty<string>();
        var initialPage = DmoCapabilities.HomeFor(capabilities) ?? "/Modules/Home";
        // Só caminhos desta aplicação: nunca outro site depois do login.
        return !LocalUrl.IsSafe(returnUrl) || returnUrl is "/JobOn" or "/"
            ? initialPage : returnUrl;
    }

    public async Task<SignInOutcome> RefreshSignInAsync(string identifier, string password, string? returnUrl = null)
    {
        // Re-authenticate after a password change so the session reflects the new credential
        // and the forced-change requirement is cleared.
        return await SignInAsync(identifier, password, returnUrl, passwordJustChanged: true);
    }

    // A versão das sessões com que este cookie entra (o validador recusa-o
    // quando a do User subir: reset ou nova palavra-passe).
    private static Claim SessionVersionClaim(User user) => new(
        DmoClaimTypes.SessionVersion,
        user.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));

        private static string MapOperatorIdToProviderIdentifier(string operatorId)
    {
        // Functional boundary: dmo-app/blueprint/modules/admin/USERS.md deliberately does not
        // define the visible/provider login identifier. Alpha currently uses this implementation
        // convention to produce a deterministic Supabase-facing email from the operator ID.
        return $"{operatorId}@dmo.local";
    }
}

public sealed class SignInOutcome
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? RedirectUrl { get; private init; }

    public static SignInOutcome Failed(string message) => new() { ErrorMessage = message };

    public static SignInOutcome RedirectTo(string url) => new() { Succeeded = true, RedirectUrl = url };
}

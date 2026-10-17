using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// The authorization semantics of the Admin foundation — the server-side
/// effective-access resolution and the ADMIN-boundary assignment
/// authorization over the persisted User↔Template relationship (see
/// <see cref="IAdminAuthorizationService"/>). Resolution is anchored on
/// the Template's stable identity: labels may be renamed freely without
/// touching anyone's effective access.
/// </summary>
public sealed class AdminAuthorizationService : IAdminAuthorizationService
{
    private readonly DmoDbContext _dbContext;
    private readonly IAdminEmailSettings _adminEmailSettings;

    public AdminAuthorizationService(
        DmoDbContext dbContext,
        IAdminEmailSettings adminEmailSettings)
    {
        _dbContext = dbContext;
        _adminEmailSettings = adminEmailSettings;
    }

    /// <inheritdoc />
    public async Task<EffectiveAccess?> ResolveEffectiveAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Uma só ida à base por pedido: o utilizador, o template corrente e
        // o email do ADMIN das Definições vêm na mesma consulta.
        var row = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                User = u,
                Assignment = u.CurrentTemplateAssignment,
                Template = u.CurrentTemplateAssignment != null ? u.CurrentTemplateAssignment.Template : null,
                AdminEmail = _dbContext.ModuleSettings
                    .Where(s => s.SettingKey == ModuleSettingKeys.AdminEmail)
                    .Select(s => s.SettingValue)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var user = row.User;

        // Canonical ADMIN boundary (o ADMIN é o email guardado nas
        // Definições — o Owner 2026-10-16): a linha cujo email é o do
        // ADMIN resolve SEM Template e SEM capabilities operacionais —
        // mesmo que uma atribuição antiga exista, a resolução trata-a
        // como sem Template.
        if (IsAdminEmail(user.Email, AdminEmailSettings.Normalize(row.AdminEmail)))
        {
            return new EffectiveAccess
            {
                UserId = user.Id,
                IsAdmin = true,
                IsStandBy = user.IsStandBy,
                ProviderUserId = user.ProviderUserId,
                RequiresPasswordChange = user.RequiresPasswordChange,
                TemplateId = null,
                TemplateKey = null,
                TemplateName = null,
                Capabilities = []
            };
        }

        var assignment = row.Assignment;
        var template = row.Template;

        // A User with no current Template has no Template-derived
        // operational capabilities; a stand-by User's current operational
        // access is disabled while the User, the assignment and historical
        // attribution stay preserved (the Template facts remain visible —
        // access is disabled, not unassigned).
        var capabilities = user.IsStandBy || assignment is null
            ? []
            : template?.Capabilities ?? [];

        return new EffectiveAccess
        {
            UserId = user.Id,
            IsAdmin = false,
            IsStandBy = user.IsStandBy,
            ProviderUserId = user.ProviderUserId,
            RequiresPasswordChange = user.RequiresPasswordChange,
            TemplateId = assignment?.TemplateId,
            TemplateKey = template?.TemplateKey,
            TemplateName = template?.Name,
            Capabilities = capabilities
        };
    }

    /// <inheritdoc />
    public async Task<TemplateAssignmentAuthorization> AuthorizeTemplateAssignmentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return TemplateAssignmentAuthorization.UserNotFound;
        }

        return await IsAdminEmailAddressAsync(user.Email, cancellationToken)
            ? TemplateAssignmentAuthorization.RefusedAdminBoundary
            : TemplateAssignmentAuthorization.Allowed;
    }

    /// <summary>
    /// Whether an email IS the ADMIN's email — the value kept in the
    /// Definições (chave canónica ModuleSettingKeys.AdminEmail). No
    /// setting → no email is ADMIN-associated. Same value the login and
    /// the session validator compare.
    /// </summary>
    private async Task<bool> IsAdminEmailAddressAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        var adminEmail = await _adminEmailSettings.GetAdminEmailAsync(cancellationToken);
        return IsAdminEmail(email, adminEmail);
    }

    private static bool IsAdminEmail(string? email, string? adminEmail) =>
        adminEmail is not null
        && email is not null
        && string.Equals(email.Trim(), adminEmail, StringComparison.OrdinalIgnoreCase);
}

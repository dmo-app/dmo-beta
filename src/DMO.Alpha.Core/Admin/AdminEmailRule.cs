using System.Diagnostics.CodeAnalysis;

namespace DMO.Alpha.Core.Admin;

/// <summary>
/// A regra única «este email é o do ADMIN?» (o ADMIN é o email guardado no
/// Admin → Setup): o email, sem espaços à volta e sem distinguir maiúsculas,
/// igual ao guardado. Sem email guardado, ninguém é o ADMIN.
/// </summary>
public static class AdminEmailRule
{
    public static bool IsAdminEmail([NotNullWhen(true)] string? email, [NotNullWhen(true)] string? adminEmail) =>
        adminEmail is not null
        && email is not null
        && string.Equals(email.Trim(), adminEmail.Trim(), StringComparison.OrdinalIgnoreCase);
}

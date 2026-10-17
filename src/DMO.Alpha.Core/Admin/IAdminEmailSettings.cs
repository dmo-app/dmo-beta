namespace DMO.Alpha.Core.Admin;

/// <summary>
/// O ADMIN é apenas um email, guardado nas Definições (chave canónica
/// <see cref="ModuleSettingKeys.AdminEmail"/>): quem autenticar com esse
/// email entra como ADMIN. Num ambiente novo (chave vazia), o
/// primeiro login por email com sucesso assume o campo e fica ADMIN.
/// </summary>
public interface IAdminEmailSettings
{
    /// <summary>
    /// O email do ADMIN corrente, ou null quando ainda não está definido
    /// (ambiente novo — o primeiro login por email assume-o).
    /// </summary>
    Task<string?> GetAdminEmailAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda o email do ADMIN (upsert da chave canónica). Recusa tipada
    /// quando o texto não é um email utilizável.
    /// </summary>
    Task<SaveAdminEmailResult> SaveAdminEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Primeiro login num ambiente novo: grava o email apenas quando a
    /// chave ainda não existe. Devolve false quando outro email já a ocupa
    /// (incluindo um login simultâneo que ganhou a corrida).
    /// </summary>
    Task<bool> TryClaimAdminEmailAsync(
        string email,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado tipado do save do email do ADMIN.</summary>
public enum SaveAdminEmailResult
{
    Success,
    InvalidEmail
}

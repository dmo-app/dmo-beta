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
    /// quando o texto não é um email utilizável e quando o email ainda não
    /// tem conta: só o ADMIN atual e os utilizadores criados em Admin →
    /// Utilizadores conseguem entrar com ele.
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

    /// <summary>
    /// O email do ADMIN e a versão da sessão do ADMIN, numa só leitura (o
    /// validador do cookie corre em cada pedido). Sem versão gravada vale 0.
    /// </summary>
    Task<(string? Email, int SessionVersion)> GetAdminSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// O ADMIN mudou a palavra-passe: sobe a versão da sessão, para as
    /// sessões abertas noutros computadores terminarem. Devolve a versão nova.
    /// </summary>
    Task<int> BumpAdminSessionVersionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Resultado tipado do save do email do ADMIN.</summary>
public enum SaveAdminEmailResult
{
    Success,
    InvalidEmail,

    /// <summary>
    /// O email não é o do ADMIN atual nem o de um utilizador: ninguém
    /// conseguiria entrar como ADMIN e a sessão atual caía (sweep de
    /// 19/10/2026, AD-1).
    /// </summary>
    NoAccount
}

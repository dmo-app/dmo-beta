namespace DMO.Alpha.Core.Admin;

/// <summary>
/// Uma definição PESSOAL (Diogo, 22/10/2026: «definições num só sítio»,
/// parte «Pessoal»): vale só para o utilizador que a grava, em qualquer
/// computador. As definições para todos ficam em ModuleSettings.
/// </summary>
public sealed class UserSetting
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>A chave canónica (ex.: Pessoal:Tema).</summary>
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;
}

/// <summary>As chaves das definições pessoais (vocabulário fechado).</summary>
public static class UserSettingKeys
{
    /// <summary>O modo claro ou escuro: «light» ou «dark».</summary>
    public const string Tema = "Pessoal:Tema";

    /// <summary>
    /// A última vez que a conta abriu o Sumário do Planeamento (UTC, ISO
    /// 8601): o ponto do separador só aparece com linhas importantes depois
    /// disto (Diogo, 23/10/2026).
    /// </summary>
    public const string SumarioVistoEm = "Pessoal:SumarioVistoEm";
}

/// <summary>Lê e grava as definições pessoais do utilizador.</summary>
public interface IDefinicoesPessoais
{
    Task<string?> LerAsync(Guid userId, string key, CancellationToken cancellationToken = default);

    Task GravarAsync(Guid userId, string key, string value, CancellationToken cancellationToken = default);
}

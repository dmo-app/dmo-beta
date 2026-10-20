namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// Um reparador de CM e MF (plano do full, peça A; blueprint
/// admin/MAQUINAS-E-REPARADORES §2 e §3): para onde vão as CM e os MF na
/// Saída para Reparação do Armazém. É uma lista à parte dos reparadores das
/// Boquilhas (decisão do Diogo, 18/10/2026: «reparadores das boquilhas
/// trabalham apenas com boquilhas»); o mesmo nome pode existir nas duas.
/// <para>
/// Fica nas Definições do Armazém, o primeiro módulo que a usa (regra das
/// definições por módulo; recomendação da MR-02). Não se apaga: arquiva-se,
/// e os movimentos guardam o nome do momento.
/// </para>
/// </summary>
public sealed class Reparador
{
    /// <summary>O tamanho máximo do nome, o mesmo dos reparadores das Boquilhas.</summary>
    public const int NomeMaxLength = 100;

    public int Id { get; set; }

    /// <summary>O nome, sem espaços nas pontas; único sem distinguir maiúsculas.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Recebe CM.</summary>
    public bool AplicaCm { get; set; } = true;

    /// <summary>Recebe MF.</summary>
    public bool AplicaMf { get; set; } = true;

    /// <summary>Ativo por omissão; arquivado sai das escolhas novas.</summary>
    public bool Ativo { get; set; } = true;
}

/// <summary>Um reparador da lista.</summary>
public sealed record ReparadorItem(int Id, string Nome, bool AplicaCm, bool AplicaMf, bool Ativo);

/// <summary>Como acabou criar ou mudar um reparador.</summary>
public enum ReparadorResultado
{
    Gravado,
    NomeVazio,
    NomeLongo,
    JaExiste,
    SemFamilia,
    NaoExiste
}

/// <summary>O que muda num reparador: só os campos não nulos.</summary>
public sealed record MudancaDeReparador(string? Nome = null, bool? AplicaCm = null, bool? AplicaMf = null, bool? Ativo = null);

/// <summary>Os reparadores de CM e MF: listar, criar e mudar.</summary>
public interface IReparadores
{
    /// <summary>Todos, ativos primeiro e depois pelo nome.</summary>
    Task<IReadOnlyList<ReparadorItem>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria um reparador. Tem de receber CM, MF ou os dois.</summary>
    Task<ReparadorResultado> CriarAsync(string? nome, bool aplicaCm, bool aplicaMf, CancellationToken cancellationToken = default);

    /// <summary>Muda o nome, a família ou o estado. Ficar sem CM e sem MF é <see cref="ReparadorResultado.SemFamilia"/>.</summary>
    Task<ReparadorResultado> MudarAsync(int id, MudancaDeReparador mudanca, CancellationToken cancellationToken = default);
}

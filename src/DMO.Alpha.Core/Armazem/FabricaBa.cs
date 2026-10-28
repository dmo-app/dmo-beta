namespace DMO.Alpha.Core.Armazem;

/// <summary>
/// Uma fábrica da BA (Diogo, 28/10/2026): a sigla de duas letras (MG, LE…) e
/// o nome, preenchidos nas Definições do Armazém. Usa-se no Empréstimo do
/// Armazém (para onde foi e de onde volta), na Localização das Ferramentas e
/// na Fábrica de origem. A tabela começa vazia. Não se apaga: as fábricas
/// fecham e abrem, por isso desativa-se e os movimentos guardam a sigla.
/// </summary>
public sealed class FabricaBa
{
    public const int SiglaLength = 2;

    public const int NomeMaxLength = 60;

    /// <summary>A fábrica proposta por omissão quando está na lista e ativa.</summary>
    public const string SiglaPorOmissao = "MG";

    public int Id { get; set; }

    /// <summary>Duas letras, em maiúsculas; única.</summary>
    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    /// <summary>Ativa por omissão; desativada sai das escolhas novas.</summary>
    public bool Ativa { get; set; } = true;
}

/// <summary>Uma fábrica da lista.</summary>
public sealed record FabricaBaItem(int Id, string Sigla, string Nome, bool Ativa);

/// <summary>Como acabou criar ou mudar uma fábrica.</summary>
public enum FabricaBaResultado
{
    Gravada,
    SiglaInvalida,
    NomeVazio,
    NomeLongo,
    JaExiste,
    NaoExiste
}

/// <summary>As fábricas da BA: listar, criar e mudar (nome e ativa).</summary>
public interface IFabricasBa
{
    /// <summary>Todas, ativas primeiro e depois pela sigla.</summary>
    Task<IReadOnlyList<FabricaBaItem>> ListarAsync(CancellationToken cancellationToken = default);

    Task<FabricaBaResultado> CriarAsync(string? sigla, string? nome, CancellationToken cancellationToken = default);

    Task<FabricaBaResultado> MudarAsync(int id, string? nome, bool? ativa, CancellationToken cancellationToken = default);
}

public static class FabricasBaTextos
{
    /// <summary>A sigla escrita, limpa: duas letras em maiúsculas, ou null.</summary>
    public static string? Sigla(string? texto)
    {
        var limpo = (texto ?? string.Empty).Trim().ToUpperInvariant();
        return limpo.Length == FabricaBa.SiglaLength && limpo.All(char.IsLetter) ? limpo : null;
    }

    /// <summary>A sigla proposta: MG quando está ativa, senão a primeira ativa, senão nenhuma.</summary>
    public static string? PorOmissao(IReadOnlyList<FabricaBaItem> fabricas) =>
        fabricas.FirstOrDefault(f => f.Ativa && f.Sigla == FabricaBa.SiglaPorOmissao)?.Sigla
        ?? fabricas.FirstOrDefault(f => f.Ativa)?.Sigla;
}

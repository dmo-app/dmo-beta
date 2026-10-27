namespace DMO.Alpha.Core.Admin;

/// <summary>
/// O POSTO DA REPARAÇÃO INTERNA (Diogo, 26/10/2026): no login, quem tem a
/// Reparação Interna (ver ou criar) abre o posto, escolhe o seu nome e entra
/// com o PIN do posto, sem palavra-passe. Cada reparador tem o seu PIN, que
/// o ADMIN define na ficha; guarda-se só o hash (PBKDF2 com sal).
/// </summary>
public interface IPostoRi
{
    /// <summary>Os utilizadores ativos com a Reparação Interna (ver ou criar) e PIN definido, por nome.</summary>
    Task<IReadOnlyList<PostoReparador>> ListarAsync(CancellationToken cancellationToken = default);

    Task<bool> TemPinAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PinGravacao> DefinirPinAsync(Guid userId, string? pin, CancellationToken cancellationToken = default);

    /// <summary>Confere o PIN de um utilizador que está na lista do posto.</summary>
    Task<bool> ConfereAsync(Guid userId, string? pin, CancellationToken cancellationToken = default);
}

public sealed record PostoReparador(Guid Id, string Nome);

public static class PostoRiRegras
{
    /// <summary>A chave em UserSettings onde fica o hash do PIN do posto.</summary>
    public const string Chave = "Posto:PinHash";

    public static bool TemRi(IEnumerable<string> capabilities) =>
        capabilities.Any(c => c is DmoCapabilities.ReparacaoInterna or DmoCapabilities.ReparacaoInternaView);

    /// <summary>Os minutos sem uso antes de a sessão do posto fechar, quando nada foi escolhido.</summary>
    public const int MinutosPorOmissao = 3;

    /// <summary>As opções das Definições da Reparação para os minutos do posto.</summary>
    public static readonly IReadOnlyList<int> MinutosOpcoes = [3, 5, 10, 15, 30];

    /// <summary>Os minutos gravados, ou os 3 por omissão quando o valor não é uma das opções.</summary>
    public static int Minutos(string? valor) =>
        int.TryParse(valor?.Trim(), out var minutos) && MinutosOpcoes.Contains(minutos) ? minutos : MinutosPorOmissao;

    /// <summary>
    /// O que uma sessão aberta pelo posto pode usar: só a Reparação Interna
    /// do template (Diogo, 27/10/2026: «Só a Reparação Interna»); os outros
    /// módulos pedem o login com o nº de operador e a palavra-passe.
    /// </summary>
    public static IReadOnlyList<string> SoDoPosto(IEnumerable<string> capabilities) =>
        capabilities.Where(c => c is DmoCapabilities.ReparacaoInterna or DmoCapabilities.ReparacaoInternaView).ToList();
}

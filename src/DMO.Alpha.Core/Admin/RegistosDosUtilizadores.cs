using DMO.Alpha.Core.Paging;

namespace DMO.Alpha.Core.Admin;

/// <summary>O módulo de onde vem um registo.</summary>
public enum RegistoModulo
{
    JobOn = 1,
    ReparacaoInterna = 2,
    Armazem = 3,
    Boquilhas = 4,
    Controlo = 5,
    Ferramentas = 6
}

/// <summary>Uma ação de um utilizador: quando, em que módulo, o que fez e sobre quê.</summary>
public sealed record RegistoDeUtilizador(DateTime EmUtc, RegistoModulo Modulo, string Acao, string Detalhe, string? Utilizador);

/// <summary>O filtro dos registos; o dia «até» conta inteiro (dias de Lisboa).</summary>
public sealed record RegistosFiltro(Guid? UserId, RegistoModulo? Modulo, DateOnly? De, DateOnly? Ate);

/// <summary>
/// Um registo com o que a procura dos Registos precisa para filtrar e ligar
/// (Diogo, 27/10/2026): o Job On, as ferramentas, a linha, o reparador e o
/// antes → depois quando o registo mudou um valor.
/// </summary>
public sealed record RegistoDetalhado(
    DateTime EmUtc,
    RegistoModulo Modulo,
    string Acao,
    string Detalhe,
    Guid? UserId,
    string? Utilizador,
    int? JobOnId,
    IReadOnlyList<string> ToolIds,
    string? Linha,
    string? Reparador,
    string? Antes,
    string? Depois,
    string? Sobre = null)
{
    /// <summary>O objeto do registo, curto (a referência e a produção, a ferramenta); sem ele, o detalhe.</summary>
    public string SobreOuDetalhe => Sobre ?? Detalhe;

    public RegistoDeUtilizador Simples() => new(EmUtc, Modulo, Acao, Detalhe, Utilizador);
}

/// <summary>
/// Os registos por utilizador para o ADMIN (Diogo, 2026-10: «logs por
/// utilizador que o Admin poderá ver, com filtro»): o que cada um registou,
/// corrigiu ou anulou nos módulos, lido dos rastos que já existem. Só leitura.
/// </summary>
public interface IRegistosDosUtilizadores
{
    /// <summary>Os mais recentes primeiro; cada módulo conta no máximo <see cref="LimitePorModulo"/>.</summary>
    Task<PagedList<RegistoDeUtilizador>> ListarAsync(RegistosFiltro filtro, int? pagina, CancellationToken cancellationToken = default);

    public const int LimitePorModulo = 500;

    /// <summary>
    /// Os mesmos registos com o Job On, as ferramentas, a linha e o reparador,
    /// os mais recentes primeiro; cada módulo conta no máximo <paramref name="limitePorModulo"/>.
    /// Inclui também as correções do Peso e dos Pegamentos e o OK/NOK das áreas.
    /// </summary>
    Task<IReadOnlyList<RegistoDetalhado>> DetalhadosAsync(RegistosFiltro filtro, int limitePorModulo, CancellationToken cancellationToken = default);

    public static string Nome(RegistoModulo modulo) => modulo switch
    {
        RegistoModulo.JobOn => "Job On",
        RegistoModulo.ReparacaoInterna => "Reparação Interna",
        RegistoModulo.Armazem => "Armazém",
        RegistoModulo.Boquilhas => "Boquilhas",
        RegistoModulo.Controlo => "Controlo",
        _ => "Ferramentas"
    };
}

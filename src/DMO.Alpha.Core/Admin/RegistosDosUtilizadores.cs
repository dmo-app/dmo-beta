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
/// Os registos por utilizador para o ADMIN (Diogo, 2026-10: «logs por
/// utilizador que o Admin poderá ver, com filtro»): o que cada um registou,
/// corrigiu ou anulou nos módulos, lido dos rastos que já existem. Só leitura.
/// </summary>
public interface IRegistosDosUtilizadores
{
    /// <summary>Os mais recentes primeiro; cada módulo conta no máximo <see cref="LimitePorModulo"/>.</summary>
    Task<PagedList<RegistoDeUtilizador>> ListarAsync(RegistosFiltro filtro, int? pagina, CancellationToken cancellationToken = default);

    public const int LimitePorModulo = 500;

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

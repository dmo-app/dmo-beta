namespace DMO.Alpha.Core.Controlo;

/// <summary>O estado de revisão de um controlo (17/10/2026).</summary>
public enum ControloRevisaoEstado
{
    Pendente = 0,
    Aprovado = 1,
    NaoAprovado = 2
}

/// <summary>
/// Um controlo na lista da Revisão: a produção, as ferramentas do Peso, o
/// peso médio, o dia em que foi registado e o estado da revisão.
/// </summary>
public sealed record ControloRevisaoItem(
    int ControloId,
    int JobOnId,
    string Reference,
    string ProductionNumber,
    string Machine,
    string? CmReference,
    string? BqReference,
    string? BqLot,
    decimal? AverageGlassWeightGrams,
    bool HasPegamentos,
    DateTime? RegistadoEmUtc,
    string? RegistadoPor,
    ControloRevisaoEstado Estado);

/// <summary>Um dia do mês na Revisão: quantos controlos e quantos por aprovar.</summary>
public sealed record ControloRevisaoDia(int Day, int Total, int Pendentes);

/// <summary>
/// A Revisão do Controlo por calendário (decisão do proprietário, 17/10/2026):
/// cada controlo com Peso ou Pegamentos fica no dia (hora de Lisboa) em que
/// o primeiro foi gravado. Os registos anteriores à data guardada não têm dia
/// e ficam numa lista própria, carregada só a pedido.
/// </summary>
public interface IControloRevisaoQuery
{
    /// <summary>Os dias do mês com controlos, com o número dos que estão por aprovar.</summary>
    Task<IReadOnlyList<ControloRevisaoDia>> MonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>Os controlos de um dia, filtrados pelo texto (referência, CM, boquilha, lote ou máquina) e pelo estado.</summary>
    Task<IReadOnlyList<ControloRevisaoItem>> DayAsync(
        DateOnly day,
        string? procura,
        ControloRevisaoEstado? estado,
        CancellationToken cancellationToken = default);

    /// <summary>Quantos controlos sem data de registo estão por aprovar.</summary>
    Task<int> CountUndatedPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Os controlos sem data de registo (os mais recentes primeiro, no máximo 50), com os mesmos filtros.</summary>
    Task<IReadOnlyList<ControloRevisaoItem>> UndatedAsync(
        string? procura,
        ControloRevisaoEstado? estado,
        CancellationToken cancellationToken = default);

    /// <summary>Um controlo pela produção; null quando a produção não tem controlo.</summary>
    Task<ControloRevisaoItem?> FindByJobOnAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O controlo da produção anterior da mesma referência que tem Peso
    /// (a «produção anterior» da comparação global); null quando não há.
    /// </summary>
    Task<ControloRevisaoItem?> FindPreviousWithPesoAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>O nome de um utilizador pelo nº de operador (quem aprovou); null quando não existe.</summary>
    Task<string?> OperatorNameAsync(string operatorId, CancellationToken cancellationToken = default);
}

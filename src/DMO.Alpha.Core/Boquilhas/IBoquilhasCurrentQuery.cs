namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// A PRODUÇÃO CORRENTE de cada máquina para o módulo das Boquilhas (a
/// regra do Owner: «quando digo em produçao é a que esta hj em maquina e
/// segue as datas do jobon» — F4-4). HOJE: o Job On ainda não tem datas
/// (o corte B) — a corrente é a ÚLTIMA produção da máquina; QUANDO as
/// datas existirem, a corrente passa a ser a cujo intervalo cobre o dia
/// OPERACIONAL (a hora de viragem configurada —
/// BoquilhasDayChange.ResolveOperationalDay, o default 8:00).
/// </summary>
public interface IBoquilhasCurrentQuery
{
    /// <summary>
    /// Todas as máquinas, cada uma com a sua produção corrente — uma linha
    /// por produção quando várias cobrem o dia operacional (a que entrou por
    /// último primeiro) — (null quando
    /// a máquina nunca produziu — «Sem produção», nunca a mais recente
    /// futura).
    /// </summary>
    Task<IReadOnlyList<MachineCurrentBq>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// O dia operacional das Boquilhas: muda à hora de viragem (Canal Jobon,
    /// 8h por omissão), nunca à meia-noite. É o «hoje» que escolhe a produção
    /// em máquina no painel, na procura e nos registos de uma BQ; os dias do
    /// histórico continuam a contar à meia-noite (especialista, R30).
    /// </summary>
    Task<DateOnly> OperationalDayAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Uma máquina com a sua produção corrente: os FATOS HUMANOS (o nº de
/// produção, a referência do conjunto, o código da máquina, a REFERÊNCIA
/// da boquilha em máquina — nunca os tool_id). O BqContextId é o id do
/// CONTEXTO BQ da produção (o ?bq_id do registo — a linha da máquina
/// liga ao registo da boquilha em máquina). O MachineId serve a lista das
/// máquinas do formulário do movimento sem voltar a ler a tabela.
/// </summary>
public sealed record MachineCurrentBq(
    int MachineId,
    string MachineCode,
    int? JobOnId,
    string? ProductionNumber,
    string? Reference,
    string? BqReference,
    string? BqToolId,
    int? BqContextId = null,
    // A linha tem produções que se SOBREPÕEM de verdade no dia operacional
    // (JobOnOverlap.Crosses — o dia da troca não conta). Só informa: o
    // painel mostra todas e um aviso; nada bloqueia.
    bool Overlap = false);

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Handler do ensure do Controlo de uma produção: get-or-create
/// idempotente por jobon_id, suportado pelo índice único na coluna
/// JobOnId (padrão DuplicateProductionNumber: verificação prévia devolve
/// o resultado tipado; o índice real permanece a última linha da verdade
/// persistida). Nunca cria um segundo controlo_id para a mesma produção,
/// nunca cria controlo para um jobon_id inexistente (recusa tipada) e
/// nunca muta o Job On nem os seus contextos — o registo Controlo é
/// dono apenas da sua identidade e da relação.
/// </summary>
public interface IEnsureControloForJobOnHandler
{
    Task<int?> FindExistingIdAsync(int jobOnId, CancellationToken cancellationToken = default);

    Task<EnsureControloForJobOnOutcome> HandleAsync(
        EnsureControloForJobOn command,
        CancellationToken cancellationToken = default);
}

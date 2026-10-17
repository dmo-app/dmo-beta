namespace DMO.Alpha.Core.Production;

/// <summary>
/// Handler do comando de edição de um Job On — a produção
/// re-planeada: grava a janela temporal (inicio→fim), a máquina e os
/// tool_ids dos contextos CM/MF/BQ numa ÚNICA escrita atómica, SEM
/// criar outra produção (o jobon_id é o mesmo). A identidade — a
/// Reference e o ProductionNumber — é imutável: o handler nem as lê. O
/// BqContext é o MESMO contexto (o bq_id não muda): só o ToolId
/// atualiza para a BQ nova — os traces/movimentos existentes ficam no
/// mesmo trace, são factos históricos. O controlo_id associado não é
/// tocado (existe desde a criação). Todas as recusas são tipadas e
/// verificadas antes de qualquer escrita: uma recusa não deixa
/// escritas parciais.
/// </summary>
public interface IUpdateJobOnHandler
{
    Task<UpdateJobOnOutcome> HandleAsync(
        UpdateJobOn command,
        CancellationToken cancellationToken = default);
}

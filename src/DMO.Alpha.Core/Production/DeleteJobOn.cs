namespace DMO.Alpha.Core.Production;

/// <summary>
/// Comando: eliminar um Job On que não vai acontecer ou foi criado por engano
/// (Diogo, 19/10/2026, D2 / AV-JO-04: «Podemos trocar, ou remover»). Nada
/// bloqueia: a confirmação da ficha avisa o que se perde
/// (<see cref="JobOnDeletionPreview"/>) e quem trata do Job On decide.
/// </summary>
public sealed record DeleteJobOn(
    int JobOnId,
    // Quem elimina (o nome, senão o nº de operador ou o email), congelado no
    // rasto dos movimentos das Boquilhas apagados; null sem identidade.
    string? DeletedBy = null);

public enum DeleteJobOnResult
{
    Success,

    /// <summary>A produção já não existe (outra pessoa eliminou-a, ou um duplo clique).</summary>
    JobOnNotFound
}

/// <summary>
/// O que se perde ao eliminar um Job On, para a confirmação. O
/// <see cref="StartReached"/> é a regra do Diogo: «comparar o dia atual com a
/// data de início»; a partir do dia de início a produção pode já ter produzido.
/// </summary>
public sealed record JobOnDeletionPreview(
    int JobOnId,
    string Reference,
    string ProductionNumber,
    DateOnly? InicioEm,
    bool StartReached,
    int Pesos,
    int Comparacoes,
    int Pegamentos,
    int Puncoes,
    int BqMovements,
    bool Revisto)
{
    /// <summary>As reparações da Reparação Interna ligadas a esta produção.</summary>
    public int ReparacoesInternas { get; init; }
}

/// <summary>
/// Elimina um Job On e tudo o que é só dele: o controlo (Pesos, comparações,
/// Pegamentos, Punções, a revisão), os cm_id, mf_id e bq_id (os atuais e os
/// trocados), as reparações da Reparação Interna e os movimentos das
/// Boquilhas desses bq_id, que ficam no rasto dos movimentos apagados. As ferramentas ficam. Uma só escrita atómica.
/// </summary>
public interface IDeleteJobOnHandler
{
    /// <summary>O que a confirmação mostra; null quando a produção não existe.</summary>
    Task<JobOnDeletionPreview?> PreviewAsync(int jobOnId, DateOnly today, CancellationToken cancellationToken = default);

    Task<DeleteJobOnResult> HandleAsync(DeleteJobOn command, CancellationToken cancellationToken = default);
}

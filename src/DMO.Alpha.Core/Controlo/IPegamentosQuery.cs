namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Surface só de leitura da folha de Pegamentos production-scoped: a
/// projeção purpose-specific DA FOLHA por controlo_id (a LEI da app: «com
/// controlo_id ganhas o contexto todo para pegamentos»), que compõe no
/// momento da leitura o contexto canónico da produção (jobon_id →
/// referência/produção/máquina/cm_id/bq_id/mf_id → tool_ids herdadas), o
/// contexto técnico corrente das tools (o gargalo de cada componente — em
/// falta quando a tool ainda não o tem), a tolerância efetiva corrente
/// (configurada ?? default) e o registo durável de Pegamentos quando já
/// existe (com as leituras persistidas, os derivados e as avaliações
/// calculados a partir dos factos congelados — nunca do estado corrente
/// das tools/configuração). Não é um repositório genérico. A ausência de
/// registo é um estado válido devolvido como null dentro do read model da
/// folha (a folha existe sobre o controlo; o registo nasce da primeira
/// medição).
/// </summary>
public interface IPegamentosQuery
{
    /// <summary>
    /// Devolve a folha de Pegamentos do controlo pedido, ou null quando o
    /// controlo_id não existe (nenhum contexto é inventado).
    /// </summary>
    Task<PegamentosSheetReadModel?> FindSheetByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A folha de um registo de Pegamentos pelo pegamentos_id: com produção,
    /// a mesma folha de <see cref="FindSheetByControloAsync"/>; sem Job On,
    /// a folha com as ferramentas guardadas no registo. Null quando o
    /// pegamentos_id não existe.
    /// </summary>
    Task<PegamentosSheetReadModel?> FindSheetByPegamentosAsync(
        int pegamentosId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Só o registo de Pegamentos do controlo (o mesmo de
    /// <see cref="FindSheetByControloAsync"/>), sem o contexto da folha;
    /// null quando ainda não existe.
    /// </summary>
    Task<PegamentosReadModel?> FindRecordByControloAsync(
        int controloId,
        CancellationToken cancellationToken = default);
}

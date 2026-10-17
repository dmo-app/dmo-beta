namespace DMO.Alpha.Core.Boquilhas;

/// <summary>
/// Resultado tipado da associação de um registo de reparação ao seu
/// contexto de produção BQ.
/// </summary>
public enum AssociateBqRepairTraceToContextResult
{
    Success,

    /// <summary>O bq_repair_trace_id não existe.</summary>
    TraceNotFound,

    /// <summary>O bq_id (BqContext) não existe.</summary>
    BqContextNotFound,

    /// <summary>
    /// O trace já está associado a um bq_id. Um trace associado nunca é
    /// reassociado: uma produção posterior nunca toma posse de um trace
    /// anterior (os retornos tardios ficam no trace de origem).
    /// </summary>
    TraceAlreadyAssociated,

    /// <summary>
    /// O bq_id já tem o seu único trace (um bq_id tem um
    /// bq_repair_trace_id). Recusa tipada em vez de violar o índice único.
    /// </summary>
    BqContextAlreadyTraced,

    /// <summary>
    /// O bq_id referencia um tool_id canónico diferente da âncora direta
    /// tool_id do trace ANTES da associação. A correlação trace ↔ bq_id
    /// faz-se pelo mesmo tool_id canónico; como a âncora é limpa no
    /// sucesso, a comparação usa o tool_id pré-associção do trace.
    /// </summary>
    TraceToolDoesNotMatchBqContextTool,

    // ------------------------------------------------------------------
    // OS FATOS HUMANOS DO POST «Associar Job On a este registo» (o corte
    // A2, o fim do ciclo do backfill): a página resolve o bq_id a partir
    // do nº de produção e da referência completa — os ids nunca visíveis
    // na folha. Estas recusas nascem na RESOLUÇÃO da página (nunca no
    // handler, que fala em ids canónicos) e partilham o mesmo canal de
    // recusa tipada da associação.
    // ------------------------------------------------------------------

    /// <summary>
    /// Os fatos humanos em falta: o POST pede o nº de produção e a
    /// referência completa; sem eles não há resolução — recusa informada,
    /// nada é associado.
    /// </summary>
    MissingFacts,

    /// <summary>
    /// A produção não foi encontrada pelo nº + referência completos — o
    /// caso backfill é o Job On retroativo que ainda não existe: cria-se
    /// pelo fluxo do JobOn (as datas do passado gravam como vêm) e volta
    /// a associar.
    /// </summary>
    ProductionNotFound,

    /// <summary>
    /// A BQ do registo não é a BQ deste Job On: nenhum contexto BQ dessa
    /// produção partilha o tool_id canónico do trace (a correlação é pelo
    /// mesmo tool_id — o handler recusa com
    /// TraceToolDoesNotMatchBqContextTool).
    /// </summary>
    BqNotInProduction
}

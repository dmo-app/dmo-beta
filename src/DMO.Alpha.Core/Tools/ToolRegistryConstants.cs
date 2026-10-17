namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Constantes canónicas do registo de Tools (Ferramentas): valores fixados
/// pelo contrato, nunca derivados de configuração nem de input.
/// </summary>
public static class ToolRegistryConstants
{
    /// <summary>
    /// tool_id da linha DUMMY do registo (tombstone): a linha Tools com
    /// ToolId = "REMOVED" (Type = CM, Reference = "REMOVED", Lot =
    /// "REMOVED"), semeada pela migração Baseline via InsertData manual. A
    /// eliminação de uma Tool COM histórico reatribui os registos de
    /// consumo (Pesos, Pegamentos) a esta linha numa transação que apaga a
    /// tool FISICAMENTE — os factos humanos congelados nos registos é que
    /// preservam a verdade histórica. A DUMMY nunca é uma tool utilizável:
    /// TODAS as queries canónicas (candidatos, lookup, ficha) a excluem
    /// por esta constante, e os handlers de consumo recusam-na como
    /// ToolNotFound. Nenhum caminho a seleciona, edita ou elimina.
    /// </summary>
    public const string RemovedToolId = "REMOVED";

    /// <summary>
    /// Lote gravado quando a criação ou a edição chega sem lote (beta.63,
    /// regra do Owner «tentar sempre warning»): a ferramenta grava com
    /// «s/lote» e a ficha avisa. A chave (tipo, referência, lote) mantém-se:
    /// uma segunda ferramenta sem lote com o mesmo tipo e referência é a
    /// MESMA (o get-or-create abre a existente).
    /// </summary>
    public const string NoLot = "s/lote";
}

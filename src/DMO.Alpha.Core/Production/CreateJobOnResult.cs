namespace DMO.Alpha.Core.Production;

/// <summary>
/// Resultado tipado da criação de um Job On.
/// </summary>
public enum CreateJobOnResult
{
    Success,

    /// <summary>
    /// O nº de produção está em branco: a coluna é obrigatória e nenhum
    /// facto é inventado.
    /// </summary>
    MissingProductionNumber,

    /// <summary>
    /// A referência está em branco: a coluna é obrigatória e nenhum
    /// facto é inventado.
    /// </summary>
    MissingReference,

    /// <summary>
    /// A máquina indicada não existe na verdade persistida (Machines).
    /// Nunca é auto-selecionada outra máquina.
    /// </summary>
    MachineNotFound,

    /// <summary>
    /// O MF (molde final) é OBRIGATÓRIO em todo o Job On — regra do Owner
    /// 2026-10-10: «jobon necessita sempre de MF, senao nao faz garrafas ou
    /// frascos». O MF dá a forma do produto E a base 4-dígitos da referência
    /// (o desenho) — sem ele não há produção real nem identificação do
    /// produto. É a identidade mínima do registo de produção (como
    /// MissingReference nas Tools), não um bloqueio de fluxo.
    /// </summary>
    MissingMfTool,

    /// <summary>O tool_id da CM selecionado não resolve a Tool canónica no registo Ferramentas.</summary>
    CmToolNotFound,

    /// <summary>O tool_id da MF selecionado não resolve a Tool canónica no registo Ferramentas.</summary>
    MfToolNotFound,

    /// <summary>O tool_id da BQ selecionado não resolve a Tool canónica no registo Ferramentas.</summary>
    BqToolNotFound,

    /// <summary>
    /// Já existe um Job On com o mesmo nº de produção (índice único): uma produção nova tem nº
    /// próprio. Recusa tipada, nunca exceção.
    /// </summary>
    DuplicateProductionNumber,

    /// <summary>
    /// A calote desta produção (mm) submetida não é utilizável — negativa
    /// ou ilegível (o campo j-calote da ficha; 0 é válido — calote plana).
    /// Nenhum Job On é criado.
    /// </summary>
    InvalidCalote,

    /// <summary>O diâmetro do tampão é zero ou negativo (fisicamente impossível).</summary>
    InvalidTampao,

    /// <summary>
    /// Um texto da folha passa o máximo do catálogo (JobOnFolhaCampos): a
    /// chave vai em ChaveRecusada. Nada é gravado e a folha volta com o que
    /// se escreveu.
    /// </summary>
    TextoLongo,

    /// <summary>
    /// Falta a CM ou a BQ (Diogo, 22/10/2026: um Job On não se grava sem CM,
    /// BQ e MF). Nada é gravado.
    /// </summary>
    MissingCmTool,

    /// <summary>Falta a BQ (ver MissingCmTool).</summary>
    MissingBqTool
}

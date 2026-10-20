namespace DMO.Alpha.Core.Production;

/// <summary>
/// Resultado tipado da edição de um Job On — a produção re-planeada.
/// Todas as recusas são informativas (mensagem limpa, o contexto
/// preservado): nunca um bloqueio, nunca uma exceção.
/// </summary>
public enum UpdateJobOnResult
{
    Success,

    /// <summary>
    /// O jobon_id indicado não existe na verdade persistida: NENHUMA
    /// linha é inventada — a edição exige uma produção existente.
    /// </summary>
    JobOnNotFound,

    /// <summary>
    /// A máquina indicada não existe na verdade persistida (Machines) —
    /// a MESMA recusa do create. Nunca é auto-selecionada outra máquina.
    /// </summary>
    MachineNotFound,

    /// <summary>
    /// O MF (molde final) é OBRIGATÓRIO em todo o Job On — a regra do
    /// Owner 2026-10-10 («jobon necessita sempre de MF, senao nao faz
    /// garrafas ou frascos») HERDA na edição: a produção re-planeada
    /// mantém a identidade mínima do registo de produção.
    /// </summary>
    MissingMfTool,

    /// <summary>O tool_id da CM selecionado não resolve a Tool canónica no registo Ferramentas.</summary>
    CmToolNotFound,

    /// <summary>O tool_id da MF selecionado não resolve a Tool canónica no registo Ferramentas.</summary>
    MfToolNotFound,

    /// <summary>O tool_id da BQ selecionado não resolve a Tool canónica no registo Ferramentas.</summary>
    BqToolNotFound,

    /// <summary>
    /// A calote desta produção (mm) submetida não é utilizável — negativa
    /// ou ilegível (o campo j-calote da ficha; 0 é válido — calote plana).
    /// Nenhuma alteração é gravada.
    /// </summary>
    InvalidCalote,

    /// <summary>
    /// Outra pessoa gravou esta produção depois de a ficha ser aberta (a versão
    /// enviada já não é a gravada). Nada foi gravado: a ficha volta com o
    /// que ficou gravado, para a pessoa refazer a alteração por cima.
    /// </summary>
    ChangedByAnotherUser
}

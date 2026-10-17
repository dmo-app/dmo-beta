namespace DMO.Alpha.Core.Production;

/// <summary>
/// Os FATOS TÉCNICOS de uma ferramenta na escolha da ficha (o showToolFacts
/// do controlo-v04 portado): o que o Peso/Pegamentos consomem da Tool — o
/// processo e o volume punção e o gargalo (CM), o volume marisa e a
/// quantidade do lote (BQ), o gargalo nominal (MF) e a geometria TP de
/// referência (o apoio TP/calote) — mais os AVISOS INFORMATIVOS que nunca
/// bloqueiam: a compatibilidade da máquina registada na ficha da tool e o
/// uso paralelo noutra produção ao mesmo tempo («Esta informação não impede a sua
/// escolha»). Os avisos são calculados PELO BACKEND (a lógica é de
/// domínio); a escolha continua sempre humana.
/// </summary>
public sealed record JobOnToolFacts(
    string ToolId,
    string Type,
    string Reference,
    string Lot,
    string? Processo,
    decimal? VolumePuncaoCm3,
    decimal? VolumeMarisaCm3,
    decimal? DiametroGargaloMm,
    int? Quantidade,
    decimal? TpDiametroMm,
    decimal? TpRaioMm,
    decimal? TpSagittaMm,
    IReadOnlyList<string> Maquinas,
    /// <summary>«CM 5447: compatibilidade registada B1, C3; confirme a máquina escolhida.» — null quando não aplicável.</summary>
    string? AvisoCompatibilidade,
    /// <summary>«CM 5447 / lote 3 também está associado a 202601 (B1). Esta informação não impede a sua escolha.» — null quando não aplicável.</summary>
    string? AvisoUsoParalelo);

/// <summary>
/// Query purpose-specific da ficha do Job On: os fatos técnicos das
/// ferramentas selecionadas + os avisos informativos (a compatibilidade da
/// máquina registada e o uso paralelo noutra produção que trabalha ao
/// mesmo tempo — os períodos sobrepõem-se —, excluída a produção em edição). Serve a
/// ficha server-render e o endpoint JSON do vivo (a MESMA fonte).
/// </summary>
public interface IJobOnToolFactsQuery
{
    Task<IReadOnlyList<JobOnToolFacts>> FindAsync(
        IReadOnlyList<string> toolIds,
        /// <summary>A máquina escolhida na ficha (o código B1..C3) — para o aviso de compatibilidade; null = sem aviso.</summary>
        string? machineCode,
        /// <summary>A produção em edição (os seus contextos não contam como uso paralelo).</summary>
        int? excludingJobOnId,
        /// <summary>O início da produção em criação ou edição; null = sem datas a comparar, sem aviso de uso paralelo.</summary>
        DateOnly? inicioEm,
        /// <summary>O fim da produção em criação ou edição; null = em aberto.</summary>
        DateOnly? fimEm,
        CancellationToken cancellationToken = default);
}

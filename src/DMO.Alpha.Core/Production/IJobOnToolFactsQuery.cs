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
    string? AvisoUsoParalelo)
{
    /// <summary>
    /// As outras produções do <see cref="AvisoUsoParalelo"/>, para o aviso
    /// levar a cada uma (Diogo, 19/10/2026: os avisos têm link para onde se
    /// resolvem). Vazio quando não há uso paralelo.
    /// </summary>
    public IReadOnlyList<JobOnToolUsoParalelo> UsosParalelos { get; init; } = Array.Empty<JobOnToolUsoParalelo>();

    /// <summary>
    /// Onde a ferramenta está pelo Armazém («Armazém · posição 1033»,
    /// «A reparar · Moldes Lda», «Fabrico»); null quando nunca foi registada
    /// no Armazém.
    /// </summary>
    public string? OndeEsta { get; init; }

    /// <summary>O nome técnico da ferramenta; null sem nome.</summary>
    public string? NomeTecnico { get; init; }

    /// <summary>As gotas que o nome técnico diz (<see cref="DMO.Alpha.Core.Tools.TechnicalNameParser.Gotas"/>); null quando não diz.</summary>
    public int? Gotas => DMO.Alpha.Core.Tools.TechnicalNameParser.Gotas(NomeTecnico);

    /// <summary>O último estado registado no Armazém; null sem estado.</summary>
    public DMO.Alpha.Core.Armazem.FerramentaEstadoTipo? Estado { get; init; }

    /// <summary>O % de uso da última leitura (Diogo, 21/10/2026: «no Job On também mostrar o % uso»); null sem leitura.</summary>
    public decimal? Utilizacao { get; init; }

    /// <summary>
    /// «CM 5447T173 / lote 3 não está registada no Armazém e não tem estado.»
    /// ou «… não tem estado registado.» (Diogo 21/10, FER-E-03: só avisa) —
    /// null quando tem as duas coisas.
    /// </summary>
    public string? AvisoArmazem { get; init; }
}

/// <summary>Outra produção que usa a ferramenta ao mesmo tempo: o id, o nº e a máquina.</summary>
public sealed record JobOnToolUsoParalelo(int JobOnId, string ProductionNumber, string Machine);

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

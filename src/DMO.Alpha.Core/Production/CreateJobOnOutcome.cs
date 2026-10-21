namespace DMO.Alpha.Core.Production;

/// <summary>
/// Resultado da criação: o outcome tipado e, em caso de sucesso, o read
/// model purpose-specific do contexto de produção estabelecido — o MESMO
/// <see cref="ProductionContextReadModel"/> que <see cref="IProductionContextQuery"/>
/// serve aos consumidores, com o jobon_id emitido e os contextos aplicáveis
/// (cm_id/mf_id/bq_id) ancorados aos tool_ids selecionados. O
/// <see cref="OverlapWarning"/> é o AVISO INFORMATIVO da sobreposição de
/// janelas na mesma máquina (o Owner: «overlap da maquina deve dar um
/// warning, nao deve bloquear»): a produção está GRAVADA na mesma e o
/// aviso detalhado (as produções em conflito, as janelas, o período
/// exato, como resolver) viaja por cima do sucesso — null sem conflito.
/// </summary>
public sealed record CreateJobOnOutcome(
    CreateJobOnResult Result,
    ProductionContextReadModel? JobOn = null,
    OverlapWarning? OverlapWarning = null,
    // A chave da folha que passou o máximo (a recusa TextoLongo); null nas outras.
    string? ChaveRecusada = null);

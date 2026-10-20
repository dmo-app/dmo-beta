namespace DMO.Alpha.Core.Production;

/// <summary>
/// Query purpose-specific da consulta do Job On: a lista dos Job Ons
/// persistidos com o seu contexto de produção estabelecido — o MESMO
/// read model (<see cref="ProductionContextReadModel"/>) que
/// <see cref="IProductionContextQuery"/> serve por jobon_id — mais
/// recentes primeiro. É a consulta incluída na capability beta Job On
/// Create (a spec: a criação inclui a consulta necessária ao próprio
/// trabalho). Não é um repositório genérico nem uma segunda fonte de
/// verdade: é uma projeção sobre as tabelas reais.
/// </summary>
public interface IJobOnListQuery
{

    /// <summary>
    /// As produções de UMA referência, a inteira e sem distinguir maiúsculas
    /// (e, quando indicada, de uma máquina), a mais recente primeiro: o
    /// segundo passo da escolha do Job On, que só carrega as produções da
    /// referência procurada — as últimas
    /// <see cref="IProductionPickerQuery.MaxResults"/>.
    /// </summary>
    Task<IReadOnlyList<ProductionContextReadModel>> ListByReferenceAsync(
        string reference,
        string? machineCode,
        CancellationToken cancellationToken = default);

    /// <summary>As produções com dia cuja janela toca o intervalo [de, até].</summary>
    Task<IReadOnlyList<ProductionContextReadModel>> ListWindowAsync(
        DateOnly de,
        DateOnly ate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A próxima produção a entrar em cada máquina depois do dia: por máquina,
    /// a de início mais cedo depois de <paramref name="dia"/> (em empate, a
    /// gravada primeiro). Uma linha por máquina, só as que têm uma.
    /// </summary>
    Task<IReadOnlyList<ProductionContextReadModel>> ListNextPerMachineAsync(
        DateOnly dia,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uma página da procura do histórico, paginada na base (mais recente
    /// primeiro). Só se chama com pelo menos um filtro: o histórico abre em
    /// branco.
    /// </summary>
    Task<DMO.Alpha.Core.Paging.PagedList<ProductionContextReadModel>> SearchHistoryPageAsync(
        JobOnHistoryFilter filter,
        int? pageNumber,
        int pageSize = DMO.Alpha.Core.Paging.ListPaging.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// As produções das referências que começam pelo texto ou cuja parte BQ
    /// começa por ele (sem distinguir maiúsculas), mais recentes primeiro,
    /// opcionalmente só as de uma máquina: o dropdown do histórico — as
    /// últimas <see cref="IProductionPickerQuery.MaxResults"/>, cada uma com
    /// a sua referência (o mesmo nº pode existir em duas referências).
    /// </summary>
    Task<IReadOnlyList<JobOnProductionOption>> ListProductionsOfReferenceAsync(
        string reference,
        int? machineId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas as produções que já existiram com a referência exata (sem
    /// distinguir maiúsculas), a mais recente primeiro, paginadas na base: a
    /// tabela da folha do Job On.
    /// </summary>
    Task<DMO.Alpha.Core.Paging.PagedList<ProductionContextReadModel>> ListPageOfReferenceAsync(
        string reference,
        int? pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Os filtros do histórico do Job On: a referência (pelo início dela ou da
/// parte BQ, sem distinguir maiúsculas), a máquina, o nº de produção (igual),
/// a referência dessa produção (igual: o mesmo nº pode existir noutra
/// referência) e o período desde–até, que apanha as produções cuja janela
/// toca o intervalo. Um filtro null não restringe.
/// </summary>
public sealed record JobOnHistoryFilter(
    string? Reference,
    int? MachineId,
    string? ProductionNumber,
    DateOnly? Desde,
    DateOnly? Ate,
    string? ProductionReference = null)
{
    public bool IsEmpty => string.IsNullOrEmpty(Reference) && MachineId is null
        && string.IsNullOrEmpty(ProductionNumber) && string.IsNullOrEmpty(ProductionReference)
        && Desde is null && Ate is null;
}

/// <summary>Uma opção do dropdown das produções de uma referência.</summary>
public sealed record JobOnProductionOption(string ProductionNumber, string Reference);

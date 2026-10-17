namespace DMO.Alpha.Core.Production;

/// <summary>
/// A procura em dois passos (decisão do proprietário de 16/10/2026: «vamos
/// usar a blank page apenas com os filtros e inserir o que queremos
/// procurar, referencia, se existe um dropdown com as produçoes da mesma»):
/// primeiro a referência, depois um select só com as produções dela. Nunca
/// carrega todas as produções. O select mostra só as últimas
/// <see cref="MaxResults"/> (decisão do proprietário de 17/10/2026: «o
/// dropdown das produções a partir de agora mostra as últimas 5»); o
/// histórico completo de uma referência vê-se na tabela paginada da folha do
/// Job On.
/// </summary>
public interface IProductionPickerQuery
{
    /// <summary>O máximo de produções num dropdown de produções: as últimas 5 (regra global, 17/10/2026).</summary>
    public const int MaxResults = 5;

    /// <summary>
    /// As produções cuja referência contém <paramref name="reference"/> (sem
    /// distinção de maiúsculas), da mais recente para a mais antiga, no
    /// máximo <see cref="MaxResults"/>. Sem referência devolve uma lista vazia.
    /// </summary>
    Task<IReadOnlyList<ProductionPickItem>> ListByReferenceAsync(
        string? reference,
        CancellationToken cancellationToken = default);

    /// <summary>Uma produção pelo jobon_id (para reabrir o select a partir de um link); null se não existe.</summary>
    Task<ProductionPickItem?> FindAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>Quantas produções a procura ao escrever mostra de cada vez.</summary>
    public const int SearchLimit = 10;

    /// <summary>
    /// O primeiro passo da procura ao escrever (Diogo, 17/10/2026: «deveria
    /// ser referência e depois produções»; «a entrada dos filtros deve poder
    /// partir de qualquer informação relacionada»): as referências que contêm
    /// <paramref name="term"/>, ou que têm uma produção cujo nº, máquina, ou
    /// referência ou lote do CM, do MF ou da BQ o contêm (sem distinção de
    /// maiúsculas). A referência com a produção mais recente primeiro, no
    /// máximo <see cref="SearchLimit"/>. Sem texto devolve uma lista vazia.
    /// </summary>
    Task<IReadOnlyList<ReferenceSearchItem>> SearchReferencesAsync(
        string? term,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// O segundo passo: as produções da referência exata (sem distinção de
    /// maiúsculas), da mais recente para a mais antiga, no máximo
    /// <see cref="SearchLimit"/>, com as datas e as ferramentas.
    /// </summary>
    Task<IReadOnlyList<ProductionSearchItem>> ListOfReferenceAsync(
        string? reference,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Uma referência encontrada ao escrever: quantas produções tem e a mais
/// recente (nº, máquina e início).
/// </summary>
public sealed record ReferenceSearchItem(
    string Reference,
    int Productions,
    string LastProductionNumber,
    string LastMachine,
    DateOnly? LastInicioEm);

/// <summary>
/// Uma produção de uma referência: a referência, o nº, a máquina, as
/// datas e as ferramentas (referência · lote) de CM, MF e BQ, quando há.
/// </summary>
public sealed record ProductionSearchItem(
    int JobOnId,
    string Reference,
    string ProductionNumber,
    string Machine,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    string? Cm,
    string? Mf,
    string? Bq);

/// <summary>Uma opção do select das produções de uma referência.</summary>
public sealed record ProductionPickItem(
    int JobOnId,
    string ProductionNumber,
    string Reference,
    string Machine,
    DateOnly? InicioEm)
{
    /// <summary>O rótulo do select: referência · nº · máquina · data de início (quando há).</summary>
    public string Label => InicioEm is { } inicio
        ? $"{Reference} · {ProductionNumber} · {Machine} · {inicio:dd/MM/yyyy}"
        : $"{Reference} · {ProductionNumber} · {Machine}";
}

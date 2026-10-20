namespace DMO.Alpha.Core.Production;

/// <summary>
/// Query purpose-specific do browser «Procurar resumo» do Resumo do
/// Controlo: as produções existentes com os factos mínimos de
/// identificação, mais recentes primeiro. O filtro por referência é
/// IGUALDADE EXATA aplicada no servidor (regra FRONTEND.md: o frontend
/// não carrega o registo completo para filtrar localmente; precedente
/// ToolCandidateQuery — sem trimming, sem case-insensitivity, sem
/// contains: nenhuma semântica difusa inventada). É uma projeção sobre
/// as tabelas reais, não um repositório genérico nem uma segunda fonte
/// de verdade. A seleção da produção a abrir é sempre um ato humano
/// explícito («Abrir»): o resultado de uma única linha nunca abre sozinho.
/// </summary>
public interface IResumoProductionQuery
{
    /// <summary>
    /// Devolve AS ÚLTIMAS PRODUÇÕES (o Owner 2026-10-13: «deveriamos
    /// escrever a referencia e depois de selecionar a referencia aparecer
    /// as ultimas produçoes em baixo, por exemplo uma lista com ultimas
    /// 5» + «ter opçao de procurar na lista por paginaçao») — 5 por
    /// página, ordenadas por jobon_id decrescente. A página 1 é a mais
    /// recente; as seguintes navegam para as mais antigas. Quando
    /// <paramref name="reference"/> é fornecido, filtra no servidor pelas
    /// produções cuja referência começa pelo texto (ou a parte BQ dela) ou
    /// cujo nº de produção começa por ele, sem distinção de maiúsculas. A contagem TOTAL vem em
    /// <see cref="ResumoProductionPage.TotalCount"/> para a paginação.
    /// </summary>
    Task<ResumoProductionPage> ListAsync(
        string? reference,
        int page,
        CancellationToken cancellationToken = default);
}

/// <summary>Uma página da lista: as 5 produções + a contagem total (para a paginação).</summary>
public sealed record ResumoProductionPage(
    IReadOnlyList<ResumoProductionItem> Items,
    int TotalCount);

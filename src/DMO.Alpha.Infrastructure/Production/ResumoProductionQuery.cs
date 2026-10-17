using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Implementação purpose-specific de <see cref="IResumoProductionQuery"/>:
/// o browser «Procurar resumo» do Resumo do Controlo. Projeção direta
/// sobre as tabelas reais (JobOns + Machines) com os factos mínimos de
/// identificação — jobon_id, nº, referência, máquina; o filtro procura o
/// texto (sem distinção de maiúsculas) na referência ou no nº de produção,
/// aplicado no servidor (regra FRONTEND.md: o frontend não carrega o
/// registo completo para filtrar localmente). Ordenação determinística por jobon_id decrescente (o mais
/// recente primeiro). AsNoTracking: consulta pura — nenhuma mutação,
/// nenhuma auto-seleção; a produção a abrir é sempre escolha humana
/// explícita («Abrir»).
/// </summary>
public sealed class ResumoProductionQuery : IResumoProductionQuery
{
    private readonly DmoDbContext _db;

    public ResumoProductionQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<ResumoProductionPage> ListAsync(
        string? reference,
        int page,
        CancellationToken cancellationToken = default)
    {
        // Aplicado no servidor: null/vazio = dimensão não aplicada (todas as produções). O FILTRO
        // NA ENTIDADE (j.Reference) ANTES da projeção — o EF10/Npgsql não
        // traduz um Where sobre o campo do record PROJETADO (o bug vivo do
        // ?reference: 500 em toda a procura com referência; os testes
        // InMemory não o apanham porque avaliam LINQ-to-objects).
        // H22-02: o operador escreve «5932t173», «5932» ou o nº «202601» —
        // a procura ignora maiúsculas e aceita parte da referência ou do nº
        // de produção (mesma regra das Ferramentas: c142 e C142 são o mesmo
        // facto).
        var jobQuery = _db.JobOns.AsNoTracking();
        var term = reference?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            jobQuery = jobQuery.Where(j =>
                j.Reference.ToUpper().Contains(term)
                || j.ProductionNumber.ToUpper().Contains(term));
        }

        var baseQuery =
            from j in jobQuery
            join m in _db.Machines on j.MachineId equals m.Id
            orderby j.Id descending
            select new ResumoProductionItem(j.Id, j.ProductionNumber, j.Reference, m.Code);

        // A CONTAGEM total (para a paginação) antes do corte da página.
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // AS ÚLTIMAS 5 POR PÁGINA (o Owner 2026-10-13: «uma lista com
        // ultimas 5» + «ter opçao de procurar na lista por paginaçao»):
        // a página 1 é a mais recente; as seguintes navegam para as mais
        // antigas (o skip preserva a ordem).
        var safePage = Math.Max(page, 1);
        var items = await baseQuery
            .Skip((safePage - 1) * 5)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new ResumoProductionPage(items, totalCount);
    }
}

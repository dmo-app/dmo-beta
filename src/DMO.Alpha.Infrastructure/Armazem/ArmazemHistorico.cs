using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// O Histórico do Armazém sobre o <see cref="DmoDbContext"/>: os movimentos
/// filtrados e paginados na base, do mais recente para o mais antigo. O texto
/// procura a referência (como nas outras procuras), o lote exato ou o código
/// da posição (a de entrada e a de onde saiu); um movimento de uma ferramenta
/// eliminada procura-se pela referência e o lote guardados.
/// </summary>
public sealed class ArmazemHistorico(DmoDbContext db) : IArmazemHistorico
{
    public async Task<PagedList<ArmazemHistoricoLinha>> ListarAsync(
        ArmazemHistoricoFiltro filtro, int pagina, CancellationToken cancellationToken = default)
    {
        var query = db.ArmazemMovimentos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtro.ToolId))
        {
            query = query.Where(m => m.ToolId == filtro.ToolId);
        }

        if (ReferenceMatch.Normalize(filtro.Texto) is { } texto)
        {
            var ferramentas = db.Tools
                .Where(ReferenceMatch.Predicate<Tool>(t => t.Reference, texto).Or(t => t.Lot.ToUpper() == texto))
                .Select(t => t.ToolId);
            var posicoes = db.ArmazemPosicoes.Where(p => p.Codigo == texto).Select(p => (int?)p.Id);
            query = query.Where(ReferenceMatch.Predicate<ArmazemMovimento>(m => m.ToolReferenceFrozen, texto)
                .Or(m => ferramentas.Contains(m.ToolId)
                    || (m.ToolLotFrozen != null && m.ToolLotFrozen.ToUpper() == texto)
                    || posicoes.Contains(m.PosicaoId)
                    || posicoes.Contains(m.PosicaoAnteriorId)));
        }

        if (filtro.Tipo is { } tipo)
        {
            query = query.Where(m => m.Tipo == tipo);
        }

        if (filtro.Destino is { } destino)
        {
            query = query.Where(m => m.Destino == destino);
        }

        if (filtro.De is { } de)
        {
            var desde = DmoBusinessTime.ToUtc(de.ToDateTime(TimeOnly.MinValue));
            query = query.Where(m => m.OcorridoEmUtc >= desde);
        }

        if (filtro.Ate is { } ate)
        {
            var antes = DmoBusinessTime.ToUtc(ate.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(m => m.OcorridoEmUtc < antes);
        }

        var resultado = await query
            .OrderByDescending(m => m.OcorridoEmUtc).ThenByDescending(m => m.Id)
            .ToPagedListAsync(pagina, cancellationToken: cancellationToken);
        var movimentos = resultado.Items;
        var ids = movimentos.Select(m => m.ToolId).Distinct().ToList();
        var tools = await db.Tools.AsNoTracking()
            .Where(t => ids.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .ToDictionaryAsync(t => t.ToolId, cancellationToken);
        var posicaoIds = movimentos.SelectMany(m => new[] { m.PosicaoId, m.PosicaoAnteriorId }).OfType<int>().Distinct().ToList();
        var codigos = posicaoIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.ArmazemPosicoes.AsNoTracking()
                .Where(p => posicaoIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Codigo, cancellationToken);

        var linhas = movimentos.Select(m =>
        {
            var tool = tools.GetValueOrDefault(m.ToolId);
            return new ArmazemHistoricoLinha(
                m.Id,
                m.OcorridoEmUtc,
                tool?.ToolId,
                tool?.Type,
                tool?.Reference ?? m.ToolReferenceFrozen ?? "?",
                tool?.Lot ?? m.ToolLotFrozen ?? "?",
                m.Tipo,
                ArmazemMovimentos.Descricao(m, codigos),
                m.Observacoes,
                m.RegistadoPor);
        }).ToList();
        return new PagedList<ArmazemHistoricoLinha>(linhas, resultado.TotalCount, resultado.PageNumber, resultado.PageSize);
    }
}

using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// Os movimentos do Armazém (a lista da função Movimentos, antes o
/// Histórico) sobre o <see cref="DmoDbContext"/>: os movimentos
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

        if (filtro.TipoFerramenta is { } tipoFerramenta)
        {
            var doTipo = db.Tools.Where(t => t.Type == tipoFerramenta).Select(t => t.ToolId);
            query = query.Where(m => doTipo.Contains(m.ToolId));
        }

        if (ReferenceMatch.Normalize(filtro.Referencia) is { } referencia)
        {
            var comReferencia = db.Tools.Where(ReferenceMatch.Predicate<Tool>(t => t.Reference, referencia)).Select(t => t.ToolId);
            query = query.Where(ReferenceMatch.Predicate<ArmazemMovimento>(m => m.ToolReferenceFrozen, referencia)
                .Or(m => comReferencia.Contains(m.ToolId)));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Lote))
        {
            var lote = filtro.Lote.Trim().ToUpperInvariant();
            var comLote = db.Tools.Where(t => t.Lot.ToUpper() == lote).Select(t => t.ToolId);
            query = query.Where(m => comLote.Contains(m.ToolId) || (m.ToolLotFrozen != null && m.ToolLotFrozen.ToUpper() == lote));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Linha))
        {
            var linha = filtro.Linha.Trim();
            var naLinha = db.ToolMachineLinks.Where(l => l.Machine.Code == linha).Select(l => l.ToolId);
            query = query.Where(m => naLinha.Contains(m.ToolId));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Reparador))
        {
            // A Saída para ele, e a Entrada que volta da Reparação logo a seguir a essa Saída.
            var reparador = filtro.Reparador.Trim();
            query = query.Where(m => m.ReparadorNome == reparador
                || (m.Tipo == ArmazemMovimentoTipo.Entrada && m.Origem == ArmazemOrigem.Reparacao
                    && db.ArmazemMovimentos.Where(p => p.ToolId == m.ToolId && p.Id < m.Id)
                        .OrderByDescending(p => p.Id).Select(p => p.ReparadorNome).FirstOrDefault() == reparador));
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

        // A Entrada que volta da Reparação não guarda o reparador: é o da Saída antes dela.
        var regressos = movimentos.Where(m => m.Tipo == ArmazemMovimentoTipo.Entrada && m.Origem == ArmazemOrigem.Reparacao).ToList();
        var reparadorDoRegresso = new Dictionary<int, string?>();
        if (regressos.Count > 0)
        {
            var toolsDosRegressos = regressos.Select(m => m.ToolId).Distinct().ToList();
            var maxId = regressos.Max(m => m.Id);
            var anteriores = await db.ArmazemMovimentos.AsNoTracking()
                .Where(m => toolsDosRegressos.Contains(m.ToolId) && m.Id < maxId)
                .Select(m => new { m.Id, m.ToolId, m.ReparadorNome })
                .ToListAsync(cancellationToken);
            foreach (var e in regressos)
            {
                reparadorDoRegresso[e.Id] = anteriores.Where(a => a.ToolId == e.ToolId && a.Id < e.Id)
                    .OrderByDescending(a => a.Id).Select(a => a.ReparadorNome).FirstOrDefault();
            }
        }

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
                m.RegistadoPor)
            {
                Destino = m.Destino,
                Reparador = m.ReparadorNome ?? reparadorDoRegresso.GetValueOrDefault(m.Id),
                Posicao = m.PosicaoId is { } p ? codigos.GetValueOrDefault(p) : null
            };
        }).ToList();
        return new PagedList<ArmazemHistoricoLinha>(linhas, resultado.TotalCount, resultado.PageNumber, resultado.PageSize);
    }

    public async Task<IReadOnlyList<string>> ReparadoresAsync(CancellationToken cancellationToken = default) =>
        await db.ArmazemMovimentos.AsNoTracking()
            .Where(m => m.ReparadorNome != null)
            .Select(m => m.ReparadorNome!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> LinhasAsync(CancellationToken cancellationToken = default) =>
        await db.Machines.AsNoTracking().Select(m => m.Code).OrderBy(c => c).ToListAsync(cancellationToken);
}

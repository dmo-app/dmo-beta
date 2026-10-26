using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.ReparacaoInterna;

/// <summary>
/// A Consulta da Reparação Interna sobre o <see cref="DmoDbContext"/>
/// (blueprint reparacao-interna/OVERVIEW §1 «Consultar», §3 cuidado de
/// volume, RI-05). É a tabela com mais registos, por isso lê em três
/// consultas curtas e nunca uma por linha: a contagem e os ids da página só
/// sobre a tabela das reparações, pelos índices (a produção, o dia, a
/// máquina com o dia, a pessoa com o dia); depois os 25 da página numa
/// consulta com a produção, a máquina, quem e a ferramenta do contexto.
/// A referência e o lote vêm do contexto (a reparação não os copia); o nome
/// técnico nunca sai daqui. Com a ferramenta escrita (peça 4), a escrita e,
/// depois de associada, a ferramenta associada; as por associar da página
/// levam, numa quarta consulta, as ferramentas do mesmo tipo que servem para
/// a janela de associar. Sem Job On, a produção fica vazia.
/// </summary>
public sealed class ReparacoesInternasConsulta(DmoDbContext db) : IReparacoesInternasConsulta
{
    public async Task<PagedList<RiConsultaLinha>> ConsultarAsync(
        RiConsultaFiltro filtro, int? pagina, CancellationToken cancellationToken = default)
    {
        // Abre vazia: sem filtro não se lê nada (Diogo, «Procura e filtros»).
        if (!filtro.ComFiltro)
        {
            return PagedList<RiConsultaLinha>.Empty;
        }

        var query = Filtrar(filtro);

        // Da mais recente para a mais antiga: o Id cresce com o registo (a
        // hora é a do servidor ao gravar), por isso ordena-se pela chave e
        // a base não tem de ordenar pela hora.
        var ids = await query.OrderByDescending(r => r.Id).Select(r => r.Id)
            .ToPagedListAsync(pagina, cancellationToken: cancellationToken);
        if (ids.Items.Count == 0)
        {
            return new PagedList<RiConsultaLinha>([], ids.TotalCount, ids.PageNumber, ids.PageSize);
        }

        var daPagina = ids.Items.ToList();
        var ferramentas = db.Tools.AsNoTracking().Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId);
        var linhas = await (
            from r in db.ReparacoesInternas.AsNoTracking()
            where daPagina.Contains(r.Id)
            join j in db.JobOns.AsNoTracking() on r.JobOnId equals (int?)j.Id into js
            from j in js.DefaultIfEmpty()
            join m in db.Machines.AsNoTracking() on r.MachineId equals m.Id
            join u in db.Users.AsNoTracking() on r.RegistadoPorUserId equals (Guid?)u.Id into us
            from u in us.DefaultIfEmpty()
            join c in db.CmContexts.AsNoTracking() on r.CmContextId equals (int?)c.Id into cs
            from c in cs.DefaultIfEmpty()
            join f in db.MfContexts.AsNoTracking() on r.MfContextId equals (int?)f.Id into fs
            from f in fs.DefaultIfEmpty()
            join t in ferramentas on (c != null ? c.ToolId : f != null ? f.ToolId : null) equals t.ToolId into ts
            from t in ts.DefaultIfEmpty()
            join a in ferramentas on r.AssociadaToolId equals a.ToolId into ats
            from a in ats.DefaultIfEmpty()
            select new
            {
                r.Id,
                r.DiaOperacional,
                r.RegistadoEmUtc,
                r.Hora,
                r.Trabalho,
                Maquina = m.Code,
                r.JobOnId,
                ProductionNumber = j != null ? j.ProductionNumber : null,
                Reference = j != null ? j.Reference : null,
                r.Tipo,
                ContextoId = c != null ? c.Id : f != null ? f.Id : (int?)null,
                FerramentaReferencia = t != null ? t.Reference : null,
                FerramentaLote = t != null ? t.Lot : null,
                FerramentaToolId = t != null ? t.ToolId : null,
                r.Numero,
                Quem = u != null ? u.Name : null,
                r.ReferenciaEscrita,
                r.LoteEscrito,
                r.AssociadaToolId,
                AssociadaReferencia = a != null ? a.Reference : null,
                AssociadaLote = a != null ? a.Lot : null
            })
            .ToListAsync(cancellationToken);

        var candidatas = await CandidatasAsync(
            linhas.Where(l => l.ReferenciaEscrita != null && l.AssociadaToolId == null)
                .Select(l => (l.Tipo, l.ReferenciaEscrita!)).Distinct().ToList(),
            cancellationToken);

        // A ordem da página (a consulta das linhas não a garante). Sem
        // referência, a ferramenta foi eliminada (o contexto passou para a
        // REMOVED); sem contexto, não há ferramenta a mostrar.
        var porId = linhas.ToDictionary(l => l.Id);
        var resultado = daPagina.Where(porId.ContainsKey).Select(id => porId[id]).Select(l => new RiConsultaLinha(
                l.Id,
                l.DiaOperacional,
                l.RegistadoEmUtc,
                l.Maquina,
                l.JobOnId,
                l.ProductionNumber,
                l.Reference,
                l.Tipo,
                l.ContextoId is { } contexto ? new RiFerramenta(contexto, l.FerramentaReferencia, l.FerramentaLote) : null,
                l.Numero,
                l.Quem,
                l.ReferenciaEscrita is null ? null : Escrita(
                    l.ReferenciaEscrita, l.LoteEscrito!, l.AssociadaToolId, l.AssociadaReferencia, l.AssociadaLote,
                    l.AssociadaToolId is null ? candidatas.GetValueOrDefault((l.Tipo, l.ReferenciaEscrita)) ?? [] : null),
                l.FerramentaToolId,
                l.Hora,
                l.Trabalho))
            .ToList();
        return new PagedList<RiConsultaLinha>(resultado, ids.TotalCount, ids.PageNumber, ids.PageSize);
    }

    // As reparações que contam (sem as anuladas, como na lista do registo) com os filtros.
    private IQueryable<Reparacao> Filtrar(RiConsultaFiltro filtro)
    {
        // As anuladas não contam, como na lista do registo.
        var query = db.ReparacoesInternas.AsNoTracking().Where(r => r.AnuladaEmUtc == null);
        if (filtro.MachineId is { } maquina)
        {
            query = query.Where(r => r.MachineId == maquina);
        }

        // Os dias são o dia operacional (o da viragem), os dois inclusive.
        if (filtro.De is { } de)
        {
            query = query.Where(r => r.DiaOperacional >= de);
        }

        if (filtro.Ate is { } ate)
        {
            query = query.Where(r => r.DiaOperacional <= ate);
        }

        if (filtro.PessoaId is { } pessoa)
        {
            query = query.Where(r => r.RegistadoPorUserId == pessoa);
        }

        if (filtro.Tipo is { } tipo)
        {
            query = query.Where(r => r.Tipo == tipo);
        }

        if (filtro.Trabalho is { } trabalho)
        {
            query = query.Where(r => r.Trabalho == trabalho);
        }

        // Só as por associar: pelo índice filtrado delas.
        if (filtro.PorAssociar)
        {
            query = query.Where(r => r.ReferenciaEscrita != null && r.AssociadaToolId == null);
        }

        // A produção escolhida manda; só com a referência, as produções cuja
        // referência começa pelo texto (a regra única das procuras: «60»
        // traz todas as que começam por 60), numa subconsulta pelo jobon_id.
        if (filtro.JobOnId is { } jobOnId)
        {
            query = query.Where(r => r.JobOnId == jobOnId);
        }
        else if (ReferenceMatch.Normalize(filtro.Referencia) is { } referencia)
        {
            var producoes = db.JobOns.AsNoTracking()
                .Where(ReferenceMatch.Predicate<JobOn>(j => j.Reference, referencia))
                .Select(j => (int?)j.Id);
            query = query.Where(r => producoes.Contains(r.JobOnId));
        }

        return query;
    }

    public async Task<IReadOnlyList<RiResumoPessoa>> ResumoPorPessoaAsync(
        RiConsultaFiltro filtro, CancellationToken cancellationToken = default)
    {
        if (!filtro.ComFiltro)
        {
            return [];
        }

        // Uma só consulta agrupada: reparações e dias diferentes por pessoa.
        var grupos = await Filtrar(filtro)
            .GroupBy(r => r.RegistadoPorUserId)
            .Select(g => new { PessoaId = g.Key, Reparacoes = g.Count(), Dias = g.Select(r => r.DiaOperacional).Distinct().Count() })
            .ToListAsync(cancellationToken);
        var ids = grupos.Where(g => g.PessoaId != null).Select(g => g.PessoaId!.Value).ToList();
        var nomes = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        return grupos
            .Select(g => new RiResumoPessoa(g.PessoaId, g.PessoaId is { } id && nomes.TryGetValue(id, out var nome) ? nome : "—", g.Reparacoes, g.Dias))
            .OrderByDescending(r => r.Reparacoes).ThenBy(r => r.Nome, StringComparer.Ordinal)
            .ToList();
    }

    // As ferramentas registadas do mesmo tipo e da referência escrita (sem a
    // REMOVED; a referência sem olhar a maiúsculas), numa consulta para as por associar da página, pelo índice
    // (tipo, referência, lote) das ferramentas.
    private async Task<Dictionary<(ReparacaoTipo, string), List<RiCandidata>>> CandidatasAsync(
        IReadOnlyList<(ReparacaoTipo Tipo, string Referencia)> escritas, CancellationToken cancellationToken)
    {
        if (escritas.Count == 0)
        {
            return [];
        }

        var referencias = escritas.Select(e => e.Referencia).Distinct().ToList();
        var tools = await db.Tools.AsNoTracking()
            .Where(t => (t.Type == ToolType.Cm || t.Type == ToolType.Mf)
                && t.ToolId != ToolRegistryConstants.RemovedToolId
                && referencias.Contains(t.Reference.ToUpper()))
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .ToListAsync(cancellationToken);
        return escritas.ToDictionary(
            e => (e.Tipo, e.Referencia),
            e => tools.Where(t => (int)t.Type == (int)e.Tipo && t.Reference.ToUpperInvariant() == e.Referencia)
                .OrderBy(t => t.Lot.Length).ThenBy(t => t.Lot, StringComparer.Ordinal).ThenBy(t => t.ToolId, StringComparer.Ordinal)
                .Select(t => new RiCandidata(t.ToolId, t.Reference, t.Lot))
                .ToList());
    }

    // As do lote escrito primeiro (a mesma ferramenta, se estiver registada);
    // sem nenhuma, as da referência.
    private static RiEscrita Escrita(
        string referencia, string lote, string? associada, string? associadaReferencia, string? associadaLote, List<RiCandidata>? daReferencia)
    {
        if (daReferencia is null)
        {
            return new RiEscrita(referencia, lote, associada, associadaReferencia, associadaLote);
        }

        var doLote = daReferencia.Where(c => string.Equals(c.Lote.Trim(), lote, StringComparison.OrdinalIgnoreCase)).ToList();
        return doLote.Count > 0
            ? new RiEscrita(referencia, lote, Candidatas: doLote, DoLote: true)
            : new RiEscrita(referencia, lote, Candidatas: daReferencia, DoLote: false);
    }

    public async Task<IReadOnlyList<RiPessoa>> PessoasAsync(CancellationToken cancellationToken = default)
    {
        // Os ids distintos pelo índice da pessoa; os nomes vêm dos
        // utilizadores (o ADMIN e os eliminados ficam NULL e não aparecem).
        var quem = db.ReparacoesInternas.AsNoTracking()
            .Where(r => r.RegistadoPorUserId != null)
            .Select(r => r.RegistadoPorUserId)
            .Distinct();
        return await db.Users.AsNoTracking()
            .Where(u => quem.Contains((Guid?)u.Id))
            .OrderBy(u => u.Name).ThenBy(u => u.Id)
            .Select(u => new RiPessoa(u.Id, u.Name))
            .ToListAsync(cancellationToken);
    }
}

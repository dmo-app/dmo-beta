using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Programada;

/// <summary>
/// A Reparação Programada sobre o <see cref="DmoDbContext"/> (Diogo,
/// 22/10/2026). Só as CM e os MF entram (as BQ vão ao reparador pelas
/// Boquilhas). A lista só se atualiza pelos movimentos do Armazém
/// (<see cref="ReparacaoProgramadaMovimentos"/>).
/// </summary>
public sealed class ReparacaoProgramadaServico(DmoDbContext db, ICurrentAccountContext account) : IReparacaoProgramada
{
    /// <summary>Quantas ferramentas a procura mostra.</summary>
    public const int ProcuraLimite = 50;

    public async Task<IReadOnlyList<ProgramadaCandidata>> ProcurarAsync(ProgramadaProcura procura, CancellationToken cancellationToken = default)
    {
        if (procura.Vazia)
        {
            return [];
        }

        var query = db.Tools.AsNoTracking()
            .Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId && (t.Type == ToolType.Cm || t.Type == ToolType.Mf));
        if (procura.Tipo is ToolType.Cm or ToolType.Mf)
        {
            query = query.Where(t => t.Type == procura.Tipo);
        }

        if (ReferenceMatch.Normalize(procura.Referencia) is { } referencia)
        {
            query = query.Where(ReferenceMatch.Predicate<Tool>(t => t.Reference, referencia));
        }

        if (!string.IsNullOrWhiteSpace(procura.Lote))
        {
            var lote = procura.Lote.Trim().ToUpperInvariant();
            query = query.Where(t => t.Lot.ToUpper() == lote);
        }

        if (!string.IsNullOrWhiteSpace(procura.Linha))
        {
            var linha = procura.Linha.Trim();
            var naLinha = db.ToolMachineLinks.Where(l => l.Machine.Code == linha).Select(l => l.ToolId);
            query = query.Where(t => naLinha.Contains(t.ToolId));
        }

        if (procura.Estado is { } estado)
        {
            // O estado atual é o último registado.
            var comEstado = db.FerramentaEstados
                .Where(e => e.Estado == estado && !db.FerramentaEstados.Any(o => o.ToolId == e.ToolId && o.Id > e.Id))
                .Select(e => e.ToolId);
            query = query.Where(t => comEstado.Contains(t.ToolId));
        }

        var tools = await query
            .OrderBy(t => t.Reference).ThenBy(t => t.Lot).ThenBy(t => t.Type)
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .Take(ProcuraLimite)
            .ToListAsync(cancellationToken);
        var ids = tools.Select(t => t.ToolId).ToList();
        var factos = await FactosAsync(ids, cancellationToken);
        var emLista = await (
                from i in db.ReparacaoProgramadaItens.AsNoTracking()
                join l in db.ReparacaoProgramadaListas.AsNoTracking() on i.ListaId equals l.Id
                where l.FechadaEmUtc == null && i.EntradaMovimentoId == null && ids.Contains(i.ToolId)
                select new { i.ToolId, l.ReparadorNome })
            .ToListAsync(cancellationToken);
        var listas = emLista.GroupBy(e => e.ToolId).ToDictionary(g => g.Key, g => g.First().ReparadorNome);

        return tools.Select(t =>
        {
            var f = factos[t.ToolId];
            return new ProgramadaCandidata(t.ToolId, t.Type, t.Reference, t.Lot, f.Utilizacao, f.Linhas, f.Posicao, f.OndeEsta, f.Estado,
                listas.GetValueOrDefault(t.ToolId));
        }).ToList();
    }

    public async Task<ProgramadaAssociacaoSaida> CriarListaAsync(int? reparadorId, CancellationToken cancellationToken = default)
    {
        if (reparadorId is null)
        {
            return new(ProgramadaAssociacao.SemReparador, null, 0, []);
        }

        var reparador = await db.Reparadores.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reparadorId && r.Ativo, cancellationToken);
        if (reparador is null)
        {
            return new(ProgramadaAssociacao.ReparadorInvalido, null, 0, []);
        }

        var lista = new ReparacaoProgramadaLista
        {
            ReparadorId = reparador.Id,
            ReparadorNome = reparador.Nome,
            CriadaEmUtc = DateTime.UtcNow,
            CriadaPor = Autor()
        };
        db.ReparacaoProgramadaListas.Add(lista);
        await db.SaveChangesAsync(cancellationToken);
        return new(ProgramadaAssociacao.Associada, lista.Id, 0, []);
    }

    public async Task<ProgramadaAssociacaoSaida> AssociarAsync(
        IReadOnlyCollection<string> toolIds, int? listaId, int? reparadorId, CancellationToken cancellationToken = default)
    {
        var pedidas = toolIds.Where(id => !string.IsNullOrWhiteSpace(id) && id != ToolRegistryConstants.RemovedToolId).Distinct().ToList();
        if (pedidas.Count == 0)
        {
            return new(ProgramadaAssociacao.SemFerramentas, listaId, 0, []);
        }

        ReparacaoProgramadaLista? lista;
        if (listaId is { } id)
        {
            lista = await db.ReparacaoProgramadaListas.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
            if (lista is null)
            {
                return new(ProgramadaAssociacao.ListaNaoExiste, null, 0, []);
            }

            if (lista.FechadaEmUtc is not null)
            {
                return new(ProgramadaAssociacao.ListaFechada, lista.Id, 0, []);
            }
        }
        else
        {
            if (reparadorId is null)
            {
                return new(ProgramadaAssociacao.SemReparador, null, 0, []);
            }

            var reparador = await db.Reparadores.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == reparadorId && r.Ativo, cancellationToken);
            if (reparador is null)
            {
                return new(ProgramadaAssociacao.ReparadorInvalido, null, 0, []);
            }

            lista = new ReparacaoProgramadaLista
            {
                ReparadorId = reparador.Id,
                ReparadorNome = reparador.Nome,
                CriadaEmUtc = DateTime.UtcNow,
                CriadaPor = Autor()
            };
        }

        var tools = await db.Tools.AsNoTracking()
            .Where(t => pedidas.Contains(t.ToolId))
            .Select(t => new { t.ToolId, t.Type, t.Reference, t.Lot })
            .ToListAsync(cancellationToken);
        var avisos = new List<string>();
        var bq = tools.Where(t => t.Type == ToolType.Bq).ToList();
        if (bq.Count > 0)
        {
            avisos.Add($"As BQ não entram na Reparação Programada (vão ao reparador pelas Boquilhas): {string.Join(", ", bq.Select(t => $"{t.Reference} · lote {t.Lot}"))}.");
        }

        var validas = tools.Where(t => t.Type != ToolType.Bq).ToList();
        var ids = validas.Select(t => t.ToolId).ToList();
        var jaNaLista = lista.Id == 0
            ? []
            : await db.ReparacaoProgramadaItens.AsNoTracking()
                .Where(i => i.ListaId == lista.Id && ids.Contains(i.ToolId))
                .Select(i => i.ToolId)
                .ToListAsync(cancellationToken);
        // Numa lista ativa de outro reparador: associa e avisa.
        var noutra = await (
                from i in db.ReparacaoProgramadaItens.AsNoTracking()
                join l in db.ReparacaoProgramadaListas.AsNoTracking() on i.ListaId equals l.Id
                where l.FechadaEmUtc == null && l.Id != lista.Id && i.EntradaMovimentoId == null && ids.Contains(i.ToolId)
                select new { i.ToolId, l.ReparadorNome })
            .ToListAsync(cancellationToken);
        var novas = validas.Where(t => !jaNaLista.Contains(t.ToolId)).ToList();
        foreach (var t in validas.Where(t => jaNaLista.Contains(t.ToolId)))
        {
            avisos.Add($"{ArmazemTextos.Ferramenta(t.Type, t.Reference, t.Lot)} já estava nesta lista.");
        }

        foreach (var t in novas.Where(t => noutra.Any(n => n.ToolId == t.ToolId)))
        {
            avisos.Add($"{ArmazemTextos.Ferramenta(t.Type, t.Reference, t.Lot)} está também na lista de {noutra.First(n => n.ToolId == t.ToolId).ReparadorNome}.");
        }

        if (novas.Count == 0)
        {
            return new(ProgramadaAssociacao.SemFerramentas, lista.Id == 0 ? null : lista.Id, 0, avisos);
        }

        var factos = await FactosAsync(novas.Select(t => t.ToolId).ToList(), cancellationToken);
        if (lista.Id == 0)
        {
            db.ReparacaoProgramadaListas.Add(lista);
            await db.SaveChangesAsync(cancellationToken);
        }

        var agora = DateTime.UtcNow;
        foreach (var t in novas)
        {
            var f = factos[t.ToolId];
            db.ReparacaoProgramadaItens.Add(new ReparacaoProgramadaItem
            {
                ListaId = lista.Id,
                ToolId = t.ToolId,
                AdicionadaEmUtc = agora,
                Tipo = t.Type,
                Referencia = t.Reference,
                Lote = t.Lot,
                Utilizacao = f.Utilizacao,
                Linhas = f.Linhas is { Length: > ReparacaoProgramadaItem.LinhasMaxLength } l ? l[..ReparacaoProgramadaItem.LinhasMaxLength] : f.Linhas
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return new(ProgramadaAssociacao.Associada, lista.Id, novas.Count, avisos);
    }

    public async Task<IReadOnlyList<ProgramadaListaResumo>> ListasAsync(bool fechadas, CancellationToken cancellationToken = default) =>
        await db.ReparacaoProgramadaListas.AsNoTracking()
            .Where(l => fechadas ? l.FechadaEmUtc != null : l.FechadaEmUtc == null)
            .OrderByDescending(l => l.CriadaEmUtc).ThenByDescending(l => l.Id)
            .Select(l => new ProgramadaListaResumo(l.Id, l.ReparadorNome, l.CriadaEmUtc, l.CriadaPor, l.FechadaEmUtc))
            .ToListAsync(cancellationToken);

    public async Task<ProgramadaListaDetalhe?> ListaAsync(int id, CancellationToken cancellationToken = default)
    {
        var lista = await db.ReparacaoProgramadaListas.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new ProgramadaListaResumo(l.Id, l.ReparadorNome, l.CriadaEmUtc, l.CriadaPor, l.FechadaEmUtc))
            .FirstOrDefaultAsync(cancellationToken);
        if (lista is null)
        {
            return null;
        }

        var itens = await db.ReparacaoProgramadaItens.AsNoTracking()
            .Where(i => i.ListaId == id)
            .OrderBy(i => i.Referencia).ThenBy(i => i.Lote).ThenBy(i => i.Tipo).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        var movimentoIds = itens.SelectMany(i => new[] { i.SaidaMovimentoId, i.EntradaMovimentoId }).OfType<int>().ToList();
        var movimentos = await (
                from m in db.ArmazemMovimentos.AsNoTracking()
                where movimentoIds.Contains(m.Id)
                join p in db.ArmazemPosicoes.AsNoTracking() on m.PosicaoId equals p.Id into ps
                from p in ps.DefaultIfEmpty()
                select new { m.Id, m.OcorridoEmUtc, m.RegistadoPor, Posicao = p == null ? null : p.Codigo })
            .ToDictionaryAsync(m => m.Id, cancellationToken);
        var vivas = itens.Select(i => i.ToolId).Where(t => t != ToolRegistryConstants.RemovedToolId).Distinct().ToList();
        var factos = await FactosAsync(vivas, cancellationToken);

        var linhas = itens.Select(i =>
        {
            var removida = i.ToolId == ToolRegistryConstants.RemovedToolId;
            var f = removida ? null : factos.GetValueOrDefault(i.ToolId);
            var saida = i.SaidaMovimentoId is { } s ? movimentos.GetValueOrDefault(s) : null;
            var entrada = i.EntradaMovimentoId is { } e ? movimentos.GetValueOrDefault(e) : null;
            return new ProgramadaLinha(i.Id, removida ? null : i.ToolId, i.Tipo, i.Referencia, i.Lote, i.Utilizacao, i.Linhas,
                f?.OndeEsta ?? "Eliminada", f?.Estado, saida?.OcorridoEmUtc, entrada?.OcorridoEmUtc, entrada?.Posicao, entrada?.RegistadoPor);
        }).ToList();
        return new(lista, linhas);
    }

    public async Task<IReadOnlyList<ArmazemReparadorOpcao>> ReparadoresAsync(CancellationToken cancellationToken = default) =>
        await db.Reparadores.AsNoTracking()
            .Where(r => r.Ativo && (r.AplicaCm || r.AplicaMf))
            .OrderBy(r => r.Nome)
            .Select(r => new ArmazemReparadorOpcao(r.Id, r.Nome))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> LinhasAsync(CancellationToken cancellationToken = default) =>
        await db.Machines.AsNoTracking().Select(m => m.Code).OrderBy(c => c).ToListAsync(cancellationToken);

    private sealed record Factos(decimal? Utilizacao, string? Linhas, string? Posicao, string OndeEsta, FerramentaEstadoTipo? Estado);

    /// <summary>O que se sabe agora de cada ferramenta: o % de uso, as Linhas, onde está e o estado.</summary>
    private async Task<Dictionary<string, Factos>> FactosAsync(List<string> ids, CancellationToken cancellationToken)
    {
        var posicoes = (await (
                from o in db.ArmazemOcupacoes.AsNoTracking()
                join p in db.ArmazemPosicoes.AsNoTracking() on o.PosicaoId equals p.Id
                where o.LibertadaEmUtc == null && ids.Contains(o.ToolId)
                select new { o.ToolId, p.Codigo })
            .ToListAsync(cancellationToken))
            .GroupBy(p => p.ToolId).ToDictionary(g => g.Key, g => g.First().Codigo);
        var localizacoes = await db.FerramentaLocalizacoes.AsNoTracking()
            .Where(l => ids.Contains(l.ToolId))
            .ToDictionaryAsync(l => l.ToolId, cancellationToken);
        var estados = (await db.FerramentaEstados.AsNoTracking()
                .Where(e => ids.Contains(e.ToolId))
                .Select(e => new { e.Id, e.ToolId, e.Estado })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.ToolId)
            .ToDictionary(g => g.Key, g => g.MaxBy(e => e.Id)!.Estado);
        var linhas = (await db.ToolMachineLinks.AsNoTracking()
                .Where(m => ids.Contains(m.ToolId))
                .Select(m => new { m.ToolId, m.Machine.Code })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.ToolId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(m => m.Code).Order(StringComparer.OrdinalIgnoreCase)));
        var usos = (await db.FerramentaUtilizacoes.AsNoTracking()
                .Where(u => ids.Contains(u.ToolId))
                .Select(u => new { u.Id, u.ToolId, u.Percentagem })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.ToolId)
            .ToDictionary(g => g.Key, g => (decimal?)g.MaxBy(u => u.Id)!.Percentagem);

        return ids.ToDictionary(id => id, id =>
        {
            var posicao = posicoes.GetValueOrDefault(id);
            var onde = posicao is not null
                ? $"Armazém · posição {posicao}"
                : localizacoes.TryGetValue(id, out var l)
                    ? ArmazemTextos.Localizacao(l.Localizacao, l.ReparadorNome)
                    : ArmazemTextos.Localizacao(FerramentaLocalizacaoTipo.Fabrico, null);
            return new Factos(usos.GetValueOrDefault(id), linhas.GetValueOrDefault(id), posicao, onde,
                estados.TryGetValue(id, out var e) ? e : null);
        });
    }

    private string? Autor()
    {
        var actor = account.Current;
        return actor.IsAuthenticated
            ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName)
            : null;
    }
}

/// <summary>
/// O que os movimentos do Armazém fazem às listas (Diogo, 22/10/2026: «a
/// pessoa depois só tem de ir dando saídas no armazém ao retirar e faz update
/// na lista»): a Saída marca a ferramenta como retirada; a Entrada marca-a
/// como devolvida e, quando foi a última da lista, a lista fecha. Corre na
/// mesma transação do movimento.
/// </summary>
public static class ReparacaoProgramadaMovimentos
{
    /// <summary>A lista ativa em que a ferramenta ainda não voltou (o reparador), ou null.</summary>
    public static async Task<string?> ListaAtivaAsync(DmoDbContext db, string toolId, CancellationToken cancellationToken) =>
        await (
                from i in db.ReparacaoProgramadaItens.AsNoTracking()
                join l in db.ReparacaoProgramadaListas.AsNoTracking() on i.ListaId equals l.Id
                where l.FechadaEmUtc == null && i.EntradaMovimentoId == null && i.ToolId == toolId
                orderby l.CriadaEmUtc
                select l.ReparadorNome)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task AoMovimentoAsync(DmoDbContext db, ArmazemMovimento movimento, CancellationToken cancellationToken)
    {
        if (movimento.Tipo == ArmazemMovimentoTipo.Corrigir)
        {
            return;
        }

        var itens = await (
                from i in db.ReparacaoProgramadaItens
                join l in db.ReparacaoProgramadaListas on i.ListaId equals l.Id
                where l.FechadaEmUtc == null && i.ToolId == movimento.ToolId && i.EntradaMovimentoId == null
                select i)
            .ToListAsync(cancellationToken);
        if (itens.Count == 0)
        {
            return;
        }

        if (movimento.Tipo == ArmazemMovimentoTipo.Saida)
        {
            foreach (var item in itens.Where(i => i.SaidaMovimentoId is null))
            {
                item.SaidaMovimentoId = movimento.Id;
            }

            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var item in itens)
        {
            item.EntradaMovimentoId = movimento.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        var listaIds = itens.Select(i => i.ListaId).Distinct().ToList();
        var porVoltar = await db.ReparacaoProgramadaItens
            .Where(i => listaIds.Contains(i.ListaId) && i.EntradaMovimentoId == null && i.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(i => i.ListaId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var fechar = await db.ReparacaoProgramadaListas
            .Where(l => listaIds.Contains(l.Id) && !porVoltar.Contains(l.Id))
            .ToListAsync(cancellationToken);
        foreach (var lista in fechar)
        {
            lista.FechadaEmUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

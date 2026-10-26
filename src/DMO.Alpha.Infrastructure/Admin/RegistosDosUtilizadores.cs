using System.Globalization;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// Lê os rastos que cada módulo já grava (quem e quando) e junta-os numa só
/// lista, os mais recentes primeiro. Cada módulo é uma consulta curta, com o
/// filtro no servidor e no máximo <see cref="IRegistosDosUtilizadores.LimitePorModulo"/>
/// linhas; a página faz-se depois de juntar.
/// </summary>
public sealed class RegistosDosUtilizadores(DmoDbContext db) : IRegistosDosUtilizadores
{
    private const int Limite = IRegistosDosUtilizadores.LimitePorModulo;

    public async Task<PagedList<RegistoDeUtilizador>> ListarAsync(RegistosFiltro filtro, int? pagina, CancellationToken cancellationToken = default)
    {
        var desde = filtro.De is { } de ? DmoBusinessTime.ToUtc(de.ToDateTime(TimeOnly.MinValue)) : DateTime.MinValue;
        var ate = filtro.Ate is { } a ? DmoBusinessTime.ToUtc(a.AddDays(1).ToDateTime(TimeOnly.MinValue)) : DateTime.MaxValue;
        var user = filtro.UserId;
        bool Inclui(RegistoModulo modulo) => filtro.Modulo is null || filtro.Modulo == modulo;

        var linhas = new List<(DateTime EmUtc, RegistoModulo Modulo, string Acao, string Detalhe, Guid? UserId)>();

        if (Inclui(RegistoModulo.JobOn))
        {
            var mudancas = await db.JobOnChanges.AsNoTracking()
                .Where(c => (user == null ? c.ChangedByUserId != null : c.ChangedByUserId == user) && c.ChangedAtUtc >= desde && c.ChangedAtUtc < ate)
                .OrderByDescending(c => c.ChangedAtUtc).Take(Limite)
                .Select(c => new { c.ChangedAtUtc, c.Field, c.OldValueFrozen, c.NewValueFrozen, c.ChangedByUserId, c.JobOn!.Reference, c.JobOn.ProductionNumber })
                .ToListAsync(cancellationToken);
            linhas.AddRange(mudancas.Select(c => (c.ChangedAtUtc, RegistoModulo.JobOn, "Alterou",
                $"{c.Reference} · {c.ProductionNumber} · {c.Field}: {c.OldValueFrozen ?? "—"} → {c.NewValueFrozen ?? "—"}", c.ChangedByUserId)));
            var removidas = await db.JobOnChanges.AsNoTracking()
                .Where(c => c.RemovedAtUtc != null && (user == null ? c.RemovedByUserId != null : c.RemovedByUserId == user) && c.RemovedAtUtc >= desde && c.RemovedAtUtc < ate)
                .OrderByDescending(c => c.RemovedAtUtc).Take(Limite)
                .Select(c => new { c.RemovedAtUtc, c.Field, c.RemovedByUserId, c.JobOn!.Reference, c.JobOn.ProductionNumber })
                .ToListAsync(cancellationToken);
            linhas.AddRange(removidas.Select(c => (c.RemovedAtUtc!.Value, RegistoModulo.JobOn, "Removeu uma alteração",
                $"{c.Reference} · {c.ProductionNumber} · {c.Field}", c.RemovedByUserId)));
        }

        if (Inclui(RegistoModulo.ReparacaoInterna))
        {
            var registos = await (
                    from r in db.ReparacoesInternas.AsNoTracking()
                    join m in db.Machines.AsNoTracking() on r.MachineId equals m.Id
                    where (user == null ? r.RegistadoPorUserId != null : r.RegistadoPorUserId == user) && r.RegistadoEmUtc >= desde && r.RegistadoEmUtc < ate
                    orderby r.RegistadoEmUtc descending
                    select new { r.RegistadoEmUtc, r.Tipo, r.Numero, r.RegistadoPorUserId, Linha = m.Code })
                .Take(Limite).ToListAsync(cancellationToken);
            linhas.AddRange(registos.Select(r => (r.RegistadoEmUtc, RegistoModulo.ReparacaoInterna, "Registou",
                $"{(r.Tipo == ReparacaoTipo.Cm ? "CM" : "MF")} nº {r.Numero} · linha {r.Linha}", r.RegistadoPorUserId)));
            var rasto = await db.ReparacoesInternasRasto.AsNoTracking()
                .Where(t => (user == null ? t.PorUserId != null : t.PorUserId == user) && t.EmUtc >= desde && t.EmUtc < ate)
                .OrderByDescending(t => t.EmUtc).Take(Limite)
                .Select(t => new { t.EmUtc, t.Acao, t.NumeroAntes, t.NumeroDepois, t.PorUserId })
                .ToListAsync(cancellationToken);
            linhas.AddRange(rasto.Select(t => (t.EmUtc, RegistoModulo.ReparacaoInterna,
                t.Acao switch { ReparacaoAcao.Corrigido => "Corrigiu", ReparacaoAcao.Anulado => "Anulou", _ => "Associou" },
                t.Acao == ReparacaoAcao.Corrigido ? $"nº {t.NumeroAntes} → {t.NumeroDepois}" : $"nº {t.NumeroAntes}", t.PorUserId)));
        }

        if (Inclui(RegistoModulo.Armazem))
        {
            var movimentos = await (
                    from m in db.ArmazemMovimentos.AsNoTracking()
                    join t in db.Tools.AsNoTracking() on m.ToolId equals t.ToolId into ts
                    from t in ts.DefaultIfEmpty()
                    where (user == null ? m.RegistadoPorUserId != null : m.RegistadoPorUserId == user) && m.RegistadoEmUtc >= desde && m.RegistadoEmUtc < ate
                    orderby m.RegistadoEmUtc descending
                    select new { m.RegistadoEmUtc, m.Tipo, m.RegistadoPorUserId, Type = t != null ? (ToolType?)t.Type : null, Reference = m.ToolReferenceFrozen ?? (t != null ? t.Reference : null), Lot = m.ToolLotFrozen ?? (t != null ? t.Lot : null) })
                .Take(Limite).ToListAsync(cancellationToken);
            linhas.AddRange(movimentos.Select(m => (m.RegistadoEmUtc, RegistoModulo.Armazem,
                m.Tipo switch { ArmazemMovimentoTipo.Entrada => "Entrada", ArmazemMovimentoTipo.Saida => "Saída", _ => "Corrigiu a localização" },
                Ferramenta(m.Type, m.Reference, m.Lot), m.RegistadoPorUserId)));
        }

        if (Inclui(RegistoModulo.Boquilhas))
        {
            var movimentos = await db.BqMovements.AsNoTracking()
                .Where(m => (user == null ? m.RegisteredByUserId != null : m.RegisteredByUserId == user) && m.CreatedAtUtc >= desde && m.CreatedAtUtc < ate)
                .OrderByDescending(m => m.CreatedAtUtc).Take(Limite)
                .Select(m => new { m.CreatedAtUtc, m.Type, m.Quantity, m.MachineCodeFrozen, m.RegisteredByUserId, Reference = m.Trace.ToolReferenceFrozen ?? (m.Trace.Tool != null ? m.Trace.Tool.Reference : null), Lot = m.Trace.ToolLotFrozen ?? (m.Trace.Tool != null ? m.Trace.Tool.Lot : null) })
                .ToListAsync(cancellationToken);
            linhas.AddRange(movimentos.Select(m => (m.CreatedAtUtc, RegistoModulo.Boquilhas,
                m.Type switch { BqMovementType.Saida => "Saída", BqMovementType.Entrada => "Entrada", _ => "Entrada sem reparação" },
                $"{Ferramenta(ToolType.Bq, m.Reference, m.Lot)} · {m.Quantity}{(m.MachineCodeFrozen is { Length: > 0 } linha ? " · linha " + linha : "")}", m.RegisteredByUserId)));
        }

        if (Inclui(RegistoModulo.Controlo))
        {
            var pesos = await db.Pesos.AsNoTracking()
                .Where(p => p.RegistadoEmUtc != null && (user == null ? p.RegistadoPorUserId != null : p.RegistadoPorUserId == user) && p.RegistadoEmUtc >= desde && p.RegistadoEmUtc < ate)
                .OrderByDescending(p => p.RegistadoEmUtc).Take(Limite)
                .Select(p => new { p.RegistadoEmUtc, p.RegistadoPorUserId, Cm = p.CmToolReferenceFrozen ?? p.CmTool.Reference, Bq = p.BqToolReferenceFrozen ?? p.BqTool.Reference })
                .ToListAsync(cancellationToken);
            linhas.AddRange(pesos.Select(p => (p.RegistadoEmUtc!.Value, RegistoModulo.Controlo, "Registou o Peso", $"CM {p.Cm} · BQ {p.Bq}", p.RegistadoPorUserId)));
            var pegamentos = await db.Pegamentos.AsNoTracking()
                .Where(p => p.RegistadoEmUtc != null && (user == null ? p.RegistadoPorUserId != null : p.RegistadoPorUserId == user) && p.RegistadoEmUtc >= desde && p.RegistadoEmUtc < ate)
                .OrderByDescending(p => p.RegistadoEmUtc).Take(Limite)
                .Select(p => new { p.RegistadoEmUtc, p.RegistadoPorUserId, Cm = p.CmToolReferenceFrozen ?? (p.CmTool != null ? p.CmTool.Reference : null) })
                .ToListAsync(cancellationToken);
            linhas.AddRange(pegamentos.Select(p => (p.RegistadoEmUtc!.Value, RegistoModulo.Controlo, "Registou os Pegamentos", p.Cm is null ? "—" : $"CM {p.Cm}", p.RegistadoPorUserId)));
            var decisoes = await (
                    from d in db.ControloDecisoes.AsNoTracking()
                    join c in db.Controlos.AsNoTracking() on d.ControloId equals c.Id
                    where (user == null ? d.DecididoPorUserId != null : d.DecididoPorUserId == user) && d.EmUtc >= desde && d.EmUtc < ate
                    orderby d.EmUtc descending
                    select new { d.EmUtc, d.Parte, d.Aprovado, d.DecididoPorUserId, c.JobOn.Reference, c.JobOn.ProductionNumber })
                .Take(Limite).ToListAsync(cancellationToken);
            linhas.AddRange(decisoes.Select(d => (d.EmUtc, RegistoModulo.Controlo, d.Aprovado ? "Aprovou" : "Não aprovou",
                $"{d.Parte} · {d.Reference} · {d.ProductionNumber}", d.DecididoPorUserId)));
        }

        if (Inclui(RegistoModulo.Ferramentas))
        {
            var usos = await (
                    from u in db.FerramentaUtilizacoes.AsNoTracking()
                    join t in db.Tools.AsNoTracking() on u.ToolId equals t.ToolId
                    where (user == null ? u.RegistadoPorUserId != null : u.RegistadoPorUserId == user) && u.RegistadaEmUtc >= desde && u.RegistadaEmUtc < ate
                    orderby u.RegistadaEmUtc descending
                    select new { u.RegistadaEmUtc, u.Percentagem, u.RegistadoPorUserId, t.Type, t.Reference, t.Lot })
                .Take(Limite).ToListAsync(cancellationToken);
            linhas.AddRange(usos.Select(u => (u.RegistadaEmUtc, RegistoModulo.Ferramentas, "Registou o % de uso",
                $"{Ferramenta(u.Type, u.Reference, u.Lot)} · {Percentagem(u.Percentagem)} %", u.RegistadoPorUserId)));
        }

        var ids = linhas.Select(l => l.UserId).OfType<Guid>().Distinct().ToList();
        var nomes = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.OperatorId })
            .ToDictionaryAsync(u => u.Id, u => $"{u.Name} ({u.OperatorId})", cancellationToken);

        var todas = linhas
            .OrderByDescending(l => l.EmUtc)
            .Select(l => new RegistoDeUtilizador(l.EmUtc, l.Modulo, l.Acao, l.Detalhe,
                l.UserId is { } id && nomes.TryGetValue(id, out var nome) ? nome : null))
            .ToList();
        return PagedList<RegistoDeUtilizador>.FromLoaded(todas, pagina);
    }

    private static string Ferramenta(ToolType? tipo, string? referencia, string? lote) =>
        referencia is null
            ? "—"
            : $"{(tipo is { } t ? t.ToString().ToUpperInvariant() + " " : "")}{referencia}{(string.IsNullOrWhiteSpace(lote) ? "" : " · Lote " + LoteExibido.Mostrar(lote))}";

    // Números: inteiros sem casas, os outros com 2 casas e ponto.
    private static string Percentagem(decimal valor) =>
        valor.ToString(valor % 1 == 0 ? "0" : "0.00", CultureInfo.InvariantCulture);
}

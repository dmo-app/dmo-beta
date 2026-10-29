using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Controlo;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Search;

/// <summary>
/// Opções dos filtros dependentes da referência. As consultas históricas leem a
/// linha e os nomes guardados no movimento; as ferramentas usam as associações atuais.
/// Não lê a página de resultados (que pode conter apenas 25 linhas).
/// </summary>
public sealed class ReferenceOptionsQuery(DmoDbContext db)
{
    public async Task<IReadOnlyDictionary<string, string[]>> ReadAsync(
        string scope, string reference, string? lot, CancellationToken ct = default)
    {
        if (ReferenceMatch.Normalize(reference) is not { } term) return new Dictionary<string, string[]>();
        var tools = db.Tools.AsNoTracking().Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId);
        // A mesma procura do catálogo inclui referências completas (CM/MF + parte BQ).
        var matchingTools = new Tools.ToolCandidateQuery(db)
            .ApplyFilters(new ToolCandidateFilter(ReferenceContains: term, Lot: string.IsNullOrWhiteSpace(lot) ? null : lot.Trim()));
        if (scope == "programada") matchingTools = matchingTools.Where(t => t.Type != ToolType.Bq);
        var toolIds = matchingTools.Select(t => t.ToolId);
        var jobs = db.JobOns.AsNoTracking().Where(ReferenceMatch.Predicate<DMO.Alpha.Core.Production.JobOn>(j => j.Reference, term));
        IQueryable<Row> rows;
        if (scope == "bq")
        {
            var jobIds = jobs.Select(j => (int?)j.Id);
            var matchingTraces = db.BqRepairTraces.AsNoTracking()
                .Where(ReferenceMatch.Predicate<DMO.Alpha.Core.Boquilhas.BqRepairTrace>(t => t.ToolReferenceFrozen, term));
            if (!string.IsNullOrWhiteSpace(lot)) matchingTraces = matchingTraces.Where(t => t.ToolLotFrozen == lot.Trim());
            var traceIds = matchingTraces.Select(t => t.Id);
            rows = from m in db.BqMovements.AsNoTracking()
                   join trace in db.BqRepairTraces.AsNoTracking() on m.BqRepairTraceId equals trace.Id
                   join context in db.BqContexts.AsNoTracking() on trace.BqContextId equals (int?)context.Id into contexts
                   from context in contexts.DefaultIfEmpty()
                   join job in db.JobOns.AsNoTracking() on (context != null ? context.JobOnId ?? context.ReplacedJobOnId : null) equals (int?)job.Id into productions
                   from job in productions.DefaultIfEmpty()
                   where toolIds.Contains(context != null ? context.ToolId : trace.ToolId!)
                       || traceIds.Contains(trace.Id)
                       || (job != null && jobIds.Contains(job.Id) && (lot == null || lot == ""))
                   select new Row {
                       Line = m.MachineCodeFrozen ?? (job != null ? job.Machine.Code
                           : db.ToolMachineLinks.Count(l => l.ToolId == trace.ToolId) == 1
                               ? db.ToolMachineLinks.Where(l => l.ToolId == trace.ToolId).Select(l => l.Machine.Code).FirstOrDefault() : null),
                       Lot = context != null ? db.Tools.Where(t => t.ToolId == context.ToolId).Select(t => t.Lot).FirstOrDefault()
                           : db.Tools.Where(t => t.ToolId == trace.ToolId).Select(t => t.Lot).FirstOrDefault() ?? trace.ToolLotFrozen,
                       LineId = job != null ? (int?)job.MachineId : null,
                       Repairer = m.BqRepairerNameFrozen,
                       Type = m.Type == DMO.Alpha.Core.Boquilhas.BqMovementType.Saida ? "saida"
                           : m.Type == DMO.Alpha.Core.Boquilhas.BqMovementType.Entrada ? "entrada" : "entrada_sem_reparacao",
                       Piece = m.AnQuantity != null ? "an" : null
                   };
        }
        else if (scope == "armazem")
        {
            var frozen = db.ArmazemMovimentos.AsNoTracking()
                .Where(ReferenceMatch.Predicate<DMO.Alpha.Core.Armazem.ArmazemMovimento>(m => m.ToolReferenceFrozen, term));
            if (!string.IsNullOrWhiteSpace(lot)) frozen = frozen.Where(m => m.ToolLotFrozen == lot.Trim());
            var frozenIds = frozen.Select(m => m.Id);
            // As associações atuais continuam úteis quando o movimento antigo não tinha linha.
            rows = from m in db.ArmazemMovimentos.AsNoTracking()
                   where toolIds.Contains(m.ToolId) || frozenIds.Contains(m.Id)
                   join link in db.ToolMachineLinks.AsNoTracking() on m.ToolId equals link.ToolId into links
                   from link in links.DefaultIfEmpty()
                   join tool in tools on m.ToolId equals tool.ToolId into ts
                   from tool in ts.DefaultIfEmpty()
                   select new Row {
                       Line = m.MachineId != null ? db.Machines.Where(x => x.Id == m.MachineId).Select(x => x.Code).FirstOrDefault()
                           : link != null ? link.Machine.Code : null,
                       LineId = m.MachineId ?? (link != null ? (int?)link.MachineId : null),
                       Repairer = m.ReparadorNome ?? m.EntradaReparadorNome,
                       Lot = tool != null ? tool.Lot : m.ToolLotFrozen,
                       Type = tool == null ? null : tool.Type == ToolType.Cm ? "CM" : tool.Type == ToolType.Mf ? "MF" : "BQ"
                   };
        }
        else if (scope == "ri")
        {
            var ids = jobs.Select(j => (int?)j.Id);
            rows = db.ReparacoesInternas.AsNoTracking().Where(r => r.AnuladaEmUtc == null && ids.Contains(r.JobOnId))
                .Select(r => new Row {
                    LineId = r.MachineId,
                    Line = db.Machines.Where(m => m.Id == r.MachineId).Select(m => m.Code).FirstOrDefault(),
                    Person = r.RegistadoPorUserId,
                    Type = r.Tipo == DMO.Alpha.Core.ReparacaoInterna.ReparacaoTipo.Cm ? "cm" : "mf",
                    Work = r.Trabalho == DMO.Alpha.Core.ReparacaoInterna.ReparacaoTrabalho.Reparacao ? "reparacao"
                        : r.Trabalho == DMO.Alpha.Core.ReparacaoInterna.ReparacaoTrabalho.Soldar ? "soldar" : "polir"
                });
        }
        else if (scope == "revisao")
        {
            var ids = new ControloRevisaoQuery(db).MatchingJobOnIds(term);
            rows = db.JobOns.AsNoTracking().Where(j => ids.Contains(j.Id))
                .Select(j => new Row { Line = j.Machine.Code, LineId = j.MachineId });
        }
        else if (scope is "controlo" or "jobon" or "jobon-historico")
        {
            // A referência também pode ser a CM/BQ da produção (procura da Revisão).
            var matchingIds = db.CmContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => c.JobOnId ?? c.ReplacedJobOnId)
                .Concat(db.BqContexts.Where(c => toolIds.Contains(c.ToolId)).Select(c => c.JobOnId ?? c.ReplacedJobOnId));
            var ids = jobs.Select(j => (int?)j.Id);
            rows = db.JobOns.AsNoTracking().Where(j => ids.Contains(j.Id) || matchingIds.Contains(j.Id))
                .Select(j => new Row { Line = j.Machine.Code, LineId = j.MachineId });
            if (scope == "jobon")
            {
                // Criar: as linhas possíveis vêm das ferramentas já registadas, não só das produções antigas.
                rows = db.ToolMachineLinks.AsNoTracking().Where(l => toolIds.Contains(l.ToolId))
                    .Select(l => new Row { Line = l.Machine.Code, LineId = l.MachineId });
            }
        }
        else if (scope is "ferramentas" or "programada")
        {
            rows = from tool in matchingTools
                   join link in db.ToolMachineLinks.AsNoTracking() on tool.ToolId equals link.ToolId into links
                   from link in links.DefaultIfEmpty()
                   select new Row {
                       Line = link != null ? link.Machine.Code : null,
                       LineId = link != null ? (int?)link.MachineId : null,
                       Type = tool.Type == ToolType.Cm ? "CM" : tool.Type == ToolType.Mf ? "MF" : "BQ",
                       Lot = tool.Lot,
                       Factory = tool.FabricaOrigem,
                       State = db.FerramentaEstados.Where(e => e.ToolId == tool.ToolId).OrderByDescending(e => e.Id).Select(e => (int?)e.Estado).FirstOrDefault(),
                       InWarehouse = db.ArmazemOcupacoes.Any(o => o.ToolId == tool.ToolId && o.LibertadaEmUtc == null),
                       Location = db.FerramentaLocalizacoes.Where(l => l.ToolId == tool.ToolId).Select(l => (int?)l.Localizacao).FirstOrDefault(),
                       Repairer = db.FerramentaLocalizacoes.Where(l => l.ToolId == tool.ToolId).Select(l => l.ReparadorNome).FirstOrDefault(),
                       LoanFactory = db.FerramentaLocalizacoes.Where(l => l.ToolId == tool.ToolId)
                           .Select(l => db.ArmazemMovimentos.Where(m => m.Id == l.MovimentoId).Select(m => m.FabricaDestino).FirstOrDefault()).FirstOrDefault()
                   };
        }
        else return new Dictionary<string, string[]>();

        var found = await rows.Distinct().ToListAsync(ct);
        static string[] Values(IEnumerable<string?> values) => values.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var result = new Dictionary<string, string[]> {
            ["maquina"] = scope == "jobon-historico"
                ? Values(found.Select(r => r.LineId?.ToString(System.Globalization.CultureInfo.InvariantCulture))) : Values(found.Select(r => r.Line)),
            ["linha"] = Values(found.Select(r => r.Line)),
            ["machine_id"] = Values(found.Select(r => r.LineId?.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        };
        if (scope is "bq" or "armazem" or "ferramentas" or "programada")
        { result["lote"] = Values(found.Select(r => r.Lot)); result["lot"] = result["lote"]; }
        if (scope is "bq" or "armazem" or "ri" or "ferramentas" or "programada") result["tipo"] = Values(found.Select(r => r.Type));
        if (scope == "ferramentas") { result["type"] = result["tipo"]; result.Remove("tipo"); result["fabrica"] = Values(found.Select(r => r.Factory)); }
        if (scope is "bq" or "armazem") result["reparador"] = Values(found.Select(r => r.Repairer));
        if (scope == "bq") result["peca"] = Values(found.Select(r => r.Piece));
        if (scope == "ri") { result["pessoa"] = Values(found.Select(r => r.Person?.ToString())); result["trabalho"] = Values(found.Select(r => r.Work)); }
        if (scope is "ferramentas" or "programada")
        {
            result["estado"] = Values(found.Select(r => r.State switch {
                1 => scope == "programada" ? "Novo" : "novo", 2 => scope == "programada" ? "Reparado" : "reparado",
                3 => scope == "programada" ? "PorReparar" : "por_reparar", 4 => scope == "programada" ? "Sucatado" : "sucatado", _ => null }));
            if (scope == "ferramentas") result["local"] = Values(found.SelectMany(r => r.InWarehouse ? new[] { "armazem" }
                : r.Location == 2 ? new[] { "reparacao", r.Repairer == null ? null : "reparacao:" + r.Repairer }
                : r.Location == 3 ? new[] { "emprestado", r.LoanFactory == null ? null : "emprestado:" + r.LoanFactory }
                : r.Location == 4 ? new[] { "sucata" } : new[] { "fabrico" }));
        }
        if (scope == "revisao")
        {
            var ids = new ControloRevisaoQuery(db).MatchingJobOnIds(term);
            var states = await ControloAprovacao.Estados(db).Where(c => ids.Contains(c.JobOnId))
                .Select(c => c.ApprovedAtUtc != null ? "aprovado" : c.NotApprovedAtUtc != null ? "nao-aprovado" : "pendente").Distinct().ToListAsync(ct);
            result["estado"] = Values(states);
        }
        return result;
    }

    private sealed class Row
    {
        public string? Line { get; set; }
        public int? LineId { get; set; }
        public string? Type { get; set; }
        public string? Repairer { get; set; }
        public Guid? Person { get; set; }
        public string? Work { get; set; }
        public string? Piece { get; set; }
        public string? Factory { get; set; }
        public string? Lot { get; set; }
        public int? State { get; set; }
        public bool InWarehouse { get; set; }
        public int? Location { get; set; }
        public string? LoanFactory { get; set; }
    }
}

using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Implementação só de leitura da descoberta de candidatos de Tool para
/// seleção humana explícita. Aplica os filtros explícitos no servidor
/// (regra de queries cirúrgicas: nunca carrega o registo completo para o
/// cliente filtrar) com projeção mínima dos factos persistidos,
/// AsNoTracking e ordenação determinística pelos FATOS HUMANOS
/// (Type → Reference → Lot; o tool_id — id opaco — é apenas o
/// desempate final da determinização, nunca a chave de leitura). A linha
/// DUMMY (REMOVED) é SEMPRE excluída por constante: a tombstone nunca é
/// candidata à seleção. Não cria Tools, não muta estado, não deriva
/// identidade de referência/lote/tipo e nunca auto-seleciona: os filtros
/// (incluindo a máquina e a referência MF associada) apenas reduzem os
/// candidatos devolvidos.
/// </summary>
public sealed class ToolCandidateQuery : IToolCandidateQuery
{
    private readonly DmoDbContext _db;

    public ToolCandidateQuery(DmoDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<ToolCandidate>> FindCandidatesAsync(
        ToolCandidateFilter? filter, CancellationToken cancellationToken = default) =>
        ReadCandidatesAsync(OrderCandidates(ApplyFilters(filter), filter), cancellationToken);

    public async Task<ToolCandidatePage> FindPageAsync(
        ToolCandidateFilter? filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = pageSize == 20 ? 20 : 10;
        var query = ApplyFilters(filter);
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        pageNumber = Math.Clamp(pageNumber, 1, pages);
        var candidates = await ReadCandidatesAsync(OrderCandidates(query, filter)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize), cancellationToken);
        return new ToolCandidatePage(candidates, total, pageNumber, pageSize);
    }

    private IOrderedQueryable<Tool> OrderCandidates(IQueryable<Tool> query, ToolCandidateFilter? filter)
    {
        // No Job On, as mais próximas primeiro (Diogo, 26/10/2026): as já usadas
        // com as ferramentas escolhidas, depois as usadas nesta referência, depois
        // as ligadas a esta linha; o resto no fim.
        if (filter is { } f && (f.CompanheiraToolIds is { Count: > 0 } || !string.IsNullOrWhiteSpace(f.ReferenciaTotal)))
        {
            var com = UsadasCom(f.CompanheiraToolIds);
            var naReferencia = UsadasNaReferencia(f.ReferenciaTotal);
            var machineId = f.MachineId ?? 0;
            query = query.OrderBy(tool => com.Contains(tool.ToolId) ? 0
                : naReferencia.Contains(tool.ToolId) ? 1
                : _db.ToolMachineLinks.Any(link => link.ToolId == tool.ToolId && link.MachineId == machineId) ? 2
                : 3);
            return ((IOrderedQueryable<Tool>)query)
                .ThenBy(tool => tool.Type).ThenBy(tool => tool.Reference).ThenBy(tool => tool.Lot).ThenBy(tool => tool.ToolId);
        }
        return query
            .OrderBy(tool => tool.Type).ThenBy(tool => tool.Reference).ThenBy(tool => tool.Lot).ThenBy(tool => tool.ToolId);
    }

    /// <summary>
    /// As ferramentas que estiveram num Job On com alguma destas (em uso ou já
    /// trocadas); vazio sem ferramentas escolhidas.
    /// </summary>
    private IQueryable<string> UsadasCom(IReadOnlyCollection<string>? toolIds)
    {
        var ids = toolIds ?? Array.Empty<string>();
        var jobs = _db.CmContexts.Where(c => ids.Contains(c.ToolId)).Select(c => c.JobOnId ?? c.ReplacedJobOnId)
            .Concat(_db.MfContexts.Where(c => ids.Contains(c.ToolId)).Select(c => c.JobOnId ?? c.ReplacedJobOnId))
            .Concat(_db.BqContexts.Where(c => ids.Contains(c.ToolId)).Select(c => c.JobOnId ?? c.ReplacedJobOnId));
        return _db.CmContexts.Where(c => jobs.Contains(c.JobOnId ?? c.ReplacedJobOnId)).Select(c => c.ToolId)
            .Concat(_db.MfContexts.Where(c => jobs.Contains(c.JobOnId ?? c.ReplacedJobOnId)).Select(c => c.ToolId))
            .Concat(_db.BqContexts.Where(c => jobs.Contains(c.JobOnId ?? c.ReplacedJobOnId)).Select(c => c.ToolId));
    }

    /// <summary>As ferramentas usadas num Job On desta referência total; vazio sem referência.</summary>
    private IQueryable<string> UsadasNaReferencia(string? referenciaTotal)
    {
        var reference = referenciaTotal?.Trim().ToUpperInvariant() ?? string.Empty;
        return _db.CmContexts.Where(c => reference != string.Empty && (c.JobOn != null ? c.JobOn.Reference : c.ReplacedJobOn!.Reference).ToUpper() == reference).Select(c => c.ToolId)
            .Concat(_db.MfContexts.Where(c => reference != string.Empty && (c.JobOn != null ? c.JobOn.Reference : c.ReplacedJobOn!.Reference).ToUpper() == reference).Select(c => c.ToolId))
            .Concat(_db.BqContexts.Where(c => reference != string.Empty && (c.JobOn != null ? c.JobOn.Reference : c.ReplacedJobOn!.Reference).ToUpper() == reference).Select(c => c.ToolId));
    }

    private IQueryable<Tool> ApplyFilters(ToolCandidateFilter? filter)
    {
        IQueryable<Tool> query = _db.Tools.AsNoTracking();

        // A DUMMY canónica nunca é candidata: excluída por CONSTANTE em
        // todas as queries de descoberta.
        query = query.Where(tool => tool.ToolId != ToolRegistryConstants.RemovedToolId);

        // Dimensões fechadas e explícitas, composta por E: cada dimensão
        // apenas reduz o conjunto. null/vazio = dimensão não aplicada, sem
        // exceção de argumento (estilo do IToolLookupQuery com valores
        // desconhecidos).
        if (filter is not null)
        {
            if (filter.ToolIds is { } toolIds)
            {
                query = query.Where(tool => toolIds.Contains(tool.ToolId));
            }

            if (filter.Type is { } type)
            {
                query = query.Where(tool => tool.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(filter.Reference))
            {
                // Igualdade exata ao valor persistido: sem trimming, sem
                // case-insensitivity, sem contains — nenhuma semântica
                // difusa inventada.
                query = query.Where(tool => tool.Reference == filter.Reference);
            }

            if (!string.IsNullOrWhiteSpace(filter.Lot))
            {
                query = query.Where(tool => tool.Lot == filter.Lot);
            }

            if (filter.MachineId is { } machineId)
            {
                // Compatibilidade de máquina (via ligações persistidas):
                // reduz candidatos, nunca determina identidade.
                query = filter.IncluirSemLinha
                    ? query.Where(tool =>
                        _db.ToolMachineLinks.Any(link =>
                            link.ToolId == tool.ToolId && link.MachineId == machineId)
                        || !_db.ToolMachineLinks.Any(link => link.ToolId == tool.ToolId))
                    : query.Where(tool =>
                        _db.ToolMachineLinks.Any(link =>
                            link.ToolId == tool.ToolId && link.MachineId == machineId));

                // A B2 em dupla ou em tripla (Diogo, 28/10/2026): na B2 só
                // aparecem as ferramentas do modo escolhido nas Definições
                // (e as que ainda não têm modo, com aviso).
                if (_db.Machines.Any(m => m.Id == machineId && m.Code == "B2"))
                {
                    var modo = Codificacao.ModoDe(_db.ModuleSettings
                        .Where(s => s.SettingKey == Codificacao.ModoB2Chave)
                        .Select(s => s.SettingValue)
                        .FirstOrDefault()) ?? B2Modo.Dupla;
                    query = query.Where(tool => tool.B2Modo == null || tool.B2Modo == modo);
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.MfReference))
            {
                // Referência MF/produção (worked example blueprint L70-93):
                // corresponde a Reference == valor OU à associação explícita
                // AssociatedMfReference == valor, QUALQUER tipo — a CM cuja
                // referência canónica difere da referência MF em que encaixa
                // continua candidata pela associação. A seleção permanece
                // humana explícita sobre o tool_id. Uma referência toda
                // (1075C142) também encontra a CM/MF registada antes só com o
                // modelo (1075), como a referência da produção (teste do
                // agente de 19/10/2026, T21ce22c).
                var (mfModel, _) = SplitReference(filter.MfReference.Trim().ToUpperInvariant());
                query = query.Where(tool =>
                    tool.Reference == filter.MfReference
                    || tool.AssociatedMfReference == filter.MfReference
                    || (mfModel != null && tool.Type != ToolType.Bq && (tool.Reference.ToUpper() == mfModel
                        || (tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper() == mfModel))));
            }
        }

        if (filter is { SoRelacionadas: true } relacionadas
            && (relacionadas.CompanheiraToolIds is { Count: > 0 } || !string.IsNullOrWhiteSpace(relacionadas.ReferenciaTotal) || relacionadas.MachineId is not null))
        {
            // Sem nada escrito, só as relacionadas: usadas com as escolhidas, usadas
            // nesta referência ou ligadas a esta linha (Diogo, 26/10/2026: «ainda
            // apareciam muitas»). Escrever volta a mostrar todas.
            var com = UsadasCom(relacionadas.CompanheiraToolIds);
            var naReferencia = UsadasNaReferencia(relacionadas.ReferenciaTotal);
            var linha = relacionadas.MachineId ?? 0;
            query = query.Where(tool => com.Contains(tool.ToolId)
                || naReferencia.Contains(tool.ToolId)
                || _db.ToolMachineLinks.Any(link => link.ToolId == tool.ToolId && link.MachineId == linha));
        }

        if (filter?.Estado is { } estado)
        {
            // O estado atual é o último registado (Armazém › Entrada).
            query = query.Where(tool => _db.FerramentaEstados.Any(e => e.ToolId == tool.ToolId && e.Estado == estado
                && !_db.FerramentaEstados.Any(o => o.ToolId == e.ToolId && o.Id > e.Id)));
        }

        if (filter?.Local is { } local)
        {
            // Numa posição do Armazém primeiro; senão, a localização dos
            // movimentos; sem movimentos é Fabrico (Diogo, 27/10/2026).
            var naPosicao = _db.ArmazemOcupacoes.Where(o => o.LibertadaEmUtc == null).Select(o => o.ToolId);
            if (local.Tipo == ToolCandidateLocalTipo.Armazem)
            {
                query = query.Where(tool => naPosicao.Contains(tool.ToolId));
            }
            else if (local.Tipo == ToolCandidateLocalTipo.Fabrico)
            {
                query = query.Where(tool => !naPosicao.Contains(tool.ToolId)
                    && !_db.FerramentaLocalizacoes.Any(l => l.ToolId == tool.ToolId && l.Localizacao != FerramentaLocalizacaoTipo.Fabrico));
            }
            else
            {
                var onde = local.Tipo switch
                {
                    ToolCandidateLocalTipo.Reparacao => FerramentaLocalizacaoTipo.AReparar,
                    ToolCandidateLocalTipo.Sucata => FerramentaLocalizacaoTipo.Sucata,
                    _ => FerramentaLocalizacaoTipo.Emprestado
                };
                var reparador = local.Reparador?.Trim().ToUpperInvariant();
                var fabrica = local.Fabrica?.Trim().ToUpperInvariant();
                query = query.Where(tool => !naPosicao.Contains(tool.ToolId)
                    && _db.FerramentaLocalizacoes.Any(l => l.ToolId == tool.ToolId && l.Localizacao == onde
                        && (reparador == null || (l.ReparadorNome != null && l.ReparadorNome.ToUpper() == reparador))
                        && (fabrica == null || _db.ArmazemMovimentos.Any(m => m.Id == l.MovimentoId
                            && m.FabricaDestino != null && m.FabricaDestino.ToUpper() == fabrica))));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter?.FabricaOrigem))
        {
            var fabrica = filter.FabricaOrigem.Trim().ToUpperInvariant();
            query = query.Where(tool => tool.FabricaOrigem != null && tool.FabricaOrigem.ToUpper().StartsWith(fabrica));
        }

        if (ReferenceMatch.Normalize(filter?.ReferenceContains) is { } part)
        {
            // O INÍCIO DA REFERÊNCIA OU DA PARTE BQ (Diogo, 18/10/2026: «Y2»
            // encontra a Y232 e a 6018Y232; «0» encontra a 0132T173, nunca a
            // 1075 só por ter um 0 no meio), ou o início da referência MF
            // associada. A regra da referência está em ReferenceMatch.
            // Uma referência toda (1075C142) também encontra as ferramentas
            // registadas só com uma parte: a CM/MF 1075 e a BQ C142.
            var (model, bqPart) = SplitReference(part);
            query = query.Where(ReferenceMatch.Predicate<Tool>(tool => tool.Reference, part)
                .Or(tool => tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper().StartsWith(part))
                .Or(tool => bqPart != null && tool.Type == ToolType.Bq && tool.Reference.ToUpper() == bqPart)
                .Or(tool => model != null && tool.Type != ToolType.Bq
                    && (tool.Reference.ToUpper() == model
                        || (tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper() == model))));
        }

        if (ReferenceMatch.Normalize(filter?.Search) is { } search)
        {
            // A referência pelo início dela ou da parte BQ (a mesma regra);
            // o lote e a máquina continuam a encontrar-se por parte do texto.
            query = query.Where(ReferenceMatch.Predicate<Tool>(tool => tool.Reference, search)
                .Or(tool => tool.Lot.ToUpper().Contains(search)
                    || _db.ToolMachineLinks.Any(link => link.ToolId == tool.ToolId
                        && link.Machine.Code.ToUpper().Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(filter?.ProductionReference))
        {
            var productionReference = filter.ProductionReference.Trim().ToUpperInvariant();
            var usedToolIds = _db.CmContexts.Where(c => (c.JobOn != null ? c.JobOn.Reference : c.ReplacedJobOn!.Reference).ToUpper() == productionReference).Select(c => c.ToolId)
                .Concat(_db.MfContexts.Where(c => (c.JobOn != null ? c.JobOn.Reference : c.ReplacedJobOn!.Reference).ToUpper() == productionReference).Select(c => c.ToolId))
                .Concat(_db.BqContexts.Where(c => (c.JobOn != null ? c.JobOn.Reference : c.ReplacedJobOn!.Reference).ToUpper() == productionReference).Select(c => c.ToolId));
            var knownTools = _db.Tools.Where(known => usedToolIds.Contains(known.ToolId) && known.ToolId != ToolRegistryConstants.RemovedToolId);
            // As ferramentas registadas antes da referência toda (Diogo,
            // 19/10/2026: «deveria ser sempre referência toda») têm só uma
            // parte: a BQ da 1075C142 é a C142 e o MF/CM é o 1075.
            var (model, bqPart) = SplitReference(productionReference);
            query = query.Where(tool =>
                tool.Reference.ToUpper() == productionReference
                || (tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper() == productionReference)
                || (bqPart != null && tool.Type == ToolType.Bq && tool.Reference.ToUpper() == bqPart)
                || (model != null && tool.Type != ToolType.Bq && (tool.Reference.ToUpper() == model
                    || (tool.AssociatedMfReference != null && tool.AssociatedMfReference.ToUpper() == model)))
                // Replacement lots of the same recorded design remain discoverable.
                || knownTools.Any(known => known.Type == tool.Type && known.Reference.ToUpper() == tool.Reference.ToUpper()));
        }

        return query;
    }

    /// <summary>A referência completa em modelo (algarismos) e parte BQ (a partir da letra): 1075C142 → (1075, C142).</summary>
    public static (string? Model, string? BqPart) SplitReference(string reference)
    {
        var letter = 0;
        while (letter < reference.Length && char.IsAsciiDigit(reference[letter])) letter++;
        return letter == 0 || letter == reference.Length || !char.IsLetter(reference[letter])
            ? (null, null)
            : (reference[..letter], reference[letter..]);
    }

    private async Task<IReadOnlyList<ToolCandidate>> ReadCandidatesAsync(
        IQueryable<Tool> query, CancellationToken cancellationToken)
    {
        // A QUANTIDADE e as MÁQUINAS (o Owner 2026-10-12: a escolha não
        // fica «tão bare»): a quantidade é coluna direta da Tool; as
        // máquinas do link n-n materializam-se em memória (o
        // string.Join é pós-materialização — o EF/Npgsql não o traduz;
        // o padrão agnóstico InMemory/Npgsql do bloco).
        var rows = await query
            .Select(tool => new
            {
                tool.ToolId,
                tool.Type,
                tool.Reference,
                tool.Lot,
                tool.BqAccountedQuantity,
                tool.AssociatedMfReference,
                Machines = _db.ToolMachineLinks
                    .Where(link => link.ToolId == tool.ToolId)
                    .Select(link => link.Machine.Code)
                    .OrderBy(code => code)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ToolCandidate(
                row.ToolId,
                row.Type,
                row.Reference,
                row.Lot,
                row.BqAccountedQuantity,
                row.Machines.Count > 0 ? string.Join(" · ", row.Machines) : null,
                row.AssociatedMfReference))
            .ToList();
    }
}

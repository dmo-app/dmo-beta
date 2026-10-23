using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Implementação de <see cref="IJobOnToolFactsQuery"/>: os fatos técnicos
/// Tool-owned (ToolTechnicalContext — o processo, os volumes, o gargalo e
/// a geometria TP de referência), as máquinas registadas na ficha da tool
/// (ToolMachineLinks — o mesmo conjunto que a Comparação_B usa para
/// decidir o que pode ser comparado) e os AVISOS INFORMATIVOS calculados
/// PELO BACKEND sobre os factos persistidos — nunca um bloqueio, nunca uma
/// inferência: a compatibilidade compara as máquinas REGISTADAS com a
/// máquina ESCOLHIDA na ficha; o uso paralelo procura as OUTRAS produções
/// cujo período (InicioEm..FimEm, o fim em aberto = ainda a correr) se
/// sobrepõe ao da produção em criação ou edição, excluída esta. A mesma
/// ferramenta pode servir outra produção, da mesma referência ou não; só não
/// pode trabalhar ao mesmo tempo (o proprietário, 17/10/2026). Sem início
/// de um dos lados não há datas a comparar: sem aviso. Na MESMA máquina duas
/// produções não trabalham ao mesmo tempo (uma segue a outra; a sobreposição
/// das datas tem o seu próprio aviso), por isso só contam as outras máquinas
/// (o Diogo, 17/10/2026: 202616 e 202618, ambas na C2). O dia da troca não
/// conta (o Diogo, 22/10/2026: «uma produção que vai entrar no mesmo dia que
/// a outra vai sair não pode causar erro»), a mesma regra de JobOnOverlap.Crosses.
/// </summary>
public sealed class JobOnToolFactsQuery : IJobOnToolFactsQuery
{
    private readonly DmoDbContext _db;

    public JobOnToolFactsQuery(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<JobOnToolFacts>> FindAsync(
        IReadOnlyList<string> toolIds,
        string? machineCode,
        int? excludingJobOnId,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        CancellationToken cancellationToken = default)
    {
        var ids = toolIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<JobOnToolFacts>();
        }

        var excluido = excludingJobOnId ?? 0;

        // Três consultas: as tools com os factos técnicos, as máquinas
        // registadas e o uso noutras produções ativas.
        var tools = await (
            from t in _db.Tools.AsNoTracking()
            where ids.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId
            join tc in _db.ToolTechnicalContexts.AsNoTracking() on t.ToolId equals tc.ToolId into tecnicos
            from tc in tecnicos.DefaultIfEmpty()
            select new
            {
                t.ToolId,
                t.Type,
                t.Reference,
                t.Lot,
                t.BqAccountedQuantity,
                Process = tc != null ? tc.Process : null,
                VolumePuncaoCm3 = tc != null ? tc.VolumePuncaoCm3 : null,
                VolumeMarisaCm3 = tc != null ? tc.VolumeMarisaCm3 : null,
                DiametroGargaloMm = tc != null ? tc.DiametroGargaloMm : null,
                TpDiametroMm = tc != null ? tc.TpDiametroMm : null,
                TpRaioMm = tc != null ? tc.TpRaioMm : null,
                TpSagittaMm = tc != null ? tc.TpSagittaMm : null,
                Posicao = (from o in _db.ArmazemOcupacoes
                           join p in _db.ArmazemPosicoes on o.PosicaoId equals p.Id
                           where o.ToolId == t.ToolId && o.LibertadaEmUtc == null
                           select p.Codigo).FirstOrDefault(),
                Localizacao = _db.FerramentaLocalizacoes.Where(l => l.ToolId == t.ToolId)
                    .Select(l => new { l.Localizacao, l.ReparadorNome }).FirstOrDefault(),
                Registada = _db.ArmazemMovimentos.Any(m => m.ToolId == t.ToolId),
                Estado = _db.FerramentaEstados.Where(e => e.ToolId == t.ToolId)
                    .OrderByDescending(e => e.Id).Select(e => (FerramentaEstadoTipo?)e.Estado).FirstOrDefault(),
                Utilizacao = _db.FerramentaUtilizacoes.Where(u => u.ToolId == t.ToolId)
                    .OrderByDescending(u => u.Id).Select(u => (decimal?)u.Percentagem).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var maquinasPorTool = (await _db.ToolMachineLinks
                .AsNoTracking()
                .Where(link => ids.Contains(link.ToolId))
                .Select(link => new { link.ToolId, link.Machine.Code })
                .ToListAsync(cancellationToken))
            .GroupBy(link => link.ToolId)
            .ToDictionary(group => group.Key, group => group.Select(link => link.Code).ToList());

        // O USO PARALELO: as OUTRAS produções com a ferramenta em qualquer
        // um dos contextos e um período que se sobrepõe ao desta.
        // Só o cm_id, o mf_id e o bq_id em uso de cada produção (os de CMs,
        // MFs e BQs trocados já não contam).
        var cm = _db.CmContexts.AsNoTracking()
            .Where(c => ids.Contains(c.ToolId) && c.JobOnId != null)
            .Select(c => new { c.ToolId, JobOnId = c.JobOnId!.Value, c.JobOn!.ProductionNumber, Machine = c.JobOn.Machine.Code, c.JobOn.InicioEm, c.JobOn.FimEm });
        var mf = _db.MfContexts.AsNoTracking()
            .Where(c => ids.Contains(c.ToolId) && c.JobOnId != null)
            .Select(c => new { c.ToolId, JobOnId = c.JobOnId!.Value, c.JobOn!.ProductionNumber, Machine = c.JobOn.Machine.Code, c.JobOn.InicioEm, c.JobOn.FimEm });
        var bq = _db.BqContexts.AsNoTracking()
            .Where(c => ids.Contains(c.ToolId) && c.JobOnId != null)
            .Select(c => new { c.ToolId, JobOnId = c.JobOnId!.Value, c.JobOn!.ProductionNumber, Machine = c.JobOn.Machine.Code, c.JobOn.InicioEm, c.JobOn.FimEm });
        var inicio = inicioEm ?? DateOnly.MaxValue;
        var usos = inicioEm is null
            ? []
            : await cm.Concat(mf).Concat(bq)
                .Where(u => u.JobOnId != excluido
                    && (machineCode == null || u.Machine != machineCode)
                    && u.InicioEm != null
                    && (fimEm == null || u.InicioEm < fimEm)
                    && (u.FimEm == null || u.FimEm > inicio))
                .Select(u => new UsoParalelo(u.ToolId, u.JobOnId, u.ProductionNumber, u.Machine))
                .ToListAsync(cancellationToken);
        var usosPorTool = usos
            .GroupBy(uso => uso.ToolId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(uso => new JobOnToolUsoParalelo(uso.JobOnId, uso.ProductionNumber, uso.Machine))
                    .Distinct()
                    .OrderBy(uso => $"{uso.ProductionNumber} ({uso.Machine})", StringComparer.Ordinal)
                    .ThenBy(uso => uso.JobOnId)
                    .ToList());

        // Localização/estado/uso vêm na projeção das ferramentas: só o último
        // estado e a última leitura, sem transferir o histórico inteiro.
        var result = new List<JobOnToolFacts>(tools.Count);
        foreach (var tool in tools)
        {
            maquinasPorTool.TryGetValue(tool.ToolId, out var maquinas);
            var maquinasRegistradas = maquinas ?? new List<string>();

            // O AVISO DE COMPATIBILIDADE (informativo, nunca um bloqueio):
            // a tool TEM máquinas registadas e a máquina ESCOLHIDA na ficha
            // não está entre elas — «confirme a máquina escolhida». Sem
            // máquinas registadas não há informação de compatibilidade:
            // sem aviso (o padrão da casa — função antes de proteção).
            string? avisoCompatibilidade = null;
            if (maquinasRegistradas.Count > 0
                && !string.IsNullOrEmpty(machineCode)
                && !maquinasRegistradas.Contains(machineCode, StringComparer.Ordinal))
            {
                avisoCompatibilidade =
                    $"{tool.Type.ToString().ToUpperInvariant()} {tool.Reference}: compatibilidade registada " +
                    $"{string.Join(", ", maquinasRegistradas)}; confirme a máquina escolhida.";
            }

            // O AVISO DE USO PARALELO (informativo, nunca um bloqueio):
            // outra produção ao mesmo tempo com a mesma ferramenta.
            usosPorTool.TryGetValue(tool.ToolId, out var usosDaTool);
            string? avisoUsoParalelo = null;
            if (usosDaTool is { Count: > 0 })
            {
                avisoUsoParalelo =
                    $"{tool.Type.ToString().ToUpperInvariant()} {tool.Reference} / lote {tool.Lot} também está associado a " +
                    $"{string.Join(", ", usosDaTool.Select(uso => $"{uso.ProductionNumber} ({uso.Machine})").Distinct())}. Esta informação não impede a sua escolha.";
            }

            var nome = $"{tool.Type.ToString().ToUpperInvariant()} {tool.Reference} / lote {tool.Lot}";
            var registada = tool.Registada;
            var temEstado = tool.Estado is not null;
            string? ondeEsta = tool.Posicao is { } posicao
                ? $"Armazém · posição {posicao}"
                : tool.Localizacao is { } localizacao
                    ? DMO.Alpha.Core.Armazem.ArmazemTextos.Localizacao(localizacao.Localizacao, localizacao.ReparadorNome)
                    : registada ? "Fabrico" : null;
            var avisoArmazem = (registada, temEstado) switch
            {
                (false, false) => $"{nome} não está registada no Armazém e não tem estado.",
                (false, true) => $"{nome} não está registada no Armazém.",
                (true, false) => $"{nome} não tem estado registado.",
                _ => null
            };

            result.Add(new JobOnToolFacts(
                tool.ToolId,
                tool.Type.ToString().ToUpperInvariant(),
                tool.Reference,
                tool.Lot,
                Processo: tool.Process is { } process
                    ? (process == CmProcess.Nnpb ? "NNPB" : "PS")
                    : null,
                VolumePuncaoCm3: tool.VolumePuncaoCm3,
                VolumeMarisaCm3: tool.VolumeMarisaCm3,
                DiametroGargaloMm: tool.DiametroGargaloMm,
                Quantidade: tool.BqAccountedQuantity,
                TpDiametroMm: tool.TpDiametroMm,
                TpRaioMm: tool.TpRaioMm,
                TpSagittaMm: tool.TpSagittaMm,
                maquinasRegistradas,
                avisoCompatibilidade,
                avisoUsoParalelo)
            {
                UsosParalelos = usosDaTool ?? new List<JobOnToolUsoParalelo>(),
                OndeEsta = ondeEsta,
                Estado = tool.Estado,
                Utilizacao = tool.Utilizacao,
                AvisoArmazem = avisoArmazem
            });
        }

        return result;
    }

    private sealed record UsoParalelo(string ToolId, int JobOnId, string ProductionNumber, string Machine);
}

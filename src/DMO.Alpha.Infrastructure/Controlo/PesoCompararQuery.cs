using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// O «Comparar peso» (Diogo, 27/10/2026: «ao escolher Comparar peso faz slide
/// da janela para escolher CM compatíveis: mesma referência e máquina»): os
/// Pesos da mesma referência de CM e da mesma máquina, o mais recente de cada
/// CM. A máquina de um Peso é a do Job On; sem Job On, as da ficha da CM.
/// Só leitura.
/// </summary>
public sealed class PesoCompararQuery : IPesoCompararQuery
{
    /// <summary>As referências sugeridas e os Pesos lidos de uma referência, no máximo.</summary>
    public const int MaxReferencias = 20;
    public const int MaxPesos = 500;

    private readonly DmoDbContext _db;
    private readonly IPesoQuery _pesoQuery;

    public PesoCompararQuery(DmoDbContext db, IPesoQuery pesoQuery)
    {
        _db = db;
        _pesoQuery = pesoQuery;
    }

    public async Task<PesoCompararCms> ListarAsync(PesoCompararPedido pedido, CancellationToken cancellationToken = default)
    {
        int? pesoBaseId = null;
        string? referenciaBase = null;
        string? maquinaBase = null;
        if (pedido.PesoId is int pesoId)
        {
            var base_ = await _db.Pesos.AsNoTracking()
                .Where(p => p.Id == pesoId)
                .Select(p => new
                {
                    p.Id,
                    Referencia = p.CmToolReferenceFrozen ?? p.CmTool.Reference,
                    Maquina = p.Controlo == null
                        ? null
                        : _db.Machines.Where(m => m.Id == p.Controlo.JobOn.MachineId).Select(m => m.Code).FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (base_ is not null)
            {
                pesoBaseId = base_.Id;
                referenciaBase = base_.Referencia;
                maquinaBase = base_.Maquina;
            }
        }

        var escrita = Limpo(pedido.CmReferencia) ?? referenciaBase?.ToUpperInvariant();
        if (escrita is null)
        {
            return new PesoCompararCms(pesoBaseId, null, null, [], [], []);
        }

        // A referência escrita no Histórico procura pelo início (a regra única
        // de 18/10/2026); com uma só, ou com uma igual ao texto, fica escolhida.
        var referencias = await _db.Pesos.AsNoTracking()
            .Select(p => p.CmToolReferenceFrozen ?? p.CmTool.Reference)
            .Where(r => r != null && r.ToUpper().StartsWith(escrita))
            .Distinct()
            .OrderBy(r => r)
            .Take(MaxReferencias)
            .ToListAsync(cancellationToken);
        var referencia = referencias.FirstOrDefault(r => string.Equals(r, escrita, StringComparison.OrdinalIgnoreCase))
            ?? (referencias.Count == 1 ? referencias[0] : null);
        if (referencia is null)
        {
            return new PesoCompararCms(pesoBaseId, null, null, referencias, [], []);
        }

        var referenciaMaiuscula = referencia.ToUpperInvariant();
        var pesos = await _db.Pesos.AsNoTracking()
            .Where(p => (p.CmToolReferenceFrozen ?? p.CmTool.Reference).ToUpper() == referenciaMaiuscula)
            .OrderByDescending(p => p.RegistadoEmUtc)
            .ThenByDescending(p => p.Id)
            .Take(MaxPesos)
            .Select(p => new
            {
                p.Id,
                p.CmToolId,
                CmReferencia = p.CmToolReferenceFrozen ?? p.CmTool.Reference,
                CmLote = p.CmToolLotFrozen ?? p.CmTool.Lot,
                Referencia = p.Controlo == null ? null : p.Controlo.JobOn.Reference,
                Producao = p.Controlo == null ? null : p.Controlo.JobOn.ProductionNumber,
                Maquina = p.Controlo == null
                    ? null
                    : _db.Machines.Where(m => m.Id == p.Controlo.JobOn.MachineId).Select(m => m.Code).FirstOrDefault(),
                p.RegistadoEmUtc,
                p.GlassDensityGramsPerCm3,
                p.TampaoDiametroMm,
                p.TampaoCaloteMm,
                JobOnDiametro = p.Controlo == null ? null : p.Controlo.JobOn.TampaoDiametroMm,
                JobOnCalote = p.Controlo == null ? null : p.Controlo.JobOn.CaloteMm,
                FichaDiametro = _db.ToolTechnicalContexts.Where(t => t.ToolId == p.CmToolId).Select(t => t.TpDiametroMm).FirstOrDefault(),
                FichaCalote = _db.ToolTechnicalContexts.Where(t => t.ToolId == p.CmToolId).Select(t => t.TpSagittaMm).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Um Peso sem Job On não tem máquina: valem as da ficha da CM.
        var semMaquina = pesos.Where(p => p.Maquina is null).Select(p => p.CmToolId).Distinct().ToList();
        var maquinasDaFicha = (await _db.ToolMachineLinks.AsNoTracking()
                .Where(l => semMaquina.Contains(l.ToolId))
                .Join(_db.Machines, l => l.MachineId, m => m.Id, (l, m) => new { l.ToolId, m.Code })
                .ToListAsync(cancellationToken))
            .GroupBy(l => l.ToolId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Code).ToHashSet(StringComparer.OrdinalIgnoreCase));
        IReadOnlySet<string> MaquinasDe(string? maquina, string cmToolId) =>
            maquina is not null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { maquina }
                : maquinasDaFicha.GetValueOrDefault(cmToolId) ?? (IReadOnlySet<string>)new HashSet<string>();

        var maquinas = pesos
            .SelectMany(p => MaquinasDe(p.Maquina, p.CmToolId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToList();
        // Compara-se sempre numa máquina: a pedida, a do Peso de onde se parte
        // ou, no Histórico, a do Peso mais recente da referência.
        var maquina = (Limpo(pedido.Maquina) is { } pedida
                ? maquinas.FirstOrDefault(m => string.Equals(m, pedida, StringComparison.OrdinalIgnoreCase))
                : null)
            ?? maquinaBase
            ?? pesos.Select(p => MaquinasDe(p.Maquina, p.CmToolId).Order(StringComparer.Ordinal).FirstOrDefault()).FirstOrDefault(m => m is not null);

        // O mais recente de cada CM; a CM do Peso de onde se parte fica com ele.
        var escolhidos = pesos
            .Where(p => maquina is null || MaquinasDe(p.Maquina, p.CmToolId).Contains(maquina) || p.Id == pesoBaseId)
            .GroupBy(p => p.CmToolId)
            .Select(g => g.FirstOrDefault(p => p.Id == pesoBaseId) ?? g.First())
            .OrderBy(p => p.Id != pesoBaseId)
            .ThenByDescending(p => p.RegistadoEmUtc)
            .ThenByDescending(p => p.Id)
            .ToList();

        var linhas = new List<PesoCompararLinha>(escolhidos.Count);
        foreach (var p in escolhidos)
        {
            var lido = await _pesoQuery.FindPesoAsync(p.Id, cancellationToken);
            var diametro = p.TampaoDiametroMm ?? p.JobOnDiametro ?? p.FichaDiametro;
            var calote = p.TampaoCaloteMm ?? p.JobOnCalote ?? p.FichaCalote;
            linhas.Add(new PesoCompararLinha(
                p.Id,
                p.CmToolId,
                p.CmReferencia,
                p.CmLote,
                p.Referencia,
                p.Producao,
                p.Maquina ?? (MaquinasDe(null, p.CmToolId) is { Count: > 0 } daFicha
                    ? string.Join(" ", daFicha.OrderBy(m => m, StringComparer.Ordinal))
                    : null),
                p.RegistadoEmUtc is { } em ? DateTime.SpecifyKind(em, DateTimeKind.Utc) : null,
                lido?.Measurements.Count ?? 0,
                lido?.Averages?.AverageGlassWeightGrams,
                p.GlassDensityGramsPerCm3,
                diametro is > 0m && calote is >= 0m ? new PesoTampao(diametro.Value, calote.Value) : null));
        }

        return new PesoCompararCms(pesoBaseId, referencia, maquina, referencias, maquinas, linhas);
    }

    private static string? Limpo(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim().ToUpperInvariant();
}

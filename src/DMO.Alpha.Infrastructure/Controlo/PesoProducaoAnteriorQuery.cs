using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Lê e grava a produção anterior de um Peso (o Owner 2026-10-17). As
/// opções são as produções da mesma referência anteriores à do Peso, as
/// mais recentes primeiro e no máximo as últimas 5; o peso é o do SAP,
/// escrito à mão.
/// </summary>
public sealed class PesoProducaoAnteriorQuery
{
    private readonly DmoDbContext _db;

    public PesoProducaoAnteriorQuery(DmoDbContext db) => _db = db;

    /// <summary>A produção anterior guardada no Peso; null se o Peso não existe.</summary>
    public async Task<PesoProducaoAnteriorReadModel?> FindAsync(int pesoId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Pesos.AsNoTracking()
            .Where(p => p.Id == pesoId)
            .Select(p => new
            {
                p.ProducaoAnteriorControloId,
                JobOnId = p.ProducaoAnteriorControlo != null ? (int?)p.ProducaoAnteriorControlo.JobOnId : null,
                p.ProducaoAnteriorPesoGramas,
                Reference = p.ProducaoAnteriorControlo != null ? p.ProducaoAnteriorControlo.JobOn.Reference : null,
                ProductionNumber = p.ProducaoAnteriorControlo != null ? p.ProducaoAnteriorControlo.JobOn.ProductionNumber : null,
                Machine = p.ProducaoAnteriorControlo != null ? p.ProducaoAnteriorControlo.JobOn.Machine.Code : null,
                InicioEm = p.ProducaoAnteriorControlo != null ? p.ProducaoAnteriorControlo.JobOn.InicioEm : null,
                FimEm = p.ProducaoAnteriorControlo != null ? p.ProducaoAnteriorControlo.JobOn.FimEm : null
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        return new PesoProducaoAnteriorReadModel(
            row.JobOnId, row.Reference, row.ProductionNumber, row.Machine,
            row.InicioEm, row.FimEm, row.ProducaoAnteriorPesoGramas)
        {
            ControloId = row.ProducaoAnteriorControloId
        };
    }

    /// <summary>
    /// As produções da mesma referência anteriores ao Job On atual, da mais
    /// recente para a mais antiga, no máximo
    /// <see cref="IProductionPickerQuery.MaxResults"/>: as que começaram antes
    /// dele (no mesmo dia, as registadas antes). Sem início no atual, as
    /// registadas antes dele, primeiro as que têm início.
    /// </summary>
    public async Task<IReadOnlyList<PesoProducaoAnteriorOpcao>> ListOpcoesAsync(
        int currentJobOnId,
        CancellationToken cancellationToken = default)
    {
        var current = await _db.JobOns.AsNoTracking()
            .Where(j => j.Id == currentJobOnId)
            .Select(j => new { j.Reference, j.InicioEm })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return Array.Empty<PesoProducaoAnteriorOpcao>();
        }

        // Só as anteriores, as últimas 5 (deep sweep 2, 18/10/2026, F-11:
        // vinham até 50, e numa produção de 2021 vinha escolhida uma de 2026
        // ou uma sem datas), pela regra única de «anterior»
        // (ProducaoAnterior). A máquina não conta aqui.
        var reference = current.Reference;
        var rows = await _db.JobOns.AsNoTracking()
            .Where(j => j.Reference == reference && j.Id != currentJobOnId)
            .SoAnteriores(j => j, currentJobOnId, current.InicioEm)
            .MaisRecentePrimeiro(j => j)
            .Take(IProductionPickerQuery.MaxResults)
            .Select(j => new { j.Id, j.ProductionNumber, Machine = j.Machine.Code, j.InicioEm, j.FimEm })
            .ToListAsync(cancellationToken);
        return rows
            .Select(j => new PesoProducaoAnteriorOpcao(j.Id, j.ProductionNumber, j.Machine, j.InicioEm, j.FimEm))
            .ToList();
    }

    /// <summary>
    /// Grava o controlo da produção anterior escolhida e o peso do SAP. Um
    /// peso vazio apaga o valor; um peso não positivo é recusado. O controlo
    /// vem do EnsureControloForJobOn (o get-or-create do Job On escolhido).
    /// </summary>
    public async Task<GuardarProducaoAnteriorResult> SaveAsync(
        int pesoId,
        int? controloId,
        decimal? pesoGramas,
        CancellationToken cancellationToken = default)
    {
        if (pesoGramas is <= 0m)
        {
            return GuardarProducaoAnteriorResult.PesoInvalido;
        }

        var peso = await _db.Pesos.FirstOrDefaultAsync(p => p.Id == pesoId, cancellationToken);
        if (peso is null)
        {
            return GuardarProducaoAnteriorResult.PesoNotFound;
        }

        if (controloId is int id && !await _db.Controlos.AnyAsync(c => c.Id == id, cancellationToken))
        {
            return GuardarProducaoAnteriorResult.JobOnNotFound;
        }

        peso.ProducaoAnteriorControloId = controloId;
        peso.ProducaoAnteriorPesoGramas = pesoGramas;
        await _db.SaveChangesAsync(cancellationToken);
        return GuardarProducaoAnteriorResult.Success;
    }
}

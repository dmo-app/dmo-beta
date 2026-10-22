using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// O aviso de sobreposição de uma produção: lê na base só as outras
/// produções da mesma máquina cuja janela pode cruzar a dela (os mesmos
/// limites da <see cref="JobOnOverlap"/>, em SQL), em vez de todas as
/// produções da máquina. A calculadora decide e escreve o aviso.
/// </summary>
public static class JobOnOverlapQuery
{
    public static async Task<OverlapWarning?> EvaluateAsync(
        DmoDbContext db,
        int jobOnId,
        int machineId,
        string machineCode,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        CancellationToken cancellationToken)
    {
        // A janela inteiramente em aberto não afirma período nenhum.
        if (inicioEm is null && fimEm is null)
        {
            return null;
        }

        var candidates = db.JobOns
            .AsNoTracking()
            .Where(j => j.MachineId == machineId
                && j.Id != jobOnId
                && (j.InicioEm != null || j.FimEm != null));
        if (inicioEm is { } inicio)
        {
            candidates = candidates.Where(j => j.FimEm == null || inicio < j.FimEm);
        }

        if (fimEm is { } fim)
        {
            candidates = candidates.Where(j => j.InicioEm == null || j.InicioEm < fim);
        }

        var others = await candidates
            .OrderBy(j => j.Id)
            .Select(j => new OverlapCandidate(j.Id, j.Reference, j.ProductionNumber, j.InicioEm, j.FimEm))
            .ToListAsync(cancellationToken);
        return JobOnOverlap.Evaluate(inicioEm, fimEm, machineCode, others);
    }
}

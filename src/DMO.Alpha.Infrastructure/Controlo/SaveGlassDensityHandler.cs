using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Grava a densidade do vidro NNPB e PS na linha de cada processo (cria-a na
/// primeira gravação, atualiza-a depois), as duas no mesmo SaveChanges. Os
/// Pesos já criados mantêm a densidade congelada. Fora do habitual grava na
/// mesma (ver <see cref="SaveGlassDensityResult"/>).
/// </summary>
public sealed class SaveGlassDensityHandler(DmoDbContext db) : ISaveGlassDensityHandler
{
    public async Task<SaveGlassDensityResult> SaveAsync(
        decimal nnpbGramsPerCm3,
        decimal psGramsPerCm3,
        CancellationToken cancellationToken = default)
    {
        var pedidas = new[] { (CmProcess.Nnpb, nnpbGramsPerCm3), (CmProcess.Ps, psGramsPerCm3) };
        if (pedidas.Any(p => GlassDensityLimits.Normalize(p.Item2) <= 0m))
        {
            return SaveGlassDensityResult.NotPositive;
        }

        var existentes = await db.GlassDensities.ToListAsync(cancellationToken);
        var arredondadas = new List<CmProcess>();
        var foraDoHabitual = new List<CmProcess>();
        foreach (var (process, pedida) in pedidas)
        {
            var gravada = GlassDensityLimits.Normalize(pedida);
            if (gravada != pedida)
            {
                arredondadas.Add(process);
            }

            if (!GlassDensityLimits.IsUsual(gravada))
            {
                foraDoHabitual.Add(process);
            }

            if (existentes.FirstOrDefault(g => g.Process == process) is { } linha)
            {
                linha.DensityGramsPerCm3 = gravada;
            }
            else
            {
                db.GlassDensities.Add(new GlassDensity { Process = process, DensityGramsPerCm3 = gravada });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SaveGlassDensityResult(true, arredondadas, foraDoHabitual);
    }
}

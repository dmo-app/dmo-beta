using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Corrige ou apaga uma leitura do Peso (Diogo, 27/10/2026; Decisão 001), como
/// o <see cref="CorrectPegamentosMeasurementHandler"/>: cada correção e cada
/// leitura apagada deixam uma linha de rasto (antes, depois, quem, quando) na
/// mesma gravação. O volume e o peso continuam a ser calculados à leitura,
/// com os valores congelados no Peso. Apagar renumera as posições (1..n),
/// pela ordem de registo, para a comparação pela posição continuar seguida.
/// </summary>
public sealed class CorrectPesoMeasurementHandler : ICorrectPesoMeasurementHandler
{
    private readonly DmoDbContext _db;

    public CorrectPesoMeasurementHandler(DmoDbContext db)
    {
        _db = db;
    }

    public Task<CorrectPesoMeasurementResult> EditAsync(
        EditPesoMeasurement command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, () => EditOnceAsync(command, cancellationToken), cancellationToken);

    // Duas correções da mesma leitura ao mesmo tempo: com o trinco do Peso, a
    // segunda espera e lê o valor que a primeira gravou.
    private async Task<CorrectPesoMeasurementResult> EditOnceAsync(
        EditPesoMeasurement command,
        CancellationToken cancellationToken)
    {
        await LockAsync(command.Anchor.PesoId, cancellationToken);
        var measurement = await FindAsync(command.Anchor, cancellationToken);
        if (measurement is null)
        {
            return CorrectPesoMeasurementResult.MeasurementNotFound;
        }

        // A massa que daria capacidade ou peso do vidro 0 ou menos é recusada,
        // como no registo.
        var peso = measurement.Peso;
        if (WaterDensityTable.ForPeso(peso.WaterDensityGramsPerCm3, peso.WaterTemperatureC) is { } density)
        {
            var capacity = PesoCalculation.CapacityCm3(command.WaterWeightGrams, density);
            if (capacity <= 0m
                || PesoCalculation.GlassWeightGrams(capacity, peso.VolumeMarisaCm3, peso.VolumePuncaoCm3, peso.GlassDensityGramsPerCm3) <= 0m)
            {
                return CorrectPesoMeasurementResult.ResultNonPositive;
            }
        }

        var cmSubject = command.CmSubject?.Trim() ?? string.Empty;

        // Guardar os mesmos valores não é uma correção: nem rasto.
        if (measurement.CmSubject == cmSubject && measurement.WaterWeightGrams == command.WaterWeightGrams)
        {
            return CorrectPesoMeasurementResult.Success;
        }

        var rasto = Rasto(measurement, apagada: false);
        measurement.CmSubject = cmSubject;
        measurement.WaterWeightGrams = command.WaterWeightGrams;
        rasto.CmSubjectDepois = measurement.CmSubject;
        rasto.WaterWeightDepoisGrams = measurement.WaterWeightGrams;
        _db.PesoMeasurementHistorico.Add(rasto);

        await _db.SaveChangesAsync(cancellationToken);
        return CorrectPesoMeasurementResult.Success;
    }

    public Task<CorrectPesoMeasurementResult> DeleteAsync(
        PesoMeasurementAnchor anchor,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, () => DeleteOnceAsync(anchor, cancellationToken), cancellationToken);

    private async Task<CorrectPesoMeasurementResult> DeleteOnceAsync(
        PesoMeasurementAnchor anchor,
        CancellationToken cancellationToken)
    {
        await LockAsync(anchor.PesoId, cancellationToken);
        var measurement = await FindAsync(anchor, cancellationToken);
        if (measurement is null)
        {
            return CorrectPesoMeasurementResult.MeasurementNotFound;
        }

        var remaining = await _db.PesoMeasurements
            .Where(m => m.PesoId == measurement.PesoId && m.Id != measurement.Id)
            .OrderBy(m => m.RowPosition)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        // A leitura sai da tabela; o rasto guarda o que tinha.
        _db.PesoMeasurementHistorico.Add(Rasto(measurement, apagada: true));
        _db.PesoMeasurements.Remove(measurement);
        await _db.SaveChangesAsync(cancellationToken);

        // As posições seguintes descem uma, depois de a apagada sair (o índice
        // único por Peso e posição não deixa duas na mesma).
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].RowPosition != i + 1)
            {
                remaining[i].RowPosition = i + 1;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return CorrectPesoMeasurementResult.Success;
    }

    private Task LockAsync(int pesoId, CancellationToken cancellationToken) =>
        DbWrite.LockAsync(_db, $"peso:registo:{pesoId}", cancellationToken);

    // O rasto com os valores da leitura antes da correção; quem corrigiu é
    // carimbado ao gravar (RegistoCarimboInterceptor).
    private static PesoMeasurementHistorico Rasto(PesoMeasurement measurement, bool apagada) => new()
    {
        PesoId = measurement.PesoId,
        MeasurementId = measurement.Id,
        Numero = measurement.RowPosition,
        Apagada = apagada,
        CmSubjectAntes = measurement.CmSubject,
        WaterWeightAntesGrams = measurement.WaterWeightGrams,
        EmUtc = DateTime.UtcNow
    };

    private Task<PesoMeasurement?> FindAsync(PesoMeasurementAnchor anchor, CancellationToken cancellationToken) =>
        _db.PesoMeasurements
            .Include(m => m.Peso)
            .FirstOrDefaultAsync(m => m.Id == anchor.MeasurementId && m.PesoId == anchor.PesoId, cancellationToken);
}

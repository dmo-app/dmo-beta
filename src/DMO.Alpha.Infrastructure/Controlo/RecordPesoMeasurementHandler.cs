using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Regista uma leitura física real (measurement row) num peso_id existente.
/// A leitura é acrescentada ao Peso como facto histórico — a massa de água
/// observada é registada na totalidade, nunca truncada, compensada ou
/// reescrita — e o registo não altera o Peso, as Tools nem qualquer contexto
/// de produção (neste corte não existe). A posição física da linha é
/// atribuída pelo backend pela ordem de registo; os valores derivados
/// (capacity, peso do vidro) são calculados pelo backend a partir dos
/// inputs consumidos persistidos no Peso — a densidade da água é resolvida
/// pela tabela canónica para o grau inteiro congelado no Peso.
/// </summary>
public sealed class RecordPesoMeasurementHandler : IRecordPesoMeasurementHandler
{
    private readonly DmoDbContext _db;

    public RecordPesoMeasurementHandler(DmoDbContext db)
    {
        _db = db;
    }

    // Duas leituras gravadas ao mesmo tempo colidem na posição: a segunda
    // volta a correr com a posição seguinte.
    public Task<RecordPesoMeasurementOutcome> HandleAsync(
        RecordPesoMeasurement command,
        CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(_db, () => HandleOnceAsync(command, cancellationToken));

    private async Task<RecordPesoMeasurementOutcome> HandleOnceAsync(
        RecordPesoMeasurement command,
        CancellationToken cancellationToken)
    {
        // 1. O Peso tem de existir.
        var peso = await _db.Pesos
            .FirstOrDefaultAsync(p => p.Id == command.PesoId, cancellationToken);
        if (peso is null)
        {
            return Refused(RecordPesoMeasurementResult.PesoNotFound);
        }

        // 2. Posição física da leitura: ordem de registo dentro do Peso.
        var nextRowPosition = await _db.PesoMeasurements
            .AsNoTracking()
            .Where(m => m.PesoId == peso.Id)
            .CountAsync(cancellationToken) + 1;

        // 3. A leitura é acrescentada como facto histórico: o número/posição
        //    CM visível é row-level measurement data; a massa observada é
        //    registada na totalidade. Nenhuma regra de sinal é imposta à
        //    persistência — preserva-se o que foi fisicamente observado.
        var measurement = new PesoMeasurement
        {
            PesoId = peso.Id,
            RowPosition = nextRowPosition,
            CmSubject = command.CmSubject ?? string.Empty,
            WaterWeightGrams = command.WaterWeightGrams
        };
        _db.PesoMeasurements.Add(measurement);

        // 4. Valores derivados da leitura, calculados pelo backend com os
        //    inputs consumidos congelados no Peso (nunca fornecidos pelo
        //    cliente). O grau congelado foi validado na criação contra o
        //    intervalo suportado da tabela canónica; um grau fora do
        //    intervalo aqui seria corrupção de estado — invariante interna,
        //    não um caso de negócio.
        var resolvedWaterDensity = WaterDensityTable.Resolve(peso.WaterTemperatureC)
            ?? throw new InvalidOperationException(
                $"O grau de temperatura da água congelado no Peso {peso.Id} " +
                "está fora do intervalo suportado pela tabela canónica de densidade da água.");

        var capacity = PesoCalculation.CapacityCm3(
            measurement.WaterWeightGrams,
            resolvedWaterDensity);
        var glassWeight = PesoCalculation.GlassWeightGrams(
            capacity,
            peso.VolumeMarisaCm3,
            peso.VolumePuncaoCm3,
            peso.GlassDensityGramsPerCm3);

        await _db.SaveChangesAsync(cancellationToken);

        return new RecordPesoMeasurementOutcome(
            RecordPesoMeasurementResult.Success,
            new RecordedPesoMeasurement(
                measurement.Id,
                peso.Id,
                measurement.RowPosition,
                measurement.CmSubject,
                measurement.WaterWeightGrams,
                capacity,
                glassWeight));
    }

    private static RecordPesoMeasurementOutcome Refused(RecordPesoMeasurementResult result) =>
        new(result);
}

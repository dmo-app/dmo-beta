using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Regista uma leitura física real (measurement row) num peso_id existente.
/// A leitura é acrescentada ao Peso como facto histórico — a massa de água
/// observada é registada na totalidade, nunca truncada, compensada ou
/// reescrita — e o registo não altera o Peso, as Tools nem o contexto da
/// produção. Um Peso de uma produção que entretanto trocou a CM ou a BQ já
/// não recebe leituras novas: vão para o Peso das ferramentas atuais, e o
/// que o Peso anterior tem fica como estava (respostas R15 e R16-3 do
/// especialista, 18/10/2026). A posição física da linha é
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

        // Um Peso associado a uma produção que entretanto trocou a CM ou a
        // BQ: o cálculo usaria as ferramentas antigas (R15). Os Pesos sem
        // produção continuam a gravar.
        if (peso.ControloId is int controloId
            && await PesoCombinations.CurrentPesoIdAsync(_db, controloId, cancellationToken) != peso.Id)
        {
            return Refused(RecordPesoMeasurementResult.PesoNotCurrent);
        }

        // Uma leitura com capacidade ou peso do vidro 0 ou menos é
        // fisicamente impossível (um engano de escrita na massa): nenhuma
        // linha do envio é gravada, como na pré-visualização (deep sweep
        // 18/10/2026, I3-M-3).
        if (WaterDensityTable.ForPeso(peso.WaterDensityGramsPerCm3, peso.WaterTemperatureC) is { } density
            && new[] { command.WaterWeightGrams }
                .Concat((command.AdditionalRows ?? []).Select(row => row.WaterWeightGrams))
                .Any(water =>
                {
                    var rowCapacity = PesoCalculation.CapacityCm3(water, density);
                    return rowCapacity <= 0m
                        || PesoCalculation.GlassWeightGrams(
                            rowCapacity, peso.VolumeMarisaCm3, peso.VolumePuncaoCm3, peso.GlassDensityGramsPerCm3) <= 0m;
                }))
        {
            return Refused(RecordPesoMeasurementResult.ResultNonPositive);
        }

        // 2. Posição física da leitura: ordem de registo dentro do Peso.
        var nextRowPosition = await _db.PesoMeasurements
            .AsNoTracking()
            .Where(m => m.PesoId == peso.Id)
            .CountAsync(cancellationToken) + 1;

        // 3. A leitura é acrescentada como facto histórico: o número/posição
        //    CM visível é row-level measurement data; a massa observada é
        //    registada na totalidade (só o fisicamente impossível, acima, é
        //    recusado).
        var measurement = new PesoMeasurement
        {
            PesoId = peso.Id,
            RowPosition = nextRowPosition,
            CmSubject = command.CmSubject ?? string.Empty,
            WaterWeightGrams = command.WaterWeightGrams
        };
        _db.PesoMeasurements.Add(measurement);

        // As outras linhas do mesmo envio (o «+»), a seguir: gravam-se no
        // mesmo SaveChanges que a primeira, para um pedido cortado a meio não
        // deixar só parte e reenviar não duplicar as primeiras (deep sweep
        // 18/10/2026). Uma colisão de posição repete o lote inteiro.
        foreach (var (row, i) in (command.AdditionalRows ?? []).Select((row, i) => (row, i)))
        {
            _db.PesoMeasurements.Add(new PesoMeasurement
            {
                PesoId = peso.Id,
                RowPosition = nextRowPosition + 1 + i,
                CmSubject = row.CmSubject ?? string.Empty,
                WaterWeightGrams = row.WaterWeightGrams
            });
        }

        // 4. Valores derivados da leitura, calculados pelo backend com os
        //    inputs consumidos congelados no Peso (nunca fornecidos pelo
        //    cliente). O grau congelado foi validado na criação contra o
        //    intervalo suportado da tabela canónica; um grau fora do
        //    intervalo aqui seria corrupção de estado — invariante interna,
        //    não um caso de negócio.
        var resolvedWaterDensity = WaterDensityTable.ForPeso(peso.WaterDensityGramsPerCm3, peso.WaterTemperatureC)
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

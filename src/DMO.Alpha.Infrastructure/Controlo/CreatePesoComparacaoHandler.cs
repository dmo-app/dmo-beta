using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Cria um registo de comparação de pesos (o registo do histórico tipo
/// «Comparação» do dmo-html): as novas leituras dos contra-moldes contra o
/// Peso inicial de uma produção, guardado sob o controlo_id da produção. A
/// validação e o cálculo vêm da FONTE ÚNICA (a
/// <see cref="IPesoComparisonQuery.CompararLeiturasAsync"/> — o mesmo caminho
/// da comparação ao vivo); o handler persiste o registo com os inputs
/// consumidos CONGELADOS (o padrão do Peso: o grau da temperatura, as
/// densidades, os volumes e os fatos humanos da ferramenta — o histórico
/// sobrevive à edição/eliminação da Tool). Todas as recusas são tipadas e
/// verificadas ANTES de qualquer escrita: uma recusa não deixa registos
/// parciais. O registo é append-only: nenhuma comparação é reescrita.
/// A mesma comparação acabada de guardar (cliques repetidos em «Guardar
/// comparação») não fica duas vezes: o pedido repetido abre a que ficou
/// gravada.
/// </summary>
public sealed class CreatePesoComparacaoHandler : ICreatePesoComparacaoHandler
{
    private readonly DmoDbContext _db;
    private readonly IPesoComparisonQuery _comparison;

    public CreatePesoComparacaoHandler(
        DmoDbContext db,
        IPesoComparisonQuery comparison)
    {
        _db = db;
        _comparison = comparison;
    }

    /// <summary>Quanto tempo a mesma comparação, acabada de guardar, conta como o mesmo clique.</summary>
    public static readonly TimeSpan RepeatedSubmitWindow = TimeSpan.FromMinutes(2);

    // Cliques repetidos em «Guardar comparação» (um POST nativo; com a rede
    // lenta o ecrã não muda e o operador carrega outra vez) chegam como
    // pedidos iguais: esperam uns pelos outros e os seguintes abrem a
    // comparação do primeiro em vez de criar outra igual (B-peso-2, deep
    // sweep 18/10/2026).
    public Task<CreatePesoComparacaoOutcome> HandleAsync(
        CreatePesoComparacao command,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, $"peso:comparacao:{command.PesoInicialId}", cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<CreatePesoComparacaoOutcome> HandleLockedAsync(
        CreatePesoComparacao command,
        CancellationToken cancellationToken)
    {
        if (command.PesoInicialId <= 0)
        {
            return new CreatePesoComparacaoOutcome(PesoComparacaoResultado.PesoInicialNaoSelecionado);
        }

        // O cálculo e as recusas pela fonte única (o mesmo caminho da
        // comparação ao vivo): nenhuma segunda implementação da matemática.
        var resultado = await _comparison.CompararLeiturasAsync(
            new PesoComparacaoLeiturasPedido(
                command.PesoInicialId,
                command.CmToolId,
                command.WaterTemperatureC,
                command.Leituras),
            cancellationToken);

        if (resultado.Refusal != PesoComparacaoResultado.Sucesso || resultado.CmToolId is null)
        {
            return new CreatePesoComparacaoOutcome(resultado.Refusal, Ignoradas: resultado.Ignoradas);
        }

        // O controlo_id da produção vem do PRÓPRIO Peso inicial (a comparação
        // é production-scoped: os iniciais da superfície são sempre Pesos de
        // produção). Sem controlo no peso inicial não há registo a criar.
        int controloId;
        if (await _db.Pesos.AsNoTracking()
                .Where(p => p.Id == command.PesoInicialId && p.ControloId != null)
                .Select(p => p.ControloId!.Value)
                .FirstOrDefaultAsync(cancellationToken) is { } resolvedControloId)
        {
            controloId = resolvedControloId;
        }
        else
        {
            return new CreatePesoComparacaoOutcome(PesoComparacaoResultado.PesoInicialNaoEncontrado);
        }

        var note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
        if (await RepetidaAsync(command.PesoInicialId, resultado, note, cancellationToken) is int repetidaId)
        {
            return new CreatePesoComparacaoOutcome(
                PesoComparacaoResultado.Sucesso,
                await _comparison.ObterComparacaoGuardadaAsync(repetidaId, cancellationToken),
                resultado.Ignoradas);
        }

        var registo = new PesoComparacao
        {
            ControloId = controloId,
            PesoInicialId = command.PesoInicialId,
            CmToolId = resultado.CmToolId,
            WaterTemperatureC = resultado.WaterTemperatureC,
            WaterDensityGramsPerCm3 = resultado.WaterDensityGramsPerCm3!.Value,
            GlassDensityGramsPerCm3 = resultado.GlassDensityGramsPerCm3!.Value,
            VolumeMarisaCm3 = resultado.VolumeMarisaCm3!.Value,
            VolumePuncaoCm3 = resultado.VolumePuncaoCm3!.Value,
            CmToolReferenceFrozen = resultado.CmToolReference,
            CmToolLotFrozen = resultado.CmToolLot,
            Note = note,
            CreatedAtUtc = DateTime.UtcNow,
            Measurements = resultado.Leituras
                .Select(l => new PesoComparacaoMeasurement
                {
                    RowPosition = l.RowPosition,
                    CmSubject = l.CmSubject,
                    WaterWeightGrams = l.WaterWeightGrams
                })
                .ToList()
        };

        _db.PesoComparacoes.Add(registo);
        await _db.SaveChangesAsync(cancellationToken);

        // O read model do registo guardado (a resposta do PRG renderiza o
        // resultado a partir do PERSISTIDO, nunca do rascunho).
        var readModel = await _comparison.ObterComparacaoGuardadaAsync(registo.Id, cancellationToken);
        // As leituras deixadas de fora não são gravadas; voltam para o aviso.
        return new CreatePesoComparacaoOutcome(PesoComparacaoResultado.Sucesso, readModel, resultado.Ignoradas);
    }

    /// <summary>
    /// A última comparação deste Peso inicial, se é esta mesma (o mesmo
    /// contra-molde, o mesmo grau, a mesma nota e as mesmas leituras pela
    /// mesma ordem) e foi guardada há menos de <see cref="RepeatedSubmitWindow"/>;
    /// null quando algum difere.
    /// </summary>
    private async Task<int?> RepetidaAsync(
        int pesoInicialId,
        PesoComparacaoLeiturasResultado resultado,
        string? note,
        CancellationToken cancellationToken)
    {
        var desde = DateTime.UtcNow - RepeatedSubmitWindow;
        var ultima = await _db.PesoComparacoes.AsNoTracking()
            .Where(c => c.PesoInicialId == pesoInicialId)
            .OrderByDescending(c => c.Id)
            .Select(c => new { c.Id, c.CmToolId, c.WaterTemperatureC, c.Note, c.CreatedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);
        if (ultima is null
            || ultima.CreatedAtUtc < desde
            || ultima.CmToolId != resultado.CmToolId
            || ultima.WaterTemperatureC != resultado.WaterTemperatureC
            || ultima.Note != note)
        {
            return null;
        }

        var gravadas = await _db.PesoComparacaoMeasurements.AsNoTracking()
            .Where(m => m.ComparacaoId == ultima.Id)
            .OrderBy(m => m.RowPosition)
            .Select(m => new { m.CmSubject, m.WaterWeightGrams })
            .ToListAsync(cancellationToken);
        var novas = resultado.Leituras.OrderBy(l => l.RowPosition).ToList();
        var iguais = gravadas.Count == novas.Count
            && gravadas.Zip(novas).All(par => par.First.CmSubject == par.Second.CmSubject
                && par.First.WaterWeightGrams == par.Second.WaterWeightGrams);
        return iguais ? ultima.Id : null;
    }
}

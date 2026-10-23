using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

public sealed class CorrectPegamentosMeasurementHandler : ICorrectPegamentosMeasurementHandler
{
    private readonly DmoDbContext _db;

    public CorrectPegamentosMeasurementHandler(DmoDbContext db)
    {
        _db = db;
    }

    public Task<CorrectPegamentosMeasurementResult> EditAsync(
        EditPegamentosMeasurement command,
        CancellationToken cancellationToken = default) =>
        command.CosturaMm <= 0m || command.ContraCosturaMm is <= 0m
            ? Task.FromResult(CorrectPegamentosMeasurementResult.ImpossibleValue)
            : DbWrite.InTransactionAsync(_db, () => EditOnceAsync(command, cancellationToken), cancellationToken);

    // A05 (23/10/2026): duas correções da mesma leitura ao mesmo tempo liam o
    // mesmo valor e gravavam 10→11 e 10→12 no histórico. Com o trinco da folha,
    // a segunda espera e lê o valor que a primeira gravou (10→11, 11→12).
    private async Task<CorrectPegamentosMeasurementResult> EditOnceAsync(
        EditPegamentosMeasurement command,
        CancellationToken cancellationToken)
    {
        await LockSheetOfAsync(command.Anchor, cancellationToken);
        var measurement = await FindAsync(command.Anchor, cancellationToken);
        if (measurement is null)
        {
            return CorrectPegamentosMeasurementResult.MeasurementNotFound;
        }

        var antes = Rasto(measurement, apagada: false, numero: 0);
        measurement.CosturaMm = command.CosturaMm;
        measurement.ContraCosturaMm = command.ContraCosturaMm;
        var numeroAntes = measurement.NumeroFerramenta;
        measurement.NumeroFerramenta = RecordPegamentosMeasurementHandler.NumeroFerramenta(command.NumeroFerramenta);

        // O nominal congelado na leitura não muda. Uma leitura que ficou sem
        // nominal passa a usar o da ferramenta, se entretanto foi preenchido.
        if (measurement.NominalGargaloMm is null)
        {
            var toolId = measurement.Component switch
            {
                PegamentosComponent.Cm => measurement.Pegamentos.CmToolId,
                PegamentosComponent.Bq => measurement.Pegamentos.BqToolId,
                _ => measurement.Pegamentos.MfToolId
            };
            measurement.NominalGargaloMm = await _db.ToolTechnicalContexts
                .AsNoTracking()
                .Where(t => t.ToolId == toolId)
                .Select(t => t.DiametroGargaloMm)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Guardar os mesmos valores não é uma correção: nem rasto nem aviso.
        // Só o número mudou: grava-se, sem rasto (não é uma medição).
        if (measurement.CosturaMm == antes.CosturaAntesMm
            && measurement.ContraCosturaMm == antes.ContraCosturaAntesMm
            && measurement.NominalGargaloMm == antes.NominalGargaloAntesMm)
        {
            if (measurement.NumeroFerramenta != numeroAntes)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            return CorrectPegamentosMeasurementResult.Success;
        }

        // O rasto (4.6) e a data da última correção, que a Revisão compara
        // com a aprovação para avisar «Corrigido depois da aprovação» (C9).
        antes.Numero = await _db.PegamentosMeasurements.CountAsync(
            m => m.PegamentosId == measurement.PegamentosId
                && m.Component == measurement.Component
                && m.RowPosition <= measurement.RowPosition,
            cancellationToken);
        antes.CosturaDepoisMm = measurement.CosturaMm;
        antes.ContraCosturaDepoisMm = measurement.ContraCosturaMm;
        antes.NominalGargaloDepoisMm = measurement.NominalGargaloMm;
        _db.PegamentosMeasurementHistorico.Add(antes);
        measurement.Pegamentos.CorrigidoEmUtc = antes.EmUtc;

        await _db.SaveChangesAsync(cancellationToken);
        return CorrectPegamentosMeasurementResult.Success;
    }

    // Apagar renumera a folha: no trinco dela, para um registo ao mesmo
    // tempo (outro separador) não ficar numa posição que já não é a última.
    public Task<CorrectPegamentosMeasurementResult> DeleteAsync(
        PegamentosMeasurementAnchor anchor,
        CancellationToken cancellationToken = default) =>
        DbWrite.InTransactionAsync(_db, () => DeleteOnceAsync(anchor, cancellationToken), cancellationToken);

    private async Task<CorrectPegamentosMeasurementResult> DeleteOnceAsync(
        PegamentosMeasurementAnchor anchor,
        CancellationToken cancellationToken)
    {
        await LockSheetOfAsync(anchor, cancellationToken);

        var measurement = await FindAsync(anchor, cancellationToken);
        if (measurement is null)
        {
            return CorrectPegamentosMeasurementResult.MeasurementNotFound;
        }

        var pegamentosId = measurement.PegamentosId;
        var remaining = await _db.PegamentosMeasurements
            .Where(m => m.PegamentosId == pegamentosId && m.Id != measurement.Id)
            .OrderBy(m => m.RowPosition)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        // A leitura sai da tabela; o rasto guarda o que tinha (4.6).
        var rasto = Rasto(measurement, apagada: true, numero: 1 + remaining.Count(
            m => m.Component == measurement.Component && m.RowPosition < measurement.RowPosition));
        _db.PegamentosMeasurementHistorico.Add(rasto);
        measurement.Pegamentos.CorrigidoEmUtc = rasto.EmUtc;
        _db.PegamentosMeasurements.Remove(measurement);

        for (var i = 0; i < remaining.Count; i++)
        {
            remaining[i].RowPosition = i + 1;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return CorrectPegamentosMeasurementResult.Success;
    }

    // O rasto com os valores da leitura antes da correção; quem corrigiu é
    // carimbado ao gravar (RegistoCarimboInterceptor).
    private async Task LockSheetOfAsync(PegamentosMeasurementAnchor anchor, CancellationToken cancellationToken)
    {
        var sheetId = await _db.PegamentosMeasurements
            .AsNoTracking()
            .Where(m => m.Id == anchor.MeasurementId)
            .Select(m => (int?)m.PegamentosId)
            .FirstOrDefaultAsync(cancellationToken);
        if (sheetId is int lockedSheet)
        {
            await RecordPegamentosMeasurementHandler.LockSheetAsync(_db, lockedSheet, cancellationToken);
        }
    }

    private static PegamentosMeasurementHistorico Rasto(PegamentosMeasurement measurement, bool apagada, int numero) => new()
    {
        PegamentosId = measurement.PegamentosId,
        MeasurementId = measurement.Id,
        Numero = numero,
        Component = measurement.Component,
        Apagada = apagada,
        CosturaAntesMm = measurement.CosturaMm,
        ContraCosturaAntesMm = measurement.ContraCosturaMm,
        NominalGargaloAntesMm = measurement.NominalGargaloMm,
        EmUtc = DateTime.UtcNow
    };

    private Task<PegamentosMeasurement?> FindAsync(PegamentosMeasurementAnchor anchor, CancellationToken cancellationToken) =>
        _db.PegamentosMeasurements
            .Include(m => m.Pegamentos)
            .Where(m => m.Id == anchor.MeasurementId)
            .Where(m => anchor.PegamentosId != null
                ? m.PegamentosId == anchor.PegamentosId
                : m.Pegamentos.ControloId == anchor.ControloId)
            .FirstOrDefaultAsync(cancellationToken);
}

using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Substitui integralmente os VALORES TÉCNICOS de uma Tool (todas as
/// colunas do ToolTechnicalContext — processo, pesos/volumes/gargalo e a
/// geometria TP): grava sempre no tool_id owner (regra Ferramentas
/// «Tool-owned technical values»), cria a linha de contexto técnico quando
/// ainda não existe e trata o vazio como NULL (o estado «em falta» é
/// válido — nenhum valor é inventado). Desde a beta.37 o tampão regista só
/// D, raio e calote; os derivados antigos (R base, R esfera, volume) não são
/// apagados quando o comando os omite. A DUMMY nunca é endereçável (ToolNotFound). Recusas
/// tipadas ANTES de qualquer escrita.
/// </summary>
public sealed class EditToolTechnicalValuesHandler : IEditToolTechnicalValuesHandler
{
    private readonly DmoDbContext _db;

    public EditToolTechnicalValuesHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<EditToolTechnicalValuesOutcome> HandleAsync(
        EditToolTechnicalValues command,
        CancellationToken cancellationToken = default)
    {
        // 1. A Tool tem de existir (e não ser a DUMMY) — os valores são
        //    factos do tool_id owner.
        if (string.IsNullOrEmpty(command.ToolId)
            || command.ToolId == ToolRegistryConstants.RemovedToolId)
        {
            return Refused(EditToolTechnicalValuesResult.ToolNotFound);
        }

        var toolType = await _db.Tools
            .AsNoTracking()
            .Where(t => t.ToolId == command.ToolId)
            .Select(t => (ToolType?)t.Type)
            .FirstOrDefaultAsync(cancellationToken);
        if (toolType is not { } type)
        {
            return Refused(EditToolTechnicalValuesResult.ToolNotFound);
        }

        // 2. Token do processo (quando fornecido): um dos autorizados
        //    (NNPB/PS) — recusa tipada, como no Peso. Vazio = NULL (limpar
        //    é um estado válido: o valor pode não existir).
        CmProcess? process = null;
        if (command.ProcessToken is { Length: > 0 } processToken)
        {
            if (processToken is not ("NNPB" or "PS"))
            {
                return Refused(EditToolTechnicalValuesResult.UnknownProcessToken);
            }

            process = processToken == "NNPB" ? CmProcess.Nnpb : CmProcess.Ps;
        }

        // Um peso, volume ou medida de 0 ou menos (a calote só abaixo de 0)
        // é fisicamente impossível e entraria nos Pesos: recusa antes de
        // qualquer escrita (Diogo, 18/10/2026; deep sweep I3-M-4).
        if (new[]
            {
                command.NominalWeightGrams, command.VolumePuncaoCm3, command.VolumeMarisaCm3,
                command.DiametroGargaloMm, command.TpDiametroMm, command.TpRaioMm,
                command.TpRaioBaseMm, command.TpRaioEsferaMm, command.TpVolumeMm3
            }.Any(value => value is <= 0m)
            || command.TpSagittaMm is < 0m)
        {
            return Refused(EditToolTechnicalValuesResult.ImpossibleValue);
        }

        // 3. Full-replace de TODAS as colunas; a linha é criada quando a
        //    Tool ainda não tem contexto técnico (os valores não têm de
        //    existir quando o tool_id é criado). Uma única escrita atómica.
        var technical = await _db.ToolTechnicalContexts
            .FirstOrDefaultAsync(t => t.ToolId == command.ToolId, cancellationToken);

        // Outra pessoa gravou depois de a ficha ser aberta (0 = a ficha abriu
        // sem linha técnica): nada se grava por cima (varredura, B4).
        if (command.ExpectedVersion is { } expected
            && (technical is null ? 0u : _db.Entry(technical).Property<uint>(DmoDbContext.RowVersion).CurrentValue) != expected)
        {
            return Refused(EditToolTechnicalValuesResult.ChangedByAnotherUser);
        }

        if (technical is null)
        {
            technical = new ToolTechnicalContext { ToolId = command.ToolId };
            _db.ToolTechnicalContexts.Add(technical);
        }

        // beta.37: cada tipo só grava os seus campos (CM → processo, peso
        // nominal, punção e tampão; BQ → marisa; todos → gargalo). Os campos
        // de outro tipo nunca são escritos nem apagados por este comando.
        if (type == ToolType.Cm)
        {
            technical.Process = process;
            technical.NominalWeightGrams = command.NominalWeightGrams;
            technical.VolumePuncaoCm3 = command.VolumePuncaoCm3;
            technical.TpDiametroMm = command.TpDiametroMm;
            // O raio já não é pedido (só D e calote): o gravado nunca é apagado.
            technical.TpRaioMm = command.TpRaioMm ?? technical.TpRaioMm;
            technical.TpSagittaMm = command.TpSagittaMm;
            // A ficha só pede D e calote; o volume é calculado
            // (PesoCapCalculation). Os derivados antigos nunca são apagados
            // por omissão: só mudam quando o comando traz um valor.
            technical.TpRaioBaseMm = command.TpRaioBaseMm ?? technical.TpRaioBaseMm;
            technical.TpRaioEsferaMm = command.TpRaioEsferaMm ?? technical.TpRaioEsferaMm;
            technical.TpVolumeMm3 = command.TpVolumeMm3 ?? technical.TpVolumeMm3;
        }

        if (type == ToolType.Bq)
        {
            technical.VolumeMarisaCm3 = command.VolumeMarisaCm3;
        }

        technical.DiametroGargaloMm = command.DiametroGargaloMm;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra pessoa gravou entre a leitura e esta escrita.
            return Refused(EditToolTechnicalValuesResult.ChangedByAnotherUser);
        }

        return new EditToolTechnicalValuesOutcome(
            EditToolTechnicalValuesResult.Success,
            new ToolTechnicalReadModel(
                technical.ToolId,
                technical.Process,
                technical.NominalWeightGrams,
                technical.VolumePuncaoCm3,
                technical.VolumeMarisaCm3,
                technical.DiametroGargaloMm,
                technical.TpDiametroMm,
                technical.TpRaioMm,
                technical.TpSagittaMm,
                technical.TpRaioBaseMm,
                technical.TpRaioEsferaMm,
                technical.TpVolumeMm3));
    }

    private static EditToolTechnicalValuesOutcome Refused(EditToolTechnicalValuesResult result) =>
        new(result);
}

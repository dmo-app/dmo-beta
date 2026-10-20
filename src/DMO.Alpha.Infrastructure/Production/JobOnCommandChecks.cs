using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// As verificações que o Criar e o Editar do Job On partilham, num só sítio
/// (sweep de 19/10/2026, J-4): a máquina existe; o MF é obrigatório (regra do
/// Owner 2026-10-10: «jobon necessita sempre de MF, senao nao faz garrafas ou
/// frascos»); a CM, o MF e a BQ escolhidos existem no registo Ferramentas e
/// nenhum é a linha DUMMY (REMOVED). Antes, cada handler repetia as regras e
/// fazia uma consulta às ferramentas por cada uma; agora é uma só, com os
/// três tool_id. Um slot null não se aplica; um tool_id preenchido, mesmo em
/// branco, que não resolve é recusa tipada. A ordem das recusas é a de antes.
/// </summary>
internal static class JobOnCommandChecks
{
    internal enum Refusal
    {
        MachineNotFound,
        MissingMfTool,
        CmToolNotFound,
        MfToolNotFound,
        BqToolNotFound
    }

    internal sealed record Outcome(Machine? Machine, Refusal? Refused);

    internal static async Task<Outcome> CheckAsync(
        DmoDbContext db,
        int machineId,
        string? cmToolId,
        string? mfToolId,
        string? bqToolId,
        CancellationToken cancellationToken)
    {
        // A máquina é âncora canónica persistida: um MachineId que não
        // resolve é recusa tipada; nunca se escolhe outra máquina.
        var machine = await db.Machines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == machineId, cancellationToken);
        if (machine is null)
        {
            return new Outcome(null, Refusal.MachineNotFound);
        }

        if (mfToolId is not { Length: > 0 })
        {
            return new Outcome(machine, Refusal.MissingMfTool);
        }

        var selected = new[] { cmToolId, mfToolId, bqToolId }
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var existing = await db.Tools
            .AsNoTracking()
            .Where(t => selected.Contains(t.ToolId) && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => t.ToolId)
            .ToListAsync(cancellationToken);

        bool Exists(string toolId) => existing.Contains(toolId, StringComparer.Ordinal);

        if (cmToolId is not null && !Exists(cmToolId))
        {
            return new Outcome(machine, Refusal.CmToolNotFound);
        }

        if (!Exists(mfToolId))
        {
            return new Outcome(machine, Refusal.MfToolNotFound);
        }

        if (bqToolId is not null && !Exists(bqToolId))
        {
            return new Outcome(machine, Refusal.BqToolNotFound);
        }

        return new Outcome(machine, null);
    }
}

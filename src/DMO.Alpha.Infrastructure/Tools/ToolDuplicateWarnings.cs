using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// O AVISO DE FERRAMENTA REPETIDA (o proprietário, 18/10/2026): registar,
/// duplicar ou editar para o tipo, referência e lote de outra ferramenta é
/// recusado e essa outra fica marcada, porque a errada pode ser a primeira.
/// Na ficha dela, quem confirmar que está certa limpa o aviso.
/// AS MÁQUINAS CONTAM (o proprietário, 19/10/2026): o mesmo tipo,
/// referência e lote só é repetido quando as duas têm pelo menos uma
/// máquina em comum (MF 1075 lote 4 B1/C3 e MF 1075 lote 4 B3 são duas
/// ferramentas). Uma ferramenta sem máquinas pode estar em qualquer uma,
/// por isso conta como repetida.
/// </summary>
public sealed class ToolDuplicateWarnings
{
    /// <summary>
    /// O duplo clique (regra do Diogo, 18/10/2026: não mostrar nada, só não
    /// criar repetido): o mesmo registo até 10 s depois de a ferramenta nascer
    /// devolve-a como se fosse o primeiro clique, sem recusa nem aviso.
    /// </summary>
    internal static readonly TimeSpan RepeatedSubmitWindow = TimeSpan.FromSeconds(10);

    internal static bool IsRepeatedSubmit(DateTime? registeredAtUtc) =>
        registeredAtUtc is { } at && DateTime.UtcNow - at < RepeatedSubmitWindow;

    /// <summary>A ferramenta existente que uma nova repetiria.</summary>
    internal sealed record Repeated(string ToolId, DateTime? RegisteredAtUtc, string? AssociatedMfReference, int? BqAccountedQuantity);

    /// <summary>
    /// A outra ferramenta com o mesmo tipo, referência e lote e pelo menos uma
    /// máquina em comum com <paramref name="machineIds"/>, ou null. A linha
    /// DUMMY e <paramref name="exceptToolId"/> não contam.
    /// </summary>
    internal static async Task<Repeated?> FindRepeatedAsync(
        DmoDbContext db,
        ToolType type,
        string reference,
        string lot,
        IReadOnlyCollection<int> machineIds,
        string? exceptToolId,
        CancellationToken cancellationToken)
    {
        var sameFacts = await db.Tools
            .AsNoTracking()
            .Where(t => t.Type == type
                && t.Reference == reference
                && t.Lot == lot
                && t.ToolId != ToolRegistryConstants.RemovedToolId
                && t.ToolId != exceptToolId)
            .OrderBy(t => t.ToolId)
            .Select(t => new Repeated(t.ToolId, t.RegisteredAtUtc, t.AssociatedMfReference, t.BqAccountedQuantity))
            .ToListAsync(cancellationToken);
        if (sameFacts.Count == 0)
        {
            return null;
        }

        var ids = sameFacts.Select(t => t.ToolId).ToList();
        var links = await db.ToolMachineLinks
            .AsNoTracking()
            .Where(link => ids.Contains(link.ToolId))
            .Select(link => new { link.ToolId, link.MachineId })
            .ToListAsync(cancellationToken);

        return sameFacts.FirstOrDefault(t =>
        {
            var theirs = links.Where(link => link.ToolId == t.ToolId).Select(link => link.MachineId).ToList();
            return machineIds.Count == 0 || theirs.Count == 0 || theirs.Any(machineIds.Contains);
        });
    }

    private readonly DmoDbContext _db;

    public ToolDuplicateWarnings(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>Marca a ferramenta existente com o aviso (quem tentou e quando).</summary>
    internal static async Task MarkAsync(DmoDbContext db, string toolId, string? requestedBy, CancellationToken cancellationToken)
    {
        var tool = await db.Tools.FirstOrDefaultAsync(t => t.ToolId == toolId, cancellationToken);
        if (tool is null)
        {
            return;
        }

        tool.DuplicateWarningAtUtc = DateTime.UtcNow;
        var by = requestedBy?.Trim();
        tool.DuplicateWarningByNameFrozen = string.IsNullOrEmpty(by) ? null : by.Length <= 254 ? by : by[..254];
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra gravação da mesma ferramenta ao mesmo tempo: o registo
            // repetido já foi recusado; o aviso fica para a próxima tentativa.
        }
    }

    /// <summary>Limpa o aviso; false quando a ferramenta não existe.</summary>
    public async Task<bool> ClearAsync(string toolId, CancellationToken cancellationToken = default)
    {
        var tool = await _db.Tools.FirstOrDefaultAsync(
            t => t.ToolId == toolId && t.ToolId != DMO.Alpha.Core.Tools.ToolRegistryConstants.RemovedToolId,
            cancellationToken);
        if (tool is null)
        {
            return false;
        }

        if (tool.DuplicateWarningAtUtc is not null || tool.DuplicateWarningByNameFrozen is not null)
        {
            tool.DuplicateWarningAtUtc = null;
            tool.DuplicateWarningByNameFrozen = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}

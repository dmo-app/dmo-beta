using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// O link do mcaliper de cada ferramenta (Diogo, 17/10/2026): fica na ficha
/// técnica da Tool (o tool_id). O Controlo Criar associa-o no Resumo; o
/// Controlo Aprovar copia-o na Revisão. O link nunca aparece escrito no ecrã.
/// </summary>
public sealed class ToolMcaliperLinks
{
    private readonly DmoDbContext _db;

    public ToolMcaliperLinks(DmoDbContext db)
    {
        _db = db;
    }

    /// <summary>Os links das Tools pedidas, só as que têm link.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAsync(
        IEnumerable<string?> toolIds, CancellationToken cancellationToken = default)
    {
        var ids = toolIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        return await _db.ToolTechnicalContexts
            .AsNoTracking()
            .Where(t => ids.Contains(t.ToolId) && t.McaliperUrl != null)
            .ToDictionaryAsync(t => t.ToolId, t => t.McaliperUrl!, cancellationToken);
    }

    /// <summary>
    /// Associa (ou retira, com o campo vazio) o link de uma Tool. Só aceita um
    /// endereço http ou https completo. Cria a linha técnica quando a Tool
    /// ainda não a tem; não mexe em mais nenhum valor técnico.
    /// </summary>
    public async Task<bool> SetAsync(string toolId, string? url, CancellationToken cancellationToken = default)
    {
        var value = Normalize(url, out var valid);
        if (!valid)
        {
            return false;
        }

        if (!await _db.Tools.AnyAsync(t => t.ToolId == toolId, cancellationToken))
        {
            return false;
        }

        var technical = await _db.ToolTechnicalContexts
            .FirstOrDefaultAsync(t => t.ToolId == toolId, cancellationToken);
        if (technical is null)
        {
            if (value is null)
            {
                return true;
            }

            technical = new ToolTechnicalContext { ToolId = toolId };
            _db.ToolTechnicalContexts.Add(technical);
        }

        technical.McaliperUrl = value;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Vazio = sem link; senão um URL http(s) absoluto dentro do limite.</summary>
    public static string? Normalize(string? url, out bool valid)
    {
        var trimmed = url?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            valid = true;
            return null;
        }

        valid = trimmed.Length <= ToolTechnicalContext.McaliperUrlMaxLength
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        return valid ? trimmed : null;
    }
}

using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// The Admin → Templates administration service over the persisted
/// <see cref="Template"/> rows (see <see cref="ITemplateAdminService"/>):
/// the foundation read (ordered by stable key) plus the functional
/// create/edit workflows. The stable <c>TemplateKey</c> is minted by the
/// backend (TemplateKeyMinter) and never rewritten; the label is a
/// mutable display fact; the capability set is validated against the
/// closed Beta vocabulary (<see cref="DmoCapabilities"/>) with typed
/// refusals BEFORE any write, and persists canonically (de-duplicated,
/// sorted, comma-joined) through the model's storage conversion. Zero
/// capabilities is valid («Sem capabilities»).
/// </summary>
public sealed class TemplateAdminService : ITemplateAdminService
{
    private readonly DmoDbContext _dbContext;

    public TemplateAdminService(DmoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var templates = await _dbContext.Templates
            .AsNoTracking()
            .OrderBy(t => t.TemplateKey)
            .ToListAsync(cancellationToken);

        return templates
            .Select(t => new TemplateSummary
            {
                Id = t.Id,
                TemplateKey = t.TemplateKey,
                Name = t.Name,
                Capabilities = t.Capabilities
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CreateTemplateOutcome> CreateTemplateAsync(
        CreateTemplate command,
        CancellationToken cancellationToken = default)
    {
        // 1. Rótulo obrigatório (apenas rótulo — nunca a âncora de acesso).
        var name = command.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return RefusedCreate(CreateTemplateResult.MissingName);
        }

        // 2. Vocabulário FECHADO: cada capability é exatamente uma das
        //    Beta (DmoCapabilities.BetaSet) — recusa tipada antes de
        //    qualquer escrita. Zero capabilities é permitido.
        var capabilities = NormalizeCapabilities(command.Capabilities);
        if (capabilities is null)
        {
            return RefusedCreate(CreateTemplateResult.UnknownCapability);
        }

        // 3. Mint da identidade estável: «tpl-» + 8 hex, verificação de
        //    existência + re-mint — nunca derivada do rótulo, nunca
        //    sequencial (precedente ToolIdMinter).
        var templateKey = await TemplateKeyMinter.MintUnusedAsync(
            key => _dbContext.Templates.AsNoTracking().AnyAsync(t => t.TemplateKey == key, cancellationToken),
            cancellationToken);

        // 4. Escrita única: a Template nasce com a key mintada, o rótulo
        //    e a forma canónica de conjunto das capabilities.
        var template = new Template
        {
            Id = Guid.NewGuid(),
            TemplateKey = templateKey,
            Name = name,
            Capabilities = capabilities
        };
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateTemplateOutcome(
            CreateTemplateResult.Success,
            new TemplateSummary
            {
                Id = template.Id,
                TemplateKey = template.TemplateKey,
                Name = template.Name,
                Capabilities = template.Capabilities
            });
    }

    /// <inheritdoc />
    public async Task<EditTemplateOutcome> EditTemplateAsync(
        EditTemplate command,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolução pela identidade ESTÁVEL (a key nunca muda; o
        //    rótulo pode ter sido renomeado — nunca é a âncora).
        var template = await _dbContext.Templates
            .FirstOrDefaultAsync(t => t.TemplateKey == command.TemplateKey, cancellationToken);

        if (template is null)
        {
            return RefusedEdit(EditTemplateResult.UnknownTemplate);
        }

        // 2. Rótulo obrigatório.
        var name = command.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return RefusedEdit(EditTemplateResult.MissingName);
        }

        // 3. Vocabulário FECHADO (o mesmo da criação).
        var capabilities = NormalizeCapabilities(command.Capabilities);
        if (capabilities is null)
        {
            return RefusedEdit(EditTemplateResult.UnknownCapability);
        }

        // 4. Escrita única: o rótulo e o conjunto mudam; a key estável e
        //    os timestamps de criação ficam intocados (apenas o
        //    UpdatedAtUtc avança). Editar uma Template muda o acesso
        //    efectivo corrente dos Users atribuídos — nunca reescreve
        //    história (TEMPLATES.md §"Template changes").
        template.Name = name;
        template.Capabilities = capabilities;
        template.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EditTemplateOutcome(
            EditTemplateResult.Success,
            new TemplateSummary
            {
                Id = template.Id,
                TemplateKey = template.TemplateKey,
                Name = template.Name,
                Capabilities = template.Capabilities
            });
    }

    /// <inheritdoc />
    public async Task<DeleteTemplateOutcome> DeleteTemplateAsync(
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        var key = templateKey?.Trim();
        var template = string.IsNullOrEmpty(key)
            ? null
            : await _dbContext.Templates
                .FirstOrDefaultAsync(t => t.TemplateKey == key, cancellationToken);

        if (template is null)
        {
            return new DeleteTemplateOutcome(DeleteTemplateResult.UnknownTemplate);
        }

        // Ainda atribuída: recusa com a contagem (a FK das atribuições é
        // Restrict — nunca se apaga uma atribuição por arrasto).
        var assignedUsers = await _dbContext.TemplateAssignments
            .CountAsync(a => a.TemplateId == template.Id, cancellationToken);
        if (assignedUsers > 0)
        {
            return new DeleteTemplateOutcome(DeleteTemplateResult.InUse, assignedUsers);
        }

        _dbContext.Templates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteTemplateOutcome(DeleteTemplateResult.Success);
    }

    /// <summary>
    /// Normaliza o conjunto de capabilities submetido: trim, vazios
    /// descartados, duplicados colapsados. Devolve null (recusa tipada
    /// UnknownCapability) quando algum token não é exatamente uma das
    /// capabilities Beta — o matrix permissão-por-módulo do mockup NÃO é
    /// vocabulário Beta e é recusado aqui.
    /// </summary>
    private static IReadOnlyList<string>? NormalizeCapabilities(IReadOnlyList<string> capabilities)
    {
        if (capabilities.Count == 0)
        {
            return [];
        }

        List<string> normalized = [];
        foreach (var capability in capabilities)
        {
            var trimmed = capability?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (!DmoCapabilities.IsBetaCapability(trimmed))
            {
                return null;
            }

            normalized.Add(trimmed);
        }

        return normalized.Distinct(StringComparer.Ordinal).ToList();
    }

    private static CreateTemplateOutcome RefusedCreate(CreateTemplateResult result) => new(result);

    private static EditTemplateOutcome RefusedEdit(EditTemplateResult result) => new(result);
}

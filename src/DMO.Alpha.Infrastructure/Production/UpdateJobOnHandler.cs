using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Edita um Job On existente — a produção re-planeada: a janela temporal
/// (inicio→fim), a máquina e os tool_ids dos contextos CM/MF/BQ gravam
/// numa ÚNICA escrita atómica (uma transação), SEM criar outra produção
/// — o jobon_id, a Reference e o ProductionNumber são o MESMO facto (a
/// identidade da produção é imutável na edição: o handler nem as lê).
/// Cada contexto que o comando traz com seleção atualiza o ToolId NO
/// MESMO contexto (o bq_id não muda: os traces/movimentos existentes
/// ficam ancorados ao mesmo contexto, são factos históricos — a
/// produção re-planeada não os apaga nem duplica); um contexto que não
/// existia nasce nesta escrita (o ensure do create herda na edição); um
/// slot null PRESERVA o contexto existente — nunca destrói um facto. O
/// controlo_id associado não é tocado (nasce com a criação, spec Job On
/// L33). As recusas tipadas (JobOnNotFound — nenhuma linha inventada;
/// MachineNotFound e as recusas de tools, as mesmas do create;
/// MissingMfTool — a regra do Owner herda na edição) são todas
/// verificadas antes de qualquer escrita: uma recusa não deixa escritas
/// parciais.
/// </summary>
public sealed class UpdateJobOnHandler : IUpdateJobOnHandler
{
    private readonly DmoDbContext _db;

    public UpdateJobOnHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateJobOnOutcome> HandleAsync(
        UpdateJobOn command,
        CancellationToken cancellationToken = default)
    {
        // A produção editada EXISTE: o jobon é carregado COM os seus
        // contextos (tracked — é a mesma linha que vai gravar). Um
        // jobon_id que não resolve é recusa tipada JobOnNotFound —
        // nenhuma linha é inventada, nunca uma exceção.
        var jobOn = await _db.JobOns
            .Include(j => j.CmContext)
            .Include(j => j.MfContext)
            .Include(j => j.BqContext)
            .FirstOrDefaultAsync(j => j.Id == command.JobOnId, cancellationToken);
        if (jobOn is null)
        {
            return Refused(UpdateJobOnResult.JobOnNotFound);
        }

        // A máquina é âncora canônica persistida — a MESMA recusa do
        // create: um MachineId que não resolve nunca é
        // auto-selecionada outra máquina.
        var machine = await _db.Machines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == command.MachineId, cancellationToken);
        if (machine is null)
        {
            return Refused(UpdateJobOnResult.MachineNotFound);
        }

        // O MF é OBRIGATÓRIO — a regra do Owner 2026-10-10 («jobon
        // necessita sempre de MF, senao nao faz garrafas ou frascos»)
        // HERDA na edição: a produção re-planeada mantém a identidade
        // mínima do registo de produção.
        if (command.MfToolId is not { Length: > 0 })
        {
            return Refused(UpdateJobOnResult.MissingMfTool);
        }

        // As Tools têm de existir no registo canônico Ferramentas — as
        // MESMAS recusas do create (a linha DUMMY/REMOVED nunca é
        // utilizável: o backend fecha a porta).
        if (command.CmToolId is { } cmToolId
            && !await _db.Tools.AsNoTracking().AnyAsync(
                t => t.ToolId == cmToolId
                    && t.ToolId != ToolRegistryConstants.RemovedToolId,
                cancellationToken))
        {
            return Refused(UpdateJobOnResult.CmToolNotFound);
        }

        if (command.MfToolId is { } mfToolId
            && !await _db.Tools.AsNoTracking().AnyAsync(
                t => t.ToolId == mfToolId
                    && t.ToolId != ToolRegistryConstants.RemovedToolId,
                cancellationToken))
        {
            return Refused(UpdateJobOnResult.MfToolNotFound);
        }

        if (command.BqToolId is { } bqToolId
            && !await _db.Tools.AsNoTracking().AnyAsync(
                t => t.ToolId == bqToolId
                    && t.ToolId != ToolRegistryConstants.RemovedToolId,
                cancellationToken))
        {
            return Refused(UpdateJobOnResult.BqToolNotFound);
        }

        // A CALOTE DESTA PRODUÇÃO (o campo j-calote da ficha — EDITÁVEL na
        // ficha): negativa é fisicamente impossível — recusa tipada ANTES
        // de qualquer escrita (0 é válido: calote plana).
        if (command.CaloteMm is < 0m)
        {
            return Refused(UpdateJobOnResult.InvalidCalote);
        }

        // TODAS as validações passaram — a produção re-planeada grava
        // numa ÚNICA escrita atómica (uma transação): as datas, a
        // máquina e os tool_ids dos contextos. A identidade
        // (Reference/ProductionNumber) nem é lida — fica o facto que é.
        // O estado antes da edição, para registar o que mudou.
        var before = new
        {
            jobOn.MachineId,
            jobOn.InicioEm,
            jobOn.FimEm,
            jobOn.CaloteMm,
            Cm = jobOn.CmContext?.ToolId,
            Mf = jobOn.MfContext?.ToolId,
            Bq = jobOn.BqContext?.ToolId
        };

        jobOn.MachineId = machine.Id;
        jobOn.InicioEm = command.InicioEm;
        jobOn.FimEm = command.FimEm;
        // A calote grava SEMPRE com o valor submetido (o campo vem
        // preenchido com o valor corrente da ficha); null = a calote limpa
        // (a referência da Tool manda no apoio TP/calote do Peso).
        jobOn.CaloteMm = command.CaloteMm;

        // Cada contexto com seleção: o MESMO contexto com o ToolId novo
        // (os factos históricos ancorados ficam); um contexto que não
        // existia nasce nesta escrita. Um slot null PRESERVA o contexto
        // existente — a edição nunca destrói um facto.
        CmContext? cm = jobOn.CmContext;
        if (command.CmToolId is { Length: > 0 } selectedCmToolId)
        {
            if (cm is null)
            {
                cm = new CmContext { JobOnId = jobOn.Id, ToolId = selectedCmToolId };
                _db.CmContexts.Add(cm);
            }
            else
            {
                cm.ToolId = selectedCmToolId;
            }
        }

        MfContext? mf = jobOn.MfContext;
        if (command.MfToolId is { Length: > 0 } selectedMfToolId)
        {
            if (mf is null)
            {
                mf = new MfContext { JobOnId = jobOn.Id, ToolId = selectedMfToolId };
                _db.MfContexts.Add(mf);
            }
            else
            {
                mf.ToolId = selectedMfToolId;
            }
        }

        // O BqContext é O MESMO contexto (o bq_id não muda): só o
        // ToolId atualiza para a BQ nova — os traces/movimentos
        // existentes ficam no mesmo trace (factos históricos da
        // produção re-planeada).
        BqContext? bq = jobOn.BqContext;
        if (command.BqToolId is { Length: > 0 } selectedBqToolId)
        {
            if (bq is null)
            {
                bq = new BqContext { JobOnId = jobOn.Id, ToolId = selectedBqToolId };
                _db.BqContexts.Add(bq);
            }
            else
            {
                bq.ToolId = selectedBqToolId;
            }
        }

        // O QUE MUDOU (o proprietário, 17/10/2026): uma linha por campo
        // alterado, na mesma escrita, para a ficha mostrar em «O que mudou».
        var changes = new List<(string Field, string? Old, string? New)>();
        if (before.MachineId != machine.Id)
        {
            var oldMachine = await _db.Machines
                .AsNoTracking()
                .Where(m => m.Id == before.MachineId)
                .Select(m => m.Code)
                .FirstOrDefaultAsync(cancellationToken);
            changes.Add((JobOnChangeFields.Machine, oldMachine, machine.Code));
        }

        if (before.InicioEm != jobOn.InicioEm)
        {
            changes.Add((JobOnChangeFields.Start, FormatDay(before.InicioEm), FormatDay(jobOn.InicioEm)));
        }

        if (before.FimEm != jobOn.FimEm)
        {
            changes.Add((JobOnChangeFields.End, FormatDay(before.FimEm), FormatDay(jobOn.FimEm)));
        }

        if (before.CaloteMm != jobOn.CaloteMm)
        {
            changes.Add((JobOnChangeFields.Calote, FormatCalote(before.CaloteMm), FormatCalote(jobOn.CaloteMm)));
        }

        var toolChanges = new[]
            {
                (Field: JobOnChangeFields.Cm, Old: before.Cm, New: cm?.ToolId),
                (Field: JobOnChangeFields.Mf, Old: before.Mf, New: mf?.ToolId),
                (Field: JobOnChangeFields.Bq, Old: before.Bq, New: bq?.ToolId)
            }
            .Where(t => !string.Equals(t.Old, t.New, StringComparison.Ordinal))
            .ToList();
        if (toolChanges.Count > 0)
        {
            var ids = toolChanges.SelectMany(t => new[] { t.Old, t.New }).OfType<string>().Distinct().ToList();
            var facts = await _db.Tools
                .AsNoTracking()
                .Where(t => ids.Contains(t.ToolId))
                .Select(t => new { t.ToolId, t.Reference, t.Lot })
                .ToDictionaryAsync(t => t.ToolId, t => $"{t.Reference} · lote {t.Lot}", cancellationToken);
            foreach (var (field, oldId, newId) in toolChanges)
            {
                changes.Add((field, Describe(oldId), Describe(newId)));
            }

            string? Describe(string? toolId) =>
                toolId is null ? null : facts.TryGetValue(toolId, out var text) ? text : toolId;
        }

        var changedAt = DateTime.UtcNow;
        var changedBy = Truncate(command.ChangedBy?.Trim(), 254);
        foreach (var (field, oldValue, newValue) in changes)
        {
            _db.JobOnChanges.Add(new JobOnChange
            {
                JobOnId = jobOn.Id,
                Field = field,
                OldValueFrozen = Truncate(oldValue, JobOnChange.ValueMaxLength),
                NewValueFrozen = Truncate(newValue, JobOnChange.ValueMaxLength),
                ChangedByNameFrozen = changedBy,
                ChangedAtUtc = changedAt
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // O contexto de produção re-estabelecido — o MESMO read model
        // que a query canônica serve por jobon_id: o jobon_id
        // INALTERADO, os contextos aplicáveis com as Tools novas.
        // O AVISO INFORMATIVO do overlap (o Owner 2026-10-12: o overlap da
        // maquina deve dar um warning, nunca bloquear): a producao
        // RE-PLANEADA ja esta GRAVADA - as outras producoes da MESMA
        // maquina cuja janela de dias sobrepoe a nova janela acrescentam
        // o aviso detalhado ao outcome (as producoes em conflito, as
        // janelas de cada, o periodo exato e como resolver). NUNCA uma
        // recusa: a edicao gravou na mesma e o aviso viaja por cima do
        // sucesso.
        var others = await _db.JobOns
            .AsNoTracking()
            .Where(j => j.MachineId == machine.Id && j.Id != jobOn.Id)
            .Select(j => new OverlapCandidate(j.Id, j.Reference, j.ProductionNumber, j.InicioEm, j.FimEm))
            .ToListAsync(cancellationToken);
        var overlapWarning = JobOnOverlap.Evaluate(
            jobOn.InicioEm, jobOn.FimEm, machine.Code, others);

        return new UpdateJobOnOutcome(
            UpdateJobOnResult.Success,
            new ProductionContextReadModel(
                jobOn.Id,
                jobOn.Reference,
                jobOn.ProductionNumber,
                machine.Code,
                cm?.Id,
                cm?.ToolId,
                mf?.Id,
                mf?.ToolId,
                bq?.Id,
                bq?.ToolId,
                jobOn.InicioEm,
                jobOn.FimEm),
            overlapWarning);
    }

    private static UpdateJobOnOutcome Refused(UpdateJobOnResult result) => new(result);

    private static string? FormatDay(DateOnly? day) =>
        day?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    // Como a ficha: o valor em mm, ou a referência da Tool quando está vazia.
    private static string FormatCalote(decimal? calote) =>
        calote is { } value
            ? value.ToString("0.##", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture) + " mm"
            : "Usar referência da Tool";

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}

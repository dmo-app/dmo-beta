using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Edita um Job On existente — a produção re-planeada: a janela temporal
/// (inicio→fim), a máquina e os tool_ids dos contextos CM/MF/BQ gravam
/// numa ÚNICA escrita atómica (uma transação), SEM criar outra produção
/// — o jobon_id, a Reference e o ProductionNumber são o MESMO facto (a
/// identidade da produção é imutável na edição: o handler nem as lê).
/// Trocar a CM, o MF ou a BQ cria um cm_id, mf_id ou bq_id novo e o
/// anterior fica no registo da produção (um cm_id, mf_id ou bq_id tem
/// sempre o mesmo tool_id); um contexto que não
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

    public Task<UpdateJobOnOutcome> HandleAsync(
        UpdateJobOn command,
        CancellationToken cancellationToken = default) =>
        // Dois envios da mesma folha ao mesmo tempo (duplo clique) gravam um
        // de cada vez: o segundo espera e já lê o que o primeiro gravou.
        DbWrite.InTransactionAsync(_db, async () =>
        {
            await DbWrite.LockAsync(_db, $"jobon:editar:{command.JobOnId}", cancellationToken);
            return await HandleLockedAsync(command, cancellationToken);
        }, cancellationToken);

    private async Task<UpdateJobOnOutcome> HandleLockedAsync(
        UpdateJobOn command,
        CancellationToken cancellationToken)
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

        // Outra pessoa gravou depois de a ficha ser aberta: nada se grava por
        // cima do que ela gravou (varredura de 18/10/2026, B4). Quando o que
        // chega é o que já está gravado, é o mesmo envio outra vez (duplo
        // clique ou Enter repetido, com a versão de antes da primeira
        // gravação): segue sem nada para gravar e abre o que ficou gravado,
        // sem mensagem e sem outra linha em «O que mudou», como no Criar.
        if (command.ExpectedVersion is { } expected
            && _db.Entry(jobOn).Property<uint>(DmoDbContext.RowVersion).CurrentValue != expected
            && !IsWhatIsSaved(jobOn, command))
        {
            return Refused(UpdateJobOnResult.ChangedByAnotherUser);
        }

        // A máquina, o MF obrigatório (a regra do Owner herda na edição) e as
        // Tools escolhidas: as mesmas regras do Criar, num só sítio e com uma
        // só consulta às ferramentas (sweep de 19/10/2026, J-4).
        var checks = await JobOnCommandChecks.CheckAsync(
            _db, command.MachineId, command.CmToolId, command.MfToolId, command.BqToolId, cancellationToken);
        if (checks.Refused is { } refused)
        {
            return Refused(refused switch
            {
                JobOnCommandChecks.Refusal.MachineNotFound => UpdateJobOnResult.MachineNotFound,
                JobOnCommandChecks.Refusal.MissingMfTool => UpdateJobOnResult.MissingMfTool,
                JobOnCommandChecks.Refusal.CmToolNotFound => UpdateJobOnResult.CmToolNotFound,
                JobOnCommandChecks.Refusal.MfToolNotFound => UpdateJobOnResult.MfToolNotFound,
                _ => UpdateJobOnResult.BqToolNotFound
            });
        }

        var machine = checks.Machine!;

        // A CALOTE DESTA PRODUÇÃO (o campo j-calote da ficha — EDITÁVEL na
        // ficha): negativa é fisicamente impossível — recusa tipada ANTES
        // de qualquer escrita (0 é válido: calote plana).
        if (command.CaloteMm is < 0m)
        {
            return Refused(UpdateJobOnResult.InvalidCalote);
        }

        // O fim antes do início grava e a página avisa (Diogo, 19/10/2026:
        // «as datas têm de ser livres»; na beta.228 era recusado).

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

        // Trocar a CM é outro cm_id (Diogo, 18/10/2026; Decisão 001): o cm_id
        // é o retrato de um tool_id naquele momento. O anterior fica no
        // registo da produção por ReplacedJobOnId; a CM nova nasce num cm_id
        // novo. Um contexto que não existia nasce nesta escrita; um slot
        // null PRESERVA o contexto existente — a edição nunca destrói um facto.
        CmContext? cm = jobOn.CmContext;
        if (command.CmToolId is { Length: > 0 } selectedCmToolId
            && !string.Equals(cm?.ToolId, selectedCmToolId, StringComparison.Ordinal))
        {
            if (cm is not null)
            {
                cm.JobOnId = null;
                cm.ReplacedJobOnId = jobOn.Id;
                cm.ReplacedAtUtc = DateTime.UtcNow;
            }

            // Voltar a uma CM que esta produção já usou retoma o cm_id dela,
            // como na BQ.
            var previous = await _db.CmContexts.FirstOrDefaultAsync(
                c => c.ReplacedJobOnId == jobOn.Id && c.ToolId == selectedCmToolId, cancellationToken);
            if (previous is not null)
            {
                previous.ReplacedJobOnId = null;
                previous.ReplacedAtUtc = null;
                previous.JobOnId = jobOn.Id;
                cm = previous;
            }
            else
            {
                cm = new CmContext { JobOnId = jobOn.Id, ToolId = selectedCmToolId };
                _db.CmContexts.Add(cm);
            }

            jobOn.CmContext = cm;
        }

        // Trocar o MF é outro mf_id (Diogo, 19/10/2026: «mf_id snapshot do
        // tool_id usado para jobon_id, então alterar tool_id deve alterar
        // mf_id; depois o que associa a produção é o jobon_id»): o anterior
        // fica no registo da produção por ReplacedJobOnId; o MF novo nasce
        // num mf_id novo, como na CM e na BQ.
        MfContext? mf = jobOn.MfContext;
        if (command.MfToolId is { Length: > 0 } selectedMfToolId
            && !string.Equals(mf?.ToolId, selectedMfToolId, StringComparison.Ordinal))
        {
            if (mf is not null)
            {
                mf.JobOnId = null;
                mf.ReplacedJobOnId = jobOn.Id;
                mf.ReplacedAtUtc = DateTime.UtcNow;
            }

            // Voltar a um MF que esta produção já usou retoma o mf_id dele.
            var previous = await _db.MfContexts.FirstOrDefaultAsync(
                c => c.ReplacedJobOnId == jobOn.Id && c.ToolId == selectedMfToolId, cancellationToken);
            if (previous is not null)
            {
                previous.ReplacedJobOnId = null;
                previous.ReplacedAtUtc = null;
                previous.JobOnId = jobOn.Id;
                mf = previous;
            }
            else
            {
                mf = new MfContext { JobOnId = jobOn.Id, ToolId = selectedMfToolId };
                _db.MfContexts.Add(mf);
            }

            jobOn.MfContext = mf;
        }

        // Trocar a BQ é outro bq_id (Diogo, 18/10/2026: «um tool_id nunca
        // pode ter o mesmo bq_id, senão o histórico não faz sentido»): o
        // bq_id anterior fica com os seus movimentos, na BQ antiga, ligado
        // à produção por ReplacedJobOnId; a BQ nova nasce num bq_id novo.
        BqContext? bq = jobOn.BqContext;
        if (command.BqToolId is { Length: > 0 } selectedBqToolId
            && !string.Equals(bq?.ToolId, selectedBqToolId, StringComparison.Ordinal))
        {
            if (bq is not null)
            {
                bq.JobOnId = null;
                bq.ReplacedJobOnId = jobOn.Id;
                bq.ReplacedAtUtc = DateTime.UtcNow;
            }

            // Voltar a uma BQ que esta produção já usou retoma o bq_id dela
            // (um bq_id por BQ e produção, com um só registo de reparação):
            // a produção não aparece duas vezes no histórico da BQ.
            var previous = await _db.BqContexts.FirstOrDefaultAsync(
                c => c.ReplacedJobOnId == jobOn.Id && c.ToolId == selectedBqToolId, cancellationToken);
            if (previous is not null)
            {
                previous.ReplacedJobOnId = null;
                previous.ReplacedAtUtc = null;
                previous.JobOnId = jobOn.Id;
                bq = previous;
            }
            else
            {
                bq = new BqContext { JobOnId = jobOn.Id, ToolId = selectedBqToolId };
                _db.BqContexts.Add(bq);
            }

            jobOn.BqContext = bq;
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

        // Uma mudança só nos contextos (CM/MF/BQ) também muda a versão do Job
        // On, para a ficha aberta por outra pessoa ficar desatualizada.
        if (changes.Count > 0)
        {
            _db.Entry(jobOn).Property(j => j.MachineId).IsModified = true;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra pessoa gravou entre a leitura e esta escrita.
            return Refused(UpdateJobOnResult.ChangedByAnotherUser);
        }

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
        var overlapWarning = await JobOnOverlapQuery.EvaluateAsync(
            _db, jobOn.Id, machine.Id, machine.Code, jobOn.InicioEm, jobOn.FimEm, cancellationToken);

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

    /// <summary>
    /// O pedido é o que está gravado: a mesma máquina, as mesmas datas, a
    /// mesma calote e, em cada slot enviado, a mesma ferramenta (um slot
    /// vazio não mexe na ferramenta, por isso qualquer uma serve).
    /// </summary>
    private static bool IsWhatIsSaved(JobOn jobOn, UpdateJobOn command) =>
        jobOn.MachineId == command.MachineId
        && jobOn.InicioEm == command.InicioEm
        && jobOn.FimEm == command.FimEm
        && jobOn.CaloteMm == command.CaloteMm
        && IsSavedTool(jobOn.CmContext?.ToolId, command.CmToolId)
        && IsSavedTool(jobOn.MfContext?.ToolId, command.MfToolId)
        && IsSavedTool(jobOn.BqContext?.ToolId, command.BqToolId);

    private static bool IsSavedTool(string? saved, string? sent) =>
        sent is not { Length: > 0 } || string.Equals(saved, sent, StringComparison.Ordinal);

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

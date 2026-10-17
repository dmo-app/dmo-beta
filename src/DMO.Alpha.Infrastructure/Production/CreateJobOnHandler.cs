using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// Cria um Job On: uma ocorrência real de produção. O jobon_id, os
/// contextos aplicáveis (cm_id/mf_id/bq_id) e o controlo_id associado
/// (spec Job On L33) são emitidos pelo backend numa única escrita atómica
/// e ancorados às Tools canónicas explicitamente selecionadas — cada
/// contexto pertence a esta produção: a mesma Tool canónica pode aparecer
/// em várias produções, cada uma com o seu próprio contexto (spec Job On).
/// Só os contextos aplicáveis são criados: um slot sem seleção não cria
/// contexto. O controlo_id nasce na MESMA SaveChanges do jobon+contextos
/// (spec Controlo: o registo existe desde a criação do Job On, antes de o
/// Peso ou outra função de Controlo estar concluída) e o registo
/// Ferramentas não é mutado. O Tool.Type nunca é um gate (precedente
/// Peso/Boquilhas): o handler apenas verifica que cada tool_id selecionado
/// resolve no registo canónico — a linha DUMMY (REMOVED) NÃO é
/// selecionável (excluída por constante: os pickers nunca a mostram e o
/// backend recusa-a como ToolNotFound) — distinguir tipos é assistência
/// dos pickers, não restrição do backend. Todas as recusas são tipadas e
/// verificadas antes de qualquer escrita: uma recusa não deixa Job Ons,
/// contextos nem controles parcialmente criados.
/// </summary>
public sealed class CreateJobOnHandler : ICreateJobOnHandler
{
    private readonly DmoDbContext _db;

    public CreateJobOnHandler(DmoDbContext db)
    {
        _db = db;
    }

    public async Task<CreateJobOnOutcome> HandleAsync(
        CreateJobOn command,
        CancellationToken cancellationToken = default)
    {
        // Guardas de entrada: os factos textuais obrigatórios (colunas NOT
        // NULL) em branco são recusa tipada — nunca texto inventado. Os
        // valores persistem SEM espaços envolventes (facto limpo).
        var productionNumber = command.ProductionNumber?.Trim();
        if (string.IsNullOrEmpty(productionNumber))
        {
            return Refused(CreateJobOnResult.MissingProductionNumber);
        }

        var reference = command.Reference?.Trim();
        if (string.IsNullOrEmpty(reference))
        {
            return Refused(CreateJobOnResult.MissingReference);
        }

        // A máquina é âncora canónica persistida: um MachineId que não
        // resolve é recusa tipada — nunca é auto-selecionada outra máquina.
        var machine = await _db.Machines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == command.MachineId, cancellationToken);
        if (machine is null)
        {
            return Refused(CreateJobOnResult.MachineNotFound);
        }

        // O MF é OBRIGATÓRIO — regra do Owner 2026-10-10: «jobon necessita
        // sempre de MF, senao nao faz garrafas ou frascos». O MF dá a forma
        // do produto e a base 4-dígitos da referência (o desenho): sem ele
        // não há produção real nem identificação do produto. Recusa tipada
        // — a identidade mínima do registo de produção, nunca exceção.
        if (command.MfToolId is not { Length: > 0 })
        {
            return Refused(CreateJobOnResult.MissingMfTool);
        }

        // As Tools têm de existir no registo canónico Ferramentas — a linha
        // DUMMY (REMOVED) nunca é utilizável (recusa tipada: os pickers
        // excluem-na e o backend fecha a porta). Um slot null é simplesmente
        // não aplicável (nenhum contexto criado); um tool_id preenchido —
        // mesmo em branco — que não resolve é recusa tipada (estilo do
        // handler do Peso: caso de negócio, nunca exceção).
        if (command.CmToolId is { } cmToolId
            && !await _db.Tools.AsNoTracking().AnyAsync(
                t => t.ToolId == cmToolId
                    && t.ToolId != ToolRegistryConstants.RemovedToolId,
                cancellationToken))
        {
            return Refused(CreateJobOnResult.CmToolNotFound);
        }

        if (command.MfToolId is { } mfToolId
            && !await _db.Tools.AsNoTracking().AnyAsync(
                t => t.ToolId == mfToolId
                    && t.ToolId != ToolRegistryConstants.RemovedToolId,
                cancellationToken))
        {
            return Refused(CreateJobOnResult.MfToolNotFound);
        }

        if (command.BqToolId is { } bqToolId
            && !await _db.Tools.AsNoTracking().AnyAsync(
                t => t.ToolId == bqToolId
                    && t.ToolId != ToolRegistryConstants.RemovedToolId,
                cancellationToken))
        {
            return Refused(CreateJobOnResult.BqToolNotFound);
        }

        // O nº de produção identifica a ocorrência: o índice único
        // impede dois Job Ons com o mesmo nº.
        // A verificação prévia devolve a recusa tipada ao utilizador; o
        // índice real permanece a última linha da verdade persistida.
        var productionNumberExists = await _db.JobOns
            .AsNoTracking()
            .AnyAsync(j => j.ProductionNumber == productionNumber, cancellationToken);
        if (productionNumberExists)
        {
            return Refused(CreateJobOnResult.DuplicateProductionNumber);
        }

        // A CALOTE DESTA PRODUÇÃO (o campo j-calote da ficha): negativa é
        // fisicamente impossível — recusa tipada ANTES de qualquer escrita
        // (0 é válido: calote plana).
        if (command.CaloteMm is < 0m)
        {
            return Refused(CreateJobOnResult.InvalidCalote);
        }

        // Nova ocorrência + contextos aplicáveis, escrita atómica única.
        // Os cm_id/mf_id/bq_id nascem vinculados a ESTE jobon_id e não a
        // nenhum outro: cada produção mantém o seu próprio contexto.
        var jobOn = new JobOn
        {
            Reference = reference,
            ProductionNumber = productionNumber,
            MachineId = machine.Id,
            // A JANELA TEMPORAL (o corte B — o Owner: «em produçao é a que
            // esta hj em maquina e segue as datas do jobon»): o dia em
            // que entra/sai de máquina; os nulls = a janela em aberto (o
            // fallback é a última produção da máquina).
            InicioEm = command.InicioEm,
            FimEm = command.FimEm,
            // A CALOTE DESTA PRODUÇÃO (o campo j-calote da ficha do
            // dmo-html): null = a referência da Tool manda (o apoio
            // TP/calote do Peso usa-a automaticamente — a B1).
            CaloteMm = command.CaloteMm
        };

        CmContext? cm = null;
        MfContext? mf = null;
        BqContext? bq = null;

        if (command.CmToolId is { Length: > 0 } selectedCmToolId)
        {
            cm = new CmContext { JobOn = jobOn, ToolId = selectedCmToolId };
        }

        if (command.MfToolId is { Length: > 0 } selectedMfToolId)
        {
            mf = new MfContext { JobOn = jobOn, ToolId = selectedMfToolId };
        }

        if (command.BqToolId is { Length: > 0 } selectedBqToolId)
        {
            bq = new BqContext { JobOn = jobOn, ToolId = selectedBqToolId };
        }

        _db.JobOns.Add(jobOn);
        if (cm is not null)
        {
            _db.CmContexts.Add(cm);
        }

        if (mf is not null)
        {
            _db.MfContexts.Add(mf);
        }

        if (bq is not null)
        {
            _db.BqContexts.Add(bq);
        }

        // O controlo_id da produção nasce na MESMA escrita atómica do
        // jobon_id e dos contextos aplicáveis (spec Job On L33: «When a
        // Job On is created, the associated controlo_id is also created»;
        // spec Controlo L9: o registo existe desde a criação do Job On,
        // antes de o Peso ou outra função estar concluída). É a mesma
        // escrita, não um segundo ato: a produção fica com o seu
        // controlo_id garantido desde o primeiro instante persistido.
        // (O nome da entidade é qualificado: «Controlo» é também o
        // segmento do namespace em qualquer ficheiro descendente.)
        _db.Controlos.Add(new DMO.Alpha.Core.Controlo.Controlo { JobOn = jobOn });

        await _db.SaveChangesAsync(cancellationToken);

        // O AVISO INFORMATIVO do overlap (o Owner 2026-10-12: «overlap da
        // maquina deve dar um warning, nao deve bloquear, o warning deve
        // dizer detalhadamente o que esta mal como resolver»): a produção
        // já está GRAVADA — as outras produções da MESMA máquina cuja
        // janela de dias sobrepõe a desta acrescentam o aviso detalhado
        // ao outcome (as produções em conflito, as janelas, o período
        // exato e como resolver). NUNCA uma recusa, nunca um bloqueio:
        // o aviso viaja por cima do sucesso.
        var overlapWarning = await EvaluateOverlapAsync(
            jobOn.Id, machine.Id, machine.Code, jobOn.InicioEm, jobOn.FimEm, cancellationToken);

        // O contexto de produção estabelecido, já com as identidades
        // emitidas — o MESMO read model que a query canónica serve aos
        // consumidores por jobon_id.
        return new CreateJobOnOutcome(
            CreateJobOnResult.Success,
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
                bq?.ToolId),
            overlapWarning);
    }

    /// <summary>
    /// As outras produções da mesma máquina (a janela de cada) contra a
    /// janela gravada — a calculadora canónica JobOnOverlap decide se há
    /// sobreposição a assinalar (a janela inteiramente em aberto não
    /// afirma período nenhum: nada a comparar).
    /// </summary>
    private async Task<OverlapWarning?> EvaluateOverlapAsync(
        int jobOnId,
        int machineId,
        string machineCode,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        CancellationToken cancellationToken)
    {
        var others = await _db.JobOns
            .AsNoTracking()
            .Where(j => j.MachineId == machineId && j.Id != jobOnId)
            .Select(j => new OverlapCandidate(j.Id, j.Reference, j.ProductionNumber, j.InicioEm, j.FimEm))
            .ToListAsync(cancellationToken);

        return JobOnOverlap.Evaluate(inicioEm, fimEm, machineCode, others);
    }

    private static CreateJobOnOutcome Refused(CreateJobOnResult result) => new(result);
}

using DMO.Alpha.Core.Production;
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
    private readonly TimeProvider _relogio;

    public CreateJobOnHandler(DmoDbContext db, TimeProvider? relogio = null)
    {
        _db = db;
        _relogio = relogio ?? TimeProvider.System;
    }

    // Dois envios ao mesmo tempo (duplo clique) passam os dois a
    // verificação prévia; o segundo bate no índice único. A segunda
    // tentativa volta a verificar e devolve a recusa tipada
    // («essa produção já existe»), nunca uma página de erro.
    public Task<CreateJobOnOutcome> HandleAsync(
        CreateJobOn command,
        CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(_db, () => HandleOnceAsync(command, cancellationToken));

    private async Task<CreateJobOnOutcome> HandleOnceAsync(
        CreateJobOn command,
        CancellationToken cancellationToken)
    {
        // Guardas de entrada: os factos textuais obrigatórios (colunas NOT
        // NULL) em branco são recusa tipada — nunca texto inventado. Os
        // valores persistem SEM espaços envolventes (facto limpo).
        var productionNumber = command.ProductionNumber?.Trim();
        // A EXPERIÊNCIA (Diogo, 26/10/2026): o número é sempre AAAA00 (o ano do
        // dia em que entra) e entra e sai no mesmo dia.
        var inicioEm = command.InicioEm;
        var fimEm = command.FimEm;
        if (command.Experiencia)
        {
            // DATA-B01 (sweep .230): sem data dada, o dia é o de Lisboa, não
            // o UTC (00:30 de Lisboa no verão é ainda a véspera em UTC).
            inicioEm ??= DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(_relogio.GetUtcNow()));
            fimEm = inicioEm;
            productionNumber = $"{inicioEm.Value.Year}00";
        }

        if (string.IsNullOrEmpty(productionNumber))
        {
            return Refused(CreateJobOnResult.MissingProductionNumber);
        }

        // A referência guarda-se sempre em maiúsculas (a página já o fazia;
        // o handler garante-o para o índice único, que distingue maiúsculas).
        var reference = command.Reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(reference))
        {
            return Refused(CreateJobOnResult.MissingReference);
        }

        // A máquina, o MF obrigatório e as Tools escolhidas: as mesmas regras
        // do Editar, num só sítio e com uma só consulta às ferramentas
        // (sweep de 19/10/2026, J-4). A linha DUMMY (REMOVED) nunca é
        // utilizável: os pickers excluem-na e o backend fecha a porta.
        var checks = await JobOnCommandChecks.CheckAsync(
            _db, command.MachineId, command.CmToolId, command.MfToolId, command.BqToolId, cancellationToken);
        if (checks.Refused is { } refused)
        {
            return Refused(refused switch
            {
                JobOnCommandChecks.Refusal.MachineNotFound => CreateJobOnResult.MachineNotFound,
                JobOnCommandChecks.Refusal.ToolMachineMismatch => CreateJobOnResult.ToolMachineMismatch,
                JobOnCommandChecks.Refusal.MissingMfTool => CreateJobOnResult.MissingMfTool,
                JobOnCommandChecks.Refusal.CmToolNotFound => CreateJobOnResult.CmToolNotFound,
                JobOnCommandChecks.Refusal.MfToolNotFound => CreateJobOnResult.MfToolNotFound,
                _ => CreateJobOnResult.BqToolNotFound
            });
        }

        var machine = checks.Machine!;

        // A produção identifica-se pela referência JUNTO com o nº: o mesmo
        // nº (ex.: 202601) existe em várias referências (Owner 17/10/2026:
        // «202601 de 9389T184 não pode bloquear 1075C142»). O índice único
        // (Reference, ProductionNumber) é a última linha da verdade; a
        // verificação prévia devolve a recusa tipada e ignora maiúsculas.
        var referenceUpper = reference.ToUpper();
        var productionNumberExists = await _db.JobOns
            .AsNoTracking()
            .AnyAsync(j => j.ProductionNumber == productionNumber
                && j.Reference.ToUpper() == referenceUpper, cancellationToken);
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

        if (command.TampaoDiametroMm is <= 0m)
        {
            return Refused(CreateJobOnResult.InvalidTampao);
        }

        // A FOLHA DA FÁBRICA: os textos normalizados; um texto acima do
        // máximo do catálogo é a única recusa da folha (um dado que não cabe),
        // antes de qualquer escrita. Nenhum campo é obrigatório.
        var folha = JobOnFolhaGravacao.De(command.Folha, command.Cal);
        if (folha.ChaveLonga is { } chaveLonga)
        {
            return new CreateJobOnOutcome(CreateJobOnResult.TextoLongo, ChaveRecusada: chaveLonga);
        }

        // O fim antes do início grava e a página avisa (Diogo, 19/10/2026:
        // «as datas têm de ser livres»; na beta.228 era recusado).

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
            InicioEm = inicioEm,
            FimEm = fimEm,
            Experiencia = command.Experiencia,
            // A CALOTE DESTA PRODUÇÃO (o campo j-calote da ficha do
            // dmo-html): null = a referência da Tool manda (o apoio
            // TP/calote do Peso usa-a automaticamente — a B1).
            CaloteMm = command.CaloteMm,
            TampaoDiametroMm = command.TampaoDiametroMm,
            CriadoEmUtc = DateTime.UtcNow
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

        // A folha e as linhas CAL nascem na mesma escrita do Job On.
        folha.Criar(_db, jobOn);

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
    private Task<OverlapWarning?> EvaluateOverlapAsync(
        int jobOnId,
        int machineId,
        string machineCode,
        DateOnly? inicioEm,
        DateOnly? fimEm,
        CancellationToken cancellationToken)
        => JobOnOverlapQuery.EvaluateAsync(_db, jobOnId, machineId, machineCode, inicioEm, fimEm, cancellationToken);

    private static CreateJobOnOutcome Refused(CreateJobOnResult result) => new(result);
}

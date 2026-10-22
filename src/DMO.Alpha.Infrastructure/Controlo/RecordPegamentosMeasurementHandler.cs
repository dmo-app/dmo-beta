using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// Regista uma leitura dimensional real (measurement row) no controlo de
/// uma produção: o gesto em cruz (costura 0° + contra costura 90° quando a
/// geometria real a permite) num componente da combinação CM+BQ+MF do
/// contexto Job On ativo. A tool do componente é HERDADA do contexto de
/// produção persistido — o operador nunca seleciona ferramentas na folha
/// (spec L92) e um componente que o contexto não tem é recusa tipada
/// (nenhuma tool inventada). A primeira medição de um controlo cria o
/// registo durável (pegamentos_id) — a ausência era o estado válido até
/// então — congelando nele as tools da combinação herdadas do contexto e
/// a tolerância efetiva do corredor (configuração do Controlo ?? default
/// do Owner ±0,20); as medições seguintes usam o registo da MESMA
/// combinação CM+BQ+MF, com a MESMA tolerância congelada. Depois de trocar
/// a CM, a BQ ou o MF a meio da produção, a primeira leitura começa uma
/// folha nova com as ferramentas novas e a tolerância corrente; voltar a
/// uma combinação que a produção já usou retoma a folha dela (
/// 18/10/2026). A folha anterior fica como estava. O gargalo em falta fornecido no
/// fluxo é completado no tool_id owner (regra Ferramentas/spec L184:
/// gravam no contexto técnico da própria Tool, nunca como facto do
/// registo de Pegamentos; valores já registados nunca são sobrescritos —
/// completion, não correção silenciosa) e o nominal consumido por ESTA
/// linha é congelado na própria linha como evidência histórica (null =
/// NotEvaluable: sem corredor para o componente, nenhum nominal inventado).
/// A leitura é acrescentada como facto histórico append-only; os valores
/// derivados (ovalização com sinal, média, corredor) são calculados pelo
/// backend a partir dos factos congelados. Todas as recusas são tipadas e
/// verificadas antes de qualquer escrita: uma recusa não deixa values
/// técnicos completados nem medições/registos parcialmente criados.
/// </summary>
public sealed class RecordPegamentosMeasurementHandler : IRecordPegamentosMeasurementHandler
{
    private readonly DmoDbContext _db;
    private readonly IPegamentosToleranceConfiguration _toleranceConfiguration;

    public RecordPegamentosMeasurementHandler(
        DmoDbContext db,
        IPegamentosToleranceConfiguration toleranceConfiguration)
    {
        _db = db;
        _toleranceConfiguration = toleranceConfiguration;
    }

    // Duas leituras gravadas ao mesmo tempo colidem na ficha técnica criada
    // para o gargalo: a segunda volta a correr.
    public Task<RecordPegamentosMeasurementOutcome> HandleAsync(
        RecordPegamentosMeasurement command,
        CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(_db, () =>
            DbWrite.InTransactionAsync(_db, () => HandleOnceAsync(command, cancellationToken), cancellationToken));

    private async Task<RecordPegamentosMeasurementOutcome> HandleOnceAsync(
        RecordPegamentosMeasurement command,
        CancellationToken cancellationToken)
    {
        // 1. Token do componente: um dos canónicos (CM/BQ/MF) — recusa
        //    tipada, nunca exceção.
        if (command.ComponentToken is not ("CM" or "BQ" or "MF"))
        {
            return Refused(RecordPegamentosMeasurementResult.UnknownComponent);
        }
        var component = command.ComponentToken == "CM"
            ? PegamentosComponent.Cm
            : command.ComponentToken == "BQ" ? PegamentosComponent.Bq
            : PegamentosComponent.Mf;

        // Uma costura ou contra costura de 0 ou menos é fisicamente
        // impossível: nenhuma linha do envio é gravada (Diogo, 18/10/2026).
        if (new[] { new PegamentosRow(command.CosturaMm, command.ContraCosturaMm) }
            .Concat(command.AdditionalRows ?? [])
            .Any(row => row.CosturaMm <= 0m || row.ContraCosturaMm is <= 0m))
        {
            return Refused(RecordPegamentosMeasurementResult.InvalidMeasurementValue);
        }

        // 2–4. O registo e a tool do componente. Pelo pegamentos_id (os
        //    Pegamentos sem Job On, ou já associados): as ferramentas são
        //    as guardadas no registo. Pelo controlo_id: a tool é a do
        //    contexto do Job On e a folha é a da combinação atual, que nasce
        //    na primeira medição com essas ferramentas.
        DMO.Alpha.Core.Controlo.Pegamentos? registro;
        string? componentToolId;
        int? recordControloId;
        var startedNewSheet = false;
        if (command.PegamentosId is int pegamentosId)
        {
            registro = await _db.Pegamentos.FirstOrDefaultAsync(p => p.Id == pegamentosId, cancellationToken);
            if (registro is null)
            {
                return Refused(RecordPegamentosMeasurementResult.PegamentosNotFound);
            }

            // Uma folha associada a uma produção que entretanto trocou a CM,
            // a BQ ou o MF fica como estava: as leituras novas vão para a
            // folha das ferramentas atuais (18/10/2026).
            if (registro.ControloId is int associatedControloId
                && await PegamentosSheets.CurrentCombinationAsync(_db, associatedControloId, cancellationToken) is { } associatedCurrent
                && !associatedCurrent.Matches(registro.CmToolId, registro.BqToolId, registro.MfToolId))
            {
                return Refused(RecordPegamentosMeasurementResult.PegamentosNotCurrent);
            }

            componentToolId = component switch
            {
                PegamentosComponent.Cm => registro.CmToolId,
                PegamentosComponent.Bq => registro.BqToolId,
                _ => registro.MfToolId
            };
            // A ferramenta eliminada (REMOVED) não recebe medições nem ficha
            // técnica: o componente deixou de existir.
            if (componentToolId is null or ToolRegistryConstants.RemovedToolId)
            {
                return Refused(RecordPegamentosMeasurementResult.ComponentNotInProduction);
            }

            recordControloId = registro.ControloId;
        }
        else
        {
            // 2. O controlo tem de existir: a folha vive sob o controlo_id de
            //    uma produção real — nenhum registo é criado para um
            //    controlo_id inexistente.
            var controloExists = await _db.Controlos
                .AsNoTracking()
                .AnyAsync(c => c.Id == command.ControloId, cancellationToken);
            if (!controloExists)
            {
                return Refused(RecordPegamentosMeasurementResult.ControloNotFound);
            }

            // Duas primeiras leituras ao mesmo tempo criavam duas folhas
            // (18/10/2026): o trinco do controlo faz a segunda esperar e
            // encontrar a folha que a primeira criou. A combinação lê-se
            // depois do trinco, uma vez: a tool da leitura e a folha são da
            // mesma combinação (uma troca a meio já não as separa).
            await DbWrite.LockAsync(_db, $"pegamentos:controlo:{command.ControloId}", cancellationToken);
            var current = (await PegamentosSheets.CurrentCombinationAsync(_db, command.ControloId, cancellationToken))!;

            // 3. A tool do componente é HERDADA do contexto de produção
            //    persistido (cm_id/bq_id/mf_id → tool_id do Job On deste
            //    controlo): um componente sem contexto nesta produção é
            //    recusa tipada — nunca inventado nem re-selecionado.
            componentToolId = current.ToolOf(component);
            if (componentToolId is null or ToolRegistryConstants.RemovedToolId)
            {
                return Refused(RecordPegamentosMeasurementResult.ComponentNotInProduction);
            }

            // 4. A folha da combinação atual (a mais antiga, se a mesma
            //    combinação tiver duas — ferramentas eliminadas ficam todas
            //    REMOVED): o caso normal é UMA por produção (spec L22-24),
            //    com a tolerância congelada nela. Na sua ausência cria-se
            //    AGORA, na primeira medição desta combinação, congelando as
            //    tools herdadas do contexto e a tolerância efetiva corrente.
            //    Nenhuma unicidade material sobre ControloId: a folha nova
            //    depois de uma troca é outro pegamentos_id e a anterior fica
            //    como estava.
            registro = await _db.Pegamentos
                .OfCombination(command.ControloId, current)
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (registro is null)
            {
                // CORTE A (exceção aprovada): os FATOS HUMANOS das tools da
                // combinação (referência/lote) são congelados À CRIAÇÃO do
                // registo — as âncoras humanas vivem no REGISTO, não nas
                // medições; um componente que o contexto não tem congela null.
                startedNewSheet = await _db.Pegamentos
                    .AsNoTracking()
                    .AnyAsync(p => p.ControloId == command.ControloId, cancellationToken);
                registro = await PegamentosRecordFactory.AddAsync(
                    _db, _toleranceConfiguration, command.ControloId,
                    current.CmToolId, current.BqToolId, current.MfToolId, cancellationToken);
            }

            recordControloId = command.ControloId;
        }

        // Registar e apagar na mesma folha ao mesmo tempo (dois separadores)
        // deixavam um buraco nas posições (deep sweep 18/10/2026): o registar
        // e o apagar/renumerar esperam um pelo outro no trinco da folha.
        if (registro.Id != 0)
        {
            await LockSheetAsync(_db, registro.Id, cancellationToken);
        }

        // Tolerância CONGELADA no registo: uma alteração posterior da
        // configuração não reescreve as avaliações deste registo
        // (CONFIGURATION.md L47-51).
        var toleranceMm = registro.ToleranceMm;

        // 5. Completion do gargalo (apenas valores EM FALTA, no tool_id
        //    owner — regra Ferramentas): o comando transporta o que o
        //    operador completou no fluxo da folha; um valor que a Tool já
        //    tem registado não é substituído por este caminho.
        var technical = await _db.ToolTechnicalContexts
            .FirstOrDefaultAsync(t => t.ToolId == componentToolId, cancellationToken);

        if (technical is null && command.GargaloMm is { } missingGargalo)
        {
            technical = new ToolTechnicalContext { ToolId = componentToolId, DiametroGargaloMm = missingGargalo };
            _db.ToolTechnicalContexts.Add(technical);
        }
        else if (technical is not null)
        {
            technical.DiametroGargaloMm ??= command.GargaloMm;
        }

        // 6. O nominal consumido por ESTA linha: o registo técnico da Tool
        //    depois da completion (null quando a Tool não tem gargalo nem
        //    foi completado — a linha fica NotEvaluable, estado válido).
        //    Nenhum nominal é inventado.
        var nominalGargalo = technical?.DiametroGargaloMm;

        // Sem nominal do gargalo não há Pegamentos (Diogo, 17/10/2026): nada
        // é gravado, nem o registo nem a ferramenta.
        if (nominalGargalo is null)
        {
            return Refused(RecordPegamentosMeasurementResult.MissingGargalo);
        }

        // 7. A leitura é acrescentada como facto histórico append-only: a
        //    posição física é atribuída pelo backend pela ordem de registo
        //    dentro do registo; os eixos observados são registados na
        //    totalidade (contra costura NULL é o estado válido do eixo
        //    único — nunca bloqueado, nunca fabricado). A ligação ao
        //    registo é pela navegação: o registo recém-criado ainda não tem
        //    pegamentos_id gerado — o backend emite-o na escrita e o EF
        //    propaga-o à leitura. A seguir à última (não à contagem): uma
        //    folha que ficou com um buraco nas posições continua a gravar.
        var nextRowPosition = (await _db.PegamentosMeasurements
            .AsNoTracking()
            .Where(m => m.PegamentosId == registro.Id)
            .MaxAsync(m => (int?)m.RowPosition, cancellationToken) ?? 0) + 1;

        var measurement = new PegamentosMeasurement
        {
            Pegamentos = registro,
            RowPosition = nextRowPosition,
            Component = component,
            CosturaMm = command.CosturaMm,
            ContraCosturaMm = command.ContraCosturaMm,
            NominalGargaloMm = nominalGargalo,
            NumeroFerramenta = NumeroFerramenta(command.NumeroFerramenta)
        };
        _db.PegamentosMeasurements.Add(measurement);

        // As outras linhas do mesmo envio («+ Linha»), a seguir, com o mesmo
        // nominal: gravam-se no mesmo SaveChanges que a primeira, para um
        // pedido cortado a meio não deixar só parte (deep sweep 18/10/2026).
        foreach (var (row, i) in (command.AdditionalRows ?? []).Select((row, i) => (row, i)))
        {
            _db.PegamentosMeasurements.Add(new PegamentosMeasurement
            {
                Pegamentos = registro,
                RowPosition = nextRowPosition + 1 + i,
                Component = component,
                CosturaMm = row.CosturaMm,
                ContraCosturaMm = row.ContraCosturaMm,
                NominalGargaloMm = nominalGargalo,
                NumeroFerramenta = NumeroFerramenta(row.NumeroFerramenta)
            });
        }

        // 8. Valores derivados da leitura, calculados pelo backend com os
        //    factos congelados (nominal da linha + tolerância do registo) —
        //    nunca fornecidos pelo cliente.
        var evaluation = PegamentosCalculation.EvaluateRow(
            nominalGargalo,
            toleranceMm,
            measurement.CosturaMm,
            measurement.ContraCosturaMm);

        await _db.SaveChangesAsync(cancellationToken);

        return new RecordPegamentosMeasurementOutcome(
            RecordPegamentosMeasurementResult.Success,
            new RecordedPegamentosMeasurement(
                measurement.Id,
                registro.Id,
                recordControloId,
                measurement.RowPosition,
                component,
                measurement.CosturaMm,
                measurement.ContraCosturaMm,
                nominalGargalo,
                evaluation)
            {
                StartedNewSheet = startedNewSheet
            });
    }

    /// <summary>
    /// O trinco da folha (pegamentos_id), até ao fim da transação: registar
    /// e apagar leituras da mesma folha correm um de cada vez.
    /// </summary>
    internal static Task LockSheetAsync(DmoDbContext db, int pegamentosId, CancellationToken cancellationToken) =>
        DbWrite.LockAsync(db, $"pegamentos:registo:{pegamentosId}", cancellationToken);

    private static RecordPegamentosMeasurementOutcome Refused(RecordPegamentosMeasurementResult result) =>
        new(result);

    /// <summary>O número da ferramenta como se grava: sem espaços à volta, vazio = null, até 20 caracteres.</summary>
    internal static string? NumeroFerramenta(string? numero)
    {
        var limpo = numero?.Trim();
        return string.IsNullOrEmpty(limpo) ? null : limpo.Length > 20 ? limpo[..20].TrimEnd() : limpo;
    }
}

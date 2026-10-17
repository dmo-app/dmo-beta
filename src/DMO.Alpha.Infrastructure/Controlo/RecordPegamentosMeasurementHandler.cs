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
/// do Owner ±0,20); as medições seguinte usam SEMPRE o MESMO registo do
/// controlo com a MESMA tolerância congelada (o caminho do registo
/// adicional explícito é slice futuro). O gargalo em falta fornecido no
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

    // Duas leituras gravadas ao mesmo tempo colidem na posição (ou na
    // ficha técnica criada para o gargalo): a segunda volta a correr.
    public Task<RecordPegamentosMeasurementOutcome> HandleAsync(
        RecordPegamentosMeasurement command,
        CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(_db, () => HandleOnceAsync(command, cancellationToken));

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

        // 2–4. O registo e a tool do componente. Pelo pegamentos_id (os
        //    Pegamentos sem Job On, ou já associados): as ferramentas são
        //    as guardadas no registo. Pelo controlo_id: a tool é a do
        //    contexto do Job On e o registo nasce na primeira medição.
        DMO.Alpha.Core.Controlo.Pegamentos? registro;
        string? componentToolId;
        int? recordControloId;
        if (command.PegamentosId is int pegamentosId)
        {
            registro = await _db.Pegamentos.FirstOrDefaultAsync(p => p.Id == pegamentosId, cancellationToken);
            if (registro is null)
            {
                return Refused(RecordPegamentosMeasurementResult.PegamentosNotFound);
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
            var controlo = await _db.Controlos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == command.ControloId, cancellationToken);
            if (controlo is null)
            {
                return Refused(RecordPegamentosMeasurementResult.ControloNotFound);
            }

            // 3. A tool do componente é HERDADA do contexto de produção
            //    persistido (cm_id/bq_id/mf_id → tool_id do Job On deste
            //    controlo): um componente sem contexto nesta produção é
            //    recusa tipada — nunca inventado nem re-selecionado.
            componentToolId = await ResolveComponentToolIdAsync(controlo.JobOnId, component, cancellationToken);
            if (componentToolId is null or ToolRegistryConstants.RemovedToolId)
            {
                return Refused(RecordPegamentosMeasurementResult.ComponentNotInProduction);
            }

            // 4. O registo durável do controlo: o caso normal é UM por
            //    produção (spec L22-24) — usa-se o registo existente (com a
            //    tolerância congelada nele); na sua ausência cria-se AGORA,
            //    na primeira medição, congelando as tools da combinação
            //    herdadas do contexto e a tolerância efetiva corrente.
            //    Nenhuma unicidade material sobre ControloId: um adicional
            //    EXPLÍCITO é permitido pela spec mas é slice futuro — este
            //    caminho nunca cria silenciosamente um segundo registo.
            registro = await _db.Pegamentos
                .Where(p => p.ControloId == controlo.Id)
                .OrderBy(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (registro is null)
            {
                // CORTE A (exceção aprovada): os FATOS HUMANOS das tools da
                // combinação (referência/lote) são congelados À CRIAÇÃO do
                // registo — as âncoras humanas vivem no REGISTO, não nas
                // medições; um componente que o contexto não tem congela null.
                var (cmToolId, bqToolId, mfToolId) = await ResolveCombinationToolsAsync(controlo.JobOnId, cancellationToken);
                registro = await PegamentosRecordFactory.AddAsync(
                    _db, _toleranceConfiguration, controlo.Id, cmToolId, bqToolId, mfToolId, cancellationToken);
            }

            recordControloId = controlo.Id;
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
        //    propaga-o à leitura.
        var nextRowPosition = await _db.PegamentosMeasurements
            .AsNoTracking()
            .CountAsync(m => m.PegamentosId == registro.Id, cancellationToken) + 1;

        var measurement = new PegamentosMeasurement
        {
            Pegamentos = registro,
            RowPosition = nextRowPosition,
            Component = component,
            CosturaMm = command.CosturaMm,
            ContraCosturaMm = command.ContraCosturaMm,
            NominalGargaloMm = nominalGargalo
        };
        _db.PegamentosMeasurements.Add(measurement);

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
                evaluation));
    }

    /// <summary>
    /// A tool_id do componente no contexto do Job On (cm_id → CM tool_id,
    /// bq_id → BQ tool_id, mf_id → MF tool_id — spec L146-148), ou null
    /// quando o contexto desta produção não tem o componente.
    /// </summary>
    private async Task<string?> ResolveComponentToolIdAsync(
        int jobOnId,
        PegamentosComponent component,
        CancellationToken cancellationToken) =>
        component switch
        {
            PegamentosComponent.Cm => await _db.CmContexts
                .AsNoTracking()
                .Where(c => c.JobOnId == jobOnId)
                .Select(c => c.ToolId)
                .FirstOrDefaultAsync(cancellationToken),
            PegamentosComponent.Bq => await _db.BqContexts
                .AsNoTracking()
                .Where(c => c.JobOnId == jobOnId)
                .Select(c => c.ToolId)
                .FirstOrDefaultAsync(cancellationToken),
            PegamentosComponent.Mf => await _db.MfContexts
                .AsNoTracking()
                .Where(c => c.JobOnId == jobOnId)
                .Select(c => c.ToolId)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };

    /// <summary>
    /// As tools da combinação CM+BQ+MF herdadas do contexto do Job On no
    /// momento da criação do registo (null nos componentes que o contexto
    /// não tem — o estado real: o J10 da base real não tem MF).
    /// </summary>
    private async Task<(string? CmToolId, string? BqToolId, string? MfToolId)> ResolveCombinationToolsAsync(
        int jobOnId,
        CancellationToken cancellationToken)
    {
        var cmToolId = await _db.CmContexts
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOnId)
            .Select(c => c.ToolId)
            .FirstOrDefaultAsync(cancellationToken);

        var bqToolId = await _db.BqContexts
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOnId)
            .Select(c => c.ToolId)
            .FirstOrDefaultAsync(cancellationToken);

        var mfToolId = await _db.MfContexts
            .AsNoTracking()
            .Where(c => c.JobOnId == jobOnId)
            .Select(c => c.ToolId)
            .FirstOrDefaultAsync(cancellationToken);

        return (cmToolId, bqToolId, mfToolId);
    }

    private static RecordPegamentosMeasurementOutcome Refused(RecordPegamentosMeasurementResult result) =>
        new(result);
}

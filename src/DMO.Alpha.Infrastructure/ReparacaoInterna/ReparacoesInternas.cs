using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.ReparacaoInterna;

/// <summary>
/// A Reparação Interna sobre o <see cref="DmoDbContext"/> (blueprint
/// reparacao-interna/OVERVIEW §§1–3). A produção do dia segue a regra das
/// Boquilhas, com a hora de viragem própria da Reparação Interna (Canal
/// Jobon, 8 por omissão) e sem o recurso às produções sem datas (DP-19).
/// Registar grava a reparação e a operação registada (envio único) na mesma
/// transação e não mexe em mais nada: nem na ferramenta, nem no Job On, nem
/// nas Boquilhas, nem no Armazém. Corrigir (só o número) e anular gravam a
/// reparação, a linha do rasto e a operação registada na mesma transação;
/// qualquer pessoa com o módulo o pode fazer (RI-04), e o rasto diz quem.
/// A ferramenta escrita (peça 4): com outra CM/MF na máquina, ou sem produção
/// para o dia, grava-se a referência e o lote escritos, sem contexto (e sem
/// Job On, se não houver), e o responsável associa depois a ferramenta, com
/// rasto e as mesmas regras de envio único e de concorrência.
/// </summary>
public sealed class ReparacoesInternas(
    DmoDbContext db,
    IModuleSettingsQuery settings,
    IOperacoesRegistadas operacoes,
    ICurrentAccountContext account,
    TimeProvider? clock = null) : IReparacoesInternas
{
    public const string OperacaoTipo = "ri.reparacao";

    public const string ResultadoTipo = "Reparacao";

    public const string OperacaoCorrigir = "ri.corrigir";

    public const string OperacaoAnular = "ri.anular";

    public const string OperacaoAssociar = "ri.associar";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<DateOnly> DiaOperacionalAsync(CancellationToken cancellationToken = default)
    {
        var configurada = await settings.FindValueAsync(ModuleSettingKeys.ReparacaoInternaDayChangeHour, cancellationToken);
        var hora = int.TryParse(configurada, out var h) && h is >= 0 and <= 23 ? h : 8;
        return BoquilhasDayChange.ResolveOperationalDay(DmoBusinessTime.ToLocal(_clock.GetUtcNow().UtcDateTime), hora);
    }

    public async Task<RiProducaoDoDia> ProducaoDoDiaAsync(int machineId, CancellationToken cancellationToken = default) =>
        await ProducaoDoDiaAsync(machineId, await DiaOperacionalAsync(cancellationToken), cancellationToken);

    public async Task<RiProducaoDoDia> ProducaoDoDiaAsync(int machineId, DateOnly dia, CancellationToken cancellationToken = default)
    {
        // Numa consulta: as produções da máquina cujas datas cobrem o dia (sem
        // fim, conta desde o início), com o cm_id e o mf_id em uso e a
        // referência e o lote das ferramentas. Uma produção sem datas não conta.
        var ferramentas = db.Tools.AsNoTracking().Where(t => t.ToolId != ToolRegistryConstants.RemovedToolId);
        var linhas = await (
            from j in db.JobOns.AsNoTracking()
            where j.MachineId == machineId
                && j.InicioEm != null
                && j.InicioEm <= dia
                && (j.FimEm == null || j.FimEm >= dia)
            join c in db.CmContexts.AsNoTracking() on (int?)j.Id equals c.JobOnId into cms
            from c in cms.DefaultIfEmpty()
            join tc in ferramentas on (c != null ? c.ToolId : null) equals tc.ToolId into tcs
            from tc in tcs.DefaultIfEmpty()
            join m in db.MfContexts.AsNoTracking() on (int?)j.Id equals m.JobOnId into mfs
            from m in mfs.DefaultIfEmpty()
            join tm in ferramentas on (m != null ? m.ToolId : null) equals tm.ToolId into tms
            from tm in tms.DefaultIfEmpty()
            orderby j.Id descending
            select new
            {
                j.Id,
                j.ProductionNumber,
                j.Reference,
                j.InicioEm,
                j.FimEm,
                CmId = c != null ? c.Id : (int?)null,
                CmReferencia = tc != null ? tc.Reference : null,
                CmLote = tc != null ? tc.Lot : null,
                CmToolId = tc != null ? tc.ToolId : null,
                MfId = m != null ? m.Id : (int?)null,
                MfReferencia = tm != null ? tm.Reference : null,
                MfLote = tm != null ? tm.Lot : null,
                MfToolId = tm != null ? tm.ToolId : null
            })
            .ToListAsync(cancellationToken);

        return new RiProducaoDoDia(dia, linhas
            .Select(l => new RiProducao(
                l.Id,
                l.ProductionNumber,
                l.Reference,
                l.InicioEm!.Value,
                l.FimEm,
                l.CmId is { } cm ? new RiFerramenta(cm, l.CmReferencia, l.CmLote) : null,
                l.MfId is { } mf ? new RiFerramenta(mf, l.MfReferencia, l.MfLote) : null,
                l.CmToolId,
                l.MfToolId))
            .ToList());
    }

    public async Task<ReparacaoSaida> RegistarAsync(PedidoDeReparacao pedido, CancellationToken cancellationToken = default)
    {
        // O mesmo envio outra vez (duplo Enter, resposta perdida): já está.
        if (await JaRegistadoAsync(pedido.OperacaoId, cancellationToken) is { } repetido)
        {
            return repetido;
        }

        var numero = (pedido.Numero ?? string.Empty).Trim();
        if (numero.Length == 0)
        {
            return new ReparacaoSaida(ReparacaoResultado.SemNumero);
        }

        if (numero.Length > Reparacao.NumeroMaxLength)
        {
            return new ReparacaoSaida(ReparacaoResultado.NumeroLongo, Numero: numero);
        }

        if (pedido.Tipo is not { } tipo)
        {
            return new ReparacaoSaida(ReparacaoResultado.SemTipo, Numero: numero);
        }

        if (pedido.MachineId is not { } machineId
            || !await db.Machines.AsNoTracking().AnyAsync(m => m.Id == machineId, cancellationToken))
        {
            return new ReparacaoSaida(ReparacaoResultado.MaquinaNaoExiste, Numero: numero);
        }

        // A produção e a ferramenta outra vez, agora: se mudaram desde que a
        // página abriu, nada se grava e a página mostra o que está agora,
        // com o número escrito. Sem produção para o dia, a reparação fica
        // sem Job On, com a ferramenta escrita (Diogo, 21/10/2026); se a
        // página mostrou uma produção que já não conta, mudou.
        var agora = _clock.GetUtcNow().UtcDateTime;
        // O dia escolhido na página (as datas são livres: só avisa); sem ele, o de agora.
        var doDia = await ProducaoDoDiaAsync(machineId, pedido.Dia ?? await DiaOperacionalAsync(cancellationToken), cancellationToken);
        RiProducao? producao = null;
        if (doDia.SemProducao)
        {
            if (pedido.JobOnId is not null)
            {
                return new ReparacaoSaida(ReparacaoResultado.ProducaoMudou, Numero: numero);
            }
        }
        else
        {
            producao = pedido.JobOnId is { } pedida
                ? doDia.Candidatas.FirstOrDefault(c => c.JobOnId == pedida)
                : doDia.Escolhida(null);
            if (producao is null)
            {
                return new ReparacaoSaida(ReparacaoResultado.ProducaoMudou, Numero: numero);
            }
        }

        var reparacao = new Reparacao
        {
            JobOnId = producao?.JobOnId,
            MachineId = machineId,
            Tipo = tipo,
            Numero = numero,
            RegistadoPorUserId = Autor(),
            RegistadoEmUtc = agora,
            DiaOperacional = doDia.Dia,
            // A hora escrita na página; sem ela, a de agora (hora local, ao minuto).
            Hora = pedido.Hora ?? HoraLocal(agora)
        };

        if (pedido.Outra || producao is null)
        {
            // A ferramenta escrita: sem espaços nas pontas, a referência em
            // maiúsculas (como nas outras procuras); as duas obrigatórias.
            var referencia = (pedido.Referencia ?? string.Empty).Trim().ToUpperInvariant();
            var lote = (pedido.Lote ?? string.Empty).Trim();
            if (referencia.Length == 0)
            {
                return new ReparacaoSaida(ReparacaoResultado.SemReferencia, Numero: numero);
            }

            if (referencia.Length > Reparacao.ReferenciaMaxLength)
            {
                return new ReparacaoSaida(ReparacaoResultado.ReferenciaLonga, Numero: numero);
            }

            if (lote.Length == 0)
            {
                return new ReparacaoSaida(ReparacaoResultado.SemLote, Numero: numero);
            }

            if (lote.Length > Reparacao.LoteMaxLength)
            {
                return new ReparacaoSaida(ReparacaoResultado.LoteLongo, Numero: numero);
            }

            reparacao.ReferenciaEscrita = referencia;
            reparacao.LoteEscrito = lote;
        }
        else
        {
            if (producao.Ferramenta(tipo) is not { } ferramenta)
            {
                return new ReparacaoSaida(ReparacaoResultado.SemFerramenta, Numero: numero);
            }

            if (pedido.ContextoId is { } contexto && contexto != ferramenta.ContextoId)
            {
                return new ReparacaoSaida(ReparacaoResultado.FerramentaMudou, Numero: numero);
            }

            reparacao.CmContextId = tipo == ReparacaoTipo.Cm ? ferramenta.ContextoId : null;
            reparacao.MfContextId = tipo == ReparacaoTipo.Mf ? ferramenta.ContextoId : null;
        }

        try
        {
            await GravarAsync(reparacao, pedido.OperacaoId, cancellationToken);
        }
        catch (DbUpdateException ex) when (OperacoesRegistadas.EOperacaoRepetida(ex))
        {
            // Outro envio com o mesmo identificador gravou primeiro.
            db.ChangeTracker.Clear();
            return await JaRegistadoAsync(pedido.OperacaoId, cancellationToken)
                ?? new ReparacaoSaida(ReparacaoResultado.JaRegistado, Numero: numero);
        }

        return Saida(ReparacaoResultado.Gravado, reparacao.Id, reparacao);
    }

    /// <summary>A hora local da fábrica, ao minuto (como a página a mostra).</summary>
    public static TimeOnly HoraLocal(DateTime utc)
    {
        var local = TimeOnly.FromDateTime(DmoBusinessTime.ToLocal(utc));
        return new TimeOnly(local.Hour, local.Minute);
    }

    private static ReparacaoSaida Saida(ReparacaoResultado resultado, int? id, Reparacao reparacao) =>
        new(resultado, id, reparacao.Numero, reparacao.ReferenciaEscrita, reparacao.LoteEscrito, reparacao.JobOnId is null);

    // A reparação e a operação vão juntas: o envio repetido colide na chave
    // da operação e a reparação dele não fica. O Id da reparação vai depois
    // para a operação, na mesma transação.
    private async Task GravarAsync(Reparacao reparacao, Guid operacaoId, CancellationToken cancellationToken)
    {
        var transacao = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            db.ReparacoesInternas.Add(reparacao);
            var operacao = operacoes.Registar(operacaoId, OperacaoTipo, new ResultadoDaOperacao(ResultadoTipo, null));
            await db.SaveChangesAsync(cancellationToken);
            operacao.ResultadoId = reparacao.Id;
            await db.SaveChangesAsync(cancellationToken);
            if (transacao is not null)
            {
                await transacao.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transacao is not null)
            {
                await transacao.DisposeAsync();
            }
        }
    }

    private async Task<ReparacaoSaida?> JaRegistadoAsync(Guid operacaoId, CancellationToken cancellationToken)
    {
        if (operacaoId == Guid.Empty || await operacoes.ProcurarAsync(operacaoId, cancellationToken) is not { } operacao)
        {
            return null;
        }

        var gravada = operacao.ResultadoId is { } id
            ? await db.ReparacoesInternas.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            : null;
        return gravada is null
            ? new ReparacaoSaida(ReparacaoResultado.JaRegistado, operacao.ResultadoId)
            : Saida(ReparacaoResultado.JaRegistado, operacao.ResultadoId, gravada);
    }

    // Quem grava: o utilizador da sessão; o ADMIN não é um utilizador (NULL).
    private Guid? Autor() =>
        account.Current is { IsAuthenticated: true, IsAdmin: false } actor ? actor.ActorId : null;

    // Sem recusas para o operador (Diogo, 21/10/2026): se outra pessoa mudou
    // a reparação entretanto, aplica-se outra vez sobre a de agora (o rasto
    // guarda as duas). Só o impossível fica: já não existe ou já foi anulada.
    public async Task<CorrecaoSaida> CorrigirAsync(PedidoDeCorrecao pedido, CancellationToken cancellationToken = default) =>
        await DeNovoSeMudouAsync(() => CorrigirUmaVezAsync(pedido, cancellationToken));

    public async Task<CorrecaoSaida> AnularAsync(PedidoDeAnulacao pedido, CancellationToken cancellationToken = default) =>
        await DeNovoSeMudouAsync(() => AnularUmaVezAsync(pedido, cancellationToken));

    private static async Task<CorrecaoSaida> DeNovoSeMudouAsync(Func<Task<CorrecaoSaida>> fazer)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            var saida = await fazer();
            if (saida.Resultado != CorrecaoResultado.Mudou || tentativa == 3)
            {
                return saida;
            }
        }
    }

    private async Task<CorrecaoSaida> CorrigirUmaVezAsync(PedidoDeCorrecao pedido, CancellationToken cancellationToken)
    {
        if (await JaFeitoAsync(pedido.OperacaoId, cancellationToken) is { } repetido)
        {
            return repetido;
        }

        // As mesmas regras do registo: sem espaços nas pontas, não vazio, até 40.
        var numero = (pedido.Numero ?? string.Empty).Trim();
        if (numero.Length == 0)
        {
            return new CorrecaoSaida(CorrecaoResultado.SemNumero);
        }

        if (numero.Length > Reparacao.NumeroMaxLength)
        {
            return new CorrecaoSaida(CorrecaoResultado.NumeroLongo, numero);
        }

        if (!Motivo(pedido.Motivo, out var motivo))
        {
            return new CorrecaoSaida(CorrecaoResultado.MotivoLongo, numero);
        }

        var reparacao = await db.ReparacoesInternas.FirstOrDefaultAsync(r => r.Id == pedido.ReparacaoId, cancellationToken);
        if (reparacao is null)
        {
            return new CorrecaoSaida(CorrecaoResultado.NaoExiste);
        }

        if (reparacao.AnuladaEmUtc is not null)
        {
            return new CorrecaoSaida(CorrecaoResultado.JaAnulada, reparacao.Numero);
        }

        // A hora: NULL deixa-a como está; muda-se só ao minuto.
        var hora = pedido.Hora is { } h ? new TimeOnly(h.Hour, h.Minute) : reparacao.Hora;
        var horaMudou = hora != reparacao.Hora;
        if (reparacao.Numero == numero && !horaMudou)
        {
            return new CorrecaoSaida(CorrecaoResultado.Igual, numero);
        }

        var rasto = new ReparacaoRasto
        {
            ReparacaoId = reparacao.Id,
            Acao = ReparacaoAcao.Corrigido,
            NumeroAntes = reparacao.Numero,
            NumeroDepois = numero,
            PorUserId = Autor(),
            EmUtc = _clock.GetUtcNow().UtcDateTime,
            Motivo = motivo,
            HoraAntes = horaMudou ? reparacao.Hora : null,
            HoraDepois = horaMudou ? hora : null
        };
        reparacao.Numero = numero;
        reparacao.Hora = hora;
        return await AplicarAsync(reparacao, rasto, pedido.OperacaoId, OperacaoCorrigir, CorrecaoResultado.Corrigido, cancellationToken);
    }

    private async Task<CorrecaoSaida> AnularUmaVezAsync(PedidoDeAnulacao pedido, CancellationToken cancellationToken)
    {
        if (await JaFeitoAsync(pedido.OperacaoId, cancellationToken) is { } repetido)
        {
            return repetido;
        }

        if (!Motivo(pedido.Motivo, out var motivo))
        {
            return new CorrecaoSaida(CorrecaoResultado.MotivoLongo);
        }

        var reparacao = await db.ReparacoesInternas.FirstOrDefaultAsync(r => r.Id == pedido.ReparacaoId, cancellationToken);
        if (reparacao is null)
        {
            return new CorrecaoSaida(CorrecaoResultado.NaoExiste);
        }

        if (reparacao.AnuladaEmUtc is not null)
        {
            return new CorrecaoSaida(CorrecaoResultado.JaAnulada, reparacao.Numero);
        }

        var agora = _clock.GetUtcNow().UtcDateTime;
        var rasto = new ReparacaoRasto
        {
            ReparacaoId = reparacao.Id,
            Acao = ReparacaoAcao.Anulado,
            NumeroAntes = reparacao.Numero,
            PorUserId = Autor(),
            EmUtc = agora,
            Motivo = motivo
        };
        reparacao.AnuladaEmUtc = agora;
        return await AplicarAsync(reparacao, rasto, pedido.OperacaoId, OperacaoAnular, CorrecaoResultado.Anulado, cancellationToken);
    }

    // O motivo é opcional: sem espaços nas pontas, vazio fica NULL, até 200.
    private static bool Motivo(string? escrito, out string? motivo)
    {
        motivo = string.IsNullOrWhiteSpace(escrito) ? null : escrito.Trim();
        return motivo is null || motivo.Length <= ReparacaoRasto.MotivoMaxLength;
    }

    // A reparação mudada e a linha do rasto, numa correção ou numa anulação.
    private async Task<CorrecaoSaida> AplicarAsync(
        Reparacao reparacao, ReparacaoRasto rasto, Guid operacaoId, string tipo, CorrecaoResultado feito, CancellationToken cancellationToken)
    {
        switch (await GravarRastoAsync(reparacao, rasto, operacaoId, tipo, cancellationToken))
        {
            case Gravacao.Repetida:
                return await JaFeitoAsync(operacaoId, cancellationToken) ?? new CorrecaoSaida(CorrecaoResultado.JaFeito, rasto.NumeroDepois ?? rasto.NumeroAntes);
            case Gravacao.Mudou:
                var agora = await db.ReparacoesInternas.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reparacao.Id, cancellationToken);
                return agora is null ? new CorrecaoSaida(CorrecaoResultado.NaoExiste)
                    : agora.AnuladaEmUtc is not null ? new CorrecaoSaida(CorrecaoResultado.JaAnulada, agora.Numero)
                    : new CorrecaoSaida(CorrecaoResultado.Mudou, agora.Numero);
            default:
                return new CorrecaoSaida(feito, rasto.NumeroDepois ?? rasto.NumeroAntes);
        }
    }

    private enum Gravacao
    {
        Feita,
        Repetida,
        Mudou
    }

    // A reparação mudada, a linha do rasto e a operação vão juntas. Outro
    // envio com o mesmo identificador primeiro: já está feito. Outra pessoa
    // gravou na mesma reparação entretanto (a versão da linha mudou): nada
    // se grava e a pessoa é avisada do que está agora.
    private async Task<Gravacao> GravarRastoAsync(
        Reparacao reparacao, ReparacaoRasto rasto, Guid operacaoId, string tipo, CancellationToken cancellationToken)
    {
        var transacao = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            db.ReparacoesInternasRasto.Add(rasto);
            operacoes.Registar(operacaoId, tipo, new ResultadoDaOperacao(ResultadoTipo, reparacao.Id));
            await db.SaveChangesAsync(cancellationToken);
            if (transacao is not null)
            {
                await transacao.CommitAsync(cancellationToken);
            }

            return Gravacao.Feita;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Gravacao.Mudou;
        }
        catch (DbUpdateException ex) when (OperacoesRegistadas.EOperacaoRepetida(ex))
        {
            db.ChangeTracker.Clear();
            return Gravacao.Repetida;
        }
        finally
        {
            if (transacao is not null)
            {
                await transacao.DisposeAsync();
            }
        }
    }

    public async Task<AssociacaoSaida> AssociarAsync(PedidoDeAssociacao pedido, CancellationToken cancellationToken = default)
    {
        if (await JaAssociadoAsync(pedido.OperacaoId, cancellationToken) is { } repetido)
        {
            return repetido;
        }

        var reparacao = await db.ReparacoesInternas.FirstOrDefaultAsync(r => r.Id == pedido.ReparacaoId, cancellationToken);
        if (reparacao is null)
        {
            return new AssociacaoSaida(AssociacaoResultado.NaoExiste);
        }

        if (reparacao.AnuladaEmUtc is not null)
        {
            return new AssociacaoSaida(AssociacaoResultado.JaAnulada, reparacao.Numero);
        }

        if (reparacao.ReferenciaEscrita is null)
        {
            return new AssociacaoSaida(AssociacaoResultado.NaoEscrita, reparacao.Numero);
        }

        if (reparacao.AssociadaToolId is { } associada)
        {
            return new AssociacaoSaida(AssociacaoResultado.JaAssociada, reparacao.Numero, await FerramentaAsync(associada, cancellationToken));
        }

        var toolId = pedido.ToolId?.Trim();
        if (string.IsNullOrEmpty(toolId))
        {
            return new AssociacaoSaida(AssociacaoResultado.SemFerramenta, reparacao.Numero);
        }

        // Só uma ferramenta registada do mesmo tipo (a REMOVED nunca). A
        // referência e o lote não têm de ser os escritos: o escrito pode
        // estar errado, e o responsável escolhe.
        var tipo = reparacao.Tipo == ReparacaoTipo.Cm ? ToolType.Cm : ToolType.Mf;
        var ferramenta = await db.Tools.AsNoTracking()
            .Where(t => t.ToolId == toolId && t.ToolId != ToolRegistryConstants.RemovedToolId && t.Type == tipo)
            .Select(t => new { t.Reference, t.Lot })
            .FirstOrDefaultAsync(cancellationToken);
        if (ferramenta is null)
        {
            return new AssociacaoSaida(AssociacaoResultado.FerramentaNaoServe, reparacao.Numero);
        }

        var agora = _clock.GetUtcNow().UtcDateTime;
        var quem = Autor();
        var rasto = new ReparacaoRasto
        {
            ReparacaoId = reparacao.Id,
            Acao = ReparacaoAcao.Associado,
            NumeroAntes = reparacao.Numero,
            PorUserId = quem,
            EmUtc = agora,
            ToolIdAssociado = toolId
        };
        reparacao.AssociadaToolId = toolId;
        reparacao.AssociadaPorUserId = quem;
        reparacao.AssociadaEmUtc = agora;
        var texto = RiTextos.Escrita(ferramenta.Reference, ferramenta.Lot);
        switch (await GravarRastoAsync(reparacao, rasto, pedido.OperacaoId, OperacaoAssociar, cancellationToken))
        {
            case Gravacao.Repetida:
                return await JaAssociadoAsync(pedido.OperacaoId, cancellationToken) ?? new AssociacaoSaida(AssociacaoResultado.JaFeito, rasto.NumeroAntes, texto);
            case Gravacao.Mudou:
                var depois = await db.ReparacoesInternas.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reparacao.Id, cancellationToken);
                return depois is null ? new AssociacaoSaida(AssociacaoResultado.NaoExiste)
                    : depois.AnuladaEmUtc is not null ? new AssociacaoSaida(AssociacaoResultado.JaAnulada, depois.Numero)
                    : depois.AssociadaToolId is { } outra ? new AssociacaoSaida(AssociacaoResultado.JaAssociada, depois.Numero, await FerramentaAsync(outra, cancellationToken))
                    : new AssociacaoSaida(AssociacaoResultado.Mudou, depois.Numero);
            default:
                return new AssociacaoSaida(AssociacaoResultado.Associado, rasto.NumeroAntes, texto);
        }
    }

    // A ferramenta associada como a pessoa a reconhece; eliminada, sem referência.
    private async Task<string> FerramentaAsync(string toolId, CancellationToken cancellationToken) =>
        await db.Tools.AsNoTracking()
            .Where(t => t.ToolId == toolId && t.ToolId != ToolRegistryConstants.RemovedToolId)
            .Select(t => t.Reference + " · lote " + t.Lot)
            .FirstOrDefaultAsync(cancellationToken) ?? "ferramenta eliminada";

    private async Task<AssociacaoSaida?> JaAssociadoAsync(Guid operacaoId, CancellationToken cancellationToken)
    {
        if (operacaoId == Guid.Empty || await operacoes.ProcurarAsync(operacaoId, cancellationToken) is not { } operacao)
        {
            return null;
        }

        var reparacao = operacao.ResultadoId is { } id
            ? await db.ReparacoesInternas.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.Numero, r.AssociadaToolId }).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new AssociacaoSaida(AssociacaoResultado.JaFeito, reparacao?.Numero,
            reparacao?.AssociadaToolId is { } toolId ? await FerramentaAsync(toolId, cancellationToken) : null);
    }

    private async Task<CorrecaoSaida?> JaFeitoAsync(Guid operacaoId, CancellationToken cancellationToken)
    {
        if (operacaoId == Guid.Empty || await operacoes.ProcurarAsync(operacaoId, cancellationToken) is not { } operacao)
        {
            return null;
        }

        var numero = operacao.ResultadoId is { } id
            ? await db.ReparacoesInternas.AsNoTracking().Where(r => r.Id == id).Select(r => r.Numero).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new CorrecaoSaida(CorrecaoResultado.JaFeito, numero);
    }

    public Task<PagedList<RiLinha>> ListarAsync(int machineId, int jobOnId, int? pagina, CancellationToken cancellationToken = default) =>
        Listar(db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == jobOnId && r.MachineId == machineId), pagina, cancellationToken);

    // Sem Job On: as desta máquina no dia operacional (pelo índice da máquina com o dia).
    public Task<PagedList<RiLinha>> ListarSemProducaoAsync(int machineId, DateOnly dia, int? pagina, CancellationToken cancellationToken = default) =>
        Listar(db.ReparacoesInternas.AsNoTracking().Where(r => r.JobOnId == null && r.MachineId == machineId && r.DiaOperacional == dia), pagina, cancellationToken);

    private Task<PagedList<RiLinha>> Listar(IQueryable<Reparacao> reparacoes, int? pagina, CancellationToken cancellationToken) =>
        (from r in reparacoes
         where r.AnuladaEmUtc == null
         join u in db.Users.AsNoTracking() on r.RegistadoPorUserId equals (Guid?)u.Id into us
         from u in us.DefaultIfEmpty()
         orderby r.Id descending
         select new RiLinha(r.Id, r.Numero, r.Tipo, u != null ? u.Name : null, r.RegistadoEmUtc, r.ReferenciaEscrita, r.LoteEscrito, r.Hora))
        .ToPagedListAsync(pagina, cancellationToken: cancellationToken);
}

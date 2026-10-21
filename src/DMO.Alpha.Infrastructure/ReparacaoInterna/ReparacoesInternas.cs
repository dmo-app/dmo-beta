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
/// nas Boquilhas, nem no Armazém.
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

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<DateOnly> DiaOperacionalAsync(CancellationToken cancellationToken = default)
    {
        var configurada = await settings.FindValueAsync(ModuleSettingKeys.ReparacaoInternaDayChangeHour, cancellationToken);
        var hora = int.TryParse(configurada, out var h) && h is >= 0 and <= 23 ? h : 8;
        return BoquilhasDayChange.ResolveOperationalDay(DmoBusinessTime.ToLocal(_clock.GetUtcNow().UtcDateTime), hora);
    }

    public async Task<RiProducaoDoDia> ProducaoDoDiaAsync(int machineId, CancellationToken cancellationToken = default) =>
        await ProducaoDoDiaAsync(machineId, await DiaOperacionalAsync(cancellationToken), cancellationToken);

    private async Task<RiProducaoDoDia> ProducaoDoDiaAsync(int machineId, DateOnly dia, CancellationToken cancellationToken)
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
                MfId = m != null ? m.Id : (int?)null,
                MfReferencia = tm != null ? tm.Reference : null,
                MfLote = tm != null ? tm.Lot : null
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
                l.MfId is { } mf ? new RiFerramenta(mf, l.MfReferencia, l.MfLote) : null))
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
        // com o número escrito.
        var agora = _clock.GetUtcNow().UtcDateTime;
        var doDia = await ProducaoDoDiaAsync(machineId, await DiaOperacionalAsync(cancellationToken), cancellationToken);
        if (doDia.SemProducao)
        {
            return new ReparacaoSaida(ReparacaoResultado.SemProducao, Numero: numero);
        }

        var producao = pedido.JobOnId is { } pedida
            ? doDia.Candidatas.FirstOrDefault(c => c.JobOnId == pedida)
            : doDia.Escolhida(null);
        if (producao is null)
        {
            return new ReparacaoSaida(ReparacaoResultado.ProducaoMudou, Numero: numero);
        }

        if (producao.Ferramenta(tipo) is not { } ferramenta)
        {
            return new ReparacaoSaida(ReparacaoResultado.SemFerramenta, Numero: numero);
        }

        if (pedido.ContextoId is { } contexto && contexto != ferramenta.ContextoId)
        {
            return new ReparacaoSaida(ReparacaoResultado.FerramentaMudou, Numero: numero);
        }

        var actor = account.Current;
        var reparacao = new Reparacao
        {
            JobOnId = producao.JobOnId,
            MachineId = machineId,
            Tipo = tipo,
            CmContextId = tipo == ReparacaoTipo.Cm ? ferramenta.ContextoId : null,
            MfContextId = tipo == ReparacaoTipo.Mf ? ferramenta.ContextoId : null,
            Numero = numero,
            RegistadoPorUserId = actor is { IsAuthenticated: true, IsAdmin: false } ? actor.ActorId : null,
            RegistadoEmUtc = agora,
            DiaOperacional = doDia.Dia
        };

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

        return new ReparacaoSaida(ReparacaoResultado.Gravado, reparacao.Id, numero);
    }

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

        var numero = operacao.ResultadoId is { } id
            ? await db.ReparacoesInternas.AsNoTracking().Where(r => r.Id == id).Select(r => r.Numero).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new ReparacaoSaida(ReparacaoResultado.JaRegistado, operacao.ResultadoId, numero);
    }

    public Task<PagedList<RiLinha>> ListarAsync(int machineId, int jobOnId, int? pagina, CancellationToken cancellationToken = default) =>
        (from r in db.ReparacoesInternas.AsNoTracking()
         where r.JobOnId == jobOnId && r.MachineId == machineId
         join u in db.Users.AsNoTracking() on r.RegistadoPorUserId equals (Guid?)u.Id into us
         from u in us.DefaultIfEmpty()
         orderby r.Id descending
         select new RiLinha(r.Id, r.Numero, r.Tipo, u != null ? u.Name : null, r.RegistadoEmUtc))
        .ToPagedListAsync(pagina, cancellationToken: cancellationToken);
}

using System.Globalization;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Boquilhas;

/// <summary>
/// O Excel das Boquilhas do chefe (Diogo, 21/10/2026): lê os movimentos do
/// intervalo pelo mesmo query do Histórico e escreve-os no livro que o
/// navegador enviou (BqExcelLivro). O livro nunca fica no servidor.
/// </summary>
public sealed class BqExcelBoquilhas(DmoDbContext db, IBqToolHistoryQuery history) : IBqExcelBoquilhas
{
    public async Task<string> VersaoAsync(CancellationToken cancellationToken = default)
    {
        var movimentos = db.BqMovements.AsNoTracking();
        var total = await movimentos.CountAsync(cancellationToken);
        var maiorId = total == 0 ? 0 : await movimentos.MaxAsync(m => m.Id, cancellationToken);
        var corrigido = await movimentos.MaxAsync(m => (DateTime?)m.CorrectedAtUtc, cancellationToken);
        return string.Create(CultureInfo.InvariantCulture, $"{total}.{maiorId}.{corrigido?.Ticks ?? 0}");
    }

    public byte[] Novo(DateOnly dia) => BqExcelLivro.Novo(dia.Year, dia.Month);

    public async Task<BqExcelResultado> EscreverAsync(byte[] livro, DateOnly desde, DateOnly ate, IReadOnlyList<BqExcelEscrita> anteriores, CancellationToken cancellationToken = default)
    {
        var versao = await VersaoAsync(cancellationToken);
        var movimentos = await history.ListMovementsAsync(desde, ate, null, null, null, null, cancellationToken);
        var resultado = BqExcelLivro.Escrever(livro, BqExcelPlano.From(movimentos, desde, ate), anteriores);
        return resultado with { Versao = versao };
    }

    public async Task<BqExcelAtualizacao> AtualizarAsync(byte[] livro, DateOnly hoje, IReadOnlyList<BqExcelEscrita> anteriores, CancellationToken cancellationToken = default)
    {
        var versao = await VersaoAsync(cancellationToken);
        // O limite seguro: o primeiro dia com movimentos na app, nunca antes
        // de 1 de janeiro do ano de hoje (o livro é de um ano).
        var primeiro = await db.BqMovements.AsNoTracking().MinAsync(m => (DateTime?)m.OccurredAtUtc, cancellationToken);
        var inicioDoAno = new DateOnly(hoje.Year, 1, 1);
        var desde = primeiro is null ? hoje : DateOnly.FromDateTime(DMO.Alpha.Core.Runtime.DmoBusinessTime.ToLocal(primeiro.Value));
        if (desde < inicioDoAno) desde = inicioDoAno;
        if (desde > hoje) desde = hoje;
        var movimentos = await history.ListMovementsAsync(desde, hoje, null, null, null, null, cancellationToken);
        var atualizacao = BqExcelLivro.Atualizar(livro, BqExcelPlano.From(movimentos, desde, hoje), hoje.AddDays(-1), anteriores);
        return atualizacao with { Resultado = atualizacao.Resultado with { Versao = versao } };
    }
}

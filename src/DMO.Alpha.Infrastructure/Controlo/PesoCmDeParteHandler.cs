using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// «Manter / Colocar de parte» as CM de uma comparação de pesos e a aprovação
/// das postas de parte, uma CM de cada vez (o Diogo, 19/10/2026, D10). Num só
/// sítio e na própria Comparação do Peso (o Diogo, 19/10/2026: «entra logo
/// na comparação, não é uma página»): quem tem o Controlo Criar escolhe, quem
/// tem o Controlo Aprovar decide na mesma tabela. É só registo: não muda o estado da CM nas Ferramentas
/// (D11) nem bloqueia nada. Cada gravação segura um trinco pela leitura, para
/// dois cliques ao mesmo tempo não criarem duas linhas.
/// </summary>
public sealed class PesoCmDeParteHandler(DmoDbContext db)
{
    /// <summary>A escolha de cada CM medida numa comparação, pela ordem das leituras.</summary>
    public async Task<IReadOnlyList<CmDecisaoLinha>> DaComparacaoAsync(int comparacaoId, CancellationToken cancellationToken)
    {
        var linhas = await (
            from m in db.PesoComparacaoMeasurements.AsNoTracking()
            where m.ComparacaoId == comparacaoId
            from d in db.PesoComparacaoCmDecisoes.Where(d => d.MeasurementId == m.Id).DefaultIfEmpty()
            orderby m.RowPosition
            select new CmDecisaoLinha(
                m.Id, m.RowPosition, m.CmSubject,
                d != null ? d.DeParte : null,
                d != null ? d.EscolhidoPor : null,
                d != null ? d.EscolhidoEmUtc : null,
                d != null ? d.Aprovado : null,
                d != null ? d.AprovadoPor : null,
                d != null ? d.AprovadoEmUtc : null))
            .ToListAsync(cancellationToken);
        return linhas;
    }

    /// <summary>
    /// Grava «Manter» (false), «Colocar de parte» (true) ou nenhuma (null) para
    /// a CM medida. Mudar a escolha tira a aprovação que havia: a CM posta de
    /// parte volta a ficar por aprovar. A mesma escolha outra vez não muda nada.
    /// </summary>
    public Task<CmDeParteResultado> EscolherAsync(
        int measurementId, bool? deParte, string quem, CancellationToken cancellationToken) =>
        DbWrite.InTransactionAsync(db, async () =>
        {
            await DbWrite.LockAsync(db, $"peso:cm-de-parte:{measurementId}", cancellationToken);
            if (!await db.PesoComparacaoMeasurements.AnyAsync(m => m.Id == measurementId, cancellationToken))
            {
                return CmDeParteResultado.NaoExiste;
            }

            var atual = await db.PesoComparacaoCmDecisoes
                .FirstOrDefaultAsync(d => d.MeasurementId == measurementId, cancellationToken);
            if (atual?.DeParte == deParte)
            {
                return CmDeParteResultado.SemAlteracao;
            }

            if (deParte is not bool escolha)
            {
                db.PesoComparacaoCmDecisoes.Remove(atual!);
            }
            else
            {
                if (atual is null)
                {
                    atual = new PesoComparacaoCmDecisao { MeasurementId = measurementId };
                    db.PesoComparacaoCmDecisoes.Add(atual);
                }

                atual.DeParte = escolha;
                atual.EscolhidoPor = quem;
                // Ao microssegundo, como a base guarda: a página devolve-a na aprovação.
                var agora = DateTime.UtcNow;
                atual.EscolhidoEmUtc = new DateTime(agora.Ticks - agora.Ticks % 10, DateTimeKind.Utc);
                atual.Aprovado = null;
                atual.AprovadoPor = null;
                atual.AprovadoEmUtc = null;
            }

            await db.SaveChangesAsync(cancellationToken);
            return CmDeParteResultado.Gravado;
        }, cancellationToken);

    /// <summary>
    /// Aprova (true), não aprova (false) ou volta a pôr por aprovar (null) a CM
    /// posta de parte. <paramref name="escolhidaEmUtc"/> é a escolha que quem
    /// decide viu: se entretanto mudou (outra escolha, ou «Manter»), nada se
    /// grava e a página mostra a escolha nova. A última decisão vence.
    /// </summary>
    public Task<CmDeParteResultado> DecidirAsync(
        int measurementId, bool? aprovado, DateTime escolhidaEmUtc, string quem, CancellationToken cancellationToken) =>
        DbWrite.InTransactionAsync(db, async () =>
        {
            await DbWrite.LockAsync(db, $"peso:cm-de-parte:{measurementId}", cancellationToken);
            var atual = await db.PesoComparacaoCmDecisoes
                .FirstOrDefaultAsync(d => d.MeasurementId == measurementId, cancellationToken);
            if (atual is not { DeParte: true } || atual.EscolhidoEmUtc.Ticks != escolhidaEmUtc.Ticks)
            {
                return CmDeParteResultado.Mudou;
            }

            if (atual.Aprovado == aprovado)
            {
                return CmDeParteResultado.SemAlteracao;
            }

            atual.Aprovado = aprovado;
            atual.AprovadoPor = aprovado is null ? null : quem;
            atual.AprovadoEmUtc = aprovado is null ? null : DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return CmDeParteResultado.Gravado;
        }, cancellationToken);

    /// <summary>Quantas CM postas de parte esperam pela aprovação.</summary>
    public Task<int> ContarPorAprovarAsync(CancellationToken cancellationToken) =>
        db.PesoComparacaoCmDecisoes.CountAsync(d => d.DeParte && d.Aprovado == null, cancellationToken);
}

/// <summary>A escolha de uma CM medida numa comparação; DeParte null = ainda sem escolha.</summary>
public sealed record CmDecisaoLinha(
    int MeasurementId,
    int RowPosition,
    string CmSubject,
    bool? DeParte,
    string? EscolhidoPor,
    DateTime? EscolhidoEmUtc,
    bool? Aprovado,
    string? AprovadoPor,
    DateTime? AprovadoEmUtc)
{
    /// <summary>Posta de parte e ainda sem decisão de quem aprova.</summary>
    public bool PorAprovar => DeParte == true && Aprovado is null;
}

public enum CmDeParteResultado
{
    Gravado,
    SemAlteracao,
    NaoExiste,

    /// <summary>A escolha mudou desde que a página foi aberta: nada se gravou.</summary>
    Mudou
}

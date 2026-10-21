using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Production;

/// <summary>
/// A gravação da folha da fábrica, partilhada pelo Criar e pelo Editar do Job
/// On: o envio normalizado (<see cref="JobOnFolhaRegras"/>), a recusa de
/// tamanho e as escritas da folha e das linhas CAL. Só acrescenta ao
/// <see cref="DmoDbContext"/>: quem chama faz o SaveChanges, na mesma transação
/// do Job On.
/// </summary>
internal sealed class JobOnFolhaGravacao
{
    private JobOnFolhaGravacao(IReadOnlyDictionary<string, string?>? folha, IReadOnlyList<LinhaCalEnvio>? cal)
    {
        Folha = folha;
        Cal = cal;
    }

    /// <summary>Os valores normalizados por chave do catálogo; null quando o envio não traz a folha.</summary>
    public IReadOnlyDictionary<string, string?>? Folha { get; }

    /// <summary>As linhas CAL normalizadas, pela ordem do envio; null quando o envio não as traz.</summary>
    public IReadOnlyList<LinhaCalEnvio>? Cal { get; }

    /// <summary>A chave do primeiro texto acima do máximo; null quando tudo cabe.</summary>
    public string? ChaveLonga => JobOnFolhaRegras.PrimeiraChaveLonga(Folha, Cal);

    /// <summary>O envio do comando, normalizado.</summary>
    public static JobOnFolhaGravacao De(IReadOnlyDictionary<string, string?>? folha, IReadOnlyList<LinhaCalEnvio>? cal) =>
        new(
            folha is null ? null : JobOnFolhaRegras.NormalizarFolha(folha),
            cal?.Select(JobOnFolhaRegras.NormalizarLinha).ToList());

    /// <summary>
    /// A folha de um Job On novo: a linha da folha só quando há alguma coisa
    /// escrita, e as linhas CAL que ficam. Nada a registar: é a criação.
    /// </summary>
    public void Criar(DmoDbContext db, JobOn jobOn)
    {
        if (Folha is { } folha && folha.Values.Any(v => v is not null))
        {
            var linha = new JobOnFolha { JobOn = jobOn };
            foreach (var (chave, valor) in folha)
            {
                linha.DefinirValor(chave, valor);
            }

            db.JobOnFolhas.Add(linha);
        }

        foreach (var cal in JobOnFolhaRegras.LinhasQueFicam(Cal ?? []))
        {
            db.JobOnFolhaCalLinhas.Add(new JobOnFolhaCalLinha
            {
                JobOn = jobOn,
                Ordem = cal.Ordem,
                Designacao = cal.Designacao,
                Valor = cal.Valor,
                QtdMaq = cal.QtdMaq
            });
        }
    }

    /// <summary>O que muda da folha <paramref name="gravada"/> para este envio (os rótulos, pela ordem da folha).</summary>
    public IReadOnlyList<string> Alteracoes(JobOnFolhaLida gravada) =>
        JobOnFolhaRegras.Alteracoes(gravada, Folha, Cal);

    /// <summary>
    /// Grava a folha de um Job On existente quando muda: os campos na linha da
    /// folha (criada na primeira gravação) e, quando a CAL muda, as linhas
    /// gravadas dão lugar às do envio. Devolve os rótulos do que mudou.
    /// </summary>
    public async Task<IReadOnlyList<string>> EditarAsync(
        DmoDbContext db,
        int jobOnId,
        JobOnFolhaLida gravada,
        CancellationToken cancellationToken)
    {
        var alteracoes = Alteracoes(gravada);
        if (alteracoes.Count == 0)
        {
            return alteracoes;
        }

        if (Folha is { } folha && JobOnFolhaCampos.Todos.Any(c => !string.Equals(gravada.Valor(c.Chave), folha[c.Chave], StringComparison.Ordinal)))
        {
            var linha = await db.JobOnFolhas.SingleOrDefaultAsync(f => f.JobOnId == jobOnId, cancellationToken);
            if (linha is null)
            {
                linha = new JobOnFolha { JobOnId = jobOnId };
                db.JobOnFolhas.Add(linha);
            }

            foreach (var (chave, valor) in folha)
            {
                linha.DefinirValor(chave, valor);
            }
        }

        if (Cal is { } cal)
        {
            var ficam = JobOnFolhaRegras.LinhasQueFicam(cal);
            if (!ficam.SequenceEqual(gravada.Cal))
            {
                db.JobOnFolhaCalLinhas.RemoveRange(
                    await db.JobOnFolhaCalLinhas.Where(l => l.JobOnId == jobOnId).ToListAsync(cancellationToken));
                db.JobOnFolhaCalLinhas.AddRange(ficam.Select(l => new JobOnFolhaCalLinha
                {
                    JobOnId = jobOnId,
                    Ordem = l.Ordem,
                    Designacao = l.Designacao,
                    Valor = l.Valor,
                    QtdMaq = l.QtdMaq
                }));
            }
        }

        return alteracoes;
    }
}

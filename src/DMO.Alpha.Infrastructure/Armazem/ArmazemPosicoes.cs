using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// As posições do Armazém sobre o <see cref="DmoDbContext"/>. Criar guarda o
/// autor da sessão (o identificador fica pelo carimbo da
/// <see cref="AutoriaMap"/>); dois pedidos iguais ao mesmo tempo deixam uma
/// posição só (índice único do código) e o segundo lê «já existe».
/// </summary>
public sealed class ArmazemPosicoes(DmoDbContext db, ICurrentAccountContext account) : IArmazemPosicoes
{
    public Task<PagedList<ArmazemPosicaoItem>> ListarAsync(
        string? comecaPor, ArmazemPosicaoFiltro filtro, int pagina, CancellationToken cancellationToken = default)
    {
        var query = db.ArmazemPosicoes.AsNoTracking();
        var inicio = (comecaPor ?? string.Empty).Trim().ToUpperInvariant();
        if (inicio.Length > 0)
        {
            query = query.Where(p => p.Codigo.StartsWith(inicio));
        }

        query = filtro switch
        {
            ArmazemPosicaoFiltro.Ativas => query.Where(p => p.Ativa),
            ArmazemPosicaoFiltro.Arquivadas => query.Where(p => !p.Ativa),
            _ => query
        };

        return query
            .OrderBy(p => p.Codigo)
            .Select(p => new ArmazemPosicaoItem(p.Id, p.Codigo, p.Ativa))
            .ToPagedListAsync(pagina, cancellationToken: cancellationToken);
    }

    public Task<(ArmazemPosicaoResultado Resultado, string Codigo)> CriarAsync(
        string? codigo, CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(db, () => CriarUmaVezAsync(codigo, cancellationToken));

    private async Task<(ArmazemPosicaoResultado, string)> CriarUmaVezAsync(string? texto, CancellationToken cancellationToken)
    {
        if (!PosicaoCodigo.TryParse(texto, out var codigo))
        {
            return (ArmazemPosicaoResultado.CodigoInvalido, (texto ?? string.Empty).Trim());
        }

        if (await db.ArmazemPosicoes.AnyAsync(p => p.Codigo == codigo, cancellationToken))
        {
            return (ArmazemPosicaoResultado.JaExiste, codigo);
        }

        var actor = account.Current;
        db.ArmazemPosicoes.Add(new ArmazemPosicao
        {
            Codigo = codigo,
            CriadaEmUtc = DateTime.UtcNow,
            CriadaPor = actor.IsAuthenticated
                ? (string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.OperatorId : actor.DisplayName)
                : null
        });
        await db.SaveChangesAsync(cancellationToken);
        return (ArmazemPosicaoResultado.Gravado, codigo);
    }

    public async Task<ArmazemPosicaoResultado> MudarAtivaAsync(int id, bool ativa, CancellationToken cancellationToken = default)
    {
        var posicao = await db.ArmazemPosicoes.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (posicao is null)
        {
            return ArmazemPosicaoResultado.NaoExiste;
        }

        if (posicao.Ativa != ativa)
        {
            posicao.Ativa = ativa;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ArmazemPosicaoResultado.Gravado;
    }
}

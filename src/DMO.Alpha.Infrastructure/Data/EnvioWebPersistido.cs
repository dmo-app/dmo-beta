using DMO.Alpha.Core.Runtime;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>O facto e a resposta do envio ficam na mesma transação. O trinco funciona entre instâncias.</summary>
public sealed class EnvioWebPersistido(DmoDbContext db)
{
    public sealed record Resultado(string? Resposta, bool Conflito = false);

    public Task<Resultado> ExecutarAsync(Guid id, Guid? autor, string hash, Func<Task<string?>> executar, CancellationToken cancellationToken) =>
        DbWrite.InTransactionAsync(db, async () =>
        {
            await DbWrite.LockAsync(db, "web:envio:" + id.ToString("N"), cancellationToken);
            var anterior = await db.OperacoesRegistadas.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
            if (anterior is not null)
                return anterior.Tipo == "web.envio" && anterior.AutorUserId == autor && anterior.EnvioHash == hash
                    ? new Resultado(anterior.EnvioResposta) : new Resultado(null, true);
            var resposta = await executar();
            if (resposta is not null)
            {
                db.OperacoesRegistadas.Add(new OperacaoRegistada
                {
                    Id = id, Tipo = "web.envio", AutorUserId = autor, ResultadoTipo = "Redirect",
                    MomentoUtc = DateTimeOffset.UtcNow, EnvioHash = hash, EnvioResposta = resposta
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            return new Resultado(resposta);
        }, cancellationToken);
}

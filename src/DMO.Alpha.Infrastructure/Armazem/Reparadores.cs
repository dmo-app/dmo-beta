using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// Os reparadores de CM e MF sobre o <see cref="DmoDbContext"/>. O nome é
/// único sem distinguir maiúsculas (a verificação aqui; o índice único é a
/// última linha); dois pedidos iguais ao mesmo tempo deixam um só.
/// </summary>
public sealed class Reparadores(DmoDbContext db) : IReparadores
{
    public async Task<IReadOnlyList<ReparadorItem>> ListarAsync(CancellationToken cancellationToken = default) =>
        await db.Reparadores.AsNoTracking()
            .OrderByDescending(r => r.Ativo)
            .ThenBy(r => r.Nome)
            .Select(r => new ReparadorItem(r.Id, r.Nome, r.AplicaCm, r.AplicaMf, r.Ativo))
            .ToListAsync(cancellationToken);

    public Task<ReparadorResultado> CriarAsync(
        string? nome, bool aplicaCm, bool aplicaMf, CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(db, async () =>
        {
            var limpo = (nome ?? string.Empty).Trim();
            var recusa = Recusa(limpo, aplicaCm, aplicaMf) ?? await NomeUsadoAsync(limpo, null, cancellationToken);
            if (recusa is { } r)
            {
                return r;
            }

            db.Reparadores.Add(new Reparador { Nome = limpo, AplicaCm = aplicaCm, AplicaMf = aplicaMf });
            await db.SaveChangesAsync(cancellationToken);
            return ReparadorResultado.Gravado;
        });

    public Task<ReparadorResultado> MudarAsync(
        int id, MudancaDeReparador mudanca, CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(db, async () =>
        {
            var reparador = await db.Reparadores.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (reparador is null)
            {
                return ReparadorResultado.NaoExiste;
            }

            var nome = mudanca.Nome?.Trim() ?? reparador.Nome;
            var cm = mudanca.AplicaCm ?? reparador.AplicaCm;
            var mf = mudanca.AplicaMf ?? reparador.AplicaMf;
            var recusa = Recusa(nome, cm, mf) ?? await NomeUsadoAsync(nome, id, cancellationToken);
            if (recusa is { } r)
            {
                return r;
            }

            reparador.Nome = nome;
            reparador.AplicaCm = cm;
            reparador.AplicaMf = mf;
            reparador.Ativo = mudanca.Ativo ?? reparador.Ativo;
            await db.SaveChangesAsync(cancellationToken);
            return ReparadorResultado.Gravado;
        });

    private static ReparadorResultado? Recusa(string nome, bool cm, bool mf) =>
        nome.Length == 0 ? ReparadorResultado.NomeVazio
        : nome.Length > Reparador.NomeMaxLength ? ReparadorResultado.NomeLongo
        : !cm && !mf ? ReparadorResultado.SemFamilia
        : null;

    private async Task<ReparadorResultado?> NomeUsadoAsync(string nome, int? outroQue, CancellationToken cancellationToken)
    {
        var maiusculas = nome.ToUpperInvariant();
        var usado = await db.Reparadores.AsNoTracking()
            .AnyAsync(r => r.Id != outroQue && r.Nome.ToUpper() == maiusculas, cancellationToken);
        return usado ? ReparadorResultado.JaExiste : null;
    }
}

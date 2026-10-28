using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Armazem;

/// <summary>
/// As fábricas da BA sobre o <see cref="DmoDbContext"/>, como os reparadores:
/// a sigla é única (a verificação aqui; o índice único é a última linha).
/// </summary>
public sealed class FabricasBa(DmoDbContext db) : IFabricasBa
{
    public async Task<IReadOnlyList<FabricaBaItem>> ListarAsync(CancellationToken cancellationToken = default) =>
        await db.FabricasBa.AsNoTracking()
            .OrderByDescending(f => f.Ativa)
            .ThenBy(f => f.Sigla)
            .Select(f => new FabricaBaItem(f.Id, f.Sigla, f.Nome, f.Ativa))
            .ToListAsync(cancellationToken);

    public Task<FabricaBaResultado> CriarAsync(string? sigla, string? nome, CancellationToken cancellationToken = default) =>
        UniqueViolationRetry.RunAsync(db, async () =>
        {
            var limpa = FabricasBaTextos.Sigla(sigla);
            var nomeLimpo = (nome ?? string.Empty).Trim();
            if (limpa is null)
            {
                return FabricaBaResultado.SiglaInvalida;
            }

            if (Recusa(nomeLimpo) is { } recusa)
            {
                return recusa;
            }

            if (await db.FabricasBa.AsNoTracking().AnyAsync(f => f.Sigla == limpa, cancellationToken))
            {
                return FabricaBaResultado.JaExiste;
            }

            db.FabricasBa.Add(new FabricaBa { Sigla = limpa, Nome = nomeLimpo });
            await db.SaveChangesAsync(cancellationToken);
            return FabricaBaResultado.Gravada;
        });

    public async Task<FabricaBaResultado> MudarAsync(int id, string? nome, bool? ativa, CancellationToken cancellationToken = default)
    {
        var fabrica = await db.FabricasBa.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fabrica is null)
        {
            return FabricaBaResultado.NaoExiste;
        }

        var nomeLimpo = nome?.Trim() ?? fabrica.Nome;
        if (Recusa(nomeLimpo) is { } recusa)
        {
            return recusa;
        }

        fabrica.Nome = nomeLimpo;
        fabrica.Ativa = ativa ?? fabrica.Ativa;
        await db.SaveChangesAsync(cancellationToken);
        return FabricaBaResultado.Gravada;
    }

    private static FabricaBaResultado? Recusa(string nome) =>
        nome.Length == 0 ? FabricaBaResultado.NomeVazio
        : nome.Length > FabricaBa.NomeMaxLength ? FabricaBaResultado.NomeLongo
        : null;
}

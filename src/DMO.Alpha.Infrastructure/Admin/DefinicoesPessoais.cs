using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>As definições pessoais na tabela UserSettings (uma linha por utilizador e chave).</summary>
public sealed class DefinicoesPessoais(DmoDbContext db) : IDefinicoesPessoais
{
    public Task<string?> LerAsync(Guid userId, string key, CancellationToken cancellationToken = default) =>
        db.UserSettings.AsNoTracking()
            .Where(s => s.UserId == userId && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task GravarAsync(Guid userId, string key, string value, CancellationToken cancellationToken = default)
    {
        var linha = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key, cancellationToken);
        if (linha is null)
        {
            db.UserSettings.Add(new UserSetting { UserId = userId, Key = key, Value = value });
        }
        else
        {
            linha.Value = value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

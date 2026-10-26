using System.Security.Cryptography;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>O posto da Reparação Interna: o PIN de cada reparador em UserSettings, só em hash.</summary>
public sealed class PostoRi(DmoDbContext db, IAdminAuthorizationService autorizacao) : IPostoRi
{
    private const int Iteracoes = 100_000;

    public async Task<IReadOnlyList<PostoReparador>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var comPin = await db.UserSettings.AsNoTracking()
            .Where(s => s.Key == PostoRiRegras.Chave)
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken);
        var candidatos = await db.Users.AsNoTracking()
            .Where(u => !u.IsStandBy && comPin.Contains(u.Id))
            .OrderBy(u => u.Name)
            .Select(u => new PostoReparador(u.Id, u.Name))
            .ToListAsync(cancellationToken);

        var lista = new List<PostoReparador>();
        foreach (var candidato in candidatos)
        {
            var acesso = await autorizacao.ResolveEffectiveAccessAsync(candidato.Id, cancellationToken);
            if (acesso is { IsAdmin: false, IsStandBy: false } && PostoRiRegras.TemRi(acesso.Capabilities))
            {
                lista.Add(candidato);
            }
        }

        return lista;
    }

    public Task<bool> TemPinAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.UserSettings.AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.Key == PostoRiRegras.Chave, cancellationToken);

    public async Task<PinGravacao> DefinirPinAsync(Guid userId, string? pin, CancellationToken cancellationToken = default)
    {
        if (!PinDasDefinicoesRegras.Valido(pin))
        {
            return PinGravacao.Invalido;
        }

        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin!, sal, Iteracoes, HashAlgorithmName.SHA256, 32);
        var valor = $"v1.{Iteracoes}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";

        var linha = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == PostoRiRegras.Chave, cancellationToken);
        if (linha is null)
        {
            db.UserSettings.Add(new UserSetting { UserId = userId, Key = PostoRiRegras.Chave, Value = valor });
        }
        else
        {
            linha.Value = valor;
        }

        // A conta do posto não passa pelo ecrã da palavra-passe temporária:
        // o reparador só entra com o PIN.
        var utilizador = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (utilizador is { RequiresPasswordChange: true })
        {
            utilizador.RequiresPasswordChange = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        return PinGravacao.Gravado;
    }

    public async Task<bool> ConfereAsync(Guid userId, string? pin, CancellationToken cancellationToken = default)
    {
        var guardado = await db.UserSettings.AsNoTracking()
            .Where(s => s.UserId == userId && s.Key == PostoRiRegras.Chave)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (guardado is null || string.IsNullOrEmpty(pin))
        {
            return false;
        }

        var partes = guardado.Split('.');
        if (partes.Length != 4 || partes[0] != "v1" || !int.TryParse(partes[1], out var iteracoes))
        {
            return false;
        }

        try
        {
            var sal = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var obtido = Rfc2898DeriveBytes.Pbkdf2(pin, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(obtido, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

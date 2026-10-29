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
        // Duas consultas, seja qual for o número de reparadores: os
        // candidatos (ativos, com PIN, por nome) e o acesso de todos de uma
        // vez, pelas mesmas regras da autorização.
        var candidatos = await db.Users.AsNoTracking()
            .Where(u => !u.IsStandBy
                && db.UserSettings.Any(s => s.UserId == u.Id && s.Key == PostoRiRegras.Chave))
            .OrderBy(u => u.Name)
            .Select(u => new PostoReparador(u.Id, u.Name))
            .ToListAsync(cancellationToken);
        var acessos = await autorizacao.ResolveEffectiveAccessAsync(
            candidatos.Select(c => c.Id).ToList(), cancellationToken);

        return candidatos
            .Where(c => acessos.GetValueOrDefault(c.Id) is { IsAdmin: false, IsStandBy: false } acesso
                && PostoRiRegras.TemRi(acesso.Capabilities))
            .ToList();
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

        // AUTH-B03 (DMO-655): um PIN novo sobe a versão da sessão, tal como
        // uma palavra-passe nova sobe a do login normal — sem isto, uma
        // sessão do posto aberta com o PIN antigo continuava válida depois
        // de o PIN mudar (AdminSessionCookieValidator recusa pela versão,
        // o mesmo caminho que já lê para o login normal).
        if (utilizador is not null)
        {
            utilizador.SessionVersion++;
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

using System.Security.Cryptography;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>O PIN das Definições em ModuleSettings, só em hash (PBKDF2-SHA256, sal aleatório).</summary>
public sealed class PinDasDefinicoes(DmoDbContext db) : IPinDasDefinicoes
{
    private const int Iteracoes = 100_000;

    public Task<string?> HashAsync(CancellationToken cancellationToken = default) =>
        db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == PinDasDefinicoesRegras.Chave)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PinGravacao> DefinirAsync(string? pin, CancellationToken cancellationToken = default)
    {
        if (!PinDasDefinicoesRegras.Valido(pin))
        {
            return PinGravacao.Invalido;
        }

        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin!, sal, Iteracoes, HashAlgorithmName.SHA256, 32);
        var valor = $"v1.{Iteracoes}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";

        var linha = await db.ModuleSettings.FirstOrDefaultAsync(s => s.SettingKey == PinDasDefinicoesRegras.Chave, cancellationToken);
        if (linha is null)
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = PinDasDefinicoesRegras.Chave, SettingValue = valor });
        }
        else
        {
            linha.SettingValue = valor;
        }

        await db.SaveChangesAsync(cancellationToken);
        return PinGravacao.Gravado;
    }

    public async Task<bool> ConfereAsync(string? pin, CancellationToken cancellationToken = default)
    {
        var guardado = await HashAsync(cancellationToken);
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
            var calculado = Rfc2898DeriveBytes.Pbkdf2(pin, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

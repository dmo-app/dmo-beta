using System.Security.Cryptography;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// Mint do PIN temporário de bootstrap/reset (USERS.md §3/§5): 4 números,
/// pedido do Diogo de 17/10/2026 para o ADMIN o ler ao utilizador sem
/// enganos. Gerado por aleatoriedade criptográfica. É APENAS o token de
/// entrega do ADMIN: o DMO nunca o persiste (o provider é dono da
/// credencial) e a palavra-passe definitiva é sempre a escolha privada do
/// utilizador no login seguinte. Para o Supabase segue com o prefixo de
/// <see cref="Authentication.SupabasePassword"/>.
/// </summary>
internal static class TemporaryPasswordMinter
{
    internal const int Length = 4;

    internal static string Mint() =>
        RandomNumberGenerator.GetInt32(10_000).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
}

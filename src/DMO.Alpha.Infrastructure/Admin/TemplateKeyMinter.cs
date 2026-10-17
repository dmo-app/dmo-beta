using System.Security.Cryptography;

namespace DMO.Alpha.Infrastructure.Admin;

/// <summary>
/// Mint da TemplateKey canónica: «tpl-» + 8 hex minúsculos (ex.:
/// «tpl-a4f91c2b»), gerada por aleatoriedade criptográfica — NUNCA
/// sequencial, NUNCA derivada do rótulo nem de factos da Template
/// (precedente <c>ToolIdMinter</c> do registo Ferramentas: a ameaça real
/// evitada é a identidade de atributos visíveis). O mint é verificado
/// contra o registo existente com re-mint em colisão: a unicidade real
/// da TemplateKey é a última linha da verdade persistida (o índice único
/// da tabela impõe-na).
/// </summary>
internal static class TemplateKeyMinter
{
    /// <summary>«tpl-» + 8 caracteres hex minúsculos (4 bytes aleatórios).</summary>
    internal static string Mint() =>
        "tpl-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();

    /// <summary>
    /// Minta uma TemplateKey nova garantidamente ausente do registo
    /// (verificação + re-mint): a probabilidade de colisão de 32 bits é
    /// desprezável, mas a verificação mantém a promessa mesmo com o
    /// registo cheio de keys de eras anteriores.
    /// </summary>
    internal static async Task<string> MintUnusedAsync(
        Func<string, Task<bool>> existsAsync,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var candidate = Mint();
            if (!await existsAsync(candidate))
            {
                return candidate;
            }
        }
    }
}

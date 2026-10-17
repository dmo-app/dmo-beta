using System.Security.Cryptography;

namespace DMO.Alpha.Infrastructure.Tools;

/// <summary>
/// Mint do tool_id canónico: «T» + 10 hex minúsculos (ex.: «Ta4f91c2b7d»),
/// gerado por aleatoriedade criptográfica — NUNCA sequencial, NUNCA
/// derivado de factos da Tool (referência/lote/tipo/máquina). O mint é
/// verificado contra o registo existente com re-mint em colisão: a
/// existência real é a última linha da verdade persistida (a PK da Tools
/// impede a duplicação). A linha DUMMY (REMOVED) nunca colide com o mint
/// (formato distinto) e nunca é gerada.
/// </summary>
internal static class ToolIdMinter
{
    /// <summary>«T» + 10 caracteres hex minúsculos (5 bytes aleatórios).</summary>
    internal static string Mint() =>
        "T" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();

    /// <summary>
    /// Minta um tool_id novo garantidamente ausente do registo
    /// (verificação + re-mint): a probabilidade de colisão de 48 bits é
    /// desprezável, mas a verificação mantém a promessa mesmo com o
    /// registo cheio de ids de eras anteriores.
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

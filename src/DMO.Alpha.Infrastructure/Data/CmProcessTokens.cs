using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Conversão entre o <see cref="CmProcess"/> e o token persistido no
/// contexto técnico das Tools e na configuração do Controlo (exatamente NNPB
/// e PS). Os conversores do EF Core são árvores de expressão, pelo que o
/// switch/throw vive aqui dentro de chamadas de método simples.
/// </summary>
internal static class CmProcessTokens
{
    public static string ToStorage(CmProcess process) => process switch
    {
        CmProcess.Nnpb => "NNPB",
        CmProcess.Ps => "PS",
        _ => throw new ArgumentOutOfRangeException(nameof(process), process, "Processo de fabrico desconhecido.")
    };

    public static CmProcess FromStorage(string token) => token switch
    {
        "NNPB" => CmProcess.Nnpb,
        "PS" => CmProcess.Ps,
        _ => throw new FormatException($"Processo de fabrico desconhecido: {token}.")
    };
}

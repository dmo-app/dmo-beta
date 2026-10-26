namespace DMO.Alpha.Core.Production;

/// <summary>
/// O que o cartão da folha traz quando, no Editar, se escolhe outra
/// ferramenta (Diogo, 26/10/2026: «quando altero para ferramenta nova não
/// atualiza em tempo real», «% uso deveria ter passado para 0%»).
/// </summary>
public interface IFolhaDaFerramenta
{
    Task<FolhaDaFerramenta> ParaAsync(string tipo, string toolId, int? excetoJobOnId, CancellationToken cancellationToken = default);
}

/// <param name="Utilizacao">A última % de uso da ferramenta; 0 quando nunca teve leitura (é nova).</param>
/// <param name="DoJobOn">O último Job On em que a ferramenta foi usada; null quando nunca foi.</param>
/// <param name="Campos">Os campos do cartão (e do AN, com a BQ) nesse Job On, por chave; vazio sem Job On.</param>
public sealed record FolhaDaFerramenta(decimal Utilizacao, int? DoJobOn, IReadOnlyDictionary<string, string?> Campos);

namespace DMO.Alpha.Core.Production;

/// <summary>Uma linha CAL gravada, pela ordem.</summary>
public sealed record JobOnFolhaCalLida(int Ordem, string? Designacao, string? Valor, string? QtdMaq);

/// <summary>
/// A folha de um Job On como se lê: o valor de cada chave do catálogo
/// <see cref="JobOnFolhaCampos"/> (todas as chaves, null quando vazio) e as
/// linhas CAL gravadas, pela ordem. Um Job On sem linha na <c>JobOnFolhas</c>
/// lê-se como folha vazia: todas as chaves a null e nenhuma linha CAL.
/// </summary>
public sealed record JobOnFolhaLida(
    int JobOnId,
    IReadOnlyDictionary<string, string?> Valores,
    IReadOnlyList<JobOnFolhaCalLida> Cal)
{
    /// <summary>O valor da <paramref name="chave"/> (null = vazio).</summary>
    /// <exception cref="ArgumentException">A chave não é do catálogo.</exception>
    public string? Valor(string chave) =>
        Valores.TryGetValue(chave, out var valor)
            ? valor
            : throw new ArgumentException($"A chave {chave} não é um campo da folha.", nameof(chave));

    /// <summary>Sem nada escrito: todos os campos vazios e nenhuma linha CAL gravada.</summary>
    public bool Vazia => Cal.Count == 0 && Valores.Values.All(v => v is null);

    /// <summary>A folha vazia de um Job On: todas as chaves do catálogo a null, sem linhas CAL.</summary>
    public static JobOnFolhaLida VaziaDe(int jobOnId) =>
        new(jobOnId, JobOnFolhaCampos.Todos.ToDictionary(c => c.Chave, _ => (string?)null, StringComparer.Ordinal), []);
}

/// <summary>
/// A leitura da folha de um Job On, para o ecrã. Nunca há um caso «sem
/// folha»: um Job On sem linha devolve a folha vazia.
/// </summary>
public interface IJobOnFolhaQuery
{
    /// <summary>Os valores por chave e as linhas CAL do Job On, pela ordem; a folha vazia quando não há linha.</summary>
    Task<JobOnFolhaLida> ForJobOnAsync(int jobOnId, CancellationToken cancellationToken = default);
}

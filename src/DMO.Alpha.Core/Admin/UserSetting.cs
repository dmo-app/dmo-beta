namespace DMO.Alpha.Core.Admin;

/// <summary>
/// Uma definição PESSOAL (Diogo, 22/10/2026: «definições num só sítio»,
/// parte «Pessoal»): vale só para o utilizador que a grava, em qualquer
/// computador. As definições para todos ficam em ModuleSettings.
/// </summary>
public sealed class UserSetting
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>A chave canónica (ex.: Pessoal:Tema).</summary>
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;
}

/// <summary>As chaves das definições pessoais (vocabulário fechado).</summary>
public static class UserSettingKeys
{
    /// <summary>O modo claro ou escuro: «light» ou «dark».</summary>
    public const string Tema = "Pessoal:Tema";

    /// <summary>
    /// A última vez que a conta abriu o Sumário do Planeamento (UTC, ISO
    /// 8601): o ponto do separador só aparece com linhas importantes depois
    /// disto (Diogo, 23/10/2026).
    /// </summary>
    public const string SumarioVistoEm = "Pessoal:SumarioVistoEm";

    /// <summary>
    /// Quantas horas a sessão dura sem atividade, escolhidas pelo utilizador
    /// (Diogo, 26/10/2026); vale a partir da entrada seguinte.
    /// </summary>
    public const string SessaoHoras = "Pessoal:SessaoHoras";

    /// <summary>
    /// Os avisos do Job On que esta pessoa escolheu não ver (Diogo,
    /// 30/10/2026): as chaves de <see cref="AvisosPessoais"/> separadas por
    /// vírgulas; vazio ou sem linha = vê todos.
    /// </summary>
    public const string AvisosEscondidos = "Pessoal:AvisosEscondidos";
}

/// <summary>
/// Os avisos das ferramentas na folha do Job On que cada pessoa pode
/// desligar nas Definições › A minha conta (Diogo, 30/10/2026: «ao usar map
/// abre sempre imensos avisos do armazém»; «os de sistema funcionamento das
/// páginas não se deve esconder»). Os erros e as mensagens das páginas não
/// entram aqui.
/// </summary>
public static class AvisosPessoais
{
    public const string Armazem = "armazem";
    public const string B2 = "b2";
    public const string Compatibilidade = "compatibilidade";
    public const string UsoParalelo = "uso-paralelo";

    public static IReadOnlyList<(string Chave, string Nome)> Todos { get; } =
    [
        (Armazem, "Ferramenta sem registo no Armazém"),
        (B2, "B2 sem Dupla nem Tripla"),
        (Compatibilidade, "Ferramenta não compatível com a máquina"),
        (UsoParalelo, "Ferramenta em uso noutro Job On ao mesmo tempo")
    ];

    /// <summary>As chaves escondidas gravadas; ignora as que não existem.</summary>
    public static IReadOnlySet<string> Ler(string? valor) =>
        (valor ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(chave => Todos.Any(a => a.Chave == chave))
            .ToHashSet(StringComparer.Ordinal);

    public static string Escrever(IEnumerable<string> escondidos) =>
        string.Join(",", Todos.Select(a => a.Chave).Where(escondidos.Contains));
}

/// <summary>As durações de sessão permitidas (horas) e a por omissão.</summary>
public static class SessaoDuracao
{
    public const int PorOmissao = 1;

    public static readonly int[] Opcoes = [1, 2, 4, 8, 12, 24];

    public static int Ler(string? valor) =>
        int.TryParse(valor, out var h) && Opcoes.Contains(h) ? h : PorOmissao;
}

/// <summary>Lê e grava as definições pessoais do utilizador.</summary>
public interface IDefinicoesPessoais
{
    Task<string?> LerAsync(Guid userId, string key, CancellationToken cancellationToken = default);

    Task GravarAsync(Guid userId, string key, string value, CancellationToken cancellationToken = default);
}

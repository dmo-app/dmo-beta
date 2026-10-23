namespace DMO.Alpha.Core.Production;

/// <summary>
/// Uma ALTERAÇÃO de um Job On já gravado (o proprietário, 17/10/2026: o Job
/// On apresenta o problema e leva à solução). Cada edição que muda a máquina,
/// as datas, a calote ou uma ferramenta grava uma linha por campo, com o valor
/// antes e depois congelados como texto, quem mudou e quando. A ficha mostra
/// estas linhas em «O que mudou», junto com as mudanças de lote, cada uma com
/// a ação a fazer no módulo que a alteração afeta.
/// </summary>
public sealed class JobOnChange
{
    public const int ValueMaxLength = 120;

    public int Id { get; set; }

    public int JobOnId { get; set; }

    public JobOn? JobOn { get; set; }

    /// <summary>O campo alterado: um dos valores de <see cref="JobOnChangeFields"/>.</summary>
    public string Field { get; set; } = null!;

    /// <summary>O valor antes, como se lia na ficha (null = vazio).</summary>
    public string? OldValueFrozen { get; set; }

    /// <summary>O valor depois, como se lê na ficha (null = vazio).</summary>
    public string? NewValueFrozen { get; set; }

    /// <summary>O nome de quem alterou, congelado; null quando o pedido chegou sem identidade.</summary>
    public string? ChangedByNameFrozen { get; set; }

    /// <summary>O utilizador de <see cref="ChangedByNameFrozen"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? ChangedByUserId { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    // REMOVER POR ENGANO (Diogo, 23/10/2026: «ter forma de remover o notice
    // de troca em caso de engano»). Nada se perde: a linha fica, marcada como
    // removida, com quem e quando. Os valores do Job On não mudam; só o aviso
    // sai da marca laranja, do resumo da linha, do Sumário e dos módulos.

    /// <summary>Quando foi removida por engano (UTC); null = ativa.</summary>
    public DateTime? RemovedAtUtc { get; set; }

    /// <summary>O nome de quem removeu, congelado.</summary>
    public string? RemovedByNameFrozen { get; set; }

    /// <summary>O utilizador de <see cref="RemovedByNameFrozen"/> (<c>Users.Id</c>).</summary>
    public Guid? RemovedByUserId { get; set; }

    /// <summary>
    /// O histórico só conta o que mudou depois de a produção começar (o Diogo,
    /// 17/10/2026): uma alteração feita no dia de início ou depois, no dia da
    /// fábrica (Lisboa). Sem data de início, a produção ainda não começou.
    /// </summary>
    public static bool AfterStart(DateTime changedAtUtc, DateOnly? inicioEm) =>
        inicioEm is { } inicio
        && DateOnly.FromDateTime(Runtime.DmoBusinessTime.ToLocal(changedAtUtc)) >= inicio;
}

/// <summary>Os campos do Job On cuja alteração fica registada.</summary>
public static class JobOnChangeFields
{
    public const string Machine = "Maquina";
    public const string Start = "Inicio";
    public const string End = "Fim";
    public const string Calote = "Calote";
    public const string Cm = "CM";
    public const string Mf = "MF";
    public const string Bq = "BQ";

    /// <summary>
    /// A folha da fábrica (os campos escritos e a CAL): uma linha por edição
    /// que a muda, com os rótulos do que mudou no valor novo («CM · QTD Stock,
    /// CAL»), sem o valor de antes.
    /// </summary>
    public const string Folha = "Folha";

    /// <summary>
    /// Acrescentar uma BQ a uma produção que não tinha BQ NÃO é uma mudança
    /// (o Diogo: «a mudança das boquilhas não aparecer [...] quando for de um
    /// estado de não existir bq»): não se grava, e as linhas antigas assim
    /// gravadas não aparecem na ficha, na linha da máquina nem nos avisos.
    /// </summary>
    public static bool IsChange(string field, string? oldValue) =>
        !(field == Bq && oldValue == null);

    /// <summary>A regra de <see cref="IsChange"/> para as consultas.</summary>
    public static readonly System.Linq.Expressions.Expression<Func<JobOnChange, bool>> Counts =
        c => !(c.Field == Bq && c.OldValueFrozen == null);

    /// <summary>A alteração não foi removida por engano: só estas contam na marca, no resumo, no Sumário e nos avisos.</summary>
    public static readonly System.Linq.Expressions.Expression<Func<JobOnChange, bool>> Active =
        c => c.RemovedAtUtc == null;

    /// <summary>O nome do campo como se lê na ficha.</summary>
    public static string Label(string field) => field switch
    {
        Machine => "a linha", // «Linha», nunca «máquina» (Diogo, 23/10/2026)
        Start => "o início",
        End => "o fim",
        Calote => "a calote",
        Cm => "a CM",
        Mf => "o MF",
        Bq => "a BQ",
        Folha => "a folha",
        _ => field
    };

    /// <summary>
    /// O módulo a rever depois de mudar este campo (ou o slot de uma mudança
    /// de lote): a CM e a calote mudam o Peso; o MF muda os Pegamentos, que se
    /// abrem pelo Controlo; a BQ pede rever o Controlo; a máquina e as datas
    /// mudam o que as Boquilhas mostram em cada máquina.
    /// </summary>
    public static JobOnFollowUp FollowUp(string field) => field switch
    {
        Cm or Calote => JobOnFollowUp.Peso,
        Mf => JobOnFollowUp.Pegamentos,
        Bq => JobOnFollowUp.Controlo,
        // A folha é informação para quem prepara a máquina: nenhum módulo a rever.
        Folha => JobOnFollowUp.Nenhum,
        _ => JobOnFollowUp.Boquilhas
    };

    /// <summary>A frase da ação para o operador.</summary>
    public static string FollowUpLabel(JobOnFollowUp followUp) => followUp switch
    {
        JobOnFollowUp.Peso => "Verificar o Peso",
        JobOnFollowUp.Pegamentos => "Verificar os Pegamentos",
        JobOnFollowUp.Controlo => "Rever o Controlo",
        JobOnFollowUp.Nenhum => string.Empty,
        _ => "Confirmar nas Boquilhas"
    };

    /// <summary>O que o Peso vigia: a CM, a BQ e a calote entram no registo do Peso.</summary>
    public static readonly IReadOnlyList<string> PesoWatches = new[] { Cm, Bq, Calote };

    /// <summary>O que os Pegamentos vigiam: as três ferramentas da combinação.</summary>
    public static readonly IReadOnlyList<string> PegamentosWatches = new[] { Cm, Mf, Bq };

    /// <summary>O que a aprovação do Controlo vigia: as ferramentas e a calote.</summary>
    public static readonly IReadOnlyList<string> ControloWatches = new[] { Cm, Mf, Bq, Calote };

    /// <summary>O que um registo das Boquilhas vigia: a máquina, as datas e a BQ.</summary>
    public static readonly IReadOnlyList<string> BoquilhasWatches = new[] { Machine, Start, End, Bq };

    /// <summary>O nome curto do campo, para a linha da máquina («CM», «Fim»).</summary>
    public static string ShortLabel(string field) => field switch
    {
        Machine => "Máquina",
        Start => "Início",
        End => "Fim",
        Calote => "Calote",
        Folha => "Folha",
        _ => field
    };

    /// <summary>
    /// O que mudou, numa linha, para a barra da máquina (o Diogo, 18/10/2026:
    /// «Mudança CM Sai 66 entra 67», porque numa ferramenta muda sempre o
    /// lote): por campo, o valor de antes da primeira mudança e o de agora,
    /// do campo mudado mais recentemente para o mais antigo —
    /// «Mudança CM · Sai 66 entra 67; Fim · Sai 18/10/2026 entra 20/10/2026».
    /// </summary>
    public static string Summarize(IEnumerable<JobOnChangeView> newestFirst) =>
        "Mudança " + string.Join("; ", newestFirst
            .GroupBy(c => c.Field)
            .Select(g =>
            {
                // Na folha, o que mudou da última vez («Folha · CM · QTD Stock»).
                if (g.Key == Folha)
                {
                    return $"{ShortLabel(g.Key)} · {g.First().NewValue ?? "—"}";
                }

                var (sai, entra) = SaiEntra(g.Key, g.Last().OldValue, g.First().NewValue);
                return $"{ShortLabel(g.Key)} · Sai {sai} entra {entra}";
            }));

    // Numa ferramenta com a mesma referência («6018 · lote 66» → «6018 · lote
    // 67») só o lote conta; com outra referência fica a ferramenta inteira.
    private static (string Sai, string Entra) SaiEntra(string field, string? oldValue, string? newValue)
    {
        if (field is Cm or Mf or Bq
            && SplitLot(oldValue) is { } antes
            && SplitLot(newValue) is { } depois
            && antes.Reference == depois.Reference)
        {
            return (antes.Lot, depois.Lot);
        }

        return (oldValue ?? "—", newValue ?? "—");
    }

    private static (string Reference, string Lot)? SplitLot(string? value)
    {
        const string marca = " · lote ";
        var at = value?.LastIndexOf(marca, StringComparison.Ordinal) ?? -1;
        return at > 0 ? (value![..at], value[(at + marca.Length)..]) : null;
    }

    /// <summary>«Mudou a BQ: 5447 · lote 3 → 5447 · lote 4».</summary>
    public static string Describe(string field, string? oldValue, string? newValue) =>
        field == Folha
            // «Mudou a folha: CM · QTD Stock, CAL» (o que mudou, sem os valores).
            ? $"Mudou {Label(field)}: {newValue ?? "—"}"
            : $"Mudou {Label(field)}: {oldValue ?? "—"} → {newValue ?? "—"}";
}

/// <summary>O módulo para onde a ação de uma alteração leva.</summary>
public enum JobOnFollowUp
{
    Peso,
    Pegamentos,
    Controlo,
    Boquilhas,

    /// <summary>Nenhum módulo a rever (a folha da fábrica): a linha fica só como informação.</summary>
    Nenhum
}

/// <summary>Uma alteração registada, pronta a mostrar.</summary>
public sealed record JobOnChangeView(
    int JobOnId,
    string Field,
    string Text,
    string? ChangedBy,
    DateTime ChangedAtUtc)
{
    /// <summary>O valor antes da alteração (para a linha da máquina).</summary>
    public string? OldValue { get; init; }

    /// <summary>O valor depois da alteração (para a linha da máquina).</summary>
    public string? NewValue { get; init; }

    /// <summary>O Id da linha (o «Remover por engano»).</summary>
    public int Id { get; init; }

    /// <summary>Quando foi removida por engano; null = ativa.</summary>
    public DateTime? RemovedAtUtc { get; init; }

    /// <summary>Quem a removeu.</summary>
    public string? RemovedBy { get; init; }
}

/// <summary>
/// O aviso num módulo (o proprietário, 17/10/2026): as alterações do Job On
/// feitas depois do registo, só as que esse módulo vigia. É informação: não
/// bloqueia nada e um Controlo aprovado continua aprovado.
/// </summary>
public sealed record JobOnChangeNotice(int JobOnId, IReadOnlyList<JobOnChangeView> Changes);

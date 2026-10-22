namespace DMO.Alpha.Core.Production;

/// <summary>
/// A calculadora canónica do overlap de janelas na mesma máquina (o
/// Owner 2026-10-12: «overlap da maquina deve dar um warning, nao deve
/// bloquear, o warning deve dizer detalhadamente o que esta mal como
/// resolver»). Lógica PURA sobre factos — os mesmos dias que os handlers
/// de gravação (Create/Update) e a folha /JobOn (o recompute do PRG)
/// calculam: os nulls são as janelas em aberto (InicioEm null = desde
/// sempre; FimEm null = até aviso — a produção corrente até próxima
/// indicação). O resultado é INFORMATIVO: quem chama grava SEMPRE a
/// produção e apenas transporta o aviso.
/// </summary>
public static class JobOnOverlap
{
    /// <summary>
    /// Avalia a janela da produção gravada contra as outras produções da
    /// MESMA máquina. Devolve o aviso com as produções em conflito (as
    /// janelas de cada, o período exato da sobreposição e a sugestão de
    /// resolução) ou null quando não há sobreposição a assinalar. A
    /// janela da nova produção INTEIRAMENTE em aberto (ambos os dias
    /// null) não afirma período nenhum — nada é comparado (o registo
    /// truthful continua; o aviso nasce quando houver dias).
    /// </summary>
    public static OverlapWarning? Evaluate(
        DateOnly? inicioEm,
        DateOnly? fimEm,
        string machineCode,
        IReadOnlyList<OverlapCandidate> othersOnMachine)
    {
        if (inicioEm is null && fimEm is null)
        {
            return null;
        }

        var conflicts = new List<OverlappingProduction>();
        foreach (var other in othersOnMachine)
        {
            // Tal como a janela nova sem dias, uma produção existente
            // sem qualquer data não afirma um período para comparar.
            if (other.InicioEm is null && other.FimEm is null)
            {
                continue;
            }

            // Duas janelas de DIAS sobrepõem-se quando se intersectam —
            // os nulls abrem o limite correspondente (início null = desde
            // sempre; fim null = até aviso): uma produção «até aviso»
            // sobrepõe tudo o que comece antes do seu fim; uma produção
            // sem início sobrepõe tudo o que não acabe antes de começar.
            if (!Crosses(inicioEm, fimEm, other.InicioEm, other.FimEm))
            {
                continue;
            }

            // O período exato da sobreposição: o max dos inícios e o min
            // dos fins — null quando o limite fica em aberto.
            var overlapFrom = MaxDate(inicioEm, other.InicioEm);
            var overlapUntil = MinDate(fimEm, other.FimEm);
            conflicts.Add(new OverlappingProduction(
                other.JobOnId,
                other.Reference,
                other.ProductionNumber,
                other.InicioEm,
                other.FimEm,
                overlapFrom,
                overlapUntil));
        }

        if (conflicts.Count == 0)
        {
            return null;
        }

        return new OverlapWarning(
            conflicts,
            machineCode,
            BuildMessage(inicioEm, fimEm, machineCode, conflicts));
    }

    /// <summary>
    /// Duas janelas de DIAS cruzam-se quando se intersectam, os nulls abrem
    /// o limite correspondente (início null = desde sempre; fim null = até
    /// aviso). O dia da troca não conta (Diogo, 21/10/2026: «uma producao
    /// sai dia 20 outra entra dia 20»): uma produção que sai no dia em que
    /// a outra entra não se sobrepõe a ela.
    /// </summary>
    public static bool Crosses(DateOnly? aInicio, DateOnly? aFim, DateOnly? bInicio, DateOnly? bFim) =>
        (aInicio is null || bFim is null || aInicio.Value < bFim.Value)
        && (bInicio is null || aFim is null || bInicio.Value < aFim.Value);

    private static DateOnly? MaxDate(DateOnly? a, DateOnly? b) =>
        a is null || b is null ? a ?? b : (a.Value > b.Value ? a.Value : b.Value);

    private static DateOnly? MinDate(DateOnly? a, DateOnly? b) =>
        a is null || b is null ? a ?? b : (a.Value < b.Value ? a.Value : b.Value);

    private static string Date(DateOnly value) =>
        value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A janela humana: os nulls pelo nome que a folha lhes dá.</summary>
    private static string WindowText(DateOnly? inicioEm, DateOnly? fimEm) => (inicioEm, fimEm) switch
    {
        (null, null) => "em aberto (sem dias registados)",
        ({ } i, null) => $"de {Date(i)} até aviso",
        (null, { } f) => $"até {Date(f)} (início em aberto)",
        ({ } i, { } f) => $"{Date(i)}–{Date(f)}"
    };

    /// <summary>O período exato da sobreposição, com os limites em aberto pelo nome.</summary>
    private static string OverlapText(DateOnly? from, DateOnly? until) => (from, until) switch
    {
        (null, { } u) => $"até {Date(u)} (início em aberto)",
        ({ } f, null) => $"de {Date(f)} até aviso",
        ({ } f, { } u) => $"{Date(f)}–{Date(u)}",
        (null, null) => "a janela inteira (em aberto)"
    };

    /// <summary>
    /// A mensagem detalhada — o que está mal e COMO RESOLVER (o Owner:
    /// «o warning deve dizer detalhadamente o que esta mal como
    /// resolver»): a janela gravada, cada produção em conflito (a
    /// referência total, o nº, a janela dela, o período da sobreposição)
    /// e a sugestão concreta — ajustar o início da nova para depois do
    /// fim da última em conflito, ou editar a anterior na folha do Job
    /// On.
    /// </summary>
    private static string BuildMessage(
        DateOnly? inicioEm,
        DateOnly? fimEm,
        string machineCode,
        IReadOnlyList<OverlappingProduction> conflicts)
    {
        var listing = conflicts
            .Select(c => $"{c.Reference} · nº {c.ProductionNumber} · janela {WindowText(c.InicioEm, c.FimEm)} (sobreposição {OverlapText(c.OverlapFrom, c.OverlapUntil)})");
        var listingText = conflicts.Count == 1
            ? $"com a produção {listing.First()}"
            : $"com {conflicts.Count} produções: {string.Join("; ", listing)}";

        // A sugestão concreta: a ÚLTIMA a sair da máquina manda — o
        // início da nova só fica livre depois do fim dela. Um fim em
        // aberto (null) diz-se e resolve-se na folha.
        // Max sobre valores nullable ignora null; isso não pode tornar
        // finita uma ocupação cujo fim continua desconhecido.
        var latestEnd = conflicts.Any(c => c.FimEm is null)
            ? (DateOnly?)null : conflicts.Select(c => c.FimEm).Max();
        var suggestion = latestEnd is { } end
            ? $"Ajusta o início da nova para depois de {Date(end)}, ou edita a anterior na folha do Job On."
            : "O fim de pelo menos uma produção em conflito ainda está em aberto — define-o na folha do Job On quando for conhecido, ou ajusta as janelas conforme o planeamento real.";

        return $"A produção foi gravada na máquina {machineCode} com a janela {WindowText(inicioEm, fimEm)}, " +
            $"mas a janela sobrepõe-se {listingText}. {suggestion}";
    }
}

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Um lado de uma comparação de pesos: uma leitura com o SEU número/posição
/// CM (o que a oficina escreve na folha — row-level measurement data, nunca
/// um tool_id), o peso do vidro calculado pela fórmula do Peso
/// (<see cref="PesoCalculation"/> — a fonte única) e o volume/capacidade da
/// leitura quando exista.
/// </summary>
public sealed record PesoComparacaoLado(
    int Posicao,
    string CmSubject,
    decimal PesoVidroGrams,
    decimal? VolumeCm3);

/// <summary>
/// Uma linha do resultado da comparação: a leitura atual e a sua
/// contraparte (quando existe um par pelo número/posição do CM). O Δ peso
/// PRESERVA O SINAL (decisão do Owner: «+0,3 e −0,3 são fatos distintos»);
/// sem contraparte os deltas ficam indefinidos (nunca zero inventado).
/// </summary>
public sealed record PesoComparacaoParRow(
    int Posicao,
    string CmSubject,
    decimal PesoVidroGrams,
    string? CmSubjectAnterior,
    decimal? PesoAnteriorGrams,
    decimal? DeltaPesoGrams,
    decimal? DeltaVolumeCm3);

/// <summary>
/// O resultado da comparação por pares: os pares válidos (cada leitura
/// anterior usada no MÁXIMO uma vez — a regra do html), as linhas sem
/// contraparte (válido, nunca um erro) e os Δ médios dos pares com o sinal
/// preservado.
/// </summary>
public sealed record PesoComparacaoPares(
    int ParesValidos,
    int SemContraparte,
    decimal? DeltaPesoMedioGrams,
    decimal? DeltaVolumeMedioCm3,
    IReadOnlyList<PesoComparacaoParRow> Rows);

/// <summary>
/// A matemática dos PARES da comparação de pesos (o <c>D.compare</c> do
/// dmo-html controlo-v04): as leituras emparelham pelo NÚMERO/POSIÇÃO do CM
/// que a oficina escreve («os pares por posição do CM») — a mesma leitura
/// anterior nunca serve dois pares; as leituras sem contraparte ficam
/// «Sem contraparte» (a comparação nunca bloqueia por isso). O peso
/// comparado é O PESO do vidro (a fórmula total do PesoCalculation — o
/// Owner: «peso é a formula total para cada CM»), calculado PELO BACKEND
/// antes de entrar aqui. NENHUMA tolerância oculta é aplicada.
/// </summary>
public static class PesoComparacaoCalculation
{
    public static PesoComparacaoPares CalcularParesPorCm(
        IReadOnlyList<PesoComparacaoLado> atuais,
        IReadOnlyList<PesoComparacaoLado> anteriores)
    {
        ArgumentNullException.ThrowIfNull(atuais);
        ArgumentNullException.ThrowIfNull(anteriores);

        // Cada leitura anterior é usada NO MÁXIMO uma vez (o html: «D.compare
        // exige que cada leitura histórica seja usada no máximo uma vez»).
        var usados = new HashSet<int>();
        var anterioresPorOrdem = anteriores.OrderBy(l => l.Posicao).ToList();

        var rows = new List<PesoComparacaoParRow>(atuais.Count);
        var deltasPeso = new List<decimal>();
        var deltasVolume = new List<decimal>();

        foreach (var atual in atuais.OrderBy(l => l.Posicao))
        {
            var subjectAtual = atual.CmSubject ?? string.Empty;
            PesoComparacaoLado? par = null;
            var subject = subjectAtual.Trim();
            if (!string.IsNullOrEmpty(subject))
            {
                par = anterioresPorOrdem.FirstOrDefault(anterior =>
                    !usados.Contains(anterior.Posicao)
                    && MesmoCmSubject(anterior.CmSubject?.Trim(), subject));
            }

            if (par is { } contraparte)
            {
                usados.Add(contraparte.Posicao);
                var deltaPeso = atual.PesoVidroGrams - contraparte.PesoVidroGrams;
                deltasPeso.Add(deltaPeso);
                decimal? deltaVolume = atual.VolumeCm3 is { } volumeAtual && contraparte.VolumeCm3 is { } volumeAnterior
                    ? volumeAtual - volumeAnterior
                    : null;
                if (deltaVolume is { } delta)
                {
                    deltasVolume.Add(delta);
                }

                rows.Add(new PesoComparacaoParRow(
                    atual.Posicao,
                    subjectAtual,
                    atual.PesoVidroGrams,
                    contraparte.CmSubject,
                    contraparte.PesoVidroGrams,
                    deltaPeso,
                    deltaVolume));
            }
            else
            {
                rows.Add(new PesoComparacaoParRow(
                    atual.Posicao,
                    subjectAtual,
                    atual.PesoVidroGrams,
                    null,
                    null,
                    null,
                    null));
            }
        }

        var paresValidos = deltasPeso.Count;
        return new PesoComparacaoPares(
            paresValidos,
            rows.Count - paresValidos,
            paresValidos > 0 ? deltasPeso.Average() : null,
            deltasVolume.Count > 0 ? deltasVolume.Average() : null,
            rows);
    }

    /// <summary>
    /// O mesmo CM para o emparelhamento (PESO-B01, sweep 2): «01» e «1» são o
    /// mesmo número, só com zeros à esquerda — comparam-se numericamente
    /// quando os DOIS são só dígitos. Com uma letra de qualquer lado (p.ex.
    /// «01A» vs «1A») a interpretação é ambígua: fica a igualdade de texto,
    /// nunca promovida a numérica, para não emparelhar CMs diferentes por
    /// engano. Nunca altera o que é gravado ou mostrado — só esta comparação.
    /// </summary>
    private static bool MesmoCmSubject(string? a, string? b)
    {
        var x = a ?? string.Empty;
        var y = b ?? string.Empty;
        if (string.Equals(x, y, StringComparison.Ordinal))
        {
            return true;
        }

        return SoDigitos(x) && SoDigitos(y)
            && int.TryParse(x, out var nx) && int.TryParse(y, out var ny) && nx == ny;
    }

    private static bool SoDigitos(string valor) =>
        valor.Length > 0 && valor.All(char.IsAsciiDigit);
}

/// <summary>
/// Quanto uma CM mexe na média da base (o Diogo, 16/10: «ver se um CM em
/// especifico esta influenciar muito a media»): a diferença para a média da
/// base e o efeito na média se esta leitura entrasse na base — a substituir a
/// leitura da mesma CM quando existe, senão como mais uma leitura.
/// </summary>
public sealed record PesoComparacaoInfluencia(
    string CmSubject,
    decimal PesoVidroGrams,
    decimal DiferencaParaMediaGrams,
    decimal MediaComEstaCmGrams,
    decimal EfeitoNaMediaGrams,
    /// <summary>True na CM com o maior efeito na média (em valor absoluto).</summary>
    bool MaiorEfeito);

public static class PesoComparacaoInfluenciaCalculation
{
    /// <summary>
    /// As CM novas contra a base (média e nº de leituras do Peso inicial):
    /// com par, a média passa a (média·n − anterior + nova)/n; sem par,
    /// (média·n + nova)/(n + 1).
    /// </summary>
    public static IReadOnlyList<PesoComparacaoInfluencia> Novas(
        IReadOnlyList<PesoComparacaoParRow> rows,
        decimal mediaBaseGrams,
        int leiturasBase)
    {
        var lista = rows.Select(row =>
        {
            var media = leiturasBase <= 0
                ? row.PesoVidroGrams
                : row.PesoAnteriorGrams is { } anterior
                    ? (mediaBaseGrams * leiturasBase - anterior + row.PesoVidroGrams) / leiturasBase
                    : (mediaBaseGrams * leiturasBase + row.PesoVidroGrams) / (leiturasBase + 1);
            return (row.CmSubject, row.PesoVidroGrams, Diferenca: row.PesoVidroGrams - mediaBaseGrams, Media: media, Efeito: media - mediaBaseGrams);
        }).ToList();
        return Marcar(lista);
    }

    /// <summary>
    /// As CM da própria base: a diferença para a média e o efeito na média
    /// se esta CM saísse (média sem ela − média com ela, com o sinal
    /// trocado: o quanto ela puxa a média).
    /// </summary>
    public static IReadOnlyList<PesoComparacaoInfluencia> DaBase(
        IReadOnlyList<(string CmSubject, decimal PesoVidroGrams)> leituras)
    {
        if (leituras.Count == 0)
        {
            return Array.Empty<PesoComparacaoInfluencia>();
        }

        var n = leituras.Count;
        var media = leituras.Average(l => l.PesoVidroGrams);
        var lista = leituras.Select(l =>
        {
            var semEla = n > 1 ? (media * n - l.PesoVidroGrams) / (n - 1) : media;
            return (l.CmSubject, l.PesoVidroGrams, Diferenca: l.PesoVidroGrams - media, Media: semEla, Efeito: media - semEla);
        }).ToList();
        return Marcar(lista);
    }

    private static IReadOnlyList<PesoComparacaoInfluencia> Marcar(
        List<(string CmSubject, decimal PesoVidroGrams, decimal Diferenca, decimal Media, decimal Efeito)> lista)
    {
        var maior = lista.Count > 1 ? lista.Max(l => Math.Abs(l.Efeito)) : (decimal?)null;
        return lista.Select(l => new PesoComparacaoInfluencia(
            l.CmSubject,
            Round(l.PesoVidroGrams),
            Round(l.Diferenca),
            Round(l.Media),
            Round(l.Efeito),
            maior is { } m && m > 0m && Math.Abs(l.Efeito) == m)).ToList();
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

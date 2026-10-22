namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>
/// AS PUNÇÕES POR POSIÇÃO DA LINHA (Diogo, 22/10/2026): as punções
/// registadas entram, pela ordem de registo, nas posições da linha. Cada
/// secção tem 4 posições (A a D) e enchem-se secção a secção: A1, B1, C1,
/// D1, A2 … D12 na B1 (12 secções). As que passam da capacidade da linha
/// são «Reserva». O mesmo número com outro lote é um punção repetido:
/// aparece com o lote e nunca bloqueia. A posição não se grava: deriva da
/// ordem de registo, por isso retirar uma punção faz subir as seguintes.
/// </summary>
public static class PuncoesPosicoes
{
    /// <summary>As posições de cada secção (A, B, C, D).</summary>
    public const int PosicoesPorSeccao = 4;

    /// <summary>As secções de uma linha sem valor guardado nas Definições.</summary>
    public const int SeccoesPorOmissao = 12;

    public const int SeccoesMinimo = 1;

    public const int SeccoesMaximo = 40;

    /// <summary>A letra da posição (0 → A … 3 → D).</summary>
    public static string Letra(int posicao) => ((char)('A' + posicao)).ToString();

    public static PuncoesNaLinha Distribuir(IReadOnlyList<PuncaoReadModel> puncoes, int seccoes)
    {
        seccoes = Math.Clamp(seccoes, SeccoesMinimo, SeccoesMaximo);
        var capacidade = seccoes * PosicoesPorSeccao;
        var contagem = puncoes
            .GroupBy(p => p.Number.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var linha = new List<PuncaoNaPosicao>();
        var reserva = new List<PuncaoNaPosicao>();
        for (var i = 0; i < puncoes.Count; i++)
        {
            var p = puncoes[i];
            var repetido = contagem[p.Number.Trim()] > 1;
            if (i < capacidade)
            {
                var seccao = i / PosicoesPorSeccao + 1;
                var posicao = i % PosicoesPorSeccao;
                linha.Add(new PuncaoNaPosicao(i + 1, p.Number, p.Lot, repetido, seccao, posicao, $"{Letra(posicao)}{seccao}"));
            }
            else
            {
                reserva.Add(new PuncaoNaPosicao(i + 1, p.Number, p.Lot, repetido, null, null, "Reserva"));
            }
        }

        return new PuncoesNaLinha(seccoes, linha, reserva);
    }
}

/// <summary>Uma punção numa posição da linha (Seccao e Posicao null na reserva).</summary>
public sealed record PuncaoNaPosicao(
    int Ordem,
    string Number,
    string Lot,
    bool Repetido,
    int? Seccao,
    int? Posicao,
    string Etiqueta);

/// <summary>As punções de um registo distribuídas pela linha: a grelha, a reserva e os repetidos.</summary>
public sealed record PuncoesNaLinha(
    int Seccoes,
    IReadOnlyList<PuncaoNaPosicao> Linha,
    IReadOnlyList<PuncaoNaPosicao> Reserva)
{
    public int Capacidade => Seccoes * PuncoesPosicoes.PosicoesPorSeccao;

    public int Total => Linha.Count + Reserva.Count;

    /// <summary>Todas as punções repetidas (o mesmo número mais de uma vez), pela ordem de registo.</summary>
    public IReadOnlyList<PuncaoNaPosicao> Repetidos => Linha.Concat(Reserva).Where(p => p.Repetido).ToArray();

    /// <summary>A punção na secção (1…) e posição (0 = A … 3 = D); null quando vazia.</summary>
    public PuncaoNaPosicao? Em(int seccao, int posicao)
    {
        var indice = (seccao - 1) * PuncoesPosicoes.PosicoesPorSeccao + posicao;
        return indice >= 0 && indice < Linha.Count ? Linha[indice] : null;
    }
}

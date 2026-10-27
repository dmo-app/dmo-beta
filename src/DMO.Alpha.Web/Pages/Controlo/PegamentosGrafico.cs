using System.Globalization;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Controlo;

namespace DMO.Alpha.Web.Pages.Controlo;

/// <summary>
/// O MAPA DE LIMITES NO ECRÃ (Diogo, 27/10/2026: «arranjar o gráfico»). O
/// desenho do PDF (<see cref="PegamentosLimitMap"/>) fica como está; no ecrã o
/// gráfico é HTML com o texto ao tamanho real (o SVG de 1700 de largura
/// encolhia as letras para 5 px):
/// - a escala ajusta-se às medições e aos limites, com uma margem, nunca a
///   um intervalo fixo;
/// - cada componente medido numa linha só sua, com o nome à esquerda;
/// - os componentes sem medições não ocupam linhas: uma nota curta por baixo.
/// As posições são percentagens da largura (0 a 100), por isso o gráfico
/// enche o cartão a qualquer largura.
/// </summary>
public sealed record PegamentosGrafico(
    IReadOnlyList<PegamentosGrafico.Linha> Linhas,
    IReadOnlyList<PegamentosGrafico.Marca> Escala,
    IReadOnlyList<PegamentosGrafico.Limite> LimitesDeFora,
    IReadOnlyList<string> SemMedicoes)
{
    /// <summary>A linha de um componente: os corredores, as medições e a média.</summary>
    public sealed record Linha(
        string Componente,
        string? Nominal,
        IReadOnlyList<Corredor> Corredores,
        IReadOnlyList<Ponto> Pontos,
        Marca? Media);

    /// <summary>O corredor (nominal ± tolerância): o início, a largura e o nominal, em %.</summary>
    public sealed record Corredor(double Inicio, double Largura, double Nominal, string Titulo);

    /// <summary>A média de uma medição. Estado: «dentro», «fora» ou «na» (não avaliável).</summary>
    public sealed record Ponto(double X, int Desvio, string Estado, string Titulo);

    /// <summary>Uma posição com texto: um valor da escala ou a média global.</summary>
    public sealed record Marca(double X, string Texto);

    /// <summary>Um limite de fora (o de baixo do primeiro corredor, o de cima do último), tracejado;
    /// o texto fica do lado de fora da linha, a não ser que saia do gráfico.</summary>
    public sealed record Limite(double X, string Texto, bool AEsquerda);

    public bool TemLinhas => Linhas.Count > 0;

    /// <summary>A nota «Sem medições: …». A Revisão já tem o aviso dela («Sem leituras de …»).</summary>
    public bool MostrarNotaSemMedicoes { get; init; } = true;

    private static readonly decimal[] Passos = [0.01m, 0.02m, 0.05m, 0.1m, 0.2m, 0.25m, 0.5m, 1m, 2m, 5m, 10m];

    public static string Mm(decimal v) => v.ToString("0.00", DmoNumberFormat.Culture);

    public static PegamentosGrafico From(PegamentosPdfGenerator.Input input)
    {
        var componentes = input.Components.Select(c => c.Component.ToUpperInvariant()).Distinct().ToArray();
        var medidos = new List<(string Key, PegamentosPdfGenerator.ComponentTable Tabela)>();
        var semMedicoes = new List<string>();
        foreach (var key in componentes)
        {
            var tabela = input.Tables.FirstOrDefault(t => t.Component.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (tabela is null || tabela.Rows.Count == 0) semMedicoes.Add(key);
            else medidos.Add((key, tabela));
        }

        // A escala: os limites dos corredores, as médias das medições e a média global.
        var valores = new List<decimal>();
        foreach (var (_, tabela) in medidos)
        {
            foreach (var r in tabela.Rows)
            {
                if (r.LowerMm is { } lo) valores.Add(lo);
                if (r.UpperMm is { } hi) valores.Add(hi);
                if (r.MediaMm is { } m) valores.Add(m);
            }
            if (tabela.AverageMediaMm is { } media) valores.Add(media);
        }

        if (valores.Count == 0)
        {
            return new PegamentosGrafico([], [], [], semMedicoes);
        }

        var min = valores.Min();
        var max = valores.Max();
        if (max - min < .20m)
        {
            var meio = (max + min) / 2;
            min = meio - .10m;
            max = meio + .10m;
        }
        var margem = (max - min) * .06m;
        min -= margem;
        max += margem;
        var passo = Passos.FirstOrDefault(p => (max - min) / p <= 8m, 10m);
        double X(decimal v) => Math.Round((double)((v - min) / (max - min) * 100m), 3);

        var escala = new List<Marca>();
        for (var t = decimal.Ceiling(min / passo) * passo; t <= max; t += passo)
        {
            escala.Add(new Marca(X(t), Mm(t)));
        }

        var linhas = new List<Linha>();
        var limites = new List<(decimal Lower, decimal Upper, string Key)>();
        foreach (var (key, tabela) in medidos)
        {
            var corredores = tabela.Rows
                .Where(r => r.NominalMm != null && r.LowerMm != null && r.UpperMm != null)
                .Select(r => (Nominal: r.NominalMm!.Value, Lower: r.LowerMm!.Value, Upper: r.UpperMm!.Value))
                .Distinct().ToArray();
            limites.AddRange(corredores.Select(c => (c.Lower, c.Upper, key)));
            var pontos = new List<Ponto>();
            for (var i = 0; i < tabela.Rows.Count; i++)
            {
                var r = tabela.Rows[i];
                if (r.MediaMm is not { } media) continue;
                var estado = r.IsAlert ? "fora" : r.NotEvaluable ? "na" : "dentro";
                var texto = r.IsAlert ? "fora" : r.NotEvaluable ? "não avaliável" : "dentro das tolerâncias";
                pontos.Add(new Ponto(X(media), i % 3 - 1, estado, $"{key} · medição {r.Position} · média {Mm(media)} mm · {texto}"));
            }
            linhas.Add(new Linha(
                key,
                corredores.Length == 0 ? null : string.Join(" / ", corredores.Select(c => c.Nominal).Distinct().Select(Mm)) + $" ± {Mm(input.ToleranceMm)}",
                corredores.Select(c => new Corredor(X(c.Lower), X(c.Upper) - X(c.Lower), X(c.Nominal),
                    $"{key} · nominal {Mm(c.Nominal)} mm · permitido {Mm(c.Lower)} – {Mm(c.Upper)} mm")).ToArray(),
                pontos,
                tabela.AverageMediaMm is { } avg ? new Marca(X(avg), Mm(avg)) : null));
        }

        var limitesDeFora = new List<Limite>();
        if (limites.Count > 0)
        {
            var baixo = limites.MinBy(l => l.Lower);
            var cima = limites.MaxBy(l => l.Upper);
            limitesDeFora.Add(new Limite(X(baixo.Lower), $"{baixo.Key} − {Mm((baixo.Upper - baixo.Lower) / 2)} · {Mm(baixo.Lower)}", X(baixo.Lower) > 18));
            limitesDeFora.Add(new Limite(X(cima.Upper), $"{cima.Key} + {Mm((cima.Upper - cima.Lower) / 2)} · {Mm(cima.Upper)}", X(cima.Upper) > 82));
        }

        return new PegamentosGrafico(linhas, escala, limitesDeFora, semMedicoes);
    }

    /// <summary>Uma percentagem para o CSS (sempre com ponto decimal).</summary>
    public static string Pct(double v) => v.ToString("0.###", CultureInfo.InvariantCulture) + "%";
}

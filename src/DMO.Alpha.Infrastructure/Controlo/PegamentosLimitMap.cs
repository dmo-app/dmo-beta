using System.Globalization;
using System.Security;
using System.Text;
using DMO.Alpha.Infrastructure.Pdf;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>Presentation only: all alert states and historical corridors come from Core evaluations.</summary>
public static class PegamentosLimitMap
{
    // As cores do desenho, também para a legenda da página (deep sweep de
    // 18/10/2026, regra 98: a legenda tinha um azul que o mapa nunca usa).
    // Ficam de propósito fora da paleta oficial: a identidade das componentes
    // e o cinzento de «não avaliável» (docs/ui-reference/paleta-oficial.md).
    public const string AlertColor = DmoPdfPalette.Nok;
    public const string NotEvaluableColor = DmoPdfPalette.NotEvaluable;

    public static string ComponentColor(string component) => component.ToUpperInvariant() switch
    {
        "CM" => "#16a34a",
        "BQ" => "#a855f7",
        _ => "#0891B2"
    };

    public static string Render(PegamentosPdfGenerator.Input input)
    {
        static string N(decimal n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        static string E(string s) => SecurityElement.Escape(s) ?? "";
        var values = new List<decimal>();
        foreach (var card in input.Components)
        {
            var table = input.Tables.FirstOrDefault(t => t.Component.Equals(card.Component, StringComparison.OrdinalIgnoreCase));
            // Sem medições, o componente não entra no mapa (Diogo, 17/10/2026).
            if (table is not null) foreach (var r in table.Rows)
            {
                if (r.LowerMm is { } lo) values.Add(lo);
                if (r.UpperMm is { } hi) values.Add(hi);
                if (r.MediaMm is { } m) values.Add(m);
            }
            if (table?.AverageMediaMm is { } avg) values.Add(avg);
        }
        var min = values.Count == 0 ? 0 : decimal.Floor((values.Min() - .04m) / .05m) * .05m;
        var max = values.Count == 0 ? 1 : decimal.Ceiling((values.Max() + .04m) / .05m) * .05m;
        if (max - min < .60m) { var mid = (max + min) / 2; min = mid - .30m; max = mid + .30m; }
        decimal X(decimal v) => 115 + (v - min) / (max - min) * 850;
        // O desenho do relatório original (Diogo, 19/10/2026: «melhorar o
        // aspecto do gráfico»): área do gráfico em branco com moldura, uma
        // faixa por componente na sua cor, a linha do nominal com o nome e o
        // valor por cima, as linhas tracejadas dos limites de fora (CM − tol.
        // e MF + tol.), círculos pequenos e o losango da média com o valor.
        const decimal Top = 44m, Bottom = 206m;
        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"Mapa de limites dos Pegamentos\" width=\"1000\" height=\"245\" viewBox=\"0 0 1000 245\">");
        void Text(decimal x, decimal y, string text, string color = DmoPdfPalette.Muted, int size = 11, string anchor = "middle", bool bold = false) =>
            svg.Append($"<text x='{N(x)}' y='{N(y)}' fill='{color}' font-family='Lato' font-size='{size}'{(bold ? " font-weight='700'" : "")} text-anchor='{anchor}'>{E(text)}</text>");
        string Mm(decimal v) => v.ToString("0.00", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture);
        svg.Append($"<path d='M115 {N(Top)}H965V{N(Bottom)}H115Z' fill='#ffffff' stroke='#d9e6f2'/>");
        var step = decimal.Ceiling((max - min) / 20 / .05m) * .05m;
        for (var tick = min; tick <= max; tick += step)
        {
            var x = X(tick);
            svg.Append($"<line x1='{N(x)}' x2='{N(x)}' y1='{N(Top)}' y2='{N(Bottom)}' stroke='#dbe5ef' stroke-width='1'/>");
            Text(x, Bottom + 16, Mm(tick), DmoPdfPalette.Muted, 10);
        }
        var limites = new List<(decimal Lower, decimal Upper, string Key)>();
        var nominais = new List<(decimal X, string Key, decimal Nominal)>();
        var medias = new List<(decimal X, decimal Y, string Valor, string Cor)>();
        foreach (var card in input.Components)
        {
            var key = card.Component.ToUpperInvariant();
            var y = key == "CM" ? 78m : key == "BQ" ? 125m : 172m;
            var color = ComponentColor(key);
            var table = input.Tables.FirstOrDefault(t => t.Component.Equals(key, StringComparison.OrdinalIgnoreCase));
            Text(104, y + 4, PegamentosPdfGenerator.Label(key), DmoPdfPalette.Ink, 12, "end", bold: true);
            svg.Append($"<line x1='115' x2='965' y1='{N(y)}' y2='{N(y)}' stroke='#cbd5e1'/>");
            // If rows have different frozen nominals, display every historical corridor.
            var corridors = table?.Rows.Where(r => r.NominalMm != null && r.LowerMm != null && r.UpperMm != null)
                .Select(r => (Nominal: r.NominalMm!.Value, Lower: r.LowerMm!.Value, Upper: r.UpperMm!.Value)).Distinct().ToArray() ?? [];
            foreach (var corridor in corridors)
            {
                limites.Add((corridor.Lower, corridor.Upper, key));
                svg.Append($"<rect x='{N(X(corridor.Lower))}' y='{N(y-13)}' width='{N(X(corridor.Upper)-X(corridor.Lower))}' height='26' fill='{color}' fill-opacity='0.12'/>");
                svg.Append($"<line x1='{N(X(corridor.Nominal))}' x2='{N(X(corridor.Nominal))}' y1='{N(Top - 6)}' y2='{N(Bottom)}' stroke='{color}' stroke-width='2'/>");
                nominais.Add((X(corridor.Nominal), key, corridor.Nominal));
            }
            if (table is null || table.Rows.Count == 0) Text(540, y+4, "Sem medições", DmoPdfPalette.Muted, 11);
            else for (int i = 0; i < table.Rows.Count; i++)
            {
                var r = table.Rows[i];
                if (r.MediaMm is not { } media) continue;
                var pointColor = r.IsAlert ? AlertColor : r.NotEvaluable ? NotEvaluableColor : color;
                var state = r.IsAlert ? "alerta" : r.NotEvaluable ? "não avaliável" : "dentro das tolerâncias";
                var title = E($"{key} {r.Position} · média {Mm(media)} mm · {state}");
                svg.Append($"<circle cx='{N(X(media))}' cy='{N(y+(i%3-1)*5)}' r='4.5' fill='{pointColor}' stroke='white' stroke-width='1'><title>{title}</title></circle>");
            }
            if (table?.AverageMediaMm is { } average)
            {
                var x = X(average);
                svg.Append($"<polygon points='{N(x)},{N(y-9)} {N(x+9)},{N(y)} {N(x)},{N(y+9)} {N(x-9)},{N(y)}' fill='#ffffff' fill-opacity='0.85' stroke='{color}' stroke-width='2.5'/>");
                medias.Add((x, y, Mm(average), color));
            }
        }
        // Os nomes e os valores dos nominais por cima do gráfico. Nominais
        // iguais ou muito próximos (CM e BQ com o mesmo valor) partilham um
        // rótulo, «CM · BQ», em vez de ficarem escritos um por cima do outro
        // (Diogo, 20/10/2026).
        var grupos = new List<List<(decimal X, string Key, decimal Nominal)>>();
        foreach (var nominal in nominais.OrderBy(n => n.X))
        {
            if (grupos.Count > 0 && nominal.X - grupos[^1][0].X < 60) grupos[^1].Add(nominal);
            else grupos.Add([nominal]);
        }
        foreach (var grupo in grupos)
        {
            var x = grupo.Average(n => n.X);
            var nomes = string.Join("<tspan fill='" + DmoPdfPalette.Muted + "'> · </tspan>",
                grupo.Select(n => n.Key).Distinct().Select(k => $"<tspan fill='{ComponentColor(k)}'>{E(k)}</tspan>"));
            svg.Append($"<text x='{N(x)}' y='18' font-family='Lato' font-size='11' font-weight='700' text-anchor='middle'>{nomes}</text>");
            Text(x, 33, string.Join(" / ", grupo.Select(n => n.Nominal).Distinct().Select(Mm)) + " mm", DmoPdfPalette.Ink, 10);
        }
        // O valor de cada losango por cima dele, sem fundo (Diogo, 20/10/2026:
        // «background atrás dos números é para ser invisível»): quando a
        // linha de um nominal passa por ali, o valor encosta ao lado dela,
        // para a linha não lhe passar por cima.
        foreach (var (x, y, valor, cor) in medias)
        {
            var linha = nominais.Select(n => n.X).Where(nx => Math.Abs(nx - x) < 22).Cast<decimal?>().FirstOrDefault();
            if (linha is { } nx)
                Text(x >= nx ? nx + 4 : nx - 4, y - 15, valor, cor, 10, x >= nx ? "start" : "end", bold: true);
            else
                Text(x, y - 15, valor, cor, 10, bold: true);
        }
        // Os limites de fora: o de baixo do primeiro corredor e o de cima do
        // último, tracejados na cor do componente, com «CM − 0.20» e «MF + 0.20» por baixo.
        if (limites.Count > 0)
        {
            var baixo = limites.MinBy(l => l.Lower);
            var cima = limites.MaxBy(l => l.Upper);
            foreach (var (valor, chave, legenda) in new[] { (baixo.Lower, baixo.Key, $"{baixo.Key} − {Mm((baixo.Upper - baixo.Lower) / 2)}"), (cima.Upper, cima.Key, $"{cima.Key} + {Mm((cima.Upper - cima.Lower) / 2)}") })
            {
                svg.Append($"<line x1='{N(X(valor))}' x2='{N(X(valor))}' y1='{N(Top)}' y2='{N(Bottom)}' stroke='{ComponentColor(chave)}' stroke-width='1.2' stroke-dasharray='4 3'/>");
                Text(X(valor), Bottom + 31, legenda, DmoPdfPalette.Muted, 10);
            }
        }
        return svg.Append("</svg>").ToString();
    }
}

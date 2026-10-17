using System.Globalization;
using System.Security;
using System.Text;
using DMO.Alpha.Infrastructure.Pdf;

namespace DMO.Alpha.Infrastructure.Controlo;

/// <summary>Presentation only: all alert states and historical corridors come from Core evaluations.</summary>
public static class PegamentosLimitMap
{
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
        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"Mapa de limites dos Pegamentos\" width=\"1000\" height=\"310\" viewBox=\"0 0 1000 310\">");
        void Text(decimal x, decimal y, string text, string color = DmoPdfPalette.Muted, int size = 11, string anchor = "middle") =>
            svg.Append($"<text x='{N(x)}' y='{N(y)}' fill='{color}' font-family='Lato' font-size='{size}' text-anchor='{anchor}'>{E(text)}</text>");
        var step = decimal.Ceiling((max - min) / 20 / .05m) * .05m;
        for (var tick = min; tick <= max; tick += step)
        {
            var x = X(tick);
            svg.Append($"<line x1='{N(x)}' x2='{N(x)}' y1='58' y2='258' stroke='#dbe5ef' stroke-width='1'/>");
            Text(x, 286, tick.ToString("0.00", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture));
        }
        foreach (var card in input.Components)
        {
            var key = card.Component.ToUpperInvariant();
            var y = key == "CM" ? 100m : key == "BQ" ? 165m : 230m;
            var color = key == "CM" ? "#16a34a" : key == "BQ" ? "#a855f7" : "#0891B2";
            var table = input.Tables.FirstOrDefault(t => t.Component.Equals(key, StringComparison.OrdinalIgnoreCase));
            Text(102, y + 4, PegamentosPdfGenerator.Label(key), DmoPdfPalette.Ink, 12, "end");
            svg.Append($"<line x1='115' x2='965' y1='{N(y)}' y2='{N(y)}' stroke='#cbd5e1'/>");
            // If rows have different frozen nominals, display every historical corridor.
            var corridors = table?.Rows.Where(r => r.NominalMm != null && r.LowerMm != null && r.UpperMm != null)
                .Select(r => (Nominal: r.NominalMm!.Value, Lower: r.LowerMm!.Value, Upper: r.UpperMm!.Value)).Distinct().ToArray() ?? [];
            foreach (var corridor in corridors)
            {
                svg.Append($"<rect x='{N(X(corridor.Lower))}' y='{N(y-18)}' width='{N(X(corridor.Upper)-X(corridor.Lower))}' height='36' rx='5' fill='{color}' fill-opacity='0.10'/>");
                svg.Append($"<line x1='{N(X(corridor.Nominal))}' x2='{N(X(corridor.Nominal))}' y1='48' y2='258' stroke='{color}' stroke-width='2'/>");
                Text(X(corridor.Nominal), 20, key, color, 12);
                Text(X(corridor.Nominal), 38, corridor.Nominal.ToString("0.00", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture) + " mm");
            }
            if (table is null || table.Rows.Count == 0) Text(540, y+4, "Sem medições", DmoPdfPalette.Muted, 13);
            else for (int i = 0; i < table.Rows.Count; i++)
            {
                var r = table.Rows[i];
                if (r.MediaMm is not { } media) continue;
                var pointColor = r.IsAlert ? "#b91c1c" : r.NotEvaluable ? "#94a3b8" : color;
                var state = r.IsAlert ? "alerta" : r.NotEvaluable ? "não avaliável" : "dentro das tolerâncias";
                var title = E($"{key} {r.Position} · média {media.ToString("0.00", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture)} mm · {state}");
                svg.Append($"<circle cx='{N(X(media))}' cy='{N(y+(i%5-2)*5)}' r='6' fill='{pointColor}' stroke='white' stroke-width='1.5'><title>{title}</title></circle>");
            }
            if (table?.AverageMediaMm is { } average)
            {
                var x = X(average);
                svg.Append($"<polygon points='{N(x)},{N(y-11)} {N(x+11)},{N(y)} {N(x)},{N(y+11)} {N(x-11)},{N(y)}' fill='none' stroke='{color}' stroke-width='3'/>");
                Text(x, y-22, average.ToString("0.00", DMO.Alpha.Core.Runtime.DmoNumberFormat.Culture), color);
            }
        }
        Text(965, 306, "mm", anchor: "end");
        return svg.Append("</svg>").ToString();
    }
}

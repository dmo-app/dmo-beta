using System.Text.RegularExpressions;

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// O PARSER CANÓNICO do NOME TÉCNICO — a gramática oficial do OP 99 PMD 02
/// (o documento do Owner, extraído; validado contra a tabela ARTIGOS-MG
/// das 6162 linhas reais da fábrica).
///
/// MOLDES  = M + (STnn | MMMM) + mm + TT + PP + V [+ M1] [-B/E1/E2]
///   STnn = as blank molds NNPB STANDARD (substituem o modelo);
///   mm: 44=4"¼ · 50=5" · 55=5"½ · 64=6"¼ · 4T/5T=triple gob · 85=VN triple gob · GS=single · M3/M4/M6=dual/triple cavity;
///   TT: MP=blank mold (a CONTRA-MOLDE/CM) · MF=blow/final (MF) · FF=bottom plate;
///   PP: NN=narrow neck (NNPB) · PS=press&blow-62 · SS=blow-blow · P4=press&blow-41;
///   V:  A/C/E/R/S/V/X/Z/Y/W/0 (a ventilação); M1: X=minox/bronze.
///
/// ACESSÓRIOS = A + T + XXXX + P + M + NN [-B/E1/E2]   (os pontos de
/// separação antigos — «A.S.S023.S.F» — são normalizados fora)
///   T: A=guide ring · B=neck ring (a BQ!) · C=timble · D=diffuser · F=funnel · I=takeout · P=plunger (a punção) · R=cooling tube · S=blow head (a MARISA/BQ!) · T=baffle (o tampão) · K=frame;
///   XXXX (depende do tipo): Annn = neck rings/guide rings/blow heads/BB plungers (1 letra+3: S023, C002) · STnn = NNPB plungers/cooling tubes · CAnn/DInn/SPnn/PTnn · o modelo de 4 dígitos (baffles e PB plungers);
///   P (1 letra): P=PS · S=SS · N=NN · 4=P4 (os takeouts: sempre P);
///   M (1 letra, o material): A=steel · C=cast iron+colmonoy · D=dameron · F=cast iron · G=graphite · I=stainless · L=brass · R=minox+colmonoy · T=tungsten · X=minox/bronze.
///
/// Exemplos reais da ARTIGOS-MG: M002044MPSSX (CM do 0020 4"¼ BB axial) ·
/// M00664TMFNNV (MF do 0066 4T) · ABS023SC4713 (neck ring do S023) ·
/// A.S.S023.S.F (blow head/marisa do S023, pontuado antigo) · APS023SD02
/// (plunger do S023) · AT0020SF0-A (baffle do 0020).
/// </summary>
public static class TechnicalNameParser
{
    public sealed record Decoded(
        ToolType? InferredType,
        string? InferredReference,
        string? MoldModel,
        string? Dimension,
        string? Process,
        string? Ventilation,
        string? Material,
        string Family);

    // M + (STnnn | 4 dígitos) + mm + TT + PP + V + [M1 o material] + [-rev/_fábrica]
    private static readonly Regex MoldRegex = new(
        @"^M(?<model>ST\d{2,3}|\d{2,4}[A-Z][A-Z0-9]?|\d{2,5})(?<mm>SG|GS|\d{2}|4T|5T|M3|M4|M6)?(?<tt>MP|MF|FF)(?<pp>NN|PS|SS|FS|P4)?(?<v>[ACERSVXZWY0])(?<m1>[A-Z0-9])?(?:[-_].*)?$",
        RegexOptions.Compiled);

    // A + T + XXXX + [P(1)] + [M(1)] + [NN] + [-rev/_fábrica]. O XXXX segue a
    // gramática (STnn, 2 letras + 2 dígitos, 1 letra + 3 dígitos, 4 dígitos);
    // só quando nenhuma serve fica o que sobra.
    private static readonly Regex AccessoryRegex = new(
        @"^A(?<t>[ABCDFIPRSTK])(?<ref>ST\d{2}|[A-Z]{2}\d{2}|[A-Z]\d{3}|\d{4}|[A-Z0-9]+?)(?<p>[PSN4])?(?<m>[ACDFGILRTX])?(?<nn>[A-Z0-9]{0,7})?(?:BU)?(?:[-_].*)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Decodifica o nome técnico. Molde (M...), acessório (A...) — os
    /// pontos de separação antigos e os sufixos de fábrica («_SO», «_BU»)
    /// saem antes; o resto é «desconhecido» (a ficha mostra o nome como
    /// está — a app nunca recusa).
    /// </summary>
    public static Decoded Decode(string technicalName)
    {
        var name = technicalName?.Trim().ToUpperInvariant().Replace(".", "");
        if (string.IsNullOrEmpty(name))
        {
            return new Decoded(null, null, null, null, null, null, null, "desconhecido");
        }

        // Os sufixos de fábrica («_SO», «_AT», o «_» sozinho) e as notas
        // em texto livre (os parênteses) saem do decode.
        var parenIndex = name.IndexOf('(');
        if (parenIndex > 0)
        {
            name = name[..parenIndex].Trim();
        }
        name = Regex.Replace(name, @"_[A-Z]{0,2}$", "");

        var mold = MoldRegex.Match(name);
        if (mold.Success)
        {
            var tt = mold.Groups["tt"].Value;
            var inferredType = tt switch
            {
                "MF" => ToolType.Mf,
                "MP" or "FF" => ToolType.Cm,
                _ => (ToolType?)null
            };
            var model = mold.Groups["model"].Value;
            return new Decoded(
                inferredType,
                model,
                model,
                DescribeDimension(mold.Groups["mm"].Value),
                DescribeProcess(mold.Groups["pp"].Value),
                mold.Groups["v"].Value,
                DescribeMaterial(mold.Groups["m1"].Value),
                tt == "MP" ? "blank mold (contra-molde)" : tt == "MF" ? "blow/final mold" : "bottom plate");
        }

        var accessory = AccessoryRegex.Match(name);
        if (accessory.Success)
        {
            var accessoryType = accessory.Groups["t"].Value;
            var inferred = accessoryType switch
            {
                "B" or "S" => ToolType.Bq,       // neck ring e blow head (marisa)
                "P" or "T" => ToolType.Cm,       // plunger (punção) e baffle (tampão)
                _ => (ToolType?)null
            };
            var family = accessoryType switch
            {
                "A" => "guide ring",
                "B" => "neck ring",
                "C" => "timble",
                "D" => "diffuser",
                "F" => "funnel",
                "I" => "takeout",
                "P" => "plunger (punção)",
                "R" => "cooling tube",
                "S" => "blow head (marisa)",
                "T" => "baffle (tampão)",
                "K" => "frame",
                _ => "acessório"
            };
            return new Decoded(
                inferred,
                accessory.Groups["ref"].Value,
                null,
                null,
                DescribeProcess(accessory.Groups["p"].Value),
                null,
                DescribeMaterial(accessory.Groups["m"].Value),
                family);
        }

        return new Decoded(null, null, null, null, null, null, null, "desconhecido");
    }

    private static string DescribeDimension(string raw) => raw switch
    {
        "44" => "4\"¼",
        "50" => "5\"",
        "55" => "5\"½",
        "64" => "6\"¼",
        "85" => "VN triple gob",
        "GS" => "single gob",
        "4T" => "4\"¼ triple gob",
        "5T" => "5\" triple gob",
        "M3" => "6\"¼ triple cavity",
        "M4" => "4\"¼ dual cavity",
        "M6" => "6\"¼ dual cavity",
        _ => raw
    };

    private static string DescribeProcess(string pp) => pp switch
    {
        "NN" or "N" => "narrow neck (NNPB)",
        "PS" or "P" => "press & blow 62",
        "SS" or "S" => "blow & blow",
        "P4" or "4" => "press & blow 41",
        _ => pp
    };

    private static string DescribeMaterial(string m) => m switch
    {
        "A" => "steel",
        "C" => "cast iron + colmonoy",
        "D" => "dameron",
        "F" => "cast iron",
        "G" => "graphite",
        "I" => "stainless steel",
        "L" => "brass/copper",
        "R" => "minox + colmonoy",
        "T" => "tungsten carbide",
        "X" => "minox/bronze",
        _ => m
    };
}

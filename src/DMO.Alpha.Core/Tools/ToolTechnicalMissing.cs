namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Os dados técnicos que faltam a uma ferramenta, pelo que cada tipo leva
/// (blueprint, Ferramentas §6): a CM o processo, o peso nominal, a punção, o
/// gargalo e o tampão (D e calote); a BQ a marisa e o gargalo; o MF o
/// gargalo. Todos são opcionais: isto só diz o que falta, nunca bloqueia.
/// </summary>
public static class ToolTechnicalMissing
{
    public static IReadOnlyList<string> For(ToolType type, ToolTechnicalContext? technical)
    {
        var missing = new List<string>();
        void Check(bool present, string label)
        {
            if (!present)
            {
                missing.Add(label);
            }
        }

        switch (type)
        {
            case ToolType.Cm:
                Check(technical?.Process is not null, "processo");
                Check(technical?.NominalWeightGrams is not null, "peso nominal");
                Check(technical?.VolumePuncaoCm3 is not null, "punção");
                Check(technical?.DiametroGargaloMm is not null, "gargalo");
                Check(technical?.TpDiametroMm is not null && technical.TpSagittaMm is not null, "tampão");
                break;
            case ToolType.Bq:
                Check(technical?.VolumeMarisaCm3 is not null, "marisa");
                Check(technical?.DiametroGargaloMm is not null, "gargalo");
                break;
            case ToolType.Mf:
                Check(technical?.DiametroGargaloMm is not null, "gargalo");
                break;
        }

        return missing;
    }
}

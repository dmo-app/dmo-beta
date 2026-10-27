using System.Globalization;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O tampão (TP) com que se pesou: o diâmetro D e a calote h, em mm, como a
/// oficina os escreve («24.95 x 4.5mm»). No «Comparar peso» só a calote muda
/// (Diogo, 27/10/2026).
/// </summary>
public sealed record PesoTampao(decimal DiametroMm, decimal CaloteMm)
{
    /// <summary>«24.95 x 4.5», como as referências dos tampões, sem casas a mais.</summary>
    public string Texto =>
        $"{DiametroMm.ToString("0.###", CultureInfo.InvariantCulture)} x {CaloteMm.ToString("0.###", CultureInfo.InvariantCulture)}";
}


namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// O resultado do «supomos» da calote do tampão: o Peso aberto com a calote
/// que tem e com a calote suposta, e a comparação com a produção escolhida
/// nos dois casos. Só exibição: nada disto é gravado.
/// </summary>
public sealed record PesoSuposicaoCalote(
    decimal CaloteAtualMm,
    decimal CaloteSupostaMm,
    decimal VolumeAtualCm3,
    decimal VolumeSupostoCm3,
    /// <summary>A parte do peso medido que vem da calote atual: V(h_atual) × densidade do vidro.</summary>
    decimal PesoDaCaloteAtualGrams,
    /// <summary>A mesma parte com a calote suposta: V(h_suposta) × densidade do vidro.</summary>
    decimal PesoDaCaloteSupostaGrams,
    /// <summary>ΔV = V(suposta) − V(atual), com o sinal.</summary>
    decimal DeltaVolumeCm3,
    /// <summary>Δ peso = ΔV × densidade do vidro do Peso aberto.</summary>
    decimal DeltaPesoGrams,
    decimal PesoMedioAtualGrams,
    decimal PesoMedioSupostoGrams,
    /// <summary>O peso médio da produção comparada (null sem comparação).</summary>
    decimal? PesoMedioComparadoGrams,
    decimal? DiferencaAtualGrams,
    decimal? DiferencaSupostaGrams,
    decimal? VariacaoSupostaPercent);

/// <summary>
/// O «supomos» do Peso (o Diogo, 16/10: «quão diferente seria se a calote do
/// tampão do 202601 fosse 5,2 mm em vez de 3 mm»). A calote nunca entra na
/// fórmula do Peso: o tampão está montado quando se tira a massa de água, por
/// isso o peso medido já traz a calote com que foi pesado (o Diogo, 17/10:
/// «a utilização é relacional... sei quanto do peso é influenciado por ele»).
/// A parte do peso que vem da calote é V(h)·ρ; supor outra calote troca essa
/// parte:
/// <code>
/// ΔV = cap(D, h_suposta).V − cap(D, h_atual).V
/// peso suposto = peso médio − V(h_atual)·ρ + V(h_suposta)·ρ = peso médio + ΔV·ρ
/// </code>
/// Usa a mesma matemática da calote (<see cref="PesoCapCalculation.Cap"/>) e
/// nunca altera o <see cref="PesoCalculation"/>.
/// </summary>
public static class PesoSuposicaoCalculation
{
    public static PesoSuposicaoCalote Calote(
        decimal diametroMm,
        decimal caloteAtualMm,
        decimal caloteSupostaMm,
        decimal glassDensityGramsPerCm3,
        decimal pesoMedioAtualGrams,
        decimal? pesoMedioComparadoGrams)
    {
        var atual = PesoCapCalculation.Cap(diametroMm, caloteAtualMm);
        var suposta = PesoCapCalculation.Cap(diametroMm, caloteSupostaMm);
        var deltaVolume = suposta.VolumeCm3 - atual.VolumeCm3;
        var deltaPeso = deltaVolume * glassDensityGramsPerCm3;
        var pesoSuposto = pesoMedioAtualGrams + deltaPeso;

        decimal? diferencaAtual = pesoMedioComparadoGrams is { } comparado ? pesoMedioAtualGrams - comparado : null;
        decimal? diferencaSuposta = pesoMedioComparadoGrams is { } comparado2 ? pesoSuposto - comparado2 : null;
        decimal? variacaoSuposta = diferencaSuposta is { } ds && pesoMedioComparadoGrams is { } baseComparada && baseComparada != 0m
            ? ds / baseComparada * 100m
            : null;

        return new PesoSuposicaoCalote(
            caloteAtualMm,
            caloteSupostaMm,
            Round(atual.VolumeCm3, 3),
            Round(suposta.VolumeCm3, 3),
            Round(atual.VolumeCm3 * glassDensityGramsPerCm3, 2),
            Round(suposta.VolumeCm3 * glassDensityGramsPerCm3, 2),
            Round(deltaVolume, 3),
            Round(deltaPeso, 2),
            Round(pesoMedioAtualGrams, 2),
            Round(pesoSuposto, 2),
            pesoMedioComparadoGrams is { } c ? Round(c, 2) : null,
            diferencaAtual is { } da ? Round(da, 2) : null,
            diferencaSuposta is { } ds2 ? Round(ds2, 2) : null,
            variacaoSuposta is { } vs ? Round(vs, 2) : null);
    }

    /// <summary>
    /// O «Comparar peso» (Diogo, 27/10/2026: «alterar o tampão usando o
    /// cálculo do volume do tampão altera o peso»): o mesmo cálculo da calote,
    /// mas o tampão simulado também pode ter outro diâmetro.
    /// <code>
    /// ΔV = cap(D_simulado, h_simulado).V − cap(D_atual, h_atual).V
    /// peso simulado = peso médio + ΔV·ρv
    /// </code>
    /// </summary>
    public static PesoTampaoSimulado Tampao(
        PesoTampao atual,
        PesoTampao simulado,
        decimal glassDensityGramsPerCm3,
        decimal pesoMedioGrams)
    {
        var volumeAtual = PesoCapCalculation.Cap(atual.DiametroMm, atual.CaloteMm).VolumeCm3;
        var volumeSimulado = PesoCapCalculation.Cap(simulado.DiametroMm, simulado.CaloteMm).VolumeCm3;
        var deltaVolume = volumeSimulado - volumeAtual;
        var pesoSimulado = pesoMedioGrams + deltaVolume * glassDensityGramsPerCm3;
        var diferenca = pesoSimulado - pesoMedioGrams;
        return new PesoTampaoSimulado(
            atual,
            simulado,
            Round(volumeAtual, 3),
            Round(volumeSimulado, 3),
            Round(deltaVolume, 3),
            Round(pesoMedioGrams, 2),
            Round(pesoSimulado, 2),
            Round(diferenca, 2),
            pesoMedioGrams != 0m ? Round(diferenca / pesoMedioGrams * 100m, 2) : null);
    }

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}

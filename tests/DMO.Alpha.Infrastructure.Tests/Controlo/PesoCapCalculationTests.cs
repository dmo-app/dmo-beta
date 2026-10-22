using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a matemática canónica do apoio TP/calote (dmo-html controlo-v04,
/// funções cap(D,h) e support(geometry, effective)) — o CÁLCULO SEPARADO que
/// o PesoCalculation.cs:6-7 já previa («o cálculo de suporte TP é separado e
/// não está neste corte»). Valores de referência do contrato: D = 50,
/// h = 10 → R = 36,25 mm e V ≈ 10,338 cm³ (o valor exato da fórmula
/// V = π·h·(3a²+h²)/6000 é 10,34107… cm³ — dentro do intervalo do «≈» da
/// referência; a tolerância do teste cobre os dois lados e rejeita qualquer
/// erro real da fórmula). ΔR/ΔV/drawing_delta PRESERVAM O SINAL.
/// </summary>
public sealed class PesoCapCalculationTests
{
    [Fact]
    public void Cap_ValoresDeReferencia_D50_H10()
    {
        // cap: a = D/2; R = (a²+h²)/(2h); V = π·h·(3a²+h²)/6000.
        // R = (625 + 100) / 20 = 36,25 mm (exato); V ≈ 10,338 cm³ (a
        // referência do contrato — o valor exato da fórmula é 10,34107…).
        var cap = PesoCapCalculation.Cap(50m, 10m);

        Assert.Equal(50m, cap.DiametroMm);
        Assert.Equal(10m, cap.AlturaMm);
        Assert.Equal(36.25m, cap.RaioMm);
        Assert.InRange(cap.VolumeCm3, 10.333m, 10.343m);
    }

    [Fact]
    public void Cap_Volume_ExatoPelaFormula()
    {
        // Precisão da fórmula: π·10·(3·25² + 10²)/6000 = 10,3410758… cm³.
        var cap = PesoCapCalculation.Cap(50m, 10m);

        Assert.InRange(cap.VolumeCm3, 10.3410m, 10.3412m);
    }

    [Fact]
    public void Cap_CalotePlana_HZero_RNullVolumeZero()
    {
        // O html: «height === 0 ? null» para o R; o volume é 0 (nunca um
        // facto inventado).
        var cap = PesoCapCalculation.Cap(50m, 0m);

        Assert.Null(cap.RaioMm);
        Assert.Equal(0m, cap.VolumeCm3);
    }

    [Fact]
    public void Cap_OutroPar_CalculaPelaFormula()
    {
        // D = 50, h = 9: R = (625 + 81)/18 = 39,2222…; V = π·9·1956/6000 ≈
        // 9,2174 cm³.
        var cap = PesoCapCalculation.Cap(50m, 9m);

        Assert.InRange(cap.RaioMm!.Value, 39.2222m, 39.2223m);
        Assert.InRange(cap.VolumeCm3, 9.2170m, 9.2180m);
    }

    [Fact]
    public void Cap_DiamentroNaoPositivo_Recusa()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PesoCapCalculation.Cap(0m, 10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PesoCapCalculation.Cap(-5m, 10m));
    }

    [Fact]
    public void Cap_CaloteNegativa_Recusa()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PesoCapCalculation.Cap(50m, -1m));
    }

    [Fact]
    public void Support_ComCaloteDoJobOn_ComparaComAReferencia()
    {
        // A calote efetiva da produção (9 mm) contra a referência da Tool
        // (h_ref 10 mm): R efetivo (625+81)/18 =
        // 39,2222…; ΔR = 39,2222… − 36,25 = +2,9722…; ΔV = 9,2174… −
        // 10,34107… = −1,1236….
        var support = PesoCapCalculation.Support(50m, 10m, 9m);

        Assert.Equal(50m, support.Referencia.DiametroMm);
        Assert.Equal(10m, support.Referencia.AlturaMm);
        Assert.Equal(36.25m, support.Referencia.RaioMm);
        Assert.Equal(9m, support.Efetivo.AlturaMm);
        Assert.InRange(support.Efetivo.RaioMm!.Value, 39.2222m, 39.2223m);
        Assert.InRange(support.DeltaRaioMm!.Value, 2.9721m, 2.9723m);
        Assert.InRange(support.Efetivo.VolumeCm3, 9.2170m, 9.2180m);
        Assert.InRange(support.DeltaVolumeCm3, -1.124m, -1.123m);
    }

    [Fact]
    public void Support_SemCaloteEfetiva_CaiNaReferenciaDaTool()
    {
        // O html: «effective ?? geometry.h_ref» — sem calote do Job On a
        // efetiva É a referência: ΔR = 0 e ΔV = 0 (o sinal do zero é +).
        var support = PesoCapCalculation.Support(50m, 10m, null);

        Assert.Equal(support.Referencia.RaioMm, support.Efetivo.RaioMm);
        Assert.Equal(support.Referencia.VolumeCm3, support.Efetivo.VolumeCm3);
        Assert.Equal(0m, support.DeltaRaioMm);
        Assert.Equal(0m, support.DeltaVolumeCm3);
    }

    [Fact]
    public void Support_ReferenciaPlana_DiferencasDeRaioIndefinidas()
    {
        // h_ref = 0: a referência não tem R → ΔR fica
        // indefinidos (null, nunca zero inventado); ΔV compara os volumes.
        var support = PesoCapCalculation.Support(50m, 0m, 5m);

        Assert.Null(support.Referencia.RaioMm);
        Assert.Null(support.DeltaRaioMm);
        Assert.InRange(support.DeltaVolumeCm3, 4.974m, 4.975m); // π·5·1900/6000 = 4,9742…
    }

    [Fact]
    public void TrySupport_GeometriaIncompleta_SemApoio()
    {
        // A Tool sem TP (ou com um valor inválido) NÃO TEM apoio: null =
        // «Referência por completar» — o Peso continua 100% intacto.
        Assert.Null(PesoCapCalculation.TrySupport(null, 10m, null));       // sem D
        Assert.Null(PesoCapCalculation.TrySupport(50m, null, null));       // sem h_ref
        Assert.Null(PesoCapCalculation.TrySupport(0m, 10m, null));         // D inválido
        Assert.Null(PesoCapCalculation.TrySupport(50m, -1m, null));        // h_ref negativo
        Assert.Null(PesoCapCalculation.TrySupport(50m, 10m, -1m));         // calote negativa
    }

    [Fact]
    public void TrySupport_GeometriaCompleta_DevolveOApoio()
    {
        var support = PesoCapCalculation.TrySupport(50m, 10m, 9m);

        Assert.NotNull(support);
        Assert.Equal(36.25m, support!.Referencia.RaioMm);
        Assert.Equal(9m, support.Efetivo.AlturaMm);
    }

    [Fact]
    public void TrySupport_SoComDiametroECalote_DevolveOApoioSemRaio()
    {
        // O raio do desenho deixou de ser pedido: D e h_ref chegam para a
        // esfera (a = D/2; R = (a² + h²)/(2h); V = π·h·(3a² + h²)/6000).
        var support = PesoCapCalculation.TrySupport(50m, 10m, null);

        Assert.NotNull(support);
        Assert.Equal(36.25m, support!.Referencia.RaioMm);
        Assert.InRange(support.Referencia.VolumeCm3, 10.338m, 10.342m);
    }
}

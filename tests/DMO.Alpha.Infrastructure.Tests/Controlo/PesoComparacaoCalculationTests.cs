using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Prova a matemática dos PARES da comparação de pesos (o D.compare do
/// dmo-html controlo-v04): as leituras emparelham pelo NÚMERO/POSIÇÃO do CM
/// que a oficina escreve, cada leitura anterior é usada NO MÁXIMO uma vez,
/// as linhas sem contraparte são o estado «Sem contraparte» (nunca um erro)
/// e os Δ médios PRESERVAM O SINAL. O peso comparado é O PESO do vidro — a
/// fórmula total do PesoCalculation, calculada pelo backend antes de entrar
/// aqui.
/// </summary>
public sealed class PesoComparacaoCalculationTests
{
    private static PesoComparacaoLado Lado(int posicao, string subject, decimal peso, decimal? volume = null) =>
        new(posicao, subject, peso, volume);

    [Fact]
    public void Pares_PeloNumeroDoCm_DeltaComSinalPreservado()
    {
        // Os pares fazem-se pelo nº de CM: «12»↔«12» (+2,50) e «21»↔«21»
        // (−0,40); o Δ médio é a média dos pares com o sinal preservado.
        var atuais = new[]
        {
            Lado(1, "12", 450.50m),
            Lado(2, "21", 200.10m)
        };
        var anteriores = new[]
        {
            Lado(1, "12", 448.00m),
            Lado(2, "21", 200.50m)
        };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(2, pares.ParesValidos);
        Assert.Equal(0, pares.SemContraparte);
        Assert.Equal(2, pares.Rows.Count);

        Assert.Equal("12", pares.Rows[0].CmSubjectAnterior);
        Assert.Equal(2.50m, pares.Rows[0].DeltaPesoGrams);
        Assert.Equal("21", pares.Rows[1].CmSubjectAnterior);
        Assert.Equal(-0.40m, pares.Rows[1].DeltaPesoGrams);

        // A média dos pares: (+2,50 + −0,40) / 2 = +1,05 (o sinal é o fato).
        Assert.Equal(1.05m, pares.DeltaPesoMedioGrams);
    }

    [Fact]
    public void Pares_SemContraparte_LinhaSemParNuncaErro()
    {
        // O contra-molde novo que o Peso inicial não tem: a linha fica sem
        // par (os deltas indefinidos, nunca zero inventado) e a comparação
        // continua — «Sem contraparte» conta.
        var atuais = new[]
        {
            Lado(1, "12", 450.50m),
            Lado(2, "31", 326.00m)
        };
        var anteriores = new[] { Lado(1, "12", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(1, pares.ParesValidos);
        Assert.Equal(1, pares.SemContraparte);
        Assert.Null(pares.Rows[1].CmSubjectAnterior);
        Assert.Null(pares.Rows[1].PesoAnteriorGrams);
        Assert.Null(pares.Rows[1].DeltaPesoGrams);
        Assert.Equal(2.50m, pares.DeltaPesoMedioGrams);
    }

    [Fact]
    public void Pares_LeituraAnteriorUsadaNoMaximoUmaVez()
    {
        // O html: «cada leitura histórica é usada no máximo uma vez» — dois
        // novos CM «12» contra um «12» anterior: o segundo fica sem par.
        var atuais = new[]
        {
            Lado(1, "12", 450.50m),
            Lado(2, "12", 451.00m)
        };
        var anteriores = new[] { Lado(1, "12", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(1, pares.ParesValidos);
        Assert.Equal(1, pares.SemContraparte);
        Assert.Equal("12", pares.Rows[0].CmSubjectAnterior);
        Assert.Null(pares.Rows[1].CmSubjectAnterior);
    }

    [Fact]
    public void Pares_SujecomVazias_NuncaEmparelham()
    {
        // O número/posição CM é o que a oficina escreve; linhas sem número
        // não emparelham com nada (honesto, sem pareamento inventado).
        var atuais = new[] { Lado(1, "", 450.50m) };
        var anteriores = new[] { Lado(1, "", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(0, pares.ParesValidos);
        Assert.Equal(1, pares.SemContraparte);
        Assert.Null(pares.DeltaPesoMedioGrams);
    }

    [Fact]
    public void Pares_SemNenhumPar_DeltaMedioIndefinido()
    {
        var atuais = new[] { Lado(1, "12", 450.50m) };
        var anteriores = new[] { Lado(1, "99", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(0, pares.ParesValidos);
        Assert.Null(pares.DeltaPesoMedioGrams);
        Assert.Null(pares.DeltaVolumeMedioCm3);
    }

    [Fact]
    public void Pares_DeltaVolume_NosDoisLadosPresentes()
    {
        var atuais = new[]
        {
            Lado(1, "12", 450.50m, 200.20m),
            Lado(2, "21", 200.10m)
        };
        var anteriores = new[] { Lado(1, "12", 448.00m, 199.20m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(1.00m, pares.Rows[0].DeltaVolumeCm3);
        Assert.Equal(1.00m, pares.DeltaVolumeMedioCm3); // a linha sem volume não entra na média
    }

    [Fact]
    public void Pares_CmComZerosAEsquerda_EmparelhaComOMesmoNumero()
    {
        // PESO-B01 (sweep 2): «01» e «1» são o mesmo CM, só com zeros à
        // esquerda — a comparação não deve dizer «Sem contraparte».
        var atuais = new[] { Lado(1, "01", 450.50m) };
        var anteriores = new[] { Lado(1, "1", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(1, pares.ParesValidos);
        Assert.Equal(0, pares.SemContraparte);
        Assert.Equal("1", pares.Rows[0].CmSubjectAnterior);
        Assert.Equal(2.50m, pares.Rows[0].DeltaPesoGrams);
    }

    [Fact]
    public void Pares_CmIguais_Emparelham()
    {
        // Sanidade: «1» contra «1» continua a emparelhar como sempre.
        var atuais = new[] { Lado(1, "1", 450.50m) };
        var anteriores = new[] { Lado(1, "1", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(1, pares.ParesValidos);
        Assert.Equal(0, pares.SemContraparte);
    }

    [Fact]
    public void Pares_CmComLetraEZeros_NuncaNumerico_FicaSemContraparte()
    {
        // «01A» vs «1A»: com uma letra, a interpretação é ambígua —
        // fica a igualdade de texto (nunca promovida a numérica), para não
        // emparelhar CMs diferentes por engano.
        var atuais = new[] { Lado(1, "01A", 450.50m) };
        var anteriores = new[] { Lado(1, "1A", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(0, pares.ParesValidos);
        Assert.Equal(1, pares.SemContraparte);
    }

    [Fact]
    public void Pares_CmDigitosDiferentes_NuncaEmparelham()
    {
        // «1» e «2» são números diferentes — nunca emparelham, com ou sem
        // zeros à esquerda.
        var atuais = new[] { Lado(1, "1", 450.50m) };
        var anteriores = new[] { Lado(1, "2", 448.00m) };

        var pares = PesoComparacaoCalculation.CalcularParesPorCm(atuais, anteriores);

        Assert.Equal(0, pares.ParesValidos);
        Assert.Equal(1, pares.SemContraparte);
    }
}

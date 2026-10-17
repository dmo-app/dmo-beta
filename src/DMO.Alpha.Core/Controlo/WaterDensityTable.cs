namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Tabela de referência canónica DMO de densidade da água (g/cm³) consumida
/// pelo Peso. Comportamento de lookup definido pelo blueprint: intervalo de
/// temperaturas suportado 5–35 °C; a temperatura usada no lookup é
/// arredondada ao grau inteiro; não é usada interpolação entre linhas.
/// A referência é canónica e de leitura — o Peso não se torna o segundo
/// owner do dataset nem o frontend mantém uma implementação concorrente.
///
/// DECISÃO OWNER 1b (2026-10-10, ESTADO-ATUAL.md): a tabela canónica é a
/// TABELA REAL DA FÁBRICA (folha «Temp» do genesis «7151S022(ST78) 202604
/// B1L18(1).xlsx», 31 valores a 5 decimais) — adoptada SEM congelamento:
/// a densidade é função da temperatura e resolve-se SEMPRE à leitura, pelo
/// que os valores derivados dos Pesos históricos recalculam retroativamente
/// com a tabela (Peso #1 incluído — registo histórico mantém os factos
/// observados: massas de água e temperatura).
/// </summary>
public static class WaterDensityTable
{
    /// <summary>Limite inferior do intervalo suportado (°C, grau inteiro).</summary>
    public const int MinTemperatureC = 5;

    /// <summary>Limite superior do intervalo suportado (°C, grau inteiro).</summary>
    public const int MaxTemperatureC = 35;

    private static readonly decimal[] DensitiesByWholeDegree =
    {
        // Índice 0..4 não usado; as entradas válidas começam em 5 °C.
        // TABELA REAL DA FÁBRICA (genesis, folha «Temp», 5 decimais) —
        // decisão Owner 1b de 2026-10-10 (supersedes a tabela de física pura
        // a 4 decimais que vivia aqui; a divergência era ~0,001/grau).
        0m, 0m, 0m, 0m, 0m,
        0.99888m, // 5
        0.99885m, // 6
        0.99882m, // 7
        0.99877m, // 8
        0.99871m, // 9
        0.99863m, // 10
        0.99854m, // 11
        0.99844m, // 12
        0.99832m, // 13
        0.99819m, // 14
        0.99805m, // 15
        0.99789m, // 16
        0.99773m, // 17
        0.99765m, // 18
        0.99737m, // 19
        0.99717m, // 20
        0.99696m, // 21
        0.99674m, // 22
        0.99652m, // 23
        0.99628m, // 24
        0.99603m, // 25
        0.99577m, // 26
        0.99551m, // 27
        0.99523m, // 28
        0.99494m, // 29
        0.99485m, // 30
        0.99435m, // 31
        0.99403m, // 32
        0.99371m, // 33
        0.99339m, // 34
        0.99305m  // 35
    };

    /// <summary>
    /// Arredonda a temperatura medida ao grau inteiro usado no lookup
    /// (arredondamento comercial: .5 sobe), tal como o blueprint define.
    /// A GUARDA DO STRESSTEST (o P1 de 2026-10-12: a temperatura
    /// 99999999999999999999 rebentava o cast a int → 500): valores fora
    /// do intervalo suportado devolvem o sentinel int.MinValue — o
    /// Resolve devolve null e o handler recusa tipadamente
    /// (InvalidWaterTemperature), NUNCA uma exceção.
    /// </summary>
    public static int RoundToWholeDegree(decimal temperatureC)
    {
        // A margem 0,5m cobre os decimais que arredondariam PARA dentro
        // (o 4,9 → 5 é válido; o 4,4 fica fora).
        if (temperatureC < MinTemperatureC - 0.5m || temperatureC > MaxTemperatureC + 0.5m)
        {
            return int.MinValue;
        }

        return (int)Math.Round(temperatureC, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Devolve a densidade da água (g/cm³) para o grau inteiro dado, ou null
    /// quando o grau está fora do intervalo suportado 5–35 °C. Nenhuma
    /// interpolação: apenas a linha exata do grau inteiro.
    /// </summary>
    public static decimal? Resolve(int temperatureWholeDegreeC)
    {
        if (temperatureWholeDegreeC < MinTemperatureC || temperatureWholeDegreeC > MaxTemperatureC)
        {
            return null;
        }

        return DensitiesByWholeDegree[temperatureWholeDegreeC];
    }
}

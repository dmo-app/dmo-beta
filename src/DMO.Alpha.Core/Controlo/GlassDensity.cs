using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Configuração do Controlo: a densidade do vidro por processo de fabrico
/// (definição do Controlo Configuration). A leitura é o consumo canónico do
/// Peso para resolver a densidade aplicável ao processo da CM; o valor exato
/// consumido é preservado no Peso como evidência histórica, pelo que uma
/// alteração posterior da configuração não reescreve Pesos concluídos.
/// </summary>
public sealed class GlassDensity
{
    /// <summary>Processo de fabrico (chave da configuração).</summary>
    public CmProcess Process { get; set; }

    /// <summary>Densidade do vidro configurada para o processo (g/cm³).</summary>
    public decimal DensityGramsPerCm3 { get; set; }
}

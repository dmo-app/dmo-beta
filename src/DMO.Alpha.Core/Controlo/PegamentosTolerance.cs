namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Configuração do Controlo: a tolerância do corredor de Pegamentos
/// (definição do Controlo Configuration — CONFIGURATION.md L20: «Pegamentos
/// tolerance configuration that can replace the default tolerance defined
/// by Pegamentos»). Registo singleton: a
/// linha presente define a tolerância configurada; a linha ausente devolve
/// null e o default canónico do Owner (±0,20) aplica-se. A leitura é o
/// consumo canónico da criação de um registo de Pegamentos: o valor efetivo
/// é preservado no registo como evidência histórica, pelo que uma alteração
/// posterior da configuração não reescreve registos concluídos
/// (CONFIGURATION.md L47-51). A edição pertence à superfície de
/// administração (Admin → App Definições → Controlo), não a este corte.
/// </summary>
public sealed class PegamentosTolerance
{
    public int Id { get; set; }

    /// <summary>
    /// Tolerância configurada (mm) que substitui o default ±0,20 na
    /// criação de novos registos de Pegamentos.
    /// </summary>
    public decimal ToleranceMm { get; set; }

    /// <summary>
    /// Discriminador do singleton: constante 1 (private set — nenhum código
    /// pode alterá-lo) com default de base de dados 1 para que inserções
    /// SQL directas também o carreguem. O índice único FILTRADO sobre ele
    /// torna uma segunda linha de configuração impossível (padrão singleton
    /// com discriminador).
    /// </summary>
    public int SingletonDiscriminator { get; private set; }
}

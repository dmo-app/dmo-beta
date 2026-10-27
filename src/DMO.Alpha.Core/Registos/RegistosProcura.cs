using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Paging;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Registos;

/// <summary>
/// O filtro da procura dos Registos (Diogo, 27/10/2026: «os registos não
/// devem ser apenas referências», «mesmo operadores», «reparadores»). O texto
/// procura na referência, na produção, na ferramenta, no lote, no operador e
/// no reparador; a ferramenta e o Job On vêm dos cliques («Filtrar»).
/// </summary>
public sealed record ProcuraFiltro(
    string? Texto,
    Guid? OperadorId,
    string? Reparador,
    RegistoModulo? Modulo,
    string? Linha,
    DateOnly? De,
    DateOnly? Ate,
    string? ToolId = null,
    int? JobOnId = null)
{
    public bool ComFiltro =>
        !string.IsNullOrWhiteSpace(Texto) || OperadorId is not null || !string.IsNullOrWhiteSpace(Reparador)
        || Modulo is not null || !string.IsNullOrWhiteSpace(Linha) || De is not null || Ate is not null
        || !string.IsNullOrWhiteSpace(ToolId) || JobOnId is not null;
}

/// <summary>Uma ferramenta num Job On, com a % de uso nessa produção; a trocada a meio vem depois da atual.</summary>
public sealed record ProducaoFerramenta(string ToolId, ToolType Tipo, string Referencia, string Lote, decimal? Uso, bool Trocada);

/// <summary>Uma produção (Job On) com as CM, MF e BQ que trabalharam nela.</summary>
public sealed record ProducaoLinha(
    int JobOnId,
    string Referencia,
    string Producao,
    string Maquina,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    IReadOnlyList<ProducaoFerramenta> Ferramentas,
    int? BqContextId)
{
    public IReadOnlyList<ProducaoFerramenta> DoTipo(ToolType tipo) => Ferramentas.Where(f => f.Tipo == tipo).ToList();
}

public sealed record ProcuraProducoes(IReadOnlyList<ProducaoLinha> Linhas, bool Cortada);

/// <summary>O que uma sugestão do campo Procurar escolhe (Diogo, 27/10/2026: «depois dependendo do que seleciona aí aplicar filtros»).</summary>
public enum SugestaoTipo
{
    JobOn = 1,
    Referencia = 2,
    Ferramenta = 3,
    Operador = 4,
    Reparador = 5
}

/// <summary>Uma sugestão: o tipo, o valor do filtro e o texto que se vê.</summary>
public sealed record Sugestao(SugestaoTipo Tipo, string Valor, string Texto, string? Detalhe);

/// <summary>O resumo de uma ferramenta para o painel: quantas produções e a última % de uso.</summary>
public sealed record FerramentaResumo(string ToolId, ToolType Tipo, string Referencia, string Lote, int Producoes, decimal? UltimoUso, DateTime? UltimoUsoEmUtc);

/// <summary>
/// A procura dos Registos: só leitura. As produções vêm dos Job On e dos
/// contextos CM/MF/BQ; os registos vêm dos rastos que cada módulo já grava
/// (<see cref="IRegistosDosUtilizadores.DetalhadosAsync"/>).
/// </summary>
public interface IRegistosProcura
{
    /// <summary>Quantas produções a procura mostra (mais avisa «cortada»).</summary>
    public const int MaxProducoes = 300;

    Task<ProcuraProducoes> ProducoesAsync(ProcuraFiltro filtro, CancellationToken cancellationToken = default);

    Task<PagedList<RegistoDetalhado>> RegistosAsync(ProcuraFiltro filtro, int? pagina, CancellationToken cancellationToken = default);

    Task<FerramentaResumo?> FerramentaAsync(string toolId, CancellationToken cancellationToken = default);

    Task<ProducaoLinha?> ProducaoAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>Os últimos registos de uma ferramenta ou de um Job On (os painéis).</summary>
    Task<IReadOnlyList<RegistoDetalhado>> UltimosAsync(string? toolId, int? jobOnId, int quantos, CancellationToken cancellationToken = default);

    /// <summary>As sugestões do campo Procurar, por tipo, poucas de cada.</summary>
    Task<IReadOnlyList<Sugestao>> SugestoesAsync(string texto, CancellationToken cancellationToken = default);

    /// <summary>Os nomes dos reparadores do Armazém e das Boquilhas, para o filtro.</summary>
    Task<IReadOnlyList<string>> ReparadoresAsync(CancellationToken cancellationToken = default);
}

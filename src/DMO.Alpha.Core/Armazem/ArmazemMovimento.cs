namespace DMO.Alpha.Core.Armazem;

/// <summary>O que um movimento do Armazém fez.</summary>
public enum ArmazemMovimentoTipo
{
    Entrada = 1,
    Saida = 2,
    Corrigir = 3
}

/// <summary>De onde veio a ferramenta que entra (blueprint armazem/OVERVIEW §2).</summary>
public enum ArmazemOrigem
{
    Producao = 1,
    Reparacao = 2,
    Emprestimo = 3,
    Nova = 4,
    JaEstava = 5
}

/// <summary>Para onde vai a ferramenta que sai, por esta ordem (Diogo, 18/10/2026).</summary>
public enum ArmazemDestino
{
    Reparacao = 1,
    Producao = 2,
    Emprestado = 3,
    Sucata = 4
}

/// <summary>Onde está a ferramenta quando não está numa posição: muda só com os movimentos do Armazém.</summary>
public enum FerramentaLocalizacaoTipo
{
    Fabrico = 1,
    AReparar = 2,
    Emprestado = 3,
    Sucata = 4
}

/// <summary>O estado de uma ferramenta (blueprint ferramentas/ESTADO-E-UTILIZACAO §2.1). Só a escolha da pessoa o muda.</summary>
public enum FerramentaEstadoTipo
{
    Novo = 1,
    Reparado = 2,
    PorReparar = 3,
    Sucatado = 4
}

/// <summary>
/// Um movimento do Armazém (blueprint armazem/OVERVIEW §3): só se acrescenta,
/// nunca se altera nem apaga; corrigir é outro movimento. Liga-se à
/// ferramenta pelo <c>tool_id</c>; a referência e o lote só se congelam quando
/// a ferramenta é eliminada (passa para a <c>REMOVED</c>).
/// </summary>
public sealed class ArmazemMovimento
{
    public const int ObservacoesMaxLength = 200;

    public const int FabricaMaxLength = 60;

    public int Id { get; set; }

    public ArmazemMovimentoTipo Tipo { get; set; }

    public string ToolId { get; set; } = string.Empty;

    /// <summary>A referência da ferramenta eliminada (só quando passou para a REMOVED).</summary>
    public string? ToolReferenceFrozen { get; set; }

    /// <summary>O lote da ferramenta eliminada (só quando passou para a REMOVED).</summary>
    public string? ToolLotFrozen { get; set; }

    /// <summary>A posição onde ficou (Entrada, ou Corrigir para uma posição).</summary>
    public int? PosicaoId { get; set; }

    /// <summary>A posição de onde saiu (Saída, ou Corrigir a partir de uma posição).</summary>
    public int? PosicaoAnteriorId { get; set; }

    public ArmazemOrigem? Origem { get; set; }

    public ArmazemDestino? Destino { get; set; }

    /// <summary>O estado escolhido na Entrada, ou na Saída para Sucata.</summary>
    public FerramentaEstadoTipo? Estado { get; set; }

    /// <summary>O reparador de CM ou MF da Saída para Reparação (Armazém › Definições).</summary>
    public int? ReparadorId { get; set; }

    /// <summary>O reparador de BQ da Saída para Reparação (Boquilhas › Definições).</summary>
    public int? BqReparadorId { get; set; }

    /// <summary>O nome do reparador, congelado no movimento, como nas Boquilhas.</summary>
    public string? ReparadorNome { get; set; }

    /// <summary>
    /// A Linha (a máquina) do movimento: na Saída para Produção, a Linha para
    /// onde foi; nos outros, a Linha de onde a ferramenta vem (copiada do
    /// movimento anterior, editável na Entrada). NULL nos movimentos de antes.
    /// </summary>
    public int? MachineId { get; set; }

    /// <summary>
    /// O Job On a que o movimento pertence: na Saída para Produção, o Job On
    /// dessa ferramenta nessa Linha; depois, o mesmo segue na Entrada, na ida à
    /// Reparação e no regresso. NULL nos movimentos de antes (a história liga-os pelas datas).
    /// </summary>
    public int? JobOnId { get; set; }

    /// <summary>O reparador com que a ferramenta volta na Entrada (o da última Saída, editável); o nome, congelado.</summary>
    public string? EntradaReparadorNome { get; set; }

    /// <summary>A fábrica de destino de uma Saída para Emprestado (opcional).</summary>
    public string? FabricaDestino { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>A data e hora reais (UTC); vazio no formulário é o momento do registo.</summary>
    public DateTime OcorridoEmUtc { get; set; }

    public DateTime RegistadoEmUtc { get; set; }

    /// <summary>Quem registou (o nome, senão o nº de operador, do momento).</summary>
    public string? RegistadoPor { get; set; }

    /// <summary>O utilizador de <see cref="RegistadoPor"/>; NULL no ADMIN e quando o utilizador foi eliminado.</summary>
    public Guid? RegistadoPorUserId { get; set; }

    /// <summary>O identificador do envio que gravou este movimento.</summary>
    public Guid OperacaoId { get; set; }

    public string? VersaoApp { get; set; }
}

/// <summary>
/// Uma ferramenta numa posição, desde a Entrada (ou do Corrigir) até à Saída.
/// Uma ocupação ativa por ferramenta (garantido na base); uma posição pode ter
/// mais de uma ferramenta (a CM e o MF da mesma referência e lote; perguntas
/// do full Q8), com aviso quando não são do mesmo par. A linha fica depois de
/// libertada.
/// </summary>
public sealed class ArmazemOcupacao
{
    public int Id { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public int PosicaoId { get; set; }

    public int MovimentoId { get; set; }

    public DateTime DesdeUtc { get; set; }

    public DateTime? LibertadaEmUtc { get; set; }

    public int? LibertadaPorMovimentoId { get; set; }
}

/// <summary>
/// Onde está cada ferramenta que já teve um movimento do Armazém: Fabrico, A
/// reparar (com o reparador), Emprestado ou Sucata, desde quando e por que
/// movimento. Sem linha, a ferramenta está no Fabrico (a localização tem
/// sempre valor). Numa tabela à parte para um movimento não mexer na ficha
/// da ferramenta.
/// </summary>
public sealed class FerramentaLocalizacao
{
    public string ToolId { get; set; } = string.Empty;

    public FerramentaLocalizacaoTipo Localizacao { get; set; }

    public string? ReparadorNome { get; set; }

    public DateTime DesdeUtc { get; set; }

    public int MovimentoId { get; set; }
}

/// <summary>
/// Um estado registado de uma ferramenta: só se acrescenta; o atual é o
/// último. Nesta versão só o Armazém o grava (Entrada e Saída para Sucata).
/// </summary>
public sealed class FerramentaEstado
{
    public const string OrigemArmazem = "armazem";

    public int Id { get; set; }

    public string ToolId { get; set; } = string.Empty;

    public FerramentaEstadoTipo Estado { get; set; }

    public string Origem { get; set; } = OrigemArmazem;

    public int? MovimentoId { get; set; }

    public DateTime RegistadoEmUtc { get; set; }

    public string? RegistadoPor { get; set; }

    public Guid? RegistadoPorUserId { get; set; }
}

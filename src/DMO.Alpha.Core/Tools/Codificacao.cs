using System.Text.RegularExpressions;

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// A B2 trabalha em dupla (com a C1) ou em tripla (com a C3) (Diogo,
/// 28/10/2026: «a B2 deve ser considerada sempre como uma ou outra»).
/// </summary>
public enum B2Modo
{
    Dupla = 1,
    Tripla = 2
}

/// <summary>Uma linha da tabela da codificação: a máquina e, na B2, o modo.</summary>
public sealed record CodificacaoLinha(string Maquina, B2Modo? Modo = null)
{
    /// <summary>«B1», «B2 Dupla», «B2 Tripla».</summary>
    public string Nome => Modo is null ? Maquina : $"{Maquina} {Modo}";

    /// <summary>«B1», «B2:Dupla»: o fim das chaves das definições.</summary>
    public string Chave => Modo is null ? Maquina : $"{Maquina}:{Modo}";
}

/// <summary>Uma célula da tabela: o nome técnico (AN não tem) e a Qtd. do lote.</summary>
public sealed record CodificacaoCelula(string Tipo, string? Padrao, int? Quantidade)
{
    public bool TemPadrao => Tipo != Codificacao.An;
}

public sealed record CodificacaoFila(CodificacaoLinha Linha, IReadOnlyList<CodificacaoCelula> Celulas)
{
    public CodificacaoCelula Celula(string tipo) => Celulas.First(c => c.Tipo == tipo);
}

public sealed record CodificacaoTabela(B2Modo ModoB2, IReadOnlyList<CodificacaoFila> Filas)
{
    public CodificacaoFila? Fila(CodificacaoLinha linha) => Filas.FirstOrDefault(f => f.Linha == linha);
}

/// <summary>O que a tabela dá a uma ferramenta: o nome técnico e as quantidades do lote.</summary>
public sealed record CodificacaoSugestao(CodificacaoLinha Linha, string? NomeTecnico, int? Quantidade, int? QuantidadeAn);

/// <summary>Uma quantidade escrita que não foi gravada (ilegível ou negativa).</summary>
public sealed record CodificacaoIgnorada(string Tipo, string Linha, string Escrito);

public enum GuardarCodificacaoResultado { Guardado, ChaveDesconhecida }

public sealed record GuardarCodificacaoOutcome(GuardarCodificacaoResultado Resultado, IReadOnlyList<CodificacaoIgnorada> Ignoradas);

/// <summary>
/// A CODIFICAÇÃO DOS MOLDES E ACESSÓRIOS POR LINHA (folha «Codificação Moldes e
/// acessórios - MG», Diogo, 28/10/2026): para cada linha, o nome técnico de
/// CM, MF e BQ («xxxx» = a referência) e a Qtd. do lote de CM, MF, BQ e AN.
/// Os nomes são editáveis nas Definições («pode mudar»). As quantidades usam
/// as mesmas chaves do total do lote por máquina; a B2 tem duas filas (Dupla e
/// Tripla) e a chave simples da B2 segue o modo escolhido.
/// </summary>
public static class Codificacao
{
    public const string Cm = "CM";
    public const string Mf = "MF";
    public const string Bq = "BQ";
    public const string An = "AN";

    public const string ModoB2Chave = "Ferramentas:B2Modo";
    public const string PadraoPrefixo = "Ferramentas:Codificacao:";

    /// <summary>O padrão já aplicado às ferramentas pelo «Atualizar» (para mudar os nomes que vieram dele).</summary>
    public const string AplicadoPrefixo = "Ferramentas:CodificacaoAplicada:";

    public static readonly IReadOnlyList<string> Tipos = [Cm, Mf, Bq, An];

    public static readonly CodificacaoLinha B1 = new("B1");
    public static readonly CodificacaoLinha B2Dupla = new("B2", B2Modo.Dupla);
    public static readonly CodificacaoLinha B2Tripla = new("B2", B2Modo.Tripla);

    public static readonly IReadOnlyList<CodificacaoLinha> Linhas =
        [B1, B2Dupla, B2Tripla, new("B3"), new("C1"), new("C2"), new("C3")];

    public static string ChavePadrao(string tipo, CodificacaoLinha linha) => PadraoPrefixo + tipo + ":" + linha.Chave;

    public static string ChaveAplicado(string tipo, CodificacaoLinha linha) => AplicadoPrefixo + tipo + ":" + linha.Chave;

    /// <summary>A chave da Qtd. do lote; fora da B2 é a do total do lote por máquina.</summary>
    public static string ChaveQuantidade(string tipo, CodificacaoLinha linha) => ToolLotQuantityKeys.Prefix + tipo + ":" + linha.Chave;

    // A folha da MG (28/10/2026). A B1 é NNPB (Diogo 11:32: «B1 usa NNX e não
    // PSX»); as outras linhas são PS. BQ x/y da folha = Qtd. BQ / Qtd. AN.
    private static readonly Dictionary<string, (string Cm, string Mf, string Bq, int CmQ, int MfQ, int BqQ, int AnQ)> Folha = new()
    {
        ["B1"] = ("Mxxxx95MPNNX", "Mxxxx95MFNNVX", "ABxxxxNX34", 96, 72, 120, 180),
        ["B2:Dupla"] = ("Mxxxx55MPPSX", "Mxxxx55MFPSV", "ABxxxxPX8855", 48, 36, 60, 90),
        ["B2:Tripla"] = ("Mxxxx44MPPSX", "Mxxxx4TMFPSV", "ABxxxxPX77", 72, 54, 90, 135),
        ["B3"] = ("Mxxxx50MPPSX", "Mxxxx5TMFPSV", "ABxxxxPX8854", 72, 54, 90, 135),
        ["C1"] = ("Mxxxx55MPPSX", "Mxxxx55MFPSV", "ABxxxxPX8855", 48, 36, 60, 90),
        ["C2"] = ("Mxxxx95MPPSX", "Mxxxx95MFPSV", "ABxxxxPX351", 96, 72, 120, 180),
        ["C3"] = ("Mxxxx44MPPSX", "Mxxxx4TMFPSV", "ABxxxxPX77", 72, 54, 90, 135)
    };

    /// <summary>O nome técnico da folha (o que vale sem nada gravado).</summary>
    public static string? PadraoPorOmissao(string tipo, CodificacaoLinha linha) =>
        Folha.TryGetValue(linha.Chave, out var f) ? tipo switch { Cm => f.Cm, Mf => f.Mf, Bq => f.Bq, _ => null } : null;

    /// <summary>A Qtd. do lote da folha.</summary>
    public static int? QuantidadePorOmissao(string tipo, CodificacaoLinha linha) =>
        Folha.TryGetValue(linha.Chave, out var f) ? tipo switch { Cm => f.CmQ, Mf => f.MfQ, Bq => f.BqQ, An => f.AnQ, _ => null } : null;

    private static readonly Regex Xs = new("x{3,}", RegexOptions.CultureInvariant);

    /// <summary>O nome técnico de uma referência: os «xxxx» do padrão passam a ser a referência.</summary>
    public static string? NomeTecnico(string? padrao, string? referencia)
    {
        if (string.IsNullOrWhiteSpace(padrao) || string.IsNullOrWhiteSpace(referencia) || !Xs.IsMatch(padrao)) return null;
        return Xs.Replace(padrao.Trim(), referencia.Trim().ToUpperInvariant(), 1);
    }

    /// <summary>
    /// O modo da B2 pelas outras linhas: com a C3 é Tripla, com a C1 é Dupla
    /// (Diogo, 28/10/2026); sem nenhuma das duas, null.
    /// </summary>
    public static B2Modo? ModoPelasLinhas(IEnumerable<string> maquinas)
    {
        var lista = maquinas.ToList();
        if (lista.Contains("C3", StringComparer.OrdinalIgnoreCase)) return B2Modo.Tripla;
        if (lista.Contains("C1", StringComparer.OrdinalIgnoreCase)) return B2Modo.Dupla;
        return null;
    }

    /// <summary>
    /// A linha da tabela que dá o nome a uma ferramenta: a primeira das suas
    /// máquinas (B1, C1, C3, B3, C2) e, só na B2, a B2 do modo da ferramenta.
    /// Null quando não há nenhuma (sem máquina, ou só a B2 sem modo).
    /// </summary>
    public static CodificacaoLinha? LinhaDe(IEnumerable<string> maquinas, B2Modo? modo)
    {
        var lista = maquinas.Select(m => m.Trim().ToUpperInvariant()).ToList();
        foreach (var maquina in new[] { "B1", "C1", "C3", "B3", "C2" })
        {
            if (lista.Contains(maquina)) return Linhas.First(l => l.Maquina == maquina);
        }

        if (!lista.Contains("B2")) return null;
        return (modo ?? ModoPelasLinhas(lista)) switch
        {
            B2Modo.Dupla => B2Dupla,
            B2Modo.Tripla => B2Tripla,
            _ => null
        };
    }

    public static B2Modo? ModoDe(string? texto) =>
        Enum.TryParse<B2Modo>(texto?.Trim(), ignoreCase: true, out var modo) && Enum.IsDefined(modo) ? modo : null;

    /// <summary>O aviso de uma ferramenta da B2 sem modo (Diogo, 28/10/2026: «sem seleção pode passar a ser um aviso importante»).</summary>
    public static string AvisoSemModo(string nome) => $"{nome} trabalha na B2 sem B2 Dupla nem B2 Tripla escolhida.";
}

/// <summary>A tabela da codificação por linha e o «Atualizar» das ferramentas registadas.</summary>
public interface ICodificacaoFerramentas
{
    Task<CodificacaoTabela> LerAsync(CancellationToken cancellationToken = default);

    /// <summary>Grava os nomes técnicos e as quantidades escritas (chave → texto).</summary>
    Task<GuardarCodificacaoOutcome> GuardarAsync(IReadOnlyDictionary<string, string?> valores, CancellationToken cancellationToken = default);

    /// <summary>Escolhe o modo da B2: filtra o Job On da B2 e põe as quantidades da B2 desse modo.</summary>
    Task GuardarModoB2Async(B2Modo modo, CancellationToken cancellationToken = default);

    /// <summary>O nome técnico e as quantidades para uma ferramenta nova.</summary>
    Task<CodificacaoSugestao?> SugerirAsync(ToolType tipo, string referencia, IReadOnlyList<int> machineIds, B2Modo? modo, CancellationToken cancellationToken = default);

    /// <summary>Quantas ferramentas registadas o «Atualizar» muda.</summary>
    Task<int> ContarAtualizarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica a tabela às ferramentas registadas: nome técnico vazio ou vindo
    /// do padrão anterior, Qtd. do lote e de AN vazias, modo da B2 pelas
    /// outras linhas. Devolve quantas mudaram.
    /// </summary>
    Task<int> AtualizarAsync(CancellationToken cancellationToken = default);
}

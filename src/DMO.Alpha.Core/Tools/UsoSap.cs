using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Uma leitura da UTILIZAÇÃO de uma ferramenta (blueprint
/// ferramentas/ESTADO-E-UTILIZACAO §2.2 e §3): a percentagem, de onde veio,
/// quando e quem. Só se acrescenta; a mostrada é a última. Nesta versão só o
/// SAP a grava (Diogo, 20–21/10/2026: o % Use do relatório «Gestão de
/// Moldes», o Consumption a dividir pela Expected life), com as peças e o
/// código do equipamento ao lado. Vai com a ferramenta quando é eliminada.
/// </summary>
public sealed class FerramentaUtilizacao
{
    public const string OrigemSap = "sap";

    /// <summary>O % uso escrito à mão no Editar da ficha (Diogo, 22/10/2026).</summary>
    public const string OrigemManual = "manual";

    public const int CodigoMaxLength = 64;

    public const int FicheiroMaxLength = 260;

    public long Id { get; set; }

    public string ToolId { get; set; } = null!;

    /// <summary>A percentagem, com duas casas (pode passar de 100).</summary>
    public decimal Percentagem { get; set; }

    public string Origem { get; set; } = OrigemSap;

    public DateTime RegistadaEmUtc { get; set; }

    public string? RegistadoPor { get; set; }

    public Guid? RegistadoPorUserId { get; set; }

    /// <summary>O Equipment Code do SAP: o nome técnico seguido do lote (M544744MPPSX11).</summary>
    public string? CodigoEquipamento { get; set; }

    /// <summary>As peças já feitas (Consumption), quando o SAP as dá.</summary>
    public long? Consumo { get; set; }

    /// <summary>As peças previstas para a vida da ferramenta (Expected life).</summary>
    public long? VidaPrevista { get; set; }

    /// <summary>O ficheiro exportado pelo SAP e a data dele, quando o browser a dá.</summary>
    public string? Ficheiro { get; set; }

    public DateTime? FicheiroModificadoEmUtc { get; set; }
}

/// <summary>
/// O LEMBRETE DE UTILIZAÇÃO (blueprint armazem/OVERVIEW §2 e
/// ferramentas/ESTADO-E-UTILIZACAO §2.2): a Entrada no Armazém vinda da
/// Produção cria-o; fecha-se com a leitura seguinte da utilização. As
/// ferramentas com o lembrete aberto são as que o script do SAP vai buscar
/// (Diogo, 21/10/2026: «deve apenas ir buscar a ferramenta que está
/// flagged»). Um aberto de cada vez por ferramenta.
/// </summary>
public sealed class FerramentaLembreteUtilizacao
{
    public int Id { get; set; }

    public string ToolId { get; set; } = null!;

    public DateTime CriadoEmUtc { get; set; }

    /// <summary>A Entrada que o criou; nenhuma quando foi pedido na ficha.</summary>
    public int? MovimentoId { get; set; }

    public string? CriadoPor { get; set; }

    public Guid? CriadoPorUserId { get; set; }

    public DateTime? FechadoEmUtc { get; set; }

    /// <summary>A leitura que o fechou.</summary>
    public long? UtilizacaoId { get; set; }
}

/// <summary>Uma linha da lista do SAP: o código do equipamento e o uso.</summary>
public sealed record UsoSapLinha(string CodigoEquipamento, decimal PercentagemUso, long? Consumo, long? VidaPrevista);

/// <summary>
/// A leitura de uma exportação do relatório «Gestão de Moldes»: em HTML (o
/// «percentagem.HTML» que o SAP grava) ou em texto (separado por tabulações
/// ou por «|») e o CSV do script do SAP (transação ZPPG: equipment e
/// percent_use). Procura o cabeçalho com «Equipamento» e «% Use»; as outras
/// colunas não interessam. Os números vêm no formato português do SAP
/// (43.200.000 e 90,2). Uma linha ilegível fica de fora, nunca recusa a lista.
/// </summary>
public static class UsoSapExportacao
{
    private static readonly Regex Linha = new(@"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Celula = new(@"<t[hd]\b[^>]*>(.*?)</t[hd]>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Etiqueta = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>A leitura: as linhas lidas, ou nenhuma quando o ficheiro não é a lista do SAP.</summary>
    public sealed record Leitura(bool CabecalhoEncontrado, IReadOnlyList<UsoSapLinha> Linhas);

    public static Leitura Ler(string? conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return new Leitura(false, []);
        }

        var tabela = Tabela(conteudo);
        for (var i = 0; i < tabela.Count; i++)
        {
            var cabecalho = tabela[i];
            var equipamento = Coluna(cabecalho, c => c is "equipamento" or "equipment" or "equipment code" or "equnr");
            var uso = Coluna(cabecalho, c => c is "% use" or "% uso" or "%use" or "percent_use" or "perc");
            if (equipamento < 0 || uso < 0)
            {
                continue;
            }

            var consumo = Coluna(cabecalho, c => c.StartsWith("consumption", StringComparison.Ordinal));
            var vida = Coluna(cabecalho, c => c.StartsWith("expected life", StringComparison.Ordinal));
            var linhas = new List<UsoSapLinha>();
            foreach (var celulas in tabela.Skip(i + 1))
            {
                var codigo = Celula_(celulas, equipamento).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
                if (codigo.Length is 0 or > FerramentaUtilizacao.CodigoMaxLength || !Numero(Celula_(celulas, uso), out var percentagem))
                {
                    continue;
                }

                linhas.Add(new UsoSapLinha(
                    codigo,
                    Math.Round(percentagem, 2),
                    Numero(Celula_(celulas, consumo), out var c) ? (long)Math.Round(c) : null,
                    Numero(Celula_(celulas, vida), out var v) ? (long)Math.Round(v) : null));
            }

            return new Leitura(true, linhas);
        }

        return new Leitura(false, []);
    }

    /// <summary>
    /// O número no formato do SAP em português: o ponto separa os milhares e a
    /// vírgula as décimas (43.200.000 · 90,2 · 129,000); 90.2 também se lê. Fica de fora o que
    /// não for um número, ou um valor fora do razoável.
    /// </summary>
    public static bool Numero(string? texto, out decimal valor)
    {
        valor = 0;
        var bruto = texto?.Trim().Replace(" ", "", StringComparison.Ordinal);
        // Um só ponto com uma ou duas casas a seguir (90.2) é a vírgula das
        // décimas noutro formato: os milhares têm sempre três algarismos.
        var limpo = bruto is not null && !bruto.Contains(',') && Regex.IsMatch(bruto, @"^\d+\.\d{1,2}$")
            ? bruto
            : bruto?.Replace(".", "", StringComparison.Ordinal).Replace(',', '.');
        if (string.IsNullOrEmpty(limpo) || limpo.EndsWith('-'))
        {
            return false;
        }

        return decimal.TryParse(limpo, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor)
            && valor >= 0 && valor < 1_000_000_000_000m;
    }

    /// <summary>As linhas e as células de uma exportação do SAP, em HTML ou em texto.</summary>
    // O ficheiro do PC da fábrica começa com o BOM do UTF-8: sem o tirar, a
    // primeira coluna («Material») não era reconhecida.
    internal static List<IReadOnlyList<string>> Tabela(string conteudo) =>
        (conteudo = conteudo.TrimStart('\uFEFF')).Contains("<tr", StringComparison.OrdinalIgnoreCase) ? TabelaHtml(conteudo) : TabelaTexto(conteudo);

    internal static int Coluna(IReadOnlyList<string> cabecalho, Func<string, bool> nome)
    {
        for (var i = 0; i < cabecalho.Count; i++)
        {
            if (nome(cabecalho[i].Trim().ToLowerInvariant()))
            {
                return i;
            }
        }

        return -1;
    }

    internal static string Celula_(IReadOnlyList<string> celulas, int indice) =>
        indice >= 0 && indice < celulas.Count ? celulas[indice].Trim() : "";

    private static List<IReadOnlyList<string>> TabelaHtml(string html) =>
        Linha.Matches(html)
            .Select(l => (IReadOnlyList<string>)Celula.Matches(l.Groups[1].Value)
                .Select(c => WebUtility.HtmlDecode(Etiqueta.Replace(c.Groups[1].Value, "")).Replace(' ', ' ').Trim())
                .ToList())
            .ToList();

    // O texto: separado por tabulações, por «|» (a lista do SAP) ou por
    // vírgulas com aspas (o CSV do script do SAP, «"90,2"»).
    private static List<IReadOnlyList<string>> TabelaTexto(string texto) =>
        texto.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Contains('\t') || l.Contains('|') || l.Contains(','))
            .Select(l => l.Contains('\t') ? l.Split('\t').Select(c => c.Trim()).ToList()
                : l.Contains('|') ? l.Split('|').Select(c => c.Trim()).ToList()
                : Csv(l))
            .Select(l => (IReadOnlyList<string>)l)
            .ToList();

    private static List<string> Csv(string linha)
    {
        var celulas = new List<string>();
        var atual = new System.Text.StringBuilder();
        var aspas = false;
        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];
            if (aspas)
            {
                if (c == '"' && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    aspas = false;
                }
                else
                {
                    atual.Append(c);
                }
            }
            else if (c == '"')
            {
                aspas = true;
            }
            else if (c == ',')
            {
                celulas.Add(atual.ToString().Trim());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        celulas.Add(atual.ToString().Trim());
        return celulas;
    }
}

/// <summary>
/// O código do equipamento no SAP: o nome técnico seguido do lote com dois
/// algarismos (M544744MPPSX + lote 11 = M544744MPPSX11). Os pontos e os
/// espaços não contam.
/// </summary>
public static class UsoSapCodigo
{
    /// <summary>O código a pedir ao SAP para a ferramenta; nenhum sem nome técnico ou sem lote em número.</summary>
    public static string? Da(string? nomeTecnico, string? lote)
    {
        var nome = Limpo(nomeTecnico);
        if (nome.Length == 0 || !int.TryParse(lote?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
        {
            return null;
        }

        var codigo = nome + numero.ToString("00", CultureInfo.InvariantCulture);
        return codigo.Length <= FerramentaUtilizacao.CodigoMaxLength ? codigo : null;
    }

    /// <summary>Se a linha do SAP é desta ferramenta: o nome técnico e, logo a seguir, o lote (11, 011).</summary>
    public static bool Corresponde(string codigoSap, string? nomeTecnico, string? lote)
    {
        var nome = Limpo(nomeTecnico);
        var codigo = Limpo(codigoSap);
        if (nome.Length == 0 || codigo.Length <= nome.Length || !codigo.StartsWith(nome, StringComparison.Ordinal)
            || !int.TryParse(lote?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
        {
            return false;
        }

        var resto = codigo[nome.Length..];
        return resto.All(char.IsAsciiDigit)
            && int.TryParse(resto, NumberStyles.None, CultureInfo.InvariantCulture, out var loteSap)
            && loteSap == numero;
    }

    private static string Limpo(string? texto) =>
        (texto ?? "").Trim().ToUpperInvariant().Replace(".", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
}

/// <summary>O que aconteceu à lista do SAP.</summary>
public enum ImportarUsoSapResultado
{
    Gravado,

    /// <summary>O ficheiro não tem a lista do SAP (sem «Equipamento» e «% Use»).</summary>
    NaoELista
}

/// <summary>
/// A leitura gravada: quantas linhas, quantas ferramentas ficaram com uma
/// leitura nova e os códigos do SAP que não têm ferramenta no DMO.
/// </summary>
public sealed record ImportarUsoSapOutcome(ImportarUsoSapResultado Resultado, int Linhas, int Atualizadas, IReadOnlyList<string> SemFerramenta);

/// <summary>A última lista lida: quando e de que ficheiro.</summary>
public sealed record UsoSapUltimaLeitura(DateTime LidoEmUtc, DateTime? FicheiroModificadoEmUtc, string? Ficheiro);

/// <summary>A utilização de uma ferramenta para a ficha: a última leitura e o lembrete aberto.</summary>
/// <summary>Uma ferramenta com a utilização por atualizar (o lembrete aberto): para o aviso do Sumário.</summary>
public sealed record FerramentaUsoPorAtualizar(string ToolId, ToolType Tipo, string Referencia, string Lote, DateTime DesdeUtc);

public sealed record FerramentaUtilizacaoAtual(FerramentaUtilizacao? Ultima, DateTime? LembreteDesdeUtc);

/// <summary>
/// A utilização das ferramentas pelo SAP: gravar a lista lida da pasta do
/// SAP (uma leitura nova por ferramenta e o lembrete fechado), os códigos a
/// pedir ao SAP (as ferramentas com o lembrete aberto, nome técnico e lote),
/// pedir a utilização de uma ferramenta na ficha e o que a ficha mostra.
/// </summary>
public interface IFerramentasUsoSap
{
    Task<ImportarUsoSapOutcome> ImportarAsync(string? conteudo, string? ficheiro, DateTime? ficheiroModificadoEmUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> CodigosAsync(CancellationToken cancellationToken = default);

    /// <summary>Os códigos de TODAS as ferramentas com nome técnico e lote («Pedir todas», Diogo 28/10/2026), com ou sem o lembrete aberto.</summary>
    Task<IReadOnlyList<string>> TodosOsCodigosAsync(CancellationToken cancellationToken = default);

    Task<UsoSapUltimaLeitura?> UltimaLeituraAsync(CancellationToken cancellationToken = default);

    Task<FerramentaUtilizacaoAtual> DaFerramentaAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>O % uso da última leitura de cada ferramenta (as que não têm leitura ficam de fora).</summary>
    Task<IReadOnlyDictionary<string, decimal>> UltimasPercentagensAsync(IReadOnlyCollection<string> toolIds, CancellationToken cancellationToken = default);

    /// <summary>Abrir o lembrete na ficha («Pedir a utilização ao SAP»); FALSE quando a ferramenta não existe. Com um aberto, não abre outro.</summary>
    Task<bool> PedirAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O % uso escrito à mão no Editar: uma leitura nova (origem manual, sem
    /// peças) que fecha o lembrete aberto. FALSE quando a ferramenta não existe,
    /// o valor é negativo ou é igual ao da última leitura (nada se grava).
    /// </summary>
    Task<bool> RegistarPercentagemManualAsync(string toolId, decimal percentagem, CancellationToken cancellationToken = default);

    /// <summary>As ferramentas com o lembrete aberto (as mais antigas primeiro), no máximo <paramref name="maximo"/>.</summary>
    Task<IReadOnlyList<FerramentaUsoPorAtualizar>> PorAtualizarAsync(int maximo, CancellationToken cancellationToken = default);
}

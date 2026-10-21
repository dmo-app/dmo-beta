using System.Globalization;

namespace DMO.Alpha.Core.Tools;

/// <summary>
/// Uma linha do relatório de fabrico do SAP («6- Manufacturing Selected»,
/// Diogo, 21/10/2026): o material (a referência com o sufixo do SAP,
/// 5538T324BRA), a linha (a máquina, C3), as datas de início e fim e o que se
/// produziu. Não tem ferramentas: liga-se ao Job On pela referência, pela
/// máquina e pelas datas, e a ferramenta vem do Job On (a CM, o MF e a BQ;
/// Diogo: «a única forma de associar a um Job On é usar a data de início e
/// fim juntamente com o CM, MF e BQ»). Ler o mesmo relatório outra vez
/// atualiza a mesma linha.
/// </summary>
public sealed class SapFabrico
{
    public const int MaterialMaxLength = 64;

    public const int LinhaMaxLength = 32;

    public long Id { get; set; }

    public string Material { get; set; } = null!;

    public string Linha { get; set; } = null!;

    public DateOnly InicioEm { get; set; }

    public DateOnly FimEm { get; set; }

    /// <summary>A produção bruta (Gross Prod), em peças.</summary>
    public long? ProducaoBruta { get; set; }

    /// <summary>A produção líquida (Net Prod.), em peças.</summary>
    public long? ProducaoLiquida { get; set; }

    public DateTime LidoEmUtc { get; set; }

    public string? LidoPor { get; set; }

    public Guid? LidoPorUserId { get; set; }

    public string? Ficheiro { get; set; }

    public DateTime? FicheiroModificadoEmUtc { get; set; }
}

/// <summary>Uma linha lida do relatório de fabrico.</summary>
public sealed record FabricoSapLinha(string Material, string Linha, DateOnly InicioEm, DateOnly FimEm, long? ProducaoBruta, long? ProducaoLiquida);

/// <summary>
/// A leitura do relatório de fabrico do SAP, em texto separado por
/// tabulações (como o SAP o grava) ou em HTML: o cabeçalho com «Material»,
/// «Line», «Begin Date» e «End Date» e pelo menos uma das produções («Gross
/// Prod», «Net Prod.»). As datas vêm como 21.01.2025 e os números no formato
/// português (1.404.234). Uma linha ilegível fica de fora.
/// </summary>
public static class FabricoSapExportacao
{
    private static readonly string[] FormatosData = ["dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"];

    public sealed record Leitura(bool CabecalhoEncontrado, IReadOnlyList<FabricoSapLinha> Linhas);

    public static Leitura Ler(string? conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return new Leitura(false, []);
        }

        var tabela = UsoSapExportacao.Tabela(conteudo);
        for (var i = 0; i < tabela.Count; i++)
        {
            var cabecalho = tabela[i];
            var material = UsoSapExportacao.Coluna(cabecalho, c => c is "material");
            var linha = UsoSapExportacao.Coluna(cabecalho, c => c is "line" or "linha");
            var inicio = UsoSapExportacao.Coluna(cabecalho, c => c is "begin date" or "data início" or "data inicio");
            var fim = UsoSapExportacao.Coluna(cabecalho, c => c is "end date" or "data fim");
            var bruta = UsoSapExportacao.Coluna(cabecalho, c => c.StartsWith("gross prod", StringComparison.Ordinal));
            var liquida = UsoSapExportacao.Coluna(cabecalho, c => c.StartsWith("net prod", StringComparison.Ordinal));
            if (material < 0 || linha < 0 || inicio < 0 || fim < 0 || (bruta < 0 && liquida < 0))
            {
                continue;
            }

            var linhas = new List<FabricoSapLinha>();
            foreach (var celulas in tabela.Skip(i + 1))
            {
                var mat = UsoSapExportacao.Celula_(celulas, material).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
                var lin = UsoSapExportacao.Celula_(celulas, linha).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
                if (mat.Length is 0 or > SapFabrico.MaterialMaxLength || lin.Length is 0 or > SapFabrico.LinhaMaxLength
                    || !Data(UsoSapExportacao.Celula_(celulas, inicio), out var de)
                    || !Data(UsoSapExportacao.Celula_(celulas, fim), out var ate))
                {
                    continue;
                }

                linhas.Add(new FabricoSapLinha(
                    mat,
                    lin,
                    de,
                    ate < de ? de : ate,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, bruta), out var b) ? (long)Math.Round(b) : null,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, liquida), out var l) ? (long)Math.Round(l) : null));
            }

            return new Leitura(true, linhas);
        }

        return new Leitura(false, []);
    }

    private static bool Data(string texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto.Trim(), FormatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out data);

    /// <summary>
    /// Se a linha do SAP é desta produção: o material começa pela referência
    /// do Job On (5538T324BRA é da 5538T324), a linha é a máquina e as datas
    /// cruzam-se com as do Job On (sem fim, conta só o dia do início).
    /// </summary>
    public static bool Corresponde(string material, string linha, DateOnly inicioSap, DateOnly fimSap,
        string referencia, string maquina, DateOnly? inicio, DateOnly? fim)
    {
        if (inicio is not { } de || string.IsNullOrWhiteSpace(referencia))
        {
            return false;
        }

        var ate = fim is { } f && f >= de ? f : de;
        return material.StartsWith(referencia.Trim().ToUpperInvariant(), StringComparison.Ordinal)
            && string.Equals(linha, maquina.Trim(), StringComparison.OrdinalIgnoreCase)
            && inicioSap <= ate && fimSap >= de;
    }
}

/// <summary>O que se leu do relatório de fabrico: as linhas e quantas eram novas.</summary>
public sealed record ImportarFabricoSapOutcome(int Linhas, int Novas);

/// <summary>
/// Uma produção (Job On) em que a ferramenta esteve, com o que o SAP diz que
/// se produziu nela: a soma das linhas do relatório que lhe correspondem.
/// Sem linha do SAP, as produções ficam vazias.
/// </summary>
public sealed record FerramentaProducao(
    int JobOnId,
    string Referencia,
    string Producao,
    string Maquina,
    DateOnly? InicioEm,
    DateOnly? FimEm,
    long? ProducaoBruta,
    long? ProducaoLiquida,
    int LinhasSap);

/// <summary>
/// O fabrico pelo SAP: gravar o relatório lido da pasta do SAP e as
/// produções de uma ferramenta, para a ficha.
/// </summary>
public interface IFerramentasFabricoSap
{
    Task<ImportarFabricoSapOutcome> ImportarAsync(IReadOnlyList<FabricoSapLinha> linhas, string? ficheiro, DateTime? ficheiroModificadoEmUtc, CancellationToken cancellationToken = default);

    /// <summary>A data do último relatório lido, quando o browser a deu.</summary>
    Task<DateTime?> UltimoFicheiroAsync(CancellationToken cancellationToken = default);

    /// <summary>Os Job On em que a ferramenta foi a CM, o MF ou a BQ, do mais recente para o mais antigo.</summary>
    Task<DMO.Alpha.Core.Paging.PagedList<FerramentaProducao>> ProducoesAsync(string toolId, int? pagina, int porPagina, CancellationToken cancellationToken = default);
}

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

    /// <summary>O peso médio do vidro na ordem (Avg weight), em gramas.</summary>
    public decimal? PesoMedioGramas { get; set; }

    /// <summary>Os dias de produção da ordem (Prod. Days).</summary>
    public int? DiasProducao { get; set; }

    /// <summary>
    /// A cadência da ordem (Cadência), em garrafas por minuto: das outras
    /// colunas da ZFMD, a única que se guarda (Diogo, 27/10/2026: «podemos
    /// guardar cadência mas acho que o resto não vale a pena»); vazia quando
    /// o relatório não a traz.
    /// </summary>
    public int? Cadencia { get; set; }

    public DateTime LidoEmUtc { get; set; }

    public string? LidoPor { get; set; }

    public Guid? LidoPorUserId { get; set; }

    public string? Ficheiro { get; set; }

    public DateTime? FicheiroModificadoEmUtc { get; set; }
}

/// <summary>Uma linha lida do relatório de fabrico.</summary>
public sealed record FabricoSapLinha(string Material, string Linha, DateOnly InicioEm, DateOnly FimEm, long? ProducaoBruta, long? ProducaoLiquida,
    decimal? PesoMedioGramas = null, int? DiasProducao = null);

/// <summary>
/// A cadência de uma linha do relatório da ZFMD, da linha com o mesmo
/// material, linha e datas. Sem a coluna, ou sem número, fica vazia.
/// </summary>
public sealed record FabricoSapDetalhe(string Material, string Linha, DateOnly InicioEm, DateOnly FimEm)
{
    public int? Cadencia { get; init; }
}

/// <summary>
/// A leitura do relatório de fabrico do SAP, em texto separado por
/// tabulações (como o SAP o grava) ou em HTML: o cabeçalho com «Material»,
/// «Line», «Begin Date» e «End Date» e pelo menos uma das produções («Gross
/// Prod», «Net Prod.»). As datas vêm como 21.01.2025 e os números no formato
/// português (1.404.234). Uma linha ilegível fica de fora. Também lê o
/// relatório «produções» (Diogo, 26/10/2026): MM, Linha, DATA I, DATA F,
/// QBRUTA (garrafas, bruto), QUTIL e N_DIAS.
/// </summary>
public static class FabricoSapExportacao
{
    private static readonly string[] FormatosData = ["dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"];

    /// <summary>As linhas lidas e, de cada uma, a cadência (<see cref="Detalhes"/>, na mesma ordem).</summary>
    public sealed record Leitura(bool CabecalhoEncontrado, IReadOnlyList<FabricoSapLinha> Linhas)
    {
        public IReadOnlyList<FabricoSapDetalhe> Detalhes { get; init; } = [];
    }

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
            var material = UsoSapExportacao.Coluna(cabecalho, c => c is "material" or "mm");
            var linha = UsoSapExportacao.Coluna(cabecalho, c => c is "line" or "linha");
            var inicio = UsoSapExportacao.Coluna(cabecalho, c => c is "begin date" or "data início" or "data inicio" || Sem(c) == "data i");
            var fim = UsoSapExportacao.Coluna(cabecalho, c => c is "end date" or "data fim" || Sem(c) == "data f");
            var bruta = UsoSapExportacao.Coluna(cabecalho, c => c.StartsWith("gross prod", StringComparison.Ordinal) || c is "qbruta");
            var liquida = UsoSapExportacao.Coluna(cabecalho, c => c.StartsWith("net prod", StringComparison.Ordinal) || c is "qutil");
            var peso = UsoSapExportacao.Coluna(cabecalho, c => c is "avg weight" or "peso médio" or "peso medio");
            var dias = UsoSapExportacao.Coluna(cabecalho, c => c is "prod. days" or "prod days" or "dias" or "n_dias");
            var cadencia = UsoSapExportacao.Coluna(cabecalho, c => c is "cadência" or "cadencia" or "cadence");
            if (material < 0 || linha < 0 || inicio < 0 || fim < 0 || (bruta < 0 && liquida < 0))
            {
                continue;
            }

            var linhas = new List<FabricoSapLinha>();
            var detalhes = new List<FabricoSapDetalhe>();
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
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, liquida), out var l) ? (long)Math.Round(l) : null,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, peso), out var p) && p > 0 && p < 100_000 ? Math.Round(p, 3) : null,
                    UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, dias), out var d) && d < 10_000 ? (int)Math.Round(d) : null));
                detalhes.Add(new FabricoSapDetalhe(mat, lin, de, ate < de ? de : ate)
                {
                    Cadencia = UsoSapExportacao.Numero(UsoSapExportacao.Celula_(celulas, cadencia), out var c) && c < 1_000_000 ? (int)Math.Round(c) : null
                });
            }

            return new Leitura(true, linhas) { Detalhes = detalhes };
        }

        return new Leitura(false, []);
    }

    // O relatório «produções» (Diogo, 26/10/2026) traz «DATA I» com um espaço
    // inseparável; aqui vale como espaço.
    private static string Sem(string cabecalho) => cabecalho.Replace('\u00a0', ' ');

    private static bool Data(string texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto.Trim(), FormatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out data);

    /// <summary>
    /// Se a referência do Job On é mesmo a do material do SAP: o material
    /// começa pela referência (5538T324BRA é da 5538T324) e o carácter a
    /// seguir ao prefixo não pode ser um dígito (senão 5538T32 apanhava
    /// 5538T324BRA, que é da 5538T324; SAP-B03, auditoria de 29/10/2026).
    /// </summary>
    public static bool ReferenciaCorresponde(string material, string referencia)
    {
        var prefixo = referencia.Trim().ToUpperInvariant();
        if (!material.StartsWith(prefixo, StringComparison.Ordinal))
        {
            return false;
        }

        return material.Length == prefixo.Length || !char.IsAsciiDigit(material[prefixo.Length]);
    }

    /// <summary>
    /// Se a linha do SAP é desta produção: a referência do material
    /// (<see cref="ReferenciaCorresponde"/>), a linha é a máquina, e a
    /// produção é a ÚNICA cujo período contém o INÍCIO da linha do SAP
    /// (SAP-B01, auditoria de 29/10/2026: antes bastava cruzar-se com o
    /// período, e uma produção sem fim engolia também as seguintes na mesma
    /// máquina, contando a mesma linha duas vezes). Sem fim, a produção vai
    /// até ao dia antes de a próxima produção começar na mesma máquina
    /// (<paramref name="proximoInicioNaMaquina"/>), ou até sempre se não há
    /// próxima.
    /// </summary>
    public static bool Corresponde(string material, string linha, DateOnly inicioSap, DateOnly fimSap,
        string referencia, string maquina, DateOnly? inicio, DateOnly? fim, DateOnly? proximoInicioNaMaquina = null)
    {
        if (inicio is not { } de || string.IsNullOrWhiteSpace(referencia))
        {
            return false;
        }

        DateOnly ate;
        if (fim is { } f)
        {
            ate = f >= de ? f : de;
        }
        else if (proximoInicioNaMaquina is { } proximo && proximo > de)
        {
            ate = proximo.AddDays(-1);
        }
        else
        {
            ate = DateOnly.MaxValue;
        }

        return ReferenciaCorresponde(material, referencia)
            && string.Equals(linha, maquina.Trim(), StringComparison.OrdinalIgnoreCase)
            && inicioSap >= de && inicioSap <= ate;
    }
}

/// <summary>
/// O peso do SAP de uma produção, para o Peso (Diogo, 21/10/2026: «usa a
/// data desse Job On juntamente com a referência da CM para ir buscar o
/// peso; depois calcula a média com base nos dias e peso, e a moda, o peso
/// que se repete mais»). Cada ordem do SAP conta com os dias que tem dentro
/// das datas do Job On.
/// </summary>
/// <summary>
/// O peso de uma produção: a média pelos dias, a moda, os dias em que a moda
/// se usou (Diogo, 28/10/2026: «quantas vezes usaram 130»), os dias e as ordens.
/// </summary>
public sealed record PesoSapResumo(decimal MediaGramas, decimal ModaGramas, int Dias, int Ordens, int ModaDias = 0);

public static class FabricoSapPeso
{
    /// <summary>
    /// A média pesada pelos dias e a moda (o peso com mais dias; empatados,
    /// o mais recente) das ordens com peso. Os dias de uma ordem são os que
    /// ficam dentro do Job On, contados como o SAP os conta (fim menos
    /// início; pelo menos 1). Nenhum resumo sem ordens com peso.
    /// </summary>
    public static PesoSapResumo? Resumo(IEnumerable<SapFabrico> ordens, DateOnly inicio, DateOnly? fim)
    {
        var ate = fim is { } f && f >= inicio ? f : DateOnly.MaxValue;
        var comPeso = ordens
            .Where(o => o.PesoMedioGramas is > 0)
            .Select(o =>
            {
                var de = o.InicioEm > inicio ? o.InicioEm : inicio;
                var aFim = o.FimEm < ate ? o.FimEm : ate;
                var dias = Math.Max(1, aFim.DayNumber - de.DayNumber);
                return (Peso: o.PesoMedioGramas!.Value, Dias: dias, o.InicioEm);
            })
            .ToList();
        if (comPeso.Count == 0)
        {
            return null;
        }

        var totalDias = comPeso.Sum(o => o.Dias);
        var media = Math.Round(comPeso.Sum(o => o.Peso * o.Dias) / totalDias, 2);
        var moda = comPeso
            .GroupBy(o => o.Peso)
            .OrderByDescending(g => g.Sum(o => o.Dias))
            .ThenByDescending(g => g.Max(o => o.InicioEm))
            .First();
        return new PesoSapResumo(media, moda.Key, totalDias, comPeso.Count, moda.Sum(o => o.Dias));
    }
}

/// <summary>
/// A cadência do SAP de uma produção, como o peso (Diogo, 28/10/2026: «uma
/// versão como a do peso [...] usar uma média e moda, mas mostrar o que foi
/// usado em cada produção»): a média pesada pelos dias e a moda.
/// </summary>
public sealed record CadenciaSapResumo(decimal Media, int Moda, int Dias, int Ordens);

/// <summary>
/// A cadência do SAP de um Job On (Diogo, 27/10/2026: «cadência para meter
/// no Job On»): a moda é a das ordens com mais dias dentro do Job On, contados
/// como no <see cref="FabricoSapPeso.Resumo"/>; empatadas, a mais recente. A
/// média pesa cada cadência pelos dias. Nenhuma sem ordens com cadência.
/// </summary>
public static class FabricoSapCadencia
{
    public static int? DoJobOn(IEnumerable<SapFabrico> ordens, DateOnly inicio, DateOnly? fim) =>
        Resumo(ordens, inicio, fim)?.Moda;

    public static CadenciaSapResumo? Resumo(IEnumerable<SapFabrico> ordens, DateOnly inicio, DateOnly? fim)
    {
        var ate = fim is { } f && f >= inicio ? f : DateOnly.MaxValue;
        var comCadencia = ordens
            .Where(o => o.Cadencia is > 0)
            .Select(o => (Cadencia: o.Cadencia!.Value, Dias: Math.Max(1, (o.FimEm < ate ? o.FimEm : ate).DayNumber - (o.InicioEm > inicio ? o.InicioEm : inicio).DayNumber), o.InicioEm))
            .ToList();
        if (comCadencia.Count == 0)
        {
            return null;
        }

        var totalDias = comCadencia.Sum(o => o.Dias);
        var media = Math.Round((decimal)comCadencia.Sum(o => (long)o.Cadencia * o.Dias) / totalDias, 1);
        var moda = comCadencia
            .GroupBy(o => o.Cadencia)
            .OrderByDescending(g => g.Sum(o => o.Dias))
            .ThenByDescending(g => g.Max(o => o.InicioEm))
            .First().Key;
        return new CadenciaSapResumo(media, moda, totalDias, comCadencia.Count);
    }
}

/// <summary>
/// A cadência que o Job On mostra: a da própria produção, depois de feita; sem
/// ela, a da produção anterior da mesma referência que a tem (Diogo,
/// 28/10/2026: «atribuir sempre a produção anterior»), com o número dela.
/// </summary>
public sealed record CadenciaDoJobOn(CadenciaSapResumo Resumo, int? AnteriorJobOnId = null, string? AnteriorProducao = null)
{
    public bool DaAnterior => AnteriorJobOnId.HasValue;
}

/// <summary>O peso médio do SAP de um Job On (R14), como a <see cref="CadenciaDoJobOn"/>: o desta produção ou o da anterior.</summary>
public sealed record PesoDoJobOn(PesoSapResumo Resumo, int? AnteriorJobOnId = null, string? AnteriorProducao = null)
{
    public bool DaAnterior => AnteriorJobOnId.HasValue;
}

/// <summary>
/// O que se leu do relatório de fabrico: as linhas, quantas eram novas e
/// quantas ordens da janela do relatório deixaram de lá vir e foram retiradas
/// (SAP-03: uma ordem anulada ou corrigida no SAP já não fica para sempre).
/// </summary>
public sealed record ImportarFabricoSapOutcome(int Linhas, int Novas, int Retiradas = 0);

/// <summary>
/// Uma produção (Job On) em que a ferramenta esteve, com o que o SAP diz que
/// se produziu nela: a soma das linhas do relatório que lhe correspondem.
/// Sem linha do SAP, as produções ficam vazias. O delta é o que a
/// utilização subiu com ela: a primeira leitura a partir do dia do fim
/// menos a última até ao dia do início (incluído) (Diogo, 21/10/2026: «o delta e o total»).
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
    int LinhasSap,
    decimal? UtilizacaoAntes = null,
    decimal? UtilizacaoDepois = null,
    CadenciaSapResumo? Cadencia = null,
    int? Ordem = null)
{
    /// <summary>Quanto a utilização subiu nesta produção (pontos percentuais); nenhum sem as duas leituras.</summary>
    public decimal? DeltaUtilizacao => UtilizacaoAntes is { } antes && UtilizacaoDepois is { } depois ? depois - antes : null;
}

/// <summary>
/// O quadro de detalhes da utilização na ficha: a página das produções e o
/// total de todas (quantas e o que o SAP diz que se produziu nelas).
/// </summary>
public sealed record FerramentaProducoes(
    DMO.Alpha.Core.Paging.PagedList<FerramentaProducao> Pagina,
    long? TotalBruta,
    long? TotalLiquida)
{
    /// <summary>
    /// O delta estimado pelo produzido, quando não há as duas leituras à
    /// volta da produção (uma leitura manual só dá o % de hoje): a parte
    /// do % atual que cabe a esta produção pela produção bruta, já que o
    /// uso se calcula pelo total produzido (Diogo, 27/10/2026: «ver porque
    /// não mostra o delta do produzido»). Nenhum sem o produzido, sem o
    /// total ou sem a leitura atual — nunca um zero inventado.
    /// </summary>
    public decimal? DeltaPeloProduzido(FerramentaProducao producao, decimal? percentagemAtual) =>
        producao.ProducaoBruta is { } bruta && TotalBruta is > 0 && percentagemAtual is { } atual
            ? Math.Round(atual * bruta / TotalBruta.Value, 2)
            : null;
}

/// <summary>
/// O fabrico pelo SAP: gravar o relatório lido da pasta do SAP e as
/// produções de uma ferramenta, para a ficha.
/// </summary>
public interface IFerramentasFabricoSap
{
    /// <summary>
    /// Grava as linhas do relatório; com <paramref name="detalhes"/>, também
    /// a cadência da linha com o mesmo material, linha e datas.
    /// </summary>
    Task<ImportarFabricoSapOutcome> ImportarAsync(IReadOnlyList<FabricoSapLinha> linhas, string? ficheiro, DateTime? ficheiroModificadoEmUtc,
        IReadOnlyList<FabricoSapDetalhe>? detalhes = null, CancellationToken cancellationToken = default);

    /// <summary>A data do último relatório lido, quando o browser a deu.</summary>
    Task<DateTime?> UltimoFicheiroAsync(CancellationToken cancellationToken = default);

    /// <summary>O peso do SAP de cada Job On (a referência, a máquina e as datas); fica de fora o Job On sem ordens com peso.</summary>
    Task<IReadOnlyDictionary<int, PesoSapResumo>> PesoDosJobOnsAsync(IReadOnlyCollection<int> jobOnIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// A cadência do SAP do Job On (<see cref="FabricoSapCadencia.Resumo"/>);
    /// sem ordens com cadência, a da produção anterior da mesma referência que
    /// as tem (das últimas <see cref="DMO.Alpha.Core.Production.IProductionPickerQuery.MaxResults"/>); nenhuma sem nenhuma.
    /// </summary>
    Task<CadenciaDoJobOn?> CadenciaDoJobOnAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>O peso médio do SAP do Job On (R14): o desta produção ou o da anterior.</summary>
    Task<PesoDoJobOn?> PesoDoJobOnAsync(int jobOnId, CancellationToken cancellationToken = default);

    /// <summary>Os Job On em que a ferramenta foi a CM, o MF ou a BQ, do mais recente para o mais antigo.</summary>
    Task<FerramentaProducoes> ProducoesAsync(string toolId, int? pagina, int porPagina, CancellationToken cancellationToken = default);

    /// <summary>As produções de várias ferramentas numa só passagem, só as que cabem na linha e nos dias do filtro; sem produções, fica de fora.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<FerramentaProducao>>> ProducoesDeFerramentasAsync(IReadOnlyCollection<string> toolIds, HistoricoFiltro? filtro, CancellationToken cancellationToken = default);
}

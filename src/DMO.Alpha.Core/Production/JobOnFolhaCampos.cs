namespace DMO.Alpha.Core.Production;

/// <summary>O tipo de um campo escrito da folha do Job On: curto (uma linha) ou nota (várias linhas).</summary>
public enum JobOnFolhaTipoCampo
{
    /// <summary>Uma linha de texto (<c>input type="text"</c>).</summary>
    Curto,

    /// <summary>Várias linhas de texto (<c>textarea</c>).</summary>
    Nota
}

/// <summary>
/// Um campo escrito da folha do Job On, como o catálogo
/// <see cref="JobOnFolhaCampos"/> o define. A <see cref="Chave"/> liga o ecrã
/// e a base, e dela saem, sem exceção, a propriedade e a coluna da
/// <c>JobOnFolhas</c> (<c>cm_pata_tol</c> → <c>CmPataTol</c>), o nome no
/// formulário (<c>Folha.CmPataTol</c>) e o id (<c>jobon-cm-pata-tol</c>).
/// </summary>
/// <param name="Chave">A chave do campo (até <see cref="JobOnFolhaCampos.ChaveMaxLength"/> caracteres).</param>
/// <param name="Familia">A família da folha: a sigla da folha da fábrica, «Cabeçalho» ou «Notas gerais».</param>
/// <param name="Rotulo">O rótulo no ecrã, com a unidade entre parênteses («Tol. (mm)», «Folgas 1»).</param>
/// <param name="RotuloAlteracao">O rótulo com a família à frente, para o «O que mudou» e para a recusa de
/// tamanho: «CM · Tol. diâm. pata (mm)», «MF · QTD Stock»; nas Notas gerais, só «Notas gerais».</param>
/// <param name="Tipo">Curto ou nota.</param>
/// <param name="Maximo">O máximo de caracteres, contado depois de normalizar o texto: 60, 1000 ou 4000.</param>
/// <param name="Detalhe">O campo é dos menos usados e fica dentro do «Mais detalhes» da família.</param>
/// <param name="Grupo">O rótulo comum das caixas que ficam juntas sob um só rótulo (as três Folgas:
/// «Folgas (mm)»); null nos outros campos.</param>
public sealed record JobOnFolhaCampo(
    string Chave,
    string Familia,
    string Rotulo,
    string RotuloAlteracao,
    JobOnFolhaTipoCampo Tipo,
    int Maximo,
    bool Detalhe = false,
    string? Grupo = null)
{
    /// <summary>A propriedade da <see cref="JobOnFolha"/>, que é também a coluna da <c>JobOnFolhas</c>.</summary>
    public string Propriedade => JobOnFolhaCampos.PropriedadeDe(Chave);

    /// <summary>O nome no formulário: <c>Folha.{Propriedade}</c>.</summary>
    public string NomeNoFormulario => JobOnFolhaCampos.PrefixoDoFormulario + Propriedade;

    /// <summary>O id no ecrã: <c>jobon-{chave com hífens}</c>.</summary>
    public string Id => JobOnFolhaCampos.IdDe(Chave);
}

/// <summary>
/// O catálogo dos campos escritos da folha do Job On (a folha da fábrica,
/// trazida da E6 do DMO-FULL): uma entrada por chave, família a família. É a
/// única fonte para o ecrã, o handler e a recusa de tamanho: a coluna, o nome
/// no formulário, o id, o rótulo e o máximo saem daqui.
/// <para>
/// Decisões do proprietário que mudam o catálogo da E6: o FO não se usa; cada
/// família CM, MF, BQ, PU, CS e TP tem «QTD Stock» e «QTD Máq.», escritos (Diogo,
/// 21/10/2026); a informação técnica fica no mínimo, por isso os campos menos
/// usados ficam no «Mais detalhes» da família (<see cref="JobOnFolhaCampo.Detalhe"/>).
/// Nenhum campo é obrigatório.
/// </para>
/// <para>
/// Os campos que já existiam continuam onde estão e não entram no catálogo: a
/// referência, o número, a máquina, as datas e a calote nas colunas da
/// <c>JobOns</c>, e as ferramentas CM, MF e BQ nos contextos. As linhas CAL têm
/// tabela própria (<see cref="JobOnFolhaCalLinha"/>); daqui vêm as oito
/// designações por omissão e o máximo de cada campo da linha.
/// </para>
/// <para>
/// Os tamanhos são os da E6 (60 nos campos curtos, 1000 nas notas e 4000 nas
/// Notas gerais). As colunas são <c>text</c> e o limite vive aqui: subir um
/// limite não pede migração.
/// </para>
/// </summary>
public static class JobOnFolhaCampos
{
    /// <summary>O máximo de um campo curto e de cada campo de uma linha CAL.</summary>
    public const int MaximoCurto = 60;

    /// <summary>O máximo das notas de cada família.</summary>
    public const int MaximoNota = 1000;

    /// <summary>O máximo das Notas gerais.</summary>
    public const int MaximoNotasGerais = 4000;

    /// <summary>O tamanho máximo de uma chave.</summary>
    public const int ChaveMaxLength = 16;

    /// <summary>O prefixo do nome no formulário (<c>Folha.CmPataTol</c>).</summary>
    public const string PrefixoDoFormulario = "Folha.";

    /// <summary>O prefixo do id no ecrã (<c>jobon-cm-pata-tol</c>).</summary>
    public const string PrefixoDoId = "jobon-";

    /// <summary>O cabeçalho da produção (Secções, Gota, Processo e Peso).</summary>
    public const string Cabecalho = "Cabeçalho";

    public const string Cm = "CM";
    public const string Mf = "MF";
    public const string Bq = "BQ";
    public const string Pu = "PU";

    /// <summary>A CAL: as linhas têm tabela própria, sem chaves no catálogo.</summary>
    public const string Cal = "CAL";

    public const string An = "AN";
    public const string Arr = "ARR";
    public const string Pi = "PI";
    public const string Cs = "CS";
    public const string Tp = "TP";

    /// <summary>As Notas gerais: a família é o título, e o campo não tem rótulo próprio.</summary>
    public const string NotasGerais = "Notas gerais";

    /// <summary>O rótulo das duas quantidades escritas de cada família e da CAL.</summary>
    public const string QtdStock = "QTD Stock";

    /// <summary>O rótulo da quantidade em máquina, nas famílias e em cada linha CAL.</summary>
    public const string QtdMaq = "QTD Máq.";

    /// <summary>Todas as famílias, pela ordem da folha.</summary>
    public static IReadOnlyList<string> Familias { get; } =
        [Cabecalho, Cm, Tp, Mf, Cs, Bq, An, Pu, Arr, Pi, Cal, NotasGerais];

    /// <summary>
    /// As famílias da folha aos pares, uma coluna por par (decisão do
    /// proprietário): CM+TP, MF+CS, BQ+AN, PU+ARR e, por fim, PI e CAL. O
    /// cabeçalho e as Notas gerais ficam fora das colunas.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Pares { get; } =
        [[Cm, Tp], [Mf, Cs], [Bq, An], [Pu, Arr], [Pi, Cal]];

    /// <summary>
    /// Todos os campos escritos, família a família e, dentro de cada uma, pela
    /// ordem do ecrã: os de uso diário primeiro, as notas, e por fim os do
    /// «Mais detalhes».
    /// </summary>
    public static IReadOnlyList<JobOnFolhaCampo> Todos { get; } =
    [
        Curto("cab_seccoes", Cabecalho, "Secções"),
        Curto("cab_gota", Cabecalho, "Gota"),
        Curto("cab_processo", Cabecalho, "Processo"),
        Curto("cab_peso", Cabecalho, "Peso (g)"),

        Curto("cm_qtd_stock", Cm, QtdStock),
        Curto("cm_qtd_maq", Cm, QtdMaq),
        Curto("cm_utilizacao", Cm, "Utilização (%)"),
        Curto("cm_reparador", Cm, "Reparador"),
        Notas("cm_notas", Cm),
        Detalhe("cm_tipo", Cm, "Tipo"),
        Detalhe("cm_adaptador", Cm, "Adaptador"),
        Detalhe("cm_parafuso", Cm, "Parafuso"),
        Detalhe("cm_pata", Cm, "Diâm. pata (mm)"),
        Detalhe("cm_pata_tol", Cm, "Tol. (mm)", "Tol. diâm. pata (mm)"),
        Detalhe("cm_gargalo", Cm, "Diâm. gargalo (mm)"),
        Detalhe("cm_gargalo_tol", Cm, "Tol. (mm)", "Tol. diâm. gargalo (mm)"),
        Detalhe("cm_exterior", Cm, "Diâm. exterior (mm)"),
        Detalhe("cm_exterior_tol", Cm, "Tol. (mm)", "Tol. diâm. exterior (mm)"),
        Folga("cm_folga1", Cm, 1),
        Folga("cm_folga2", Cm, 2),
        Folga("cm_folga3", Cm, 3),
        Detalhe("cm_inversao", Cm, "Inversão (mm)"),
        Detalhe("cm_almofada3", Cm, "3.ª Almofada"),

        // A calote do TP continua na coluna da JobOns (calote_mm), no «Mais detalhes».
        Curto("tp_qtd_stock", Tp, QtdStock),
        Curto("tp_qtd_maq", Tp, QtdMaq),
        Notas("tp_notas", Tp),
        Detalhe("tp_diametro_ps", Tp, "Diâmetro PS (mm)"),
        Detalhe("tp_bacia_ps", Tp, "Bacia PS"),
        Detalhe("tp_referencia", Tp, "Referência"),

        Curto("mf_qtd_stock", Mf, QtdStock),
        Curto("mf_qtd_maq", Mf, QtdMaq),
        Curto("mf_utilizacao", Mf, "Utilização (%)"),
        Curto("mf_reparador", Mf, "Reparador"),
        Notas("mf_notas", Mf),
        Detalhe("mf_tipo", Mf, "Tipo"),
        Detalhe("mf_adaptador", Mf, "Adaptador"),
        Detalhe("mf_parafuso", Mf, "Parafuso"),
        Detalhe("mf_corpo", Mf, "Diâm. corpo (mm)"),
        Detalhe("mf_corpo_tol", Mf, "Tol. (mm)", "Tol. diâm. corpo (mm)"),
        Detalhe("mf_gargalo", Mf, "Diâm. gargalo (mm)"),
        Detalhe("mf_gargalo_tol", Mf, "Tol. (mm)", "Tol. diâm. gargalo (mm)"),
        Detalhe("mf_exterior", Mf, "Diâm. exterior (mm)"),
        Detalhe("mf_exterior_tol", Mf, "Tol. (mm)", "Tol. diâm. exterior (mm)"),
        Folga("mf_folga1", Mf, 1),
        Folga("mf_folga2", Mf, 2),
        Folga("mf_folga3", Mf, 3),
        Detalhe("mf_inversao", Mf, "Inversão (mm)"),
        Detalhe("mf_fundo_final", Mf, "Fundo final"),
        Detalhe("mf_lote_ff", Mf, "Lote FF"),

        Curto("cs_referencia", Cs, "Referência"),
        Curto("cs_qtd_stock", Cs, QtdStock),
        Curto("cs_qtd_maq", Cs, QtdMaq),
        Notas("cs_notas", Cs),
        Detalhe("cs_furos", Cs, "Furos"),
        Detalhe("cs_tubo", Cs, "Tubo"),

        Curto("bq_qtd_stock", Bq, QtdStock),
        Curto("bq_qtd_maq", Bq, QtdMaq),
        Curto("bq_utilizacao", Bq, "Utilização (%)"),
        Notas("bq_notas", Bq),

        Curto("an_referencia", An, "Referência"),
        Curto("an_lote", An, "Lote"),
        Notas("an_notas", An),

        Curto("pu_referencia", Pu, "Referência"),
        Curto("pu_qtd_stock", Pu, QtdStock),
        Curto("pu_qtd_maq", Pu, QtdMaq),
        Notas("pu_notas", Pu),
        Detalhe("pu_versao", Pu, "Versão"),
        Detalhe("pu_desenho", Pu, "N.º de desenho"),

        Curto("arr_referencia", Arr, "Referência"),
        Notas("arr_notas", Arr),
        Detalhe("arr_versao", Arr, "Versão"),
        Detalhe("arr_desenho", Arr, "N.º de desenho"),

        Curto("pi_pincas", Pi, "Pinças"),
        Curto("pi_diametro", Pi, "Diâmetro (mm)"),
        Notas("pi_notas", Pi),

        new("notas_gerais", NotasGerais, NotasGerais, NotasGerais, JobOnFolhaTipoCampo.Nota, MaximoNotasGerais),
    ];

    private static readonly Dictionary<string, JobOnFolhaCampo> PorChave =
        Todos.ToDictionary(c => c.Chave, StringComparer.Ordinal);

    /// <summary>
    /// As oito designações CAL por omissão, pela ordem da folha da fábrica.
    /// Aparecem quando a folha não tem linhas CAL gravadas. São o ponto de
    /// partida, não dados: sem valor, sem QTD Máq. e com a designação por
    /// mudar, não se gravam.
    /// </summary>
    public static IReadOnlyList<string> CalPorOmissao { get; } =
        ["Bucha Marcada", "CS P Interna", "Tampão", "Inversão MF", "Inversão MP", "Nível", "Cab.Sopro", "Pinças"];

    /// <summary>O máximo da designação, do valor e da QTD Máq. de uma linha CAL.</summary>
    public const int CalMaximo = MaximoCurto;

    /// <summary>O campo da chave, ou null quando a chave não é do catálogo.</summary>
    public static JobOnFolhaCampo? Encontrar(string chave) =>
        PorChave.TryGetValue(chave, out var campo) ? campo : null;

    /// <summary>Os campos de uma família, pela ordem do ecrã (nenhum na CAL).</summary>
    public static IReadOnlyList<JobOnFolhaCampo> DaFamilia(string familia) =>
        Todos.Where(c => c.Familia == familia).ToList();

    /// <summary>A propriedade e a coluna de uma chave: <c>cm_pata_tol</c> → <c>CmPataTol</c>.</summary>
    public static string PropriedadeDe(string chave) =>
        string.Concat(chave
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => char.ToUpperInvariant(parte[0]) + parte[1..]));

    /// <summary>O id no ecrã de uma chave: <c>cm_pata_tol</c> → <c>jobon-cm-pata-tol</c>.</summary>
    public static string IdDe(string chave) => PrefixoDoId + chave.Replace('_', '-');

    private static JobOnFolhaCampo Curto(string chave, string familia, string rotulo, string? rotuloAlteracao = null) =>
        new(chave, familia, rotulo, ComFamilia(familia, rotuloAlteracao ?? rotulo), JobOnFolhaTipoCampo.Curto, MaximoCurto);

    private static JobOnFolhaCampo Detalhe(string chave, string familia, string rotulo, string? rotuloAlteracao = null) =>
        Curto(chave, familia, rotulo, rotuloAlteracao) with { Detalhe = true };

    // As três caixas das Folgas, sob um só rótulo, como na folha da fábrica;
    // no rótulo com a família, o número diz qual («MF · Folgas 2 (mm)»).
    private static JobOnFolhaCampo Folga(string chave, string familia, int numero) =>
        new(chave, familia, $"Folgas {numero}", ComFamilia(familia, $"Folgas {numero} (mm)"),
            JobOnFolhaTipoCampo.Curto, MaximoCurto, Detalhe: true, Grupo: "Folgas (mm)");

    private static JobOnFolhaCampo Notas(string chave, string familia) =>
        new(chave, familia, "Notas", ComFamilia(familia, "Notas"), JobOnFolhaTipoCampo.Nota, MaximoNota);

    private static string ComFamilia(string familia, string rotulo) => $"{familia} · {rotulo}";
}

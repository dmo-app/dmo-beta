using System.Reflection;

namespace DMO.Alpha.Core.Production;

/// <summary>
/// A folha de um Job On: uma linha por Job On, com o que se escreveu em cada
/// campo do catálogo <see cref="JobOnFolhaCampos"/>. A chave é o
/// <see cref="JobOnId"/>, que é também a chave estrangeira para o Job On, e a
/// linha apaga-se em cascata com ele.
/// <para>
/// Há uma propriedade <c>string?</c> por chave do catálogo, com o nome que sai
/// da chave (<c>cm_pata_tol</c> → <see cref="CmPataTol"/>). As colunas são
/// <c>text</c> sem tamanho: os limites vivem no catálogo. Null é vazio.
/// </para>
/// <para>
/// Um Job On sem linha (os de antes da folha, ou criados sem nada escrito) lê-se
/// como folha vazia (<see cref="IJobOnFolhaQuery"/>), e a primeira gravação com
/// alguma coisa escrita cria a linha.
/// </para>
/// </summary>
public sealed class JobOnFolha
{
    // A propriedade de cada chave do catálogo, pelo nome que sai da chave.
    private static readonly Dictionary<string, PropertyInfo> Propriedades =
        JobOnFolhaCampos.Todos.ToDictionary(
            c => c.Chave,
            c => typeof(JobOnFolha).GetProperty(c.Propriedade)
                 ?? throw new InvalidOperationException($"A chave {c.Chave} da folha não tem a propriedade {c.Propriedade}."),
            StringComparer.Ordinal);

    /// <summary>O Job On desta folha: a chave e a chave estrangeira.</summary>
    public int JobOnId { get; set; }

    public JobOn? JobOn { get; set; }

    /// <summary>O valor escrito no campo da <paramref name="chave"/> (null = vazio).</summary>
    /// <exception cref="ArgumentException">A chave não é do catálogo.</exception>
    public string? Valor(string chave) => (string?)Propriedade(chave).GetValue(this);

    /// <summary>Escreve <paramref name="valor"/> no campo da <paramref name="chave"/> (null = vazio).</summary>
    /// <exception cref="ArgumentException">A chave não é do catálogo.</exception>
    public void DefinirValor(string chave, string? valor) => Propriedade(chave).SetValue(this, valor);

    private static PropertyInfo Propriedade(string chave) =>
        Propriedades.TryGetValue(chave, out var propriedade)
            ? propriedade
            : throw new ArgumentException($"A chave {chave} não é um campo da folha.", nameof(chave));

    /// <summary>Cabeçalho · Secções (<c>cab_seccoes</c>).</summary>
    public string? CabSeccoes { get; set; }

    /// <summary>Cabeçalho · Gota (<c>cab_gota</c>).</summary>
    public string? CabGota { get; set; }

    /// <summary>Cabeçalho · Processo (<c>cab_processo</c>).</summary>
    public string? CabProcesso { get; set; }

    /// <summary>Cabeçalho · Peso (g) (<c>cab_peso</c>).</summary>
    public string? CabPeso { get; set; }

    /// <summary>CM · QTD Stock (<c>cm_qtd_stock</c>).</summary>
    public string? CmQtdStock { get; set; }

    /// <summary>CM · QTD Máq. (<c>cm_qtd_maq</c>).</summary>
    public string? CmQtdMaq { get; set; }

    /// <summary>CM · Utilização (%) (<c>cm_utilizacao</c>).</summary>
    public string? CmUtilizacao { get; set; }

    /// <summary>CM · Reparador (<c>cm_reparador</c>).</summary>
    public string? CmReparador { get; set; }

    /// <summary>CM · Notas (<c>cm_notas</c>).</summary>
    public string? CmNotas { get; set; }

    /// <summary>CM · Tipo (<c>cm_tipo</c>).</summary>
    public string? CmTipo { get; set; }

    /// <summary>CM · Adaptador (<c>cm_adaptador</c>).</summary>
    public string? CmAdaptador { get; set; }

    /// <summary>CM · Parafuso (<c>cm_parafuso</c>).</summary>
    public string? CmParafuso { get; set; }

    /// <summary>CM · Diâm. pata (mm) (<c>cm_pata</c>).</summary>
    public string? CmPata { get; set; }

    /// <summary>CM · Tol. diâm. pata (mm) (<c>cm_pata_tol</c>).</summary>
    public string? CmPataTol { get; set; }

    /// <summary>CM · Diâm. gargalo (mm) (<c>cm_gargalo</c>).</summary>
    public string? CmGargalo { get; set; }

    /// <summary>CM · Tol. diâm. gargalo (mm) (<c>cm_gargalo_tol</c>).</summary>
    public string? CmGargaloTol { get; set; }

    /// <summary>CM · Diâm. exterior (mm) (<c>cm_exterior</c>).</summary>
    public string? CmExterior { get; set; }

    /// <summary>CM · Tol. diâm. exterior (mm) (<c>cm_exterior_tol</c>).</summary>
    public string? CmExteriorTol { get; set; }

    /// <summary>CM · Folgas 1 (mm) (<c>cm_folga1</c>).</summary>
    public string? CmFolga1 { get; set; }

    /// <summary>CM · Folgas 2 (mm) (<c>cm_folga2</c>).</summary>
    public string? CmFolga2 { get; set; }

    /// <summary>CM · Folgas 3 (mm) (<c>cm_folga3</c>).</summary>
    public string? CmFolga3 { get; set; }

    /// <summary>CM · Inversão (mm) (<c>cm_inversao</c>).</summary>
    public string? CmInversao { get; set; }

    /// <summary>CM · 3.ª Almofada (<c>cm_almofada3</c>).</summary>
    public string? CmAlmofada3 { get; set; }

    /// <summary>TP · QTD Stock (<c>tp_qtd_stock</c>).</summary>
    public string? TpQtdStock { get; set; }

    /// <summary>TP · QTD Máq. (<c>tp_qtd_maq</c>).</summary>
    public string? TpQtdMaq { get; set; }

    /// <summary>TP · Notas (<c>tp_notas</c>).</summary>
    public string? TpNotas { get; set; }

    /// <summary>TP · Diâmetro PS (mm) (<c>tp_diametro_ps</c>).</summary>
    public string? TpDiametroPs { get; set; }

    /// <summary>TP · Bacia PS (<c>tp_bacia_ps</c>).</summary>
    public string? TpBaciaPs { get; set; }

    /// <summary>TP · Referência (<c>tp_referencia</c>).</summary>
    public string? TpReferencia { get; set; }

    /// <summary>MF · QTD Stock (<c>mf_qtd_stock</c>).</summary>
    public string? MfQtdStock { get; set; }

    /// <summary>MF · QTD Máq. (<c>mf_qtd_maq</c>).</summary>
    public string? MfQtdMaq { get; set; }

    /// <summary>MF · Utilização (%) (<c>mf_utilizacao</c>).</summary>
    public string? MfUtilizacao { get; set; }

    /// <summary>MF · Reparador (<c>mf_reparador</c>).</summary>
    public string? MfReparador { get; set; }

    /// <summary>MF · Notas (<c>mf_notas</c>).</summary>
    public string? MfNotas { get; set; }

    /// <summary>MF · Tipo (<c>mf_tipo</c>).</summary>
    public string? MfTipo { get; set; }

    /// <summary>MF · Adaptador (<c>mf_adaptador</c>).</summary>
    public string? MfAdaptador { get; set; }

    /// <summary>MF · Parafuso (<c>mf_parafuso</c>).</summary>
    public string? MfParafuso { get; set; }

    /// <summary>MF · Diâm. corpo (mm) (<c>mf_corpo</c>).</summary>
    public string? MfCorpo { get; set; }

    /// <summary>MF · Tol. diâm. corpo (mm) (<c>mf_corpo_tol</c>).</summary>
    public string? MfCorpoTol { get; set; }

    /// <summary>MF · Diâm. gargalo (mm) (<c>mf_gargalo</c>).</summary>
    public string? MfGargalo { get; set; }

    /// <summary>MF · Tol. diâm. gargalo (mm) (<c>mf_gargalo_tol</c>).</summary>
    public string? MfGargaloTol { get; set; }

    /// <summary>MF · Diâm. exterior (mm) (<c>mf_exterior</c>).</summary>
    public string? MfExterior { get; set; }

    /// <summary>MF · Tol. diâm. exterior (mm) (<c>mf_exterior_tol</c>).</summary>
    public string? MfExteriorTol { get; set; }

    /// <summary>MF · Folgas 1 (mm) (<c>mf_folga1</c>).</summary>
    public string? MfFolga1 { get; set; }

    /// <summary>MF · Folgas 2 (mm) (<c>mf_folga2</c>).</summary>
    public string? MfFolga2 { get; set; }

    /// <summary>MF · Folgas 3 (mm) (<c>mf_folga3</c>).</summary>
    public string? MfFolga3 { get; set; }

    /// <summary>MF · Inversão (mm) (<c>mf_inversao</c>).</summary>
    public string? MfInversao { get; set; }

    /// <summary>MF · Fundo final (<c>mf_fundo_final</c>).</summary>
    public string? MfFundoFinal { get; set; }

    /// <summary>
    /// MF · Lote FF (<c>mf_lote_ff</c>): o lote do fundo, quando o FF trabalha
    /// com outro lote que não o do MF (Diogo, 22/10/2026). A folha abre-o com o
    /// lote do MF enquanto está vazio.
    /// </summary>
    public string? MfLoteFf { get; set; }

    /// <summary>CS · Referência (<c>cs_referencia</c>).</summary>
    public string? CsReferencia { get; set; }

    /// <summary>CS · QTD Stock (<c>cs_qtd_stock</c>).</summary>
    public string? CsQtdStock { get; set; }

    /// <summary>CS · QTD Máq. (<c>cs_qtd_maq</c>).</summary>
    public string? CsQtdMaq { get; set; }

    /// <summary>CS · Notas (<c>cs_notas</c>).</summary>
    public string? CsNotas { get; set; }

    /// <summary>CS · Furos (<c>cs_furos</c>).</summary>
    public string? CsFuros { get; set; }

    /// <summary>CS · Tubo (<c>cs_tubo</c>).</summary>
    public string? CsTubo { get; set; }

    /// <summary>BQ · QTD Stock (<c>bq_qtd_stock</c>).</summary>
    public string? BqQtdStock { get; set; }

    /// <summary>BQ · QTD Máq. (<c>bq_qtd_maq</c>).</summary>
    public string? BqQtdMaq { get; set; }

    /// <summary>BQ · Utilização (%) (<c>bq_utilizacao</c>).</summary>
    public string? BqUtilizacao { get; set; }

    /// <summary>BQ · Notas (<c>bq_notas</c>).</summary>
    public string? BqNotas { get; set; }

    /// <summary>AN · Referência (<c>an_referencia</c>).</summary>
    public string? AnReferencia { get; set; }

    /// <summary>AN · Notas (<c>an_notas</c>).</summary>
    public string? AnNotas { get; set; }

    /// <summary>PU · Referência (<c>pu_referencia</c>).</summary>
    public string? PuReferencia { get; set; }

    /// <summary>PU · QTD Stock (<c>pu_qtd_stock</c>).</summary>
    public string? PuQtdStock { get; set; }

    /// <summary>PU · QTD Máq. (<c>pu_qtd_maq</c>).</summary>
    public string? PuQtdMaq { get; set; }

    /// <summary>PU · Notas (<c>pu_notas</c>).</summary>
    public string? PuNotas { get; set; }

    /// <summary>PU · Versão (<c>pu_versao</c>).</summary>
    public string? PuVersao { get; set; }

    /// <summary>PU · N.º de desenho (<c>pu_desenho</c>).</summary>
    public string? PuDesenho { get; set; }

    /// <summary>ARR · Referência (<c>arr_referencia</c>).</summary>
    public string? ArrReferencia { get; set; }

    /// <summary>ARR · Notas (<c>arr_notas</c>).</summary>
    public string? ArrNotas { get; set; }

    /// <summary>ARR · Versão (<c>arr_versao</c>).</summary>
    public string? ArrVersao { get; set; }

    /// <summary>ARR · N.º de desenho (<c>arr_desenho</c>).</summary>
    public string? ArrDesenho { get; set; }

    /// <summary>PI · Pinças (<c>pi_pincas</c>).</summary>
    public string? PiPincas { get; set; }

    /// <summary>PI · Diâmetro (mm) (<c>pi_diametro</c>).</summary>
    public string? PiDiametro { get; set; }

    /// <summary>PI · Notas (<c>pi_notas</c>).</summary>
    public string? PiNotas { get; set; }

    /// <summary>Notas gerais (<c>notas_gerais</c>).</summary>
    public string? NotasGerais { get; set; }
}


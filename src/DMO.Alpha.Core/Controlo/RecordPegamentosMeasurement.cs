namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Comando: registar uma leitura dimensional real (measurement row) no
/// controlo de uma produção — o gesto em cruz do operador num componente
/// (CM/BQ/MF) do contexto Job On ativo: a costura (eixo 0°, facto base
/// sempre registado) e a contra costura (eixo 90°, null quando a geometria
/// real não permite a medição — nunca um segundo eixo fabricado). A tool do
/// componente é HERDADA do contexto de produção persistido (spec L92: «It
/// does not ask the user to reselect CM, BQ or MF»): o operador nunca
/// seleciona ferramentas na folha. O campo gargalo transporta APENAS um
/// valor Tool-owned EM FALTA coletado no fluxo da folha (regra
/// Ferramentas/spec L184): grava no tool_id owner (tool_technical_values),
/// nunca como facto do registo de Pegamentos, e um valor fornecido para
/// uma tool que já tem gargalo é ignorado (completion, não correção
/// silenciosa da Tool). Os valores derivados (ovalização, média, corredor)
/// são calculados pelo backend a partir dos factos congelados — nunca
/// fornecidos pelo cliente.
/// </summary>
/// <remarks>
/// Com <see cref="PegamentosId"/>, a leitura vai para esse registo (os
/// Pegamentos sem Job On, ou já associados): as ferramentas são as do
/// registo e o <see cref="ControloId"/> é ignorado.
/// <see cref="AdditionalRows"/>: as outras linhas do mesmo envio («+ Linha»),
/// gravadas a seguir à primeira na mesma escrita — ficam todas ou nenhuma.
/// <see cref="NumeroFerramenta"/>: o número da CM/BQ/MF lido (opcional).
/// </remarks>
public sealed record RecordPegamentosMeasurement(
    int ControloId,
    string ComponentToken,
    decimal CosturaMm,
    decimal? ContraCosturaMm = null,
    decimal? GargaloMm = null,
    int? PegamentosId = null,
    IReadOnlyList<PegamentosRow>? AdditionalRows = null,
    string? NumeroFerramenta = null);

/// <summary>Uma linha a mais do mesmo envio: a costura, a contra costura (null = n/a) e o número da ferramenta.</summary>
public sealed record PegamentosRow(decimal CosturaMm, decimal? ContraCosturaMm, string? NumeroFerramenta = null);

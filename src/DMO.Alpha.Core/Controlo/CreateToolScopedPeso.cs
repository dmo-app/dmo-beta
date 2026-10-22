namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Comando: criar um Peso tool-scoped — uma pesagem real de uma Tool quando
/// ainda não existe produção nem Job On. Input: o tool_id canónico da CM
/// pesada, o tool_id canónico da BQ (marisa) usada na pesagem (dona do
/// volume_marisa que o cálculo principal consome) e a temperatura da água
/// medida (o backend arredonda ao grau inteiro para o lookup da tabela
/// canónica). Nenhum jobon_id/controlo_id/cm_id é inventado ou exigido.
/// Os campos opcionais de contexto técnico transportam APENAS valores
/// Tool-owned EM FALTA coletados diretamente no fluxo do Peso (regra
/// Ferramentas): gravam no tool_id owner — no contexto técnico da própria
/// Tool — e nunca como facto privado do Peso. Um valor fornecido para um
/// campo que a Tool já tem registado é ignorado (completion, não correção
/// silenciosa da Tool). <see cref="ControloId"/> só é passado pelo Peso de
/// uma produção, para gravar o Peso e a associação de uma só vez.
/// <see cref="ComparacaoDoPesoId"/> cria um Peso só para comparação com esse
/// Peso (de uma produção): sem Job On, e sem nunca pedir para o associar.
/// </summary>
public sealed record CreateToolScopedPeso(
    string CmToolId,
    string BqToolId,
    decimal WaterTemperatureC,
    string? CmProcessToken = null,
    decimal? CmVolumePuncaoCm3 = null,
    decimal? CmNominalWeightGrams = null,
    decimal? BqVolumeMarisaCm3 = null,
    int? ControloId = null,
    int? ComparacaoDoPesoId = null);

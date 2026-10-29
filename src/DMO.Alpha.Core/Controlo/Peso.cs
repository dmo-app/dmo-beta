using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Core.Controlo;

/// <summary>
/// Controlo de Peso — o registo operacional durável identificado pelo
/// <c>peso_id</c> (emitido pelo backend). O Peso pode ser tool-scoped:
/// uma pesagem real de uma Tool mesmo sem produção ou Job On. O Peso fica
/// ancorado às Tools canónicas reais usadas na pesagem — a CM pesada e a BQ
/// (marisa) cujo volume_marisa o cálculo consumiu — e nunca inventa
/// <c>jobon_id</c> ou <c>cm_id</c>: o <see cref="ControloId"/> só existe
/// quando o Peso nasce na produção ou pela associação posterior explícita
/// (<c>AssociatePesoToControloHandler</c>, quando a CM do Job On usa o
/// mesmo tool_id).
/// Os valores consumidos pelo cálculo (processo, volume_marisa,
/// volume_puncao, peso nominal opcional, densidade do vidro e o grau inteiro
/// da temperatura da água usado no lookup) são preservados no momento da
/// criação como evidência histórica: uma edição posterior da Tool ou da
/// configuração não reescreve o resultado de um Peso concluído. As leituras
/// (measurement rows) pertencem ao Peso e são factos históricos.
/// </summary>
public sealed class Peso
{
    public int Id { get; set; }

    /// <summary>
    /// tool_id canónico da CM pesada — a ferramenta à qual este Peso fica
    /// diretamente consultável (histórico de Pesos da Tool).
    /// </summary>
    public string CmToolId { get; set; } = null!;

    public Tool CmTool { get; set; } = null!;

    /// <summary>
    /// tool_id canónico da BQ (marisa) usada na pesagem, dona do volume_marisa
    /// consumido pelo cálculo principal.
    /// </summary>
    public string BqToolId { get; set; } = null!;

    public Tool BqTool { get; set; } = null!;

    /// <summary>
    /// Processo de fabrico da CM (token NNPB/PS) consumido na criação —
    /// resolveu a densidade do vidro aplicável. Evidência histórica
    /// congelada; o contexto técnico corrente da Tool pode mudar depois.
    /// </summary>
    public string Process { get; set; } = null!;

    /// <summary>
    /// Grau inteiro da temperatura da água (°C) usado no lookup da tabela
    /// canónica de densidade (temperatura medida arredondada ao grau; sem
    /// interpolação). O valor consumido pelo cálculo, persistido.
    /// </summary>
    public int WaterTemperatureC { get; set; }

    /// <summary>
    /// Densidade da água (g/cm³) que o Peso usou ao ser gravado: a da tabela
    /// para o grau, congelada (AV-PESO-01, 19/10/2026; a mesma regra da
    /// densidade do vidro, D15: os Pesos gravados ficam com o que usaram). A
    /// tabela pode corrigir-se depois (DT9, beta.283) sem mudar os Pesos já
    /// feitos. Null só num Peso criado diretamente sem ela: vale a tabela
    /// (<see cref="WaterDensityTable.ForPeso"/>).
    /// </summary>
    public decimal? WaterDensityGramsPerCm3 { get; set; }

    /// <summary>
    /// volume_marisa (cm³) consumido da BQ Tool — evidência histórica do
    /// input exato do cálculo principal, congelada na criação.
    /// </summary>
    public decimal VolumeMarisaCm3 { get; set; }

    /// <summary>
    /// volume_puncao (cm³) consumido da CM Tool — evidência histórica do
    /// input exato do cálculo principal, congelada na criação.
    /// </summary>
    public decimal VolumePuncaoCm3 { get; set; }

    /// <summary>
    /// Diâmetro D (mm) do tampão, congelado com a punção no momento do Peso
    /// (Diogo, 23/10/2026). Null nos Pesos anteriores a esta coluna.
    /// </summary>
    public decimal? TampaoDiametroMm { get; set; }

    /// <summary>Calote (mm) do tampão, congelada com a punção. Null = não registada.</summary>
    public decimal? TampaoCaloteMm { get; set; }

    /// <summary>
    /// Peso nominal (g) consumido da CM Tool quando existia; null quando a
    /// Tool ainda não tinha peso nominal registado (dado opcional — a ausência
    /// nunca bloqueou a criação do Peso).
    /// </summary>
    public decimal? NominalWeightGrams { get; set; }

    /// <summary>
    /// Densidade do vidro (g/cm³) consumida — o valor exato configurado por
    /// processo no momento da criação, preservado como evidência histórica
    /// para que uma alteração posterior da configuração não reescreva o
    /// resultado deste Peso.
    /// </summary>
    public decimal GlassDensityGramsPerCm3 { get; set; }

    /// <summary>
    /// Referência canónica da CM congelada à criação (evidência histórica):
    /// a app mostra FATOS HUMANOS no histórico da Tool, mesmo depois de a
    /// Tool ser editada ou eliminada (a reatribuição ao DUMMY preserva os
    /// factos congelados; a Tool pode já não existir). Backfill no
    /// handler de eliminação para registos anteriores a esta coluna.
    /// </summary>
    public string? CmToolReferenceFrozen { get; set; }

    /// <summary>Lote da CM congelado à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? CmToolLotFrozen { get; set; }

    /// <summary>Referência canónica da BQ congelada à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? BqToolReferenceFrozen { get; set; }

    /// <summary>Lote da BQ congelado à criação (evidência histórica; ver <see cref="CmToolReferenceFrozen"/>).</summary>
    public string? BqToolLotFrozen { get; set; }

    /// <summary>
    /// controlo_id da produção a que este Peso está associado (corte C — o
    /// Peso production-scoped, spec PESO.md «Production association
    /// cardinality»). NULL nos Pesos tool-scoped pré-produção: a associação
    /// posterior é uma decisão humana explícita e nunca automática. Um Peso
    /// associa-se a NO MÁXIMO uma produção — uma produção posterior da mesma
    /// Tool exige um NOVO peso_id (a associação consome o Peso). Uma produção
    /// tem um Peso para cada CM+BQ (índice único filtrado, sem as linhas
    /// REMOVED): o Peso congela a CM, o volume punção, o nominal e o volume
    /// marisa da BQ, por isso trocar a CM ou a BQ a meio da produção pede um
    /// Peso novo e o anterior fica como estava (respostas R15 e R16-5 do
    /// especialista, 18/10/2026); trocar só o MF não muda o Peso.
    /// </summary>
    public int? ControloId { get; set; }

    public Controlo? Controlo { get; set; }

    /// <summary>
    /// Quando este Peso ficou associado a <see cref="ControloId"/> (UTC), se
    /// foi por uma associação posterior explícita (<c>AssociatePesoToControloHandler</c>);
    /// NULL quando o Peso já nasceu com a produção (nunca associado depois)
    /// ou nos registos anteriores a esta coluna. Sem isto, um Peso criado
    /// antes de o controlo ser aprovado mas associado depois da aprovação
    /// parecia «aprovado» sem o aprovador o ter visto (SAP-B.../PESO-B03,
    /// auditoria de 29/10/2026): a invalidação da aprovação só olhava para
    /// <see cref="RegistadoEmUtc"/>.
    /// </summary>
    public DateTime? AssociadoAoControloEmUtc { get; set; }

    /// <summary>
    /// Quando o registo foi gravado (UTC, pelo backend). Null nos registos
    /// anteriores a 17/10/2026: a data não foi guardada e não se inventa.
    /// A Revisão do Controlo usa-a para pôr o controlo no dia do calendário.
    /// </summary>
    public DateTime? RegistadoEmUtc { get; set; }

    /// <summary>Quem gravou o registo (o nome da sessão, congelado); null quando não se sabe.</summary>
    public string? RegistadoPor { get; set; }

    /// <summary>O utilizador de <see cref="RegistadoPor"/> (<c>Users.Id</c>); NULL no ADMIN, sem sessão, nos registos de antes e quando o utilizador foi eliminado.</summary>
    public Guid? RegistadoPorUserId { get; set; }

    /// <summary>
    /// O controlo da produção anterior usada como referência (o Owner
    /// 2026-10-17: «usa o contexto do controlo anterior»): dele vêm a
    /// referência, o número, a máquina e as datas de início e fim do Job On.
    /// Opcional; o delta é sempre contra o peso abaixo. Como o ControloId, é
    /// um controlo, nunca um jobon_id.
    /// </summary>
    public int? ProducaoAnteriorControloId { get; set; }

    public Controlo? ProducaoAnteriorControlo { get; set; }

    /// <summary>
    /// O peso usado na produção anterior (g), escrito à mão a partir do
    /// SAP. Vai para a folha e para o PDF; null = ainda por indicar.
    /// </summary>
    public decimal? ProducaoAnteriorPesoGramas { get; set; }

    /// <summary>
    /// O Peso para o qual este foi criado só para comparação (o Diogo,
    /// 22/10/2026: «fica associado àquele peso como comparação only; não pede
    /// para associar depois a um Job On»). Null num Peso normal. Um Peso só
    /// para comparação nunca é associado a um Job On (CHECK na base) e é o
    /// Peso anterior do Peso indicado aqui, na «Comparação com outro controlo».
    /// </summary>
    public int? ComparacaoDoPesoId { get; set; }

    /// <summary>Leituras físicas registadas neste Peso (factos históricos).</summary>
    public ICollection<PesoMeasurement> Measurements { get; set; } = new List<PesoMeasurement>();
}

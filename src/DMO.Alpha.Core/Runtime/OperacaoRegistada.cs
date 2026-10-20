namespace DMO.Alpha.Core.Runtime;

/// <summary>
/// Uma operação registada (E4 do DMO-FULL, W-02): o identificador de operação
/// (<c>operacao_id</c>) que a página gera quando é mostrada e manda com o
/// envio. Um envio repetido com o mesmo identificador (um duplo clique, uma
/// resposta perdida, um reinício da app pelo meio) encontra esta linha e não
/// grava um segundo facto: a pessoa vê «já registado».
/// <para>
/// Completa o <c>[EnvioUnico]</c> da beta, que se lembra dos envios em
/// memória durante 30 minutos: esta linha fica na base, vai no mesmo
/// <c>SaveChanges</c> do facto que regista (os dois ficam ou nenhum fica) e
/// sobrevive a um reinício ou a mais de uma instância. Os módulos novos do
/// full (Reparação Interna, Tampões, Armazém, Programada) gravam por aqui.
/// </para>
/// <para>
/// É um registo técnico e não um facto: não tem chave estrangeira e não
/// impede eliminar um utilizador.
/// </para>
/// </summary>
public sealed class OperacaoRegistada
{
    /// <summary>O <c>operacao_id</c> do envio: a chave primária.</summary>
    public Guid Id { get; init; }

    /// <summary>O que o envio fez, num token do módulo (por exemplo «armazem.movimento»).</summary>
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Quem enviou (o <c>ActorId</c> da sessão); NULL sem sessão. Sem chave estrangeira.</summary>
    public Guid? AutorUserId { get; init; }

    /// <summary>O tipo do resultado, por exemplo «JobOn» ou «ArmazemMovimento».</summary>
    public string ResultadoTipo { get; set; } = string.Empty;

    /// <summary>O identificador do resultado (o registo criado ou a folha onde gravou); NULL quando não tem.</summary>
    public int? ResultadoId { get; set; }

    /// <summary>Quando a operação foi registada (UTC).</summary>
    public DateTimeOffset MomentoUtc { get; init; }
}

/// <summary>O resultado de uma operação: o tipo e o identificador, que o envio repetido recebe de volta.</summary>
public sealed record ResultadoDaOperacao(string Tipo, int? Id);

/// <summary>
/// As operações registadas: procurar o resultado de um envio anterior com o
/// mesmo identificador e registar o envio no mesmo contexto do facto, para ir
/// no mesmo <c>SaveChanges</c>.
/// </summary>
public interface IOperacoesRegistadas
{
    /// <summary>A operação já registada com este identificador, ou null.</summary>
    Task<OperacaoRegistada?> ProcurarAsync(Guid operacaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta a linha ao contexto, sem gravar: quem chama grava-a com o
    /// facto. Um segundo envio com o mesmo identificador colide na chave
    /// primária e o facto dele não fica.
    /// </summary>
    OperacaoRegistada Registar(Guid operacaoId, string tipo, ResultadoDaOperacao resultado);
}

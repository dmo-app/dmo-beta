using DMO.Alpha.Core.Runtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// As operações registadas (E4 do DMO-FULL) sobre o <see cref="DmoDbContext"/>
/// do pedido: <see cref="Registar"/> acrescenta a linha ao mesmo contexto do
/// facto, para irem os dois no mesmo <c>SaveChanges</c>. Dois envios com o
/// mesmo identificador ao mesmo tempo colidem na chave primária
/// (<c>PK_OperacoesRegistadas</c>, 23505): o segundo não grava nada, relê e
/// devolve «já registado» com o resultado do primeiro
/// (<see cref="EOperacaoRepetida"/>).
/// </summary>
public sealed class OperacoesRegistadas : IOperacoesRegistadas
{
    /// <summary>O nome da chave primária, que o 23505 de um envio repetido traz.</summary>
    public const string ChavePrimaria = "PK_OperacoesRegistadas";

    private readonly DmoDbContext _db;
    private readonly ICurrentAccountContext? _account;

    public OperacoesRegistadas(DmoDbContext db, ICurrentAccountContext? account = null)
    {
        _db = db;
        _account = account;
    }

    public Task<OperacaoRegistada?> ProcurarAsync(Guid operacaoId, CancellationToken cancellationToken = default) =>
        _db.OperacoesRegistadas
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == operacaoId, cancellationToken);

    public OperacaoRegistada Registar(Guid operacaoId, string tipo, ResultadoDaOperacao resultado, string? impressao = null)
    {
        var operacao = new OperacaoRegistada
        {
            Id = operacaoId,
            Tipo = tipo,
            AutorUserId = Autor(),
            ResultadoTipo = resultado.Tipo,
            ResultadoId = resultado.Id,
            MomentoUtc = DateTimeOffset.UtcNow,
            Impressao = impressao
        };
        _db.OperacoesRegistadas.Add(operacao);
        return operacao;
    }

    public bool EOMesmoEnvio(OperacaoRegistada registada, string tipo, string? impressao) =>
        registada.Tipo == tipo
        && registada.AutorUserId == Autor()
        && registada.Impressao == impressao;

    private Guid? Autor() =>
        _account?.Current is { IsAuthenticated: true } actor ? actor.ActorId : null;

    /// <summary>
    /// O 23505 na chave primária das operações: outro envio com o mesmo
    /// identificador gravou primeiro. Não se repete: relê-se e é «já registado».
    /// </summary>
    public static bool EOperacaoRepetida(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ChavePrimaria };
}

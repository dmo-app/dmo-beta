using System.Linq.Expressions;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Um autor guardado num facto (E3 do DMO-FULL): o texto de sempre (o nome
/// ou o número de operador do momento) e, ao lado, o <c>Users.Id</c> de quem
/// gravou. Os nomes são os das propriedades da entidade, que são também os
/// das colunas.
/// </summary>
public sealed record Autoria(Type Entidade, string Texto, string Identificador)
{
    /// <summary>Um autor de <typeparamref name="T"/>, com as propriedades como expressões, para um nome errado não compilar.</summary>
    public static Autoria De<T>(Expression<Func<T, string?>> texto, Expression<Func<T, Guid?>> identificador) =>
        new(typeof(T), NomeDe(texto), NomeDe(identificador));

    private static string NomeDe(LambdaExpression propriedade)
    {
        var corpo = propriedade.Body is UnaryExpression { NodeType: ExpressionType.Convert } conversao
            ? conversao.Operand
            : propriedade.Body;
        return corpo is MemberExpression membro
            ? membro.Member.Name
            : throw new ArgumentException($"A expressão {propriedade} não é uma propriedade da entidade.", nameof(propriedade));
    }
}

/// <summary>
/// A lista dos autores guardados nos factos (E3 do DMO-FULL, fundações do
/// full). Cada coluna que guarda um autor como texto está aqui, com o
/// identificador do utilizador ao lado (o teste <c>AutoriaMapTests</c> falha
/// quando falta uma). O <see cref="DMO.Alpha.Infrastructure.Controlo.RegistoCarimboInterceptor"/>
/// grava o identificador de quem tem a sessão sempre que o texto é escrito, e
/// o <see cref="DmoDbContext"/> dá a cada identificador o seu índice e a sua
/// chave estrangeira para <c>Users</c>.
/// <para>
/// A chave estrangeira é <c>ON DELETE SET NULL</c>: eliminar um utilizador
/// continua a funcionar como na beta, e os factos dele ficam com o texto do
/// momento. O ADMIN da beta não tem linha em <c>Users</c>, por isso os factos
/// dele ficam só com o texto.
/// </para>
/// </summary>
public static class AutoriaMap
{
    public static IReadOnlyList<Autoria> Autores { get; } =
    [
        // O Controlo.
        Autoria.De<Peso>(p => p.RegistadoPor, p => p.RegistadoPorUserId),
        Autoria.De<Pegamentos>(p => p.RegistadoPor, p => p.RegistadoPorUserId),
        Autoria.De<PegamentosMeasurementHistorico>(h => h.Por, h => h.PorUserId),
        Autoria.De<PuncaoInspecao>(i => i.RegisteredBy, i => i.RegisteredByUserId),
        Autoria.De<Puncoes>(p => p.UpdatedBy, p => p.UpdatedByUserId),
        Autoria.De<DMO.Alpha.Core.Controlo.Controlo>(c => c.ApprovedByOperatorId, c => c.ApprovedByUserId),
        Autoria.De<DMO.Alpha.Core.Controlo.Controlo>(c => c.NotApprovedByOperatorId, c => c.NotApprovedByUserId),
        Autoria.De<ControloDecisao>(d => d.OperatorId, d => d.DecididoPorUserId),
        Autoria.De<ControloAreaState>(s => s.UpdatedBy, s => s.UpdatedByUserId),
        Autoria.De<ControloAreaStateHistorico>(h => h.Por, h => h.PorUserId),
        Autoria.De<ControloViewing>(v => v.OperatorId, v => v.ViewerUserId),
        Autoria.De<PesoComparacaoCmDecisao>(d => d.EscolhidoPor, d => d.EscolhidoPorUserId),
        Autoria.De<PesoComparacaoCmDecisao>(d => d.AprovadoPor, d => d.AprovadoPorUserId),
        Autoria.De<EmailEnvio>(e => e.EnviadoPor, e => e.EnviadoPorUserId),

        // As Boquilhas.
        Autoria.De<BqMovement>(m => m.RegisteredByOperatorId, m => m.RegisteredByUserId),
        Autoria.De<BqMovement>(m => m.CorrectedByNameFrozen, m => m.CorrectedByUserId),
        Autoria.De<BqMovementDeletion>(d => d.DeletedByNameFrozen, d => d.DeletedByUserId),
        Autoria.De<BqRepairTrace>(t => t.DiscrepancyResetByNameFrozen, t => t.DiscrepancyResetByUserId),

        // O Job On e as Ferramentas.
        Autoria.De<JobOnChange>(c => c.ChangedByNameFrozen, c => c.ChangedByUserId),
        Autoria.De<JobOnChange>(c => c.RemovedByNameFrozen, c => c.RemovedByUserId),
        Autoria.De<Tool>(t => t.DuplicateWarningByNameFrozen, t => t.DuplicateWarningByUserId),

        // O Armazém.
        Autoria.De<ArmazemPosicao>(p => p.CriadaPor, p => p.CriadaPorUserId),
        Autoria.De<ArmazemMovimento>(m => m.RegistadoPor, m => m.RegistadoPorUserId),
        Autoria.De<ArmazemLoteNovo>(l => l.RegistadoPor, l => l.RegistadoPorUserId),
        Autoria.De<ArmazemLoteNovo>(l => l.ResolvidoPor, l => l.ResolvidoPorUserId),
        Autoria.De<FerramentaEstado>(e => e.RegistadoPor, e => e.RegistadoPorUserId),

        // A Reparação Programada.
        Autoria.De<DMO.Alpha.Core.Programada.ReparacaoProgramadaLista>(l => l.CriadaPor, l => l.CriadaPorUserId),

        // A utilização das ferramentas.
        Autoria.De<FerramentaUtilizacao>(u => u.RegistadoPor, u => u.RegistadoPorUserId),
        Autoria.De<FerramentaLembreteUtilizacao>(l => l.CriadoPor, l => l.CriadoPorUserId),
        Autoria.De<SapFabrico>(f => f.LidoPor, f => f.LidoPorUserId),
    ];

    /// <summary>Os autores de uma entidade.</summary>
    public static IEnumerable<Autoria> De(Type entidade) => Autores.Where(a => a.Entidade == entidade);
}

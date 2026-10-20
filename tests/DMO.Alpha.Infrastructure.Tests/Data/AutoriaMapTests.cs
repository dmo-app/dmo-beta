using System.Text.RegularExpressions;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Contra a deriva do autor pelo utilizador (E3 do DMO-FULL), sem servidor:
/// cada coluna do modelo que guarda um autor como texto está na
/// <see cref="AutoriaMap"/>, e cada identificador tem índice e a chave
/// estrangeira para Users com SET NULL. Um autor novo que fique fora da lista
/// falha aqui, antes de os factos dele ficarem sem identificador.
/// </summary>
public sealed class AutoriaMapTests
{
    // Ligação nunca contactada: só se lê o modelo com o fornecedor PostgreSQL.
    private const string TestConnectionString = "Host=localhost;Database=dmo_alpha_test;Username=dmo;Password=dmo";

    /// <summary>Um nome que diz «quem»: …By, …Por, com OperatorId, NameFrozen, Name ou Nome; o OperatorId sozinho; o que tiver «Autor».</summary>
    private static readonly Regex AutorComoTexto = new(@"(By|Por)(OperatorId|NameFrozen|Name|Nome)?$|^OperatorId$|Autor", RegexOptions.CultureInvariant);

    /// <summary>As colunas com nome de autor que não são autores de factos, e porquê.</summary>
    private static readonly (Type Entidade, string Propriedade, string Motivo)[] ForaDaLista =
    [
        (typeof(User), nameof(User.OperatorId), "o número do próprio utilizador"),
        (typeof(JobOnLotNote), nameof(JobOnLotNote.CreatedBy), "as notas de lote antigas já não se criam desde a beta.142"),
    ];

    private static DmoDbContext Contexto() =>
        new(new DbContextOptionsBuilder<DmoDbContext>().UseNpgsql(TestConnectionString).Options);

    [Fact]
    public void CadaAutorGuardadoComoTexto_EstaNaLista()
    {
        using var db = Contexto();
        var naLista = AutoriaMap.Autores.Select(a => (a.Entidade, a.Texto)).ToHashSet();
        var fora = ForaDaLista.Select(f => (f.Entidade, f.Propriedade)).ToHashSet();

        var textos = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Where(p => p.ClrType == typeof(string)))
            .Where(p => AutorComoTexto.IsMatch(p.Name))
            .Select(p => (p.DeclaringType.ClrType, p.Name))
            .ToList();

        Assert.Empty(textos.Where(t => !naLista.Contains(t) && !fora.Contains(t)).Select(t => $"{t.ClrType.Name}.{t.Name}"));
        Assert.All(fora, f => Assert.Contains(f, textos));
    }

    [Fact]
    public void CadaIdentificador_TemIndiceEChaveParaUsersComSetNull()
    {
        using var db = Contexto();
        foreach (var autoria in AutoriaMap.Autores)
        {
            var entidade = db.Model.FindEntityType(autoria.Entidade)!;
            Assert.NotNull(entidade.FindProperty(autoria.Texto));
            var identificador = entidade.FindProperty(autoria.Identificador)!;
            Assert.Equal(typeof(Guid?), identificador.ClrType);

            var chave = Assert.Single(entidade.GetForeignKeys(), fk => fk.Properties.Single() == identificador);
            Assert.Equal(typeof(User), chave.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.SetNull, chave.DeleteBehavior);
            Assert.Contains(entidade.GetIndexes(), i => i.Properties.SequenceEqual([identificador]));
        }
    }
}

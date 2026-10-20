using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// O autor pelo utilizador (fundações do full, E3 do DMO-FULL): quando um
/// facto escreve o autor como texto, o carimbo grava ao lado o utilizador da
/// sessão. O ADMIN (sem linha em Users) e quem não tem sessão ficam sem ele.
/// </summary>
public sealed class AutorPorUtilizadorTests
{
    private static readonly Guid Utilizador = Guid.NewGuid();
    private static readonly Guid OutroUtilizador = Guid.NewGuid();

    private readonly string _base = Guid.NewGuid().ToString();

    private DmoDbContext Contexto(ActorContext actor) =>
        new(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(_base)
            .AddInterceptors(new RegistoCarimboInterceptor(new Sessao(actor)))
            .Options);

    private static ActorContext Operador(Guid id, string nome) => new() { IsAuthenticated = true, ActorId = id, DisplayName = nome, OperatorId = "4801" };

    private async Task<Peso> PesoGravadoPorAsync(ActorContext actor)
    {
        int id;
        await using (var db = Contexto(actor))
        {
            var peso = new Peso { CmToolId = "CM-1", BqToolId = "BQ-1", Process = "NNPB" };
            db.Pesos.Add(peso);
            await db.SaveChangesAsync();
            id = peso.Id;
        }

        await using var leitura = Contexto(ActorContext.Anonymous);
        return await leitura.Pesos.AsNoTracking().SingleAsync(p => p.Id == id);
    }

    [Fact]
    public async Task PesoNovo_GuardaOUtilizadorAoLadoDoNome()
    {
        var peso = await PesoGravadoPorAsync(Operador(Utilizador, "Operador"));

        Assert.Equal("Operador", peso.RegistadoPor);
        Assert.Equal(Utilizador, peso.RegistadoPorUserId);
    }

    // O ADMIN da beta não tem linha em Users: mesmo com um identificador na
    // sessão, o facto fica só com o texto (a chave estrangeira recusaria-o).
    [Fact]
    public async Task OAdmin_FicaSoComOTexto()
    {
        var peso = await PesoGravadoPorAsync(new ActorContext { IsAuthenticated = true, IsAdmin = true, ActorId = Guid.NewGuid(), DisplayName = "admin@dmo.test" });

        Assert.Equal("admin@dmo.test", peso.RegistadoPor);
        Assert.Null(peso.RegistadoPorUserId);
    }

    [Fact]
    public async Task SemSessao_OTextoFicaEOIdentificadorNao()
    {
        await using (var db = Contexto(new ActorContext { IsAuthenticated = false, ActorId = Guid.NewGuid(), DisplayName = "Alguém" }))
        {
            db.Tools.Add(new Tool { ToolId = "CM-SEM-SESSAO", Type = ToolType.Cm, Reference = "1075", Lot = "1", DuplicateWarningByNameFrozen = "Alguém" });
            await db.SaveChangesAsync();
        }

        await using var leitura = Contexto(ActorContext.Anonymous);
        var tool = await leitura.Tools.AsNoTracking().SingleAsync(t => t.ToolId == "CM-SEM-SESSAO");
        Assert.Equal("Alguém", tool.DuplicateWarningByNameFrozen);
        Assert.Null(tool.DuplicateWarningByUserId);
    }

    [Fact]
    public async Task FactoAlterado_OUtilizadorSegueOTexto()
    {
        await using (var db = Contexto(Operador(Utilizador, "Operador")))
        {
            var tool = new Tool { ToolId = "CM-AUTOR", Type = ToolType.Cm, Reference = "1075", Lot = "1" };
            db.Tools.Add(tool);
            await db.SaveChangesAsync();
            Assert.Null(tool.DuplicateWarningByUserId);

            tool.DuplicateWarningByNameFrozen = "Operador";
            await db.SaveChangesAsync();
            Assert.Equal(Utilizador, tool.DuplicateWarningByUserId);
        }

        // Outra sessão muda só o lote: o autor do aviso continua o primeiro.
        await using (var db = Contexto(Operador(OutroUtilizador, "Outro")))
        {
            var tool = await db.Tools.SingleAsync(t => t.ToolId == "CM-AUTOR");
            tool.Lot = "2";
            await db.SaveChangesAsync();
            Assert.Equal(Utilizador, tool.DuplicateWarningByUserId);

            // A mesma sessão escreve o aviso outra vez: passa a ser dela.
            tool.DuplicateWarningByNameFrozen = "Outro";
            await db.SaveChangesAsync();
            Assert.Equal(OutroUtilizador, tool.DuplicateWarningByUserId);

            tool.DuplicateWarningByNameFrozen = null;
            await db.SaveChangesAsync();
            Assert.Null(tool.DuplicateWarningByUserId);
        }
    }

    private sealed class Sessao(ActorContext actor) : ICurrentAccountContext
    {
        public ActorContext Current { get; } = actor;
    }
}

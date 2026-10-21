using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// O Armazém nas ferramentas do Job On (Diogo 21/10, FER-E-03: só avisa):
/// onde está a ferramenta (na posição, a reparar, no Fabrico) e o último
/// estado; uma ferramenta nunca registada no Armazém ou sem estado tem um
/// aviso, que não impede nada.
/// </summary>
public sealed class JobOnToolFactsArmazemTests
{
    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-jobon-armazem-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = "na-posicao", Type = ToolType.Cm, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "a-reparar", Type = ToolType.Mf, Reference = "5447T173", Lot = "3" },
            new Tool { ToolId = "no-fabrico", Type = ToolType.Bq, Reference = "T173", Lot = "01" },
            new Tool { ToolId = "sem-estado", Type = ToolType.Cm, Reference = "6012A118", Lot = "2" },
            new Tool { ToolId = "nunca", Type = ToolType.Mf, Reference = "6012A118", Lot = "2" },
            new Tool { ToolId = "so-estado", Type = ToolType.Bq, Reference = "A118", Lot = "07" });
        db.ArmazemPosicoes.Add(new ArmazemPosicao { Id = 1, Codigo = "1033", CriadaEmUtc = DateTime.UtcNow });
        db.ArmazemMovimentos.AddRange(
            Movimento(1, "na-posicao", ArmazemMovimentoTipo.Entrada),
            Movimento(2, "a-reparar", ArmazemMovimentoTipo.Saida),
            Movimento(3, "no-fabrico", ArmazemMovimentoTipo.Entrada),
            Movimento(4, "no-fabrico", ArmazemMovimentoTipo.Saida),
            Movimento(5, "sem-estado", ArmazemMovimentoTipo.Saida));
        db.ArmazemOcupacoes.Add(new ArmazemOcupacao { ToolId = "na-posicao", PosicaoId = 1, MovimentoId = 1, DesdeUtc = DateTime.UtcNow });
        db.FerramentaLocalizacoes.Add(new FerramentaLocalizacao
        {
            ToolId = "a-reparar", Localizacao = FerramentaLocalizacaoTipo.AReparar, ReparadorNome = "Moldes Lda", DesdeUtc = DateTime.UtcNow, MovimentoId = 2
        });
        db.FerramentaEstados.AddRange(
            new FerramentaEstado { Id = 1, ToolId = "na-posicao", Estado = FerramentaEstadoTipo.PorReparar, RegistadoEmUtc = DateTime.UtcNow },
            new FerramentaEstado { Id = 2, ToolId = "na-posicao", Estado = FerramentaEstadoTipo.Reparado, RegistadoEmUtc = DateTime.UtcNow },
            new FerramentaEstado { Id = 3, ToolId = "a-reparar", Estado = FerramentaEstadoTipo.PorReparar, RegistadoEmUtc = DateTime.UtcNow },
            new FerramentaEstado { Id = 4, ToolId = "no-fabrico", Estado = FerramentaEstadoTipo.Novo, RegistadoEmUtc = DateTime.UtcNow },
            // Com estado mas sem nenhum movimento no Armazém: o caso das ferramentas vindas do Access.
            new FerramentaEstado { Id = 5, ToolId = "so-estado", Estado = FerramentaEstadoTipo.Reparado, RegistadoEmUtc = DateTime.UtcNow });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ArmazemMovimento Movimento(int id, string toolId, ArmazemMovimentoTipo tipo) => new()
    {
        Id = id, Tipo = tipo, ToolId = toolId, OcorridoEmUtc = DateTime.UtcNow, RegistadoEmUtc = DateTime.UtcNow, OperacaoId = Guid.NewGuid()
    };

    [Fact]
    public async Task OndeEstaOEstadoEOAviso()
    {
        using var db = CreateContext();

        var factos = (await new JobOnToolFactsQuery(db).FindAsync(
                ["na-posicao", "a-reparar", "no-fabrico", "sem-estado", "nunca", "so-estado"], null, null, null, null))
            .ToDictionary(f => f.ToolId);

        Assert.Equal(("Armazém · posição 1033", FerramentaEstadoTipo.Reparado, (string?)null),
            (factos["na-posicao"].OndeEsta, factos["na-posicao"].Estado, factos["na-posicao"].AvisoArmazem));
        Assert.Equal(("A reparar · Moldes Lda", FerramentaEstadoTipo.PorReparar, (string?)null),
            (factos["a-reparar"].OndeEsta, factos["a-reparar"].Estado, factos["a-reparar"].AvisoArmazem));
        Assert.Equal(("Fabrico", FerramentaEstadoTipo.Novo, (string?)null),
            (factos["no-fabrico"].OndeEsta, factos["no-fabrico"].Estado, factos["no-fabrico"].AvisoArmazem));
        Assert.Equal(("Fabrico", (FerramentaEstadoTipo?)null, "CM 6012A118 / lote 2 não tem estado registado."),
            (factos["sem-estado"].OndeEsta, factos["sem-estado"].Estado, factos["sem-estado"].AvisoArmazem));
        Assert.Equal(((string?)null, (FerramentaEstadoTipo?)null, "MF 6012A118 / lote 2 não está registada no Armazém e não tem estado."),
            (factos["nunca"].OndeEsta, factos["nunca"].Estado, factos["nunca"].AvisoArmazem));
        Assert.Equal(((string?)null, FerramentaEstadoTipo.Reparado, "BQ A118 / lote 07 não está registada no Armazém."),
            (factos["so-estado"].OndeEsta, factos["so-estado"].Estado, factos["so-estado"].AvisoArmazem));
    }

    [Fact]
    public async Task OUsoEAUltimaLeitura_ESemLeituraFicaVazio()
    {
        // Diogo, 21/10/2026: «no Job On também mostrar o % uso».
        using var db = CreateContext();
        db.FerramentaUtilizacoes.AddRange(
            new FerramentaUtilizacao { ToolId = "na-posicao", Percentagem = 30m, RegistadaEmUtc = DateTime.UtcNow.AddDays(-5) },
            new FerramentaUtilizacao { ToolId = "na-posicao", Percentagem = 33.5m, RegistadaEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var factos = (await new JobOnToolFactsQuery(db).FindAsync(["na-posicao", "nunca"], null, null, null, null))
            .ToDictionary(f => f.ToolId);

        Assert.Equal(33.5m, factos["na-posicao"].Utilizacao);
        Assert.Null(factos["nunca"].Utilizacao);
    }
}

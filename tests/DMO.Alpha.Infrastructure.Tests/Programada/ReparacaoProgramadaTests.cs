using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Programada;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Armazem;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Programada;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Programada;

/// <summary>
/// A Reparação Programada (Diogo, 22/10/2026): associar CM e MF a uma lista
/// de um reparador; a Saída do Armazém marca «Saída», a Entrada marca a
/// entrada (dia, posição, quem) e, na última, a lista fecha; a Entrada de uma
/// ferramenta numa lista ativa propõe «Reparado» e, se a pessoa mudar, grava
/// e avisa.
/// </summary>
public sealed class ReparacaoProgramadaTests
{
    private static DmoDbContext CreateContext()
    {
        var db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase("dmo-programada-" + Guid.NewGuid().ToString("N"))
            .Options);
        db.Tools.AddRange(
            new Tool { ToolId = ToolRegistryConstants.RemovedToolId, Type = ToolType.Cm, Reference = "REMOVED", Lot = "REMOVED" },
            new Tool { ToolId = "cm-5447-3", Type = ToolType.Cm, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "mf-5447-3", Type = ToolType.Mf, Reference = "5447", Lot = "3" },
            new Tool { ToolId = "cm-5809-1", Type = ToolType.Cm, Reference = "5809", Lot = "1" },
            new Tool { ToolId = "bq-t173-1", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        db.Machines.Add(new DMO.Alpha.Core.Production.Machine { Id = 1, Code = "B1" });
        db.ToolMachineLinks.Add(new ToolMachineLink { ToolId = "cm-5447-3", MachineId = 1 });
        db.FerramentaUtilizacoes.Add(new FerramentaUtilizacao { ToolId = "cm-5447-3", Percentagem = 24m, Origem = "sap", RegistadaEmUtc = DateTime.UtcNow });
        db.ArmazemPosicoes.AddRange(
            new ArmazemPosicao { Id = 1, Codigo = "1033", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Id = 2, Codigo = "4152", CriadaEmUtc = DateTime.UtcNow },
            new ArmazemPosicao { Id = 3, Codigo = "2011", CriadaEmUtc = DateTime.UtcNow });
        db.Reparadores.AddRange(
            new Reparador { Id = 1, Nome = "Moldin" },
            new Reparador { Id = 2, Nome = "Antigo", Ativo = false });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private static ReparacaoProgramadaServico Programada(DmoDbContext db) => new(db, new Sessao());

    private static ArmazemMovimentos Armazem(DmoDbContext db) => new(db, new OperacoesRegistadas(db), new Sessao());

    private static async Task EntrarAsync(DmoDbContext db, string toolId, string posicao, FerramentaEstadoTipo estado = FerramentaEstadoTipo.PorReparar)
    {
        var saida = await Armazem(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), toolId, posicao, ArmazemOrigem.JaEstava, estado, null, null));
        Assert.True(saida.Gravou, saida.Resultado.ToString());
        db.ChangeTracker.Clear();
    }

    private static async Task SairAsync(DmoDbContext db, string toolId)
    {
        var saida = await Armazem(db).SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), toolId, ArmazemDestino.Reparacao, 1, null, null, null, null));
        Assert.True(saida.Gravou, saida.Resultado.ToString());
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Procurar_SemFiltros_AbreVazia_EComFiltros_MostraCmEMfComAPosicao()
    {
        using var db = CreateContext();
        await EntrarAsync(db, "cm-5447-3", "1033");

        Assert.Empty(await Programada(db).ProcurarAsync(new ProgramadaProcura()));
        var porReparar = await Programada(db).ProcurarAsync(new ProgramadaProcura(Estado: FerramentaEstadoTipo.PorReparar));
        var cm = Assert.Single(porReparar);
        Assert.Equal(("cm-5447-3", "1033", 24m, "B1"), (cm.ToolId, cm.Posicao, cm.Utilizacao, cm.Linhas));
        var todas = await Programada(db).ProcurarAsync(new ProgramadaProcura(Referencia: "5447"));
        Assert.Equal(["cm-5447-3", "mf-5447-3"], todas.Select(c => c.ToolId).Order());
        Assert.Empty(await Programada(db).ProcurarAsync(new ProgramadaProcura(Referencia: "T173")));
        Assert.Single(await Programada(db).ProcurarAsync(new ProgramadaProcura(Linha: "B1")));
    }

    /// <summary>
    /// Garante (DMO-579): a Referência escrita encontra como no Pesquisar das
    /// Ferramentas: a referência toda (5447T173) encontra a CM/MF registada só
    /// com o modelo (5447), e o nome técnico também encontra.
    /// </summary>
    [Fact]
    public async Task Procurar_PelaReferenciaToda_OuPeloNomeTecnico_EncontraACmEOMf()
    {
        using var db = CreateContext();
        var cm = db.Tools.Single(t => t.ToolId == "cm-5809-1");
        cm.TechnicalName = "M580995MPNNX";
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var toda = await Programada(db).ProcurarAsync(new ProgramadaProcura(Referencia: "5447t173"));
        Assert.Equal(["cm-5447-3", "mf-5447-3"], toda.Select(c => c.ToolId).Order());
        var tecnico = await Programada(db).ProcurarAsync(new ProgramadaProcura(Referencia: "M5809"));
        Assert.Equal("cm-5809-1", Assert.Single(tecnico).ToolId);
        Assert.Empty(await Programada(db).ProcurarAsync(new ProgramadaProcura(Referencia: "9999T173")));
    }

    [Fact]
    public async Task Associar_CriaListaDoReparador_ComOQueSeViaDaFerramenta_EAcrescentaAUmaAtiva()
    {
        using var db = CreateContext();

        var nova = await Programada(db).AssociarAsync(["cm-5447-3", "bq-t173-1"], null, 1);
        var mais = await Programada(db).AssociarAsync(["mf-5447-3", "cm-5447-3"], nova.ListaId, null);

        Assert.True(nova.Associou);
        Assert.Equal(1, nova.Associadas);
        Assert.Contains(nova.Avisos, a => a.Contains("BQ", StringComparison.Ordinal));
        Assert.Equal((ProgramadaAssociacao.Associada, nova.ListaId, 1), (mais.Resultado, mais.ListaId, mais.Associadas));
        Assert.Contains(mais.Avisos, a => a.Contains("já estava nesta lista", StringComparison.Ordinal));
        var lista = Assert.Single(await db.ReparacaoProgramadaListas.ToListAsync());
        Assert.Equal(("Moldin", 1, "Rui", (DateTime?)null), (lista.ReparadorNome, lista.ReparadorId, lista.CriadaPor, lista.FechadaEmUtc));
        var item = await db.ReparacaoProgramadaItens.SingleAsync(i => i.ToolId == "cm-5447-3");
        Assert.Equal((ToolType.Cm, "5447", "3", 24m, "B1"), (item.Tipo, item.Referencia, item.Lote, item.Utilizacao, item.Linhas));
        Assert.Equal(2, await db.ReparacaoProgramadaItens.CountAsync());
    }

    [Fact]
    public async Task Associar_RecusaSemReparador_ReparadorArquivado_EListaFechada()
    {
        using var db = CreateContext();

        Assert.Equal(ProgramadaAssociacao.SemFerramentas, (await Programada(db).AssociarAsync([], null, 1)).Resultado);
        Assert.Equal(ProgramadaAssociacao.SemReparador, (await Programada(db).AssociarAsync(["cm-5447-3"], null, null)).Resultado);
        Assert.Equal(ProgramadaAssociacao.ReparadorInvalido, (await Programada(db).AssociarAsync(["cm-5447-3"], null, 2)).Resultado);
        db.ReparacaoProgramadaListas.Add(new ReparacaoProgramadaLista { Id = 9, ReparadorNome = "Moldin", CriadaEmUtc = DateTime.UtcNow, FechadaEmUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.Equal(ProgramadaAssociacao.ListaFechada, (await Programada(db).AssociarAsync(["cm-5447-3"], 9, null)).Resultado);
        Assert.Empty(await db.ReparacaoProgramadaItens.ToListAsync());
    }

    [Fact]
    public async Task SaidaEEntrada_AtualizamALista_EALinhaQueEntrouMostraDiaPosicaoENome()
    {
        using var db = CreateContext();
        await EntrarAsync(db, "cm-5447-3", "1033");
        await EntrarAsync(db, "cm-5809-1", "2011");
        var listaId = (await Programada(db).AssociarAsync(["cm-5447-3", "cm-5809-1"], null, 1)).ListaId!.Value;

        await SairAsync(db, "cm-5447-3");
        var depoisDaSaida = (await Programada(db).ListaAsync(listaId))!.Linhas.Single(l => l.ToolId == "cm-5447-3");
        Assert.True(depoisDaSaida.Saiu);
        Assert.False(depoisDaSaida.Entrou);
        Assert.Equal("A reparar · Moldin", depoisDaSaida.OndeEsta);
        Assert.Equal(FerramentaEstadoTipo.PorReparar, depoisDaSaida.Estado);

        var entradaReparado = await Armazem(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), "cm-5447-3", "4152", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.Reparado, null, null));
        Assert.True(entradaReparado.Gravou);
        Assert.DoesNotContain(entradaReparado.Avisos, a => a.Contains("Reparação Programada", StringComparison.Ordinal));
        db.ChangeTracker.Clear();
        var detalhe = (await Programada(db).ListaAsync(listaId))!;
        var entrou = detalhe.Linhas.Single(l => l.ToolId == "cm-5447-3");
        Assert.True(entrou.Entrou);
        Assert.Equal(("4152", "Rui", FerramentaEstadoTipo.Reparado), (entrou.EntradaPosicao, entrou.EntradaPor, entrou.Estado));
        Assert.Equal("Armazém · posição 4152", entrou.OndeEsta);
        Assert.True(detalhe.Lista.Ativa);
        Assert.False(detalhe.Linhas.Single(l => l.ToolId == "cm-5809-1").Saiu);
    }

    [Fact]
    public async Task Lista_FechaSozinha_QuandoAUltimaFerramentaDaEntrada()
    {
        using var db = CreateContext();
        await EntrarAsync(db, "cm-5447-3", "1033");
        await EntrarAsync(db, "cm-5809-1", "2011");
        var listaId = (await Programada(db).AssociarAsync(["cm-5447-3", "cm-5809-1"], null, 1)).ListaId!.Value;
        await SairAsync(db, "cm-5447-3");
        await SairAsync(db, "cm-5809-1");

        await EntrarAsync(db, "cm-5447-3", "4152", FerramentaEstadoTipo.Reparado);
        Assert.Null((await db.ReparacaoProgramadaListas.SingleAsync()).FechadaEmUtc);
        await EntrarAsync(db, "cm-5809-1", "2011", FerramentaEstadoTipo.Reparado);

        Assert.NotNull((await db.ReparacaoProgramadaListas.SingleAsync()).FechadaEmUtc);
        Assert.Empty(await Programada(db).ListasAsync(false));
        Assert.Equal(listaId, Assert.Single(await Programada(db).ListasAsync(true)).Id);
        // Uma saída depois de fechada já não mexe na lista.
        await SairAsync(db, "cm-5447-3");
        Assert.All(await db.ReparacaoProgramadaItens.ToListAsync(), i => Assert.NotNull(i.EntradaMovimentoId));
    }

    [Fact]
    public async Task Entrada_DeFerramentaNumaListaAtiva_PropoeReparado_EMudarGravaComAviso()
    {
        using var db = CreateContext();
        await EntrarAsync(db, "cm-5447-3", "1033");
        await Programada(db).AssociarAsync(["cm-5447-3"], null, 1);
        // Sai para a Produção (não para a Reparação): mesmo assim a lista propõe «Reparado».
        var saida = await Armazem(db).SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), "cm-5447-3", ArmazemDestino.Producao, null, null, null, null, null));
        Assert.True(saida.Gravou);
        db.ChangeTracker.Clear();

        var situacao = await Armazem(db).SituacaoAsync("cm-5447-3");
        Assert.Equal(("Moldin", FerramentaEstadoTipo.Reparado), (situacao!.ListaProgramada, situacao.EstadoProposto));

        var entrada = await Armazem(db).EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), "cm-5447-3", "4152", ArmazemOrigem.Producao, FerramentaEstadoTipo.PorReparar, null, null));
        Assert.Equal(ArmazemMovimentoResultado.Gravado, entrada.Resultado);
        Assert.Contains(entrada.Avisos, a => a.Contains("Reparação Programada de Moldin", StringComparison.Ordinal));
        Assert.NotNull((await db.ReparacaoProgramadaListas.SingleAsync()).FechadaEmUtc);
    }

    [Fact]
    public async Task Entrada_ForaDeQualquerLista_NaoMudaAProposta()
    {
        using var db = CreateContext();
        await EntrarAsync(db, "cm-5447-3", "1033");
        var saida = await Armazem(db).SaidaAsync(new PedidoDeSaida(Guid.NewGuid(), "cm-5447-3", ArmazemDestino.Producao, null, null, null, null, null));
        Assert.True(saida.Gravou);
        db.ChangeTracker.Clear();

        var situacao = await Armazem(db).SituacaoAsync("cm-5447-3");

        Assert.Null(situacao!.ListaProgramada);
        Assert.Equal(FerramentaEstadoTipo.PorReparar, situacao.EstadoProposto);
    }

    private sealed class Sessao : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = Guid.NewGuid(), DisplayName = "Rui", OperatorId = "2165" };
    }
}

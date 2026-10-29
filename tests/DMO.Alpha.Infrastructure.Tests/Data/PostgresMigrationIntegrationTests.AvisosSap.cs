using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("2147483648", null)]
    [InlineData("-1", null)]
    [InlineData("2,5", null)]
    [InlineData("98", 98)]
    public void Zppg_LeAPrimeiraQuantidadeOFornecedorEADataPurchase(string quantidade, int? esperado)
    {
        var html = $"<table><tr><th>Equipamento</th><th>% Use</th><th>No. of pieces</th><th>Supplier</th><th>Purchase</th><th>No. of pieces</th></tr><tr><td>TEST07</td><td>69,6</td><td>{quantidade}</td><td>Fornecedor &amp; Filhos</td><td>19.12.2025</td><td>100</td></tr></table>";
        var leitura = UsoSapExportacao.Ler(html);
        Assert.Single(leitura.Linhas);
        var linha = Assert.Single(leitura.Informacao);
        Assert.Equal(esperado, linha.Pecas);
        Assert.Equal("Fornecedor & Filhos", linha.Fornecedor);
        Assert.Equal(new DateOnly(2025, 12, 19), linha.DataAquisicao);
        var invalida = UsoSapExportacao.Ler("equipment,percent_use,supplier\nTEST07,69.6,A\0B");
        Assert.Null(Assert.Single(invalida.Informacao).Fornecedor);
    }

    [SkippableTheory]
    [InlineData(ToolType.Bq)]
    [InlineData(ToolType.Cm)]
    [InlineData(ToolType.Mf)]
    public async Task Sap_SimGravaNaQuantidadeDoTipoCertoESoNoToolIdEscolhido(ToolType tipo)
    {
        SkipWithoutConnection();
        var id = "S663" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.AddRange(new Tool { ToolId = id, Type = tipo, Reference = "TESTE663", Lot = "7", TechnicalName = id, BqAccountedQuantity = 10, AnQuantity = 20 },
            new Tool { ToolId = id + "OUTRA", Type = tipo, Reference = "TESTE663", Lot = "7", BqAccountedQuantity = 10, AnQuantity = 20 });
        await db.SaveChangesAsync();
        try
        {
            await new FerramentasUsoSap(db, new SessaoUsoSap()).ImportarAsync(CsvAvisos(id, 98, 50), "teste.csv", null);
            await using var decidir = CreateContext();
            var servico = new FerramentasAvisosSap(decidir, new SessaoUsoSap());
            var aviso = (await servico.LerAsync(id))!;
            Assert.Equal(98, aviso.Pecas);
            Assert.Equal(DecisaoSapResultado.Gravado, await servico.DecidirAsync(id, aviso.Versao, "pecas", true));
            await using var leitura = CreateContext();
            var tool = await leitura.Tools.AsNoTracking().SingleAsync(t => t.ToolId == id);
            Assert.Equal(98, tipo == ToolType.Bq ? tool.BqAccountedQuantity : tool.AnQuantity);
            Assert.Equal(tipo == ToolType.Bq ? 20 : 10, tipo == ToolType.Bq ? tool.AnQuantity : tool.BqAccountedQuantity);
            var outra = await leitura.Tools.AsNoTracking().SingleAsync(t => t.ToolId == id + "OUTRA");
            Assert.Equal((10, 20), (outra.BqAccountedQuantity, outra.AnQuantity));
            Assert.Equal("Fornecedor de teste", tool.SapFornecedor);
            Assert.Equal(new DateOnly(2025, 12, 19), tool.SapDataAquisicao);
        }
        finally { await LimparAvisosAsync(id); }
    }

    [SkippableFact]
    public async Task Sap_NaoMantemQuantidadeEUsoManual_NaoRepeteMesmoValorEMostraNovoValor()
    {
        SkipWithoutConnection();
        var id = "S663" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.Add(new Tool { ToolId = id, Type = ToolType.Bq, Reference = "TESTE663", Lot = "7", TechnicalName = id, BqAccountedQuantity = 10 });
        await db.SaveChangesAsync();
        try
        {
            var importar = new FerramentasUsoSap(db, new SessaoUsoSap());
            await importar.RegistarPercentagemManualAsync(id, 15);
            await importar.ImportarAsync(CsvAvisos(id, 98, 50), "teste.csv", null);
            db.ChangeTracker.Clear();
            var servico = new FerramentasAvisosSap(db, new SessaoUsoSap());
            var aviso = (await servico.LerAsync(id))!;
            Assert.Equal((98, 50m), (aviso.Pecas, aviso.Uso));
            Assert.Equal(15m, (await importar.DaFerramentaAsync(id)).Ultima!.Percentagem);
            Assert.Equal(DecisaoSapResultado.Gravado, await servico.DecidirAsync(id, aviso.Versao, "pecas", false));
            aviso = (await servico.LerAsync(id))!;
            Assert.Equal(DecisaoSapResultado.Gravado, await servico.DecidirAsync(id, aviso.Versao, "uso", false));
            db.ChangeTracker.Clear();
            await importar.ImportarAsync(CsvAvisos(id, 98, 50), "teste.csv", null);
            await using var leitura = CreateContext();
            var apos = (await new FerramentasAvisosSap(leitura, new SessaoUsoSap()).LerAsync(id))!;
            Assert.Null(apos.Pecas);
            Assert.Null(apos.Uso);
            Assert.Equal(10, apos.QuantidadeAtual);
            Assert.Equal(15m, apos.UsoAtual);
            await importar.ImportarAsync(CsvAvisos(id, 99, 51), "novo.csv", null);
            await using var nova = CreateContext();
            var novos = (await new FerramentasAvisosSap(nova, new SessaoUsoSap()).LerAsync(id))!;
            Assert.Equal((99, 51m), (novos.Pecas, novos.Uso));
        }
        finally { await LimparAvisosAsync(id); }
    }

    [SkippableFact]
    public async Task Sap_SimUsaOPercentDoServidorEDepoisVoltaAAplicarAutomaticamente()
    {
        SkipWithoutConnection();
        var id = "S663" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.Add(new Tool { ToolId = id, Type = ToolType.Cm, Reference = "TESTE663", Lot = "7", TechnicalName = id });
        await db.SaveChangesAsync();
        try
        {
            var uso = new FerramentasUsoSap(db, new SessaoUsoSap());
            await uso.ImportarAsync(CsvAvisos(id, 5, 10), "1.csv", null);
            Assert.Equal(10m, (await uso.DaFerramentaAsync(id)).Ultima!.Percentagem);
            await uso.RegistarPercentagemManualAsync(id, 15);
            await uso.ImportarAsync(CsvAvisos(id, 5, 20), "2.csv", null);
            var servico = new FerramentasAvisosSap(db, new SessaoUsoSap());
            var aviso = (await servico.LerAsync(id))!;
            Assert.Equal(20m, aviso.Uso);
            Assert.Equal(DecisaoSapResultado.Gravado, await servico.DecidirAsync(id, aviso.Versao, "uso", true));
            db.ChangeTracker.Clear();
            await uso.ImportarAsync(CsvAvisos(id, 5, 30), "3.csv", null);
            await using var leitura = CreateContext();
            Assert.Equal(30m, (await new FerramentasUsoSap(leitura, new SessaoUsoSap()).DaFerramentaAsync(id)).Ultima!.Percentagem);
            Assert.Null((await new FerramentasAvisosSap(leitura, new SessaoUsoSap()).LerAsync(id))!.Uso);
        }
        finally { await LimparAvisosAsync(id); }
    }

    [SkippableFact]
    public async Task Sap_UmaDecisaoAntigaNaoSubstituiUmaImportacaoMaisRecente()
    {
        SkipWithoutConnection();
        var id = "S663" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.Add(new Tool { ToolId = id, Type = ToolType.Bq, Reference = "TESTE663", Lot = "7", TechnicalName = id, BqAccountedQuantity = 10 });
        await db.SaveChangesAsync();
        try
        {
            var uso = new FerramentasUsoSap(db, new SessaoUsoSap());
            await uso.ImportarAsync(CsvAvisos(id, 98, 50), "1.csv", null);
            var primeiro = (await new FerramentasAvisosSap(db, new SessaoUsoSap()).LerAsync(id))!;
            await uso.ImportarAsync(CsvAvisos(id, 99, 50), "2.csv", null);
            await using var decidir = CreateContext();
            Assert.Equal(DecisaoSapResultado.Desatualizado, await new FerramentasAvisosSap(decidir, new SessaoUsoSap()).DecidirAsync(id, primeiro.Versao, "pecas", true));
            await using var leitura = CreateContext();
            Assert.Equal(10, (await leitura.Tools.AsNoTracking().SingleAsync(t => t.ToolId == id)).BqAccountedQuantity);
        }
        finally { await LimparAvisosAsync(id); }
    }

    private static string CsvAvisos(string id, int pecas, int uso) =>
        $"equipment,percent_use,pieces,supplier,purchase\n{id}07,{uso},{pecas},Fornecedor de teste,19.12.2025";

    [Fact]
    public void Sap_AMigracaoSoAcrescentaColunasOpcionais()
    {
        var operacoes = new DMO.Alpha.Infrastructure.Migrations.AvisosSapPecasFornecedorData().UpOperations;
        Assert.NotEmpty(operacoes);
        Assert.All(operacoes, op =>
        {
            var coluna = Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation>(op);
            Assert.Equal("Tools", coluna.Table);
            Assert.True(coluna.IsNullable);
        });
    }

    [SkippableFact]
    public async Task Sap_DuasDecisoesDaMesmaFicha_SoUmaGrava()
    {
        SkipWithoutConnection();
        var id = "S663" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.Add(new Tool { ToolId = id, Type = ToolType.Bq, Reference = "TESTE663", Lot = "7", BqAccountedQuantity = 10, SapPecas = 98 });
        await db.SaveChangesAsync();
        try
        {
            var original = (await new FerramentasAvisosSap(db, new SessaoUsoSap()).LerAsync(id))!;
            async Task<DecisaoSapResultado> Decidir(bool sim)
            {
                await using var contexto = CreateContext();
                return await new FerramentasAvisosSap(contexto, new SessaoUsoSap()).DecidirAsync(id, original.Versao, "pecas", sim);
            }
            var resultados = await Task.WhenAll(Decidir(true), Decidir(false));
            Assert.Single(resultados, r => r == DecisaoSapResultado.Gravado);
            Assert.Single(resultados, r => r == DecisaoSapResultado.Desatualizado);
            await using var leitura = CreateContext();
            var tool = await leitura.Tools.AsNoTracking().SingleAsync(t => t.ToolId == id);
            Assert.Equal(resultados[0] == DecisaoSapResultado.Gravado ? 98 : 10, tool.BqAccountedQuantity);
        }
        finally { await LimparAvisosAsync(id); }
    }

    private async Task LimparAvisosAsync(string id)
    {
        await using var db = CreateContext();
        await db.FerramentaLembretesUtilizacao.Where(t => t.ToolId == id).ExecuteDeleteAsync();
        await db.FerramentaUtilizacoes.Where(t => t.ToolId == id).ExecuteDeleteAsync();
        await db.Tools.Where(t => t.ToolId == id || t.ToolId == id + "OUTRA").ExecuteDeleteAsync();
    }

    [SkippableFact]
    public async Task Sap_EditarOutrosFactosDaCmNaoApagaAQuantidadeAceite()
    {
        SkipWithoutConnection();
        var id = "S663" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.Add(new Tool { ToolId = id, Type = ToolType.Cm, Reference = id, Lot = "7", AnQuantity = 98, SapPecas = 98 });
        await db.SaveChangesAsync();
        try
        {
            await new EditToolFactsHandler(db).HandleAsync(new EditToolFacts(id, "CM", id, "7", FabricaOrigem: "MG"));
            await using var leitura = CreateContext();
            var tool = await leitura.Tools.AsNoTracking().SingleAsync(t => t.ToolId == id);
            Assert.Equal(98, tool.AnQuantity);
            Assert.Equal("MG", tool.FabricaOrigem);
        }
        finally { await LimparAvisosAsync(id); }
    }

    [SkippableFact]
    public async Task Sap_UmValorManualIgualAoSapContinuaProtegidoQuandoOSapMuda()
    {
        SkipWithoutConnection();
        var id = "S663" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Tools.Add(new Tool { ToolId = id, Type = ToolType.Cm, Reference = id, Lot = "7", TechnicalName = id });
        await db.SaveChangesAsync();
        try
        {
            var uso = new FerramentasUsoSap(db, new SessaoUsoSap());
            await uso.RegistarPercentagemManualAsync(id, 15);
            await uso.PedirAsync(id);
            await uso.ImportarAsync(CsvAvisos(id, 5, 15), "igual.csv", null);
            await uso.ImportarAsync(CsvAvisos(id, 5, 20), "diferente.csv", null);
            await using var leitura = CreateContext();
            var aviso = (await new FerramentasAvisosSap(leitura, new SessaoUsoSap()).LerAsync(id))!;
            Assert.Equal(15m, aviso.UsoAtual);
            Assert.Equal(20m, aviso.Uso);
            // A ficha ainda tem a Tool em tracking; outra importação chega
            // antes de o operador escrever um novo valor manual.
            await using (var outra = CreateContext())
                await new FerramentasUsoSap(outra, new SessaoUsoSap()).ImportarAsync(CsvAvisos(id, 5, 25), "outra.csv", null);
            Assert.True(await uso.RegistarPercentagemManualAsync(id, 17));
            await using var verificar = CreateContext();
            var atual = (await new FerramentasAvisosSap(verificar, new SessaoUsoSap()).LerAsync(id))!;
            Assert.Equal(17m, atual.UsoAtual);
            Assert.Equal(25m, atual.Uso);
        }
        finally { await LimparAvisosAsync(id); }
    }
}

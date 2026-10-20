using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// «Nada repetido» (fase de testes, 19/10/2026): os índices únicos que
/// nenhum teste guardava. Para cada um, a base recusa o repetido (23505 e o
/// nome do índice), o primeiro registo fica e o repetido não. Onde a app
/// volta a correr depois da recusa (áreas, visitas, Punções, leituras de
/// Pegamentos), duas gravações ao mesmo tempo dão uma só linha ou posições
/// diferentes.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    /// <summary>A gravação tem de ser recusada pela base no índice único indicado.</summary>
    private static async Task RecusaORepetidoAsync(Func<Task> gravar, string indice)
    {
        var erro = await Record.ExceptionAsync(gravar);
        var recusa = erro as PostgresException ?? erro?.InnerException as PostgresException;
        Assert.True(recusa is not null, $"A base devia recusar o repetido em {indice}, mas gravou ({erro?.GetType().Name ?? "sem erro"}).");
        Assert.Equal("23505", recusa!.SqlState); // unique_violation
        Assert.Equal(indice, recusa.ConstraintName);
    }

    private static async Task<int> JobOnDoControloAsync(int controloId)
    {
        await using var db = CreateContext();
        return await db.Controlos.AsNoTracking().Where(c => c.Id == controloId).Select(c => c.JobOnId).SingleAsync();
    }

    // ---------------------------------------------------------------- produção

    [SkippableTheory]
    [InlineData("BQ", "IX_BqContexts_JobOnId")]
    [InlineData("CM", "IX_CmContexts_JobOnId")]
    [InlineData("MF", "IX_MfContexts_JobOnId")]
    public async Task Producao_SoTemUmaFerramentaAtualDeCadaTipo_ASegundaBqCmOuMfERecusada(string tipo, string indice)
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            var jobOnId = await JobOnDoControloAsync(producoes.Controlo1);
            var original = producoes.Marker + "-" + tipo;
            var outra = producoes.Marker + "-" + tipo + "2";
            await using (var db = CreateContext())
            {
                db.Tools.Add(new Tool
                {
                    ToolId = outra,
                    Type = tipo switch { "BQ" => ToolType.Bq, "CM" => ToolType.Cm, _ => ToolType.Mf },
                    Reference = "1075" + tipo,
                    Lot = "2"
                });
                await db.SaveChangesAsync();
            }

            await using (var db = CreateContext())
            {
                switch (tipo)
                {
                    case "BQ": db.BqContexts.Add(new BqContext { JobOnId = jobOnId, ToolId = outra }); break;
                    case "CM": db.CmContexts.Add(new CmContext { JobOnId = jobOnId, ToolId = outra }); break;
                    default: db.MfContexts.Add(new MfContext { JobOnId = jobOnId, ToolId = outra }); break;
                }

                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), indice);
            }

            await using var check = CreateContext();
            var atuais = tipo switch
            {
                "BQ" => await check.BqContexts.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.ToolId).ToListAsync(),
                "CM" => await check.CmContexts.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.ToolId).ToListAsync(),
                _ => await check.MfContexts.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.ToolId).ToListAsync()
            };
            Assert.Equal([original], atuais);
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task Boquilhas_UmaBqDaProducaoTemUmSoRegistoDeReparacao_OSegundoERecusado()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        var bq = producoes.Marker + "-BQ";
        try
        {
            var jobOnId = await JobOnDoControloAsync(producoes.Controlo1);
            int contextoId;
            int primeiro;
            await using (var db = CreateContext())
            {
                contextoId = await db.BqContexts.AsNoTracking().Where(c => c.JobOnId == jobOnId).Select(c => c.Id).SingleAsync();
                var trace = new BqRepairTrace { ToolId = bq, BqContextId = contextoId };
                db.BqRepairTraces.Add(trace);
                await db.SaveChangesAsync();
                primeiro = trace.Id;
            }

            await using (var db = CreateContext())
            {
                db.BqRepairTraces.Add(new BqRepairTrace { ToolId = bq, BqContextId = contextoId });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_BqRepairTraces_BqContextId");
            }

            await using var check = CreateContext();
            Assert.Equal([primeiro], await check.BqRepairTraces.AsNoTracking()
                .Where(t => t.BqContextId == contextoId).Select(t => t.Id).ToListAsync());
        }
        finally
        {
            await using (var db = CreateContext())
            {
                await db.BqRepairTraces.Where(t => t.ToolId == bq).ExecuteDeleteAsync();
            }

            await DeleteDuasProducoesAsync(producoes);
        }
    }

    // ------------------------------------------------------- estado das áreas

    [SkippableFact]
    public async Task EstadoDoDocumento_CadaAreaTemUmSoEstadoPorControlo_ORepetidoERecusado()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            await using (var db = CreateContext())
            {
                db.ControloAreaStates.Add(new ControloAreaState
                {
                    ControloId = producoes.Controlo1, Area = "CM", Status = "OK", UpdatedBy = "4301", UpdatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            await using (var db = CreateContext())
            {
                db.ControloAreaStates.Add(new ControloAreaState
                {
                    ControloId = producoes.Controlo1, Area = "CM", Status = "NOK", UpdatedBy = "4302", UpdatedAtUtc = DateTime.UtcNow
                });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_ControloAreaStates_ControloId_Area");
            }

            await using var check = CreateContext();
            Assert.Equal(["OK"], await check.ControloAreaStates.AsNoTracking()
                .Where(s => s.ControloId == producoes.Controlo1 && s.Area == "CM").Select(s => s.Status).ToListAsync());
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task EstadoDoDocumento_DoisPostosGravamAMesmaAreaAoMesmoTempo_FicaUmaLinhaComOUltimo()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            // O outro posto grava o OK da área CM depois de este ler que a
            // área ainda não tem linha e antes de ele gravar o NOK.
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await other.Database.ExecuteSqlAsync($"""
                    INSERT INTO "ControloAreaStates" ("ControloId", "Area", "Status", "UpdatedBy", "UpdatedAtUtc")
                    VALUES ({producoes.Controlo1}, 'CM', 'OK', '4302', now())
                    """);
            }));

            Assert.True(await new ControloAreaStateHandler(db).SetAsync(producoes.Controlo1, "CM", "NOK", null, "4301"));

            await using var check = CreateContext();
            Assert.Equal(["NOK"], await check.ControloAreaStates.AsNoTracking()
                .Where(s => s.ControloId == producoes.Controlo1 && s.Area == "CM").Select(s => s.Status).ToListAsync());
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    // ------------------------------------------------------ registo de visão

    [SkippableFact]
    public async Task RegistoDeVisao_UmaLinhaPorOperadorEControlo_ARepetidaERecusada()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            var primeira = DateTime.UtcNow.AddMinutes(-5);
            await using (var db = CreateContext())
            {
                db.ControloViewings.Add(new ControloViewing { ControloId = producoes.Controlo1, OperatorId = "4301", ViewedAtUtc = primeira });
                await db.SaveChangesAsync();
            }

            await using (var db = CreateContext())
            {
                db.ControloViewings.Add(new ControloViewing { ControloId = producoes.Controlo1, OperatorId = "4301", ViewedAtUtc = DateTime.UtcNow });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_ControloViewings_ControloId_OperatorId");
            }

            await using var check = CreateContext();
            var visitas = await check.ControloViewings.AsNoTracking()
                .Where(v => v.ControloId == producoes.Controlo1).Select(v => v.ViewedAtUtc).ToListAsync();
            var visita = Assert.Single(visitas);
            Assert.True(Math.Abs((visita - primeira).TotalSeconds) < 1, $"Ficou a visita de {visita:O}, não a primeira ({primeira:O}).");
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task RegistoDeVisao_DuasAbasAbremAFolhaAoMesmoTempo_FicaUmaSoVisitaDoOperador()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        try
        {
            // A outra aba grava a visita depois de esta ler que ainda não há
            // e antes de ela gravar a sua.
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await other.Database.ExecuteSqlAsync($"""
                    INSERT INTO "ControloViewings" ("ControloId", "OperatorId", "ViewedAtUtc")
                    VALUES ({producoes.Controlo1}, '4301', now() - interval '1 minute')
                    """);
            }));

            var visitas = await new ControloViewingHandler(db).MarkAndGetViewingsAsync(producoes.Controlo1, "4301");

            Assert.Single(visitas);
            await using var check = CreateContext();
            Assert.Equal(1, await check.ControloViewings.AsNoTracking()
                .CountAsync(v => v.ControloId == producoes.Controlo1 && v.OperatorId == "4301"));
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    // ---------------------------------------------------------------- Punções

    [SkippableFact]
    public async Task Puncoes_UmSoRegistoPorProducao_OSegundoERecusado()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync(CmProcess.Nnpb);
        try
        {
            int primeiro;
            await using (var db = CreateContext())
            {
                var registo = new Puncoes { ControloId = producoes.Controlo1, PunchReference = "1075", Lot = "1", UpdatedAtUtc = DateTime.UtcNow };
                db.Puncoes.Add(registo);
                await db.SaveChangesAsync();
                primeiro = registo.Id;
            }

            await using (var db = CreateContext())
            {
                db.Puncoes.Add(new Puncoes { ControloId = producoes.Controlo1, PunchReference = "1075", Lot = "2", UpdatedAtUtc = DateTime.UtcNow });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_Puncoes_ControloId");
            }

            await using var check = CreateContext();
            Assert.Equal([primeiro], await check.Puncoes.AsNoTracking()
                .Where(p => p.ControloId == producoes.Controlo1).Select(p => p.Id).ToListAsync());
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task Puncoes_DoisPostosComecamORegistoDaProducaoAoMesmoTempo_FicaUmSoRegisto()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync(CmProcess.Nnpb);
        try
        {
            // O outro posto cria o registo da produção depois de este ver que
            // ainda não há e antes de ele gravar o seu.
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await other.Database.ExecuteSqlAsync($"""
                    INSERT INTO "Puncoes" ("ControloId", "PunchReference", "Lot", "UpdatedBy", "UpdatedAtUtc")
                    VALUES ({producoes.Controlo1}, '1075', '1', '4302', now())
                    """);
            }));

            var resultado = await new PuncoesHandler(db).SaveIdentificationAsync(
                PuncoesTarget.ForControlo(producoes.Controlo1), "1075", "2", "4301");

            Assert.Equal(PuncoesResult.Success, resultado);
            await using var check = CreateContext();
            Assert.Equal(["2"], await check.Puncoes.AsNoTracking()
                .Where(p => p.ControloId == producoes.Controlo1).Select(p => p.Lot).ToListAsync());
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task Puncoes_CadaPosicaoDoRegistoTemUmaSoPuncao_ARepetidaERecusada()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync(CmProcess.Nnpb);
        try
        {
            int puncoesId;
            await using (var db = CreateContext())
            {
                var registo = new Puncoes { ControloId = producoes.Controlo1, PunchReference = "1075", Lot = "1", UpdatedAtUtc = DateTime.UtcNow };
                registo.Inspecoes.Add(NovaPuncao(1, "3", "4302"));
                db.Puncoes.Add(registo);
                await db.SaveChangesAsync();
                puncoesId = registo.Id;
            }

            await using (var db = CreateContext())
            {
                var repetida = NovaPuncao(1, "7", "4301");
                repetida.PuncoesId = puncoesId;
                db.PuncaoInspecoes.Add(repetida);
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_PuncaoInspecoes_PuncoesId_Position");
            }

            await using var check = CreateContext();
            Assert.Equal(["3"], await check.PuncaoInspecoes.AsNoTracking()
                .Where(i => i.PuncoesId == puncoesId).Select(i => i.Number).ToListAsync());
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    [SkippableFact]
    public async Task Puncoes_DuasPuncoesRegistadasAoMesmoTempo_FicamEmPosicoesDiferentes()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync(CmProcess.Nnpb);
        try
        {
            int puncoesId;
            await using (var db = CreateContext())
            {
                var registo = new Puncoes { ControloId = producoes.Controlo1, PunchReference = "1075", Lot = "1", UpdatedAtUtc = DateTime.UtcNow };
                db.Puncoes.Add(registo);
                await db.SaveChangesAsync();
                puncoesId = registo.Id;
            }

            // O outro posto regista a punção 3 na posição 1 depois de este ler
            // as posições e antes de ele gravar a punção 7.
            await using var mine = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await other.Database.ExecuteSqlAsync($"""
                    INSERT INTO "PuncaoInspecoes" ("PuncoesId", "Position", "Number", "Lot", "PunchSeen", "CoolerSeen", "RegisteredBy", "RegisteredAtUtc")
                    VALUES ({puncoesId}, 1, '3', '1', true, true, '4302', now())
                    """);
            }));

            var resultado = await new PuncoesHandler(mine).AddAsync(
                PuncoesTarget.ForControlo(producoes.Controlo1), "7", "1075", "1", "4301");

            Assert.Equal(PuncoesResult.Success, resultado);
            await using var check = CreateContext();
            var puncoes = await check.PuncaoInspecoes.AsNoTracking()
                .Where(i => i.PuncoesId == puncoesId)
                .OrderBy(i => i.Position)
                .Select(i => i.Position + ":" + i.Number)
                .ToListAsync();
            Assert.Equal(["1:3", "2:7"], puncoes);
        }
        finally
        {
            await DeleteDuasProducoesAsync(producoes);
        }
    }

    private static PuncaoInspecao NovaPuncao(int posicao, string numero, string por) => new()
    {
        Position = posicao,
        Number = numero,
        Lot = "1",
        PunchSeen = true,
        CoolerSeen = true,
        RegisteredBy = por,
        RegisteredAtUtc = DateTime.UtcNow
    };

    // -------------------------------------------------------------- Pegamentos

    [SkippableFact]
    public async Task Pegamentos_CadaPosicaoDaFolhaTemUmaSoLeitura_ARepetidaERecusada()
    {
        SkipWithoutConnection();
        var folha = await SeedPegamentosFolhaAsync(pelaProducao: false);
        try
        {
            await using (var db = CreateContext())
            {
                db.PegamentosMeasurements.Add(new PegamentosMeasurement
                {
                    PegamentosId = folha.PegamentosId, RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 70.10m
                });
                await db.SaveChangesAsync();
            }

            await using (var db = CreateContext())
            {
                db.PegamentosMeasurements.Add(new PegamentosMeasurement
                {
                    PegamentosId = folha.PegamentosId, RowPosition = 1, Component = PegamentosComponent.Cm, CosturaMm = 70.30m
                });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_PegamentosMeasurements_PegamentosId_RowPosition");
            }

            await using var check = CreateContext();
            Assert.Equal([70.10m], await check.PegamentosMeasurements.AsNoTracking()
                .Where(m => m.PegamentosId == folha.PegamentosId).Select(m => m.CosturaMm).ToListAsync());
        }
        finally
        {
            await DeletePegamentosFolhaAsync(folha);
        }
    }

    [SkippableFact]
    public async Task Pegamentos_DuasLeiturasGravadasAoMesmoTempo_FicamEmPosicoesDiferentes()
    {
        SkipWithoutConnection();
        var folha = await SeedPegamentosFolhaAsync(pelaProducao: false);
        try
        {
            // O outro posto grava uma leitura na posição 1 depois de este ler a
            // posição e antes de ele gravar a sua.
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await other.Database.ExecuteSqlAsync($"""
                    INSERT INTO "PegamentosMeasurements" ("PegamentosId", "RowPosition", "Component", "CosturaMm")
                    VALUES ({folha.PegamentosId}, 1, 'CM', 70.10)
                    """);
            }));

            var leitura = await new RecordPegamentosMeasurementHandler(db, new PegamentosToleranceConfiguration(db)).HandleAsync(
                new RecordPegamentosMeasurement(0, "CM", 70.30m, PegamentosId: folha.PegamentosId));

            Assert.Equal(RecordPegamentosMeasurementResult.Success, leitura.Result);
            Assert.Equal(2, leitura.Measurement!.RowPosition);
            Assert.Equal(new[] { 1, 2 }, await PosicoesAsync(folha.PegamentosId));
        }
        finally
        {
            await DeletePegamentosFolhaAsync(folha);
        }
    }

    [SkippableFact]
    public async Task Pegamentos_ToleranciaGravadaPelaPrimeiraVezEmDoisPostosAoMesmoTempo_FicaUmaSoLinha()
    {
        SkipWithoutConnection();
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        // A tolerância é uma linha única; o teste parte da tabela vazia e
        // repõe no fim o que lá estava.
        var antes = await setup.PegamentosTolerances.AsNoTracking().OrderBy(t => t.Id).Select(t => t.ToleranceMm).ToListAsync();
        await setup.PegamentosTolerances.ExecuteDeleteAsync();
        try
        {
            await using var db = CreateContextWith(new BeforeFirstSave(async () =>
            {
                await using var other = CreateContext();
                await new SavePegamentosToleranceHandler(other).SaveAsync(0.25m);
            }));

            await RecusaORepetidoAsync(
                () => new SavePegamentosToleranceHandler(db).SaveAsync(0.30m),
                "IX_PegamentosTolerances_SingletonDiscriminator");

            await using var check = CreateContext();
            Assert.Equal([0.25m], await check.PegamentosTolerances.AsNoTracking().Select(t => t.ToleranceMm).ToListAsync());
        }
        finally
        {
            await setup.PegamentosTolerances.ExecuteDeleteAsync();
            foreach (var tolerancia in antes)
            {
                await setup.Database.ExecuteSqlAsync($"INSERT INTO \"PegamentosTolerances\" (\"ToleranceMm\") VALUES ({tolerancia})");
            }
        }
    }

    // -------------------------------------------------------------------- Peso

    [SkippableFact]
    public async Task PesoComparacao_CadaPosicaoTemUmaSoLeitura_ARepetidaERecusada()
    {
        SkipWithoutConnection();
        var producoes = await SeedDuasProducoesAsync();
        var cm = producoes.Marker + "-CM";
        try
        {
            int comparacaoId;
            await using (var db = CreateContext())
            {
                var peso = new Peso
                {
                    CmToolId = cm,
                    BqToolId = producoes.Marker + "-BQ",
                    Process = "NNPB",
                    WaterTemperatureC = 20,
                    VolumeMarisaCm3 = 10m,
                    VolumePuncaoCm3 = 30m,
                    GlassDensityGramsPerCm3 = 2.50m
                };
                db.Pesos.Add(peso);
                var comparacao = new PesoComparacao
                {
                    ControloId = producoes.Controlo1,
                    PesoInicial = peso,
                    CmToolId = cm,
                    WaterTemperatureC = 20,
                    WaterDensityGramsPerCm3 = 0.998m,
                    GlassDensityGramsPerCm3 = 2.50m,
                    VolumeMarisaCm3 = 10m,
                    VolumePuncaoCm3 = 30m,
                    CreatedAtUtc = DateTime.UtcNow
                };
                comparacao.Measurements.Add(new PesoComparacaoMeasurement { RowPosition = 1, CmSubject = "1", WaterWeightGrams = 199.50m });
                db.PesoComparacoes.Add(comparacao);
                await db.SaveChangesAsync();
                comparacaoId = comparacao.Id;
            }

            await using (var db = CreateContext())
            {
                db.PesoComparacaoMeasurements.Add(new PesoComparacaoMeasurement
                {
                    ComparacaoId = comparacaoId, RowPosition = 1, CmSubject = "2", WaterWeightGrams = 199.64m
                });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_PesoComparacaoMeasurements_ComparacaoId_RowPosition");
            }

            await using var check = CreateContext();
            Assert.Equal([199.50m], await check.PesoComparacaoMeasurements.AsNoTracking()
                .Where(m => m.ComparacaoId == comparacaoId).Select(m => m.WaterWeightGrams).ToListAsync());
        }
        finally
        {
            await using (var db = CreateContext())
            {
                await db.PesoComparacoes.Where(c => c.CmToolId == cm).ExecuteDeleteAsync();
            }

            await DeleteDuasProducoesAsync(producoes);
        }
    }

    // ------------------------------------------------------ Admin e utilizadores

    [SkippableFact]
    public async Task Templates_DuasTemplatesNaoTemAMesmaChave_ARepetidaERecusada()
    {
        SkipWithoutConnection();
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var chave = "nada-repetido-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            setup.Templates.Add(new Template { TemplateKey = chave, Name = "Primeira " + chave });
            await setup.SaveChangesAsync();

            await using (var db = CreateContext())
            {
                db.Templates.Add(new Template { TemplateKey = chave, Name = "Segunda " + chave });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_Templates_TemplateKey");
            }

            await using var check = CreateContext();
            Assert.Equal(["Primeira " + chave], await check.Templates.AsNoTracking()
                .Where(t => t.TemplateKey == chave).Select(t => t.Name).ToListAsync());
        }
        finally
        {
            await setup.Templates.Where(t => t.TemplateKey == chave).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Utilizadores_DoisUtilizadoresNaoTemOMesmoNumeroDeOperador_ORepetidoERecusado()
    {
        SkipWithoutConnection();
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var operador = await OperadorLivreAsync(setup);
        var marker = "nada-repetido-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            setup.Users.Add(new User { Name = "Primeiro", OperatorId = operador, ProviderUserId = marker + "-1" });
            await setup.SaveChangesAsync();

            await using (var db = CreateContext())
            {
                db.Users.Add(new User { Name = "Segundo", OperatorId = operador, ProviderUserId = marker + "-2" });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_Users_OperatorId");
            }

            await using var check = CreateContext();
            Assert.Equal(["Primeiro"], await check.Users.AsNoTracking()
                .Where(u => u.OperatorId == operador).Select(u => u.Name).ToListAsync());
        }
        finally
        {
            await setup.Users.Where(u => u.ProviderUserId.StartsWith(marker)).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Utilizadores_CadaUtilizadorTemUmaSoTemplateAtribuida_ASegundaERecusada()
    {
        SkipWithoutConnection();
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var marker = "nada-repetido-" + Guid.NewGuid().ToString("N")[..8];
        var user = new User { Name = "Operador", OperatorId = await OperadorLivreAsync(setup), ProviderUserId = marker };
        var primeira = new Template { TemplateKey = marker + "-a", Name = marker + " A" };
        var segunda = new Template { TemplateKey = marker + "-b", Name = marker + " B" };
        setup.Users.Add(user);
        setup.Templates.AddRange(primeira, segunda);
        await setup.SaveChangesAsync();
        try
        {
            setup.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = primeira.Id });
            await setup.SaveChangesAsync();

            await using (var db = CreateContext())
            {
                db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = segunda.Id });
                await RecusaORepetidoAsync(() => db.SaveChangesAsync(), "IX_TemplateAssignments_UserId");
            }

            await using var check = CreateContext();
            Assert.Equal([primeira.Id], await check.TemplateAssignments.AsNoTracking()
                .Where(a => a.UserId == user.Id).Select(a => a.TemplateId).ToListAsync());
        }
        finally
        {
            await setup.TemplateAssignments.Where(a => a.UserId == user.Id).ExecuteDeleteAsync();
            await setup.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
            await setup.Templates.Where(t => t.TemplateKey.StartsWith(marker)).ExecuteDeleteAsync();
        }
    }

    /// <summary>Um nº de operador de quatro dígitos que nenhum utilizador da base usa.</summary>
    private static async Task<string> OperadorLivreAsync(DmoDbContext db)
    {
        var usados = (await db.Users.AsNoTracking().Select(u => u.OperatorId).ToListAsync()).ToHashSet();
        return Enumerable.Range(0, 1000).Select(i => (9999 - i).ToString()).First(o => !usados.Contains(o));
    }
}

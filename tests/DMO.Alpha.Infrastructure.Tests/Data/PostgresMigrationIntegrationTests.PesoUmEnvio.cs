using System.Data.Common;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Diogo, 27/10/2026: cada envio de leituras do Peso grava uma só vez,
    // também depois de uma resposta perdida ou de um reinício da app. A
    // leitura e a operação registada ficam as duas ou nenhuma.

    private static readonly Guid PessoaA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid PessoaB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");

    [SkippableFact]
    public async Task PesoLeitura_OMesmoEnvioOutraVez_DepoisDeUmReinicio_NaoGravaDuasVezes()
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            var envio = Guid.NewGuid();
            var leitura = new RecordPesoMeasurement(peso, "1", 199.50m, [new PesoMeasurementRow("2", 199.60m)], envio);

            // A primeira vez grava; a resposta perde-se. A app reinicia: um
            // contexto e um registo de operações novos, nada em memória.
            Assert.Equal(RecordPesoMeasurementResult.Success, (await GravarAsync(leitura, PessoaA)).Result);
            var repetido = await GravarAsync(leitura, PessoaA);

            Assert.Equal(RecordPesoMeasurementResult.AlreadyRecorded, repetido.Result);
            Assert.Equal([(1, "1", 199.50m), (2, "2", 199.60m)], await LeiturasAsync(peso));
            await using var check = CreateContext();
            var operacao = await check.OperacoesRegistadas.AsNoTracking().SingleAsync(o => o.Id == envio);
            Assert.Equal((RecordPesoMeasurementHandler.OperacaoTipo, PessoaA, peso), (operacao.Tipo, operacao.AutorUserId, operacao.ResultadoId));
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }

    [SkippableFact]
    public async Task PesoLeitura_DuasLeiturasIguaisDeEnviosDiferentes_GravamAsDuas()
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            var primeira = await GravarAsync(new RecordPesoMeasurement(peso, "1", 199.50m, OperacaoId: Guid.NewGuid()), PessoaA);
            var segunda = await GravarAsync(new RecordPesoMeasurement(peso, "1", 199.50m, OperacaoId: Guid.NewGuid()), PessoaA);

            Assert.Equal((RecordPesoMeasurementResult.Success, RecordPesoMeasurementResult.Success), (primeira.Result, segunda.Result));
            Assert.Equal([(1, "1", 199.50m), (2, "1", 199.50m)], await LeiturasAsync(peso));
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }

    [SkippableTheory]
    [InlineData("outra pessoa")]
    [InlineData("outra operação")]
    [InlineData("outras leituras")]
    public async Task PesoLeitura_OMesmoIdentificadorNoutraCoisa_ERecusadoENadaFica(string caso)
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            var envio = Guid.NewGuid();
            if (caso == "outra operação")
            {
                await using var db = CreateContext();
                new OperacoesRegistadas(db, new Sessao(PessoaA)).Registar(envio, "armazem.movimento", new ResultadoDaOperacao("ArmazemMovimento", 1));
                await db.SaveChangesAsync();
            }
            else
            {
                Assert.Equal(RecordPesoMeasurementResult.Success,
                    (await GravarAsync(new RecordPesoMeasurement(peso, "1", 199.50m, OperacaoId: envio), PessoaA)).Result);
            }

            var antes = await LeiturasAsync(peso);
            var outro = caso switch
            {
                "outra pessoa" => await GravarAsync(new RecordPesoMeasurement(peso, "1", 199.50m, OperacaoId: envio), PessoaB),
                "outra operação" => await GravarAsync(new RecordPesoMeasurement(peso, "1", 199.50m, OperacaoId: envio), PessoaA),
                _ => await GravarAsync(new RecordPesoMeasurement(peso, "1", 199.70m, OperacaoId: envio), PessoaA)
            };

            Assert.Equal(RecordPesoMeasurementResult.OperationConflict, outro.Result);
            Assert.Equal(antes, await LeiturasAsync(peso));
            await using var check = CreateContext();
            Assert.Equal(1, await check.OperacoesRegistadas.CountAsync(o => o.Id == envio));
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }

    [SkippableFact]
    public async Task PesoLeitura_OMesmoEnvioDuasVezesAoMesmoTempo_FicaUmaSoLeitura()
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            var leitura = new RecordPesoMeasurement(peso, "1", 199.50m, OperacaoId: Guid.NewGuid());

            // O outro pedido com o mesmo envio grava depois de este ver que a
            // operação ainda não existe e antes de ele gravar.
            RecordPesoMeasurementOutcome? outro = null;
            await using var db = CreateContextWith(new BeforeFirstSave(async () => outro = await GravarAsync(leitura, PessoaA)));
            var este = await new RecordPesoMeasurementHandler(db, new OperacoesRegistadas(db, new Sessao(PessoaA))).HandleAsync(leitura);

            Assert.Equal(RecordPesoMeasurementResult.Success, outro!.Result);
            Assert.Equal(RecordPesoMeasurementResult.AlreadyRecorded, este.Result);
            Assert.Equal([(1, "1", 199.50m)], await LeiturasAsync(peso));
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }

    [SkippableFact]
    public async Task PesoLeitura_FalhaAMeioDaGravacao_NaoFicaNemALeituraNemAOperacao_EORepetirGrava()
    {
        SkipWithoutConnection();
        var (peso, marker) = await SeedPesoAsync();
        try
        {
            var envio = Guid.NewGuid();
            var leitura = new RecordPesoMeasurement(peso, "1", 199.50m, [new PesoMeasurementRow("2", 199.60m)], envio);

            // A base já recebeu as linhas e a operação quando a ligação falha.
            await using (var db = CreateContextWith(new FalhaDepoisDaOperacao()))
            {
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    new RecordPesoMeasurementHandler(db, new OperacoesRegistadas(db, new Sessao(PessoaA))).HandleAsync(leitura));
            }

            Assert.Empty(await LeiturasAsync(peso));
            await using (var check = CreateContext())
            {
                Assert.False(await check.OperacoesRegistadas.AnyAsync(o => o.Id == envio));
            }

            Assert.Equal(RecordPesoMeasurementResult.Success, (await GravarAsync(leitura, PessoaA)).Result);
            Assert.Equal([(1, "1", 199.50m), (2, "2", 199.60m)], await LeiturasAsync(peso));
        }
        finally
        {
            await DeletePesoAsync(peso, marker);
        }
    }

    private static async Task<RecordPesoMeasurementOutcome> GravarAsync(RecordPesoMeasurement leitura, Guid pessoa)
    {
        await using var db = CreateContext();
        return await new RecordPesoMeasurementHandler(db, new OperacoesRegistadas(db, new Sessao(pessoa))).HandleAsync(leitura);
    }

    private static async Task<List<(int, string, decimal)>> LeiturasAsync(int peso)
    {
        await using var db = CreateContext();
        return (await db.PesoMeasurements.AsNoTracking()
                .Where(m => m.PesoId == peso)
                .OrderBy(m => m.RowPosition)
                .Select(m => new { m.RowPosition, m.CmSubject, m.WaterWeightGrams })
                .ToListAsync())
            .Select(m => (m.RowPosition, m.CmSubject, m.WaterWeightGrams))
            .ToList();
    }

    private static async Task<(int Peso, string Marker)> SeedPesoAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var marker = "UE-" + Guid.NewGuid().ToString("N")[..8];
        db.Tools.Add(new Tool { ToolId = marker + "-CM", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        db.Tools.Add(new Tool { ToolId = marker + "-BQ", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        var peso = new Peso
        {
            CmToolId = marker + "-CM",
            BqToolId = marker + "-BQ",
            Process = "NNPB",
            WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m,
            VolumePuncaoCm3 = 30m,
            GlassDensityGramsPerCm3 = 2.50m
        };
        db.Pesos.Add(peso);
        await db.SaveChangesAsync();
        return (peso.Id, marker);
    }

    private static async Task DeletePesoAsync(int peso, string marker)
    {
        await using var db = CreateContext();
        await db.OperacoesRegistadas.Where(o => o.Tipo == RecordPesoMeasurementHandler.OperacaoTipo && o.ResultadoId == peso).ExecuteDeleteAsync();
        await db.OperacoesRegistadas.Where(o => o.Tipo == "armazem.movimento" && o.AutorUserId == PessoaA).ExecuteDeleteAsync();
        await db.Database.ExecuteSqlAsync($"DELETE FROM \"Pesos\" WHERE \"Id\" = {peso}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM \"Tools\" WHERE \"ToolId\" LIKE {marker + "-%"}");
    }

    private sealed class Sessao(Guid pessoa) : ICurrentAccountContext
    {
        public ActorContext Current { get; } = new() { IsAuthenticated = true, ActorId = pessoa };
    }

    /// <summary>A ligação falha depois de a base receber a operação, antes do fim da transação.</summary>
    private sealed class FalhaDepoisDaOperacao : DbCommandInterceptor
    {
        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("INSERT INTO \"OperacoesRegistadas\"", StringComparison.Ordinal)
                ? throw new InvalidOperationException("A ligação caiu a meio da gravação.")
                : ValueTask.FromResult(result);

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("INSERT INTO \"OperacoesRegistadas\"", StringComparison.Ordinal)
                ? throw new InvalidOperationException("A ligação caiu a meio da gravação.")
                : ValueTask.FromResult(result);
    }
}

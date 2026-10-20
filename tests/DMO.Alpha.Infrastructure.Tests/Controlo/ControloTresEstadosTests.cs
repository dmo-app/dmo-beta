using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// Os três estados da aprovação fora da Revisão (sweep de 19/10/2026, C-3):
/// o Histórico, o Resumo e os PDFs dizem «Aprovado por X», «Não aprovado por
/// X · nota» ou «Por aprovar», com o nome de quem decidiu, como na Revisão.
/// Antes, um «Não aprovar» aparecia como «Por aprovar» (ou «Sem registo») e a
/// nota só se via na Revisão; e cada PDF mostrava o estado do controlo, não a
/// decisão da sua parte.
/// </summary>
public sealed class ControloTresEstadosTests : IDisposable
{
    private static readonly DateTime RegistadoEm = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private readonly DmoDbContext _db;
    private readonly ControloApprovalHandler _handler;

    public ControloTresEstadosTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _db.Machines.Add(new Machine { Code = "B1" });
        _db.Tools.Add(new Tool { ToolId = "T3", Type = ToolType.Cm, Reference = "5447", Lot = "3" });
        _db.Tools.Add(new Tool { ToolId = "BQ-TOOL-9", Type = ToolType.Bq, Reference = "T194", Lot = "12" });
        _db.Users.Add(new User { Id = Guid.NewGuid(), OperatorId = "4802", Name = "Chefe Aprovação", ProviderUserId = "chefe-4802" });
        _db.Users.Add(new User { Id = Guid.NewGuid(), OperatorId = "4801", Name = "Ana Revisão", ProviderUserId = "ana-4801" });
        _db.SaveChanges();
        _handler = new ControloApprovalHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task Historico_MostraOsTresEstados_ComQuemDecidiuEANota()
    {
        var (aprovado, _) = await NovaProducaoAsync("2026201");
        var (naoAprovado, _) = await NovaProducaoAsync("2026202");
        var (semUtilizador, _) = await NovaProducaoAsync("2026203");
        await NovaProducaoAsync("2026204");
        const string notaLonga = "Repetir os pegamentos da CM e da BQ no turno da tarde, com o paquímetro calibrado, e voltar a pesar as quatro garrafas.";
        await _handler.DecidePartAsync(aprovado, ControloParte.Peso, aprovado: true, "4802", null);
        await _handler.DecidePartAsync(naoAprovado, ControloParte.Peso, aprovado: false, "4801", notaLonga);
        await _handler.DecidePartAsync(semUtilizador, ControloParte.Peso, aprovado: false, "4999", "Falta a BQ.");

        var historico = new ControloHistorySearchQuery(_db);
        async Task<ControloHistorySearchItem> LinhaAsync(string producao, DateOnly? dia = null) =>
            Assert.Single((await historico.SearchPageAsync(null, producao, null, ControloHistoryKind.Resumo, dia, 1, 50)).Items);

        Assert.Equal("Aprovado por Chefe Aprovação", (await LinhaAsync("2026201")).Resumo);
        // A nota longa cabe na linha da lista; inteira, no Resumo e na Revisão.
        var linha = await LinhaAsync("2026202");
        Assert.Equal(
            "Não aprovado por Ana Revisão · Repetir os pegamentos da CM e da BQ no turno da tarde, com o paquímetro calibra…",
            linha.Resumo);
        Assert.Equal(notaLonga, (await _handler.GetAsync(naoAprovado))!.ReviewNote);
        // Sem utilizador com esse nº, o nº gravado.
        Assert.Equal("Não aprovado por 4999 · Falta a BQ.", (await LinhaAsync("2026203")).Resumo);
        var pendente = await LinhaAsync("2026204");
        Assert.Equal("Por aprovar", pendente.Resumo);
        Assert.Null(pendente.DataUtc);

        // A data é a da decisão: o filtro de data encontra o não aprovado nesse
        // dia; o que está por aprovar não tem data.
        var decididoEm = (await _handler.GetAsync(naoAprovado))!.NotApprovedAtUtc!.Value;
        Assert.Equal(decididoEm, linha.DataUtc);
        var dia = DateOnly.FromDateTime(DmoBusinessTime.ToLocal(decididoEm));
        Assert.Equal(naoAprovado, (await LinhaAsync("2026202", dia)).ControloId);
        Assert.Empty((await historico.SearchPageAsync(null, "2026204", null, ControloHistoryKind.Resumo, dia, 1, 50)).Items);

        // Os decididos primeiro, o mais recente à frente; depois os por aprovar.
        var todos = (await historico.SearchPageAsync(null, null, null, ControloHistoryKind.Resumo, null, 1, 50)).Items;
        Assert.Equal(new[] { "2026203", "2026202", "2026201", "2026204" }, todos.Select(i => i.ProductionNumber));
    }

    [Fact]
    public async Task ParteAsync_OPesoMostraASuaDecisao_EOControloEODoPeso()
    {
        var (controloId, _) = await NovaProducaoAsync("2026205", comPegamentos: true);
        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: false, "4801", "Repetir o Peso.");

        var peso = await _handler.ParteAsync(controloId, ControloParte.Peso);
        Assert.True(peso!.NaoAprovado);
        Assert.Equal(
            $"Não aprovado por Ana Revisão · {Local(peso.NotApprovedAtUtc)} · Repetir o Peso.",
            peso.Frase(DmoNumberFormat.Culture));

        // O controlo (o Resumo e o PDF do Resumo): o do Peso, com o nome.
        var controlo = await _handler.GetAsync(controloId);
        Assert.True(controlo!.NaoAprovado);
        Assert.Equal("Ana Revisão", controlo.Quem);

        await _handler.DecidePartAsync(controloId, ControloParte.Peso, aprovado: true, "4802", null);
        peso = await _handler.ParteAsync(controloId, ControloParte.Peso);
        Assert.Equal($"Aprovado por Chefe Aprovação · {Local(peso!.ApprovedAtUtc)}", peso.Frase(DmoNumberFormat.Culture));
        Assert.Equal("Chefe Aprovação", (await _handler.GetAsync(controloId))!.Quem);
    }

    [Fact]
    public async Task ParteAsync_SemDecisoesPorParte_MostraOResumoGravado_ENullSemControlo()
    {
        // Os controlos de antes das decisões por parte: as duas partes mostram
        // o que ficou gravado no controlo.
        var (controloId, _) = await NovaProducaoAsync("2026207");
        var controlo = await _db.Controlos.SingleAsync(c => c.Id == controloId);
        controlo.ApprovedByOperatorId = "4221";
        controlo.ApprovedAtUtc = RegistadoEm;
        await _db.SaveChangesAsync();

        foreach (var parte in Enum.GetValues<ControloParte>())
        {
            var estado = await _handler.ParteAsync(controloId, parte);
            Assert.Equal("4221", estado!.ApprovedByOperatorId);
            Assert.Equal(RegistadoEm, estado.ApprovedAtUtc);
            Assert.Equal("Operador 4221", estado.Quem);
        }

        Assert.Null(await _handler.ParteAsync(controloId + 100, ControloParte.Peso));
    }

    [Fact]
    public async Task OEstadoTrazONomeDeQuemDecidiu_EmTodasAsLeituras()
    {
        var (controloId, jobOnId) = await NovaProducaoAsync("2026208");

        var devolvido = await _handler.ApproveAndGetAsync(controloId, "4802", "Ok.");
        Assert.Equal("Chefe Aprovação", devolvido!.DecididoPorNome);
        Assert.Equal("Chefe Aprovação", (await _handler.GetAsync(controloId))!.DecididoPorNome);
        Assert.Equal("Chefe Aprovação", (await _handler.FindByJobOnAsync(jobOnId))!.Value.Approval.DecididoPorNome);
    }

    [Fact]
    public void AsFrasesDosTresEstados()
    {
        var em = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var pt = DmoNumberFormat.Culture;

        var aprovado = new ControloApprovalReadModel("4802", em, ReviewNote: "Ok.") { DecididoPorNome = "Chefe Aprovação" };
        Assert.Equal("Chefe Aprovação · 24/07/2026 13:00", aprovado.Detalhe(pt));
        // A nota de uma aprovação não entra na frase; a do «Não aprovar» sim.
        Assert.Equal("Aprovado por Chefe Aprovação · 24/07/2026 13:00", aprovado.Frase(pt));

        var naoAprovado = new ControloApprovalReadModel(null, null, "4801", em, "Repetir os pegamentos.");
        Assert.True(naoAprovado.NaoAprovado);
        Assert.Equal("Operador 4801", naoAprovado.Quem);
        Assert.Equal("Não aprovado por Operador 4801 · 24/07/2026 13:00 · Repetir os pegamentos.", naoAprovado.Frase(pt));

        // Quem entrou por email fica com o email.
        Assert.Equal("Aprovado por chefe@empresa.pt · 24/07/2026 13:00",
            new ControloApprovalReadModel("chefe@empresa.pt", em).Frase(pt));

        var porAprovar = new ControloApprovalReadModel(null, null);
        Assert.False(porAprovar.NaoAprovado);
        Assert.Null(porAprovar.Quem);
        Assert.Null(porAprovar.Detalhe(pt));
        Assert.Equal("Por aprovar", porAprovar.Frase(pt));
    }

    private static string Local(DateTime? utc) =>
        DmoBusinessTime.ToLocal(utc!.Value).ToString("g", DmoNumberFormat.Culture);

    private async Task<(int ControloId, int JobOnId)> NovaProducaoAsync(string numero, bool comPegamentos = false)
    {
        var jobOn = new JobOn { Reference = "5447T194", ProductionNumber = numero, MachineId = (await _db.Machines.SingleAsync()).Id };
        _db.JobOns.Add(jobOn);
        await _db.SaveChangesAsync();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = jobOn.Id };
        _db.Controlos.Add(controlo);
        await _db.SaveChangesAsync();
        _db.Pesos.Add(new Peso
        {
            CmToolId = "T3", BqToolId = "BQ-TOOL-9", Process = "NNPB", WaterTemperatureC = 20,
            VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, GlassDensityGramsPerCm3 = 2.50m,
            ControloId = controlo.Id, RegistadoEmUtc = RegistadoEm
        });
        if (comPegamentos)
        {
            _db.Pegamentos.Add(new Pegamentos
            {
                ControloId = controlo.Id, CmToolId = "T3", ToleranceMm = 0.20m, RegistadoEmUtc = RegistadoEm
            });
        }

        await _db.SaveChangesAsync();
        return (controlo.Id, jobOn.Id);
    }
}

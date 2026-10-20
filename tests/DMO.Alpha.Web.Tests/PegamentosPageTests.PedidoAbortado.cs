using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PegamentosPageTests
{
    [Fact]
    public Task PostMeasurement_SeveralLines_ClientAbortsAfterTheFirstWrite_AllLinesAreSaved() =>
        PostSeveralLinesAndAbortAsync(abortBeforeTheWrite: false);

    [Fact]
    public Task PostMeasurement_SeveralLines_ClientAbortsBeforeTheWrite_AllLinesAreStillSaved() =>
        PostSeveralLinesAndAbortAsync(abortBeforeTheWrite: true);

    private async Task PostSeveralLinesAndAbortAsync(bool abortBeforeTheWrite)
    {
        // Deep sweep, 18/10/2026: com várias linhas («+ Linha»), cada linha
        // era uma gravação própria com o token do pedido. O operador saía da
        // página (ou a rede caía) a meio e ficava só a 1.ª linha; as outras
        // perdiam-se e reenviar duplicava a 1.ª.
        SeedUser("4481", "Pegamentos Operador", "provider-peg-abort", "PegAbort1!");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IRecordPegamentosMeasurementHandler>(provider => new AbortsTheRequest(
                ActivatorUtilities.CreateInstance<RecordPegamentosMeasurementHandler>(provider),
                provider.GetRequiredService<IHttpContextAccessor>(),
                abortBeforeTheWrite))));
        using var client = await LoginClientAsync("4481", "PegAbort1!", factory);
        var controloId = await SeedRealProductionAsync();
        var url = $"{PagePath}?controlo_id={controloId}";

        var token = await GetVerificationTokenAsync(client, url);
        try
        {
            await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
                ("controlo_id", controloId.ToString()), ("component", "CM"), ("gargalo", "70.00"),
                ("costura", "70.10"), ("contra_costura", "69.90"),
                ("costura", "70.05"), ("contra_costura", ""),
                ("costura", "70.20"), ("contra_costura", "70.00"),
                ("__RequestVerificationToken", token)));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            // O cliente foi-se embora: é o browser a sair da página.
        }

        // O servidor continua depois de o cliente sair: espera pelas gravações.
        List<PegamentosMeasurement> rows = [];
        for (var attempt = 0; attempt < 30 && rows.Count < 3; attempt++)
        {
            await Task.Delay(100);
            using var scope = factory.Services.CreateScope();
            rows = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PegamentosMeasurements
                .AsNoTracking().OrderBy(m => m.RowPosition).ToListAsync();
        }

        Assert.Equal(new[] { 70.10m, 70.05m, 70.20m }, rows.Select(r => r.CosturaMm));
        Assert.Equal(new decimal?[] { 69.90m, null, 70.00m }, rows.Select(r => r.ContraCosturaMm));
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.RowPosition));
        Assert.All(rows, r => Assert.Equal(70m, r.NominalGargaloMm));
    }

    /// <summary>
    /// Corta o pedido antes ou logo a seguir à gravação, como o browser ao
    /// sair da página: o pedido já chegou inteiro e grava até ao fim.
    /// </summary>
    private sealed class AbortsTheRequest(IRecordPegamentosMeasurementHandler inner, IHttpContextAccessor accessor, bool before)
        : IRecordPegamentosMeasurementHandler
    {
        public async Task<RecordPegamentosMeasurementOutcome> HandleAsync(
            RecordPegamentosMeasurement command,
            CancellationToken cancellationToken = default)
        {
            if (before)
            {
                accessor.HttpContext?.Abort();
            }

            var outcome = await inner.HandleAsync(command, cancellationToken);
            accessor.HttpContext?.Abort();
            return outcome;
        }
    }
}

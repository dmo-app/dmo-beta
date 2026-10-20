using System.Net;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class PesoToolScopedPageTests
{
    [Fact]
    public Task RegisterMeasurement_SeveralLines_ClientAbortsAfterTheFirstWrite_AllLinesAreSaved() =>
        PostSeveralLinesAndAbortAsync(abortBeforeTheWrite: false, "4231");

    [Fact]
    public Task RegisterMeasurement_SeveralLines_ClientAbortsBeforeTheWrite_AllLinesAreStillSaved() =>
        PostSeveralLinesAndAbortAsync(abortBeforeTheWrite: true, "4232");

    private async Task PostSeveralLinesAndAbortAsync(bool abortBeforeTheWrite, string operatorId)
    {
        // Deep sweep, 18/10/2026: com várias linhas (o «+»), cada linha era
        // uma gravação própria com o token do pedido. O operador saía da
        // página (ou a rede caía) a meio e ficavam só as primeiras; as outras
        // perdiam-se sem aviso e reenviar duplicava as primeiras.
        await SeedWorkedExampleAsync();
        SeedUser(operatorId, "Peso Operador", "provider-peso-abort-" + operatorId, "PesoAbort1!");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IRecordPesoMeasurementHandler>(provider => new AbortsTheRequest(
                ActivatorUtilities.CreateInstance<RecordPesoMeasurementHandler>(provider),
                provider.GetRequiredService<IHttpContextAccessor>(),
                abortBeforeTheWrite))));
        using var client = await LoginClientAsync(operatorId, "PesoAbort1!", factory);

        var created = await PostCreateAsync(client, $"{PagePath}?cm_tool_id=T3&bq_tool_id=BQ-TOOL-9", "T3", "BQ-TOOL-9", "20",
            volumePuncao: "30.00", volumeMarisa: "10.00");
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var pesoUrl = created.Headers.Location!.ToString();
        var pesoId = int.Parse(System.Text.RegularExpressions.Regex.Match(pesoUrl, @"peso_id=(\d+)").Groups[1].Value);

        var token = await GetVerificationTokenAsync(client, pesoUrl);
        try
        {
            await client.PostAsync($"{PagePath}?handler=RegisterMeasurement", HtmlFormHelpers.BuildFormPost(
                ("peso_id", pesoId.ToString()),
                ("cm_subject", "12"), ("water_weight", "199,64"),
                ("cm_subject", "21"), ("water_weight", "99.82"),
                ("cm_subject", "45"), ("water_weight", "150.2"),
                ("__RequestVerificationToken", token)));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            // O cliente foi-se embora: é o browser a sair da página.
        }

        // O servidor continua depois de o cliente sair: espera pela gravação.
        List<PesoMeasurement> rows = [];
        for (var attempt = 0; attempt < 30 && rows.Count < 3; attempt++)
        {
            await Task.Delay(100);
            using var scope = factory.Services.CreateScope();
            rows = await scope.ServiceProvider.GetRequiredService<DmoDbContext>().PesoMeasurements
                .AsNoTracking().Where(m => m.PesoId == pesoId).OrderBy(m => m.RowPosition).ToListAsync();
        }

        Assert.Equal(new[] { "12", "21", "45" }, rows.Select(r => r.CmSubject));
        Assert.Equal(new[] { 199.64m, 99.82m, 150.2m }, rows.Select(r => r.WaterWeightGrams));
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.RowPosition));
    }

    /// <summary>
    /// Corta o pedido antes ou logo a seguir à gravação, como o browser ao
    /// sair da página: o pedido já chegou inteiro e grava até ao fim.
    /// </summary>
    private sealed class AbortsTheRequest(IRecordPesoMeasurementHandler inner, IHttpContextAccessor accessor, bool before)
        : IRecordPesoMeasurementHandler
    {
        public async Task<RecordPesoMeasurementOutcome> HandleAsync(
            RecordPesoMeasurement command,
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

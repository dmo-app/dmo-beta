using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class FerramentasPagesTests
{
    [Fact]
    public async Task PostCreate_ClientAbortsAfterTheToolIsRegistered_TheTechnicalValuesAreStillSaved()
    {
        // Deep sweep, 18/10/2026: com dois ou três cliques em «Registar
        // ferramenta», o browser aborta o primeiro pedido. A ferramenta já
        // estava gravada; a informação técnica perdia-se sem aviso, porque o
        // segundo pedido encontra a ferramenta e não a grava outra vez.
        SeedUser("4471", "Ferramentas Owner", "provider-ferr-abort", "FerrAbort1!");
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ICreateToolHandler>(provider => new AbortsAfterCreate(
                ActivatorUtilities.CreateInstance<CreateToolHandler>(provider),
                provider.GetRequiredService<IHttpContextAccessor>()))));
        using var client = await LoginClientAsync("4471", "FerrAbort1!", factory);

        var token = await GetVerificationTokenAsync(client, $"{IndexPath}?criar=1");
        try
        {
            await client.PostAsync($"{IndexPath}?handler=Create", HtmlFormHelpers.BuildFormPost(
                ("type_token", "CM"),
                ("reference", "9931"),
                ("lot", "1"),
                ("process_token", "PS"),
                ("nominal_weight", "250,5"),
                ("diametro_gargalo", "26"),
                ("__RequestVerificationToken", token)));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            // O cliente foi-se embora: é o browser a abortar o primeiro clique.
        }

        // O servidor continua depois de o cliente sair: espera pela gravação.
        ToolTechnicalContext? stored = null;
        for (var attempt = 0; attempt < 50 && stored is null; attempt++)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            stored = await db.ToolTechnicalContexts.AsNoTracking()
                .SingleOrDefaultAsync(t => db.Tools.Any(tool => tool.ToolId == t.ToolId && tool.Reference == "9931"));
            if (stored is null) await Task.Delay(100);
        }

        Assert.NotNull(stored);
        Assert.Equal(250.5m, stored!.NominalWeightGrams);
        Assert.Equal(26m, stored.DiametroGargaloMm);
    }

    /// <summary>Grava a ferramenta e corta o pedido logo a seguir, como o browser no duplo clique.</summary>
    private sealed class AbortsAfterCreate(ICreateToolHandler inner, IHttpContextAccessor accessor) : ICreateToolHandler
    {
        public async Task<CreateToolOutcome> HandleAsync(CreateTool command, CancellationToken cancellationToken = default)
        {
            var outcome = await inner.HandleAsync(command, cancellationToken);
            accessor.HttpContext?.Abort();
            return outcome;
        }
    }
}

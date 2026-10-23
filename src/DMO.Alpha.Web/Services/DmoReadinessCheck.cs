using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DMO.Alpha.Web.Services;

/// <summary>Pronto para servir: configuração, base acessível e esquema atualizado. Nunca migra nem chama Auth.</summary>
public sealed class DmoReadinessCheck(IServiceScopeFactory scopes, IConfiguration configuration, IWebHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var development = environment.IsDevelopment();
        var supabase = !development || string.Equals(configuration["Dmo:AuthenticationProvider"], "Supabase", StringComparison.OrdinalIgnoreCase);
        if (StartupConfigurationCheck.Problems(configuration, development, supabase).Any(p => p.Level >= LogLevel.Error))
            return HealthCheckResult.Unhealthy("Configuração incompleta.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            if (!db.Database.IsRelational())
                return development ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Base indisponível.");
            if (!await db.Database.CanConnectAsync(timeout.Token))
                return HealthCheckResult.Unhealthy("Base indisponível.");
            if ((await db.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
                return HealthCheckResult.Unhealthy("Esquema por atualizar.");
            return HealthCheckResult.Healthy();
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A resposta pública não expõe ligações, mensagens SQL nem credenciais.
            return HealthCheckResult.Unhealthy("Dependência indisponível.");
        }
    }
}

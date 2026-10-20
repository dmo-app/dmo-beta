namespace DMO.Alpha.Web.Services;

/// <summary>
/// O que falta na configuração do serviço, dito no log do arranque (sweep de
/// 19/10/2026, 5.5): sem as chaves do Supabase tudo dava «Credenciais
/// inválidas.» e nada no log dizia porquê. Cada linha nomeia a variável em
/// falta, nunca o valor. Nada trava o arranque.
/// </summary>
public static class StartupConfigurationCheck
{
    // Variáveis que o alojamento define sozinho: com uma delas, a app não está
    // num computador de desenvolvimento (Railway; Azure App Service; Azure
    // Container Apps).
    private static readonly string[] HostedMarkers = ["RAILWAY_SERVICE_ID", "WEBSITE_SITE_NAME", "CONTAINER_APP_NAME"];

    public sealed record Problem(LogLevel Level, string Message);

    public static IReadOnlyList<Problem> Problems(IConfiguration configuration, bool isDevelopment, bool useSupabase)
    {
        var problems = new List<Problem>();

        if (isDevelopment)
        {
            // Aviso e não erro: o DEV no Railway corre em Development de
            // propósito (handoff de 15/10/2026); mudar é decisão do Diogo.
            if (HostedMarkers.Any(marker => !string.IsNullOrWhiteSpace(configuration[marker])))
            {
                problems.Add(new(LogLevel.Warning,
                    "ASPNETCORE_ENVIRONMENT=Development on a hosted service: error details are shown to anyone " +
                    "who opens the app. Set ASPNETCORE_ENVIRONMENT=Production."));
            }
        }
        else
        {
            if (!string.Equals(configuration["Dmo:AuthenticationProvider"], "Supabase", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add(new(LogLevel.Warning,
                    "Authentication and user provisioning use Supabase outside Development. " +
                    "Set Dmo__AuthenticationProvider=Supabase explicitly; InMemory is only available in Development."));
            }

            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DmoDatabase")))
            {
                problems.Add(new(LogLevel.Error,
                    "ConnectionStrings__DmoDatabase is missing. PostgreSQL operations are unavailable; " +
                    "no InMemory database will be used outside Development. Login and the error page remain available."));
            }
        }

        if (useSupabase)
        {
            if (!Uri.IsWellFormedUriString(configuration["Dmo:SupabaseUrl"], UriKind.Absolute))
            {
                problems.Add(new(LogLevel.Error,
                    "Dmo__SupabaseUrl is missing or is not an absolute URL: nobody can sign in."));
            }

            if (string.IsNullOrWhiteSpace(configuration["Dmo:SupabasePublishableKey"]))
            {
                problems.Add(new(LogLevel.Error,
                    "Dmo__SupabasePublishableKey is missing: nobody can sign in."));
            }

            if (string.IsNullOrWhiteSpace(configuration["Dmo:SupabaseSecretKey"]))
            {
                problems.Add(new(LogLevel.Error,
                    "Dmo__SupabaseSecretKey is missing: the Admin cannot create users or set temporary passwords, " +
                    "and nobody can change their password."));
            }
        }

        return problems;
    }
}

namespace DMO.Alpha.Infrastructure.Authentication;

/// <summary>
/// Options for the Supabase-backed authentication provider — the CURRENT
/// Supabase key system only: publishable (replaces legacy anon) and secret
/// (replaces legacy service_role) keys. Legacy keys are deprecated by the
/// end of 2026 and are never stored or depended on by new code. The values
/// come from the deployment configuration (Dmo:Supabase* settings).
/// </summary>
public sealed class SupabaseAuthOptions
{
    public string Url { get; set; } = string.Empty;

    /// <summary>The project's CURRENT-system publishable key (short string, not a JWT).</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>The project's CURRENT-system secret key (server-side only, short string, not a JWT).</summary>
    public string SecretKey { get; set; } = string.Empty;
}

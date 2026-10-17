namespace DMO.Alpha.Infrastructure.Authentication;

/// <summary>
/// O DMO aceita palavras-passe curtas (o PIN temporário de 4 números e
/// palavras-passe escolhidas com menos de 6 caracteres, pedido do Diogo de
/// 17/10/2026), mas o Supabase Auth impõe sempre um mínimo de 6 caracteres
/// (piso fixo do GoTrue, que o projeto não pode baixar). Uma palavra-passe
/// curta segue para o Supabase com um prefixo fixo que também cumpre as
/// políticas de classes de caracteres (maiúscula, minúscula, número e
/// símbolo). As de 6 ou mais caracteres seguem tal como estão, por isso as
/// contas existentes não mudam. Aplica-se em todos os caminhos que enviam a
/// palavra-passe ao Supabase: login, mudança, criação e reposição.
/// </summary>
public static class SupabasePassword
{
    internal const int ProviderMinimumLength = 6;

    private const string ShortPasswordPrefix = "Dmo#1:";

    public static string ToProvider(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return password.Length < ProviderMinimumLength
            ? ShortPasswordPrefix + password
            : password;
    }
}

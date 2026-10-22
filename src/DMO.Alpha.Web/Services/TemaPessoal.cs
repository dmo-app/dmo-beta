namespace DMO.Alpha.Web.Services;

/// <summary>
/// O modo claro/escuro da conta no cookie «dmo-tema»: escrito ao entrar e ao
/// gravar nas Definições → Pessoal; o &lt;head&gt; lê-o antes de desenhar.
/// </summary>
public static class TemaPessoal
{
    public const string Cookie = "dmo-tema";

    public static void Escrever(HttpContext context, string? tema)
    {
        if (tema is "light" or "dark")
        {
            context.Response.Cookies.Append(Cookie, tema, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(365),
                IsEssential = true
            });
        }
    }

    public static string? Ler(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Cookie, out var tema) && tema is "light" or "dark" ? tema : null;
}

using System.Diagnostics.CodeAnalysis;

namespace DMO.Alpha.Web.Services;

/// <summary>
/// A única regra de «caminho desta aplicação» para os regressos depois de uma
/// ação (o login, o registo nas Ferramentas, a ficha da ferramenta): começa
/// por «/», sem «//», sem «\» (que os browsers tratam como «/»), sem
/// caracteres de controlo nem espaços (o browser tira o TAB e a mudança de
/// linha de «/&lt;TAB&gt;/outro.site» e abriria «//outro.site»).
/// É mais estrita que o <c>Url.IsLocalUrl</c>, por isso um caminho que passa
/// aqui nunca faz o <c>LocalRedirect</c> falhar (sweep de 19/10/2026, 5.7:
/// havia duas regras e a das Ferramentas deixava passar o TAB, que dava erro
/// 500 depois de gravar a ferramenta).
/// </summary>
public static class LocalUrl
{
    public static bool IsSafe([NotNullWhen(true)] string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || url[1] != '/')
        && !url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\');
}

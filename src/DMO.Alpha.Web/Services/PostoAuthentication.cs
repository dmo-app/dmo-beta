namespace DMO.Alpha.Web.Services;

/// <summary>
/// AUTH-B05 (DMO-657): o posto da Reparação Interna tem o SEU PRÓPRIO
/// esquema de cookie, separado do login normal. Antes, as duas sessões
/// partilhavam o mesmo cookie com <c>SlidingExpiration = true</c> e
/// <c>ExpireTimeSpan</c> de 1 hora: QUALQUER pedido autenticado deslizava
/// o cookie do posto para a hora inteira, por cima do ExpiresUtc de 3
/// minutos (ou do valor das Definições) que <see cref="DmoSignInService.SignInPostoAsync"/>
/// já escrevia — a sessão nunca fechava sozinha enquanto houvesse QUALQUER
/// pedido, não só os de uso real. Com o esquema próprio, sem deslizar, o
/// ExpiresUtc escrito no login é o único que conta.
/// </summary>
public static class PostoAuthentication
{
    public const string Scheme = "DmoAuthPosto";

    public const string CookieName = "DmoAuthPosto";
}

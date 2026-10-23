namespace DMO.Alpha.Web.Presentation;

/// <summary>
/// A FERRAMENTA COM O USO CHEIO (Diogo, 23/10/2026): com 100% ou mais de uso
/// nunca se bloqueia nem se avisa; só o tool_id, a referência e o lote mudam
/// ligeiramente de cor (a classe <see cref="Classe"/>, em dmo-normalize.css).
/// </summary>
public static class UsoCheio
{
    public const decimal Limite = 100m;

    public const string Classe = "dmo-uso-cheio";

    public static bool Cheio(decimal? percentagem) => percentagem >= Limite;

    /// <summary>A classe para o elemento, ou null (o Razor não escreve o atributo).</summary>
    public static string? ClasseDe(decimal? percentagem) => Cheio(percentagem) ? Classe : null;
}

namespace DMO.Alpha.Web.Presentation;

/// <summary>O lote como se mostra; a regra vive em <see cref="DMO.Alpha.Core.Tools.LoteExibido"/>.</summary>
public static class LoteTexto
{
    public static string Mostrar(string? lote) => DMO.Alpha.Core.Tools.LoteExibido.Mostrar(lote);
}

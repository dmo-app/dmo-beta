namespace DMO.Alpha.Core.Tools;

/// <summary>
/// O LOTE COMO SE MOSTRA (Diogo, 22/10/2026): sem zeros à esquerda, «Lote 2»
/// e nunca «Lote 02». Só a exibição: o lote gravado fica como está. Um lote
/// que não é só algarismos (ex.: «02A») mostra-se como foi escrito.
/// </summary>
public static class LoteExibido
{
    public static string Mostrar(string? lote)
    {
        var texto = lote?.Trim() ?? string.Empty;
        if (texto.Length == 0 || !texto.All(char.IsAsciiDigit))
        {
            return texto;
        }

        var semZeros = texto.TrimStart('0');
        return semZeros.Length == 0 ? "0" : semZeros;
    }
}

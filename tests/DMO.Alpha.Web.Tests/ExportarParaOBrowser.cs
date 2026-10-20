namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O HTML que a app desenha, guardado para os testes no Chromium
/// (tests/visual), quando DMO_UI_EVIDENCE_DIR está definida (o
/// scripts/ci/ci-local.sh define-a). Sem ela não guarda nada.
/// </summary>
internal static class ExportarParaOBrowser
{
    public static void Gravar(string nome, string html)
    {
        var pasta = Environment.GetEnvironmentVariable("DMO_UI_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(pasta)) return;
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, nome), html);
    }
}

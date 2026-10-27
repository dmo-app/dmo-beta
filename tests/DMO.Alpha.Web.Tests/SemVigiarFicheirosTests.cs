namespace DMO.Alpha.Web.Tests;

// Diogo, 27/10/2026: os testes de páginas falhavam ao acaso com erro 500.
// Cada aplicação de teste gastava vigias de ficheiros do Linux, que são só
// 128 por utilizador; ver SemVigiarFicheiros.
public sealed class SemVigiarFicheirosTests
{
    [SkippableFact]
    public async Task UmaAplicacaoDeTeste_ArrancaEAbreOLogin_SemGastarVigiasDeFicheiros()
    {
        Skip.IfNot(Directory.Exists("/proc/self/fd"), "Só no Linux.");
        var antes = Vigias();

        using var fabrica = new CustomWebApplicationFactory();
        var cliente = fabrica.CreateClient();
        await cliente.GetAsync("/");
        await cliente.GetAsync("/Login");

        Assert.Equal(antes, Vigias());
    }

    private static int Vigias() => Directory.GetFiles("/proc/self/fd").Count(fd =>
    {
        try
        {
            return new FileInfo(fd).LinkTarget?.Contains("inotify", StringComparison.Ordinal) == true;
        }
        catch (IOException)
        {
            return false;
        }
    });
}

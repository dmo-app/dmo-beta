using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

public sealed partial class PlaneamentoSumarioPageTests
{
    // Auditoria de funções de 27/10/2026 (F1): só com o Job On ver, as abas não
    // levam ao Histórico do Job On, que pede o Job On criar. O «Job On» e o título
    // do painel abrem o Planeamento em leitura (Diogo, 27/10/2026: «jobon view
    // esta sem planeamento»).
    [Fact]
    public async Task WithJobOnViewOnly_TheTabsLeadToPagesTheProfileCanOpen()
    {
        using var soVer = await LoginAsync("5604", DmoCapabilities.JobOnView);
        var html = await GetAsync(soVer);
        Assert.Contains("href=\"/JobOn\"", html);
        Assert.DoesNotContain("href=\"/JobOn?historico=1\"", html);

        using var criar = await LoginAsync("5605", DmoCapabilities.JobOnCreate);
        var completo = await GetAsync(criar);
        Assert.Contains("href=\"/JobOn?historico=1\"", completo);
        Assert.Contains("href=\"/JobOn\"", completo);
    }
}

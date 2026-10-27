using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Web.Tests;

public sealed partial class PlaneamentoSumarioPageTests
{
    // Auditoria de funções de 27/10/2026 (F1): só com o Job On ver, as abas e
    // o título do painel não levam ao calendário nem ao Histórico do Job On,
    // que pedem o Job On criar; o «Job On» abre a Folha.
    [Fact]
    public async Task WithJobOnViewOnly_TheTabsLeadToPagesTheProfileCanOpen()
    {
        using var soVer = await LoginAsync("5604", DmoCapabilities.JobOnView);
        var html = await GetAsync(soVer);
        Assert.Contains("href=\"/JobOn/Folha\"", html);
        Assert.DoesNotContain("href=\"/JobOn?historico=1\"", html);
        Assert.DoesNotContain("href=\"/JobOn\"", html);

        using var criar = await LoginAsync("5605", DmoCapabilities.JobOnCreate);
        var completo = await GetAsync(criar);
        Assert.Contains("href=\"/JobOn?historico=1\"", completo);
        Assert.Contains("href=\"/JobOn\"", completo);
    }
}

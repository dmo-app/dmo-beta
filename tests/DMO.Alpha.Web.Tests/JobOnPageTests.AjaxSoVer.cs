using System.Net;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// AUTH-B06 (DMO-653): os handlers AJAX ProcurarProducao, ToolCandidates e
/// ToolFacts do Job On não tinham o guard SoVerAsync, deixando quem só tem
/// Job On ver (jobon.view, sem jobon.create) chegar ao catálogo de
/// ferramentas e aos fatos técnicos pelo endereço direto, mesmo sem esses
/// dados aparecerem na UI de leitura.
/// </summary>
public partial class JobOnPageTests
{
    [Fact]
    public async Task JobOnVer_HandlerProcurarProducao_EhRedirecionado()
    {
        SeedUserSoVer("4494", "provider-so-ver-procurar", "Folha1234!");
        await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4494", "Folha1234!");

        var resposta = await client.GetAsync($"{PagePath}?handler=ProcurarProducao&q=5810");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal(PagePath, resposta.Headers.Location!.ToString());
    }

    [Fact]
    public async Task JobOnVer_HandlerToolCandidates_EhRedirecionado()
    {
        SeedUserSoVer("4495", "provider-so-ver-candidates", "Folha1234!");
        await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4495", "Folha1234!");

        var resposta = await client.GetAsync($"{PagePath}?handler=ToolCandidates&type=CM&all=true&q=");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal(PagePath, resposta.Headers.Location!.ToString());
    }

    [Fact]
    public async Task JobOnVer_HandlerToolFacts_EhRedirecionado()
    {
        SeedUserSoVer("4496", "provider-so-ver-toolfacts", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4496", "Folha1234!");

        var resposta = await client.GetAsync($"{PagePath}?handler=ToolFacts&cm_tool_id=F-CM&jobon_id={id}");
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal(PagePath, resposta.Headers.Location!.ToString());
    }

    [Fact]
    public async Task JobOnCriar_HandlersAjax_ContinuamAFuncionar()
    {
        SeedUser("4497", "Chefe planeamento ajax", "provider-criar-ajax", "Folha1234!");
        var id = await SeedJobOnCompletoAsync();
        using var client = await LoginClientAsync("4497", "Folha1234!");

        var procurar = await client.GetAsync($"{PagePath}?handler=ProcurarProducao&q=5810");
        Assert.Equal(HttpStatusCode.OK, procurar.StatusCode);

        var candidates = await client.GetAsync($"{PagePath}?handler=ToolCandidates&type=CM&all=true&q=");
        Assert.Equal(HttpStatusCode.OK, candidates.StatusCode);

        var facts = await client.GetAsync($"{PagePath}?handler=ToolFacts&cm_tool_id=F-CM&jobon_id={id}");
        Assert.Equal(HttpStatusCode.OK, facts.StatusCode);
    }
}

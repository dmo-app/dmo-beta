using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;
public sealed class ReferenceFilterPagesTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private async Task<HttpClient> LoginAsync(params string[] capabilities)
    {
        factory.ResetState();
        const string email="referencia@dmo.local";
        factory.AuthProvider.AddAccount(email,"ref-provider","Referencia123!",email);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var user=new User {Id=Guid.NewGuid(),Name="Operador referência",OperatorId="7989",ProviderUserId="ref-provider"};
        var template=new Template {Id=Guid.NewGuid(),TemplateKey="ref-test",Name="Referências",Capabilities=capabilities};
        db.Users.Add(user);db.Templates.Add(template);db.TemplateAssignments.Add(new TemplateAssignment {UserId=user.Id,TemplateId=template.Id});db.SaveChanges();
        var client=factory.CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect=false,HandleCookies=true});
        var html=await client.GetStringAsync("/login");
        using var response=await client.PostAsync("/login",HtmlFormHelpers.BuildFormPost(("identifier",email),("password","Referencia123!"),("__RequestVerificationToken",HtmlFormHelpers.ExtractRequestVerificationToken(html)!)));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);return client;
    }

    private void Seed()
    {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ArmazemMovimentos.RemoveRange(db.ArmazemMovimentos);db.ToolMachineLinks.RemoveRange(db.ToolMachineLinks);db.Tools.RemoveRange(db.Tools);db.Machines.RemoveRange(db.Machines);db.SaveChanges();
        var c2=new Machine {Code="C2"};var b1=new Machine {Code="B1"};db.Machines.AddRange(c2,b1);
        db.Tools.AddRange(new Tool {ToolId="REF-A",Type=ToolType.Cm,Reference="REF173",Lot="3"},new Tool {ToolId="REF-B",Type=ToolType.Mf,Reference="OUTRA",Lot="4"});db.SaveChanges();
        db.ToolMachineLinks.AddRange(new ToolMachineLink {ToolId="REF-A",MachineId=c2.Id},new ToolMachineLink {ToolId="REF-B",MachineId=b1.Id});
        foreach(var pair in new[]{("REF-A",c2.Id,"Oficina C"),("REF-B",b1.Id,"Oficina B")})
            db.ArmazemMovimentos.Add(new ArmazemMovimento {ToolId=pair.Item1,MachineId=pair.Item2,Tipo=ArmazemMovimentoTipo.Saida,ReparadorNome=pair.Item3,OperacaoId=Guid.NewGuid(),OcorridoEmUtc=DateTime.UtcNow,RegistadoEmUtc=DateTime.UtcNow});
        db.SaveChanges();
    }

    private static string[] Available(string html,string id)
    {
        var select=Regex.Match(html,$"<select\\b[^>]*id=\"{id}\"[^>]*>([\\s\\S]*?)</select>");Assert.True(select.Success,"O campo existe");
        return Regex.Matches(select.Groups[1].Value,"<option\\b([^>]*)>([\\s\\S]*?)</option>")
            .Where(m=>!Regex.IsMatch(m.Groups[1].Value,"\\b(disabled|hidden)\\b"))
            .Select(m=>Regex.Match(m.Groups[1].Value,"value=\"([^\"]*)\"").Groups[1].Value).Where(v=>v.Length>0).ToArray();
    }

    [Fact]
    public async Task NoHistoricoAReferenciaRestringeAsLinhasNaPaginaENasOpcoes()
    {
        using var client=await LoginAsync(DmoCapabilities.ArmazemGestao);Seed();
        var all=await client.GetStringAsync("/Armazem/Ultimos");Assert.Equal(new[]{"B1","C2"},Available(all,"armMovLinha"));
        var filtered=await client.GetStringAsync("/Armazem/Ultimos?referencia=REF173");Assert.Equal(new[]{"C2"},Available(filtered,"armMovLinha"));
        var json=JsonDocument.Parse(await client.GetStringAsync("/OpcoesDaReferencia?scope=armazem&reference=REF173"));
        Assert.Equal(new[]{"C2"},json.RootElement.GetProperty("linha").EnumerateArray().Select(x=>x.GetString()));
        Assert.Equal(new[]{"Oficina C"},json.RootElement.GetProperty("reparador").EnumerateArray().Select(x=>x.GetString()));
    }

    [Fact]
    public async Task AsOpcoesNaoRevelamDadosDeModulosSemPermissao()
    {
        using var client=await LoginAsync(DmoCapabilities.ArmazemGestao);Seed();
        using var response=await client.GetAsync("/OpcoesDaReferencia?scope=ferramentas&reference=REF173");Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
}

using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

// Cliques repetidos não mostram mensagens (Diogo, 18/10/2026; deep sweep,
// B-admin-6): a rede caiu a meio de «Criar template»; o servidor criou-a,
// mas o browser não recebeu a resposta. Ao voltar a carregar (o nome
// continua no campo), o segundo pedido criava outra template com o mesmo
// nome, e o dropdown da ficha do utilizador ficava com duas opções iguais.
public sealed partial class AdminUsersAndTemplatesPagesTests
{
    [Fact]
    public async Task AdminTemplates_CriarOutraVezDepoisDeUmaFalha_AbreATemplateJaCriada()
    {
        SeedConfiguredAdmin("tpl-repetir@example.test", "tpl-repetir-provider", "TplRepetir123!");
        using var adminClient = await LoginAdminAsync(_factory, "tpl-repetir@example.test", "TplRepetir123!");
        var name = "Offline " + Guid.NewGuid().ToString("N")[..8];
        Task<HttpResponseMessage> Create() => PostAsync(
            adminClient, "/Admin/Templates?handler=Create",
            ("name", name),
            ("capabilities", DmoCapabilities.JobOnCreate),
            ("capabilities", DmoCapabilities.ControloCreate));

        // A resposta do primeiro perdeu-se; o segundo é o clique depois de a
        // rede voltar. Mostra o mesmo que a primeira: a template criada.
        var lost = await Create();
        var again = await Create();
        Assert.Equal(HttpStatusCode.Redirect, lost.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        var templateKey = LocationQueryValue(lost, "template");
        Assert.NotNull(templateKey);
        Assert.Equal(templateKey, LocationQueryValue(again, "template"));
        Assert.Equal("1", LocationQueryValue(again, "created"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        Assert.Equal(1, await db.Templates.AsNoTracking().CountAsync(t => t.Name == name));
    }
}

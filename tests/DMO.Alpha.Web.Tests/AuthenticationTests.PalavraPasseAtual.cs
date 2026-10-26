using System.Net;

namespace DMO.Alpha.Web.Tests;

public partial class AuthenticationTests
{
    [Fact]
    public async Task ChangePassword_WithoutTemporaryPassword_AsksForTheCurrentOne()
    {
        // Auditoria externa DS-LOG-03 (26/10/2026): quem encontra uma sessão
        // aberta não muda a palavra-passe do dono sem saber a atual. Só a
        // mudança obrigatória (palavra-passe temporária) dispensa a atual.
        SeedUser("9892", "Rui", "provider-user-9892", "right");
        using var client = CreateClient();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, "9892", "right")).StatusCode);

        var page = await client.GetAsync("/change-password");
        Assert.Contains("currentPassword", await page.Content.ReadAsStringAsync());

        var token = await GetVerificationTokenAsync(client, "/change-password");
        var semAtual = await client.PostAsync("/change-password", HtmlFormHelpers.BuildFormPost(
            ("newPassword", "nova1"), ("confirmPassword", "nova1"),
            ("__RequestVerificationToken", token)));
        Assert.Equal(HttpStatusCode.OK, semAtual.StatusCode);
        Assert.Contains("Indique a palavra-passe atual", await semAtual.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(CreateClient(), "9892", "right")).StatusCode);
    }
}

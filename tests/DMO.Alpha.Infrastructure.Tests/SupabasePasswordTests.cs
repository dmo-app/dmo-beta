using System.Net;
using System.Text.Json;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Infrastructure.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DMO.Alpha.Infrastructure.Tests;

/// <summary>
/// Palavras-passe curtas (PIN de 4 números, pedido do Diogo de 17/10/2026)
/// seguem para o Supabase com prefixo, porque o Supabase exige 6 caracteres;
/// as de 6 ou mais seguem tal como estão, para as contas existentes.
/// </summary>
public sealed class SupabasePasswordTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"user":{"id":"11111111-2222-3333-4444-555555555555","email":"a@b.pt"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        }
    }

    [Theory]
    [InlineData("1234", "Dmo#1:1234")]
    [InlineData("0007", "Dmo#1:0007")]
    [InlineData("abcde", "Dmo#1:abcde")]
    [InlineData("abcdef", "abcdef")]
    [InlineData("NewSecurePass1!", "NewSecurePass1!")]
    public void ToProvider_PrefixesOnlyPasswordsShorterThanSix(string typed, string sent)
    {
        Assert.Equal(sent, SupabasePassword.ToProvider(typed));
    }

    [Fact]
    public async Task LoginAndChange_WithAPin_SendThePrefixedPassword()
    {
        var handler = new CapturingHandler();
        using var provider = new SupabaseAuthenticationProvider(
            new HttpClient(handler),
            Options.Create(new SupabaseAuthOptions
            {
                Url = "https://test-project.supabase.co",
                PublishableKey = "OPAQUE-TEST-PUBLISHABLE",
                SecretKey = "OPAQUE-TEST-SECRET"
            }),
            NullLogger<SupabaseAuthenticationProvider>.Instance);

        var login = await provider.AuthenticateAsync("a@b.pt", "4821");
        var change = await provider.ChangePasswordAsync("11111111-2222-3333-4444-555555555555", "7305");

        Assert.True(login.Success);
        Assert.Equal(ChangePasswordResult.Success, change);
        Assert.Equal("Dmo#1:4821", JsonDocument.Parse(handler.Bodies[0]).RootElement.GetProperty("password").GetString());
        Assert.Equal("Dmo#1:7305", JsonDocument.Parse(handler.Bodies[1]).RootElement.GetProperty("password").GetString());
    }
}

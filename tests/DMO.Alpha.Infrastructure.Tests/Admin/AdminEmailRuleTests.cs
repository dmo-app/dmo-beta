using DMO.Alpha.Core.Admin;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// A regra «é o email do ADMIN?» num só sítio (varredura de 18/10/2026, E5):
/// o login, a sessão, a autorização e a lista de utilizadores usam-na.
/// </summary>
public sealed class AdminEmailRuleTests
{
    [Theory]
    [InlineData("admin@dmo.pt", "admin@dmo.pt", true)]
    [InlineData(" Admin@DMO.pt ", "admin@dmo.pt", true)]
    [InlineData("admin@dmo.pt", " ADMIN@dmo.pt", true)]
    [InlineData("outro@dmo.pt", "admin@dmo.pt", false)]
    [InlineData(null, "admin@dmo.pt", false)]
    [InlineData("admin@dmo.pt", null, false)]
    [InlineData(null, null, false)]
    public void IsAdminEmail(string? email, string? adminEmail, bool expected) =>
        Assert.Equal(expected, AdminEmailRule.IsAdminEmail(email, adminEmail));
}

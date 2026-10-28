using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// Repor o acesso grava primeiro na app (sessões antigas fechadas e mudança
/// obrigatória) e só depois muda a palavra-passe no provider (issue #1 do
/// dmo-Improvements; relatório de backend de 28/10/2026, BE12).
/// </summary>
public sealed class ReporAcessoPrimeiroAAppTests : IDisposable
{
    private readonly FalhaAoGravar _falha = new();
    private readonly DmoDbContext _db;
    private readonly ProviderFalso _provider = new();
    private readonly UserAdminService _service;

    public ReporAcessoPrimeiroAAppTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_falha)
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            OperatorId = "0042",
            Name = "Operador 0042",
            Email = "ana@empresa.pt",
            ProviderUserId = "id-0042",
            SessionVersion = 3
        });
        _db.SaveChanges();
        _service = new UserAdminService(_db, _provider, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AGravacaoNaAppFalha_APalavraPasseNaoMudaNoProvider()
    {
        _falha.Ligada = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ResetUserAccessAsync(new ResetUserAccess("0042")));

        Assert.Equal(0, _provider.Resets);
        _falha.Ligada = false;
        var user = await LerAsync();
        Assert.Equal(3, user.SessionVersion);
        Assert.False(user.RequiresPasswordChange);
    }

    [Fact]
    public async Task OProviderNaoResponde_ASessaoAntigaJaCaiuEAMudancaEObrigatoria()
    {
        _provider.Rebenta = true;

        await Assert.ThrowsAsync<HttpRequestException>(() => _service.ResetUserAccessAsync(new ResetUserAccess("0042")));

        Assert.Equal(1, _provider.Resets);
        var user = await LerAsync();
        Assert.Equal(4, user.SessionVersion);
        Assert.True(user.RequiresPasswordChange);
    }

    [Fact]
    public async Task OProviderRecusa_OUtilizadorFicaComoEstava()
    {
        _provider.Resultado = UserProvisioningResetResult.ProviderFailure();

        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("0042"));

        Assert.Equal(ResetUserAccessResult.ProviderFailure, outcome.Result);
        var user = await LerAsync();
        Assert.Equal(3, user.SessionVersion);
        Assert.False(user.RequiresPasswordChange);
    }

    [Fact]
    public async Task ReporComSucesso_FechaAsSessoesEDevolveOPin()
    {
        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("0042"));

        Assert.Equal(ResetUserAccessResult.Success, outcome.Result);
        Assert.False(string.IsNullOrEmpty(outcome.TemporaryPassword));
        var user = await LerAsync();
        Assert.Equal(4, user.SessionVersion);
        Assert.True(user.RequiresPasswordChange);
    }

    private async Task<User> LerAsync()
    {
        _db.ChangeTracker.Clear();
        return await _db.Users.AsNoTracking().SingleAsync();
    }

    private sealed class FalhaAoGravar : SaveChangesInterceptor
    {
        public bool Ligada { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Ligada) throw new InvalidOperationException("falha-ao-gravar");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ProviderFalso : IUserProvisioningProvider
    {
        public UserProvisioningResetResult Resultado { get; set; } = UserProvisioningResetResult.Success();
        public bool Rebenta { get; set; }
        public int Resets { get; private set; }

        public Task<UserProvisioningCreateResult> CreateAsync(string email, string temporaryPassword, CancellationToken cancellationToken = default) =>
            Task.FromResult(UserProvisioningCreateResult.ProviderFailure());

        public Task<UserProvisioningResetResult> ResetAsync(string providerUserId, string temporaryPassword, CancellationToken cancellationToken = default)
        {
            Resets++;
            if (Rebenta) throw new HttpRequestException("sem resposta");
            return Task.FromResult(Resultado);
        }

        public Task<UserProvisioningDeleteResult> DeleteAsync(string providerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(UserProvisioningDeleteResult.ProviderFailure());
    }
}

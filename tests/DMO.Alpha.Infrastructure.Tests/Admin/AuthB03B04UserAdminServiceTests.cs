using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Admin;

/// <summary>
/// AUTH-B03 (DMO-655, A-0397) e AUTH-B04 (DMO-656, A-0398), ao nível do
/// serviço: o reset apaga o PIN do posto e a mudança do stand-by (nas duas
/// direções) sobe a versão da sessão.
/// </summary>
public sealed class AuthB03B04UserAdminServiceTests : IDisposable
{
    private readonly DmoDbContext _db;
    private readonly ProviderFalso _provider = new();
    private readonly UserAdminService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public AuthB03B04UserAdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
        _db.Users.Add(new User
        {
            Id = _userId,
            OperatorId = "0099",
            Name = "Reparador AuthB03B04",
            Email = "reparador@empresa.pt",
            ProviderUserId = "id-0099",
            SessionVersion = 1
        });
        _db.UserSettings.Add(new UserSetting { UserId = _userId, Key = PostoRiRegras.Chave, Value = "v1.100000.sal.hash" });
        _db.SaveChanges();
        _service = new UserAdminService(_db, _provider, new AdminAuthorizationService(_db, new AdminEmailSettings(_db)), new AdminEmailSettings(_db));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ResetUserAccessAsync_ApagaOPinDoPosto()
    {
        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("0099"));

        Assert.Equal(ResetUserAccessResult.Success, outcome.Result);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.UserSettings.AsNoTracking().AnyAsync(s => s.UserId == _userId && s.Key == PostoRiRegras.Chave));
    }

    [Fact]
    public async Task ResetUserAccessAsync_OProviderRecusa_OPinDoPostoVoltaAFicar()
    {
        _provider.Resultado = UserProvisioningResetResult.ProviderFailure();

        var outcome = await _service.ResetUserAccessAsync(new ResetUserAccess("0099"));

        Assert.Equal(ResetUserAccessResult.ProviderFailure, outcome.Result);
        _db.ChangeTracker.Clear();
        var pin = await _db.UserSettings.AsNoTracking()
            .Where(s => s.UserId == _userId && s.Key == PostoRiRegras.Chave)
            .Select(s => s.Value)
            .SingleOrDefaultAsync();
        Assert.Equal("v1.100000.sal.hash", pin);
    }

    [Fact]
    public async Task SetUserStandByAsync_Arquivar_SobeAVersaoDaSessao()
    {
        var outcome = await _service.SetUserStandByAsync(new SetUserStandBy("0099", true));

        Assert.Equal(SetUserStandByResult.Success, outcome.Result);
        _db.ChangeTracker.Clear();
        var user = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _userId);
        Assert.True(user.IsStandBy);
        Assert.Equal(2, user.SessionVersion);
    }

    [Fact]
    public async Task SetUserStandByAsync_ArquivarEReativar_SobeAVersaoNasDuasVezes()
    {
        await _service.SetUserStandByAsync(new SetUserStandBy("0099", true));
        var outcome = await _service.SetUserStandByAsync(new SetUserStandBy("0099", false));

        Assert.Equal(SetUserStandByResult.Success, outcome.Result);
        _db.ChangeTracker.Clear();
        var user = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _userId);
        Assert.False(user.IsStandBy);
        // Arquivar (1→2) e reativar (2→3): as DUAS transições sobem a
        // versão — uma cookie da versão 1 continua recusada depois de
        // reativar, sem passar pela versão 2 outra vez (AUTH-B04).
        Assert.Equal(3, user.SessionVersion);
    }

    [Fact]
    public async Task SetUserStandByAsync_SemMudarOEstado_NaoSobeAVersao()
    {
        var outcome = await _service.SetUserStandByAsync(new SetUserStandBy("0099", false));

        Assert.Equal(SetUserStandByResult.Success, outcome.Result);
        _db.ChangeTracker.Clear();
        var user = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _userId);
        Assert.Equal(1, user.SessionVersion);
    }

    private sealed class ProviderFalso : IUserProvisioningProvider
    {
        public UserProvisioningResetResult Resultado { get; set; } = UserProvisioningResetResult.Success();

        public Task<UserProvisioningCreateResult> CreateAsync(string email, string temporaryPassword, CancellationToken cancellationToken = default) =>
            Task.FromResult(UserProvisioningCreateResult.ProviderFailure());

        public Task<UserProvisioningResetResult> ResetAsync(string providerUserId, string temporaryPassword, CancellationToken cancellationToken = default) =>
            Task.FromResult(Resultado);

        public Task<UserProvisioningDeleteResult> DeleteAsync(string providerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(UserProvisioningDeleteResult.ProviderFailure());
    }
}

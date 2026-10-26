using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Authentication.Local;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// A primeira instalação (/instalar): cria o ADMIN só numa base sem ADMIN nem
/// palavras-passe, só com o código de instalação, e depois deixa de servir.
/// </summary>
public sealed class InstalacaoInicialTests
{
    private const string Codigo = "ABCD-2345";

    private static InstalacaoInicial Nova(out DmoDbContext db, out CodigoDeInstalacao codigo, string? fixo = Codigo, bool ativo = true)
    {
        db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Dmo:CodigoDeInstalacao"] = fixo })
            .Build();
        codigo = new CodigoDeInstalacao(config, NullLogger<CodigoDeInstalacao>.Instance);
        return new InstalacaoInicial(db, new AdminEmailSettings(db), codigo, new LoginProprio(ativo),
            NullLogger<InstalacaoInicial>.Instance);
    }

    [Fact]
    public async Task BaseVazia_ComOCodigo_CriaOAdmin_QueEntraComAPalavraPasse()
    {
        var instalacao = Nova(out var db, out _);

        Assert.True(await instalacao.PendenteAsync());
        var erro = await instalacao.InstalarAsync("abcd 2345", " Diogo@Fabrica.pt ", "4321", "4321");

        Assert.Null(erro);
        Assert.False(await instalacao.PendenteAsync());
        Assert.Equal("Diogo@Fabrica.pt", await new AdminEmailSettings(db).GetAdminEmailAsync());
        var login = await new LocalAuthenticationProvider(db, NullLogger<LocalAuthenticationProvider>.Instance)
            .AuthenticateAsync("diogo@fabrica.pt", "4321");
        Assert.True(login.Success);
        Assert.False(login.IsTemporaryPassword);
    }

    [Fact]
    public async Task CodigoErrado_NaoCriaNada()
    {
        var instalacao = Nova(out var db, out _);

        var erro = await instalacao.InstalarAsync("ZZZZ-9999", "diogo@fabrica.pt", "4321", "4321");

        Assert.Equal(InstalacaoInicial.CodigoErrado, erro);
        Assert.True(await instalacao.PendenteAsync());
        Assert.Empty(db.LocalCredentials);
    }

    [Theory]
    [InlineData("sem-arroba", "4321", "4321")]
    [InlineData("diogo@fabrica.pt", "4321", "4322")]
    [InlineData("diogo@fabrica.pt", "123", "123")]
    [InlineData("diogo@fabrica.pt", "", "")]
    public async Task DadosQueNaoServem_NaoCriamNada(string email, string palavraPasse, string confirmar)
    {
        var instalacao = Nova(out var db, out _);

        var erro = await instalacao.InstalarAsync(Codigo, email, palavraPasse, confirmar);

        Assert.NotNull(erro);
        Assert.True(await instalacao.PendenteAsync());
        Assert.Empty(db.LocalCredentials);
    }

    [Fact]
    public async Task ComAdminJaGravado_NaoHaInstalacao()
    {
        var instalacao = Nova(out var db, out _);
        await new AdminEmailSettings(db).TryClaimAdminEmailAsync("outro@fabrica.pt");

        Assert.False(await instalacao.PendenteAsync());
        Assert.Equal(InstalacaoInicial.JaFeita,
            await instalacao.InstalarAsync(Codigo, "diogo@fabrica.pt", "4321", "4321"));
        Assert.Empty(db.LocalCredentials);
    }

    [Fact]
    public async Task SemLoginProprio_NaoHaInstalacao()
    {
        var instalacao = Nova(out var db, out _, ativo: false);

        Assert.False(await instalacao.PendenteAsync());
        Assert.Equal(InstalacaoInicial.JaFeita,
            await instalacao.InstalarAsync(Codigo, "diogo@fabrica.pt", "4321", "4321"));
        Assert.Empty(db.LocalCredentials);
    }

    [Fact]
    public async Task ComPalavrasPasseNaBase_NaoHaInstalacao()
    {
        var instalacao = Nova(out var db, out _);
        await new LocalAuthenticationProvider(db, NullLogger<LocalAuthenticationProvider>.Instance)
            .CreateAsync("ana@fabrica.pt", "1234");

        Assert.False(await instalacao.PendenteAsync());
    }

    [Fact]
    public async Task DepoisDeFeita_OCodigoNaoVoltaAServir()
    {
        var instalacao = Nova(out _, out var codigo);
        Assert.Null(await instalacao.InstalarAsync(Codigo, "diogo@fabrica.pt", "4321", "4321"));

        Assert.False(codigo.Confere(Codigo));
    }

    [Fact]
    public void CodigoSorteado_MudaAoFimDeCincoEnganos()
    {
        Nova(out _, out var codigo, fixo: null);
        var antes = codigo.Valor;
        Assert.Matches("^[A-HJ-KM-NP-Z2-9]{4}-[A-HJ-KM-NP-Z2-9]{4}$", antes);

        for (var i = 0; i < CodigoDeInstalacao.EnganosAteTrocar; i++)
        {
            Assert.False(codigo.Confere("errado"));
        }

        Assert.NotEqual(antes, codigo.Valor);
        Assert.False(codigo.Confere(antes));
        Assert.True(codigo.Confere(codigo.Valor));
    }
}

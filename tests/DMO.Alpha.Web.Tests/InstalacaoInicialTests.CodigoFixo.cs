using DMO.Alpha.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// O código de instalação configurado (DMO_CODIGO_INSTALACAO) não muda com os
/// enganos; para não se poder tentar sem fim, depois de 5 enganos a
/// instalação espera um minuto (audit externo, 16/11/2026, DMO-758).
/// </summary>
public sealed class InstalacaoInicialCodigoFixoTests
{
    [Fact]
    public void CodigoFixo_CincoEnganos_EsperaUmMinutoAntesDeAceitar()
    {
        var relogio = new Relogio();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Dmo:CodigoDeInstalacao"] = "ABCD-2345" })
            .Build();
        var codigo = new CodigoDeInstalacao(config, NullLogger<CodigoDeInstalacao>.Instance, relogio);

        for (var i = 0; i < CodigoDeInstalacao.EnganosAteTrocar; i++)
        {
            Assert.False(codigo.Confere("errado"));
        }

        // O código certo também espera: é o que trava quem tenta adivinhar.
        Assert.False(codigo.Confere("ABCD-2345"));
        relogio.Avancar(CodigoDeInstalacao.EsperaComCodigoFixo - TimeSpan.FromSeconds(1));
        Assert.False(codigo.Confere("ABCD-2345"));

        relogio.Avancar(TimeSpan.FromSeconds(1));
        Assert.True(codigo.Confere("ABCD-2345"));
    }

    [Fact]
    public void CodigoFixo_MenosDeCincoEnganos_AceitaLogo()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Dmo:CodigoDeInstalacao"] = "ABCD-2345" })
            .Build();
        var codigo = new CodigoDeInstalacao(config, NullLogger<CodigoDeInstalacao>.Instance, new Relogio());

        for (var i = 0; i < CodigoDeInstalacao.EnganosAteTrocar - 1; i++)
        {
            Assert.False(codigo.Confere("errado"));
        }

        Assert.True(codigo.Confere("ABCD-2345"));
    }

    private sealed class Relogio : TimeProvider
    {
        private DateTimeOffset _agora = new(2026, 11, 16, 2, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }
}

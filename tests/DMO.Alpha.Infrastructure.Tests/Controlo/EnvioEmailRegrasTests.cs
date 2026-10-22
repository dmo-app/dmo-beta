using DMO.Alpha.Core.Controlo;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>
/// As regras do texto do envio por email (D9, 19/10/2026): que endereços
/// entram numa lista e como o modelo leva os dados da produção.
/// </summary>
public sealed class EnvioEmailRegrasTests
{
    [Theory]
    [InlineData("producao@fabrica.pt", true)]
    [InlineData("Produção <producao@fabrica.pt>", false)]
    [InlineData("producao@fabrica", false)]
    [InlineData("producao", false)]
    [InlineData("", false)]
    public void EmailValido_SoUmEnderecoCompleto(string email, bool valido) =>
        Assert.Equal(valido, EnvioEmailRegras.EmailValido(email));

    [Fact]
    public void Enderecos_SeparaPorVirgulaPontoEVirgulaEspacoELinha_SemRepetidos()
    {
        var (validos, invalidos) = EnvioEmailRegras.Enderecos("a@f.pt; b@f.pt,\nc@f.pt  A@F.PT x");

        Assert.Equal(new[] { "a@f.pt", "b@f.pt", "c@f.pt" }, validos);
        Assert.Equal(new[] { "x" }, invalidos);
    }

    [Fact]
    public void Preencher_TrocaAReferenciaAProducaoEAMaquina()
    {
        Assert.Equal("Peso 5447T173 · 202605 · B1",
            EnvioEmailRegras.Preencher("Peso {referencia} · {Producao} · {MAQUINA}", "5447T173", "202605", "B1"));
    }

    [Fact]
    public void Preencher_TrocaOPeso_ComAUnidade()
    {
        // Diogo, 21/10/2026: o assunto diz logo o peso que deu.
        Assert.Equal("Peso 5447T173 · B1 · 202605 · 245,30 g",
            EnvioEmailRegras.Preencher("Peso {referencia} · {maquina} · {producao} · {PESO}", "5447T173", "202605", "B1", 245.3m));
        Assert.Equal("Peso —", EnvioEmailRegras.Preencher("Peso {peso}", "5447T173", "202605", "B1"));
    }
}

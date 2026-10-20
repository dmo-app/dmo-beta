using DMO.Alpha.Core.Boquilhas;

namespace DMO.Alpha.Infrastructure.Tests.Boquilhas;

/// <summary>
/// A TRADUÇÃO dos movimentos para a língua do operador (a A5 repair — o
/// Owner apanhou «EntradaSemReparacao» cru no PDF, e o mesmo switch velho
/// servia o ecrã). O buraco real era de VOCABULÁRIO, não de acentos: o
/// <c>BoquilhasRegistoQuery</c> entrega o token de armazenamento
/// (<c>entrada_sem_reparacao</c>) mas o <c>BqToolHistoryQuery</c> entrega o
/// NOME do enum (<c>EntradaSemReparacao</c>) — e o switch só conhecia o
/// primeiro. Os nomes de uma só palavra passavam por sorte; o de três palavras
/// caía no fallback e ia cru. Estes testes fecham as DUAS vocabulários, porque
/// é a mistura delas que rebentava.
/// </summary>
public sealed class BqMovementLabelsTests
{
    [Theory]
    // O vocabulário do Histórico (o nome do enum) — era aqui que vazava.
    [InlineData("Saida", "Saída")]
    [InlineData("Entrada", "Entrada")]
    [InlineData("EntradaSemReparacao", "Entrada sem reparação")]
    // O vocabulário do registo / armazenamento (o token canónico).
    [InlineData("saida", "Saída")]
    [InlineData("entrada", "Entrada")]
    [InlineData("entrada_sem_reparacao", "Entrada sem reparação")]
    // Os dois sobreviveram ao bug por acaso; ficam provados para não regredirem.
    public void Of_TraduzAsDuasVocabularios(string valor, string esperado)
        => Assert.Equal(esperado, BqMovementLabels.Of(valor));

    [Fact]
    public void Of_NuncaDevolveOMnomeDoEnum()
    {
        // A queixa verbatim do Owner: o cru do enum nunca pode ser visto pelo
        // operador, no ecrã nem no papel.
        var saida = BqMovementLabels.Of(nameof(BqMovementType.Saida));
        var semReparacao = BqMovementLabels.Of(nameof(BqMovementType.EntradaSemReparacao));

        Assert.DoesNotContain("Saida", saida, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaSemReparacao", semReparacao, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BqMovementType.Saida, "Saída")]
    [InlineData(BqMovementType.Entrada, "Entrada")]
    [InlineData(BqMovementType.EntradaSemReparacao, "Entrada sem reparação")]
    public void Of_ComOTipoForte_FalaA_mesmaLinguaDaFolha(BqMovementType tipo, string esperado)
        => Assert.Equal(esperado, BqMovementLabels.Of(tipo));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Of_SemValor_DevolveOAtraceDaCasa(string? valor)
        => Assert.Equal("—", BqMovementLabels.Of(valor));

    [Theory]
    [InlineData("Retrocedida")]      // nome que não existe no enum
    [InlineData("9")]               // número perdido não se disfarça de rótulo
    [InlineData("ENTRADA_SEM_")]     // token mutilado
    public void Of_Desconhecido_NaoInventa(string valor)
    {
        // A casa é truthful: o que não se reconhece sai tal e qual, para o
        // operador ver que há dados fora do vocabulário — nunca texto inventado
        // por cima, nunca uma exceção no meio de um PDF.
        Assert.Equal(valor, BqMovementLabels.Of(valor));
    }

    [Fact]
    public void Of_ToleraEspacosADerroDaFonte()
        => Assert.Equal("Entrada sem reparação", BqMovementLabels.Of("  EntradaSemReparacao  "));
}

using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Web.Presentation;

namespace DMO.Alpha.Web.Tests.Controlo;

// O formulário da Comparação de Peso, fora da página (Diogo, 27/10/2026):
// as mesmas regras, testadas sem a página.
public sealed class PesoComparacaoFormularioTests
{
    [Fact]
    public void AsLinhasEmBrancoIgnoramSe_ASemMassaEAIlegivelFicamDeFora_AsOutrasComparamSe()
    {
        var lido = PesoComparacaoFormulario.Ler(
            7, "20,5", ["1", " ", "2", "3", ""], ["199,64", "", "", "abc", "-1"]);

        Assert.Null(lido.Recusa);
        Assert.Equal(20.5m, lido.Temperatura);
        Assert.Equal([("1", "199,64"), ("2", ""), ("3", "abc"), ("", "-1")], lido.Linhas);
        Assert.Equal([new("1", 199.64m), new("", -1m)], lido.Leituras!);
        Assert.Equal(
            [new(2, "2", "", PesoComparacaoMotivoIgnorada.SemMassa), new(3, "3", "abc", PesoComparacaoMotivoIgnorada.MassaIlegivel)],
            lido.Fora);
    }

    [Theory]
    [InlineData(null, "20", "1", "199", PesoComparacaoResultado.PesoInicialNaoSelecionado)]
    [InlineData(0, "20", "1", "199", PesoComparacaoResultado.PesoInicialNaoSelecionado)]
    [InlineData(7, "", "1", "199", PesoComparacaoResultado.TemperaturaIlegivel)]
    [InlineData(7, "vinte", "1", "199", PesoComparacaoResultado.TemperaturaIlegivel)]
    [InlineData(7, "20", "", "", PesoComparacaoResultado.SemLeituras)]
    [InlineData(7, "20", "1", "", PesoComparacaoResultado.MassaAguaIlegivel)]
    [InlineData(7, "20", "1", "abc", PesoComparacaoResultado.MassaAguaIlegivel)]
    public void SemNadaAComparar_ERecusadoComOMotivo(int? inicial, string temperatura, string cm, string massa, PesoComparacaoResultado recusa)
    {
        var lido = PesoComparacaoFormulario.Ler(inicial, temperatura, [cm], [massa]);

        Assert.Equal(recusa, lido.Recusa);
        Assert.Null(lido.Leituras);
    }

    [Fact]
    public void AsQueOBackendDeixaDeFora_ApontamALinhaDoFormulario()
    {
        var lido = PesoComparacaoFormulario.Ler(7, "20", ["1", "2", "3"], ["", "0", "199"]);

        // A 1.ª leitura enviada ao backend é a linha 2 do formulário.
        var aviso = lido.ComAsDoBackend([new PesoComparacaoLeituraIgnorada(1, "2", 0m, PesoComparacaoMotivoIgnorada.MassaNaoPositiva)]);

        Assert.Equal(
            [new(1, "1", "", PesoComparacaoMotivoIgnorada.SemMassa), new(2, "2", "0", PesoComparacaoMotivoIgnorada.MassaNaoPositiva)],
            aviso);
        Assert.Same(lido.Fora, lido.ComAsDoBackend([]));
    }

    [Fact]
    public void OAvisoAtravessaOPrgDoGuardar()
    {
        IReadOnlyList<PesoComparacaoLeituraFora> fora =
        [
            new(1, "1|a", "abc\n", PesoComparacaoMotivoIgnorada.MassaIlegivel),
            new(3, null, null, PesoComparacaoMotivoIgnorada.SemMassa)
        ];

        var codificado = PesoComparacaoFormulario.Codificar(fora);

        Assert.Equal(
            [new(1, "1/a", "abc", PesoComparacaoMotivoIgnorada.MassaIlegivel), new(3, null, null, PesoComparacaoMotivoIgnorada.SemMassa)],
            PesoComparacaoFormulario.Descodificar(codificado));
        Assert.Null(PesoComparacaoFormulario.Codificar([]));
        Assert.Empty(PesoComparacaoFormulario.Descodificar("x|Inventado|1|2\n2|99|1|2"));
    }

    [Fact]
    public void OPesoInicial_OPedidoSeEDaProducao_SenaoODasFerramentasAtuais_SenaoOMaisRecente()
    {
        var antigo = Opcao(1) with { IsCurrent = false };
        var atual = Opcao(2);
        var recente = Opcao(3) with { IsCurrent = false };

        Assert.Equal(1, PesoComparacaoFormulario.EscolherPesoInicial([antigo, atual, recente], 1));
        Assert.Equal(2, PesoComparacaoFormulario.EscolherPesoInicial([antigo, atual, recente], 99));
        Assert.Equal(3, PesoComparacaoFormulario.EscolherPesoInicial([antigo, recente], null));
        Assert.Null(PesoComparacaoFormulario.EscolherPesoInicial([], 1));
    }

    private static PesoComparacaoPesoOpcao Opcao(int id) =>
        new(id, "5447", "202501", "C2", "CM-" + id, "5447", "3", 2, 199m);
}

using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Tools;

namespace DMO.Alpha.Web.Tests.Ferramentas;

/// <summary>
/// Audit externo, 16/11/2026 (G1): o «Associar» do Registar Reparação não
/// tinha [EnvioUnico], e quatro envios iguais (a ligação caiu e carregou-se
/// de novo) faziam quatro listas. O mesmo envio faz uma lista só.
/// </summary>
public sealed partial class ReparacaoProgramadaPaginaTests
{
    [Fact]
    public async Task Associar_MesmoEnvioDuasVezes_FazUmaListaSo()
    {
        await NoArmazemAsync(m => m.EntradaAsync(new PedidoDeEntrada(Guid.NewGuid(), Cm, "1033", ArmazemOrigem.JaEstava, FerramentaEstadoTipo.PorReparar, null, null)));
        using var chefia = await LoginAsync("7409", DmoCapabilities.Ferramentas);
        var procura = await chefia.GetStringAsync($"{Pagina}?estado=PorReparar&referencia=5447RP");
        var token = HtmlFormHelpers.ExtractRequestVerificationToken(procura)!;
        var chave = EnvioUnicoPedidos.NovaChave();
        Task<HttpResponseMessage> Associar() => chefia.PostComChaveAsync($"{Pagina}?handler=Associar", chave,
            ("ferramenta", Cm), ("destino", ""), ("reparador", ReparadorId().ToString()), ("estado", "PorReparar"),
            ("__RequestVerificationToken", token));

        using var primeiro = await Associar();
        using var repetido = await Associar();

        Assert.Equal(HttpStatusCode.Redirect, primeiro.StatusCode);
        Assert.Equal(primeiro.Headers.Location, repetido.Headers.Location);
        Assert.Single(NaBase(db => db.ReparacaoProgramadaListas.ToList()));
    }
}

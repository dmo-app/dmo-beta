namespace DMO.Alpha.Web.Tests;

public sealed class MensagemFabricoSapTests
{
    // DMO-724 / A-0496 (QD253-02): o número de produções retiradas chega ao ecrã.
    [Fact]
    public void AsRetiradas_AparecemNaMensagem()
    {
        Assert.Equal("Fabrico lido do SAP: 2 produções, nenhuma nova. 1 produção retirada (já não vem no SAP).",
            DMO.Alpha.Web.Pages.Ferramentas.DefinicoesModel.MensagemFabricoSap(new DMO.Alpha.Core.Tools.ImportarFabricoSapOutcome(2, 0, 1)));
        Assert.Equal("Fabrico lido do SAP: 1 produção nova.",
            DMO.Alpha.Web.Pages.Ferramentas.DefinicoesModel.MensagemFabricoSap(new DMO.Alpha.Core.Tools.ImportarFabricoSapOutcome(1, 1, 0)));
    }
}

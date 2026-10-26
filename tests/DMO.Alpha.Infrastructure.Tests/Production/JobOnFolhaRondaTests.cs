using DMO.Alpha.Core.Production;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>Ronda de erros de 26/10/2026 (grupo Job On), nas regras da folha.</summary>
public sealed class JobOnFolhaRondaTests
{
    // J7: guardar sem mexer não regista mudanças só por o número vir com ponto e duas casas.
    [Fact]
    public void Alteracoes_SoAFormaDoNumero_NaoEMudanca()
    {
        var gravada = JobOnFolhaLida.VaziaDe(1) with
        {
            Valores = JobOnFolhaCampos.Todos.ToDictionary(c => c.Chave, c => c.Chave switch
            {
                "cab_peso" => "12.5",
                "cm_pata" => "10,123",
                "cm_notas" => "Rodar 1/4",
                _ => (string?)null
            }, StringComparer.Ordinal)
        };
        var enviada = gravada.Valores.ToDictionary(v => v.Key, v => v.Key switch
        {
            "cab_peso" => "12.50",
            "cm_pata" => "10.12",
            _ => v.Value
        }, StringComparer.Ordinal);

        Assert.Empty(JobOnFolhaRegras.Alteracoes(gravada, enviada, null));

        enviada["cab_peso"] = "13";
        Assert.Equal(["Cabeçalho · Peso (g)"], JobOnFolhaRegras.Alteracoes(gravada, enviada, null));
    }
}

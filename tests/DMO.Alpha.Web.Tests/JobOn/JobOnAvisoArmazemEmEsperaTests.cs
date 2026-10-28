using DMO.Alpha.Core.Production;
using DMO.Alpha.Web.Pages.JobOn;
using Microsoft.Extensions.Configuration;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Os avisos «… não está registada no Armazém e não tem estado» do Job On
/// mostram-se por omissão (Diogo, 28/10/2026: sem registo no Armazém →
/// «Sem registo» + aviso; antes estavam em espera desde 22/10). Escondem-se
/// só com Dmo:Avisos:ArmazemSemRegisto = false. Os outros avisos não mudam.
/// </summary>
public sealed class JobOnAvisoArmazemEmEsperaTests
{
    private static readonly JobOnToolFacts Facto = new(
        "t1", "CM", "5447T173", "3", null, null, null, null, null, null, null, null, [], null,
        "CM 5447T173 / lote 3 também está associado a 202601 (B1). Esta informação não impede a sua escolha.")
    {
        AvisoArmazem = "CM 5447T173 / lote 3 não está registada no Armazém e não tem estado."
    };

    [Fact]
    public void SemOInterruptor_OAvisoDoArmazemNaoAparece_EOsOutrosSim()
    {
        var avisos = IndexModel.AvisosDasFerramentas(Facto, comAvisoArmazem: false);

        Assert.DoesNotContain(avisos, a => a.Texto.Contains("não está registada no Armazém", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Texto.Contains("também está associado", StringComparison.Ordinal));
    }

    [Fact]
    public void ComOInterruptor_OAvisoDoArmazemVolta()
    {
        var avisos = IndexModel.AvisosDasFerramentas(Facto, comAvisoArmazem: true);

        Assert.Contains(avisos, a => a.Texto.Contains("não está registada no Armazém", StringComparison.Ordinal));
    }

    [Fact]
    public void PorOmissao_OAvisoDoArmazemMostraSe()
    {
        var raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DMO.Alpha.Web"));
        Assert.True(File.Exists(Path.Combine(raiz, "appsettings.json")));
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(raiz, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(raiz, "appsettings.Production.json"), optional: true)
            .Build();

        Assert.True(IndexModel.AvisoArmazemPorOmissao);
        Assert.True(config.GetValue(IndexModel.AvisoArmazemChave, IndexModel.AvisoArmazemPorOmissao));
    }
}

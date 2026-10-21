using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Infrastructure.ReparacaoInterna;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A Consulta da Reparação Interna no PostgreSQL (peça 3): os filtros, a
/// referência pelo início, a ferramenta do contexto e o filtro «Quem»
/// traduzem-se na base real (o InMemory aceita expressões que o Npgsql
/// recusa), e as anuladas ficam de fora.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ReparacaoInternaConsulta_OsFiltrosTraduzemSeNaBase()
    {
        SkipWithoutConnection();
        var cenario = await ReparacaoCenarioAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            await using (var db = CreateContext())
            {
                db.ReparacoesInternas.AddRange(
                    Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, "1"),
                    Linha(cenario, ReparacaoTipo.Mf, null, cenario.MfId, "2"));
                var anulada = Linha(cenario, ReparacaoTipo.Cm, cenario.CmId, null, "3");
                anulada.AnuladaEmUtc = DateTime.UtcNow;
                db.ReparacoesInternas.Add(anulada);
                await db.SaveChangesAsync();
            }

            await using var leitura = CreateContext();
            var consulta = new ReparacoesInternasConsulta(leitura);
            var todas = await consulta.ConsultarAsync(new RiConsultaFiltro(
                MachineId: cenario.MachineId, De: hoje, Ate: hoje, Referencia: cenario.Marca[..4].ToLowerInvariant()), null);
            Assert.Equal(new[] { "2", "1" }, todas.Items.Select(l => l.Numero));
            Assert.Equal(2, todas.TotalCount);
            var mf = todas.Items[0];
            Assert.Equal((cenario.Marca, "1", cenario.JobOnId), (mf.Maquina, mf.Producao, mf.JobOnId));
            Assert.Equal(new RiFerramenta(cenario.MfId, cenario.Marca, "1"), mf.Ferramenta);
            Assert.Equal(new RiFerramenta(cenario.CmId, cenario.Marca, "1"), todas.Items[1].Ferramenta);

            var cm = await consulta.ConsultarAsync(new RiConsultaFiltro(JobOnId: cenario.JobOnId, Tipo: ReparacaoTipo.Cm), null);
            Assert.Equal("1", Assert.Single(cm.Items).Numero);
            // Sem utilizador (como o ADMIN), a pessoa não filtra ninguém desta máquina.
            Assert.Empty((await consulta.ConsultarAsync(new RiConsultaFiltro(MachineId: cenario.MachineId, PessoaId: Guid.NewGuid()), null)).Items);
            Assert.NotNull(await consulta.PessoasAsync());
        }
        finally
        {
            await LimparReparacaoAsync(cenario);
        }
    }
}

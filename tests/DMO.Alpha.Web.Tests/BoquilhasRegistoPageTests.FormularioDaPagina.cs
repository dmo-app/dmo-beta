using System.Net;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Estudo dos testes, fase 1 (19/10/2026): os outros testes do Registo
/// enviam os campos com os nomes que o próprio teste escolhe, por isso um
/// nome trocado no formulário deixava o CI verde e a fábrica sem gravar
/// movimentos. Aqui o envio é o do formulário que a página desenha
/// (EnvioDoFormulario), só com o que o operador preenche.
/// </summary>
public partial class BoquilhasRegistoPageTests
{
    [Fact]
    public async Task MovimentoPeloFormularioDaPagina_GravaAQuantidadeEAMaquinaNaBQ()
    {
        await SeedToolAsync("BQ-FORM-4741", "RGT741", "3");
        int b5Id;
        var trace = await SeedTraceAsync("BQ-FORM-4741");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var b5 = new Machine { Code = "B5" };
            db.Machines.AddRange(new Machine { Code = "B3" }, b5);
            await db.SaveChangesAsync();
            b5Id = b5.Id;
            db.ToolMachineLinks.Add(new DMO.Alpha.Core.Tools.ToolMachineLink { ToolId = "BQ-FORM-4741", MachineId = b5Id });
            await db.SaveChangesAsync();
        }
        SeedUser("4741", "Registo Formulário", "provider-registo-formulario", "RegistoForm1!");
        using var client = await LoginClientAsync("4741", "RegistoForm1!");

        // O operador abre o registo pela linha B5 do painel lateral (a
        // máquina segue escondida, sem o campo «Máquina em uso») e escreve a
        // quantidade; a Saída já vem marcada e a data e hora com o agora.
        var html = await GetPageHtmlAsync(client, $"{TraceUrl(trace.Id)}&movement_machine_id={b5Id}");
        var (acao, campos) = EnvioDoFormulario.Preencher(html, ("quantity", "7"));
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(TraceUrl(trace.Id), response.Headers.Location?.OriginalString);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var gravado = await db.BqMovements.AsNoTracking().Include(m => m.Trace).SingleAsync();
            Assert.Equal(trace.Id, gravado.BqRepairTraceId);
            Assert.Equal("BQ-FORM-4741", gravado.Trace.ToolId);
            Assert.Equal(BqMovementType.Saida, gravado.Type);
            Assert.Equal(7, gravado.Quantity);
            Assert.Equal("B5", gravado.MachineCodeFrozen);
        }
    }
}

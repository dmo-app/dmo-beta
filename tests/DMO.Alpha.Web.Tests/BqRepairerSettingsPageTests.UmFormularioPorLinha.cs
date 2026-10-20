using System.Net;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

/// <summary>
/// Deep sweep, 18/10/2026: o envio de uma linha das Definições das
/// Boquilhas leva só essa linha. Os pedidos são os que o browser faz com a
/// página desenhada (EnvioDoFormulario): o formulário do campo mudado, com
/// os valores que a página tinha ao abrir.
/// </summary>
public sealed partial class BqRepairerSettingsPageTests
{
    private async Task<string> EnviarCampoAsync(HttpClient client, string html, string idDoCampo, string valorNovo)
    {
        var (acao, campos) = EnvioDoFormulario.Mudar(html, idDoCampo, valorNovo);
        using var response = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location?.OriginalString ?? string.Empty;
    }

    private async Task<IReadOnlyDictionary<string, string>> RotasAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        return await db.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey.StartsWith("Boquilhas:Repairer"))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
    }

    [Fact]
    public async Task MachineRoute_DuasPessoasEmMaquinasDiferentes_FicamAsDuasRotas()
    {
        // A1-M1: as rotas das seis máquinas iam num só formulário. Quem
        // mudava a B1 com a página aberta de antes enviava também a B2 como
        // estava ao abrir, e apagava a rota que outra pessoa tinha gravado
        // entretanto. Um formulário por máquina: cada envio leva só a sua.
        var oficina = await SeedRepairerAsync("Oficina Norte");
        var moldes = await SeedRepairerAsync("Moldes Silva");
        using var a = await LoginBoquilhasAsync("4721");
        using var b = await LoginBoquilhasAsync("4722");

        // As duas pessoas abrem a página antes de qualquer rota gravada.
        var paginaA = await GetPageHtmlAsync(a);
        var paginaB = await GetPageHtmlAsync(b);

        Assert.Contains("save=ok-rota", await EnviarCampoAsync(b, paginaB, "machine-route-B2", $"B2|{moldes}"));
        Assert.Contains("save=ok-rota", await EnviarCampoAsync(a, paginaA, "machine-route-B1", $"B1|{oficina}"));

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Boquilhas:RepairerB1"] = oficina.ToString(),
                ["Boquilhas:RepairerB2"] = moldes.ToString()
            },
            await RotasAsync());
        Assert.Single(EnvioDoFormulario.Mudar(paginaA, "machine-route-B1", $"B1|{oficina}").Campos,
            c => c.Nome == "machine_code");

        // Depois de gravar, A vê também a rota que B gravou.
        var depois = await GetPageHtmlAsync(a);
        Assert.Contains($"value=\"B1|{oficina}\" selected", depois);
        Assert.Contains($"value=\"B2|{moldes}\" selected", depois);

        // «— sem reparador —» continua a limpar a rota, só dessa máquina.
        Assert.Contains("save=ok-rota", await EnviarCampoAsync(a, paginaA, "machine-route-B1", "B1|"));
        Assert.Equal(
            new Dictionary<string, string> { ["Boquilhas:RepairerB2"] = moldes.ToString() },
            await RotasAsync());
    }

    [Fact]
    public async Task EditRepairer_EnvioDeRecurso_GravaONomeDoReparadorEditado()
    {
        // B-boquilhas-3: o nome grava no próprio valor (pedido JSON); se a
        // ligação falha, o script envia o formulário do campo. Com um só
        // formulário para a tabela, o envio levava todos os reparadores, o
        // servidor gravava o primeiro com o nome que já tinha, a página dizia
        // «Definição gravada.» e o nome editado não mudava. Um formulário por
        // reparador: o envio leva só o editado.
        var primeiro = await SeedRepairerAsync("AG Rep 1");
        var moldes = await SeedRepairerAsync("Moldes Silva");
        var ultimo = await SeedRepairerAsync("Vidroart");
        using var client = await LoginBoquilhasAsync();

        var html = await GetPageHtmlAsync(client);
        Assert.Contains("save=ok-reparador",
            await EnviarCampoAsync(client, html, $"bq-reparador-{moldes}", "Moldes Silva Lda"));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var nomes = await db.BqRepairers.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name);
            Assert.Equal("Moldes Silva Lda", nomes[moldes]);
            Assert.Equal("AG Rep 1", nomes[primeiro]);
            Assert.Equal("Vidroart", nomes[ultimo]);
        }
        Assert.Equal(
            new[] { ("repairer_id", moldes.ToString()), ("name", "Moldes Silva Lda") },
            EnvioDoFormulario.Mudar(html, $"bq-reparador-{moldes}", "Moldes Silva Lda").Campos
                .Where(c => c.Nome != "__RequestVerificationToken")
                .Select(c => (c.Nome, c.Valor))
                .ToArray());
    }
}

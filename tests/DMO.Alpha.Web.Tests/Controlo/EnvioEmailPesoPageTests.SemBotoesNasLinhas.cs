using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests.Controlo;

public sealed partial class EnvioEmailPesoPageTests
{
    // Diogo, 27/10/2026: «não quero botões em rows por todo o lado». Nos
    // modelos do email (Controlo › Definições › Email), o nome deixou de ser
    // um botão: um clique escolhe a linha (rádio escondido), o Enter ou dois
    // cliques abrem a janela do modelo (data-dmo-abre-janela) e UM «Abrir»
    // no canto abre a do escolhido. Sem escolha, o «Abrir» fica desativado.
    [Fact]
    public async Task Modelos_UmaLinhaEscolhe_EUmSoAbrirNoCanto()
    {
        var (listaId, primeiro) = SeedModelo();
        int segundo;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var modelo = new EmailModelo { Nome = "Qualidade", Assunto = "Peso {producao}", Corpo = "Corpo", ListaId = listaId };
            db.EmailModelos.Add(modelo);
            db.SaveChanges();
            segundo = modelo.Id;
        }
        using var client = await LoginAsync("4891");

        var html = await GetAsync(client, "/Definicoes?secao=controlo&sub=email");
        var cartao = Regex.Match(html, "<section id=\"ctlDefModelos\"[\\s\\S]*?</section>").Value;
        Assert.NotEmpty(cartao);

        // O «Abrir» no canto, desativado até haver uma linha escolhida.
        var canto = Regex.Match(cartao, "<div class=\"dmo-card-topo-acoes\" data-dmo-for=\"ctlDefModelosLista\">[\\s\\S]*?</div>").Value;
        var abrir = Regex.Match(canto, "<button[^>]*>Abrir</button>").Value;
        Assert.Contains("data-dmo-precisa-escolha data-dmo-dialog-modelo=\"ctl-modelo-{id}\" disabled>", abrir);
        Assert.DoesNotContain("data-dmo-dialog=", abrir);
        Assert.Contains("data-dmo-dialog=\"ctl-modelo-novo\">Novo modelo</button>", canto);

        // Nas linhas, só o «×» de apagar; cada linha abre a janela do seu modelo.
        var tabela = Regex.Match(cartao, "<table class=\"dmo-table\" id=\"ctlDefModelosLista\" data-dmo-list=\"Modelos de email\"[\\s\\S]*?</table>").Value;
        Assert.NotEmpty(tabela);
        var semJanelas = Regex.Replace(tabela, "<dialog[\\s\\S]*?</dialog>", "");
        var nasLinhas = Regex.Matches(semJanelas, "<button[^>]*>").Select(m => m.Value).ToList();
        Assert.Equal(2, nasLinhas.Count);
        Assert.All(nasLinhas, b => Assert.Contains("class=\"ctl-def-x\"", b));
        foreach (var (id, nome) in new[] { (primeiro, "Peso para a produção"), (segundo, "Qualidade") })
        {
            Assert.Matches($"<tr data-dmo-modelo-email=\"{id}\" data-dmo-row data-id=\"{id}\" data-dmo-abre-janela=\"ctl-modelo-{id}\" aria-selected=\"false\"[^>]*>"
                + $"\\s*<td><input type=\"radio\" name=\"ctl_modelo_escolhido\" value=\"{id}\" aria-label=\"Escolher o modelo {nome}\" /></td>\\s*<td>{nome}</td>", tabela);
            Assert.Contains($"<dialog id=\"ctl-modelo-{id}\" class=\"dmo-dialog\"", cartao);
        }
    }

    // Com um só modelo, a linha já vem escolhida e o «Abrir» abre-o.
    [Fact]
    public async Task Modelos_UmSoModelo_VemEscolhido()
    {
        var (_, modeloId) = SeedModelo();
        using var client = await LoginAsync("4892");

        var html = await GetAsync(client, "/Definicoes?secao=controlo&sub=email");
        Assert.Contains($"<button type=\"button\" class=\"dmo-button\" id=\"ctlDefModeloAbrir\" data-dmo-precisa-escolha data-dmo-dialog-modelo=\"ctl-modelo-{{id}}\" data-dmo-dialog=\"ctl-modelo-{modeloId}\">Abrir</button>", html);
        Assert.Matches($"<tr data-dmo-modelo-email=\"{modeloId}\"[^>]*class=\"selected\" aria-selected=\"true\"", html);
        Assert.Contains($"<input type=\"radio\" name=\"ctl_modelo_escolhido\" value=\"{modeloId}\" checked=\"checked\"", html);
    }
}

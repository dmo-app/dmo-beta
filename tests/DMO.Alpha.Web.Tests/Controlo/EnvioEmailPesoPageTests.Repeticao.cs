using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using DMO.Alpha.Web.Services;

namespace DMO.Alpha.Web.Tests.Controlo;

public sealed partial class EnvioEmailPesoPageTests
{
    [Fact]
    public async Task Email_SemEspacoParaNovosFicheiros_NaoPreparaOutro_MasPermiteRepetirOPrimeiro()
    {
        var producao = await SeedProducaoAsync();
        var (lista, modelo) = SeedModelo();
        using var primeiroClient = await LoginAsync("4835");
        await CriarPesoAsync(primeiroClient, producao);
        using var limitada = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new EnviosGravados(orcamentoDeFicheiros: 1))));
        using var client = limitada.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await GetAsync(client, "/login");
        using var entrou = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(
            ("identifier", "4835@dmo.local"), ("password", "Envio4835!"),
            ("__RequestVerificationToken", HtmlFormHelpers.ExtractRequestVerificationToken(login)!)));
        Assert.Equal(HttpStatusCode.Redirect, entrou.StatusCode);
        var html = await GetAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        var (acao, campos) = EnvioDoFormulario.Enviar(html,
            [("resumoEnvioModelo", modelo.ToString()), ("resumoEnvioLista", lista.ToString())]);
        async Task<HttpResponseMessage> Enviar(string chave)
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, acao) { Content = EnvioDoFormulario.Corpo(campos) };
            pedido.Headers.Add(EnvioUnicoPedidos.Cabecalho, chave);
            return await client.SendAsync(pedido);
        }
        var chave = EnvioUnicoPedidos.NovaChave();
        using var primeira = await Enviar(chave);
        using var semEspaco = await Enviar(EnvioUnicoPedidos.NovaChave());
        using var repetida = await Enviar(chave);
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, semEspaco.StatusCode);
        Assert.Contains("Não foi preparado nenhum email", await semEspaco.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(await primeira.Content.ReadAsByteArrayAsync(), await repetida.Content.ReadAsByteArrayAsync());
        Assert.Single(Envios());
    }

    private string VersaoDaLista(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var lista = scope.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Data.DmoDbContext>().EmailListas.Single(l => l.Id == id);
        return DMO.Alpha.Core.Controlo.EnvioEmailRegras.Versao(lista);
    }

    [Fact]
    public async Task Lista_RepetirARemocaoNaoRetiraAPessoaQuePassouParaAMesmaPosicao()
    {
        var (lista, _) = SeedModelo();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Data.DmoDbContext>();
            db.EmailListas.Single(l => l.Id == lista).Destinatarios += "\nterceira@fabrica.pt";
            db.SaveChanges();
        }
        using var client = await LoginAsync("4833");
        var html = await GetAsync(client, "/Definicoes?secao=controlo&sub=email");
        var (acao, campos) = EnvioDoFormulario.Enviar(html, [], idDoBotao: $"ctl-tirar-{lista}-0");
        using var primeira = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        using var repetida = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));
        Assert.Contains("envio=ok-lista", primeira.Headers.Location!.ToString());
        Assert.Contains("envio=Conflito", repetida.Headers.Location!.ToString());
        using var check = _factory.Services.CreateScope();
        Assert.Equal("chefe@fabrica.pt\nterceira@fabrica.pt", check.ServiceProvider
            .GetRequiredService<DMO.Alpha.Infrastructure.Data.DmoDbContext>().EmailListas.Single(l => l.Id == lista).Destinatarios);
    }

    [Fact]
    public async Task Lista_SemAVersaoDoFormularioNaoGrava()
    {
        var (lista, _) = SeedModelo();
        using var client = await LoginAsync("4834");
        var html = await GetAsync(client, "/Definicoes?secao=controlo&sub=email");
        var (acao, campos) = EnvioDoFormulario.Mudar(html, $"ctl-lista-nome-{lista}", "Sem versão");
        using var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos.Where(c => c.Nome != "versao").ToArray()));
        Assert.Contains("envio=Conflito", resposta.Headers.Location!.ToString());
        using var check = _factory.Services.CreateScope();
        Assert.Equal("Produção", check.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Data.DmoDbContext>()
            .EmailListas.Single(l => l.Id == lista).Nome);
    }

    [Fact]
    public async Task Lista_UmFormularioAntigoNaoSubstituiAAlteracaoDeOutraAba()
    {
        var (lista, _) = SeedModelo();
        using var client = await LoginAsync("4832");
        var html = await GetAsync(client, "/Definicoes?secao=controlo&sub=email");
        var (acao, campos) = EnvioDoFormulario.Mudar(html, $"ctl-lista-nome-{lista}", "Nome da aba antiga");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Data.DmoDbContext>();
            db.EmailListas.Single(l => l.Id == lista).Nome = "Nome da outra aba";
            db.SaveChanges();
        }

        using var resposta = await client.PostAsync(acao, EnvioDoFormulario.Corpo(campos));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("envio=Conflito", resposta.Headers.Location!.ToString());
        var atual = await GetAsync(client, resposta.Headers.Location.ToString());
        Assert.Contains("A lista mudou entretanto", atual);
        using var check = _factory.Services.CreateScope();
        Assert.Equal("Nome da outra aba", check.ServiceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Data.DmoDbContext>()
            .EmailListas.Single(l => l.Id == lista).Nome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Email_OMesmoEnvioDevolveOMesmoFicheiroERegistaUmaVez_OutraChavePreparaOutro(bool simultaneo)
    {
        var producao = await SeedProducaoAsync();
        var (lista, modelo) = SeedModelo();
        using var client = await LoginAsync("4831");
        await CriarPesoAsync(client, producao);
        var html = await GetAsync(client, $"/Controlo/Resumo?jobon_id={producao.JobOnId}");
        var (acao, campos) = EnvioDoFormulario.Enviar(html,
            [("resumoEnvioModelo", modelo.ToString()), ("resumoEnvioLista", lista.ToString())]);
        async Task<HttpResponseMessage> Enviar(string chave)
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, acao) { Content = EnvioDoFormulario.Corpo(campos) };
            pedido.Headers.Add(EnvioUnicoPedidos.Cabecalho, chave);
            return await client.SendAsync(pedido);
        }

        var chave = EnvioUnicoPedidos.NovaChave();
        var respostas = simultaneo
            ? await Task.WhenAll(Enviar(chave), Enviar(chave))
            : [await Enviar(chave), await Enviar(chave)];
        try
        {
            Assert.All(respostas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Single(Envios());
            Assert.Equal("message/rfc822", respostas[0].Content.Headers.ContentType?.MediaType);
            Assert.Equal(respostas[0].Content.Headers.ContentDisposition?.FileName,
                respostas[1].Content.Headers.ContentDisposition?.FileName);
            Assert.Equal(await respostas[0].Content.ReadAsByteArrayAsync(), await respostas[1].Content.ReadAsByteArrayAsync());
            using var outro = await Enviar(EnvioUnicoPedidos.NovaChave());
            Assert.Equal(HttpStatusCode.OK, outro.StatusCode);
            Assert.Equal(2, Envios().Count);
        }
        finally
        {
            foreach (var resposta in respostas) resposta.Dispose();
        }
    }
}

using System.Text;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Controlo;

/// <summary>A01: o papel e o anexo mostram a decisão aplicável à folha selecionada.</summary>
public sealed class PesoAprovacaoDocumentoTests : IDisposable
{
    private static readonly DateTime DecididoEm = new(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
    private readonly DmoDbContext _db;
    private readonly ControloApprovalHandler _approval;
    private readonly PesoPdfDocumentBuilder _builder;
    private readonly int _controloId;
    private readonly int _pesoAntigoId;

    public PesoAprovacaoDocumentoTests()
    {
        _db = new DmoDbContext(new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Database.EnsureCreated();
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.AddRange(
            new Tool { ToolId = "CM1", Type = ToolType.Cm, Reference = "5447", Lot = "1" },
            new Tool { ToolId = "CM2", Type = ToolType.Cm, Reference = "5447", Lot = "2" },
            new Tool { ToolId = "BQ1", Type = ToolType.Bq, Reference = "T173", Lot = "1" });
        var production = new JobOn { Machine = machine, Reference = "5447T173", ProductionNumber = "2026101" };
        _db.JobOns.Add(production);
        _db.SaveChanges();
        var controlo = new DMO.Alpha.Core.Controlo.Controlo { JobOnId = production.Id };
        _db.Controlos.Add(controlo);
        _db.CmContexts.Add(new CmContext { JobOnId = production.Id, ToolId = "CM1" });
        _db.BqContexts.Add(new BqContext { JobOnId = production.Id, ToolId = "BQ1" });
        _db.SaveChanges();
        _controloId = controlo.Id;
        var antigo = NovoPeso("CM1", DecididoEm.AddMinutes(-1));
        _db.Pesos.Add(antigo);
        _db.ControloDecisoes.Add(new ControloDecisao
        {
            ControloId = _controloId, Parte = ControloParte.Peso,
            Aprovado = true, OperatorId = "4802", EmUtc = DecididoEm
        });
        _db.SaveChanges();
        _pesoAntigoId = antigo.Id;
        _approval = new ControloApprovalHandler(_db);
        var pesos = new PesoQuery(_db);
        _builder = new PesoPdfDocumentBuilder(pesos, new ProductionContextQuery(_db),
            new PesoComparisonQuery(_db, pesos), _approval, new PesoCapSupportQuery(_db),
            new PesoProducaoAnteriorQuery(_db), new PesoPdfGenerator());
    }

    public void Dispose() => _db.Dispose();

    private Peso NovoPeso(string cm, DateTime? criadoEm) => new()
    {
        ControloId = _controloId, CmToolId = cm, BqToolId = "BQ1", Process = "NNPB",
        WaterTemperatureC = 20, GlassDensityGramsPerCm3 = 2.5m,
        VolumeMarisaCm3 = 10m, VolumePuncaoCm3 = 30m, RegistadoEmUtc = criadoEm
    };

    private int TrocarECriarPeso(DateTime? criadoEm)
    {
        _db.CmContexts.Single().ToolId = "CM2";
        var novo = NovoPeso("CM2", criadoEm);
        _db.Pesos.Add(novo);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return novo.Id;
    }

    private async Task<string> TextoAsync(int? controloId, int? pesoId)
    {
        var documento = await _builder.BuildAsync(controloId, pesoId, null, null, "Auditoria", default);
        Assert.NotNull(documento);
        return PdfText.Of(documento.Bytes);
    }

    [Fact]
    public async Task PesoNovoNaoHerdaAAprovacaoNoPdf_ENoHistoricoAPrimeiraFolhaContinuaAprovada()
    {
        Assert.Contains("Aprovado por Operador 4802", await TextoAsync(_controloId, _pesoAntigoId));
        var novoId = TrocarECriarPeso(DecididoEm.AddMinutes(1));

        var novo = await TextoAsync(_controloId, novoId);
        Assert.Contains("Por aprovar", novo);
        Assert.DoesNotContain("Aprovado por Operador 4802", novo);
        Assert.Contains("Por aprovar", await TextoAsync(_controloId, null));
        Assert.Contains("Por aprovar", await TextoAsync(null, novoId));
        Assert.Contains("Aprovado por Operador 4802", await TextoAsync(null, _pesoAntigoId));
        Assert.Null((await _approval.GetAsync(_controloId))!.ApprovedByOperatorId);
        Assert.True(Assert.Single(await _db.ControloDecisoes.AsNoTracking().ToListAsync()).Aprovado);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task AprovacaoSoCobrePesosRegistadosAteAoInstanteDaDecisao(int segundos, bool aprovado)
    {
        var novoId = TrocarECriarPeso(DecididoEm.AddSeconds(segundos));
        var estado = await _approval.PesoAsync(_controloId, novoId);
        Assert.NotNull(estado);
        Assert.Equal(aprovado, estado.ApprovedByOperatorId is not null);
    }

    [Fact]
    public async Task UmaLeituraNovaNaoInvalidaAAprovacaoDaFolha()
    {
        _db.PesoMeasurements.Add(new PesoMeasurement
        {
            PesoId = _pesoAntigoId, RowPosition = 1, CmSubject = "1",
            WaterWeightGrams = 250m, RegistadoEmUtc = DecididoEm.AddMinutes(2)
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        Assert.Contains("Aprovado por Operador 4802", await TextoAsync(_controloId, _pesoAntigoId));
    }

    [Fact]
    public async Task ReaprovarDepoisDoPesoNovoAtualizaOPdf()
    {
        var novoId = TrocarECriarPeso(DecididoEm.AddMinutes(1));
        Assert.Contains("Por aprovar", await TextoAsync(_controloId, novoId));
        _db.ControloDecisoes.Single().EmUtc = DecididoEm.AddMinutes(2);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        Assert.Contains("Aprovado por Operador 4802", await TextoAsync(_controloId, novoId));
    }

    [Fact]
    public async Task NaoAprovarMantemSeNoPdfDepoisDeUmPesoNovo()
    {
        var decisao = _db.ControloDecisoes.Single();
        decisao.Aprovado = false;
        decisao.Nota = "Repetir a pesagem.";
        await _db.SaveChangesAsync();
        var novoId = TrocarECriarPeso(DecididoEm.AddMinutes(1));
        var texto = await TextoAsync(_controloId, novoId);
        Assert.Contains("Não aprovado por Operador 4802", texto);
        Assert.Contains("Repetir a pesagem.", texto);
    }

    [Fact]
    public async Task OAnexoDoEmailTambemFicaPorAprovar()
    {
        Assert.Contains("Aprovado por Operador 4802", await TextoAsync(_controloId, _pesoAntigoId));
        var novoId = TrocarECriarPeso(DecididoEm.AddMinutes(1));
        var lista = new EmailLista { Nome = "Qualidade", Destinatarios = "qualidade@example.com" };
        var modelo = new EmailModelo
        {
            Nome = "Peso", Remetente = "controlo@example.com", Assunto = "Peso {producao}", Corpo = "Segue o Peso."
        };
        _db.EmailListas.Add(lista);
        _db.EmailModelos.Add(modelo);
        await _db.SaveChangesAsync();

        var (resultado, email) = await new EnviarPesoPorEmailHandler(_db, _builder)
            .PrepararAsync(_controloId, modelo.Id, lista.Id, "4801", "Auditoria");

        Assert.Equal(EnviarPesoResultado.Preparado, resultado);
        Assert.NotNull(email);
        var partes = Regex.Matches(Encoding.ASCII.GetString(email.Eml), "\r\n\r\n([A-Za-z0-9+/=\r\n]+?)--dmo-");
        Assert.Equal(2, partes.Count);
        var texto = PdfText.Of(Convert.FromBase64String(partes[1].Groups[1].Value));
        Assert.Contains("Por aprovar", texto);
        Assert.DoesNotContain("Aprovado por Operador 4802", texto);
        Assert.Equal(novoId, (await _db.EmailEnvios.AsNoTracking().SingleAsync()).PesoId);
    }

    [Fact]
    public async Task UmPesoDeOutroControloOuInexistenteNaoRecebeADecisao()
    {
        Assert.NotNull(await _approval.PesoAsync(_controloId, _pesoAntigoId));
        Assert.Null(await _approval.PesoAsync(_controloId + 1, _pesoAntigoId));
        Assert.Null(await _approval.PesoAsync(_controloId, int.MaxValue));
    }
}

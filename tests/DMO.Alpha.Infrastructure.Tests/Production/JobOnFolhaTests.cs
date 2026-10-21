using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Production;

/// <summary>
/// A folha da fábrica do Job On: o catálogo dos campos (sem o FO, com a QTD
/// Stock e a QTD Máq. em cada família CM, MF, BQ, PU, CS e TP; Diogo,
/// 21/10/2026), as regras do texto escrito (só o tamanho bloqueia) e a
/// gravação com o Criar e o Editar, lida de novo da base.
/// </summary>
public sealed class JobOnFolhaTests : IDisposable
{
    private readonly DmoDbContext _db;

    public JobOnFolhaTests()
    {
        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new DmoDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    // ---- O catálogo ----

    [Fact]
    public void Catalogo_CadaChaveEUnica_CurtaETemColunaNaFolha()
    {
        var chaves = JobOnFolhaCampos.Todos.Select(c => c.Chave).ToList();
        Assert.Equal(chaves.Count, chaves.Distinct().Count());
        Assert.All(chaves, c => Assert.True(c.Length <= JobOnFolhaCampos.ChaveMaxLength, c));

        var folha = new JobOnFolha();
        foreach (var campo in JobOnFolhaCampos.Todos)
        {
            folha.DefinirValor(campo.Chave, campo.Chave);
            Assert.Equal(campo.Chave, folha.Valor(campo.Chave));
        }
    }

    [Fact]
    public void Catalogo_SemFo_EComQtdStockEQtdMaqEscritasEmCadaFamiliaQueAsTem()
    {
        Assert.DoesNotContain(JobOnFolhaCampos.Todos, c => c.Chave.StartsWith("fo_", StringComparison.Ordinal));
        Assert.DoesNotContain("FO", JobOnFolhaCampos.Familias);

        foreach (var familia in new[] { JobOnFolhaCampos.Cm, JobOnFolhaCampos.Mf, JobOnFolhaCampos.Bq, JobOnFolhaCampos.Pu, JobOnFolhaCampos.Cs, JobOnFolhaCampos.Tp })
        {
            var campos = JobOnFolhaCampos.DaFamilia(familia);
            var stock = Assert.Single(campos, c => c.Rotulo == "QTD Stock");
            var maquina = Assert.Single(campos, c => c.Rotulo == "QTD Máq.");
            // Escritas, curtas e à vista (fora do «Mais detalhes»).
            Assert.All(new[] { stock, maquina }, c =>
            {
                Assert.Equal(JobOnFolhaTipoCampo.Curto, c.Tipo);
                Assert.Equal(JobOnFolhaCampos.MaximoCurto, c.Maximo);
                Assert.False(c.Detalhe);
            });
        }

        foreach (var familia in new[] { JobOnFolhaCampos.An, JobOnFolhaCampos.Arr, JobOnFolhaCampos.Pi, JobOnFolhaCampos.Cabecalho })
        {
            Assert.DoesNotContain(JobOnFolhaCampos.DaFamilia(familia), c => c.Rotulo.StartsWith("QTD", StringComparison.Ordinal));
        }

        Assert.DoesNotContain(JobOnFolhaCampos.Todos, c => c.Rotulo.Contains("opcional", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Catalogo_OsParesSaoOsDoProprietario_ECadaFamiliaAparecaUmaVez()
    {
        Assert.Equal(
            new[] { "CM+TP", "MF+CS", "BQ+AN", "PU+ARR", "PI+CAL" },
            JobOnFolhaCampos.Pares.Select(p => string.Join("+", p)));
        var nosPares = JobOnFolhaCampos.Pares.SelectMany(p => p).ToList();
        Assert.Equal(nosPares.Count, nosPares.Distinct().Count());
        Assert.Equal(
            JobOnFolhaCampos.Familias.Except([JobOnFolhaCampos.Cabecalho, JobOnFolhaCampos.NotasGerais]).Order(),
            nosPares.Order());
        // Cada família do catálogo tem a sua sigla na lista das famílias.
        Assert.All(JobOnFolhaCampos.Todos, c => Assert.Contains(c.Familia, JobOnFolhaCampos.Familias));
    }

    [Fact]
    public void Catalogo_OsTecnicosMenosUsadosFicamNoMaisDetalhes()
    {
        var cm = JobOnFolhaCampos.DaFamilia(JobOnFolhaCampos.Cm);
        Assert.All(new[] { "cm_tipo", "cm_pata", "cm_pata_tol", "cm_folga1", "cm_inversao" },
            chave => Assert.True(cm.Single(c => c.Chave == chave).Detalhe, chave));
        Assert.All(new[] { "cm_qtd_stock", "cm_qtd_maq", "cm_utilizacao", "cm_reparador", "cm_notas" },
            chave => Assert.False(cm.Single(c => c.Chave == chave).Detalhe, chave));
        // Na ordem do ecrã, os do «Mais detalhes» vêm no fim da família.
        foreach (var familia in JobOnFolhaCampos.Familias)
        {
            var campos = JobOnFolhaCampos.DaFamilia(familia).ToList();
            var primeiroDetalhe = campos.FindIndex(c => c.Detalhe);
            if (primeiroDetalhe >= 0)
            {
                Assert.All(campos.Skip(primeiroDetalhe), c => Assert.True(c.Detalhe, c.Chave));
            }
        }
    }

    // ---- As regras do texto ----

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  12 ", "12")]
    [InlineData("a\r\nb", "a\nb")]
    public void Normalizar_TiraAsPontas_CrlfPassaALf_EOVazioPassaANull(string? texto, string? esperado) =>
        Assert.Equal(esperado, JobOnFolhaRegras.Normalizar(texto));

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, false)]
    [InlineData(61, true)]
    public void CampoCurto_NoLimiteCabe_LogoAcimaERecusado(int tamanho, bool recusado)
    {
        var folha = JobOnFolhaRegras.NormalizarFolha(new Dictionary<string, string?> { ["cm_qtd_stock"] = new string('1', tamanho) });
        Assert.Equal(recusado ? "cm_qtd_stock" : null, JobOnFolhaRegras.PrimeiraChaveLonga(folha, null));
    }

    [Theory]
    [InlineData(999, false)]
    [InlineData(1000, false)]
    [InlineData(1001, true)]
    public void Nota_ContaCadaQuebraDeLinhaComoUmCaracter(int tamanho, bool recusado)
    {
        // O browser envia cada quebra como CRLF e conta-a como uma.
        var texto = "a\r\n" + new string('b', tamanho - 2);
        var folha = JobOnFolhaRegras.NormalizarFolha(new Dictionary<string, string?> { ["mf_notas"] = texto });
        Assert.Equal(recusado ? "mf_notas" : null, JobOnFolhaRegras.PrimeiraChaveLonga(folha, null));
    }

    [Theory]
    [InlineData(3999, false)]
    [InlineData(4000, false)]
    [InlineData(4001, true)]
    public void NotasGerais_NoLimiteCabem_LogoAcimaSaoRecusadas(int tamanho, bool recusado)
    {
        var folha = JobOnFolhaRegras.NormalizarFolha(new Dictionary<string, string?> { ["notas_gerais"] = new string('n', tamanho) });
        Assert.Equal(recusado ? "notas_gerais" : null, JobOnFolhaRegras.PrimeiraChaveLonga(folha, null));
    }

    [Theory]
    [InlineData(59, null)]
    [InlineData(60, null)]
    [InlineData(61, "cal:2:qtd_maq")]
    public void LinhaCal_AQtdMaqTemOMesmoMaximo(int tamanho, string? chave)
    {
        var cal = new[] { new LinhaCalEnvio("Nível", "37", "1"), new LinhaCalEnvio("Pinças", null, new string('2', tamanho)) };
        Assert.Equal(chave, JobOnFolhaRegras.PrimeiraChaveLonga(null, cal));
    }

    [Fact]
    public void ChaveRecusada_DaORotuloComAFamilia_EOMaximo()
    {
        Assert.Equal("CM · Notas", JobOnFolhaRegras.RotuloDaChave("cm_notas"));
        Assert.Equal("CAL · linha 2 · QTD Máq.", JobOnFolhaRegras.RotuloDaChave("cal:2:qtd_maq"));
        Assert.Equal("CAL · linha 3 · Valor", JobOnFolhaRegras.RotuloDaChave("cal:3:valor"));
        Assert.Null(JobOnFolhaRegras.RotuloDaChave("fo_tipo"));
        Assert.Null(JobOnFolhaRegras.RotuloDaChave("cal:0:valor"));
        Assert.Equal(4000, JobOnFolhaRegras.MaximoDaChave("notas_gerais"));
        Assert.Equal(60, JobOnFolhaRegras.MaximoDaChave("cal:1:designacao"));
    }

    [Fact]
    public void LinhasCal_AsPorOmissaoPorEscreverNaoFicam_UmaSoComAQtdMaqFica()
    {
        var cal = new[]
        {
            new LinhaCalEnvio("Bucha Marcada", null, null, "Bucha Marcada"),
            new LinhaCalEnvio("Tampão", null, "2", "Tampão"),
            new LinhaCalEnvio("Nível MF", null, null, "Nível"),
            new LinhaCalEnvio(null, null, null),
            new LinhaCalEnvio(null, "37.6", null)
        };

        Assert.Equal(
            new[]
            {
                new JobOnFolhaCalLida(1, "Tampão", null, "2"),
                new JobOnFolhaCalLida(2, "Nível MF", null, null),
                new JobOnFolhaCalLida(3, null, "37.6", null)
            },
            JobOnFolhaRegras.LinhasQueFicam(cal));
    }

    [Fact]
    public void Resumo_ListaOQueMudou_EContaNoFimOQueNaoCabe()
    {
        Assert.Null(JobOnFolhaRegras.Resumo([]));
        Assert.Equal("CM · QTD Stock, CAL", JobOnFolhaRegras.Resumo(["CM · QTD Stock", "CAL"]));
        var muitos = JobOnFolhaCampos.Todos.Select(c => c.RotuloAlteracao).ToList();
        var resumo = JobOnFolhaRegras.Resumo(muitos)!;
        Assert.True(resumo.Length <= JobOnChange.ValueMaxLength);
        Assert.Matches(@" e mais \d+$", resumo);
    }

    // ---- A gravação, lida de novo da base ----

    private async Task<(Machine Machine, string MfToolId)> SeedAsync()
    {
        var machine = new Machine { Code = "B1" };
        _db.Machines.Add(machine);
        _db.Tools.Add(new Tool { ToolId = "MF-1", Type = ToolType.Mf, Reference = "5810", Lot = "1" });
        await _db.SaveChangesAsync();
        return (machine, "MF-1");
    }

    private async Task<JobOnFolhaLida> LerDeNovoAsync(int jobOnId)
    {
        _db.ChangeTracker.Clear();
        return await new JobOnFolhaQuery(_db).ForJobOnAsync(jobOnId);
    }

    [Fact]
    public async Task Criar_GravaAFolhaEACal_ELeSeDeNovo()
    {
        var (machine, mf) = await SeedAsync();

        var outcome = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: mf,
            Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = " 12 ", ["tp_qtd_maq"] = "3", ["notas_gerais"] = "linha 1\r\nlinha 2" },
            Cal: [new LinhaCalEnvio("Nível", "37.3 MM", "2", "Nível"), new LinhaCalEnvio("Pinças", null, null, "Pinças")]));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        var lida = await LerDeNovoAsync(outcome.JobOn!.JobOnId);
        Assert.Equal("12", lida.Valor("cm_qtd_stock"));
        Assert.Equal("3", lida.Valor("tp_qtd_maq"));
        Assert.Equal("linha 1\nlinha 2", lida.Valor("notas_gerais"));
        Assert.Null(lida.Valor("bq_qtd_stock"));
        Assert.Equal(new[] { new JobOnFolhaCalLida(1, "Nível", "37.3 MM", "2") }, lida.Cal);
        Assert.Empty(await _db.JobOnChanges.ToListAsync());
    }

    [Fact]
    public async Task Criar_SemNadaEscrito_NaoCriaLinhaDaFolha_ELeSeVazia()
    {
        var (machine, mf) = await SeedAsync();

        var outcome = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: mf,
            Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = "  " },
            Cal: JobOnFolhaRegras.CalParaAbrir([])));

        Assert.Equal(CreateJobOnResult.Success, outcome.Result);
        Assert.False(await _db.JobOnFolhas.AnyAsync());
        Assert.True((await LerDeNovoAsync(outcome.JobOn!.JobOnId)).Vazia);
    }

    [Fact]
    public async Task Criar_ComUmTextoLongoDemais_ERecusado_ENadaFicaGravado()
    {
        var (machine, mf) = await SeedAsync();

        var outcome = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: mf,
            Folha: new Dictionary<string, string?> { ["pu_qtd_maq"] = new string('1', 61) }));

        Assert.Equal(CreateJobOnResult.TextoLongo, outcome.Result);
        Assert.Equal("pu_qtd_maq", outcome.ChaveRecusada);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.JobOns.AnyAsync());
        Assert.False(await _db.JobOnFolhas.AnyAsync());
    }

    [Fact]
    public async Task Editar_GravaOQueMudou_ERegistaUmaLinhaComOsRotulos()
    {
        var (machine, mf) = await SeedAsync();
        var criado = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: mf,
            Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = "12" },
            Cal: [new LinhaCalEnvio("Nível", "37", null)]));
        var jobOnId = criado.JobOn!.JobOnId;
        _db.ChangeTracker.Clear();

        var outcome = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(
            jobOnId, machine.Id, MfToolId: mf, ChangedBy: "Diogo",
            Folha: new Dictionary<string, string?> { ["cm_qtd_stock"] = "10", ["bq_qtd_maq"] = "1" },
            Cal: [new LinhaCalEnvio("Nível", "37", "4"), new LinhaCalEnvio("Altura", "120", null)]));

        Assert.Equal(UpdateJobOnResult.Success, outcome.Result);
        var lida = await LerDeNovoAsync(jobOnId);
        Assert.Equal(("10", "1"), (lida.Valor("cm_qtd_stock"), lida.Valor("bq_qtd_maq")));
        Assert.Equal(
            new[] { new JobOnFolhaCalLida(1, "Nível", "37", "4"), new JobOnFolhaCalLida(2, "Altura", "120", null) },
            lida.Cal);
        var mudou = Assert.Single(await _db.JobOnChanges.AsNoTracking().ToListAsync());
        Assert.Equal(JobOnChangeFields.Folha, mudou.Field);
        Assert.Null(mudou.OldValueFrozen);
        Assert.Equal("CM · QTD Stock, BQ · QTD Máq., CAL", mudou.NewValueFrozen);
        Assert.Equal("Diogo", mudou.ChangedByNameFrozen);
        Assert.Equal("Mudou a folha: CM · QTD Stock, BQ · QTD Máq., CAL",
            JobOnChangeFields.Describe(mudou.Field, mudou.OldValueFrozen, mudou.NewValueFrozen));
        Assert.Equal(JobOnFollowUp.Nenhum, JobOnChangeFields.FollowUp(JobOnChangeFields.Folha));
    }

    [Fact]
    public async Task Editar_ComAMesmaFolha_NaoRegistaNada_ESemFolhaNoEnvioAFolhaFica()
    {
        var (machine, mf) = await SeedAsync();
        var folha = new Dictionary<string, string?> { ["mf_qtd_maq"] = "2" };
        var criado = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: mf, Folha: folha, Cal: [new LinhaCalEnvio("Nível", "37", null)]));
        var jobOnId = criado.JobOn!.JobOnId;
        _db.ChangeTracker.Clear();

        // A mesma folha, com os espaços e as linhas por omissão por escrever que a página envia.
        var mesma = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(
            jobOnId, machine.Id, MfToolId: mf,
            Folha: new Dictionary<string, string?> { ["mf_qtd_maq"] = " 2 " },
            Cal: [new LinhaCalEnvio("Nível", "37", null), new LinhaCalEnvio("Pinças", null, null, "Pinças")]));
        Assert.Equal(UpdateJobOnResult.Success, mesma.Result);

        // Um envio sem a folha (só as ferramentas) não mexe nela.
        _db.ChangeTracker.Clear();
        var semFolha = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(jobOnId, machine.Id, MfToolId: mf));
        Assert.Equal(UpdateJobOnResult.Success, semFolha.Result);

        Assert.Empty(await _db.JobOnChanges.ToListAsync());
        var lida = await LerDeNovoAsync(jobOnId);
        Assert.Equal("2", lida.Valor("mf_qtd_maq"));
        Assert.Single(lida.Cal);
    }

    [Fact]
    public async Task Editar_ComUmTextoLongoDemais_ERecusado_ENadaMuda()
    {
        var (machine, mf) = await SeedAsync();
        var criado = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: mf, Folha: new Dictionary<string, string?> { ["cs_qtd_stock"] = "5" }));
        var jobOnId = criado.JobOn!.JobOnId;
        _db.ChangeTracker.Clear();

        var outcome = await new UpdateJobOnHandler(_db).HandleAsync(new UpdateJobOn(
            jobOnId, machine.Id, MfToolId: mf, FimEm: new DateOnly(2026, 12, 1),
            Folha: new Dictionary<string, string?> { ["cs_qtd_stock"] = "6" },
            Cal: [new LinhaCalEnvio(new string('N', 61), null, null)]));

        Assert.Equal(UpdateJobOnResult.TextoLongo, outcome.Result);
        Assert.Equal("cal:1:designacao", outcome.ChaveRecusada);
        var lida = await LerDeNovoAsync(jobOnId);
        Assert.Equal("5", lida.Valor("cs_qtd_stock"));
        Assert.Empty(lida.Cal);
        Assert.Null((await _db.JobOns.AsNoTracking().SingleAsync(j => j.Id == jobOnId)).FimEm);
        Assert.Empty(await _db.JobOnChanges.ToListAsync());
    }

    [Fact]
    public async Task EliminarJobOn_LevaAFolhaEACal()
    {
        var (machine, mf) = await SeedAsync();
        var criado = await new CreateJobOnHandler(_db).HandleAsync(new CreateJobOn(
            "202601", "1075C142", machine.Id, MfToolId: mf,
            Folha: new Dictionary<string, string?> { ["pi_pincas"] = "Latão" },
            Cal: [new LinhaCalEnvio("Nível", "37", null)]));
        _db.ChangeTracker.Clear();

        var resultado = await new DeleteJobOnHandler(_db).HandleAsync(new DeleteJobOn(criado.JobOn!.JobOnId));

        Assert.Equal(DeleteJobOnResult.Success, resultado);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.JobOnFolhas.AnyAsync());
        Assert.False(await _db.JobOnFolhaCalLinhas.AnyAsync());
    }
}

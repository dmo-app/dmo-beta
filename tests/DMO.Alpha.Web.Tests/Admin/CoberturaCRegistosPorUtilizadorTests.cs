using DMO.Alpha.Core.Controlo;
using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.ReparacaoInterna;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static DMO.Alpha.Web.Tests.PosicaoNoHtml;

namespace DMO.Alpha.Web.Tests.Admin;

/// <summary>
/// O que faltava provar nos registos por utilizador do Admin (DMO-392): a
/// lista dos mais recentes primeiro, o filtro dos dias (De e Até, ambos
/// inclusivos) e o que cada utilizador corrigiu ou anulou.
/// </summary>
public sealed class CoberturaCRegistosPorUtilizadorTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string AdminEmail = "cobc-registos-admin@example.test";
    private const string AdminPassword = "CobCRegistos1!";

    // Três dias fixos: o evento de um dia é à hora local da fábrica, não em UTC.
    private static readonly DateOnly Dia1 = new(2026, 9, 10);
    private static readonly DateOnly Dia2 = new(2026, 9, 11);
    private static readonly DateOnly Dia3 = new(2026, 9, 12);

    private readonly CustomWebApplicationFactory _factory;
    private readonly Guid _ana = Guid.NewGuid();

    public CoberturaCRegistosPorUtilizadorTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetState();
        using (var limpar = _factory.Services.CreateScope())
        {
            var antes = limpar.ServiceProvider.GetRequiredService<DmoDbContext>();
            antes.JobOnChanges.RemoveRange(antes.JobOnChanges);
            antes.ReparacoesInternasRasto.RemoveRange(antes.ReparacoesInternasRasto);
            antes.Users.RemoveRange(antes.Users.Where(u => u.OperatorId == "7951"));
            antes.SaveChanges();
        }

        _factory.AuthProvider.AddAccount(AdminEmail, "provider-cobc-registos-admin", AdminPassword, AdminEmail);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
        db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = AdminEmail });
        db.Users.Add(new User { Id = _ana, OperatorId = "7951", Name = "Ana Dias", ProviderUserId = "provider-7951" });
        var maquina = new Machine { Code = "RC1" };
        db.Machines.Add(maquina);
        db.SaveChanges();
        var job = new JobOn { Reference = "5447T173", ProductionNumber = "202698", MachineId = maquina.Id };
        db.JobOns.Add(job);
        db.SaveChanges();

        // Quatro rastos da Ana, do mais antigo ao mais recente:
        //  - 1 de setembro: alterou o Tampão (e só a 12, às 00:30, removeu essa alteração);
        //  - dia 10 às 12:00: alterou a Calote;
        //  - dia 11 às 23:30 (o último minuto do dia): corrigiu a reparação nº 11 para 12.
        db.JobOnChanges.AddRange(
            new JobOnChange
            {
                JobOnId = job.Id, Field = JobOnChangeFields.Tampao, OldValueFrozen = "3", NewValueFrozen = "4",
                ChangedByUserId = _ana, ChangedByNameFrozen = "Ana Dias", ChangedAtUtc = Local(new DateOnly(2026, 9, 1), 12, 0),
                RemovedAtUtc = Local(Dia3, 0, 30), RemovedByUserId = _ana, RemovedByNameFrozen = "Ana Dias"
            },
            new JobOnChange
            {
                JobOnId = job.Id, Field = JobOnChangeFields.Calote, OldValueFrozen = "1", NewValueFrozen = "2",
                ChangedByUserId = _ana, ChangedByNameFrozen = "Ana Dias", ChangedAtUtc = Local(Dia1, 12, 0)
            });
        db.ReparacoesInternasRasto.Add(new ReparacaoRasto
        {
            ReparacaoId = 1, Acao = ReparacaoAcao.Corrigido, NumeroAntes = "11", NumeroDepois = "12",
            PorUserId = _ana, EmUtc = Local(Dia2, 23, 30)
        });
        db.SaveChanges();
    }

    private static DateTime Local(DateOnly dia, int hora, int minuto) =>
        DmoBusinessTime.ToUtc(dia.ToDateTime(new TimeOnly(hora, minuto)));

    private async Task<string> ListaAsync(string consulta)
    {
        var cliente = CoberturaCApoio.Cliente(_factory);
        using var entrada = await CoberturaCApoio.EntrarAsync(cliente, AdminEmail, AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        return WebUtility.HtmlDecode(await CoberturaCApoio.PaginaAsync(cliente, "/Admin/Registos?" + consulta));
    }

    private const string Alterou1 = "5447T173 · 202698 · Calote: 1 → 2";
    private const string Corrigiu = "nº 11 → 12";
    private const string Removeu = "5447T173 · 202698 · Tampao";
    private const string Alterou0 = "5447T173 · 202698 · Tampao: 3 → 4";

    [Fact]
    public async Task OsRegistos_VemDosMaisRecentesParaOsMaisAntigos_ComOQueCorrigiuOuAnulou()
    {
        var html = await ListaAsync("utilizador=7951");

        Assert.Contains("4 registos", html);
        Assert.Contains("Removeu uma alteração", html);
        Assert.Contains("Corrigiu", html);
        Assert.True(Posicao(html, Removeu) < Posicao(html, Corrigiu), "o mais recente (dia 12) devia vir primeiro");
        Assert.True(Posicao(html, Corrigiu) < Posicao(html, Alterou1), "dia 11 antes do dia 10");
        Assert.True(Posicao(html, Alterou1) < Posicao(html, Alterou0), "dia 10 antes de 1 de setembro");
    }

    [Fact]
    public async Task OFiltroDosDias_DeEAteIncluemOsDiasEscolhidos_ESoElesTrazemORegistoDoUltimoMinuto()
    {
        // De o dia 11 em diante: o das 23:30 do dia 11 e o das 00:30 do dia 12; os de antes ficam de fora.
        var de = await ListaAsync($"utilizador=7951&de={Dia2:yyyy-MM-dd}");
        Assert.Contains(Corrigiu, de);
        Assert.Contains(Removeu + "</td>", de);
        Assert.DoesNotContain(Alterou1, de);
        Assert.DoesNotContain(Alterou0, de);

        // De o dia 12: já não há o das 23:30 do dia 11.
        var deDia3 = await ListaAsync($"utilizador=7951&de={Dia3:yyyy-MM-dd}");
        Assert.Contains(Removeu + "</td>", deDia3);
        Assert.DoesNotContain(Corrigiu, deDia3);

        // Até o dia 11: o das 23:30 conta; o das 00:30 do dia 12 não.
        var ate = await ListaAsync($"utilizador=7951&ate={Dia2:yyyy-MM-dd}");
        Assert.Contains(Corrigiu, ate);
        Assert.Contains(Alterou1, ate);
        Assert.DoesNotContain(Removeu + "</td>", ate);

        // Até o dia 10: só o das 12:00 do dia 10 e o de 1 de setembro.
        var ateDia1 = await ListaAsync($"utilizador=7951&ate={Dia1:yyyy-MM-dd}");
        Assert.Contains(Alterou1, ateDia1);
        Assert.DoesNotContain(Corrigiu, ateDia1);

        // Entre o dia 10 e o 11 (os dois incluídos): a Calote e a correção.
        var entre = await ListaAsync($"utilizador=7951&de={Dia1:yyyy-MM-dd}&ate={Dia2:yyyy-MM-dd}");
        Assert.Contains("2 registos", entre);
        Assert.Contains(Alterou1, entre);
        Assert.Contains(Corrigiu, entre);
        Assert.DoesNotContain(Alterou0, entre);
        Assert.DoesNotContain(Removeu + "</td>", entre);

        // Dias sem nada dizem-no.
        var vazio = await ListaAsync($"utilizador=7951&de=2026-12-01&ate=2026-12-02");
        Assert.Contains("Nenhum registo com estes filtros.", vazio);
        // O filtro só dos dias (sem utilizador) também abre a lista.
        Assert.Contains(Corrigiu, await ListaAsync($"de={Dia2:yyyy-MM-dd}&ate={Dia2:yyyy-MM-dd}"));
    }
}

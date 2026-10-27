using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

public sealed partial class PostgresMigrationIntegrationTests
{
    // Diogo, 27/10/2026: a lista do posto da RI lia o acesso de cada
    // reparador com uma consulta própria. As regras ficam as mesmas (conta
    // ativa, PIN do posto, Reparação Interna ver ou criar, nunca o ADMIN),
    // mas o número de consultas já não cresce com o número de pessoas.
    [SkippableFact]
    public async Task PostoRi_ListaSoQuemTemRiEPinPorNome_ComAsMesmasConsultasPara5Ou100Reparadores()
    {
        SkipWithoutConnection();
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var marker = "posto-" + Guid.NewGuid().ToString("N")[..8];
        var adminEmail = marker + "@admin.test";
        var adminAnterior = await setup.ModuleSettings.AsNoTracking()
            .Where(s => s.SettingKey == ModuleSettingKeys.AdminEmail)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync();
        var comRi = new Template { TemplateKey = marker + "-ri", Name = marker + " RI", Capabilities = [DmoCapabilities.ReparacaoInterna] };
        var soVer = new Template { TemplateKey = marker + "-ver", Name = marker + " Ver", Capabilities = [DmoCapabilities.ReparacaoInternaView] };
        var semRi = new Template { TemplateKey = marker + "-sem", Name = marker + " Sem", Capabilities = [DmoCapabilities.Boquilhas] };
        setup.Templates.AddRange(comRi, soVer, semRi);
        await DefinirAdminAsync(setup, adminEmail);
        await setup.SaveChangesAsync();
        var operadores = new Queue<string>(await OperadoresLivresAsync(setup, 120));
        try
        {
            // Os que nunca aparecem: arquivado, sem PIN, sem RI, sem template e o ADMIN.
            await AdicionarAsync(setup, marker + " z arquivado", comRi, pin: true, arquivado: true);
            await AdicionarAsync(setup, marker + " z sem pin", comRi, pin: false);
            await AdicionarAsync(setup, marker + " z sem ri", semRi, pin: true);
            await AdicionarAsync(setup, marker + " z sem template", null, pin: true);
            await AdicionarAsync(setup, marker + " z admin", comRi, pin: true, email: adminEmail);

            // Cinco reparadores, criados fora da ordem do nome.
            foreach (var n in new[] { 4, 1, 3, 0, 2 })
            {
                await AdicionarAsync(setup, $"{marker} {n:000}", n % 2 == 0 ? comRi : soVer, pin: true);
            }

            var (com5, consultas5) = await ListarAsync(marker);
            Assert.Equal(Enumerable.Range(0, 5).Select(n => $"{marker} {n:000}"), com5);

            for (var n = 5; n < 100; n++)
            {
                await AdicionarAsync(setup, $"{marker} {n:000}", n % 2 == 0 ? comRi : soVer, pin: true);
            }

            var (com100, consultas100) = await ListarAsync(marker);
            Assert.Equal(Enumerable.Range(0, 100).Select(n => $"{marker} {n:000}"), com100);
            Assert.Equal(consultas5, consultas100);
            Assert.True(consultas100 <= 3, $"A lista do posto fez {consultas100} consultas.");
        }
        finally
        {
            var ids = setup.Users.Where(u => u.Name.StartsWith(marker)).Select(u => u.Id);
            await setup.UserSettings.Where(s => ids.Contains(s.UserId)).ExecuteDeleteAsync();
            await setup.TemplateAssignments.Where(a => ids.Contains(a.UserId)).ExecuteDeleteAsync();
            await setup.Users.Where(u => u.Name.StartsWith(marker)).ExecuteDeleteAsync();
            await setup.Templates.Where(t => t.TemplateKey.StartsWith(marker)).ExecuteDeleteAsync();
            setup.ChangeTracker.Clear();
            if (adminAnterior is null)
            {
                await setup.ModuleSettings.Where(s => s.SettingKey == ModuleSettingKeys.AdminEmail).ExecuteDeleteAsync();
            }
            else
            {
                await DefinirAdminAsync(setup, adminAnterior);
                await setup.SaveChangesAsync();
            }
        }

        async Task AdicionarAsync(DmoDbContext db, string nome, Template? template, bool pin, bool arquivado = false, string? email = null)
        {
            var user = new User { Name = nome, OperatorId = operadores.Dequeue(), ProviderUserId = nome, Email = email, IsStandBy = arquivado };
            db.Users.Add(user);
            if (template is not null)
            {
                db.TemplateAssignments.Add(new TemplateAssignment { UserId = user.Id, TemplateId = template.Id });
            }

            if (pin)
            {
                db.UserSettings.Add(new UserSetting { UserId = user.Id, Key = PostoRiRegras.Chave, Value = "v1.1.AA==.AA==" });
            }

            await db.SaveChangesAsync();
        }
    }

    private static async Task<(List<string> Nomes, int Consultas)> ListarAsync(string marker)
    {
        var contador = new ContaConsultas();
        await using var db = CreateContextWith(contador);
        var lista = await new PostoRi(db, new AdminAuthorizationService(db, new AdminEmailSettings(db))).ListarAsync();
        return (lista.Select(r => r.Nome).Where(n => n.StartsWith(marker)).ToList(), contador.Vezes);
    }

    private static async Task DefinirAdminAsync(DmoDbContext db, string email)
    {
        var linha = await db.ModuleSettings.FirstOrDefaultAsync(s => s.SettingKey == ModuleSettingKeys.AdminEmail);
        if (linha is null)
        {
            db.ModuleSettings.Add(new ModuleSetting { SettingKey = ModuleSettingKeys.AdminEmail, SettingValue = email });
        }
        else
        {
            linha.SettingValue = email;
        }
    }

    private static async Task<List<string>> OperadoresLivresAsync(DmoDbContext db, int quantos)
    {
        var usados = (await db.Users.AsNoTracking().Select(u => u.OperatorId).ToListAsync()).ToHashSet();
        return Enumerable.Range(0, 9000).Select(i => (9999 - i).ToString()).Where(o => !usados.Contains(o)).Take(quantos).ToList();
    }
}

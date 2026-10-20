using DMO.Alpha.Core.Admin;
using DMO.Alpha.Infrastructure.Admin;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// «Criar template» duas vezes ao mesmo tempo com o mesmo pedido (deep
/// sweep, 18/10/2026, B-admin-6): o segundo espera pelo primeiro (trinco) e
/// abre a template que ele criou. Fica uma só com esse nome.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task Concurrency_CreateTheSameTemplateTwiceAtOnce_KeepsOneTemplate()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var name = "Offline " + Guid.NewGuid().ToString("N")[..8];
        try
        {
            // O outro pedido parte quando este já procurou e vai gravar; espera
            // até acabar (sem a correção) ou até 1,5 s (com o trinco, fica à
            // espera de que este grave).
            Task<CreateTemplateOutcome>? other = null;
            await using var mine = CreateContextWith(new BeforeFirstSave(async () =>
            {
                other = Task.Run(async () =>
                {
                    await using var b = CreateContext();
                    return await new TemplateAdminService(b).CreateTemplateAsync(
                        new CreateTemplate(name, [DmoCapabilities.JobOnCreate]));
                });
                await Task.WhenAny(other, Task.Delay(1500));
            }));

            var outcome = await new TemplateAdminService(mine).CreateTemplateAsync(
                new CreateTemplate(name, [DmoCapabilities.JobOnCreate]));
            var otherOutcome = await other!;

            Assert.Equal(CreateTemplateResult.Success, outcome.Result);
            Assert.Equal(CreateTemplateResult.Success, otherOutcome.Result);
            Assert.Equal(outcome.Template!.TemplateKey, otherOutcome.Template!.TemplateKey);
            Assert.Equal(1, await db.Templates.AsNoTracking().CountAsync(t => t.Name == name));
        }
        finally
        {
            await db.Templates.Where(t => t.Name == name).ExecuteDeleteAsync();
        }
    }
}

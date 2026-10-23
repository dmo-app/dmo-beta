using System.Net;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public sealed class EnvioUnicoPostgresPageTests
{
    [SkippableFact]
    public async Task Movimento_RepetidoNoPostgres_RedirectPersistidoEConflitoSemDuplicacao()
    {
        var connection = Environment.GetEnvironmentVariable("DMO_POSTGRES_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(connection), "PostgreSQL descartável não configurado.");
        Skip.IfNot(new Npgsql.NpgsqlConnectionStringBuilder(connection!).Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true,
            "É obrigatória uma base descartável com test no nome.");
        var options = new DbContextOptionsBuilder<DmoDbContext>().UseNpgsql(connection!).Options;
        await using var db = new DmoDbContext(options);
        await db.Database.MigrateAsync();
        var marker = Guid.NewGuid().ToString("N");
        var user = new User { Id = Guid.NewGuid(), OperatorId = "7198", Name = "Envio PostgreSQL", ProviderUserId = marker, Email = marker + "@example.test" };
        var template = new Template { Id = Guid.NewGuid(), TemplateKey = marker, Name = marker, Capabilities = [DmoCapabilities.JobOnCreate, DmoCapabilities.Boquilhas] };
        var tool = new Tool { ToolId = "BQ-" + marker, Type = ToolType.Bq, Reference = marker[..8], Lot = "1" };
        var trace = new BqRepairTrace { ToolId = tool.ToolId };
        db.Users.Add(user);
        db.Templates.Add(template);
        db.TemplateAssignments.Add(new TemplateAssignment { Id = Guid.NewGuid(), UserId = user.Id, TemplateId = template.Id });
        db.Tools.Add(tool);
        db.BqRepairTraces.Add(trace);
        await db.SaveChangesAsync();
        try
        {
            using var original = new CustomWebApplicationFactory();
            original.AuthProvider.AddAccount(user.Email, marker, "EnvioPass1!", user.Email);
            using var factory = original.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.AddScoped(_ => options);
                s.AddScoped(sp => new DmoDbContext(sp.GetRequiredService<DbContextOptions<DmoDbContext>>()));
            }));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var token = HtmlFormHelpers.ExtractRequestVerificationToken(await client.GetStringAsync("/login"))!;
            var login = await client.PostAsync("/login", HtmlFormHelpers.BuildFormPost(("identifier", user.Email),
                ("password", "EnvioPass1!"), ("returnUrl", "/"), ("__RequestVerificationToken", token)));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            var html = await client.GetStringAsync($"/Boquilhas?trace_id={trace.Id}");
            var key = EnvioUnicoPedidos.NovaChave();
            Task<HttpResponseMessage> Post(string quantity, bool campoRepetido = false)
            {
                var form = EnvioDoFormulario.Enviar(html, [("quantity", quantity)], [("movement_type", "saida")]);
                (string, string)[] extras = campoRepetido
                    ? [("audit", "x"), ("audit", "b"), ("audit", "y")]
                    : [("audit", "x"), ("b", "y")];
                return client.PostComChaveAsync(form.Acao, key, form.Campos.Select(c => (c.Nome, c.Valor)).Concat(extras).ToArray());
            }
            var first = await Post("3");
            var repeated = await Post("3");
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
            Assert.Equal(first.StatusCode, repeated.StatusCode);
            Assert.Equal(first.Headers.Location, repeated.Headers.Location);
            Assert.Equal(HttpStatusCode.Conflict, (await Post("4")).StatusCode);
            // A separação entre nomes e listas de valores faz parte da impressão.
            Assert.Equal(HttpStatusCode.Conflict, (await Post("3", campoRepetido: true)).StatusCode);
            Assert.Single(await db.BqMovements.AsNoTracking().Where(m => m.BqRepairTraceId == trace.Id).ToListAsync());
            Assert.Single(await db.OperacoesRegistadas.AsNoTracking().Where(o => o.AutorUserId == user.Id && o.Tipo == "web.envio").ToListAsync());
        }
        finally
        {
            await db.BqMovements.Where(m => m.BqRepairTraceId == trace.Id).ExecuteDeleteAsync();
            await db.BqRepairTraces.Where(t => t.Id == trace.Id).ExecuteDeleteAsync();
            await db.Tools.Where(t => t.ToolId == tool.ToolId).ExecuteDeleteAsync();
            await db.OperacoesRegistadas.Where(o => o.AutorUserId == user.Id).ExecuteDeleteAsync();
            await db.TemplateAssignments.Where(a => a.UserId == user.Id).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
            await db.Templates.Where(t => t.Id == template.Id).ExecuteDeleteAsync();
        }
    }
}

using System.Net;
using System.Text.RegularExpressions;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DMO.Alpha.Web.Tests;

public partial class JobOnPageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sumario_MostraATrocaDeLoteDaBQ_MesmoSeFoiPlaneadaAntesDoInicio(bool antesDoInicio)
    {
        SeedUser("4791", "Chefe Sumário", "provider-sumario-alteracoes", "Sumario1234!");
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DmoDbContext>();
            var maquina = db.Machines.FirstOrDefault(m => m.Code == "B2") ?? new Machine { Code = "B2" };
            var job = new JobOn { Machine = maquina, Reference = "8287T194", ProductionNumber = "202601", InicioEm = new(2026, 11, 1) };
            db.JobOns.Add(job);
            await db.SaveChangesAsync();
            id = job.Id;
            db.JobOnChanges.Add(new JobOnChange {
                JobOnId = id, Field = JobOnChangeFields.Bq,
                OldValueFrozen = "T194 · lote 2", NewValueFrozen = "T194 · lote 4",
                ChangedAtUtc = new DateTime(2026, antesDoInicio ? 10 : 11, 20, 12, 0, 0, DateTimeKind.Utc)
            });
            // A alteração anulada fica no histórico, mas não é uma troca em vigor.
            db.JobOnChanges.Add(new JobOnChange {
                JobOnId = id, Field = JobOnChangeFields.Bq,
                OldValueFrozen = "T194 · lote 4", NewValueFrozen = "T194 · lote 9",
                ChangedAtUtc = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc),
                RemovedAtUtc = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        }
        using var client = await LoginClientAsync("4791", "Sumario1234!");
        var html = await GetPageHtmlAsync(client, PagePath + "?ano=2026&mes=11&dia=25");
        var bloco = Regex.Match(html, $"<div\\b[^>]*\\bid=\"jobonLinha-{id}\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
        Assert.True(bloco.Success, "A linha do Job On tem de estar no Sumário.");
        var linha = bloco.Groups[1].Value;
        var texto = WebUtility.HtmlDecode(Regex.Replace(linha, "<[^>]*>", " "));
        Assert.Contains("BQ", texto);
        Assert.Contains("Sai lote 2 entra lote 4", texto);
        Assert.DoesNotContain("entra lote 9", texto);
    }
}

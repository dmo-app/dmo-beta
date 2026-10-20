using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// Sweep de 19/10/2026, DB-8: a procura pela parte BQ traduzida no
/// PostgreSQL (o operador ~, com o padrão como parâmetro). O InMemory avalia
/// o Regex em C# e não apanha uma tradução diferente. As referências têm
/// letras ao acaso, para a base partilhada pelos testes não interferir.
/// </summary>
public sealed partial class PostgresMigrationIntegrationTests
{
    [SkippableFact]
    public async Task ProcuraPelaParteBq_ComecaNaPrimeiraLetra_NoPostgres()
    {
        SkipWithoutConnection();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var p = Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var letras = new string(p.Select(c => (char)('A' + (c - '0'))).ToArray());
        var prefixo = $"PBQ-{p}-";
        // A parte BQ começa na primeira letra: em «Q{letras}» as letras
        // procuradas vêm a meio, depois do Q.
        db.Tools.AddRange(
            new Tool { ToolId = $"{prefixo}MEIO", Type = ToolType.Bq, Reference = $"Q{letras}", Lot = "1" },
            new Tool { ToolId = $"{prefixo}DEPOIS", Type = ToolType.Bq, Reference = $"{p}{letras}", Lot = "1" },
            new Tool { ToolId = $"{prefixo}INICIO", Type = ToolType.Bq, Reference = $"{letras}9", Lot = "1" },
            new Tool { ToolId = $"{prefixo}PONTO", Type = ToolType.Bq, Reference = $"Q{letras}X", Lot = "1" });
        await db.SaveChangesAsync();

        try
        {
            async Task<string[]> Procura(string texto) => await db.Tools.AsNoTracking()
                .Where(t => t.ToolId.StartsWith(prefixo))
                .Where(ReferenceMatch.Predicate<Tool>(t => t.Reference, texto))
                .OrderBy(t => t.ToolId)
                .Select(t => t.ToolId)
                .ToArrayAsync();

            Assert.Equal(new[] { $"{prefixo}DEPOIS", $"{prefixo}INICIO" }, await Procura(letras.ToLowerInvariant()));
            Assert.Equal(new[] { $"{prefixo}MEIO", $"{prefixo}PONTO" }, await Procura($"q{letras}"));
            // O ponto é um ponto, não «qualquer carácter».
            Assert.Empty(await Procura($"Q{letras}."));
            Assert.Equal(new[] { $"{prefixo}DEPOIS" }, await Procura(p));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.Tools.Where(t => t.ToolId.StartsWith(prefixo)).ExecuteDeleteAsync();
        }
    }
}

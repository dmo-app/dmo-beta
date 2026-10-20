using System.Text.RegularExpressions;

namespace DMO.Alpha.Infrastructure.Tests.Data;

/// <summary>
/// A app no DEV liga-se com o utilizador <c>dmo_beta_dev_runtime</c>, que só
/// chega às tabelas a que o SQL dá acesso. Cada tabela criada por um
/// <c>docs/dev/*/apply.sql</c> em uso tem de ter o GRANT à tabela e à sequência
/// do Id no mesmo ficheiro; sem ele a página dá erro 500 no DEV
/// («permission denied for table PesoComparacaoCmDecisoes», 19/10/2026).
/// Na 0.2 o SQL à mão é só o de <c>docs/base-de-dados/</c> (as migrações
/// aplicam-se no deploy e o dmo_app recebe as tabelas novas pelos privilégios
/// por omissão), e a mesma regra vale para ele.
/// </summary>
public sealed class SqlDoDevPermissoesTests
{
    [Fact]
    public void CadaTabelaCriadaNoSqlDoDev_TemGrantAoUtilizadorDaApp()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DMO.Alpha.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var emUso = Directory.EnumerateFiles(Path.Combine(dir!.FullName, "docs", "base-de-dados"), "*.sql", SearchOption.AllDirectories)
            .Where(f => !File.ReadAllText(f).Contains("SQL arquivado de antes da Baseline"))
            .ToArray();
        Assert.Contains(emUso, f => f.Contains("atualizar-de-0.2.0-beta.2"));

        var tabelasVistas = 0;
        foreach (var file in emUso)
        {
            var sql = File.ReadAllText(file);
            var grants = Regex.Matches(sql, @"GRANT[^;]*TO dmo_beta_dev_runtime;").Select(m => m.Value).ToArray();
            foreach (Match criada in Regex.Matches(sql, "CREATE TABLE \"(\\w+)\""))
            {
                tabelasVistas++;
                var tabela = criada.Groups[1].Value;
                var nome = $"{Path.GetFileName(Path.GetDirectoryName(file))}/{Path.GetFileName(file)}";
                Assert.True(grants.Any(g => g.Contains("SELECT, INSERT") && g.Contains("ON TABLE") && g.Contains($"\"{tabela}\"")),
                    $"{nome} cria \"{tabela}\" sem GRANT na tabela a dmo_beta_dev_runtime");
                Assert.True(grants.Any(g => g.Contains("ON SEQUENCE") && g.Contains($"\"{tabela}_Id_seq\"")),
                    $"{nome} cria \"{tabela}\" sem GRANT na sequência \"{tabela}_Id_seq\"");
            }
        }
        Assert.True(tabelasVistas > 0);
    }
}

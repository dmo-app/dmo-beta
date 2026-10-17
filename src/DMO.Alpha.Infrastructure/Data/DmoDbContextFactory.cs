using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DMO.Alpha.Infrastructure.Data;

/// <summary>
/// Design-time factory usada pelos comandos `dotnet ef` (migrations add,
/// migrations script, migrations list, database update). Garante que as
/// ferramentas constroem sempre o <see cref="DmoDbContext"/> com o provider
/// relacional Npgsql — o provider InMemory não suporta migrations.
///
/// A connection string é lida da mesma chave de configuração usada pelo
/// runtime: <c>ConnectionStrings:DmoDatabase</c> (variável de ambiente
/// <c>ConnectionStrings__DmoDatabase</c>). Nenhuma credencial vive no código.
/// Gerar ou escrever o SQL de uma migration nunca abre ligação — a connection
/// string apenas é contactada por `database update`/Migrate. Sem ligação
/// configurada usa-se um placeholder sem credenciais, claramente marcado,
/// suficiente para gerar migrations offline e nunca utilizável como alvo
/// real de base de dados.
/// </summary>
public sealed class DmoDbContextFactory : IDesignTimeDbContextFactory<DmoDbContext>
{
    /// <summary>Mesma chave usada pelo runtime (IConfiguration).</summary>
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__DmoDatabase";

    // Placeholder de design-time: sem credenciais, sem alvo real. Nunca é
    // contactado por `migrations add`/`script`; `database update` contra ele
    // falha claramente (não há servidor), exigindo a ligação real configurada.
    private const string DesignTimePlaceholderConnectionString =
        "Host=localhost;Database=dmo_alpha_design_time;Username=configure_a_real_connection_string";

    public DmoDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DesignTimePlaceholderConnectionString;
        }

        var options = new DbContextOptionsBuilder<DmoDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(DmoDbContext).Assembly.FullName))
            .Options;

        return new DmoDbContext(options);
    }
}

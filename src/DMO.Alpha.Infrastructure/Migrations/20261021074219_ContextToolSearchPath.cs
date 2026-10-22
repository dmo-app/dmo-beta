using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContextToolSearchPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Aviso do linter do Supabase (function_search_path_mutable, 21/10/2026):
            // a função do trigger não usa tabelas, por isso o search_path fica vazio.
            migrationBuilder.Sql("""ALTER FUNCTION "dmo_context_tool_fixed"() SET search_path = '';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER FUNCTION "dmo_context_tool_fixed"() RESET search_path;""");
        }
    }
}

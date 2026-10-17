using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Authentication;
using DMO.Alpha.Core.Boquilhas;
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Authentication;
using DMO.Alpha.Infrastructure.Boquilhas;
using DMO.Alpha.Infrastructure.Controlo;
using DMO.Alpha.Infrastructure.Data;
using DMO.Alpha.Infrastructure.Production;
using DMO.Alpha.Infrastructure.Tools;
using DMO.Alpha.Infrastructure.Admin;
using DMO.Alpha.Web.Authorization;
using DMO.Alpha.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Authentication provider
// -------------------------------------------------------------------------
// Online BETA: credentials are deployment settings.
var authProviderName = builder.Configuration.GetValue<string>("Dmo:AuthenticationProvider");
// Resolve the effective provider before registering either authentication or
// provisioning. InMemory is a local Development seam, never a deployment fallback.
var useSupabase = !builder.Environment.IsDevelopment()
    || string.Equals(authProviderName, "Supabase", StringComparison.OrdinalIgnoreCase);

if (useSupabase)
{
    builder.Services.AddSingleton<IOptions<SupabaseAuthOptions>>(_ =>
        Options.Create(new SupabaseAuthOptions
        {
            Url = builder.Configuration["Dmo:SupabaseUrl"] ?? string.Empty,
            PublishableKey = builder.Configuration["Dmo:SupabasePublishableKey"] ?? string.Empty,
            SecretKey = builder.Configuration["Dmo:SupabaseSecretKey"] ?? string.Empty
        }));
    builder.Services.AddHttpClient<IAuthenticationProvider, SupabaseAuthenticationProvider>();
    // The Admin â†’ Users provisioning seam (USERS.md Â§2/Â§5): DMO asks the
    // Supabase Auth admin API to create/reset an authentication identity
    // with a backend-minted temporary password â€” the provider owns every
    // credential; DMO never becomes a password store. Same options and
    // secret key as the provider above.
    builder.Services.AddHttpClient<IUserProvisioningProvider, SupabaseUserProvisioningProvider>();
}
else
{
    // InMemory seam for local development and deterministic integration tests.
    builder.Services.AddSingleton<InMemoryAuthenticationProvider>(sp =>
    {
        var provider = new InMemoryAuthenticationProvider();
        var section = builder.Configuration.GetSection("Dmo:InMemoryAccounts");

        foreach (var child in section.GetChildren())
        {
            var identifier = child["Identifier"];
            var password = child["Password"];

            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            {
                continue;
            }

            var providerUserId = child["ProviderUserId"] ?? identifier;
            var email = child["Email"];
            var isTemporary = bool.TryParse(child["IsTemporaryPassword"], out var t) && t;

            provider.AddAccount(identifier, providerUserId, password, email, isTemporary);
        }

        return provider;
    });
    builder.Services.AddSingleton<IAuthenticationProvider>(sp =>
        sp.GetRequiredService<InMemoryAuthenticationProvider>());
    // The matching deterministic provisioning seam: users created in the
    // application (Admin â†’ Users) are immediately usable by the in-memory
    // login surface â€” the worked examples run end-to-end in Development
    // and in the tests without any real Supabase project.
    builder.Services.AddSingleton<IUserProvisioningProvider>(sp =>
        new InMemoryUserProvisioningProvider(sp.GetRequiredService<InMemoryAuthenticationProvider>()));
}

// BETA online: PostgreSQL is configured directly by the deployment.
builder.Services.AddDbContext<DmoDbContext>((serviceProvider, options) =>
{
    // O carimbo de quando e quem gravou cada Peso e Pegamentos (17/10/2026).
    options.AddInterceptors(serviceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Controlo.RegistoCarimboInterceptor>());
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var databaseConnection = config.GetConnectionString("DmoDatabase");
    var devPoolerHost = config["Dmo:DevPoolerHost"];
    if (!string.IsNullOrWhiteSpace(databaseConnection) &&
        !string.IsNullOrWhiteSpace(devPoolerHost))
    {
        // Only DMO-DEV can use this override. Reuse the dedicated DEV
        // runtime role/password already held in the Railway secret.
        if (!string.Equals(devPoolerHost, "aws-0-eu-west-1.pooler.supabase.com", StringComparison.Ordinal) ||
            !string.Equals(new NpgsqlConnectionStringBuilder(databaseConnection).Host,
                "db.gkblhzonflzwxvbbullv.supabase.co", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DEV pooler override is restricted to DMO-DEV.");

        var cs = new NpgsqlConnectionStringBuilder(databaseConnection)
        {
            Host = devPoolerHost,
            Port = 5432
        };
        var username = cs.Username ?? string.Empty;
        if (!username.EndsWith(".gkblhzonflzwxvbbullv", StringComparison.Ordinal))
            cs.Username = username + ".gkblhzonflzwxvbbullv";
        databaseConnection = cs.ConnectionString;
    }
    if (!string.IsNullOrWhiteSpace(databaseConnection))
        options.UseNpgsql(databaseConnection, npgsql =>
            npgsql.MigrationsAssembly(typeof(DmoDbContext).Assembly.FullName));
    else if (builder.Environment.IsDevelopment())
        // Preserve the existing local Development/test seam.
        options.UseInMemoryDatabase("DmoAlphaModule1");
    else
        // Register the real provider without inventing a connection. Login and
        // the error page remain available; database operations fail explicitly
        // until deployment configuration supplies the PostgreSQL connection.
        options.UseNpgsql(npgsql =>
            npgsql.MigrationsAssembly(typeof(DmoDbContext).Assembly.FullName));
});

// -------------------------------------------------------------------------
// Runtime services
// -------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentAccountContext, HttpContextCurrentAccountContext>();
builder.Services.AddSingleton<DMO.Alpha.Infrastructure.Controlo.RegistoCarimboInterceptor>();
builder.Services.AddScoped<ShellStateFactory>();
builder.Services.AddScoped<DmoSignInService>();
// Boquilhas write path: registo de um movimento num bq_repair_trace_id existente.
builder.Services.AddScoped<IRegisterBqMovementHandler, RegisterBqMovementHandler>();
// Boquilhas: correção e eliminação de movimentos já gravados.
builder.Services.AddScoped<IEditBqMovementHandler, EditBqMovementHandler>();
builder.Services.AddScoped<IResetBqDiscrepancyHandler, ResetBqDiscrepancyHandler>();
builder.Services.AddScoped<IDeleteBqMovementHandler, DeleteBqMovementHandler>();
// Boquilhas read path: read model consumer-specific do surface Registo.
builder.Services.AddScoped<IBoquilhasRegistoQuery, BoquilhasRegistoQuery>();
// Boquilhas write path: criar um trace ancorado a um tool_id e associÃ¡-lo
// mais tarde ao seu bq_id de produÃ§Ã£o.
builder.Services.AddScoped<ICreateBqRepairTraceHandler, CreateBqRepairTraceHandler>();
builder.Services.AddScoped<IAssociateBqRepairTraceToContextHandler, AssociateBqRepairTraceToContextHandler>();
// Boquilhas read path (o corte F): a produção corrente de cada máquina
// (a última produção enquanto o Job On não tem datas — a regra F4-4).
builder.Services.AddScoped<IBoquilhasCurrentQuery, BoquilhasCurrentQuery>();
// Boquilhas read path (o corte F): o histórico de produções de uma
// boquilha pelos context ids (a traçabilidade reversa: os jobons em que
// a ferramenta foi usada) + o acesso ao contexto bq_id + O CALENDÁRIO DO
// HISTÓRICO (os movimentos do dia e os pontos do mês, com os filtros por
// reparador/máquina/referência).
builder.Services.AddScoped<IBqToolHistoryQuery, BqToolHistoryQuery>();
// Boquilhas write path (o bloco dos reparadores): as ENTIDADES
// REPARADORAS — o vocabulário do encaminhamento por máquina (o Owner
// 2026-10-12: «reparadores sao as entidades para onde enviamos as
// boquilhas»), administrado em Admin → App Definições → Boquilhas.
builder.Services.AddScoped<ICreateBqRepairerHandler, CreateBqRepairerHandler>();
builder.Services.AddScoped<IEditBqRepairerHandler, EditBqRepairerHandler>();
// Boquilhas read path: a lista das entidades reparadoras para a Admin.
builder.Services.AddScoped<IBqRepairerQuery, BqRepairerQuery>();
// Tools read path: resoluÃ§Ã£o canÃ³nica e sÃ³ de leitura de uma Tool pelo seu tool_id exato.
builder.Services.AddScoped<IToolLookupQuery, ToolLookupQuery>();
// Tools read path: descoberta de candidatos para seleÃ§Ã£o humana explÃ­cita
// (filtros explÃ­citos aplicados no servidor; apenas reduzem candidatos).
builder.Services.AddScoped<IToolCandidateQuery, ToolCandidateQuery>();
builder.Services.AddScoped<IToolLotQuantitySettings, ToolLotQuantitySettings>();
// Tools read path: contexto tÃ©cnico Tool-owned (valores consumidos pelo Peso,
// completÃ¡veis no fluxo e gravados no tool_id owner â€” regra Ferramentas).
builder.Services.AddScoped<IToolTechnicalQuery, ToolTechnicalQuery>();
// Ferramentas write path (corte A â€” a RAIZ da cascata tool_id â†’ jobon_id â†’
// controlo_id â†’ pegamentos/peso): registar a Tool real (tool_id SEMPRE
// mintado pelo backend), editar factos e valores tÃ©cnicos (full-replace no
// mesmo tool_id), duplicar (lote novo obrigatÃ³rio, fonte intacta) e eliminar
// (transaÃ§Ã£o canÃ³nica com reatribuiÃ§Ã£o Ã  DUMMY). Recusas tipadas via enums
// fechados â€” formato de referÃªncia Ã© hint, nunca recusa.
builder.Services.AddScoped<ICreateToolHandler, CreateToolHandler>();
builder.Services.AddScoped<IEditToolFactsHandler, EditToolFactsHandler>();
builder.Services.AddScoped<IEditToolTechnicalValuesHandler, EditToolTechnicalValuesHandler>();
builder.Services.AddScoped<IDuplicateToolHandler, DuplicateToolHandler>();
builder.Services.AddScoped<IDeleteToolHandler, DeleteToolHandler>();
// Ferramentas read path: a ficha purpose-specific por tool_id (factos +
// mÃ¡quinas + contexto tÃ©cnico), o aviso de eliminaÃ§Ã£o (produÃ§Ãµes afetadas +
// contagens dos registos de consumo) e a compatibilidade de mÃ¡quinas da
// lista â€” a linha DUMMY (REMOVED) Ã© excluÃ­da por constante em todas.
builder.Services.AddScoped<IToolSheetQuery, ToolSheetQuery>();
builder.Services.AddScoped<IToolDeletionImpactQuery, ToolDeletionImpactQuery>();
// Controlo/Peso write path: criar um Peso tool-scoped (sem produÃ§Ã£o/Job On)
// e registar leituras reais (factos histÃ³ricos).
builder.Services.AddScoped<ICreateToolScopedPesoHandler, CreateToolScopedPesoHandler>();
// Controlo/Peso write path (corte C): o Peso production-scoped â€” criar/abrir
// o Peso baseline de um controlo, as ferramentas herdadas do contexto.
builder.Services.AddScoped<ICreatePesoForControloHandler, CreatePesoForControloHandler>();
builder.Services.AddScoped<IAssociatePesoToControloHandler, AssociatePesoToControloHandler>();
builder.Services.AddScoped<IPesoAssociationQuery, PesoAssociationQuery>();
builder.Services.AddScoped<IRecordPesoMeasurementHandler, RecordPesoMeasurementHandler>();
// Controlo/Peso read path: read model do Peso e histÃ³rico de Pesos por tool.
builder.Services.AddScoped<IPesoQuery, PesoQuery>();
// Controlo/Peso read path (corte C â€” ComparaÃ§Ã£o_B): os candidatos histÃ³ricos
// da mesma referÃªncia e o diferencial pela posiÃ§Ã£o fÃ­sica das leituras.
builder.Services.AddScoped<IPesoComparisonQuery, PesoComparisonQuery>();
// Controlo configuration read path: densidade do vidro configurada por processo
// (a ediÃ§Ã£o pertence Ã  superfÃ­cie de administraÃ§Ã£o; o Peso consome e preserva).
builder.Services.AddScoped<IGlassDensityConfiguration, GlassDensityConfiguration>();
// Controlo configuration read/write path (o corte PDF): as definiÃ§Ãµes
// funcionais POR MÃ“DULO (spec CONFIGURATION.md + a decisÃ£o do Owner
// 2026-10-11) â€” o directÃ³rio de saÃ­da dos documentos do Controlo e a
// hora de viragem do dia operacional das Boquilhas.
builder.Services.AddScoped<IModuleSettingsQuery, ModuleSettingsQuery>();
builder.Services.AddScoped<ISaveModuleSettingHandler, SaveModuleSettingHandler>();
// O corte PDF: o gerador da folha de pesagem (o alvo 22) â€” stateless,
// o ficheiro grava no directÃ³rio configurado com o nome canÃ³nico do resolver.
builder.Services.AddSingleton<PesoPdfGenerator>();
// O APPROVE-MÃNIMO (a decisÃ£o GATE 4): o registo de visÃ£o da folha do
// controlo â€” quem abriu e quando; nunca Ã© gate.
builder.Services.AddScoped<ControloViewingHandler>();
// O SELO DE APROVAÇÃO (o corte D): o chefe marca o controlo — o estado
// vive no próprio registo; nunca gate.
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.ControloAreaStateHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PuncoesHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PuncoesPdfGenerator>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Production.JobOnLotNoteHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Production.JobOnChangeQuery>();
builder.Services.AddScoped<DMO.Alpha.Core.Controlo.IPesoCapSupportQuery, DMO.Alpha.Infrastructure.Controlo.PesoCapSupportQuery>();
// O MODO CHEFE (o corte H-lite, o Owner 2026-10-12: Â«controlo.approve Ã© o
// que o chefe tem, nao tem opÃ§ao de criar peso nem pegamentosÂ»): as
// capabilities efetivas do user logado pelo serviÃ§o canÃ³nico do Admin â€”
// o split Controlo Create (os forms) vs Approve (o ver: as folhas, o
// histÃ³rico, a aprovaÃ§Ã£o).
builder.Services.AddScoped<CurrentUserCapabilities>();
// Controlo write path: ensure do controlo_id de uma produÃ§Ã£o â€” get-or-create
// idempotente por jobon_id (spec Job On L33: o controlo_id nasce com o Job On;
// na abertura do Resumo faz o backfill das produÃ§Ãµes criadas antes deste
// corte). Bookkeeping idempotente, nunca um ato de criaÃ§Ã£o do utilizador.
builder.Services.AddScoped<IEnsureControloForJobOnHandler, EnsureControloForJobOnHandler>();
// Production/Job On write path: criar um Job On â€” ocorrÃªncia real de produÃ§Ã£o
// com as Tools canÃ³nicas explicitamente selecionadas; o backend emite o
// jobon_id e os contextos aplicÃ¡veis (cm_id/mf_id/bq_id).
builder.Services.AddScoped<ICreateJobOnHandler, CreateJobOnHandler>();
// Production/Job On write path: EDITAR um Job On existente — a produção
// re-planeada (a janela temporal, a máquina, os tool_ids) SEM criar outra:
// o jobon_id é o mesmo, a identidade imutável e o BqContext o MESMO contexto
// com o ToolId novo (os factos históricos ancorados ficam).
builder.Services.AddScoped<IUpdateJobOnHandler, UpdateJobOnHandler>();
// Production read path: a query canÃ³nica do contexto de produÃ§Ã£o por
// jobon_id (a mesma que os mÃ³dulos seguintes consomem) e a consulta incluÃ­da
// na capability Job On Create (lista dos Job Ons com o contexto estabelecido)
// + mÃ¡quinas persistidas para seleÃ§Ã£o explÃ­cita.
builder.Services.AddScoped<IProductionContextQuery, ProductionContextQuery>();
builder.Services.AddScoped<IJobOnListQuery, JobOnListQuery>();
builder.Services.AddScoped<IProductionPickerQuery, ProductionPickerQuery>();
// As sugestões ao escrever dos campos de procura (DMO-044): uma consulta
// curta por pedido, servida por GET /Sugestoes e usada por js/dmo-suggest.js.
builder.Services.AddScoped<DMO.Alpha.Core.Search.ISuggestionQuery, DMO.Alpha.Infrastructure.Search.SuggestionQuery>();
builder.Services.AddScoped<IMachineListQuery, MachineListQuery>();
// Controlo read path (browser Â«Procurar resumoÂ»): produÃ§Ãµes com os factos
// mÃ­nimos de identificaÃ§Ã£o, mais recentes primeiro, com filtro por
// referÃªncia de IGUALDADE EXATA aplicado no servidor (query
// purpose-specific; a produÃ§Ã£o a abrir Ã© sempre escolha humana explÃ­cita).
builder.Services.AddScoped<IResumoProductionQuery, ResumoProductionQuery>();
// A Revisão do Controlo por calendário (17/10/2026): os dias com controlos e os por aprovar.
builder.Services.AddScoped<DMO.Alpha.Core.Controlo.IControloRevisaoQuery, DMO.Alpha.Infrastructure.Controlo.ControloRevisaoQuery>();
// O PLANEAMENTO (o corte G): o calendário das produções por máquina —
// as DATAS do Job On (o corte B) alimentam a grelha.
builder.Services.AddScoped<IJobOnToolFactsQuery, JobOnToolFactsQuery>();
// O PDF dos Pegamentos (o gerador; o resolver é o ControloDocumentPaths
// canónico — PEGAMENTOS-<ref>-<produção>.pdf).
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PegamentosPdfGenerator>();
// O PDF do Resumo (a folha do controlo inteira — RESUMO-<ref>-<produção>.pdf).
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.ResumoPdfGenerator>();
// Controlo/Pegamentos write path: registar uma leitura dimensional real
// (gesto em cruz) num componente da combinaÃ§Ã£o CM+BQ+MF herdada do contexto
// Job On ativo â€” a tool nunca Ã© selecionada na folha; a primeira mediÃ§Ã£o
// cria o registo durÃ¡vel (pegamentos_id) com as tools e a tolerÃ¢ncia do
// corredor congeladas; o gargalo em falta Ã© completado no fluxo e grava no
// tool_id owner (regra Ferramentas).
builder.Services.AddScoped<IRecordPegamentosMeasurementHandler, RecordPegamentosMeasurementHandler>();
builder.Services.AddScoped<ICorrectPegamentosMeasurementHandler, CorrectPegamentosMeasurementHandler>();
builder.Services.AddScoped<ICreateToolScopedPegamentosHandler, CreateToolScopedPegamentosHandler>();
builder.Services.AddScoped<IAssociatePegamentosToControloHandler, AssociatePegamentosToControloHandler>();
// Controlo/Pegamentos read path: a projeÃ§Ã£o purpose-specific da FOLHA por
// controlo_id (a LEI: Â«com controlo_id ganhas o contexto todo para
// pegamentosÂ») â€” contexto canÃ³nico da produÃ§Ã£o, gargalos correntes das
// tools, tolerÃ¢ncia efetiva corrente e o registo durÃ¡vel quando existe,
// com as leituras avaliadas a partir dos factos congelados.
builder.Services.AddScoped<IPegamentosQuery, PegamentosQuery>();
// Controlo configuration read path: tolerÃ¢ncia do corredor de Pegamentos
// configurada (a ediÃ§Ã£o pertence Ã  superfÃ­cie de administraÃ§Ã£o; a criaÃ§Ã£o
// do registo consome o valor efetivo e preserva-o congelado).
builder.Services.AddScoped<IPegamentosToleranceConfiguration, PegamentosToleranceConfiguration>();
builder.Services.AddScoped<ISavePegamentosToleranceHandler, SavePegamentosToleranceHandler>();

// Admin foundation (Step B): the server-side authorization semantics
// (effective access over the persisted Userâ†”Template relationship + the
// ADMIN-boundary template-assignment authorization) and the three Admin
// read views over that foundation (Templates, Users, App DefiniÃ§Ãµes
// areas). The functional management workflows (Steps C/D â€” this build)
// extend these same contracts.
builder.Services.AddScoped<IAdminAuthorizationService, AdminAuthorizationService>();
// O ADMIN é o email guardado nas Definições: o setting canónico que o
// login, o validador de sessão e os boundary checks leem.
builder.Services.AddScoped<IAdminEmailSettings, AdminEmailSettings>();
builder.Services.AddScoped<ITemplateAdminService, TemplateAdminService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();

// -------------------------------------------------------------------------
// Authentication & authorization
// -------------------------------------------------------------------------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "DmoAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        // ADMIN-only session invalidation: the session's email claim must
        // still be the email kept in the Definições (o ADMIN é o email —
        // o Owner 2026-10-16). Operator principals are untouched; changing
        // the field ends the previous ADMIN's cookie at its next request.
        options.Events.OnValidatePrincipal = AdminSessionCookieValidator.ValidateAsync;
    });

// Only authenticated ADMIN users can enter the Administration.
builder.Services.AddScoped<IAuthorizationHandler, AdminAreaAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuthorizationPolicies.RequireAdmin, policy =>
        policy.Requirements.Add(new AdminAreaRequirement()));
});
builder.Services.AddAntiforgery();

// Razor writes pt-PT text (Ã , Ã§, Â·, â€”) literally instead of numeric entities:
// the default HtmlEncoder entity-encodes every non-Basic-Latin character.
// HTML-sensitive characters (<, >, &, ", ') remain encoded regardless.
builder.Services.AddSingleton<HtmlEncoder>(
    HtmlEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.GeneralPunctuation));

// -------------------------------------------------------------------------
// Razor Pages
// -------------------------------------------------------------------------
// Admin routes remain restricted to the ADMIN claim.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", AdminAuthorizationPolicies.RequireAdmin);
});

// Durable DataProtection keyring: antiforgery tokens and the DmoAuth cookie
// must survive restarts and redeploys. The Railway container has no volume,
// so a directory under /app was lost on every deploy («key ... was not found
// in the key ring»). Order:
//   1. Dmo:DataProtectionKeyRingPath, when configured, wins (file system).
//   2. With ConnectionStrings:DmoDatabase (PostgreSQL), the keyring lives in
//      the application database (table "DataProtectionKeys").
//   3. Otherwise (local Development/tests with InMemory) the
//      content-root-adjacent "data-protection" directory.
// DPAPI protection stays Windows-only. OPS NOTE: without the certificate the
// keys are stored unencrypted (file or table); access is limited by the
// filesystem / database permissions.
var dataProtection = builder.Services.AddDataProtection();
var dataProtectionKeyRingPath = builder.Configuration["Dmo:DataProtectionKeyRingPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyRingPath));
}
else if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DmoDatabase")))
{
    dataProtection.PersistKeysToDbContext<DmoDbContext>();
}
else
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "data-protection")));
}

// A CHAVE-MESTRA PORTÁTIL (o certificado X509): quando Dmo:DataProtectionCertPfx
// está definida (base64 do PFX), o keyring passa a proteger-se com o CERTIFICADO —
// uma chave que WINDOWS E LINUX abrem igual. Sem o cert: o DPAPI (Windows-only,
// o que matou o Railway 12/10 — o key {bb434b71...} ineligible: PlatformNotSupported
// no Linux). O cert VEM PRIMEIRO; o DPAPI fica como fallback Windows quando não
// há cert.
var dataProtectionCertPfx = builder.Configuration["Dmo:DataProtectionCertPfx"];
if (!string.IsNullOrWhiteSpace(dataProtectionCertPfx))
{
    var dataProtectionCertPassword = builder.Configuration["Dmo:DataProtectionCertPassword"];
    var dataProtectionCertificate = X509CertificateLoader.LoadPkcs12(
        Convert.FromBase64String(dataProtectionCertPfx), dataProtectionCertPassword);
    dataProtection.ProtectKeysWithCertificate(dataProtectionCertificate);
}
else if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    if (!string.Equals(authProviderName, "Supabase", StringComparison.OrdinalIgnoreCase))
    {
        app.Logger.LogWarning(
            "Authentication and user provisioning use Supabase outside Development. " +
            "Set Dmo__AuthenticationProvider=Supabase explicitly; InMemory is only available in Development.");
    }

    if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DmoDatabase")))
    {
        app.Logger.LogError(
            "ConnectionStrings__DmoDatabase is missing. PostgreSQL operations are unavailable; " +
            "no InMemory database will be used outside Development. Login and the error page remain available.");
    }
}

// Optional read-only diagnostics for the existing online DEV service.
// This is not an access gate or a migration: failure is only
// logged, and the application still starts normally.
if (app.Configuration.GetValue<bool>("Dmo:DevConnectionProbe"))
{
    try
    {
        using var probeScope = app.Services.CreateScope();
        var probeDb = probeScope.ServiceProvider.GetRequiredService<DmoDbContext>();
        var users = await probeDb.Users.AsNoTracking().CountAsync();
        var jobOns = await probeDb.JobOns.AsNoTracking().CountAsync();
        app.Logger.LogInformation("DMO-BETA DEV database verified: Users={Users}, JobOns={JobOns}", users, jobOns);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("DMO-BETA DEV database probe failed: {ExceptionType}", ex.GetType().Name);
    }
}

// Resolve the database provider at boot without changing any data.
using (var scope = app.Services.CreateScope())
{
    _ = scope.ServiceProvider.GetRequiredService<DbContextOptions<DmoDbContext>>();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();

// Admin-vs-operational gate.
// A fronteira ADMIN não é apenas uma regra da barra de navegação.
// O ADMIN não tem Template nem capacidades operacionais, pelo que nem
// URLs diretos nem POSTs podem aceder a Job On/Controlo/Boquilhas/Ferramentas.
// A regra vive na pipeline autenticada (não no CSS/Razor) e antecede
// os PageModels; os operadores continuam sujeitos às capabilities próprias.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && context.User.HasClaim(DmoClaimTypes.IsAdmin, bool.TrueString))
    {
        var path = context.Request.Path;
        var operationalPath =
            path.StartsWithSegments("/JobOn")
            || path.StartsWithSegments("/Planeamento")
            || path.StartsWithSegments("/Controlo")
            || path.StartsWithSegments("/Boquilhas")
            || path.StartsWithSegments("/Ferramentas")
            || path.StartsWithSegments("/Modules")
            || path.StartsWithSegments("/Sugestoes")
            || path == "/";

        if (operationalPath)
        {
            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                context.Response.Redirect("/Admin/Users");
            else
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }

    await next();
});

app.UseAuthorization();

// Force password change before any operational page when the claim is present.
app.Use(async (context, next) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true &&
        user.HasClaim(DmoClaimTypes.RequiresPasswordChange, bool.TrueString))
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (!string.Equals(path.TrimEnd('/'), "/error", StringComparison.Ordinal) &&
            !string.Equals(path.TrimEnd('/'), "/versao", StringComparison.Ordinal) &&
            !path.StartsWith("/change-password", StringComparison.Ordinal) &&
            !path.StartsWith("/logout", StringComparison.Ordinal) &&
            !path.StartsWith("/login", StringComparison.Ordinal) &&
            !path.StartsWith("/css/", StringComparison.Ordinal) &&
            !path.StartsWith("/js/", StringComparison.Ordinal) &&
            !path.StartsWith("/_", StringComparison.Ordinal))
        {
            context.Response.Redirect("/change-password");
            return;
        }
    }

    await next();
});

app.MapRazorPages();

// As sugestões ao escrever (DMO-044): só para sessões autenticadas; fonte e
// tipo fechados — um valor desconhecido devolve uma lista vazia.
app.MapGet("/Sugestoes", async (
    string? fonte, string? q, string? tipo,
    DMO.Alpha.Core.Search.ISuggestionQuery suggestions, HttpContext context) =>
{
    var source = fonte?.Trim().ToLowerInvariant() switch
    {
        "producoes" => DMO.Alpha.Core.Search.SuggestionSource.Producoes,
        "referencias" => DMO.Alpha.Core.Search.SuggestionSource.Referencias,
        "numeros" => DMO.Alpha.Core.Search.SuggestionSource.NumerosProducao,
        "ferramentas" => DMO.Alpha.Core.Search.SuggestionSource.Ferramentas,
        "reparadores" => DMO.Alpha.Core.Search.SuggestionSource.Reparadores,
        _ => (DMO.Alpha.Core.Search.SuggestionSource?)null
    };
    if (source is null)
        return Results.Json(Array.Empty<DMO.Alpha.Core.Search.Suggestion>());

    var toolType = tipo?.Trim().ToLowerInvariant() switch
    {
        "cm" => ToolType.Cm,
        "mf" => ToolType.Mf,
        "bq" => ToolType.Bq,
        _ => (ToolType?)null
    };
    var items = await suggestions.SuggestAsync(source.Value, q, toolType, context.RequestAborted);
    return Results.Json(items.Select(item => new { value = item.Value, label = item.Label }));
}).RequireAuthorization();

app.Run();
return 0;

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
    builder.Services.AddHttpClient<IAuthenticationProvider, SupabaseAuthenticationProvider>(client =>
        client.Timeout = SupabaseAuthenticationProvider.RequestTimeout);
    // The Admin → Users provisioning seam (USERS.md §2/§5): DMO asks the
    // Supabase Auth admin API to create/reset an authentication identity
    // with a backend-minted temporary password — the provider owns every
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
    // application (Admin → Users) are immediately usable by the in-memory
    // login surface — the worked examples run end-to-end in Development
    // and in the tests without any real Supabase project.
    builder.Services.AddSingleton<IUserProvisioningProvider>(sp =>
        new InMemoryUserProvisioningProvider(sp.GetRequiredService<InMemoryAuthenticationProvider>()));
}

// BETA online: PostgreSQL is configured directly by the deployment.
builder.Services.AddDbContext<DmoDbContext>((serviceProvider, options) =>
{
    // O carimbo de quando e quem gravou cada Peso e Pegamentos (17/10/2026).
    options.AddInterceptors(serviceProvider.GetRequiredService<DMO.Alpha.Infrastructure.Controlo.RegistoCarimboInterceptor>());
    // PERF-01: conta as consultas e o tempo da base de cada pedido.
    options.AddInterceptors(serviceProvider.GetRequiredService<DMO.Alpha.Web.Services.MedicaoPedidoInterceptor>());
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
builder.Services.AddSingleton<DMO.Alpha.Web.Services.MedicaoPedidoInterceptor>();
builder.Services.AddScoped<ShellStateFactory>();
builder.Services.AddSingleton<EnviosGravados>();
// O envio único guardado na base, no mesmo SaveChanges do facto (E4 do DMO-FULL).
builder.Services.AddScoped<IOperacoesRegistadas, OperacoesRegistadas>();
builder.Services.AddScoped<DmoSignInService>();
// Boquilhas write path: registo de um movimento num bq_repair_trace_id existente.
builder.Services.AddScoped<IRegisterBqMovementHandler, RegisterBqMovementHandler>();
// Boquilhas: correção e eliminação de movimentos já gravados.
builder.Services.AddScoped<IEditBqMovementHandler, EditBqMovementHandler>();
builder.Services.AddScoped<IResetBqDiscrepancyHandler, ResetBqDiscrepancyHandler>();
builder.Services.AddScoped<IDeleteBqMovementHandler, DeleteBqMovementHandler>();
// Boquilhas read path: read model consumer-specific do surface Registo.
builder.Services.AddScoped<IBoquilhasRegistoQuery, BoquilhasRegistoQuery>();
// Boquilhas write path: criar um trace ancorado a um tool_id e associá-lo
// mais tarde ao seu bq_id de produção.
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
builder.Services.AddScoped<IBqExcelBoquilhas, BqExcelBoquilhas>();
// Boquilhas write path (o bloco dos reparadores): as ENTIDADES
// REPARADORAS — o vocabulário do encaminhamento por máquina (o Owner
// 2026-10-12: «reparadores sao as entidades para onde enviamos as
// boquilhas»), administrado em Definições → Admin → App Definições → Boquilhas.
builder.Services.AddScoped<ICreateBqRepairerHandler, CreateBqRepairerHandler>();
builder.Services.AddScoped<IEditBqRepairerHandler, EditBqRepairerHandler>();
builder.Services.AddScoped<IDeleteBqRepairerHandler, DeleteBqRepairerHandler>();
// Boquilhas read path: a lista das entidades reparadoras para a Admin.
builder.Services.AddScoped<IBqRepairerQuery, BqRepairerQuery>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IArmazemPosicoes, DMO.Alpha.Infrastructure.Armazem.ArmazemPosicoes>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IReparadores, DMO.Alpha.Infrastructure.Armazem.Reparadores>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IArmazemMovimentos, DMO.Alpha.Infrastructure.Armazem.ArmazemMovimentos>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IArmazemLotesNovos, DMO.Alpha.Infrastructure.Armazem.ArmazemLotesNovos>();
builder.Services.AddScoped<DMO.Alpha.Core.Programada.IReparacaoProgramada, DMO.Alpha.Infrastructure.Programada.ReparacaoProgramadaServico>();
// Reparação Interna: a produção do dia, registar e a lista por baixo do registo;
// a Consulta dos chefes (só lê).
builder.Services.AddScoped<DMO.Alpha.Core.ReparacaoInterna.IReparacoesInternas, DMO.Alpha.Infrastructure.ReparacaoInterna.ReparacoesInternas>();
builder.Services.AddScoped<DMO.Alpha.Core.ReparacaoInterna.IReparacoesInternasConsulta, DMO.Alpha.Infrastructure.ReparacaoInterna.ReparacoesInternasConsulta>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IArmazemMapa, DMO.Alpha.Infrastructure.Armazem.ArmazemMapa>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IArmazemHistorico, DMO.Alpha.Infrastructure.Armazem.ArmazemHistorico>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IHistoriaDasReparacoes, DMO.Alpha.Infrastructure.Armazem.HistoriaDasReparacoes>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IArmazemReparacao, DMO.Alpha.Infrastructure.Armazem.ArmazemReparacao>();
builder.Services.AddScoped<DMO.Alpha.Core.Armazem.IArmazemProximos, DMO.Alpha.Infrastructure.Armazem.ArmazemProximos>();
builder.Services.AddScoped<DMO.Alpha.Core.Tools.IFerramentasUsoSap, DMO.Alpha.Infrastructure.Tools.FerramentasUsoSap>();
builder.Services.AddScoped<DMO.Alpha.Core.Tools.IFerramentasFabricoSap, DMO.Alpha.Infrastructure.Tools.FerramentasFabricoSap>();
builder.Services.AddScoped<DMO.Alpha.Core.Tools.IFerramentaHistorico, DMO.Alpha.Infrastructure.Tools.FerramentaHistoricoQuery>();
// Tools read path: resolução canónica e só de leitura de uma Tool pelo seu tool_id exato.
builder.Services.AddScoped<IToolLookupQuery, ToolLookupQuery>();
builder.Services.AddScoped<IPegamentosEscolhasQuery, PegamentosEscolhasQuery>();
// Tools read path: descoberta de candidatos para seleção humana explícita
// (filtros explícitos aplicados no servidor; apenas reduzem candidatos).
builder.Services.AddScoped<IToolCandidateQuery, ToolCandidateQuery>();
builder.Services.AddScoped<IToolLotQuantitySettings, ToolLotQuantitySettings>();
// Tools read path: contexto técnico Tool-owned (valores consumidos pelo Peso,
// completáveis no fluxo e gravados no tool_id owner — regra Ferramentas).
builder.Services.AddScoped<IToolTechnicalQuery, ToolTechnicalQuery>();
// Ferramentas write path (corte A — a RAIZ da cascata tool_id → jobon_id →
// controlo_id → pegamentos/peso): registar a Tool real (tool_id SEMPRE
// mintado pelo backend), editar factos e valores técnicos (full-replace no
// mesmo tool_id), duplicar (lote novo obrigatório, fonte intacta) e eliminar
// (transação canónica com reatribuição à DUMMY). Recusas tipadas via enums
// fechados — formato de referência é hint, nunca recusa.
builder.Services.AddScoped<ICreateToolHandler, CreateToolHandler>();
builder.Services.AddScoped<IEditToolFactsHandler, EditToolFactsHandler>();
builder.Services.AddScoped<IEditToolTechnicalValuesHandler, EditToolTechnicalValuesHandler>();
builder.Services.AddScoped<IDuplicateToolHandler, DuplicateToolHandler>();
builder.Services.AddScoped<IDeleteToolHandler, DeleteToolHandler>();
// Ferramentas read path: a ficha purpose-specific por tool_id (factos +
// máquinas + contexto técnico), o aviso de eliminação (produções afetadas +
// contagens dos registos de consumo) e a compatibilidade de máquinas da
// lista — a linha DUMMY (REMOVED) é excluída por constante em todas.
builder.Services.AddScoped<IToolSheetQuery, ToolSheetQuery>();
builder.Services.AddScoped<IToolDeletionImpactQuery, ToolDeletionImpactQuery>();
// Controlo/Peso write path: criar um Peso tool-scoped (sem produção/Job On)
// e registar leituras reais (factos históricos).
builder.Services.AddScoped<ICreateToolScopedPesoHandler, CreateToolScopedPesoHandler>();
// Controlo/Peso write path (corte C): o Peso production-scoped — criar/abrir
// o Peso baseline de um controlo, as ferramentas herdadas do contexto.
builder.Services.AddScoped<ICreatePesoForControloHandler, CreatePesoForControloHandler>();
builder.Services.AddScoped<IAssociatePesoToControloHandler, AssociatePesoToControloHandler>();
builder.Services.AddScoped<IPesoAssociationQuery, PesoAssociationQuery>();
builder.Services.AddScoped<IRecordPesoMeasurementHandler, RecordPesoMeasurementHandler>();
// Controlo/Peso read path: read model do Peso e histórico de Pesos por tool.
builder.Services.AddScoped<IPesoQuery, PesoQuery>();
builder.Services.AddScoped<IPesoFolhaQuery, PesoFolhaQuery>();
builder.Services.AddScoped<IPesoEscolhasQuery, PesoEscolhasQuery>();
builder.Services.AddScoped<IPesoComparacaoEstadoQuery, PesoComparacaoEstadoQuery>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PesoProducaoAnteriorQuery>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.RevisaoControloAnteriorQuery>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PegamentosHistoricoQuery>();
// Controlo/Peso read path (corte C — Comparação_B): os candidatos históricos
// da mesma referência e o diferencial pela posição física das leituras.
// O Peso usa também as comparações gravadas (fora da interface): a mesma
// instância por pedido para as duas.
builder.Services.AddScoped<PesoComparisonQuery>();
builder.Services.AddScoped<IPesoComparisonQuery>(sp => sp.GetRequiredService<PesoComparisonQuery>());
builder.Services.AddScoped<ICreatePesoComparacaoHandler, CreatePesoComparacaoHandler>();
// As ferramentas herdadas da produção (o Peso) e a procura do histórico do Controlo.
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PesoToolFactsQuery>();
builder.Services.AddScoped<IControloHistorySearchQuery, DMO.Alpha.Infrastructure.Controlo.ControloHistorySearchQuery>();
// Controlo configuration read path: densidade do vidro configurada por processo
// (a edição pertence à superfície de administração; o Peso consome e preserva).
builder.Services.AddScoped<IGlassDensityConfiguration, GlassDensityConfiguration>();
// Controlo configuration read/write path (o corte PDF): as definições
// funcionais POR MÓDULO (spec CONFIGURATION.md + a decisão do Owner
// 2026-10-11) — o directório de saída dos documentos do Controlo e a
// hora de viragem do dia operacional das Boquilhas.
builder.Services.AddScoped<IModuleSettingsQuery, ModuleSettingsQuery>();
builder.Services.AddScoped<ISaveModuleSettingHandler, SaveModuleSettingHandler>();
// O corte PDF: o gerador da folha de pesagem (o alvo 22) — stateless,
// o ficheiro grava no directório configurado com o nome canónico do resolver.
builder.Services.AddSingleton<PesoPdfGenerator>();
// O PDF do Peso num só sítio: o «Exportar PDF» e o «Enviar» do Resumo.
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PesoPdfDocumentBuilder>();
// O PDF do Resumo num só sítio: o «Exportar PDF» do Resumo e o da Revisão.
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.ResumoPdfDocumentBuilder>();
// O envio do PDF do Peso por email (D9): o email abre no Outlook do PC, sem
// conta no servidor; as listas e os modelos estão nas Definições.
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.EnvioEmailDefinicoes>();
builder.Services.AddScoped<DMO.Alpha.Core.Admin.IDefinicoesPessoais, DMO.Alpha.Infrastructure.Admin.DefinicoesPessoais>();
// As Definições num só sítio: a página /Definicoes mostra as secções com os modelos das páginas antigas.
builder.Services.AddScoped<DMO.Alpha.Web.Services.MudancaDePalavraPasse>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<DMO.Alpha.Core.Admin.IPinDasDefinicoes, DMO.Alpha.Infrastructure.Admin.PinDasDefinicoes>();
builder.Services.AddScoped<DMO.Alpha.Web.Services.DesbloqueioDasDefinicoes>();
builder.Services.AddScoped<DMO.Alpha.Web.Services.SumarioNovidades>();
builder.Services.AddTransient<DMO.Alpha.Web.Pages.Controlo.DefinicoesModel>();
builder.Services.AddTransient<DMO.Alpha.Web.Pages.Boquilhas.DefinicoesModel>();
builder.Services.AddTransient<DMO.Alpha.Web.Pages.Ferramentas.DefinicoesModel>();
builder.Services.AddTransient<DMO.Alpha.Web.Pages.Armazem.DefinicoesModel>();
builder.Services.AddTransient<DMO.Alpha.Web.Pages.Admin.Definicoes.IndexModel>();
builder.Services.AddTransient<DMO.Alpha.Web.Pages.Admin.CanalJobon.IndexModel>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.EnviarPesoPorEmailHandler>();
// O APPROVE-MÍNIMO (a decisão GATE 4): o registo de visão da folha do
// controlo — quem abriu e quando; nunca é gate.
builder.Services.AddScoped<ControloViewingHandler>();
// O SELO DE APROVAÇÃO (o corte D): o chefe marca o controlo — o estado
// vive no próprio registo; nunca gate.
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.ControloApprovalHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.ControloAreaStateHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Tools.ToolMcaliperLinks>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Tools.ToolDuplicateWarnings>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PuncoesHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PesoCmDeParteHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PuncoesPosicoesPdfGenerator>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PuncoesLinhaDefinicoes>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Production.JobOnLotNoteHandler>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Production.JobOnChangeQuery>();
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Production.RemoveJobOnChangeHandler>();
builder.Services.AddScoped<DMO.Alpha.Core.Planeamento.ISumarioQuery, DMO.Alpha.Infrastructure.Planeamento.SumarioQuery>();
builder.Services.AddScoped<DMO.Alpha.Core.Controlo.IPesoCapSupportQuery, DMO.Alpha.Infrastructure.Controlo.PesoCapSupportQuery>();
// O MODO CHEFE (o corte H-lite, o Owner 2026-10-12: «controlo.approve é o
// que o chefe tem, nao tem opçao de criar peso nem pegamentos»): as
// capabilities efetivas do user logado pelo serviço canónico do Admin —
// o split Controlo Create (os forms) vs Approve (o ver: as folhas, o
// histórico, a aprovação).
builder.Services.AddScoped<CurrentUserCapabilities>();
// Controlo write path: ensure do controlo_id de uma produção — get-or-create
// idempotente por jobon_id (spec Job On L33: o controlo_id nasce com o Job On;
// na abertura do Resumo faz o backfill das produções criadas antes deste
// corte). Bookkeeping idempotente, nunca um ato de criação do utilizador.
builder.Services.AddScoped<IEnsureControloForJobOnHandler, EnsureControloForJobOnHandler>();
// Production/Job On write path: criar um Job On — ocorrência real de produção
// com as Tools canónicas explicitamente selecionadas; o backend emite o
// jobon_id e os contextos aplicáveis (cm_id/mf_id/bq_id).
builder.Services.AddScoped<ICreateJobOnHandler, CreateJobOnHandler>();
// Production/Job On write path: EDITAR um Job On existente — a produção
// re-planeada (a janela temporal, a máquina, os tool_ids) SEM criar outra:
// o jobon_id é o mesmo, a identidade imutável e o BqContext o MESMO contexto
// com o ToolId novo (os factos históricos ancorados ficam).
builder.Services.AddScoped<IUpdateJobOnHandler, UpdateJobOnHandler>();
builder.Services.AddScoped<IDeleteJobOnHandler, DeleteJobOnHandler>();
// Production read path: a query canónica do contexto de produção por
// jobon_id (a mesma que os módulos seguintes consomem) e a consulta incluída
// na capability Job On Create (lista dos Job Ons com o contexto estabelecido)
// + máquinas persistidas para seleção explícita.
builder.Services.AddScoped<IProductionContextQuery, ProductionContextQuery>();
builder.Services.AddScoped<IJobOnListQuery, JobOnListQuery>();
builder.Services.AddScoped<IJobOnPaginaQuery, JobOnPaginaQuery>();
builder.Services.AddScoped<IProductionPickerQuery, ProductionPickerQuery>();
// As sugestões ao escrever dos campos de procura (DMO-044): uma consulta
// curta por pedido, servida por GET /Sugestoes e usada por js/dmo-suggest.js.
builder.Services.AddScoped<DMO.Alpha.Core.Search.ISuggestionQuery, DMO.Alpha.Infrastructure.Search.SuggestionQuery>();
builder.Services.AddScoped<IMachineListQuery, MachineListQuery>();
// Controlo read path (browser «Procurar resumo»): produções com os factos
// mínimos de identificação, mais recentes primeiro, com filtro por
// referência de IGUALDADE EXATA aplicado no servidor (query
// purpose-specific; a produção a abrir é sempre escolha humana explícita).
builder.Services.AddScoped<IResumoProductionQuery, ResumoProductionQuery>();
// A Revisão do Controlo por calendário (17/10/2026): os dias com controlos e os por aprovar.
builder.Services.AddScoped<DMO.Alpha.Core.Controlo.IControloRevisaoQuery, DMO.Alpha.Infrastructure.Controlo.ControloRevisaoQuery>();
// O PLANEAMENTO (o corte G): o calendário das produções por máquina —
// as DATAS do Job On (o corte B) alimentam a grelha.
builder.Services.AddScoped<IJobOnToolFactsQuery, JobOnToolFactsQuery>();
builder.Services.AddScoped<IJobOnFolhaQuery, JobOnFolhaQuery>();
// O PDF dos Pegamentos (o gerador; o resolver é o ControloDocumentPaths
// canónico — PEGAMENTOS-<ref>-<produção>.pdf).
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.PegamentosPdfGenerator>();
// O PDF do Resumo (a folha do controlo inteira — RESUMO-<ref>-<produção>.pdf).
builder.Services.AddScoped<DMO.Alpha.Infrastructure.Controlo.ResumoPdfGenerator>();
// Controlo/Pegamentos write path: registar uma leitura dimensional real
// (gesto em cruz) num componente da combinação CM+BQ+MF herdada do contexto
// Job On ativo — a tool nunca é selecionada na folha; a primeira medição
// cria o registo durável (pegamentos_id) com as tools e a tolerância do
// corredor congeladas; o gargalo em falta é completado no fluxo e grava no
// tool_id owner (regra Ferramentas).
builder.Services.AddScoped<IRecordPegamentosMeasurementHandler, RecordPegamentosMeasurementHandler>();
builder.Services.AddScoped<ICorrectPegamentosMeasurementHandler, CorrectPegamentosMeasurementHandler>();
builder.Services.AddScoped<ICreateToolScopedPegamentosHandler, CreateToolScopedPegamentosHandler>();
builder.Services.AddScoped<IAssociatePegamentosToControloHandler, AssociatePegamentosToControloHandler>();
// Controlo/Pegamentos read path: a projeção purpose-specific da FOLHA por
// controlo_id (a LEI: «com controlo_id ganhas o contexto todo para
// pegamentos») — contexto canónico da produção, gargalos correntes das
// tools, tolerância efetiva corrente e o registo durável quando existe,
// com as leituras avaliadas a partir dos factos congelados.
builder.Services.AddScoped<IPegamentosQuery, PegamentosQuery>();
// Controlo configuration read path: tolerância do corredor de Pegamentos
// configurada (a edição pertence à superfície de administração; a criação
// do registo consome o valor efetivo e preserva-o congelado).
builder.Services.AddScoped<IPegamentosToleranceConfiguration, PegamentosToleranceConfiguration>();
builder.Services.AddScoped<ISavePegamentosToleranceHandler, SavePegamentosToleranceHandler>();
builder.Services.AddScoped<ISaveGlassDensityHandler, SaveGlassDensityHandler>();

// Admin foundation (Step B): the server-side authorization semantics
// (effective access over the persisted User↔Template relationship + the
// ADMIN-boundary template-assignment authorization) and the three Admin
// read views over that foundation (Templates, Users, App Definições
// areas). The functional management workflows (Steps C/D — this build)
// extend these same contracts.
builder.Services.AddScoped<IAdminAuthorizationService, AdminAuthorizationService>();
// O ADMIN é o email guardado nas Definições: o setting canónico que o
// login, o validador de sessão e os boundary checks leem.
builder.Services.AddScoped<IAdminEmailSettings, AdminEmailSettings>();
// Sem interruptores dos módulos (Diogo, 22/10/2026: «isso já está no criar
// template»): só o template decide. As linhas Modulo:* e Modulos:Testadores
// que existam em ModuleSettings ficam lá, mas já não se leem.
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

// Razor writes pt-PT text (à, ç, ·, —) literally instead of numeric entities:
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
    // Um envio grava uma vez (deep sweep, 18/10/2026): os handlers com
    // [EnvioUnico] não gravam duas vezes a mesma chave X-Dmo-Envio.
    options.Conventions.ConfigureFilter(new EnvioUnicoFilter());
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

// O Railway termina o HTTPS à frente da app e passa o pedido em HTTP com
// X-Forwarded-Proto/For. Sem ler esses cabeçalhos a app julga-se em HTTP:
// os cookies da sessão saíam sem Secure e o HSTS nunca era enviado. A rede
// do proxy não é fixa, por isso aceita-se o cabeçalho de qualquer origem
// (o pior que um cliente consegue é marcar o próprio cookie como Secure).
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// A configuração em falta fica no log do arranque, pelo nome da variável e
// nunca pelo valor (sweep de 19/10/2026, 5.5). Nada trava o arranque.
foreach (var problem in StartupConfigurationCheck.Problems(app.Configuration, app.Environment.IsDevelopment(), useSupabase))
{
    app.Logger.Log(problem.Level, "{ConfigurationProblem}", problem.Message);
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

// PERF-01: mede o pedido completo (Server-Timing e uma linha no log); os
// ficheiros estáticos, servidos acima, ficam de fora. Ver docs/medicao-pedidos.md.
app.UseMedicaoPedidos();

app.UseRouting();
app.UseAuthentication();

// Depois de «Sair», o Voltar do browser não mostra as páginas da sessão
// (deep sweep B-admin-5): o browser não guarda as respostas da aplicação.
// Os ficheiros estáticos, servidos antes, continuam em cache.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey(Microsoft.Net.Http.Headers.HeaderNames.CacheControl))
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        return Task.CompletedTask;
    });

    await next();
});

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

        // As Definições num só sítio: o ADMIN muda a parte Geral pelos
        // handlers das páginas antigas de definições (o filtro
        // DefinicoesEntry só o deixa nos handlers [DefinicaoGeral]).
        var definicoesDoModulo = path.Value?.EndsWith("/Definicoes", StringComparison.OrdinalIgnoreCase) == true
            && HttpMethods.IsPost(context.Request.Method);

        if (operationalPath && !definicoesDoModulo)
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
// tipo fechados — um valor desconhecido devolve uma lista vazia. Cada fonte
// só responde a quem tem um dos módulos que a usam (as permissões são os
// módulos): sem esse módulo a lista vem vazia.
var suggestionModules = new Dictionary<string, string[]>(StringComparer.Ordinal)
{
    ["producoes"] = [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove],
    ["numeros"] = [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.ControloApprove],
    ["referencias"] = [DmoCapabilities.JobOnCreate, DmoCapabilities.ControloCreate, DmoCapabilities.Boquilhas, DmoCapabilities.ReparacaoInterna, DmoCapabilities.ReparacaoInternaView],
    ["ferramentas"] = [DmoCapabilities.Ferramentas, DmoCapabilities.ControloCreate, DmoCapabilities.Boquilhas],
    ["reparadores"] = [DmoCapabilities.Boquilhas, DmoCapabilities.BoquilhasView],
    ["referencias_bq"] = [DmoCapabilities.Boquilhas, DmoCapabilities.BoquilhasView],
    ["referencias_registo"] = [DmoCapabilities.Ferramentas],
};
app.MapGet("/Sugestoes", async (
    string? fonte, string? q, string? tipo,
    DMO.Alpha.Core.Search.ISuggestionQuery suggestions, CurrentUserCapabilities capabilities, HttpContext context) =>
{
    var fonteKey = fonte?.Trim().ToLowerInvariant() ?? string.Empty;
    var granted = await capabilities.GetAsync(context.RequestAborted);
    if (!suggestionModules.TryGetValue(fonteKey, out var modules) || !modules.Any(granted.Contains))
        return Results.Json(Array.Empty<DMO.Alpha.Core.Search.Suggestion>());

    var source = fonteKey switch
    {
        "producoes" => DMO.Alpha.Core.Search.SuggestionSource.Producoes,
        "referencias" => DMO.Alpha.Core.Search.SuggestionSource.Referencias,
        "numeros" => DMO.Alpha.Core.Search.SuggestionSource.NumerosProducao,
        "ferramentas" => DMO.Alpha.Core.Search.SuggestionSource.Ferramentas,
        "reparadores" => DMO.Alpha.Core.Search.SuggestionSource.Reparadores,
        "referencias_bq" => DMO.Alpha.Core.Search.SuggestionSource.ReferenciasEBq,
        "referencias_registo" => DMO.Alpha.Core.Search.SuggestionSource.ReferenciasRegisto,
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

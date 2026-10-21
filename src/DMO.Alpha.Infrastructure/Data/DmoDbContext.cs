using DMO.Alpha.Core.Admin;
using DMO.Alpha.Core.Armazem;
using DMO.Alpha.Core.Boquilhas;
// «Controlo» é simultaneamente o segmento do namespace
// (DMO.Alpha.Core.Controlo) e o nome da entidade do corte ensure+Resumo:
// os usos da ENTIDADE são sempre qualificados (DMO.Alpha.Core.Controlo.Controlo)
// para nunca colidirem com o segmento do namespace.
using DMO.Alpha.Core.Controlo;
using DMO.Alpha.Core.Identity;
using DMO.Alpha.Core.Production;
using DMO.Alpha.Core.Runtime;
using DMO.Alpha.Core.Tools;
using DMO.Alpha.Infrastructure.Admin;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DMO.Alpha.Infrastructure.Data;

public sealed class DmoDbContext : DbContext, IDataProtectionKeyContext
{
    public DmoDbContext(DbContextOptions<DmoDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// A versão da linha do Job On, da Tool e da informação técnica: no
    /// PostgreSQL é a coluna de sistema xmin, que muda em cada gravação (não
    /// cria coluna nenhuma). As fichas enviam-na escondida para quem grava por
    /// cima de outra pessoa ser avisado em vez de apagar o que ela gravou.
    /// </summary>
    public const string RowVersion = "Version";

    public DbSet<User> Users => Set<User>();

    // Admin foundation: Access Templates and the persisted User↔Template
    // relationship (TEMPLATES.md §"Access model"). The relationship is the
    // single access authority for effective-access resolution; the
    // Template's stable key (never its mutable label) is the anchor.
    public DbSet<Template> Templates => Set<Template>();

    public DbSet<TemplateAssignment> TemplateAssignments => Set<TemplateAssignment>();

    public DbSet<Machine> Machines => Set<Machine>();

    public DbSet<JobOn> JobOns => Set<JobOn>();

    /// <summary>As mudanças de lote registadas durante as produções (notas do «Duplicar → Lote»).</summary>
    public DbSet<JobOnLotNote> JobOnLotNotes => Set<JobOnLotNote>();

    public DbSet<JobOnChange> JobOnChanges => Set<JobOnChange>();

    public DbSet<CmContext> CmContexts => Set<CmContext>();

    public DbSet<MfContext> MfContexts => Set<MfContext>();

    public DbSet<BqContext> BqContexts => Set<BqContext>();

    public DbSet<Tool> Tools => Set<Tool>();

    public DbSet<ToolTechnicalContext> ToolTechnicalContexts => Set<ToolTechnicalContext>();

    public DbSet<ToolMachineLink> ToolMachineLinks => Set<ToolMachineLink>();

    public DbSet<BqRepairTrace> BqRepairTraces => Set<BqRepairTrace>();

    public DbSet<BqRepairer> BqRepairers => Set<BqRepairer>();

    // O Armazém do full (peça E do plano): as posições e os reparadores de CM e MF.
    public DbSet<ArmazemPosicao> ArmazemPosicoes => Set<ArmazemPosicao>();

    public DbSet<Reparador> Reparadores => Set<Reparador>();

    // Os movimentos do Armazém, as ocupações das posições, onde está cada
    // ferramenta e os estados registados.
    public DbSet<ArmazemMovimento> ArmazemMovimentos => Set<ArmazemMovimento>();

    public DbSet<ArmazemOcupacao> ArmazemOcupacoes => Set<ArmazemOcupacao>();

    public DbSet<FerramentaLocalizacao> FerramentaLocalizacoes => Set<FerramentaLocalizacao>();

    public DbSet<FerramentaEstado> FerramentaEstados => Set<FerramentaEstado>();

    // A utilização das ferramentas (as leituras, só de acrescentar) e o
    // lembrete aberto pela Entrada vinda da Produção.
    public DbSet<FerramentaUtilizacao> FerramentaUtilizacoes => Set<FerramentaUtilizacao>();

    public DbSet<FerramentaLembreteUtilizacao> FerramentaLembretesUtilizacao => Set<FerramentaLembreteUtilizacao>();

    public DbSet<BqMovement> BqMovements => Set<BqMovement>();

    public DbSet<BqMovementDeletion> BqMovementDeletions => Set<BqMovementDeletion>();

    public DbSet<Peso> Pesos => Set<Peso>();

    public DbSet<PesoMeasurement> PesoMeasurements => Set<PesoMeasurement>();

    public DbSet<PesoComparacao> PesoComparacoes => Set<PesoComparacao>();

    public DbSet<PesoComparacaoMeasurement> PesoComparacaoMeasurements => Set<PesoComparacaoMeasurement>();

    /// <summary>«Manter» ou «Colocar de parte» cada CM de uma comparação, e a aprovação (D10, 19/10/2026).</summary>
    public DbSet<PesoComparacaoCmDecisao> PesoComparacaoCmDecisoes => Set<PesoComparacaoCmDecisao>();

    public DbSet<GlassDensity> GlassDensities => Set<GlassDensity>();

    public DbSet<DMO.Alpha.Core.Controlo.Controlo> Controlos =>
        Set<DMO.Alpha.Core.Controlo.Controlo>();

    /// <summary>As decisões por parte da Revisão (Peso, Pegamentos, Ferramentas; 19/10/2026).</summary>
    public DbSet<DMO.Alpha.Core.Controlo.ControloDecisao> ControloDecisoes =>
        Set<DMO.Alpha.Core.Controlo.ControloDecisao>();

    public DbSet<Pegamentos> Pegamentos => Set<Pegamentos>();

    public DbSet<PegamentosMeasurement> PegamentosMeasurements => Set<PegamentosMeasurement>();

    // O rasto das leituras corrigidas ou apagadas (4.6): só se acrescenta.
    public DbSet<PegamentosMeasurementHistorico> PegamentosMeasurementHistorico => Set<PegamentosMeasurementHistorico>();

    public DbSet<PegamentosTolerance> PegamentosTolerances => Set<PegamentosTolerance>();

    public DbSet<ModuleSetting> ModuleSettings => Set<ModuleSetting>();

    public DbSet<ControloViewing> ControloViewings => Set<ControloViewing>();

    public DbSet<ControloAreaState> ControloAreaStates => Set<ControloAreaState>();

    // As gravações do OK/NOK de cada área (D13): só se acrescentam.
    public DbSet<ControloAreaStateHistorico> ControloAreaStateHistorico => Set<ControloAreaStateHistorico>();

    // O registo das punções NNPB de uma produção e as punções registadas.
    public DbSet<Puncoes> Puncoes => Set<Puncoes>();

    public DbSet<PuncaoInspecao> PuncaoInspecoes => Set<PuncaoInspecao>();

    // O envio do PDF do Peso por email (D9): as listas, os modelos e o
    // registo de cada envio (o PDF nunca fica no servidor).
    public DbSet<EmailLista> EmailListas => Set<EmailLista>();

    public DbSet<EmailModelo> EmailModelos => Set<EmailModelo>();

    public DbSet<EmailEnvio> EmailEnvios => Set<EmailEnvio>();

    // Keyring do ASP.NET Core DataProtection (antiforgery e cookie DmoAuth)
    // guardado na própria base PostgreSQL: sobrevive a redeploys do contentor.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // O envio único guardado na base (E4 do DMO-FULL): um envio, uma linha.
    public DbSet<OperacaoRegistada> OperacoesRegistadas => Set<OperacaoRegistada>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.OperatorId).IsUnique();
            entity.Property(u => u.OperatorId).HasMaxLength(4).IsRequired();
            entity.Property(u => u.Name).IsRequired();
            entity.Property(u => u.ProviderUserId).IsRequired();
            entity.Property(u => u.TemplateName).HasMaxLength(100);
            // O EMAIL VISÍVEL (o mockup 13_ADMIN: a coluna, a procura «Nome
            // ou email», o campo no editar — o Owner 2026-10-13): rótulo de
            // identificação, NUNCA credencial (a senha vive no provedor).
            entity.Property(u => u.Email).HasMaxLength(254);
        });

        // Admin foundation (TEMPLATES.md): Access Templates — identidade
        // estável (TemplateKey, única e nunca reescrita; o Name é rótulo
        // mutável) e as capabilities como strings módulo-owned persistidas
        // na forma canónica de conjunto (TemplateCapabilityTokens).
        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.TemplateKey).IsUnique();
            entity.Property(t => t.TemplateKey).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Capabilities)
                .HasConversion(
                    c => TemplateCapabilityTokens.ToStorage(c),
                    s => TemplateCapabilityTokens.FromStorage(s))
                .IsRequired();
        });

        // As operações registadas (E4 do DMO-FULL): a chave primária é o
        // operacao_id que a página gerou, por isso um envio repetido colide
        // nela (23505 em PK_OperacoesRegistadas). Sem chave estrangeira: é um
        // registo técnico, não um facto, e não impede eliminar um utilizador.
        modelBuilder.Entity<OperacaoRegistada>(entity =>
        {
            entity.ToTable("OperacoesRegistadas");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedNever();
            entity.Property(o => o.Tipo).HasMaxLength(64).IsRequired();
            entity.Property(o => o.ResultadoTipo).HasMaxLength(64).IsRequired();
        });

        // Admin foundation (TEMPLATES.md §"Access model"): a relação
        // persistida User↔Template — uma linha É a atribuição corrente do
        // User (1 User : 0..1 Template actual, imposta pelo índice ÚNICO
        // sobre UserId; um Template pode estar atribuído a muitos Users).
        // Users e Templates são duas vistas administrativas sobre esta
        // MESMA relação — não existem atribuições concorrentes. FK → Users
        // Cascade (eliminar o User arrasta a sua atribuição); FK → Templates
        // Restrict (um Template atribuído não é eliminável em silêncio —
        // o acesso efectivo corrente nunca muda por eliminação implícita).
        modelBuilder.Entity<TemplateAssignment>(entity =>
        {
            entity.HasKey(a => a.Id);

            // 1 User : 0..1 Template actual: no máximo UMA linha de
            // atribuição corrente por User.
            entity.HasIndex(a => a.UserId).IsUnique();

            entity.HasOne(a => a.User)
                .WithOne(u => u.CurrentTemplateAssignment)
                .HasForeignKey<TemplateAssignment>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Template)
                .WithMany()
                .HasForeignKey(a => a.TemplateId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        // O ADMIN é o email guardado nas Definições (chave Admin:Email de
        // ModuleSettings); não tem tabela própria.

        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Code).HasMaxLength(50).IsRequired();
            // A app escolhe a máquina pelo código (Duplicar/Editar, o
            // reparador das Boquilhas, o histórico): duas «B1» não podem
            // existir.
            entity.HasIndex(m => m.Code).IsUnique();
        });

        modelBuilder.Entity<JobOn>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property<uint>(RowVersion).IsRowVersion();
            entity.Property(j => j.Reference).HasMaxLength(100).IsRequired();
            entity.Property(j => j.ProductionNumber).HasMaxLength(50).IsRequired();
            // Uma produção é a referência + o nº: o mesmo nº repete-se
            // noutras referências.
            entity.HasIndex(j => new { j.Reference, j.ProductionNumber }).IsUnique();
            entity.HasIndex(j => j.ProductionNumber);

            // OS ÍNDICES COMPOSTOS DAS JANELAS (o Owner 2026-10-12: «se os
            // indices compostos vao ajudar, deveremos os implementar»): a
            // agenda por máquina-e-datas — as perguntas que TODAS as páginas
            // das janelas fazem (a produção corrente das Boquilhas, o
            // calendário, o rail do Planeamento, o overlap). A referência
            // para a procura do Resumo.
            entity.HasIndex(j => new { j.MachineId, j.InicioEm, j.FimEm });
            entity.HasIndex(j => j.Reference);

            // Apagar uma máquina nunca apaga as produções dela (e, em cascata,
            // os controlos, pesos e pegamentos): a base recusa (varredura F8).
            entity.HasOne(j => j.Machine)
                .WithMany()
                .HasForeignKey(j => j.MachineId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // O cm_id em uso da produção; os de CMs trocadas ficam em
            // CmContext.ReplacedJobOnId (JobOnId null).
            entity.HasOne(j => j.CmContext)
                .WithOne(c => c.JobOn)
                .HasForeignKey<CmContext>(c => c.JobOnId)
                .OnDelete(DeleteBehavior.Cascade);

            // O mf_id em uso da produção; os de MFs trocados ficam em
            // MfContext.ReplacedJobOnId (JobOnId null).
            entity.HasOne(j => j.MfContext)
                .WithOne(c => c.JobOn)
                .HasForeignKey<MfContext>(c => c.JobOnId)
                .OnDelete(DeleteBehavior.Cascade);

            // O bq_id em uso da produção; os de BQs trocadas ficam em
            // BqContext.ReplacedJobOnId (JobOnId null).
            entity.HasOne(j => j.BqContext)
                .WithOne(c => c.JobOn)
                .HasForeignKey<BqContext>(c => c.JobOnId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CmContext>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ToolId).HasMaxLength(100).IsRequired();
            // A vida da ferramenta (os usos da tool — a deteção dos
            // registos avulsos). O JobOnId já é indexado pela própria
            // relação 1:1 (a convenção do EF) — não se declara a mais.
            entity.HasIndex(c => c.ToolId);
            // A ferramenta da produção existe sempre no registo: ao apagar
            // uma ferramenta, o contexto passa para a linha REMOVED (como
            // os Pesos e os Pegamentos), nunca fica a apontar para nada.
            entity.HasOne<Tool>().WithMany().HasForeignKey(c => c.ToolId).OnDelete(DeleteBehavior.Restrict);
            // O cm_id anterior de uma produção cuja CM foi trocada: continua
            // no registo da produção.
            entity.HasOne(c => c.ReplacedJobOn)
                .WithMany()
                .HasForeignKey(c => c.ReplacedJobOnId)
                .OnDelete(DeleteBehavior.Restrict);
            UmaSoProducao(entity, "CmContexts");
        });

        modelBuilder.Entity<MfContext>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ToolId).HasMaxLength(100).IsRequired();
            entity.HasIndex(c => c.ToolId);
            entity.HasOne<Tool>().WithMany().HasForeignKey(c => c.ToolId).OnDelete(DeleteBehavior.Restrict);
            // O mf_id anterior de uma produção cujo MF foi trocado: continua
            // no registo da produção.
            entity.HasOne(c => c.ReplacedJobOn)
                .WithMany()
                .HasForeignKey(c => c.ReplacedJobOnId)
                .OnDelete(DeleteBehavior.Restrict);
            UmaSoProducao(entity, "MfContexts");
        });

        modelBuilder.Entity<BqContext>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ToolId).HasMaxLength(100).IsRequired();
            entity.HasIndex(c => c.ToolId);
            entity.HasOne<Tool>().WithMany().HasForeignKey(c => c.ToolId).OnDelete(DeleteBehavior.Restrict);
            // O bq_id anterior de uma produção cuja BQ foi trocada: continua
            // ligado à produção para o histórico da BQ antiga.
            entity.HasOne(c => c.ReplacedJobOn)
                .WithMany()
                .HasForeignKey(c => c.ReplacedJobOnId)
                .OnDelete(DeleteBehavior.Restrict);
            UmaSoProducao(entity, "BqContexts");
        });

        // Registo canónico de Tools (Ferramentas). O tool_id é a identidade
        // emitida pelo backend. Não existe nenhuma regra de unicidade sobre
        // factos da Tool (referência, lote, máquina): são apenas atributos de
        // descoberta de candidatos e não formam uma chave de identidade
        // derivada. Um lote diferente persiste como uma Tool distinta com o
        // seu próprio tool_id. O corte A acrescenta os factos de master data
        // (referência MF associada — descoberta; quantidade contabilística do
        // lote BQ) como colunas ADITIVAS nullable: a ausência é estado
        // válido, a forma existente fica inalterada e a linha DUMMY canónica
        // (ToolId = "REMOVED", semeada pela migração Baseline) é uma Tool como
        // qualquer outra na persistência — excluída por CONSTANTE nas
        // queries, nunca por constraint.
        modelBuilder.Entity<Tool>(entity =>
        {
            entity.HasKey(t => t.ToolId);
            entity.Property<uint>(RowVersion).IsRowVersion();
            entity.Property(t => t.ToolId).IsRequired();
            // A CHAVE DE DESCOBERTA DO GET-OR-CREATE (a decisão do Owner: a
            // unicidade de facto (tipo, referência, lote) — índice de
            // procura, NÃO unique: as duplicadas históricas pré-decisão
            // continuam na persistência como verdade do registo).
            entity.HasIndex(t => new { t.Type, t.Reference, t.Lot });

            // O registo cobre exatamente os tipos actualmente em scope.
            entity.Property(t => t.Type)
                .HasConversion(
                    t => ToolTypeTokens.ToStorage(t),
                    s => ToolTypeTokens.FromStorage(s))
                .IsRequired();

            entity.Property(t => t.Reference).IsRequired();
            entity.Property(t => t.Lot).IsRequired();
            entity.Property(t => t.DuplicateWarningByNameFrozen).HasMaxLength(254);
        });

        // Contexto técnico estável das Tools (regra Ferramentas «Tool-owned
        // technical values»): uma linha 1:1 por tool_id, dona do próprio
        // tool_id — os valores são factos da Tool, nunca uma cópia privada
        // de uma função consumidora. Persistem-se os valores consumidos pelo
        // Peso (CM: processo, peso nominal, volume_puncao; BQ:
        // volume_marisa), o diametro_gargalo consumido pelos Pegamentos e a
        // geometria TP de referência do corte A (D, R_draw, sagitta/h_ref,
        // R base, R esfera, volume — valores registados tal como vêm da
        // folha: a app NUNCA recalcula derivados); todos nullable — um valor
        // técnico não tem de existir quando o tool_id é criado e é
        // completado depois, gravando sempre no tool_id owner. O processo
        // persiste por token canónico (NNPB/PS). Nenhuma unicidade além da
        // própria chave 1:1.
        modelBuilder.Entity<ToolTechnicalContext>(entity =>
        {
            entity.HasKey(t => t.ToolId);
            entity.Property<uint>(RowVersion).IsRowVersion();

            // FK 1:1 opcional-do-lado-técnico → Tools. DeleteBehavior
            // Restrict (DB: ON DELETE RESTRICT): a transação canónica de
            // eliminação da Tool apaga a linha técnica DENTRO da mesma
            // transação (a ficha técnica é o registo da tool; os Pesos já
            // congelaram o que consumiram).
            entity.HasOne(t => t.Tool)
                .WithMany()
                .HasForeignKey(t => t.ToolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(t => t.Process)
                .HasConversion(
                    p => p.HasValue ? CmProcessTokens.ToStorage(p.Value) : null,
                    s => s == null ? null : CmProcessTokens.FromStorage(s));

            entity.Property(t => t.McaliperUrl)
                .HasMaxLength(ToolTechnicalContext.McaliperUrlMaxLength);
        });

        // Ligações Tool–máquina (master data Ferramentas): a compatibilidade
        // de máquinas/linhas por Tool — uma linha por par (PK composta
        // ToolId+MachineId), sem navegação de coleção na Tool. A forma é a
        // mesma para CM/MF/BQ: conjunto de compatibilidades, sem limite de uma máquina.
        // FK → Tools Restrict (a Tool referenciada não desaparece
        // silenciosamente; a transação canónica de delete apaga as ligações
        // da tool) e FK → Machines CASCADE pelo precedente JobOn → Machines.
        // Índice em MachineId: a procura por máquina (filtro de candidatos)
        // não faz scan da PK composta (cuja coluna leading é ToolId).
        modelBuilder.Entity<ToolMachineLink>(entity =>
        {
            entity.HasKey(link => new { link.ToolId, link.MachineId });

            entity.HasOne(link => link.Tool)
                .WithMany()
                .HasForeignKey(link => link.ToolId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(link => link.Machine)
                .WithMany()
                .HasForeignKey(link => link.MachineId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(link => link.MachineId);
        });

        // Registo canónico de reparação de Boquilhas. O tool_id é uma ÂNCORA
        // DIRETA TEMPORÁRIA usada apenas na fase pré-produção (bq_id ainda
        // não resolvido); na associação a bq_id a âncora é LIMPA (ToolId =
        // null) e a Tool passa a alcançar-se via bq_id → BqContext.ToolId.
        // Não existe estado open/closed nem lifecycle. Regra FECHADA: existe
        // no máximo UM trace pré-produção não resolvido por tool_id, imposta
        // por um índice único parcial sobre ToolId restrito às linhas não
        // resolvidas ("BqContextId" IS NULL) e reforçada no handler de
        // criação (reutilização). Traces históricos já associados a bq_id
        // mais antigos têm a âncora limpa e, por isso, não colidem com o
        // índice parcial — a mesma Tool canónica pode ter vários traces de
        // produção ao longo do tempo (um por bq_id). A outra unicidade é a
        // relação canónica decidida: um bq_id tem um único trace ("one bq_id
        // has one bq_repair_trace_id"). A associação nunca muta o contexto
        // de produção existente. O corte A acrescenta os factos humanos
        // congelados (referência/lote da tool à criação do trace) como
        // colunas ADITIVAS nullable — a evidência histórica sobrevive à
        // edição/eliminação da Tool.
        modelBuilder.Entity<BqRepairTrace>(entity =>
        {
            entity.HasKey(t => t.Id);

            // FK opcional: a âncora direta tool_id é limpa na associação,
            // pelo que ToolId é nullable e a Tool deixa de ser obrigatória.
            // DeleteBehavior.Restrict (DB: ON DELETE RESTRICT) impede um trace
            // órfão silenciosamente inalcançável; a transação canónica de
            // eliminação da Tool limpa a âncora DENTRO da mesma transação
            // (traces não resolvidos → ToolId NULL).
            entity.HasOne(t => t.Tool)
                .WithMany()
                .HasForeignKey(t => t.ToolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.BqContext)
                .WithMany()
                .HasForeignKey(t => t.BqContextId);

            entity.HasIndex(t => t.BqContextId).IsUnique();

            // No máximo um trace pré-produção não resolvido por tool_id: o
            // índice único é filtrado às linhas ainda não associadas, pelo
            // que não impede os vários traces históricos (âncora limpa) da
            // mesma Tool canónica ao longo do tempo.
            entity.HasIndex(t => t.ToolId)
                .IsUnique()
                .HasFilter("\"BqContextId\" IS NULL");

            // Quem pôs a discrepância a zero (Diogo, 17/10/2026), como nas correções.
            entity.Property(t => t.DiscrepancyResetByNameFrozen).HasMaxLength(254);
        });

        // Movimentos canónicos de Boquilhas. Cada movimento pertence
        // obrigatoriamente a um bq_repair_trace_id; o tipo é exatamente um
        // de saida, entrada, entrada_sem_reparacao; a quantidade observada
        // é registada na totalidade; a discrepância produzida pelo
        // movimento é um facto histórico (null = sem discrepância).
        // Nenhuma regra aritmética é imposta à persistência: sem
        // constraints de sinal, de balanço ou de quantidade — o movimento
        // preserva o que foi fisicamente observado.
        // O REPARADOR DA SAÍDA: a FK opcional à
        // entidade BqRepairer (a rota por máquina — nunca um input do
        // form) e o NOME CONGELADO como evidência histórica (a verdade do
        // movimento sobrevive à edição/eliminação da entidade; a FK
        // opcional significa «sem reparador» — estado truthful, nunca
        // bloqueio).
        modelBuilder.Entity<BqMovement>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.HasOne(m => m.Trace)
                .WithMany()
                .HasForeignKey(m => m.BqRepairTraceId)
                .IsRequired();

            // A FK opcional ao reparador da saída: Restrict (DB: ON DELETE
            // RESTRICT) — a entidade referenciada não desaparece em
            // silêncio; a eliminação canónica (corte futuro) congela o
            // nome nos movimentos ANTES de limpar a FK, dentro da mesma
            // transação. Sem navegação na entidade: o fato humano do
            // movimento é o NOME CONGELADO, nunca a entidade viva.
            entity.HasOne<BqRepairer>()
                .WithMany()
                .HasForeignKey(m => m.BqRepairerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => m.BqRepairerId);

            // O calendário e os períodos do Histórico filtram pela data real
            // (teste de carga de 17/10/2026: sem índice, cada abertura lia a
            // tabela inteira).
            entity.HasIndex(m => m.OccurredAtUtc);

            entity.Property(m => m.Type)
                .HasConversion(
                    t => BqMovementTypeTokens.ToStorage(t),
                    s => BqMovementTypeTokens.FromStorage(s))
                .IsRequired();

            entity.Property(m => m.Quantity).IsRequired();

            entity.Property(m => m.BqRepairerNameFrozen).HasMaxLength(100);
            entity.Property(m => m.MachineCodeFrozen).HasMaxLength(32);
            // A última correção (o Owner 2026-10-16): quem e quando, nullable.
            entity.Property(m => m.CorrectedByNameFrozen).HasMaxLength(254);
        });

        // O rasto dos movimentos apagados (o Owner 2026-10-16): cópia dos
        // factos do movimento com quem apagou e quando. Sem FKs — o movimento
        // já não existe e o rasto não deve impedir nada.
        modelBuilder.Entity<BqMovementDeletion>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.Type)
                .HasConversion(
                    t => BqMovementTypeTokens.ToStorage(t),
                    s => BqMovementTypeTokens.FromStorage(s))
                .IsRequired();

            entity.Property(d => d.MachineCodeFrozen).HasMaxLength(32);
            entity.Property(d => d.DeletedByNameFrozen).HasMaxLength(254);
        });

        // ENTIDADES REPARADORAS das Boquilhas (o Owner 2026-10-12:
        // «reparadores sao as entidades para onde enviamos as boquilhas»)
        // — o vocabulário administrado do encaminhamento por máquina
        // (Admin → App Definições → Boquilhas). A identidade é o Id
        // emitido pelo backend; o NOME é o fato humano (único — dois
        // reparadores com o mesmo nome seriam o mesmo fato para a rota;
        // a unicidade é recusa TIPADA no handler com o índice único como
        // última linha da persistência).
        modelBuilder.Entity<BqRepairer>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Name).HasMaxLength(100).IsRequired();

            entity.HasIndex(r => r.Name).IsUnique();
        });

        // O Armazém do full: posições com o código em maiúsculas e único (não
        // se apagam; arquivam-se) e os reparadores de CM e MF, uma lista à
        // parte dos reparadores das Boquilhas, com o nome único e pelo menos
        // uma das famílias.
        modelBuilder.Entity<ArmazemPosicao>(entity =>
        {
            entity.ToTable("ArmazemPosicoes", t =>
                t.HasCheckConstraint("CK_ArmazemPosicoes_Codigo", "\"Codigo\" ~ '^[0-9A-Z]{1,16}$'"));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Codigo).HasMaxLength(ArmazemPosicao.CodigoMaxLength).IsRequired();
            entity.HasIndex(p => p.Codigo).IsUnique();
            entity.Property(p => p.CriadaPor).HasMaxLength(254);
        });

        modelBuilder.Entity<Reparador>(entity =>
        {
            entity.ToTable("Reparadores", t =>
                t.HasCheckConstraint("CK_Reparadores_Familia", "\"AplicaCm\" OR \"AplicaMf\""));
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Nome).HasMaxLength(Reparador.NomeMaxLength).IsRequired();
            entity.HasIndex(r => r.Nome).IsUnique();
        });

        // Os movimentos do Armazém só se acrescentam. O reparador é de CM/MF
        // ou de BQ, nunca os dois; a data real e o momento do registo ficam
        // os dois. A ocupação ativa é única por ferramenta (o índice parcial)
        // e tem versão, como a localização: dois movimentos da mesma
        // ferramenta ao mesmo tempo não passam os dois.
        modelBuilder.Entity<ArmazemMovimento>(entity =>
        {
            entity.ToTable("ArmazemMovimentos", t =>
                t.HasCheckConstraint("CK_ArmazemMovimentos_UmReparador", "\"ReparadorId\" IS NULL OR \"BqReparadorId\" IS NULL"));
            entity.HasKey(m => m.Id);
            entity.HasOne<Tool>().WithMany().HasForeignKey(m => m.ToolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemPosicao>().WithMany().HasForeignKey(m => m.PosicaoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemPosicao>().WithMany().HasForeignKey(m => m.PosicaoAnteriorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Reparador>().WithMany().HasForeignKey(m => m.ReparadorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BqRepairer>().WithMany().HasForeignKey(m => m.BqReparadorId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(m => m.ToolId).HasMaxLength(100).IsRequired();
            entity.Property(m => m.ReparadorNome).HasMaxLength(Reparador.NomeMaxLength);
            entity.Property(m => m.FabricaDestino).HasMaxLength(ArmazemMovimento.FabricaMaxLength);
            entity.Property(m => m.Observacoes).HasMaxLength(ArmazemMovimento.ObservacoesMaxLength);
            entity.Property(m => m.RegistadoPor).HasMaxLength(254);
            entity.Property(m => m.VersaoApp).HasMaxLength(64);
            entity.HasIndex(m => m.OperacaoId).IsUnique();
            entity.HasIndex(m => m.OcorridoEmUtc);
        });

        modelBuilder.Entity<ArmazemOcupacao>(entity =>
        {
            entity.ToTable("ArmazemOcupacoes");
            entity.HasKey(o => o.Id);
            entity.HasOne<Tool>().WithMany().HasForeignKey(o => o.ToolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemPosicao>().WithMany().HasForeignKey(o => o.PosicaoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemMovimento>().WithMany().HasForeignKey(o => o.MovimentoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemMovimento>().WithMany().HasForeignKey(o => o.LibertadaPorMovimentoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(o => o.ToolId).HasMaxLength(100).IsRequired();
            entity.HasIndex(o => o.ToolId).IsUnique().HasFilter("\"LibertadaEmUtc\" IS NULL").HasDatabaseName("IX_ArmazemOcupacoes_ToolId_Ativa");
            entity.HasIndex(o => o.PosicaoId).HasFilter("\"LibertadaEmUtc\" IS NULL");
            entity.Property<uint>(RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<FerramentaLocalizacao>(entity =>
        {
            entity.ToTable("FerramentaLocalizacoes");
            entity.HasKey(l => l.ToolId);
            entity.HasOne<Tool>().WithOne().HasForeignKey<FerramentaLocalizacao>(l => l.ToolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemMovimento>().WithMany().HasForeignKey(l => l.MovimentoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(l => l.ToolId).HasMaxLength(100);
            entity.Property(l => l.ReparadorNome).HasMaxLength(Reparador.NomeMaxLength);
            entity.Property<uint>(RowVersion).IsRowVersion();
        });

        // A utilização e o lembrete são da ferramenta e vão com ela. Um só
        // lembrete aberto por ferramenta (o índice parcial).
        modelBuilder.Entity<FerramentaUtilizacao>(entity =>
        {
            entity.ToTable("FerramentaUtilizacoes");
            entity.HasKey(u => u.Id);
            entity.HasOne<Tool>().WithMany().HasForeignKey(u => u.ToolId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(u => u.ToolId).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Percentagem).HasPrecision(9, 2);
            entity.Property(u => u.Origem).HasMaxLength(32).IsRequired();
            entity.Property(u => u.RegistadoPor).HasMaxLength(254);
            entity.Property(u => u.CodigoEquipamento).HasMaxLength(FerramentaUtilizacao.CodigoMaxLength);
            entity.Property(u => u.Ficheiro).HasMaxLength(FerramentaUtilizacao.FicheiroMaxLength);
            entity.HasIndex(u => new { u.ToolId, u.Id });
        });

        modelBuilder.Entity<FerramentaLembreteUtilizacao>(entity =>
        {
            entity.ToTable("FerramentaLembretesUtilizacao");
            entity.HasKey(l => l.Id);
            entity.HasOne<Tool>().WithMany().HasForeignKey(l => l.ToolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemMovimento>().WithMany().HasForeignKey(l => l.MovimentoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FerramentaUtilizacao>().WithMany().HasForeignKey(l => l.UtilizacaoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(l => l.ToolId).HasMaxLength(100).IsRequired();
            entity.Property(l => l.CriadoPor).HasMaxLength(254);
            entity.HasIndex(l => l.ToolId).IsUnique().HasFilter("\"FechadoEmUtc\" IS NULL").HasDatabaseName("IX_FerramentaLembretesUtilizacao_ToolId_Aberto");
        });

        modelBuilder.Entity<FerramentaEstado>(entity =>
        {
            entity.ToTable("FerramentaEstados");
            entity.HasKey(e => e.Id);
            entity.HasOne<Tool>().WithMany().HasForeignKey(e => e.ToolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ArmazemMovimento>().WithMany().HasForeignKey(e => e.MovimentoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.ToolId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Origem).HasMaxLength(32).IsRequired();
            entity.Property(e => e.RegistadoPor).HasMaxLength(254);
            entity.HasIndex(e => new { e.ToolId, e.Id });
        });

        // Controlo de Peso tool-scoped. O peso_id é a identidade durável
        // emitida pelo backend. O Peso fica ancorado às Tools reais usadas
        // na pesagem — a CM pesada (dona da consulta histórica por Tool) e a
        // BQ cujo volume_marisa o cálculo consumiu — e NÃO transporta nem
        // inventa jobon_id, controlo_id ou cm_id: a associação posterior a
        // uma produção é decisão humana explícita de um slice futuro (um
        // peso_id associa-se a no máximo uma produção — regra funcional
        // aplicada quando essa associação existir; nenhuma coluna de
        // produção é criada agora). Os valores consumidos (processo, grau
        // inteiro da temperatura da água, volumes, nominal opcional e
        // densidade do vidro) são evidência histórica congelada na criação:
        // uma edição posterior da Tool/configuração não reescreve o
        // resultado. O processo persiste por token canónico (NNPB/PS).
        // Nenhuma unicidade sobre (CmToolId, BqToolId) sozinhos: a mesma
        // Tool pode ter vários Pesos ao longo do tempo (cada pesagem real é
        // um peso_id novo); a única regra de cardinalidade é a da produção —
        // um Peso por controlo para cada CM+BQ (abaixo). O corte A acrescenta os FATOS
        // HUMANOS congelados (referência/lote das tools à criação) como
        // colunas ADITIVAS nullable — o histórico sobrevive à
        // edição/eliminação da Tool (reatribuição à DUMMY preserva-os).
        modelBuilder.Entity<Peso>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.HasOne(p => p.CmTool)
                .WithMany()
                .HasForeignKey(p => p.CmToolId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.BqTool)
                .WithMany()
                .HasForeignKey(p => p.BqToolId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // Corte C — o Peso production-scoped (spec PESO.md «Production
            // association cardinality»): a associação à produção é OPCIONAL
            // (null nos Pesos tool-scoped pré-produção) e explícita; a
            // associação consome o Peso para essa produção. Um Peso por
            // controlo para cada CM+BQ (18/10/2026): o Peso congela a
            // CM, o volume punção, o nominal e o volume marisa da BQ, por
            // isso trocar a CM ou a BQ a meio da produção pede um Peso novo
            // (respostas R15 e R16-5 do especialista); trocar só o MF não.
            // O índice do ControloId fica, sem ser único, para a chave
            // estrangeira. O único é filtrado: apagar uma ferramenta passa os
            // Pesos dela para a DUMMY (REMOVED), e dois Pesos da mesma
            // produção podem ficar com a mesma combinação sem colidir.
            entity.HasOne(p => p.Controlo)
                .WithMany()
                .HasForeignKey(p => p.ControloId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(p => p.ControloId);
            entity.HasIndex(p => new { p.ControloId, p.CmToolId, p.BqToolId })
                .IsUnique()
                .HasFilter(
                    $"\"ControloId\" IS NOT NULL AND \"CmToolId\" <> '{ToolRegistryConstants.RemovedToolId}' " +
                    $"AND \"BqToolId\" <> '{ToolRegistryConstants.RemovedToolId}'");

            // A produção anterior de referência (o Owner 2026-10-17): o peso
            // do SAP é escrito à mão; o controlo anterior só dá o contexto.
            entity.HasOne(p => p.ProducaoAnteriorControlo)
                .WithMany()
                .HasForeignKey(p => p.ProducaoAnteriorControloId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(p => p.Process).IsRequired();

            entity.Property(p => p.WaterTemperatureC).IsRequired();
            entity.Property(p => p.VolumeMarisaCm3).IsRequired();
            entity.Property(p => p.VolumePuncaoCm3).IsRequired();
            entity.Property(p => p.GlassDensityGramsPerCm3).IsRequired();
            entity.Property(p => p.RegistadoPor).HasMaxLength(254);
        });

        // Leituras físicas do Peso (measurement rows). Cada leitura pertence
        // obrigatoriamente a um peso_id; a posição física é a ordem de
        // registo; o número/posição CM visível é row-level measurement data
        // (nunca um cm_id nem um tool_id); a massa de água observada é
        // registada na totalidade. Factos históricos: nenhuma regra
        // aritmética é imposta à persistência (sem constraints de sinal) —
        // a leitura preserva o que foi fisicamente observado.
        modelBuilder.Entity<PesoMeasurement>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.HasOne(m => m.Peso)
                .WithMany(p => p.Measurements)
                .HasForeignKey(m => m.PesoId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(m => m.RowPosition).IsRequired();
            entity.Property(m => m.CmSubject).IsRequired();
            entity.Property(m => m.WaterWeightGrams).IsRequired();
            // Uma posição por leitura: duas gravações ao mesmo tempo não
            // ficam com a mesma posição (a segunda volta a correr).
            entity.HasIndex(m => new { m.PesoId, m.RowPosition }).IsUnique();
        });

        // O REGISTO DE COMPARAÇÃO de pesos (o registo do histórico tipo
        // «Comparação» do dmo-html): as novas leituras dos contra-moldes
        // contra o Peso inicial de uma produção, sob o controlo_id da
        // produção. Os inputs consumidos são congelados no registo (o padrão
        // do Peso: o grau da temperatura, as densidades, os volumes e os
        // fatos humanos da CM congelados — o histórico sobrevive à
        // edição/eliminação da Tool); os derivados calculam-se à leitura
        // pela fonte única. FKs Restrict (o controlo, o peso inicial e a
        // Tool referenciados não desaparecem silenciosamente); a nota é
        // opcional; a data de criação (UTC) é a data do histórico. Índice
        // por ControloId: a procura do histórico pela produção não faz scan
        // da tabela inteira.
        modelBuilder.Entity<PesoComparacao>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.HasOne(c => c.Controlo)
                .WithMany()
                .HasForeignKey(c => c.ControloId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(c => c.ControloId);

            entity.HasOne(c => c.PesoInicial)
                .WithMany()
                .HasForeignKey(c => c.PesoInicialId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.CmTool)
                .WithMany()
                .HasForeignKey(c => c.CmToolId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(c => c.CmToolId).HasMaxLength(100).IsRequired();
            entity.Property(c => c.WaterTemperatureC).IsRequired();
            entity.Property(c => c.WaterDensityGramsPerCm3).IsRequired();
            entity.Property(c => c.GlassDensityGramsPerCm3).IsRequired();
            entity.Property(c => c.VolumeMarisaCm3).IsRequired();
            entity.Property(c => c.VolumePuncaoCm3).IsRequired();
            entity.Property(c => c.Note).HasMaxLength(500);
            entity.Property(c => c.CreatedAtUtc).IsRequired();
        });

        // As leituras de contra-molde do registo de comparação (measurement
        // rows): pertencem obrigatoriamente a um registo; a posição é a
        // ordem de registo; o número/posição CM visível é row-level
        // measurement data (nunca um tool_id); a massa de água observada é
        // registada na totalidade. Factos históricos append-only (o padrão
        // do Peso — sem constraints de sinal: a leitura preserva o que foi
        // fisicamente observado).
        modelBuilder.Entity<PesoComparacaoMeasurement>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.HasOne(m => m.Comparacao)
                .WithMany(c => c.Measurements)
                .HasForeignKey(m => m.ComparacaoId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(m => m.RowPosition).IsRequired();
            entity.Property(m => m.CmSubject).IsRequired();
            entity.Property(m => m.WaterWeightGrams).IsRequired();
            entity.HasIndex(m => new { m.ComparacaoId, m.RowPosition }).IsUnique();
        });

        // «Manter» ou «Colocar de parte» uma CM da comparação (D10): uma
        // escolha por leitura, protegida também na base (índice único).
        modelBuilder.Entity<PesoComparacaoCmDecisao>(entity =>
        {
            entity.ToTable("PesoComparacaoCmDecisoes");
            entity.HasKey(d => d.Id);
            entity.HasOne(d => d.Measurement)
                .WithMany()
                .HasForeignKey(d => d.MeasurementId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(d => d.MeasurementId).IsUnique();
            entity.Property(d => d.EscolhidoPor).HasMaxLength(254).IsRequired();
            entity.Property(d => d.AprovadoPor).HasMaxLength(254);
        });

        // Configuração do Controlo: densidade do vidro por processo de
        // fabrico. Uma linha por processo (chave = processo, token canónico
        // NNPB/PS). O Peso consome o valor aplicável e preserva-o como
        // evidência histórica no Peso criado; uma alteração posterior da
        // configuração não reescreve Pesos concluídos.
        modelBuilder.Entity<GlassDensity>(entity =>
        {
            entity.HasKey(g => g.Process);

            entity.Property(g => g.Process)
                .HasConversion(
                    p => CmProcessTokens.ToStorage(p),
                    s => CmProcessTokens.FromStorage(s));

            entity.Property(g => g.DensityGramsPerCm3).IsRequired();
        });

        // Registo de Controlo de uma produção (corte ensure + Resumo). O
        // controlo_id é a identidade durável emitida pelo backend,
        // associado a UMA produção pela relação JobOnId com ÍNDICE ÚNICO
        // (idempotência material: get-or-create por JobOnId — abrir duas
        // vezes é o MESMO controlo_id; padrão DuplicateProductionNumber:
        // a verificação prévia do handler devolve o resultado tipado e o
        // índice real é a última linha da verdade persistida). A tabela
        // tem EXACTAMENTE a identidade e a relação: nenhuma coluna de
        // factos CM/BQ/MF/PU/CS (edição é corte futuro), nenhum timestamp,
        // nenhum estado/revisão, nenhuma cópia do contexto de produção —
        // o contexto resolve-se pela relação com a query canónica. O Job
        // On NÃO ganha navegação nem coluna de Controlo (spec Job On L31):
        // a FK vive no lado do Controlo, configurada como Restrict (DB: ON
        // DELETE RESTRICT) — uma produção referenciada por um controlo_id
        // não desaparece silenciosamente.
        modelBuilder.Entity<DMO.Alpha.Core.Controlo.Controlo>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.HasOne(c => c.JobOn)
                .WithMany()
                .HasForeignKey(c => c.JobOnId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => c.JobOnId).IsUnique();
            entity.Property(c => c.NotApprovedByOperatorId).HasMaxLength(254);
            // Aprovado e não aprovado ao mesmo tempo é impossível.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Controlos_ApprovedOrNotApproved",
                "\"ApprovedAtUtc\" IS NULL OR \"NotApprovedAtUtc\" IS NULL"));
            entity.Property(c => c.ReviewNote).HasMaxLength(2000);
        });

        // Uma decisão por parte do controlo (Peso, Pegamentos, Ferramentas;
        // 19/10/2026): a última vence. Apagar um controlo não apaga as
        // decisões em silêncio (Restrict, como o resto do Controlo).
        modelBuilder.Entity<DMO.Alpha.Core.Controlo.ControloDecisao>(entity =>
        {
            entity.ToTable("ControloDecisoes");
            entity.HasKey(d => d.Id);
            entity.HasOne<DMO.Alpha.Core.Controlo.Controlo>()
                .WithMany()
                .HasForeignKey(d => d.ControloId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(d => new { d.ControloId, d.Parte }).IsUnique();
            entity.Property(d => d.OperatorId).HasMaxLength(254).IsRequired();
            entity.Property(d => d.Nota).HasMaxLength(2000);
        });

        // O envio por email do PDF do Peso (Diogo, 19/10/2026, D9). Os nomes
        // das listas e dos modelos são únicos (o operador escolhe-os pelo
        // nome). Apagar uma lista deixa os modelos sem lista proposta. O
        // registo dos envios copia os nomes e não depende de nada: fica
        // mesmo que o modelo, a lista ou o Peso mudem.
        modelBuilder.Entity<EmailLista>(entity =>
        {
            entity.ToTable("EmailListas");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Nome).HasMaxLength(EnvioEmailRegras.NomeMax).IsRequired();
            entity.HasIndex(l => l.Nome).IsUnique();
            entity.Property(l => l.Destinatarios).HasMaxLength(13000).IsRequired();
        });

        modelBuilder.Entity<EmailModelo>(entity =>
        {
            entity.ToTable("EmailModelos");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Nome).HasMaxLength(EnvioEmailRegras.NomeMax).IsRequired();
            entity.HasIndex(m => m.Nome).IsUnique();
            entity.Property(m => m.Remetente).HasMaxLength(EnvioEmailRegras.EmailMax);
            entity.Property(m => m.Assunto).HasMaxLength(EnvioEmailRegras.AssuntoMax).IsRequired();
            entity.Property(m => m.Corpo).HasMaxLength(EnvioEmailRegras.CorpoMax).IsRequired();
            entity.HasOne<EmailLista>()
                .WithMany()
                .HasForeignKey(m => m.ListaId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmailEnvio>(entity =>
        {
            entity.ToTable("EmailEnvios");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ControloId);
            entity.Property(e => e.Ficheiro).HasMaxLength(260).IsRequired();
            entity.Property(e => e.ModeloNome).HasMaxLength(EnvioEmailRegras.NomeMax).IsRequired();
            entity.Property(e => e.ListaNome).HasMaxLength(EnvioEmailRegras.NomeMax).IsRequired();
            entity.Property(e => e.Remetente).HasMaxLength(EnvioEmailRegras.EmailMax);
            entity.Property(e => e.Destinatarios).HasMaxLength(13000).IsRequired();
            entity.Property(e => e.Assunto).HasMaxLength(EnvioEmailRegras.AssuntoMax).IsRequired();
            entity.Property(e => e.EnviadoPor).HasMaxLength(254).IsRequired();
        });

        // Registo de Pegamentos production-scoped (corte folha de
        // Pegamentos). O pegamentos_id é a identidade durável emitida pelo
        // backend, associado a UM controlo pela relação ControloId SEM
        // índice único: a cardinalidade canónica da spec é «normalmente um
        // por produção» e NÃO uma restrição absoluta — um registo
        // adicional é permitido (spec L26-28): depois de trocar a CM, a BQ
        // ou o MF a meio da produção, a primeira leitura começa a folha da
        // combinação nova (18/10/2026), sem mudar o schema. As âncoras
        // de Tool (Cm/Bq/Mf) são herdadas do contexto Job On no momento da
        // criação e congeladas como evidência histórica (nullable — o
        // contexto pode não ter um componente; o MF do J10 real não
        // aplica); a ToleranceMm congela a tolerância efetiva do corredor
        // consumida por todas as linhas do registo (CONFIGURATION.md
        // L47-51: alteração posterior da configuração não reescreve a
        // verdade histórica consumida). O corte A acrescenta os FATOS
        // HUMANOS congelados das tools da combinação (referência/lote por
        // componente — as âncoras vivem no REGISTO, não nas medições) como
        // colunas ADITIVAS nullable.
        modelBuilder.Entity<Pegamentos>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.RegistadoPor).HasMaxLength(254);

            // ControloId opcional: os Pegamentos sem Job On ficam sem
            // controlo até serem associados.
            entity.HasOne(p => p.Controlo)
                .WithMany()
                .HasForeignKey(p => p.ControloId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.CmTool)
                .WithMany()
                .HasForeignKey(p => p.CmToolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.BqTool)
                .WithMany()
                .HasForeignKey(p => p.BqToolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.MfTool)
                .WithMany()
                .HasForeignKey(p => p.MfToolId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(p => p.ToleranceMm).IsRequired();

            // Uma folha por combinação de ferramentas em cada controlo
            // (beta.236; sweep de 19/10/2026, DB-3): o trinco do registo já o
            // garante, e a base passa a recusar a segunda folha mesmo que um
            // caminho futuro se esqueça dele. As folhas sem controlo ficam
            // livres e as de uma ferramenta apagada (REMOVED) não contam, como
            // nos Pesos. Uma folha com um componente em falta também não
            // conta: associar uns Pegamentos sem Job On sem MF a uma produção
            // que já tem uma folha antiga sem MF continua a passar, como até
            // aqui. O índice do ControloId fica para a chave estrangeira: o
            // único é filtrado e não a cobre.
            entity.HasIndex(p => p.ControloId);
            entity.HasIndex(p => new { p.ControloId, p.CmToolId, p.BqToolId, p.MfToolId })
                .IsUnique()
                .HasFilter(
                    $"\"ControloId\" IS NOT NULL AND \"CmToolId\" IS DISTINCT FROM '{ToolRegistryConstants.RemovedToolId}' " +
                    $"AND \"BqToolId\" IS DISTINCT FROM '{ToolRegistryConstants.RemovedToolId}' " +
                    $"AND \"MfToolId\" IS DISTINCT FROM '{ToolRegistryConstants.RemovedToolId}'");
        });

        // Leituras dimensionais da folha de Pegamentos (measurement rows).
        // Cada leitura pertence obrigatoriamente a um pegamentos_id; a
        // posição física é a ordem de registo; o componente persiste por
        // token canónico (CM/BQ/MF); a costura (eixo 0°) é o facto base
        // sempre registado; a contra costura (eixo 90°) é NULL quando a
        // geometria real não a permite — estado válido que NUNCA gera
        // bloqueio na base de dados nem erro cru 23502 (manual §7 L326) — e
        // o nominal gargalo congelado na linha no momento da medição
        // (null = NotEvaluable, nunca um nominal inventado). Factos
        // históricos append-only: nenhuma regra aritmética é imposta à
        // persistência — a leitura preserva o que foi fisicamente
        // observado.
        modelBuilder.Entity<PegamentosMeasurement>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.HasOne(m => m.Pegamentos)
                .WithMany(p => p.Measurements)
                .HasForeignKey(m => m.PegamentosId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(m => m.Component)
                .HasConversion(
                    c => PegamentosComponentTokens.ToStorage(c),
                    s => PegamentosComponentTokens.FromStorage(s))
                .IsRequired();

            entity.Property(m => m.CosturaMm).IsRequired();
            entity.HasIndex(m => new { m.PegamentosId, m.RowPosition }).IsUnique();
        });

        // O rasto de uma leitura corrigida ou apagada (sweep de 19/10/2026,
        // 4.6): a leitura pode já não existir, por isso não tem chave para
        // ela; sai com a folha.
        modelBuilder.Entity<PegamentosMeasurementHistorico>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.HasIndex(h => new { h.PegamentosId, h.EmUtc });
            entity.Property(h => h.Component)
                .HasConversion(
                    c => PegamentosComponentTokens.ToStorage(c),
                    s => PegamentosComponentTokens.FromStorage(s))
                .IsRequired();
            entity.Property(h => h.Por).HasMaxLength(254);

            entity.HasOne<Pegamentos>()
                .WithMany()
                .HasForeignKey(h => h.PegamentosId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuração do Controlo: tolerância do corredor de Pegamentos
        // (definição Controlo Configuration — CONFIGURATION.md L20).
        // Registo singleton: o discriminador constante 1 (private set) com
        // default de base de dados 1 e índice único FILTRADO tornam uma
        // segunda linha impossível. A leitura é o consumo canónico da
        // criação de registos; a edição pertence à superfície de
        // administração (não a este corte).
        modelBuilder.Entity<PegamentosTolerance>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.SingletonDiscriminator).HasDefaultValue(1);
            entity.HasIndex(t => t.SingletonDiscriminator)
                .IsUnique()
                .HasFilter("\"SingletonDiscriminator\" IS NOT NULL");

            entity.Property(t => t.ToleranceMm).IsRequired();
        });

        // As definições funcionais EDITÁVEIS POR MÓDULO (a decisão do Owner
        // 2026-10-11: «no admin vamos ter definições para cada modulo») —
        // chaves namespaced (ex.: Boquilhas:DayChangeHour — a hora de
        // viragem do dia operacional). Chave única; o valor é texto
        // configurado pelo operador; as chaves futuras da spec
        // CONFIGURATION.md entram na mesma tabela.
        modelBuilder.Entity<ModuleSetting>(entity =>
        {
            entity.HasKey(s => s.SettingKey);

            entity.Property(s => s.SettingKey).HasMaxLength(64).IsRequired();
            entity.Property(s => s.SettingValue).IsRequired();
        });

        // O REGISTO DE VISÃO de um controlo (o approve-mínimo, a decisão
        // GATE 4 do Owner: «um registo historico de como existia
        // informação sobre acontecimentos e foi visto pelo chefes» — quem
        // abriu a folha e quando; NUNCA é gate): uma linha por
        // (controlo, operador) — o índice único garante; reabrir a folha
        // atualiza o momento da visita.
        modelBuilder.Entity<ControloViewing>(entity =>
        {
            entity.HasKey(v => v.Id);

            entity.HasIndex(v => new { v.ControloId, v.OperatorId }).IsUnique();

            entity.Property(v => v.OperatorId).HasMaxLength(4).IsRequired();

            entity.HasOne(v => v.Controlo)
                .WithMany()
                .HasForeignKey(v => v.ControloId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Estado do documento: OK/NOK e comentário por área (CM/BQ/MF/PU/CS),
        // uma linha por controlo e área.
        modelBuilder.Entity<ControloAreaState>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.HasIndex(s => new { s.ControloId, s.Area }).IsUnique();

            entity.Property(s => s.Area).HasMaxLength(2).IsRequired();
            entity.Property(s => s.Status).HasMaxLength(3);
            entity.Property(s => s.Comment).HasMaxLength(ControloAreaState.CommentMaxLength);
            entity.Property(s => s.UpdatedBy).HasMaxLength(254);
            entity.Property(s => s.Identificacao).HasMaxLength(ControloAreaState.IdentificacaoMaxLength);

            entity.HasOne(s => s.Controlo)
                .WithMany()
                .HasForeignKey(s => s.ControloId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ControloAreaStateHistorico>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.HasIndex(h => new { h.ControloId, h.Area, h.EmUtc });
            entity.Property(h => h.Area).HasMaxLength(2).IsRequired();
            entity.Property(h => h.Status).HasMaxLength(3);
            entity.Property(h => h.Por).HasMaxLength(254);

            entity.HasOne(h => h.Controlo)
                .WithMany()
                .HasForeignKey(h => h.ControloId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Puncoes>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.ControloId).IsUnique();
            entity.HasIndex(p => p.Reference);
            entity.Property(p => p.Reference).HasMaxLength(DMO.Alpha.Core.Controlo.Puncoes.ReferenceMaxLength);
            entity.Property(p => p.PunchReference).HasMaxLength(DMO.Alpha.Core.Controlo.Puncoes.PunchReferenceMaxLength).IsRequired();
            entity.Property(p => p.Lot).HasMaxLength(DMO.Alpha.Core.Controlo.Puncoes.LotMaxLength).IsRequired();
            entity.Property(p => p.UpdatedBy).HasMaxLength(254);

            entity.HasOne(p => p.Controlo)
                .WithMany()
                .HasForeignKey(p => p.ControloId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PuncaoInspecao>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.PuncoesId, i.Position }).IsUnique();
            entity.Property(i => i.Number).HasMaxLength(PuncaoInspecao.NumberMaxLength).IsRequired();
            entity.Property(i => i.Lot).HasMaxLength(DMO.Alpha.Core.Controlo.Puncoes.LotMaxLength).IsRequired();
            entity.Property(i => i.RegisteredBy).HasMaxLength(254);

            entity.HasOne(i => i.Puncoes)
                .WithMany(p => p.Inspecoes)
                .HasForeignKey(i => i.PuncoesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // As mudanças de lote durante a produção (o «Duplicar → Lote»): só
        // notas, a produção e os contextos não mudam.
        modelBuilder.Entity<JobOnLotNote>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.HasIndex(n => new { n.JobOnId, n.Dia });
            entity.HasOne<Tool>().WithMany().HasForeignKey(n => n.NewToolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(n => n.Dia);

            entity.Property(n => n.Slot).HasMaxLength(2).IsRequired();
            entity.Property(n => n.NewToolId).HasMaxLength(100).IsRequired();
            entity.Property(n => n.ToolReferenceFrozen).IsRequired();
            entity.Property(n => n.NewLotFrozen).IsRequired();
            entity.Property(n => n.Note).HasMaxLength(JobOnLotNote.NoteMaxLength);
            entity.Property(n => n.CreatedBy).HasMaxLength(254);

            entity.HasOne(n => n.JobOn)
                .WithMany()
                .HasForeignKey(n => n.JobOnId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // As alterações de um Job On já gravado (o que mudou, de → para,
        // quem e quando): evidência para «O que mudou» na ficha.
        modelBuilder.Entity<JobOnChange>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.JobOnId, c.ChangedAtUtc });

            entity.Property(c => c.Field).HasMaxLength(16).IsRequired();
            entity.Property(c => c.OldValueFrozen).HasMaxLength(JobOnChange.ValueMaxLength);
            entity.Property(c => c.NewValueFrozen).HasMaxLength(JobOnChange.ValueMaxLength);
            entity.Property(c => c.ChangedByNameFrozen).HasMaxLength(254);

            entity.HasOne(c => c.JobOn)
                .WithMany()
                .HasForeignKey(c => c.JobOnId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // O autor pelo utilizador (E3 do DMO-FULL): cada identificador da
        // AutoriaMap tem o seu índice e a chave estrangeira para Users, com
        // SET NULL para eliminar um utilizador continuar a funcionar (o texto
        // do momento fica no facto).
        foreach (var autoria in AutoriaMap.Autores)
        {
            var entity = modelBuilder.Entity(autoria.Entidade);
            entity.HasIndex(autoria.Identificador);
            entity.HasOne(typeof(User))
                .WithMany()
                .HasForeignKey(autoria.Identificador)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    /// <summary>
    /// Um cm_id, mf_id ou bq_id pertence a uma só produção: a atual
    /// (JobOnId) ou a que o usou antes de trocar a ferramenta
    /// (ReplacedJobOnId, sempre com a data). Sweep de 19/10/2026, DB-1: a
    /// migração ContextToolIdentity junta o que o EF não descreve, o índice
    /// único por produção e ferramenta (voltar a uma ferramenta retoma o
    /// contexto dela) e o gatilho que recusa mudar a ferramenta de um
    /// contexto; só passa para REMOVED, ao apagar a ferramenta. Trocar a
    /// ferramenta cria sempre um contexto novo.
    /// </summary>
    private static void UmaSoProducao<T>(EntityTypeBuilder<T> entity, string table)
        where T : class =>
        entity.ToTable(t =>
        {
            t.HasCheckConstraint(
                $"CK_{table}_CurrentOrReplaced",
                "(\"JobOnId\" IS NULL) <> (\"ReplacedJobOnId\" IS NULL)");
            t.HasCheckConstraint(
                $"CK_{table}_ReplacedWithDate",
                "(\"ReplacedJobOnId\" IS NULL) = (\"ReplacedAtUtc\" IS NULL)");
        });
}

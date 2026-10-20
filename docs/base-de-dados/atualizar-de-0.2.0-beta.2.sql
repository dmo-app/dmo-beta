-- DMO-BETA-0.2: atualizar uma base da 0.2.0-beta.2 (Baseline + AddBqDiscrepancyReset) para a 0.2.0-beta.3.
-- É o mesmo SQL já aplicado no DEV da DMO-BETA-DEV (main 420d5d5, beta.316), pela ordem das migrações 3 a 20.
-- Cada bloco é idempotente: verifica o __EFMigrationsHistory (ou usa IF NOT EXISTS) e pode correr-se mais de uma vez.
-- Os GRANT dão acesso ao utilizador da app do DEV (dmo_beta_dev_runtime) só se esse papel existir.
-- Numa base com outro utilizador da app, repetir esses GRANT para esse utilizador (ver README.md desta pasta).

-- ===== jobon-referencia-numero (docs/dev/jobon-referencia-numero/apply.sql na DMO-BETA-DEV) =====
-- Aplicação da migration 20261017173054_JobOnReferenceProductionNumberUnique no DEV
-- (Supabase SQL Editor — o runtime do Railway não é owner das tabelas).
-- Uma produção é a referência + o nº: o índice único passa do nº sozinho
-- para o par ("Reference", "ProductionNumber"); o nº fica com um índice
-- simples para as procuras pelo nº.
-- Idempotente e sem perda de dados: só troca índices. Como o nº era único,
-- o par também é, e o índice novo cria-se sem conflitos.
-- Aplicar depois de docs/dev/bq-discrepancy-reset/apply.sql (ordem do DEV).

START TRANSACTION;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_JobOns_Reference_ProductionNumber"
    ON "JobOns" ("Reference", "ProductionNumber");

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = current_schema()
          AND tablename = 'JobOns'
          AND indexname = 'IX_JobOns_ProductionNumber'
          AND indexdef LIKE 'CREATE UNIQUE INDEX%') THEN
        DROP INDEX "IX_JobOns_ProductionNumber";
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_JobOns_ProductionNumber" ON "JobOns" ("ProductionNumber");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20261017173054_JobOnReferenceProductionNumberUnique', '10.0.12'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173054_JobOnReferenceProductionNumberUnique');

COMMIT;


-- ===== user-session-version (docs/dev/user-session-version/apply.sql na DMO-BETA-DEV) =====
-- Aplicação da migration 20261018021834_UserSessionVersion no DEV
-- (Supabase SQL Editor — o runtime do Railway não é owner das tabelas).
-- Versão das sessões de cada User: sobe no reset da palavra-passe e quando
-- o User escolhe uma nova; um cookie com uma versão anterior deixa de valer.
-- Idempotente e só acrescenta: uma coluna com valor 0 para todos (as sessões
-- abertas continuam válidas até ao próximo reset ou mudança).
-- Aplicar depois de docs/dev/jobon-referencia-numero/apply.sql (ordem do DEV).

START TRANSACTION;

ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "SessionVersion" integer NOT NULL DEFAULT 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20261018021834_UserSessionVersion', '10.0.12'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018021834_UserSessionVersion');

COMMIT;


-- ===== jobon-machine-restrict (docs/dev/jobon-machine-restrict/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018044547_JobOnMachineRestrict') THEN
    ALTER TABLE "JobOns" DROP CONSTRAINT "FK_JobOns_Machines_MachineId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018044547_JobOnMachineRestrict') THEN
    ALTER TABLE "JobOns" ADD CONSTRAINT "FK_JobOns_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018044547_JobOnMachineRestrict') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018044547_JobOnMachineRestrict', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== row-version (docs/dev/row-version/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018062357_RowVersionXmin') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018062357_RowVersionXmin', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== bq-context-replaced (docs/dev/bq-context-replaced/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018122946_BqContextReplaced') THEN
    ALTER TABLE "BqContexts" ALTER COLUMN "JobOnId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018122946_BqContextReplaced') THEN
    ALTER TABLE "BqContexts" ADD "ReplacedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018122946_BqContextReplaced') THEN
    ALTER TABLE "BqContexts" ADD "ReplacedJobOnId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018122946_BqContextReplaced') THEN
    CREATE INDEX "IX_BqContexts_ReplacedJobOnId" ON "BqContexts" ("ReplacedJobOnId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018122946_BqContextReplaced') THEN
    ALTER TABLE "BqContexts" ADD CONSTRAINT "FK_BqContexts_JobOns_ReplacedJobOnId" FOREIGN KEY ("ReplacedJobOnId") REFERENCES "JobOns" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018122946_BqContextReplaced') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018122946_BqContextReplaced', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== ferramenta-repetida (docs/dev/ferramenta-repetida/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018130550_ToolDuplicateWarning') THEN
    ALTER TABLE "Tools" ADD "DuplicateWarningAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018130550_ToolDuplicateWarning') THEN
    ALTER TABLE "Tools" ADD "DuplicateWarningByNameFrozen" character varying(254);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018130550_ToolDuplicateWarning') THEN
    ALTER TABLE "Tools" ADD "RegisteredAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018130550_ToolDuplicateWarning') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018130550_ToolDuplicateWarning', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== cm-context-replaced (docs/dev/cm-context-replaced/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018132451_CmContextReplaced') THEN
    ALTER TABLE "CmContexts" ALTER COLUMN "JobOnId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018132451_CmContextReplaced') THEN
    ALTER TABLE "CmContexts" ADD "ReplacedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018132451_CmContextReplaced') THEN
    ALTER TABLE "CmContexts" ADD "ReplacedJobOnId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018132451_CmContextReplaced') THEN
    CREATE INDEX "IX_CmContexts_ReplacedJobOnId" ON "CmContexts" ("ReplacedJobOnId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018132451_CmContextReplaced') THEN
    ALTER TABLE "CmContexts" ADD CONSTRAINT "FK_CmContexts_JobOns_ReplacedJobOnId" FOREIGN KEY ("ReplacedJobOnId") REFERENCES "JobOns" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018132451_CmContextReplaced') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018132451_CmContextReplaced', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== peso-per-tool-combination (docs/dev/peso-per-tool-combination/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018155710_PesoPerToolCombination') THEN
    DROP INDEX "IX_Pesos_ControloId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018155710_PesoPerToolCombination') THEN
    CREATE INDEX "IX_Pesos_ControloId" ON "Pesos" ("ControloId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018155710_PesoPerToolCombination') THEN
    CREATE UNIQUE INDEX "IX_Pesos_ControloId_CmToolId_BqToolId" ON "Pesos" ("ControloId", "CmToolId", "BqToolId") WHERE "ControloId" IS NOT NULL AND "CmToolId" <> 'REMOVED' AND "BqToolId" <> 'REMOVED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018155710_PesoPerToolCombination') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018155710_PesoPerToolCombination', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== controlo-decisoes (docs/dev/controlo-decisoes/apply.sql na DMO-BETA-DEV) =====
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019051808_ControloDecisoes') THEN
    CREATE TABLE "ControloDecisoes" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "ControloId" integer NOT NULL,
        "Parte" integer NOT NULL,
        "Aprovado" boolean NOT NULL,
        "OperatorId" character varying(254) NOT NULL,
        "EmUtc" timestamp with time zone NOT NULL,
        "Nota" character varying(2000),
        CONSTRAINT "PK_ControloDecisoes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ControloDecisoes_Controlos_ControloId" FOREIGN KEY ("ControloId") REFERENCES "Controlos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019051808_ControloDecisoes') THEN
    CREATE UNIQUE INDEX "IX_ControloDecisoes_ControloId_Parte" ON "ControloDecisoes" ("ControloId", "Parte");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019051808_ControloDecisoes') THEN
    INSERT INTO "ControloDecisoes" ("ControloId", "Parte", "Aprovado", "OperatorId", "EmUtc", "Nota")
    SELECT c."Id", p.parte, c."ApprovedAtUtc" IS NOT NULL,
           COALESCE(c."ApprovedByOperatorId", c."NotApprovedByOperatorId"),
           COALESCE(c."ApprovedAtUtc", c."NotApprovedAtUtc"),
           c."ReviewNote"
    FROM "Controlos" c
    CROSS JOIN (VALUES (1), (2)) AS p(parte)
    WHERE COALESCE(c."ApprovedByOperatorId", c."NotApprovedByOperatorId") IS NOT NULL
      AND COALESCE(c."ApprovedAtUtc", c."NotApprovedAtUtc") IS NOT NULL
    ON CONFLICT ("ControloId", "Parte") DO NOTHING;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019051808_ControloDecisoes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019051808_ControloDecisoes', '10.0.12');
    END IF;
END $EF$;
DO $access$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dmo_beta_dev_runtime') THEN
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "ControloDecisoes" TO dmo_beta_dev_runtime;
  GRANT USAGE, SELECT ON SEQUENCE "ControloDecisoes_Id_seq" TO dmo_beta_dev_runtime;
 END IF;
END $access$;
COMMIT;


-- ===== mf-context-replaced (docs/dev/mf-context-replaced/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019062754_MfContextReplaced') THEN
    ALTER TABLE "MfContexts" ALTER COLUMN "JobOnId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019062754_MfContextReplaced') THEN
    ALTER TABLE "MfContexts" ADD "ReplacedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019062754_MfContextReplaced') THEN
    ALTER TABLE "MfContexts" ADD "ReplacedJobOnId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019062754_MfContextReplaced') THEN
    CREATE INDEX "IX_MfContexts_ReplacedJobOnId" ON "MfContexts" ("ReplacedJobOnId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019062754_MfContextReplaced') THEN
    ALTER TABLE "MfContexts" ADD CONSTRAINT "FK_MfContexts_JobOns_ReplacedJobOnId" FOREIGN KEY ("ReplacedJobOnId") REFERENCES "JobOns" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019062754_MfContextReplaced') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019062754_MfContextReplaced', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== context-tool-identity (docs/dev/context-tool-identity/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    ALTER TABLE "MfContexts" ADD CONSTRAINT "CK_MfContexts_CurrentOrReplaced" CHECK (("JobOnId" IS NULL) <> ("ReplacedJobOnId" IS NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    ALTER TABLE "MfContexts" ADD CONSTRAINT "CK_MfContexts_ReplacedWithDate" CHECK (("ReplacedJobOnId" IS NULL) = ("ReplacedAtUtc" IS NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    ALTER TABLE "CmContexts" ADD CONSTRAINT "CK_CmContexts_CurrentOrReplaced" CHECK (("JobOnId" IS NULL) <> ("ReplacedJobOnId" IS NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    ALTER TABLE "CmContexts" ADD CONSTRAINT "CK_CmContexts_ReplacedWithDate" CHECK (("ReplacedJobOnId" IS NULL) = ("ReplacedAtUtc" IS NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    ALTER TABLE "BqContexts" ADD CONSTRAINT "CK_BqContexts_CurrentOrReplaced" CHECK (("JobOnId" IS NULL) <> ("ReplacedJobOnId" IS NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    ALTER TABLE "BqContexts" ADD CONSTRAINT "CK_BqContexts_ReplacedWithDate" CHECK (("ReplacedJobOnId" IS NULL) = ("ReplacedAtUtc" IS NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    CREATE UNIQUE INDEX "IX_CmContexts_Production_ToolId" ON "CmContexts"
        ((COALESCE("JobOnId", "ReplacedJobOnId")), "ToolId")
        WHERE "ToolId" <> 'REMOVED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    CREATE UNIQUE INDEX "IX_MfContexts_Production_ToolId" ON "MfContexts"
        ((COALESCE("JobOnId", "ReplacedJobOnId")), "ToolId")
        WHERE "ToolId" <> 'REMOVED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    CREATE UNIQUE INDEX "IX_BqContexts_Production_ToolId" ON "BqContexts"
        ((COALESCE("JobOnId", "ReplacedJobOnId")), "ToolId")
        WHERE "ToolId" <> 'REMOVED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    CREATE FUNCTION "dmo_context_tool_fixed"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
        IF NEW."ToolId" <> 'REMOVED' THEN
            RAISE EXCEPTION 'O contexto % de % não pode passar da ferramenta % para %: trocar a ferramenta cria um contexto novo.',
                OLD."Id", TG_TABLE_NAME, OLD."ToolId", NEW."ToolId"
                USING ERRCODE = 'check_violation';
        END IF;
        RETURN NULL;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    CREATE TRIGGER "TR_CmContexts_ToolFixed" AFTER UPDATE OF "ToolId" ON "CmContexts"
        FOR EACH ROW WHEN (OLD."ToolId" IS DISTINCT FROM NEW."ToolId")
        EXECUTE FUNCTION "dmo_context_tool_fixed"();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    CREATE TRIGGER "TR_MfContexts_ToolFixed" AFTER UPDATE OF "ToolId" ON "MfContexts"
        FOR EACH ROW WHEN (OLD."ToolId" IS DISTINCT FROM NEW."ToolId")
        EXECUTE FUNCTION "dmo_context_tool_fixed"();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    CREATE TRIGGER "TR_BqContexts_ToolFixed" AFTER UPDATE OF "ToolId" ON "BqContexts"
        FOR EACH ROW WHEN (OLD."ToolId" IS DISTINCT FROM NEW."ToolId")
        EXECUTE FUNCTION "dmo_context_tool_fixed"();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092259_ContextToolIdentity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019092259_ContextToolIdentity', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== pegamentos-per-tool-combination (docs/dev/pegamentos-per-tool-combination/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092325_PegamentosPerToolCombination') THEN
    CREATE UNIQUE INDEX "IX_Pegamentos_ControloId_CmToolId_BqToolId_MfToolId" ON "Pegamentos" ("ControloId", "CmToolId", "BqToolId", "MfToolId") WHERE "ControloId" IS NOT NULL AND "CmToolId" IS DISTINCT FROM 'REMOVED' AND "BqToolId" IS DISTINCT FROM 'REMOVED' AND "MfToolId" IS DISTINCT FROM 'REMOVED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019092325_PegamentosPerToolCombination') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019092325_PegamentosPerToolCombination', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== controlo-revisao-depois-da-aprovacao (docs/dev/controlo-revisao-depois-da-aprovacao/apply.sql na DMO-BETA-DEV) =====
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019121642_ControloRevisaoDepoisDaAprovacao') THEN
    ALTER TABLE "PesoMeasurements" ADD "RegistadoEmUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019121642_ControloRevisaoDepoisDaAprovacao') THEN
    ALTER TABLE "PegamentosMeasurements" ADD "RegistadoEmUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019121642_ControloRevisaoDepoisDaAprovacao') THEN
    ALTER TABLE "Pegamentos" ADD "CorrigidoEmUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019121642_ControloRevisaoDepoisDaAprovacao') THEN
    CREATE TABLE "ControloAreaStateHistorico" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "ControloId" integer NOT NULL,
        "Area" character varying(2) NOT NULL,
        "Status" character varying(3),
        "Por" character varying(254),
        "EmUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ControloAreaStateHistorico" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ControloAreaStateHistorico_Controlos_ControloId" FOREIGN KEY ("ControloId") REFERENCES "Controlos" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019121642_ControloRevisaoDepoisDaAprovacao') THEN
    CREATE INDEX "IX_ControloAreaStateHistorico_ControloId_Area_EmUtc" ON "ControloAreaStateHistorico" ("ControloId", "Area", "EmUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019121642_ControloRevisaoDepoisDaAprovacao') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019121642_ControloRevisaoDepoisDaAprovacao', '10.0.12');
    END IF;
END $EF$;
DO $access$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dmo_beta_dev_runtime') THEN
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "ControloAreaStateHistorico" TO dmo_beta_dev_runtime;
  GRANT USAGE, SELECT ON SEQUENCE "ControloAreaStateHistorico_Id_seq" TO dmo_beta_dev_runtime;
 END IF;
END $access$;
COMMIT;



-- ===== envio-email-peso (docs/dev/envio-email-peso/apply.sql na DMO-BETA-DEV) =====
-- Envio do PDF do Peso por email (D9): migration 20261019125706_EnvioEmailPeso, idempotente,
-- mais as permissões do runtime. Aplicar no SQL Editor do Supabase DEV (ver README.md).
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    CREATE TABLE "EmailEnvios" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "ControloId" integer,
        "PesoId" integer NOT NULL,
        "Ficheiro" character varying(260) NOT NULL,
        "ModeloNome" character varying(100) NOT NULL,
        "ListaNome" character varying(100) NOT NULL,
        "Remetente" character varying(254),
        "Destinatarios" character varying(13000) NOT NULL,
        "Assunto" character varying(200) NOT NULL,
        "EnviadoPor" character varying(254) NOT NULL,
        "EnviadoEmUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_EmailEnvios" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    CREATE TABLE "EmailListas" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "Nome" character varying(100) NOT NULL,
        "Destinatarios" character varying(13000) NOT NULL,
        CONSTRAINT "PK_EmailListas" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    CREATE TABLE "EmailModelos" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "Nome" character varying(100) NOT NULL,
        "Remetente" character varying(254),
        "Assunto" character varying(200) NOT NULL,
        "Corpo" character varying(4000) NOT NULL,
        "ListaId" integer,
        CONSTRAINT "PK_EmailModelos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_EmailModelos_EmailListas_ListaId" FOREIGN KEY ("ListaId") REFERENCES "EmailListas" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    CREATE INDEX "IX_EmailEnvios_ControloId" ON "EmailEnvios" ("ControloId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    CREATE UNIQUE INDEX "IX_EmailListas_Nome" ON "EmailListas" ("Nome");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    CREATE INDEX "IX_EmailModelos_ListaId" ON "EmailModelos" ("ListaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    CREATE UNIQUE INDEX "IX_EmailModelos_Nome" ON "EmailModelos" ("Nome");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019125706_EnvioEmailPeso') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019125706_EnvioEmailPeso', '10.0.12');
    END IF;
END $EF$;
DO $access$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dmo_beta_dev_runtime') THEN
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "EmailListas", "EmailModelos", "EmailEnvios" TO dmo_beta_dev_runtime;
  GRANT USAGE, SELECT ON SEQUENCE "EmailListas_Id_seq", "EmailModelos_Id_seq", "EmailEnvios_Id_seq" TO dmo_beta_dev_runtime;
 END IF;
END $access$;
COMMIT;


-- ===== jobon-criado-em (docs/dev/jobon-criado-em/apply.sql na DMO-BETA-DEV) =====
﻿START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019130614_JobOnCriadoEm') THEN
    ALTER TABLE "JobOns" ADD "CriadoEmUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019130614_JobOnCriadoEm') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019130614_JobOnCriadoEm', '10.0.12');
    END IF;
END $EF$;
COMMIT;



-- ===== cm-de-parte-densidade-agua (docs/dev/cm-de-parte-densidade-agua/apply.sql na DMO-BETA-DEV) =====
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019131708_CmDeParteEDensidadeDaAgua') THEN
    ALTER TABLE "Pesos" ADD "WaterDensityGramsPerCm3" numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019131708_CmDeParteEDensidadeDaAgua') THEN
    UPDATE "Pesos" AS p
    SET "WaterDensityGramsPerCm3" =
        CASE WHEN p."RegistadoEmUtc" IS NULL OR p."RegistadoEmUtc" < TIMESTAMPTZ '2026-10-19 12:09:31+00'
             THEN t.antes ELSE t.dt9 END
    FROM (VALUES
        (5, 0.99888, 0.99888),
        (6, 0.99885, 0.99886),
        (7, 0.99882, 0.99882),
        (8, 0.99877, 0.99877),
        (9, 0.99871, 0.99871),
        (10, 0.99863, 0.99863),
        (11, 0.99854, 0.99854),
        (12, 0.99844, 0.99844),
        (13, 0.99832, 0.99832),
        (14, 0.99819, 0.99819),
        (15, 0.99805, 0.99805),
        (16, 0.99789, 0.99789),
        (17, 0.99773, 0.99773),
        (18, 0.99765, 0.99755),
        (19, 0.99737, 0.99737),
        (20, 0.99717, 0.99717),
        (21, 0.99696, 0.99696),
        (22, 0.99674, 0.99674),
        (23, 0.99652, 0.99652),
        (24, 0.99628, 0.99628),
        (25, 0.99603, 0.99603),
        (26, 0.99577, 0.99577),
        (27, 0.99551, 0.99551),
        (28, 0.99523, 0.99523),
        (29, 0.99494, 0.99494),
        (30, 0.99485, 0.99465),
        (31, 0.99435, 0.99435),
        (32, 0.99403, 0.99403),
        (33, 0.99371, 0.99371),
        (34, 0.99339, 0.99339),
        (35, 0.99305, 0.99305)
    ) AS t(grau, antes, dt9)
    WHERE p."WaterTemperatureC" = t.grau
      AND p."WaterDensityGramsPerCm3" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019131708_CmDeParteEDensidadeDaAgua') THEN
    CREATE TABLE "PesoComparacaoCmDecisoes" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "MeasurementId" integer NOT NULL,
        "DeParte" boolean NOT NULL,
        "EscolhidoPor" character varying(254) NOT NULL,
        "EscolhidoEmUtc" timestamp with time zone NOT NULL,
        "Aprovado" boolean,
        "AprovadoPor" character varying(254),
        "AprovadoEmUtc" timestamp with time zone,
        CONSTRAINT "PK_PesoComparacaoCmDecisoes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PesoComparacaoCmDecisoes_PesoComparacaoMeasurements_Measure~" FOREIGN KEY ("MeasurementId") REFERENCES "PesoComparacaoMeasurements" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019131708_CmDeParteEDensidadeDaAgua') THEN
    CREATE UNIQUE INDEX "IX_PesoComparacaoCmDecisoes_MeasurementId" ON "PesoComparacaoCmDecisoes" ("MeasurementId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019131708_CmDeParteEDensidadeDaAgua') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019131708_CmDeParteEDensidadeDaAgua', '10.0.12');
    END IF;
END $EF$;
DO $access$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dmo_beta_dev_runtime') THEN
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "PesoComparacaoCmDecisoes" TO dmo_beta_dev_runtime;
  GRANT USAGE, SELECT ON SEQUENCE "PesoComparacaoCmDecisoes_Id_seq" TO dmo_beta_dev_runtime;
 END IF;
END $access$;
COMMIT;


-- ===== pegamentos-rasto-das-leituras (docs/dev/pegamentos-rasto-das-leituras/apply.sql na DMO-BETA-DEV) =====
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019140057_PegamentosRastoDasLeituras') THEN
    CREATE TABLE "PegamentosMeasurementHistorico" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "PegamentosId" integer NOT NULL,
        "MeasurementId" integer NOT NULL,
        "Numero" integer NOT NULL,
        "Component" text NOT NULL,
        "Apagada" boolean NOT NULL,
        "CosturaAntesMm" numeric NOT NULL,
        "ContraCosturaAntesMm" numeric,
        "NominalGargaloAntesMm" numeric,
        "CosturaDepoisMm" numeric,
        "ContraCosturaDepoisMm" numeric,
        "NominalGargaloDepoisMm" numeric,
        "Por" character varying(254),
        "EmUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PegamentosMeasurementHistorico" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PegamentosMeasurementHistorico_Pegamentos_PegamentosId" FOREIGN KEY ("PegamentosId") REFERENCES "Pegamentos" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019140057_PegamentosRastoDasLeituras') THEN
    CREATE INDEX "IX_PegamentosMeasurementHistorico_PegamentosId_EmUtc" ON "PegamentosMeasurementHistorico" ("PegamentosId", "EmUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019140057_PegamentosRastoDasLeituras') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019140057_PegamentosRastoDasLeituras', '10.0.12');
    END IF;
END $EF$;
DO $access$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dmo_beta_dev_runtime') THEN
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE "PegamentosMeasurementHistorico" TO dmo_beta_dev_runtime;
  GRANT USAGE, SELECT ON SEQUENCE "PegamentosMeasurementHistorico_Id_seq" TO dmo_beta_dev_runtime;
 END IF;
END $access$;
COMMIT;


-- ===== ControloAreaIdentificacao (20261020015832) =====
-- Migração 20261020015832_ControloAreaIdentificacao (referência e lote escritos no PU e no CS).
-- Só acrescenta; pode correr-se mais de uma vez.
ALTER TABLE "ControloAreaStates" ADD COLUMN IF NOT EXISTS "Identificacao" character varying(120);
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261020015832_ControloAreaIdentificacao', '10.0.12')
ON CONFLICT DO NOTHING;

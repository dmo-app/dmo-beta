-- DADOS FALSOS para a cópia da app na nuvem (DMO-736). Nada disto é real.
-- :hash = hash da palavra-passe de teste (feito por hash.cs).
BEGIN;
INSERT INTO "ModuleSettings"("SettingKey","SettingValue") VALUES ('Admin:Email','admin@teste.local');

INSERT INTO "Templates" VALUES ('00000000-0000-0000-0000-0000000000a1','tudo','Tudo (teste)',
 'armazem.gestao,boquilhas,controlo.approve,controlo.create,ferramentas,jobon.create,ri', now(), now());

INSERT INTO "Users"("Id","Name","OperatorId","ProviderUserId","Email","RequiresPasswordChange","IsStandBy","CreatedAt") VALUES
 ('00000000-0000-0000-0000-000000000001','Admin Teste','9001','local-admin','admin@teste.local',false,false,now()),
 ('00000000-0000-0000-0000-000000000002','Operador Teste','9002','local-op','operador@teste.local',false,false,now());
INSERT INTO "TemplateAssignments" VALUES ('00000000-0000-0000-0000-0000000000b2','00000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-0000000000a1',now());
INSERT INTO "LocalCredentials"("ProviderUserId","Email","PasswordHash","IsTemporaryPassword","UpdatedAt") VALUES
 ('local-admin','admin@teste.local',:'hash',false,now()),
 ('local-op','operador@teste.local',:'hash',false,now());

INSERT INTO "Machines"("Id","Code") SELECT g, 'M'||g FROM generate_series(1,8) g;

-- 40 ferramentas: CM, MF, BQ
INSERT INTO "Tools"("ToolId","Type","Reference","Lot","RegisteredAtUtc")
SELECT 'T-'||t||'-'||g, t, (1000+g)::text, (1+g%3)::text, now()-(g||' days')::interval
FROM generate_series(1,40) g, (VALUES ('CM'),('MF'),('BQ')) v(t);

-- 30 Job Ons
INSERT INTO "JobOns"("Id","Reference","ProductionNumber","MachineId","InicioEm","FimEm","CriadoEmUtc")
SELECT g, 'REF'||(500+g), (100+g)::text, 1+g%8, current_date-g, CASE WHEN g>8 THEN current_date-g+7 END, now()-(g||' days')::interval FROM generate_series(1,30) g;
INSERT INTO "CmContexts"("Id","JobOnId","ToolId") SELECT g,g,'T-CM-'||g FROM generate_series(1,30) g;
INSERT INTO "MfContexts"("Id","JobOnId","ToolId") SELECT g,g,'T-MF-'||g FROM generate_series(1,30) g;
INSERT INTO "BqContexts"("Id","JobOnId","ToolId") SELECT g,g,'T-BQ-'||g FROM generate_series(1,30) g;

-- Boquilhas: reparadores, rastos e 120 movimentos
INSERT INTO "BqRepairers"("Id","Name") VALUES (1,'Reparador A'),(2,'Reparador B');
INSERT INTO "BqRepairTraces"("Id","ToolId","BqContextId","ToolReferenceFrozen","ToolLotFrozen")
SELECT g,'T-BQ-'||g,g,(1000+g)::text,(1+g%3)::text FROM generate_series(1,30) g;
INSERT INTO "BqMovements"("Id","BqRepairTraceId","Type","Quantity","MachineCodeFrozen","OccurredAtUtc","CreatedAtUtc","RegisteredByUserId","BqRepairerId","BqRepairerNameFrozen")
SELECT g, 1+g%30, CASE WHEN g%2=0 THEN 'saida' ELSE 'entrada' END, 2+g%6, 'M'||(1+g%8),
 now()-(g||' hours')::interval, now()-(g||' hours')::interval, '00000000-0000-0000-0000-000000000002', 1+g%2, CASE WHEN g%2=0 THEN 'Reparador A' ELSE 'Reparador B' END
FROM generate_series(1,120) g;

SELECT setval(pg_get_serial_sequence('"'||t||'"','Id'), 1000) FROM unnest(ARRAY['Machines','JobOns','CmContexts','MfContexts','BqContexts','BqRepairers','BqRepairTraces','BqMovements']) t;
COMMIT;

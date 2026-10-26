-- Os papéis da base no servidor do portátil (passo 4 do plano do portátil, A-0108).
--
-- A app deixa de ligar como «postgres» (o dono de tudo) e passa a ligar com
-- um papel que só lê e grava dados. As migrações continuam com o dono (o
-- migrate.sh, por DMO_MIGRATOR_CONNECTION). O backup liga com um papel que só lê.
--
-- Corre-se como postgres, na base da app, com as palavras-passe passadas na
-- linha de comando (nunca escritas no ficheiro):
--   psql -v ON_ERROR_STOP=1 -v app_pw="..." -v backup_pw="..." -d dmo -f papeis.sql
--
-- Idempotente: pode correr outra vez (atualiza as palavras-passe e os direitos).
-- Não apaga dados nem tira nada a ninguém além do PUBLIC no esquema public.

-- O \quit do psql sai sempre com 0; os erros param o ficheiro por aqui.
\set ON_ERROR_STOP on

\if :{?app_pw}
\else
  DO $$ BEGIN RAISE EXCEPTION 'Falta -v app_pw=...'; END $$;
\endif
\if :{?backup_pw}
\else
  DO $$ BEGIN RAISE EXCEPTION 'Falta -v backup_pw=...'; END $$;
\endif
-- Uma palavra-passe vazia ou curta deixava o papel aberto: recusa-se.
SELECT length(:'app_pw') < 12 AS app_pw_fraca, length(:'backup_pw') < 12 AS backup_pw_fraca \gset
\if :app_pw_fraca
  DO $$ BEGIN RAISE EXCEPTION 'A app_pw tem de ter pelo menos 12 caracteres.'; END $$;
\endif
\if :backup_pw_fraca
  DO $$ BEGIN RAISE EXCEPTION 'A backup_pw tem de ter pelo menos 12 caracteres.'; END $$;
\endif

SELECT format('CREATE ROLE dmo_app LOGIN')
 WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dmo_app') \gexec
SELECT format('CREATE ROLE dmo_backup LOGIN')
 WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dmo_backup') \gexec

ALTER ROLE dmo_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD :'app_pw';
-- O backup lê tudo, mesmo tabelas com RLS ligado (como o dmo_backup_leitura do Supabase).
ALTER ROLE dmo_backup LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE BYPASSRLS PASSWORD :'backup_pw';

-- Ninguém cria objetos no esquema public além do dono.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SELECT format('GRANT CONNECT ON DATABASE %I TO dmo_app, dmo_backup', current_database()) \gexec
GRANT USAGE ON SCHEMA public TO dmo_app, dmo_backup;

-- A app: ler e gravar dados, sem mudar a estrutura.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO dmo_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO dmo_app;
-- O backup: só ler.
GRANT SELECT ON ALL TABLES IN SCHEMA public TO dmo_backup;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO dmo_backup;

-- As tabelas que as migrações criarem a seguir (pelo dono que corre este
-- script) recebem os mesmos direitos sozinhas.
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO dmo_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO dmo_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO dmo_backup;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON SEQUENCES TO dmo_backup;

-- Conferir: a app não pode ser dona de nenhuma tabela.
SELECT count(*) AS tabelas_do_dmo_app FROM pg_tables WHERE schemaname = 'public' AND tableowner = 'dmo_app';

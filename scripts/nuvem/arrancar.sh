#!/usr/bin/env bash
# CÓPIA DA APP NA NUVEM (DMO-736 / A-0509): põe a app a correr dentro do
# contentor de uma thread, com uma base PostgreSQL local e DADOS FALSOS.
# Não toca na oficial nem no ensaio. O SAP fica desligado (nada configurado).
#
#   bash scripts/nuvem/arrancar.sh          # instala o que falta, base nova, arranca em :5080
#   node scripts/nuvem/verificar.cjs        # entra e verifica scroll a 1366 e meio ecrã
#
# Contas de teste: admin@teste.local e operador@teste.local, palavra-passe
# «teste-nuvem-123» (ou DMO_TESTE_PW). Só existem nesta base de brincar.
set -euo pipefail
cd "$(dirname "$0")/../.."
PW="${DMO_TESTE_PW:-teste-nuvem-123}"
PORTA="${DMO_PORTA:-5080}"
BASE=dmo_nuvem
LOG="${TMPDIR:-/tmp}/dmo-nuvem.log"

# 1. .NET 10 (o site da Microsoft está bloqueado pelo proxy; o pacote do Ubuntu passa)
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0 >/dev/null
fi

# parar uma cópia anterior (a que tem a porta)
fuser -k "$PORTA/tcp" >/dev/null 2>&1 || true; pkill -f "[D]MO.Alpha.Web.dll" || true; sleep 1

# 2. PostgreSQL local (já vem no contentor) e base nova, sempre vazia
service postgresql start >/dev/null 2>&1 || pg_ctlcluster 16 main start
su postgres -c "psql -qc \"ALTER USER postgres PASSWORD 'postgres'\""
su postgres -c "dropdb --if-exists $BASE && createdb $BASE"
CS="Host=127.0.0.1;Database=$BASE;Username=postgres;Password=postgres"

# 3. Migrações (as mesmas da oficial) e dados falsos
dotnet tool restore >/dev/null
dotnet ef database update --project src/DMO.Alpha.Infrastructure --startup-project src/DMO.Alpha.Web --connection "$CS" >/dev/null
HASH=$(dotnet run scripts/nuvem/hash.cs -- "$PW" | tail -1)
PGPASSWORD=postgres psql -h 127.0.0.1 -U postgres -d $BASE -v ON_ERROR_STOP=1 -q -v hash="$HASH" -f scripts/nuvem/dados-falsos.sql >/dev/null

# 4. Arrancar a app (login da própria app, como no portátil)
ASPNETCORE_ENVIRONMENT=Development Dmo__AuthenticationProvider=Local \
ConnectionStrings__DmoDatabase="$CS" ASPNETCORE_URLS="http://127.0.0.1:$PORTA" \
  nohup dotnet run --project src/DMO.Alpha.Web --no-launch-profile >"$LOG" 2>&1 &
for _ in $(seq 90); do
  curl -sf -o /dev/null "http://127.0.0.1:$PORTA/login" && { echo "App a correr em http://127.0.0.1:$PORTA (registo: $LOG)"; exit 0; }
  sleep 2
done
echo "A app não arrancou. Ver $LOG" >&2; exit 1

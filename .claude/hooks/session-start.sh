#!/bin/bash
# Sessões Claude na nuvem: instala o .NET 10 e arranca um PostgreSQL local,
# para que todos os testes corram (também os que precisam de base de dados).
# A base é descartável e só existe dentro do contentor da sessão: nunca a
# base oficial nem a do ensaio do portátil.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# .NET 10 (o global.json pede 10.0.100 ou mais recente na mesma linha).
if ! command -v dotnet >/dev/null 2>&1; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0 >/dev/null
fi

# PostgreSQL local, com a mesma ligação por omissão do scripts/ci/ci-local.sh.
if ! command -v pg_ctlcluster >/dev/null 2>&1; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq postgresql >/dev/null
fi
cluster="$(pg_lsclusters -h | awk 'NR==1 {print $1" "$2}')"
if ! pg_lsclusters -h | awk 'NR==1 {exit $4=="online" ? 0 : 1}'; then
  pg_ctlcluster $cluster start
fi
for _ in $(seq 1 30); do
  pg_isready -q -h localhost -p 5432 && break
  sleep 1
done
su postgres -c "psql -qc \"alter user postgres password 'postgres'\""
# Base fixa para «dotnet test» direto; o ci-local.sh cria e apaga a sua.
if ! su postgres -c "psql -tAc \"select 1 from pg_database where datname='dmo_test'\"" | grep -q 1; then
  su postgres -c "createdb dmo_test"
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  echo 'export DMO_POSTGRES_CONNECTION="Host=localhost;Port=5432;Database=dmo_test;Username=postgres;Password=postgres"' >> "$CLAUDE_ENV_FILE"
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >> "$CLAUDE_ENV_FILE"
fi

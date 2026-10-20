#!/bin/sh
# Gera o pacote de migração do EF (efbundle) do DMO (0.2).
# É o mesmo script no build da imagem (Dockerfile, a seguir ao dotnet publish)
# e no CI local (scripts/ci/ci-local.sh, a seguir ao Build), com o mesmo
# dotnet-ef, fixado em .config/dotnet-tools.json na versão do EF Core.
#
# Uso: sh scripts/db/efbundle.sh <saída>
#   Corre depois de um build ou publish em Release (usa --no-build).
#   Sem -r nem --self-contained: o pacote (cerca de 50 MB) usa o runtime .NET
#   da imagem. Gerar o pacote não abre nenhuma base de dados; a ligação só é
#   usada quando o pacote corre (migrate.sh, com --connection).
set -eu

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo "Uso: sh scripts/db/efbundle.sh <saída>" >&2
  exit 2
fi

# A saída relativa conta a partir da pasta de onde o script foi chamado.
case "$1" in
  /*) output="$1" ;;
  *) output="$(pwd)/$1" ;;
esac

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

dotnet tool restore
dotnet ef migrations bundle \
  --project src/DMO.Alpha.Infrastructure \
  --startup-project src/DMO.Alpha.Web \
  --configuration Release \
  --no-build \
  --output "$output" \
  --force

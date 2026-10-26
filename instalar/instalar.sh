#!/usr/bin/env bash
# Instala ou atualiza o DMO nesta máquina com um só comando (Docker Compose).
#
#   ./instalar.sh
#
# Da primeira vez cria o .env com palavras-passe sorteadas (fica só nesta
# máquina, com permissões 600) e uma base nova e vazia nesta máquina. Depois
# constrói a app, aplica as migrações, cria os utilizadores da base e arranca.
# No fim diz o endereço e, numa base sem ADMIN, o código da primeira
# instalação. Correr outra vez atualiza para o código que estiver na pasta.
set -euo pipefail

cd "$(dirname "$0")"

if ! docker compose version >/dev/null 2>&1; then
  echo "instalar: FALHOU. Falta o Docker com o Compose (docker compose)." >&2
  exit 1
fi

if [ ! -f .env ]; then
  sorteia() { tr -dc 'A-Za-z0-9' </dev/urandom | head -c 32; }
  umask 077
  cat > .env <<ENV
# Criado por instalar.sh em $(date -u +%Y-%m-%dT%H:%MZ). Guarde uma cópia em sítio seguro.
# Base nesta máquina (perfil base-local). Para usar uma base que já existe:
# apague a linha COMPOSE_PROFILES e preencha DMO_BASE_HOST/PORTA/NOME/DONO.
COMPOSE_PROFILES=base-local
DMO_BASE_DONO_PW=$(sorteia)
DMO_APP_PW=$(sorteia)
DMO_BACKUP_PW=$(sorteia)
DMO_PORTA=8080
# Opcional: o código da primeira instalação (sem ele, é sorteado e mostrado no fim).
DMO_CODIGO_INSTALACAO=
ENV
  echo "Criado o .env com palavras-passe novas."
fi

set -a; . ./.env; set +a
DMO_REVISAO="$(git rev-parse --short=7 HEAD 2>/dev/null || echo local)"
export DMO_REVISAO
if [ -z "$(git status --porcelain 2>/dev/null)" ] && git rev-parse HEAD >/dev/null 2>&1; then export DMO_ESTADO=clean; fi

# A atualizar uma instalação que já tem base nesta máquina: cópia da base
# antes de migrar, verificada, para se poder voltar atrás (pg_restore).
if [ -n "$(docker compose ps --status running -q base 2>/dev/null)" ]; then
  mkdir -p copias
  copia="copias/dmo-$(date +%Y%m%d-%H%M%S)-antes-de-$DMO_REVISAO.dump"
  dono="${DMO_BASE_DONO:-postgres}"
  nome_base="${DMO_BASE_NOME:-dmo}"
  (umask 077; docker compose exec -T base pg_dump -U "$dono" -d "$nome_base" -Fc > "$copia")
  if ! docker compose exec -T base pg_restore --list < "$copia" >/dev/null 2>&1; then
    echo "instalar: FALHOU. A cópia da base ($copia) não se consegue ler; nada foi mudado." >&2
    exit 1
  fi
  echo "Cópia da base antes de atualizar: instalar/$copia"
  ls -1t copias/*.dump | tail -n +11 | xargs -r rm --
fi

echo "A construir e a arrancar (versão $DMO_REVISAO)…"
docker compose up -d --build --remove-orphans

porta="${DMO_PORTA:-8080}"
echo "À espera que a app responda (até 2 minutos)…"
for _ in $(seq 1 60); do
  if curl -fs -o /dev/null "http://127.0.0.1:$porta/login" 2>/dev/null; then
    echo
    echo "instalar: OK. A app está em http://<este computador>:$porta"
    codigo="$(docker compose logs app 2>/dev/null | grep -o 'código de instalação [A-Z0-9-]*' | tail -1 | awk '{print $NF}')"
    if [ -n "$codigo" ]; then
      echo "Primeira instalação: abra /instalar e use o código $codigo"
    elif [ -n "${DMO_CODIGO_INSTALACAO:-}" ] && docker compose logs app 2>/dev/null | grep -q 'Primeira instalação por fazer'; then
      echo "Primeira instalação: abra /instalar e use o código DMO_CODIGO_INSTALACAO do .env"
    fi
    exit 0
  fi
  sleep 2
done

echo "instalar: FALHOU. A app não respondeu. Últimas linhas:" >&2
docker compose logs --tail 40 migrar papeis app >&2 || true
exit 1

#!/usr/bin/env bash
# Atualiza a app no servidor do portátil com um só comando (passo 5 do plano do portátil, A-0108).
#
# Corre no Ubuntu do portátil (WSL2), como o utilizador da app:
#   bash ~/dmo-beta/scripts/portatil/atualizar.sh            # ramo integracao
#   bash ~/dmo-beta/scripts/portatil/atualizar.sh main       # outro ramo
#
# O que faz, por esta ordem, e pára no primeiro erro:
#   1. vai buscar o ramo ao GitHub e põe ~/dmo-beta nesse commit;
#   2. constrói a imagem nova (com o número do commit, que a app mostra);
#   3. tira uma cópia da base antes de mexer (pg_dump);
#   4. aplica as migrações com a imagem nova (migrate.sh, com o dono da base);
#   5. troca o contentor e espera que a página de entrada responda;
#   6. se a versão nova não responder, volta a pôr a anterior a correr.
# As migrações só acrescentam; se o passo 6 voltar atrás, a versão anterior
# continua a funcionar com a base já migrada (é a regra dos PRs).
#
# Configuração (variáveis de ambiente, todas com valor por omissão):
#   DMO_REPO      pasta do código           (~/dmo-beta)
#   DMO_ENV       env da app                (~/dmo-local.env)
#   DMO_MIGRAR    env das migrações         (~/dmo-migrar.env; tem
#                 DMO_MIGRATOR_CONNECTION com o dono da base; sem ele usa o DMO_ENV)
#   DMO_COPIAS    cópias antes de migrar    (~/dmo-antes-de-atualizar, 10 últimas)
set -euo pipefail

# O passo 1 muda o próprio ficheiro (git checkout); o bash lê o script aos
# poucos, por isso corre-se uma cópia fora do repositório.
if [ -z "${DMO_ATUALIZAR_COPIA:-}" ]; then
  copia_script="$(mktemp)"
  cp "$0" "$copia_script"
  DMO_ATUALIZAR_COPIA=1 exec bash "$copia_script" "$@"
fi

ramo="${1:-integracao}"
repo="${DMO_REPO:-$HOME/dmo-beta}"
env_app="${DMO_ENV:-$HOME/dmo-local.env}"
env_migrar="${DMO_MIGRAR:-$HOME/dmo-migrar.env}"
copias="${DMO_COPIAS:-$HOME/dmo-antes-de-atualizar}"
nome="dmo-local"
anterior="dmo-local-anterior"
rede="dmo-net"
base_ct="dmo-pg"

passo() { echo; echo "== $*"; }
falha() { echo "atualizar: FALHOU. $*" >&2; exit 1; }

[ -f "$env_app" ] || falha "Não encontrei $env_app."
[ -f "$env_migrar" ] || env_migrar="$env_app"

passo "1. Código: ramo $ramo"
cd "$repo"
git fetch --quiet origin "$ramo"
git checkout --quiet --detach "origin/$ramo"
rev="$(git rev-parse --short=7 HEAD)"
echo "commit $rev: $(git log -1 --format=%s)"

passo "2. Imagem dmo-local:$rev"
docker build --quiet \
  --build-arg DMO_SOURCE_REVISION="$rev" \
  --build-arg DMO_SOURCE_STATE=clean \
  -t "dmo-local:$rev" . >/dev/null
echo "imagem pronta"

passo "3. Cópia da base antes de mexer"
mkdir -p "$copias"
copia="$copias/dmo-$(date +%Y%m%d-%H%M%S)-antes-de-$rev.dump"
docker exec "$base_ct" pg_dump -U postgres -d dmo -Fc > "$copia"
echo "$copia ($(du -h "$copia" | cut -f1))"
ls -1t "$copias"/*.dump | tail -n +11 | xargs -r rm --

passo "4. Migrações"
docker run --rm --network "$rede" --env-file "$env_migrar" \
  "dmo-local:$rev" ./migrate.sh

passo "5. Trocar o contentor"
docker rm -f "$anterior" >/dev/null 2>&1 || true
if docker inspect "$nome" >/dev/null 2>&1; then
  docker stop "$nome" >/dev/null
  docker rename "$nome" "$anterior"
  docker update --restart=no "$anterior" >/dev/null
fi
docker run -d --name "$nome" --restart unless-stopped --network "$rede" \
  -p 8080:8080 --env-file "$env_app" "dmo-local:$rev" >/dev/null

echo "à espera que a app responda (até 60 s)…"
ok=""
for _ in $(seq 1 30); do
  # Enquanto a app arranca, o curl falha sem dizer nada; só conta o «200».
  if curl -fs -o /dev/null http://127.0.0.1:8080/login 2>/dev/null; then ok=1; break; fi
  sleep 2
done

if [ -n "$ok" ]; then
  docker tag "dmo-local:$rev" dmo-local:latest
  echo
  echo "atualizar: OK. A app está no commit $rev (ramo $ramo)."
  echo "A versão anterior ficou parada em $anterior; apaga-se na próxima atualização."
  exit 0
fi

passo "6. A versão nova não respondeu: volta a anterior"
docker logs --tail 40 "$nome" >&2 || true
docker rm -f "$nome" >/dev/null
if docker inspect "$anterior" >/dev/null 2>&1; then
  docker rename "$anterior" "$nome"
  docker update --restart=unless-stopped "$nome" >/dev/null
  docker start "$nome" >/dev/null
  falha "Voltou a versão anterior. A cópia da base de antes está em $copia."
fi
falha "Não havia versão anterior para repor. A cópia da base está em $copia."

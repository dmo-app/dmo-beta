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
#   4. ensaia as migrações numa cópia da base e pára se alguma tabela perder linhas;
#   5. aplica as migrações com a imagem nova (migrate.sh, com o dono da base);
#   6. troca o contentor e espera que a página de entrada responda;
#   7. se a versão nova não arrancar ou não responder, volta a pôr a anterior a correr.
# As migrações só acrescentam; se o passo 7 voltar atrás, a versão anterior
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

# Tira a versão nova e volta a pôr a anterior a correr, com o nome e o
# arranque automático de sempre. Falha se não havia versão anterior.
repor_anterior() {
  docker rm -f "$nome" >/dev/null 2>&1 || true
  docker container inspect "$anterior" >/dev/null 2>&1 || return 1
  docker rename "$anterior" "$nome"
  docker update --restart=unless-stopped "$nome" >/dev/null
  docker start "$nome" >/dev/null
}

# Depois de a versão anterior parar, qualquer erro (o set -e sai logo, por
# exemplo se o docker run da nova falhar) tem de a repor; senão a app fica parada.
recuar_se_sair() {
  local rc=$?
  trap - EXIT
  echo "atualizar: FALHOU a meio da troca; volta a versão anterior." >&2
  if repor_anterior; then
    echo "Voltou a versão anterior. A cópia da base de antes está em $copia." >&2
  else
    echo "Não havia versão anterior para repor. A cópia da base está em $copia." >&2
  fi
  exit "$rc"
}

[ -f "$env_app" ] || falha "Não encontrei $env_app."
[ -f "$env_migrar" ] || env_migrar="$env_app"

# A app liga com o papel que só lê e grava dados (papeis.sql), nunca com o
# dono da base nem com outro superutilizador.
utilizador_app="$(grep -m1 '^ConnectionStrings__DmoDatabase=' "$env_app" | tr ';' '\n' \
  | grep -i -E '^ *(username|user id|userid|user) *=' | head -1 | cut -d= -f2- | tr -d ' "')"
[ -n "$utilizador_app" ] || falha "Em $env_app, ConnectionStrings__DmoDatabase não diz o utilizador (Username=dmo_app)."
poderes="$(docker exec "$base_ct" psql -U postgres -d dmo -tAc \
  "SELECT rolsuper OR rolcreaterole OR rolcreatedb FROM pg_roles WHERE rolname = '${utilizador_app//\'/}'")"
[ -n "$poderes" ] || falha "O utilizador da app ($utilizador_app) não existe na base. Corra o papeis.sql (scripts/portatil/LEIA-ME.md)."
[ "$poderes" = "f" ] || falha "A app liga como $utilizador_app, que manda na base. Use Username=dmo_app em $env_app (scripts/portatil/LEIA-ME.md)."

passo "1. Código: ramo $ramo"
cd "$repo"
git fetch --quiet origin "$ramo"
git checkout --quiet --detach "origin/$ramo"
rev="$(git rev-parse --short=7 HEAD)"
echo "commit $rev: $(git log -1 --format=%s)"
# O checkout mantém ficheiros mudados à mão ou novos; a app só se diz «clean»
# quando a pasta é exatamente o commit.
estado=clean
if [ -n "$(git status --porcelain)" ]; then
  estado=modified
  echo "Atenção: a pasta $repo tem ficheiros mudados ou novos; a versão fica marcada como modificada."
fi

passo "2. Imagem dmo-local:$rev"
docker build --quiet \
  --build-arg DMO_SOURCE_REVISION="$rev" \
  --build-arg DMO_SOURCE_STATE="$estado" \
  -t "dmo-local:$rev" . >/dev/null
echo "imagem pronta"

passo "3. Cópia da base antes de mexer"
mkdir -p "$copias"
copia="$copias/dmo-$(date +%Y%m%d-%H%M%S)-antes-de-$rev.dump"
# A cópia leva os resumos das palavras-passe: só o dono a lê (como no instalar.sh).
(umask 077; docker exec "$base_ct" pg_dump -U postgres -d dmo -Fc > "$copia")
# A cópia só conta se se conseguir ler e tiver os dados de todas as tabelas.
tabelas_copia="$(docker exec -i "$base_ct" pg_restore --list < "$copia" | grep -c ' TABLE DATA public ' || true)"
tabelas_base="$(docker exec "$base_ct" psql -U postgres -d dmo -tAc \
  "SELECT count(*) FROM pg_tables WHERE schemaname = 'public'")"
[ "$tabelas_copia" = "$tabelas_base" ] \
  || falha "A cópia $copia não está boa ($tabelas_copia de $tabelas_base tabelas). Nada foi mudado."
echo "$copia ($(du -h "$copia" | cut -f1), $tabelas_copia tabelas)"
ls -1t "$copias"/*.dump | tail -n +11 | xargs -r rm --

passo "4. Ensaio das migrações numa cópia da base"
# As migrações correm primeiro numa base à parte, reposta da cópia; se alguma
# tabela perder linhas, pára aqui sem ter tocado na base da app.
ensaio="dmo_ensaio_atualizar"
psql_base() { docker exec -i "$base_ct" psql -U postgres -v ON_ERROR_STOP=1 -qtA "$@"; }
contagens() {
  psql_base -d "$1" -c "SELECT table_name || ' ' || (xpath('/row/c/text()', query_to_xml(
      format('SELECT count(*) AS c FROM public.%I', table_name), false, true, '')))[1]::text
    FROM information_schema.tables
   WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY 1"
}
psql_base -d postgres -c "DROP DATABASE IF EXISTS $ensaio WITH (FORCE)" -c "CREATE DATABASE $ensaio"
docker exec -i "$base_ct" pg_restore -U postgres -d "$ensaio" --no-owner --exit-on-error < "$copia"
env_ensaio="$(mktemp)"
sed -E 's/((^|;)[[:space:]]*[Dd]atabase[[:space:]]*=)[^;]*/\1'"$ensaio"'/' "$env_migrar" > "$env_ensaio"
antes="$(contagens "$ensaio")"
if ! docker run --rm --network "$rede" --env-file "$env_ensaio" "dmo-local:$rev" ./migrate.sh >/dev/null; then
  rm -f "$env_ensaio"
  falha "As migrações falharam na cópia ($ensaio fica para ver). A base da app não foi mexida."
fi
rm -f "$env_ensaio"
depois="$(contagens "$ensaio")"
perdas="$(LC_ALL=C join <(LC_ALL=C sort <<<"$antes") <(LC_ALL=C sort <<<"$depois") | awk '$3 < $2 { print $1 ": " $2 " → " $3 }')"
[ -z "$perdas" ] || falha "As migrações apagam linhas ($ensaio fica para ver):
$perdas
A base da app não foi mexida."
psql_base -d postgres -c "DROP DATABASE $ensaio"
echo "ensaio OK: $(echo "$antes" | wc -l) tabelas, nenhuma perdeu linhas"

passo "5. Migrações"
docker run --rm --network "$rede" --env-file "$env_migrar" \
  "dmo-local:$rev" ./migrate.sh

passo "6. Trocar o contentor"
docker rm -f "$anterior" >/dev/null 2>&1 || true
trap recuar_se_sair EXIT
if docker container inspect "$nome" >/dev/null 2>&1; then
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
  trap - EXIT
  docker tag "dmo-local:$rev" dmo-local:latest
  echo
  echo "atualizar: OK. A app está no commit $rev (ramo $ramo)."
  echo "A versão anterior ficou parada em $anterior; apaga-se na próxima atualização."
  exit 0
fi

passo "7. A versão nova não respondeu: volta a anterior"
trap - EXIT
docker logs --tail 40 "$nome" >&2 || true
if repor_anterior; then
  falha "Voltou a versão anterior. A cópia da base de antes está em $copia."
fi
falha "Não havia versão anterior para repor. A cópia da base está em $copia."

#!/usr/bin/env bash
# CI local: compila, corre os testes e mostra-os pelos grupos de scripts/ci/testes.md,
# pela ordem da tabela, e termina com «CI local: OK», «CI local: INCOMPLETO» (uma
# parte foi desligada com CI_SEM_*; sai com 2) ou «CI local: FALHOU» (sai com 1).
# Precisa do .NET e do Python 3.
# Começa pela guarda dos testes (scripts/ci/guarda-testes.py): um teste que mudou,
# saiu ou entrou sem explicação em scripts/ci/testes-alteracoes.md faz falhar o CI.
# Os testes de migração correm numa base PostgreSQL descartável, criada no
# início e apagada no fim.
#
# Uso: scripts/ci/ci-local.sh
#   CI_PG_ADMIN        ligação ao PostgreSQL local
#                      (por omissão Host=localhost;Port=5432;Username=postgres;Password=postgres)
#   CI_SEM_POSTGRES=1  corre sem PostgreSQL (os testes de migração ficam ignorados; INCOMPLETO)
#   CI_SEM_NODE=1      não corre os testes de JavaScript da app (tests/ShellProof; INCOMPLETO)
#   CI_SEM_BROWSER=1   não corre os testes no browser (catálogo, paginação, pasta dos PDFs e Excel das Boquilhas, tests/visual; INCOMPLETO)
set -uo pipefail

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
fi

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"
index="scripts/ci/testes.md"
infra="tests/DMO.Alpha.Infrastructure.Tests"
web="tests/DMO.Alpha.Web.Tests"
logs="$(mktemp -d)"
pg_admin="${CI_PG_ADMIN:-Host=localhost;Port=5432;Username=postgres;Password=postgres}"

value() { echo "$pg_admin" | tr ';' '\n' | sed -n "s/^$1=//Ip" | head -1; }
pg_host="$(value Host)"; pg_port="$(value Port)"; pg_user="$(value Username)"; pg_pass="$(value Password)"
pg_port="${pg_port:-5432}"
db=""
# O que ficou por correr por escolha (CI_SEM_*): o resultado é INCOMPLETO, nunca OK.
incompleto=()

cleanup() {
  if [[ -n "$db" ]]; then
    PGPASSWORD="$pg_pass" psql -h "$pg_host" -p "$pg_port" -U "$pg_user" -qc "drop database if exists \"$db\" with (force)" >/dev/null 2>&1
  fi
  rm -rf "$logs"
}
trap cleanup EXIT

if [[ "${CI_SEM_POSTGRES:-}" == "1" ]]; then
  echo "PostgreSQL: não corrido (CI_SEM_POSTGRES=1); os testes de migração ficam ignorados."
  unset DMO_POSTGRES_CONNECTION
  incompleto+=("PostgreSQL")
else
  if [[ "$pg_host" == *supabase* ]]; then
    echo "CI local: FALHOU. CI_PG_ADMIN aponta para o Supabase; os testes de migração apagam dados. Use um PostgreSQL local."
    exit 1
  fi
  db="dmo_ci_$(date +%s)_$$_test"
  if ! PGPASSWORD="$pg_pass" psql -h "$pg_host" -p "$pg_port" -U "$pg_user" -qc "create database \"$db\"" >/dev/null 2>&1; then
    db=""
    echo "CI local: FALHOU. Não foi possível criar a base descartável em $pg_host:$pg_port. Arranque o PostgreSQL local ou use CI_SEM_POSTGRES=1."
    exit 1
  fi
  export DMO_POSTGRES_CONNECTION="Host=$pg_host;Port=$pg_port;Database=$db;Username=$pg_user;Password=$pg_pass"
  echo "PostgreSQL: base descartável $db em $pg_host:$pg_port."
fi

summary=()
failed=0

step() {  # nome, comando
  echo
  echo "== $1"
  if bash -c "$2" >"$logs/step.log" 2>&1; then
    summary+=("OK      $1")
  else
    tail -30 "$logs/step.log"
    summary+=("FALHOU  $1")
    failed=1
  fi
}

step "Guarda dos testes" "python3 scripts/ci/guarda-testes.py verificar"
step "Restore" "dotnet restore DMO.Alpha.slnx"
step "Build" "dotnet build DMO.Alpha.slnx --no-restore --configuration Release"
if [[ $failed -ne 0 ]]; then
  printf '\n== Resumo\n'; printf '%s\n' "${summary[@]}"; echo "CI local: FALHOU"; exit 1
fi

# Cada projeto de teste corre uma só vez, com o relatório em TRX; scripts/ci/grupos.py
# reparte os resultados pelos grupos de scripts/ci/testes.md, pela ordem da tabela.
# Os testes ToolCatalog_* exportam aqui o HTML que o browser abre mais abaixo.
ui="$logs/ui"; mkdir -p "$ui"
for project in "$infra" "$web"; do
  name="$(basename "$project")"
  DMO_UI_EVIDENCE_DIR="$ui" dotnet test "$project" --no-build --configuration Release \
    --logger "trx;LogFileName=$name.trx" --results-directory "$logs" >"$logs/$name.log" 2>&1
  code=$?
  # Um código de erro sem testes falhados quer dizer que o projeto não chegou a correr até ao fim.
  if [[ $code -ne 0 ]] && ! grep -q "^Failed! " "$logs/$name.log"; then
    echo; echo "== $name: não correu até ao fim"; tail -30 "$logs/$name.log"
    summary+=("FALHOU  $name (não correu até ao fim)"); failed=1
  fi
done
python3 scripts/ci/grupos.py "$index" "$logs/grupos.txt" \
  "$logs/$(basename "$infra").trx" "$logs/$(basename "$web").trx" || failed=1
while IFS= read -r line; do summary+=("$line"); done < "$logs/grupos.txt"

# O JavaScript da app (tests/ShellProof): a folha do Peso e o atualizar só o que muda do Job On; corre em Node com o jsdom,
# instalado na própria pasta (node_modules fica fora do git).
echo
echo "== JavaScript da app"
echo "   A pré-visualização do Peso e o atualizar só o que muda (Job On), no browser simulado."
if [[ "${CI_SEM_NODE:-}" == "1" ]]; then
  echo "   não corrido (CI_SEM_NODE=1)"
  summary+=("--      JavaScript da app (CI_SEM_NODE=1)")
  incompleto+=("JavaScript")
elif ! command -v node >/dev/null 2>&1 || ! command -v npm >/dev/null 2>&1; then
  echo "   Node.js não encontrado. Instale o Node.js ou use CI_SEM_NODE=1."
  summary+=("FALHOU  JavaScript da app (sem Node.js)")
  failed=1
elif npm --prefix tests/ShellProof ci --no-audit --no-fund >"$logs/js.log" 2>&1 \
  && npm --prefix tests/ShellProof run test:js >>"$logs/js.log" 2>&1; then
  js_pass="$(sed -n 's/^# pass //p' "$logs/js.log" | tail -1)"
  echo "   ${js_pass:-0} passaram"
  summary+=("OK      JavaScript da app (${js_pass:-0} passaram)")
else
  tail -30 "$logs/js.log"
  summary+=("FALHOU  JavaScript da app")
  failed=1
fi

# O browser no catálogo das Boquilhas (tests/visual/boquilhas_catalog_browser.py):
# os testes ToolCatalog_* já exportaram o HTML que a app gera e o Chromium, pelo
# Playwright para Python, verifica o «Criar boquilha» (com a máquina do filtro)
# e abrir a ficha com um clique, a 1920, 768 e 390 px.
echo
echo "== Browser nas Boquilhas"
echo "   O catálogo das Boquilhas no Chromium: «Criar boquilha» e abrir a ficha."
if [[ "${CI_SEM_BROWSER:-}" == "1" ]]; then
  echo "   não corrido (CI_SEM_BROWSER=1)"
  summary+=("--      Browser nas Boquilhas (CI_SEM_BROWSER=1)")
  incompleto+=("browser")
else
  if ! python3 -c "import playwright" >/dev/null 2>&1; then
    python3 -m pip install --quiet playwright==1.56.0 >"$logs/ui.log" 2>&1
  fi
  if python3 tests/visual/boquilhas_catalog_browser.py "$ui" >>"$logs/ui.log" 2>&1; then
    echo "   3 larguras e sem JavaScript: passaram"
    summary+=("OK      Browser nas Boquilhas")
  else
    tail -30 "$logs/ui.log"
    echo "   Precisa do Python 3 com o Playwright e de um Chromium (python3 -m playwright install chromium, ou DMO_CHROMIUM=<caminho>), ou CI_SEM_BROWSER=1."
    summary+=("FALHOU  Browser nas Boquilhas")
    failed=1
  fi
fi

# A paginação parcial (tests/visual/paginacao_parcial_browser.py): o HTML que os
# testes das Ferramentas e do catálogo exportaram, no Chromium, com «seguinte» e
# «anterior» a trocar só a zona dos resultados, sem recarregar a página.
echo
echo "== Browser na paginação"
echo "   Seguinte e anterior nas Ferramentas e no catálogo das Boquilhas trocam só os resultados, a 1400 e a 390 px."
if [[ "${CI_SEM_BROWSER:-}" == "1" ]]; then
  echo "   não corrido (CI_SEM_BROWSER=1)"
  summary+=("--      Browser na paginação (CI_SEM_BROWSER=1)")
elif python3 tests/visual/paginacao_parcial_browser.py "$ui" >"$logs/ui-paginacao.log" 2>&1; then
  echo "   passaram"
  summary+=("OK      Browser na paginação")
else
  tail -30 "$logs/ui-paginacao.log"
  summary+=("FALHOU  Browser na paginação")
  failed=1
fi

echo
echo "== Browser nas falhas do teste do agente"
echo "   A dica «Posta a zero», o Lote do PU NNPB, os botões do Peso no Resumo e os da Revisão e as sugestões da procura do Job On, no Chromium."
if [[ "${CI_SEM_BROWSER:-}" == "1" ]]; then
  echo "   não corrido (CI_SEM_BROWSER=1)"
  summary+=("--      Browser nas falhas do teste do agente (CI_SEM_BROWSER=1)")
elif python3 tests/visual/nok_agente_browser.py "$ui" >"$logs/ui-nok.log" 2>&1; then
  echo "   passaram"
  summary+=("OK      Browser nas falhas do teste do agente")
else
  tail -30 "$logs/ui-nok.log"
  summary+=("FALHOU  Browser nas falhas do teste do agente")
  failed=1
fi

echo
echo "== Browser na pasta dos PDFs"
echo "   A pasta dos PDFs no Chromium: sem pasta, com pasta (referência/produção), deixar de usar e duplo clique."
if [[ "${CI_SEM_BROWSER:-}" == "1" ]]; then
  echo "   não corrido (CI_SEM_BROWSER=1)"
  summary+=("--      Browser na pasta dos PDFs (CI_SEM_BROWSER=1)")
elif python3 tests/visual/pasta_pdf_browser.py "$logs/ui-pasta" >"$logs/ui-pasta.log" 2>&1; then
  echo "   passaram"
  summary+=("OK      Browser na pasta dos PDFs")
else
  tail -30 "$logs/ui-pasta.log"
  summary+=("FALHOU  Browser na pasta dos PDFs")
  failed=1
fi

echo
echo "== Browser no Excel das Boquilhas"
echo "   O Excel das Boquilhas no Chromium: sem Excel, escrita ao abrir, versão igual, depois de um registo, recusa do servidor e deixar de usar."
if [[ "${CI_SEM_BROWSER:-}" == "1" ]]; then
  echo "   não corrido (CI_SEM_BROWSER=1)"
  summary+=("--      Browser no Excel das Boquilhas (CI_SEM_BROWSER=1)")
elif python3 tests/visual/excel_bq_browser.py "$logs/ui-excel-bq" >"$logs/ui-excel-bq.log" 2>&1; then
  echo "   passaram"
  summary+=("OK      Browser no Excel das Boquilhas")
else
  tail -30 "$logs/ui-excel-bq.log"
  summary+=("FALHOU  Browser no Excel das Boquilhas")
  failed=1
fi

echo
echo "== Resumo"
printf '%s\n' "${summary[@]}"
if [[ $failed -ne 0 ]]; then
  echo "CI local: FALHOU"
  exit 1
fi
if [[ ${#incompleto[@]} -gt 0 ]]; then
  echo "CI local: INCOMPLETO (não correu: $(printf '%s, ' "${incompleto[@]}" | sed 's/, $//'))"
  exit 2
fi
echo "CI local: OK"

#!/bin/sh
# O PASSO DE MIGRAÇÃO DO DEPLOY: aplica o pacote de migração (efbundle) à base,
# antes de a versão nova arrancar (no Railway, o comando de pré-deploy do
# serviço, que pode passar pelo entrypoint: ./entrypoint.sh /app/migrate.sh).
# A app continua a não aplicar migrações no arranque.
#
# A ligação vem de DMO_MIGRATOR_CONNECTION (o papel de migração) ou, sem ela,
# de ConnectionStrings__DmoDatabase. É sempre passada ao pacote com
# --connection: sem nenhuma das duas, o script falha em vez de deixar o pacote
# cair na ligação de exemplo da fábrica de design (DmoDbContextFactory).
# Escreve qual das duas usou, nunca o valor.
#
#   DMO_EFBUNDLE  caminho do pacote (por omissão /app/efbundle)
# Devolve o código de saída do pacote.
set -u

bundle="${DMO_EFBUNDLE:-/app/efbundle}"

if [ -n "${DMO_MIGRATOR_CONNECTION:-}" ]; then
  connection="$DMO_MIGRATOR_CONNECTION"
  source_name="DMO_MIGRATOR_CONNECTION"
elif [ -n "${ConnectionStrings__DmoDatabase:-}" ]; then
  connection="$ConnectionStrings__DmoDatabase"
  source_name="ConnectionStrings__DmoDatabase"
else
  echo "migrate.sh: FALHOU. Falta a ligação à base: defina DMO_MIGRATOR_CONNECTION (ou ConnectionStrings__DmoDatabase)." >&2
  exit 1
fi

if [ ! -x "$bundle" ]; then
  echo "migrate.sh: FALHOU. O pacote de migração $bundle não existe ou não é executável." >&2
  exit 1
fi

echo "migrate.sh: a aplicar o pacote $bundle com a ligação de $source_name."
"$bundle" --connection "$connection" "$@"
code=$?
if [ "$code" -eq 0 ]; then
  echo "migrate.sh: OK."
else
  echo "migrate.sh: FALHOU (código $code do pacote)." >&2
fi
exit "$code"

#!/bin/sh
# O ENTRYPOINT DO DMO NO RAILWAY: a chave-mestra portátil (o certificado X509)
# chega PELA ENV (Dmo__DataProtectionCertPfx, base64 do PFX) e o ASP.NET
# carrega-a diretamente no arranque (ProtectKeysWithCertificate no Program.cs)
# — sem ficheiros no container. A chave DPAPI da casa (Windows-only) NÃO
# viaja mais: o Linux nunca a abriria (o ineligible do 12/10).

# Com argumentos, executa-os sem arrancar a app: o pré-deploy pode passar pelo
# ENTRYPOINT (por exemplo ./entrypoint.sh /app/migrate.sh).
if [ "$#" -gt 0 ]; then
  exec "$@"
fi

# Sem argumentos, arranca a app. O segredo do migrador é só do pré-deploy:
# o processo web não o recebe.
unset DMO_MIGRATOR_CONNECTION
# A porta vem pela env PORT do Railway, lida aqui no arranque (8080 sem ela).
export ASPNETCORE_URLS="http://+:${PORT:-8080}"
exec dotnet DMO.Alpha.Web.dll --no-launch-profile

#!/bin/sh
# O ENTRYPOINT DO DMO NO RAILWAY: a chave-mestra portátil (o certificado X509)
# chega PELA ENV (Dmo__DataProtectionCertPfx, base64 do PFX) e o ASP.NET
# carrega-a diretamente no arranque (ProtectKeysWithCertificate no Program.cs)
# — sem ficheiros no container. A chave DPAPI da casa (Windows-only) NÃO
# viaja mais: o Linux nunca a abriria (o ineligible do 12/10).
exec dotnet DMO.Alpha.Web.dll --no-launch-profile

# Dockerfile da 0.2 (base do full). O SDK é o mesmo do global.json.
# O PIN DO SDK (o mistério das rotas 2026-10-13: o Railway construia com a tag
# flutuante sdk:10.0 — uma versao que gerava a tabela de rotas das vistas torta,
# TODAS as rotas a servir /Admin/Definicoes). Mantém-se o pin explícito, agora
# atualizado para 10.0.402 e validado com a suite de rotas.
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
COPY . .
ARG DMO_SOURCE_REVISION
ARG RAILWAY_GIT_COMMIT_SHA
ARG DMO_SOURCE_STATE=unverified
# o publish Release (o OutDir do container)
RUN dotnet publish src/DMO.Alpha.Web -c Release -o /app \
    -p:DmoRevision="${DMO_SOURCE_REVISION:-$RAILWAY_GIT_COMMIT_SHA}" \
    -p:DmoSourceState="$DMO_SOURCE_STATE"
# O pacote de migração (efbundle), gerado do publish acima pelo mesmo script e
# com o mesmo dotnet-ef do CI local (.config/dotnet-tools.json). Não abre
# nenhuma base; o deploy aplica-o antes de a versão nova arrancar (migrate.sh).
RUN sh scripts/db/efbundle.sh /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13
# O Npgsql procura o libgssapi_krb5 ao arrancar; sem ele regista um erro em cada arranque.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# O entrypoint executa a app no Railway; o certificado de proteção de dados,
# quando configurado, é entregue exclusivamente pelas variáveis do serviço.
# O migrate.sh aplica o pacote de migração (/app/efbundle) no pré-deploy.
COPY entrypoint.sh migrate.sh ./
RUN chmod +x entrypoint.sh migrate.sh
# A porta: o entrypoint define ASPNETCORE_URLS com a PORT do Railway no
# arranque (8080 sem ela). Um ENV com ${PORT} resolvia-se no build, com a PORT
# vazia, e a app escutava na 80.
EXPOSE 8080
ENTRYPOINT ["./entrypoint.sh"]

# O PIN DO SDK (o mistério das rotas 2026-10-13: o Railway construia com a tag
# flutuante sdk:10.0 — uma versao que gerava a tabela de rotas das vistas torta,
# TODAS as rotas a servir /Admin/Definicoes; o SDK 10.0.401 da casa gera certo)
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY . .
ARG DMO_SOURCE_REVISION
ARG RAILWAY_GIT_COMMIT_SHA
ARG DMO_SOURCE_STATE=unverified
# o publish Release (o OutDir do container)
RUN dotnet publish src/DMO.Alpha.Web -c Release -o /app \
    -p:DmoRevision="${DMO_SOURCE_REVISION:-$RAILWAY_GIT_COMMIT_SHA}" \
    -p:DmoSourceState="$DMO_SOURCE_STATE"

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# O Npgsql procura o libgssapi_krb5 ao arrancar; sem ele regista um erro em cada arranque.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# O entrypoint executa a app no Railway; o certificado de proteção de dados,
# quando configurado, é entregue exclusivamente pelas variáveis do serviço.
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh
# O RAILWAY: a porta vem pela env PORT — a app escuta nela
ENV ASPNETCORE_URLS=http://+:${PORT}
EXPOSE 8080
ENTRYPOINT ["./entrypoint.sh"]

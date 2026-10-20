# Registo das alterações aos testes

Cada entrada explica uma mudança nos testes: o que entrou, o que mudou e o que saiu, e porquê.
É escrito por `python3 scripts/ci/guarda-testes.py registar` e só cresce no fim; nunca se apagam
nem se mudam entradas antigas. O CI local falha se um teste mudar sem uma entrada aqui.

Tipos:

- `teste-novo`: só testes novos (sempre permitido).
- `contagem-lista`: só se acertou um número ou uma lista; o que o teste verifica fica igual.
- `ok-do-diogo`: mudança a um teste existente aprovada pelo Diogo; a «Referência» diz onde.
- `inicio`: o ponto de partida da guarda.

A thread «Guardião dos testes» lê cada entrada nova e avisa o coordenador quando a explicação
não justifica a mudança (por exemplo um teste enfraquecido para passar).

## A-0001 · 2026-10-20 · inicio

Porquê: Ponto de partida da guarda dos testes no full: a impressão de todos os testes da main em 380fa2f (0.2.0-beta.5).
Unidades: 2010 (1711 testes)

Selo depois: `92404eb75bf09714`

## A-0002 · 2026-10-20 · contagem-lista

Porquê: a lista das migrações e das tabelas ganha a OperacoesRegistadas (20→21 migrações); teste novo: o mesmo envio duas vezes grava uma só vez
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.OperacoesRegistadas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.OperacoesRegistadas.cs :: PostgresMigrationIntegrationTests.OperacaoRegistada_OMesmoEnvioDuasVezes_GravaUmaSoVez`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `a189a8dba975d7ac`

## A-0003 · 2026-10-20 · teste-novo

Porquê: o caso contrário: dois envios com ids diferentes gravam os dois (um id fixo no Registar fazia todos colidirem)
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.OperacoesRegistadas.cs :: PostgresMigrationIntegrationTests.OperacaoRegistada_DoisEnviosDiferentes_GravamOsDois`

Selo depois: `64b4fb2cf44d98cf`

## A-0004 · 2026-10-20 · contagem-lista

Porquê: as listas das tabelas do modelo nos testes do Controlo, do Peso e dos Pegamentos ganham a OperacoesRegistadas
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPesoTables`

Selo depois: `117a82a26f9fd319`

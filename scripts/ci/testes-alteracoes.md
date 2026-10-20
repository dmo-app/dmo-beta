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

## A-0005 · 2026-10-20 · contagem-lista

Porquê: listas e contagens que enumeram colunas e chaves ganham os identificadores novos: migrações 21→22; colunas do Controlo, Peso, Pegamentos e BqMovementDeletions; chaves do BqRepairTraces 2→3 (com o nome da nova); chaves do Controlo e do Peso como lista exata em vez de uma só (a de JobOnId continua verificada); testes novos: cada autor como texto está na AutoriaMap com índice e chave SET NULL; o carimbo grava o utilizador da sessão (não o ADMIN, nem sem sessão) e segue o texto; eliminar o utilizador deixa o facto com o texto e um identificador sem utilizador é recusado
Novos: 11 · mudaram: 7 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AutorPorUtilizadorTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AutorPorUtilizadorTests.cs :: AutorPorUtilizadorTests.FactoAlterado_OUtilizadorSegueOTexto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AutorPorUtilizadorTests.cs :: AutorPorUtilizadorTests.OAdmin_FicaSoComOTexto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AutorPorUtilizadorTests.cs :: AutorPorUtilizadorTests.PesoNovo_GuardaOUtilizadorAoLadoDoNome`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AutorPorUtilizadorTests.cs :: AutorPorUtilizadorTests.SemSessao_OTextoFicaEOIdentificadorNao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/AutoriaMapTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/AutoriaMapTests.cs :: AutoriaMapTests.CadaAutorGuardadoComoTexto_EstaNaLista`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/AutoriaMapTests.cs :: AutoriaMapTests.CadaIdentificador_TemIndiceEChaveParaUsersComSetNull`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AutorPorUtilizador.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AutorPorUtilizador.cs :: PostgresMigrationIntegrationTests.AutorPorUtilizador_EliminarOUtilizador_OFactoFicaComOTexto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AutorPorUtilizador.cs :: PostgresMigrationIntegrationTests.AutorPorUtilizador_UmIdentificadorSemUtilizador_ERecusado`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.ControloEntity_ExactlyIdentityAndRelation_NoFactColumns`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.ControloEntity_UniqueJobOnIdIndex_RestrictForeignKey`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.PegamentosEntity_RegistroUnderControlo_ToleranceFrozen_ToolsInherited`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.PesoEntity_HasNoProductionIdentityColumns`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.cs :: NoRouteSettings.BqMovementAudit_HandlersWriteWhoAndWhen`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.cs :: NoRouteSettings.Migrate_AppliesBaseline_AndCreatesTheCanonicalSchema`

Selo depois: `feeb305b4944b80c`

## A-0006 · 2026-10-20 · contagem-lista

Porquê: 0.2.0-beta.8, Armazém (posições e reparadores de CM e MF): testes novos das posições (com o limite de 16 caracteres), dos reparadores (com o limite de 100), das páginas (pelo formulário que a página desenha, EnvioDoFormulario, e o texto dentro de cada zona) e da base. As listas das tabelas do modelo (MigrationFoundation, Peso, Pegamentos e Resumo) e a das migrações ganham ArmazemPosicoes, Reparadores e a migração ArmazemPosicoesEReparadores; o que cada teste verifica fica igual. No índice, um grupo «Armazém» novo (.Armazem.), sem mudar os outros grupos.
Novos: 26 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_AsTresFormasDoAccess_GuardaEmMaiusculas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_ForaDaRegra_NaoGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_NoLimiteDoTamanho_Grava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_OMesmoCodigoComOutrasMaiusculas_JaExiste`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Listar_MaisDoQueUmaPagina_Pagina`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Listar_PeloInicioDoCodigoEPeloEstado_PelaOrdemDoCodigo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.MudarAtiva_ArquivaEReativa_SemApagar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Criar_ComNomeEFamilias_Grava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Criar_ForaDaRegra_NaoGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Criar_NomeLongo_NaoGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Criar_NomeNoLimite_Grava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Criar_OMesmoNomeComOutrasMaiusculas_JaExiste`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Criar_UmNomeQueJaExisteNaOutraLista_Grava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Mudar_ForaDaRegra_NaoGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs :: ReparadoresTests.Mudar_NomeFamiliasEEstado_MudaSoOQueVem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Armazem.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Armazem.cs :: PostgresMigrationIntegrationTests.Armazem_ABaseRecusaCodigoForaDaRegraEReparadorSemFamilia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Armazem.cs :: PostgresMigrationIntegrationTests.Armazem_AMesmaPosicaoAoMesmoTempo_FicaUmaSo`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.AdminTemplates_MostraAChaveDoArmazem`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.ComAChave_AsDuasFuncoesEOMenu`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.Definicoes_CriarEMudarReparadores`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.Posicoes_CriarEArquivarNaLinha`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.SemAChave_NaoEntra`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `6daefbbf738cb5d4`

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

## A-0007 · 2026-10-20 · contagem-lista

Porquê: 0.2.0-beta.9, movimentos do Armazém: testes novos da Entrada, Saída e Corrigir localização (serviço, página e PostgreSQL, com a ferramenta que não existe nos três caminhos e duas Saídas e um Corrigir da mesma ferramenta ao mesmo tempo); as listas das tabelas (+ArmazemMovimentos, ArmazemOcupacoes, FerramentaLocalizacoes, FerramentaEstados, 42→46) e das migrações (+20261020160836_ArmazemMovimentos) e a descrição do grupo Armazém no testes.md
Novos: 49 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Corrigir_NaoEstaNoArmazemDepoisDaSucata_TiraDaPosicaoEVoltaAoFabrico`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Corrigir_ParaOndeJaEsta_NadaMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Corrigir_ParaOutraPosicao_LibertaAAnteriorEOcupaACerta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Corrigir_ParaUmaPosicaoQueNaoExiste_PedeParaCriar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.EliminarAFerramenta_PassaOsMovimentosParaARemovedELibertaAPosicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_DataNoFuturo_GravaEAvisaPassadosCincoMinutos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_DepoisDaSucata_GravaEAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_FerramentaFora_GravaMovimentoOcupacaoLocalizacaoEstadoEOperacao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_JaEstaNoArmazem_RecusaEDizAPosicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_OMesmoEnvioDuasVezes_GravaUmaVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_OMfDaMesmaReferenciaELoteDaCm_NaoAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_ObservacoesNoLimite_GravaAteAo200`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_PosicaoArquivada_GravaEAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_PosicaoComOutraFerramenta_GravaEAvisaQuemLaEsta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_PosicaoForaDaRegra_Recusa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_PosicaoQueNaoExiste_PedeParaCriarECriaComCriarERegistar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_SemOrigemOuSemEstado_Recusa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Ocupantes_SoAsFerramentasQueAindaLaEstao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Procurar_DizOndeEstaCadaFerramenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Procurar_PelaReferenciaLoteOuPosicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_ComDataAnteriorAoUltimoMovimento_GravaEAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_EmprestadoComAFabricaNoLimite_GravaAteAos60`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_ParaReparacaoDeUmaBq_UsaOsReparadoresDasBoquilhas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_ParaReparacaoDeUmaCm_UsaOsReparadoresDoArmazemECongelaONome`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_ParaSucataSemEstadoEParaProducaoComEstado_NaoMudaOEstado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_ParaSucata_GravaOEstadoQueAPessoaConfirmou`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_ReparadorDeOutraFamiliaArquivadoOuQueNaoExiste_Recusa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_SemDestinoSemReparadorOuForaDoArmazem_Recusa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Situacao_DepoisDeUmaSaida_PropoeAOrigemEOEstado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Situacao_NoArmazem_MostraAPosicaoOEstadoEOsUltimosMovimentos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Situacao_SemMovimentos_EstaNoFabricoSemPropostas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.UmaFerramentaQueNaoExiste_RecusaENaoGravaNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs :: PostgresMigrationIntegrationTests.Armazem_ABaseRecusaUmMovimentoComOsDoisReparadores`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs :: PostgresMigrationIntegrationTests.Armazem_AMesmaFerramentaEntraAoMesmoTempo_FicaNumaPosicaoSo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs :: PostgresMigrationIntegrationTests.Armazem_DuasSaidasDaMesmaFerramentaAoMesmoTempo_FicaUmaSo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs :: PostgresMigrationIntegrationTests.Armazem_OMesmoEnvioAoMesmoTempo_GravaUmMovimento`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs :: PostgresMigrationIntegrationTests.Armazem_ProcurarESaida_NoPostgreSql`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.CorrigirLocalizacao_NoOpcoesTiraDoArmazem`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Entrada_NumaPosicaoQueNaoExiste_PerguntaECriaComCriarERegistar`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Entrada_PeloFormulario_PoeNaPosicaoEPassaASaida`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.OMesmoEnvioDuasVezes_GravaUmaVez`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Posicoes_MostramQuemLaEstaOuLivre`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_MostraOndeEstaEAbreAFerramenta`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Saida_ODestinoEscolheOsCamposEDepoisAEntradaPropoe`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Saida_ParaReparacaoSemReparador_RecusaComOFormularioPreenchido`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.SemAChave_NaoEntra`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `562789e1f1b62a7f`

## A-0008 · 2026-10-21 · contagem-lista

Porquê: Mapa do Armazém (0.2.0-beta.10): 11 testes novos do serviço e da página; a única mudança é a descrição do grupo Armazém em scripts/ci/testes.md, que passa a nomear o Mapa (nenhum teste existente mudou).
Referência: full: Mapa do Armazém
Novos: 11 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OArmario_EOInicioDoCodigo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OFiltro_ContaSempreOArmarioInteiro`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OParDaMesmaReferenciaELote_NaoEMisturado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.SemArmario_AbreOPrimeiro_ComAOcupacao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.SemPosicoes_UmMapaVazio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.TodosOsArmarios_OuUmQueNaoExisteAbreOPrimeiro`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.AbreOPrimeiroArmario_ComAOcupacaoEOParNumaLinha`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.OutroArmarioEOFiltro_PorReparar`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.SemAChave_NaoEntra`
- mudou: `scripts/ci/testes.md`

Selo depois: `46c671b8fb08ab23`

## A-0009 · 2026-10-21 · contagem-lista

Porquê: Entrada como no Access (0.2.0-beta.11): testes novos da CM e do MF juntos (serviço, PostgreSQL e página) e das posições livres; a única mudança é a descrição do grupo Armazém em scripts/ci/testes.md (nenhum teste existente mudou).
Referência: full: Entrada como no Access
Novos: 15 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.ASituacao_MostraOParEOndeEsta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.ComOPar_ACmEOMfEntramNaMesmaPosicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.OMesmoEnvioDuasVezes_GravaOParUmaVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.OParJaNoArmazem_EntraSoAFerramentaEAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.SemOPar_EntraSoAFerramenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.SemParQueExista_EntraSoAFerramenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemPar.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemPar.cs :: PostgresMigrationIntegrationTests.Armazem_ACmEOMfEntramJuntos_NoPostgreSql`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemPar.cs :: PostgresMigrationIntegrationTests.Armazem_OParEOMfSozinhoAoMesmoTempo_OMfFicaNumaPosicaoSo`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ACm_MostraOMfEAsPosicoesLivres`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ComOParDesmarcado_EntraSoACm`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ComOParMarcado_ACmEOMfEntramJuntos`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.UmaFerramentaSemPar_NaoMostraAEscolha`
- mudou: `scripts/ci/testes.md`

Selo depois: `f4ead351c9fd9c97`

## A-0010 · 2026-10-21 · contagem-lista

Porquê: Histórico do Armazém e Localização na ficha (0.2.0-beta.12): testes novos do serviço, do PostgreSQL e das páginas (o Histórico abre vazio, só com os filtros; 00:30 em Lisboa conta como o dia seguinte); a única mudança é a descrição do grupo Armazém em scripts/ci/testes.md (nenhum teste existente mudou).
Referência: full PR #11
Novos: 16 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.AFerramentaOMovimentoEODestino`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.APaginacao_VinteECincoPorPagina`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.MeiaNoiteEMeiaEmLisboa_JaContaComoODiaSeguinte`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.OTexto_ProcuraAReferenciaOLoteOuAPosicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.OsDias_EmHoraDeLisboa_InclusiveNasPontas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.SemFiltro_DoMaisRecenteParaOMaisAntigo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.UmaFerramentaEliminada_FicaComAReferenciaGuardada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemHistorico.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemHistorico.cs :: PostgresMigrationIntegrationTests.Armazem_OHistoricoFiltraNoPostgreSql`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.AFicha_MostraALocalizacaoEOHistorico_SoAQuemTemOArmazem`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.AbreVazio_SoComOsFiltrosSemBotao`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.ComUmFiltro_ListaDoMaisRecente`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.OsFiltros_TextoDestinoEDias`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.SemAChave_NaoEntra`
- mudou: `scripts/ci/testes.md`

Selo depois: `86a77643df98594c`

## A-0011 · 2026-10-21 · ok-do-diogo

Porquê: Próximos Job On (testes novos, com as localizações A reparar, Emprestado e Sucata) e a regra da posição do Diogo (21/10, AR-03): 4 algarismos, 1.º corredor, 2.º e 3.º estante, 4.º prateleira; o mapa agrupa por corredor e estante; CM+MF juntos é raro e vem desmarcado (AR-02). Os testes das posições, do mapa e do par mudam com a regra.
Referência: Diogo 21/10 cmsg_01A27e9ozcpoh4jdPRsuJbFy9ZPTyiKaQTH6T6oso1YDLh
Novos: 14 · mudaram: 25 · saíram: 4

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.APosicao_CorredorEstanteEPrateleira`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_OMesmoCodigoComEspacos_JaExiste`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_QuatroAlgarismos_Grava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.AsProducoesDoPeriodo_PelaDataDeInicio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.CadaFerramenta_OndeEstaEOQueFalta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.ForaDoArmazemEDoFabrico_NaoDisponivel`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.SemProducoes_ListaVazia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemProximos.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemProximos.cs :: PostgresMigrationIntegrationTests.Armazem_OsProximosJobOn_NoPostgreSql`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: ArmazemProximosPaginaTests.MostraAsProducoesDoPeriodo_ComOQueFaltaTirar`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: ArmazemProximosPaginaTests.OPeriodoMaisLongo_MostraMais`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: ArmazemProximosPaginaTests.SemAChave_NaoEntra`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OFiltro_ContaSempreOArmarioInteiro`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OParDaMesmaReferenciaELote_NaoEMisturado`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.SemArmario_AbreOPrimeiro_ComAOcupacao`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.TodosOsArmarios_OuUmQueNaoExisteAbreOPrimeiro`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Corrigir_ParaUmaPosicaoQueNaoExiste_PedeParaCriar`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Entrada_PosicaoQueNaoExiste_PedeParaCriarECriaComCriarERegistar`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_ForaDaRegra_NaoGrava`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Listar_PeloInicioDoCodigoEPeloEstado_PelaOrdemDoCodigo`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Armazem.cs :: PostgresMigrationIntegrationTests.Armazem_AMesmaPosicaoAoMesmoTempo_FicaUmaSo`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ACm_MostraOMfEAsPosicoesLivres`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ComOParDesmarcado_EntraSoACm`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ComOParMarcado_ACmEOMfEntramJuntos`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.ComUmFiltro_ListaDoMaisRecente`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.OsFiltros_TextoDestinoEDias`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.AbreOPrimeiroArmario_ComAOcupacaoEOParNumaLinha`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.OutroArmarioEOFiltro_PorReparar`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Entrada_NumaPosicaoQueNaoExiste_PerguntaECriaComCriarERegistar`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.Posicoes_CriarEArquivarNaLinha`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OArmario_EOInicioDoCodigo`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_AsTresFormasDoAccess_GuardaEmMaiusculas`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_NoLimiteDoTamanho_Grava`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_OMesmoCodigoComOutrasMaiusculas_JaExiste`

Selo depois: `961d0a4bceccca49`

## A-0012 · 2026-10-21 · ok-do-diogo

Porquê: O «Enviar» deixa de mandar pelo servidor (SMTP) e passa a devolver um .eml que o Outlook do PC abre pronto a enviar. O teste do envio passa a verificar o .eml (lista, assunto, corpo, PDF em anexo) e o registo; os testes «sem conta no servidor» e «servidor recusa» saem porque o servidor já não envia; entram «sem lista/sem modelo» e «sem Peso»; o teste do aviso sem modelos deixa de procurar o aviso de servidor. Testes novos do .eml (EmailRascunhoTests).
Referência: Diogo 21/10/2026 05:51, thread do envio por email (cmsg_01A27e9ozcpoh4jdPRsuJbFyQebu37V7cHh6GdpvKWjpqg): «mas é enviado pelo outlook do pc da empresa» + cartão 06:26: «Abrir no Outlook» (cmsg_01A27e9ozcpoh4jdPRsuJbFyAabj7LLRnPvoShkVDC971k); a mesma mudança da beta.319 (registo A-0002 da beta)
Novos: 6 · mudaram: 2 · saíram: 3

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EmailRascunhoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EmailRascunhoTests.cs :: EmailRascunhoTests.Gerar_AbreComoMensagemNova_SemFrom_QuandoOModeloNaoTemRemetente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EmailRascunhoTests.cs :: EmailRascunhoTests.Gerar_AcentosNoAssuntoENoAnexo_VaoEmUtf8_EUmaLinhaNovaNaoPartiOCabecalho`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_DevolveOEmailParaOOutlook_ComALista_OAssunto_OCorpo_EOPdf_EFicaORegisto`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_SemListaOuSemModelo_VoltaAoResumo_SemEmailNemRegisto_EDizPorque`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_SemPeso_NaoPreparaEmail`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_SoApareceComPeso_ESemModelosAvisaComLinkParaAsDefinicoes`
- saiu: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_MandaOPdfDoPesoALista_ComOAssuntoEOCorpoDoModelo_EFicaORegisto`
- saiu: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_QuandoOServidorDeEmailRecusa_NaoFicaRegisto_EDizQueNaoSaiu`
- saiu: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_SemContaDeEmailNoServidor_NaoEnviaNemRegista_EDizPorque`

Selo depois: `92082ab9a42bc24f`

## A-0013 · 2026-10-21 · teste-novo

Porquê: Teste novo: modo escuro redefine os tokens; nenhuma folha nem página (style) pinta fundos a branco solto, fora do @media print; cabeçalho com o botão (Diogo, 21/10/2026); a mesma mudança da beta.320 (registo A-0003 da beta)
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs :: UiStyleRuleTests.DarkMode_RedefinesTheTokens_AndNoSheetPaintsLooseWhite`

Selo depois: `8fbdafa2217bfa4e`

## A-0014 · 2026-10-21 · ok-do-diogo

Porquê: Job On: onde está cada ferramenta no Armazém e aviso quando não está registada ou não tem estado (Diogo 21/10, formulário, FER-E-03: «Warn. Also a tool that isn't registered in Armazém and no state»). Dois testes da ficha passam a contar com este aviso novo; testes novos dos factos (incluindo a ferramenta com estado nunca registada no Armazém) e da ficha.
Referência: Diogo 21/10 perguntas-full.md Q7 (FER-E-03)
Novos: 3 · mudaram: 4 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsArmazemTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsArmazemTests.cs :: JobOnToolFactsArmazemTests.OndeEstaOEstadoEOAviso`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_OndeEstaNoArmazem_EAvisoSemRegisto`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_AbremAFichaDaFerramenta_SemAvisoDeDadosEmFalta`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ToolFactsEndpoint_AvisosLevamLinkParaOndeSeResolvem`

Selo depois: `18fc41aa14b7c57e`

## A-0015 · 2026-10-21 · contagem-lista

Porquê: Utilização pelo SAP: a lista do SAP (o HTML da ZPPF e o CSV do script da ZPPG) acrescenta uma leitura de utilização a cada ferramenta pelo nome técnico e o lote e fecha o lembrete dela; a Entrada no Armazém vinda da Produção abre o lembrete (blueprint armazem/OVERVIEW §2) e só as ferramentas com ele aberto vão nos códigos a pedir ao SAP; a ficha mostra a utilização. Os testes do esquema e das migrações só acrescentam as duas tabelas novas e a migração à lista.
Novos: 23 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.UsoSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.UsoSap.cs :: PostgresMigrationIntegrationTests.UsoSap_ImportarFechaOLembreteEUmSoAberto_NoPostgreSql`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.AEntradaVindaDaProducao_AbreOLembreteDaFerramentaEDoPar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.ALeitura_FechaOLembreteMesmoComOMesmoValor`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.ALinhaDoSap_EDaFerramentaComOMesmoNomeTecnicoELote`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.AListaHtmlDoSap_DaOCodigoEOUsoDeCadaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.EliminarAFerramenta_LevaAUtilizacaoEOLembrete`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.Importar_AcrescentaUmaLeituraPorFerramentaEDizOQueNaoTemFerramenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OCodigoAPedirAoSap_EONomeTecnicoComOLoteEmDoisAlgarismos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OCsvDoScript_LeOPercDasLinhasEncontradasEDeixaAsOutras`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OMesmoValorOutraVez_NaoAcrescentaNadaEUmValorNovoAcrescenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OQueNaoEUmNumero_FicaDeFora`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OsCodigos_SaoSoOsDasFerramentasComOLembreteAberto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OsNumerosDoSap_EmFormatoPortugues`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.Pedir_AbreUmLembreteSoUmaVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.UmFicheiroQueNaoEALista_NaoGravaNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.UmFicheiroQueNaoEALista_NaoTemCabecalho`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.AListaDoSap_GravaOUsoQueAFichaDaFerramentaMostra`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.OsCodigosAPedirAoSap_SaoOsPedidosNaFicha`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.SemOModuloFerramentas_ALeituraNaoGravaNada`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.UmFicheiroQueNaoEALista_DizQueNadaFoiGravado`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `c77826e383efba3c`

## A-0016 · 2026-10-21 · contagem-lista

Porquê: Pedido do Guardião ao PR #17: os 3 testes de esquema mudam só de nome (ExactlyThirtyThreeTables → ExactlyTheListedTables), porque a lista de tabelas cresce a cada módulo e o número no nome deixava de bater certo; o corpo e as tabelas verificadas não mudam. Nos testes da utilização pelo SAP, o «Pedir a utilização ao SAP» passa a enviar o formulário que a ficha desenha (EnvioDoFormulario, regra 9) e os textos verificam-se cada um na sua zona (#ferrUtilizacao, #ferrUtilizacaoPedida, #ferrUtilizacaoLembrete, #ferrOpcoes) em vez da página inteira.
Novos: 3 · mudaram: 3 · saíram: 3

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.AListaDoSap_GravaOUsoQueAFichaDaFerramentaMostra`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.OsCodigosAPedirAoSap_SaoOsPedidosNaFicha`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingControlos`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPegamentosTables`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyThirtyThreeTables_IncludingPesoTables`

Selo depois: `1a312ffd2cc64ced`

## A-0017 · 2026-10-21 · contagem-lista

Porquê: Folha completa do Job On (tabelas JobOnFolhas e JobOnFolhaCalLinhas, sem FO, com QTD Stock e QTD Máq., Diogo 21/10): testes novos (com os três casos nos limites da CAL e das Notas gerais, e as recusas lidas na zona do aviso); os testes das listas de tabelas e de migrações ganham as duas tabelas e a migração nova.
Referência: full: folha do Job On
Novos: 29 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFolha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFolha.cs :: PostgresMigrationIntegrationTests.JobOnFolha_GravarELerDeNovo_EApagaEmCascataComOJobOn`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFolha.cs :: PostgresMigrationIntegrationTests.JobOnFolha_UmaFolhaPorJobOn_ABaseRecusaASegunda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.CampoCurto_NoLimiteCabe_LogoAcimaERecusado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Catalogo_CadaChaveEUnica_CurtaETemColunaNaFolha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Catalogo_OsParesSaoOsDoProprietario_ECadaFamiliaAparecaUmaVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Catalogo_OsTecnicosMenosUsadosFicamNoMaisDetalhes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Catalogo_SemFo_EComQtdStockEQtdMaqEscritasEmCadaFamiliaQueAsTem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.ChaveRecusada_DaORotuloComAFamilia_EOMaximo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Criar_ComUmTextoLongoDemais_ERecusado_ENadaFicaGravado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Criar_GravaAFolhaEACal_ELeSeDeNovo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Criar_SemNadaEscrito_NaoCriaLinhaDaFolha_ELeSeVazia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Editar_ComAMesmaFolha_NaoRegistaNada_ESemFolhaNoEnvioAFolhaFica`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Editar_ComUmTextoLongoDemais_ERecusado_ENadaMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Editar_GravaOQueMudou_ERegistaUmaLinhaComOsRotulos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.EliminarJobOn_LevaAFolhaEACal`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.LinhaCal_AQtdMaqTemOMesmoMaximo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.LinhasCal_AsPorOmissaoPorEscreverNaoFicam_UmaSoComAQtdMaqFica`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Normalizar_TiraAsPontas_CrlfPassaALf_EOVazioPassaANull`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Nota_ContaCadaQuebraDeLinhaComoUmCaracter`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.NotasGerais_NoLimiteCabem_LogoAcimaSaoRecusadas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs :: JobOnFolhaTests.Resumo_ListaOQueMudou_EContaNoFimOQueNaoCabe`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Criar_GravaAFolhaNaMesmaEscritaDoJobOn`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Editar_MostraAsFamiliasAosPares_ComAQtdStockEAQtdMaqEmCadaFamiliaENaCal`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Guardar_GravaAFolhaDoFormulario_EOEditarVoltaAMostrarOsValores`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.TextoLongoDemais_NoCriar_NaoCriaOJobOn_EAFolhaVoltaComOQueSeEscreveu`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.TextoLongoDemais_NoEditar_ERecusado_EAFolhaVoltaComOQueSeEscreveu`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `84cdb88d67550c90`

## A-0018 · 2026-10-21 · contagem-lista

Porquê: Fabrico pelo SAP e produções na ficha (Diogo 21/10/2026: ligar cada Job On ao que produziu; a ligação é pelas datas de início e fim com a CM, o MF e a BQ). Testes novos: a leitura do relatório «6- Manufacturing Selected» (cópia do ficheiro que o Diogo mandou), a ligação ao Job On pela referência, máquina e datas (com os limites dos dois lados pedidos pelo Guardião, e um Job On sem fim, ainda na máquina, apanha as ordens desde o início), ler duas vezes atualiza a mesma linha (memória e PostgreSQL com o índice único), as produções de uma CM (também trocada) e de uma BQ com a paginação, e na página o link do Opções da ficha, a tabela com as peças, o traço sem fabrico e a ferramenta sem Job On. Mudanças só de lista: a tabela nova SapFabricos nas listas de tabelas e a migração 20261021083819_SapFabrico na lista das migrações; a linha do grupo Ferramentas em testes.md; o ajudante de zona dos testes da utilização passa a servir qualquer elemento pelo id (section e p), sem mudar o que os testes verificam.
Novos: 14 · mudaram: 7 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FabricoSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FabricoSap.cs :: PostgresMigrationIntegrationTests.FabricoSap_LerDuasVezesUmaSoLinha_NoPostgreSql`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ABoquilhaTambemTemAsProducoes_EAListaPagina`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ALinhaDoSap_LigaSeAoJobOnPelaReferenciaMaquinaEDatas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.AsProducoesDaFerramenta_SaoOsJobOnDelaComOQueOSapDiz`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.LerOMesmoRelatorioOutraVez_AtualizaAsMesmasLinhas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ORelatorioDoSap_DaOMaterialALinhaAsDatasEAProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.SemDataDeInicio_NaoSeLiga`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.UmFicheiroSemOCabecalhoDoFabrico_NaoEORelatorio`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.ORelatorioDeFabrico_MostraOQueCadaJobOnDaFerramentaProduziu`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.SemORelatorioDeFabrico_AProducaoFicaComTraco`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.UmaFerramentaSemJobOn_DizQueNaoEsteveEmNenhum`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: (resto do ficheiro)`

Selo depois: `70203b7076adc35b`

## A-0019 · 2026-10-21 · contagem-lista

Porquê: Job On peça 3: 3 testes novos da ficha por famílias; o resto do ficheiro só ganha um using e funções de ajuda novas; testes.md descreve a folha em leitura
Referência: Job On peça 3, ficha por famílias
Novos: 3 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Ficha_ACalMostraAsLinhasGravadas_ComAQtdMaq`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Ficha_MostraSoAsFamiliasEOsCamposEscritos_ComAQtdStockEAQtdMaq`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Ficha_SemNadaEscritoNaFolha_DizSoIssoComOEditar`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: (resto do ficheiro)`

Selo depois: `cba617c94d072a65`

## A-0020 · 2026-10-21 · contagem-lista

Porquê: Quadro de detalhes da utilização e peso do SAP no Peso (Diogo 21/10/2026, 08:53–08:57: «% uso, mas um box com o que fabricou naquela produção, o delta e o total»; «o Peso usa a data do Job On escolhido com a referência da CM para ir buscar o peso; média com base nos dias e peso, e a moda, o peso que se repete mais»). Testes novos: o delta da utilização por produção (leitura antes do início e depois do fim) e o total de todas as produções, não só da página; a média pelos dias e a moda (empate: a mais recente; dias contados dentro do Job On); o peso de cada Job On pela referência, máquina e datas; na página, o «Detalhes» da Utilização abre o quadro com o delta e o total, e no Peso a média e a moda do SAP com o campo preenchido com a média, ou o aviso sem ordens do SAP. Mudanças sem enfraquecer: a leitura do relatório passa a dar também o Avg weight e os Prod. Days (os valores esperados ganham esses dois campos); as produções vêm agora dentro de FerramentaProducoes (.Pagina), com as mesmas verificações; o teste do PostgreSQL verifica também o peso e os dias; a migração nova SapFabricoPeso na lista; a classe dos testes da folha do Peso passa a partial e limpa também a SapFabricos entre testes; testes.md com as linhas do Peso e das Ferramentas. Limite do início (Guardião): a leitura no próprio dia do início conta como «antes» do delta; a ordem sem peso dentro do Job On fica de fora da média e da moda.
Novos: 9 · mudaram: 8 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ODelta_EQuantoAUtilizacaoSubiuEmCadaProducao_EOTotalSomaTodas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.OPesoDoSap_EAMediaPelosDiasEAModaOPesoComMaisDias`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.OPesoDosJobOns_VemDasOrdensDaMesmaReferenciaMaquinaEDatas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.OTotal_ContaTodasAsProducoes_NaoSoAPagina`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.OsDiasDeCadaOrdem_SaoOsQueFicamDentroDoJobOn_EAModaEmpatadaEAMaisRecente`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.ODetalhesDaUtilizacao_MostraODeltaDeCadaProducaoEOTotal`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FabricoSap.cs :: PostgresMigrationIntegrationTests.FabricoSap_LerDuasVezesUmaSoLinha_NoPostgreSql`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ABoquilhaTambemTemAsProducoes_EAListaPagina`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.AsProducoesDaFerramenta_SaoOsJobOnDelaComOQueOSapDiz`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ORelatorioDoSap_DaOMaterialALinhaAsDatasEAProducao`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: (resto do ficheiro)`

Selo depois: `f3c0d6dba4892f9b`

## A-0021 · 2026-10-21 · contagem-lista

Porquê: Job On peça 4: testes novos do Duplicar com a folha (cópia da folha e da CAL, marcas «Confirmar», datas livres, sem contadores); testes.md descreve o Duplicar
Referência: Job On peça 4, Duplicar
Novos: 5 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs :: JobOnDuplicarFolhaTests.Duplicar_MarcaConfirmarNaMaquinaNasFerramentasENasUtilizacoesCopiadas`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs :: JobOnDuplicarFolhaTests.Duplicar_SemFolhaNaOrigem_AbreAsMedidasPorOmissao_SemMarcasNaFolha`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs :: JobOnDuplicarFolhaTests.Duplicar_TrazAFolhaDaOrigem_EOGuardarGravaANaProducaoNova`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs :: JobOnDuplicarFolhaTests.NovoEEditar_NaoTemMarcasDoDuplicar`
- mudou: `scripts/ci/testes.md`

Selo depois: `b8facb048c70d2d2`

## A-0022 · 2026-10-21 · contagem-lista

Porquê: Reparação Interna peça 1: testes novos do registo na máquina (com o Eliminar do Job On com reparações e os limites 39/40/41 e 23/24); listas de tabelas e migrações acrescentadas
Referência: Reparação Interna peça 1
Novos: 35 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInterna.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInterna.cs :: PostgresMigrationIntegrationTests.ReparacaoInterna_ABaseRecusaContextoErradoENumeroComEspacos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInterna.cs :: PostgresMigrationIntegrationTests.ReparacaoInterna_EliminarOJobOn_LevaAsReparacoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInterna.cs :: PostgresMigrationIntegrationTests.ReparacaoInterna_OMesmoEnvioAoMesmoTempo_GravaUmaReparacao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.AHoraDeOutroModulo_NaoMudaADaReparacaoInterna`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.AHoraDeViragemDaReparacaoInterna_GravaSeAParte`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.ALista_DestaMaquinaEProducao_DaMaisRecente_EmPaginasDe25`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.AntesDaViragem_AProducaoDeOntem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.DuasProducoes_AsDuasCandidatas_ADeMaiorJobOnIdEscolhida`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.DuasProducoes_GravaNaEscolhida`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.EliminarOJobOn_ContaELevaAsReparacoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.ODiaOperacional_ViraAHoraDaReparacaoInterna`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.OMesmoEnvioDuasVezes_GravaUmaVez_OMesmoNumeroOutraVezGravaOutra`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.ONumero_TemAte40Caracteres_SemContarOsEspacosDasPontas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.OQueFaltaOuMudou_Recusa_ComONumeroEscrito`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.Registar_GravaALinhaCurta_ComOCmIdDoMomento`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.Registar_NaoMexeEmMaisNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.Registar_UmMf_GuardaSoOMfId`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.SemNumero_Recusa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.SemProducaoDatada_NaoHaAQueLigar_ENadaSeGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.UmUtilizadorEliminado_FicaSemQuem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.UmaProducao_ADeHojeSemFim_AsSemDatasNaoContam`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.AdminTemplatesECanal_AChaveEAHoraDaReparacaoInterna`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.DuasProducoes_AvisaComAsCandidatas_ADeMaiorIdEscolhida`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EliminarAProducao_AvisaQueSePerdemAsReparacoes`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EliminarJobOnComReparacoes_AJanelaDizQuantasSePerdem`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EscolherAMaquinaEGravar_SoMudamAsZonas`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EscreverONumeroEEnter_Grava_OCampoVoltaVazio_EALinhaApareceNoTopo`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.OMesmoEnvioDuasVezes_GravaUmaVez`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.SemAChave_NaoEntra`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.SemProducao_AvisaENaoHaFormulario`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.UmaRecusa_VoltaComONumeroEscrito_ENadaSeGrava`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `562b45402552bdbd`

## A-0023 · 2026-10-21 · contagem-lista

Porquê: Reparação Interna peça 2: testes novos de corrigir e anular com rasto; listas de tabelas e migrações acrescentadas
Referência: Reparação Interna peça 2
Novos: 25 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaRasto.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaRasto.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaRasto_AnularAoMesmoTempo_AnulaUmaVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaRasto.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaRasto_EliminarOJobOn_LevaAsReparacoesEORasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaRasto.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaRasto_SoSeAcrescenta_EABaseRecusaUmaAcaoErrada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Anular_OutraPessoa_SaiDaListaEDasContagens_FicaNaBase_ComRasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.AsAnuladas_NaoContamNasPaginas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Corrigir_ONumeroTemAte40Caracteres`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Corrigir_OutraPessoaComOModulo_MudaSoONumero_EDeixaRasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Corrigir_ParaOMesmoNumero_NadaMuda_SemRasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Corrigir_SemNumero_Recusa_ENadaMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.DuasCorrecoes_DoisRastos_PelaOrdem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.EliminarOJobOn_ContaTodas_ELevaAsReparacoesEORasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.OMesmoEnvioDuasVezes_AplicaUmaVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.OMotivo_EOpcional_ETemAte200Caracteres`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.SemMotivo_ORastoFicaSemMotivo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.UmaAnulada_NaoSeCorrigeNemSeAnulaOutraVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.UmaQueJaNaoExiste_NadaMuda`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ONumeroAbreACorrecao_EOXisAAnulacao`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.AnuladaEntretanto_CorrigirOuAnularAvisa_ENadaMuda`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_Anula_ALinhaSai_EFicaNaBase`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_CorrigeONumero_ComRasto_EOMesmoEnvioAplicaUmaVez`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.SemAChave_NaoCorrigeNemAnula`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.UmNumeroLongo_Recusa_ENadaMuda`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `180a9c2a51aba19d`

## A-0024 · 2026-10-21 · contagem-lista

Porquê: Reparação Interna peça 3: testes novos da consulta com filtros
Referência: Reparação Interna peça 3
Novos: 21 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaConsulta.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaConsulta.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaConsulta_OsFiltrosTraduzemSeNaBase`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.ALinha_AProducaoAFerramentaEQuem_NuncaONomeTecnico`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.AsAnuladas_FicamDeFora`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.CadaFiltroSozinho`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.DaMaisRecenteParaAMaisAntiga`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.EmPaginasDe25`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.OFiltroQuem_SoOsUtilizadoresQueRegistaram_PorNome`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.OsDias_DeEAteInclusive`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.OsFiltrosJuntos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.SemFiltro_NaoLeNada`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.AProducaoEmDoisPassos_AReferenciaDepoisAsUltimas5`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.AbreVazia_SoComOsFiltros`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.AsPessoasEOsDias_Filtram`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.ComOJobOn_AProducaoAbreAFicha`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.DepoisDeFiltrar_AsReparacoes_SemBotoes_ENuncaONomeTecnico`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.Filtrar_SoMudamAsZonas`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.OsDoisSeparadores_RegistarEConsultar`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.SemAChave_NaoEntra`
- mudou: `scripts/ci/testes.md`

Selo depois: `bfbd0316cf5dd1e3`

## A-0025 · 2026-10-21 · ok-do-diogo

Porquê: Pedido do Diogo na thread «SAP tool usage extraction» (21/10/2026): 11:36 (cmsg_01A27e9ozcpoh4jdPRsuJbFyFip85u8znDx8ca5TiHdnTM) «preferi ter um box Média Moda, selecionar um ou outro ou ambos» em vez dos botões do Peso; 11:40 (cmsg_01A27e9ozcpoh4jdPRsuJbFy9jXeAdffxYoGsStADTMNpe e cmsg_01A27e9ozcpoh4jdPRsuJbFyNXpJa2APZVQ9z76e9jPhub) na ficha só o % de uso, que abre uma janela pequena com produção, produzido e delta, e cada produção leva à ficha do Job On; 12:13–12:14 (cmsg_01A27e9ozcpoh4jdPRsuJbFyP4u42KywEoZTHmxji2yL7B, cmsg_01A27e9ozcpoh4jdPRsuJbFy3SaS21QndkJ92y9Ve6eTWr) o produzido é a produção bruta: «o uso é calculado pelo total produzido e não pelo que consideram boas». Os 5 testes mudados verificam agora a caixa (Média escolhida com a diferença, Moda por escolher, sem botões) e a janela (botão do %, fechada com a página ou aberta com ?jobon=1, o produzido bruto, o link para o Job On e o total); nada foi enfraquecido.
Referência: thread «SAP tool usage extraction», mensagens do Diogo de 21/10 11:36–12:14 (ids no porquê)
Novos: 0 · mudaram: 5 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.ODetalhesDaUtilizacao_MostraODeltaDeCadaProducaoEOTotal`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.ORelatorioDeFabrico_MostraOQueCadaJobOnDaFerramentaProduziu`

Selo depois: `79ea765de9d9c06f`

## A-0026 · 2026-10-21 · ok-do-diogo

Porquê: Na página (pedido do Guardião): a procura do Armazém mostra o % na coluna do uso da CM e «—» na BQ sem leitura; o cartão da ferramenta do Job On mostra o uso da última leitura, só na que tem leitura; a folha do Peso não tem «(SAP)» e a caixa da produção não mostra «SAP» em nenhum texto à vista. A coluna das máquinas da procura chama-se «Linhas» (regra nova do Diogo: máquina passa a linha).
Referência: thread «SAP tool usage extraction», Diogo 21/10 11:50 (cmsg_01A27e9ozcpoh4jdPRsuJbFyUcQqiZ3ap8bvwmmevMak7b) e 12:32 (cmsg_01A27e9ozcpoh4jdPRsuJbFy4SUt54ApUckttfb8oEXeQA, cmsg_01A27e9ozcpoh4jdPRsuJbFyBjqBzuCq63NgbNxTPh4b9Z); regra «máquina passa a linha», Diogo 21/10 12:53 (cmsg_01A27e9ozcpoh4jdPRsuJbFyQgXrdUMgZDb69cYsG5HvnR)
Novos: 4 · mudaram: 5 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Procurar_DaOEstadoAsMaquinasEOUltimoUso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsArmazemTests.cs :: JobOnToolFactsArmazemTests.OUsoEAUltimaLeitura_ESemLeituraFicaVazio`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_MostraOUsoDaUltimaLeitura_ESemLeituraUmTraco`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_MostramOUsoDaUltimaLeitura`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PropoeAMaisRecente_GuardaOPesoDoSap_EMostraODelta`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.SemORelatorioDeFabrico_AProducaoFicaComTraco`

Selo depois: `69e5ccde73ff92d4`

## A-0027 · 2026-10-21 · ok-do-diogo

Porquê: Diogo: «registar na ferramenta», «a página da Ferramenta é onde alteramos», «o armazém é onde se arruma a ferramenta», «a ferramenta tem um módulo próprio». O aviso do Job On (sem registo ou sem estado) leva agora a «Registar na ferramenta», que abre a página da Ferramenta, em vez dos Movimentos do Armazém. O teste verifica o link novo; o utilizador do teste passa a ter a chave das Ferramentas (o link só aparece a quem a tem), em vez da do Armazém.
Referência: thread «SAP tool usage extraction», Diogo 21/10 13:33–13:34 (cmsg_01A27e9ozcpoh4jdPRsuJbFyVpyBiQsLUpqraRKppKhzXH, cmsg_01A27e9ozcpoh4jdPRsuJbFyFzgQigbdsPoM5FHfKYDLF4, cmsg_01A27e9ozcpoh4jdPRsuJbFyE5K71SLxmFKjg9AtbZP6M4, cmsg_01A27e9ozcpoh4jdPRsuJbFy8exde2FNuZys31UC5nDToF)
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_OndeEstaNoArmazem_EAvisoSemRegisto`

Selo depois: `f457af5ff3b5ecf2`

## A-0028 · 2026-10-21 · teste-novo

Porquê: Cartão SAP com LEIA-ME nas Definições do Admin (Diogo 21/10 14:11–14:12); textos verificados dentro do cartão e utilizador normal recusado
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Definicoes_Admin_TemOCartaoSap_ComOLeiaMe`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Definicoes_UtilizadorNormal_NaoVeOCartaoSap`

Selo depois: `fd6d3c8393596692`

## A-0029 · 2026-10-22 · ok-do-diogo

Porquê: O LEIA-ME do SAP sai do Setup do Admin para a página de ajuda das Ferramentas, aberta pela ajuda do cartão (Diogo 22/10 06:57)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyBXTH55ZaH6ZdrV4jUrehGd
Novos: 3 · mudaram: 0 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Definicoes_Admin_NaoTemOCartaoSap`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_AAjudaDoSapAbreAPaginaDeAjuda`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Definicoes_Admin_TemOCartaoSap_ComOLeiaMe`

Selo depois: `484f3ba6e423e1ab`

## A-0030 · 2026-10-22 · ok-do-diogo

Porquê: Reparação Interna peça 4: cartões das linhas, outra CM/MF por referência e lote, registo sem Job On, associar no Consultar, sem recusas; «Máquina» passa a «Linha» no Consultar.
Referência: Diogo 21/10 12:08-12:10 (cmsg_01A27e9ozcpoh4jdPRsuJbFyMwHpG4RYgsH5jDFxbXYaTE, cmsg_01A27e9ozcpoh4jdPRsuJbFy18yDSarsjuMuJxE7NczQNn); máquina passa a linha 12:53 (cmsg_01A27e9ozcpoh4jdPRsuJbFyQgXrdUMgZDb69cYsG5HvnR)
Novos: 30 · mudaram: 13 · saíram: 2

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta_ABaseAceitaAsDuasFormas_ERecusaAMistura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta_SemJobOn_AssociarEORasto_NaBaseReal`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.APaginaMostrouUmaProducaoQueJaNaoConta_Mudou`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.AReferencia_TemAte40_SemContarOsEspacosDasPontas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.Associar_AFerramentaDoTipo_GravaQuemEQuando_EORasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.Associar_OMesmoEnvioDuasVezes_AssociaUmaVez`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.Associar_OQueNaoServe_Recusa_ENadaMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.Consulta_SoPorAssociar_ComAsFerramentasQueServem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.EliminarAFerramentaAssociada_PassaParaAREMOVED_EORastoFica`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.OLote_TemAte12_SemContarOsEspacosDasPontas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.OMesmoEnvioDuasVezes_GravaUmaVez_EVoltaComAEscrita`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.OutraCm_GravaAReferenciaEOLoteEscritos_SemContexto_ComOJobOn`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.OutraSemAReferenciaOuOLote_Recusa_ComONumeroEscrito_ENadaSeGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.SemOutra_ARegraDoJobOnFicaIgual_AReferenciaEscritaNaoConta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs :: ReparacoesInternasOutraFerramentaTests.SemProducao_GravaSemJobOn_ComAEscrita_EAListaDaMaquinaNoDia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.SemProducaoDatada_SemAFerramentaEscrita_NadaSeGrava`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.AFerramentaMostrada_LevaAFicha_SoComAsFerramentas_AEscritaFicaTexto`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_AJanelaAssociar_GravaUmaVez_EALinhaFicaComAFerramenta`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_SemFerramentaQueSirva_AJanelaAvisaComOLinkParaRegistar`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.CorrigirParaOMesmoNumero_NadaMuda_ESemAviso`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.GravarComOutraCm_AvisaQueFaltaAssociar_EOCampoVoltaVazioEDesmarcado`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.OCartaoDoNumero_TemAMarcaOutraCm_EOsCamposCurtos_ODaProducaoFicaIgual`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.OsCartoes_BAntesDeC_ATabelaMinimizadaComOUltimo_EditarAbre`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.OutraSemAReferencia_RecusaNoCampo_ENadaSeGrava`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.SemProducao_GravaSemJobOn_AvisaEATabelaMostraAsDoDia`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.SemProducao_AvisaEOFormularioPedeAReferenciaEOLote`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.DuasProducoes_GravaNaEscolhida`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.DepoisDeFiltrar_AsReparacoes_SemBotoes_ENuncaONomeTecnico`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ONumeroAbreACorrecao_EOXisAAnulacao`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.DuasProducoes_AvisaComAsCandidatas_ADeMaiorIdEscolhida`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EscolherAMaquinaEGravar_SoMudamAsZonas`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EscreverONumeroEEnter_Grava_OCampoVoltaVazio_EALinhaApareceNoTopo`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.OMesmoEnvioDuasVezes_GravaUmaVez`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.UmaRecusa_VoltaComONumeroEscrito_ENadaSeGrava`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.SemProducaoDatada_NaoHaAQueLigar_ENadaSeGrava`
- saiu: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.SemProducao_AvisaENaoHaFormulario`

Selo depois: `e1eaa30f9e45e768`

## A-0033 · 2026-10-22 · ok-do-diogo

Porquê: Reparação Interna: o Registar passa a usar o painel lateral das linhas (como nas Boquilhas), com dia livre, log da pessoa e um separador Histórico por linha e dias, sem filtro de quem.
Referência: Diogo 22/10 11:40 (cmsg_01A27e9ozcpoh4jdPRsuJbFyLWQ8XqSjRnSAL3FNbDmZne)
Novos: 6 · mudaram: 7 · saíram: 2

- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ODuploCliqueAbreAReparacao_ComCorrigirEAnular`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: ReparacaoInternaHistoricoPaginaTests.CorrigirNoHistorico_VoltaAoHistoricoComOsFiltros`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: ReparacaoInternaHistoricoPaginaTests.MudarODia_RegistaNesseDia_ESoAvisa`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: ReparacaoInternaHistoricoPaginaTests.OHistorico_AbreVazio_SemFiltroDeQuem_EMostraSoAsDestaPessoa`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.OPainel_BAntesDeC_ORegistoSoDestaPessoa_EditarNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_Anula_ALinhaSai_EFicaNaBase`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_CorrigeONumero_ComRasto_EOMesmoEnvioAplicaUmaVez`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.SemProducao_GravaSemJobOn_AvisaEATabelaMostraAsDoDia`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EscreverONumeroEEnter_Grava_OCampoVoltaVazio_EALinhaApareceNoTopo`
- saiu: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ONumeroAbreACorrecao_EOXisAAnulacao`
- saiu: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.OsCartoes_BAntesDeC_ATabelaMinimizadaComOUltimo_EditarAbre`

Selo depois: `afe9406c14af4744`

## A-0034 · 2026-10-22 · ok-do-diogo

Porquê: 0.2.0-beta.34: as correções da beta .318–.352 levadas para o full (uma só app). Os testes vêm iguais aos da beta, já aprovados lá; nos testes da folha do Job On do full, o Job On de teste passa a ter CM e BQ, porque um Job On sem CM/BQ/MF é recusado.
Referência: Uma só app: cmsg_01A27e9ozcpoh4jdPRsuJbFyLK1Q5dmDAEv5mfixpSmuBU; CM/BQ/MF obrigatórios: cmsg_01A27e9ozcpoh4jdPRsuJbFy9igAxMSp4hH1PfBm6xZuYZ
Novos: 85 · mudaram: 81 · saíram: 6

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasCurrentQueryTests.cs :: HourSettings.DiaDaTroca_AProducaoQueEntra_EACorrente_MesmoRegistadaAntes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasCurrentQueryTests.cs :: HourSettings.SobreposicaoReal_MostraAsDuasProducoes_ComAviso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasCurrentQueryTests.cs :: HourSettings.UmaSoProducao_UmaLinha_SemAviso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.AsLinhasEscritasAMao_NoMesmoDia_FicamIntactas_AoEscreverEAoAnular`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Atualizar_AsLinhasEscritasAMao_FicamIntactas_ENaoContamComoDiferenca`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Atualizar_DoisDiasDiferentes_EDepoisUmIgual_AtualizaExatamenteDois`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Atualizar_OntemIgual_ParaLogo_ESoEscreveHoje`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Atualizar_PassaParaOMesAnterior_EPara_NoPrimeiroDiaDaApp`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Escreve_ARefEAsQuantidades_NaMaquinaENoDia_ESomaComoODiogo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Escrever_NumExcelNovo_GuardaOsEstilosEAsJuncoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.OutroAno_EMaisDeCincoBoquilhasNoDia_FicamNosAvisos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmDiaReescrito_SemOMovimentoAnulado_FicaLimpo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmExcelNovo_SoTemOsDiasDoMes_EOMesSeguinteGanhaOsQueFaltam`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmExcelNovo_TemAsJuncoesAsCoresEAsLargurasDoOriginal`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmExcelNovo_TemOMesPedido_ERecebeOsMovimentos_EOsMesesSeguintes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmFicheiroQueNaoEExcel_ERecusado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmMesSemSeparador_CriaOSeparador_ComAsDatasEOsDiasDaSemana`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmMesSemSeparador_ESemMovimentos_NaoCriaNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairerWriteHandlersTests.Remover.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairerWriteHandlersTests.Remover.cs :: BqRepairerWriteHandlersTests.Delete_ReparadorQueNaoExiste_RecusaTipada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairerWriteHandlersTests.Remover.cs :: BqRepairerWriteHandlersTests.Delete_SaiOReparador_AsSaidasGuardamONome_EAsRotasFicamSemReparador`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EnvioEmailRegrasTests.cs :: EnvioEmailRegrasTests.Preencher_TrocaOPeso_ComAUnidade`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs :: PesoCapCalculationTests.TrySupport_SoComDiametroECalote_DevolveOApoioSemRaio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.cs :: PesoPdfGeneratorTests.ProducaoAnterior_DizProducao_ENaoSAP`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.NumeroRepetido_ComOLoteDessaPuncao_RegistaComEsseLote_EOLoteDeCimaFica`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Overlap.cs :: PostgresMigrationIntegrationTests.Overlap_NoDiaDaTroca_NaoAvisa_ENoDiaAntesAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.RemoverReparador.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.RemoverReparador.cs :: PostgresMigrationIntegrationTests.RemoverReparadorComSaidas_AsSaidasFicamComONome`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Crosses_ODiaDaTrocaNaoESobreposicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsQueryTests.cs :: JobOnToolFactsQueryTests.FindAsync_OutraAcabaNoDiaEmQueEstaComeca_NoDiaDaTrocaNao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsQueryTests.cs :: JobOnToolFactsQueryTests.FindAsync_OutraComecaNoDiaEmQueEstaAcaba_NoDiaDaTrocaNao_NaVesperaSim`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Search/SuggestionQueryTests.cs :: SuggestionQueryTests.ReferenciasRegisto_NaBq_OQueSeVeEOQueFicaEscrito`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasHandlersTests.cs :: FerramentasHandlersTests.CreateTool_BqSemLote_ERecusada_ENadaEGravado`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.HistoricoDiario.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.HistoricoDiario.cs :: BoquilhasRegistoPageTests.Historico_AAbaAbreOResumoDiario_EACelulaAbreORegistoComOJobOn`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.Sobreposicao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.Sobreposicao.cs :: BoquilhasRegistoPageTests.DiaDaTroca_AQueEntraSemBq_MostraAProducaoEAbreOJobOn_NaoSemProducao`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.Sobreposicao.cs :: BoquilhasRegistoPageTests.DiaDaTroca_OPainelMostraAsDuasProducoes_SemAviso`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.Sobreposicao.cs :: BoquilhasRegistoPageTests.DiaDaTroca_RegistaPeloPainelUmMovimentoEmCadaProducao_NoJobOnCerto`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.Sobreposicao.cs :: BoquilhasRegistoPageTests.SobreposicaoReal_OPainelMostraAsDuasProducoes_ComAviso`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.Sobreposicao.cs :: BoquilhasRegistoPageTests.UmaSoProducao_OPainelFicaComoAntes`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: BqRepairerSettingsPageTests.ExcelDasBoquilhas_Atualizar_EscreveOntem_EDizQueDiasAtualizou`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: BqRepairerSettingsPageTests.ExcelDasBoquilhas_OCartao_AVersaoMudaComUmMovimento_EOFicheiroVoltaEscrito`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: BqRepairerSettingsPageTests.ExcelDasBoquilhas_SemFicheiroOuSemSerExcel_RespondeComAMensagem`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: BqRepairerSettingsPageTests.ExcelDasBoquilhas_UmExcelNovo_TemOSeparadorDoMes`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Remover.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Remover.cs :: BqRepairerSettingsPageTests.DeleteRepairer_PelaJanela_SaiDaLista_ASaidaGuardaONome_EARotaFicaVazia`
- novo: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.EditRepairer_InPlace_AnswersJson_AndTheTableHasNoSaveButton`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_ComPesoNoAssunto_OAssuntoDoEmailTrazOPesoMedioDaProducao`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.NoResumo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.NoResumo.cs :: PesoComparacaoPageTests.Comparacao_AbreNoResumoDaProducao_SemSeparadorProprio`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.Historico_SemJobOnAssociado_EncontraOPesoSemProducao`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoSoParaComparacaoPageTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoSoParaComparacaoPageTests.cs :: PesoSoParaComparacaoPageTests.CriarDaComparacao_LigaOPesoAoAtual_MostraAComparacao_ENuncaPedeJobOn`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoSoParaComparacaoPageTests.cs :: PesoSoParaComparacaoPageTests.PesoSoParaComparacao_AssociarAUmJobOn_ERecusado`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.BqDoJobOn.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.BqDoJobOn.cs :: FerramentasPagesTests.CriarBqDoJobOn_ComAReferenciaTotal_GuardaETornaAMostrarAReferenciaToda`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.BqDoJobOn.cs :: FerramentasPagesTests.CriarBqDoJobOn_SemLote_ERecusada_ENadaEGravado`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.MfAssociada.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.MfAssociada.cs :: FerramentasPagesTests.Ficha_ReferenciaMfAssociada_SoNaCmComReferenciaDiferente`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.CartoesDasFerramentas_ONomeEAbrirFicha_LevamAFichaDaFerramenta`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.FimNoDiaDoInicioOuDepoisNaCriacao_GravaEFicaNoJobOnSemAviso`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ProducoesDoDia_LinhaVermelha_SoComSobreposicaoReal`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Create_ComOControloCriar_FicaNoJobOn_NaoVaiAoResumo`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Create_SemCmOuSemBq_ERecusado_ENadaEGravado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Edit_PorABqNumJobOnSemBq_NaoEMudancaDeBoquilhas`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Edit_SemBq_ERecusado_ENadaMuda`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Planeamento_CriarJobOn_FicaNaBarraDaProcura_ADireitaDasCaixas_ComODia`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: JobOnSidepanelWeekTests.Em_curso_primeiro_e_as_que_ja_acabaram_ficam_de_fora`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs :: PegamentosPageTests.NumeroFerramenta_CartaoBqOuMf_ColunaComOSeuNomeEGrava`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs :: PegamentosPageTests.NumeroFerramenta_Corrigir_ONumeroAbreComFocoEGrava`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs :: PegamentosPageTests.NumeroFerramenta_Limite20_19e20GravamInteiros_21ECortadoComAviso`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs :: PegamentosPageTests.NumeroFerramenta_LinhaSoComONumero_NaoERegistadaEAvisa`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs :: PegamentosPageTests.NumeroFerramenta_RegistarComNumero_GravaEAFolhaMostraNaColunaNoCm`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs :: PegamentosPageTests.NumeroFerramenta_VariasLinhas_CadaUmaComOSeuNumero_SoONumeroContaComoVazia`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs :: PlaneamentoSumarioPageTests.Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs :: PuncoesPageTests.NumeroRepetido_PeloFormularioDaPagina_GravaOLoteDessaPuncao`
- novo: `tests/ShellProof/jobon-tool-open.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/jobon-tool-open.test.cjs :: Cartão da ferramenta: o nome e «Abrir ficha» seguem a ferramenta escolhida`
- novo: `tests/ShellProof/puncoes-numero.test.cjs :: um número já registado pede o lote dessa punção; um número novo não`
- novo: `tests/visual/excel_bq_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasRegistoQueryTests.cs :: BoquilhasRegistoQueryTests.FindByContext_WithAssociatedTrace_ReturnsTraceProductionAndTool`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairerWriteHandlersTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.PegamentosMeasurementEntity_AppendOnlyFacts_ContraCosturaNullable`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs :: PesoCapCalculationTests.Support_ComCaloteDoJobOn_ComparaComAReferencia`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs :: PesoCapCalculationTests.Support_ReferenciaPlana_DiferencasDeRaioIndefinidas`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs :: PesoCapCalculationTests.Support_SemCaloteEfetiva_CaiNaReferenciaDaTool`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs :: PesoCapCalculationTests.TrySupport_GeometriaCompleta_DevolveOApoio`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs :: PesoCapCalculationTests.TrySupport_GeometriaIncompleta_SemApoio`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapSupportQueryTests.cs :: PesoCapSupportQueryTests.GetAsync_ProducaoComCalote_OrigemJobOn`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapSupportQueryTests.cs :: PesoCapSupportQueryTests.GetAsync_SemControlo_UsaAReferenciaDaTool`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapSupportQueryTests.cs :: PesoCapSupportQueryTests.GetAsync_ToolSemTP_ReferenciaPorCompletar`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.PesoEntity_HasNoProductionIdentityColumns`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Overlap.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/EdicoesAoMesmoTempoTests.cs :: EdicoesAoMesmoTempoTests.JobOn_OMesmoEnvioOutraVez_AbreOGravado_SemOutraLinha`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_AcabaNoDiaEmQueAOutraComeca_AvisaEGrava_NaVesperaNao`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_ComecaNoDiaEmQueAOutraAcaba_AvisaEGrava_NoDiaSeguinteNao`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasHandlersTests.cs :: FerramentasHandlersTests.CreateTool_EmptyLot_SavesNoLot_AndASecondOneIsRefused`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasMovementCorrectionTests.cs :: BoquilhasRegistoPageTests.DeleteMovement_RecentMovementIsDeleted`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.PersistedToolAndContextFacts_RenderFromServerData`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs :: BoquilhasRegistoPageTests.OpeningAMachine_DoesNotCreateTheTrace`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.BoquilhasSection_RendersTheRepairerList_WithZeroEntitiesWarning`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.BoquilhasSection_WithEntities_RendersTheMachineMapOfTheSixCodes`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.CapSupportEndpoint_DevolveOCalculoDoBackend`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_PainelDeFactos_MostraVolumesDensidadeEApoioComACaloteDoJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_SemTP_ReferenciaPorCompletarEPesoIntacto`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.DoisPassos_SemReferenciaNadaCarrega_ComReferenciaSoAsDela`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.Historico_Filtros_TrocamSoOsResultadosEAsProducoes`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.Historico_ProcuraUnificadaPelosQuatroTipos`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PropoeAMaisRecente_GuardaOPesoDoSap_EMostraODelta`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoJobOnEscolhaPageTests.cs :: PesoJobOnEscolhaPageTests.JobOnIdSo_SemControlo_AbreOResumoDaProducao_SemCriarNadaNoPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoPorCombinacaoPageTests.cs :: Producao.ComDoisPesos_OResumoARevisaoEODetalheMostramOsDois`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoPorCombinacaoPageTests.cs :: Producao.DepoisDeTrocarACm_AFolhaPropoeCriarOPesoNovo_EDizPorque_SemGravarNada`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ResumoNoBrowser.cs :: ControloCapabilitiesCutHPageTests.Resumo_SemPesoComUmPesoParaAssociar_ParaOBrowser`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs :: ControloCapabilitiesCutHPageTests.Revisao_ArrumadaComoODiogoPediu`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs :: ControloCapabilitiesCutHPageTests.PesoAssociation_PrePesoWithJobOnCm_IsOfferedAndAssociatedOnlyByExplicitCreatePost`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs :: ControloResumoPageTests.SecondaryNav_GroupsTheSheetsUnderFuncoes`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CliquesRepetidos.cs :: FerramentasPagesTests.Registar_GravaPelaFilaDaPagina_AsMensagensNumaZona`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Registo_InformacaoTecnicaEmSeccoesComDica_QuantidadeAntesDaRefMf`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: FerramentasPagesTests.Criar_APartirDoCartaoDoJobOn_OTipoFicaFixo`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.GetFicha_OldRefusalLinks_StillShowTheirMessages`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.PostCreate_EmptyLot_SavesNoLotWithWarning_AndASecondOneIsRefused`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.PostEditTechnical_GenesisSheetValues_PrgPersistedExactly`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs :: JobOnDuplicarFolhaTests.Duplicar_MarcaConfirmarNaMaquinaNasFerramentasENasUtilizacoesCopiadas`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.CaloteNaCriacao_PreservaODecimalAoGuardarEReabrir`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ProducoesDoDia_MostramAProximaMudancaEmCadaMaquina`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ToolFactsEndpoint_DatasQueNaoSeCruzam_SemAvisoDeUsoParalelo`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Update_OverlapWarningsReflectKnownDates_AndNeverBlockSaving`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Criar_GravaAFolhaNaMesmaEscritaDoJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.TextoLongoDemais_NoCriar_NaoCriaOJobOn_EAFolhaVoltaComOQueSeEscreveu`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: HistoryReads.Historico_AbreEmBranco_SoComOsFiltros_SemLerProducoes`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.CreateFlow_WithOverlapOnSameMachine_StaysOnJobOnWithTheYellowBanner`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.CreateRefusal_IsTypedAndRedirectsWithPreservedValues`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Create_DoubleClick_SecondSubmitOpensTheSavedJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Create_DuplicateProductionNumber_RefusedTyped`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.EditFlow_ReplansTheSameProduction_WindowLineAndTools`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.EditFlow_SavedMeanwhileByAnotherUser_RefusesAndShowsWhatWasSaved`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.EditFlow_TheSameSaveTwice_GoesWhereTheFirstWent_WithoutTheRefusal`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.ProximaMudanca_FicaANegrito`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: JobOnSidepanelWeekTests.Mostra_so_entradas_de_hoje_ate_domingo`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.ControloInformation_IsForEitherControloModule`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.Planeamento_SeesTheProductionsStartingToday_AndTheTabSitsNextToJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.TheCalendarOpensTheChosenDay`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs :: PuncoesPageTests.WithoutJobOn_RegisterStandsAlone_AndIsAssociatedByHand`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs :: PuncoesPageTests.WithoutJobOn_TheAssociationSearchAndRefusals_ChangeOnlyTheirZone`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedTriangle_AndLivesOnlyInThePartial`
- mudou: `tests/DMO.Alpha.Web.Tests/ZonasGravacao.cs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/cliques-repetidos.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/package.json`
- mudou: `tests/ShellProof/puncoes-numero.test.cjs :: (resto do ficheiro)`
- mudou: `tests/visual/definicoes_parcial_browser.py`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsQueryTests.cs :: JobOnToolFactsQueryTests.FindAsync_OutraAcabaNoDiaEmQueEstaComeca_Avisa_NaVesperaNao`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsQueryTests.cs :: JobOnToolFactsQueryTests.FindAsync_OutraComecaNoDiaEmQueEstaAcaba_Avisa_NoDiaSeguinteNao`
- saiu: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.EditRepairer_InPlace_AnswersJson_AndTheTableHasNoButtons`
- saiu: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.CompararPesosGuardados_PorId_NosSelectsDoHistorico`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.FimNoDiaDoInicioOuDepoisNaCriacao_GravaESegueParaOResumoSemAviso`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.CreateFlow_UnselectedBqSlot_IsNotApplicable_NoBqContextCreated`

Selo depois: `013c99618fc58a39`

## A-0036 · 2026-10-22 · ok-do-diogo

Porquê: App única, passo 3: interruptores dos módulos no Admin › Setup e faixa «Em desenvolvimento»; Armazém e Reparação Interna desligados por omissão (também sem a chave na configuração). A fábrica de testes liga os módulos em desenvolvimento para os testes que já existiam.
Referência: Diogo 22/10 12:06 aprova o plano uma-versão (cmsg_01A27e9ozcpoh4jdPRsuJbFyLK1Q5dmDAEv5mfixpSmuBU)
Novos: 7 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.Estado_FiltraSoOsModulosDesligados`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.LigadoParaTodos_EntramTodosOsQueTemOModulo`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.LigadoSoParaTeste_EntraQuemEDeTesteEVeAFaixa`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.PorOmissao_OsModulosEmDesenvolvimentoFicamDesligados`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.SemAChave_ArmazemERiFicamDesligados`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.Setup_MostraOsInterruptoresComOGuardarNoTopo`
- mudou: `tests/DMO.Alpha.Web.Tests/CustomWebApplicationFactory.cs :: (resto do ficheiro)`

Selo depois: `78c07ffe6cf4d05e`

## A-0035 · 2026-10-22 · ok-do-diogo

Porquê: PU NNPB: punções por posição, reserva, repetidos com lote, PDF à parte
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyLxtbXWax7U9hDSa4vVz3kV
Novos: 6 · mudaram: 2 · saíram: 3

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CoresDeEstadoNosPdfsTests.cs :: CoresDeEstadoNosPdfsTests.Puncoes_SaemComAsCoresDeEstadoNoResumo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.Pdf_PosicoesDaLinha_EOResumoLigaOFicheiroDasPuncoes`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.FolhaDasPuncoes_LigadaDoResumoEDoRegisto_SoComRegisto_EOPdfDoControloTrazODasPuncoes`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_A1aD12_Reserva_RepetidosComLote_ETotais_NaZonaDaFolha`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_SeguemAsSeccoesDaLinha_GuardadasNasDefinicoes`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.Pdf_StandaloneRegister_SaysSemJobOn`
- mudou: `tests/visual/pasta_pdf_browser.py`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CoresDeEstadoNosPdfsTests.cs :: CoresDeEstadoNosPdfsTests.Puncoes_SaemComAsMesmasCoresNoPdfDoPuNnpbENoDoResumo`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.Pdf_HasNoApprovalStamp`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.Pdf_SheetAndResumoWithPuncoes_AreGeneratedInMemory`

Selo depois: `64ea816d4d66879b`

## A-0038 · 2026-10-22 · ok-do-diogo

Porquê: Testes web: cada fábrica tem a sua base em memória (antes uma só para todas as classes, que corriam em paralelo e apagavam os dados umas das outras; DMO-312)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyRTc4GbKqSyKguBgVdi92LW
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/CustomWebApplicationFactory.cs :: (resto do ficheiro)`

Selo depois: `c77edb7db73eac47`

## A-0039 · 2026-10-22 · ok-do-diogo

Porquê: Armazém: separador «Movimentos» (fora em reparação só CM/MF; últimas entradas e saídas de tudo, BQ incluídas, com filtros por dias, tipo, referência, lote, linha e reparador) substitui o Histórico; «Registo» passa a primeiro separador. A regra «abre vazio» não se aplica aqui: o Diogo pediu que a página mostre as últimas entradas e saídas (22/10 13:25).
Referência: Diogo 22/10 13:25–13:43 (cmsg_01A27e9ozcpoh4jdPRsuJbFy12VMmFYgLHpRhn1RAK5HLp, cmsg_01A27e9ozcpoh4jdPRsuJbFyPgTMUWFVkDYEkGJU4gwQav, cmsg_01A27e9ozcpoh4jdPRsuJbFyV24qJxcxGS2xJRaWYoDSF4, cmsg_01A27e9ozcpoh4jdPRsuJbFyFf6WkWFn2hQgaVcKvKLb4j)
Novos: 14 · mudaram: 2 · saíram: 6

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemReparacaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemReparacaoTests.cs :: ArmazemReparacaoTests.Fora_SoCmEMfCujoUltimoMovimentoESaidaParaReparacao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemReparacaoTests.cs :: ArmazemReparacaoTests.Ultimas_BqIncluida_ComReparadorEPosicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemReparacaoTests.cs :: ArmazemReparacaoTests.Ultimas_OsFiltrosNovos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemReparacao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemReparacao.cs :: PostgresMigrationIntegrationTests.Armazem_AReparacaoLeNoPostgreSql`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.AFicha_MostraALocalizacaoEOsMovimentos_SoAQuemTemOArmazem`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.AsFuncoes_RegistoDepoisMovimentos_SemHistorico`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Fora_SemFerramentas_NaoAbreAFicha`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Fora_SoCmEMf_ComReparadorEDia_BqNao`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.SemAChave_NaoEntra`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Ultimas_OsFiltros`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Ultimas_Todas_DoMaisRecente_BqIncluida_PosicaoSoNaEntrada`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_MostraOndeEstaEAbreAFerramenta`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.AFicha_MostraALocalizacaoEOHistorico_SoAQuemTemOArmazem`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.AbreVazio_SoComOsFiltrosSemBotao`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.ComUmFiltro_ListaDoMaisRecente`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.OsFiltros_TextoDestinoEDias`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemHistoricoPaginaTests.cs :: ArmazemHistoricoPaginaTests.SemAChave_NaoEntra`

Selo depois: `06942017116725ab`

## A-0041 · 2026-10-22 · ok-do-diogo

Porquê: Armazém: o Registo passa a ter o painel CM, MF, BQ (como na Reparação Interna), com a procura só do tipo escolhido e o log das últimas entradas e saídas desse tipo; o Mapa sai (não é preciso mapear o Armazém, basta a posição de 4 algarismos).
Referência: Diogo 22/10 13:41 (cmsg_01A27e9ozcpoh4jdPRsuJbFyV24qJxcxGS2xJRaWYoDSF4), 14:32–14:33 (cmsg_01A27e9ozcpoh4jdPRsuJbFyH7TVX6tBZwy8eVzpn12kHf, cmsg_01A27e9ozcpoh4jdPRsuJbFyUCfpU1YNPJKA9oJM1VMmA3)
Novos: 4 · mudaram: 3 · saíram: 5

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.OLog_MostraSoOTipo_DeTodos_EALinhaAbreAFerramenta`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.OPainel_TemCmMfBq_ESemTipoPedeParaEscolher`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_SemFerramenta_AvisaComOLinkParaAsFerramentas`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_SoNoCartaoDoTipo_MostraOndeEstaEAbreAFerramenta`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_MostraOUsoDaUltimaLeitura_ESemLeituraUmTraco`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.AsFuncoes_RegistoDepoisMovimentos_SemHistorico`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.AbreOPrimeiroArmario_ComAOcupacaoEOParNumaLinha`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.OutroArmarioEOFiltro_PorReparar`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMapaPaginaTests.cs :: ArmazemMapaPaginaTests.SemAChave_NaoEntra`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_MostraOndeEstaEAbreAFerramenta`

Selo depois: `86f7a1b4e3cec2eb`

## A-0042 · 2026-10-22 · ok-do-diogo

Porquê: Armazém: um lote novo sem ferramenta regista-se «por criar» (referência, lote, posição) e aparece um aviso no Sumário do Planeamento a quem tem Ferramentas, que o cria (formulário preenchido) ou liga a uma existente; aí nasce a entrada. Saem os separadores Próximos Job On e Posições. As listas de tabelas dos testes de esquema ganham ArmazemLotesNovos.
Referência: Diogo 22/10 14:34–14:38 (cmsg_01A27e9ozcpoh4jdPRsuJbFy2zHChJQdLDNK6avT3ZiQDc, cmsg_01A27e9ozcpoh4jdPRsuJbFyQkcWpKYLcscszBPF1xNLJZ, cmsg_01A27e9ozcpoh4jdPRsuJbFyP4hMeKkavxu15b6g6xFAVR), 15:13 (cmsg_01A27e9ozcpoh4jdPRsuJbFyKg2rdNP3oYtPP4z4fuyZt4)
Novos: 12 · mudaram: 10 · saíram: 7

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLotesNovosTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLotesNovosTests.cs :: ArmazemLotesNovosTests.FerramentaExistente_ComAMesmaReferenciaELote_LigaEmVezDeCriar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLotesNovosTests.cs :: ArmazemLotesNovosTests.Registar_GuardaPorCriar_SemCriarFerramentaNemMovimento`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLotesNovosTests.cs :: ArmazemLotesNovosTests.Registar_SoRecusaOQueFaltaEAPosicaoSemQuatroAlgarismos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLotesNovosTests.cs :: ArmazemLotesNovosTests.Resolver_ComFerramentaQueNaoExiste_NaoResolve`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLotesNovosTests.cs :: ArmazemLotesNovosTests.Resolver_DaAEntradaNaPosicaoGuardada_EOUltimoMovimentoPoeLa`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs :: ArmazemLoteNovoPaginaTests.FerramentaQueJaExiste_LigaAoRegistoEmVezDeCriar`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs :: ArmazemLoteNovoPaginaTests.Registar_GuardaPorCriar_SemCriarFerramenta_EMostraNoLog`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs :: ArmazemLoteNovoPaginaTests.Registar_SoRecusaAPosicaoSemQuatroAlgarismosEOQueFalta`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs :: ArmazemLoteNovoPaginaTests.Sumario_AvisaSoQuemTemFerramentas_ECriarResolveEDaAEntrada`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.ComAChave_RegistoMovimentosEDefinicoes`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.LigadoParaTodos_EntramTodosOsQueTemOModulo`
- mudou: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.LigadoSoParaTeste_EntraQuemEDeTesteEVeAFaixa`
- mudou: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.PorOmissao_OsModulosEmDesenvolvimentoFicamDesligados`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.SemAChave_NaoEntra`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.AsFuncoes_RegistoDepoisMovimentos_SemHistorico`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Posicoes_MostramQuemLaEstaOuLivre`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.ComAChave_AsDuasFuncoesEOMenu`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.Posicoes_CriarEArquivarNaLinha`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: ArmazemProximosPaginaTests.MostraAsProducoesDoPeriodo_ComOQueFaltaTirar`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: ArmazemProximosPaginaTests.OPeriodoMaisLongo_MostraMais`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemProximosPaginaTests.cs :: ArmazemProximosPaginaTests.SemAChave_NaoEntra`

Selo depois: `ec4f12892d2ba255`

## A-0040 · 2026-10-22 · ok-do-diogo

Porquê: Boquilhas beta.46: mudança de BQ, Registo só de Job On, Histórico, Registar sem Job On
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyLYtxuHqcPGnrE4uFTkNR7w
Referências do Diogo, por mudança:
- cmsg_01A27e9ozcpoh4jdPRsuJbFyLYtxuHqcPGnrE4uFTkNR7w — acrescentar BQ não é mudança no painel do calendário.
- cmsg_01A27e9ozcpoh4jdPRsuJbFyXAv4zn3ZRQsjWu6pw4g4tw — sem anel de foco/autofocus na «Saída», sem «Máquina em uso», «Registar» à esquerda.
- cmsg_01A27e9ozcpoh4jdPRsuJbFyS6hb9Vb5DCDWCRaSEukWmm — sem «Procurar / trocar», «Histórico» à vista.
- cmsg_01A27e9ozcpoh4jdPRsuJbFyXekf9uE9hqpRT4L1xsutkf, cmsg_01A27e9ozcpoh4jdPRsuJbFyEuCy6fs1W93HYcFPrt4eWm, cmsg_01A27e9ozcpoh4jdPRsuJbFyAZju7jrXDRHhCZJUK1Sb5q — registo sem Job On numa janela na página Boquilhas; o Registo é só de Job On (inclui tirar o «+ Registo com data antiga»: a data livre da janela substitui-o).
- cmsg_01A27e9ozcpoh4jdPRsuJbFyQqvnxegrtFcaEamR7gJfMe — a Linha na janela e na tabela.
- cmsg_01A27e9ozcpoh4jdPRsuJbFy1ctjeLanzx8dtk2B438UUM — o aviso do lote fala da contagem da reparação.
Novos: 8 · mudaram: 14 · saíram: 14

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnChangeTests.cs :: JobOnChangeTests.Update_AddingABqWhereThereWasNone_IsNotAChange_AndOldRowsAreHidden`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraPeloInicioTests.cs :: BoquilhasRegistoPageTests.ProcurarBq_JaNaoResponde_ComALista`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoOpcoesTests.cs :: BoquilhasRegistoPageTests.Registo_LinkHistorico_AbreOHistoricoFiltradoPorEstaBqELote_ComCadaReferenciaTotal`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoTrocaTests.cs :: BoquilhasRegistoPageTests.Registo_TrocaSoAsZonas_SemFocoAutomaticoNoTipo`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_NaPaginaBoquilhas_GravaEMostraOMovimento`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_SemQuantidade_ReabreAJanelaComAFrase`
- novo: `tests/ShellProof/bq-registo.test.cjs :: gravar um movimento não mexe no scroll`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.FormularioDaPagina.cs :: BoquilhasRegistoPageTests.MovimentoPeloFormularioDaPagina_GravaAQuantidadeEAMaquinaNaBQ`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.HoraDeViragem.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.HoraDeViragem.cs :: BoquilhasRegistoPageTests.DeNoiteAntesDaViragem_AProcuraEOsRegistosSeguemAProducaoEmMaquina`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.MachineInUse_ListsOnlyTheMachinesOfTheTool_AndQuantityComesFirst`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.MachinesPanel_DirectLinkOpensProductionRegisterAndKeepsSearchCollapsed`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.RegisterMetrics_RenderFromTheBackend_NeverFromTheClient`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.RegisterMovement_ChosenMachineIsSavedAndShownInTheRegister`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.ToolSheet_ShowsTheFactsTheHistoricalFormAndTheRegisters`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs :: BoquilhasRegistoPageTests.OpeningAMachine_WarnsAboutTheUnresolvedTraceOfTheSameBq`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.cs :: BoquilhasRegistoPageTests.ToolCatalog_CriarBoquilha_AbreORegistoDasFerramentasComBqEAMaquina_EVoltaComAFicha`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.cs :: BoquilhasRegistoPageTests.ToolCatalog_SoAMaquinaNaoMostraALista_OTextoMostra_EAMaquinaSoReduz`
- mudou: `tests/ShellProof/bq-registo.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/bq-registo.test.cjs :: o registo aberto fora do ecrã vem à vista`
- mudou: `tests/visual/boquilhas_catalog_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraPeloInicioTests.cs :: BoquilhasRegistoPageTests.Procura_PorUmaProducaoAntiga_DizQualEAbreORegistoDela`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraPostgresTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraPostgresTests.cs :: BoquilhasProcuraPostgresTests.ProcuraBq_PeloInicioDaReferenciaOuDaParteBq_NoPostgres`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraTests.cs :: BoquilhasRegistoPageTests.Procura_DepoisDeTrocarABq_OLoteSubstituidoJaNaoEstaEmProducao`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraTests.cs :: BoquilhasRegistoPageTests.Procura_ShowsBqLotMachinesAndProductionState_AndOpensTheRegister`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraTests.cs :: BoquilhasRegistoPageTests.Procura_WithoutLogin_IsRefused`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoOpcoesTests.cs :: BoquilhasRegistoPageTests.Registo_Opcoes_HistoricoBq_AbreOHistoricoFiltradoPorEstaBqELote`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.OpenHistoricalRegister_OpensTheRegisterWithTheRealDatePrefilled`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.Search_ShowsTheRegisterChoicePanel_WithAndWithoutJobOn`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoTrocaTests.cs :: BoquilhasRegistoPageTests.Registo_TrocaSoAsZonas_ComOFocoNoTipoDoProximoMovimento`
- saiu: `tests/ShellProof/bq-registo.test.cjs :: abrir uma boquilha encontrada fecha a procura depois da troca`
- saiu: `tests/ShellProof/bq-registo.test.cjs :: abrir uma máquina no painel lateral também fecha a procura`
- saiu: `tests/ShellProof/bq-registo.test.cjs :: gravar um movimento não mexe na procura nem no scroll`

Selo depois: `294d3b64edbc4222`

## A-0043 · 2026-10-22 · ok-do-diogo

Porquê: Reparação Programada (testes novos e asserts do Guardião); Armazém: posição escrita com 4 algarismos, Par retirado, nome «CM 5447 · Lote 3», procura com Linha e % uso
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy1cW4SLjtJXkpk8oM1WN5PB, cmsg_01A27e9ozcpoh4jdPRsuJbFyFvoCqZxBCfbNiMCSCaYUJr, cmsg_01A27e9ozcpoh4jdPRsuJbFy9qgxfrBEJB18gAj73jMxV9, cmsg_01A27e9ozcpoh4jdPRsuJbFyREJtBGBtAMys1aMqhj4ScT, cmsg_01A27e9ozcpoh4jdPRsuJbFy9Y4VRqA9QFDVXiEb9TVcHX, cmsg_01A27e9ozcpoh4jdPRsuJbFyRsaoxEoxkRVC7iHGuSe6VD, cmsg_01A27e9ozcpoh4jdPRsuJbFy8KJ4hzSSrHYsnq5qdo19eV, cmsg_01A27e9ozcpoh4jdPRsuJbFyKzeCU6vgqk5cWWEqqVWneS
Novos: 17 · mudaram: 8 · saíram: 4

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.Associar_CriaListaDoReparador_ComOQueSeViaDaFerramenta_EAcrescentaAUmaAtiva`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.Associar_RecusaSemReparador_ReparadorArquivado_EListaFechada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.Entrada_DeFerramentaNumaListaAtiva_PropoeReparado_EMudarGravaComAviso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.Entrada_ForaDeQualquerLista_NaoMudaAProposta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.Lista_FechaSozinha_QuandoAUltimaFerramentaDaEntrada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.Procurar_SemFiltros_AbreVazia_EComFiltros_MostraCmEMfComAPosicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.SaidaEEntrada_AtualizamALista_EALinhaQueEntrouMostraDiaPosicaoENome`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ACm_NaoMostraOPar_EAPosicaoEscritaCom4Algarismos`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.AEntrada_EntraSoACm`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.CorrigirLocalizacao_TiraDoArmazem`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaHistoricoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaHistoricoTests.cs :: ReparacaoProgramadaHistoricoTests.Imprimir_SoMostraAsPorRetirar_EOHistoricoAbreVazio_EListaAsCriadas`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.ArmazemEntrada_DeFerramentaNaLista_SugereReparado_AvisaAoMudar_EALinhaFicaCinza`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.Ferramentas_VeOSeparador_AProcuraAbreVazia_EAssociaAUmaListaNova`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.SemFerramentas_NaoEntra_NemAssocia_NemImprime`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_MostraOUsoDaUltimaLeitura_ESemLeituraUmTraco`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Fora_SoCmEMf_ComReparadorEDia_BqNao`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Ultimas_Todas_DoMaisRecente_BqIncluida_PosicaoSoNaEntrada`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ACm_MostraOMfEAsPosicoesLivres`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ComOParDesmarcado_EntraSoACm`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs :: ArmazemEntradaComParPaginaTests.ComOParMarcado_ACmEOMfEntramJuntos`
- saiu: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.CorrigirLocalizacao_NoOpcoesTiraDoArmazem`

Selo depois: `4997387bbc158b7c`

## A-0037 · 2026-10-22 · ok-do-diogo

Porquê: Ícones: ajuda em círculo, triângulo só para recolher (também pedido em cmsg_01A27e9ozcpoh4jdPRsuJbFyAPrbKkhtKC5EQrkAhMNtXx); Peso, Gargalo e Tampão alinhados (cmsg_01A27e9ozcpoh4jdPRsuJbFyXBzFqVnzmNuHBDNNhA9M9W)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyTwmoz769Le9LdAjXagR781
Novos: 2 · mudaram: 1 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.Caret_SoParaRecolher_ApontaParaAPalavraEAbertoParaBaixo`
- novo: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico`
- saiu: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedTriangle_AndLivesOnlyInThePartial`

Selo depois: `4e415b1e638138f6`

## A-0044 · 2026-10-22 · ok-do-diogo

Porquê: Armazém: tabelas com Linha e % uso, linha abre a ficha; avisos do Armazém no Job On em espera; Reparação Programada com Listas/Histórico separados e procura só com filtros
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyXfNU7jcpKvE6aoyJFejJsb, cmsg_01A27e9ozcpoh4jdPRsuJbFy7VQFHmWxH8LLRGu2LgzbUm, cmsg_01A27e9ozcpoh4jdPRsuJbFy3ywAgFqJ4UPrRkxVw9H1sN, cmsg_01A27e9ozcpoh4jdPRsuJbFyKSqvNb77drHCVLbJLjF6FN, cmsg_01A27e9ozcpoh4jdPRsuJbFyKNWM2e5cFZmoLvzMAqdm5s, cmsg_01A27e9ozcpoh4jdPRsuJbFy1mRo5xn7zAQy42TtGpH8pF, cmsg_01A27e9ozcpoh4jdPRsuJbFyHo7org3RGuhgc7bX2MW432, cmsg_01A27e9ozcpoh4jdPRsuJbFyBLnH9tw7Pd3XGwwRSrXdqy, cmsg_01A27e9ozcpoh4jdPRsuJbFyFePhNLVk6MX4pSvkrh3mGy
Novos: 8 · mudaram: 5 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.ComAsFerramentas_ALinhaDasUltimasAbreAFicha_NoRegistoENosMovimentos`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaHistoricoTests.cs :: ReparacaoProgramadaHistoricoTests.Imprimir_SoMostraAsPorRetirar_EAsFechadasAbremVazias_EFiltramPeloReparador`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaHistoricoTests.cs :: ReparacaoProgramadaHistoricoTests.NovaLista_CriaComOReparador_ESemListaOAdicionarAvisa`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.ComOInterruptor_OAvisoDoArmazemVolta`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.PorOmissao_AAplicacaoNaoOLigaNasDefinicoes`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.SemOInterruptor_OAvisoDoArmazemNaoAparece_EOsOutrosSim`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.AvisoDoArmazem_EmEspera_NaoApareceNaFicha_NemNoEndpoint_MasOsOutrosSim`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Fora_SoCmEMf_ComReparadorEDia_BqNao`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Ultimas_Todas_DoMaisRecente_BqIncluida_PosicaoSoNaEntrada`
- mudou: `tests/DMO.Alpha.Web.Tests/CustomWebApplicationFactory.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.ArmazemEntrada_DeFerramentaNaLista_SugereReparado_AvisaAoMudar_EALinhaFicaCinza`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaHistoricoTests.cs :: ReparacaoProgramadaHistoricoTests.Imprimir_SoMostraAsPorRetirar_EOHistoricoAbreVazio_EListaAsCriadas`

Selo depois: `69bb56498f37525b`

## A-0045 · 2026-10-22 · ok-do-diogo

Porquê: 0.3.0-beta.3: ficha da ferramenta (Linhas compatíveis, Informação opcional com Fábrica de origem, % uso no Editar com os limites 0 e 100, posição e estado do Armazém, «produção» em vez de «SAP»); aviso da utilização por atualizar no Sumário; ajuda num círculo vazio mais pequeno, acima dos botões
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy5p3qq7SxaMMGvauvLSyNav, cmsg_01A27e9ozcpoh4jdPRsuJbFyMz2soxnCyBtiE2B7JJVzqr, cmsg_01A27e9ozcpoh4jdPRsuJbFyUPiaq8Li4P6Nj19Pgq7ps5, cmsg_01A27e9ozcpoh4jdPRsuJbFy4tAPdRQjxH9PWS5rLJR3Di, cmsg_01A27e9ozcpoh4jdPRsuJbFyPbTAPQBEuX72Sqnqu3ZQ8x, cmsg_01A27e9ozcpoh4jdPRsuJbFy1mRo5xn7zAQy42TtGpH8pF, cmsg_01A27e9ozcpoh4jdPRsuJbFyQqraqL8sY7pUxJc1CmnVdJ
Novos: 5 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: Sessao.OPercentManual_GravaUmaLeituraEFechaOLembrete_IgualNaoGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: Sessao.PorAtualizar_SaoOsLembretesAbertos_OsMaisAntigosPrimeiro`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Ficha_Editar_LinhasCompativeis_InformacaoOpcionalComFabricaDeOrigem`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Ficha_PercentagemDeUso_NoEditar_GravaComOGuardar_SoAvisos`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.UtilizacaoPorAtualizar_AvisaNoSumario_SoQuemTemFerramentas`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.AFicha_MostraALocalizacaoEOsMovimentos_SoAQuemTemOArmazem`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.OsCodigosAPedirAoSap_SaoOsPedidosNaFicha`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.FichaWarnings_GoToTheCorner`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial`

Selo depois: `66076833a517fa10`

## A-0046 · 2026-10-22 · teste-novo

Porquê: História de uma produção: os módulos e as suas ligações (Job On, RI, BQ, Peso, Pegamento, Armazém)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFySMKCQbjDHDqNK4StKX4cvJ, cmsg_01A27e9ozcpoh4jdPRsuJbFyY8vNWWReJph26vFXSxA6MT, cmsg_01A27e9ozcpoh4jdPRsuJbFyTmS6KYpHKMqSDWhsg9NugJ, cmsg_01A27e9ozcpoh4jdPRsuJbFyL19wGTjcKJZaz9UcWt9EPV
Novos: 4 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs :: HistoriaCenario.HistoriaDeUmaProducao_TodosOsModulosVeemOMesmo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs :: HistoriaCenario.JobOnAntigo_MostraQuemReparouCadaFerramenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs :: HistoriaCenario.MudarOJobOn_NaoPerdeNadaDosOutrosModulos`

Selo depois: `771fcfc9beca2c80`

## A-0048 · 2026-10-22 · contagem-lista

Porquê: Rascunho do Job On guardado ao recarregar e Enter na procura da Reparação Programada: testes novos (página e ShellProof); package.json só acrescenta os dois ficheiros à lista do test:js
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyJ3Y6hLo6zvBpEqbQwNgjbV
Novos: 15 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/RascunhoEEnterNaProcuraTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/RascunhoEEnterNaProcuraTests.cs :: RascunhoEEnterNaProcuraTests.NovoJobOn_AFolhaLevaAChaveDoRascunho_EOScriptQueOGuarda`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/RascunhoEEnterNaProcuraTests.cs :: RascunhoEEnterNaProcuraTests.ReparacaoProgramada_OEnterNaReferenciaENoLoteSubmeteAProcura_ENaoOAdicionar`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: os campos que chegam no endereço ganham ao rascunho (o regresso de «Criar CM»)`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: os rascunhos do Novo, do Duplicar X e do Editar Y não se misturam`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: quando o servidor recusa a gravação, o rascunho volta`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: recarregar a folha repõe os campos, com «Rascunho recuperado»`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: sem localStorage a folha funciona, sem erros`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: uma gravação com sucesso apaga o rascunho, e ele não volta depois`
- novo: `tests/ShellProof/jobon-rascunho.test.cjs :: «Descartar rascunho» repõe os valores do servidor e apaga o rascunho`
- novo: `tests/ShellProof/programada-enter.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/programada-enter.test.cjs :: Enter na Referência e no Lote submete o rpFiltros, nunca o rpAssociar${comCanonico ? ' (com o script da casa)' : ''}`
- novo: `tests/ShellProof/programada-enter.test.cjs :: o script do Enter está na página`
- novo: `tests/ShellProof/programada-enter.test.cjs :: um Enter que outro script já tratou (a sugestão escolhida) não submete outra vez`
- mudou: `tests/ShellProof/package.json`

Selo depois: `fbaea0abc9efe7ea`

## A-0050 · 2026-10-22 · ok-do-diogo

Porquê: Diogo aprovou a escolha da BQ
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyBwdNnf7f4cEjbZrpHH5rRM
Novos: 1 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.SemJobOnTool_Label_MostraOQueIdentificaABq_PelaReferencia`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_NaPaginaBoquilhas_GravaEMostraOMovimento`

Selo depois: `919f42fd259d5817`

## A-0051 · 2026-10-22 · ok-do-diogo

Porquê: 0.3.0-beta.6: Definições num só sítio (menu à esquerda, Pessoal, cadeado do Geral com o PIN, Definições → App com PIN e email do ADMIN), sem interruptores dos módulos, Canal Jobon no Admin, «BETA» centrado; testes da faixa «Em desenvolvimento» e do PIN certo depois de 4 erros (Diogo, 22/10/2026)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy9UbGxhgGjCLkfcC67zhxwF cmsg_01A27e9ozcpoh4jdPRsuJbFyBwdNnf7f4cEjbZrpHH5rRM
Novos: 23 · mudaram: 59 · saíram: 8

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AFaixaEmDesenvolvimento_SoNosModulosEmDesenvolvimento`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.APalavraPasse_ComAAtualErrada_AvisaENaoMudaNada`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.APalavraPasse_MudaComAAtualCerta`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.APastaDosPdfs_GravaSeParaTodos_ComAPermissao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.ASDefinicoes_EstaoNaBarraDeCima_EJaNaoNaBarraDosModulos`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AsPaginasAntigas_AbremAEntradaDoModulo`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AsPastas_SaoGerais_SoAsAcoesFicamParaTodos`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.ComOPin_OGeralEditaSe_SemCadeado_EVoltaAEntrada`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_CriaOPinNasDefinicoesApp_SoOHashFicaGuardado`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_VeTodosOsModulos_EEdita`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OCadeado_AbreComOPin_AteSair`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OCadeado_Depois4PinsErrados_OCertoAindaAbre`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OCadeado_DepoisDe5PinsErrados_Espera`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OCadeado_SemPin_PedeAoAdminParaOCriar`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OMenu_PessoalEOsModulosAtivos_CadaEntradaMostraSoASuaSeccao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OMenu_TemOCadeadoNosModulos_QuandoOGeralESoDeLeitura`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OModoEscuro_GuardaSeSoParaQuemOMuda`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Pessoal_SoTemAContaAPalavraPasseEAAparencia`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.SemAPermissao_OGeralLeSeComCadeado_ENaoGrava`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.UmModuloInativo_NaoTemSeccao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPin.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.Setup_MudouSeParaAsDefinicoes_App`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Admin_SeestTheDefinicoesTab_EncostadoADireita`
- mudou: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Definicoes_Admin_NaoTemOCartaoSap`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminAuthorizationPolicyTests.cs :: AdminAuthorizationPolicyTests.AdminPages_AdminEmailSettingRemoved_RevokesAdminCookie`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminAuthorizationPolicyTests.cs :: AdminAuthorizationPolicyTests.AdminPages_ConfiguredInstallation_AdminSessionIsAllowed`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.Admin_DefinicoesPlaceholdersAreLinked_AndNewPagesAreDressedWithTheAdminShell`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.ComAChave_RegistoMovimentosEDefinicoes`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.Definicoes_CriarEMudarReparadores`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.AsFuncoes_RegistoDepoisMovimentos_SemHistorico`
- mudou: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs :: AuthenticationTests.AdminHierarchy_AfterActivation_RequiresAdmin`
- mudou: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs :: AuthenticationTests.AdminLogin_Success_RedirectsToAdminArea`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.EmailDoAdminComConta.cs :: BqRepairerSettingsPageTests.AdminEmailSetting_EmailDeUmUtilizador_GravaEEsseEmailEntraComoAdmin`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.EmailDoAdminComConta.cs :: BqRepairerSettingsPageTests.AdminEmailSetting_EmailSemConta_NaoGravaEAvisaComLinkParaCriarOUtilizador`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.EmailDoAdminComConta.cs :: BqRepairerSettingsPageTests.AdminEmailSetting_OProprioEmailComOutrasMaiusculas_Grava`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.AdminEmailSetting_ConfirmationMismatch_KeepsTheFieldAndTheSession`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.AdminEmailSetting_InvalidEmail_IsTypedRefusalAndKeepsTheField`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.AdminEmailSetting_SaveUpdatesTheFieldAndPreviousSessionDies`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.BoquilhasDefinicoes_IsATabOfTheModule_AndOnlyForWhoHasIt`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.CreateRepairer_Post_PersistsTheEntity_AndListsIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_CriarEMudarUmModelo_ERemetenteQueNaoEEmailNaoGrava`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_CriarLista_GuardaOsEmails_EAvisaDosQueNaoSaoEmails`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_ListaSemNenhumEmail_OuComNomeRepetido_NaoGrava`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_MudarOsEmailsNaLinha_GravaEDevolveOValor`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_SemListaOuSemModelo_VoltaAoResumo_SemEmailNemRegisto_EDizPorque`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Enviar_SoApareceComPeso_ESemModelosAvisaComLinkParaAsDefinicoes`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CmDeParte.cs :: PesoComparacaoPageTests.CompararPesos_ComADensidadeMudadaNasDefinicoes_AvisaNaoComparavel_ComLink_SemRecusar`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.ControloCriar_VeAsDuasDensidades_EGuardarMudaAsDuas`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.ForaDoHabitual_GravaComAviso`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.NaoENumeroMaiorQueZero_NenhumaGrava`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs :: ControloDefinicoesPageTests.AdminDefinicoes_NoLongerCarryTheControloSettings`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs :: ControloDefinicoesPageTests.ControloCriar_SeesThePdfFolderAndTheNorm_InItsOwnTab`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs :: ControloDefinicoesPageTests.OutsideTheUsual_IsSavedWithAWarning`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs :: ControloDefinicoesPageTests.Save_CreatesThenUpdatesTheSingleRow`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs :: ControloDefinicoesPageTests.WithoutControloCriar_TheTabDoesNotOpenAndNothingIsSaved`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs :: ControloResumoPageTests.SecondaryNav_GroupsTheSheetsUnderFuncoes`
- mudou: `tests/DMO.Alpha.Web.Tests/CustomWebApplicationFactory.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_AAjudaDoSapAbreAPaginaDeAjuda`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.ORelatorioDeFabrico_MostraOQueCadaJobOnDaFerramentaProduziu`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.AListaDoSap_GravaOUsoQueAFichaDaFerramentaMostra`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.UmFicheiroQueNaoEALista_DizQueNadaFoiGravado`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_OnlyForWhoHasTheModule_AndLeftTheAdmin`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_TotalsGrid_SavesWithGuardar`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_BoquilhasDefinicoes_ReparadoresERotasEmZonas_UmaMensagemNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_ControloDefinicoes_ToleranciaNumaZona_APastaDosPdfsFica`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_FerramentasDefinicoes_QuadroNumaZona_OAvisoVaiParaOCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_SetupECanalJobon_CartaoNumaZona_MensagemNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_SeguemAsSeccoesDaLinha_GuardadasNasDefinicoes`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTexts_LiveInTheTip_NotWrittenOnTheScreen`
- mudou: `tests/visual/definicoes_parcial_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.Estado_FiltraSoOsModulosDesligados`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.LigadoParaTodos_EntramTodosOsQueTemOModulo`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.LigadoSoParaTeste_EntraQuemEDeTesteEVeAFaixa`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.PorOmissao_OsModulosEmDesenvolvimentoFicamDesligados`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.SemAChave_ArmazemERiFicamDesligados`
- saiu: `tests/DMO.Alpha.Web.Tests/Admin/ModuleEntryInterruptoresTests.cs :: ModuleEntryInterruptoresTests.Setup_MostraOsInterruptoresComOGuardarNoTopo`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.Setup_KeepsOnlyTheAdminEmail`

Selo depois: `ea4c2dbad41571a1`

## A-0052 · 2026-10-22 · ok-do-diogo

Porquê: Movimentos do Armazém guardam Linha, Reparador e Job On (com o dia da troca); texto curto «Entrada 0042»/«Saída 0042» e sem «Já estava no armazém» (pedido do Diogo); lista de migrações com a nova
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy2eZhwhwphmpA9yAbNiXzTu, cmsg_01A27e9ozcpoh4jdPRsuJbFyJsDWKL1MtvYHWYYYJPUT7d
Novos: 18 · mudaram: 7 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.Entrada_ComALinhaEOReparadorDoFormulario_GuardaOQueAPessoaDeixou`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.Entrada_ComOReparadorApagadoNoFormulario_FicaSemReparador`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.HistoriaDoJobOn_OReparadorCorrigidoNoRegresso_Manda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.HistoriaDoJobOn_RIEIdasAReparacao_PeloJobOnGuardadoEPelasDatasNasDeAntes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.OJobOnSegueAFerramenta_DaProducaoAReparacaoEDeVolta_ComOReparadorDaSaida`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.SaidaParaProducao_AntesDeAProducaoComecar_LigaAoProximoJobOnDaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.SaidaParaProducao_ComUmaLinhaQueNaoExiste_GravaSemLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.SaidaParaProducao_DepoisDeOsJobOnsAcabarem_GuardaSoALinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.SaidaParaProducao_GuardaALinhaEOJobOnDessaFerramentaNessaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.SaidaParaProducao_NoDiaDaTroca_LigaAoJobOnQueEstaACorrerNesseDia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.SaidaParaProducao_NoutraLinhaSemJobOn_GuardaSoALinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs :: ArmazemLinhaReparadorJobOnTests.Situacao_ForaPelaReparacao_PropoeALinhaEOReparadorComQueVem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs :: HistoriaCenario.JobOn_QuemReparouCadaFerramenta_PeloJobOnGuardadoNoMovimento`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLinhaEReparadorPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLinhaEReparadorPaginaTests.cs :: ArmazemLinhaEReparadorPaginaTests.Entrada_DaReparacao_MostraOReparadorEALinhaComQueVem_EGravaOQueAPessoaMudou`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLinhaEReparadorPaginaTests.cs :: ArmazemLinhaEReparadorPaginaTests.FichaDeUmaBq_NaoTemASeccaoDaCm`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLinhaEReparadorPaginaTests.cs :: ArmazemLinhaEReparadorPaginaTests.SaidaParaProducao_PedeALinhaDepoisDoDestino_EGravaOJobOn`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.AFerramentaOMovimentoEODestino`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.APaginacao_VinteECincoPorPagina`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.SemFiltro_DoMaisRecenteParaOMaisAntigo`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Situacao_NoArmazem_MostraAPosicaoOEstadoEOsUltimosMovimentos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemHistorico.cs :: PostgresMigrationIntegrationTests.Armazem_OHistoricoFiltraNoPostgreSql`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Entrada_PeloFormulario_PoeNaPosicaoEPassaASaida`

Selo depois: `aabe4ebe61e8fd49`

## A-0047 · 2026-10-22 · contagem-lista

Porquê: Ecrã pequeno (teste de uso real de 22/10/2026): os dois testes só passam a exportar o HTML da folha para o Chromium (ExportarParaOBrowser), sem mudar o que verificam; o CI corre o novo tests/visual/ecra_pequeno_browser.py.
Novos: 1 · mudaram: 4 · saíram: 0

- novo: `tests/visual/ecra_pequeno_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.PostMeasurement_CreatesRegistro_PRG_BackToShowWithBackendCalculations`
- mudou: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs :: PesoToolScopedPageTests.ToolScopedFlow_CreateRecordConsult_ThroughThePage`

Selo depois: `76c4b78f9b5bff97`

## A-0049 · 2026-10-22 · ok-do-diogo

Porquê: Diogo aprovou a Boquilhas ver (ok para tudo)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyBwdNnf7f4cEjbZrpHH5rRM
Novos: 5 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.SoVer.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.SoVer.cs :: BoquilhasRegistoPageTests.O_painel_de_quem_so_ve_abre_os_movimentos_da_producao_escolhida_e_so_esses`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.SoVer.cs :: BoquilhasRegistoPageTests.Quem_cria_nas_Boquilhas_continua_a_registar_e_a_abrir_os_registos`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.SoVer.cs :: BoquilhasRegistoPageTests.Quem_so_ve_as_Boquilhas_e_recusado_ao_registar_e_nada_fica_gravado`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.SoVer.cs :: BoquilhasRegistoPageTests.Quem_so_ve_as_Boquilhas_ve_os_registos_no_Historico_sem_formularios_de_registo`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.DetailedMovements_AreATableWithTheHumanFacts_AndTheDoubleClickOpens`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoOpcoesTests.cs :: BoquilhasRegistoPageTests.Registo_LinkHistorico_AbreOHistoricoFiltradoPorEstaBqELote_ComCadaReferenciaTotal`

Selo depois: `9619ddd13d4b61e1`

## A-0053 · 2026-10-23 · ok-do-diogo

Porquê: Histórico de cada ferramenta e página Histórico das Ferramentas (tabela única, testes do Guardião); separadores «Registar Reparação» e «Pesquisar» (pedido do Diogo); teste da RI deixa de depender da hora (sementes do dia operacional; mesmas verificações)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFySf77yPB56WfLMogMFxpVjS, cmsg_01A27e9ozcpoh4jdPRsuJbFy1J4L47FJGZPpVSWAoFSZNR, cmsg_01A27e9ozcpoh4jdPRsuJbFyRTc4GbKqSyKguBgVdi92LW
Novos: 30 · mudaram: 5 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.ABq_MostraSoASuaParteDaReferencia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.AProcuraPelaLinhaEPelosDias_SemReferencia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.AProcuraPelaReferenciaEPeloTipo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.AcimaDoLimite_AProcuraFicaCortada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.CadaFerramenta_TemASuaPercentagemDeUso_NaProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.NaBq_OsMfDeCadaJobOnPeloBqContext`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.NoCm_UmaLinhaPorJobOn_ComOUsoEAsOutrasFerramentas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.OsDias_ContamODiaDoInicio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.OsDias_IncluemOsLimites_DoInicioEDoFim`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.OsDias_SemFim_ContamAteHoje`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.SemFiltro_AProcuraFicaVazia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.UmaFerramentaQueNaoExiste_NaoTemHistorico`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.BarraDasFerramentas_PesquisarERegistarReparacao_SemOsNomesAntigos`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.Ficha_TemOBotaoHistorico_QueAbreOsJobOnDaFerramenta`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.HistoricoDasFerramentas_AbreVazio_EProcuraPelaReferencia`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.HistoricoDasFerramentas_AcimaDoLimite_MostraOAviso`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.Historico_ComControloERi_OMenuTemOsTresDestinos`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.Historico_SemAChaveDasFerramentas_NaoAbre`
- novo: `tests/DMO.Alpha.Web.Tests/LoteTextoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/LoteTextoTests.cs :: LoteTextoTests.OLote_SemZerosAEsquerda`
- novo: `tests/DMO.Alpha.Web.Tests/LoteTextoTests.cs :: LoteTextoTests.ONomeDoHistorico_UsaOLoteSemZero`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.OPainel_LogoDepoisDaMeiaNoite_ORegistoDoDiaOperacional`
- novo: `tests/ShellProof/historico-menu.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/historico-menu.test.cjs :: abrir outra linha fecha a primeira`
- novo: `tests/ShellProof/historico-menu.test.cjs :: clicar na linha abre o menu com os três destinos desse Job On`
- novo: `tests/ShellProof/historico-menu.test.cjs :: o script da página foi encontrado`
- novo: `tests/ShellProof/historico-menu.test.cjs :: um clique fora da tabela fecha o menu`
- novo: `tests/ShellProof/historico-menu.test.cjs :: um segundo clique na linha fecha o menu`
- mudou: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.Ferramentas_VeOSeparador_AProcuraAbreVazia_EAssociaAUmaListaNova`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.OPainel_BAntesDeC_ORegistoSoDestaPessoa_EditarNoCanto`
- mudou: `tests/ShellProof/package.json`

Selo depois: `c3c2038d14994c36`

## A-0054 · 2026-10-23 · ok-do-diogo

Porquê: O Diogo aprovou («perfeito», «ok»): /Admin só com Utilizadores e Templates; Canal Jobon e App num grupo «Admin» nas Definições (só ADMIN); Controlo no painel lateral e um botão por cartão; PU NNPB só B1 e C3 (as outras linhas ficam como estão, a C2 continua com 8 posições); círculo de ajuda de 9px com o topo na altura das maiúsculas (±1,5 px, medido no browser); um só triângulo de recolher (.dmo-caret) à esquerda, a .25em do texto, medido no browser; o grupo do menu lateral abre e fecha com um clique; modo escuro só nas Definições; cabeçalho com o utilizador e, por baixo, «Admin» e «Sair». Pedido do Guardião: repostas as verificações do Admin/Templates.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy8Q2gT3FH9nxyd16FyYaewB
Novos: 4 · mudaram: 25 · saíram: 2

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Controlo_FuncoesNoMenuLateral_UmBotaoPorCartao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.PuNnpb_UmGuardarParaB1EC3_AsOutrasLinhasFicamComoEstao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.Admin_TemDefinicoesProprias_CanalJobonEApp`
- novo: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.Caret_UmaRegraParaTudo_AEsquerdaDoTexto`
- mudou: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Admin_SeestTheDefinicoesTab_EncostadoADireita`
- mudou: `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs :: DefinicoesNavTabTests.Definicoes_Admin_NaoTemOCartaoSap`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminAuthorizationPolicyTests.cs :: AdminAuthorizationPolicyTests.AdminPages_ConfiguredInstallation_AdminSessionIsAllowed`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.Admin_DefinicoesPlaceholdersAreLinked_AndNewPagesAreDressedWithTheAdminShell`
- mudou: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs :: AuthenticationTests.AdminHierarchy_AfterActivation_RequiresAdmin`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs :: ControloDefinicoesPageTests.AdminDefinicoes_NoLongerCarryTheControloSettings`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs :: ControloResumoPageTests.SecondaryNav_GroupsTheSheetsUnderFuncoes`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_CriaOPinNasDefinicoesApp_SoOHashFicaGuardado`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OCadeado_SemPin_PedeAoAdminParaOCriar`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OMenu_PessoalEOsModulosAtivos_CadaEntradaMostraSoASuaSeccao`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.CanalJobon_SavesTheDayChangeHour_ForTheAdminOnly`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_OnlyForWhoHasTheModule_AndLeftTheAdmin`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_BoquilhasDefinicoes_ReparadoresERotasEmZonas_UmaMensagemNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_ControloDefinicoes_ToleranciaNumaZona_APastaDosPdfsFica`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_SetupECanalJobon_CartaoNumaZona_MensagemNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasFichaTrocaTests.cs :: FerramentasPagesTests.Ficha_BotoesDeCadaCard_NoCantoSuperiorDireito`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.PostCreate_WithTechnicalValues_SavesThemOnTheNewTool_AndReturnsToTheFlow`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Editar_MostraAsFamiliasAosPares_ComAQtdStockEAQtdMaqEmCadaFamiliaENaCal`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_SeguemAsSeccoesDaLinha_GuardadasNasDefinicoes`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.AdminTemplatesECanal_AChaveEAHoraDaReparacaoInterna`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial`
- mudou: `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs :: UiStyleRuleTests.DarkMode_RedefinesTheTokens_AndNoSheetPaintsLooseWhite`
- mudou: `tests/visual/definicoes_parcial_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.Setup_MudouSeParaAsDefinicoes_App`
- saiu: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.Caret_SoParaRecolher_ApontaParaAPalavraEAbertoParaBaixo`

Selo depois: `45baba8c0122ac1a`

## A-0055 · 2026-10-23 · ok-do-diogo

Porquê: Folha nova do Job On (A-0055): testes da folha bloqueada, do Editar, do «Job On ver» (POST forjado não muda nada), das Opções com Controlo, Reparação e Boquilhas, das produções e do CsFurosTexto; dois testes do Admin passam a usar jobon.edit como chave desconhecida, porque jobon.view passou a existir.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyRTc4GbKqSyKguBgVdi92LW, cmsg_01A27e9ozcpoh4jdPRsuJbFy93maEKN2ZCLoyGMNntC5BN
Novos: 19 · mudaram: 4 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CsFurosTextoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CsFurosTextoTests.cs :: CsFurosTextoTests.Escrever`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CsFurosTextoTests.cs :: CsFurosTextoTests.Ler`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_Editar_DesbloqueiaEGuardarVoltaAFolha_SemApagarOQueNaoMudou`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_JobOnVer_VeBloqueadaSemEditar_EOPlaneamentoLevaAFolha`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_OpcoesBoquilhas_SoComBoquilhas_AbreOsMovimentosDaBQDesteJobOn`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_OpcoesControlo_ComControlo_AparecemControloAntesDaReparacao`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_ProducoesAnteriores_NoDropdown_AbremAFolhaDeCada`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_SemReferenciaMfMaisBq_SoAvisa`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Bloqueada: um clique não faz nada; o duplo clique leva à ficha`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: CS furos no Editar: um dígito de abertos e outro de fechados vão para o campo como «Abertos 4 / Fechados 2»`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Editar: escolher outra ferramenta muda o cartão e a ficha do duplo clique`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Editar: um clique abre a procura; o duplo clique leva à ficha sem abrir a procura`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Imprimir: abre a informação técnica e chama window.print(); o gancho dmoJobOnImprimir substitui-o`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: O painel das linhas: «Linhas» abre, o × esconde, e a escolha fica no browser`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Produções: lista fechada; abrir cai por cima; escolher outra muda a <select> e dispara change`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/TemplateAdminServiceTests.cs :: TemplateAdminServiceTests.CreateTemplate_InvariantViolations_AreTypedRefusals`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.AdminTemplates_ZeroCapabilitiesAllowed_NonBetaVocabularyRefused`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/package.json`

Selo depois: `22a5bbe559777eb6`

## A-0056 · 2026-10-23 · ok-do-diogo

Porquê: Reparadores de CM e MF: sem o «Recebe» ao criar (o Diogo); o reparador novo recebe CM e MF e o teste passa a criar sem escolher.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyCHtQM4phYkHQ6fUzkVEKMv
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.Definicoes_CriarEMudarReparadores`

Selo depois: `f8014bd20313ba06`

## A-0057 · 2026-10-23 · ok-do-diogo

Porquê: Job On ver: quem só vê não tem Imprimir nem Opções (o Diogo); só vê a folha e abre ou fecha as secções.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyFYtHKazbRHUoLDGDAbesVv, cmsg_01A27e9ozcpoh4jdPRsuJbFyLFFLDNwAj7EmL6hfwiKkSw
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_JobOnVer_VeBloqueadaSemEditar_EOPlaneamentoLevaAFolha`

Selo depois: `d96a7f2ee41e164c`

## A-0058 · 2026-10-23 · ok-do-diogo

Porquê: Registar Reparação sem as frases «Nenhuma lista ativa…» e «Escolha um filtro e procure.» (o Diogo: «remover comentários»); o teste prova que não aparecem e que a lista nova se chama «reparador · dd/mm/aaaa» (o Diogo: «nova lista fica com que nome?»).
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyQd8YbYN5Lfe9Y35h2uvui6, cmsg_01A27e9ozcpoh4jdPRsuJbFyEYj4jAdmPBR3GuBoG7dKj6, cmsg_01A27e9ozcpoh4jdPRsuJbFyJf9MUgTsdJTXN3CZeLafvu
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.Ferramentas_VeOSeparador_AProcuraAbreVazia_EAssociaAUmaListaNova`

Selo depois: `e34f8d72d0d0e910`

## A-0060 · 2026-10-23 · contagem-lista

Porquê: PERF-10: testes novos do cancelamento das sugestões; package.json passa a correr o ficheiro novo
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAriKefh54B7BZbzJjE5Emb
Novos: 5 · mudaram: 1 · saíram: 0

- novo: `tests/ShellProof/dmo-suggest-abort.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/dmo-suggest-abort.test.cjs :: escrever depressa cancela o pedido anterior e só o texto atual mostra sugestões`
- novo: `tests/ShellProof/dmo-suggest-abort.test.cjs :: limpar o campo cancela o pedido em voo`
- novo: `tests/ShellProof/dmo-suggest-abort.test.cjs :: sair do campo cancela o pedido em voo`
- novo: `tests/ShellProof/dmo-suggest-abort.test.cjs :: um pedido cancelado não mostra mensagem de erro`
- mudou: `tests/ShellProof/package.json`

Selo depois: `5f9bb3bf3356252d`

## A-0061 · 2026-10-23 · teste-novo

Porquê: PERF-05: fallback das Boquilhas escolhido na base; resultado igual e contagem de linhas
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAriKefh54B7BZbzJjE5Emb
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasCorrente.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasCorrente.cs :: ConsultaDasCandidatas.BoquilhasCorrente_NaoTrazTodasAsSemDatasParaAMemoria`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasCorrente.cs :: ConsultaDasCandidatas.BoquilhasCorrente_NoPostgres_EscolheAsMesmasProducoes`

Selo depois: `58e4cf95052cd365`

## A-0062 · 2026-10-23 · teste-novo

Porquê: PERF-01: medir pedidos completos
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAriKefh54B7BZbzJjE5Emb
Novos: 4 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/MedicaoPedidosTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/MedicaoPedidosTests.cs :: MedicaoPedidosTests.Desligada_NaoPoeOCabecalho_NemALinha_EONoStoreFica`
- novo: `tests/DMO.Alpha.Web.Tests/MedicaoPedidosTests.cs :: MedicaoPedidosTests.NoPostgres_ONumeroDeConsultasBateComOsComandos_EOParametroNaoVaiParaOLog`
- novo: `tests/DMO.Alpha.Web.Tests/MedicaoPedidosTests.cs :: MedicaoPedidosTests.PaginaAutenticada_TemServerTiming_EOsEstaticosNao_EOLogNaoLevaValores`

Selo depois: `d47cbaa35324c42d`

## A-0072 · 2026-10-23 · teste-novo

Porquê: PERF-01: ramo de erro regista -> 500 (nota do Guardião)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAriKefh54B7BZbzJjE5Emb
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/FornecedorDeAutenticacaoQueFalha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/MedicaoPedidosTests.cs :: MedicaoPedidosTests.PedidoQueLancaExcecao_ALinhaDizQuinhentos`

Selo depois: `33dd2100ff4eeb26`

## A-0059 · 2026-10-23 · ok-do-diogo

Porquê: Sumário: dados técnicos em falta para quem tem Controlo criar (Peso e Pegamentos precisam deles); % de uso para Ferramentas; lote novo por criar; grupo Importante no topo até resolver; links; ponto; sem duplicados (A-0059)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyPoQF4rYmk1aqAzjBow6N1m
Novos: 14 · mudaram: 8 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.BqApagados_FicamNoTopoSeteDias`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.CadaLinha_TemLink_ParaUmaPaginaQueAContaAbre`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.Importante_SoOsQuePedemAcao`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.NaoAprovado_FicaAteSerAprovado`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.OPonto_ApareceComImportantesPorVer_EDesapareceAoAbrirOSumario`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.OPonto_ContaOsLotesNovosPorCriar_ParaQuemTemFerramentas`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.OPonto_NaoApareceSoComInformacao`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.OsImportantes_FicamNumGrupoNoTopo_AInformacaoPorBaixo`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.QuemVe_DadosEmFaltaComOControloCriar_UsoELotesComAsFerramentas`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.UmImportanteDeOutroDia_FicaAteResolver_EDepoisSai`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.UsoAtualizado_FicaAteHaverALeituraDoFim_SoQuemTemFerramentas`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.AMesmaAlteracao_NoMesmoMinuto_FicaNumaSoLinha_EOutraContinua`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.ToolsWithMissingTechnicalData_AreForControloCreate_NotForFerramentas`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnChangeTests.cs :: JobOnChangeTests.Update_RecordsOneLinePerChangedField_WithHumanValuesAndWho`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs :: ArmazemLoteNovoPaginaTests.Sumario_AvisaSoQuemTemFerramentas_ECriarResolveEDaAEntrada`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Resumo_SemOModulo_ALinhaFicaSoComoInformacao`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.Changes_AreListed_AlsoBeforeTheStart_AndTheDaysFilterReachesBack`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.ControloInformation_IsForEitherControloModule`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.DeletedBqMovements_AreOnlyForBoquilhas`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.UtilizacaoPorAtualizar_AvisaNoSumario_SoQuemTemFerramentas`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPostgresTests.cs :: PlaneamentoSumarioPostgresTests.TodosOsTipos_NoPostgres`
- saiu: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.ToolsWithMissingTechnicalData_AreOnlyForFerramentas`

Selo depois: `1a1003013dd1bbee`

## A-0073 · 2026-10-23 · ok-do-diogo

Porquê: Armazém: o Registo começa por Entrada | Saída, que filtra o que pode entrar e sair; a escolha vem antes da Posição e da Referência (A-0073)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFySoxoY816HBEaSQf6CywFGN
Novos: 4 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemModoEntradaSaidaPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemModoEntradaSaidaPaginaTests.cs :: ArmazemModoEntradaSaidaPaginaTests.Entrada_MostraSoAsDeFora_ComOLoteNovo_EAbreAEntrada`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemModoEntradaSaidaPaginaTests.cs :: ArmazemModoEntradaSaidaPaginaTests.Saida_MostraSoAsDasPosicoes_SemLoteNovo_EAPosicaoProcuraOQueLaEsta`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemModoEntradaSaidaPaginaTests.cs :: ArmazemModoEntradaSaidaPaginaTests.SemModo_EAEntrada_EOModoSegueNaProcura`

Selo depois: `ff1839e76e72ab4c`

## A-0063 · 2026-10-23 · ok-do-diogo

Porquê: Lote de interface aprovado pelo Diogo (06:11). Pedidos: cmsg_01A27e9ozcpoh4jdPRsuJbFy6rLgP521oC99oR5byhRkDm (Pegamentos sem folha em branco), cmsg_01A27e9ozcpoh4jdPRsuJbFySqjDpVw7BPpYM4AFah3cqt (número até 3 algarismos), cmsg_01A27e9ozcpoh4jdPRsuJbFyF3oHFEm7oGxuXL9oNakayW (título Reparação), cmsg_01A27e9ozcpoh4jdPRsuJbFyEjinZZrxBuxnqgR2Y5TBbn (punção repetida), cmsg_01A27e9ozcpoh4jdPRsuJbFySnJZd4dRs6kx9PdDMuA1zi (cabeçalho só com Sair, substitui a verificação do #54)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyB1ubohd4RHovJaSj5pTEv4
Novos: 14 · mudaram: 55 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Armazem_SemCadeado_ESemTextoComAListaVazia`
- novo: `tests/DMO.Alpha.Web.Tests/DmoNumberFormatTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DmoNumberFormatTests.cs :: DmoNumberFormatTests.Decimal2_SemZerosNumInteiro_DuasCasasComDecimais`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.Pdf_WithoutMeasurements_NoButton_AndPostIsRefused_WithoutWriting`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Repetida.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Repetida.cs :: PuncoesPageTests.MesmoNumeroEMesmoLote_ERecusada_ComAMensagem_EOutroLoteGrava`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.TabelaNoBrowser.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.TabelaNoBrowser.cs :: PuncoesPageTests.Tabela_TemLargurasFixas_EVaiParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.CorrigirComNumeroQueNaoESoAlgarismosAte3_Recusa_ENadaMuda`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.ONumero_TresAlgarismos_Grava_EOCampoSoAceitaAlgarismos`
- novo: `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs :: UiStyleRuleTests.LinksNosMiniCartoes_TemACorDoValor_SemSublinhado_NemNoHover`
- novo: `tests/visual/ajuda_junto_browser.py`
- novo: `tests/visual/painel_lateral_browser.py`
- novo: `tests/visual/puncoes_tabela_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryPdfGeneratorTests.cs :: BqHistoryPdfGeneratorTests.DailyMatrix_ComMovimentosSemMaquina_AvisaNoPapel`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryPdfGeneratorTests.cs :: BqHistoryPdfGeneratorTests.GroupedHistory_PreservesEveryMovementIncludingUnknownMachineAndSeparateLots`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.RepeatedClicks_StartTheRegisterAndRegisterThePunchOnlyOnce`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.WithoutJobOn_RegisterStandsAlone_ListedByReference`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.AdminTemplates_MostraAChaveDoArmazem`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.DetailedMovementsView_ListsOneMovementPerLine_WithoutGroupHeaders_AndItsPdf`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.FilterCounters_AreQuantitySums_NotStockBalances`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.HistoryDay_ReplacesRangePreservesFiltersAndExportsTheSameSelection`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.History_Filters_ChangeOnlyTheZones`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.History_MonthOnly_ListsThatMonthsMovements`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.History_OpensBlank_UntilAReferenceOrAPeriodIsSearched`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.MachineFilterWithoutPeriod_ShowsOnlyTheMostRecent_AndTheListAndPdfAgree`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.ViewDropdown_AndContextualPdfButton_AreWiredToTheGenerators`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoOpcoesTests.cs :: BoquilhasRegistoPageTests.Registo_LinkHistorico_AbreOHistoricoFiltradoPorEstaBqELote_ComCadaReferenciaTotal`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs :: BoquilhasRegistoPageTests.OpeningAMachine_WarnsAboutTheUnresolvedTraceOfTheSameBq`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.Folha_OsFormulariosLevamAComparacaoEOSupomos_ERecusaDoSupomosNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.PesoAbertoPeloHistorico_ComparaNaFolhaESupoeCaloteSemGravar`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_GravaSemRecarregar_ZonasTrocadasELeituraNovaQueFica`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PropoeAMaisRecente_GuardaOPesoDoSap_EMostraODelta`
- mudou: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.Ferramentas_VeOSeparador_AProcuraAbreVazia_EAssociaAUmaListaNova`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Planeamento_CriarJobOn_FicaNaBarraDaProcura_ADireitaDasCaixas_ComODia`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Folhas.cs :: PegamentosPageTests.CmChanged_PageIsReadyForANewSheet_TheFirstReadingStartsIt_AndThePageSaysSo`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Folhas.cs :: PegamentosPageTests.EscolhaDoJobOn_DepoisDeTrocarACm_AbreAFolhaDasFerramentasAtuais`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Folhas.cs :: PegamentosPageTests.OldSheet_OpensWithItsTools_NoNewReadingRow_CorrectionReturnsToIt_PdfNamesFollowTheSheet`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Legenda.cs :: PegamentosPageTests.MapaDeLimites_ALegendaMostraAsCoresDoDesenho`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.GetFolha_ControloId_ShowsInheritedContext_GargaloMissing_MfAlwaysInherited`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.HistoricalToolFacts_RemainOnReadingCardAndPdf_AfterCatalogEdit`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.PostMeasurement_MfInProduction_RegistersWithInFlowGargaloCompletion`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.PostMeasurement_SingleAxis_OvalizacaoUndefined_MediaEqualsCostura`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.RegistarLeitura_ZonasPedidas_DesenhaSoAsZonas_SemLerAsFerramentas`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.FolhaDasPuncoes_LigadaDoResumoEDoRegisto_SoComRegisto_EOPdfDoControloTrazODasPuncoes`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_A1aD12_Reserva_RepetidosComLote_ETotais_NaZonaDaFolha`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_SeguemAsSeccoesDaLinha_GuardadasNasDefinicoes`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs :: PuncoesPageTests.NnpbProduction_RegistersPunches_ShowsAlteracao_TotalsResumoAndPdf`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ODuploCliqueAbreAReparacao_ComCorrigirEAnular`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_CorrigeONumero_ComRasto_EOMesmoEnvioAplicaUmaVez`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.UmNumeroLongo_Recusa_ENadaMuda`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: ReparacaoInternaHistoricoPaginaTests.CorrigirNoHistorico_VoltaAoHistoricoComOsFiltros`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: ReparacaoInternaHistoricoPaginaTests.MudarODia_RegistaNesseDia_ESoAvisa`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: ReparacaoInternaHistoricoPaginaTests.OHistorico_AbreVazio_SemFiltroDeQuem_EMostraSoAsDestaPessoa`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.GravarComOutraCm_AvisaQueFaltaAssociar_EOCampoVoltaVazioEDesmarcado`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.AdminTemplatesECanal_AChaveEAHoraDaReparacaoInterna`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EliminarAProducao_AvisaQueSePerdemAsReparacoes`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EliminarJobOnComReparacoes_AJanelaDizQuantasSePerdem`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EscreverONumeroEEnter_Grava_OCampoVoltaVazio_EALinhaApareceNoTopo`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.UmaRecusa_VoltaComONumeroEscrito_ENadaSeGrava`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial`
- mudou: `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs :: UiStyleRuleTests.DarkMode_RedefinesTheTokens_AndNoSheetPaintsLooseWhite`
- mudou: `tests/visual/definicoes_parcial_browser.py`
- mudou: `tests/visual/ecra_pequeno_browser.py`
- mudou: `tests/visual/nok_agente_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.Pdf_WithoutMeasurements_DownloadsBlankSheet_WithoutWriting`

Selo depois: `7e4bced974268ae6`

## A-0075 · 2026-10-23 · ok-do-diogo

Porquê: Job On: a folha nova em todo o lado; Planeamento abre a folha numa página; Criar e Duplicar na folha em Editar; ficha antiga sai; o onde está no Armazém e o aviso sem registo passam para a folha (FER-E-03, nada se perde); sem títulos dos grupos; TP Diâmetro
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy2K1Lvbfz3uREZWHLw2fG5Z, cmsg_01A27e9ozcpoh4jdPRsuJbFyX1cpKvWD5T4iNh6m2fxmk8, cmsg_01A27e9ozcpoh4jdPRsuJbFyVj2pwZirzCvvjyAJSrtbZw, cmsg_01A27e9ozcpoh4jdPRsuJbFyYcQC3nnmqGaiZPRBg32T17, cmsg_01A27e9ozcpoh4jdPRsuJbFyAx6Epq4SUz2pLfR2jqH3uP, cmsg_01A27e9ozcpoh4jdPRsuJbFyAyKSbk4nLWfmjr4m9CBWPn, cmsg_01A27e9ozcpoh4jdPRsuJbFyE7tiP6DoaPx4TJH65FxhpT
Novos: 12 · mudaram: 22 · saíram: 4

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Folha_ACalMostraAsLinhasGravadas_ComAQtdMaq_NaFolhaNova`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Folha_MostraOsCamposGravados_NaFolhaNovaBloqueada`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Folha_SemNadaEscrito_MostraOsVaziosEOEditar_SemAFichaAntiga`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Criar_AbreVaziaEmEditar_EGuardaPeloCreateEVoltaAFolha`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Criar_NumeroRepetido_ERecusadoTipado_ESoAvisaAsDatasTrocadas`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Criar_SemCmBqOuMf_ERecusado_AFolhaVoltaComOQueSeEscreveu_ENadaEGravado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Duplicar_CopiaOsDados_SemDatasNemNumero_ComODiaDoCalendario_EGravaUmaProducaoNova`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_JobOnVer_NaoCriaNemDuplica_SoVe`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_TP_ODiametroSemPS_ECampoIgual`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.Planeamento_EscolherUmaProducao_AbreAFolhaNova_SemAFichaAntiga`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Ficha_ProducoesDaReferencia_SaiComAFichaAntiga_AFolhaTemODropdown`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnEliminarTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Editar_RegistaOQueMudou_EAFichaMostraAAcao`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_AcoesEFactos_AoUsoDoHtml`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_AbremAFichaDaFerramenta_SemAvisoDeDadosEmFalta`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_MostramOUsoDaUltimaLeitura`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_OndeEstaNoArmazem_EAvisoSemRegisto`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_SemColocacaoManualNemRetirarDoPlaneamento`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Opcoes_EditarPrimeiro_SemDuplicarLote_EFecharVoltaAFicha`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.PrepararProducao_SoAoCriar_EAMaquinaVaziaAbreACriacaoComDiaEMaquina`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ProducoesDoDia_MostramAProximaMudancaEmCadaMaquina`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.CreateFlow_ExplicitSelections_EstablishesQueryableContext`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.EditFlow_ReplansTheSameProduction_WindowLineAndTools`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Historico_Mostra25ProducoesPorPagina_MaisRecentesPrimeiro`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Planeamento_CriarJobOn_FicaNaBarraDaProcura_ADireitaDasCaixas_ComODia`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Procura_EmDoisCampos_AReferenciaEncheODropdownDasProducoes`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs :: PlaneamentoSumarioPageTests.Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.EliminarJobOnComReparacoes_AJanelaDizQuantasSePerdem`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Ficha_ACalMostraAsLinhasGravadas_ComAQtdMaq`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Ficha_MostraSoAsFamiliasEOsCamposEscritos_ComAQtdStockEAQtdMaq`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Ficha_SemNadaEscritoNaFolha_DizSoIssoComOEditar`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Ficha_Opcoes_ProducoesDaReferencia_TabelaPaginadaSoQuandoSeAbre`

Selo depois: `99995dcb276a8d80`

## A-0076 · 2026-10-23 · ok-do-diogo

Porquê: Pesquisar ferramentas: Referência primeiro, «Linha», «Por página» junto da paginação (Diogo 23/10)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyLMarZyqR8Ep4yGP6vNwa3g, cmsg_01A27e9ozcpoh4jdPRsuJbFyWEszyPXLLpKWk5nPM2SGoS, cmsg_01A27e9ozcpoh4jdPRsuJbFyViwxtVYh2GuyQND76gdr9K
Novos: 0 · mudaram: 2 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: FerramentasPagesTests.Criar_APartirDoCartaoDoJobOn_OTipoFicaFixo`
- mudou: `tests/visual/paginacao_parcial_browser.py`

Selo depois: `74948639fa47de50`

## A-0074 · 2026-10-23 · ok-do-diogo

Porquê: Hora na Reparação Interna, preenchida com a hora atual e editável; rasto fixo também nas horas; limites 00:00, 23:59 e 24:00; nova migração na lista
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAdjqLNEgQ9Y4VgMMMXYG4N
Novos: 17 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaHora.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaHora.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaHora_ORastoNaoMudaAsHoras`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasHoraTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasHoraTests.cs :: ReparacoesInternasHoraTests.Corrigir_SemHora_DeixaAHora_ENumeroEHoraIguais_NadaMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasHoraTests.cs :: ReparacoesInternasHoraTests.Corrigir_SoAHora_MudaAHora_ComRasto_EONumeroFica`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasHoraTests.cs :: ReparacoesInternasHoraTests.Registar_ComAHoraMudada_GravaAEscrita`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasHoraTests.cs :: ReparacoesInternasHoraTests.Registar_SemHora_GravaAHoraLocalDeAgora_AoMinuto_EORegistadoEmFicaIgual`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasHoraTests.cs :: ReparacoesInternasHoraTests.UmaDeAntes_SemHora_ListaComHoraNula_ECorrigirDaLheHora`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: ReparacaoInternaHoraPaginaTests.AHoraMudada_FicaGravada_EAparecenoRegisto`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: ReparacaoInternaHoraPaginaTests.Corrigir_ComUmaHoraInvalida_NadaMuda`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: ReparacaoInternaHoraPaginaTests.Corrigir_MudaAHora_ComRasto`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: ReparacaoInternaHoraPaginaTests.OCampoHora_AoLadoDoDia_VemComAHoraDeAgora_EGravaSeComOsMinutos`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: ReparacaoInternaHoraPaginaTests.OsLimitesDaHora_DaMeiaNoiteAo2359_E24Recusa`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: ReparacaoInternaHoraPaginaTests.UmaDeAntes_SemHora_MostraAHoraDoRegisto`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs :: ReparacaoInternaHoraPaginaTests.UmaHoraInvalida_Recusa_ComMensagem_ENadaSeGrava`
- novo: `tests/visual/ri_hora_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.ALista_DestaMaquinaEProducao_DaMaisRecente_EmPaginasDe25`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_CorrigeONumero_ComRasto_EOMesmoEnvioAplicaUmaVez`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs :: ReparacaoInternaHistoricoPaginaTests.CorrigirNoHistorico_VoltaAoHistoricoComOsFiltros`

Selo depois: `76537f95ac3f30e1`

## A-0078 · 2026-10-23 · ok-do-diogo

Porquê: Folha do Job On (primeira fila, notas, Processo, Modo Editar, imagem, cabeçalho, Dias na linha do Início), resumo das alterações no fim da folha sem o aviso amarelo na lista do dia, e remover por engano (Diogo 23/10)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy6eUpe6BnLNwfd19yFYYkoX, cmsg_01A27e9ozcpoh4jdPRsuJbFy4me2Hy8H4GGhtRH8r8ABnD, cmsg_01A27e9ozcpoh4jdPRsuJbFyCEQjg5VC9S8WCqR94sTGb9, cmsg_01A27e9ozcpoh4jdPRsuJbFyLeuZcWmha6UGYYqqE86j2F, cmsg_01A27e9ozcpoh4jdPRsuJbFyNmPuTERBdjo1amXaDtngmf, cmsg_01A27e9ozcpoh4jdPRsuJbFyM2andZbUn9D3FdwWyMFgh1
Novos: 12 · mudaram: 4 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnChangeTests.cs :: JobOnChangeTests.RemoveByMistake_KeepsTheRow_MarksWhoAndWhen_HidesItFromTheMarkerAndNotices_ValuesUnchanged`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Dias_NaLinhaDoInicio_FimMenosInicioMaisUm`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Editar_ModoEditarNoLugarDaProcura_EOProcessoNnpbPs_ComODaCmSugerido`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Folha_PrimeiraFila_CmMfBq_NotasEmTodosOsCartoes_RotulosSemFichaDeMoldes`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.SoVer_NaoTemEditar_NemModoEditar_MesmoPedindoOEditar`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.QuemSoVeOJobOn_NaoVeAJanela_EUmPostForjadoNaoMudaNada`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.RemoverAUltimaAtiva_AMarcaSai_AFolhaContinuaAMostrarAsRemovidas_EUmSegundoEnvioNaoMuda`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.Remover_SaiDaMarcaEDoSumario_FicaRiscadaComQuemEQuando_EOsValoresNaoMudam`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.UmaAlteracaoDeOutroJobOn_NaoSeRemove`
- novo: `tests/visual/jobon_folha_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Editar_RegistaOQueMudou_EAFichaMostraAAcao`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.OQueMudou_SoAsAlteracoesFeitasDepoisDoInicio`

Selo depois: `8caa9df1fe235bb8`

## A-0077 · 2026-10-23 · ok-do-diogo

Porquê: Pacote de interface aprovado pelo Diogo: painel lateral com as cores do Planeamento, «Voltar ao Job On», % uso no Criar, círculos de ajuda iguais, «Por página» junto da paginação
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy4sDMLbGy5vRjHMGnQ33SSe
Novos: 11 · mudaram: 8 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.cs :: BoquilhasRegistoPageTests.ToolCatalog_PorPagina_NaPaginacao_NuncaNosFiltros`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoNoCriar.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoNoCriar.cs :: FerramentasPagesTests.Criar_PercentagemDeUsoIlegivel_NaoGrava_EAvisa`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoNoCriar.cs :: FerramentasPagesTests.Criar_PercentagemDeUso_GravaAPrimeiraLeitura_SoAvisos`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VoltarJobOn.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VoltarJobOn.cs :: FerramentasPagesTests.Ficha_ComVoltarDoJobOn_MostraOVoltarParaAFolhaExata`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VoltarJobOn.cs :: FerramentasPagesTests.Ficha_SemVoltarValido_NaoMostraOVoltarAoJobOn`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelCores.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelCores.cs :: JobOnPageTests.FolhaNova_PainelLateral_CadaCartaoLevaACorDaSuaLinha`
- novo: `tests/visual/painel_cores_linhas_browser.py`
- novo: `tests/visual/tecnica_alinhada_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_AbremAFichaDaFerramenta_SemAvisoDeDadosEmFalta`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial`
- mudou: `tests/ShellProof/jobon-folha.test.cjs :: Editar: escolher outra ferramenta muda o cartão e a ficha do duplo clique`
- mudou: `tests/visual/ajuda_junto_browser.py`
- mudou: `tests/visual/definicoes_parcial_browser.py`

Selo depois: `64b5fd86806ae05e`

## A-0079 · 2026-10-23 · ok-do-diogo

Porquê: Cor leve a 100% de uso (aprovada pelo Diogo), sem aviso a ≥100 no Editar; «Registar ferramenta» sem «real» (cmsg_01A27e9ozcpoh4jdPRsuJbFyX2adtku5aR57VcmvT5faWQ)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyBDuxhn9W8xLtDxJ4KkoCkn
Novos: 2 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoCheio.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoCheio.cs :: FerramentasPagesTests.UsoCheio_SoMudaACor_NaFichaENoPesquisar_NuncaAvisaNemBloqueia`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Ficha_PercentagemDeUso_NoEditar_GravaComOGuardar_SoAvisos`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: FerramentasPagesTests.Criar_APartirDoCartaoDoJobOn_OTipoFicaFixo`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: FerramentasPagesTests.GetIndex_ShowsHumanFactsList_ToolIdOnlyInUrl_NeverAField`

Selo depois: `e4001b1e522a0ccf`

## A-0080 · 2026-10-23 · ok-do-diogo

Porquê: Folha do Job On: ordem dos cartões (TP e CS, Calibres, PI à direita), CM/MF/BQ numa fila a 1366 e 1280, sem esticar com a Informação técnica aberta, produção numa linha, e linha vazia abre a folha em branco para duplicar ou criar com a linha e o dia (Diogo 23/10)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyR9DLoJRShGmbTL76USNiBk, cmsg_01A27e9ozcpoh4jdPRsuJbFySZCHF2SYMQvqF5oC5ivr8C, cmsg_01A27e9ozcpoh4jdPRsuJbFy6p8hzdVZKJQNmZvY3owES7, cmsg_01A27e9ozcpoh4jdPRsuJbFy7pCVHGx9xRydNF3aQSC1ms, cmsg_01A27e9ozcpoh4jdPRsuJbFyVRtpXnEHc2wm7aHQrAtbhJ, cmsg_01A27e9ozcpoh4jdPRsuJbFyRUM1ouKf6v9y7Lv1c816xB
Novos: 1 · mudaram: 2 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.PrepararProducao_AMaquinaVaziaAbreAFolhaEmBranco_ParaDuplicarOuCriar_ComDiaEMaquina`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Folha_PrimeiraFila_CmMfBq_NotasEmTodosOsCartoes_RotulosSemFichaDeMoldes`
- mudou: `tests/visual/jobon_folha_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.PrepararProducao_SoAoCriar_EAMaquinaVaziaAbreACriacaoComDiaEMaquina`

Selo depois: `e7b31ffc3658f539`

## A-0082 · 2026-10-23 · ok-do-diogo

Porquê: O Diogo pediu que a alteração removida por engano se apague por completo, para não juntar lixo; os testes passam a verificar que a linha desaparece.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyS6ySBbPmD2UR76TUjZLB2N, cmsg_01A27e9ozcpoh4jdPRsuJbFy8V5jRJ6Vyt7H3A32Ud2CoD
Novos: 1 · mudaram: 7 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.RemoverAUltima_AMarcaSai_OQueMudouDesaparece_EUmSegundoEnvioNaoFalha`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnChangeTests.cs :: JobOnChangeTests.RemoveByMistake_KeepsTheRow_MarksWhoAndWhen_HidesItFromTheMarkerAndNotices_ValuesUnchanged`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.QuemSoVeOJobOn_NaoVeAJanela_EUmPostForjadoNaoMudaNada`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.Remover_SaiDaMarcaEDoSumario_FicaRiscadaComQuemEQuando_EOsValoresNaoMudam`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.UmaAlteracaoDeOutroJobOn_NaoSeRemove`
- mudou: `tests/visual/jobon_folha_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs :: JobOnRemoverAlteracaoTests.RemoverAUltimaAtiva_AMarcaSai_AFolhaContinuaAMostrarAsRemovidas_EUmSegundoEnvioNaoMuda`

Selo depois: `32a264ffb223c884`

## A-0081 · 2026-10-23 · ok-do-diogo

Porquê: Listas de email: tabela Nome | Email, listas minimizáveis, colar do Outlook; nome de lista existente junta a pessoa (pedido do Diogo cmsg_01A27e9ozcpoh4jdPRsuJbFyYXh1fnoybcdjcLEghEQiQc); email já na lista avisa e não duplica
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyW4qmvhz9g3JgigBx2NddUm
Novos: 6 · mudaram: 1 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_ColarComUmEmailErrado_AvisaENaoOGrava`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_ColarListaDoOutlook_GuardaNomesEEmails`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_JuntarAUmaLista_UmEmailQueJaLaEsta_NaoDuplica_EAvisa`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_ListaSemNenhumEmail_NaoGrava_EONomeDeUmaQueExisteJuntaAPessoa`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_ListasDeAntes_SemNomes_MostramSeFechadasComOTraco`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_PessoaDaLista_MudarNomeEEmail_Tirar_EJuntar`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_CriarLista_GuardaOsEmails_EAvisaDosQueNaoSaoEmails`
- saiu: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_ListaSemNenhumEmail_OuComNomeRepetido_NaoGrava`

Selo depois: `65cddedf2800e31e`

## A-0083 · 2026-10-23 · ok-do-diogo

Porquê: Templates: criar um segundo template já não grava por cima do primeiro; Ferramentas e Armazém juntos na secção Gestão; a densidade do vidro grava com um só valor
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyNkoBTrMhAR3VewhBFWsKgJ cmsg_01A27e9ozcpoh4jdPRsuJbFyJk91Dmz4xRbnKyYghwkojw cmsg_01A27e9ozcpoh4jdPRsuJbFy4nkxmWrrwW4tabZAcvT81m
Novos: 9 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.Gestao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.Gestao.cs :: AdminUsersAndTemplatesPagesTests.AdminTemplates_FerramentasEArmazem_NaSeccaoGestao`
- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.NovaTemplate.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.NovaTemplate.cs :: AdminUsersAndTemplatesPagesTests.AdminTemplates_CriarDuasSeguidas_FicamDuas_APrimeiraNaoMuda`
- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.NovaTemplate.cs :: AdminUsersAndTemplatesPagesTests.AdminTemplates_GuardarAAberta_AtualizaEssa`
- novo: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.OsDoisVazios_NadaMuda_ComAviso`
- novo: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.SoNnpb_Grava_PsFicaComoEstava`
- novo: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.SoPs_Grava_NnpbFicaComoEstava`
- novo: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.UmInvalido_OOutroGrava_ComAviso`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.AdminTemplates_MostraAChaveDoArmazem`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs :: ControloDefinicoesDensidadeTests.NaoENumeroMaiorQueZero_NenhumaGrava`

Selo depois: `14d84e20af09ab60`

## A-0084 · 2026-10-23 · ok-do-diogo

Porquê: O Diogo disse que as ferramentas existem mas o Job On não as mostra (a lista deixa de esconder as que não estão ligadas à linha, com aviso) e que a máquina vazia abre logo o Job On vazio para escrever referência e produção.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyVweZF5BqmFCDP3QHKq7FjK, cmsg_01A27e9ozcpoh4jdPRsuJbFyToPe3xxvGaCQmMVePiptBA
Novos: 1 · mudaram: 1 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.PrepararProducao_AMaquinaVaziaAbreOJobOnVazio_ComDiaEMaquina`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.ToolCandidates_Mf_ShowsOnlyTheMfOfTheChosenCm_AndTheMachine`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.PrepararProducao_AMaquinaVaziaAbreAFolhaEmBranco_ParaDuplicarOuCriar_ComDiaEMaquina`

Selo depois: `eeea5e50049f15e3`

## A-0085 · 2026-10-23 · ok-do-diogo

Porquê: O Diogo pediu que o Job On novo abra com o template vazio, sem Editar, só com Referência e Produção; por isso ?novo=1 abre esse template (quem só vê continua sem forma de criar) e o Criar passa a etapa=criar; e o Peso mostra no máximo duas casas, sem zeros no fim.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyDVmzwNXmJ27Y9wujPVctWi, cmsg_01A27e9ozcpoh4jdPRsuJbFy7ptiGBcxeK1STv81feVGJJ, cmsg_01A27e9ozcpoh4jdPRsuJbFy5bGHNtwLZnbmHdtmqXcSce
Novos: 2 · mudaram: 5 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Peso_DuasCasasDecimaisNoMaximo_SemZerosNoFim`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_AbreComOTemplateSemEditar_SoReferenciaEProducao_EDepoisCria`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.PrepararProducao_AMaquinaVaziaAbreOJobOnVazio_ComDiaEMaquina`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Criar_AbreVaziaEmEditar_EGuardaPeloCreateEVoltaAFolha`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Criar_NumeroRepetido_ERecusadoTipado_ESoAvisaAsDatasTrocadas`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Criar_SemCmBqOuMf_ERecusado_AFolhaVoltaComOQueSeEscreveu_ENadaEGravado`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_JobOnVer_NaoCriaNemDuplica_SoVe`

Selo depois: `2f13253978d003d5`

## A-0086 · 2026-10-23 · ok-do-diogo

Porquê: A01: o PDF e o anexo do Peso só mostram a aprovação que cobre esse Peso. A02: a Reparação Interna separa «registar» (ri) de «ver» (ri.view, a página Consultar dos registos para a chefia). Os 11 testes mudados acrescentam ri.view aos utilizadores de consulta porque quem só tem ri deixa de abrir o Consultar. CI local OK, guarda OK; regressão provada (ParteAsync de volta: 3 falham; chave ri no Consultar: 4 falham).
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy6rzgTJY4GZEkr9rGWXFVHW cmsg_01A27e9ozcpoh4jdPRsuJbFy2q3RzcQZhibvbAjWKqHtBi cmsg_01A27e9ozcpoh4jdPRsuJbFy1hsBucu2Xnx9BvmUL7uLWt
Novos: 12 · mudaram: 11 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: PesoAprovacaoDocumentoTests.AprovacaoSoCobrePesosRegistadosAteAoInstanteDaDecisao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: PesoAprovacaoDocumentoTests.NaoAprovarMantemSeNoPdfDepoisDeUmPesoNovo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: PesoAprovacaoDocumentoTests.OAnexoDoEmailTambemFicaPorAprovar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: PesoAprovacaoDocumentoTests.PesoNovoNaoHerdaAAprovacaoNoPdf_ENoHistoricoAPrimeiraFolhaContinuaAprovada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: PesoAprovacaoDocumentoTests.ReaprovarDepoisDoPesoNovoAtualizaOPdf`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: PesoAprovacaoDocumentoTests.UmPesoDeOutroControloOuInexistenteNaoRecebeADecisao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs :: PesoAprovacaoDocumentoTests.UmaLeituraNovaNaoInvalidaAAprovacaoDaFolha`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.AChaveAntigaSoPermiteRegistar_EConsultarExigeAChaveDosResponsaveis`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.QuemSoConsultaPodeProcurarAsReferencias`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.RetirarAConsultaRecusaOFormularioDeAssociacaoJaAberto_SemAlterarORegisto`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.RetirarORegistoMantendoAConsultaRecusaOFormularioDeRegistoJaAberto`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.AProducaoEmDoisPassos_AReferenciaDepoisAsUltimas5`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.AbreVazia_SoComOsFiltros`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.AsPessoasEOsDias_Filtram`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.ComOJobOn_AProducaoAbreAFicha`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.DepoisDeFiltrar_AsReparacoes_SemBotoes_ENuncaONomeTecnico`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.Filtrar_SoMudamAsZonas`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.OsDoisSeparadores_RegistarEConsultar`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.AFerramentaMostrada_LevaAFicha_SoComAsFerramentas_AEscritaFicaTexto`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_AJanelaAssociar_GravaUmaVez_EALinhaFicaComAFerramenta`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_SemFerramentaQueSirva_AJanelaAvisaComOLinkParaRegistar`

Selo depois: `c1072eb40d05b45e`

## A-0087 · 2026-10-23 · teste-novo

Porquê: A05: duas correções da mesma leitura dos Pegamentos ao mesmo tempo gravavam 10→11 e 10→12; com o trinco da folha o histórico fica em cadeia
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy5RDFe9ayKRzmBAFe1ejsru
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosCorrigir.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosCorrigir.cs :: PostgresMigrationIntegrationTests.Pegamentos_DuasCorrecoesAoMesmoTempo_OHistoricoFicaEmCadeia`

Selo depois: `8bd45e1e8bfd79c4`

## A-0088 · 2026-10-23 · teste-novo

Porquê: A04: duas devoluções ao mesmo tempo fecham a lista da Programada e não se associa a uma lista que está a fechar; A08: o mesmo envio (Boquilhas sem Job On, criar lista da Programada) grava uma só vez
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy2osxiK3ZUCa4WJNsdkGvTg
Novos: 7 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProgramadaConcorrente.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProgramadaConcorrente.cs :: PostgresMigrationIntegrationTests.Programada_AdicionarEnquantoAUltimaFerramentaVolta_NaoAcrescentaAUmaListaFechada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProgramadaConcorrente.cs :: PostgresMigrationIntegrationTests.Programada_AsDuasUltimasFerramentasVoltamAoMesmoTempo_AListaFecha`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOn.EnvioUnico.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOn.EnvioUnico.cs :: BoquilhasRegistoPageTests.SemJobOn_OMesmoEnvioGravaUmaVez_OutraChavePermiteOutroMovimento`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaEnvioUnicoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaEnvioUnicoTests.cs :: ReparacaoProgramadaEnvioUnicoTests.CriarLista_OMesmoEnvioCriaUmaSo_OutraChaveCriaOutraLista`

Selo depois: `d9f3f5bde5c67ac0`

## A-0089 · 2026-10-23 · ok-do-diogo

Porquê: A07: o mesmo pedido de email (X-Dmo-Envio) devolve o mesmo ficheiro e regista um só envio. A06: uma aba antiga das listas de email (versão diferente) recebe Conflito e não grava. Os testes antigos do email só recebem o campo versao; o inventário de migrações passa a 42
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyYSQ4sP37o7nTgtUsn2mB9L
Novos: 9 · mudaram: 5 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EmailListas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EmailListas.cs :: PostgresMigrationIntegrationTests.EmailLista_UmaGravacaoConcorrenteNaoApagaAAlteracaoDeOutraPessoa`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs :: EnvioEmailPesoPageTests.Email_OMesmoEnvioDevolveOMesmoFicheiroERegistaUmaVez_OutraChavePreparaOutro`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs :: EnvioEmailPesoPageTests.Email_SemEspacoParaNovosFicheiros_NaoPreparaOutro_MasPermiteRepetirOPrimeiro`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs :: EnvioEmailPesoPageTests.Lista_RepetirARemocaoNaoRetiraAPessoaQuePassouParaAMesmaPosicao`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs :: EnvioEmailPesoPageTests.Lista_SemAVersaoDoFormularioNaoGrava`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs :: EnvioEmailPesoPageTests.Lista_UmFormularioAntigoNaoSubstituiAAlteracaoDeOutraAba`
- novo: `tests/ShellProof/troca-parcial.test.cjs :: A06: editar atualiza a versão dos formulários da mesma lista e isola as outras listas`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_MudarOsEmailsNaLinha_GravaEDevolveOValor`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_PessoaDaLista_MudarNomeEEmail_Tirar_EJuntar`
- mudou: `tests/ShellProof/troca-parcial.test.cjs :: (resto do ficheiro)`

Selo depois: `f1d73210fa39da9b`

## A-0090 · 2026-10-23 · teste-novo

Porquê: PR1 do Peso (outra ferramenta, ramo draft/peso-pr1-extraction): o carregamento da folha do Peso passa para PesoFolhaQuery; testes do serviço, do Postgres (só SELECT, orçamento de consultas) e da página
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy7fMDaiggYKqjtZpaBVbvjH
Novos: 17 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.AntesDoPrimeiroPeso_MostraFactosDaProducaoECandidatosSemCriarNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.CaloteSimulada_MudaSoAApresentacaoSemRelerTecnicos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.CancelarOCarregamento_PropagaOCancelamentoSemGravar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.ControloInexistente_DevolveAFolhaVaziaSemInventarContexto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.DepoisDaTroca_SemPesoAtual_NaoAbreOUltimoENaoAssociaSozinho`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.IdentificadorNaoCanonico_MantemORecursoAQueryDoApoio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.PesoAnteriorEscolhido_MantemValoresCongeladosEUsaASuaCmNoApoio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.PesoDeOutraProducao_NaoSubstituiOPesoDestaProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.SemEscolhaValida_AbreOPesoAtualDaProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs :: PesoFolhaQueryTests.TecnicosEmFalta_NaoImpedemAbrirAFolhaNemInventamValores`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoFolha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoFolha.cs :: PostgresMigrationIntegrationTests.PesoFolha_CarregamentoSoLeSemAumentarConsultas`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaCarregamentoPageTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaCarregamentoPageTests.cs :: PesoFolhaCarregamentoPageTests.AbrirProducao_MostraCriacaoEAssociacaoSemGravar`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaCarregamentoPageTests.cs :: PesoFolhaCarregamentoPageTests.RecusaDeCriacao_PreservaATemperaturaEscritaEAMensagem`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaCarregamentoPageTests.cs :: PesoFolhaCarregamentoPageTests.SemSessaoOuSemModulo_NaoExecutaOCarregamentoDaFolha`

Selo depois: `aa3a46108bd59cc6`

## A-0091 · 2026-10-25 · ok-do-diogo

Porquê: O Diogo pediu o PU na primeira fila e os Calibres na horizontal ao lado do CS, com o TP e o CS mais estreitos (o teste do browser espera os Calibres ao lado do CS), os Dias outra vez ao centro entre as barras (o teste dos Dias deixa de esperar a linha vazia), e que a sobreposição só se aplique às BQ: os testes que esperavam aviso, marca vermelha ou banner entre produções da mesma linha passam a esperar que não haja, e a calculadora continua a distinguir a véspera do dia da troca.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyMLJyVMvSHt8nCnfDQJj14J, cmsg_01A27e9ozcpoh4jdPRsuJbFy5CvC5vHd3UG6j4st2ydTvC, cmsg_01A27e9ozcpoh4jdPRsuJbFyNGXdPSjqk8Vph85X5iBU3N, cmsg_01A27e9ozcpoh4jdPRsuJbFyL3Zdukw69fdMxwmGuyEqvb, cmsg_01A27e9ozcpoh4jdPRsuJbFyBMNxZJTp39f529kPPQ3h2V, cmsg_01A27e9ozcpoh4jdPRsuJbFyC7FrQwjiFkEemquds2jsub, cmsg_01A27e9ozcpoh4jdPRsuJbFyEcpedrzXjuZLgaunpXbMky
Novos: 13 · mudaram: 3 · saíram: 13

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Overlap.cs :: PostgresMigrationIntegrationTests.Overlap_FilteredInSql_NuncaAvisaEntreProducoesDaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Overlap.cs :: PostgresMigrationIntegrationTests.Overlap_NoDiaDaTroca_ENoDiaAntes_NuncaAvisam`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapQueryTests.cs :: JobOnOverlapQueryTests.EveryWindow_NeverWarnsBetweenProductionsOfTheSameLine_InMemory`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_AcabaNoDiaEmQueAOutraComeca_GravaSemAviso_NaVesperaNaoCruza`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_ComecaNoDiaEmQueAOutraAcaba_GravaSemAviso_NoDiaSeguinteNaoCruza`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_OpenEndAgainstOpenStart_SavesWithoutWarning`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_WithOverlapOnSameMachine_SavesWithoutWarning`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Update_WithOverlapOnSameMachine_SavesWithoutWarning`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ProducoesDoDia_DuasNaMesmaMaquina_NaoMarcamSobreposicao`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ProducoesDoDia_LinhaNuncaFicaVermelha_PorProducoesQueSeCruzam`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Update_SemAvisoDeSobreposicaoEntreProducoesDaLinha_ENuncaBloqueiaAGravacao`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Dias_AoCentro_FimMenosInicioMaisUm`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.CreateFlow_WithOverlapOnSameMachine_SavesWithoutBanner`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapQueryTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: (resto do ficheiro)`
- mudou: `tests/visual/jobon_folha_browser.py`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Overlap.cs :: PostgresMigrationIntegrationTests.Overlap_FilteredInSql_GivesTheSameWarningAsComparingEveryProduction`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Overlap.cs :: PostgresMigrationIntegrationTests.Overlap_NoDiaDaTroca_NaoAvisa_ENoDiaAntesAvisa`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapQueryTests.cs :: JobOnOverlapQueryTests.EveryWindow_GivesTheSameWarningAsComparingEveryProduction_InMemory`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_AcabaNoDiaEmQueAOutraComeca_AvisaEGrava_NaVesperaNao`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_ComecaNoDiaEmQueAOutraAcaba_AvisaEGrava_NoDiaSeguinteNao`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_OpenEndAgainstOpenStart_TheHonestOverlapText`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Create_WithOverlapOnSameMachine_SavesAndWarnsInDetail`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs :: JobOnOverlapWarningTests.Update_WithOverlapOnSameMachine_SavesAndWarnsInDetail`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ProducoesDoDia_DuasNaMesmaMaquina_MarcaASobreposicao`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.ProducoesDoDia_LinhaVermelha_SoComSobreposicaoReal`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Update_OverlapWarningsReflectKnownDates_AndNeverBlockSaving`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Dias_NaLinhaDoInicio_FimMenosInicioMaisUm`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.CreateFlow_WithOverlapOnSameMachine_StaysOnJobOnWithTheYellowBanner`

Selo depois: `c20f63ff70a5681e`

## A-0092 · 2026-10-25 · teste-novo

Porquê: Reshape do Peso, PR2: os 4 acessos diretos à base da página passam para PesoEscolhasQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyJGqu987Ms3tuZqAaDhS7mh
Novos: 8 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs :: PesoEscolhasQueryTests.CodigosDeMaquinas_VemPorOrdemDoCodigo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs :: PesoEscolhasQueryTests.ComparacaoGuardada_SoExisteComOParDeIdentificadoresCerto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs :: PesoEscolhasQueryTests.ControloDaProducao_DevolveOControloDessaProducaoESemControloDevolveNulo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs :: PesoEscolhasQueryTests.OsQuatroPedidos_NaoGravamNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs :: PesoEscolhasQueryTests.ProducaoDoControlo_DevolveAProducaoEControloInexistenteDevolveNulo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoEscolhas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoEscolhas.cs :: PostgresMigrationIntegrationTests.PesoEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura`

Selo depois: `4aa1a9dc98189376`

## A-0093 · 2026-10-25 · contagem-lista

Porquê: Tampão da produção no Job On e no Peso (Diogo 23/10): 17 testes novos (apoio do Peso, Job On, congelamento no Peso); três testes de esquema ganham só as colunas e a migração novas; nenhuma verificação sai.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy7KQihN8Vng87o3q7BE8DiM
Novos: 17 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoTampaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoTampaoTests.cs :: CreateToolScopedPesoTampaoTests.ComJobOn_CongelaOTampaoDoJobOn`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoTampaoTests.cs :: CreateToolScopedPesoTampaoTests.SemJobOn_CongelaOQueACmTemDeArranque`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoTampaoTests.cs :: CreateToolScopedPesoTampaoTests.SemTampaoEmLadoNenhum_PesoCriaSeComOTampaoVazio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs :: PesoTampaoDoJobOnTests.DiametroDoJobOn_MandaSobreODaCm`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs :: PesoTampaoDoJobOnTests.FromKnown_UsaODiametroDoJobOn`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs :: PesoTampaoDoJobOnTests.JobOnSemDiametro_CaiNoDaCmDeArranque`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs :: PesoTampaoDoJobOnTests.SemTampaoEmLado_Nenhum_ReferenciaPorCompletarSemBloquear`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs :: PesoTampaoDoJobOnTests.SemTampaoNaCm_OJobOnBasta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs :: JobOnTampaoTests.Create_GravaODiametro`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs :: JobOnTampaoTests.Create_ZeroOuNegativo_RecusaSemEscrita`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs :: JobOnTampaoTests.Update_ComOCampo_GravaERegistaAMudanca`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs :: JobOnTampaoTests.Update_EnviadoVazio_LimpaOTampao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs :: JobOnTampaoTests.Update_SemOCampo_NaoLimpaOTampao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs :: JobOnTampaoTests.Update_Zero_RecusaSemMexerNoFacto`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.ControloEntity_JobOnDoesNotGainControloNavigationOrColumn`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.PesoEntity_HasNoProductionIdentityColumns`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `512faa69805daf54`

## A-0094 · 2026-10-25 · teste-novo

Porquê: Reshape do Peso, PR3: produções da referência, base e contra-moldes da comparação passam para PesoComparacaoEstadoQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyJGqu987Ms3tuZqAaDhS7mh
Novos: 14 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.Base_ComLeituras_CalculaAInfluenciaDaBasePelaOrdemDasPosicoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.Base_ComPares_CalculaAInfluenciaDasNovasContraAMediaDaBase`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.Base_Inexistente_DevolveBaseNulaSemInfluencias`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.Base_SemLeituras_MostraAPesoMasSemInfluencias`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.ContraMoldes_AReferenciaDoInicialVemPrimeiroDepoisPorReferencia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.OsTresPedidos_NaoGravamNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.ProducaoAntigaAlemDasUltimasCinco_FicaNoSelectEEscolhida`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.ProducaoPedidaDeOutraReferencia_NaoFicaEscolhida`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.ProducaoPedida_SemReferenciaEscrita_UsaAReferenciaDela`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.ReferenciaEscritaSemProducoes_DizQueNaoHaProducoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs :: PesoComparacaoEstadoQueryTests.SemReferenciaNemProducaoPedida_DevolveOSelectVazio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoComparacaoEstado.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoComparacaoEstado.cs :: PostgresMigrationIntegrationTests.PesoComparacaoEstado_CadaLeituraSoLeSemAumentarConsultas`

Selo depois: `911671ed878ed020`

## A-0095 · 2026-10-25 · teste-novo

Porquê: Reshape do Job On, J1: os acessos diretos à base de JobOn/Index.cshtml.cs passam para JobOnPaginaQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyJGqu987Ms3tuZqAaDhS7mh
Novos: 11 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnPagina.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnPagina.cs :: PostgresMigrationIntegrationTests.JobOnPagina_CadaPedidoFazUmaSoConsultaDeLeitura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.AvisoDeSobreposicao_EntreProducoesDaMesmaLinhaNaoAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.AvisoDeSobreposicao_ProducaoInexistenteNaoDaAviso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.CaloteECodigoDaMaquina_LeemOGuardadoEDevolvemNuloSeNaoExiste`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.OsPedidos_NaoGravamNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.ProducaoComReferenciaENumero_DevolveFerramentasEDatas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.ProducaoDoHistorico_ComVariasReferenciasDoMesmoInicio_EscolheAMaisRecente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.ProducaoDoHistorico_EscolheAMaisRecenteComEsseNumero`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs :: JobOnPaginaQueryTests.ReferenciasDeMf_SoParaAFerramentaDoTipoCerto`

Selo depois: `77e42f1b702b86d7`

## A-0096 · 2026-10-25 · teste-novo

Porquê: Reshape do Job On, J2: o quadro do dia de JobOn/Index.cshtml.cs passa para JobOnQuadroDoDiaQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAmgz6Smt5sJ1FZ7Riri2Hy
Novos: 12 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.QuadroDoDia.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.QuadroDoDia.cs :: PostgresMigrationIntegrationTests.JobOnQuadroDoDia_CadaPedidoFazSoAsConsultasDeLeitura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.DuasProducoesDaMesmaLinhaQueSeCruzam_NaoMarcamSobreposicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.NotasDeLoteDoDiaEResumoDasAlteracoes_ficamNaProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.OPedido_NaoGravaNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.ProducaoAteAviso_CobreOsDiasDepoisDoInicio_MasNaoOsAntes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.ProducaoCobreOPrimeiroEOUltimoDia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.ProximaMudanca_ASeguinteMaisCedoQueATroca_GanhaAProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.ProximaMudanca_EscolheAMaisCedoEntreAProducaoSeguinteEATrocaDeComponente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.SemDiaEscolhido_NaoHaProximaMudanca_MasHaProducoesDoDiaInicial`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.UmaLinhaPorMaquina_NaOrdemDasMaquinas_ComAsProducoesDoDiaPorReferencia`

Selo depois: `dccfc5697c021f5a`

## A-0098 · 2026-10-26 · contagem-lista

Porquê: Verificação antes da apresentação (25/10/2026): dois testes novos. No Job On novo, o cartão vazio passa a mostrar a ferramenta escolhida e o aviso de falta acompanha a escolha. A lista de ferramentas da linha passa a juntar as que não têm linha nenhuma. No jobon-folha.test.cjs, o «resto do ficheiro» só muda pelo «;» do teste acrescentado no fim; nenhum teste existente foi alterado.
Referência: relatório /mnt/project-files/analises/filtros-apresentacao-2026-10-26/relatorio.md
Novos: 2 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateQueryTests.cs :: ToolCandidateQueryTests.MachineFilter_IncluirSemLinha_JuntaAsSemLinha_NuncaAsDeOutraLinha`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Editar: no cartão vazio, escolher a CM mostra a referência e o lote e tira a CM do aviso`
- mudou: `tests/ShellProof/jobon-folha.test.cjs :: (resto do ficheiro)`

Selo depois: `11ee7a625f27ba46`

## A-0099 · 2026-10-26 · ok-do-diogo

Porquê: O teste do Sumário «criado hoje, começa depois» punha o início a +5 dias, que no fim do mês cai dentro do mês que o «começam» mostra (falhava desde 26/10). Passa a +40 dias, sempre fora; o que o teste verifica fica igual.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyRTc4GbKqSyKguBgVdi92LW (autorização de testes do Diogo, anexada pelo coordenador a 26/10)
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.AJobOn_IsAnnouncedOnTheDayItWasCreated_NotOnlyOnItsStart`

Selo depois: `d26cb179d634ae6e`

## A-0097 · 2026-10-26 · teste-novo

Porquê: Reshape de Controlo/Pegamentos (B1): os 2 acessos diretos à base de Pegamentos.cshtml.cs passam para PegamentosEscolhasQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy6LLAHPU5WAmH4YiKwfVeei
Novos: 7 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosEscolhasQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosEscolhasQueryTests.cs :: PegamentosEscolhasQueryTests.CodigosDeMaquinas_VemPorOrdemDoCodigo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosEscolhasQueryTests.cs :: PegamentosEscolhasQueryTests.LotesDaReferencia_ReferenciaEmBranco_NaoDevolveNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosEscolhasQueryTests.cs :: PegamentosEscolhasQueryTests.LotesDaReferencia_VemComOLoteMaisAltoPrimeiroSemMisturarTiposNemReferencias`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosEscolhasQueryTests.cs :: PegamentosEscolhasQueryTests.OsDoisPedidos_NaoGravamNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosEscolhas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosEscolhas.cs :: PostgresMigrationIntegrationTests.PegamentosEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura`

Selo depois: `8c61c6519de55957`

## A-0101 · 2026-10-26 · ok-do-diogo

Porquê: O Diogo pediu números com duas casas só quando necessário (inteiros sem .00; com decimais sempre duas, 2.50; ponto decimal) na ficha da ferramenta, na calote e no apoio do tampão do Peso; os testes que esperavam 3 casas (70.362, 9.217, D 50.000) ou uma casa (2.5, 9.5, 70.1) passam a esperar o formato novo (70.36, 9.22, D 50, 2.50, 9.50, 70.10). Nos campos de editar um valor com 3 casas fica igual (0.125). Nenhuma verificação sai; um teste novo da ficha (140.000 → 140).
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyPkQLjid1XkSfzAcW3RZ9Ne, cmsg_01A27e9ozcpoh4jdPRsuJbFyRoekZxLPcSo1TQzEC9J7rY, cmsg_01A27e9ozcpoh4jdPRsuJbFy2EadhQLcRARQPVdSprtcV6
Novos: 3 · mudaram: 13 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Decimais.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Decimais.cs :: FichaDecimaisTests.Decimal2_InteirosSemCasas_DecimaisComDuas`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.Ficha_CamposTecnicos_NoMaximoDuasCasas_SemZerosNoFim`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_PainelDeFactos_MostraVolumesDensidadeEApoioComACaloteDoJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_SemCaloteNoJobOn_UsaAReferenciaDaTool`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.SimulacaoIlegivel_AvisoHonestoSemBloqueio`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.SimulacaoPorGet_RecalculaPeloBackendEAbreOApoio`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ToolScopedMode_ApoioSemProducao_UsaAReferenciaDaTool`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoSoParaComparacaoPageTests.cs :: PesoSoParaComparacaoPageTests.CriarDaComparacao_LigaOPesoAoAtual_MostraAComparacao_ENuncaPedeJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.CaloteEditavel_GravaPelaEdicao`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.CaloteIgnorada_NaEdicao_MantemACaloteAnteriorEAvisa`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.CaloteNaCriacao_PreservaODecimalAoGuardarEReabrir`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.CriarCmNoEditar_VoltaAEdicaoDoMesmoJobOnERegistaATroca`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.DuplicarEsteJobOn_CopiaOsDadosParaUmaProducaoNova`

Selo depois: `269869e315a430fa`

## A-0104 · 2026-10-26 · teste-novo

Porquê: Reshape do Job On, J3: o histórico de JobOn/Index.cshtml.cs passa para JobOnHistoricoQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAmgz6Smt5sJ1FZ7Riri2Hy
Novos: 16 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnHistorico.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnHistorico.cs :: PostgresMigrationIntegrationTests.JobOnHistorico_CadaPedidoFazSoAsConsultasDeLeitura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.DataIlegivel_AvisaEIgnora`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.DesdeDepoisDeAte_TrocaAsDatasEAvisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.MaquinaDesconhecida_NaoFiltra`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.OPedido_NaoGravaNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.OsTresFormatosDeData_SaoAceites`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.Paginacao_UmaPaginaDeCadaVezMaisRecentePrimeiro`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.ProducaoComNumeroEReferencia_EscolheEssaReferencia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.ProducaoDeOutraReferencia_NaoFiltra`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.ProducaoMaisAntigaPedidaPorLink_EntraNoDropdownEFiltra`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.ProducaoSoComNumero_EscolheAMaisRecenteComEsseNumero`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.ReferenciaEMaquina_FiltraDropdownEResultados`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.SemFiltro_NaoProcuraENaoLePagina`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs :: JobOnHistoricoQueryTests.SoReferencia_ListaAsProducoesEPesquisaPelaReferencia`

Selo depois: `1d85ff80026183a9`

## A-0106 · 2026-10-26 · teste-novo

Porquê: PERF-02/09/03/06: Reparação Interna e Histórico das Ferramentas leem menos da base; testes de leituras no Postgres e das zonas da página
Novos: 14 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMapaEstados.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMapaEstados.cs :: PostgresMigrationIntegrationTests.ArmazemMapa_UltimoEstadoNaBase_IgualAoDeMemoria_ELinhasLidasNaoDependemDaHistoria`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoricoLote.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoricoLote.cs :: PostgresMigrationIntegrationTests.HistoricoFerramentas_ProducoesEmLote_IguaisAUmaAUma_ComConsultasFixas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProducoesDoDia.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProducoesDoDia.cs :: PostgresMigrationIntegrationTests.ReparacaoInterna_ProducoesDoDia_NumaSoLeituraComOMesmoResultadoQueUmaPorUma`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: ReparacaoInternaZonasPaginaTests.AAutorizacaoEADaPagina_ComECemOCabecalho`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: ReparacaoInternaZonasPaginaTests.DepoisDeGravar_RegistoEAvisos_SaoOsMesmosPartialsDaPaginaInteira`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: ReparacaoInternaZonasPaginaTests.OHistorico_ZonaDosResultados_ESoAsMinhasReparacoes_SaoOsMesmosPartials`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: ReparacaoInternaZonasPaginaTests.OPainelDasLinhas_PedidoSozinho_ELinhaSemEscolher_SaoOsMesmosPartials`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: ReparacaoInternaZonasPaginaTests.OsPedidosComZonas_LeemMenosDaBaseQueAPaginaInteira_NoPostgres`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: ReparacaoInternaZonasPaginaTests.SemOCabecalho_AResposta_EAPaginaInteiraDeSempre`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs :: ReparacaoInternaZonasPaginaTests.ZonaDesconhecidaOuDeOutraVista_RespondeComAPaginaInteira`

Selo depois: `3b7fb22d41c58d7b`

## A-0102 · 2026-10-26 · teste-novo

Porquê: Reshape de Boquilhas (B2): as leituras de escolha e de filtro de Boquilhas/Index.cshtml.cs passam para BoquilhasEscolhasQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy6LLAHPU5WAmH4YiKwfVeei
Novos: 8 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs :: BoquilhasEscolhasQueryTests.BqSemJobOn_SoAsBqSemATombstone_PorReferenciaELote_ComMaquinasEUltimaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs :: BoquilhasEscolhasQueryTests.BqsDaReferencia_SoBqDaReferenciaSemDistinguirMaiusculasEMisturarTipos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs :: BoquilhasEscolhasQueryTests.Ferramenta_DevolveAReferenciaEOLoteESemFerramentaDevolveNulo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs :: BoquilhasEscolhasQueryTests.OsQuatroPedidos_NaoGravamNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs :: BoquilhasEscolhasQueryTests.ProducaoDoFiltro_DevolveNumeroReferenciaELinhaESemProducaoDevolveNulo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasEscolhas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasEscolhas.cs :: PostgresMigrationIntegrationTests.BoquilhasEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura`

Selo depois: `13c21601efbecfee`

## A-0107 · 2026-10-26 · contagem-lista

Porquê: Job On novo: as listas de CM/MF/BQ filtram-se pelas ferramentas já escolhidas, pela referência total e pela linha, e a referência do Job On novo sugere só as que correram na linha. Testes novos do serviço, do Postgres, da página e do JavaScript; no jobon-folha.test.cjs só se acrescentou um teste no fim (nenhum teste existente mudou)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyXr51rqHqoEPfuXFy99ZQ6F
Novos: 6 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FerramentasRelacionadas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FerramentasRelacionadas.cs :: PostgresMigrationIntegrationTests.FerramentasRelacionadas_OrdemEFiltro_NoPostgres`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Search/SuggestionQueryTests.cs :: SuggestionQueryTests.Referencias_ComLinha_SoAsQueCorreramNela_SemNenhumaTodas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateQueryTests.cs :: ToolCandidateQueryTests.JobOn_Companheiras_OrdenamPrimeiroAsUsadasComElas_ESoRelacionadasTiraAsOutras`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.ToolCandidates_ComCmEscolhida_ABqMostraAsRelacionadas_EEscreverMostraTodas`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Editar: a procura da BQ leva a CM e a MF escolhidas, e a lista mostra a linha e o filtro`
- mudou: `tests/ShellProof/jobon-folha.test.cjs :: (resto do ficheiro)`

Selo depois: `d9e0ee510f9834d7`

## A-0105 · 2026-10-26 · teste-novo

Porquê: Reshape do Job On, J4: as leituras da ficha do Job On aberto de JobOn/Index.cshtml.cs passam para JobOnFichaQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAmgz6Smt5sJ1FZ7Riri2Hy
Novos: 12 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFicha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFicha.cs :: PostgresMigrationIntegrationTests.JobOnFicha_CadaPedidoFazSoAsConsultasDeLeitura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.AsLeiturasSaoPelaOrdemDaPagina_NotasAlteracoesPreviewFactosFolha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.Duplicar_NaoLePreviewNemFactos_MasLeAFolhaQueCopia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.FolhaGravada_Presente_OuAusenteLeSeComoVazia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.JobOnAberto_DevolveNotasAlteracoesPreviewFactosEFolha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.OPedido_NaoGravaNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.OPreviewUsaODiaPedido`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.SemFerramentas_NaoLeFactos_MasLePreview`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.SemNotasNemAlteracoes_ListasVazias`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.SoUmaFerramenta_LeFactosSoDessa`

Selo depois: `5dce71a3ddc81f12`

## A-0103 · 2026-10-26 · ok-do-diogo

Porquê: Definições: o Diogo pediu que o ADMIN só veja as definições do Admin («no admin só deve ter definições importantes»; «as definições da app abrangem os módulos e só mostram os módulos que um user tem») e que quem só tem «Boquilhas · ver» não tenha horas de viragem. O Canal Jobon sai do Admin: a hora das Boquilhas vai para as Definições das Boquilhas e a da Reparação para uma entrada «Reparação», ambas com «Boquilhas · criar». Saem os testes que esperavam o ADMIN a ver os módulos e o Canal Jobon; entram os que provam o contrário. Tira acesso ao ADMIN: ok do Diogo.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyYRyWGv6Y5CabLhDFr1DLwa, cmsg_01A27e9ozcpoh4jdPRsuJbFyQ8WiYwTVK78uuk1DvMwT8X, cmsg_01A27e9ozcpoh4jdPRsuJbFyCVtCLc1FuK1Ug3hF8cdhTG, cmsg_01A27e9ozcpoh4jdPRsuJbFyUJwYKkR1Jw5deUYwen5U5c
Novos: 4 · mudaram: 6 · saíram: 4

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_SoVeAsDefinicoesDoAdmin_ENaoDosModulos`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.Admin_TemDefinicoesProprias_SoAApp`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.HoraDeViragem_CadaUmaNoSeuModulo_ComOsSeusHandlers`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_SetupEReparacao_CartaoNumaZona_MensagemNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs :: BqRepairerSettingsPageTests.EditRepairer_InPlace_AnswersJson_AndTheTableHasNoSaveButton`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OMenu_PessoalEOsModulosAtivos_CadaEntradaMostraSoASuaSeccao`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.AdminTemplatesECanal_AChaveEAHoraDaReparacaoInterna`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTexts_LiveInTheTip_NotWrittenOnTheScreen`
- mudou: `tests/visual/definicoes_parcial_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_VeTodosOsModulos_EEdita`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.Admin_TemDefinicoesProprias_CanalJobonEApp`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.CanalJobon_SavesTheDayChangeHour_ForTheAdminOnly`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_SetupECanalJobon_CartaoNumaZona_MensagemNoCanto`

Selo depois: `f870f0a656abcc8b`

## A-0111 · 2026-10-26 · teste-novo

Porquê: Reshape de Boquilhas (B3a): as leituras da ficha da BQ de Boquilhas/Index.cshtml.cs passam para BoquilhasFichaQuery; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy6LLAHPU5WAmH4YiKwfVeei
Novos: 7 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasFichaQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasFichaQueryTests.cs :: BoquilhasFichaQueryTests.Ferramenta_DevolveReferenciaLoteEQuantidadeESemFerramentaDevolveNulo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasFichaQueryTests.cs :: BoquilhasFichaQueryTests.MaquinasDaFerramenta_VemPorOrdemDoIdDaMaquina_SoDessaFerramenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasFichaQueryTests.cs :: BoquilhasFichaQueryTests.MovimentosSemJobOn_SoDestaBqSemJobOn_MaisRecentesPrimeiro_ComLimite`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasFichaQueryTests.cs :: BoquilhasFichaQueryTests.OsTresPedidos_NaoGravamNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasFicha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasFicha.cs :: PostgresMigrationIntegrationTests.BoquilhasFicha_CadaPedidoFazUmaSoConsultaDeLeitura`

Selo depois: `8f7cf08cf9932c69`

## A-0112 · 2026-10-26 · teste-novo

Porquê: Reshape de Boquilhas (B3b): os registos de uma BQ, a associação a um Job On e o registo pendente passam para BoquilhasAssociacaoQuery; a página Boquilhas já não lê a base; testes do serviço e orçamento de consultas no Postgres
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy6LLAHPU5WAmH4YiKwfVeei
Novos: 10 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: BoquilhasAssociacaoQueryTests.ContextoDaBqNaProducao_SoDaProducaoEDaFerramentaDoRegisto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: BoquilhasAssociacaoQueryTests.OsPedidos_NaoGravamNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: BoquilhasAssociacaoQueryTests.Producao_DevolveIdEReferenciaESemProducaoDevolveNulo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: BoquilhasAssociacaoQueryTests.ProducoesDoNumero_MaisRecentePrimeiro_ComAIndicacaoDeTerAMesmaBq`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: BoquilhasAssociacaoQueryTests.RegistoSemJobOn_DevolveOTraceAvulsoComOsMovimentosESemEleDevolveNulo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: BoquilhasAssociacaoQueryTests.RegistosDaBq_PaginaPedidaForaDoFimFicaNaUltima`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs :: BoquilhasAssociacaoQueryTests.RegistosDaBq_VemOsDaFerramentaEOsAssociadosMaisRecentesPrimeiro_ComProducaoEMovimentos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasAssociacao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasAssociacao.cs :: PostgresMigrationIntegrationTests.BoquilhasAssociacao_CadaPedidoMantemOCustoEmConsultasDeLeitura`

Selo depois: `57ab224e182ba5d2`

## A-0115 · 2026-10-26 · ok-do-diogo

Porquê: O painel lateral do Planeamento passa a ter o título «Planeamento» como link para /JobOn e a mostrar a referência e a produção numa linha e o estado noutra, a pedido do Diogo (26/10/2026). Os testes seguem o texto novo; o que verificam (o painel, os links, a ordem) não muda.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyNF2jrGSXBjoNJpEQKJVTrc; cmsg_01A27e9ozcpoh4jdPRsuJbFyLZ7gLSKXpt1YiJZRMTdjh1; autorização de testes cmsg_01A27e9ozcpoh4jdPRsuJbFyRTc4GbKqSyKguBgVdi92LW
Novos: 0 · mudaram: 2 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.CreateFlow_ExplicitSelections_EstablishesQueryableContext`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs :: PlaneamentoSumarioPageTests.Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas`

Selo depois: `63d6530418f8c955`

## A-0114 · 2026-10-26 · ok-do-diogo

Porquê: O Editar antigo do Job On (JobOn/Index) deixa de ler notas de lote, alterações e o que se perde ao eliminar, que nenhuma vista mostrava: JobOnFichaQuery passa a ler só os factos das ferramentas e a folha gravada. Os testes do #112 perdem as asserções dessas leituras (4 renomeados, 2 saem), o orçamento de consultas desce (16→10, 4→2, 8→2) e um teste novo prova que essas leituras já não se fazem.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy3dUaW7rit23pc6wmyyXQ44
Novos: 5 · mudaram: 5 · saíram: 6

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.AsLeiturasSaoPelaOrdemDaPagina_FactosEFolha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.Duplicar_NaoLeFactos_MasLeAFolhaQueCopia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.JobOnAberto_DevolveFactosEFolha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.NaoLeNotasDeLoteNemAlteracoesNemOQueSePerdeAoEliminar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.SemFerramentas_NaoLeFactos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFicha.cs :: PostgresMigrationIntegrationTests.JobOnFicha_CadaPedidoFazSoAsConsultasDeLeitura`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.FolhaGravada_Presente_OuAusenteLeSeComoVazia`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.OPedido_NaoGravaNada`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.SoUmaFerramenta_LeFactosSoDessa`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.AsLeiturasSaoPelaOrdemDaPagina_NotasAlteracoesPreviewFactosFolha`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.Duplicar_NaoLePreviewNemFactos_MasLeAFolhaQueCopia`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.JobOnAberto_DevolveNotasAlteracoesPreviewFactosEFolha`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.OPreviewUsaODiaPedido`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.SemFerramentas_NaoLeFactos_MasLePreview`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs :: JobOnFichaQueryTests.SemNotasNemAlteracoes_ListasVazias`

Selo depois: `2a6fb53ba94863bb`

## A-0117 · 2026-10-26 · teste-novo

Porquê: Testes novos das folhas do Imprimir do Job On (Ficha de Artigo e Trabalho de Equipa): o botão Imprimir abre a lista das folhas, as folhas vêm com os dados do Job On, e quem só tem «Job On ver» não as recebe.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyPAdng59k4sXTVH3yh5tyX2
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_Imprimir_SoVer_NaoRecebeAsFolhas`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_Imprimir_TemOMenuEAsFolhasComOsDadosDoJobOn`

Selo depois: `3b6824c4d32faea1`

## A-0116 · 2026-10-26 · teste-novo

Porquê: Sumário: o Diogo não via os links dos avisos do «Importante» («não tem os links para a página onde se pode corrigir»). A linha inteira passa a abrir a página que resolve; o teste novo prova o link na linha, sem botões, e as regras do CSS que esticam o link pela linha.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyDm7rAVnv8boHSLPT52F7YR
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs :: PlaneamentoSumarioPageTests.UmImportante_ALinhaInteiraAbreAPaginaQueResolve`

Selo depois: `6f637b56f2125107`

## A-0118 · 2026-10-26 · contagem-lista

Porquê: Login próprio da app (modo Local, sem Supabase), do PR #101: testes do fornecedor Local (palavras-passe do Supabase, limite de tentativas); a tabela LocalCredentials entra nas listas de tabelas e de migrações.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy7oqPGxoe5kii8iFPC2Mgwk
Novos: 12 · mudaram: 5 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.Apagar_TiraOAcesso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.Cifra_TemSalDiferenteEVerificaSoACerta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.CincoFalhasSeguidas_BloqueiamMesmoComACerta_EDepoisVoltaAEntrar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.Criar_EntraComATemporariaEPedeMudanca`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.EmailRepetido_EhRecusado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.Espera_DobraAteQuinzeMinutos`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.Mudar_TiraATemporariaEAAntigaDeixaDeServir`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.PalavraPasseDoSupabase_Entra_EFicaCifradaNoFormatoDaApp`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.PalavraPasseErrada_OuEmailDesconhecido_Falha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.Repor_VoltaATemporaria`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/RelogioDeTeste.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `106a406a6df785bb`

## A-0119 · 2026-10-26 · ok-do-diogo

Porquê: PR #109/#120, passo 3 da app no portátil: sem Dmo__AuthenticationProvider, fora de Development, o login passa a ser o da app (Local); um valor desconhecido continua no Supabase, com aviso. Sai o atalho do pooler do Supabase (DevPoolerHost) e o seu teste. As mensagens do Admin deixam de dizer «Supabase». Os casos Production/Staging vazios passam do teste antigo para o novo (Local); os restantes ficam iguais.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyRYz9rVfSPGua8CqeapJTKF (Diogo «sim» ao plano https://claude.ai/artifact/RBi5qndtR3xsjwyTPgpLV9)
Novos: 3 · mudaram: 2 · saíram: 2

- novo: `tests/DMO.Alpha.Web.Tests/PersistencePolicyTests.cs :: PersistencePolicyTests.NonDevelopment_SemFornecedorOuLocal_UsaOLoginProprio`
- novo: `tests/DMO.Alpha.Web.Tests/StartupConfigurationCheckTests.cs :: StartupConfigurationCheckTests.Producao_ComFornecedorDesconhecido_Avisa`
- novo: `tests/DMO.Alpha.Web.Tests/StartupConfigurationCheckTests.cs :: StartupConfigurationCheckTests.Producao_SemFornecedorDeclarado_UsaOLoginProprio_SemAviso`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.DeleteUser_TwoStepConfirmation_RemovesTheRow_AdminNever`
- mudou: `tests/DMO.Alpha.Web.Tests/PersistencePolicyTests.cs :: PersistencePolicyTests.NonDevelopment_AlwaysRegistersRealAuthenticationAndProvisioning`
- saiu: `tests/DMO.Alpha.Web.Tests/PersistencePolicyTests.cs :: PersistencePolicyTests.Beta_DevPoolerOverride_ReusesStoredTechnicalCredential`
- saiu: `tests/DMO.Alpha.Web.Tests/StartupConfigurationCheckTests.cs :: StartupConfigurationCheckTests.Producao_SemOFornecedorDeclarado_Avisa`

Selo depois: `3ea761a7a6b4d133`

## A-0120 · 2026-10-26 · teste-novo

Porquê: PR #109/#120: primeira instalação (/instalar), que cria o ADMIN numa base sem ADMIN nem palavras-passe, só com o login próprio e com o código de instalação (Diogo, 26/10/2026).
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyWbdS4e71SJst8xdH6RU2EW
Novos: 9 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.BaseVazia_ComOCodigo_CriaOAdmin_QueEntraComAPalavraPasse`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.CodigoErrado_NaoCriaNada`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.CodigoSorteado_MudaAoFimDeCincoEnganos`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.ComAdminJaGravado_NaoHaInstalacao`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.ComPalavrasPasseNaBase_NaoHaInstalacao`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.DadosQueNaoServem_NaoCriamNada`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.DepoisDeFeita_OCodigoNaoVoltaAServir`
- novo: `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs :: InstalacaoInicialTests.SemLoginProprio_NaoHaInstalacao`

Selo depois: `bfb24a5eaf62cccf`

## A-0121 · 2026-10-26 · ok-do-diogo

Porquê: O teste das alterações no mesmo minuto falhava quando corria aos :00 segundos: criava «agora» e «agora − 1 s», que caíam em minutos diferentes e davam duas linhas. A segunda hora fica até 1 s antes sem sair do minuto de «agora»; as asserções ficam iguais. Muda a preparação de um teste existente: autorização de testes do Diogo.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyRTc4GbKqSyKguBgVdi92LW
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.AMesmaAlteracao_NoMesmoMinuto_FicaNumaSoLinha_EOutraContinua`

Selo depois: `99b5882a21ea6965`

## A-0113 · 2026-10-26 · ok-do-diogo

Porquê: Os números da folha do Job On passam a sair com ponto e duas casas quando têm casas, também dentro do texto («2,50» → «2.50», «70/69,4» → «70/69.40»), a pedido do Diogo (26/10/2026). Há um teste novo do formato; dois testes antigos que esperavam «2,5» passam a esperar «2.50», e o do Peso muda de nome.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy2EadhQLcRARQPVdSprtcV6; cmsg_01A27e9ozcpoh4jdPRsuJbFyQoVKQfBetn2koRQ7rjiSdj; ok à captura cmsg_01A27e9ozcpoh4jdPRsuJbFyUTBYDuFtNXF5hUqF1v4BRr; autorização de testes cmsg_01A27e9ozcpoh4jdPRsuJbFyRTc4GbKqSyKguBgVdi92LW
Novos: 2 · mudaram: 1 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/DmoNumberFormatTests.cs :: DmoNumberFormatTests.FolhaTexto_PontoEDuasCasas_TambemDentroDoTexto`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Peso_DuasCasasDecimaisNoMaximo_ComPonto`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Folha_ACalMostraAsLinhasGravadas_ComAQtdMaq_NaFolhaNova`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Peso_DuasCasasDecimaisNoMaximo_SemZerosNoFim`

Selo depois: `d56824e14c3ac255`

## A-0123 · 2026-10-26 · ok-do-diogo

Porquê: Login: da 5.ª palavra-passe errada seguida em diante o ecrã manda consultar o ADMIN; a conta nunca fica bloqueada e a certa entra sempre
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy3mFwYjgASf4KLxaoXW6mfD
Novos: 2 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.Tentativas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.Tentativas.cs :: AuthenticationTests.Login_CincoPalavrasPasseErradas_MandaConsultarOAdmin`
- mudou: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs :: AuthenticationTests.UserLogin_RepeatedFailures_NeverLockTheUser`

Selo depois: `e4f3ba263bddfa5c`

## A-0124 · 2026-10-26 · ok-do-diogo

Porquê: Login próprio: deixa de bloquear depois de 5 erros (a palavra-passe certa entra sempre; fica só o aviso «Consulte o ADMIN»); o teste do bloqueio passa a provar que não bloqueia, sai o da espera, e um teste Web novo prova o mesmo com o LocalAuthenticationProvider
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy3mFwYjgASf4KLxaoXW6mfD
Novos: 3 · mudaram: 0 · saíram: 3

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.CincoFalhasSeguidas_NaoBloqueiam_ACertaEntraLogo`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.LoginLocal.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.LoginLocal.cs :: AuthenticationTests.LoginLocal_RepeatedFailures_WarnButNeverLockTheUser`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.CincoFalhasSeguidas_BloqueiamMesmoComACerta_EDepoisVoltaAEntrar`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs :: LocalAuthenticationProviderTests.Espera_DobraAteQuinzeMinutos`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Authentication/RelogioDeTeste.cs :: (resto do ficheiro)`

Selo depois: `349a29c1afb472af`

## A-0126 · 2026-10-26 · teste-novo

Porquê: Templates do Admin: o Diogo pediu as permissões sem caixas, as não escolhidas leves e as escolhidas escuras com letra branca. O teste novo prova que a caixa continua no formulário (escondida), que o cartão é a etiqueta e as regras de cor, também no modo escuro.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy6QVqRVdiThbepM8QkqjAGZ
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.Gestao.cs :: AdminUsersAndTemplatesPagesTests.AdminTemplates_SemCaixas_EscolhidoEscuroComLetraBranca`

Selo depois: `1bc8bd89cbd3cd46`

## A-0127 · 2026-10-26 · ok-do-diogo

Porquê: Mudar a palavra-passe passa a pedir a atual, exceto quando é temporária (auditoria externa DS-LOG-03): os 2 testes do ADMIN que mudam a palavra-passe enviam a atual; teste novo prova que sem ela nada muda
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyUctKbk5MGZ3NjoGuDF1Jdp
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.PalavraPasseAtual.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.PalavraPasseAtual.cs :: AuthenticationTests.ChangePassword_WithoutTemporaryPassword_AsksForTheCurrentOne`
- mudou: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.PedidoCortado.cs :: AuthenticationTests.AdminPasswordChange_ClientLeavesAfterTheProviderChange_TheOtherAdminSessionsStillEnd`
- mudou: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs :: AuthenticationTests.AdminPasswordChange_EndsTheOtherAdminSessions`

Selo depois: `79f54fc617b75cbe`

## A-0133 · 2026-10-26 · ok-do-diogo

Porquê: Excel das Boquilhas: os 12 meses ficam cada um no seu separador (pedido do Diogo 26/10); os testes procuram o separador pelo nome; a data passa a ter o valor escrito; teste novo do domingo vazio no Atualizar.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyMR6KCaxK4JedPnkWmiSBgT
Novos: 2 · mudaram: 6 · saíram: 1

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Atualizar_UmDomingoVazio_NaoPara_EEscreveOsDiasDeAntes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.CadaMes_FicaNoSeuSeparador_PorOrdem_ComAsDatasEmDiaMesAno`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.Escrever_NumExcelNovo_GuardaOsEstilosEAsJuncoes`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmExcelNovo_SoTemOsDiasDoMes_EOMesSeguinteGanhaOsQueFaltam`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmMesSemSeparador_CriaOSeparador_ComAsDatasEOsDiasDaSemana`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: BqRepairerSettingsPageTests.ExcelDasBoquilhas_OCartao_AVersaoMudaComUmMovimento_EOFicheiroVoltaEscrito`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: BqRepairerSettingsPageTests.ExcelDasBoquilhas_UmExcelNovo_TemOSeparadorDoMes`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs :: BqExcelLivroTests.UmMesSemSeparador_ESemMovimentos_NaoCriaNada`

Selo depois: `3b25a8fc9f9336fe`

## A-0129 · 2026-10-26 · ok-do-diogo

Porquê: Diogo pediu: um só triângulo nos cartões dobráveis, botão invertido branco no escuro, sem itálico, toggles nas caixas da ferramenta, hover do calendário
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy5hWpevZxdmL29DgykSdZi6
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.CardDobravel_UmSoTriangulo_EBotaoInvertido_BrancoNoEscuro`
- novo: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.SemItalico_EmTodaAApp`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoCheio.cs :: FerramentasPagesTests.UsoCheio_SoMudaACor_NaFichaENoPesquisar_NuncaAvisaNemBloqueia`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.Calendar_SelectedDayHover_KeepsSlateBackground`

Selo depois: `b1fbf8371e3118cd`

## A-0134 · 2026-10-26 · teste-novo

Porquê: Cabeçalhos de segurança em todas as respostas
Referência: A-0134
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/CabecalhosDeSegurancaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CabecalhosDeSegurancaTests.cs :: CabecalhosDeSegurancaTests.Resposta_LevaOsCabecalhosDeSeguranca`

Selo depois: `2ed82e8fca8a939f`

## A-0135 · 2026-10-26 · teste-novo

Porquê: Reshape: as últimas leituras diretas da base (Folha e Reparação do Job On) passam pelo IJobOnPaginaQuery; testes novos do MaquinaDoJobOnAsync
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyG4kTeYNHDJtHEYeeMzqUiT
Novos: 4 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnMaquina.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnMaquina.cs :: PostgresMigrationIntegrationTests.JobOnMaquina_FazUmaSoConsultaDeLeitura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnMaquinaDoJobOnTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnMaquinaDoJobOnTests.cs :: JobOnMaquinaDoJobOnTests.MaquinaDoJobOn_DevolveALinhaDaProducao_OuZeroQuandoNaoExiste`

Selo depois: `11e205f5bd501994`

## A-0136 · 2026-10-26 · teste-novo

Porquê: Testes novos: Registos por utilizador no Admin (abrem vazios, filtram por utilizador e módulo, só o ADMIN entra) e a cor ligeira da ferramenta a 100% na Folha do Job On.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyKDJ8zZui3W6yJSGDxU5PUZ
Novos: 5 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Admin/RegistosPorUtilizadorTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/RegistosPorUtilizadorTests.cs :: RegistosPorUtilizadorTests.Registos_AbremVazios_EFiltramPorUtilizadorEModulo`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/RegistosPorUtilizadorTests.cs :: RegistosPorUtilizadorTests.Registos_SoOAdminEntra`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaUsoCheio.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaUsoCheio.cs :: JobOnPageTests.Folha_FerramentaA100PorCento_ReferenciaELoteComCorLigeira`

Selo depois: `291bede439cd4981`

## A-0130 · 2026-10-26 · ok-do-diogo

Porquê: Diogo pediu: Voltar volta à página anterior, cartões do registo das Boquilhas com link à ferramenta e ao Job On, módulos do Pessoal em tabela (cmsg_01A27e9ozcpoh4jdPRsuJbFyNKpEEbQzJxBt6UxyAm6UQH)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy5hWpevZxdmL29DgykSdZi6
Novos: 8 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CartoesComLink.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CartoesComLink.cs :: BoquilhasRegistoPageTests.Cartoes_BqLoteAbreAFicha_ORestoAbreOJobOnDaProducao`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Voltar.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Voltar.cs :: FerramentasPagesTests.Ficha_OVoltar_VoltaAPaginaDeOndeSeVeio_SemOrigemALista`
- novo: `tests/ShellProof/voltar.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/voltar.test.cjs :: depois de guardar (vindo da própria ficha) ou do Histórico, a origem mantém-se`
- novo: `tests/ShellProof/voltar.test.cjs :: sem origem do mesmo site, fica o link do servidor`
- novo: `tests/ShellProof/voltar.test.cjs :: vindo de outra página, o Voltar volta lá com os filtros`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.HistoricoDiario.cs :: BoquilhasRegistoPageTests.Historico_AAbaAbreOResumoDiario_EACelulaAbreORegistoComOJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.PersistedToolAndContextFacts_RenderFromServerData`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs :: BoquilhasRegistoPageTests.Cartoes_InicioEFimSaoOsDoJobOn_SemInicioAvisoAmarelo`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs :: BoquilhasRegistoPageTests.OpeningAMachine_DoesNotCreateTheTrace`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Pessoal_SoTemAContaAPalavraPasseEAAparencia`
- mudou: `tests/ShellProof/package.json`

Selo depois: `e3b8d1822d04685e`

## A-0137 · 2026-10-26 · ok-do-diogo

Porquê: DS-PERM-01: na Reparação Interna só quem registou e a chefia (a Consulta) corrigem ou anulam. Os testes em que outra pessoa (ou o serviço sem sessão) corrigia ou anulava passam a usar a chefia; testes novos mostram que outro operador sem a chefia não muda nada.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyCwjpBqhTBK4Sd2W4oD7FYF (Diogo «sim» ao DS-PERM-01, 26/10/2026)
Novos: 6 · mudaram: 6 · saíram: 4

- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Anular_AChefia_ADeOutraPessoaSaiDaListaEDasContagens_FicaNaBase_ComRasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Corrigir_AChefia_MudaSoONumeroDeOutraPessoa_EDeixaRasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.OutraPessoaSemAChefia_NaoCorrigeNemAnula_ENadaMuda`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.AChefia_AnulaADeOutraPessoa_ALinhaSai_EFicaNaBase`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.AChefia_CorrigeONumeroDeOutraPessoa_ComRasto_EOMesmoEnvioAplicaUmaVez`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutroOperadorSemAChefia_NaoCorrigeNemAnula_ENadaMuda`
- mudou: `scripts/ci/testes.md`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaRasto.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaRasto_AnularAoMesmoTempo_AnulaUmaVez`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaRasto.cs :: PostgresMigrationIntegrationTests.ReparacaoInternaRasto_EliminarOJobOn_LevaAsReparacoesEORasto`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.DuasCorrecoes_DoisRastos_PelaOrdem`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.UmaAnulada_NaoSeCorrigeNemSeAnulaOutraVez`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Anular_OutraPessoa_SaiDaListaEDasContagens_FicaNaBase_ComRasto`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs :: ReparacoesInternasCorrigirTests.Corrigir_OutraPessoaComOModulo_MudaSoONumero_EDeixaRasto`
- saiu: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_Anula_ALinhaSai_EFicaNaBase`
- saiu: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs :: ReparacaoInternaCorrigirPaginaTests.OutraPessoaComOModulo_CorrigeONumero_ComRasto_EOMesmoEnvioAplicaUmaVez`

Selo depois: `73089c51f2cc3956`

## A-0138 · 2026-10-26 · ok-do-diogo

Porquê: Folha do Job On, pedidos do Diogo a 26/10/2026: os furos do CS no ver sem a «/» (um por baixo do outro); o Duplicar sugere a produção seguinte à última da referência (o teste do Duplicar passa a esperar 202603 em vez do campo vazio, e muda de nome); testes novos: a produção sugerida e as produções da referência da mais recente para a mais antiga.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyELUj7Rp8tMrtt8xnuiRtQz, cmsg_01A27e9ozcpoh4jdPRsuJbFyVApVgtiy5mqC98Db9xbXo8
Novos: 3 · mudaram: 1 · saíram: 1

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/ProductionPickerQueryTests.cs :: ProductionPickerQueryTests.Procura_AMaisRecentePrimeiro_PeloInicioEDepoisPeloNumero`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.Duplicar_AProducaoSugerida_EASeguinteAMaior`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Duplicar_CopiaOsDados_SemDatas_ComAProducaoSeguinte_ComODiaDoCalendario_EGravaUmaProducaoNova`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_Editar_DesbloqueiaEGuardarVoltaAFolha_SemApagarOQueNaoMudou`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs :: JobOnPageTests.FolhaNova_Duplicar_CopiaOsDados_SemDatasNemNumero_ComODiaDoCalendario_EGravaUmaProducaoNova`

Selo depois: `7ca7ff243aac45b1`

## A-0140 · 2026-10-26 · ok-do-diogo

Porquê: Testes novos: Tampão da Folha = tampão da CM, Lote FF, sem Tampão na CM, Volume na BQ. Mudanças: Tampão e Punção saem da ficha da CM (cmsg_01A27e9ozcpoh4jdPRsuJbFyR4JxijzJuzAZzq37Qzi296); um só Voltar (cmsg_01A27e9ozcpoh4jdPRsuJbFyRfdwnWiuT6MCVrPw2pdSRm); Volume na BQ (cmsg_01A27e9ozcpoh4jdPRsuJbFyWtKmjoyB3wGtxzcCSxX69C); apoio TP do Peso com h nominal e h Job On (cmsg_01A27e9ozcpoh4jdPRsuJbFyJeXejBKtyrUeFgGaVPybCd) e só volume nominal, do Job On e Δ (cmsg_01A27e9ozcpoh4jdPRsuJbFyGfg3KWEXc5o6gr5g6qLJzG), a 2 casas pela regra dos números do Diogo; mensagem sem tampão aponta à Folha do Job On
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyR4JxijzJuzAZzq37Qzi296
Novos: 8 · mudaram: 25 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SemTampaoNaCm.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SemTampaoNaCm.cs :: FerramentasPagesTests.Ficha_GuardarSemTampaoNemPuncao_MantemOsValoresGravados`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VolumeDaBq.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VolumeDaBq.cs :: FerramentasPagesTests.Criar_TituloDoBloco_VolumeNaBqEPesoNaCm`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LoteFf.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LoteFf.cs :: JobOnPageTests.FolhaNova_LoteFf_AbreComOLoteDoMf_EGravaOutro`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.TampaoDaFolha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.TampaoDaFolha.cs :: JobOnPageTests.FolhaNova_DiametroDoTp_EOTampaoDaProducao`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapSupportQueryTests.cs :: PesoCapSupportQueryTests.GetAsync_ToolSemTP_ReferenciaPorCompletar`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs :: PesoTampaoDoJobOnTests.SemTampaoEmLado_Nenhum_ReferenciaPorCompletarSemBloquear`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_PainelDeFactos_MostraVolumesDensidadeEApoioComACaloteDoJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_SemCaloteNoJobOn_UsaAReferenciaDaTool`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_SemTP_ReferenciaPorCompletarEPesoIntacto`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.SimulacaoIlegivel_AvisoHonestoSemBloqueio`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.SimulacaoPorGet_RecalculaPeloBackendEAbreOApoio`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ToolScopedMode_ApoioSemProducao_UsaAReferenciaDaTool`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasFichaTrocaTests.cs :: FerramentasPagesTests.Ficha_BotoesDeCadaCard_NoCantoSuperiorDireito`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasFichaTrocaTests.cs :: FerramentasPagesTests.Ficha_ValorTecnicoPorLer_FicaNoCampoParaCorrigir_ComOFoco`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Registo_InformacaoTecnicaEmSeccoesComDica_QuantidadeAntesDaRefMf`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Voltar.cs :: FerramentasPagesTests.Ficha_OVoltar_VoltaAPaginaDeOndeSeVeio_SemOrigemALista`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VoltarJobOn.cs :: FerramentasPagesTests.Ficha_ComVoltarDoJobOn_MostraOVoltarParaAFolhaExata`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VoltarJobOn.cs :: FerramentasPagesTests.Ficha_SemVoltarValido_NaoMostraOVoltarAoJobOn`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: FerramentasPagesTests.Criar_APartirDoCartaoDoJobOn_OTipoFicaFixo`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.Ficha_CamposTecnicos_NoMaximoDuasCasas_SemZerosNoFim`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.PostCreate_Refusal_KeepsTheTechnicalValues_AndOpensThem`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.PostDuplicate_NewLotRequired_OpensNewToolFicha_SourceIntact`
- mudou: `tests/ShellProof/peso-calote.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/peso-calote.test.cjs :: a resposta de uma calote anterior que chega depois da atual não substitui os valores da atual`
- mudou: `tests/ShellProof/peso-calote.test.cjs :: escrever outra vez durante a espera invalida logo o pedido que já saiu`
- mudou: `tests/ShellProof/peso-calote.test.cjs :: sem resposta, com erro do servidor ou sem apoio, os valores da calote anterior saem («—»)`

Selo depois: `df200d8b62e76eb5`

## A-0141 · 2026-10-26 · ok-do-diogo

Porquê: Alterar lote da BQ: o Movimentos passa para dentro das Opções do Registo (pedido do Diogo) e as consultas do Registo contam mais uma (os lotes para alterar).
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyNRWRx1TS25hZbnTzbzQN7o
Novos: 14 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs :: AlterarLoteBqTests.Alterar_AUtilizacaoDaFolhaPassaParaADoLoteNovo_0SeENovo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs :: AlterarLoteBqTests.Alterar_DuasVezes_TodosOsLotesUsados_PelaOrdem`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs :: AlterarLoteBqTests.Alterar_LoteNovoNumBqIdNovo_OAntigoFicaNaProducao_ComRasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs :: AlterarLoteBqTests.Alterar_NumLoteJaTrocado_NaoMudaNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs :: AlterarLoteBqTests.Alterar_OutraReferenciaOutroTipoOuOMesmo_NaoServe_ENadaMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs :: AlterarLoteBqTests.Lotes_SoAsOutrasBqDaMesmaReferencia_PorOrdemDoLote`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/FolhaDaFerramentaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/FolhaDaFerramentaTests.cs :: FolhaDaFerramentaTests.FerramentaNova_Utilizacao0_ESemJobOnNenhum`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/FolhaDaFerramentaTests.cs :: FolhaDaFerramentaTests.FerramentaUsada_AUltimaPercentagemDela_EAsNotasDoUltimoJobOn`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoOpcoesTests.cs :: BoquilhasRegistoPageTests.Registo_AlterarLote_ABqPassaParaOLoteEscolhido_EORegistoAbreNoLoteNovo`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LotesUsados.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LotesUsados.cs :: JobOnPageTests.Editar_AFerramentaNovaTrazOsDadosDela_UsoA0`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LotesUsados.cs :: JobOnPageTests.Folha_DepoisDeAlterarOLoteDaBq_MostraOAtualEOsDeAntes`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasRegistoConsultasTests.cs :: BoquilhasRegistoConsultasTests.Registo_GetFazPoucasConsultas`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoOpcoesTests.cs :: BoquilhasRegistoPageTests.Registo_LinkHistorico_AbreOHistoricoFiltradoPorEstaBqELote_ComCadaReferenciaTotal`

Selo depois: `efca3bbb72e1c0c8`

## A-0143 · 2026-10-26 · contagem-lista

Porquê: a lista das migrações e as colunas do Job On ganham a Experiencia (migração JobOnExperiencia); teste novo: a experiência usa o nº AAAA00, entra e sai no mesmo dia e convive com a produção da linha
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CreateJobOnExperienciaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CreateJobOnExperienciaTests.cs :: CreateJobOnExperienciaTests.Experiencia_UsaAnoZeroZero_EntraESaiNoMesmoDia_ENaoBloqueiaAProducaoDaLinha`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.ControloEntity_JobOnDoesNotGainControloNavigationOrColumn`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `de6e4f299d90520b`

## A-0144 · 2026-10-26 · ok-do-diogo

Porquê: o registo da leitura do Peso deixa de ser fixo à direita e fica no fluxo da página (pedido do Diogo, #145)
Referência: Diogo, 26/10/2026: «o inserir os valores não fica fora» (PR #145)
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/visual/ecra_pequeno_browser.py`

Selo depois: `fb64851554ffdfc4`

## A-0145 · 2026-10-26 · teste-novo

Porquê: Testes novos da ronda de erros de 26/10: forma do número em O que mudou, caixa do uso, Criar com referência existente, utilização com ponto e cor
Novos: 7 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaRondaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaRondaTests.cs :: JobOnFolhaRondaTests.Alteracoes_SoAFormaDoNumero_NaoEMudanca`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoEscrito.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoEscrito.cs :: FerramentasPagesTests.Criar_CaixaDoUso_DeixaEscreverOValorTodo`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.Ronda.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.Ronda.cs :: JobOnPageTests.Criar_ReferenciaExistente_ComProducaoNova_AbreODuplicarComAProducaoEALinha`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.Ronda.cs :: JobOnPageTests.Folha_UtilizacaoComPontoEAcimaDe100ComCor`

Selo depois: `ab9e2d6aa5e7ad1b`

## A-0147 · 2026-10-26 · ok-do-diogo

Porquê: Clicar numa boquilha da lista Boquilhas registadas abre a Ficha da ferramenta em vez da ficha em página; os testes esperam /Ferramentas/Ficha (visual e 2 testes Web)
Referência: Diogo, 26/10/2026 14:12 (cmsg_01A27e9ozcpoh4jdPRsuJbFyYB4H83f1h22SW7XdyLPDdV): «carregar aqui na boquilha deverias abrir a fgolha da ferramenta dela» (PR #149)
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.CatalogRows_OpenTheToolSheetOnDoubleClick`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.cs :: BoquilhasRegistoPageTests.ToolCatalog_SearchAndPaginationPreserveSelectionContext_AndExcludeOtherToolTypes`
- mudou: `tests/visual/boquilhas_catalog_browser.py`

Selo depois: `1f5b3db999a31bde`

## A-0151 · 2026-10-26 · ok-do-diogo

Porquê: O Resumo deixa de ter «Associar link do mcaliper» e a janela; só abre o link (quem muda o link é quem tem Criar, na ficha). O teste do link do mcaliper passa a verificar isso.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFySu9kGPCHGA7bwP7E8ViUxQ
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs :: ControloCapabilitiesCutHPageTests.McaliperLink_CreateAssociatesOnToolSheet_ApproveCopiesIt`

Selo depois: `6db60bb76d73efbb`

## A-0152 · 2026-10-26 · ok-do-diogo

Porquê: Placeholder do filtro de produção passa a «Produção» em Historico_ProcuraUnificadaPelosQuatroTipos (Diogo 26/10)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyVXGyDiJHgyTJP2v8ShT1rm
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.Historico_ProcuraUnificadaPelosQuatroTipos`

Selo depois: `862512d39e8a2f1a`

## A-0153 · 2026-10-26 · ok-do-diogo

Porquê: Job On > Opções > Reparação abre a RI ver filtrada; a página antiga /JobOn/Reparacao sai e 3 asserções de JobOnPageTests.FolhaNova passam a apontar à RI (PR #155, lote aprovado pelo Diogo)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAcHKLAVvVgvoPwNUk8FP7t
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_JobOnVer_VeBloqueadaSemEditar_EOPlaneamentoLevaAFolha`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_OpcoesControlo_ComControlo_AparecemControloAntesDaReparacao`

Selo depois: `b79a42dd5c530327`

## A-0154 · 2026-10-26 · teste-novo

Porquê: Teste novo: meter a discrepância a zero limpa o Saldo das linhas anteriores (PR #157)
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/ResetBqDiscrepancyTests.cs :: FixedClock.Reset_ClearsTheSaldoOfTheEarlierLines_ButKeepsTheMovements`

Selo depois: `dc6a82a28326fe61`

## A-0156 · 2026-10-26 · ok-do-diogo

Porquê: Folha do Job On: o Imprimir passa a um só clique (sem lista de folhas) e as migrações ganham a coluna AnLote; um teste novo guarda as decisões do Diogo sobre a Folha
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyAcHKLAVvVgvoPwNUk8FP7t
Novos: 1 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_DecisoesDoDiogo_ComponentesSeparadosAnComLoteCaloteUnicaSemComentarios`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_Imprimir_TemOMenuEAsFolhasComOsDadosDoJobOn`

Selo depois: `0a2ef153938dc2f0`

## A-0158 · 2026-10-26 · ok-do-diogo

Porquê: O hover escuro dos botões azuis passa de fundo branco para fundo azul, letra e contorno brancos (Diogo, 26/10: «sim as três»); o teste CardDobravel_UmSoTriangulo_EBotaoInvertido_BrancoNoEscuro passa a esperar a regra nova em dmo-normalize.css.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyVXGyDiJHgyTJP2v8ShT1rm
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.CardDobravel_UmSoTriangulo_EBotaoInvertido_BrancoNoEscuro`

Selo depois: `3e3c12c08ee5874d`

## A-0155 · 2026-10-26 · teste-novo

Porquê: Teste novo ResumoPorPessoa_ContaReparacoesDiasEMedia_SemAsAnuladas (resumo por pessoa da Consulta da RI, PR #160, .101); a lista de migrações ganha ReparacaoInternaTrabalho sem mudar o que o teste verifica
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs :: ReparacoesInternasConsultaTests.ResumoPorPessoa_ContaReparacoesDiasEMedia_SemAsAnuladas`

Selo depois: `0974a6d25321d126`

## A-0157 · 2026-10-26 · teste-novo

Porquê: SAP: «Apagar lidos», corrida às 18:00 na ajuda e leitura do relatório «produções» (Diogo 22/10 07:43 e 26/10 16:08)
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ORelatorioProducoes_DaAsGarrafasBrutasDeCadaProducao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_OCartaoDoSapTemApagarLidos_EAAjudaExplica`

Selo depois: `4b12b7b049b2c65c`

## A-0159 · 2026-10-26 · teste-novo

Porquê: A referência toda (1075C142) encontra a CM/MF e a BQ registadas só com uma parte
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateQueryTests.cs :: ToolCandidateQueryTests.ReferenceContains_AWholeReferenceAlsoFindsTheToolsRegisteredWithOnlyOnePart`

Selo depois: `f100f52b5cd8b3c9`

## A-0160 · 2026-10-26 · teste-novo

Porquê: Ficha da ferramenta: cartão «Histórico de produções» por baixo da informação técnica, com garrafas, peso da produção (média, moda e delta contra a anterior), % de cada Job On e total (Diogo 26/10 17:16 e 17:48).
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.OHistoricoDeProducoes_MostraAsGarrafasOPesoEAPercentagemDeCadaJobOn`

Selo depois: `a1189c0ece6af100`

## A-0162 · 2026-10-26 · teste-novo

Porquê: Histórico de produções: Δ garrafas contra a produção anterior e janela do peso médio com moda e Δ peso (Diogo 26/10 19:58) e ligação das produções do relatório real do PC da fábrica (BOM no início do ficheiro) ao Job On.
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.OHistoricoDeProducoes_DaODeltaDeGarrafasEAJanelaDoPeso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ORelatorioProducoesReal_LigaAsProducoesDaCmDaB1_ComGarrafas`

Selo depois: `0e773a7349323fea`

## A-0163 · 2026-10-26 · ok-do-diogo

Porquê: Peso: a caixa do peso da produção anterior mostra só a média e a moda abre numa janela (A-0163); o teste deixa de pedir as caixas Média/Moda (Diogo 26/10 20:18).
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFy3gTYGTcTs8P97dEqSZ6wty
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia`

Selo depois: `bca6c7d6576d340c`

## A-0165 · 2026-10-26 · ok-do-diogo

Porquê: Reparação Interna, Consulta: a caixa «Só por associar» passa a «Sem Job On» (Diogo 26/10 20:24); o teste Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas espera o texto novo.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyVghyhABryEjx37ZczZBUwu
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas`

Selo depois: `0c2d1d933f0fcb50`

## A-0166 · 2026-10-26 · teste-novo

Porquê: Teste novo JobOnCabecalhoCssTests: a regra do cabeçalho do Job On (Referência e Produção sem sobreposição) não pode desaparecer (PR #174)
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnCabecalhoCssTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnCabecalhoCssTests.cs :: JobOnCabecalhoCssTests.CabecalhoDoJobOn_ReferenciaEProducaoNaoSeTocam`

Selo depois: `973b7e99f4ebd5d6`

## A-0168 · 2026-10-26 · ok-do-diogo

Porquê: CS · Furos: o formato antigo «4X4», «4X2», «3X4» lê-se como abertos e fechados (Diogo 26/10 20:30)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyPaL9FV6fUfot3pWo9RAcag
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CsFurosTextoTests.cs :: CsFurosTextoTests.Ler`

Selo depois: `903451198ac59828`

## A-0170 · 2026-10-26 · ok-do-diogo

Porquê: Folha sem comentários (DMO-434): o teste das notas nos cartões passa a verificar o Reparador; testes novos da largura mínima e da calote.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyBwdNnf7f4cEjbZrpHH5rRM
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Folha_CartoesNaoColapsam_ComLarguraMinimaEMedidasSemScroll`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Folha_TpMostraACaloteGravada_NuncaAPalavraBacia`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: JobOnFolhaPrimeiraFilaTests.Folha_PrimeiraFila_CmMfBq_NotasEmTodosOsCartoes_RotulosSemFichaDeMoldes`

Selo depois: `a3a46538a4781592`

## A-0171 · 2026-10-26 · ok-do-diogo

Porquê: Folha DMO-434: a verificação do browser e o teste da primeira fila passam à grelha em 4 colunas.
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyBwdNnf7f4cEjbZrpHH5rRM
Novos: 0 · mudaram: 2 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs :: (resto do ficheiro)`
- mudou: `tests/visual/jobon_folha_browser.py`

Selo depois: `e98bc8f3a267f285`

## A-0172 · 2026-10-26 · ok-do-diogo

Porquê: Impressão das folhas como as da fábrica (DMO-435): dois testes antigos da impressão passam às regras novas e entra o teste das 4 folhas como as da fábrica (sim do Diogo à pergunta cmsg_01A27e9ozcpoh4jdPRsuJbFy2HNdo7uGLa6e8xKZGB1qax).
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyKnqyqqivyphUNaRPhHdDCD
Novos: 1 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_Imprimir_SaemAsQuatroFolhasComoAsDaFabrica`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_DecisoesDoDiogo_ComponentesSeparadosAnComLoteCaloteUnicaSemComentarios`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_Imprimir_TemOMenuEAsFolhasComOsDadosDoJobOn`

Selo depois: `bd963d170146d957`

## A-0174 · 2026-10-26 · ok-do-diogo

Porquê: A punção voltou à informação técnica da CM (Criar e ficha, um só formulário), visível e editável: os testes que recusavam o campo «Volume punção (cm³)» passam a esperá-lo; teste novo para ver e gravar o valor na ficha.
Referência: Diogo, 26/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraT5faqGrCPjdkUkYmc6GCBM («nao gosto do facto de nao conseguir ver o valor»)
Novos: 1 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SemTampaoNaCm.cs :: FerramentasPagesTests.Ficha_Cm_MostraOVolumeDaPuncao_EGuardaOValorEditado`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Registo_InformacaoTecnicaEmSeccoesComDica_QuantidadeAntesDaRefMf`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered`

Selo depois: `6c67ffefb43aac18`

## A-0175 · 2026-10-26 · teste-novo

Porquê: Sumário para o Job On ver e informação nova «Movimentos do Armazém» (DMO-438): testes novos.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraWEaSsUNtRHvwj5NTeZSM7f, cmsg_01K75FTs9bQnbhhH7qttpgra5RmpCY7HiWL1Fi338S3eFf
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Armazem.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Armazem.cs :: PlaneamentoSumarioPageTests.ArmazemMovimentos_ParaQuemTemJobOnVer_JobOnCriar_OuArmazem`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.WithJobOnView_ThePageOpensWithOnlyTheAccountModules`

Selo depois: `f27c31b9a7ed1685`

## A-0176 · 2026-10-27 · ok-do-diogo

Porquê: Painel «Planeamento» até uma semana em avanço e uma produção em curso por linha (DMO-444, 0.3.0-beta.130): os testes antigos seguiam «até domingo» e duas produções na mesma linha.
Referência: Diogo, 27/10/2026 00:25, «sim» (cmsg_01K75FTs9bQnbhhH7qttpgraTm1FJcWmeJJMnTEtB2R4VE), PR #187
Novos: 4 · mudaram: 6 · saíram: 4

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.DuasProducoesDaMesmaLinhaQueSeCruzam_FicaAQueEntrouPorUltimo_SemSobreposicao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.UmaLinhaPorMaquina_NaOrdemDasMaquinas_ComAProducaoQueEntrouPorUltimo`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: JobOnSidepanelWeekTests.Mostra_so_entradas_de_hoje_ate_uma_semana`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: JobOnSidepanelWeekTests.Uma_linha_so_tem_uma_producao_em_curso`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.NotasDeLoteDoDiaEResumoDasAlteracoes_ficamNaProducao`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.ProducaoAteAviso_CobreOsDiasDepoisDoInicio_MasNaoOsAntes`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: JobOnSidepanelWeekTests.Em_curso_primeiro_e_as_que_ja_acabaram_ficam_de_fora`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs :: PlaneamentoSumarioPageTests.Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.DuasProducoesDaMesmaLinhaQueSeCruzam_NaoMarcamSobreposicao`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs :: JobOnQuadroDoDiaQueryTests.UmaLinhaPorMaquina_NaOrdemDasMaquinas_ComAsProducoesDoDiaPorReferencia`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: JobOnSidepanelWeekTests.Ao_domingo_mostra_so_esse_dia`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs :: JobOnSidepanelWeekTests.Mostra_so_entradas_de_hoje_ate_domingo`

Selo depois: `d409b622f6c26765`

## A-0177 · 2026-10-27 · ok-do-diogo

Porquê: Design novo (DMO-436): o Diogo aprovou juntar o design à integracao com os testes mudados (Armazém sem lista antes da procura, Job On bloqueado com Editar, Boquilhas, Sumário, paleta e botões únicos)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraWtEDyiH367uf4aFwbYd3b5 (27/10 01:42, «Sim: testes e integracao»); botões: cmsg_01K75FTs9bQnbhhH7qttpgraPc5CZ7o8GxVCiuBHj4uft6
Novos: 1 · mudaram: 12 · saíram: 0

- novo: `tests/visual/design-review.cjs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs :: ArmazemLoteNovoPaginaTests.Sumario_AvisaSoQuemTemFerramentas_ECriarResolveEDaAEntrada`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.OLog_MostraSoOTipo_DeTodos_EALinhaAbreAFerramenta`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.OPainel_TemCmMfBq_ESemTipoPedeParaEscolher`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.SoVer.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.MachineInUse_ListsOnlyTheMachinesOfTheTool_AndQuantityComesFirst`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs :: JobOnPageTests.FolhaNova_JobOnVer_VeBloqueadaSemEditar_EOPlaneamentoLevaAFolha`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs :: PlaneamentoSumarioPageTests.Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.CardDobravel_UmSoTriangulo_EBotaoInvertido_BrancoNoEscuro`
- mudou: `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs :: UiStyleRuleTests.Buttons_HaveOneLook_BlueWithWhiteText_HoverInverts`
- mudou: `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs :: UiStyleRuleTests.DarkMode_RedefinesTheTokens_AndNoSheetPaintsLooseWhite`
- mudou: `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs :: UiStyleRuleTests.Palette_IsDefinedOnce_AndDrivesButtonsRowsAndHover`

Selo depois: `71aa1f5e6f59d13a`

## A-0178 · 2026-10-27 · ok-do-diogo

Porquê: Erros do design de 27/10/2026: Stock e QTD máq. fora dos cartões do Job On; «PDF Pegamentos»; Estado e Onde está na lista de Ferramentas; PU NNPB sem Linha; mapa dos Pegamentos em HTML legível no ecrã (PDF igual); BQ e lote nas linhas das Boquilhas (o teste procura só na lista). Testes novos dos mesmos erros.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraTeE5d1TKjz1h4KjrhScySV (pedidos: cmsg_01K75FTs9bQnbhhH7qttpgraNDGxpDQAbKb3urj4PR1RjM, cmsg_01K75FTs9bQnbhhH7qttpgraQXKR3abFzYB1QMLs63bXHa, cmsg_01K75FTs9bQnbhhH7qttpgra4gixkrU4QiiXPCqSrZqvpF)
Novos: 20 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.ErrosDiogo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.ErrosDiogo.cs :: BoquilhasRegistoPageTests.JobOnMudou_FicaNumaLinhaNoCabecalhoDoRegisto_SemBlocoPorBaixoDosCartoes`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.ErrosDiogo.cs :: BoquilhasRegistoPageTests.PainelDasLinhas_MostraABqEmMaquinaComOLote_EAProducaoNumaLinhaDiscreta`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs :: ControloCapabilitiesCutHPageTests.Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.DeltaPeloProduzido.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.DeltaPeloProduzido.cs :: DefinicoesPorModuloPageTests.ComAsLeiturasAVoltaDaProducao_ODeltaNaoEEstimado`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.DeltaPeloProduzido.cs :: DefinicoesPorModuloPageTests.SemLeiturasAVoltaDaProducao_ODeltaEstimaSePeloProduzido`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FaixaDaFicha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FaixaDaFicha.cs :: DefinicoesPorModuloPageTests.AJanelaDaUtilizacao_LevaAoHistoricoDeProducoes`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FaixaDaFicha.cs :: DefinicoesPorModuloPageTests.ORegistarDoArmazem_ComUmaFerramenta_TemOVoltar`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PesoPorProducao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PesoPorProducao.cs :: DefinicoesPorModuloPageTests.OPesoDoHistorico_AbreAJanelaComTodasAsProducoes_MediaModaEDeltaEmGEPercentagem`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosGraficoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosGraficoTests.cs :: PegamentosGraficoTests.CadaComponenteMedido_NaSuaLinha_PelaOrdemDaFolha`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosGraficoTests.cs :: PegamentosGraficoTests.Escala_DasMedicoesEDosLimites_ComMargem_SemLinhasVazias`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosGraficoTests.cs :: PegamentosGraficoTests.SemMedicoes_NenhumaLinha`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.ErrosControlo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.ErrosControlo.cs :: PuncoesPageTests.Folha_TodasAsCelulasComONumeroEALinhaDoLote`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.ErrosControlo.cs :: PuncoesPageTests.Identificacao_SemLinhaNemProcesso_ETotais_ComReserva`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.History_OpensBlank_UntilAReferenceOrAPeriodIsSearched`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs :: ControloResumoPageTests.OpenPreCutJobOn_FolhaShowsCanonicalContext_BackfillsControloIdOnce`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: FerramentasPagesTests.GetIndex_ShowsHumanFactsList_ToolIdOnlyInUrl_NeverAField`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Folha_MostraOsCamposGravados_NaFolhaNovaBloqueada`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Legenda.cs :: PegamentosPageTests.MapaDeLimites_ALegendaMostraAsCoresDoDesenho`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs :: PuncoesPageTests.NnpbProduction_RegistersPunches_ShowsAlteracao_TotalsResumoAndPdf`

Selo depois: `3531fa616f3f82db`

## A-0179 · 2026-10-27 · teste-novo

Porquê: Auditoria de funções de 27/10/2026 (F1): abas do Planeamento só com o Job On ver.
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.SoVer.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.SoVer.cs :: PlaneamentoSumarioPageTests.WithJobOnViewOnly_TheTabsLeadToPagesTheProfileCanOpen`

Selo depois: `833606a8293db3b6`

## A-0180 · 2026-10-27 · ok-do-diogo

Porquê: Histórico do Controlo sem texto de ajuda e sem a coluna Resumo (Diogo, 27/10/2026).
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra8ZfpVoajgn5AkYsPBX7fMg (pedido cmsg_01K75FTs9bQnbhhH7qttpgra1vX5iME1pheahiGpCrhsqU; erros cmsg_01K75FTs9bQnbhhH7qttpgraCq647u2zepuXXqqCxiWAKr)
Novos: 0 · mudaram: 2 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.GuardarComparacao_CriaORegistoDoHistorico`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.Historico_ProcuraUnificadaPelosQuatroTipos`

Selo depois: `aded66bcf5324e3a`
## A-0182 · 2026-10-27 · teste-novo

Porquê: Posto da RI: a lista dos reparadores (conta ativa, PIN do posto, RI ver ou criar, nunca o ADMIN) sai pela mesma ordem e com o mesmo número de consultas para 5 ou 100 pessoas (DMO-449, 0.3.0-beta.139)
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PostoRi.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PostoRi.cs :: PostgresMigrationIntegrationTests.PostoRi_ListaSoQuemTemRiEPinPorNome_ComAsMesmasConsultasPara5Ou100Reparadores`

Selo depois: `4167dbe27ca52e85`

## A-0183 · 2026-10-27 · teste-novo

Porquê: Juntar a integracao (com o #195, DMO-449, 0.3.0-beta.139) ao design novo #180: os 2 testes novos do Posto da RI (A-0182) passam à impressão do ramo
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PostoRi.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PostoRi.cs :: PostgresMigrationIntegrationTests.PostoRi_ListaSoQuemTemRiEPinPorNome_ComAsMesmasConsultasPara5Ou100Reparadores`

Selo depois: `9a4eeaa62eadde2a`

## A-0184 · 2026-10-27 · ok-do-diogo

Porquê: Ficha de utilizador sem email e email opcional ao criar (DMO-442): o teste da ficha deixa de esperar o email e o das recusas deixa de recusar quem não tem email; entra o teste da conta sem email.
Referência: Diogo, 27/10/2026, cartão cmsg_01K75FTs9bQnbhhH7qttpgraX6am21dAvjuJ2KELwbTMvF («Sim, aprovo»); pedido cmsg_01K75FTs9bQnbhhH7qttpgraEMxzGkVktKoK7yyAw6RnoU
Novos: 1 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs :: ToggleSaveFailureInterceptor.CreateDmoUser_SemEmail_CriaAContaQueEntraPeloNumero`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs :: ToggleSaveFailureInterceptor.CreateDmoUser_InvariantViolations_AreTypedRefusalsBeforeAnyWrite`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.AdminUsers_FichaShowsHumanFacts_AndTheActionsMovedFromTheTablePostToTheSameIndexHandlers`

Selo depois: `4c6656d1bd4956fb`

## A-0185 · 2026-10-27 · ok-do-diogo

Porquê: Definições em três separadores (DMO-440): os testes acompanham os pedidos do Diogo (estrutura, um só cadeado, contagens no singular/plural, sem círculos de ajuda, pastas só com o estado, «Ler pasta» com Opções; o link «Ler a pasta da produção» do Peso abre Este computador)
Referência: estrutura cmsg_01K75FTs9bQnbhhH7qttpgraXJAtaKT9Zq5Rd7rwLJZDkP; PIN cmsg_01K75FTs9bQnbhhH7qttpgra1QwpiaNcti2W7y9EaFGtbE; plural cmsg_01K75FTs9bQnbhhH7qttpgra5kxKKSuy4sbRBmUpdgdxfR; botões no canto cmsg_01K75FTs9bQnbhhH7qttpgraQVvJwFV5LYqaod3CuetwJ6; sem comentários cmsg_01K75FTs9bQnbhhH7qttpgraJTppk4FtwLL69Lux9wbqXg e cmsg_01K75FTs9bQnbhhH7qttpgraSzhzka8hBipHGMtEsRNy7C; Opções cmsg_01K75FTs9bQnbhhH7qttpgraHPADVWa4FvWFsfvbAFqbcy; «sim» para a main cmsg_01K75FTs9bQnbhhH7qttpgraFhzyFUMh7gh2JDP4czaFou
Novos: 4 · mudaram: 20 · saíram: 4

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Armazem_AtrasDoMesmoCadeado_ESemTextoComAListaVazia`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Controlo_PartesPorCimaDosCartoes_UmBotaoPorCartao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OsSeparadores_AMinhaContaEsteComputadorEGerais_CadaEntradaMostraSoASuaSeccao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.UmSoCadeado_PorCimaDasPastasEDasGerais_QuandoSaoSoDeLeitura`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.AdminUsers_ListCount_ShowsTheTotal_NotJustTheCurrentPage`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.cs :: BoquilhasRegistoPageTests.ToolCatalog_SearchAndPaginationPreserveSelectionContext_AndExcludeOtherToolTypes`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.cs :: BoquilhasRegistoPageTests.ToolCatalog_SoAMaquinaNaoMostraALista_OTextoMostra_EAMaquinaSoReduz`
- mudou: `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs :: BqRepairerSettingsPageTests.ExcelDasBoquilhas_OCartao_AVersaoMudaComUmMovimento_EOFicheiroVoltaEscrito`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.CompararPesos_CalculaAoVivoSemGravar`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.CompararPesos_LeiturasMasFicamDeForaComAviso_AsOutrasComparam`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoPorCombinacaoPageTests.cs :: Producao.ComDoisPesos_OResumoARevisaoEODetalheMostramOsDois`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Detalhe.cs :: ControloCapabilitiesCutHPageTests.RevisaoDetalhe_OsPegamentosUsamOFormatoDaFolha`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs :: ControloCapabilitiesCutHPageTests.ResumoDoControlo_SeparadorComProcura_MostraOControloInteiro`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AsPastas_SaoGerais_SoAsAcoesFicamParaTodos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_SoVeAsDefinicoesDoAdmin_ENaoDosModulos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Pessoal_SoTemAContaAPalavraPasseEAAparencia`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.Admin_TemDefinicoesProprias_SoAApp`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Folhas.cs :: PegamentosPageTests.ReviewerScreens_ShowBothSheets_TheAlertOfTheSecondIsNotHidden`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.Caret_UmaRegraParaTudo_AEsquerdaDoTexto`
- mudou: `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs :: SharedComponentsTests.HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial`
- mudou: `tests/visual/definicoes_parcial_browser.py`
- mudou: `tests/visual/excel_bq_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Armazem_SemCadeado_ESemTextoComAListaVazia`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Controlo_FuncoesNoMenuLateral_UmBotaoPorCartao`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OMenu_PessoalEOsModulosAtivos_CadaEntradaMostraSoASuaSeccao`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OMenu_TemOCadeadoNosModulos_QuandoOGeralESoDeLeitura`

Selo depois: `f07dea73a879bfe6`

## A-0186 · 2026-10-27 · ok-do-diogo

Porquê: Templates DMO-441: os utilizadores escolhem-se como os módulos; a ligação à página do utilizador passa de data-dmo-open (linha da tabela) para href (botão «Página») e entra o teste de associar e remover.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraMmJ1Ugn2nxTvbMq94b7Qpq
Novos: 1 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.AdminTemplates_UsersLikeModules_AssociateWithoutTemplate_RemoveExisting_LeaveOthers`
- mudou: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs :: AdminUsersAndTemplatesPagesTests.AdminUsers_ListShowsApplicationFacts_StandByAndTemplateEditing_ThroughTheSingleRelation`

Selo depois: `0e4d37baa6e06955`

## A-0187 · 2026-10-27 · ok-do-diogo

Porquê: Imprimir do Job On a preto e branco (PDF do Diogo, 27/10/2026): no Trabalho de equipa os nomes passam a sigla + nome («CM Contra-Molde»), o FF fica à parte e o AN sai dessa folha (continua nas outras duas). Teste novo: CS Abertos/Fechados, Tampão «34.95 x 6.5mm», cabeçalho e páginas.
Referência: «sim» do Diogo, cmsg_01K75FTs9bQnbhhH7qttpgraRywqAAMmePUCTDoN6WPF1F (DMO-447, 0.3.0-beta.145)
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ImprimirPretoBranco.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ImprimirPretoBranco.cs :: JobOnPageTests.Folha_ImprimirPretoBranco_CsAbertosFechados_TampaoDiametroXCalote_EPaginas`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_DecisoesDoDiogo_ComponentesSeparadosAnComLoteCaloteUnicaSemComentarios`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs :: JobOnPageTests.Folha_Imprimir_TemOMenuEAsFolhasComOsDadosDoJobOn`

Selo depois: `7df95d24814fbd98`

## A-0188 · 2026-10-27 · contagem-lista

Porquê: Peso: o mesmo envio de leituras grava uma só vez, também depois de um reinício ou de uma resposta perdida; leituras iguais de envios diferentes gravam; o mesmo identificador de outra pessoa, operação ou com outras leituras é recusado; dois envios iguais ao mesmo tempo dão uma leitura; uma falha a meio não deixa nem leitura nem operação (DMO-450, 0.3.0-beta.146). A lista das migrações ganha 20261027030704_OperacoesRegistadasImpressao.
Novos: 8 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoUmEnvio.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoUmEnvio.cs :: PostgresMigrationIntegrationTests.PesoLeitura_DuasLeiturasIguaisDeEnviosDiferentes_GravamAsDuas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoUmEnvio.cs :: PostgresMigrationIntegrationTests.PesoLeitura_FalhaAMeioDaGravacao_NaoFicaNemALeituraNemAOperacao_EORepetirGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoUmEnvio.cs :: PostgresMigrationIntegrationTests.PesoLeitura_OMesmoEnvioDuasVezesAoMesmoTempo_FicaUmaSoLeitura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoUmEnvio.cs :: PostgresMigrationIntegrationTests.PesoLeitura_OMesmoEnvioOutraVez_DepoisDeUmReinicio_NaoGravaDuasVezes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoUmEnvio.cs :: PostgresMigrationIntegrationTests.PesoLeitura_OMesmoIdentificadorNoutraCoisa_ERecusadoENadaFica`
- novo: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.UmEnvio.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.UmEnvio.cs :: PesoToolScopedPageTests.RegisterMeasurement_OMesmoEnvioDepoisDeUmReinicio_NaoGravaOutraVez_EUmEnvioNovoIgualGrava`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `e184a131a41a6920`

## A-0181 · 2026-10-27 · teste-novo

Porquê: Comparar peso (Diogo, 27/10/2026): Pesos compatíveis (mesma referência da CM e máquina), escolha pela linha, tabela igual à do Peso, só a calote muda pela Calote Tampo; nada é gravado
Novos: 18 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: PesoCompararQueryTests.DoHistorico_PeloInicioDaReferencia_SemJobOnValemAsMaquinasDaFicha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: PesoCompararQueryTests.DoHistorico_SemMaquina_FicaADoPesoMaisRecente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: PesoCompararQueryTests.DoPesoAntigo_FicaOPedidoNaCmDele`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: PesoCompararQueryTests.DoPeso_SoAMesmaReferenciaEMaquina_OMaisRecenteDeCadaCm`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: PesoCompararQueryTests.OutraMaquinaEscolhida_TrocaAsCm`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: PesoCompararQueryTests.SemTampaoNoPeso_ValeODoJobOn`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs :: PesoCompararQueryTests.VariasReferencias_PedeParaEscolher`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoSimuladoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoSimuladoTests.cs :: PesoTampaoSimuladoTests.SoACaloteMuda_OPesoMudaPelaCaloteTampo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoSimuladoTests.cs :: PesoTampaoSimuladoTests.Texto_DiametroXCalote_SemZerosAMais`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs :: PesoCompararPageTests.CaloteQueNaoSeLe_Avisa`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs :: PesoCompararPageTests.CaloteSimulada_MudaSoEsseLadoPelaCaloteTampo_ENaoGrava`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs :: PesoCompararPageTests.DoHistorico_PelaReferenciaDaCm_EOutraMaquina`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs :: PesoCompararPageTests.DoPeso_ListaOsCompativeis_EComparaComOSeguinte`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs :: PesoCompararPageTests.OPesoEOHistorico_TemOBotao_EOPainelLateralAbreAPagina`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs :: PesoCompararPageTests.OutraMaquina_ESemOutroPeso_DizQueNaoHaComQueComparar`

Selo depois: `f211690090c32975`

## A-0189 · 2026-10-27 · teste-novo

Porquê: Posto da RI com travão por origem (um PIN de cada vez, espera crescente da 5.ª falha em diante, a conta nunca bloqueia) e sessão obrigatória por omissão, só as páginas públicas abrem sem sessão (DMO-451, 0.3.0-beta.148)
Novos: 7 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/PaginasSemSessaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PaginasSemSessaoTests.cs :: PaginasSemSessaoTests.PorOmissao_TodasAsPaginasPedemSessao`
- novo: `tests/DMO.Alpha.Web.Tests/PaginasSemSessaoTests.cs :: PaginasSemSessaoTests.SemSessao_SoAbremAsPaginasPublicas`
- novo: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: PostoTravaoTests.Espera_SoDepoisDaQuintaFalha_ECrescendoAteTrintaSegundos`
- novo: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: PostoTravaoTests.MesmaOrigem_UmPinDeCadaVez_OutraOrigemNaoEspera`
- novo: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: PostoTravaoTests.Posto_PinsErrados_EsperamCadaVezMais_EOPinCertoEntraNaMesma`

Selo depois: `d16d7799e427d7ce`

## A-0190 · 2026-10-27 · contagem-lista

Porquê: Juntar o #199 à integracao (DMO-452, DMO-457, 0.3.0-beta.149): teste novo de remover um reparador das Boquilhas usado no Armazém (vistoria BD-02), teste novo do índice da referência das ferramentas em maiúsculas (BD-04, migração 20261027050241_ToolsReferenciaMaiusculas, Diogo 27/10/2026 05:00, cartão cmsg_01K75FTs9bQnbhhH7qttpgraQWakNx4bC4V2mjtidjJLzt); a migração nova entra na lista das migrações (só a lista muda)
Novos: 4 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.RemoverReparadorArmazem.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.RemoverReparadorArmazem.cs :: PostgresMigrationIntegrationTests.RemoverReparadorUsadoNoArmazem_OMovimentoFicaComONome`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ToolsReferenciaMaiusculas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ToolsReferenciaMaiusculas.cs :: PostgresMigrationIntegrationTests.ToolsReferenciaMaiusculas_AsProcurasSemOlharAMaiusculasUsamOIndice`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `931c72c58f9a8361`

## A-0191 · 2026-10-27 · contagem-lista

Porquê: Vistoria ao frontend (DMO-453, 0.3.0-beta.150): testes novos das marcas de versão e da cache dos ficheiros (F1), do filtro ao escrever com a sessão terminada, um erro ou sem rede (F2) e do rascunho do Job On por pessoa e por um dia (F3); package.json só acrescenta vistoria-frontend.test.cjs à lista do test:js
Novos: 13 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/FicheirosComVersaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FicheirosComVersaoTests.cs :: FicheirosComVersaoTests.CadaFolhaEScriptDasPaginas_LevaAMarcaDeVersao`
- novo: `tests/DMO.Alpha.Web.Tests/FicheirosComVersaoTests.cs :: FicheirosComVersaoTests.OLogin_PedeCadaFolhaComAMarcaDeVersao`
- novo: `tests/DMO.Alpha.Web.Tests/FicheirosComVersaoTests.cs :: FicheirosComVersaoTests.UmFicheiroComMarca_GuardaSeUmAno_ESemMarca_PerguntaSempre`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F2: com a sessão terminada, o filtro avisa e dá o link «Entrar»`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F2: com um erro do servidor, o filtro avisa que não foi possível procurar`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F2: o filtro ao escrever troca os resultados quando a resposta traz a lista`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F2: sem rede, o filtro repete a mesma procura quando a ligação volta`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F3: o que se escreve fica guardado com o dono`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F3: o rascunho de outra pessoa não aparece e sai do browser`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F3: o rascunho de quem abre a folha volta`
- novo: `tests/ShellProof/vistoria-frontend.test.cjs :: F3: um rascunho com mais de um dia não volta; um de 23 h volta`
- mudou: `tests/ShellProof/package.json`

Selo depois: `10f6a12c79e57112`

## A-0196 · 2026-10-27 · ok-do-diogo

Porquê: Limpeza do código que só os testes usavam (DMO-458, 0.3.0-beta.154): saem HasCapability, ArmazemArmarios.Prateleira, ArmazemMapaPosicao.Misturada, ArmazemProximoJobOn.PorTirar/Indisponiveis, ControloDocumentPaths.GetProductionFolder, HistoricoLinha.OutrosTipos, PesoCmDeParteHandler.ContarPorAprovarAsync e CmDecisaoLinha.PorAprovar, ReparacaoProgramadaMovimentos.AoMovimentoAsync, DeleteJobOnPreview.HasRecords, a parcial _JobOnFichaFolha e a escolha data-dmo-procura-valor. Os testes verificam o mesmo pelo que fica (Capabilities, as ferramentas, a contagem na base, AoMovimentosAsync, o nome do ficheiro); saem só as asserções e os 2 testes da função que saiu.
Referência: Diogo, thread «Limpar o código da app», 27/10/2026 05:22 (cmsg_01K75FTs9bQnbhhH7qttpgraXWcTtH6xwwn13Snc37nLi1), «sim» à lista dos 9 testes
Novos: 0 · mudaram: 19 · saíram: 2

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminAuthorizationServiceTests.cs :: AdminAuthorizationServiceTests.Resolve_AdminEmailUser_IsAlwaysResolvedWithoutTemplate`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminAuthorizationServiceTests.cs :: AdminAuthorizationServiceTests.Resolve_NoCurrentTemplate_HasNoTemplateDerivedCapabilities`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminAuthorizationServiceTests.cs :: AdminAuthorizationServiceTests.Resolve_RenamedTemplate_EffectiveAccessIsUnchanged`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminAuthorizationServiceTests.cs :: AdminAuthorizationServiceTests.Resolve_StandBy_DisablesCurrentAccessButPreservesAssignmentFacts`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminAuthorizationServiceTests.cs :: AdminAuthorizationServiceTests.Resolve_TemplateWithBothControlos_GrantsBoth`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminAuthorizationServiceTests.cs :: AdminAuthorizationServiceTests.Resolve_WithCurrentTemplate_ResolvesTheTemplateCapabilities`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.APosicao_CorredorEstanteEPrateleira`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OParDaMesmaReferenciaELote_NaoEMisturado`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.CadaFerramenta_OndeEstaEOQueFalta`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.ForaDoArmazemEDoFabrico_NaoDisponivel`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloDocumentPathsTests.cs :: ControloDocumentPathsTests.Segments_WithPathSeparators_NeverBecomeStructure`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoDecisoesDoDiogoTests.cs :: PesoDecisoesDoDiogoTests.ColocarDeParte_FicaPorAprovar_EAAprovacaoEPorCm`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoDecisoesDoDiogoTests.cs :: PesoDecisoesDoDiogoTests.MudarAEscolha_TiraAAprovacao_EManterNaoVaiParaAprovacao`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProgramadaConcorrente.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/DeleteJobOnHandlerTests.cs :: DeleteJobOnHandlerTests.Confirmacao_ContaOQueSePerde`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs :: ReparacoesInternasTests.EliminarOJobOn_ContaELevaAsReparacoes`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: FerramentaHistoricoQueryTests.NaBq_OsMfDeCadaJobOnPeloBqContext`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs :: JobOnFolhaTests.Folha_SemNadaEscrito_MostraOsVaziosEOEditar_SemAFichaAntiga`
- mudou: `tests/ShellProof/escrito-perdido.test.cjs :: numa procura com linhas, as linhas do texto anterior não escolhem enquanto o novo não responde`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloDocumentPathsTests.cs :: ControloDocumentPathsTests.ProductionFolder_Is_Dir_Reference_Production`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloDocumentPathsTests.cs :: ControloDocumentPathsTests.ProductionFolder_TrimsTrailingSeparatorsOfBaseDir`

Selo depois: `98d72642ab0f5365`

## A-0198 · 2026-10-27 · teste-novo

Porquê: Teste do ensaio (27/10/2026): operador sem acesso vê «Sem acesso» no login (DMO-460)
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/LoginSemAcessoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/LoginSemAcessoTests.cs :: LoginSemAcessoTests.Operador_NoAdmin_VeSemAcesso_SemFormularioDeEntrada`
- novo: `tests/DMO.Alpha.Web.Tests/LoginSemAcessoTests.cs :: LoginSemAcessoTests.SemSessao_OLoginMostraOFormulario`

Selo depois: `3a0eca909ecdff43`

## A-0206 · 2026-10-27 · ok-do-diogo

Porquê: «PDF Pegamentos» só aparece com medições (Diogo, 27/10/2026, cartão cmsg_01K75FTs9bQnbhhH7qttpgraB4cWtFy2LfgzGWkBMpzRuk); os 2 testes deixam de esperar o botão sem medições (DMO-460)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraTqHfWEMTVqx3FqVyLJ8Xiw
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PdfPegamentos.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PdfPegamentos.cs :: ControloCapabilitiesCutHPageTests.Resumo_PdfPegamentos_ApareceComMedicoes`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs :: ControloCapabilitiesCutHPageTests.Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs :: ControloResumoPageTests.OpenPreCutJobOn_FolhaShowsCanonicalContext_BackfillsControloIdOnce`

Selo depois: `17136d8bbe5adaa3`

## A-0212 · 2026-10-27 · ok-do-diogo

Porquê: Job On novo: com referência existente abre a última para ver e só o «Duplicar» duplica (Diogo, 27/10/2026); o teste J3 deixa de esperar o duplicar automático (DMO-460)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraGfcQKkCC3T6ysms3JfYV83
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.Ronda.cs :: JobOnPageTests.Criar_ReferenciaExistente_ComProducaoNova_AbreODuplicarComAProducaoEALinha`

Selo depois: `a9e4150c33bc34b7`

## A-0199 · 2026-10-27 · ok-do-diogo

Porquê: Planeamento também para o Job On ver (DMO-461): o «Job On» de quem só vê passa a levar ao Planeamento em leitura, sem Histórico; testes novos do Planeamento só leitura, do que o Job On ver não vê (Duplicar, Controlo, Reparação, Boquilhas, ficha da ferramenta sem as Ferramentas; Diogo cmsg_01K75FTs9bQnbhhH7qttpgraKZZGQJJTCtaNvH2hUi2AXY) e do Sumário (quem só vê recebe os Job On criados, as mudanças e as alterações; blueprint, Sumário §2).
Referência: «sim» do Diogo no cartão cmsg_01K75FTs9bQnbhhH7qttpgra4tZH7cFTTV4YNG2gutByrM (DMO-461, 0.3.0-beta.157)
Novos: 6 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PlaneamentoSoVer.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PlaneamentoSoVer.cs :: JobOnPageTests.Planeamento_JobOnVer_VeOCalendarioEAsLinhas_SemCriarNemHistorico`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.SoVerSemFerramentas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.SoVerSemFerramentas.cs :: JobOnPageTests.JobOnVer_SoVeOPlaneamentoEOsJobOn_SemDuplicarNemOutrosModulos`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.SoVerJobOn.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.SoVerJobOn.cs :: PlaneamentoSumarioPageTests.JobOnVer_RecebeOsJobOnEAsMudancas_ComLinkQueAbre`
- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.SoVer.cs :: PlaneamentoSumarioPageTests.WithJobOnViewOnly_TheTabsLeadToPagesTheProfileCanOpen`

Selo depois: `ecf2653287d9d651`

## A-0200 · 2026-10-27 · teste-novo

Porquê: Peso, separador Histórico: a procura (filtros, dropdown das produções da referência, produção fora do dropdown, referência como ferramenta, sem Job On) passou para ControloHistoricoProcura e é testada sem a página (DMO-462, 0.3.0-beta.158)
Novos: 9 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.OTipoEADataSaoLidosDoPedido`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.OValorDoDropdown_EscolheONumeroDaquelaReferencia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.SemJobOn_NaoHaDropdownDasProducoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.SemNenhumFiltro_NaoProcura`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.SemResultadosPelaReferencia_ProcuraAReferenciaComoFerramenta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.UmLinkAntigoSoComONumero_EscolheAMaisRecenteComEsseNumero`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.UmaProducaoForaDoDropdown_FiltraEEntraNoDropdown`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs :: ControloHistoricoProcuraTests.UmaProducaoQueJaNaoEDaReferencia_NaoFiltra`

Selo depois: `74292cfeef1490d1`

## A-0216 · 2026-10-27 · teste-novo

Porquê: Testes de páginas que falhavam ao acaso com erro 500: as aplicações de teste deixam de gastar vigias de ficheiros do Linux (inotify, 128 por utilizador); o teste prova que uma aplicação de teste arranca e abre o login sem gastar nenhuma (DMO-474, 0.3.0-beta.175)
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/SemVigiarFicheiros.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/SemVigiarFicheirosTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/SemVigiarFicheirosTests.cs :: SemVigiarFicheirosTests.UmaAplicacaoDeTeste_ArrancaEAbreOLogin_SemGastarVigiasDeFicheiros`

Selo depois: `c22447c6290ce98f`

## A-0208 · 2026-10-27 · ok-do-diogo

Porquê: Comparar peso no Resumo: produção fixa, sem «a que mais puxa/mexe», + e − ao lado da massa, só «Comparar pesos» e «Guardar», exemplo «Ex.: 199,64» (DMO-469, .175).
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraJ6x5Umit13ZS5WQioYVTR2 (27/10 07:08, pedido em cmsg_01K75FTs9bQnbhhH7qttpgraMjVxpFF2jJp6oRNmt8uTaH)
Novos: 3 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.ProducaoFixa.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.ProducaoFixa.cs :: PesoComparacaoPageTests.PeloResumo_AProducaoFicaFixa_SemReferenciaNemProducao`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.ProducaoFixa.cs :: PesoComparacaoPageTests.ProducaoFixa_SegueNoCompararENoGuardar`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.NoResumo.cs :: PesoComparacaoPageTests.Comparacao_AbreNoResumoDaProducao_SemSeparadorProprio`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.CamposDecimais_SaoTextoComTecladoDecimal_ParaAceitarVirgula`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.ComparacaoTab_ATerraPreselecionadoPelaFolha`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.ComparacaoTab_MostraBaseEEfeitoNaMedia`
- mudou: `tests/ShellProof/peso-comparacao.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/peso-comparacao.test.cjs :: «Comparar pesos» e «Guardar comparação» trocam só as zonas, sem recarregar; as linhas adicionam-se no formulário novo`

Selo depois: `95e012cdaed29670`

## A-0210 · 2026-10-27 · teste-novo

Porquê: DMO-471 SAP: teste novo das consultas por ferramenta (B3 vistoria backend)
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.UsoSapConsultas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.UsoSapConsultas.cs :: PostgresMigrationIntegrationTests.UsoSap_ImportarComAsMesmasConsultasPara5Ou100Ferramentas_NoPostgreSql`

Selo depois: `eb42111e0485ab74`

## A-0219 · 2026-10-27 · ok-do-diogo

Porquê: A Pasta partilhada dos PDFs fica só com Associar/Remover: sai o campo do nome da pasta, o Guardar e o aviso de nome diferente (Diogo, 27/10/2026: «remover este erro e o campo para escolher nome», «a app só tem de seguir o dir»). Dois testes passam a verificar que o campo e o Guardar não existem; o que gravava o nome sai.
Referência: «sim» do Diogo, cmsg_01K75FTs9bQnbhhH7qttpgraGh1feCmYhsHNv26gMB6hYz (DMO-477, 0.3.0-beta.178)
Novos: 0 · mudaram: 2 · saíram: 1

- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AsPastas_SaoGerais_SoAsAcoesFicamParaTodos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Controlo_PartesPorCimaDosCartoes_UmBotaoPorCartao`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.APastaDosPdfs_GravaSeParaTodos_ComAPermissao`

Selo depois: `138f201af6d7df8a`

## A-0213 · 2026-10-27 · ok-do-diogo

Porquê: Peso sem Job On: com a CM e a BQ escolhidas, os volumes da punção e da marisa aparecem uma só vez, na faixa dos volumes (Volume Punção / CM, Volume Marisa / BQ); os 2 testes procuram esses nomes. Teste novo: o Peso aberto sem produção já não mostra o cartão Ferramentas e os Job Ons para associar são linhas que se escolhem, com um só «Associar» (DMO-473, 0.3.0-beta.169)
Referência: cartão de decisão do Diogo na thread «Arrumar a página do Peso», 27/10/2026 06:42 (cmsg_01K75FTs9bQnbhhH7qttpgra1g1D25NUvmLaCqFq1H1mpx)
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoAbertoArrumado.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoAbertoArrumado.cs :: ControloCapabilitiesCutHPageTests.PesoAberto_SemFerramentas_EAssociarNumaTabelaComUmSoBotao`
- mudou: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs :: PesoToolScopedPageTests.ToolScopedFlow_CreateRecordConsult_ThroughThePage`
- mudou: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs :: PesoToolScopedPageTests.ToolScopedFlow_ResolvesByTheHumanReference_WhatTheSheetsShow`

Selo depois: `172e1a536bce7902`

## A-0214 · 2026-10-27 · ok-do-diogo

Porquê: A BQ do Peso sem Job On deixa de ter lote e a associação ao Job On conta a referência da BQ (qualquer lote). Os 3 testes da associação usavam um outro lote da mesma BQ (bq-b, Y232) como «outra BQ»: passam a usar uma BQ de outra referência (bq-c, Y300), a mesma recusa. O teste da folha por referência deixa de esperar o lote da BQ (12) no cartão. Testes novos: o Peso de outro lote da mesma BQ associa-se e fica com o lote da produção; o campo «BQ — lote» não aparece com a marisa igual.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra1F8zuYkECXUkomt4uk6jE4 («sim», 27/10/2026 07:01) e cmsg_01K75FTs9bQnbhhH7qttpgraVKRTRjGjK7GvF2BEsEHkoc («nao tem lote»); cartão cmsg_01K75FTs9bQnbhhH7qttpgraU48Jy5Uk3vfwYNztsu9MAk «Tirar e associar pela referência»
Novos: 2 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs :: AssociatePesoToControloHandlerTests.Associate_PesoOfAnotherLotOfTheSameBq_IsAcceptedAndTakesTheProductionLot`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoAbertoArrumado.cs :: ControloCapabilitiesCutHPageTests.PesoSemJobOn_BqSemCampoDoLote_QuandoAMarisaEIgual`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs :: AssociatePesoToControloHandlerTests.Associate_PesoOfAnotherBq_IsRefusedAsBqMismatch`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs :: AssociatePesoToControloHandlerTests.Candidates_AfterSwapWithoutCurrentPeso_AreLoosePesosOfTheToolsTheProductionHadOrHas`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs :: AssociatePesoToControloHandlerTests.Productions_ForPeso_IncludeJobOnsThatHadTheCmBeforeASwap`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoAbertoArrumado.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs :: PesoToolScopedPageTests.ToolScopedFlow_ResolvesByTheHumanReference_WhatTheSheetsShow`

Selo depois: `350f94e337b7d721`

## A-0215 · 2026-10-27 · ok-do-diogo

Porquê: O Peso sem produção deixa de mostrar a nota «sem associação a produção — CM · Lote» (a CM já está no título). O teste da folha deixa de a esperar; o da identificação gravada passa a provar que o lote editado no catálogo (33) não aparece; o dos Pegamentos deixa de esperar a nota no Peso.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraNyjVEgBfESart96h9XoX9j («remover», 27/10/2026 07:08, a nota «sem associação a produção — CM ST100 · Lote 8»)
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.SavedPesoNavigation_UsesItsOwnProduction_AndDoesNotAssociateToolScopedPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs :: PesoToolScopedPageTests.SavedPeso_KeepsRecordedIdentificationAfterCatalogEdit_AndDownloadsPdf`
- mudou: `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs :: PesoToolScopedPageTests.ToolScopedFlow_CreateRecordConsult_ThroughThePage`

Selo depois: `be84027a25ad05f6`

## A-0220 · 2026-10-27 · teste-novo

Porquê: Filtro «Informação» do Sumário com as opções Importante, Job On (criados, mudanças, alterações e experiências), Armazém e Controlo (DMO-475, 0.3.0-beta.181): testes novos das opções, do Job On, do Controlo no topo e das opções por módulo
Novos: 5 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Filtros.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Filtros.cs :: PlaneamentoSumarioPageTests.Filtro_Controlo_MostraSoOsDoControloNoTopo`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Filtros.cs :: PlaneamentoSumarioPageTests.Filtro_JobOn_JuntaCriados_Mudancas_EExperiencias_SemOsImportantes`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Filtros.cs :: PlaneamentoSumarioPageTests.Filtro_SoMostraAsOpcoesDosModulosDaConta`
- novo: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Filtros.cs :: PlaneamentoSumarioPageTests.Filtro_TemImportante_JobOn_Armazem_EControlo`

Selo depois: `7dc647986f4267b8`

## A-0221 · 2026-10-27 · ok-do-diogo

Porquê: A opção «Mudanças» do filtro do Sumário passa a estar dentro de «Job On» (DMO-475): o teste confirma a opção «Job On» escolhida com o link antigo ?tipo=comecam; o resto do teste fica igual
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra6boArqNgm7gomFxoytyUEU
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs :: PlaneamentoSumarioPageTests.Linhas_TemADataAFrente_AMaquinaComACor_ESemContagens`

Selo depois: `6500f7b9a7dbe746`

## A-0211 · 2026-10-27 · contagem-lista

Porquê: DMO-472 Posto da RI: testes novos (só a Reparação Interna; a sessão do posto fecha ao fim de 3 min sem uso; minutos nas Definições); o package.json só ganha o ficheiro posto-sair.test.cjs na lista
Novos: 17 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PostoMinutosSettingTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PostoMinutosSettingTests.cs :: PostoMinutosSettingTests.ForaDasOpcoes_ERecusado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PostoMinutosSettingTests.cs :: PostoMinutosSettingTests.OsMinutosLidos_SemValorValidoSao3`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PostoMinutosSettingTests.cs :: PostoMinutosSettingTests.UmaDasOpcoes_Grava`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_ComOTempoDasDefinicoes_ASessaoDuraEsseTempo`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_EmUso_ASessaoContinua`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_SemUsoDurante3Minutos_ASessaoFecha`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_SoAReparacaoInterna_PeloLoginTodosOsModulos`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.SairDoPosto_VoltaAoPosto_PeloLoginNaoHaTempo`
- novo: `tests/ShellProof/posto-sair.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/posto-sair.test.cjs :: com mais tempo nas Definições, espera esse tempo`
- novo: `tests/ShellProof/posto-sair.test.cjs :: em uso, avisa o servidor no máximo uma vez por minuto`
- novo: `tests/ShellProof/posto-sair.test.cjs :: escrever num campo conta como uso`
- novo: `tests/ShellProof/posto-sair.test.cjs :: fora do posto (login normal) não faz nada`
- novo: `tests/ShellProof/posto-sair.test.cjs :: quem está a usar a página não é posto fora`
- novo: `tests/ShellProof/posto-sair.test.cjs :: sem uso durante os minutos do posto, sai e volta ao posto`
- mudou: `tests/ShellProof/package.json`

Selo depois: `456e1ef94b10f58a`

## A-0205 · 2026-10-27 · ok-do-diogo

Porquê: Controlo das Definições numa só vista (DMO-467): o teste das partes do Controlo dá lugar ao da vista única
Referência: pedido «inserir tudo na opção do controlo e voltar a reduzir» e «sim» cmsg_01K75FTs9bQnbhhH7qttpgraPdgDP4VBjupRjBgYAbyMQp
Novos: 1 · mudaram: 0 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Controlo_TudoNumaSoVista_UmBotaoPorCartao`
- saiu: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.Controlo_PartesPorCimaDosCartoes_UmBotaoPorCartao`

Selo depois: `0eecb24220a0e7a7`

## A-0222 · 2026-10-27 · ok-do-diogo

Porquê: Reparadores do Armazém sem «Recebe»: todos recebem CM e MF, os das BQ são outra lista
Referência: cartão do Diogo cmsg_01K75FTs9bQnbhhH7qttpgra1ydbFiy9XkcYkSMVCbc6t3
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_ReparadorDeOutraFamiliaArquivadoOuQueNaoExiste_Recusa`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Situacao_SemMovimentos_EstaNoFabricoSemPropostas`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs :: ArmazemPaginasTests.Definicoes_CriarEMudarReparadores`

Selo depois: `f455c5a50a2aae82`

## A-0223 · 2026-10-27 · teste-novo

Porquê: Sem botões nas linhas (Diogo, 27/10/2026): testes novos para a Revisão (referência do PU/CS no valor) e «O que mudou» da folha do Job On.
Novos: 5 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.SemBotoesNasLinhas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.SemBotoesNasLinhas.cs :: ControloCapabilitiesCutHPageTests.ResumoDoControlo_ReferenciaDoPu_GravaNoValor_SemBotaoNaLinha`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaSemBotoesNasLinhasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaSemBotoesNasLinhasTests.cs :: JobOnFolhaSemBotoesNasLinhasTests.OQueMudou_SemOModuloDaAcao_NaoHaBotaoNoCanto`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaSemBotoesNasLinhasTests.cs :: JobOnFolhaSemBotoesNasLinhasTests.OQueMudou_UmSoBotaoNoCanto_ParaAAlteracaoEscolhida_NenhumNasLinhas`

Selo depois: `3296f158ca7ef80c`

## A-0224 · 2026-10-27 · ok-do-diogo

Porquê: Sem botões nas linhas: modelos de email do Controlo e Visto/Por verificar das Punções passam a clique na linha com botão no canto; testes novos das Punções
Referência: cartão do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraT2BKJWBp736qLPToVcxwwS
Novos: 9 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.Seen_SemValor_TrocaOEstadoQueLaEsta`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.SemBotoesNasLinhas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.SemBotoesNasLinhas.cs :: EnvioEmailPesoPageTests.Modelos_UmSoModelo_VemEscolhido`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.SemBotoesNasLinhas.cs :: EnvioEmailPesoPageTests.Modelos_UmaLinhaEscolhe_EUmSoAbrirNoCanto`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs :: PuncoesPageTests.RegistosSemJobOn_UmaLinhaEscolhe_EUmSoBotaoNoCantoAssocia`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs :: PuncoesPageTests.UmSoRegistoSemJobOn_VemEscolhido`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs :: PuncoesPageTests.Vistos_UmaLinhaEscolhe_EOsDoisBotoesDoCantoTrocam`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs :: PuncoesPageTests.Vistos_UmaSoPuncao_VemEscolhida`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs :: Producao.Definicoes_CriarEMudarUmModelo_ERemetenteQueNaoEEmailNaoGrava`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.TabelaNoBrowser.cs :: PuncoesPageTests.Tabela_TemLargurasFixas_EVaiParaOBrowser`
- mudou: `tests/visual/puncoes_tabela_browser.py`

Selo depois: `9efa6a7b115c5c82`

## A-0225 · 2026-10-27 · ok-do-diogo

Porquê: Resumo do Controlo sem «A combinação da produção» (já está no Estado do documento)
Referência: cartão do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraSa1TjHXyqneQ4wBzRv4r2F
Novos: 0 · mudaram: 2 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs :: ControloCapabilitiesCutHPageTests.Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs :: ControloResumoPageTests.OpenPreCutJobOn_FolhaShowsCanonicalContext_BackfillsControloIdOnce`

Selo depois: `04bff18ccb39d12f`

## A-0232 · 2026-10-27 · ok-do-diogo

Porquê: Sem círculos de dica em lado nenhum: os testes de browser verificam que não há círculos à vista e que o texto da dica passa ao title ao passar o rato
Referência: cartão do Diogo cmsg_01K75FTs9bQnbhhH7qttpgra3LMmVgq4VTe6FMmd197hQs
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/visual/ajuda_junto_browser.py`
- mudou: `tests/visual/boquilhas_catalog_browser.py`
- mudou: `tests/visual/nok_agente_browser.py`

Selo depois: `edb3c3048a671e2a`
## A-0231 · 2026-10-27 · teste-novo

Porquê: Peso: corrigir ou apagar uma leitura com rasto (Diogo 27/10, cmsg_01K75FTs9bQnbhhH7qttpgraPrzkZJc9zsVJxQnPY8frwm «conseguir editar a qq momento»)
Referência: A-0231 (DMO-490)
Novos: 12 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CorrectPesoMeasurementHandlerTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CorrectPesoMeasurementHandlerTests.cs :: CorrectPesoMeasurementHandlerTests.Apagar_TiraALeitura_DeixaRasto_ERenumeraAsPosicoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CorrectPesoMeasurementHandlerTests.cs :: CorrectPesoMeasurementHandlerTests.CorrigirParaPesoDoVidroZeroOuMenos_ERecusado_ALeituraFicaIgual`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CorrectPesoMeasurementHandlerTests.cs :: CorrectPesoMeasurementHandlerTests.Corrigir_MudaALeitura_EDeixaRastoComOAntesEODepois`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CorrectPesoMeasurementHandlerTests.cs :: CorrectPesoMeasurementHandlerTests.GuardarOsMesmosValores_NaoDeixaRasto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CorrectPesoMeasurementHandlerTests.cs :: CorrectPesoMeasurementHandlerTests.LeituraDeOutroPeso_OuQueNaoExiste_NaoEncontrada_NadaMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoCorrigir.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoCorrigir.cs :: PostgresMigrationIntegrationTests.PesoLeitura_CorrigirEApagarADoMeio_RenumeraEDeixaRasto`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoCorrigir.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoCorrigir.cs :: ControloCapabilitiesCutHPageTests.PesoLeitura_ALinhaEscolheSeComUmClique_EACorrecaoAbreNoLugarDaLeituraNova`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoCorrigir.cs :: ControloCapabilitiesCutHPageTests.PesoLeitura_CorrecaoComMassaIlegivel_ERecusada_EOValorEscritoFica`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoCorrigir.cs :: ControloCapabilitiesCutHPageTests.PesoLeitura_CorrigirEApagar_PelaFolha_FicaGravadoComRasto`

Selo depois: `18d27644ced11859`

## A-0231 · 2026-10-27 · contagem-lista

Porquê: A tabela nova PesoMeasurementHistorico e a migração 20261027111956_PesoMeasurementHistorico entram nas listas de tabelas e de migrações; o que os testes verificam fica igual.
Referência: A-0231 (DMO-490)
Novos: 0 · mudaram: 5 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `1330384d86b35971`

## A-0233 · 2026-10-27 · ok-do-diogo

Porquê: O Diogo pediu para distinguir o Controlo anterior (a tabela) da Produção anterior (média e moda da produção), juntar os números da produção anterior numa só faixa e tirar o «Hoje». Os testes passam a pedir os nomes novos, o cabeçalho agrupado, o «Guardar» no cartão da Produção anterior, três zonas parciais em vez de duas e a média e a moda à vista sem janela nem «Hoje».
Referência: Diogo 27/10 11:30–11:31: cmsg_01K75FTs9bQnbhhH7qttpgraSGxUefy18MyznA8nv1rULs, cmsg_01K75FTs9bQnbhhH7qttpgraChGeZYDRdpWXkZFJYD88JY, cmsg_01K75FTs9bQnbhhH7qttpgra64wN7wJBwgW1MPgAeTBaS9 (DMO-491)
Novos: 0 · mudaram: 8 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.UltimasProducoes.cs :: PesoComparacaoPageTests.Folha_AProducaoACompararTemAsUltimas5Anteriores_EAPedidaFica`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.Folha_ComComparacao_PrevisualizarNaoAlteraPesosGuardadosNemPdf`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.PesoAbertoPeloHistorico_ComparaNaFolhaESupoeCaloteSemGravar`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_GravaSemRecarregar_ZonasTrocadasELeituraNovaQueFica`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PropoeAMaisRecente_GuardaOPesoDoSap_EMostraODelta`

Selo depois: `a5e334c99005e344`

## A-0229 · 2026-10-27 · ok-do-diogo

Porquê: Reparação Interna: sem linha escolhida, o texto diz «Escolha a linha acima para registar.» (as linhas estão em cima, não num painel lateral)
Referência: mensagem do Diogo cmsg_01K75FTs9bQnbhhH7qttpgra7NFQu7Q6NgYSLAEsRrjpLy
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs :: ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas`

Selo depois: `1816215d3d43701f`

## A-0227 · 2026-10-27 · contagem-lista

Porquê: Estrutura fixa dos painéis deslizantes e das páginas (DMO-485): testes novos do painel de apoio na estrutura, da marcação da página (<dmo-pagina>), da folha dmo-estrutura.css, do dmo-painel.js e do Job On dentro do painel (título, largura, folha sem título repetido); o package.json do ShellProof só ganha o ficheiro novo na lista
Referência: pedidos do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraXQ2rXx1BrfWwyZwQTG7mM6 e cmsg_01K75FTs9bQnbhhH7qttpgraPV7XbiRJ1Gyqk2HTcdouiD
Novos: 9 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs :: ControloCapabilitiesCutHPageTests.Estrutura_Folha_SoOCorpoDesliza`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs :: ControloCapabilitiesCutHPageTests.Estrutura_JobOnNoPainel_TituloLarguraEFolha`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs :: ControloCapabilitiesCutHPageTests.Estrutura_Pagina_TopoComTituloEAcoesECorpo`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs :: ControloCapabilitiesCutHPageTests.Estrutura_PainelDeApoio_TemOEsqueletoFixo`
- novo: `tests/ShellProof/painel-estrutura.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/painel-estrutura.test.cjs :: Painel com confirmar: com o formulário mudado pede antes de fechar`
- novo: `tests/ShellProof/painel-estrutura.test.cjs :: Painel: Escape fecha (o «cancel» do <dialog>) e DmoPainel abre por código`
- novo: `tests/ShellProof/painel-estrutura.test.cjs :: Painel: o botão abre, o «Fechar» fecha e o foco volta ao botão`
- mudou: `tests/ShellProof/package.json`

Selo depois: `a66892ce2862276f`

## A-0226 · 2026-10-27 · ok-do-diogo

Porquê: Sem botões nas linhas: o × sai dos Pegamentos («Apagar leitura» no canto com a leitura aberta) e das Punções («Remover» no canto, a linha escolhe)
Referência: mensagem do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraRuMahsuiuussnrEx189C1j («perfeito» ao pedido cmsg_01K75FTs9bQnbhhH7qttpgra6Q9srV4paLFsRUhndLXmSD)
Novos: 0 · mudaram: 5 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.Readings_EditByClickingTheValue_DeleteWithTheX_Renumbers`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs :: PuncoesPageTests.Vistos_UmaLinhaEscolhe_EOsDoisBotoesDoCantoTrocam`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs :: PuncoesPageTests.Vistos_UmaSoPuncao_VemEscolhida`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs :: PuncoesPageTests.NumberWithQuotes_IsWrittenAsText_NeverInsideAnOnclick`
- mudou: `tests/visual/puncoes_tabela_browser.py`

Selo depois: `37a5c3540b1e3b39`

## A-0237 · 2026-10-27 · contagem-lista

Porquê: Um painel deslizante de cada vez (DMO-498): testes novos do dmo-painel.js e do dmo-support.js para cliques repetidos e troca de página no mesmo painel; o package.json do ShellProof só ganha o ficheiro novo na lista
Referência: pedido do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraQnjX8BdkkguuiCZDMe1j6e
Novos: 4 · mudaram: 1 · saíram: 0

- novo: `tests/ShellProof/painel-um-de-cada-vez.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/painel-um-de-cada-vez.test.cjs :: Painel de apoio: abrir fecha um <dmo-painel> que esteja aberto`
- novo: `tests/ShellProof/painel-um-de-cada-vez.test.cjs :: Painel de apoio: cliques repetidos na mesma página não a voltam a carregar e outra página troca a que lá está`
- novo: `tests/ShellProof/painel-um-de-cada-vez.test.cjs :: Um painel de cada vez: o mesmo botão não abre outro por cima e outro painel fecha o que está aberto`
- mudou: `tests/ShellProof/package.json`

Selo depois: `4286ec752addb091`

## A-0235 · 2026-10-27 · contagem-lista

Porquê: SAP (DMO-492): testes novos das 12 colunas da ZFMD (ler, gravar, PostgreSQL) e do ficheiro mais antigo que não grava; a lista das migrações acrescenta 20261027121559_SapFabricoColunasZfmd
Novos: 9 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoColunasZfmd.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoColunasZfmd.cs :: PostgresMigrationIntegrationTests.SapFabricoColunasZfmd_GravaTodasAsColunasDoRelatorio_NoPostgreSql`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.ARelatorioDaZfmd_DaTodasAsColunasDeCadaProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.Importar_GravaAsColunasNovas_ESemElasNaoApagaAsJaGravadas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.ODeltaDoPeso_LeSeComOSinal`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.UmRelatorioSemAsColunasNovas_DeixaAsVazias`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapMaisAntigo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapMaisAntigo.cs :: DefinicoesPorModuloPageTests.UmFicheiroDoSapMaisAntigoDoQueOUltimoLido_NaoGrava`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `49685c9a47bd1a6d`

## A-0238 · 2026-10-27 · teste-novo

Porquê: Peso: sair da correção fica na folha, também no painel (Diogo 27/10, cmsg_01K75FTs9bQnbhhH7qttpgra9BpjJuJkq98wTX76Vv5QYA).
Referência: DMO-499
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoCorrigir.cs :: ControloCapabilitiesCutHPageTests.PesoLeitura_SairDaCorrecao_FicaNaFolha_TambemNoPainel`

Selo depois: `a50485f0c4e988d2`

## A-0240 · 2026-10-27 · ok-do-diogo

Porquê: O «% uso» de uma ferramenta passa a ser uma ligação ao histórico do uso (painel): na Procura do Armazém o teste lê o texto da célula sem as etiquetas e verifica a ligação; no Histórico das Ferramentas aceita a ligação dentro do hist-uso
Referência: mensagem do Diogo cmsg_01K75FTs9bQnbhhH7qttpgra3oGkfZkfnuNZ4jftsvNw7B
Novos: 4 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoNoPainel.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoNoPainel.cs :: DefinicoesPorModuloPageTests.OHistoricoDoUso_DeUmaFerramentaQueNaoExiste_DaNaoEncontrada`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoNoPainel.cs :: DefinicoesPorModuloPageTests.OHistoricoDoUso_MostraAsProducoesEOUsoQueFoiAcumulando`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoNoPainel.cs :: DefinicoesPorModuloPageTests.OUsoNaFichaDoArmazem_AbreOHistoricoDoUso`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_MostraOUsoDaUltimaLeitura_ESemLeituraUmTraco`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.Historico_ComControloERi_OMenuTemOsTresDestinos`

Selo depois: `155ae28a8e306475`

## A-0228 · 2026-10-27 · ok-do-diogo

Porquê: A grelha da folha do Job On passa a ser a da maqueta 01: em cima CM, MF, BQ+AN e PU+ARR com a mesma largura; por baixo TP e CS mais estreitos e os Calibres com mais de metade da largura, até ao fim de PU+ARR, para os 8 calibres caberem à vista. O teste deixa de exigir TP e CS com a largura dos cartões de cima e passa a exigir os Calibres largos.
Referência: Diogo 27/10/2026 13:13, cartão cmsg_01K75FTs9bQnbhhH7qttpgraRLtcQMRYqY7oQkMoqYfJnc «Sim, como a maqueta 01»
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/visual/jobon_folha_browser.py`

Selo depois: `1146924046f4fbb9`

## A-0241 · 2026-10-27 · teste-novo

Porquê: Peso, Comparação: a leitura do formulário (linhas em branco, sem massa, massa ilegível, recusas, leituras de fora do backend, aviso pelo PRG, escolha do Peso inicial) passou para PesoComparacaoFormulario e é testada sem a página (DMO-466, 0.3.0-beta.202)
Novos: 6 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoFormularioTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoFormularioTests.cs :: PesoComparacaoFormularioTests.AsLinhasEmBrancoIgnoramSe_ASemMassaEAIlegivelFicamDeFora_AsOutrasComparamSe`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoFormularioTests.cs :: PesoComparacaoFormularioTests.AsQueOBackendDeixaDeFora_ApontamALinhaDoFormulario`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoFormularioTests.cs :: PesoComparacaoFormularioTests.OAvisoAtravessaOPrgDoGuardar`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoFormularioTests.cs :: PesoComparacaoFormularioTests.OPesoInicial_OPedidoSeEDaProducao_SenaoODasFerramentasAtuais_SenaoOMaisRecente`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoFormularioTests.cs :: PesoComparacaoFormularioTests.SemNadaAComparar_ERecusadoComOMotivo`

Selo depois: `28f6e6e2d7095e53`

## A-0242 · 2026-10-27 · teste-novo

Porquê: Definições (DMO-507): teste novo das pastas de «Este computador» em duas colunas fixas
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PastasEmColunas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PastasEmColunas.cs :: DefinicoesPorModuloPageTests.AsPastasDesteComputador_FicamCadaUmaNaSuaColuna`

Selo depois: `a7ed5198b60deb02`

## A-0243 · 2026-10-27 · teste-novo

Porquê: PU NNPB: a gota de cada linha nas Definições (B1 4, C3 3 por omissão, 1 a 4) dita as posições de cada secção na folha e no PDF; punções da linha = secções × gota (Diogo 27/10, cmsg_01K75FTs9bQnbhhH7qttpgra3p4eE4qrDuNcqxPhGnS86V)
Referência: DMO-508
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Gotas.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Gotas.cs :: PuncoesPageTests.Gotas_DaLinha_DitamAsPosicoesDeCadaSeccao_NaFolhaENoPdf`
- novo: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Gotas.cs :: PuncoesPageTests.Gotas_NasDefinicoes_B1Com4EC3Com3PorOmissao_GravamSeERecusamForaDe1a4`

Selo depois: `8814c455ecaa1b86`

## A-0245 · 2026-10-27 · teste-novo

Porquê: PU NNPB: o PDF gera-se sempre com a reserva cheia e os números da reserva inteiros (Diogo 27/10, cmsg_01K75FTs9bQnbhhH7qttpgraGGAMBs5g7FD3HuhbAZe3hc «não consigo extrair PU»)
Referência: DMO-511
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesPdfReservaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesPdfReservaTests.cs :: PuncoesPdfReservaTests.Pdf_ComAReservaCheia_GeraSe_ComOsNumerosInteiros`

Selo depois: `f875edf05732704d`

## A-0244 · 2026-10-27 · ok-do-diogo

Porquê: SAP (DMO-501): das colunas da ZFMD fica só a cadência. Os testes do A-0235 que verificavam as 11 colunas que saem dão lugar aos da cadência (ler, gravar, PostgreSQL sem as 11 colunas, a cadência do Job On e no cabeçalho e na folha impressa); a lista das migrações acrescenta 20261027125325_SapFabricoSoCadencia
Referência: Diogo, thread do SAP, 27/10/2026 14:40: «sim» (cmsg_01K75FTs9bQnbhhH7qttpgra6b5dPSjxGNtQ6TaAXYknn1) ao cartão «Posso trocar os testes das 11 colunas pelos testes da cadência?» (cmsg_01K75FTs9bQnbhhH7qttpgra5BAHBfeNZhYE1nurbBUbQr); decisão de só guardar a cadência: cmsg_01K75FTs9bQnbhhH7qttpgra1Vxhh5skRzdUBVqckufAu9
Novos: 9 · mudaram: 1 · saíram: 7

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoCadencia.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoCadencia.cs :: PostgresMigrationIntegrationTests.SapFabricoCadencia_GravaSoACadencia_NoPostgreSql`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaTests.cs :: FabricoSapCadenciaTests.ACadenciaDoJobOn_EADaOrdemComMaisDias_EmpatadaAMaisRecente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaTests.cs :: FabricoSapCadenciaTests.ARelatorioDaZfmd_DaACadenciaDeCadaProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaTests.cs :: FabricoSapCadenciaTests.Importar_GravaACadencia_ESemElaNaoApagaAJaGravada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaTests.cs :: FabricoSapCadenciaTests.UmRelatorioSemCadencia_DeixaAVazia`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CadenciaSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CadenciaSap.cs :: JobOnPageTests.Folha_MostraACadenciaDoSap_DaOrdemComMaisDias`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoColunasZfmd.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoColunasZfmd.cs :: PostgresMigrationIntegrationTests.SapFabricoColunasZfmd_GravaTodasAsColunasDoRelatorio_NoPostgreSql`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.ARelatorioDaZfmd_DaTodasAsColunasDeCadaProducao`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.Importar_GravaAsColunasNovas_ESemElasNaoApagaAsJaGravadas`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.ODeltaDoPeso_LeSeComOSinal`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapColunasZfmdTests.cs :: FabricoSapColunasZfmdTests.UmRelatorioSemAsColunasNovas_DeixaAsVazias`

Selo depois: `6a2e977f45a5ec02`

## A-0246 · 2026-10-27 · ok-do-diogo

Porquê: DMO-506: a faixa «Planeamento» em cima do calendário (Job On e Sumário) só mostra o que entra, de hoje até uma semana; as produções em curso saem dela (Diogo: «deveria remover o que já entrou»). O teste do painel lateral do Sumário deixa de exigir «Em curso desde» e passa a exigir que a de ontem fique de fora. A gaveta «Linhas» da folha não muda.
Referência: cartão do Diogo cmsg_01K75FTs9bQnbhhH7qttpgra2AkV12PB3yw9WgGicGBS4G («Sim, só o que entra»)
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs :: PlaneamentoSumarioPageTests.Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas`

Selo depois: `68b7521fc048a7b6`

## A-0247 · 2026-10-27 · teste-novo

Porquê: DMO-512: um link para a própria página (corrigir uma leitura nos Pegamentos) fica na página, sem abrir o painel de apoio
Referência: DMO-512
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/ShellProof/painel-propria-pagina.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/painel-propria-pagina.test.cjs :: Painel de apoio: um link para a própria página (corrigir uma leitura nos Pegamentos) fica na página, sem painel`

Selo depois: `fc687ab32acec121`

## A-0249 · 2026-10-27 · ok-do-diogo

Porquê: PU NNPB: a tabela dos repetidos mostra só as repetições (sem a primeira vez); os testes da página e do PDF deixam de esperar a primeira ocorrência
Referência: mensagem do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraGVXj3RGvFEFRDjoK8AKgVb
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.Pdf_PosicoesDaLinha_EOResumoLigaOFicheiroDasPuncoes`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_A1aD12_Reserva_RepetidosComLote_ETotais_NaZonaDaFolha`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.Posicoes_SeguemAsSeccoesDaLinha_GuardadasNasDefinicoes`

Selo depois: `1344320029127839`

## A-0250 · 2026-10-27 · ok-do-diogo

Porquê: PDFs para imprimir (DMO-515): sem setas «Ver detalhe →», «Ver →», «Abrir PDF das punções →» e «← Voltar ao Resumo»; a ligação das punções fica no título PU NNPB; o texto do peso médio lê-se noutra ordem porque as etiquetas dos números ficaram alinhadas.
Referência: Diogo nesta thread: «assim esta vem melbor» cmsg_01K75FTs9bQnbhhH7qttpgraTi8pNMkxbb42HPorBHVSDZ e «faz updat» cmsg_01K75FTs9bQnbhhH7qttpgra1JbRoNSJpnxi8CKE6qCTCb
Novos: 0 · mudaram: 4 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PdfRecoveryTests.cs :: with.GenerateCompleteVisualEvidence`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosFolhaPorFerramentasTests.cs :: PegamentosFolhaPorFerramentasTests.ResumoPdfCompleto_FolhaDeFora_SemLigacao_ACardLigaAFolhaQueEntra`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosFolhaPorFerramentasTests.cs :: PegamentosFolhaPorFerramentasTests.ResumoPdf_TwoSheets_OneSectionPerSheet_TheAlertOfTheSecondIsNotHidden`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs :: PuncoesHandlerTests.Pdf_PosicoesDaLinha_EOResumoLigaOFicheiroDasPuncoes`

Selo depois: `dba4e2efbb45ed56`

## A-0248 · 2026-10-27 · ok-do-diogo

Porquê: Peso (DMO-513): o Controlo anterior vem escolhido com o Peso mais recente das anteriores (ou o criado para comparar) e a tabela aparece logo; antes (17/10) só depois de escolher.
Referência: Diogo nesta thread: «peso não assume o anterior» cmsg_01K75FTs9bQnbhhH7qttpgraUvPVzqVHduTo5zrh9CF9qr e «sim» cmsg_01K75FTs9bQnbhhH7qttpgra8wtfGJ17f4EnDCXtGohQRZ
Novos: 0 · mudaram: 4 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.UltimasProducoes.cs :: PesoComparacaoPageTests.Folha_AProducaoACompararTemAsUltimas5Anteriores_EAPedidaFica`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs :: PesoComparacaoPageTests.PesoAbertoPeloHistorico_ComparaNaFolhaESupoeCaloteSemGravar`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_TrocaParcial_ForaDasZonasNadaMuda_EOsFormulariosLevamAComparacao`

Selo depois: `f4fe603cf0e02cbf`

## A-0253 · 2026-10-27 · teste-novo

Porquê: DMO-519: testes novos do uso por produção (a leitura de hoje só conta para a última produção)
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ALeituraDeHoje_SoEOUsoDepoisDaUltimaProducao_NaoDasAnteriores`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoPorProducao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoPorProducao.cs :: DefinicoesPorModuloPageTests.ALeituraDeHoje_NaoEOUsoNoFimDasProducoesAnteriores`

Selo depois: `c9758d36d9d180df`

## A-0252 · 2026-10-27 · ok-do-diogo

Porquê: DMO-517: o link do mcaliper não funcionava porque, no Resumo, só quem tinha o Aprovar e o Criar o podia pôr (na Revisão). Quem tem o Controlo Criar passa a ver «Associar link do mcaliper» na linha CM/BQ/MF sem link e grava-o ali. Os dois testes deixam de exigir que o Resumo esconda o associar e passam a exigir o botão e a janela; com o link associado, o Resumo só o abre.
Referência: cartão do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraTxDSbtGzfCy3FcLtUkwGGf («Pôr o link no próprio Resumo»)
Novos: 0 · mudaram: 2 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs :: ControloCapabilitiesCutHPageTests.Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs :: ControloCapabilitiesCutHPageTests.McaliperLink_CreateAssociatesOnToolSheet_ApproveCopiesIt`

Selo depois: `b1362fb0a7f9a859`

## A-0251 · 2026-10-27 · ok-do-diogo

Porquê: DMO-521: a folha do Job On aberta no painel deslizante passa a um resumo para consulta rápida, com «Abrir folha» (Diogo: «vai servir apenas de consulta rápida e ter uma opção para abrir a janela»). O painel deixa de abrir a folha com a largura toda (largas só com /JobOn) e abre-a com a largura média (resumos); o teste da estrutura dos painéis passa a exigir estas linhas.
Referência: «sim» do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraW1FNJggGa8qKeQKRyhzxvC
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs :: ControloCapabilitiesCutHPageTests.Estrutura_JobOnNoPainel_TituloLarguraEFolha`

Selo depois: `1ecbaa53f6ccc384`

## A-0262 · 2026-10-28 · ok-do-diogo

Porquê: Diogo aprovou mudar estes testes: sem setas no Voltar e no Registo das punções, CM e lote num cartão no Peso, título «Produção» nas Boquilhas, «Sem registo» no Armazém (DMO-523)
Referência: thread Rever tudo antes da oficial, Diogo 28/10 09:38 «Mete sim no que está em stand by» e 09:39 «perfeito sim» (cmsg_01K75FTs9bQnbhhH7qttpgraGA5bipyUyVzm4eZtodh1on)
Novos: 0 · mudaram: 8 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsArmazemTests.cs :: JobOnToolFactsArmazemTests.OndeEstaOEstadoEOAviso`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Procurar_SoNoCartaoDoTipo_MostraOndeEstaEAbreAFerramenta`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.CanonicalTraceId_LoadsPersistedTrace`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.PersistedToolAndContextFacts_RenderFromServerData`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs :: BoquilhasRegistoPageTests.OpeningAMachine_DoesNotCreateTheTrace`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoPorCombinacaoPageTests.cs :: Producao.CriarDepoisDaTroca_AbreOPesoNovo_CopiaAProducaoAnterior_EOAnteriorFicaSemLeituraNova`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FaixaDaFicha.cs :: DefinicoesPorModuloPageTests.ORegistarDoArmazem_ComUmaFerramenta_TemOVoltar`
- mudou: `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs :: PuncoesPageTests.FolhaDasPuncoes_LigadaDoResumoEDoRegisto_SoComRegisto_EOPdfDoControloTrazODasPuncoes`

Selo depois: `d859e6b4b665092d`

## A-0268 · 2026-10-28 · ok-do-diogo

Porquê: Rótulo «Nome técnico (produção)» passa a «Nome técnico (SAP)», pedido do Diogo (DMO-523)
Referência: Diogo 28/10 10:41 «removee producao nome tecnico meter SAP» (cmsg_01K75FTs9bQnbhhH7qttpgraL8tPtNgyoZrN2cZWqfG68d)
Novos: 0 · mudaram: 2 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Ficha_Editar_LinhasCompativeis_InformacaoOpcionalComFabricaDeOrigem`

Selo depois: `44e498ae87db9050`

## A-0269 · 2026-10-28 · teste-novo

Porquê: DMO-536: referência sem Job On abre o Job On novo na linha de onde se veio
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReferenciaNova.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReferenciaNova.cs :: JobOnPageTests.Folha_ReferenciaSemJobOn_AbreOJobOnNovoNaLinhaDeOndeVeio`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReferenciaNova.cs :: JobOnPageTests.Folha_ReferenciaSemJobOn_QuemSoVe_FicaComOAviso`

Selo depois: `f36070a927fa88b7`

## A-0291 · 2026-10-28 · ok-do-diogo

Porquê: DMO-556: a lista «Produção» do Planeamento vem já com a última produção escolhida
Referência: Diogo 28/10 13:18 «ok» na thread Job On (cmsg_01K75FTs9bQnbhhH7qttpgra3FCNTj9umBvP7F7hPToAYt)
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: JobOnPageTests.Procura_EmDoisCampos_AReferenciaEncheODropdownDasProducoes`

Selo depois: `46c4e9cd976b4215`

## A-0292 · 2026-10-28 · ok-do-diogo

Porquê: Folha do Job On (Diogo, 28/10/2026): tolerância com um só sinal; folgas com um número por espaço (MF folga · tol. · flecha, CM só folga · tol.) e o Editar recusa dois números numa folga; a BQ sem «Antes nesta produção» (o teste dos lotes usados passa a exigir que não aparece); posição e estado do Armazém a negrito, a posição abre o Armazém (o teste do Armazém no cartão passa a exigir o negrito). Pedidos do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraXsQAqwXMvdwzxEBtZXVi8x, cmsg_01K75FTs9bQnbhhH7qttpgraKrQi7JHehQiEg7H86U68T1, cmsg_01K75FTs9bQnbhhH7qttpgraVoYdoBPQWZ26Vyr5f5nUSn, cmsg_01K75FTs9bQnbhhH7qttpgraVgPCefiS4NQsiePhNhB2HE
Referência: DMO-557
Novos: 7 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolgasRecusaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolgasRecusaTests.cs :: JobOnFolgasRecusaTests.DoisNumerosNumaFolga_ERecusada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolgasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolgasTests.cs :: JobOnFolgasTests.UmNumeroPorEspaco`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolgasDimensoes.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolgasDimensoes.cs :: JobOnPageTests.Editar_DoisNumerosNumaFolga_ERecusado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolgasDimensoes.cs :: JobOnPageTests.Folha_ToleranciaComUmSoSinal_EFolgasUmNumeroPorEspaco`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_OndeEstaNoArmazem_EAvisoSemRegisto`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LotesUsados.cs :: JobOnPageTests.Folha_DepoisDeAlterarOLoteDaBq_MostraOAtualEOsDeAntes`

Selo depois: `9639e32da4f53b65`

## A-0293 · 2026-10-28 · teste-novo

Porquê: Calibres da folha do Job On: o «mm» vai para o título e sai do valor; «56.40 X 58.60» em três linhas (Diogo, 28/10/2026, cmsg_01K75FTs9bQnbhhH7qttpgraY5DcPyZsqrbPRVhQf3PV4y e cmsg_01K75FTs9bQnbhhH7qttpgraYLxGTcbw5qgNTTgafoptKT)
Referência: DMO-560
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CalMm.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CalMm.cs :: JobOnPageTests.Folha_CalibresComMm_OMmVaiParaOTitulo`

Selo depois: `3c8bedd55fb52a4f`

## A-0294 · 2026-10-28 · ok-do-diogo

Porquê: Folha do Job On (Diogo, 28/10/2026): um clique na CM, MF ou BQ abre a ficha (cmsg_01K75FTs9bQnbhhH7qttpgra8GmmFF8a9FLWUhv4j7vS7F; o teste «bloqueada: um clique não faz nada» passa a «um clique leva à ficha»); a linha do Armazém com o estado primeiro e só a posição, «Reparado 4094» (cmsg_01K75FTs9bQnbhhH7qttpgra5n2GjpqGpVYF5m8QHge882, cmsg_01K75FTs9bQnbhhH7qttpgraLZ41oDX4qovbS7Gq9ksQWZ)
Referência: DMO-562
Novos: 1 · mudaram: 1 · saíram: 1

- novo: `tests/ShellProof/jobon-folha.test.cjs :: Bloqueada: um clique leva à ficha`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Ficha_FerramentasUsadas_OndeEstaNoArmazem_EAvisoSemRegisto`
- saiu: `tests/ShellProof/jobon-folha.test.cjs :: Bloqueada: um clique não faz nada; o duplo clique leva à ficha`

Selo depois: `0f714b35b5fd7df3`

## A-0295 · 2026-10-28 · ok-do-diogo

Porquê: Diogo 28/10 14:54-14:55 (cmsg_01K75FTs9bQnbhhH7qttpgraN8reRcbxZwyA2NZDx8YcZG, cmsg_01K75FTs9bQnbhhH7qttpgraX1A34EDwGZauiatJpzMRCM): «recomer corredor de nao sei das quantas … deixar Tolerancia 0.20» / «+/- 0.20 mas tirar os comentarios todo». Os testes pedem «Tolerância» e «± 0.20» em vez de «nominal ± 0.20 mm» e dos textos «congelada…».
Referência: DMO-563
Novos: 0 · mudaram: 4 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Folhas.cs :: PegamentosPageTests.CmChanged_PageIsReadyForANewSheet_TheFirstReadingStartsIt_AndThePageSaysSo`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Folhas.cs :: PegamentosPageTests.SingleSheet_PageLooksAsBefore_NoSheetsCard`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.ToleranciaDasDefinicoes.cs :: PegamentosPageTests.FolhaSemMedicoes_MostraAToleranciaDasDefinicoes_EAPrimeiraMedicaoCongelaA`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.GetFolha_ControloId_ShowsInheritedContext_GargaloMissing_MfAlwaysInherited`

Selo depois: `931a9ce77f2c77bd`

## A-0296 · 2026-10-28 · teste-novo

Porquê: DMO-535: a Gota do Job On vem do nome técnico da CM (senão do MF): 55 = 2, 44/4T/50/5T = 3, 95 = 4 (Diogo, 28/10/2026: «ver se o jobon já tem em consideração nome técnico para mudar gota e meter número correto»). Testes novos do parser e da folha.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraKF2m3xMYuf6tyaurxyUd5F
Novos: 6 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserGotasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserGotasTests.cs :: TechnicalNameParserGotasTests.Molde_DizAsGotasPelaDimensao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserGotasTests.cs :: TechnicalNameParserGotasTests.SemNomeAcessorioOuOutraDimensao_NaoDiz`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.GotaNomeTecnico.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.GotaNomeTecnico.cs :: JobOnPageTests.Folha_GotaVemDoNomeTecnicoDaCm_PorCimaDaEscrita`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.GotaNomeTecnico.cs :: JobOnPageTests.Folha_SemNomeNaCm_GotaDoMf_ESemNenhum_AEscrita`

Selo depois: `3f4d1d2b421330dd`

## A-0255 · 2026-10-28 · ok-do-diogo

Porquê: DMO-525: a cadência do SAP passa a média e moda por produção, como o peso. O teste da cadência no Job On passa a exigir o nome «Cadência usada», a moda com a média no título e, sem SAP dessa produção, a da produção anterior marcada «ant.»; sem nenhuma, «—» como antes. Testes novos: média e moda, a produção anterior, a cadência de cada produção no Histórico de produções e o «≈» do % uso no Histórico.
Referência: «sim» do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraDtxDo1jmQDo6x9jiTc2Ubd (cartão cmsg_01K75FTs9bQnbhhH7qttpgraKVHfGf2ThnhN8rTRanYkpz)
Novos: 8 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaPorProducaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaPorProducaoTests.cs :: FabricoSapCadenciaPorProducaoTests.AMediaPesaOsDias_EAModaEADasOrdensComMaisDias`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaPorProducaoTests.cs :: FabricoSapCadenciaPorProducaoTests.CadaProducaoDaFerramenta_TemASuaCadencia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaPorProducaoTests.cs :: FabricoSapCadenciaPorProducaoTests.OJobOnSemCadencia_FicaComADaProducaoAnteriorDaMesmaReferencia`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.CadenciaPorProducao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.CadenciaPorProducao.cs :: DefinicoesPorModuloPageTests.OHistorico_MostraACadenciaDeCadaProducao_EAJanelaComMediaModaEDelta`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoUsoEstimado.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoUsoEstimado.cs :: DefinicoesPorModuloPageTests.OHistorico_SemLeiturasAVolta_EstimaOUsoPeloProduzido`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CadenciaSap.cs :: JobOnPageTests.Folha_MostraACadenciaDoSap_DaOrdemComMaisDias`

Selo depois: `f0420e29cb4dc503`

## A-0258 · 2026-10-28 · teste-novo

Porquê: O Novo Peso mostra a temperatura escrita e a densidade da água dela.
Referência: DMO-528
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoDensidadeAgua.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoDensidadeAgua.cs :: ControloCapabilitiesCutHPageTests.NovoPeso_MostraATemperaturaEADensidadeDaAgua`

Selo depois: `e3c2fdbe5805a823`

## A-0300 · 2026-10-28 · teste-novo

Porquê: O Peso lê o Diâmetro e a Calote do Tampão da folha do Job On quando o Job On não os tem gravados (Job On importado).
Referência: DMO-564
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_TampaoSoNaFolhaDoJobOn_LeODaFolha`

Selo depois: `10c7afebdc0b9b62`

## A-0301 · 2026-10-28 · teste-novo

Porquê: O «Criar» do Controlo anterior tem as duas opções: sem Job On ou num Job On anterior da referência.
Referência: DMO-565
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.AssociarJobOn.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.AssociarJobOn.cs :: PesoFolhaProducaoAnteriorPageTests.CriarControloAnterior_SemJobOnOuNumJobOnAnterior`

Selo depois: `0699590f0e13f023`

## A-0266 · 2026-10-28 · ok-do-diogo

Porquê: Produção anterior passa a ser uma tabela (títulos numa linha, valores por baixo) com a Média e a Moda (com «X de Y dias»), cada uma com a sua diferença, e deixa de ter o peso escrito à mão; Resumo e PDF usam as duas. Os testes antigos deixam de procurar a caixa «Peso usado» e o campo anterior_peso, e o resumo SAP passa a ter os dias da moda.
Referência: DMO-534; ok do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraVoNvoQBUPf4cwx9jYiHvPJ, cmsg_01K75FTs9bQnbhhH7qttpgraTaRYMDqbdFc2okYBHmExZW e cmsg_01K75FTs9bQnbhhH7qttpgraPN7CxcF7Rzwfmmd7mHxLcm
Novos: 1 · mudaram: 10 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.Resumo_ProducaoAnteriorComAMediaEAModa`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.OPesoDoSap_EAMediaPelosDiasEAModaOPesoComMaisDias`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.OPesoDosJobOns_VemDasOrdensDaMesmaReferenciaMaquinaEDatas`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.OsDiasDeCadaOrdem_SaoOsQueFicamDentroDoJobOn_EAModaEmpatadaEAMaisRecente`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_TrocaParcial_ForaDasZonasNadaMuda_EOsFormulariosLevamAComparacao`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_AsUltimas5_EAGuardadaForaDelasFicaEscolhida`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PesoIlegivel_RecusaComEcho_ENaoGuarda`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PropoeAMaisRecente_GuardaOPesoDoSap_EMostraODelta`

Selo depois: `d5028b5b0339c195`

## A-0254 · 2026-10-28 · ok-do-diogo

Porquê: DMO-524: o Diogo pediu um campo para colar o link do mcaliper e «Medições» que abre o mcaliper em vez do link/copiar. Os três testes deixam de exigir a janela «Associar link do mcaliper», o «Link do mcaliper associado» e o «Copiar» (data-dmo-copiar) e passam a exigir o campo com o link e o «Medições» com o endereço; o teste do link ganha a gravação JSON do campo (recusa e gravação).
Referência: mensagem do Diogo cmsg_01K75FTs9bQnbhhH7qttpgraGpkfZmVrGptNbCoC9pNFFB («basta meter um campo para colar o link e no controlo.ver em vez d ver um link aparece Mediçoe abre o mcaliper»)
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs :: ControloCapabilitiesCutHPageTests.Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs :: ControloCapabilitiesCutHPageTests.ResumoDoControlo_CartoesDasAreas_GravamEVoltamAoResumo`
- mudou: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs :: ControloCapabilitiesCutHPageTests.McaliperLink_CreateAssociatesOnToolSheet_ApproveCopiesIt`

Selo depois: `7f41a6c1558f560b`

## A-0305 · 2026-10-28 · contagem-lista

Porquê: «Voltar» passo a passo pelo caminho feito no separador (DMO-569): ficheiro novo voltar-caminho.test.cjs e a sua entrada na lista do test:js
Referência: DMO-569
Novos: 6 · mudaram: 1 · saíram: 0

- novo: `tests/ShellProof/voltar-caminho.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/voltar-caminho.test.cjs :: a mesma página com outros filtros ou depois de guardar não é um passo`
- novo: `tests/ShellProof/voltar-caminho.test.cjs :: a primeira página do separador usa a página anterior do site; o login começa de novo`
- novo: `tests/ShellProof/voltar-caminho.test.cjs :: as folhas do controlo também contam uma a uma`
- novo: `tests/ShellProof/voltar-caminho.test.cjs :: as páginas abertas no painel deslizante não entram no caminho`
- novo: `tests/ShellProof/voltar-caminho.test.cjs :: três páginas seguidas: o Voltar recua uma de cada vez até à primeira`
- mudou: `tests/ShellProof/package.json`

Selo depois: `43d26cce1f4efcab`

## A-0306 · 2026-10-28 · ok-do-diogo

Porquê: Lote 0.3.0-beta.227: #264 PDF do Peso sem duplicados (DMO-526, antes A-0256: o peso usado da produção anterior já está no cartão, sai a repetição no rodapé); #267 AN nas Boquilhas e DMO-572/573/574 (antes A-0263, contagem-lista); #273 triângulo no Editar e reparador na linha do Armazém (DMO-559, DMO-570, testes novos); #270 relatório de frontend e largura dos campos (DMO-537, testes JS novos).
Referência: #264: Diogo 28/10 cmsg_01K75FTs9bQnbhhH7qttpgraKQa9WN4maaHqZRbPbFJPru; #267, #270 e #273: ver os PR
Novos: 25 · mudaram: 5 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementAnTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementAnTests.cs :: SemDefinicoes.Corrigir_SoAn_AceitaZeroBq_SemAnRecusa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementAnTests.cs :: SemDefinicoes.Corrigir_SoMudaOsAnQuandoOsTraz`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementAnTests.cs :: SemDefinicoes.Registar_AnNegativo_RecusaENadaGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementAnTests.cs :: SemDefinicoes.Registar_ComAn_GravaOsAn_SemAnOuZeroFicaSemAn`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoPorBqTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoPorBqTests.cs :: NoSettings.BqQueSaiuNumJobOn_EVoltaNoutro_NaoFicaComSaldo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoPorBqTests.cs :: NoSettings.EntraMaisDoQueSaiu_EmTodasAsProducoes_FicaComSaldo`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs :: BoquilhasRegistoPageTests.An_ADiscrepanciaTambemContaParaOsAn`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs :: BoquilhasRegistoPageTests.An_IlegivelOuNegativo_NaoGravaNada_EOFormularioVoltaComOQueSeEscreveu`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs :: BoquilhasRegistoPageTests.An_NaFichaDaBq_GravaEMostraBqEAn`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs :: BoquilhasRegistoPageTests.An_NoRegisto_GravaComOMovimento_EContaOsAnFora`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs :: BoquilhasRegistoPageTests.An_NosMovimentos_OFiltroPecaAn_MostraSoOsMovimentosComAn`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs :: BoquilhasRegistoPageTests.An_RegistarSemJobOn_APecaVemEmBq_ComAnGravaSoOsAn`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_ALinhaPropostaEAUltimaOndeTeveMovimentos_SeForDaFicha`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_ALinhaSoTemAsDaFichaDaBq_EVemJaEscolhida`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_NumJobOnAnterior_FicaNoRegistoDesseJobOn_EODaEntradaDizQuemRegistou`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReparadorNaLinha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReparadorNaLinha.cs :: JobOnPageTests.Folha_ReparadorDepoisDoEstadoEDaPosicao_SemLinhaAParte`
- novo: `tests/ShellProof/dmo-suggest-sair.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/dmo-suggest-sair.test.cjs :: sair da página cancela o pedido em voo`
- novo: `tests/ShellProof/dmo-suggest-sair.test.cjs :: trocar a zona do campo 25 vezes deixa um só «pagehide» e sair da página ainda cancela o pedido`
- novo: `tests/ShellProof/painel-apoio-alteracoes.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/painel-apoio-alteracoes.test.cjs :: Painel de apoio: um <dmo-painel> com alterações por guardar fica com o aviso e o apoio não abre por cima`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.cs :: PesoPdfGeneratorTests.ProducaoAnterior_DizProducao_ENaoSAP`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasRegistoConsultasTests.cs :: BoquilhasRegistoConsultasTests.Registo_GetFazPoucasConsultas`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico`
- mudou: `tests/ShellProof/package.json`

Selo depois: `6c1ef279e6a499af`

## A-0313 · 2026-10-28 · ok-do-diogo

Porquê: Lote 0.3.0-beta.228: #266 filtros Estado/Localização/Fábrica e Fábricas da BA (DMO-529/530, antes A-0297..A-0299); #268 relatórios do dmo-Improvements (DMO-533/546/547, antes A-0283, contagem-lista); #273 consulta da reparação arrumada (DMO-577, teste novo); #276 regras dos agentes.
Referência: ver os PR #266, #268, #273 e #276
Novos: 41 · mudaram: 11 · saíram: 2

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/ReporAcessoPrimeiroAAppTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/ReporAcessoPrimeiroAAppTests.cs :: ReporAcessoPrimeiroAAppTests.AGravacaoNaAppFalha_APalavraPasseNaoMudaNoProvider`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/ReporAcessoPrimeiroAAppTests.cs :: ReporAcessoPrimeiroAAppTests.OProviderNaoResponde_ASessaoAntigaJaCaiuEAMudancaEObrigatoria`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/ReporAcessoPrimeiroAAppTests.cs :: ReporAcessoPrimeiroAAppTests.OProviderRecusa_OUtilizadorFicaComoEstava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/ReporAcessoPrimeiroAAppTests.cs :: ReporAcessoPrimeiroAAppTests.ReporComSucesso_FechaAsSessoesEDevolveOPin`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/FabricasBaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/FabricasBaTests.cs :: FabricasBaTests.Comeca_vazia_e_acrescenta_com_sigla_de_duas_letras_unica`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/FabricasBaTests.cs :: FabricasBaTests.Desativa_sem_apagar_e_MG_e_a_proposta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/FabricasBaTests.cs :: FabricasBaTests.Emprestimo_filtra_pela_fabrica_da_ultima_saida`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararLimiteTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararLimiteTests.cs :: PesoCompararLimiteTests.OPesoDeOndeSeParte_ForaDos500MaisRecentes_FicaDoLadoAtual`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararLimiteTests.cs :: PesoCompararLimiteTests.Os500MaisRecentesNoutraMaquina_ACmDestaMaquinaContinuaACompararSe`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCorrigidoDepoisDaAprovacaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCorrigidoDepoisDaAprovacaoTests.cs :: PesoCorrigidoDepoisDaAprovacaoTests.ApagarAUnicaLeituraDepoisDaAprovacao_FicaComAData`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCorrigidoDepoisDaAprovacaoTests.cs :: PesoCorrigidoDepoisDaAprovacaoTests.AsCorrecoesDeOutroControlo_NaoContam`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCorrigidoDepoisDaAprovacaoTests.cs :: PesoCorrigidoDepoisDaAprovacaoTests.SemCorrecoes_AUltimaCorrecaoFicaVazia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCorrigidoDepoisDaAprovacaoTests.cs :: PesoCorrigidoDepoisDaAprovacaoTests.UmaCorrecaoAntesDeAprovar_NaoContaDepoisDaAprovacao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCorrigidoDepoisDaAprovacaoTests.cs :: PesoCorrigidoDepoisDaAprovacaoTests.UmaLeituraCorrigidaDepoisDaAprovacao_MantemAAprovacao_EFicaComAData`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.TrincoDoPeso.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.TrincoDoPeso.cs :: PostgresMigrationIntegrationTests.LeituraDoPeso_EsperaPeloTrincoDoCorrigirEApagar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Planeamento/SumarioLimiteTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Planeamento/SumarioLimiteTests.cs :: SumarioLimiteTests.PoucasLinhas_UmaLeituraComoAntes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Planeamento/SumarioLimiteTests.cs :: SumarioLimiteTests.QuinhentasProducoesNoDia1_NaoEscondemADoDia24`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateEstadoLocalTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateEstadoLocalTests.cs :: ToolCandidateEstadoLocalTests.Estado_e_o_ultimo_registado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateEstadoLocalTests.cs :: ToolCandidateEstadoLocalTests.Fabrica_de_origem_sem_maiusculas_e_antes_da_paginacao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateEstadoLocalTests.cs :: ToolCandidateEstadoLocalTests.Localizacao_armazem_primeiro_e_sem_movimentos_e_fabrico`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.HistoricoSemFerramenta_VaiParaOPesquisar`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs :: ReparacaoInternaConsultaPaginaTests.OResumoPorPessoa_Repartido_ONomeFiltraPorEla`
- novo: `tests/ShellProof/excel-bq-escritas.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/excel-bq-escritas.test.cjs :: S03: sem registo novo durante a escrita, escreve uma só vez`
- novo: `tests/ShellProof/excel-bq-escritas.test.cjs :: S03: um registo durante uma escrita lenta volta a escrever e o Excel fica com ele`
- novo: `tests/ShellProof/excel-bq-escritas.test.cjs :: S04: dois separadores escrevem um de cada vez, e o Excel fica com o mais novo`
- novo: `tests/ShellProof/excel-bq-escritas.test.cjs :: sem Web Locks no browser, um separador escreve como antes`
- novo: `tests/ShellProof/jobon-rascunho-envio.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/jobon-rascunho-envio.test.cjs :: S01: na mesma versão o rascunho volta como antes`
- novo: `tests/ShellProof/jobon-rascunho-envio.test.cjs :: S01: o rascunho da versão 3 não substitui sozinho a versão 4; «Usar o rascunho» traz-o`
- novo: `tests/ShellProof/jobon-rascunho-envio.test.cjs :: S01: «Descartar rascunho» numa versão nova apaga-o`
- novo: `tests/ShellProof/jobon-rascunho-envio.test.cjs :: S02: a folha gravada apaga a cópia do envio, e o rascunho não volta`
- novo: `tests/ShellProof/jobon-rascunho-envio.test.cjs :: S02: um envio sem resposta (rede) e depois outra página não perde o que se escreveu`
- novo: `tests/ShellProof/jobon-rascunho-envio.test.cjs :: S02: voltar atrás à mesma folha depois de um envio falhado traz o que se escreveu`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs :: ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs :: PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.OsCodigosAPedirAoSap_SaoOsPedidosNaFicha`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.BarraDasFerramentas_PesquisarERegistarReparacao_SemOsNomesAntigos`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: FerramentasPagesTests.GetIndex_ShowsHumanFactsList_ToolIdOnlyInUrl_NeverAField`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs :: ForbiddenMachineListQuery.GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered`
- mudou: `tests/ShellProof/package.json`
- mudou: `tests/visual/paginacao_parcial_browser.py`
- saiu: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.HistoricoDasFerramentas_AbreVazio_EProcuraPelaReferencia`
- saiu: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.HistoricoDasFerramentas_AcimaDoLimite_MostraOAviso`

Selo depois: `fe7959d2a3a9a1df`

## A-0303 · 2026-10-28 · ok-do-diogo

Porquê: Lote 0.3.0-beta.229: #274 o SAP dentro da app (DMO-567, antes A-0303, com o #271: histórico com artigo e referências DMO-545 e «Pedir todas» DMO-566) e o aviso do nome técnico (DMO-575, antes A-0311). Asserções do cartão e da ajuda do SAP atualizadas para o botão novo «Atualizar do SAP».
Referência: ver os PR #271 e #274
Novos: 15 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.TodosOsCodigos_SaoOsDeTodasAsFerramentasComNomeTecnico`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserAvisoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserAvisoTests.cs :: TechnicalNameParserAvisoTests.UmNomeForaDaRegra_Avisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserAvisoTests.cs :: TechnicalNameParserAvisoTests.UmNomeQueBate_NaoTemAviso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserAvisoTests.cs :: TechnicalNameParserAvisoTests.UmaBqComONomeDeUmMolde_Avisa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserAvisoTests.cs :: TechnicalNameParserAvisoTests.UmaCmComONomeDeUmMf_Avisa`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_MudaOSap_EOsScriptsDaPastaLevamAsDefinicoes`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoComArtigo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoComArtigo.cs :: DefinicoesPorModuloPageTests.OHistoricoDeProducoes_MostraOArtigoDeCadaProducao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.AtualizarPorReferencia_DaOsCodigosDessaReferenciaEDoTipo`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.PedirTodas_DaOsCodigosDeTodasAsFerramentasComNomeTecnico`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoPeloArmazem.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoPeloArmazem.cs :: DefinicoesPorModuloPageTests.AJanelaDaUtilizacao_MostraOsJobOnDaLinhaEnquantoAFerramentaLaEsteve`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoReferencia.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoReferencia.cs :: DefinicoesPorModuloPageTests.AJanelaDaUtilizacao_MostraAReferenciaDeCadaProducao`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AsPastas_SaoGerais_SoAsAcoesFicamParaTodos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_OCartaoDoSapTemApagarLidos_EAAjudaExplica`

Selo depois: `a55afb73749c0cc3`

## A-0290 · 2026-10-28 · ok-do-diogo

Porquê: A localização passa de «A reparar · X» para «Reparação · X» e mostra a fábrica no empréstimo; sem registo no Armazém, a ficha e o Armazém dizem «Sem registo» em vez de «Fabrico» (DMO-555, #279): as verificações antigas esperam o texto novo.
Referência: Diogo 28/10 12:27 «perfeito, Sim» (cmsg_01K75FTs9bQnbhhH7qttpgra3qMJmaLMKN6qABzkRXLprv)
Novos: 2 · mudaram: 8 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Localizacao_EmprestadaDizAFabricaEscolhidaNaSaida`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.SemRegistoNoArmazem_AFichaDizSemRegisto_ENaoFabrico`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Procurar_DizOndeEstaCadaFerramenta`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.CadaFerramenta_OndeEstaEOQueFalta`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.ForaDoArmazemEDoFabrico_NaoDisponivel`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsArmazemTests.cs :: JobOnToolFactsArmazemTests.OndeEstaOEstadoEOAviso`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.SaidaEEntrada_AtualizamALista_EALinhaQueEntrouMostraDiaPosicaoENome`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Saida_ODestinoEscolheOsCamposEDepoisAEntradaPropoe`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.AFicha_MostraALocalizacaoEOsMovimentos_SoAQuemTemOArmazem`
- mudou: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.ArmazemEntrada_DeFerramentaNaLista_SugereReparado_AvisaAoMudar_EALinhaFicaCinza`

Selo depois: `a913e7297f49e5fe`

## A-0314 · 2026-10-28 · ok-do-diogo

Porquê: A janela Utilização conta também as saídas do Armazém (DMO-571, #271): mais uma leitura por ferramenta, o teste das consultas passa de n*4 para n*5; os resultados continuam iguais.
Referência: Diogo 28/10 «Sim, muda o teste» (cmsg_01K75FTs9bQnbhhH7qttpgraWAyxNzW7qWufbX2ZnbyHFg)
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoricoLote.cs :: PostgresMigrationIntegrationTests.HistoricoFerramentas_ProducoesEmLote_IguaisAUmaAUma_ComConsultasFixas`

Selo depois: `c2acdce52a1c5ddb`

## A-0312 · 2026-10-28 · ok-do-diogo

Porquê: O Histórico em lote também lê os movimentos do Armazém, como a ficha (DMO-571, #274 d742aa9): mais uma consulta fixa (6→7, 12→13), além da 5.ª por ferramenta; os resultados continuam iguais.
Referência: Diogo 28/10 «Sim, muda o teste» (cmsg_01K75FTs9bQnbhhH7qttpgraWAyxNzW7qWufbX2ZnbyHFg)
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoricoLote.cs :: PostgresMigrationIntegrationTests.HistoricoFerramentas_ProducoesEmLote_IguaisAUmaAUma_ComConsultasFixas`

Selo depois: `65a93dc61611553b`

## A-0257 · 2026-10-28 · ok-do-diogo

Porquê: Pasta dos PDFs (DMO-527, #281): a subpasta da produção passa a «<produção> <referência>»; os testes da pasta pedem o formato novo.
Referência: Diogo 28/10: cmsg_01K75FTs9bQnbhhH7qttpgraMP6JULLoLAQaT7A1nRgtsh e formato «202601 1075C142» cmsg_01K75FTs9bQnbhhH7qttpgraHgZ7AWc5LyZZPeje9cH7jP
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloDocumentPathsTests.cs :: ControloDocumentPathsTests.ProductionRelativeFolder_IsReferenceThenProduction_SafeForWindowsShares`
- mudou: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs :: PegamentosPageTests.PdfDownloads_UsePersistedModels_AllComponents_AndDoNotWriteDatabaseOrServerFiles`
- mudou: `tests/visual/pasta_pdf_browser.py`

Selo depois: `32dc4f2d6565160f`

## A-0315 · 2026-10-28 · ok-do-diogo

Porquê: Codificação por linha (DMO-578): a tabela das Definições das Ferramentas passa a codificação por linha (nomes técnicos editáveis, Qtd. CM/MF/BQ/AN, seletor B2 Dupla/Tripla, «Atualizar (N)»), a migração FerramentaB2Modo entra na lista e os testes novos garantem a folha da MG, o modo da B2, o criar/editar e o Job On. Procura da Reparação Programada (DMO-579): a Referência encontra pela referência toda e pelo nome técnico.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra9XbzqvdhTJocukrC3eQNhA, cmsg_01K75FTs9bQnbhhH7qttpgraCCgozAHgc15Raax5tq4XMg, cmsg_01K75FTs9bQnbhhH7qttpgraMyPMrUNJqfd5WoPEoSXomY, cmsg_01K75FTs9bQnbhhH7qttpgra8k8joixEkf2QtoBdbDNiqp, cmsg_01K75FTs9bQnbhhH7qttpgra9rGu8RYhnhF261Md4bRX3F
Novos: 12 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs :: ReparacaoProgramadaTests.Procurar_PelaReferenciaToda_OuPeloNomeTecnico_EncontraACmEOMf`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoFerramentasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoFerramentasTests.cs :: CodificacaoFerramentasTests.Atualizar_aplica_a_tabela_as_ferramentas_registadas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoFerramentasTests.cs :: CodificacaoFerramentasTests.Guardar_muda_os_nomes_e_as_quantidades_e_deixa_de_fora_a_ma`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoFerramentasTests.cs :: CodificacaoFerramentasTests.O_modo_da_B2_poe_as_quantidades_desse_modo_na_B2`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoFerramentasTests.cs :: CodificacaoFerramentasTests.Sem_nada_gravado_a_tabela_e_a_folha_da_MG`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoFerramentasTests.cs :: CodificacaoFerramentasTests.Sugerir_pela_linha_e_pelo_modo_da_B2`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoNasFerramentasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoNasFerramentasTests.cs :: CodificacaoNasFerramentasTests.Criar_poe_o_nome_tecnico_da_linha_e_o_modo_da_B2`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoNasFerramentasTests.cs :: CodificacaoNasFerramentasTests.Editar_muda_o_modo_da_B2`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/CodificacaoNasFerramentasTests.cs :: CodificacaoNasFerramentasTests.O_Job_On_da_B2_filtra_pelo_modo_escolhido`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_Codificacao_SeletorDaB2`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_TotalsGrid_SavesWithGuardar`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs :: DefinicoesPorModuloPageTests.E10_FerramentasDefinicoes_QuadroNumaZona_OAvisoVaiParaOCanto`

Selo depois: `cc3a3e5d878ee217`

## A-0317 · 2026-10-28 · ok-do-diogo

Porquê: Revisão dos testes (DMO-580, #285): saem os 22 testes do Armazém de serviços sem página (Mapa, Posições, Próximos, Entrada com par); o aviso «Sem registo» do Armazém passa a estar ligado por omissão (Diogo 28/10); os modelos de email do Peso deixam o «×» nas linhas (Apagar no canto, em último) e a Consulta da Reparação Interna deixa o link «Por associar» na linha (a linha abre a janela): nunca botões nas linhas.
Referência: Diogo 28/10 17:40 «sim» (cmsg_01K75FTs9bQnbhhH7qttpgraQPo2WgcywrK4oUrJtfhKkr), na thread Rever os testes; PR #285
Novos: 1 · mudaram: 2 · saíram: 27

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.PorOmissao_OAvisoDoArmazemMostraSe`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.SemBotoesNasLinhas.cs :: EnvioEmailPesoPageTests.Modelos_UmaLinhaEscolhe_EUmSoAbrirNoCanto`
- mudou: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs :: ReparacaoInternaOutraFerramentaPaginaTests.Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.ASituacao_MostraOParEOndeEsta`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.ComOPar_ACmEOMfEntramNaMesmaPosicao`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.OMesmoEnvioDuasVezes_GravaOParUmaVez`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.OParJaNoArmazem_EntraSoAFerramentaEAvisa`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.SemOPar_EntraSoAFerramenta`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs :: ArmazemEntradaComParTests.SemParQueExista_EntraSoAFerramenta`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.APosicao_CorredorEstanteEPrateleira`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OFiltro_ContaSempreOArmarioInteiro`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.OParDaMesmaReferenciaELote_NaoEMisturado`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.SemArmario_AbreOPrimeiro_ComAOcupacao`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.SemPosicoes_UmMapaVazio`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs :: ArmazemMapaTests.TodosOsArmarios_OuUmQueNaoExisteAbreOPrimeiro`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_ForaDaRegra_NaoGrava`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_OMesmoCodigoComEspacos_JaExiste`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Criar_QuatroAlgarismos_Grava`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Listar_MaisDoQueUmaPagina_Pagina`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.Listar_PeloInicioDoCodigoEPeloEstado_PelaOrdemDoCodigo`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs :: ArmazemPosicoesTests.MudarAtiva_ArquivaEReativa_SemApagar`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.AsProducoesDoPeriodo_PelaDataDeInicio`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.CadaFerramenta_OndeEstaEOQueFalta`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.ForaDoArmazemEDoFabrico_NaoDisponivel`
- saiu: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs :: ArmazemProximosTests.SemProducoes_ListaVazia`
- saiu: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.PorOmissao_AAplicacaoNaoOLigaNasDefinicoes`

Selo depois: `5c318483048cbfe4`

## A-0319 · 2026-10-28 · teste-novo

Porquê: O script do SAP envia os ficheiros direto para a app com a chave do Admin › SAP; sem a chave recusa (DMO-582).
Referência: PR #290
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.OScriptDoSap_EnviaDireto_ComAChave_ESemElaNao`

Selo depois: `be0b65595a608b7e`

## A-0322 · 2026-10-28 · contagem-lista

Porquê: Tabela dos códigos de procura no SAP para CM, MF e BQ por Linha e «Código SAP» por ferramenta (DMO-585/586). As listas ganham a migração SapCodigoDaFerramenta, os endpoints /sap/codigos e /sap/enviar (com chave, DMO-582) e a coluna «search» lida do CSV; o que cada teste verifica fica igual.
Referência: PR #290
Novos: 9 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.ACodigo_PorOmissao_EONomeTecnicoComOLote`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.ARegraDaLinha_GanhaAGeral_EOCodigoAMaoGanhaATudo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.AsTresGerais_EstaoSempre_EAsGravadasLeemSeDeVolta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.OCsvDoScript_LigaPeloCodigoProcurado_QuandoONomeTecnicoNaoBate`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.OsCodigosPedidosAoSap_SeguemATabelaEOCodigoSap_DeTodosOsTipos`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.ATabelaDosCodigosDeProcura_GravaNoAdminEMudaOsCodigosPedidos`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.OCodigoSapDaFicha_GanhaATabela_EVazioVoltaAEla`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OCsvDoScript_LeOPercDasLinhasEncontradasEDeixaAsOutras`
- mudou: `tests/DMO.Alpha.Web.Tests/PaginasSemSessaoTests.cs :: PaginasSemSessaoTests.SemSessao_SoAbremAsPaginasPublicas`

Selo depois: `22d7c7aa100fe569`

## A-0323 · 2026-10-28 · teste-novo

Porquê: A % do SAP com «%» ou espaços inquebráveis lê-se; o CSV do script traz o consumo e a vida prevista (revisão do script pedida pelo Diogo).
Referência: PR #290
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OCsvDoScript_TrazOConsumoEAVidaPrevista`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: FerramentasUsoSapTests.OsNumerosComPercentagemOuEspacosEstranhos_LeemSe`

Selo depois: `b53737140df2c16e`

## A-0318 · 2026-10-29 · contagem-lista

Porquê: C02 da auditoria do Codex de 28/10/2026 (DMO-581, #286): teste de browser novo tests/visual/cabecalho_nome_comprido_browser.py, que garante que com um nome e um título compridos a conta fica na fila dos módulos e a folha do Job On a 1290×646 não desliza; no ci-local.sh só se acrescenta o passo que o corre.
Referência: PR #286
Novos: 1 · mudaram: 1 · saíram: 0

- novo: `tests/visual/cabecalho_nome_comprido_browser.py`
- mudou: `scripts/ci/ci-local.sh`

Selo depois: `0e6f198e89a387c4`

## A-0325 · 2026-10-29 · ok-do-diogo

Porquê: A localização no cartão do Job On passa a negrito também quando não é uma posição («Sem registo»), como o Diogo pediu; o teste do reparador na linha passa a esperar <b>Sem registo</b>. Testes novos: localização abre a Entrada/Saída do Armazém; diâmetros só com um número (DMO-587, #288).
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraNidJsALf8gXwDKhAgYzVEb
Novos: 4 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnDiametrosRecusaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnDiametrosRecusaTests.cs :: JobOnDiametrosRecusaTests.DoisNumerosNumDiametro_ERecusado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LocalizacaoArmazem.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LocalizacaoArmazem.cs :: JobOnPageTests.Folha_LocalizacaoANegrito_AbreAEntradaSaidaDoArmazem`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReparadorNaLinha.cs :: JobOnPageTests.Folha_ReparadorDepoisDoEstadoEDaPosicao_SemLinhaAParte`

Selo depois: `8f89f97f666856db`

## A-0326 · 2026-10-29 · contagem-lista

Porquê: Testes novos da auditoria de cobertura (DMO-588, #291): 173 testes para 75 correções DMO-380…577 que estavam sem teste ou de raspão (C#, JavaScript e browser). Listas acrescentadas: 9 ficheiros no test:js (inclui painel-propria-pagina.test.cjs, que nunca corria) e 4 passos de browser cobertura_*_browser.py no ci-local.sh. Os pendentes_*_browser.py ficam fora do CI. Nenhum teste existente mudou.
Referência: Diogo 28/10: cmsg_01K75FTs9bQnbhhH7qttpgra7cwoVACUyomrv9aShsVyV6, cmsg_01K75FTs9bQnbhhH7qttpgraX7KNnAiKEpCXv78YGZccVx
Novos: 203 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoNoHistoricoCoberturaA2Tests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoNoHistoricoCoberturaA2Tests.cs :: NoSettings.Historico_BqQueSaiuNumJobOnEVoltaNoutro_NaoMostraSaldo_MesmoComUmSaldoAntigoGravado`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoNoHistoricoCoberturaA2Tests.cs :: NoSettings.Historico_EntraMaisDoQueSaiu_MostraOSaldoNaEntrada_ENoPdf`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.CoberturaBPdf.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.CoberturaBPdf.cs :: PesoPdfGeneratorCoberturaBTests.PdfDoPeso_NaoRepeteRotulosNemExplicacoesRetiradas`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.CoberturaBPdf.cs :: PesoPdfGeneratorCoberturaBTests.ProducaoAnterior_ComMediaEModa_OPdfDizAsDuasEOsDiasDaModa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.CoberturaBPdf.cs :: PesoPdfGeneratorCoberturaBTests.ProducaoAnterior_SemMediaNemModa_UsaOPesoGuardado_ESemDizerMedia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesPosicoesPdfCoberturaB.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesPosicoesPdfCoberturaB.cs :: PuncoesPosicoesPdfCoberturaBTests.PdfDasPuncoes_AsLinhasSaoAsGotasDaLinha_ETresGotasDeixamMaisNaReserva`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesPosicoesPdfCoberturaB.cs :: PuncoesPosicoesPdfCoberturaBTests.PdfDasPuncoes_SaiNumaSoFolhaA4_ComAReservaCheiaOuNao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CoberturaA2TrincoCorrigir.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CoberturaA2TrincoCorrigir.cs :: PostgresMigrationIntegrationTests.ApagarUmaLeituraDoPeso_EsperaPelaLeituraNovaEmCurso_ENaoDeixaBuracoNasPosicoes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CoberturaA2TrincoCorrigir.cs :: PostgresMigrationIntegrationTests.CorrigirUmaLeituraDoPeso_EsperaPelaLeituraNovaEmCurso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CoberturaBTrincoDoPeso.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CoberturaBTrincoDoPeso.cs :: PostgresMigrationIntegrationTests.CorrigirEApagarUmaLeituraDoPeso_EsperamPeloTrincoDeQuemRegista`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ComOMesmoDiaDeInicio_FicaALancadaPorUltimo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.DuasExperienciasDeUmDiaNaMesmaLinha_FicaAUltimaLancada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.DuasProducoesQueCobremODiaNaMesmaLinha_FicaAQueComecouPorUltimo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.EntradasPorDia_ExperienciaDeUmDiaEntraAoLadoDaProducaoDessaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.EntradasPorDia_PelaOrdemDeEntrada_ESemAsQueNaoTemDiaDeInicio`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.EntradasPorDia_UmaPorLinhaEPorDia_AUltimaLancada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ExperienciaDeUmDiaSozinha_AparecenaLinhaSemProducao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ExperienciaDeUmDia_NaoTiraAProducaoNemSubstituiAEscolhaDaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ExperienciaDeUmDia_SoApareceNoSeuDia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ExperienciaDeUmDia_SoQuandoEExperienciaEEntraESaiNoMesmoDia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ExperienciaQueEntraESaiNoDia_FicaAoLadoDaProducaoDaLinha_SemATirar`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ExperienciaSemFim_NaoEDeUmDia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.OFimCobreOUltimoDia_ESoNesse_NoLimiteELogoAbaixoELogoAcima`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ProducaoSemDiaDeInicio_NaoEstaEmCurso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.ProducaoSemFim_CobreOsDiasDepoisDoInicio_ENaoOsAntes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.QuadroDoDia_ExperienciaDeDoisDias_NaoFicaAoLado_ECompeteComAProducaoDaLinha`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs :: CoberturaA1ProducaoEmCursoTests.QuadroDoDia_ExperienciaDeUmDiaFicaAoLadoDaProducao_EVemMarcadaComoExperiencia`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasDuplicarCoberturaA2Tests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasDuplicarCoberturaA2Tests.cs :: FerramentasDuplicarCoberturaA2Tests.DuplicarParaLoteNovo_LevaOLinkMcaliperEAFabricaDeOrigem_ESemUtilizacao`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/CoberturaCRegistosPorUtilizadorTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/CoberturaCRegistosPorUtilizadorTests.cs :: CoberturaCRegistosPorUtilizadorTests.OFiltroDosDias_DeEAteIncluemOsDiasEscolhidos_ESoElesTrazemORegistoDoUltimoMinuto`
- novo: `tests/DMO.Alpha.Web.Tests/Admin/CoberturaCRegistosPorUtilizadorTests.cs :: CoberturaCRegistosPorUtilizadorTests.OsRegistos_VemDosMaisRecentesParaOsMaisAntigos_ComOQueCorrigiuOuAnulou`
- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.CoberturaA2Exporta.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.CoberturaA2Exporta.cs :: AdminUsersAndTemplatesPagesTests.ListaDosUtilizadores_ComMaisDe25_ParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2FiltroLote.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2FiltroLote.cs :: BoquilhasRegistoPageTests.FichaDaBoquilha_TemOsBotoesNoTopo_ESeguemOsQuatroMiniCartoesDeDados`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2FiltroLote.cs :: BoquilhasRegistoPageTests.ProcurarBoquilha_OFiltroLoteVemLogoDepoisDaReferencia_EFiltraPorEsseLote`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2LinhaCor.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2LinhaCor.cs :: BoquilhasRegistoPageTests.PainelDasMaquinas_CadaCartaoLevaALinhaDele_ESoAsLinhasComCor`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2RegistoComProducao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2RegistoComProducao.cs :: BoquilhasRegistoPageTests.RegistoComProducao_MostraBqLoteEOsCartoesDaProducao_ParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2SemJobOn.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2SemJobOn.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_ALinhaVemJaEscolhidaComAUltimaOndeABqTeveMovimentos`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2SemJobOn.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_UmaBqSemLinhasNaFicha_MostraTodasAsLinhasSemNenhumaEscolhida`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs :: CoberturaA1EntradasDaSemanaTests.Faixa_ExperienciaQueEntraESaiNoMesmoDia_FicaAoLadoDaProducaoDessaLinha`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs :: CoberturaA1EntradasDaSemanaTests.Faixa_PelaOrdemEmQueEntram`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs :: CoberturaA1EntradasDaSemanaTests.Faixa_SoTemOQueEntraDeHojeAteUmaSemana_OQueJaEntrouSai`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs :: CoberturaA1EntradasDaSemanaTests.Faixa_VariasAEntrarNaMesmaLinhaNoMesmoDia_SoAUltimaLancada`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs :: CoberturaA1EntradasDaSemanaTests.Gaveta_AsEmCurso_UmaPorLinha_VemAntesDasQueEntram`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs :: CoberturaA1EntradasDaSemanaTests.Gaveta_ExperienciaDeUmDia_NaoEEmCurso`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaBExportaParaOBrowserTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaBExportaParaOBrowserTests.cs :: CoberturaBExportaParaOBrowserTests.ExportaAsDefinicoesDoControlo`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaBExportaParaOBrowserTests.cs :: CoberturaBExportaParaOBrowserTests.ExportaOLoginComAPalavraPasseErrada`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaBExportaParaOBrowserTests.cs :: CoberturaBExportaParaOBrowserTests.ExportaORegistoDoArmazemComUmaFerramentaAberta`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCApoio.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCContaETopoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCContaETopoTests.cs :: CoberturaCContaETopoTests.ADuracaoDaSessao_OferecePeloMenosUmaHoraAteVinteEQuatro_ESoGravaAsPermitidas_SoDeQuemAEscolhe`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCContaETopoTests.cs :: CoberturaCContaETopoTests.AFichaDoUtilizador_TemOVoltarPorCimaDoTitulo_ENaoRepeteONumeroDeOperadorNosFactos`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCContaETopoTests.cs :: CoberturaCContaETopoTests.AMinhaConta_EscondeOEmailInventadoDoLoginPorNumero_EMostraOEmailRealDoAdmin`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCContaETopoTests.cs :: CoberturaCContaETopoTests.ASessaoNova_DuraAsHorasQueOUtilizadorEscolheu_ESemEscolhaDuraUmaHora`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCContaETopoTests.cs :: CoberturaCContaETopoTests.OTopo_MostraONomeEOTituloDoOperadorAntesDoSair_ESemTituloOAdminSoTemONome`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCExportaParaOBrowserTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCExportaParaOBrowserTests.cs :: CoberturaCExportaParaOBrowserTests.ExportaARegistarEAConsultaDaReparacaoInterna`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCExportaParaOBrowserTests.cs :: CoberturaCExportaParaOBrowserTests.ExportaOLoginAsDefinicoesEAFichaDoUtilizador`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCInstalarPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCInstalarPaginaTests.cs :: CoberturaCInstalarPaginaTests.ABaseSemAdmin_MostraOFormularioDaPrimeiraInstalacao`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCInstalarPaginaTests.cs :: CoberturaCInstalarPaginaTests.ComOCodigoCerto_CriaOAdmin_VaiParaOLogin_ADepoisDeFeitaJaNaoServe`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCInstalarPaginaTests.cs :: CoberturaCInstalarPaginaTests.SemOLoginProprio_NaoHaPrimeiraInstalacao_ELevaAoLogin`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCInstalarPaginaTests.cs :: CoberturaCInstalarPaginaTests.UmCampoMalPreenchido_VoltaComOAviso_GuardaOEmailEscrito_ENadaSeCria`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCPostoDaReparacaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCPostoDaReparacaoTests.cs :: CoberturaCPostoDaReparacaoTests.OBotaoReparacaoDoLogin_AbreOPosto_EOPostoTemOVoltarAoLogin`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCPostoDaReparacaoTests.cs :: CoberturaCPostoDaReparacaoTests.OPinGuardadoPeloAdmin_FicaEmHash_ESoComEleOReparadorEntraNoPosto`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCPostoDaReparacaoTests.cs :: CoberturaCPostoDaReparacaoTests.QuemNaoTemAReparacaoInterna_TemPinMasNaoApareceNoPosto`
- novo: `tests/DMO.Alpha.Web.Tests/CoberturaCPostoDaReparacaoTests.cs :: CoberturaCPostoDaReparacaoTests.UmPinNovoSubstituiOAnterior_ENaoSeAceitaUmPinForaDosQuatroAosDozeAlgarismos`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CoberturaBExporta.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CoberturaBExporta.cs :: PesoComparacaoPageTests.Comparacao_ComBase_TemALinhaDestacada_ParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CoberturaBUmaLinha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CoberturaBUmaLinha.cs :: PesoComparacaoPageTests.Comparacao_ComecaComUmaSoLinhaDeLeitura_EDepoisDeCompararVoltaComAsEscritas`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaA2Abrir.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaA2Abrir.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_AbrirLevaAoPesoDaProducaoEscolhidaNoSelect`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaA2ResumoLongo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaA2ResumoLongo.cs :: PesoFolhaProducaoAnteriorPageTests.Resumo_ComPesoDaProducaoAnteriorLongo_ParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBAgrupamento.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBAgrupamento.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_TabelaDaComparacao_AgrupaEsteControlo_ControloAnterior_EDiferenca_PorEstaOrdem`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBCartao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBCartao.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_NomeSoNoTitulo_BotaoAbrir_EInicioEFimNoCartaoDoPeso`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBExporta.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBExporta.cs :: PesoFolhaProducaoAnteriorPageTests.Peso_NoPainel_TemOsNumerosEALeituraNova_ParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBResumo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBResumo.cs :: PesoFolhaProducaoAnteriorPageTests.Resumo_TabelaDoPeso_TemAsColunasValorGEPercentagem_EACadaLinhaAsDuasDiferencas`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaA1Aspeto.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaA1Aspeto.cs :: ControloCapabilitiesCutHPageTests.ResumoComPainelDeApoioEBotaoDosPegamentos_ExportadoParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaA2Revisao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaA2Revisao.cs :: ControloCapabilitiesCutHPageTests.ConsultaDosPegamentos_VoltarPorCimaDoTitulo_SemOQuadroLeituras_EOMapaDepoisDasTabelas`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaA2Revisao.cs :: ControloCapabilitiesCutHPageTests.Revisao_TemOTituloRevisao_EAsConsultasJuntoAoTituloDoResumo`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBExporta.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBExporta.cs :: ControloCapabilitiesCutHPageTests.PesoSemJobOn_ComCmEBq_ParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBLoteCm.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBLoteCm.cs :: ControloCapabilitiesCutHPageTests.PesoSemJobOn_CmComVariosLotes_PedeOLote_ESoResolveQuandoEscolhido`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBVoltar.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBVoltar.cs :: ControloCapabilitiesCutHPageTests.PaginasDeRegisto_TemOVoltarAoResumoDaProducao_AntesDoTitulo`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaA2Ficha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaA2Ficha.cs :: FerramentasPagesTests.Ficha_ParaOBrowser_CabeNoEcraSemDeslizar`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaA2FiltroLote.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaA2FiltroLote.cs :: FerramentasPagesTests.Pesquisar_OFiltroLoteVemLogoDepoisDaReferencia_EFiltraPorEsseLote`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaCLinhasCompativeis.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaCLinhasCompativeis.cs :: FerramentasPagesTests.Registar_DaUmaCaixaPorLinhaComOCodigoDela_EVaiParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Browser.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Browser.cs :: JobOnPageTests.Folha_Cheia_ComOitoCalibresENotasCompridas_ExportadaParaOBrowser`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Duplicar_TrazOCabecalhoDaOrigem_OPuEACalote_EGravaAProducaoNovaComEles`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Editar_LoteDoAnEscritoNoEditar_GravaSeEFicaComoLoteDoAn`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Editar_LoteDoAnVazioMostraODaBqComoSugestao_EGuardarOutraCoisaNaoOCopiaParaABase`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Folha_CaloteDoTp_SemCaloteDaProducaoMostraABaciaPs_ComElaMandaACalote`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Folha_LoteDoAnEscritoAMao_FicaEMantemSeQuandoATrocaDeBq`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Folha_LoteDoAnSemEscrever_ESempreODaBq_NoEcraENaImpressao_MesmoDepoisDeTrocarAsBq`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Folha_NaoTemOContadorDeSessaoNemOBotaoDeDesbloquearDoPainel`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Folha_PosicaoDoArmazem_ELinkParaOArmazemDaFerramenta_SoParaQuemTemOArmazem`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Imprimir_UsaAsPincasDoCartaoPiInteiras_ComODiametro_SemALinhaTipo`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Painel_FolhaNoPainel_EUmResumoComAbrirFolha_SemOsCartoesDaFolha`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Planeamento_FaixaDoJobOn_TemOQueEntraEstaSemana_ENaoOQueJaEntrou`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaBExperiencia.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaBExperiencia.cs :: JobOnPageTests.FolhaNova_Duplicar_NaoTemOBotaoExperiencia`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaBExperiencia.cs :: JobOnPageTests.FolhaNova_Experiencia_AbreEmBrancoComONumeroAnoZeroZero_EMarcadaComoExperiencia`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaBExperiencia.cs :: JobOnPageTests.FolhaNova_TemOsDoisBotoes_ProducaoEExperiencia_ComAProducaoEscolhida`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.CoberturaA2Mapa.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.CoberturaA2Mapa.cs :: PegamentosPageTests.FolhaComMedicoes_OMapaDeLimitesFicaPorBaixoDasLeituras_SemTextoDeAjuda`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.CoberturaBPassos.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.CoberturaBPassos.cs :: PegamentosPageTests.Folha_TemAsLeiturasEOMapa_SemOsPassosContextoMedicoesMapa`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/CoberturaCReparacaoInternaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/CoberturaCReparacaoInternaTests.cs :: CoberturaCReparacaoInternaTests.ANaoSerReparacao_AConsultaMostraOTrabalhoNaLinha_EFiltraPeloTipoDeTrabalho`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/CoberturaCReparacaoInternaTests.cs :: CoberturaCReparacaoInternaTests.OTrabalhoEscolhidoNoRegisto_FicaGravado_ESemEscolhaOuComUmValorDesconhecidoEReparacao`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/CoberturaCReparacaoInternaTests.cs :: CoberturaCReparacaoInternaTests.OsFiltrosDaConsulta_VemPelaOrdemPedida_ESemJobOnTemUmaDica`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/CoberturaCReparacaoInternaTests.cs :: CoberturaCReparacaoInternaTests.OsFiltrosDoHistorico_VemPelaMesmaOrdem_SemQuemNemCmMf`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaCoberturaBTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaCoberturaBTests.cs :: ReparacaoInternaConsultaCoberturaBTests.Consulta_ReferenciaPrimeiro_ProducaoLogoAseguir_ETodosOsFiltrosNumSoFormulario`
- novo: `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs :: a linha que limpa a escolha não tem triângulo`
- novo: `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs :: cada ferramenta da lista tem o triângulo que aponta para a ficha dela`
- novo: `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs :: clicar no texto da ferramenta escolhe-a e não abre a ficha`
- novo: `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs :: clicar no triângulo abre a ficha no painel e não escolhe a ferramenta`
- novo: `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs :: sem painel de apoio, o triângulo abre a ficha noutro separador sem escolher`
- novo: `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs :: uma procura sem endereço de ficha não desenha triângulo`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: Calibres: «P 59.20 / M 58.60» quebra só na barra, em dois segmentos inteiros`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: Dimensões compridas: só se pode quebrar depois de cada barra`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: Editar: a procura da CM dá à lista o endereço da ficha de cada ferramenta`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: a Informação técnica abre de uma em uma, e o clique fora ou o Esc fecham-na`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: a nota ampliada diz de que ferramenta é e deixa o lugar guardado no cartão`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: com uma nota ampliada, o clique noutra nota só fecha a primeira e não abre a outra`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: com uma nota ampliada, o rato premido fora não põe o cursor noutra caixa`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: no Editar, a nota abre pronta a escrever com o cursor no fim do texto`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: o artigo com imagem da referência mostra-a no lugar do desenho e oferece retirar`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: o artigo sem imagem mostra o desenho de reserva e não oferece retirar`
- novo: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: um clique na procura da Referência seleciona-a toda, para o Backspace a apagar`
- novo: `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs :: Cancelar o recorte não grava nada e a imagem anterior fica como estava`
- novo: `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs :: Colar algo que não é imagem não abre o recorte`
- novo: `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs :: Colar dentro de um campo de escrita fica no campo e não grava imagem`
- novo: `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs :: Colar uma imagem (Ctrl+V) fora dos campos abre o recorte e grava o recorte`
- novo: `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs :: Guardar recorte com imagem anterior: a anterior fica guardada e o artigo.png é o novo`
- novo: `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs :: Guardar recorte grava na pasta da referência o recorte, não a imagem inteira`
- novo: `tests/ShellProof/cobertura_a2_bq-sem-jobon.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_a2_bq-sem-jobon.test.cjs :: As sugestões da BQ só aparecem depois de escrever, e o rótulo é «BQ C002 · Lote 18»`
- novo: `tests/ShellProof/cobertura_a2_bq-sem-jobon.test.cjs :: Com a BQ já escolhida ao abrir, a Linha só tem as da ficha e vem com a primeira`
- novo: `tests/ShellProof/cobertura_a2_bq-sem-jobon.test.cjs :: Escolher a BQ deixa na Linha só as da ficha dela, com a primeira já escolhida`
- novo: `tests/ShellProof/cobertura_a2_bq-sem-jobon.test.cjs :: Job On anterior: a BQ mostra as referências dela, a referência as produções, e a produção põe a linha do Job On`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: A primeira leitura da pasta envia todos os ficheiros do SAP, do mais antigo para o mais novo, e nenhum do DMO`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Apagar lidos: pergunta antes e apaga só os ficheiros do SAP já lidos`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Apagar lidos: se a pessoa não confirma, nada é apagado`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Apagar lidos: sem nada lido ainda, avisa e não apaga nem pergunta`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Com um ficheiro já lido, só os mais novos do que o último lido vão para o servidor`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Numa pasta com mais de dez listas por ler, vão as dez mais recentes`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Se a lista mais recente já foi lida, nada vai para o servidor e o cartão diz isso`
- novo: `tests/ShellProof/cobertura_b_peso-faixa.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_b_peso-faixa.test.cjs :: A diferença nominal positiva leva «+» e duas casas, mesmo inteira`
- novo: `tests/ShellProof/cobertura_b_peso-faixa.test.cjs :: A faixa escreve inteiros sem casas e decimais com duas, como o servidor`
- novo: `tests/ShellProof/cobertura_b_peso-menos.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_b_peso-menos.test.cjs :: Esc dentro da correção sai da correção e não chega ao resto da página`
- novo: `tests/ShellProof/cobertura_b_peso-menos.test.cjs :: Um pedido de apagar anterior não fica no endereço do novo`
- novo: `tests/ShellProof/cobertura_b_peso-menos.test.cjs :: «−» com duas linhas só tira a linha vazia, sem perguntar nada`
- novo: `tests/ShellProof/cobertura_b_peso-menos.test.cjs :: «−» com uma só linha abre a pergunta de apagar a última leitura gravada, sem apagar nada`
- novo: `tests/ShellProof/cobertura_b_peso-menos.test.cjs :: «−» com uma só linha e nenhuma leitura gravada não faz nada`
- novo: `tests/ShellProof/cobertura_b_ri-consulta.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/cobertura_b_ri-consulta.test.cjs :: A Produção fechada mostra só o número; aberta, o resto por baixo de cada uma`
- novo: `tests/ShellProof/cobertura_b_ri-consulta.test.cjs :: Carregar numa pessoa do resumo escolhe-a no «Quem» e aplica; uma que não está na lista segue o link`
- novo: `tests/ShellProof/cobertura_b_ri-consulta.test.cjs :: Esc fecha a lista aberta sem escolher nada`
- novo: `tests/ShellProof/cobertura_b_ri-consulta.test.cjs :: Escolher uma produção fecha a lista, fica só o número e o select muda e avisa`
- novo: `tests/visual/cobertura_a1_browser.py`
- novo: `tests/visual/cobertura_a2_browser.py`
- novo: `tests/visual/cobertura_b_browser.py`
- novo: `tests/visual/cobertura_c_browser.py`
- novo: `tests/visual/pendentes_a1_browser.py`
- novo: `tests/visual/pendentes_a2_browser.py`
- novo: `tests/visual/pendentes_b_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/ShellProof/package.json`

Selo depois: `f1d0e4a34de15020`

## A-0327 · 2026-10-29 · ok-do-diogo

Porquê: Os 3 testes novos do #291 (DMO-588) que gravam a folha passam a escrever o diâmetro do corpo do MF com um só número (71.7), porque o #288 (DMO-587) recusa dois números num diâmetro; o que eles verificam não muda.
Referência: Regra do Diogo 28/10 «no corpo não deve ter dois valores» (cmsg_01K75FTs9bQnbhhH7qttpgraNidJsALf8gXwDKhAgYzVEb)
Novos: 0 · mudaram: 3 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Duplicar_TrazOCabecalhoDaOrigem_OPuEACalote_EGravaAProducaoNovaComEles`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Editar_LoteDoAnEscritoNoEditar_GravaSeEFicaComoLoteDoAn`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs :: JobOnPageTests.Editar_LoteDoAnVazioMostraODaBqComoSugestao_EGuardarOutraCoisaNaoOCopiaParaABase`

Selo depois: `3f7b0c747f3b0cec`

## A-0328 · 2026-10-29 · contagem-lista

Porquê: C03 da auditoria do Codex de 28/10/2026 (DMO-590, #293): teste de browser novo tests/visual/bq_historico_lista_browser.py (lista de Movimentos das Boquilhas com cabeçalho e uma linha inteira a 1290×646, 1366×768 e 1100×646); no ci-local.sh só se acrescenta o passo que o corre.
Referência: PR #293
Novos: 1 · mudaram: 1 · saíram: 0

- novo: `tests/visual/bq_historico_lista_browser.py`
- mudou: `scripts/ci/ci-local.sh`

Selo depois: `88d81159021341b1`

## A-0332 · 2026-10-29 · teste-novo

Porquê: Peso (#295): × para fechar o aviso «O Job On mudou» (DMO-594) e barra dos filtros do Aprovar sem a etiqueta «Pendente» (DMO-597); testes novos.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraL37Lbd4WXdmt65HTzFQZEw; cmsg_01K75FTs9bQnbhhH7qttpgraQhsrmB2N6KUBsjwFoahTBK
Novos: 6 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs :: ControloCapabilitiesCutHPageTests.Approve_BarraDosFiltrosNaoRepeteAEtiquetaPendente`
- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs :: ControloCapabilitiesCutHPageTests.Approve_PesoDaProducaoAnteriorMostraAhDiferenca`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Peso_OAvisoDeQueOJobOnMudouTemUmXParaFechar`
- novo: `tests/ShellProof/aviso-dispensar.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/aviso-dispensar.test.cjs :: o × fecha o aviso e ele não volta ao reabrir a página`
- novo: `tests/ShellProof/aviso-dispensar.test.cjs :: uma mudança mais nova do que a que se fechou traz o aviso de volta`

Selo depois: `09cea02a345007d1`

## A-0330 · 2026-10-29 · contagem-lista

Porquê: Painéis fecham ao clicar fora, «Voltar» fixo e hover legível (DMO-592/593, #296): ficheiro novo painel-fecha-ao-clicar-fora.test.cjs e a sua entrada na lista do test:js; o «sim» do Diogo a mudar a regra de 28/10 está nas mensagens de 29/10 05:40.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraRjoHenMh7tBoYHhFiZKV6A
Novos: 6 · mudaram: 2 · saíram: 0

- novo: `tests/ShellProof/painel-fecha-ao-clicar-fora.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/painel-fecha-ao-clicar-fora.test.cjs :: com alterações por guardar, um clique fora mostra o aviso em vez de fechar`
- novo: `tests/ShellProof/painel-fecha-ao-clicar-fora.test.cjs :: painel de apoio: um clique fora fecha-o`
- novo: `tests/ShellProof/painel-fecha-ao-clicar-fora.test.cjs :: painel de apoio: «Voltar» é um link no canto esquerdo, antes do título, e volta à página que lá estava`
- novo: `tests/ShellProof/painel-fecha-ao-clicar-fora.test.cjs :: um clique fora fecha o painel; dentro da caixa não`
- novo: `tests/visual/hover_linhas_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/ShellProof/package.json`

Selo depois: `6c7ce321b5f42e87`

## A-0337 · 2026-10-29 · ok-do-diogo

Porquê: Lote .235: DMO-599 (#301, teste novo: Job On no painel abre o resumo); DMO-598 (#298, botão do SAP «Atualizar»); DMO-406/495/509/522 (#299, os 3 pendentes_*_browser.py entram no CI no passo «Browser no aspeto»); DMO-591 (#294, a opção da lista leva reparador + dia); DMO-596 (#300, o quadro Saídas/Entradas saiu e 5 testes deixam de o exigir).
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraSqBF3QGLt9P9ZqybZ6R37g; cmsg_01K75FTs9bQnbhhH7qttpgraLnQSBGPCNmfDYFWnFKi1A8; Diogo «sim» cmsg_01K75FTs9bQnbhhH7qttpgra9QKtyXnRyQfgfVzCYpBW2R
Novos: 7 · mudaram: 7 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.Movimentos_SemOQuadroDeTotais_SoAContagem`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.OBotaoDoSap_DizAtualizar_ENaoPedirSoAsAEspera`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.Lista_MostraSoOReparador_EAProcuraCorreAoEscrever`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelResumo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelResumo.cs :: JobOnPageTests.Planeamento_NoPainel_AbreOResumoDaFolha`
- novo: `tests/visual/bq_registo_dia_calendario_browser.py`
- novo: `tests/visual/reparacao_listas_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.HistoryDay_ReplacesRangePreservesFiltersAndExportsTheSameSelection`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.History_CalendarDay_ChangesOnlyTheZones`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.History_Filters_ChangeOnlyTheZones`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.History_OpensBlank_UntilAReferenceOrAPeriodIsSearched`
- mudou: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.Matrix_MovimentosSemMaquina_AvisaComLinkParaALista`
- mudou: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: ReparacaoProgramadaPaginaTests.Ferramentas_VeOSeparador_AProcuraAbreVazia_EAssociaAUmaListaNova`
- saiu: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.FilterCounters_AreQuantitySums_NotStockBalances`

Selo depois: `50d42fefc3f8d7e2`

## A-0338 · 2026-10-29 · teste-novo

Porquê: Teste novo: o resumo do Job On no painel abre mais largo (DMO-600, #296).
Referência: PR #296
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs :: ControloCapabilitiesCutHPageTests.Estrutura_PainelMedio_AbreMaisLargo`

Selo depois: `c07be0113f2cf44c`

## A-0340 · 2026-10-29 · ok-do-diogo

Porquê: DMO-601: o Histórico de produções da ficha deixa de ter o quadro «Referência: … (n)»; o teste garante que não aparece
Referência: Diogo cmsg_01K75FTs9bQnbhhH7qttpgraKAikKZHUHqxozwSr6bXXZT, sim cmsg_01K75FTs9bQnbhhH7qttpgraBdSHRV6GZHaqDPaN7n3ZfT
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoComArtigo.cs :: DefinicoesPorModuloPageTests.OHistoricoDeProducoes_MostraOArtigoDeCadaProducao`

Selo depois: `2ea04e4a0c3694fb`

## A-0348 · 2026-10-29 · ok-do-diogo

Porquê: DMO-608: o Geral da ficha numa só linha (Linhas na vertical); DMO-609: o Histórico de produções sem Δ garrafas e com os dias à frente das datas
Referência: Diogo cmsg_01K75FTs9bQnbhhH7qttpgraP8k4Sq3SFm2yW2yJiMKhi4, cmsg_01K75FTs9bQnbhhH7qttpgraTm2zMmYCwLg6k4ZBrdQp33, sim cmsg_01K75FTs9bQnbhhH7qttpgraMY9B9gYQvikPov9ExmSecF
Novos: 4 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoSoBruto.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoSoBruto.cs :: DefinicoesPorModuloPageTests.OHistoricoDeProducoes_SoGarrafasEmBruto_EOsDiasAFrenteDasDatas`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.GeralNumaLinha.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.GeralNumaLinha.cs :: FerramentasPagesTests.Ficha_Geral_LinhasNaVertical_EUtilizacaoNaMesmaLinha`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.OHistoricoDeProducoes_DaODeltaDeGarrafasEAJanelaDoPeso`
- mudou: `tests/visual/cobertura_a2_browser.py`

Selo depois: `0c2e5405174ec261`

## A-0341 · 2026-10-29 · teste-novo

Porquê: Δ anterior do peso por produção salta as anteriores sem peso (DMO-602)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraUgL2ipM5RzYX6UEZx6Kunt
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PesoPorProducao.cs :: DefinicoesPorModuloPageTests.OPesoDoHistorico_DeltaSaltaAsProducoesAnterioresSemPeso`

Selo depois: `199918b21337a0b9`

## A-0343 · 2026-10-29 · contagem-lista

Porquê: Lote 0 da auditoria .230 (Codex, SAP-01..03): logger no FerramentasFabricoSap (apaga ordens que saíram da janela do relatório, avisa colisões), testes novos para isso, teste novo do «Atualizar do SAP» quando o diário diz «erro», e a ajuda do SAP já não diz que nenhuma senha fica nos ficheiros.
Novos: 5 · mudaram: 12 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.DuasLinhasComAMesmaChave_FicamColapsadasNumaSoSemApagarNada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.UmaOrdemForaDaJanelaDoRelatorioNovo_NaoEApagada`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.UmaOrdemQueDeixaDeVirNoRelatorio_EApagadaDaJanelaDoRelatorio`
- novo: `tests/ShellProof/ferramentas-sap_feito-com-falha.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/ferramentas-sap_feito-com-falha.test.cjs :: Atualizar do SAP: quando o diário escreve «erro», o cartão mostra falha e não «feito»`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FabricoSap.cs :: PostgresMigrationIntegrationTests.FabricoSap_LerDuasVezesUmaSoLinha_NoPostgreSql`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs :: HistoriaCenario.HistoriaDeUmaProducao_TodosOsModulosVeemOMesmo`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoricoLote.cs :: PostgresMigrationIntegrationTests.HistoricoFerramentas_ProducoesEmLote_IguaisAUmaAUma_ComConsultasFixas`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoCadencia.cs :: PostgresMigrationIntegrationTests.SapFabricoCadencia_GravaSoACadencia_NoPostgreSql`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaPorProducaoTests.cs :: FabricoSapCadenciaPorProducaoTests.CadaProducaoDaFerramenta_TemASuaCadencia`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaPorProducaoTests.cs :: FabricoSapCadenciaPorProducaoTests.OJobOnSemCadencia_FicaComADaProducaoAnteriorDaMesmaReferencia`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaTests.cs :: FabricoSapCadenciaTests.Importar_GravaACadencia_ESemElaNaoApagaAJaGravada`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_AAjudaDoSapAbreAPaginaDeAjuda`
- mudou: `tests/ShellProof/package.json`

Selo depois: `4cba7644ecb57f38`

## A-0349 · 2026-10-29 · contagem-lista

Porquê: Só entrou o ficheiro novo voltar-automatico.test.cjs na lista de testes (package.json); Diogo 29/10: «nos ja criamos o voltar para todas as paginas'?»
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra4mH2zZ2eTtEjgoducXvkiA
Novos: 2 · mudaram: 1 · saíram: 0

- novo: `tests/ShellProof/voltar-automatico.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/voltar-automatico.test.cjs :: a página sem Voltar escrito ganha-o no canto, só se vier de outra página`
- mudou: `tests/ShellProof/package.json`

Selo depois: `1667b4002ee21767`

## A-0350 · 2026-10-29 · ok-do-diogo

Porquê: Lote .236: junta as impressões já aprovadas em A-0340/A-0348 (#302 DMO-601/608/609), A-0341 (#304 DMO-602), A-0343 (#305 DMO-605/606/607), A-0349 (#306 DMO-610) e o #301 (DMO-603, sem testes)
Referência: A-0340; A-0341; A-0343; A-0348; A-0349
Novos: 2 · mudaram: 1 · saíram: 0

- novo: `tests/ShellProof/voltar-automatico.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/voltar-automatico.test.cjs :: a página sem Voltar escrito ganha-o no canto, só se vier de outra página`
- mudou: `tests/ShellProof/package.json`

Selo depois: `48fb94b315990dac`

## A-0381 · 2026-10-29 · teste-novo

Porquê: DMO-641 Pesquisa no Armazém e DMO-643 (A-0383) link da % uso: testes novos
Novos: 8 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.LinkDaPercentagemDeUso_TemOEstiloDaApp`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.OSeparador_EstaAoLadoDoRegistoEDosMovimentos_EAPaginaAbreVazia`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.Pesquisar_PelaPosicao_EComNada_AvisaQueNaoEncontrou`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.Pesquisar_ProcuraCmMfEBq_EACadaLinhaAbreORegistoDaFerramenta`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.ProcuraAoEscrever_DevolveSoAZonaDosResultados`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.SemAChave_NaoEntra`
- novo: `tests/visual/armazem_pesquisar_browser.py`

Selo depois: `70ba42c3e40f4230`

## A-0351 · 2026-10-29 · contagem-lista

Porquê: 2.ª passagem da auditoria .230, lote 1: travão do Posto por X-Forwarded-For falsificável (AUTH-B02), resposta atrasada de ferramenta sobrepõe o cartão (Job On), Enter sem navegar limpa a CM em silêncio (Job On).
Novos: 3 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: PostoTravaoTests.Posto_PinsErrados_ComXForwardedForDiferenteDeCadaVez_EsperaNaMesma`
- novo: `tests/ShellProof/dmo-suggest.test.cjs :: Enter sem navegar nas flechas confirma a ferramenta já escolhida, não a limpa (auditoria do Codex, 29/10/2026)`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Editar: a resposta atrasada de uma ferramenta trocada não sobrepõe o cartão da escolhida a seguir`
- mudou: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/dmo-suggest.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/jobon-folha.test.cjs :: (resto do ficheiro)`

Selo depois: `9a278c4dad62b6a0`

## A-0360 · 2026-10-29 · contagem-lista

Porquê: AUTH-B01 (auditoria de 29/10/2026, decisão do Diogo): o login passa a esperar a sério depois de várias palavras-passe erradas seguidas, reutilizando o travão do Posto (TravaoDoPosto) chaveado pela conta; a espera esgota-se sozinha, nunca bloqueia para sempre.
Novos: 4 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: AuthenticationTests.Login_DuasFalhasEDepoisACerta_NaoEspera`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: AuthenticationTests.Login_MesmaContaDuasOrigens_UmaPalavraPasseDeCadaVez`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: AuthenticationTests.Login_PalavrasPasseErradas_EsperamCadaVezMais_EACertaEntraNaMesmaAposEsperar`

Selo depois: `5d4ac9207aa000f8`

## A-0354 · 2026-10-29 · contagem-lista

Porquê: 2.ª passagem da auditoria .230 (Codex/Qoder, 29/10): fabrico SAP — SAP-B01 (linha contada a dobrar numa produção sem fim), SAP-B02 (reimportação apaga valores já lidos) e SAP-B03 (referência truncada apanha o material errado).
Novos: 4 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ProducaoQueAcabaAMeioDaLinha_LigaELevaALinhaInteira`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ProducaoSemFim_ComProximaNaMesmaMaquina_NaoEngoleALinhaDaSeguinte`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ReferenciaCorresponde_ExigeFronteiraAposOPrefixo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ReimportarComRelatorioParcial_NaoApagaOQueJaEstavaGravado`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ALinhaDoSap_LigaSeAoJobOnPelaReferenciaMaquinaEDatas`

Selo depois: `eb7e46cd07d185de`

## A-0344 · 2026-10-29 · contagem-lista

Porquê: SEC-01 (auditoria Codex 29/10/2026): «Atualizar do SAP» e «Instalar», que escreviam a senha do SAP nos scripts, saem da página Ferramentas (qualquer conta com o módulo chegava-lhes); ficam só em Admin › Definições › SAP. Teste novo confirma que o operador perde o handler (ScriptsSap) e o Admin mantém a senha; teste novo do browser confirma que o cartão sem esses botões não rebenta e «Apagar lidos» continua.
Novos: 1 · mudaram: 4 · saíram: 0

- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Sem «Atualizar do SAP» no cartão (Admin ficou com ele), o script não rebenta e «Apagar lidos» continua a funcionar`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AsPastas_SaoGerais_SoAsAcoesFicamParaTodos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_MudaOSap_EOsScriptsDaPastaLevamAsDefinicoes`
- mudou: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: (resto do ficheiro)`

Selo depois: `07d1b1e410dc2fdb`

## A-0382 · 2026-10-29 · ok-do-diogo

Porquê: Lote .236: junta as impressões já explicadas em A-0344 (#311), A-0351 (#308), A-0354 (#310), A-0360 (#309) e A-0381 (#307), além das do lote anterior (A-0350)
Referência: A-0344; A-0351; A-0354; A-0360; A-0381
Novos: 20 · mudaram: 8 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ProducaoQueAcabaAMeioDaLinha_LigaELevaALinhaInteira`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ProducaoSemFim_ComProximaNaMesmaMaquina_NaoEngoleALinhaDaSeguinte`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ReferenciaCorresponde_ExigeFronteiraAposOPrefixo`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ReimportarComRelatorioParcial_NaoApagaOQueJaEstavaGravado`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.LinkDaPercentagemDeUso_TemOEstiloDaApp`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.OSeparador_EstaAoLadoDoRegistoEDosMovimentos_EAPaginaAbreVazia`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.Pesquisar_PelaPosicao_EComNada_AvisaQueNaoEncontrou`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.Pesquisar_ProcuraCmMfEBq_EACadaLinhaAbreORegistoDaFerramenta`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.ProcuraAoEscrever_DevolveSoAZonaDosResultados`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPesquisarPaginaTests.cs :: ArmazemPesquisarPaginaTests.SemAChave_NaoEntra`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: AuthenticationTests.Login_DuasFalhasEDepoisACerta_NaoEspera`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: AuthenticationTests.Login_MesmaContaDuasOrigens_UmaPalavraPasseDeCadaVez`
- novo: `tests/DMO.Alpha.Web.Tests/AuthenticationTests.TravaoDeLogin.cs :: AuthenticationTests.Login_PalavrasPasseErradas_EsperamCadaVezMais_EACertaEntraNaMesmaAposEsperar`
- novo: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: PostoTravaoTests.Posto_PinsErrados_ComXForwardedForDiferenteDeCadaVez_EsperaNaMesma`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Sem «Atualizar do SAP» no cartão (Admin ficou com ele), o script não rebenta e «Apagar lidos» continua a funcionar`
- novo: `tests/ShellProof/dmo-suggest.test.cjs :: Enter sem navegar nas flechas confirma a ferramenta já escolhida, não a limpa (auditoria do Codex, 29/10/2026)`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: Editar: a resposta atrasada de uma ferramenta trocada não sobrepõe o cartão da escolhida a seguir`
- novo: `tests/visual/armazem_pesquisar_browser.py`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs :: FerramentasFabricoSapTests.ALinhaDoSap_LigaSeAoJobOnPelaReferenciaMaquinaEDatas`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.AsPastas_SaoGerais_SoAsAcoesFicamParaTodos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.OAdmin_MudaOSap_EOsScriptsDaPastaLevamAsDefinicoes`
- mudou: `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/dmo-suggest.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/jobon-folha.test.cjs :: (resto do ficheiro)`

Selo depois: `201be6b94f26e7e5`

## A-0384 · 2026-10-29 · ok-do-diogo

Porquê: Avisos dos cartões no canto superior direito com × (teste novo, DMO-644); nos Calibres o «P / M» fica um por cima do outro sem a barra, como o Diogo pediu (DMO-645): o teste do Extrator passa a esperar os valores sem «/» e o texto escrito no título.
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra8XrmodJiio6g6eC4ixneHQ
Novos: 3 · mudaram: 1 · saíram: 0

- novo: `tests/ShellProof/jobon-folha-avisos-canto.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/jobon-folha-avisos-canto.test.cjs :: Editar: os avisos ficam onde estão`
- novo: `tests/ShellProof/jobon-folha-avisos-canto.test.cjs :: Ver: avisos do CM e da BQ vão para o canto, com o botão e um × que fecha`
- mudou: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: Calibres: «P 59.20 / M 58.60» quebra só na barra, em dois segmentos inteiros`

Selo depois: `7a4c93168414ea60`

## A-0385 · 2026-10-29 · ok-do-diogo

Porquê: Lote .236: junta as impressões do #301 (DMO-644/645/646) já explicadas em A-0384
Referência: A-0384
Novos: 3 · mudaram: 1 · saíram: 0

- novo: `tests/ShellProof/jobon-folha-avisos-canto.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/jobon-folha-avisos-canto.test.cjs :: Editar: os avisos ficam onde estão`
- novo: `tests/ShellProof/jobon-folha-avisos-canto.test.cjs :: Ver: avisos do CM e da BQ vão para o canto, com o botão e um × que fecha`
- mudou: `tests/ShellProof/cobertura_a1_folha-js.test.cjs :: Calibres: «P 59.20 / M 58.60» quebra só na barra, em dois segmentos inteiros`

Selo depois: `4390bbb2cbbcc0af`

## A-0358 · 2026-10-29 · contagem-lista

Porquê: 2.ª passagem da auditoria .230 (PESO-B03, BQ-B01): a associação do Peso ao controlo grava a sua hora (um Peso criado antes mas associado depois de aprovar fica «Por aprovar»); o Histórico das Boquilhas usa a referência/lote congelados quando a BQ foi eliminada.
Novos: 2 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/RegisterBqMovementHandlerTests.cs :: StubSettings.Historico_ComABqEliminada_MostraAReferenciaEOLoteCongelados`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAprovacaoTests.cs :: ControloAprovacaoTests.UmPesoCriadoAntesEAssociadoDepoisDeAprovar_FicaPorAprovar`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs :: AssociatePesoToControloHandlerTests.Associate_SameCmTool_KeepsSamePesoAndLinksProduction`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.PesoEntity_HasNoProductionIdentityColumns`

Selo depois: `bf0976279e3f3a84`

## A-0387 · 2026-10-29 · teste-novo

Porquê: DMO-648/A-0387 e DMO-649/A-0388 (auditoria 29/10): referência com lote inexistente nas Boquilhas mostra mensagem (lista e PDF) em vez de erro 500; Enter no Lote das Últimas entradas e saídas do Armazém aplica o filtro (botão de envio escondido). 2 testes .NET e 1 script de browser novos; nenhum teste antigo mudou.
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Ultimas_OFormularioTemUmEnvioEscondido_ParaOEnterNoLote`
- novo: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.ReferenciaExistente_ComLoteInexistente_MostraMensagemClara_NaListaENoPdf`
- novo: `tests/visual/armazem_ultimos_enter_lote_browser.py`

Selo depois: `78b84285abd75ac5`

## A-0389 · 2026-10-29 · ok-do-diogo

Porquê: Lote .237: junta as impressões já explicadas em A-0358 (#313) e A-0387 (#314)
Referência: A-0358; A-0387
Novos: 5 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/RegisterBqMovementHandlerTests.cs :: StubSettings.Historico_ComABqEliminada_MostraAReferenciaEOLoteCongelados`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAprovacaoTests.cs :: ControloAprovacaoTests.UmPesoCriadoAntesEAssociadoDepoisDeAprovar_FicaPorAprovar`
- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Ultimas_OFormularioTemUmEnvioEscondido_ParaOEnterNoLote`
- novo: `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs :: BoquilhasHistoricoPageTests.ReferenciaExistente_ComLoteInexistente_MostraMensagemClara_NaListaENoPdf`
- novo: `tests/visual/armazem_ultimos_enter_lote_browser.py`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs :: AssociatePesoToControloHandlerTests.Associate_SameCmTool_KeepsSamePesoAndLinksProduction`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs :: PesoToolScopedSchemaTests.PesoEntity_HasNoProductionIdentityColumns`

Selo depois: `7d6e9e4dd09e857f`

## A-0359 · 2026-10-29 · contagem-lista

Porquê: CI do #313: migração nova (PesoAssociadoAoControloEmUtc) tinha de entrar na lista fixa do MigrationFoundationTests
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `852a8fa9a1e466cb`

## A-0390 · 2026-10-29 · contagem-lista

Porquê: Lote .237: a lista das migrações no MigrationFoundationTests ganha a do #313 (PesoAssociadoAoControloEmUtc), como em A-0315/A-0322; só a lista, nenhuma asserção nova ou afrouxada
Referência: #313; #316
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`

Selo depois: `1cfa56ff3a5f7761`

## A-0391 · 2026-10-29 · teste-novo

Porquê: Os scripts do SAP não usam o «Like» do VBA, que dava 800A0023 no PC do SAP (DMO-650).
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraHox5ySvEzeS1u4MkLYRRrV
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: SapScriptsVbsTests.OsScriptsDoSap_NaoUsamOLikeDoVba`

Selo depois: `11d6061decabf2cd`

## A-0392 · 2026-10-29 · teste-novo

Porquê: Hora e dias do SAP na mesma linha (DMO-651).
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraMwugQGfF7VkHCAd75ed5wu
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.AHoraEOsDiasDoSap_FicamNaMesmaLinha`

Selo depois: `ce60ee745ffd910a`

## A-0393 · 2026-10-29 · teste-novo

Porquê: Lote .238: junta os testes novos do #322 (SapScriptsVbsTests, AHoraEOsDiasDoSap_FicamNaMesmaLinha), já explicados em A-0391/A-0392
Referência: A-0391; A-0392
Novos: 3 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.AHoraEOsDiasDoSap_FicamNaMesmaLinha`
- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: SapScriptsVbsTests.OsScriptsDoSap_NaoUsamOLikeDoVba`

Selo depois: `9b47efa82c4e2c2f`

## A-0380 · 2026-10-29 · ok-do-diogo

Porquê: DEL-01 (2.ª passagem do sweep .230, commit ef98da5): eliminar um Job On com uma CM «Colocar de parte» na comparação do Peso falhava no PostgreSQL pela FK Restrict de PesoComparacaoCmDecisoes; o cenário de EliminarJobOn passa a semear essa decisão e a prova fica no teste real (DMO-640).
Referência: dmo-Improvements versoes/0.3.0-beta.230/sweep-bugs-2026-10-29/DADOS-E-ELIMINACAO.md
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/EliminarJobOnCenario.cs :: (resto do ficheiro)`

Selo depois: `6a17a94d909e4104`

## A-0365 · 2026-10-29 · contagem-lista

Porquê: PESO-B01/B04/B05 (sweep 2): testes novos para o emparelhamento do CM com zeros à esquerda, a revalidação da produção anterior e o corte das últimas 5 por produção; o 'resto do ficheiro' de PesoComparisonQueryTests.cs só ganhou o ajudante SeedPesoVazioAsync, sem mudar o que os testes existentes verificam.
Novos: 8 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmComLetraEZeros_NuncaNumerico_FicaSemContraparte`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmComZerosAEsquerda_EmparelhaComOMesmoNumero`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmDigitosDiferentes_NuncaEmparelham`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmIguais_Emparelham`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs :: PesoComparisonQueryTests.FindCandidates_PesoVazioCriadoDepois_NaoEscondeAProducaoComLeituras`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoProducaoAnteriorQueryTests.cs :: PesoProducaoAnteriorQueryTests.AindaValida_DepoisDeMudarAsDatas_DeixaDeSerAnterior_Falso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoProducaoAnteriorQueryTests.cs :: PesoProducaoAnteriorQueryTests.AindaValida_ProducaoAindaAnterior_Verdadeiro`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoProducaoAnteriorQueryTests.cs :: PesoProducaoAnteriorQueryTests.AindaValida_SemProducaoAtual_Falso`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs :: (resto do ficheiro)`

Selo depois: `93ff394fb2507cde`

## A-0339 · 2026-10-29 · contagem-lista

Porquê: DMO-634/635/636/637: adiciona testes para 4 fixes JS (espelho na recusa, Escape na lista de sugestões, link Por associar com data-dmo-dialog, CS furos limpa cs_furos); as mudanças nos ficheiros de teste são só os ajudantes (espelho na página de reparadores) à volta dos novos testes.
Novos: 4 · mudaram: 3 · saíram: 0

- novo: `tests/ShellProof/dmo-suggest.test.cjs :: DMO-635: Escape com a lista de sugestões aberta não sobe até ao painel/janela`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: CS furos no Editar: apagar abertos e depois fechados limpa o campo escondido cs_furos`
- novo: `tests/ShellProof/troca-parcial.test.cjs :: DMO-634: uma recusa ao editar no sítio também repõe o espelho da linha`
- novo: `tests/ShellProof/troca-parcial.test.cjs :: DMO-636: um link «Por associar» que também abre uma janela não pede a página nem cria histórico`
- mudou: `tests/ShellProof/dmo-suggest.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/jobon-folha.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/troca-parcial.test.cjs :: (resto do ficheiro)`

Selo depois: `c195df72148d3ca9`

## A-0386 · 2026-10-29 · teste-novo

Porquê: BQ-B02..B06 (2.ª passagem do sweep .230): testes novos para o Saldo de BQs eliminadas, o meter-a-zero no Histórico/PDF, o Corrigir no Armazém, a versão do Excel e o filtro do reparador.
Novos: 9 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.OFiltroDoReparador_UsaOMesmoValorQueALinhaMostra_BQ_B06`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_DepoisDeUmCorrigir_HerdaALinhaDaEntradaAnterior_BQ_B04`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelBoquilhasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelBoquilhasTests.cs :: BqExcelBoquilhasTests.VersaoAsync_IgualSemNenhumaMudanca`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryDiscrepancyTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryDiscrepancyTests.cs :: BqHistoryDiscrepancyTests.DuasBqsEliminadas_TemSaldosSeparados_BQ_B02`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryDiscrepancyTests.cs :: BqHistoryDiscrepancyTests.MeterAZero_EscondeOSaldoDasLinhasDeAntes_BQ_B03`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BqExcelVersao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BqExcelVersao.cs :: PostgresMigrationIntegrationTests.VersaoAsync_MudaQuandoSeEditaOLoteDaBQ_SemNovoMovimento_BQ_B05`

Selo depois: `4866d9b665f0f44a`

## A-0394 · 2026-10-29 · ok-do-diogo

Porquê: Lote .239: junta os testes (novos, mudados e os dois antigos do SAP-B04/B05 que saíram) já explicados em A-0339 (#318), A-0365 (#317), A-0380 (#315), A-0386 (#319) e os do #321
Referência: A-0339; A-0365; A-0380; A-0386
Novos: 26 · mudaram: 12 · saíram: 2

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs :: ArmazemHistoricoTests.OFiltroDoReparador_UsaOMesmoValorQueALinhaMostra_BQ_B06`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs :: ArmazemMovimentosTests.Saida_DepoisDeUmCorrigir_HerdaALinhaDaEntradaAnterior_BQ_B04`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelBoquilhasTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelBoquilhasTests.cs :: BqExcelBoquilhasTests.VersaoAsync_IgualSemNenhumaMudanca`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryDiscrepancyTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryDiscrepancyTests.cs :: BqHistoryDiscrepancyTests.DuasBqsEliminadas_TemSaldosSeparados_BQ_B02`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryDiscrepancyTests.cs :: BqHistoryDiscrepancyTests.MeterAZero_EscondeOSaldoDasLinhasDeAntes_BQ_B03`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmComLetraEZeros_NuncaNumerico_FicaSemContraparte`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmComZerosAEsquerda_EmparelhaComOMesmoNumero`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmDigitosDiferentes_NuncaEmparelham`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs :: PesoComparacaoCalculationTests.Pares_CmIguais_Emparelham`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs :: PesoComparisonQueryTests.FindCandidates_PesoVazioCriadoDepois_NaoEscondeAProducaoComLeituras`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoProducaoAnteriorQueryTests.cs :: PesoProducaoAnteriorQueryTests.AindaValida_DepoisDeMudarAsDatas_DeixaDeSerAnterior_Falso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoProducaoAnteriorQueryTests.cs :: PesoProducaoAnteriorQueryTests.AindaValida_ProducaoAindaAnterior_Verdadeiro`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoProducaoAnteriorQueryTests.cs :: PesoProducaoAnteriorQueryTests.AindaValida_SemProducaoAtual_Falso`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BqExcelVersao.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BqExcelVersao.cs :: PostgresMigrationIntegrationTests.VersaoAsync_MudaQuandoSeEditaOLoteDaBQ_SemNovoMovimento_BQ_B05`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/CreateJobOnHandlerTests.cs :: CreateJobOnHandlerTests.Experiencia_SemData_UsaODiaDeLisboaNaoOUtc_NaMadrugadaDeVerao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: Sessao.OPercentManual_IgualAoValorExatoDaUltimaLeituraSap_NaoGrava_MasDiferenteDoArredondadoGrava`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaBExperiencia.cs :: JobOnPageTests.FolhaNova_Experiencia_SemData_UsaODiaDeLisboaNaoOUtc_NaMadrugadaDeVerao`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: SAP-B04: numa pasta com mais de dez listas por ler na primeira leitura, vão todas, da mais antiga para a mais nova`
- novo: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: SAP-B05: Apagar lidos apaga só os ficheiros confirmados gravados, nunca um recusado só por estar antes do corte`
- novo: `tests/ShellProof/dmo-suggest.test.cjs :: DMO-635: Escape com a lista de sugestões aberta não sobe até ao painel/janela`
- novo: `tests/ShellProof/jobon-folha.test.cjs :: CS furos no Editar: apagar abertos e depois fechados limpa o campo escondido cs_furos`
- novo: `tests/ShellProof/troca-parcial.test.cjs :: DMO-634: uma recusa ao editar no sítio também repõe o espelho da linha`
- novo: `tests/ShellProof/troca-parcial.test.cjs :: DMO-636: um link «Por associar» que também abre uma janela não pede a página nem cria histórico`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/CreateJobOnHandlerTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Production/EliminarJobOnCenario.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs :: Sessao.OPercentManual_GravaUmaLeituraEFechaOLembrete_IgualNaoGrava`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs :: FerramentasPagesTests.Ficha_PercentagemDeUso_NoEditar_GravaComOGuardar_SoAvisos`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaBExperiencia.cs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Apagar lidos: sem nada lido ainda, avisa e não apaga nem pergunta`
- mudou: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Sem «Atualizar do SAP» no cartão (Admin ficou com ele), o script não rebenta e «Apagar lidos» continua a funcionar`
- mudou: `tests/ShellProof/dmo-suggest.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/jobon-folha.test.cjs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/troca-parcial.test.cjs :: (resto do ficheiro)`
- saiu: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Apagar lidos: pergunta antes e apaga só os ficheiros do SAP já lidos`
- saiu: `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs :: Numa pasta com mais de dez listas por ler, vão as dez mais recentes`

Selo depois: `526996f9f225191c`

## A-0400 · 2026-10-29 · ok-do-diogo

Porquê: As BQ procuram-se por nome técnico até NX/PX + * + lote (ABC002NX*64) e o «*» passa a ser aceite nos códigos de procura; os testes que recusavam o «*» usam agora aspas/«*» sozinho, e os códigos esperados das BQ levam o «*» (DMO-658).
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraFHmPWCijtGmxuoRj5jRi4i
Novos: 2 · mudaram: 4 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.ABq_ProcuraSePeloNomeTecnicoAteAoProcesso_EstrelaELote`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.OCsvDoScript_DaABqPeloCodigoComEstrela`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.AsTresGerais_EstaoSempre_EAsGravadasLeemSeDeVolta`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.OsCodigosPedidosAoSap_SeguemATabelaEOCodigoSap_DeTodosOsTipos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.ATabelaDosCodigosDeProcura_GravaNoAdminEMudaOsCodigosPedidos`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs :: DefinicoesPorModuloPageTests.AtualizarPorReferencia_DaOsCodigosDessaReferenciaEDoTipo`

Selo depois: `d750d0d392be5ec3`

## A-0401 · 2026-10-29 · ok-do-diogo

Porquê: DMO-659: ZPPG só na plant MG; BQ procura até ao processo de 2 letras, mais casos (Diogo 29/10, ARTIGOS-MG)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra17ZmcRmgwGkHQK9FSJwe6E
Novos: 1 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: SapScriptsVbsTests.OScriptDaZppg_ProcuraSoNaPlantMg`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Tools/SapProcuraRegrasTests.cs :: SapProcuraRegrasTests.ABq_ProcuraSePeloNomeTecnicoAteAoProcesso_EstrelaELote`

Selo depois: `d6778ba23b53272e`

## A-0395 · 2026-10-29 · contagem-lista

Porquê: AUTH-B06 (DMO-653): adicionado o guard SoVerAsync aos handlers AJAX ProcurarProducao, ToolCandidates e ToolFacts do Job On, que faltava e deixava quem só tem jobon.view aceder ao catálogo de ferramentas e aos fatos técnicos pelo endereço direto; 4 testes novos (JobOnPageTests.AjaxSoVer.cs). PERM-B01 (DMO-654): adicionado [ModuleEntry(DmoCapabilities.Ferramentas)] à página Ferramentas/Utilizacao, confinando a sessão do Posto (Reparação Interna) à RI também nesta página, não só no menu; 1 teste novo (PostoFerramentasUtilizacaoTests.cs).
Novos: 7 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnCriar_HandlersAjax_ContinuamAFuncionar`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnVer_HandlerProcurarProducao_EhRedirecionado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnVer_HandlerToolCandidates_EhRedirecionado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnVer_HandlerToolFacts_EhRedirecionado`
- novo: `tests/DMO.Alpha.Web.Tests/PostoFerramentasUtilizacaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PostoFerramentasUtilizacaoTests.cs :: PostoFerramentasUtilizacaoTests.PeloPosto_FerramentasUtilizacao_EhRecusada_PeloLoginContinuaAAbrir`

Selo depois: `1bf72afdf36220cb`

## A-0402 · 2026-10-29 · teste-novo

Porquê: Lote .241: junta os testes novos do #327 (A-0395) e do #328 (A-0401)
Referência: A-0395; A-0401
Novos: 7 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnCriar_HandlersAjax_ContinuamAFuncionar`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnVer_HandlerProcurarProducao_EhRedirecionado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnVer_HandlerToolCandidates_EhRedirecionado`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.AjaxSoVer.cs :: JobOnPageTests.JobOnVer_HandlerToolFacts_EhRedirecionado`
- novo: `tests/DMO.Alpha.Web.Tests/PostoFerramentasUtilizacaoTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/PostoFerramentasUtilizacaoTests.cs :: PostoFerramentasUtilizacaoTests.PeloPosto_FerramentasUtilizacao_EhRecusada_PeloLoginContinuaAAbrir`

Selo depois: `e2521895af016d8f`

## A-0403 · 2026-10-29 · ok-do-diogo

Porquê: DMO-661: sem senha do SAP os scripts param logo e o Instalar recusa; com o SAP aberto a senha ainda se escreve (Diogo 29/10: «prendeu na pass», PC bloqueado)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraQu3J6rFFkjYFcUdKevP4Ln
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs :: DefinicoesNumSoSitioTests.SemSenhaDoSap_OsScriptsNaoSeDao`
- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: SapScriptsVbsTests.SemSenha_OScriptParaLogo`

Selo depois: `5dd9004e5e11ea1f`

## A-0411 · 2026-10-29 · teste-novo

Porquê: Teste novo: os avisos dos cartões da folha do Job On vão para a pilha dos alertas da app (DMO-666).
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/ShellProof/jobon-folha-avisos-alerta.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/jobon-folha-avisos-alerta.test.cjs :: Ver: avisos do CM e da BQ vão para a pilha dos alertas da app, com o botão e o ×`

Selo depois: `3133427fe0c23198`

## A-0414 · 2026-10-29 · teste-novo

Porquê: Teste novo: diâmetros antigos com dois números separam-se em valor e Tol. e o Guardar passa (DMO-669).
Novos: 4 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnDiametrosSepararTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnDiametrosSepararTests.cs :: JobOnDiametrosSepararTests.DoisNumeros_VaiOSegundoParaATolVazia_EOGuardarPassa`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnDiametrosSepararTests.cs :: JobOnDiametrosSepararTests.TolJaEscrita_FicaComoEsta`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnDiametrosSepararTests.cs :: JobOnDiametrosSepararTests.UmNumero_NaoMuda`

Selo depois: `4308a5058f2f4495`

## A-0404 · 2026-10-29 · contagem-lista

Porquê: AUTH-B03 (DMO-655): o reset de acesso apaga o PIN do Posto; o rollback do provider repõe o PIN apagado.

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AuthB03B04UserAdminServiceTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AuthB03B04UserAdminServiceTests.cs :: AuthB03B04UserAdminServiceTests.ResetUserAccessAsync_ApagaOPinDoPosto`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AuthB03B04UserAdminServiceTests.cs :: AuthB03B04UserAdminServiceTests.ResetUserAccessAsync_OProviderRecusa_OPinDoPostoVoltaAFicar`
- novo: `tests/DMO.Alpha.Web.Tests/AuthB03B04B05Tests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/AuthB03B04B05Tests.cs :: AuthB03B04B05Tests.ComRequiresPasswordChange_AEntradaPeloPostoRecusaMesmoComOPinCerto`
- novo: `tests/DMO.Alpha.Web.Tests/AuthB03B04B05Tests.cs :: AuthB03B04B05Tests.MudarOPin_FechaAsSessoesDoPostoAbertasComOPinAnterior`
- novo: `tests/DMO.Alpha.Web.Tests/AuthB03B04B05Tests.cs :: AuthB03B04B05Tests.ResetDeAcesso_ApagaOPinDoPosto_OPinAntigoJaNaoEntra`

## A-0405 · 2026-10-29 · contagem-lista

Porquê: AUTH-B04 (DMO-656): arquivar e reativar um utilizador sobem o SessionVersion nas duas mudanças, para uma cookie antiga não voltar a valer depois de reativar.

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AuthB03B04UserAdminServiceTests.cs :: AuthB03B04UserAdminServiceTests.SetUserStandByAsync_ArquivarEReativar_SobeAVersaoNasDuasVezes`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AuthB03B04UserAdminServiceTests.cs :: AuthB03B04UserAdminServiceTests.SetUserStandByAsync_Arquivar_SobeAVersaoDaSessao`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Admin/AuthB03B04UserAdminServiceTests.cs :: AuthB03B04UserAdminServiceTests.SetUserStandByAsync_SemMudarOEstado_NaoSobeAVersao`
- novo: `tests/DMO.Alpha.Web.Tests/AuthB03B04B05Tests.cs :: AuthB03B04B05Tests.ArquivarEReativar_ACookieAntigaContinuaRecusadaDepoisDeReativar`

## A-0406 · 2026-10-29 · contagem-lista

Porquê: AUTH-B05 (DMO-657): cookie próprio do Posto sem deslizar; o endpoint /posto/ativo (só a partir de uma ação real da pessoa) renova o limite, outros pedidos não renovam.
Novos: 3 · mudaram: 1 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_ChamarAtivo_RenovaOLimitePorMaisNMinutos`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_DentroDoLimite_OsPedidosContinuam`
- novo: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_PedidoQueNaoPassaPorAtivo_NaoRenova`
- mudou: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: (resto do ficheiro)`
- saiu: `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs :: PostoSoRiTests.PeloPosto_EmUso_ASessaoContinua`

Selo depois: `1868af282879fe3a`

## A-0409 · 2026-10-29 · ok-do-diogo

Porquê: DMO-664: sem ferramentas à espera o uso do SAP acaba sem erro (Diogo 29/10, «A leitura do SAP falhou»)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraHYrksKjJRFizcpFcHB7tKz
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: SapScriptsVbsTests.SemCodigosAEspera_OUsoAcabaSemErro`

Selo depois: `4a55af040cab0651`

## A-0410 · 2026-10-29 · ok-do-diogo

Porquê: DMO-665: a janela Atualizar do SAP mostra só a parte escolhida (Diogo 29/10)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraCdENbGEZaUsyR5Q2wb938B
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.AJanelaDoSap_EscondeAsPartesQueNaoEstaoEscolhidas`

Selo depois: `b07ddb963368e9dd`

## A-0412 · 2026-10-29 · teste-novo

Porquê: DMO-667/A-0412 (Diogo 29/10): no Registo do Armazém, «Voltar» por cima do título e a Fábrica do Empréstimo sempre em lista (Definições). 1 teste novo; nenhum antigo mudou.
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Registo_VoltarFicaPorCimaDoTitulo_EAFabricaDoEmprestimoEUmaLista`

Selo depois: `55fad2475f50042a`

## A-0416 · 2026-10-29 · teste-novo

Porquê: Teste novo: o painel das linhas da Folha só mostra o que entra nos próximos 5 dias (DMO-671).
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelCincoDias.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelCincoDias.cs :: JobOnPageTests.Folha_PainelDasLinhas_SoOQueEntraNosProximosCincoDias`

Selo depois: `cc3bd3e5d96c86c2`

## A-0417 · 2026-10-29 · teste-novo

Porquê: Peso volta a deslizar; Enter não volta ao topo; + − dentro da caixa (DMO-672)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra4341FR3J1Tyu38gwWqzNfJ
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs :: PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_DiferencasFicamForaDaTroca_RegistarNaoVoltaAoTopo`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs :: JobOnFichaTests.Peso_ComparacaoTemOMaisEOMenosDentroDaCaixa`

Selo depois: `7bf7f2456fd5e9bc`

## A-0418 · 2026-10-29 · teste-novo

Porquê: Painel do Peso sem aviso falso de alterações por guardar (DMO-672)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgra9W3LRiSMF46vR7EVbf8Uq7
Novos: 2 · mudaram: 0 · saíram: 0

- novo: `tests/ShellProof/painel-gravou-sem-recarregar.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/painel-gravou-sem-recarregar.test.cjs :: Painel de apoio: depois de gravar sem recarregar, sem campos alterados, fecha sem aviso`

Selo depois: `daf9bbebd5494b9b`

## A-0419 · 2026-10-29 · ok-do-diogo

Porquê: Tabela do Controlo anterior lado a lado por grandeza com o anterior a cinza (DMO-672)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraM8r59Af85tn7f3zTFfP4DF
Novos: 1 · mudaram: 3 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBAgrupamento.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_TabelaDaComparacao_AgrupaPorGrandeza_AtualAnteriorEDiferenca_ComAnteriorACinza`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.UltimasProducoes.cs :: PesoComparacaoPageTests.Folha_AProducaoACompararTemAsUltimas5Anteriores_EAPedidaFica`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoSoParaComparacaoPageTests.cs :: PesoSoParaComparacaoPageTests.CriarDaComparacao_LigaOPesoAoAtual_MostraAComparacao_ENuncaPedeJobOn`
- saiu: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBAgrupamento.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_TabelaDaComparacao_AgrupaEsteControlo_ControloAnterior_EDiferenca_PorEstaOrdem`

Selo depois: `3d2817c5fc411e69`

## A-0420 · 2026-10-29 · ok-do-diogo

Porquê: Lote .243: junta #330, #331, #334, #335, #338; as entradas A-0404..A-0419 explicam os testes (PeloPosto_EmUso_ASessaoContinua saiu em A-0406; o Peso foi trocado em A-0417..A-0419).
Referência: lote .243
Novos: 3 · mudaram: 3 · saíram: 1

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBAgrupamento.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_TabelaDaComparacao_AgrupaPorGrandeza_AtualAnteriorEDiferenca_ComAnteriorACinza`
- novo: `tests/ShellProof/painel-gravou-sem-recarregar.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/painel-gravou-sem-recarregar.test.cjs :: Painel de apoio: depois de gravar sem recarregar, sem campos alterados, fecha sem aviso`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.UltimasProducoes.cs :: PesoComparacaoPageTests.Folha_AProducaoACompararTemAsUltimas5Anteriores_EAPedidaFica`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoSoParaComparacaoPageTests.cs :: PesoSoParaComparacaoPageTests.CriarDaComparacao_LigaOPesoAoAtual_MostraAComparacao_ENuncaPedeJobOn`
- saiu: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBAgrupamento.cs :: PesoFolhaProducaoAnteriorPageTests.Folha_TabelaDaComparacao_AgrupaEsteControlo_ControloAnterior_EDiferenca_PorEstaOrdem`

Selo depois: `188b67503f0e65e6`

## A-0415 · 2026-10-29 · ok-do-diogo

Porquê: DMO-668: Utilização por produção com Voltar e sem quadros verdes, Opções sem Código SAP, menu do Histórico com Job On/Controlo/Reparação/Boquilhas; DMO-670: B2 numa fila com dropdown Dupla/Tripla
Referência: Diogo cmsg_01K75FTs9bQnbhhH7qttpgraGcthVpKtU8Ve3YXaFzFA1E, cmsg_01K75FTs9bQnbhhH7qttpgraDA13z7kmRhF33cV1uyAvMN, cmsg_01K75FTs9bQnbhhH7qttpgra9Cd53tmPP3qFHv2eV5p2vE, cmsg_01K75FTs9bQnbhhH7qttpgra2qWamwB946ubLpHqDngTxe, sim cmsg_01K75FTs9bQnbhhH7qttpgraABfYgJtLFVf7Q5F9METyuZ
Novos: 2 · mudaram: 6 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoVoltar.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoVoltar.cs :: DefinicoesPorModuloPageTests.AUtilizacaoPorProducao_TemOVoltarPorCimaESemQuadrosVerdes`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.DeltaPeloProduzido.cs :: DefinicoesPorModuloPageTests.SemLeiturasAVoltaDaProducao_ODeltaEstimaSePeloProduzido`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs :: DefinicoesPorModuloPageTests.ODetalhesDaUtilizacao_MostraODeltaDeCadaProducaoEOTotal`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs :: DefinicoesPorModuloPageTests.OCodigoSapDaFicha_GanhaATabela_EVazioVoltaAEla`
- mudou: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs :: DefinicoesPorModuloPageTests.FerramentasDefinicoes_Codificacao_SeletorDaB2`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.Ficha_TemOBotaoHistorico_QueAbreOsJobOnDaFerramenta`
- mudou: `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs :: FerramentasPagesTests.Historico_ComControloERi_OMenuTemOsTresDestinos`

Selo depois: `ec9ba3ad5cec76c3`

## A-0421 · 2026-10-29 · teste-novo

Porquê: DMO-667/A-0412 (parte 2, Diogo 29/10): na Saída de uma BQ há a opção dos reparadores das BQ e, a seguir, os do Armazém. 1 teste novo; nenhum antigo mudou.
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Saida_DeBq_ParaReparacao_OferecePorGruposOsReparadoresDasBqEDoArmazem`

Selo depois: `b7ee30734ac3ae0f`

## A-0422 · 2026-10-29 · ok-do-diogo

Porquê: Moda no Resumo mostra dias e não diferença (DMO-673)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraJPtT8ohDC782LcHjvY4Xhg
Novos: 0 · mudaram: 1 · saíram: 0

- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBResumo.cs :: PesoFolhaProducaoAnteriorPageTests.Resumo_TabelaDoPeso_TemAsColunasValorGEPercentagem_EACadaLinhaAsDuasDiferencas`

Selo depois: `d29f1065cfbc8c48`

## A-0423 · 2026-10-29 · ok-do-diogo

Porquê: Lote .244: junta #336, #340, #341 e a lista de testes em dia; as entradas A-0415, A-0421 e A-0422 explicam os testes.
Referência: lote .244
Novos: 1 · mudaram: 1 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs :: ArmazemMovimentosPaginaTests.Saida_DeBq_ParaReparacao_OferecePorGruposOsReparadoresDasBqEDoArmazem`
- mudou: `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBResumo.cs :: PesoFolhaProducaoAnteriorPageTests.Resumo_TabelaDoPeso_TemAsColunasValorGEPercentagem_EACadaLinhaAsDuasDiferencas`

Selo depois: `ffd1b58d9d7cece0`

## A-0424 · 2026-10-29 · contagem-lista

Porquê: DMO-674: teste novo da referência completa e do espaço entre botões no Registar Reparação. Mudanças a ficheiros antigos só de lista: a classe ReparacaoProgramadaPaginaTests passa a partial (o teste novo vive em ReparacaoProgramadaPaginaTests.Artigo.cs) e o ci-local.sh ganha o passo do rp_botoes_browser.py. Nenhum teste existente muda o que verifica.
Referência: DMO-674
Novos: 3 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.Artigo.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.Artigo.cs :: ReparacaoProgramadaPaginaTests.ListaEProcura_MostramAReferenciaCompletaAFrenteDaFerramenta`
- novo: `tests/visual/rp_botoes_browser.py`
- mudou: `scripts/ci/ci-local.sh`
- mudou: `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs :: (resto do ficheiro)`

Selo depois: `3e7437e38d1637ff`

## A-0425 · 2026-10-29 · teste-novo

Porquê: Teste novo: os dados técnicos do Peso (volumes, densidade, TP/calote) ficam num dobrável fechado por omissão
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraEivdTWrHp8gVA2VQQjDEY5
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_DadosTecnicos_FicamNumDobravelFechadoPorOmissao`

Selo depois: `bffe122afda559ff`

## A-0408 · 2026-10-29 · contagem-lista

Porquê: DMO-663 / A-0408: testes novos ZPPG peças/Supplier/Purchase e fornecedor inválido; avisos Sim/Não por tool_id; uso manual, concorrência PostgreSQL e ×. Lista JS ganha o ficheiro novo; a lista exata de migrações ganha só 20261029104943_AvisosSapPecasFornecedorData, autorizado pelo proprietário. Nenhuma verificação antiga mudou. ID 0408 reservado pelo proprietário.
Referência: Pedido DMO-663/A-0408 nesta conversa; proprietário autorizou acrescentar só a migração à lista de MigrationFoundationTests.
Novos: 17 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_AMigracaoSoAcrescentaColunasOpcionais`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_DuasDecisoesDaMesmaFicha_SoUmaGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_EditarOutrosFactosDaCmNaoApagaAQuantidadeAceite`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_NaoMantemQuantidadeEUsoManual_NaoRepeteMesmoValorEMostraNovoValor`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_SimGravaNaQuantidadeDoTipoCertoESoNoToolIdEscolhido`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_SimUsaOPercentDoServidorEDepoisVoltaAAplicarAutomaticamente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_UmValorManualIgualAoSapContinuaProtegidoQuandoOSapMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_UmaDecisaoAntigaNaoSubstituiUmaImportacaoMaisRecente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Zppg_LeAPrimeiraQuantidadeOFornecedorEADataPurchase`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: DefinicoesPorModuloPageTests.Sap_AFichaMostraFornecedorECompraNaInformacaoOpcional_EOSimGravaAPeca`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: DefinicoesPorModuloPageTests.Sap_ONaoMantemAQuantidadeENaoVoltaAMostrarOMesmoAviso`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: DefinicoesPorModuloPageTests.Sap_UmaDecisaoSemAntiforgeryNaoGrava`
- novo: `tests/ShellProof/ferramentas-avisos-sap.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/ferramentas-avisos-sap.test.cjs :: O × fecha só o aviso escolhido, sem decidir «Não» nem enviar o formulário`
- novo: `tests/ShellProof/ferramentas-avisos-sap.test.cjs :: Os avisos carregados depois de uma troca parcial também se podem fechar`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/ShellProof/package.json`

Selo depois: `954605f2186cb165`

## A-0426 · 2026-10-29 · ok-do-diogo

Porquê: Lote .245: junta #339, #343 e #344; as entradas A-0408, A-0424 e A-0425 explicam os testes.
Referência: lote .245
Novos: 18 · mudaram: 2 · saíram: 0

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_AMigracaoSoAcrescentaColunasOpcionais`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_DuasDecisoesDaMesmaFicha_SoUmaGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_EditarOutrosFactosDaCmNaoApagaAQuantidadeAceite`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_NaoMantemQuantidadeEUsoManual_NaoRepeteMesmoValorEMostraNovoValor`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_SimGravaNaQuantidadeDoTipoCertoESoNoToolIdEscolhido`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_SimUsaOPercentDoServidorEDepoisVoltaAAplicarAutomaticamente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_UmValorManualIgualAoSapContinuaProtegidoQuandoOSapMuda`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Sap_UmaDecisaoAntigaNaoSubstituiUmaImportacaoMaisRecente`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs :: PostgresMigrationIntegrationTests.Zppg_LeAPrimeiraQuantidadeOFornecedorEADataPurchase`
- novo: `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs :: PesoCapSupportPageTests.ProductionMode_DadosTecnicos_FicamNumDobravelFechadoPorOmissao`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: DefinicoesPorModuloPageTests.Sap_AFichaMostraFornecedorECompraNaInformacaoOpcional_EOSimGravaAPeca`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: DefinicoesPorModuloPageTests.Sap_ONaoMantemAQuantidadeENaoVoltaAMostrarOMesmoAviso`
- novo: `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs :: DefinicoesPorModuloPageTests.Sap_UmaDecisaoSemAntiforgeryNaoGrava`
- novo: `tests/ShellProof/ferramentas-avisos-sap.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/ferramentas-avisos-sap.test.cjs :: O × fecha só o aviso escolhido, sem decidir «Não» nem enviar o formulário`
- novo: `tests/ShellProof/ferramentas-avisos-sap.test.cjs :: Os avisos carregados depois de uma troca parcial também se podem fechar`
- mudou: `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs :: MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt`
- mudou: `tests/ShellProof/package.json`

Selo depois: `58354ba1d7582bbb`

## A-0428 · 2026-10-29 · ok-do-diogo

Porquê: DMO-677: sem caixa da senha o script não escreve pelo teclado na janela do SAP em uso (Diogo 29/10, «Função impossível»)
Referência: cmsg_01K75FTs9bQnbhhH7qttpgraYbZnyAJ6oEvMe6guNRA41v
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: SapScriptsVbsTests.SemCaixaDaSenha_NaoEscrevePeloTeclado`

Selo depois: `3923e2ad82832cb3`

## A-0429 · 2026-10-29 · teste-novo

Porquê: Lote .245: junta o #346 (A-0428); o teste novo SemCaixaDaSenha_NaoEscrevePeloTeclado está explicado em A-0428.
Referência: lote .245
Novos: 1 · mudaram: 0 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/SapScriptsVbsTests.cs :: SapScriptsVbsTests.SemCaixaDaSenha_NaoEscrevePeloTeclado`

Selo depois: `a6aba0b16d9e9794`

## A-0430 · 2026-10-29 · ok-do-diogo

Porquê: Após merge da integracao beta.245: referência restringe opções de linhas/lotes/reparadores e restantes campos; limpar escolhas incompatíveis, preservar produção histórica e rejeitar linha incompatível sem gravar. Novos testes: 9 JavaScript de transições, resposta tardia, erro e Enter; 3 casos de gravação; 1 PostgreSQL de opções multisscope, referência composta, ficha atual e histórico; 2 páginas/permissões. Existentes: sem associações BQ deixa de propor todas as linhas; cenários válidos ganham associação explícita e os resets em memória limpam associações órfãs; opção BQ verifica seleção e texto descodificado sem ordem de atributos. Preservadas as alterações e testes novos da integracao.
Referência: Pedido do Diogo nesta conversa em 29/10/2026: aplicar alterações e adaptar testes para englobar a alteração.
Novos: 20 · mudaram: 7 · saíram: 1

- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/ReferenciaCompatibilidadeTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/ReferenciaCompatibilidadeTests.cs :: ReferenciaCompatibilidadeTests.CriarJobOnComFerramentaAssociadaAOutraLinhaNaoGrava`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/ReferenciaCompatibilidadeTests.cs :: ReferenciaCompatibilidadeTests.UmaLinhaForaDaFichaNaoGravaMovimento_SalvoAProducaoHistorica`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReferenceOptions.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReferenceOptions.cs :: PostgresMigrationIntegrationTests.OpcoesDaReferencia_SeparamCompatibilidadeAtualDaLinhaHistorica_ERecusamOutraLinha`
- novo: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2SemJobOn.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_UmaBqSemLinhasNaFicha_NaoPermiteEscolherLinhas`
- novo: `tests/DMO.Alpha.Web.Tests/ReferenceFilterPagesTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/ReferenceFilterPagesTests.cs :: ReferenceFilterPagesTests.AsOpcoesNaoRevelamDadosDeModulosSemPermissao`
- novo: `tests/DMO.Alpha.Web.Tests/ReferenceFilterPagesTests.cs :: ReferenceFilterPagesTests.NoHistoricoAReferenciaRestringeAsLinhasNaPaginaENasOpcoes`
- novo: `tests/ShellProof/filtros-referencia.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/filtros-referencia.test.cjs :: A resposta atrasada da referência anterior não substitui as opções da referência atual`
- novo: `tests/ShellProof/filtros-referencia.test.cjs :: Enter no lote envia os filtros mesmo sem botão no formulário`
- novo: `tests/ShellProof/filtros-referencia.test.cjs :: Falha de leitura bloqueia escolhas antigas e avisa; uma nova pesquisa recupera`
- novo: `tests/ShellProof/filtros-referencia.test.cjs :: Uma consulta sem resultados não oferece linhas de outras referências`
- novo: `tests/ShellProof/filtros-referencia.test.cjs :: Uma escolha ainda válida conserva-se e apagar a referência repõe as opções gerais`
- novo: `tests/ShellProof/filtros-referencia.test.cjs :: Uma referência restringe linhas e reparadores e elimina a linha anterior incompatível`
- novo: `tests/ShellProof/referencia-dependencias.test.cjs :: (resto do ficheiro)`
- novo: `tests/ShellProof/referencia-dependencias.test.cjs :: Escrever outra referência limpa a produção e as linhas da BQ anterior`
- novo: `tests/ShellProof/referencia-dependencias.test.cjs :: Retirar o Job On anterior repõe apenas as linhas da ficha da BQ`
- novo: `tests/ShellProof/referencia-dependencias.test.cjs :: Uma ferramenta sem linhas configuradas não propõe linhas compatíveis`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.FormularioDaPagina.cs :: BoquilhasRegistoPageTests.MovimentoPeloFormularioDaPagina_GravaAQuantidadeEAMaquinaNaBQ`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs :: BoquilhasRegistoPageTests.RegisterMovement_ChosenMachineIsSavedAndShownInTheRegister`
- mudou: `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_NaPaginaBoquilhas_GravaEMostraOMovimento`
- mudou: `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs :: (resto do ficheiro)`
- mudou: `tests/ShellProof/cobertura_a2_bq-sem-jobon.test.cjs :: Escolher a BQ deixa na Linha só as da ficha dela, com a primeira já escolhida`
- mudou: `tests/ShellProof/package.json`
- saiu: `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2SemJobOn.cs :: BoquilhasRegistoPageTests.RegistarSemJobOn_UmaBqSemLinhasNaFicha_MostraTodasAsLinhasSemNenhumaEscolhida`

Selo depois: `966e410cbc21147d`

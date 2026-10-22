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

Porquê: Armazém: tabelas sem a referência completa, com Linha e % uso; avisos do Armazém no Job On em espera (interruptor)
Referência: cmsg_01A27e9ozcpoh4jdPRsuJbFyXfNU7jcpKvE6aoyJFejJsb, cmsg_01A27e9ozcpoh4jdPRsuJbFy7VQFHmWxH8LLRGu2LgzbUm, cmsg_01A27e9ozcpoh4jdPRsuJbFy3ywAgFqJ4UPrRkxVw9H1sN, cmsg_01A27e9ozcpoh4jdPRsuJbFyKSqvNb77drHCVLbJLjF6FN
Novos: 4 · mudaram: 3 · saíram: 0

- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: (resto do ficheiro)`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.ComOInterruptor_OAvisoDoArmazemVolta`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.PorOmissao_AAplicacaoNaoOLigaNasDefinicoes`
- novo: `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs :: JobOnAvisoArmazemEmEsperaTests.SemOInterruptor_OAvisoDoArmazemNaoAparece_EOsOutrosSim`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Fora_SoCmEMf_ComReparadorEDia_BqNao`
- mudou: `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs :: ArmazemUltimosPaginaTests.Ultimas_Todas_DoMaisRecente_BqIncluida_PosicaoSoNaEntrada`
- mudou: `tests/DMO.Alpha.Web.Tests/CustomWebApplicationFactory.cs :: (resto do ficheiro)`

Selo depois: `2337fe3f6c5417f8`

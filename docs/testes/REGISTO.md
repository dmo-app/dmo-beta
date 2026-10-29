# Registo dos testes: o que cada teste garante

Revisão de 28/10/2026 sobre a `integracao` 0.3.0-beta.230 (abbbae36), com as correções de DMO-580, pedida pelo Diogo
(«ver os testes se estão todos bem e testam o que realmente se quer»). Comparado com as decisões
do Diogo guardadas na memória do projeto, o código atual e o histórico; **não** com o blueprint,
que está desatualizado (Diogo, 28/10 17:22).

Cada linha: ficheiro, teste, a funcionalidade que garante (frase do negócio) e o estado:

- **OK** — testa uma regra útil e falha se a regra se estragar.
- **FRACO** — passaria com a regra estragada (só abre a página, só «não aparece», só classes/estilos, recusa sem reler a base, garantia de base testada só em memória).
- **ERRADO** — testa o contrário de uma decisão do Diogo.
- **DUPLICADO** — repete outro teste.
- **OBSOLETO** — testa algo que a app já não usa.

Os problemas e as regras sem teste estão em [PROBLEMAS.md](PROBLEMAS.md). Como correr: `scripts/ci/testes.md`
(índice por grupos) e, para os agentes de fora, o repositório dmo-app/dmo-ci.
Testes de uma `Theory` e testes iguais podem estar agrupados numa só linha.

**Teste novo:** acrescentar a linha dele aqui, na secção da área, no mesmo PR (além do `guarda-testes.py registar --porque`).

| Estado | Testes |
|---|---|
| OK | 2365 |
| FRACO | 241 |
| ERRADO | 0 |
| DUPLICADO | 41 |
| OBSOLETO | 0 |

## Controlo (Peso, Pegamentos, Punções, Resumo, Aprovar)

711 linhas: OK 597, FRACO 92, DUPLICADO 22

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AprovacaoNosPdfsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AprovacaoNosPdfsTests.PegamentosPdf_SemSeloDeAprovacao | PDF dos Pegamentos não leva selo de aprovação | FRACO | só DoesNotContain, sem confirmar que o texto do PDF tem conteúdo (ex. título); um PDF vazio passava |
| AprovacaoNosPdfsTests.PesoPdf_OsTresEstados | PDF do Peso mostra Aprovado, Não aprovado e Por aprovar | OK |  |
| AprovacaoNosPdfsTests.ResumoPdfCompleto_AFolhaDoPesoMostraADecisaoDoPeso_EADosPegamentosNenhuma | Só o Peso leva selo de aprovação no PDF completo | OK |  |
| AprovacaoNosPdfsTests.ResumoPdf_Aprovado_MostraONomeDeQuemAprovou | PDF do Resumo mostra nome de quem aprovou e quando | OK |  |
| AprovacaoNosPdfsTests.ResumoPdf_NaoAprovado_MostraQuemQuandoEANota | PDF do Resumo mostra Não aprovado, quem, quando e a nota | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AssociatePesoToControloHandlerTests.Associate_AfterCmSwap_PesoOfTheEarlierCm_IsAcceptedOncePerCombination | Após trocar a CM, Peso da CM anterior associa uma vez por combinação | OK |  |
| AssociatePesoToControloHandlerTests.Associate_OtherLotOfSameReference_IsRefusedAsCmMismatch | Peso de outro lote da CM é recusado e nada fica gravado | OK |  |
| AssociatePesoToControloHandlerTests.Associate_PesoOfAnotherBq_IsRefusedAsBqMismatch | Peso de outra BQ é recusado | OK |  |
| AssociatePesoToControloHandlerTests.Associate_PesoOfAnotherLotOfTheSameBq_IsAcceptedAndTakesTheProductionLot | Peso de outro lote da mesma BQ associa e fica com o lote da produção | OK | Decisão Diogo 27/10 (peso-arrumado-pr212) |
| AssociatePesoToControloHandlerTests.Associate_PesoOfAnotherProduction_IsRefused | Peso já associado a outra produção é recusado | OK |  |
| AssociatePesoToControloHandlerTests.Associate_ProductionThatAlreadyHasPeso_IsRefused | Produção com Peso da mesma combinação recusa outro | OK |  |
| AssociatePesoToControloHandlerTests.Associate_ProductionWithoutBq_IsRefusedAsContextMissingBq | Produção sem BQ recusa associar Peso | OK |  |
| AssociatePesoToControloHandlerTests.Associate_SameCmTool_KeepsSamePesoAndLinksProduction | Associar Peso solto à produção mantém o Peso e o lote da CM | OK |  |
| AssociatePesoToControloHandlerTests.Associate_Twice_IsIdempotent | Associar duas vezes o mesmo Peso não repete nada | FRACO | idempotência só provada em memória; sem índice único/trinco do PostgreSQL não prova duas pessoas ao mesmo tempo |
| AssociatePesoToControloHandlerTests.Associate_UnknownIdsAndMissingCm_AreTypedRefusals | Ids desconhecidos ou produção sem CM são recusados | FRACO | só verifica o resultado devolvido, não que nada foi gravado |
| AssociatePesoToControloHandlerTests.Candidates_AfterSwapWithoutCurrentPeso_AreLoosePesosOfTheToolsTheProductionHadOrHas | Após troca, candidatos são Pesos das ferramentas que a produção teve | OK |  |
| AssociatePesoToControloHandlerTests.Candidates_ForControlo_AreOnlyUnassociatedPesosOfTheJobOnCm | Candidatos: só Pesos soltos da CM da produção | OK |  |
| AssociatePesoToControloHandlerTests.Productions_ForPeso_AreJobOnsWithThatCmAndWithoutPeso | Produções candidatas: com a CM do Peso e sem Peso | OK |  |
| AssociatePesoToControloHandlerTests.Productions_ForPeso_IncludeJobOnsThatHadTheCmBeforeASwap | Produções que tiveram a CM antes da troca também servem | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AutorPorUtilizadorTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AutorPorUtilizadorTests.FactoAlterado_OUtilizadorSegueOTexto | Ao mudar o facto, o utilizador acompanha o texto | OK |  |
| AutorPorUtilizadorTests.OAdmin_FicaSoComOTexto | Autor ADMIN fica só como texto | OK |  |
| AutorPorUtilizadorTests.PesoNovo_GuardaOUtilizadorAoLadoDoNome | Peso novo guarda o autor pelo utilizador | OK |  |
| AutorPorUtilizadorTests.SemSessao_OTextoFicaEOIdentificadorNao | Sem sessão fica o texto e nenhum id | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAprovacaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloAprovacaoTests.NaoAprovarComNota_GravaADecisaoDoPeso_EANota | Não aprovar com nota grava decisão do Peso, autor e nota | OK |  |
| ControloAprovacaoTests.NaoAprovarSemNota_NaoGravaNada_OControloFicaPorAprovar | Não aprovar sem nota é recusado e nada fica gravado | OK |  |
| ControloAprovacaoTests.NaoAprovarUmaParteComNota_GravaADecisaoDessaParte_EANota | Não aprovar uma parte com nota grava a decisão dessa parte | DUPLICADO | igual a NaoAprovarComNota_GravaADecisaoDoPeso_EANota (Theory só com Peso) |
| ControloAprovacaoTests.NaoAprovarUmaParteSemNota_NaoGravaNada_OControloFicaPorAprovar | Não aprovar uma parte sem nota recusa e nada grava | DUPLICADO | igual a NaoAprovarSemNota_NaoGravaNada_OControloFicaPorAprovar (só muda o método) |
| ControloAprovacaoTests.PegamentosRegistadosDepoisDeAprovarOPeso_ContinuaAprovado | Pegamentos novos depois de aprovar não tiram a aprovação | OK |  |
| ControloAprovacaoTests.SemDecisoesPorParte_ValeOResumoGravado | Sem decisões por parte vale o selo gravado no Resumo | OK |  |
| ControloAprovacaoTests.UmPesoNovoDepoisDeAprovar_FicaPorAprovar | Peso novo depois de aprovar volta a Por aprovar | OK |  |
| ControloAprovacaoTests.UmaDecisaoAntigaDosPegamentos_NaoConta_OPesoDecide | Só o Peso decide a aprovação; decisão antiga de Pegamentos não conta | OK |  |
| ControloAprovacaoTests.UmaFolhaNovaDepoisDeAprovarOsPegamentos_ContinuaAprovado | Folha de Pegamentos nova não tira a aprovação | DUPLICADO | igual a PegamentosRegistadosDepoisDeAprovarOPeso_ContinuaAprovado (mesma regra) |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAreaStateHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloAreaStateHandlerTests.Get_WithoutRecords_ListsTheFiveAreasAsNotRegistered | Sem registos, as cinco áreas CM BQ MF PU CS aparecem por avaliar | OK |  |
| ControloAreaStateHandlerTests.Set_CommentOnly_IsKept_AndEmptyStateRemovesTheLine | Só comentário fica; sem estado nem comentário apaga a linha | OK |  |
| ControloAreaStateHandlerTests.Set_OnlyOneField_KeepsTheOtherAsSaved | Cada formulário só muda o seu campo (estado ou comentário) | OK |  |
| ControloAreaStateHandlerTests.Set_OutsideTheVocabulary_WritesNothing | Área ou estado fora do vocabulário ou controlo inexistente: nada gravado | OK |  |
| ControloAreaStateHandlerTests.Set_SavesStatusAndComment_AndSecondSaveReplacesTheSameLine | Gravar estado de área substitui a linha, última gravação vence | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAreaStateHistoricoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloAreaStateHistoricoTests.CadaMudancaDoOkNok_FicaNoHistorico_EAUltimaVale | Cada mudança de OK/NOK fica no histórico, a última vale | OK |  |
| ControloAreaStateHistoricoTests.UmOkGravadoAntesDoHistorico_EntraPrimeiroAoMudar | OK antigo sem histórico entra primeiro ao mudar | OK |  |
| ControloAreaStateHistoricoTests.VoltarATraco_FicaNoHistorico | Voltar a traço (por avaliar) fica no histórico | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloDocumentPathsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloDocumentPathsTests.FileNames_AreCanonical_PesoPegamentosResumo | Nomes canónicos dos PDFs Peso, Pegamentos e Resumo | OK |  |
| ControloDocumentPathsTests.PegamentosFileName_SecondSheetOnwards_EndsWithItsNumber | Pegamentos a partir da 2.ª folha levam o número no nome | OK |  |
| ControloDocumentPathsTests.PesoFileName_SecondPesoOnwards_EndsWithItsNumber | Peso a partir do 2.º leva o número no nome | OK |  |
| ControloDocumentPathsTests.ProductionRelativeFolder_IsReferenceThenProduction_SafeForWindowsShares | Pasta da produção: referência e depois produção, segura no Windows | OK |  |
| ControloDocumentPathsTests.Segments_WithPathSeparators_NeverBecomeStructure | Separadores de caminho nunca viram pastas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistoricoProcuraTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloHistoricoProcuraTests.OTipoEADataSaoLidosDoPedido | Tipo e data do Histórico lidos do pedido | OK |  |
| ControloHistoricoProcuraTests.OValorDoDropdown_EscolheONumeroDaquelaReferencia | Dropdown da produção escolhe o nº daquela referência | OK |  |
| ControloHistoricoProcuraTests.SemJobOn_NaoHaDropdownDasProducoes | Sem Job On não há dropdown de produções | OK |  |
| ControloHistoricoProcuraTests.SemNenhumFiltro_NaoProcura | Sem filtros o Histórico não procura | OK |  |
| ControloHistoricoProcuraTests.SemResultadosPelaReferencia_ProcuraAReferenciaComoFerramenta | Sem resultados, procura a referência como ferramenta | OK |  |
| ControloHistoricoProcuraTests.UmLinkAntigoSoComONumero_EscolheAMaisRecenteComEsseNumero | Link antigo só com nº escolhe a produção mais recente | OK |  |
| ControloHistoricoProcuraTests.UmaProducaoForaDoDropdown_FiltraEEntraNoDropdown | Produção fora do dropdown filtra e entra na lista | OK |  |
| ControloHistoricoProcuraTests.UmaProducaoQueJaNaoEDaReferencia_NaoFiltra | Produção que não é da referência não filtra | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistorySearchQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloHistorySearchQueryTests.Pegamentos_DuasFolhasNaMesmaProducao_CadaLinhaDizAsFerramentasDaSua | Duas folhas de Pegamentos: cada linha diz as suas ferramentas | OK |  |
| ControloHistorySearchQueryTests.Pegamentos_DuasFolhasQueSoDiferemNoLoteDaBq_CadaLinhaDizOLoteDaSua | Folhas que só diferem no lote da BQ dizem o seu lote | OK |  |
| ControloHistorySearchQueryTests.Peso_DoisPesosNaMesmaProducao_CadaLinhaDizABqDoSeu | Dois Pesos na produção: cada linha diz a sua BQ | OK |  |
| ControloHistorySearchQueryTests.ProducaoDaReferenciaAsync_AMaisRecenteComONumero_PelaRegraDaTabela | Produção da referência: a mais recente com esse nº | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_FiltroData_EncontraOResumoAprovadoNesseDia | Resumo aprovado aparece no dia da aprovação | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_FiltroData_PesoPeloDiaDeLisboa_EOsSemCarimboNaoEntram | Filtro de data usa dia de Lisboa; Pesos sem carimbo não entram | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_FiltroData_PesosEPegamentosPelaDataDoRegisto | Filtro por data usa a data do registo | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_FiltroData_UsaODiaDeLisboa | Filtro de data usa o dia de Lisboa | DUPLICADO | mesma regra do dia de Lisboa de SearchAsync_FiltroData_PesoPeloDiaDeLisboa_EOsSemCarimboNaoEntram (aqui para Comparação) |
| ControloHistorySearchQueryTests.SearchAsync_FiltroFerramenta_PelaReferenciaDaCM | Filtro de ferramenta pela referência da CM | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_FiltroFerramenta_PeloInicio | Filtro de ferramenta pelo início da referência | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_FiltroReferenciaEProducao | Filtro por referência e produção | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_FiltroTipo_SoOPedido | Filtro por tipo devolve só esse tipo | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_Ordenacao_MaisRecentePrimeiro | Histórico ordenado com o mais recente primeiro | OK | Decisão: listas com o último registo em cima |
| ControloHistorySearchQueryTests.SearchAsync_ProducaoComAReferenciaDela_NaoJuntaOMesmoNumeroDeOutraReferencia | Mesmo nº de produção de outra referência não se mistura | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_SemFiltros_DevolveOsQuatroTipos | Histórico sem filtros devolve Resumo, Peso, Pegamentos e Comparação | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_SemJobOn_AReferenciaEADataFiltram | Sem Job On: referência e data filtram | OK |  |
| ControloHistorySearchQueryTests.SearchAsync_SemJobOn_SoOQueFicouPorAssociar | Sem Job On: só Pesos e Pegamentos por associar | OK |  |
| ControloHistorySearchQueryTests.SearchPageAsync_AsPaginasSaoAListaCompletaCortadaEmBlocosDe25 | Histórico pagina em blocos de 25 | OK |  |
| ControloHistorySearchQueryTests.SearchPageAsync_ComDatas_AsPaginasSaoAListaCompletaPelaOrdemDasDatas | Páginas do Histórico respeitam a ordem das datas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloResumoSchemaTests.ControloEntity_ExactlyIdentityAndRelation_NoFactColumns | Controlo guarda só identidade e selo de aprovação | FRACO | verifica nomes de colunas do modelo, não comportamento |
| ControloResumoSchemaTests.ControloEntity_JobOnDoesNotGainControloNavigationOrColumn | Job On não ganha ligação ao Controlo | FRACO | verifica forma do modelo (nomes), não comportamento |
| ControloResumoSchemaTests.ControloEntity_UniqueJobOnIdIndex_RestrictForeignKey | Um controlo por Job On (índice único) e FK sem apagar em cascata | OK |  |
| ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos | Modelo da base tem exatamente as tabelas esperadas | FRACO | compara lista de tabelas, não um efeito; parte quando se acrescenta tabela legítima |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloTresEstadosTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloTresEstadosTests.AsFrasesDosTresEstados | Frases dos três estados de aprovação | OK |  |
| ControloTresEstadosTests.Historico_MostraOsTresEstados_ComQuemDecidiuEANota | Histórico mostra Aprovado, Não aprovado e Por aprovar com quem e nota | OK |  |
| ControloTresEstadosTests.OEstadoTrazONomeDeQuemDecidiu_EmTodasAsLeituras | Todas as leituras do estado trazem o nome de quem decidiu | OK |  |
| ControloTresEstadosTests.ParteAsync_OPesoMostraASuaDecisao_EOControloEODoPeso | O estado do controlo é o da decisão do Peso | OK |  |
| ControloTresEstadosTests.ParteAsync_SemDecisoesPorParte_MostraOResumoGravado_ENullSemControlo | Sem decisões por parte mostra o selo do Resumo | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloViewingHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloViewingHandlerTests.Mark_EmptyOperator_RegistersNothing | Sem nº de operador não regista visita | OK |  |
| ControloViewingHandlerTests.Mark_FirstVisit_RegistersAndLists | Primeira abertura regista «Visto por» | OK |  |
| ControloViewingHandlerTests.Mark_Reopen_UpdatesTheSameLine_NeverDuplicates | Reabrir atualiza a mesma linha, sem repetir | FRACO | «nunca repete» só provado em memória; falta índice único no PostgreSQL e janela de 30 s |
| ControloViewingHandlerTests.Mark_TwoOperators_TwoLines | Dois operadores, duas linhas de «Visto por» | OK |  |
| ControloViewingHandlerTests.Mark_UnknownControlo_RegistersNothing | Controlo inexistente não regista visita | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CoresDeEstadoNosPdfsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CoresDeEstadoNosPdfsTests.OsGeradoresDePdf_NaoTemCoresSoltas | Geradores de PDF usam só cores da paleta | FRACO | verifica cores no código-fonte (estilo), não o efeito; pertence às guardas do sistema de design (regra 10) |
| CoresDeEstadoNosPdfsTests.PegamentosPdf_NaoAvaliavel_SaiACinzento_ComoNoMapa | Não avaliável sai a cinzento no PDF dos Pegamentos | OK |  |
| CoresDeEstadoNosPdfsTests.Puncoes_SaemComAsCoresDeEstadoNoResumo | Punções saem com as cores de estado no PDF | OK |  |
| CoresDeEstadoNosPdfsTests.ResumoPdf_NaoAvaliaveis_SaemACinzento | Não avaliáveis saem a cinzento no PDF do Resumo | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CorrectPesoMeasurementHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CorrectPesoMeasurementHandlerTests.Apagar_TiraALeitura_DeixaRasto_ERenumeraAsPosicoes | Apagar leitura deixa rasto e renumera posições | OK |  |
| CorrectPesoMeasurementHandlerTests.CorrigirParaPesoDoVidroZeroOuMenos_ERecusado_ALeituraFicaIgual | Correção que dá peso ≤ 0 é recusada e a leitura fica igual | OK |  |
| CorrectPesoMeasurementHandlerTests.Corrigir_MudaALeitura_EDeixaRastoComOAntesEODepois | Corrigir leitura do Peso deixa rasto com antes e depois | OK |  |
| CorrectPesoMeasurementHandlerTests.GuardarOsMesmosValores_NaoDeixaRasto | Guardar os mesmos valores não deixa rasto | OK |  |
| CorrectPesoMeasurementHandlerTests.LeituraDeOutroPeso_OuQueNaoExiste_NaoEncontrada_NadaMuda | Leitura de outro Peso ou inexistente: nada muda | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreatePesoForControloHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CreatePesoForControloHandlerTests.Create_SavesThePesoAlreadyLinkedAndReturnsItAgain | Criar Peso da produção grava ligado e devolve o mesmo | FRACO | idempotência só em memória; falta teste PostgreSQL do índice único por combinação |
| CreatePesoForControloHandlerTests.Create_WhenAnotherRequestSavedFirst_ReturnsThatPesoWithoutAnOrphan | Dois pedidos ao mesmo tempo criam um só Peso | FRACO | simula a corrida em memória (sem índice único nem trinco do PostgreSQL), regra 7 |
| CreatePesoForControloHandlerTests.PesoNovo_CopiaAProducaoAnteriorDoPesoDaProducao | Peso novo copia a produção anterior do Peso da produção | OK |  |
| CreatePesoForControloHandlerTests.TrocaDeBq_CriaPesoNovo | Trocar a BQ cria Peso novo | OK |  |
| CreatePesoForControloHandlerTests.TrocaDeCm_CriaSegundoPeso_OAntigoMantemAsLeituras | Trocar a CM cria 2.º Peso; o antigo mantém leituras | OK |  |
| CreatePesoForControloHandlerTests.TrocaSoDoMf_MantemOPeso | Trocar só o MF mantém o Peso | OK |  |
| CreatePesoForControloHandlerTests.VoltarACmAnterior_ReabreOPesoA | Voltar à CM anterior reabre o Peso antigo, sem duplicar | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPegamentosHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CreateToolScopedPegamentosHandlerTests.RepeatedClicks_OpenTheSameEmptySheet_NewOneOnlyAfterMeasurements | Cliques repetidos abrem a mesma folha vazia; nova só após leituras | FRACO | «nada repetido» só em memória; falta PostgreSQL com duas pessoas ao mesmo tempo |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CreateToolScopedPesoHandlerTests.Create_CongezesConsumedInputsAndAnchorsTools | Criar Peso congela as entradas e liga às ferramentas por id | OK |  |
| CreateToolScopedPesoHandlerTests.Create_EmptyCmToolId_RefusedCmToolNotFound | CM sem id é recusada, nada gravado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_GlassDensityNotConfiguredForProcess_Refused | Sem densidade do vidro para o processo é recusado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_MissingBqVolumeMarisa_Refused | Volume da punção ou da marisa em falta é recusado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_MissingCmProcess_AfterCompletionAttempt_Refused | Processo em falta ou desconhecido é recusado, nada gravado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_MissingCmVolumePuncao_Refused | Volume da punção ou da marisa em falta é recusado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_MissingToolValues_CompletedOnOwningToolId | Valores em falta completam-se na ferramenta dona | OK |  |
| CreateToolScopedPesoHandlerTests.Create_NominalWeightIsOptional | O peso nominal é opcional | OK |  |
| CreateToolScopedPesoHandlerTests.Create_RefusalLeavesNoPartialCompletionBehind | Recusa não deixa completações parciais na ferramenta | OK |  |
| CreateToolScopedPesoHandlerTests.Create_RegistadosValuesAreNeverOverwrittenByCompletion | Valores registados nunca são sobrescritos | OK |  |
| CreateToolScopedPesoHandlerTests.Create_SameTool_ManyPesosOverTime | A mesma ferramenta pode ter vários Pesos ao longo do tempo | OK |  |
| CreateToolScopedPesoHandlerTests.Create_UnknownBqTool_Refused | CM ou BQ desconhecida é recusada, nada gravado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_UnknownCmTool_Refused | CM ou BQ desconhecida é recusada, nada gravado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_UnknownProcessToken_Refused | Processo em falta ou desconhecido é recusado, nada gravado | OK |  |
| CreateToolScopedPesoHandlerTests.Create_UsesGlassDensityConfiguredForConsumedProcess | Densidade do vidro vem da configuração do processo | OK |  |
| CreateToolScopedPesoHandlerTests.Create_VolumesOrNominalNonPositive_RefusedTyped | Volumes ou nominal ≤ 0 recusados | DUPLICADO | coberto por UmVolumeOuONominalZeroOuNegativoSozinho_ERecusado_NadaFicaGravado (que testa um campo de cada vez) |
| CreateToolScopedPesoHandlerTests.Create_WaterTemperatureAbsurdOverflow_RefusedTyped_Never500 | Temperatura absurda é recusada sem erro 500 | OK |  |
| CreateToolScopedPesoHandlerTests.Create_WaterTemperatureOutsideSupportedRange_Refused | Temperatura fora de 4,5–35,5 é recusada (limites) | OK |  |
| CreateToolScopedPesoHandlerTests.Create_WaterTemperatureRoundsInToSupportedRange_Accepted | Temperatura que arredonda para dentro é aceite | OK |  |
| CreateToolScopedPesoHandlerTests.RepeatedClicks_OpenTheSameEmptyPeso_NewOneOnlyAfterReadings | Cliques repetidos abrem o mesmo Peso vazio; novo só após leituras | FRACO | «nada repetido» só em memória; falta teste PostgreSQL (índice único/trinco) com dois pedidos ao mesmo tempo |
| CreateToolScopedPesoHandlerTests.UmVolumeOuONominalDe001_EAceite_FicaNoPesoENaFerramenta | Volume ou nominal 0,01 é aceite e fica no Peso e na ferramenta | OK |  |
| CreateToolScopedPesoHandlerTests.UmVolumeOuONominalZeroOuNegativoSozinho_ERecusado_NadaFicaGravado | Volume ou nominal 0 ou negativo, um de cada vez, recusa | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoTampaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CreateToolScopedPesoTampaoTests.ComJobOn_CongelaOTampaoDoJobOn | Peso com Job On congela o tampão do Job On | OK |  |
| CreateToolScopedPesoTampaoTests.SemJobOn_CongelaOQueACmTemDeArranque | Peso sem Job On congela o tampão da CM | OK |  |
| CreateToolScopedPesoTampaoTests.SemTampaoEmLadoNenhum_PesoCriaSeComOTampaoVazio | Sem tampão em lado nenhum o Peso nasce com tampão vazio | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EmailRascunhoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EmailRascunhoTests.Gerar_AbreComoMensagemNova_SemFrom_QuandoOModeloNaoTemRemetente | Rascunho de email abre como mensagem nova com PDF anexo | OK |  |
| EmailRascunhoTests.Gerar_AcentosNoAssuntoENoAnexo_VaoEmUtf8_EUmaLinhaNovaNaoPartiOCabecalho | Acentos em UTF-8 e sem injeção de cabeçalhos no email | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EnsureControloForJobOnHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EnsureControloForJobOnHandlerTests.Ensure_ExistingJobOn_CreatesExactlyOneControlo | Job On existente ganha exatamente um controlo | OK |  |
| EnsureControloForJobOnHandlerTests.Ensure_PreCutJobOn_GainsControloByBackfillOnce | Job On antigo ganha o controlo uma só vez | DUPLICADO | igual a Ensure_ReExecution_ReturnsSameControloId (idempotência) |
| EnsureControloForJobOnHandlerTests.Ensure_ReExecution_ReturnsSameControloId | Repetir devolve o mesmo controlo, sem repetir | OK | Unicidade por Job On garantida à parte no PostgreSQL (ControloEntity_UniqueJobOnIdIndex) |
| EnsureControloForJobOnHandlerTests.Ensure_TwoJobOns_TwoControlos | Dois Job Ons têm dois controlos | OK |  |
| EnsureControloForJobOnHandlerTests.Ensure_UnknownJobOn_RefusedTypedWithoutWrite | Job On inexistente é recusado sem gravar | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EnvioEmailRegrasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EnvioEmailRegrasTests.EmailValido_SoUmEnderecoCompleto | Só um endereço de email completo é válido | OK |  |
| EnvioEmailRegrasTests.Preencher_TrocaAReferenciaAProducaoEAMaquina | Assunto do email troca referência, produção e máquina | OK |  |
| EnvioEmailRegrasTests.Preencher_TrocaOPeso_ComAUnidade | Assunto do email troca o peso com unidade | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PdfRecoveryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PdfRecoveryTests.BlankRows_OnlyOnTheSeparateBlankSheet_PartialRecordAndResumoUnchanged | Linhas em branco só na folha em branco separada | OK |  |
| PdfRecoveryTests.GenerateCompleteVisualEvidence | PDFs completos de Pegamentos e Resumo trazem os valores certos | OK |  |
| PdfRecoveryTests.IndividualAlertsSurviveMeanInsideCorridor | Alerta individual não desaparece com a média dentro do corredor | OK |  |
| PdfRecoveryTests.MapHasEveryComponentAndHistoricalCorridorsAndCoreAlert | Mapa dos Pegamentos mostra os componentes, corredores e o alerta | OK |  |
| PdfRecoveryTests.MapaJuntaOsRotulosDeNominaisIguais | Mapa junta etiquetas de nominais iguais | FRACO | verifica cores e opacidades do SVG (estilo), não o efeito |
| PdfRecoveryTests.MapaMostraOsLimitesDeForaTracejadosComoOOriginal | Mapa mostra limites de fora tracejados | FRACO | verifica stroke-dasharray (estilo) em vez do efeito |
| PdfRecoveryTests.NominalWeightDeviationDoesNotInventMissingOrZeroReference | Desvio ao nominal não inventa referência em falta ou zero | OK |  |
| PdfRecoveryTests.SummaryPdfRendersEmptyAndApprovedProductions | PDF do Resumo vazio e aprovado | OK |  |
| PdfRecoveryTests.UnmeasuredComponentRemainsVisibleAndUnknownNominalDoesNotBecomeGreen | Componente sem medições fica visível; nominal desconhecido não é verde | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosCalculationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosCalculationTests.Corridor_NominalPlusMinusTolerance | Corredor é nominal mais ou menos tolerância | OK |  |
| PegamentosCalculationTests.EvaluateRow_BothAxes_IndependentVerification_CosturaAlertDoesNotHideBehindAverage | Alerta de um eixo não se esconde atrás da média | OK |  |
| PegamentosCalculationTests.EvaluateRow_ConfiguredToleranceReplacesDefault | Tolerância das Definições substitui a por omissão | OK |  |
| PegamentosCalculationTests.EvaluateRow_NoNominal_NotEvaluable_NoInventedValues | Sem nominal a linha é não avaliável, sem inventar valores | OK |  |
| PegamentosCalculationTests.EvaluateRow_OvalizacaoAtOrAboveTolerance_AlertsEvenWithEveryAxisInside | Ovalização na tolerância ou acima dá alerta | OK |  |
| PegamentosCalculationTests.EvaluateRow_OvalizacaoBelowTolerance_NoAlert | Ovalização abaixo da tolerância não dá alerta | OK |  |
| PegamentosCalculationTests.EvaluateRow_SingleAxis_CorridorAppliesToMediaWhichEqualsCostura | Com um eixo o corredor aplica-se à média | OK |  |
| PegamentosCalculationTests.IsAlert_ReachingOrCrossingEitherBoundary_Alerts | Tocar ou cruzar o limite dá alerta | OK |  |
| PegamentosCalculationTests.IsAlert_StrictlyInside_NoAlert | Estritamente dentro do corredor não dá alerta | OK |  |
| PegamentosCalculationTests.Media_BothAxes_ArithmeticMean | Média dos dois eixos é aritmética | OK |  |
| PegamentosCalculationTests.Media_SingleAxis_EqualsCostura | Média com um eixo é a costura | OK |  |
| PegamentosCalculationTests.Ovalizacao_CosturaMinusContraCostura_SignPreserved | Ovalização é costura menos contra costura, com sinal | OK |  |
| PegamentosCalculationTests.Ovalizacao_SingleAxis_Undefined | Ovalização sem um eixo é indefinida | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosEscolhasQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosEscolhasQueryTests.CodigosDeMaquinas_VemPorOrdemDoCodigo | Códigos das máquinas por ordem | OK |  |
| PegamentosEscolhasQueryTests.LotesDaReferencia_ReferenciaEmBranco_NaoDevolveNada | Referência em branco não devolve lotes | OK |  |
| PegamentosEscolhasQueryTests.LotesDaReferencia_VemComOLoteMaisAltoPrimeiroSemMisturarTiposNemReferencias | Lotes da referência: o mais alto primeiro, sem misturar | OK |  |
| PegamentosEscolhasQueryTests.OsDoisPedidos_NaoGravamNada | As consultas de escolhas não gravam nada | FRACO | ChangeTracker vazio não prova que nada foi gravado; devia reler a base |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosFolhaPorFerramentasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosFolhaPorFerramentasTests.AfterTheChange_BeforeTheFirstReading_TheSheetIsReadyWithTheNewTools_AndTheOldOneOpensOnRequest | Após a troca a folha está pronta com as ferramentas novas | OK |  |
| PegamentosFolhaPorFerramentasTests.Association_ProductionWithOnlyAnEarlierCombinationSheet_TakesTheSheetOfTheCurrentTools | Associar folha solta só com as ferramentas atuais | OK |  |
| PegamentosFolhaPorFerramentasTests.BackToAnEarlierCombination_ReusesItsSheet_NoThirdSheet | Voltar a uma combinação anterior reutiliza a folha | OK |  |
| PegamentosFolhaPorFerramentasTests.NewSheet_FreezesTheCurrentTolerance_TheOldOneKeepsItsOwn | Folha nova congela a tolerância; a antiga mantém a sua | OK |  |
| PegamentosFolhaPorFerramentasTests.OldSheet_RefusesNewReadings_ButItsReadingsAreStillCorrectedAndDeleted | Folha antiga recusa leituras novas mas aceita correções | OK |  |
| PegamentosFolhaPorFerramentasTests.ResumoPdfCompleto_FolhaDeFora_SemLigacao_ACardLigaAFolhaQueEntra | PDF completo com «Ver →», «Ver detalhe →» e «← Voltar ao Resumo» e só com a folha certa (DMO-684, Diogo 29/10) | OK | A-0438 |
| PegamentosFolhaPorFerramentasTests.ResumoPdf_TwoSheets_OneSectionPerSheet_TheAlertOfTheSecondIsNotHidden | PDF do Resumo com duas folhas, com «Ver →», não esconde o alerta da segunda (DMO-684, Diogo 29/10) | OK | A-0438 |
| PegamentosFolhaPorFerramentasTests.SingleSheet_LooksAsBefore_NoSheetLabelOrSuffix | Folha única sem etiqueta nem sufixo | OK |  |
| PegamentosFolhaPorFerramentasTests.ToolChangedMidProduction_FirstReadingStartsANewSheet_TheOldOneStaysAsItWas | Trocar CM, BQ ou MF abre folha nova; a antiga fica como estava | OK |  |
| PegamentosFolhaPorFerramentasTests.WithoutMf_ReadingsShareOneSheet_AndAnMfAddedLaterStartsANewSheet | Sem MF as leituras partilham a folha; MF novo abre outra | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosRastoDasLeiturasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosRastoDasLeiturasTests.ApagarUmaLeitura_GuardaOQueTinha_EAConsultaDoControloMostraSoAsDele | Apagar leitura guarda o que tinha e mostra só as do controlo | OK |  |
| PegamentosRastoDasLeiturasTests.CorrigirUmaLeitura_GuardaOValorAntigoONovoQuemEQuando | Corrigir leitura guarda antes, depois, quem e quando | OK |  |
| PegamentosRastoDasLeiturasTests.EliminarOJobOn_LevaORastoComAFolha | Eliminar o Job On leva o rasto com a folha | OK |  |
| PegamentosRastoDasLeiturasTests.GuardarOsMesmosValores_NaoDeixaRastoNemAviso | Guardar valores iguais não deixa rasto | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosRealTargetTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosRealTargetTests.RealBq_Measurements_SharedBoundaryWithMf | Medições reais da BQ: fronteira partilhada com o MF | OK |  |
| PegamentosRealTargetTests.RealCm_Measurements_IndividualCells_AlertBeyondTheTargetsOwnMarks | Medições reais da CM: alertas por célula | OK |  |
| PegamentosRealTargetTests.RealCorridors_AreSharedBoundaries | Corredores de CM, BQ e MF partilham fronteiras | FRACO | as últimas verificações são médias calculadas no próprio teste; não exercitam código da app |
| PegamentosRealTargetTests.RealMf_Measurements_IndividualContraCells_TheTargetDoesNotMark | Medições reais do MF: alertas na contra costura | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables | Modelo tem exatamente as tabelas de Pegamentos | FRACO | compara lista de tabelas, não um efeito |
| PegamentosSchemaTests.PegamentosControloIdIndex_IsNotUnique_CardinalityIsNotADomainRestriction | Um controlo pode ter várias folhas de Pegamentos | FRACO | só verifica que o índice não é único (ausência de proteção), não que várias folhas se gravam |
| PegamentosSchemaTests.PegamentosEntity_RegistroUnderControlo_ToleranceFrozen_ToolsInherited | Folha de Pegamentos: FKs sem cascata e tolerância obrigatória | FRACO | verifica nomes de colunas do modelo; só as FKs/obrigatoriedade têm valor |
| PegamentosSchemaTests.PegamentosMeasurementEntity_AppendOnlyFacts_ContraCosturaNullable | Leitura de Pegamentos: contra costura opcional, apaga com a folha | FRACO | verifica forma do modelo, não comportamento |
| PegamentosSchemaTests.PegamentosToleranceEntity_SingletonShape | Só existe uma tolerância de Pegamentos (índice único) | OK |  |
| PegamentosSchemaTests.ToolTechnicalContext_GargaloColumnIsNullable | Gargalo da ferramenta é opcional | FRACO | verifica só a nulidade da coluna no modelo |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosWorkedExampleTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosWorkedExampleTests.WorkedExample_J10_5810_B1_T3_BQ1_FolhaCompleta | Exemplo completo: Job On cria controlo e folha com as 3 ferramentas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoAprovacaoDocumentoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoAprovacaoDocumentoTests.AprovacaoSoCobrePesosRegistadosAteAoInstanteDaDecisao | A aprovação só cobre Pesos registados até à decisão | OK |  |
| PesoAprovacaoDocumentoTests.NaoAprovarMantemSeNoPdfDepoisDeUmPesoNovo | Não aprovado mantém-se no PDF após Peso novo | OK |  |
| PesoAprovacaoDocumentoTests.OAnexoDoEmailTambemFicaPorAprovar | Anexo do email do Peso novo também fica Por aprovar | OK |  |
| PesoAprovacaoDocumentoTests.PesoNovoNaoHerdaAAprovacaoNoPdf_ENoHistoricoAPrimeiraFolhaContinuaAprovada | Peso novo não herda a aprovação no PDF | OK |  |
| PesoAprovacaoDocumentoTests.ReaprovarDepoisDoPesoNovoAtualizaOPdf | Reaprovar após Peso novo atualiza o PDF | OK |  |
| PesoAprovacaoDocumentoTests.UmPesoDeOutroControloOuInexistenteNaoRecebeADecisao | Peso de outro controlo ou inexistente não recebe decisão | OK |  |
| PesoAprovacaoDocumentoTests.UmaLeituraNovaNaoInvalidaAAprovacaoDaFolha | Leitura nova não invalida a aprovação da folha | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoCapCalculationTests.Cap_CaloteNegativa_Recusa | Diâmetro ≤ 0 ou calote negativa é recusada | OK |  |
| PesoCapCalculationTests.Cap_CalotePlana_HZero_RNullVolumeZero | Calote plana tem volume zero | OK |  |
| PesoCapCalculationTests.Cap_DiamentroNaoPositivo_Recusa | Diâmetro ≤ 0 ou calote negativa é recusada | OK |  |
| PesoCapCalculationTests.Cap_OutroPar_CalculaPelaFormula | Calote com outro par de medidas pela fórmula | OK |  |
| PesoCapCalculationTests.Cap_ValoresDeReferencia_D50_H10 | Calote D50 H10: raio e volume de referência | OK |  |
| PesoCapCalculationTests.Cap_Volume_ExatoPelaFormula | Volume da calote D50 H10 pela fórmula exata | DUPLICADO | mesma conta D50/H10 de Cap_ValoresDeReferencia_D50_H10 (só aperta a margem) |
| PesoCapCalculationTests.Support_ComCaloteDoJobOn_ComparaComAReferencia | Apoio da calote compara a do Job On com a referência | OK |  |
| PesoCapCalculationTests.Support_ReferenciaPlana_DiferencasDeRaioIndefinidas | Referência plana: diferença de raio indefinida | OK |  |
| PesoCapCalculationTests.Support_SemCaloteEfetiva_CaiNaReferenciaDaTool | Sem calote efetiva usa a referência da ferramenta | OK |  |
| PesoCapCalculationTests.TrySupport_GeometriaCompleta_DevolveOApoio | Geometria completa dá apoio | OK |  |
| PesoCapCalculationTests.TrySupport_GeometriaIncompleta_SemApoio | Geometria incompleta não dá apoio | OK |  |
| PesoCapCalculationTests.TrySupport_SoComDiametroECalote_DevolveOApoioSemRaio | Só diâmetro e calote dá apoio sem raio | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapSupportQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoCapSupportQueryTests.GetAsync_CaloteNegativaNoJobOn_SemApoioSemBloqueio | Calote negativa: sem apoio, sem bloquear o Peso | OK |  |
| PesoCapSupportQueryTests.GetAsync_CaloteZero_CalotePlanaHonesta | Calote zero é calote plana | OK |  |
| PesoCapSupportQueryTests.GetAsync_ProducaoComCalote_OrigemJobOn | Apoio da calote usa a calote do Job On | OK |  |
| PesoCapSupportQueryTests.GetAsync_ProducaoSemCalote_CaiNaReferenciaDaTool | Sem calote no Job On usa a da ferramenta | OK |  |
| PesoCapSupportQueryTests.GetAsync_SemControlo_UsaAReferenciaDaTool | Sem controlo usa a referência da ferramenta | OK |  |
| PesoCapSupportQueryTests.GetAsync_Simulacao_SobrepoeApenasNaExibicao | Simulação só muda a exibição, não o Job On | OK |  |
| PesoCapSupportQueryTests.GetAsync_ToolDesconhecidaOuVazia_SemApoioSemBloqueio | Ferramenta desconhecida: sem apoio, sem bloquear | OK |  |
| PesoCapSupportQueryTests.GetAsync_ToolSemTP_ReferenciaPorCompletar | Ferramenta sem TP pede para completar o tampão | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoCalculationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoCalculationTests.Pares_DeltaVolume_NosDoisLadosPresentes | Diferença de volume só com os dois lados | OK |  |
| PesoComparacaoCalculationTests.Pares_LeituraAnteriorUsadaNoMaximoUmaVez | Leitura anterior só serve um par | OK |  |
| PesoComparacaoCalculationTests.Pares_PeloNumeroDoCm_DeltaComSinalPreservado | Pares pelo nº da CM, diferença com sinal | OK |  |
| PesoComparacaoCalculationTests.Pares_SemContraparte_LinhaSemParNuncaErro | Linha sem par nunca dá erro | OK |  |
| PesoComparacaoCalculationTests.Pares_SemNenhumPar_DeltaMedioIndefinido | Sem pares, a diferença média é indefinida | OK |  |
| PesoComparacaoCalculationTests.Pares_SujecomVazias_NuncaEmparelham | Números de CM vazios nunca emparelham | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoEstadoQueryTests.Base_ComLeituras_CalculaAInfluenciaDaBasePelaOrdemDasPosicoes | Influência de cada CM da base pela ordem das posições | OK |  |
| PesoComparacaoEstadoQueryTests.Base_ComPares_CalculaAInfluenciaDasNovasContraAMediaDaBase | Influência das CM novas contra a média da base | OK |  |
| PesoComparacaoEstadoQueryTests.Base_Inexistente_DevolveBaseNulaSemInfluencias | Peso base inexistente não dá base nem influências | OK |  |
| PesoComparacaoEstadoQueryTests.Base_SemLeituras_MostraAPesoMasSemInfluencias | Base sem leituras mostra o Peso sem influências | OK |  |
| PesoComparacaoEstadoQueryTests.ContraMoldes_AReferenciaDoInicialVemPrimeiroDepoisPorReferencia | Lista de CM: a do Peso inicial primeiro | OK |  |
| PesoComparacaoEstadoQueryTests.OsTresPedidos_NaoGravamNada | As consultas da Comparação não gravam nada | FRACO | ChangeTracker vazio não prova que nada foi gravado; devia reler a base |
| PesoComparacaoEstadoQueryTests.ProducaoAntigaAlemDasUltimasCinco_FicaNoSelectEEscolhida | Produção antiga além das cinco últimas entra no select | OK |  |
| PesoComparacaoEstadoQueryTests.ProducaoPedidaDeOutraReferencia_NaoFicaEscolhida | Produção de outra referência não fica escolhida | OK |  |
| PesoComparacaoEstadoQueryTests.ProducaoPedida_SemReferenciaEscrita_UsaAReferenciaDela | Produção pedida usa a sua referência | OK |  |
| PesoComparacaoEstadoQueryTests.ReferenciaEscritaSemProducoes_DizQueNaoHaProducoes | Referência sem produções diz que não há | OK |  |
| PesoComparacaoEstadoQueryTests.SemReferenciaNemProducaoPedida_DevolveOSelectVazio | Sem referência nem produção o select vem vazio | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoHandlerTests.CompararGuardados_Inexistente_Null | Comparar Peso inexistente não devolve nada | OK |  |
| PesoComparacaoHandlerTests.CompararGuardados_MesmoPeso_DeltaZeroEmVezDeRecusa | Comparar o mesmo Peso dá diferença zero | OK |  |
| PesoComparacaoHandlerTests.CompararGuardados_PorId_ParesPeloNumeroDoCm | Comparar Pesos guardados: pares pelo nº da CM | OK |  |
| PesoComparacaoHandlerTests.CompararLeituras_CalculaPeloBackendComABaseDoPesoInicial | Comparação calcula no servidor com a base do Peso inicial | OK |  |
| PesoComparacaoHandlerTests.CompararLeituras_MassaNaoPositivaNumaLinha_FicaDeForaEAsOutrasComparam | Linha com massa ≤ 0 fica de fora e as outras comparam | OK |  |
| PesoComparacaoHandlerTests.CompararLeituras_NenhumaLeituraUtilizavel_RecusaComAsDeFora | Sem nenhuma leitura utilizável recusa e diz as de fora | OK |  |
| PesoComparacaoHandlerTests.CompararLeituras_RecusasTipadas | Comparação recusa temperatura, massa e leituras inválidas | FRACO | várias recusas juntas num teste (regra 4: um campo de cada vez) e sem ler a base para provar que nada ficou gravado |
| PesoComparacaoHandlerTests.CompararLeituras_ResultadoNaoPositivoNumaLinha_FicaDeForaEAsOutrasComparam | Linha com resultado ≤ 0 fica de fora e as outras comparam | OK |  |
| PesoComparacaoHandlerTests.CompararLeituras_ResultadoNaoPositivo_RecusaAntesDeQualquerValor | Resultado ≤ 0 recusa sem devolver valores | OK |  |
| PesoComparacaoHandlerTests.CompararLeituras_SemProcessoNaTool_UsaADensidadeCongeladaDoPesoInicial | Sem processo usa a densidade congelada do Peso inicial | OK |  |
| PesoComparacaoHandlerTests.CompararLeituras_SemToolCaiNoCmDoPesoInicial | Sem ferramenta cai na CM do Peso inicial | OK |  |
| PesoComparacaoHandlerTests.Guardado_CalculoCongeladoContraEdicaoPosteriorDaTool | Comparação guardada não muda se a ferramenta mudar depois | OK |  |
| PesoComparacaoHandlerTests.Guardar_AMesmaComparacaoDepoisDaJanela_CriaOutra | Mesma comparação depois da janela cria outra | OK |  |
| PesoComparacaoHandlerTests.Guardar_AMesmaComparacaoTresVezesSeguidas_FicaUmRegisto | Guardar a mesma comparação 3 vezes deixa um só registo | FRACO | «nada repetido» provado em memória; sem trinco/índice do PostgreSQL não prova dois cliques ao mesmo tempo |
| PesoComparacaoHandlerTests.Guardar_LeituraNaoPositiva_GravaAsOutrasEDevolveAsDeFora | Guardar grava as leituras boas e devolve as de fora | OK |  |
| PesoComparacaoHandlerTests.Guardar_OutraLeituraOuOutraNota_CriaOutraComparacao | Outra leitura ou nota cria outra comparação | OK |  |
| PesoComparacaoHandlerTests.Guardar_PersisteORegistoComOsInputsCongelados | Guardar comparação congela as entradas | OK |  |
| PesoComparacaoHandlerTests.Guardar_RecusaPreservaNada | Guardar recusado não grava nada | OK |  |
| PesoComparacaoHandlerTests.ListarIniciais_OsPesosDaProducaoDoMaisAntigo | Lista os Pesos iniciais da produção mais antiga | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoInfluenciaCalculationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoInfluenciaCalculationTests.DaBase_MarcaACmQueMaisPuxa | Marca a CM que mais puxa a média da base | OK |  |
| PesoComparacaoInfluenciaCalculationTests.Novas_ComParSubstituiSemParSoma | CM nova com par substitui; sem par soma | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararLimiteTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoCompararLimiteTests.OPesoDeOndeSeParte_ForaDos500MaisRecentes_FicaDoLadoAtual | O Peso de partida fica sempre na comparação (limite 500) | OK |  |
| PesoCompararLimiteTests.Os500MaisRecentesNoutraMaquina_ACmDestaMaquinaContinuaACompararSe | 500 Pesos de outra máquina não escondem as CM desta | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCompararQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoCompararQueryTests.DoHistorico_PeloInicioDaReferencia_SemJobOnValemAsMaquinasDaFicha | Do Histórico, sem Job On valem as máquinas da ficha | OK |  |
| PesoCompararQueryTests.DoHistorico_SemMaquina_FicaADoPesoMaisRecente | Do Histórico sem máquina vale a do Peso mais recente | OK |  |
| PesoCompararQueryTests.DoPesoAntigo_FicaOPedidoNaCmDele | Do Peso antigo fica o pedido na CM dele | OK |  |
| PesoCompararQueryTests.DoPeso_SoAMesmaReferenciaEMaquina_OMaisRecenteDeCadaCm | Comparar: mesma referência e máquina, o mais recente de cada CM | OK | Decisão Diogo 27/10 (peso-comparar-tp) |
| PesoCompararQueryTests.OutraMaquinaEscolhida_TrocaAsCm | Outra máquina escolhida troca as CM | OK |  |
| PesoCompararQueryTests.SemTampaoNoPeso_ValeODoJobOn | Sem tampão no Peso vale o do Job On | OK |  |
| PesoCompararQueryTests.VariasReferencias_PedeParaEscolher | Várias referências: pede para escolher | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparisonQueryTests.CompararPesosGuardados_AReferenciaPeloInicio_NuncaAMeio | Procura de referência pelo início, nunca pelo meio | OK |  |
| PesoComparisonQueryTests.FindCandidates_DoPesoAnterior_UsaAsMaquinasDaCmDele | Do Peso anterior usam-se as máquinas da CM dele | OK |  |
| PesoComparisonQueryTests.FindCandidates_NuncaMaisDe5Anteriores_MaisAPedida | Nunca mais de 5 anteriores mais a pedida | DUPLICADO | igual a FindCandidates_SoAsUltimas5_EOPedidoMaisAntigoFica |
| PesoComparisonQueryTests.FindCandidates_ProductionWithoutCmContext_AllMachinesOfTheReference | Produção sem contexto de CM: todas as máquinas | DUPLICADO | igual a FindCandidates_ToolWithoutMachineLinks_AllMachinesOfTheReference |
| PesoComparisonQueryTests.FindCandidates_SemInicioNaAtual_SoAsRegistadasAntes_AsSemInicioNoFim | Sem início na atual, só as registadas antes | OK |  |
| PesoComparisonQueryTests.FindCandidates_SoAsAnteriores_APosteriorNaoAparece_EAPedidaPosteriorFicaNoFim | Candidatos só anteriores; a pedida posterior vai ao fim | OK |  |
| PesoComparisonQueryTests.FindCandidates_SoAsUltimas5_EOPedidoMaisAntigoFica | Candidatos: últimas 5, mais o pedido antigo | OK |  |
| PesoComparisonQueryTests.FindCandidates_ToolLinkedToB1AndC3_TheDropdownHasC3 | Candidatos: só máquinas ligadas à ferramenta | OK |  |
| PesoComparisonQueryTests.FindCandidates_ToolLinkedToB1Only_TheDropdownHasNoB3 | Candidatos: ferramenta só de B1 não traz B3 | DUPLICADO | mesma regra de FindCandidates_ToolLinkedToB1AndC3_TheDropdownHasC3 |
| PesoComparisonQueryTests.FindCandidates_ToolWithoutMachineLinks_AllMachinesOfTheReference | Sem ligações a máquinas valem todas as da referência | OK |  |
| PesoComparisonQueryTests.FindCandidates_VariosPesosPorProducao_OsDaAtualFicamDeFora | Pesos da produção atual não são candidatos | OK |  |
| PesoComparisonQueryTests.ListarIniciais_DoisPesos_MarcaOAtualEDizABq | Dois Pesos iniciais: marca o atual e diz a BQ | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCorrigidoDepoisDaAprovacaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoCorrigidoDepoisDaAprovacaoTests.ApagarAUnicaLeituraDepoisDaAprovacao_FicaComAData | Apagar a única leitura após aprovar regista a data | OK |  |
| PesoCorrigidoDepoisDaAprovacaoTests.AsCorrecoesDeOutroControlo_NaoContam | Correções de outro controlo não contam | OK |  |
| PesoCorrigidoDepoisDaAprovacaoTests.SemCorrecoes_AUltimaCorrecaoFicaVazia | Sem correções, não há data de última correção | OK |  |
| PesoCorrigidoDepoisDaAprovacaoTests.UmaCorrecaoAntesDeAprovar_NaoContaDepoisDaAprovacao | Correção antes de aprovar não conta como posterior | OK |  |
| PesoCorrigidoDepoisDaAprovacaoTests.UmaLeituraCorrigidaDepoisDaAprovacao_MantemAAprovacao_EFicaComAData | Corrigir após aprovar mantém a aprovação e regista a data | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoDecisoesDoDiogoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoDecisoesDoDiogoTests.Aprovar_ComAEscolhaMudadaEntretanto_NaoGravaNada | Aprovar com escolha mudada entretanto não grava | OK |  |
| PesoDecisoesDoDiogoTests.ColocarDeParteEAprovar_NaoMudaACmNasFerramentas | Pôr de parte e aprovar não mexe na CM das Ferramentas | OK |  |
| PesoDecisoesDoDiogoTests.ColocarDeParte_FicaPorAprovar_EAAprovacaoEPorCm | CM posta de parte fica por aprovar, aprovação por CM | OK |  |
| PesoDecisoesDoDiogoTests.ColocarDeParte_NumaLeituraQueNaoExiste_NaoGravaNada | Pôr de parte leitura inexistente não grava | OK |  |
| PesoDecisoesDoDiogoTests.Comparacao_ADensidadeDoVidroMudouNasDefinicoes_AvisaNaoComparavel_EAGuardadaFicaComOsSeusValores | Densidade mudou: avisa não comparável | OK |  |
| PesoDecisoesDoDiogoTests.Comparacao_APuncaoDaCmMudouDesdeOPesoInicial_AvisaNaoComparavel_SemRecusar | Punção mudou: avisa não comparável, sem recusar | OK |  |
| PesoDecisoesDoDiogoTests.Comparacao_ComAMesmaPuncaoEDensidade_EComparavelESemAviso | Mesma punção e densidade: comparável, sem aviso | OK |  |
| PesoDecisoesDoDiogoTests.DensidadeDoVidro_ForaDoHabitualOuComMaisCasas_GravaComAviso | Densidade fora do habitual ou com mais casas grava com aviso | OK |  |
| PesoDecisoesDoDiogoTests.DensidadeDoVidro_GravaNnpbEPs_EOsPesosGravadosFicamComAQueUsaram | Densidade do vidro nova não muda os Pesos já gravados | OK |  |
| PesoDecisoesDoDiogoTests.DensidadeDoVidro_ZeroOuNegativaNumDosCampos_NaoGravaNenhuma | Densidade ≤ 0 num dos campos: não grava nenhuma | OK |  |
| PesoDecisoesDoDiogoTests.MudarAEscolha_TiraAAprovacao_EManterNaoVaiParaAprovacao | Mudar a escolha tira a aprovação | OK |  |
| PesoDecisoesDoDiogoTests.PesoGravado_UsaADensidadeDaAguaQueGuardou_NaoADaTabelaCorrigida | Peso gravado usa a densidade da água que guardou | OK |  |
| PesoDecisoesDoDiogoTests.PesoNovo_GravaADensidadeDaAguaQueUsou | Peso novo grava a densidade da água usada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoEscolhasQueryTests.CodigosDeMaquinas_VemPorOrdemDoCodigo | Códigos das máquinas por ordem | DUPLICADO | igual a PegamentosEscolhasQueryTests.CodigosDeMaquinas_VemPorOrdemDoCodigo |
| PesoEscolhasQueryTests.ComparacaoGuardada_SoExisteComOParDeIdentificadoresCerto | Comparação guardada só existe com o par de ids certo | OK |  |
| PesoEscolhasQueryTests.ControloDaProducao_DevolveOControloDessaProducaoESemControloDevolveNulo | Controlo da produção, ou nulo | OK |  |
| PesoEscolhasQueryTests.OsQuatroPedidos_NaoGravamNada | As consultas de escolhas do Peso não gravam nada | FRACO | ChangeTracker vazio não prova que nada foi gravado |
| PesoEscolhasQueryTests.ProducaoDoControlo_DevolveAProducaoEControloInexistenteDevolveNulo | Produção do controlo, ou nulo | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoFolhaQueryTests.AntesDoPrimeiroPeso_MostraFactosDaProducaoECandidatosSemCriarNada | Antes do 1.º Peso mostra factos e candidatos sem criar nada | OK |  |
| PesoFolhaQueryTests.CaloteSimulada_MudaSoAApresentacaoSemRelerTecnicos | Calote simulada só muda a apresentação | OK |  |
| PesoFolhaQueryTests.CancelarOCarregamento_PropagaOCancelamentoSemGravar | Cancelar o carregamento não grava | OK |  |
| PesoFolhaQueryTests.ControloInexistente_DevolveAFolhaVaziaSemInventarContexto | Controlo inexistente dá folha vazia | OK |  |
| PesoFolhaQueryTests.DepoisDaTroca_SemPesoAtual_NaoAbreOUltimoENaoAssociaSozinho | Após troca sem Peso atual não abre nem associa sozinho | OK |  |
| PesoFolhaQueryTests.IdentificadorNaoCanonico_MantemORecursoAQueryDoApoio | Id de ferramenta não canónico mantém o apoio | FRACO | verifica só pedidos ao serviço de apoio (mock), não o efeito visível |
| PesoFolhaQueryTests.PesoAnteriorEscolhido_MantemValoresCongeladosEUsaASuaCmNoApoio | Peso anterior mantém valores congelados e a sua CM | OK |  |
| PesoFolhaQueryTests.PesoDeOutraProducao_NaoSubstituiOPesoDestaProducao | Peso de outra produção não substitui o desta | OK |  |
| PesoFolhaQueryTests.SemEscolhaValida_AbreOPesoAtualDaProducao | Sem escolha válida abre o Peso atual da produção | OK |  |
| PesoFolhaQueryTests.TecnicosEmFalta_NaoImpedemAbrirAFolhaNemInventamValores | Dados técnicos em falta não impedem abrir nem inventam valores | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoPdfGeneratorTests.GenerateBytes_ProducesARealPdf_InMemory | PDF do Peso traz leituras, médias, nominal e desvio | OK |  |
| PesoPdfGeneratorTests.GenerateBytes_ShowsTheGlassDensityWithAllItsDecimals | PDF mostra a densidade do vidro com todas as casas | OK |  |
| PesoPdfGeneratorTests.GenerateBytes_WithoutComparison_StillProducesTheSheet | PDF do Peso sem comparação continua a sair | OK |  |
| PesoPdfGeneratorTests.ProducaoAnterior_DizProducao_ENaoSAP | PDF do Peso diz «Produção anterior», nunca SAP | OK | Decisão Diogo 21/10: nunca «SAP» à vista no Peso (peso-arrumado-pr212) |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoProducaoAnteriorQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoProducaoAnteriorQueryTests.ComInicio_AsUltimas5_EnoMesmoDiaAsRegistadasAntes | Produção anterior: últimas 5, no mesmo dia as registadas antes | OK |  |
| PesoProducaoAnteriorQueryTests.ComInicio_SoAsQueComecaramAntes_SemAsPosterioresNemAsSemDatas | Produção anterior: só as que começaram antes | OK |  |
| PesoProducaoAnteriorQueryTests.SemInicio_AsRegistadasAntes_PrimeiroAsComInicio | Sem início: as registadas antes, primeiro as com início | OK |  |
| PesoProducaoAnteriorQueryTests.SemOutrasAnteriores_ListaVazia | Sem outras anteriores a lista vem vazia | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoQueryTests.FindByControlo_SemPesoAtual_DevolveOMaisRecente | Sem Peso atual devolve o mais recente | OK |  |
| PesoQueryTests.FindPesoAsync_CalculatesRowsAndAveragesFromPersistedInputs | Linhas e médias calculadas a partir das entradas gravadas | OK |  |
| PesoQueryTests.FindPesoAsync_CalculationFrozenAgainstLaterToolEdits | Cálculo congelado contra edições posteriores da ferramenta | OK |  |
| PesoQueryTests.FindPesoAsync_NoMeasurements_BlankAveragesNotZero | Sem leituras as médias ficam em branco, não zero | OK |  |
| PesoQueryTests.FindPesoAsync_UnknownPesoId_ReturnsNull | Peso inexistente devolve nulo | OK |  |
| PesoQueryTests.HistoricalIdentification_PartialLegacyFacts_FallBackPerField | Factos antigos parciais recorrem campo a campo | OK |  |
| PesoQueryTests.HistoricalIdentification_UsesRecordedFacts_WithLegacyFallback | Identificação histórica usa os factos gravados, com recurso antigo | OK |  |
| PesoQueryTests.ListByControlo_DoisPesos_OrdenadosEMarcaOAtual | Dois Pesos ordenados, marca o atual | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoSuposicaoCalculationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoSuposicaoCalculationTests.Calote_Igual_NaoMudaNada | Calote mais baixa tira peso; igual não muda | OK |  |
| PesoSuposicaoCalculationTests.Calote_MaisAlta_TrocaAParteDoPesoQueVemDaCalote | Calote mais alta troca a parte do peso vinda da calote | OK |  |
| PesoSuposicaoCalculationTests.Calote_MaisBaixa_TiraPeso | Calote mais baixa tira peso; igual não muda | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoTampaoDoJobOnTests.DiametroDoJobOn_MandaSobreODaCm | Diâmetro do tampão do Job On manda sobre o da CM | OK |  |
| PesoTampaoDoJobOnTests.FromKnown_UsaODiametroDoJobOn | Apoio conhecido usa o diâmetro do Job On | DUPLICADO | igual a DiametroDoJobOn_MandaSobreODaCm |
| PesoTampaoDoJobOnTests.JobOnSemDiametro_CaiNoDaCmDeArranque | Job On sem diâmetro usa o da CM de arranque | OK |  |
| PesoTampaoDoJobOnTests.SemTampaoEmLado_Nenhum_ReferenciaPorCompletarSemBloquear | Sem tampão em lado nenhum pede para completar, sem bloquear | OK |  |
| PesoTampaoDoJobOnTests.SemTampaoNaCm_OJobOnBasta | Sem tampão na CM basta o do Job On | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoSimuladoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoTampaoSimuladoTests.SoACaloteMuda_OPesoMudaPelaCaloteTampo | Só a calote muda: o peso muda pela Calote Tampo | OK | Memória peso-comparar-tp |
| PesoTampaoSimuladoTests.Texto_DiametroXCalote_SemZerosAMais | Tampão escreve-se «diâmetro x calote» sem zeros a mais | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables | Modelo tem exatamente as tabelas do Peso | FRACO | compara lista de tabelas, não um efeito |
| PesoToolScopedSchemaTests.GlassDensity_ProcessIsPrimaryKey | Uma densidade do vidro por processo | OK |  |
| PesoToolScopedSchemaTests.PesoEntity_AnchorsToolsWithRestrict_MeasurementsCascade | Peso liga às ferramentas sem apagar em cascata | OK |  |
| PesoToolScopedSchemaTests.PesoEntity_HasNoProductionIdentityColumns | Peso não guarda identidade da produção | FRACO | verifica nomes de colunas do modelo, não comportamento |
| PesoToolScopedSchemaTests.ToolTechnicalContext_OneToOneWithTool_Restrict | Dados técnicos 1-para-1 com a ferramenta, sem cascata | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedWorkedExampleTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoToolScopedWorkedExampleTests.WorkedExample_T3_NoJobOn_P30ConsultableFromT3 | Exemplo: Peso sem Job On criado e consultado pela CM | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoWaterDensityTableTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoWaterDensityTableTests.Resolve_MatchesTheOfficialCetieDt9Table | Tabela da água igual à oficial CETIE DT9 | OK |  |
| PesoWaterDensityTableTests.Resolve_OutsideSupportedRange_ReturnsNull | Fora de 5–35 °C não há densidade | OK |  |
| PesoWaterDensityTableTests.Resolve_ReturnsCanonicalDensityForWholeDegree | Densidade da água para graus inteiros | OK |  |
| PesoWaterDensityTableTests.RoundToWholeDegree_RoundsMeasuredTemperature | Temperatura arredonda ao grau (limites) | OK |  |
| PesoWaterDensityTableTests.RoundToWholeDegree_ThenResolve_UsesExactRowNoInterpolation | Sem interpolação: usa a linha do grau arredondado | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PostoMinutosSettingTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostoMinutosSettingTests.ForaDasOpcoes_ERecusado | Minutos fora das opções são recusados, nada gravado | OK |  |
| PostoMinutosSettingTests.OsMinutosLidos_SemValorValidoSao3 | Sem valor válido os minutos são 3 | OK |  |
| PostoMinutosSettingTests.UmaDasOpcoes_Grava | Minutos do posto: 3, 10 ou 30 gravam | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesHandlerTests.EmptyProcess_FallsBackToTheNnOfTheCmTechnicalName | Sem processo usa o NN do nome técnico da CM | OK |  |
| PuncoesHandlerTests.InvalidInput_IsRefused_AndNothingIsWritten | Entrada inválida é recusada e nada é gravado | OK |  |
| PuncoesHandlerTests.NnpbProduction_RegistersPunches_SeenByDefault_WithLotAndAlteracao | Punções NNPB registam-se vistas, com lote e alteração | OK |  |
| PuncoesHandlerTests.NumeroRepetido_ComOLoteDessaPuncao_RegistaComEsseLote_EOLoteDeCimaFica | Número repetido com lote próprio regista esse lote | OK |  |
| PuncoesHandlerTests.Pdf_StandaloneRegister_SaysSemJobOn | PDF das punções sem Job On diz «Sem Job On» | OK |  |
| PuncoesHandlerTests.PsOrUnknownProcess_IsNotNnpb_AndCannotStartARegisto | Produção PS não é NNPB e não abre registo de punções | OK |  |
| PuncoesHandlerTests.RepeatedClicks_StartTheRegisterAndRegisterThePunchOnlyOnce | Cliques repetidos registam a punção uma só vez | FRACO | «nada repetido» só em memória; falta teste PostgreSQL com duas pessoas ao mesmo tempo |
| PuncoesHandlerTests.ResolveNnpb_FichaWinsOverTheTechnicalName | O processo da ficha manda sobre o nome técnico; PDF da PU | OK |  |
| PuncoesHandlerTests.SeenMarks_AndRemove_UpdateTheTotals | Vistos e remover atualizam os totais | OK |  |
| PuncoesHandlerTests.Seen_SemValor_TrocaOEstadoQueLaEsta | Vista sem valor troca o estado atual | OK |  |
| PuncoesHandlerTests.StandaloneRegister_IsAssociatedByHand_ToAnNnpbProductionWithoutRegister | Registo solto associa-se à mão a produção NNPB | OK |  |
| PuncoesHandlerTests.StandaloneRegister_IsNotAssociated_ToAPsProduction | Registo solto não associa a produção PS | OK |  |
| PuncoesHandlerTests.WithoutJobOn_RegisterStandsAlone_ListedByReference | Registo de punções sem Job On, listado por referência | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesPdfReservaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPdfReservaTests.Pdf_ComAReservaCheia_GeraSe_ComOsNumerosInteiros | PDF das punções com reserva cheia gera com números inteiros | OK | Decisão Diogo 27/10 (puncoes-gota) |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RecordPegamentosMeasurementHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| RecordPegamentosMeasurementHandlerTests.Completion_NeverOverwritesExistingToolValue | Gargalo da ferramenta nunca é sobrescrito | OK |  |
| RecordPegamentosMeasurementHandlerTests.ComponentNotInProduction_MFRefusesTyped_ToolNeverInvented | Leitura de Pegamentos recusada (controlo, componente, MF, gargalo): nada gravado | OK |  |
| RecordPegamentosMeasurementHandlerTests.ConfiguredTolerance_FrozenAtFirstMeasurement_LaterConfigChangeDoesNotRewrite | Tolerância congela na 1.ª leitura | OK |  |
| RecordPegamentosMeasurementHandlerTests.ControloNotFound_RefusesTyped_NoWrites | Leitura de Pegamentos recusada (controlo, componente, MF, gargalo): nada gravado | OK |  |
| RecordPegamentosMeasurementHandlerTests.Correction_ToZeroOrLess_IsImpossible_ReadingKeepsItsValues | Correção para ≤ 0 recusada | DUPLICADO | igual a CosturaOuContraZeroOuMenosNaCorrecao_ERecusada_ALeituraFicaIgual |
| RecordPegamentosMeasurementHandlerTests.CosturaOrContraZeroOrLess_IsImpossible_NothingWritten | Costura ou contra costura ≤ 0 recusada | DUPLICADO | coberto por CosturaOuContraZeroOuMenosNaPrimeiraLinha_ERecusada_NadaFicaGravado (que também verifica a folha) |
| RecordPegamentosMeasurementHandlerTests.CosturaOuContraDe001NaCorrecao_EAceite_ALeituraFicaComOValorNovo | Correção de 0,01 aceite | OK |  |
| RecordPegamentosMeasurementHandlerTests.CosturaOuContraDe001NaPrimeiraLinha_EAceite_FicaGravada | Costura ou contra de 0,01 é aceite | OK |  |
| RecordPegamentosMeasurementHandlerTests.CosturaOuContraZeroOuMenosNaCorrecao_ERecusada_ALeituraFicaIgual | Correção ≤ 0 recusada, leitura igual | OK |  |
| RecordPegamentosMeasurementHandlerTests.CosturaOuContraZeroOuMenosNaPrimeiraLinha_ERecusada_NadaFicaGravado | Costura ou contra ≤ 0 na 1.ª linha recusada, nada gravado | OK |  |
| RecordPegamentosMeasurementHandlerTests.HistoricalIdentity_UsesFrozenFacts_AndLegacyRecordsFallBackToCatalog | Identidade histórica usa factos congelados | OK |  |
| RecordPegamentosMeasurementHandlerTests.MissingGargaloWithoutCompletion_IsRefused_NothingWritten | Leitura de Pegamentos recusada (controlo, componente, MF, gargalo): nada gravado | OK |  |
| RecordPegamentosMeasurementHandlerTests.MixedAxes_AveragesUseActualRowResults_AndPreserveIndividualAlerts | Médias com eixos mistos e alertas individuais | OK |  |
| RecordPegamentosMeasurementHandlerTests.OvalizacaoAboveTolerance_CountsAsAlert_InTheSheetAndThePdf | Ovalização acima da tolerância conta como alerta | OK |  |
| RecordPegamentosMeasurementHandlerTests.RegistroFreezesCombinationToolsFromContextAtCreation | Folha congela as ferramentas pelo id na criação | OK | Decisão Diogo 28/10: ligar pelo tool_id |
| RecordPegamentosMeasurementHandlerTests.SameControlo_ReusesSingleRegistro_AppendOnlyKeepsEarlierRows | Mesma combinação reutiliza a folha e acrescenta linhas | OK |  |
| RecordPegamentosMeasurementHandlerTests.UnknownComponentToken_RefusesTyped_NoWrites | Leitura de Pegamentos recusada (controlo, componente, MF, gargalo): nada gravado | OK |  |
| RecordPegamentosMeasurementHandlerTests.WithoutConfiguredTolerance_DefaultOwnerToleranceApplies | Sem tolerância configurada vale 0,20 | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RecordPesoMeasurementHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| RecordPesoMeasurementHandlerTests.PesoIdAntigo_Recusa_PesoNotCurrent | Peso antigo recusa leituras novas | OK |  |
| RecordPesoMeasurementHandlerTests.PesoSemProducao_ContinuaAGravar | Peso sem produção continua a gravar | OK |  |
| RecordPesoMeasurementHandlerTests.Record_AppendOnly_NeverRewritesExistingMeasurements | Leituras antigas nunca são reescritas | OK |  |
| RecordPesoMeasurementHandlerTests.Record_AppendsHistoricalMeasurementWithBackendCalculation | Leitura grava capacidade e peso calculados no servidor | OK |  |
| RecordPesoMeasurementHandlerTests.Record_CalculationUsesPesoInputsEvenIfToolContextChanges | Cálculo usa as entradas congeladas do Peso | OK |  |
| RecordPesoMeasurementHandlerTests.Record_DoesNotModifyPesoOrTools | Registar leitura não mexe no Peso nem nas ferramentas | OK |  |
| RecordPesoMeasurementHandlerTests.Record_GlassWeightNotPositive_IsRefused_NoRowSaved | Peso de vidro ≤ 0 recusado, nada gravado | OK |  |
| RecordPesoMeasurementHandlerTests.Record_NullCmSubject_PersistedAsEmptyRowData | Leitura sem nº de CM grava vazio | OK |  |
| RecordPesoMeasurementHandlerTests.Record_ObservedQuantityRegisteredInFull_NoSignConstraints | Massa de água registada tal como lida | FRACO | só confere um valor positivo normal; não prova a ausência de restrição de sinal que o nome promete |
| RecordPesoMeasurementHandlerTests.Record_SequentialRowPositionsByRegistrationOrder | Posições das leituras por ordem de registo | OK |  |
| RecordPesoMeasurementHandlerTests.Record_SeveralLinesOfOneSubmit_SavedTogetherAfterTheExistingOnes | Várias linhas de um envio gravam depois das existentes | OK |  |
| RecordPesoMeasurementHandlerTests.Record_UnknownPeso_Refused | Peso inexistente recusa leitura | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RevisaoDepoisDaAprovacaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| RevisaoDepoisDaAprovacaoTests.UmPesoNovoDepoisDeAprovarOPeso_TemAprovacaoPropria_EAParteVoltaAPorAprovar | Peso novo após aprovar volta a Por aprovar | DUPLICADO | igual a ControloAprovacaoTests.UmPesoNovoDepoisDeAprovar_FicaPorAprovar |
| RevisaoDepoisDaAprovacaoTests.UmPesoNovoDepoisDeNaoAprovar_ContinuaNaoAprovado | Peso novo após Não aprovar continua Não aprovado | OK |  |
| RevisaoDepoisDaAprovacaoTests.UmaFolhaNovaDepoisDeAprovar_ContinuaAprovado | Folha de Pegamentos nova não tira a aprovação | DUPLICADO | igual a ControloAprovacaoTests.UmaFolhaNovaDepoisDeAprovarOsPegamentos_ContinuaAprovado |
| RevisaoDepoisDaAprovacaoTests.UmaLeituraDosPegamentosCorrigidaOuApagadaDepoisDaAprovacao_MantemAAprovacao | Pegamentos corrigidos ou apagados mantêm a aprovação | OK |  |
| RevisaoDepoisDaAprovacaoTests.UmaLeituraRegistadaDepoisDaAprovacao_MantemAAprovacao_EFicaComAData | Leitura nova após aprovar mantém a aprovação com data | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/SaveModuleSettingHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasDayChangeTests.AtDayChangeHour_TheNewDayBegins | Dia operacional das Boquilhas muda à hora definida | OK | Fora da área do Controlo (Boquilhas) |
| BoquilhasDayChangeTests.BeforeDayChangeHour_StillYesterdaysOperationalDay | Dia operacional das Boquilhas muda à hora definida | OK | Fora da área do Controlo (Boquilhas) |
| BoquilhasDayChangeTests.LateNight_BelongsToTheSameOperationalDay | Dia operacional das Boquilhas muda à hora definida | OK | Fora da área do Controlo (Boquilhas) |
| BoquilhasDayChangeTests.Midnight_WithZeroChangeHour_BehavesAsCalendarDay | Dia operacional das Boquilhas muda à hora definida | OK | Fora da área do Controlo (Boquilhas) |
| SaveModuleSettingHandlerTests.Save_BoquilhasDayChangeHour_InvalidValue_Refuses | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_BoquilhasDayChangeHour_ValidHour_Saves | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_MachineRoute_EmptyValue_ClearsTheRoute | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_MachineRoute_ExistingRepairerId_Saves | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_MachineRoute_MachineOutsideTheSixCodes_RefusesUnknownKey | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_MachineRoute_MalformedValue_RefusesTyped | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_MachineRoute_MissingRepairerId_RefusesTyped | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_RetiredControloOutputDirectoryKey_IsUnknown_AndNeverTouchesTheDisk | Chave antiga da pasta de saída do Controlo é desconhecida | OK |  |
| SaveModuleSettingHandlerTests.Save_Twice_Upserts_TheSameKey | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |
| SaveModuleSettingHandlerTests.Save_UnknownKey_Refuses | Definições do módulo (hora de mudança de dia, rota de reparador): grava ou recusa | OK | Fora da área do Controlo (Boquilhas/Reparação) |

### `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EnvioEmailPesoPageTests.Email_OMesmoEnvioDevolveOMesmoFicheiroERegistaUmaVez_OutraChavePreparaOutro | O mesmo envio regista uma só vez | FRACO | «nada repetido» em memória, sem índice único do PostgreSQL |
| EnvioEmailPesoPageTests.Email_SemEspacoParaNovosFicheiros_NaoPreparaOutro_MasPermiteRepetirOPrimeiro | Sem espaço não prepara outro email mas repete o primeiro | OK |  |
| EnvioEmailPesoPageTests.Lista_RepetirARemocaoNaoRetiraAPessoaQuePassouParaAMesmaPosicao | Repetir a remoção não retira outra pessoa | OK |  |
| EnvioEmailPesoPageTests.Lista_SemAVersaoDoFormularioNaoGrava | Lista sem a versão do formulário não grava | OK |  |
| EnvioEmailPesoPageTests.Lista_UmFormularioAntigoNaoSubstituiAAlteracaoDeOutraAba | Formulário antigo não substitui a alteração de outra aba | FRACO | duas pessoas ao mesmo tempo só testado em memória; falta PostgreSQL (regra 7) |

### `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.SemBotoesNasLinhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EnvioEmailPesoPageTests.Modelos_UmSoModelo_VemEscolhido | Com um só modelo a linha vem escolhida | FRACO | verifica atributos e classes HTML (class="selected", checked), não o efeito |
| EnvioEmailPesoPageTests.Modelos_UmaLinhaEscolhe_EUmSoAbrirNoCanto | Modelos de email: sem botões nas linhas; Abrir e Apagar no canto, Apagar em último | OK | Corrigido em DMO-580 (#285). |

### `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EnvioEmailPesoPageTests.Definicoes_ColarComUmEmailErrado_AvisaENaoOGrava | Colar com email errado avisa e não o grava | OK |  |
| EnvioEmailPesoPageTests.Definicoes_ColarListaDoOutlook_GuardaNomesEEmails | Colar lista do Outlook guarda nomes e emails | OK |  |
| EnvioEmailPesoPageTests.Definicoes_CriarEMudarUmModelo_ERemetenteQueNaoEEmailNaoGrava | Criar e mudar modelo; remetente inválido não grava | OK |  |
| EnvioEmailPesoPageTests.Definicoes_CriarLista_GuardaOsEmails_EAvisaDosQueNaoSaoEmails | Criar lista guarda emails e avisa dos inválidos | OK |  |
| EnvioEmailPesoPageTests.Definicoes_JuntarAUmaLista_UmEmailQueJaLaEsta_NaoDuplica_EAvisa | Juntar email repetido não duplica e avisa | OK |  |
| EnvioEmailPesoPageTests.Definicoes_ListaSemNenhumEmail_NaoGrava_EONomeDeUmaQueExisteJuntaAPessoa | Lista sem emails não grava; nome existente junta a pessoa | OK |  |
| EnvioEmailPesoPageTests.Definicoes_ListasDeAntes_SemNomes_MostramSeFechadasComOTraco | Listas antigas sem nomes aparecem fechadas com traço | FRACO | verifica CSS inline (.ctl-def-listas{display:grid...}) e classes em vez do efeito |
| EnvioEmailPesoPageTests.Definicoes_MudarOsEmailsNaLinha_GravaEDevolveOValor | Mudar o email na linha grava e devolve o valor | OK |  |
| EnvioEmailPesoPageTests.Definicoes_PessoaDaLista_MudarNomeEEmail_Tirar_EJuntar | Mudar, tirar e juntar pessoas da lista de emails | OK |  |
| EnvioEmailPesoPageTests.Enviar_ComPesoNoAssunto_OAssuntoDoEmailTrazOPesoMedioDaProducao | Assunto do email traz o peso médio da produção | OK |  |
| EnvioEmailPesoPageTests.Enviar_DevolveOEmailParaOOutlook_ComALista_OAssunto_OCorpo_EOPdf_EFicaORegisto | Enviar devolve .eml com lista, assunto, corpo, PDF e regista o envio | OK |  |
| EnvioEmailPesoPageTests.Enviar_SemListaOuSemModelo_VoltaAoResumo_SemEmailNemRegisto_EDizPorque | Sem lista ou modelo não prepara email e diz porquê | OK |  |
| EnvioEmailPesoPageTests.Enviar_SemPeso_NaoPreparaEmail | Sem Peso não prepara email | OK |  |
| EnvioEmailPesoPageTests.Enviar_SoApareceComPeso_ESemModelosAvisaComLinkParaAsDefinicoes | «Enviar» só aparece com Peso; sem modelos avisa | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PegamentosSemJobOnPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosSemJobOnPageTests.AssociarJobOn_TrocaSoAsZonasDaEscolha_ComOBotaoNaMesmaLinha | Associar Job On troca só as zonas da escolha | FRACO | verifica atributos e classes do HTML |
| PegamentosSemJobOnPageTests.Associar_ComOutraCm_Recusa_ENadaMuda | Associar com outra CM é recusado e nada muda | OK |  |
| PegamentosSemJobOnPageTests.Associar_PegamentosJaAssociadosAOutraProducao_RecusaComMensagemNoCanto | Pegamentos já associados a outra produção: recusa | OK |  |
| PegamentosSemJobOnPageTests.Associar_ProducaoQueJaTemPegamentos_Recusa | Produção que já tem Pegamentos recusa associar | OK |  |
| PegamentosSemJobOnPageTests.EscolherJobOn_ReferenciaEmMinusculas_EncontraAsProducoes | Referência em minúsculas encontra as produções | OK |  |
| PegamentosSemJobOnPageTests.EscolherJobOn_TrocaSoAsZonas_NumaLinha | Escolher Job On troca só as zonas | FRACO | verifica classes (dmo-filter-bar, dmo-field-producao) e atributos HTML, não o efeito |
| PegamentosSemJobOnPageTests.Novos_SemFerramentas_Recusa_EAssociarAbrePeloControloId | Pegamentos sem ferramentas recusam e nada se cria | OK |  |
| PegamentosSemJobOnPageTests.SemJobOn_CriaMedeEAssociaMaisTarde | Pegamentos sem Job On: criar, medir e associar depois | OK |  |
| PegamentosSemJobOnPageTests.SemJobOn_CriarRecusado_VoltaComOLoteEscolhido | Criar recusado volta com o lote escolhido, nada criado | OK |  |
| PegamentosSemJobOnPageTests.SemJobOn_LoteQueNaoEDaReferencia_NaoConta | Lote de outra referência não conta | OK |  |
| PegamentosSemJobOnPageTests.SemJobOn_ReferenciaComVariosLotes_EscolheOLoteEAssocia | Referência com vários lotes: escolhe-se o lote da BQ | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoCapSupportPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoCapSupportPageTests.CapSupportEndpoint_DevolveOCalculoDoBackend | Endpoint do apoio devolve o cálculo do servidor | OK |  |
| PesoCapSupportPageTests.CapSupportEndpoint_SemCalote_UsaACaloteDoJobOn | Endpoint sem calote usa a do Job On | OK |  |
| PesoCapSupportPageTests.CapSupportEndpoint_SemTP_TemReferenciaFalse | Endpoint sem TP diz que não há referência | OK |  |
| PesoCapSupportPageTests.ProductionMode_PainelDeFactos_MostraVolumesDensidadeEApoioComACaloteDoJobOn | Peso da produção mostra volumes, densidade e apoio da calote | OK |  |
| PesoCapSupportPageTests.ProductionMode_SemCaloteNoJobOn_UsaAReferenciaDaTool | Sem calote no Job On usa a da ferramenta | OK |  |
| PesoCapSupportPageTests.ProductionMode_SemTP_ReferenciaPorCompletarEPesoIntacto | Sem TP pede para completar e o Peso fica intacto | OK |  |
| PesoCapSupportPageTests.ProductionMode_TampaoSoNaFolhaDoJobOn_LeODaFolha | Tampão só na folha do Job On lê o da folha | OK | Memória peso-arrumado-pr212 (DMO-564) |
| PesoCapSupportPageTests.SimulacaoIlegivel_AvisoHonestoSemBloqueio | Simulação ilegível avisa sem bloquear | OK |  |
| PesoCapSupportPageTests.SimulacaoPorGet_RecalculaPeloBackendEAbreOApoio | Simular calote recalcula no servidor | OK |  |
| PesoCapSupportPageTests.ToolScopedMode_ApoioSemProducao_UsaAReferenciaDaTool | Apoio sem produção usa a referência da ferramenta | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoFormularioTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoFormularioTests.AsLinhasEmBrancoIgnoramSe_ASemMassaEAIlegivelFicamDeFora_AsOutrasComparamSe | Linhas em branco ignoram-se; sem massa ou ilegíveis ficam de fora | OK |  |
| PesoComparacaoFormularioTests.AsQueOBackendDeixaDeFora_ApontamALinhaDoFormulario | Leituras de fora apontam à linha do formulário | OK |  |
| PesoComparacaoFormularioTests.OAvisoAtravessaOPrgDoGuardar | O aviso atravessa o redirecionamento do Guardar | OK |  |
| PesoComparacaoFormularioTests.OPesoInicial_OPedidoSeEDaProducao_SenaoODasFerramentasAtuais_SenaoOMaisRecente | Peso inicial: o pedido, senão o das ferramentas atuais, senão o mais recente | OK |  |
| PesoComparacaoFormularioTests.SemNadaAComparar_ERecusadoComOMotivo | Sem nada a comparar é recusado com o motivo | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CmDeParte.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoPageTests.AprovarNaComparacao_SoQuemTemControloAprovar_UmaCmDeCadaVez_EVoltaAComparacao | Só quem tem Controlo Aprovar aprova a CM posta de parte | OK |  |
| PesoComparacaoPageTests.ComparacaoGuardada_ColocarDeParte_GravaPorCm_FicaPorAprovar_EVoltaAComparacao | Pôr CM de parte grava por CM e fica por aprovar | OK |  |
| PesoComparacaoPageTests.CompararPesos_ComADensidadeMudadaNasDefinicoes_AvisaNaoComparavel_ComLink_SemRecusar | Densidade mudada: avisa não comparável com link, sem recusar | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.EnvioUnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoPageTests.ComparacaoTab_CompararEGuardarTrocamSoAsZonas | Comparar e Guardar trocam só as zonas | OK |  |
| PesoComparacaoPageTests.GuardarComparacao_SameEnvioAfterTheRepeatWindow_SavesOnceAndAnswersWithTheSameRedirect | O mesmo envio de Guardar comparação grava uma só vez | FRACO | «nada repetido» em memória; falta PostgreSQL |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.HistoricoControlo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoPageTests.Historico_AProducaoEscolhida_FicaAMesmaQuandoAReferenciaSeAlarga | Produção escolhida mantém-se ao alargar a referência | OK |  |
| PesoComparacaoPageTests.Historico_ODropdownDasProducoes_TemARegraDaTabelaEAReferenciaDeCadaProducao | Dropdown de produções do Histórico com a referência de cada uma | OK |  |
| PesoComparacaoPageTests.Historico_PesosEPegamentos_ComADataDoRegisto_ESemJobOnAReferenciaEADataFiltram | Histórico: data do registo, sem Job On filtra por referência e data | OK |  |
| PesoComparacaoPageTests.Historico_UmaProducaoMaisAntigaPedidaPorLink_ContinuaAFiltrarEEntraNoDropdown | Produção antiga pedida por link filtra e entra no dropdown | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.NoResumo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoPageTests.Comparacao_AbreNoResumoDaProducao_SemSeparadorProprio | Comparação abre no Resumo, sem separador próprio | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.ProducaoFixa.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoPageTests.PeloResumo_AProducaoFicaFixa_SemReferenciaNemProducao | Pelo Resumo a produção fica fixa | OK | Decisão Diogo 27/10 05:56 (peso-comparar-tp) |
| PesoComparacaoPageTests.ProducaoFixa_SegueNoCompararENoGuardar | A produção fixa segue no Comparar e no Guardar | DUPLICADO | mesma regra de PeloResumo_AProducaoFicaFixa_SemReferenciaNemProducao, só continua para Comparar/Guardar |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.UltimasProducoes.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoPageTests.Comparacao_AsProducoesPeloInicioDaReferencia_CadaUmaPeloJobOnId | Produções pelo início da referência, cada uma pelo Job On id | OK |  |
| PesoComparacaoPageTests.Folha_AProducaoACompararTemAsUltimas5Anteriores_EAPedidaFica | Comparar: últimas 5 produções anteriores e a pedida | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoComparacaoPageTests.CamposDecimais_SaoTextoComTecladoDecimal_ParaAceitarVirgula | Campos decimais aceitam vírgula (texto com teclado decimal) | FRACO | verifica atributos HTML (type, inputmode, placeholder), não que a vírgula é aceite |
| PesoComparacaoPageTests.ComparacaoTab_ATerraPreselecionadoPelaFolha | Comparação abre com a CM e o Peso inicial da folha | FRACO | sobretudo textos de rótulos e botões do HTML (+ e −), poucas regras |
| PesoComparacaoPageTests.ComparacaoTab_EscolherAReferenciaEAProducao_TrocaSoAsZonas | Escolher referência e produção troca só as zonas | FRACO | verifica atributos e ids de zonas HTML |
| PesoComparacaoPageTests.ComparacaoTab_MostraBaseEEfeitoNaMedia | Comparação mostra a base e o efeito na média | FRACO | verifica class peso-linha-destaque e atributos data-cmp; só alguns números têm valor |
| PesoComparacaoPageTests.CompararPesos_CalculaAoVivoSemGravar | Comparar calcula ao vivo sem gravar | OK |  |
| PesoComparacaoPageTests.CompararPesos_LeiturasMasFicamDeForaComAviso_AsOutrasComparam | Comparar: leituras inválidas ficam de fora com aviso | OK |  |
| PesoComparacaoPageTests.DoisPassos_SemReferenciaNadaCarrega_ComReferenciaSoAsDela | Sem referência nada carrega; com referência só as dela | OK |  |
| PesoComparacaoPageTests.Folha_ComComparacao_PrevisualizarNaoAlteraPesosGuardadosNemPdf | Pré-visualizar não altera Pesos nem PDF | OK |  |
| PesoComparacaoPageTests.Folha_OsFormulariosLevamAComparacaoEOSupomos_ERecusaDoSupomosNoCanto | Formulários levam Comparação e Supomos; recusa no canto | FRACO | verifica sobretudo regex de HTML (zonas e inputs ocultos) |
| PesoComparacaoPageTests.Folha_ProducaoComCmEBq_NaoMostraOAvisoDeFerramentas | Produção com CM e BQ não mostra aviso de ferramentas | OK |  |
| PesoComparacaoPageTests.Folha_ProducaoSemCmOuBq_ExplicaPorQueNaoHaNovoPeso | Produção sem CM ou BQ explica porque não há novo Peso | OK |  |
| PesoComparacaoPageTests.GuardarComparacao_CliquesRepetidos_UmSoRegistoESemMensagem | Cliques repetidos em Guardar dão um só registo | FRACO | «nada repetido» em memória, sem PostgreSQL; duplica GuardarComparacao_SameEnvioAfterTheRepeatWindow |
| PesoComparacaoPageTests.GuardarComparacao_CriaORegistoDoHistorico | Guardar comparação cria o registo do Histórico | OK |  |
| PesoComparacaoPageTests.GuardarComparacao_ExisteOModuloControloCriar | Guardar comparação exige o módulo Controlo Criar | OK |  |
| PesoComparacaoPageTests.GuardarComparacao_GravaAsLeiturasUtilizaveisEAvisaAsDeFora | Guardar grava as leituras boas e avisa as de fora | OK |  |
| PesoComparacaoPageTests.Historico_Filtros_TrocamSoOsResultadosEAsProducoes | Filtros do Histórico trocam só resultados e produções | FRACO | verifica atributos e ids de zonas HTML, não o efeito |
| PesoComparacaoPageTests.Historico_ProcuraUnificadaPelosQuatroTipos | Histórico procura Resumo, Peso, Pegamentos e Comparação | OK |  |
| PesoComparacaoPageTests.Historico_SemJobOnAssociado_EncontraOPesoSemProducao | Histórico encontra o Peso sem Job On, por associar | OK |  |
| PesoComparacaoPageTests.PesoAbertoPeloHistorico_ComparaNaFolhaESupoeCaloteSemGravar | Peso do Histórico compara e supõe calote sem gravar | OK | Controlo anterior vem escolhido (27/10 16:42, peso-arrumado-pr212) |
| PesoComparacaoPageTests.RecusasTipadas_PreservamOFormulario | Recusas preservam o formulário e não gravam | FRACO | vários campos inválidos no mesmo teste (regra 4) |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoCompararPageTests.CaloteQueNaoSeLe_Avisa | Calote ilegível avisa e não simula | OK |  |
| PesoCompararPageTests.CaloteSimulada_MudaSoEsseLadoPelaCaloteTampo_ENaoGrava | Calote simulada muda só esse lado e não grava | OK | Decisão Diogo 27/10 (peso-comparar-tp) |
| PesoCompararPageTests.DoHistorico_PelaReferenciaDaCm_EOutraMaquina | Comparar a partir do Histórico pela referência da CM | FRACO | sobretudo ids e links no HTML; pouco efeito |
| PesoCompararPageTests.DoPeso_ListaOsCompativeis_EComparaComOSeguinte | Comparar peso lista CM compatíveis (mesma referência e máquina) | OK | Decisão Diogo 27/10 (peso-comparar-tp) |
| PesoCompararPageTests.OPesoEOHistorico_TemOBotao_EOPainelLateralAbreAPagina | O Peso e o Histórico têm «Comparar peso» que abre no painel | FRACO | só verifica a existência de links e uma string de JS, não o fluxo |
| PesoCompararPageTests.OutraMaquina_ESemOutroPeso_DizQueNaoHaComQueComparar | Sem outro Peso na máquina diz que não há com que comparar | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaCarregamentoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoFolhaCarregamentoPageTests.AbrirProducao_MostraCriacaoEAssociacaoSemGravar | Abrir a produção mostra criar e associar sem gravar | OK |  |
| PesoFolhaCarregamentoPageTests.RecusaDeCriacao_PreservaATemperaturaEscritaEAMensagem | Criação recusada preserva a temperatura e diz porquê | OK |  |
| PesoFolhaCarregamentoPageTests.SemSessaoOuSemModulo_NaoExecutaOCarregamentoDaFolha | Sem sessão ou sem módulo a folha não carrega | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.AssociarJobOn.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.CriarControloAnterior_SemJobOnOuNumJobOnAnterior | «Criar» o Controlo anterior sem Job On ou num Job On anterior | OK | Decisão Diogo 28/10 (DMO-565) |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.Sap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_MostraAMediaPelosDiasEAModaDoSap_EOCampoVemComAMedia | Produção anterior mostra Média e Moda do SAP com diferenças | OK | Decisão Diogo 28/10 (peso-arrumado-pr212): Média e Moda, nunca «SAP» à vista |
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemOrdensDoSap_DizQueOSapAindaNaoDeuOPeso | Sem ordens do SAP diz que ainda não há peso | OK |  |
| PesoFolhaProducaoAnteriorPageTests.Resumo_ProducaoAnteriorComAMediaEAModa | Resumo mostra Produção anterior com Média e Moda | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.Folha_GravaSemRecarregar_ZonasTrocadasELeituraNovaQueFica | Gravar não recarrega: troca só as zonas | FRACO | verifica classes e atributos HTML das zonas (dmo-card, peso-compare), não o efeito |
| PesoFolhaProducaoAnteriorPageTests.Folha_PrimeiraTabelaTemVolumeEPesoPorCm_EAComparacaoFicaAbaixo | Primeira tabela com volume e peso por CM; comparação abaixo | OK |  |
| PesoFolhaProducaoAnteriorPageTests.Folha_TrocaParcial_ForaDasZonasNadaMuda_EOsFormulariosLevamAComparacao | Troca parcial só mexe nas zonas; formulários levam a comparação | FRACO | sobretudo inputs ocultos e classes no HTML |
| PesoFolhaProducaoAnteriorPageTests.Gravar_VoltaComAComparacaoEOSupomos_TambemNoPesoAbertoPeloHistorico | Gravar volta com a comparação e o Supomos | OK |  |
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_AsUltimas5_EAGuardadaForaDelasFicaEscolhida | Últimas 5 produções anteriores; a guardada fica escolhida | OK |  |
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PesoIlegivel_RecusaComEcho_ENaoGuarda | Peso da produção anterior ilegível: recusa e não guarda | OK |  |
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_PropoeAMaisRecente_GuardaOPesoDoSap_EMostraODelta | Produção anterior propõe a mais recente e guarda a ligação | OK |  |
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SemAnteriores_AGuardadaContinuaNoSelect | Sem anteriores a guardada continua no select | OK |  |
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SoAsAnteriores_SemAPosteriorNemASemDatas | Produção anterior só entre as anteriores, com datas | DUPLICADO | mesma regra de PesoProducaoAnteriorQueryTests.ComInicio_SoAsQueComecaramAntes_SemAsPosterioresNemAsSemDatas |
| PesoFolhaProducaoAnteriorPageTests.RecusaDaLeitura_DeixaAMarcaNaZonaDasLeituras | Leitura recusada deixa a marca na zona das leituras | OK |  |
| PesoFolhaProducaoAnteriorPageTests.RegistarLeitura_UmaLinhaIlegivel_NaoGravaNenhuma | Uma linha ilegível: não grava nenhuma | OK |  |
| PesoFolhaProducaoAnteriorPageTests.RegistarLeitura_VariasLinhas_GravaTodasEVoltaAFolhaDaProducao | Várias linhas gravam todas e voltam à folha | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoJobOnEscolhaPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoJobOnEscolhaPageTests.AssociarJobOn_AbrePeloControloId_ComAsFerramentasDoJobOn | Associar Job On abre o Peso da produção sem gravar | OK |  |
| PesoJobOnEscolhaPageTests.AssociarJobOn_EscolherTrocaSoAsZonas_NumaLinha | Escolher Job On troca só as zonas | FRACO | verifica classes e atributos HTML |
| PesoJobOnEscolhaPageTests.AssociarJobOn_SemEscolhaOuInexistente_VoltaComRecusa | Sem escolha ou Job On inexistente: recusa, nada criado | OK |  |
| PesoJobOnEscolhaPageTests.JobOnIdSo_ComControlo_AbreOPesoDaProducao | Job On com controlo abre o Peso da produção | OK |  |
| PesoJobOnEscolhaPageTests.JobOnIdSo_SemControlo_AbreOResumoDaProducao_SemCriarNadaNoPeso | Job On sem controlo abre o Resumo sem criar nada | OK |  |
| PesoJobOnEscolhaPageTests.NovoPeso_MostraAsDuasFormas_SemJobOnPorOmissao | Novo Peso: Sem Job On ou Associar Job On | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoPorCombinacaoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoPorCombinacaoPageTests.ComDoisPesos_OResumoARevisaoEODetalheMostramOsDois | Com dois Pesos Resumo, Revisão e detalhe mostram os dois | OK |  |
| PesoPorCombinacaoPageTests.CriarDepoisDaTroca_AbreOPesoNovo_CopiaAProducaoAnterior_EOAnteriorFicaSemLeituraNova | Criar após troca abre o Peso novo; o antigo não recebe leituras | OK |  |
| PesoPorCombinacaoPageTests.DepoisDeTrocarACm_AFolhaPropoeCriarOPesoNovo_EDizPorque_SemGravarNada | Trocar a CM propõe Peso novo e diz porquê, sem gravar | OK |  |
| PesoPorCombinacaoPageTests.LeituraNoPesoAnterior_ERecusada_ENadaEGravado | Leitura no Peso antigo é recusada e nada é gravado | OK |  |
| PesoPorCombinacaoPageTests.PdfCompleto_ComUmPesoSemLeituras_DeixaOVazioDeFora | PDF completo deixa de fora o Peso sem leituras | FRACO | só compara tamanhos de PDF; devia ler o texto (regra 12) |
| PesoPorCombinacaoPageTests.PdfDoSegundoPeso_LevaMenos2_OPrimeiroFicaComONomeDeSempre | PDF do 2.º Peso leva «-2» no nome | OK |  |
| PesoPorCombinacaoPageTests.TrocarABq_PedePesoNovo_EAssociarUmPesoDeOutraBqERecusadoComARazao | Trocar a BQ pede Peso novo; associar Peso de outra BQ recusa | OK |  |
| PesoPorCombinacaoPageTests.UmSoPeso_AFolhaEOsEcrasDeRevisaoFicamComoSempre | Com um só Peso a folha e a revisão ficam como sempre | OK |  |
| PesoPorCombinacaoPageTests.VoltarACmAnterior_ReabreOPesoDela | Voltar à CM anterior reabre o Peso dela | OK |  |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoSoParaComparacaoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoSoParaComparacaoPageTests.CriarDaComparacao_LigaOPesoAoAtual_MostraAComparacao_ENuncaPedeJobOn | Peso só para comparação nunca pede Job On | OK |  |
| PesoSoParaComparacaoPageTests.PesoSoParaComparacao_AssociarAUmJobOn_ERecusado | Peso só para comparação não se associa a Job On | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.AvisoNaoAprovado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.NaoAprovado_DeixaAvisoNoResumo_ComLinkParaARevisaoSoAQuemATem | Não aprovado deixa aviso no Resumo; link à Revisão só para quem a tem | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CalendarioComFiltros.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Revisao_CalendarioNomeiaOFormularioDosFiltros_ComOsCamposQueOServidorGuarda | Calendário da Revisão leva os filtros do formulário | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Consulta.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Revisao_Filtros_ConsultamOPeriodo_ComQuemAprovou | Revisão filtra por período e mostra quem aprovou | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Decisoes.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Decisao_SemAPermissaoDeAprovar_ERecusada_ENadaFicaGravado | Sem permissão de aprovar a decisão é recusada, nada gravado | OK |  |
| ControloCapabilitiesCutHPageTests.RevisaoConsultas_AprovarSoNoPeso | Nas consultas só o Peso se aprova | OK |  |
| ControloCapabilitiesCutHPageTests.RevisaoConsultas_ComparacaoNoPeso_EResumoDasFerramentas | Comparação global só no Peso; Resumo das ferramentas com CM e NOK | OK |  |
| ControloCapabilitiesCutHPageTests.Revisao_ExportaOResumo_ComoOResumo | Revisão exporta PDF do Resumo e completo | FRACO | verifica rótulos de botões e o tipo PDF; não lê o texto do PDF (regra 12) |
| ControloCapabilitiesCutHPageTests.Revisao_PuNnpb_TotalVerificados_EConsulta | Revisão mostra total de punções verificadas e consulta da PU NNPB | OK |  |
| ControloCapabilitiesCutHPageTests.Revisao_SoOPesoSeAprova_EDecideOControlo | Só o Peso se aprova e decide o controlo | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.DepoisDaAprovacao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.RevisaoConsulta_MostraAsGravacoesAnterioresDoOkNok | Consulta mostra as gravações anteriores do OK/NOK | OK |  |
| ControloCapabilitiesCutHPageTests.Revisao_ControloAnterior_EOEscolhidoNoPeso_SemEscolhaSoSugere | Revisão mostra o controlo anterior escolhido no Peso, ou só sugere | OK |  |
| ControloCapabilitiesCutHPageTests.Revisao_DepoisDaAprovacao_AvisaOuPedeAprovacaoPropria | Após aprovar: registo novo avisa, Peso novo pede aprovação própria | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Detalhe.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.RevisaoDetalhe_MostraAsLeiturasDoPesoComoARevisao | Detalhe da Revisão mostra as leituras do Peso | OK |  |
| ControloCapabilitiesCutHPageTests.RevisaoDetalhe_OsPegamentosUsamOFormatoDaFolha | Detalhe dos Pegamentos usa o formato da folha | FRACO | verifica class peg-state-bad e HTML da tabela |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar | Resumo: cabeçalho, link mcaliper e Voltar | FRACO | verifica classes (resumo-cabecalho, resumo-linked-grid) e código JS como texto |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Estrutura_Folha_SoOCorpoDesliza | Na folha só o corpo desliza | FRACO | procura texto de CSS e JS no código-fonte, não o comportamento (regra 10) |
| ControloCapabilitiesCutHPageTests.Estrutura_Pagina_TopoComTituloEAcoesECorpo | Página tem topo com título e ações e corpo | FRACO | compara o HTML exato com classes; aspeto vive nas guardas do design (regra 10) |
| ControloCapabilitiesCutHPageTests.Estrutura_PainelDeApoio_TemOEsqueletoFixo | Painel de apoio tem o esqueleto fixo | FRACO | verifica classes e ficheiros CSS/JS carregados, não o efeito (regra 10) |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.FormularioDaPagina.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.AprovarENaoAprovarComNota_PeloFormularioDaRevisao_GravamADecisaoEANotaDeCadaParte | Aprovar e Não aprovar com nota gravam a decisão | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.OperadorNaoAprova.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Revisao_OperadorSemControloAprovar_NaoAprovaNemNaoAprova | Quem não tem Controlo Aprovar não aprova nem recusa | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PdfPegamentos.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Resumo_PdfPegamentos_ApareceComMedicoes | «PDF Pegamentos» aparece com medições | FRACO | só confere o texto do botão; sem o «não aparece» sem medições |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoAbertoArrumado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.PesoAberto_SemFerramentas_EAssociarNumaTabelaComUmSoBotao | Associar Peso: linha escolhida e um só botão no canto | OK | Decisão Diogo 27/10 (peso-arrumado-pr212) |
| ControloCapabilitiesCutHPageTests.PesoSemJobOn_BqSemCampoDoLote_QuandoAMarisaEIgual | Peso sem Job On: BQ sem lote se a marisa é igual | OK | Decisão Diogo 27/10 (peso-arrumado-pr212) |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoCorrigir.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.PesoLeitura_ALinhaEscolheSeComUmClique_EACorrecaoAbreNoLugarDaLeituraNova | Clique na linha da leitura abre a correção no lugar | OK | Decisão Diogo 27/10 (peso-arrumado-pr212) |
| ControloCapabilitiesCutHPageTests.PesoLeitura_CorrecaoComMassaIlegivel_ERecusada_EOValorEscritoFica | Correção com massa ilegível é recusada | OK |  |
| ControloCapabilitiesCutHPageTests.PesoLeitura_CorrigirEApagar_PelaFolha_FicaGravadoComRasto | Corrigir e apagar leitura deixa rasto | OK |  |
| ControloCapabilitiesCutHPageTests.PesoLeitura_SairDaCorrecao_FicaNaFolha_TambemNoPainel | Sair da correção fica na folha, também no painel | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PesoDensidadeAgua.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.NovoPeso_MostraATemperaturaEADensidadeDaAgua | Novo Peso mostra temperatura e densidade da água | OK | Decisão Diogo 28/10 (DMO-528) |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ProcessoDoPeso.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.PesoDaProducaoSemPeso_MostraOProcessoComoNasFolhas | Peso da produção sem Peso mostra o processo | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ResumoNoBrowser.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Resumo_SemPesoComUmPesoParaAssociar_ParaOBrowser | Resumo sem Peso oferece Associar Peso existente e Registar | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ResumoPdf.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.PdfDoResumo_NaRevisaoENoResumo_MesmoFicheiroEMesmaPasta | PDF do Resumo é igual na Revisão e no Resumo | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.ResumoDoControlo_CartoesDasAreas_GravamEVoltamAoResumo | Cartões das áreas gravam OK/NOK e voltam ao Resumo | OK |  |
| ControloCapabilitiesCutHPageTests.ResumoDoControlo_PuECs_EscrevemAReferenciaECmNao | PU e CS escrevem a referência; a CM não | OK |  |
| ControloCapabilitiesCutHPageTests.ResumoDoControlo_SeparadorComProcura_MostraOControloInteiro | Separador «Resumo do controlo» com procura mostra o controlo inteiro | FRACO | sobretudo classes e ids de HTML |
| ControloCapabilitiesCutHPageTests.Revisao_ArrumadaComoODiogoPediu | Revisão arrumada: consultas, filtros, ações | FRACO | verifica rótulos e classes (dmo-button, revisao-section) |
| ControloCapabilitiesCutHPageTests.Revisao_SemPeso_NaoOfereceAprovar | Sem Peso a Revisão não oferece aprovar | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.SemBotoesNasLinhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.ResumoDoControlo_ReferenciaDoPu_GravaNoValor_SemBotaoNaLinha | Referência do PU grava no valor, sem botão na linha | OK | Decisão Diogo 27/10 (nunca botões dentro de linhas) |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.TresEstados.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.NaoAprovado_ChegaAoResumoEAoHistorico_ComQuemEANota | Não aprovado chega ao Resumo e ao Histórico com quem e nota | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.Admin_OperationalRoutesAreBlockedOnGetAndPost_AdminAreaRemainsOpen | O ADMIN não entra no Controlo | OK |  |
| ControloCapabilitiesCutHPageTests.Approve_WithoutIdentityClaim_IsRefusedWithMessage_NotSilently | Aprovar sem identidade é recusado com mensagem | OK |  |
| ControloCapabilitiesCutHPageTests.Approve_WithoutPeso_ShowsNeutralNotice_AndApproves | Sem Peso a Revisão mostra aviso neutro e aprova | OK |  |
| ControloCapabilitiesCutHPageTests.AreaState_OperacionalSavesOkNokAndComment_ChefeIsRefusedTyped | Operador grava OK/NOK e comentário; o chefe é recusado | OK |  |
| ControloCapabilitiesCutHPageTests.ChefeApproveOnly_CreateRefusedTyped_ApproveSucceeds | Chefe: criar é recusado, aprovar funciona | OK |  |
| ControloCapabilitiesCutHPageTests.ChefeApproveOnly_UsesDistinctReviewPages_WithoutOperationalForms | Chefe só com Aprovar vê a Revisão, sem formulários operacionais | OK |  |
| ControloCapabilitiesCutHPageTests.McaliperLink_CreateAssociatesOnToolSheet_ApproveCopiesIt | Link mcaliper associa-se à ferramenta; na Revisão o botão abre-o num separador novo (DMO-678, Diogo 29/10: «eu pedi um botao para abrir o link») | OK | A-0430 |
| ControloCapabilitiesCutHPageTests.PesoAssociation_PrePesoWithJobOnCm_IsOfferedAndAssociatedOnlyByExplicitCreatePost | Peso solto só se associa por pedido explícito | OK |  |
| ControloCapabilitiesCutHPageTests.PesoCreation_ReadableNominal_NoWarning | Nominal legível cria o Peso sem aviso | OK |  |
| ControloCapabilitiesCutHPageTests.PesoCreation_UnreadableOrNonPositiveOptionalNominal_CreatesWithoutNominalAndWarns | Nominal ilegível cria o Peso sem ele e avisa | OK |  |
| ControloCapabilitiesCutHPageTests.PesoGates_CreatePassesForCapable_ReadingRefusedTypedForChefe | Chefe pode criar mas leitura é recusada | DUPLICADO | igual a ChefeApproveOnly_CreateRefusedTyped_ApproveSucceeds (permissões do chefe) e AreaState_OperacionalSavesOkNokAndComment_ChefeIsRefusedTyped |
| ControloCapabilitiesCutHPageTests.PesosSemProducao_TwoCandidates_ChooseTheRowAndAssociateFromTheCorner | Dois Pesos soltos: escolhe-se a linha e associa-se pelo canto | OK | Decisão Diogo 27/10 (peso-arrumado-pr212) |
| ControloCapabilitiesCutHPageTests.ReviewTab_ReturnsToTheExplicitProduction | Separador da Revisão volta à produção explícita | OK |  |
| ControloCapabilitiesCutHPageTests.Review_OpeningWritesNothing_ApprovingCreatesTheMissingControlo | Abrir a Revisão não grava; aprovar cria o controlo em falta | OK |  |
| ControloCapabilitiesCutHPageTests.Revisao_Calendario_MarcaODiaDoPeso_E_NaoAprovarExigeNota | Calendário marca o dia do Peso; Não aprovar exige nota | FRACO | mistura calendário e nota; verifica classes (dmo-calendar-day has selected) |
| ControloCapabilitiesCutHPageTests.Revisao_Decisao_GravaSemRecarregar_ComContextoNoRedirect_SemRepetir | Decisão grava sem recarregar e mantém o contexto | FRACO | verifica atributos HTML (data-dmo-parcial, campos ocultos) |
| ControloCapabilitiesCutHPageTests.Revisao_ResultadosParciais_RespeitamFiltrosContextoEAcesso_SemCalendarioOuDetalhe | Resultados parciais da Revisão respeitam filtros e acesso | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloDefinicoesDensidadeTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloDefinicoesDensidadeTests.ControloCriar_VeAsDuasDensidades_EGuardarMudaAsDuas | Densidades NNPB e PS visíveis e guardáveis | OK |  |
| ControloDefinicoesDensidadeTests.ForaDoHabitual_GravaComAviso | Densidade fora do habitual grava com aviso | OK |  |
| ControloDefinicoesDensidadeTests.NaoENumeroMaiorQueZero_NenhumaGrava | Densidade inválida: nenhuma grava | OK |  |
| ControloDefinicoesDensidadeTests.OsDoisVazios_NadaMuda_ComAviso | Os dois campos vazios: nada muda e avisa | OK |  |
| ControloDefinicoesDensidadeTests.SemControloCriar_NaoGrava | Sem Controlo Criar não grava a densidade | OK |  |
| ControloDefinicoesDensidadeTests.SoNnpb_Grava_PsFicaComoEstava | Só uma densidade preenchida grava só essa | OK |  |
| ControloDefinicoesDensidadeTests.SoPs_Grava_NnpbFicaComoEstava | Só uma densidade preenchida grava só essa | OK |  |
| ControloDefinicoesDensidadeTests.UmInvalido_OOutroGrava_ComAviso | Uma densidade inválida: a outra grava com aviso | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloDefinicoesPageTests.AdminDefinicoes_NoLongerCarryTheControloSettings | As definições do ADMIN já não levam as do Controlo | OK |  |
| ControloDefinicoesPageTests.ControloCriar_SeesThePdfFolderAndTheNorm_InItsOwnTab | Definições do Controlo mostram a pasta de PDFs e a tolerância | FRACO | só confere textos, nomes de campos e scripts presentes no HTML |
| ControloDefinicoesPageTests.NotANumberWithoutSign_IsNotSavedAndNothingChanges | Tolerância inválida não grava e nada muda | OK |  |
| ControloDefinicoesPageTests.OutsideTheUsual_IsSavedWithAWarning | Tolerância fora do habitual grava com aviso | OK |  |
| ControloDefinicoesPageTests.Save_CreatesThenUpdatesTheSingleRow | Guardar a tolerância cria e depois atualiza a única linha | OK |  |
| ControloDefinicoesPageTests.WithoutControloCriar_TheTabDoesNotOpenAndNothingIsSaved | Sem Controlo Criar o separador não abre | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloPegamentosServicesRegistrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloPegamentosServicesRegistrationTests.ControloPegamentosServices_ResolveAsScopedImplementations | Serviços dos Pegamentos registados por pedido | FRACO | só verifica registo de serviços (DI), não uma regra do negócio |

### `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloResumoPageTests.AnonymousRequests_ReceiveNoPageAndNoMutation | Sem sessão o Resumo não abre nem grava | OK |  |
| ControloResumoPageTests.AreaState_TwoStations_EachFormKeepsTheOtherField | Dois postos: cada formulário mantém o outro campo | OK |  |
| ControloResumoPageTests.Browser_ReferenceFilter_ServerSide_AndRecentesPrimeiro | Procurar resumo filtra no servidor, mais recentes primeiro | OK | Decisão Diogo: último registo em cima |
| ControloResumoPageTests.CreateJobOnViaPage_ControloGuaranteedBySameWrite | Criar Job On cria o controlo na mesma escrita | OK |  |
| ControloResumoPageTests.Folha_MarcaAsZonasDaTroca_EGravarVoltaAMesmaPagina | Resumo marca as zonas da troca; gravar volta à mesma página | FRACO | sobretudo atributos e classes do HTML |
| ControloResumoPageTests.GravarArea_ZonasPedidas_DesenhaSoAsZonas_SemLerOQueNaoMostram | Gravar área desenha só as zonas pedidas | FRACO | verifica ids e classes das zonas HTML (resumo-area nok) |
| ControloResumoPageTests.NonExistentJobOn_ShowsWarning_NoControloCreated | Job On inexistente avisa e não cria controlo | OK |  |
| ControloResumoPageTests.OpenPreCutJobOn_FolhaShowsCanonicalContext_BackfillsControloIdOnce | Job On antigo abre o Resumo e ganha o controlo uma só vez | FRACO | verifica classes CSS e ficheiro CSS no HTML |
| ControloResumoPageTests.SearchPagination_ReturnsOnlyTheRequestedList_AndDoesNotOpenOrCreateAControlo | Procurar e paginar não abre nem cria controlos | OK |  |
| ControloResumoPageTests.SecondaryNav_GroupsTheSheetsUnderFuncoes | Barra do Controlo agrupa Peso, Pegamentos e PU NNPB em Funções | OK |  |
| ControloResumoPageTests.SecondaryNav_ShowsPesoAndResumo_OnBothControloPages | Barra do Controlo mostra Peso e Resumo | FRACO | só links e rótulos no HTML |
| ControloResumoPageTests.Suggestions_AuthenticatedOnly_AndResumoFieldUsesThem | Sugestões de produções só com sessão; campo do Resumo usa-as | OK |  |
| ControloResumoPageTests.ZonasPedidas_SemOModulo_NaoDesenhaNada | Sem o módulo não se desenha nada | OK |  |

### `tests/DMO.Alpha.Web.Tests/ControloResumoServicesRegistrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ControloResumoServicesRegistrationTests.ControloResumoServices_ResolveAsScopedImplementations | Serviços do Resumo registados por pedido | FRACO | só verifica registo de serviços (DI), não uma regra do negócio |

### `tests/DMO.Alpha.Web.Tests/MedicaoPedidosTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| MedicaoPedidosTests.Desligada_NaoPoeOCabecalho_NemALinha_EONoStoreFica | Medição desligada não põe cabeçalho nem log | OK | Fora do Controlo (medição de desempenho) |
| MedicaoPedidosTests.NoPostgres_ONumeroDeConsultasBateComOsComandos_EOParametroNaoVaiParaOLog | No PostgreSQL o nº de consultas bate com os comandos | OK | Fora do Controlo; só corre se houver base de teste |
| MedicaoPedidosTests.PaginaAutenticada_TemServerTiming_EOsEstaticosNao_EOLogNaoLevaValores | Medição de pedidos: Server-Timing e log sem valores | OK | Fora do Controlo (medição de desempenho) |
| MedicaoPedidosTests.PedidoQueLancaExcecao_ALinhaDizQuinhentos | Pedido com exceção regista 500 sem dados | OK | Fora do Controlo (medição de desempenho) |

### `tests/DMO.Alpha.Web.Tests/PegamentosGraficoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosGraficoTests.CadaComponenteMedido_NaSuaLinha_PelaOrdemDaFolha | Cada componente medido na sua linha, pela ordem da folha | OK |  |
| PegamentosGraficoTests.Escala_DasMedicoesEDosLimites_ComMargem_SemLinhasVazias | Gráfico: escala com medições e limites, sem linhas vazias | OK |  |
| PegamentosGraficoTests.SemMedicoes_NenhumaLinha | Sem medições o gráfico não tem linhas | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.EnvioUnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.PostMeasurement_SameEnvioTwice_SavesOnce | O mesmo envio de leitura grava uma só vez | FRACO | «nada repetido» só em memória; falta PostgreSQL (regra 7) |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Folhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.CmChanged_PageIsReadyForANewSheet_TheFirstReadingStartsIt_AndThePageSaysSo | Após trocar a CM a próxima leitura abre folha nova e a página diz | OK |  |
| PegamentosPageTests.EscolhaDoJobOn_DepoisDeTrocarACm_AbreAFolhaDasFerramentasAtuais | Escolher Job On após troca abre a folha atual | OK |  |
| PegamentosPageTests.OldSheet_OpensWithItsTools_NoNewReadingRow_CorrectionReturnsToIt_PdfNamesFollowTheSheet | Folha antiga abre com as suas ferramentas, sem leitura nova | OK |  |
| PegamentosPageTests.ReviewerScreens_ShowBothSheets_TheAlertOfTheSecondIsNotHidden | Revisão mostra as duas folhas sem esconder o alerta da 2.ª | OK |  |
| PegamentosPageTests.SingleSheet_PageLooksAsBefore_NoSheetsCard | Com uma só folha não aparece o cartão de folhas | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.FormularioDaPagina.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.LeituraPeloFormularioDaPagina_GravaACosturaEAContraCostura | Leitura pelo formulário da página grava costura e contra costura | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Legenda.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.MapaDeLimites_ALegendaMostraAsCoresDoDesenho | A legenda do mapa tem as cores desenhadas | FRACO | compara cores e classes do SVG (estilo), não o efeito |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NaoAvaliavelCinzento.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.Resumo_MedicoesNaoAvaliaveis_OCartaoFicaCinzento | Medições não avaliáveis: cartão do Resumo a cinzento | FRACO | verifica classes e regex de CSS (border-color), não o efeito (regra 10) |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.NumeroFerramenta_CartaoBqOuMf_ColunaComOSeuNomeEGrava | Cartões BQ e MF têm a coluna do seu nº | OK |  |
| PegamentosPageTests.NumeroFerramenta_Corrigir_ONumeroAbreComFocoEGrava | Corrigir o nº abre com foco e grava | OK |  |
| PegamentosPageTests.NumeroFerramenta_Limite20_19e20GravamInteiros_21ECortadoComAviso | Nº com 19 e 20 caracteres grava inteiro; 21 é cortado com aviso | OK |  |
| PegamentosPageTests.NumeroFerramenta_LinhaSoComONumero_NaoERegistadaEAvisa | Linha só com o nº não é registada e avisa | DUPLICADO | igual a NumeroFerramenta_VariasLinhas_CadaUmaComOSeuNumero_SoONumeroContaComoVazia |
| PegamentosPageTests.NumeroFerramenta_RegistarComNumero_GravaEAFolhaMostraNaColunaNoCm | Registar com nº da ferramenta grava e mostra a coluna | OK |  |
| PegamentosPageTests.NumeroFerramenta_VariasLinhas_CadaUmaComOSeuNumero_SoONumeroContaComoVazia | Várias linhas com nº próprio; linha só com nº não conta | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.PedidoAbortado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.PostMeasurement_SeveralLines_ClientAbortsAfterTheFirstWrite_AllLinesAreSaved | Várias linhas com cliente a desistir: gravam todas | FRACO | tudo ou nada só em memória (sem transação); falta PostgreSQL (regra 7) |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Rasto.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.CorrigirEApagarUmaLeitura_AConsultaDaRevisaoMostraOAntesODepoisEQuem | Revisão mostra antes, depois e quem corrigiu ou apagou | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Resumo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.ResumoCartao_AlertaENaoAvaliavel_FicaNok | Cartão do Resumo fica NOK com alerta e não avaliável | OK |  |
| PegamentosPageTests.ResumoCartao_AlertaNaFolhaAntiga_FicaNok_ComAFolhaAtualDentro | Alerta na folha antiga mantém o cartão NOK | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.ToleranciaDasDefinicoes.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.FolhaSemMedicoes_MostraAToleranciaDasDefinicoes_EAPrimeiraMedicaoCongelaA | Folha sem medições mostra a tolerância das Definições | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPageTests.AnonymousRequests_ChallengedBeforeAnyReadOrWrite | Sem sessão não lê nem grava Pegamentos | OK |  |
| PegamentosPageTests.Completion_UsesToolRootFromProductionContext_NotAnotherLot | Gargalo completa-se na ferramenta da produção (por id), não noutro lote | OK | Decisão Diogo 28/10: ligar pelo tool_id |
| PegamentosPageTests.Correcao_ContraCosturaIlegivel_Recusa_SemApagarAGuardada | Contra costura ilegível recusa e mantém a guardada | OK |  |
| PegamentosPageTests.Folha_GravarSemRecarregar_UmaZonaPorCartao | Gravar sem recarregar: uma zona por cartão | FRACO | verifica atributos, ids e classes do HTML |
| PegamentosPageTests.GetFolha_ControloId_ShowsInheritedContext_GargaloMissing_MfAlwaysInherited | Folha mostra contexto herdado da produção | FRACO | verifica sobretudo classes CSS (peg-context-grid, peg-section-head) |
| PegamentosPageTests.GetUnknownControloId_ShowsCleanWarning_NoSheetInvented | Controlo inexistente avisa sem inventar folha | OK |  |
| PegamentosPageTests.GetWithoutControloId_ShowsProvenanceState_NothingInvented | Pegamentos sem controlo mostram Sem Job On, nada inventado | OK |  |
| PegamentosPageTests.HistoricalToolFacts_RemainOnReadingCardAndPdf_AfterCatalogEdit | Factos da ferramenta no cartão e PDF ficam após editar o catálogo | FRACO | PDF só confere cabeçalho %PDF (regra 12: ler o texto) |
| PegamentosPageTests.InvalidCostura_StillRefused_PreservesDraftAndSavedReadings_ThenAllowsCorrection | Costura inválida recusa e preserva rascunho e leituras | OK |  |
| PegamentosPageTests.MissingNominal_RefusesMeasurement_AndPointsToTheToolSheet | Sem nominal do gargalo recusa a leitura, nada gravado | OK |  |
| PegamentosPageTests.MissingNominal_ToolSheet_SavesGargalo_AndReturnsToTheSheet | Completar o gargalo na ficha da ferramenta volta à folha | OK |  |
| PegamentosPageTests.MissingNominal_UnreadableGargalo_IsRefused_ToolUntouched | Gargalo ilegível é recusado e a ferramenta fica intacta | OK |  |
| PegamentosPageTests.MixedAxes_SummaryAndPdfUseRowMeans_WithoutHidingAlert | Resumo e PDF usam médias por linha sem esconder alerta | FRACO | PDF só confere cabeçalho %PDF; células lidas por classe CSS peg-cell-bad |
| PegamentosPageTests.PdfDownloads_UsePersistedModels_AllComponents_AndDoNotWriteDatabaseOrServerFiles | PDF de Pegamentos não grava na base nem no servidor | OK |  |
| PegamentosPageTests.Pdf_WithoutMeasurements_NoButton_AndPostIsRefused_WithoutWriting | Sem medições não há botão de PDF e o pedido é recusado | OK |  |
| PegamentosPageTests.PostMeasurement_CreatesRegistro_PRG_BackToShowWithBackendCalculations | Leitura cria folha e volta à página com cálculos do servidor | OK |  |
| PegamentosPageTests.PostMeasurement_MfInProduction_RegistersWithInFlowGargaloCompletion | MF da produção regista e completa o gargalo | OK |  |
| PegamentosPageTests.PostMeasurement_SeveralLines_RecordsEachFilledLine | Várias linhas gravam cada linha preenchida | OK |  |
| PegamentosPageTests.PostMeasurement_SingleAxis_OvalizacaoUndefined_MediaEqualsCostura | Leitura com um eixo: ovalização indefinida | OK |  |
| PegamentosPageTests.PostMeasurement_UnusableCostura_NoWrite_AsksForCorrection | Costura inutilizável não grava e pede correção | DUPLICADO | igual a InvalidCostura_StillRefused_PreservesDraftAndSavedReadings_ThenAllowsCorrection |
| PegamentosPageTests.Readings_EditByClickingTheValue_DeleteWithTheX_Renumbers | Corrigir clicando no valor; apagar renumera | OK | Decisão Diogo 27/10 (A-0226): sem × nas linhas |
| PegamentosPageTests.RealTarget_IndividualCells_OutsideCorridor_EachPaintedWithItsOwnState | Células fora do corredor assinaladas uma a uma | FRACO | verifica a classe CSS peg-cell-bad nas células; a regra já está em PegamentosRealTargetTests |
| PegamentosPageTests.RegistarEApagarNoutroCartao_MantemACorrecaoAberta | Registar noutro cartão mantém a correção aberta | OK |  |
| PegamentosPageTests.RegistarLeitura_ZonasPedidas_DesenhaSoAsZonas_SemLerAsFerramentas | Registar leitura desenha só as zonas pedidas | FRACO | verifica atributos HTML das zonas e botões; poucas regras |
| PegamentosPageTests.ResumoPage_LinksToPegamentosSheet_WithEnsuredControloId | Resumo liga à folha de Pegamentos pelo controlo | FRACO | só confere links e rótulos «Pegamentos» no HTML |
| PegamentosPageTests.SavedPesoNavigation_UsesItsOwnProduction_AndDoesNotAssociateToolScopedPeso | Navegação do Peso guardado usa a sua produção e não associa | OK |  |
| PegamentosPageTests.UnreadableContraCostura_SavesCosturaOnly_WithWarning | Contra costura ilegível grava só a costura com aviso | OK |  |

### `tests/DMO.Alpha.Web.Tests/PegamentosPrevisaoCasosTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PegamentosPrevisaoCasosTests.OServidorDaOMesmoQueAPrevisualizacao | Servidor e pré-visualização dão o mesmo resultado | OK |  |

### `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.EnvioUnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoToolScopedPageTests.RegisterMeasurement_SameEnvioTwice_SavesOnce | O mesmo envio de leitura do Peso grava uma só vez | FRACO | «nada repetido» só em memória; falta PostgreSQL (regra 7) |

### `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.FormularioDaPagina.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoToolScopedPageTests.PesoELeituraPeloFormularioDaPagina_GravamOQueOOperadorEscreveu | Criar Peso e leitura pelo formulário grava o escrito | OK |  |

### `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.PedidoAbortado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoToolScopedPageTests.RegisterMeasurement_SeveralLines_ClientAbortsAfterTheFirstWrite_AllLinesAreSaved | Várias linhas com cliente a desistir: gravam todas | FRACO | tudo ou nada só em memória (sem transação); falta PostgreSQL (regra 7) |

### `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.UmEnvio.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoToolScopedPageTests.RegisterMeasurement_OMesmoEnvioDepoisDeUmReinicio_NaoGravaOutraVez_EUmEnvioNovoIgualGrava | Mesmo envio após reinício não grava outra vez | FRACO | «nada repetido» só em memória; duplica RegisterMeasurement_SameEnvioTwice_SavesOnce |

### `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PesoToolScopedPageTests.CreateRefusal_IsTypedAndRedirectsWithToolContext | Criação recusada mantém o contexto da ferramenta | OK |  |
| PesoToolScopedPageTests.CreateRefusal_TemperaturaForaDoIntervalo_ContinuaRecusa | Temperatura fora do intervalo é recusada | OK |  |
| PesoToolScopedPageTests.CreateRefusal_ValorEscritoMasIlegivel_DizIlegivel | Valor escrito mas ilegível diz «ilegível» e não cria | OK |  |
| PesoToolScopedPageTests.RegisterMeasurement_MassaIlegivelDizIlegivel_MassaZeroContinuaRecusa | Massa ilegível ou zero é recusada, nada gravado | OK |  |
| PesoToolScopedPageTests.SavedPeso_KeepsRecordedIdentificationAfterCatalogEdit_AndDownloadsPdf | Peso guardado mantém a identificação após editar o catálogo | OK | Decisão Diogo 28/10: congelar por tool_id |
| PesoToolScopedPageTests.ToolScopedFlow_CreateRecordConsult_ThroughThePage | Criar, registar e consultar o Peso pela página | FRACO | verifica ficheiro CSS e classes (peso-card-heading) além do fluxo |
| PesoToolScopedPageTests.ToolScopedFlow_ResolvesByTheHumanReference_WhatTheSheetsShow | Peso sem Job On escolhe-se pela referência que as folhas mostram | OK |  |
| PesoToolScopedPageTests.UnauthenticatedRequests_ReceiveNoPageAndNoMutation | Sem sessão o Peso não abre nem grava | OK |  |
| PesoToolScopedPageTests.VolumeMarisaZero_ERecusado_OOperadorLeARecusaNoNovoPeso_NadaFicaGravado | Volume da marisa 0 é recusado, nada gravado | OK |  |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.EnvioUnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.Add_SameEnvioAfterTheRepeatWindow_SavesOnceWithoutAFalseAlteracao | O mesmo envio de punção grava uma vez, sem falsa alteração | FRACO | «nada repetido» só em memória; falta PostgreSQL (regra 7) |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.ErrosControlo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.Folha_TodasAsCelulasComONumeroEALinhaDoLote | Todas as células da folha com número e linha do lote | FRACO | verifica classes e CSS (display:block;height:13px), regra 10 |
| PuncoesPageTests.Identificacao_SemLinhaNemProcesso_ETotais_ComReserva | Identificação sem linha nem processo e totais com reserva | OK |  |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Gotas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.Gotas_DaLinha_DitamAsPosicoesDeCadaSeccao_NaFolhaENoPdf | A gota dita as posições de cada secção na folha e no PDF | OK | Decisão Diogo 27/10 (puncoes-gota) |
| PuncoesPageTests.Gotas_NasDefinicoes_B1Com4EC3Com3PorOmissao_GravamSeERecusamForaDe1a4 | Gota por linha: B1 4, C3 3; fora de 1 a 4 recusa | OK | Decisão Diogo 27/10 (puncoes-gota) |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.LoteNoBrowser.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.Identificacao_OCampoDoLoteVaiParaOBrowser | Campo do lote da identificação está na página | FRACO | só confere que o id e o texto do botão existem; é um teste do browser sem browser |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Posicoes.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.FolhaDasPuncoes_LigadaDoResumoEDoRegisto_SoComRegisto_EOPdfDoControloTrazODasPuncoes | Folha das punções ligada ao Resumo; PDF do controlo traz as punções | OK |  |
| PuncoesPageTests.Posicoes_A1aD12_Reserva_RepetidosComLote_ETotais_NaZonaDaFolha | Posições A1 a D12, reserva, repetidos com lote e totais | OK | Decisão Diogo 27/10: tabela só com as repetições |
| PuncoesPageTests.Posicoes_SeguemAsSeccoesDaLinha_GuardadasNasDefinicoes | As posições seguem as secções da linha das Definições | OK |  |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.Repetida.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.MesmoNumeroEMesmoLote_ERecusada_ComAMensagem_EOutroLoteGrava | Mesmo número e lote recusa; outro lote grava | OK |  |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.RegistosSemJobOn_UmaLinhaEscolhe_EUmSoBotaoNoCantoAssocia | Registos sem Job On: linha escolhe, um só botão no canto | OK | Decisão Diogo 27/10 (peso-arrumado-pr212) |
| PuncoesPageTests.UmSoRegistoSemJobOn_VemEscolhido | Com um só registo sem Job On a linha vem escolhida | FRACO | verifica class="selected" e atributos checked no HTML |
| PuncoesPageTests.Vistos_UmaLinhaEscolhe_EOsDoisBotoesDoCantoTrocam | Vistos: linha escolhe; botões do canto, Remover em último | OK | Decisão Diogo: Eliminar em último |
| PuncoesPageTests.Vistos_UmaSoPuncao_VemEscolhida | Com uma só punção a linha vem escolhida | FRACO | verifica class="selected" e atributos checked no HTML |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.TabelaNoBrowser.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.Tabela_TemLargurasFixas_EVaiParaOBrowser | A tabela de punções tem larguras fixas | FRACO | verifica classes (pu-table-fixa) e <colgroup>, não o efeito (regra 10) |

### `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PuncoesPageTests.CmWithoutProcess_TheNoticeLinksToTheCmSheet_OnlyWithFerramentas | CM sem processo avisa e liga à ficha só com Ferramentas | OK |  |
| PuncoesPageTests.NnpbProduction_RegistersPunches_ShowsAlteracao_TotalsResumoAndPdf | Produção NNPB regista punções, mostra alterações, totais e PDF | OK |  |
| PuncoesPageTests.NumberWithQuotes_IsWrittenAsText_NeverInsideAnOnclick | Número com aspas nunca vai para um onclick | OK |  |
| PuncoesPageTests.NumeroRepetido_PeloFormularioDaPagina_GravaOLoteDessaPuncao | Número repetido grava o lote dessa punção | DUPLICADO | igual a PuncoesHandlerTests.NumeroRepetido_ComOLoteDessaPuncao_RegistaComEsseLote_EOLoteDeCimaFica (mas pelo formulário da página) |
| PuncoesPageTests.Procura_TrocaSoAsZonas_ComAProducaoAoLadoDaReferencia | Procura troca só as zonas, com a produção ao lado | FRACO | verifica atributos e regex do HTML |
| PuncoesPageTests.PsProduction_HasNoRegisto_NoCardAndNoWrite | Produção PS não tem registo de punções | OK |  |
| PuncoesPageTests.RefusedLot_KeepsTheTypedValues_AndWritesNothing | Lote recusado mantém o escrito e não grava | OK |  |
| PuncoesPageTests.WithJobOn_TheProductionIsChosenInTwoSteps | Com Job On a produção escolhe-se em dois passos | OK |  |
| PuncoesPageTests.WithoutJobOn_RegisterStandsAlone_AndIsAssociatedByHand | Registo de punções sem Job On associa-se à mão | OK |  |
| PuncoesPageTests.WithoutJobOn_TheAssociationSearchAndRefusals_ChangeOnlyTheirZone | Associar punções: procura e recusas trocam só a zona | FRACO | verifica ids, atributos e zonas HTML |

## Boquilhas e Armazém

444 linhas: OK 409, FRACO 31, DUPLICADO 4

### `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemHistoricoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AFerramentaOMovimentoEODestino | Filtros do histórico por ferramenta, tipo de movimento e destino | OK |  |
| APaginacao_VinteECincoPorPagina | Histórico pagina a 25 movimentos | OK |  |
| MeiaNoiteEMeiaEmLisboa_JaContaComoODiaSeguinte | Movimento depois da meia-noite de Lisboa conta no dia seguinte | OK |  |
| OTexto_ProcuraAReferenciaOLoteOuAPosicao (Theory x6) | Procura no histórico por referência, lote ou posição | OK |  |
| OsDias_EmHoraDeLisboa_InclusiveNasPontas (Theory x4) | Filtro de datas inclui os dois dias, em hora de Lisboa | OK |  |
| SemFiltro_DoMaisRecenteParaOMaisAntigo | Histórico do armazém com o último registo em cima | OK |  |
| UmaFerramentaEliminada_FicaComAReferenciaGuardada | Ferramenta eliminada mantém referência e lote no histórico | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLinhaReparadorJobOnTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Entrada_ComALinhaEOReparadorDoFormulario_GuardaOQueAPessoaDeixou | Entrada guarda a linha e o reparador escolhidos pela pessoa | OK |  |
| Entrada_ComOReparadorApagadoNoFormulario_FicaSemReparador | Reparador apagado no formulário fica sem reparador | OK |  |
| HistoriaDoJobOn_OReparadorCorrigidoNoRegresso_Manda | Reparador corrigido na entrada de regresso prevalece na história | OK |  |
| HistoriaDoJobOn_RIEIdasAReparacao_PeloJobOnGuardadoEPelasDatasNasDeAntes | História de reparações do Job On junta a RI e as idas ao reparador (pelo Job On guardado ou pelas datas nas antigas) | OK | Liga pelo Job On guardado na saída; só usa datas nas antigas sem Job On, coerente com cm_id/mf_id |
| OJobOnSegueAFerramenta_DaProducaoAReparacaoEDeVolta_ComOReparadorDaSaida | Linha e Job On acompanham a ferramenta da produção à reparação e de volta | OK |  |
| SaidaParaProducao_AntesDeAProducaoComecar_LigaAoProximoJobOnDaLinha | Saída antes da produção começar liga ao próximo Job On da linha | OK |  |
| SaidaParaProducao_ComUmaLinhaQueNaoExiste_GravaSemLinha | Linha inexistente: grava a saída sem linha | OK |  |
| SaidaParaProducao_DepoisDeOsJobOnsAcabarem_GuardaSoALinha | Depois dos Job On acabarem guarda só a linha | OK |  |
| SaidaParaProducao_GuardaALinhaEOJobOnDessaFerramentaNessaLinha | Saída para Produção guarda a linha e o Job On dessa ferramenta | OK |  |
| SaidaParaProducao_NoDiaDaTroca_LigaAoJobOnQueEstaACorrerNesseDia (Theory x2) | No dia da troca liga ao Job On que corre nesse dia | OK |  |
| SaidaParaProducao_NoutraLinhaSemJobOn_GuardaSoALinha | Linha sem Job On: guarda só a linha | OK |  |
| Situacao_ForaPelaReparacao_PropoeALinhaEOReparadorComQueVem | A entrada propõe a linha e o reparador com que a ferramenta saiu | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemLotesNovosTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentaExistente_ComAMesmaReferenciaELote_LigaEmVezDeCriar | Lote novo liga à ferramenta que já existe com a mesma referência e lote | OK |  |
| Registar_GuardaPorCriar_SemCriarFerramentaNemMovimento | Lote novo fica «por criar»; o Armazém nunca cria ferramentas | OK |  |
| Registar_SoRecusaOQueFaltaEAPosicaoSemQuatroAlgarismos (Theory x4) | Lote novo recusa sem referência, sem lote ou posição sem 4 algarismos, e nada grava | OK |  |
| Resolver_ComFerramentaQueNaoExiste_NaoResolve | Resolver com ferramenta inexistente é recusado e o lote continua por criar | OK |  |
| Resolver_DaAEntradaNaPosicaoGuardada_EOUltimoMovimentoPoeLa | Ao criar a ferramenta, a entrada nasce na posição guardada, uma só vez | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMovimentosTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Corrigir_NaoEstaNoArmazemDepoisDaSucata_TiraDaPosicaoEVoltaAoFabrico | Corrigir depois de sucata repõe Fabrico e deixa o estado | OK |  |
| Corrigir_ParaOndeJaEsta_NadaMuda | Corrigir para onde já está não grava nada | OK |  |
| Corrigir_ParaOutraPosicao_LibertaAAnteriorEOcupaACerta | Corrigir localização liberta a posição anterior e ocupa a certa | OK |  |
| Corrigir_ParaUmaPosicaoQueNaoExiste_PedeParaCriar | Corrigir para posição inexistente pede para criar | OK |  |
| EliminarAFerramenta_PassaOsMovimentosParaARemovedELibertaAPosicao | Eliminar ferramenta guarda movimentos como REMOVED e liberta a posição | OK |  |
| Entrada_DataNoFuturo_GravaEAvisaPassadosCincoMinutos (Theory x3) | Data no futuro grava e avisa depois de 5 minutos | OK |  |
| Entrada_DepoisDaSucata_GravaEAvisa | Entrada depois da sucata grava e avisa | OK |  |
| Entrada_FerramentaFora_GravaMovimentoOcupacaoLocalizacaoEstadoEOperacao | Entrada grava movimento, ocupação, localização, estado e operação juntos | OK | Atomicidade (tudo ou nada) não provada: em memória sem transação |
| Entrada_JaEstaNoArmazem_RecusaEDizAPosicao | Entrada de ferramenta que já está no armazém é recusada e diz a posição | OK |  |
| Entrada_OMesmoEnvioDuasVezes_GravaUmaVez | Mesmo envio da entrada duas vezes grava uma só vez | OK | A garantia de concorrência está em PostgresMigrationIntegrationTests.ArmazemMovimentos (Data/) |
| Entrada_OMfDaMesmaReferenciaELoteDaCm_NaoAvisa | CM e MF da mesma referência e lote partilham posição sem aviso | OK |  |
| Entrada_ObservacoesNoLimite_GravaAteAo200 (Theory x3) | Observações até 200 caracteres (limite, abaixo e acima) | OK |  |
| Entrada_PosicaoArquivada_GravaEAvisa | Posição arquivada grava e avisa | OK |  |
| Entrada_PosicaoComOutraFerramenta_GravaEAvisaQuemLaEsta | Posição já ocupada por outra ferramenta grava e avisa quem lá está | OK |  |
| Entrada_PosicaoForaDaRegra_Recusa (Theory x4) | Posição sem 4 algarismos é recusada e nada grava | OK |  |
| Entrada_PosicaoQueNaoExiste_PedeParaCriarECriaComCriarERegistar | Posição inexistente pede «Criar e registar» | OK |  |
| Entrada_SemOrigemOuSemEstado_Recusa | Entrada exige origem e estado, sem valor por omissão | OK |  |
| Localizacao_EmprestadaDizAFabricaEscolhidaNaSaida | Emprestada mostra a fábrica escolhida na saída | OK |  |
| Ocupantes_SoAsFerramentasQueAindaLaEstao | Ocupantes da posição são só as ferramentas que lá estão | OK |  |
| Procurar_DaOEstadoAsMaquinasEOUltimoUso | Procura dá estado, máquinas e último % de uso | OK | Liga o % uso pelo tool_id (correto) |
| Procurar_DizOndeEstaCadaFerramenta | Procura diz onde está cada ferramenta (armazém ou reparação) | OK |  |
| Procurar_PelaReferenciaLoteOuPosicao (Theory x6) | Procura de ferramentas por referência, lote ou posição | OK |  |
| Saida_ComDataAnteriorAoUltimoMovimento_GravaEAvisa | Saída com data anterior ao último movimento grava e avisa | OK |  |
| Saida_EmprestadoComAFabricaNoLimite_GravaAteAos60 (Theory x3) | Fábrica do empréstimo até 60 caracteres | OK |  |
| Saida_ParaReparacaoDeUmaBq_UsaOsReparadoresDasBoquilhas | Saída de BQ usa reparadores das Boquilhas; listas não se misturam | OK |  |
| Saida_ParaReparacaoDeUmaCm_UsaOsReparadoresDoArmazemECongelaONome | Saída para reparação de CM usa reparadores do Armazém e congela o nome | OK |  |
| Saida_ParaSucataSemEstadoEParaProducaoComEstado_NaoMudaOEstado | Só a escolha da pessoa muda o estado, e só na sucata | OK |  |
| Saida_ParaSucata_GravaOEstadoQueAPessoaConfirmou | Saída para sucata grava o estado Sucatado confirmado pela pessoa | OK |  |
| Saida_ReparadorDeOutraFamiliaArquivadoOuQueNaoExiste_Recusa (Theory x2) | Reparador de outra família, arquivado ou inexistente é recusado | OK | Falta verificar que a ocupação não foi libertada |
| Saida_SemDestinoSemReparadorOuForaDoArmazem_Recusa | Saída sem destino, sem reparador ou de ferramenta fora é recusada | OK |  |
| Situacao_DepoisDeUmaSaida_PropoeAOrigemEOEstado (Theory x4) | Depois de uma saída propõe a origem e o estado da entrada | OK |  |
| Situacao_NoArmazem_MostraAPosicaoOEstadoEOsUltimosMovimentos | Situação mostra posição, estado e últimos movimentos (último em cima) | OK |  |
| Situacao_SemMovimentos_EstaNoFabricoSemPropostas | Sem movimentos a ferramenta aparece em Fabrico, sem propostas | OK |  |
| UmaFerramentaQueNaoExiste_RecusaENaoGravaNada (Theory x3) | Entrada, saída e correção de ferramenta inexistente são recusadas sem gravar nada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemReparacaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Fora_SoCmEMfCujoUltimoMovimentoESaidaParaReparacao | Fora em reparação lista só CM e MF cuja última saída foi para reparação | OK |  |
| Ultimas_BqIncluida_ComReparadorEPosicao | Últimas entradas e saídas incluem BQ, com reparador e posição | OK |  |
| Ultimas_OsFiltrosNovos | Filtros das últimas por tipo, referência, lote, linha e reparador | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Armazem/FabricasBaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Comeca_vazia_e_acrescenta_com_sigla_de_duas_letras_unica | Fábricas de empréstimo: sigla de duas letras, nome obrigatório, sem repetidas | OK |  |
| Desativa_sem_apagar_e_MG_e_a_proposta | Fábrica desativa-se sem apagar; MG é a proposta por omissão | OK |  |
| Emprestimo_filtra_pela_fabrica_da_ultima_saida | Procurar ferramentas emprestadas filtra pela fábrica da última saída | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Criar_ComNomeEFamilias_Grava | Criar reparador com nome e famílias CM/MF | OK |  |
| Criar_ForaDaRegra_NaoGrava (Theory x3) | Reparador sem nome ou sem família é recusado e nada grava | OK |  |
| Criar_NomeLongo_NaoGrava | Nome acima do limite é recusado | FRACO | Não verifica que nada foi gravado (REGRAS 5) |
| Criar_NomeNoLimite_Grava (Theory x2) | Nome no limite e logo abaixo grava | OK |  |
| Criar_OMesmoNomeComOutrasMaiusculas_JaExiste | Reparador com o mesmo nome (maiúsculas diferentes) não se repete | OK |  |
| Criar_UmNomeQueJaExisteNaOutraLista_Grava | Listas de reparadores CM/MF e BQ não se misturam | OK |  |
| Mudar_ForaDaRegra_NaoGrava | Alteração inválida de reparador é recusada e nada muda | OK |  |
| Mudar_NomeFamiliasEEstado_MudaSoOQueVem | Alterar reparador muda só o que veio; ativos primeiro | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ContextoDaBqNaProducao_SoDaProducaoEDaFerramentaDoRegisto | bq_id só se a produção e a BQ do registo coincidem | OK |  |
| OsPedidos_NaoGravamNada | As consultas de associação não gravam nada | FRACO | Verifica o ChangeTracker vazio e não a base relida |
| Producao_DevolveIdEReferencia... | Produção a associar devolve id e referência | OK |  |
| ProducoesDoNumero_MaisRecentePrimeiro_ComAIndicacaoDeTerAMesmaBq | Produções com o mesmo número, última primeiro, a indicar se têm a mesma BQ | OK |  |
| RegistoSemJobOn_DevolveOTraceAvulso... | Registo sem Job On da BQ: só o avulso, com a contagem de movimentos | OK |  |
| RegistosDaBq_PaginaPedidaForaDoFimFicaNaUltima | Página pedida além do fim abre na última | OK |  |
| RegistosDaBq_VemOsDaFerramentaEOsAssociadosMaisRecentesPrimeiro_ComProducaoEMovimentos | Registos da BQ: os da ferramenta e os associados, último em cima, com produção e movimentos | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasCurrentQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Card_ReflectsBqIdPlannedByTheJobOn | O cartão da máquina mostra a BQ ligada à produção corrente (bq_id) | OK |  |
| Card_WithCurrentBq_CarriesBqReferenceContextAndMachine | Cartão com BQ corrente traz referência, tool_id, bq_id e máquina | OK |  |
| Card_WithoutCurrentBq_HasNoBq | Sem produção corrente não há BQ nem bq_id | DUPLICADO | Repete MachineWithoutProduction_ShowsNoProduction |
| CurrentProduction_ChangesAtEightInLisbonRatherThanServerTime (Theory x4) | A produção corrente muda às 8h de Lisboa (verão e inverno) | OK |  |
| DatedJobOn_CoveringOperationalDay_IsTheCurrent | Produção cujas datas cobrem o dia é a corrente; o futuro nunca | OK |  |
| DatedJobOn_EndedBeforeOperationalDay_FallsBackToUndated | Produção já terminada cede à última sem datas | OK |  |
| DiaDaTroca_AProducaoQueEntra_EACorrente_MesmoRegistadaAntes | No dia da troca aparecem as duas produções, a que entra primeiro, sem aviso | OK |  |
| JobOnWithoutBqContext_ShowsProductionWithoutBq | Produção sem BQ mostra a produção sem boquilha | OK |  |
| MachineWithoutProduction_ShowsNoProduction | Máquina sem produção mostra «Sem produção» | OK |  |
| OperationalDay_ChangesAtTheDayChangeHourInLisbon (Theory x4) | Dia operacional muda à hora de viragem configurada | OK |  |
| SobreposicaoReal_MostraAsDuasProducoes_ComAviso | Produções que se cruzam aparecem as duas, com aviso, sem bloquear | OK |  |
| UmaSoProducao_UmaLinha_SemAviso | Uma só produção dá uma linha sem aviso | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BqSemJobOn_SoAsBqSemATombstone_PorReferenciaELote_ComMaquinasEUltimaLinha | Escolha de BQ sem Job On: por referência e lote, com máquinas e última linha | OK |  |
| BqsDaReferencia_SoBqDaReferencia... | BQs da referência sem distinguir maiúsculas nem misturar tipos | OK |  |
| Ferramenta_DevolveAReferenciaEOLote... | Identifica a BQ pelo tool_id | OK |  |
| OsQuatroPedidos_NaoGravamNada | As consultas de escolha não gravam nada | FRACO | Verifica o ChangeTracker vazio e não a base relida |
| ProducaoDoFiltro_DevolveNumeroReferenciaELinha... | Produção do filtro devolve nº, referência e linha | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasFichaQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Ferramenta_DevolveReferenciaLoteEQuantidade... | Ficha da BQ com referência, lote e quantidade do lote | OK |  |
| MaquinasDaFerramenta_VemPorOrdemDoIdDaMaquina_SoDessaFerramenta | Máquinas da BQ na ficha, só as dela | OK |  |
| MovimentosSemJobOn_SoDestaBqSemJobOn_MaisRecentesPrimeiro_ComLimite | Movimentos sem Job On da BQ, último primeiro, com limite | OK |  |
| OsTresPedidos_NaoGravamNada | As consultas da ficha não gravam nada | FRACO | Verifica o ChangeTracker vazio e não a base relida |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasRegistoQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AccumulatedDiscrepancy_SumsTheAnnotatedExcesses | Discrepância acumulada soma os excedentes anotados | OK |  |
| AssociatedTrace_WithClearedAnchor_ResolvesToolViaBqContextChain | Registo associado resolve a BQ pelo bq_id, não pela âncora direta | OK |  |
| FindByContext_ToolNotRegisteredOrRemoved_HasNoTool | BQ inexistente ou removida: sem ferramenta | OK |  |
| FindByContext_WithAssociatedTrace_ReturnsTraceProductionAndTool | Abrir pelo bq_id dá produção, registo associado e BQ com quantidade do lote | OK |  |
| FindByContext_WithoutTrace_ReturnsToolAndNoTrace | bq_id sem registo dá a BQ e nenhum registo | OK |  |
| FindTrace_CarriesLotAccountedQuantity | Registo traz a quantidade do lote | OK |  |
| FindTrace_ReadsOnlyThePageAsked_TheTotalsCountEveryMovement | Registo lê 25 movimentos por página; totais contam todos | OK |  |
| MissingPiecesAreNotDiscrepancy_TheBalanceStaysLegitimatelyOut | Peças que faltam não são discrepância: ficam em reparação | OK |  |
| Movements_AreListedByRealDate_NotByInsertionOrder | Movimentos listados pela data real, não pela ordem de gravação | OK |  |
| Movements_CarryTheRealDateAndTheFrozenRepairer | Movimentos trazem a data real e o reparador congelado | OK |  |
| UnresolvedTrace_ResolvesToolViaDirectAnchor | Registo sem Job On resolve a BQ pelo tool_id | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqDiscrepancyFoldTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Apply_ClearsDiscrepancyOfSaidas_AndRecomputesEveryReturn | Recalcula todas as entradas e limpa a discrepância das saídas | OK |  |
| Apply_DiogosExample_OutOfOrderGuias_SortByTheRealTime | Exemplo do Diogo (20/15 e 10/14): -4 e 5 em reparação | OK |  |
| Apply_EqualDates_SaidaFirst_ThenIdAndTheUnsavedMovementGoesLast | À mesma hora a saída conta antes das entradas | OK |  |
| Apply_OrdersByRealDate_NotByInsertionOrder | Cálculo segue a data real, não a ordem de lançamento | OK |  |
| Backfill_SaidaDaSemanaPassada_ExplicaAEntradaDeHoje | Saída antiga lançada depois explica a entrada de hoje (relido da base) | OK |  |
| CountsAfterReset_UsesTheResetMinute_ForMovementsSavedAfterIt (Theory x4) | Depois de posta a zero conta pela data real ou pelo minuto do zero | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelLivroTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AsLinhasEscritasAMao_NoMesmoDia_FicamIntactas_AoEscreverEAoAnular | Linhas escritas à mão pelo chefe ficam intactas | OK |  |
| Atualizar_AsLinhasEscritasAMao_FicamIntactas_ENaoContamComoDiferenca | Linhas à mão não contam como diferença ao atualizar | OK |  |
| Atualizar_DoisDiasDiferentes_EDepoisUmIgual_AtualizaExatamenteDois | Atualizar escreve só os dias diferentes até um igual | OK |  |
| Atualizar_OntemIgual_ParaLogo_ESoEscreveHoje | Atualizar pára no primeiro dia igual | OK |  |
| Atualizar_PassaParaOMesAnterior_EPara_NoPrimeiroDiaDaApp | Atualizar passa de mês e pára no primeiro dia da app | OK |  |
| Atualizar_UmDomingoVazio_NaoPara_EEscreveOsDiasDeAntes | Atualizar não pára num domingo vazio | OK |  |
| CadaMes_FicaNoSeuSeparador_PorOrdem_ComAsDatasEmDiaMesAno | Cada mês no seu separador, datas dd/mm/aaaa | OK |  |
| Escreve_ARefEAsQuantidades_NaMaquinaENoDia_ESomaComoODiogo | Excel das BQ escreve referência/lote e quantidades por máquina e dia, somando | OK |  |
| Escrever_NumExcelNovo_GuardaOsEstilosEAsJuncoes | Escrever num Excel novo mantém estilos e junções | OK |  |
| OutroAno_EMaisDeCincoBoquilhasNoDia_FicamNosAvisos | Outro ano ou mais de 5 BQ no dia ficam nos avisos | OK |  |
| UmDiaReescrito_SemOMovimentoAnulado_FicaLimpo | Dia reescrito sem o movimento anulado fica limpo | OK |  |
| UmExcelNovo_SoTemOsDiasDoMes_EOMesSeguinteGanhaOsQueFaltam | Excel novo só tem os dias do mês | OK |  |
| UmExcelNovo_TemAsJuncoesAsCoresEAsLargurasDoOriginal | Excel novo com junções, cores e larguras do original | OK | Muito colado a números de estilo; falha ao mexer no tema sem mudar a regra |
| UmExcelNovo_TemOMesPedido_ERecebeOsMovimentos_EOsMesesSeguintes | Excel novo tem o mês pedido e recebe os movimentos | OK |  |
| UmFicheiroQueNaoEExcel_ERecusado | Ficheiro que não é Excel é recusado | OK |  |
| UmMesSemSeparador_CriaOSeparador_ComAsDatasEOsDiasDaSemana | Mês sem separador cria o separador com datas e dias da semana | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistoryPdfGeneratorTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DailyMatrix_ComMovimentosSemMaquina_AvisaNoPapel | PDF avisa quantos movimentos sem máquina ficaram fora da grelha | OK |  |
| DailyMatrix_FormatsTheGrid_FromTheA4ReadModel | PDF do resumo diário mostra por máquina «Saiu X · entrou Y», lido pelo texto | OK |  |
| DailyMatrix_WithEmptyPeriod_StillProducesTheSheet | PDF de período vazio sai com o cabeçalho | OK |  |
| GroupedHistory_PreservesEveryMovementIncludingUnknownMachineAndSeparateLots | Agrupar por máquina e registo não perde movimentos nem mistura lotes | OK |  |
| MovementList_FormatsTheMachineAndMonthLists_FromTheA4ReadModel | PDF da lista de movimentos com hora de Lisboa, reparador e produção | OK |  |
| Pdfs_WithAWarning_StillProduceTheSheet_WithTheWarningOnPaper | PDFs com aviso levam o aviso no papel | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqHistorySummaryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DailySummary_ExcludesMovementsWithoutMachine_KeepsThemInTheDetailedList | Resumo exclui movimentos sem máquina; a lista detalhada mantém-nos | OK |  |
| DailySummary_GroupsByDayMachineAndTrace_WithEsrCounts_OnTheRealDate | Resumo diário agrupa por dia, máquina e registo, com contagem E/S/SR | OK |  |
| HistoryAndCalendar_UseTheLisbonDateAtTheMonthBoundary (Theory x2) | Histórico e calendário usam a data de Lisboa na fronteira do mês | OK |  |
| MovementsList_FiltersByRangeTypeAndText_OverTheRealDate | Lista de movimentos filtra por intervalo, tipo, texto e máquina | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementAnTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Corrigir_SoAn_AceitaZeroBq_SemAnRecusa | Zero BQ só vale com AN (registar e corrigir) | OK |  |
| Corrigir_SoMudaOsAnQuandoOsTraz | Corrigir só muda os AN quando o pedido os traz | OK |  |
| Registar_AnNegativo_RecusaENadaGrava | AN negativo é recusado e nada grava | OK |  |
| Registar_ComAn_GravaOsAn_SemAnOuZeroFicaSemAn | Registar guarda a quantidade de AN; sem AN ou zero fica sem AN | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementCorrectionHandlersTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Delete_AnyMovement_EvenTheOldest_IsRemovedAndTheCountIsRecalculated | Apaga-se qualquer movimento, mesmo o mais antigo, com rasto | OK |  |
| Delete_MovementOfAnotherTrace_IsRefused | Apagar movimento de outro registo é recusado e nada se apaga | OK |  |
| Delete_RecentMovement_RemovesItAndRecomputes | Apagar movimento recalcula a discrepância | OK |  |
| Delete_Refused_WritesNoTrail | Apagar recusado não escreve rasto | OK |  |
| Delete_Success_WritesTheDeletionTrail | Apagar deixa o rasto da eliminação | OK |  |
| Delete_WithoutIdentity_StillDeletes_WithWhoLeftEmpty | Apagar sem identidade grava rasto com «quem» vazio | OK |  |
| Edit_Quantity_RecomputesTheReturnDiscrepancy | Corrigir a quantidade recalcula a discrepância da entrada | OK |  |
| Edit_RealDateInTheFutureBeyondTheTolerance_IsSaved_AndSaysSo (Theory x2) | Data no futuro (além de 10 min) grava e avisa | OK |  |
| Edit_RealDateWithinTheToleranceOrInThePast_IsSaved (Theory x3) | Data no limite da folga ou no passado grava sem aviso | OK | Limite coberto (9, 10 min); falta o caso 11 min só no theory acima |
| Edit_RealDate_MovesTheSaidaAfterTheEntrada_AndTheEntradaBecomesUnexplained | Corrigir a data real reordena o cálculo | OK |  |
| Edit_Refusals_LeaveTheMovementUntouched | Correção recusada (outro registo, tipo, quantidade 0) deixa o movimento intacto | OK |  |
| Edit_Refused_DoesNotRecordACorrection | Correção recusada não deixa rasto de correção | OK |  |
| Edit_Success_RecordsWhoCorrectedAndWhen | Correção guarda quem corrigiu e quando | OK |  |
| Edit_TimeInTheDstGap_MovesToTheNextHourAndReportsTheShift | Hora inexistente na mudança de hora passa à seguinte e avisa | OK |  |
| Edit_Type_SaidaToEntrada_KeepsFrozenFactsAndRecomputes | Corrigir saída para entrada mantém máquina, reparador e operador e recalcula | OK |  |
| Edit_ValidTime_ReportsNoShift | Hora válida não é deslocada | OK |  |
| Edit_WithoutIdentity_StillSaves_WithWhoLeftEmpty | Correção sem identidade grava com «quem» vazio | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementIntegrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AllThreeMovementTypes_PersistWithObservedQuantityAndProducedDiscrepancy | Os três tipos de movimento guardam-se com quantidade e discrepância | FRACO | Insere linhas direto no DbContext e relê; não passa por nenhum handler: só prova que o EF grava |
| Movement_ResolvesItsTrace | Movimento resolve o seu registo e a BQ | FRACO | Só testa o Include do EF, sem regra do negócio |
| Movements_DoNotChangeJobOnOrBqContextData | Gravar movimentos não altera o Job On nem o bq_id | FRACO | Nunca chama o handler de registo, por isso nunca falharia se o handler escrevesse no Job On |
| MultipleMovements_BelongToOnePreProductionTrace | Vários movimentos pertencem ao mesmo registo sem Job On | FRACO | Só insere e conta linhas; nenhum código da app é exercido |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementLabelsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Of_ComOTipoForte_FalaA_mesmaLinguaDaFolha (Theory x3) | Rótulos com o tipo forte | DUPLICADO | Repete Of_TraduzAsDuasVocabularios |
| Of_Desconhecido_NaoInventa (Theory x3) | Valor desconhecido sai tal e qual | OK |  |
| Of_NuncaDevolveOMnomeDoEnum | O nome cru do enum nunca aparece | DUPLICADO | Já coberto por Of_TraduzAsDuasVocabularios (Saida→Saída) |
| Of_SemValor_DevolveOAtraceDaCasa (Theory x3) | Sem valor mostra traço | OK |  |
| Of_ToleraEspacosADerroDaFonte | Tolera espaços à volta do tipo | OK |  |
| Of_TraduzAsDuasVocabularios (Theory x6) | Rótulos Saída / Entrada / Entrada sem reparação nos dois vocabulários | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairTraceIntegrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Association_ClearsToolIdAnchor_ToolReachedViaBqContext | Associar liga o registo ao bq_id e limpa a âncora direta, sem mexer no Job On | OK |  |
| ModelShape_UniqueBqContext_AndPartialUniqueToolIdOnUnresolved | Um registo por bq_id e um sem Job On por BQ (modelo) | FRACO | Só metadados do modelo EF, não a base real; duplica PreProductionTrace_...OneUnresolvedPerToolByIndex |
| PreProductionTrace_PersistsWithoutBqContext_OneUnresolvedPerToolByIndex | No máximo um registo sem Job On por BQ (índice único parcial) | FRACO | Inspeciona metadados do modelo EF, não a base; o bq_id único já tem prova em PostgreSQL (Data/NadaRepetido), o índice parcial por tool_id não |
| RelationalSchema_UniqueBqContext_AndPartialUniqueToolId | Esquema SQL tem os índices únicos do registo | FRACO | Verifica texto do script gerado, sem PostgreSQL a recusar duplicados |
| SameTool_ManyProductionTracesAcrossTime | A mesma BQ tem um registo por produção ao longo do tempo | FRACO | Insere linhas diretas e conta; não exercita código da app |
| Trace_ResolvesCanonicalToolId | O registo resolve a BQ canónica pelo tool_id | FRACO | Só insere e lê com Include do EF; nenhum código da app |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairTraceWritePathTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AlreadyAssociatedTrace_ReassociationRefused | Registo já associado não se reassocia | OK |  |
| Association_DoesNotModifyJobOnOrBqContextData | Associar não altera Job On nem bq_id | OK |  |
| Association_PreservesTraceIdentityAndMovements_ClearsToolId | Associar mantém identidade e movimentos do registo | OK |  |
| BqContextAlreadyTracedByAnotherTrace_RefusedCleanly | bq_id que já tem registo recusa outro, sem alterar | OK |  |
| Create_AfterAssociation_CreatesNewTraceBecauseAnchorWasCleared | Depois de associar, novo registo sem Job On é possível | OK |  |
| ExistingToolId_CreatesTraceWithNullBqContextId | Criar registo para uma BQ existente, sem Job On | OK |  |
| ExistingTrace_AssociatesToExistingBqContext | Associar um registo a um bq_id existente | OK |  |
| PreProductionTrace_WithRetroactiveMovements_AssociatesToRetroactiveJobOn_KeepsMovementsAndDates | Registo antigo associa-se a Job On retroativo e mantém movimentos e datas | OK |  |
| SecondCreate_ForSameTool_ReusesExistingUnresolvedTrace | Segundo registo da mesma BQ sem Job On reutiliza o existente | OK |  |
| ToolMismatchBetweenTraceAndBqContext_Refused | Associar com BQ diferente da produção é recusado | OK |  |
| UnknownBqContext_Refused | Associar a bq_id inexistente é recusado e nada muda | OK |  |
| UnknownToolId_Refused | BQ inexistente é recusada e nada se grava | OK |  |
| UnknownTrace_Refused | Associar registo inexistente é recusado | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairerWriteHandlersTests.Remover.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Delete_ReparadorQueNaoExiste_RecusaTipada | Remover reparador inexistente é recusado | OK |  |
| Delete_SaiOReparador_AsSaidasGuardamONome_EAsRotasFicamSemReparador | Remover reparador: saídas guardam o nome e a rota fica vazia | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairerWriteHandlersTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Create_EmptyName_RefusesTyped (Theory x3) | Reparador sem nome é recusado e nada grava | OK |  |
| Create_NameAlreadyInUse_RefusesTyped | Nome de reparador repetido é recusado | OK |  |
| Create_NameIsTrimmed_BeforeSaving | Nome do reparador grava sem espaços à volta | OK |  |
| Create_WithTheName_SavesAndEmitsTheId | Criar reparador de BQ com nome | OK |  |
| Edit_ChangesTheName_WithoutTouchingTheFrozenMovements | Renomear reparador não altera o nome congelado nos movimentos antigos | OK |  |
| Edit_MissingRepairer_RefusesTyped | Renomear reparador inexistente é recusado | OK |  |
| Edit_NameAlreadyUsedByAnotherRepairer_RefusesTyped | Renomear para nome de outro reparador é recusado e nada muda | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoPorBqTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BqQueSaiuNumJobOn_EVoltaNoutro_NaoFicaComSaldo | Saldo da BQ conta todas as produções: sai numa e volta noutra, sem discrepância | OK | Cobre DMO-574 |
| EntraMaisDoQueSaiu_EmTodasAsProducoes_FicaComSaldo | Entra mais do que saiu em todas as produções: discrepância negativa, gravada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/RegisterBqMovementForContextTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FirstMovement_CreatesOneAssociatedTrace_AndTheMovement | 1.º movimento cria o registo ligado ao bq_id e o movimento | OK |  |
| FutureRealDate_IsSaved_AndSaysSo | Data real no futuro grava no 1.º movimento e avisa | OK |  |
| RefusedMovement_KeepsTheUnresolvedTraceUnassociated | Movimento recusado não associa o registo sem Job On | OK |  |
| RefusedMovement_LeavesNoTrace | Movimento recusado não deixa registo criado | OK |  |
| SecondMovement_ReusesTheTrace | 2.º movimento reutiliza o registo e calcula o saldo | OK |  |
| UnknownContext_IsRefusedAsTraceNotFound | bq_id inexistente é recusado e nada se cria | OK |  |
| UnresolvedTraceOfTheSameTool_IsAssociatedWithItsMovements | Registo sem Job On da mesma BQ associa-se ao 1.º movimento da produção | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/RegisterBqMovementHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AssociatedTrace_WithClearedToolId_ResolvesToolViaBqContext | Registo associado resolve a BQ pelo bq_id ao registar | OK |  |
| DistinctMovementToken_PreservedInHistory | Entrada sem reparação mantém-se distinta no histórico | OK |  |
| EntradaSemReparacao_AfterSaida_ReducesOutstanding | Entrada sem reparação abate ao que está fora | OK |  |
| EntradaSemReparacao_Excess_RecordsFullQuantityAndNegativeDiscrepancy | Entrada sem reparação a mais grava a discrepância negativa | OK |  |
| Entrada_AfterEntradaSemReparacao_UsesRemainingOutstanding | Entrada depois de uma sem reparação usa o que resta fora | OK |  |
| Entrada_FollowsTraceHistorySequence | Entradas seguem o histórico do registo | OK |  |
| Entrada_FullyMatched_HasNoDiscrepancy | Entrada igual à saída não tem discrepância | OK |  |
| Entrada_NeverCarriesRepairer_EvenWithTheRouteConfigured | Entrada nunca leva reparador, mesmo com rota | OK |  |
| Entrada_WithUnexplainedQuantity_RecordsFullQuantityAndNegativeDiscrepancy | Entrada a mais grava a quantidade inteira e discrepância negativa | OK |  |
| Entrada_WithoutAnySaida_IsAcceptedInFull | Entrada sem saída anterior é aceite por inteiro | OK |  |
| MovementRegistration_DoesNotDependOnToolType | Registar movimento numa ferramenta CM também grava | FRACO | Não é regra do negócio das Boquilhas (só BQ); não falha se a regra de tipo for estragada |
| MovimentoComDataRetroativa_ApareceNoHistoricoEContadoresPelaDataReal | Movimento retroativo aparece no histórico pela data real | OK |  |
| MovimentoComDataRetroativa_PersisteADataReal_EAuditoriaFicaOAgora | Data retroativa grava a data real; a auditoria fica no agora | OK |  |
| MovimentoSemData_GravaComOAgoraDoBackend_UsoDiarioIntacto | Sem data grava com o agora do servidor | OK |  |
| Register_RealDateInTheFutureBeyondTheTolerance_IsSaved_AndSaysSo (Theory x3) | Data no futuro (além de 10 min) grava e avisa | OK |  |
| Register_RealDateInThePast_IsSaved_LatePaperworkStillGoesIn | Guia em atraso (data passada) grava | OK |  |
| Register_RealDateWithinTheTolerance_IsSaved (Theory x3) | Data até ao limite da folga grava sem aviso | OK |  |
| Register_TheRepeatedHourOfTheClockChange_TheNowIsNotTheFuture | Hora repetida na mudança de hora não conta como futuro | OK |  |
| RegisteringMovements_DoesNotModifyJobOnBqContextOrTrace | Registar movimentos não altera Job On, bq_id nem registo | OK |  |
| SaiCincoEntraTres_MissingPiecesAreNotDiscrepancy_TheBalanceIsLegitimate | Sai 5, entra 3: sem discrepância, 2 ficam fora | OK |  |
| SaiTresEntraCinco_OwnerConvention_EntradaAnnotatesMinusTwo_LoteNeverGrows | Sai 3, entra 5: discrepância -2 na entrada, o lote nunca cresce | OK |  |
| Saida_AvulsoToolWithoutMachineLink_SavesWithoutRepairer | BQ sem máquina associada grava sem reparador | OK |  |
| Saida_AvulsoTrace_RoutesViaTheToolAssociatedMachine | Registo sem Job On usa a máquina associada à BQ para a rota | OK |  |
| Saida_ChosenMachineControlsRepairer_AndHistoryKeepsMachineAfterCompatibilityChanges | A máquina escolhida decide o reparador e o histórico guarda-a | OK |  |
| Saida_MachineRouteConfigured_FreezesTheRepairerOnTheMovement | Saída congela o reparador da rota da máquina | OK |  |
| Saida_MachineWithoutRoute_SavesWithoutRepairer_NeverBlocks | Máquina sem rota grava sem reparador, nunca bloqueia | OK |  |
| Saida_MultipleCompatibilitiesWithoutChoice_DoesNotPickArbitraryRepairer | Várias máquinas possíveis sem escolha não inventam reparador | OK |  |
| Saida_RegistersMovementWithoutDiscrepancy | Saída grava sem discrepância | OK |  |
| Saida_RouteConfigChangedLater_OldMovementKeepsItsFrozenRepairer | Mudar a rota depois não altera o reparador dos movimentos antigos | OK |  |
| Saida_RoutePointingToMissingRepairer_SavesWithoutRepairer | Rota com reparador inexistente grava sem reparador | OK |  |
| TraceToolMissingFromRegistry_Refused | BQ fora do registo de ferramentas é recusada e nada grava | OK |  |
| UnauthorizedTypeTokens_Refused (Theory x4) | Tipo de movimento desconhecido é recusado e nada grava | OK |  |
| UnknownTrace_RefusedTraceNotFound | Registo inexistente é recusado | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/ResetBqDiscrepancyTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AccumulatedDiscrepancy_CountsAMovementSavedAfterTheReset_InTheSameMinute | Movimento gravado depois do zero, no mesmo minuto, volta a contar | OK |  |
| AccumulatedDiscrepancy_CountsOnlyTheMovementsAfterTheReset | Discrepância acumulada só conta movimentos depois do zero | OK |  |
| Reset_ClearsTheSaldoOfTheEarlierLines_ButKeepsTheMovements | Depois do zero as linhas antigas não contam, mas ficam | OK |  |
| Reset_SavesWhoAndWhen_AndKeepsTheMovements | Pôr a zero guarda quem e quando e mantém os movimentos | OK |  |
| Reset_UnknownTrace_IsRefused | Pôr a zero num registo inexistente é recusado | OK |  |

### `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ACm_NaoMostraOPar_EAPosicaoEscritaCom4Algarismos | Entrada sem escolha do par e posição escrita com 4 algarismos | OK |  |
| AEntrada_EntraSoACm | Entrada pelo formulário grava só a CM escolhida | OK |  |
| UmaFerramentaSemPar_NaoMostraAEscolha | Ferramenta sem par não mostra escolha do par | FRACO | Só «não aparece»; sem «aparece» antes na mesma zona (REGRAS 11); redundante com ACm_NaoMostraOPar |

### `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLinhaEReparadorPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Entrada_DaReparacao_MostraOReparadorEALinhaComQueVem_EGravaOQueAPessoaMudou | Entrada vinda da reparação traz reparador e linha, editáveis, e grava o que a pessoa mudou | OK |  |
| FichaDeUmaBq_NaoTemASeccaoDaCm | Ficha de BQ não mostra a secção de RI e pesos da CM | FRACO | Só «não aparece», sem verificar a secção numa ficha de CM no mesmo teste |
| SaidaParaProducao_PedeALinhaDepoisDoDestino_EGravaOJobOn | Saída para Produção pede a linha e grava linha e Job On | OK |  |

### `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLoteNovoPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentaQueJaExiste_LigaAoRegistoEmVezDeCriar | Ferramenta já existente liga ao lote novo em vez de duplicar | OK |  |
| Registar_GuardaPorCriar_SemCriarFerramenta_EMostraNoLog | Lote novo fica por criar, sem criar ferramenta, no log só do tipo certo | OK |  |
| Registar_SoRecusaAPosicaoSemQuatroAlgarismosEOQueFalta | Lote novo recusa posição sem 4 algarismos ou sem lote, mantém o escrito e nada grava | OK |  |
| Sumario_AvisaSoQuemTemFerramentas_ECriarResolveEDaAEntrada | Sumário avisa o lote novo a quem tem Ferramentas; criar resolve e dá a entrada | OK |  |

### `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemModoEntradaSaidaPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Entrada_MostraSoAsDeFora_ComOLoteNovo_EAbreAEntrada | Modo Entrada mostra só as ferramentas de fora e o lote novo | OK |  |
| Saida_MostraSoAsDasPosicoes_SemLoteNovo_EAPosicaoProcuraOQueLaEsta | Modo Saída mostra só as que estão em posições, sem lote novo | OK |  |
| SemModo_EAEntrada_EOModoSegueNaProcura | Sem modo escolhido abre em Entrada e o modo segue na procura | OK |  |

### `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemMovimentosPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ComAsFerramentas_ALinhaDasUltimasAbreAFicha_NoRegistoENosMovimentos | Clique na linha abre a ficha, sem botões nas linhas | OK |  |
| CorrigirLocalizacao_TiraDoArmazem | Corrigir localização tira do armazém e volta ao Fabrico | OK |  |
| Entrada_NumaPosicaoQueNaoExiste_PerguntaECriaComCriarERegistar | Posição inexistente: pergunta, nada grava, «Criar e registar» grava | OK |  |
| Entrada_PeloFormulario_PoeNaPosicaoEPassaASaida | Entrada pelo formulário põe na posição e passa a Saída | OK |  |
| OLog_MostraSoOTipo_DeTodos_EALinhaAbreAFerramenta | Log do registo só do tipo escolhido, linha clicável, sem botões | OK |  |
| OMesmoEnvioDuasVezes_GravaUmaVez | Mesmo envio duas vezes grava uma só vez | OK | A garantia de concorrência está em PostgresMigrationIntegrationTests.ArmazemMovimentos (Data/) |
| OPainel_TemCmMfBq_ESemTipoPedeParaEscolher | Painel CM, MF, BQ; sem tipo pede para escolher; sem procura não há lista | OK |  |
| Procurar_MostraOUsoDaUltimaLeitura_ESemLeituraUmTraco | Procura mostra o % uso da última leitura, ou traço | OK | Liga pelo tool_id |
| Procurar_SemFerramenta_AvisaComOLinkParaAsFerramentas | Sem resultado, link para criar a ferramenta nas Ferramentas | OK |  |
| Procurar_SoNoCartaoDoTipo_MostraOndeEstaEAbreAFerramenta | Procura só no cartão do tipo; sem registo mostra «Sem registo» | OK |  |
| Saida_ODestinoEscolheOsCamposEDepoisAEntradaPropoe | Destino escolhe os campos; entrada seguinte propõe origem e estado | OK |  |
| Saida_ParaReparacaoSemReparador_RecusaComOFormularioPreenchido | Saída para reparação sem reparador é recusada, mantém o escrito e nada grava | OK |  |
| SemAChave_NaoEntra | Sem a chave do Armazém a página não abre | OK |  |
| Registo_VoltarFicaPorCimaDoTitulo_EAFabricaDoEmprestimoEUmaLista | «Voltar» fica por cima do título e a Fábrica do Empréstimo é uma lista das Definições, nunca texto livre | OK | A-0412 (DMO-667) |
| Saida_DeBq_ParaReparacao_OferecePorGruposOsReparadoresDasBqEDoArmazem | Na Saída de uma BQ há os reparadores das BQ e, a seguir, os do Armazém | OK | A-0421 (DMO-667) |

### `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemPaginasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminTemplates_MostraAChaveDoArmazem | Templates mostram as chaves dos módulos com o rótulo «Módulo · ação» | OK |  |
| ComAChave_RegistoMovimentosEDefinicoes | Separadores Registo, Movimentos, Definições; Posições e Próximos já não existem | OK |  |
| Definicoes_CriarEMudarReparadores | Criar, renomear e arquivar reparadores; nomes repetidos recusados | OK |  |
| SemAChave_NaoEntra (Theory x1) | Definições do Armazém não abrem sem a chave | OK |  |

### `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemUltimosPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AFicha_MostraALocalizacaoEOsMovimentos_SoAQuemTemOArmazem | Ficha mostra localização e estado a todos; o link só a quem tem Armazém | OK |  |
| AsFuncoes_RegistoDepoisMovimentos_SemHistorico | Barra do Armazém: Registo, Movimentos; sem Histórico, Mapa, Posições, Próximos | OK |  |
| Fora_SemFerramentas_NaoAbreAFicha | Sem a chave das Ferramentas a linha não abre a ficha | OK |  |
| Fora_SoCmEMf_ComReparadorEDia_BqNao | Fora em reparação lista só CM/MF com reparador e dia, linha clicável sem botões | OK |  |
| SemAChave_NaoEntra | Movimentos do Armazém não abrem sem a chave | OK |  |
| SemRegistoNoArmazem_AFichaDizSemRegisto_ENaoFabrico | Ferramenta sem registo no Armazém diz «Sem registo», não «Fabrico» | OK |  |
| Ultimas_OsFiltros | Filtros por dia, tipo, referência, lote e reparador; vazio com aviso | OK |  |
| Ultimas_Todas_DoMaisRecente_BqIncluida_PosicaoSoNaEntrada | Últimas entradas e saídas com o último em cima, BQ incluída, sem botões | OK |  |
| Ultimas_OFormularioTemUmEnvioEscondido_ParaOEnterNoLote | O Enter no campo Lote das Últimas entradas e saídas aplica o filtro | OK | A-0387 (DMO-649) |

### `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ClampMatrixPeriod_CutsAtTheFirst366DaysAndExplains | Resumo diário corta aos 366 dias e explica | OK |  |
| DailySummaryGrid_RendersDayByMachine_FromTheBackend | Resumo diário: dia por máquina com E/S/SR em quantidades, duplo clique abre o registo | OK | Parte das verificações é de classes CSS (entry-e); o texto e as quantidades é que contam |
| DetailedMovementsView_ListsOneMovementPerLine_WithoutGroupHeaders_AndItsPdf | Movimentos detalhados: um por linha, sem cabeçalhos, e o seu PDF | OK | PDF só verificado por tipo e nome |
| DetailedMovements_AEntradaSemReparacao_NuncaVaiCruParaOEcran | «Entrada sem reparação» aparece em português, nunca o nome do enum | OK |  |
| DetailedMovements_AreATableWithTheHumanFacts_AndTheDoubleClickOpens | Movimentos detalhados em tabela; clique abre a ficha da ferramenta | OK |  |
| FilterCounters_AreQuantitySums_NotStockBalances | Totais do filtro são somas de quantidades por tipo | OK |  |
| Filters_TypeMachineTextAndRange_WorkOverTheRealDate | Filtros do histórico por tipo, máquina, texto e datas | OK |  |
| GroupedHistory_KeepsLotsSeparateAndUsesFrozenMovementMachine | Vista por linha/Job On separa lotes e usa a máquina congelada do movimento | OK |  |
| HistoryDay_ReplacesRangePreservesFiltersAndExportsTheSameSelection | Escolher um dia mantém os filtros e o PDF segue a mesma seleção | OK |  |
| History_CalendarDay_ChangesOnlyTheZones | Escolher um dia no calendário só muda as zonas (sem piscar) | FRACO | Compara atributos data-dmo-regiao e classes de markup, não o efeito para o operador (REGRAS 10) |
| History_Filters_ChangeOnlyTheZones | Mudar filtros só troca as zonas; a lista e os totais mudam | OK | Metade é estrutura de markup; as contagens de movimentos validam o efeito |
| History_MonthOnly_ListsThatMonthsMovements | Só com o mês escolhido lista os movimentos desse mês | OK |  |
| History_OpensBlank_UntilAReferenceOrAPeriodIsSearched | Histórico abre vazio até haver pesquisa ou período | OK |  |
| MachineFilterWithoutPeriod_ShowsOnlyTheMostRecent_AndTheListAndPdfAgree | Só máquina, sem período: mostra os 500 mais recentes e avisa | OK |  |
| Matrix_LongerThan366Days_ShowsTheFirst366AndWarns | Grelha com mais de 366 dias mostra os primeiros e avisa | OK |  |
| Matrix_MovimentosSemMaquina_AvisaComLinkParaALista | Grelha avisa quantos movimentos sem máquina ficaram de fora, com link | OK |  |
| MonthJump_RebuildsTheCalendarAndTheMatrixPeriod | Mudar de mês refaz o calendário e o período da grelha | OK |  |
| PdfHistorico_ForAMonth_ReturnsTheMonthPdf | PDF de um mês descarrega | FRACO | Só %PDF e tamanho > 1000 bytes |
| PdfHistorico_MatrixLongerThan366Days_GeneratesTheFirst366Days | PDF da grelha com mais de 366 dias sai cortado | FRACO | Só %PDF; não verifica o corte |
| PdfHistorico_MatrixView_ReturnsTheGridPdf | PDF da grelha do resumo diário descarrega | FRACO | Só verifica tipo, nome e cabeçalho %PDF; não lê o texto (REGRAS 12) |
| PdfHistorico_WithTheMachineFilter_ReturnsTheMachinePdf | PDF filtrado por máquina descarrega | FRACO | Só %PDF e nome do ficheiro; não verifica que só tem a máquina B1 |
| PdfHistorico_WithoutPeriodOrFilter_ExportsTheCalendarMonth | PDF sem período exporta só o mês do calendário | OK | Conta páginas do PDF |
| PdfHistorico_WithoutTheMachine_ExportsAllMachines | PDF sem máquina exporta todas | FRACO | Só %PDF e nome; não verifica o conteúdo |
| Sidepanel_SurvivesTheHistoryViews | Painel das produções correntes mantém-se nas vistas do histórico | OK |  |
| ViewDropdown_AndContextualPdfButton_AreWiredToTheGenerators | Dropdown de vistas e um só botão «Imprimir / PDF» ligado aos filtros | OK |  |
| ReferenciaExistente_ComLoteInexistente_MostraMensagemClara_NaListaENoPdf | Referência com lote que não existe mostra mensagem clara, na lista e no PDF, sem erro | OK | A-0387 (DMO-648) |

### `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasRegistoConsultasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| Registo_GetFazPoucasConsultas | Abrir o Registo e o histórico faz poucas consultas à base (limites) | OK | Guarda de desempenho, não regra de negócio; os limites foram subindo a cada funcionalidade |

### `tests/DMO.Alpha.Web.Tests/BoquilhasAnTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.An_ADiscrepanciaTambemContaParaOsAn | A discrepância também conta para os AN (Diogo 28/10) | OK |  |
| BoquilhasRegistoPageTests.An_IlegivelOuNegativo_NaoGravaNada_EOFormularioVoltaComOQueSeEscreveu | AN ilegível ou negativo é recusado, nada grava, mantém o escrito | OK | Formulário montado à mão (REGRAS 9) |
| BoquilhasRegistoPageTests.An_NaFichaDaBq_GravaEMostraBqEAn | Na ficha da BQ grava e mostra a quantidade de AN junto das BQ | OK |  |
| BoquilhasRegistoPageTests.An_NoRegisto_GravaComOMovimento_EContaOsAnFora | Registo grava os AN com o movimento e conta os AN fora/na fábrica | OK |  |
| BoquilhasRegistoPageTests.An_NosMovimentos_OFiltroPecaAn_MostraSoOsMovimentosComAn | Filtro «Peça» nos Movimentos mostra só os com AN | OK |  |
| BoquilhasRegistoPageTests.An_RegistarSemJobOn_APecaVemEmBq_ComAnGravaSoOsAn | Registar sem Job On: peça BQ por omissão; com AN grava só os AN | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasAvisosTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.AssociateForm_SendsTheChosenReference_AndOnlyItsProductionIsAssociated | Associar liga só a produção escolhida; sem a BQ é recusado | OK |  |
| BoquilhasRegistoPageTests.AssociateForm_WithTheStartOfTheReference_AssociatesTheChosenProduction_NotAnotherWithTheSameNumber | Associar pelo início da referência liga a produção escolhida; Job On inexistente é recusado | OK |  |
| BoquilhasRegistoPageTests.AssociateJobOnForm_ReferenceFirst_ThenOnlyItsProductions | Associar: primeiro a referência, depois só as produções dela, botão «Associar» | OK |  |
| BoquilhasRegistoPageTests.EditMovement_TimeInTheDstGap_SavesAtTheNextHourAndWarns | Corrigir com hora inexistente grava na seguinte e avisa uma vez | OK |  |
| BoquilhasRegistoPageTests.EditMovement_UnreadableDate_IsRefusedWithAReadableCause | Corrigir com data ilegível é recusado com a causa | FRACO | Não verifica que o movimento ficou igual |
| BoquilhasRegistoPageTests.PostAssociateJobOn_ByNumberOnlyOrSameReference_AssociatesWithoutWarning | Associar pelo nº ou com a mesma referência não avisa | OK |  |
| BoquilhasRegistoPageTests.PostAssociateJobOn_ByNumberWithADifferentReference_AssociatesAndWarns | Associar pelo nº com referência diferente associa e avisa | OK |  |
| BoquilhasRegistoPageTests.PostAssociateJobOn_SameNumberInSeveralReferences_PicksTheProductionWithThisBq | Mesmo nº em várias referências: ganha a produção com esta BQ | OK |  |
| BoquilhasRegistoPageTests.PostMovement_TimeInTheDstGap_SavesAtTheNextHourAndWarns | Hora inexistente na mudança de hora grava na seguinte e avisa | OK |  |
| BoquilhasRegistoPageTests.PostMovement_UnreadableQuantity_ShowsTheCauseInsteadOfAnEmptyBox | Quantidade ilegível é recusada com a causa e nada grava | OK | Formulário montado à mão (REGRAS 9) |
| BoquilhasRegistoPageTests.PostMovement_ValidTime_HasNoWarning | Hora válida não dá aviso e a data mantém-se no formulário | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasDiscrepancyResetTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.DiscrepancyReset_ClickOpensTheWindow_AndPuttingToZeroKeepsTheMovements | Meter a zero: o valor abre a janela, guarda quem e quando, mantém os movimentos | OK |  |
| BoquilhasRegistoPageTests.DiscrepancyReset_WithoutDiscrepancy_ShowsNoLink | Sem discrepância não há link nem janela de meter a zero | OK |  |
| BoquilhasRegistoPageTests.PostMovement_WithAnOldDate_KeepsThatDateInTheForm_WithNowGoesBackToNow | Data antiga mantém-se no formulário depois de gravar; hora de agora volta ao agora | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasMovementCorrectionTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.DeleteMovement_RecentMovementIsDeleted | Apagar movimento recalcula e deixa o rasto de quem apagou | OK |  |
| BoquilhasRegistoPageTests.DeleteMovement_TheOldestOfSixCanBeDeleted | Apaga-se qualquer movimento, mesmo o mais antigo | OK |  |
| BoquilhasRegistoPageTests.EditMovement_ChangesTheTypeAndRecomputesTheBalance | Corrigir tipo recalcula o saldo e guarda quem corrigiu; quantidade 0 recusada sem alterar | OK |  |
| BoquilhasRegistoPageTests.MovementCorrection_IsHiddenUntilTheEditModeIsOpened | Corrigir movimentos só abre numa janela; apagar pede confirmação da app | OK | Janela fecha com «Fechar»/«Cancelar»; a decisão do Diogo para painéis é «Voltar» (confirmar se janelas contam) |
| BoquilhasRegistoPageTests.Registo_ShowsOnlyThePageAsked_AndAMovementFromAnotherPageOpensToCorrect | Registo mostra 25 movimentos por página; um de outra página abre para corrigir | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasOrdemTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.GuiasOutOfOrder_TableFollowsTheRealTime_AndTheSaidaCountsFirstAtTheSameTime | Guias fora de ordem: cálculo pela hora real, saída antes da entrada, último em cima | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraPeloInicioTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.AssociarJobOn_PassoDois_PeloInicioOuPelaParteBq_NuncaAMeio | Associar: referência pelo início ou pela parte BQ, nunca a meio | OK |  |
| BoquilhasRegistoPageTests.Historico_PesquisaSugereReferenciasEBq_EADicaFalaNumClique | Pesquisa do histórico sugere referências e BQ | OK |  |
| BoquilhasRegistoPageTests.ProcurarBq_JaNaoResponde_ComALista | O pedido antigo «Procurar BQ» já não devolve lista | FRACO | Só «não aparece»; a condição inicial aceita 404 ou qualquer página com DOCTYPE |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoOpcoesTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.Registo_AlterarLote_ABqPassaParaOLoteEscolhido_EORegistoAbreNoLoteNovo | Alterar lote passa a produção para o lote escolhido; movimentos ficam no antigo | OK | Formulário montado à mão (REGRAS 9) |
| BoquilhasRegistoPageTests.Registo_LinkHistorico_AbreOHistoricoFiltradoPorEstaBqELote_ComCadaReferenciaTotal | Opções › Movimentos abre o histórico filtrado pela BQ e lote | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CartoesComLink.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.Cartoes_BqLoteAbreAFicha_ORestoAbreOJobOnDaProducao | Cartão BQ·lote abre a ficha (só com Ferramentas); restantes abrem o Job On | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.DataNoFuturo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.EditMovement_RealDateInTheFuture_IsSaved_AndWarnsInTheCorner | Corrigir para o futuro grava e avisa; passado grava sem aviso | OK |  |
| BoquilhasRegistoPageTests.PostMovement_RealDateInTheFuture_IsSaved_AndWarnsInTheCorner | Data real no futuro grava e avisa no canto, com link para corrigir | OK |  |
| BoquilhasRegistoPageTests.PostMovement_RealDateWithinTheTolerance_OrInThePast_IsSaved_WithoutWarning | Data dentro da folga ou no passado grava sem aviso | OK |  |
| BoquilhasRegistoPageTests.RealDate_TheFieldIsLisbonTime_TheServerComparesInUtc_SummerAndWinter (Theory x3) | Hora de Lisboa vs UTC no aviso de futuro, no verão e no inverno (9 e 11 min) | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.EntradaPeloFormulario.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.EntradaComADataReal_PeloFormularioDaPagina_GravaOTipoEAHoraDeLisboaEmUtc | Entrada e entrada sem reparação pelo formulário gravam tipo e hora de Lisboa em UTC | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.EnvioUnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.RegisterMovement_OtherEnvio_IsANewMovement | Dois envios diferentes iguais são dois movimentos | OK |  |
| BoquilhasRegistoPageTests.RegisterMovement_SameEnvioAtTheSameTime_TheSecondWaitsAndSavesNothing | Mesmo envio ao mesmo tempo grava uma só vez | OK | Simulado em memória com atraso; sem PostgreSQL para o envio único das Boquilhas |
| BoquilhasRegistoPageTests.RegisterMovement_SameEnvioTwice_SavesOnceAndAnswersWithTheSameRedirect | Mesmo envio duas vezes grava uma só vez e responde igual | OK | Chave de envio em memória |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.ErrosDiogo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.JobOnMudou_FicaNumaLinhaNoCabecalhoDoRegisto_SemBlocoPorBaixoDosCartoes | Aviso «Job On mudou» no cabeçalho do registo, com o que mudou | OK | Exige o triângulo (.dmo-caret) no summary |
| BoquilhasRegistoPageTests.PainelDasLinhas_MostraABqEmMaquinaComOLote_EAProducaoNumaLinhaDiscreta | Painel das linhas mostra a BQ com lote antes da produção | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.FormularioDaPagina.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.MovimentoPeloFormularioDaPagina_GravaAQuantidadeEAMaquinaNaBQ | Formulário desenhado grava quantidade e máquina na BQ | OK | Usa EnvioDoFormulario e relê da base |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.HistoricoDiario.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.Historico_AAbaAbreOResumoDiario_EACelulaAbreORegistoComOJobOn | Histórico: resumo diário e célula abre registo com Job On | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.HoraDeViragem.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.DeNoiteAntesDaViragem_AProcuraEOsRegistosSeguemAProducaoEmMaquina | Antes da hora de viragem, procura segue a produção em máquina | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.RecusaGuardaOEscrito.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.RecusaDoMovimento_OFormularioVoltaComOQueSeEscreveu_ENadaGrava | Recusa devolve o formulário preenchido e nada grava | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.SoVer.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.O_painel_de_quem_so_ve_abre_os_movimentos_da_producao_escolhida_e_so_esses | Painel de quem só vê mostra só movimentos da produção escolhida | OK |  |
| BoquilhasRegistoPageTests.Quem_cria_nas_Boquilhas_continua_a_registar_e_a_abrir_os_registos | Quem cria continua a registar e abrir registos | OK |  |
| BoquilhasRegistoPageTests.Quem_so_ve_as_Boquilhas_e_recusado_ao_registar_e_nada_fica_gravado | Quem só vê é recusado ao registar e nada grava | OK |  |
| BoquilhasRegistoPageTests.Quem_so_ve_as_Boquilhas_ve_os_registos_no_Historico_sem_formularios_de_registo | Quem só vê, vê histórico sem formulários de registo | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.Sobreposicao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.DiaDaTroca_AQueEntraSemBq_MostraAProducaoEAbreOJobOn_NaoSemProducao | Produção que entra sem BQ abre o Job On, não «sem produção» | OK | Regra de Diogo 22/10/2026 |
| BoquilhasRegistoPageTests.DiaDaTroca_OPainelMostraAsDuasProducoes_SemAviso | Dia da troca: painel mostra as duas produções sem aviso | OK |  |
| BoquilhasRegistoPageTests.DiaDaTroca_RegistaPeloPainelUmMovimentoEmCadaProducao_NoJobOnCerto | Dia da troca: um movimento em cada produção, no Job On certo | OK | Relê da base |
| BoquilhasRegistoPageTests.SobreposicaoReal_OPainelMostraAsDuasProducoes_ComAviso | Sobreposição real na linha mostra as duas produções com aviso | OK |  |
| BoquilhasRegistoPageTests.UmaSoProducao_OPainelFicaComoAntes | Uma só produção na linha sem aviso de sobreposição | OK | Afirma a ausência de classe CSS dmo-rail-warning; o aviso por texto já é coberto |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.ZeroNoBrowser.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.PostaAZero_ADicaDaDiscrepanciaFicaNaEtiqueta_ParaOBrowser | Discrepância metida a zero mostra quem e quando na ajuda | OK | Também exporta HTML para inspeção no browser |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.Anonymous_MovementPost_RedirectedToLogin_NothingPersisted | Anónimo não regista movimento, vai para o login | OK |  |
| BoquilhasRegistoPageTests.Anonymous_NativeTraceRead_RedirectedToLogin | Anónimo é enviado para o login ao ler registo | OK |  |
| BoquilhasRegistoPageTests.CanonicalTraceId_LoadsPersistedTrace | Registo carrega pelo identificador e mostra produção e quantidades | FRACO | Afirma ficheiro CSS e classe de navegação, não regras |
| BoquilhasRegistoPageTests.CatalogRows_OpenTheToolSheetOnDoubleClick | Linha do catálogo abre a ficha; lista vazia até procurar | OK |  |
| BoquilhasRegistoPageTests.Historico_ReparadorColumn_ShowsTheFrozenNameOfTheRoutedSaida | Saída congela o reparador da rota e Histórico mostra-o | OK |  |
| BoquilhasRegistoPageTests.Historico_ShowsTheDayMovementsTable_AndTheSidepanelSurvivesTheTabs | Histórico em branco até pesquisar; tabela de movimentos | OK |  |
| BoquilhasRegistoPageTests.LastMovements_ShowTheRealDateAndTheFrozenRepairer | Últimos movimentos mostram data real e reparador congelado | OK |  |
| BoquilhasRegistoPageTests.MachineInUse_ListsOnlyTheMachinesOfTheTool_AndQuantityComesFirst | Campo da máquina escondido; Registar antes de Saída | FRACO | Testa ordem de marcação, não regra; nome já não corresponde |
| BoquilhasRegistoPageTests.MachinesCard_ShowsTheThreeLiveValues_WhenTheCurrentBqExists | Registo mostra em reparação e discrepância do lote | FRACO | Nome diz três valores, só verifica dois; discrepância só por texto do rótulo |
| BoquilhasRegistoPageTests.MachinesCard_WithoutCurrentProduction_StaysAsIs_NoLiveValues | Linha sem produção não mostra valores do lote | OK | Precedido do caso com produção noutros testes |
| BoquilhasRegistoPageTests.MachinesPanel_DirectLinkOpensProductionRegisterAndKeepsSearchCollapsed | Painel de linhas abre o registo da produção; linha sem produção inerte | FRACO | Muitas asserções de classes CSS; escreve HTML de evidência |
| BoquilhasRegistoPageTests.MovementForm_RendersTheRealDateField_PreFilledWithNow | Formulário traz data e hora reais preenchidas com agora | OK | Tolerância de 2 minutos |
| BoquilhasRegistoPageTests.NativePath_ServerDataIsSourceOfTruth | Registo e movimento vêm do servidor, não do browser | DUPLICADO | Repete PostSaida_PersistsThroughHandler |
| BoquilhasRegistoPageTests.PersistedToolAndContextFacts_RenderFromServerData | Cartões de referência e produção vêm da base | OK |  |
| BoquilhasRegistoPageTests.PostAssociateJobOn_AlreadyTracedBqContext_RefusedOneTracePerBq | Um bq_id nunca recebe segundo registo | OK | Garantia em PG está em Data |
| BoquilhasRegistoPageTests.PostAssociateJobOn_BqNotInProduction_RefusedByToolCorrelation | Associar a produção de outra BQ recusado | OK |  |
| BoquilhasRegistoPageTests.PostAssociateJobOn_RefusalsTypedWithPtCause | Associar sem dados ou produção inexistente recusado | OK |  |
| BoquilhasRegistoPageTests.PostAssociateJobOn_WithHumanFacts_AssociatesAndKeepsMovementsAndDates | Associar Job On mantém movimentos e datas | OK |  |
| BoquilhasRegistoPageTests.PostEntradaSemReparacao_PersistsWithClosedSemantics | Entrada sem reparação grava, sem discrepância, em reparação desce | OK |  |
| BoquilhasRegistoPageTests.PostEntrada_ServerComputedDiscrepancyReflected | Entrada maior que o fora calcula discrepância -2 | OK |  |
| BoquilhasRegistoPageTests.PostMovement_DoesNotModifyJobOnOrBqContext | Registar movimento não altera Job On nem contexto BQ | OK |  |
| BoquilhasRegistoPageTests.PostMovement_WithRetroactiveRealDate_PersistsItAndTheAuditStaysNow (Theory 2) | Data retroativa grava em UTC (verão e inverno), auditoria no agora | OK |  |
| BoquilhasRegistoPageTests.PostMovement_WithoutTheRealDateField_GravaComOAgoraDoBackend | Sem data real grava com a hora do servidor | OK |  |
| BoquilhasRegistoPageTests.PostSaida_PersistsThroughHandler | Saída grava quantidade e mostra em reparação | OK | Formulário montado à mão; envio real em FormularioDaPagina |
| BoquilhasRegistoPageTests.RegisterMetrics_RenderFromTheBackend_NeverFromTheClient | Quantidade do lote, fabricação e em reparação calculadas; sem lote «—» | OK |  |
| BoquilhasRegistoPageTests.RegisterMovement_ChosenMachineIsSavedAndShownInTheRegister | Máquina escolhida grava-se e aparece no registo | OK |  |
| BoquilhasRegistoPageTests.RegisterMovement_UserWithoutTemplate_IsRefusedAndNothingIsWritten | Sem módulo Boquilhas não entra nem regista | OK |  |
| BoquilhasRegistoPageTests.ToolSheet_ShowsTheFactsTheHistoricalFormAndTheRegisters | Ficha da BQ mostra factos, movimentos sem Job On e registos | OK |  |
| BoquilhasRegistoPageTests.UnassociatedRegister_ShowsTheAssociateJobOnForm_AssociatedDoesNot | Registo sem Job On mostra formulário de associar; associado não | OK |  |
| BoquilhasRegistoPageTests.UnknownMovementType_RefusedViaTypedRefusal | Tipo de movimento desconhecido recusado, nada grava | OK |  |
| BoquilhasRegistoPageTests.UnknownTrace_RefusedCleanly | Registo inexistente recusado e nada grava | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.Cartoes_InicioEFimSaoOsDoJobOn_SemInicioAvisoAmarelo | Início e fim do registo vêm do Job On; sem início avisa | FRACO | Distingue o aviso pela classe CSS dmo-fact-falta em vez do texto |
| BoquilhasRegistoPageTests.FirstMovement_CreatesTheTrace_AndTheSecondReusesIt | Primeiro movimento cria o registo, os seguintes reutilizam-no | OK | Relê da base |
| BoquilhasRegistoPageTests.OpeningAMachine_DoesNotCreateTheTrace | Abrir uma máquina não cria registo e mostra os cartões | OK | Muitas asserções de marcação HTML mas cobre regras de Diogo |
| BoquilhasRegistoPageTests.OpeningAMachine_WarnsAboutTheUnresolvedTraceOfTheSameBq | Aviso de registo sem Job On da mesma BQ e associação | OK |  |
| BoquilhasRegistoPageTests.RefusedFirstMovement_ReturnsToTheMachine_WithoutCreatingTheTrace | Primeiro movimento recusado não cria registo | OK | Quantidade 0 testada; refusa nada guarda |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoTrocaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.Registo_AssociarJobOn_GravaSemRecarregar | Associar Job On troca só a zona do registo | OK |  |
| BoquilhasRegistoPageTests.Registo_ComUmaJanelaAberta_OFocoVaiParaAJanela | Com janela de correção aberta o foco vai para ela | FRACO | Afirma atributos autofocus e classes; pouco valor de negócio |
| BoquilhasRegistoPageTests.Registo_TrocaSoAsZonas_SemFocoAutomaticoNoTipo | Registo troca só as zonas, sem foco automático no tipo | FRACO | Testa classes CSS e ordem de marcação, não comportamento |

### `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOn.EnvioUnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.SemJobOn_OMesmoEnvioGravaUmaVez_OutraChavePermiteOutroMovimento (Theory 2) | Registo sem Job On: mesmo envio grava uma vez, também em simultâneo | OK | Relê da base; concorrência em memória, a garantia em PG está em Data |

### `tests/DMO.Alpha.Web.Tests/BoquilhasSemJobOnTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.RegistarSemJobOn_ALinhaPropostaEAUltimaOndeTeveMovimentos_SeForDaFicha | Linha proposta é a última com movimentos, se da ficha | OK | Três casos |
| BoquilhasRegistoPageTests.RegistarSemJobOn_ALinhaSoTemAsDaFichaDaBq_EVemJaEscolhida | Linha limitada às da ficha da BQ e já escolhida | OK |  |
| BoquilhasRegistoPageTests.RegistarSemJobOn_NaPaginaBoquilhas_GravaEMostraOMovimento | Registar sem Job On grava BQ, linha e quantidade e mostra-o | OK |  |
| BoquilhasRegistoPageTests.RegistarSemJobOn_NumJobOnAnterior_FicaNoRegistoDesseJobOn_EODaEntradaDizQuemRegistou | Registar em Job On anterior; Job On de outra BQ recusado | OK | Recusa JobOnErrado sem verificar explicitamente que nada grava antes do segundo envio |
| BoquilhasRegistoPageTests.RegistarSemJobOn_SemQuantidade_ReabreAJanelaComAFrase | Sem Job On: quantidade 0 recusada, janela reaberta, nada grava | OK |  |
| BoquilhasRegistoPageTests.SemJobOnTool_Label_MostraOQueIdentificaABq_PelaReferencia | Etiqueta da BQ com referência, lote, quantidade, máquinas e MF | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.CliquesRepetidos.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.ToolCatalog_SemOFormularioCurto_OPostAntigoNaoGravaEAbreORegisto_EAGuardadaMostraAMensagemNaZona | POST antigo de criar boquilha não grava e abre Ferramentas | OK |  |

### `tests/DMO.Alpha.Web.Tests/BoquilhasToolCatalogTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BoquilhasRegistoPageTests.ToolCatalog_CriarBoquilha_AbreORegistoDasFerramentasComBqEAMaquina_EVoltaComAFicha | Criar boquilha abre Ferramentas com BQ e máquina e volta à ficha | OK | Exporta HTML de evidência se a variável existir |
| BoquilhasRegistoPageTests.ToolCatalog_PorPagina_NaPaginacao_NuncaNosFiltros | «Por página» fica na paginação, nunca nos filtros | OK |  |
| BoquilhasRegistoPageTests.ToolCatalog_SearchAndPaginationPreserveSelectionContext_AndExcludeOtherToolTypes | Pesquisa e paginação do catálogo mantêm contexto e excluem outros tipos | OK |  |
| BoquilhasRegistoPageTests.ToolCatalog_SoAMaquinaNaoMostraALista_OTextoMostra_EAMaquinaSoReduz | Catálogo só mostra lista com texto; máquina só reduz | OK |  |

### `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.EmailDoAdminComConta.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BqRepairerSettingsPageTests.AdminEmailSetting_EmailDeUmUtilizador_GravaEEsseEmailEntraComoAdmin | Email de utilizador existente grava e entra como ADMIN | OK | Teste de Admin dentro da área Boquilhas |
| BqRepairerSettingsPageTests.AdminEmailSetting_EmailSemConta_NaoGravaEAvisaComLinkParaCriarOUtilizador | Email do ADMIN sem conta não grava e avisa | OK | Teste de Admin dentro da área Boquilhas; deveria estar com os de Admin |
| BqRepairerSettingsPageTests.AdminEmailSetting_OProprioEmailComOutrasMaiusculas_Grava | Mesmo email do ADMIN com outras maiúsculas grava | OK | Teste de Admin dentro da área Boquilhas |

### `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Excel.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BqRepairerSettingsPageTests.ExcelDasBoquilhas_Atualizar_EscreveOntem_EDizQueDiasAtualizou | Atualizar escreve ontem e diz que dias atualizou | OK | Falta o caso «ontem igual não atualiza» |
| BqRepairerSettingsPageTests.ExcelDasBoquilhas_OCartao_AVersaoMudaComUmMovimento_EOFicheiroVoltaEscrito | Excel das Boquilhas: versão muda e movimento escrito na célula do dia | OK | Só cobre Saída; Entrada e AN sem teste |
| BqRepairerSettingsPageTests.ExcelDasBoquilhas_SemFicheiroOuSemSerExcel_RespondeComAMensagem | Sem ficheiro ou não-Excel: recusa com mensagem | OK |  |
| BqRepairerSettingsPageTests.ExcelDasBoquilhas_UmExcelNovo_TemOSeparadorDoMes | Excel novo tem os 12 meses | OK |  |

### `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.Remover.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BqRepairerSettingsPageTests.DeleteRepairer_PelaJanela_SaiDaLista_ASaidaGuardaONome_EARotaFicaVazia | Remover reparador: saída guarda o nome, rota fica vazia | OK |  |

### `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.UmFormularioPorLinha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BqRepairerSettingsPageTests.EditRepairer_EnvioDeRecurso_GravaONomeDoReparadorEditado | Envio de recurso grava só o reparador editado | OK |  |
| BqRepairerSettingsPageTests.MachineRoute_DuasPessoasEmMaquinasDiferentes_FicamAsDuasRotas | Duas pessoas em linhas diferentes mantêm as duas rotas | OK |  |

### `tests/DMO.Alpha.Web.Tests/BqRepairerSettingsPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| BqRepairerSettingsPageTests.AdminEmailSetting_ConfirmationMismatch_KeepsTheFieldAndTheSession | Confirmação do email diferente: não grava, sessão mantém-se | OK |  |
| BqRepairerSettingsPageTests.AdminEmailSetting_InvalidEmail_IsTypedRefusalAndKeepsTheField | Email do ADMIN inválido recusado, campo intacto | OK |  |
| BqRepairerSettingsPageTests.AdminEmailSetting_SaveUpdatesTheFieldAndPreviousSessionDies | Guardar email do ADMIN grava e termina a sessão anterior | OK |  |
| BqRepairerSettingsPageTests.BoquilhasDefinicoes_IsATabOfTheModule_AndOnlyForWhoHasIt | Definições das Boquilhas só para quem tem o módulo | OK |  |
| BqRepairerSettingsPageTests.BoquilhasSection_RendersTheRepairerList_WithZeroEntitiesWarning | Definições sem reparadores mostram aviso e secção Linhas | OK |  |
| BqRepairerSettingsPageTests.BoquilhasSection_WithEntities_RendersTheMachineMapOfTheSixCodes | Mapa das seis linhas com seletor de reparador sem botões | OK |  |
| BqRepairerSettingsPageTests.CreateRepairer_DuplicateName_TypedRefusal_KeepsTheOriginal | Reparador com nome repetido recusado, fica só o original | OK |  |
| BqRepairerSettingsPageTests.CreateRepairer_Post_PersistsTheEntity_AndListsIt | Criar reparador grava e lista | OK |  |
| BqRepairerSettingsPageTests.EditRepairer_InPlace_AnswersJson_AndTheTableHasNoSaveButton | Nome edita-se no valor; vazio recusado; sem botões na tabela | OK |  |
| BqRepairerSettingsPageTests.EditRepairer_Post_RenamesTheEntity | Editar reparador muda o nome | OK |  |
| BqRepairerSettingsPageTests.MachineRoute_CodeOutsideTheSixMachines_TypedRefusal | Código de linha fora das seis recusado, nada grava | OK |  |
| BqRepairerSettingsPageTests.MachineRoute_EmptyValue_ClearsTheRoute | «Sem reparador» limpa a rota da linha | OK |  |
| BqRepairerSettingsPageTests.MachineRoute_Post_SavesTheModuleSettingKey_AndSelectsIt | Rota da linha grava a definição e fica escolhida | OK |  |
| BqRepairerSettingsPageTests.MachineRoute_Posting_WithoutAdminSession_IsRedirectedToLogin | Sem sessão não grava reparadores, vai para o login | OK |  |

## Job On, Ferramentas, Planeamento, SAP

691 linhas: OK 617, FRACO 71, DUPLICADO 3

### `tests/DMO.Alpha.Infrastructure.Tests/Planeamento/SumarioLimiteTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SumarioLimiteTests.PoucasLinhas_UmaLeituraComoAntes | Sumário ordena dias recentes primeiro e conta tipos | OK |  |
| SumarioLimiteTests.QuinhentasProducoesNoDia1_NaoEscondemADoDia24 | Sumário mostra todas as produções do mês, sem corte por limite | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/AlterarLoteBqTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AlterarLoteBqTests.Alterar_AUtilizacaoDaFolhaPassaParaADoLoteNovo_0SeENovo | Ao trocar o lote, a % uso da folha segue o lote novo | OK |  |
| AlterarLoteBqTests.Alterar_DuasVezes_TodosOsLotesUsados_PelaOrdem | Trocar o lote duas vezes lista todos os lotes usados | OK |  |
| AlterarLoteBqTests.Alterar_LoteNovoNumBqIdNovo_OAntigoFicaNaProducao_ComRasto | Lote novo nasce em bq_id novo, antigo fica com rasto | OK |  |
| AlterarLoteBqTests.Alterar_NumLoteJaTrocado_NaoMudaNada | Trocar de novo um lote já trocado não grava nada | OK |  |
| AlterarLoteBqTests.Alterar_OutraReferenciaOutroTipoOuOMesmo_NaoServe_ENadaMuda (Theory x4) | Lote de outra referência, tipo ou igual é recusado sem gravar | OK |  |
| AlterarLoteBqTests.Lotes_SoAsOutrasBqDaMesmaReferencia_PorOrdemDoLote | Alterar lote só oferece BQ da mesma referência | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/CreateJobOnExperienciaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CreateJobOnExperienciaTests.Experiencia_UsaAnoZeroZero_EntraESaiNoMesmoDia_ENaoBloqueiaAProducaoDaLinha | Job On de experiência (ano 00) não bloqueia a produção da linha | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/CreateJobOnHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CreateJobOnHandlerTests.Create_AllToolsSelected_EstablishesProductionContext | Criar Job On grava contextos CM/MF/BQ e controlo | OK |  |
| CreateJobOnHandlerTests.Create_DuplicateProductionNumber_RefusedWithoutPartialWrite | Mesmo nº da mesma referência é recusado, nada fica a meio | FRACO | Só em memória: o índice único e a transação (duas pessoas ao mesmo tempo) não são provados; falta teste PostgreSQL. |
| CreateJobOnHandlerTests.Create_MfOnlySelected_MinimalProduction_SingleMfContext | Produção mínima só com MF cria um contexto e controlo | OK |  |
| CreateJobOnHandlerTests.Create_MissingMfTool_Refused (Theory x2) | Job On sem MF é recusado e nada grava | OK | Regra do Diogo de 10/10 (MF obrigatório); confirmar que a folha nova mantém. |
| CreateJobOnHandlerTests.Create_MissingProductionNumber_Refused (Theory x3) | Sem nº de produção a criação é recusada e nada grava | OK |  |
| CreateJobOnHandlerTests.Create_MissingReference_Refused (Theory x3) | Sem referência a criação é recusada e nada grava | OK |  |
| CreateJobOnHandlerTests.Create_OnlyApplicableContexts_AreCreated | Só se criam contextos das ferramentas escolhidas | OK |  |
| CreateJobOnHandlerTests.Create_RecordsWhenTheJobOnWasSaved | Job On guarda a data/hora em que foi criado | OK |  |
| CreateJobOnHandlerTests.Create_SameProductionNumberOtherReference_Succeeds | Mesmo nº com outra referência é permitido | OK |  |
| CreateJobOnHandlerTests.Create_SameReferenceOtherCase_RefusedAsDuplicate | Referência em minúsculas conta como duplicado | OK |  |
| CreateJobOnHandlerTests.Create_SameToolIdInTwoSlots_IsNotRefused | Mesma ferramenta em CM e MF não é recusada | OK | Regra negativa sem decisão registada na memória; confirmar que se quer. |
| CreateJobOnHandlerTests.Create_SameToolInTwoProductions_EachKeepsItsOwnContext | Mesma ferramenta em duas produções tem contextos próprios | OK |  |
| CreateJobOnHandlerTests.Create_TrimsSurroundingWhitespaceFromTextFacts | Espaços à volta do nº e referência são tirados | OK |  |
| CreateJobOnHandlerTests.Create_UnknownBqTool_Refused | BQ inexistente é recusada sem gravar | OK |  |
| CreateJobOnHandlerTests.Create_UnknownCmTool_Refused | CM inexistente é recusada sem gravar | OK |  |
| CreateJobOnHandlerTests.Create_UnknownMachine_Refused | Máquina inexistente é recusada sem gravar | OK |  |
| CreateJobOnHandlerTests.Create_UnknownMfTool_Refused | MF inexistente é recusado sem gravar | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/DeleteJobOnHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DeleteJobOnHandlerTests.Confirmacao_AvisaAPartirDoDiaDeInicio (Theory x3) | Confirmar eliminação avisa se o dia de início já chegou | OK |  |
| DeleteJobOnHandlerTests.Confirmacao_ContaOQueSePerde | Confirmação conta pesos, pegamentos, punções e movimentos BQ | OK |  |
| DeleteJobOnHandlerTests.Confirmacao_SemDiaDeInicio_NaoAvisa | Sem dia de início a eliminação não avisa | OK |  |
| DeleteJobOnHandlerTests.Eliminar_ApagaTudoDaProducao_DeixaRastoDosMovimentos_EAOutraProducaoFica | Eliminar apaga a produção toda e deixa a outra | OK |  |
| DeleteJobOnHandlerTests.Eliminar_OutraVez_DizQueJaNaoExiste_SemMexerEmNada | Eliminar duas vezes diz que já não existe | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/EdicoesAoMesmoTempoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EdicoesAoMesmoTempoTests.Ferramenta_OutraVersao_Recusa_FactosETecnica | Editar ferramenta com versão antiga é recusado | FRACO | Versão sempre 0 em memória (regra 7). |
| EdicoesAoMesmoTempoTests.Ferramenta_SemLinhaTecnica_AVersaoAbertaE0 | Ferramenta sem linha técnica abre com versão 0 e grava | OK |  |
| EdicoesAoMesmoTempoTests.JobOn_MesmaVersao_Grava | Editar Job On com a versão certa grava | FRACO | Versão sempre 0 em memória; não prova o token. |
| EdicoesAoMesmoTempoTests.JobOn_OMesmoEnvioOutraVez_AbreOGravado_SemOutraLinha | Duplo clique em guardar não duplica registos | OK |  |
| EdicoesAoMesmoTempoTests.JobOn_OutraVersao_Recusa_SemGravarNada | Editar Job On com versão antiga é recusado sem gravar | FRACO | Em memória a versão é sempre 0 (regra 7); o token de concorrência só se prova em PostgreSQL. |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/FolhaDaFerramentaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FolhaDaFerramentaTests.FerramentaNova_Utilizacao0_ESemJobOnNenhum | Ferramenta nova começa com 0% de uso e sem Job On | OK |  |
| FolhaDaFerramentaTests.FerramentaUsada_AUltimaPercentagemDela_EAsNotasDoUltimoJobOn | Folha traz última % da ferramenta (pelo id) e notas do último Job On | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnCaloteTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnCaloteTests.ContextoReadModel_TrazACalote | Contexto da produção devolve a calote | OK |  |
| JobOnCaloteTests.Create_CaloteNegativa_RecusaTipadaSemEscrita | Calote negativa na criação é recusada sem gravar | OK |  |
| JobOnCaloteTests.Create_ComCalote_GravaNoJobOn | Calote indicada na criação fica gravada | OK |  |
| JobOnCaloteTests.Create_SemCalote_NullPorDefeito | Sem calote fica vazia (usa a referência da ferramenta) | OK |  |
| JobOnCaloteTests.Update_CaloteEditavel_GravaOValorSubmetido | Editar calote grava o valor novo | OK |  |
| JobOnCaloteTests.Update_CaloteLimpa_VoltaAReferenciaDaTool | Limpar a calote volta a usar a referência da ferramenta | OK |  |
| JobOnCaloteTests.Update_CaloteNegativa_RecusaTipadaSemEscrita | Calote negativa na edição é recusada e mantém o valor | OK |  |
| JobOnCaloteTests.Update_CaloteZero_Valida_CalotePlana | Calote 0 é válida (calote plana) | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnChangeTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnChangeTests.FollowUp_LeadsToTheModuleTheChangeAffects (Theory x6) | Cada mudança aponta para o módulo a verificar | OK |  |
| JobOnChangeTests.Notices_ShowOnlyChangesAfterTheRecord_ThatTheModuleWatches | Avisos de troca só mostram mudanças posteriores ao registo, por módulo | OK |  |
| JobOnChangeTests.Notices_WithNoChangeAfterTheRecord_ShowNothing | Sem mudança posterior não há aviso | OK |  |
| JobOnChangeTests.PegamentosNotice_UsesTheDateOfTheSheetShown | Cada folha de Pegamentos avisa só do que mudou depois dela | OK |  |
| JobOnChangeTests.RemoveByMistake_KeepsTheRow_MarksWhoAndWhen_HidesItFromTheMarkerAndNotices_ValuesUnchanged | Remover aviso de troca por engano apaga só o aviso | OK | Nome diz «KeepsTheRow» mas o corpo apaga a linha; nome enganador. |
| JobOnChangeTests.Update_AddingABqWhereThereWasNone_IsNotAChange_AndOldRowsAreHidden | Pôr BQ onde não havia não conta como mudança | OK |  |
| JobOnChangeTests.Update_RecordsOneLinePerChangedField_WithHumanValuesAndWho | Cada campo alterado fica registado com valores legíveis e autor | OK |  |
| JobOnChangeTests.Update_Refused_RecordsNothing | Edição recusada não regista mudanças | OK |  |
| JobOnChangeTests.Update_WithNothingChanged_RecordsNothing | Guardar sem alterar não regista mudanças | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnCommandChecksTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnCommandChecksTests.Criar_VerificaAsFerramentasPelaOrdemDeAntes (Theory x12) | Criar valida CM, MF e BQ pela mesma ordem | OK |  |
| JobOnCommandChecksTests.Editar_VerificaAsFerramentasComoOCriar (Theory x12) | Editar valida as ferramentas como o criar | OK |  |
| JobOnCommandChecksTests.SemMaquina_AMaquinaVemAntesDoMfEmFalta_NoCriarENoEditar | Máquina inexistente é verificada antes do MF em falta | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFichaQueryTests.AsLeiturasSaoPelaOrdemDaPagina_FactosEFolha | Ordem interna das leituras da ficha | FRACO | Testa detalhe de implementação (ordem de chamadas), não uma regra do negócio. |
| JobOnFichaQueryTests.Duplicar_NaoLeFactos_MasLeAFolhaQueCopia | Duplicar copia a folha sem ler factos das ferramentas | OK |  |
| JobOnFichaQueryTests.FolhaGravada_Presente_OuAusenteLeSeComoVazia | Folha ausente lê-se como vazia | OK |  |
| JobOnFichaQueryTests.JobOnAberto_DevolveFactosEFolha | Ficha aberta devolve factos das ferramentas e a folha | OK |  |
| JobOnFichaQueryTests.NaoLeNotasDeLoteNemAlteracoesNemOQueSePerdeAoEliminar | Ficha não lê notas de lote nem alterações | FRACO | Verifica por reflexão propriedades e construtor da classe, não comportamento do negócio. |
| JobOnFichaQueryTests.OPedido_NaoGravaNada | Abrir a ficha não grava nada | OK |  |
| JobOnFichaQueryTests.SemFerramentas_NaoLeFactos | Sem ferramentas não se leem factos | OK |  |
| JobOnFichaQueryTests.SoUmaFerramenta_LeFactosSoDessa | Com só uma ferramenta lê só os factos dela | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolgasRecusaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFolgasRecusaTests.DoisNumerosNumaFolga_ERecusada (Theory x4) | Duas folgas escritas num só campo são recusadas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolgasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFolgasTests.UmNumeroPorEspaco (Theory x7) | Folgas escritas juntas separam-se um número por espaço | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaRondaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFolhaRondaTests.Alteracoes_SoAFormaDoNumero_NaoEMudanca | Mudar só a forma do número não conta como alteração | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFolhaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFolhaTests.CampoCurto_NoLimiteCabe_LogoAcimaERecusado (Theory x3) | Campo curto aceita 60 caracteres e recusa 61 | OK |  |
| JobOnFolhaTests.Catalogo_CadaChaveEUnica_CurtaETemColunaNaFolha | Cada campo da folha tem chave única e coluna | OK |  |
| JobOnFolhaTests.Catalogo_OsParesSaoOsDoProprietario_ECadaFamiliaAparecaUmaVez | Famílias da folha em pares CM+TP, MF+CS, BQ+AN, PU+ARR, PI+CAL | OK |  |
| JobOnFolhaTests.Catalogo_OsTecnicosMenosUsadosFicamNoMaisDetalhes | Campos técnicos menos usados ficam em «Mais detalhes» | OK |  |
| JobOnFolhaTests.Catalogo_SemFo_EComQtdStockEQtdMaqEscritasEmCadaFamiliaQueAsTem | Folha sem FO e com QTD Stock/Máq. em cada família | OK |  |
| JobOnFolhaTests.ChaveRecusada_DaORotuloComAFamilia_EOMaximo | Recusa de campo longo diz a família e o máximo | OK |  |
| JobOnFolhaTests.Criar_ComUmTextoLongoDemais_ERecusado_ENadaFicaGravado | Texto longo demais recusa a criação sem gravar nada | OK |  |
| JobOnFolhaTests.Criar_GravaAFolhaEACal_ELeSeDeNovo | Criar Job On grava a folha e a CAL | OK |  |
| JobOnFolhaTests.Criar_SemNadaEscrito_NaoCriaLinhaDaFolha_ELeSeVazia | Folha em branco não cria linha na base | OK |  |
| JobOnFolhaTests.Editar_ComAMesmaFolha_NaoRegistaNada_ESemFolhaNoEnvioAFolhaFica | Guardar a mesma folha não regista; sem folha no envio mantém-na | OK |  |
| JobOnFolhaTests.Editar_ComUmTextoLongoDemais_ERecusado_ENadaMuda | Texto longo na edição recusa tudo, nada muda | OK |  |
| JobOnFolhaTests.Editar_GravaOQueMudou_ERegistaUmaLinhaComOsRotulos | Editar a folha regista uma mudança com os rótulos | OK |  |
| JobOnFolhaTests.EliminarJobOn_LevaAFolhaEACal | Eliminar o Job On apaga folha e linhas CAL | OK |  |
| JobOnFolhaTests.LinhaCal_AQtdMaqTemOMesmoMaximo (Theory x3) | QTD Máq. da linha CAL aceita 60 e recusa 61 | OK |  |
| JobOnFolhaTests.LinhasCal_AsPorOmissaoPorEscreverNaoFicam_UmaSoComAQtdMaqFica | Linhas CAL por omissão sem dados não ficam gravadas | OK |  |
| JobOnFolhaTests.Normalizar_TiraAsPontas_CrlfPassaALf_EOVazioPassaANull (Theory x5) | Texto da folha limpa pontas e quebras de linha | OK |  |
| JobOnFolhaTests.Nota_ContaCadaQuebraDeLinhaComoUmCaracter (Theory x3) | Nota aceita 1000 caracteres e recusa 1001 | OK |  |
| JobOnFolhaTests.NotasGerais_NoLimiteCabem_LogoAcimaSaoRecusadas (Theory x3) | Notas gerais aceitam 4000 caracteres e recusam 4001 | OK |  |
| JobOnFolhaTests.Resumo_ListaOQueMudou_EContaNoFimOQueNaoCabe | Resumo das alterações da folha lista e abrevia | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnHistoricoQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnHistoricoQueryTests.DataIlegivel_AvisaEIgnora | Data ilegível avisa e é ignorada | OK |  |
| JobOnHistoricoQueryTests.DesdeDepoisDeAte_TrocaAsDatasEAvisa | Datas trocadas avisam e são corrigidas | OK |  |
| JobOnHistoricoQueryTests.MaquinaDesconhecida_NaoFiltra | Máquina desconhecida não filtra | OK |  |
| JobOnHistoricoQueryTests.OPedido_NaoGravaNada | Consultar o histórico não grava nada | OK |  |
| JobOnHistoricoQueryTests.OsTresFormatosDeData_SaoAceites (Theory x4) | Histórico aceita datas ISO, barras e hífens | OK |  |
| JobOnHistoricoQueryTests.Paginacao_UmaPaginaDeCadaVezMaisRecentePrimeiro | Histórico pagina com o mais recente primeiro | OK |  |
| JobOnHistoricoQueryTests.ProducaoComNumeroEReferencia_EscolheEssaReferencia | Nº e referência escolhem exatamente essa produção | OK |  |
| JobOnHistoricoQueryTests.ProducaoDeOutraReferencia_NaoFiltra | Produção de outra referência não filtra | OK |  |
| JobOnHistoricoQueryTests.ProducaoMaisAntigaPedidaPorLink_EntraNoDropdownEFiltra | Produção antiga pedida por link entra na lista | OK |  |
| JobOnHistoricoQueryTests.ProducaoSoComNumero_EscolheAMaisRecenteComEsseNumero | Só o nº da produção escolhe a mais recente | OK |  |
| JobOnHistoricoQueryTests.ReferenciaEMaquina_FiltraDropdownEResultados | Referência e máquina filtram lista e resultados | OK |  |
| JobOnHistoricoQueryTests.SemFiltro_NaoProcuraENaoLePagina | Histórico sem filtro não procura nada | OK |  |
| JobOnHistoricoQueryTests.SoReferencia_ListaAsProducoesEPesquisaPelaReferencia | Histórico por referência lista as produções, mais recente primeiro | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnListQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnListQueryTests.ListByReferenceAsync_EListPageOfReferenceAsync_SemDistinguirMaiusculas (Theory x3) | Referência sem distinguir maiúsculas, exata | OK |  |
| JobOnListQueryTests.ListByReferenceAsync_SoAsUltimas5 | Por referência mostra só as últimas 5 | OK |  |
| JobOnListQueryTests.ListNextPerMachineAsync_APrimeiraAEntrarDepoisDoDia_PorMaquina | Próxima produção a entrar por máquina | OK |  |
| JobOnListQueryTests.ListPageOfReferenceAsync_TodasAsDaReferenciaExata_Paginadas_ComAsFerramentas | Todas as produções da referência, paginadas, com ferramentas | OK |  |
| JobOnListQueryTests.ListProductionsOfReferenceAsync_EHistorico_PeloInicioDaReferenciaOuDaParteBq (Theory x5) | Procura pelo início da referência ou da parte BQ | OK |  |
| JobOnListQueryTests.ListProductionsOfReferenceAsync_OMesmoNumeroEmDuasReferencias_CincoOpcoesComAReferencia | Mesmo nº em duas referências dá 5 opções distintas | OK |  |
| JobOnListQueryTests.ListProductionsOfReferenceAsync_SoAsUltimas5 | Lista de produções mostra só as últimas 5 | OK |  |
| JobOnListQueryTests.SearchHistoryPageAsync_ComAReferenciaDaProducao_SoEssaProducao | Histórico com referência da produção mostra só essa | OK |  |
| JobOnListQueryTests.SearchHistoryPageAsync_EmptyProduction_ReturnsEmptyList | Sem produções o histórico vem vazio | FRACO | Trivial: lista vazia devolve vazio, passa com quase qualquer implementação. |
| JobOnListQueryTests.SearchHistoryPageAsync_ReturnsJobOnsWithEstablishedContext | Histórico traz produções com as ferramentas, mais recente primeiro | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnLotNoteHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnLotNoteHandlerTests.ForDay_SoAsNotasDoDiaDasProducoesIndicadas_ComOTextoDaMudanca | Notas de troca de lote do dia, só das produções pedidas | OK | Notas de lote já não se criam (só lidas); confirmar que ainda se quer. |
| JobOnLotNoteHandlerTests.ForJobOn_TodasAsNotasDaProducao_PorDia | Todas as notas de lote da produção por dia | OK |  |
| JobOnLotNoteHandlerTests.NextAfter_APrimeiraTrocaDepoisDoDia | Primeira troca de lote depois do dia | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnMaquinaDoJobOnTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnMaquinaDoJobOnTests.MaquinaDoJobOn_DevolveALinhaDaProducao_OuZeroQuandoNaoExiste | Devolve a linha da produção, ou 0 se não existe | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnOverlapQueryTests.EveryWindow_NeverWarnsBetweenProductionsOfTheSameLine_InMemory | Entre produções da mesma linha nunca há aviso de sobreposição | OK | Falharia se o aviso entre produções da linha voltasse; sobra `_ = expected` por limpar. |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnOverlapWarningTests.Create_AcabaNoDiaEmQueAOutraComeca_GravaSemAviso_NaVesperaNaoCruza (Theory x3) | Acabar no dia em que outra começa não cruza | OK |  |
| JobOnOverlapWarningTests.Create_ComecaNoDiaEmQueAOutraAcaba_GravaSemAviso_NoDiaSeguinteNaoCruza (Theory x3) | Dia da troca não conta como sobreposição (véspera, dia, dia seguinte) | OK | Diogo 21/10: sai dia 20, entra dia 20. |
| JobOnOverlapWarningTests.Create_EntirelyOpenWindow_NothingToCompare_ZeroWarning | Janela toda em aberto não gera aviso | FRACO | Só verifica «sem aviso»; passa sempre. |
| JobOnOverlapWarningTests.Create_OpenEndAgainstOpenStart_SavesWithoutWarning | Existente «até aviso» sobreposta grava sem aviso | OK |  |
| JobOnOverlapWarningTests.Create_WithOverlapOnSameMachine_SavesWithoutWarning | Criar com janela sobreposta na mesma máquina grava sem aviso | OK | Diogo 25/10: aviso só para BQ. |
| JobOnOverlapWarningTests.Create_WithoutOverlap_ZeroWarning | Janelas disjuntas ou outra máquina não avisam | FRACO | Só verifica «sem aviso»; como o aviso entre linhas foi removido, passa sempre. |
| JobOnOverlapWarningTests.Crosses_ODiaDaTrocaNaoESobreposicao (Theory x3) | Cálculo do cruzamento ignora o dia da troca | OK |  |
| JobOnOverlapWarningTests.JobOnOverlap_PureCalculation_TwoConflicts_ListsEach | Aviso lista cada conflito e sugere início depois do último | OK |  |
| JobOnOverlapWarningTests.MixedOpenAndClosedConflicts_DoesNotSuggestThatTheMachineIsFreeAfterTheClosedEnd | Conflitos abertos e fechados não sugerem máquina livre | OK |  |
| JobOnOverlapWarningTests.UndatedCandidate_DoesNotHideTheRealConflict | Candidata sem datas não esconde o conflito real | OK |  |
| JobOnOverlapWarningTests.UndatedExistingProduction_DoesNotInventOverlap_OnCreateOrUpdate (Theory x2) | Produção sem datas não inventa sobreposição | OK |  |
| JobOnOverlapWarningTests.Update_WithOverlapOnSameMachine_SavesWithoutWarning | Editar para janela sobreposta grava sem aviso | OK | Comentário do teste ainda diz «grava + avisa» (desatualizado). |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPaginaQueryTests.AvisoDeSobreposicao_EntreProducoesDaMesmaLinhaNaoAvisa | Produções da mesma linha que se cruzam não avisam | OK | Diogo 25/10: aviso só para BQ. |
| JobOnPaginaQueryTests.AvisoDeSobreposicao_ProducaoInexistenteNaoDaAviso | Produção inexistente não dá aviso de sobreposição | FRACO | Trivial: devolve null para um id que não existe. |
| JobOnPaginaQueryTests.CaloteECodigoDaMaquina_LeemOGuardadoEDevolvemNuloSeNaoExiste | Calote e código da máquina lidos da base | OK |  |
| JobOnPaginaQueryTests.OsPedidos_NaoGravamNada | Consultas da página não gravam nada | OK |  |
| JobOnPaginaQueryTests.ProducaoComReferenciaENumero_DevolveFerramentasEDatas | Produção existente devolve máquina, datas, calote e ferramentas | OK |  |
| JobOnPaginaQueryTests.ProducaoDoHistorico_ComVariasReferenciasDoMesmoInicio_EscolheAMaisRecente | Várias referências com o mesmo início: a mais recente | OK |  |
| JobOnPaginaQueryTests.ProducaoDoHistorico_EscolheAMaisRecenteComEsseNumero | Histórico escolhe a produção mais recente com esse nº | OK |  |
| JobOnPaginaQueryTests.ReferenciasDeMf_SoParaAFerramentaDoTipoCerto | Referência MF só vem de MF ou da CM associada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnQuadroDoDiaQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnQuadroDoDiaQueryTests.DuasProducoesDaMesmaLinhaQueSeCruzam_FicaAQueEntrouPorUltimo_SemSobreposicao | Duas produções que se cruzam: fica a última, sem marca | OK |  |
| JobOnQuadroDoDiaQueryTests.NotasDeLoteDoDiaEResumoDasAlteracoes_ficamNaProducao | Quadro traz notas de lote do dia e resumo de alterações | OK |  |
| JobOnQuadroDoDiaQueryTests.OPedido_NaoGravaNada | Abrir o quadro do dia não grava nada | OK |  |
| JobOnQuadroDoDiaQueryTests.ProducaoAteAviso_CobreOsDiasDepoisDoInicio_MasNaoOsAntes | Produção «até aviso» cobre os dias depois do início | OK |  |
| JobOnQuadroDoDiaQueryTests.ProducaoCobreOPrimeiroEOUltimoDia | Produção cobre o primeiro e o último dia | OK |  |
| JobOnQuadroDoDiaQueryTests.ProximaMudanca_ASeguinteMaisCedoQueATroca_GanhaAProducao | Produção seguinte mais cedo que a troca ganha | OK |  |
| JobOnQuadroDoDiaQueryTests.ProximaMudanca_EscolheAMaisCedoEntreAProducaoSeguinteEATrocaDeComponente | Próxima mudança é a mais cedo entre produção seguinte e troca | OK |  |
| JobOnQuadroDoDiaQueryTests.SemDiaEscolhido_NaoHaProximaMudanca_MasHaProducoesDoDiaInicial | Sem dia escolhido não há próxima mudança | OK |  |
| JobOnQuadroDoDiaQueryTests.UmaLinhaPorMaquina_NaOrdemDasMaquinas_ComAProducaoQueEntrouPorUltimo | Quadro do dia: uma produção por máquina, a que entrou por último | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnTampaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnTampaoTests.Create_GravaODiametro | Diâmetro do tampão fica gravado na criação | OK |  |
| JobOnTampaoTests.Create_ZeroOuNegativo_RecusaSemEscrita (Theory x2) | Tampão 0 ou negativo é recusado sem gravar | OK |  |
| JobOnTampaoTests.Update_ComOCampo_GravaERegistaAMudanca | Mudar o tampão grava e regista a mudança | OK |  |
| JobOnTampaoTests.Update_EnviadoVazio_LimpaOTampao | Enviar o tampão vazio limpa-o | OK |  |
| JobOnTampaoTests.Update_SemOCampo_NaoLimpaOTampao | Editar sem o campo do tampão não o apaga | OK |  |
| JobOnTampaoTests.Update_Zero_RecusaSemMexerNoFacto | Tampão 0 na edição é recusado, mantém o valor | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsArmazemTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnToolFactsArmazemTests.OUsoEAUltimaLeitura_ESemLeituraFicaVazio | % uso da ferramenta é a última leitura, pelo id | OK |  |
| JobOnToolFactsArmazemTests.OndeEstaOEstadoEOAviso | Onde está a ferramenta: posição, reparação, fabrico ou «Sem registo» com aviso | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnToolFactsQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnToolFactsQueryTests.FindAsync_APropriaProducao_NaoContaComoUsoParalelo | A própria produção não conta como uso paralelo | OK |  |
| JobOnToolFactsQueryTests.FindAsync_CompatibilidadeRegistada_AvisoSemBloqueio | Máquina fora da compatibilidade avisa sem bloquear | OK |  |
| JobOnToolFactsQueryTests.FindAsync_DatasQueNaoSeCruzam_NaoAvisa | Datas que não se cruzam não avisam | OK |  |
| JobOnToolFactsQueryTests.FindAsync_DatasQueSeCruzam_AvisaMesmoNoFuturoOuJaTerminada | Datas que se cruzam avisam, mesmo no futuro ou no passado | OK |  |
| JobOnToolFactsQueryTests.FindAsync_FatosTecnicosDaTool_OQueOsControlosConsomem | Factos técnicos da ferramenta (volume, gargalo, TP, quantidade) | OK |  |
| JobOnToolFactsQueryTests.FindAsync_MaquinaCompativel_SemAviso | Máquina compatível não avisa | OK |  |
| JobOnToolFactsQueryTests.FindAsync_MesmaMaquina_NaoContaComoUsoParalelo | Mesma máquina não conta como uso paralelo | OK |  |
| JobOnToolFactsQueryTests.FindAsync_OutraAcabaNoDiaEmQueEstaComeca_NoDiaDaTrocaNao (Theory x3) | Uso paralelo ignora o dia da troca (fim da outra) | OK |  |
| JobOnToolFactsQueryTests.FindAsync_OutraComecaNoDiaEmQueEstaAcaba_NoDiaDaTrocaNao_NaVesperaSim (Theory x3) | Uso paralelo ignora o dia da troca (início da outra) | OK |  |
| JobOnToolFactsQueryTests.FindAsync_SemMaquinasRegistadas_SemAviso | Sem compatibilidade registada não avisa | OK |  |
| JobOnToolFactsQueryTests.FindAsync_UsoParaleloNoutraMaquina_AvisaParaCmMfEBq (Theory x3) | Uso paralelo avisa para CM, MF e BQ | OK |  |
| JobOnToolFactsQueryTests.FindAsync_UsoParalelo_ProducaoAtiva_AvisaSemBloqueio | Ferramenta noutra produção ao mesmo tempo avisa | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/MachineListQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| MachineListQueryTests.ListAsync_NoMachinesRegistered_ReturnsEmptyList | Sem máquinas a lista vem vazia | FRACO | Trivial: lista vazia devolve vazia. |
| MachineListQueryTests.ListAsync_ReturnsPersistedMachinesOrderedByCode | Lista de máquinas por código | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/ProductionContextQueryIntegrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ProductionContextQueryIntegrationTests.GeneratedSql_Npgsql_RelationalProvider_ProducesSingleLeftJoinQuery | SQL gerado tem um LEFT JOIN por contexto | FRACO | Verifica texto do SQL de uma consulta escrita no próprio teste, não o comportamento do negócio nem a query real. |
| ProductionContextQueryIntegrationTests.GetAsync_JobOnWithAllContexts_ReturnsAllContextIdsAndToolIds | Contexto da produção devolve ids e ferramentas CM/MF/BQ | OK |  |
| ProductionContextQueryIntegrationTests.GetAsync_JobOnWithPartialContexts_ReturnsOnlyExistingOnes | Produção só com MF devolve só o MF | OK |  |
| ProductionContextQueryIntegrationTests.GetAsync_JobOnWithoutContexts_ReturnsNullContextIdsAndToolIds | Produção sem ferramentas devolve ids vazios | OK |  |
| ProductionContextQueryIntegrationTests.GetAsync_NonExistentJobOn_ReturnsNull | Job On inexistente devolve nada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/ProductionPickerQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ProductionPickerQueryTests.ComParteDaReferencia_PeloInicioOuPelaParteBq (Theory x7) | Seletor procura pelo início da referência ou da parte BQ | OK |  |
| ProductionPickerQueryTests.ComReferencia_CortaNoMaximo | Seletor corta no máximo de resultados | OK |  |
| ProductionPickerQueryTests.ComReferencia_OMesmoNumeroNoutraReferencia_CadaUmaComOSeuJobOn | Mesmo nº em duas referências: cada item com o seu Job On | OK |  |
| ProductionPickerQueryTests.ComReferencia_PeloInicioOuPelaParteBq_NuncaPeloMeio | Seletor nunca procura a meio da referência | DUPLICADO | Coberto pela Theory ComParteDaReferencia_PeloInicioOuPelaParteBq (casos 018 e 350). |
| ProductionPickerQueryTests.ComReferencia_SoAsDela_MaisRecentePrimeiro_SemDistinguirMaiusculas | Seletor mostra só a referência, mais recente primeiro | OK |  |
| ProductionPickerQueryTests.FindAsync_DevolveAProducaoOuNull | Encontrar produção por id, ou nada | OK |  |
| ProductionPickerQueryTests.Procura_AMaisRecentePrimeiro_PeloInicioEDepoisPeloNumero | Produções ordenadas pela mais recente primeiro | OK |  |
| ProductionPickerQueryTests.Procura_AsUltimas5Producoes | Dropdown de produções mostra as últimas 5 | OK |  |
| ProductionPickerQueryTests.Procura_SoAReferenciaCompleta | Primeiro campo filtra só referências completas | OK |  |
| ProductionPickerQueryTests.ProducoesDaReferencia_SoACompleta_ComAsFerramentas | Produções da referência completa, com as ferramentas | OK |  |
| ProductionPickerQueryTests.SemReferencia_NaoDevolveNada (Theory x3) | Sem referência o seletor não devolve produções | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/UpdateJobOnHandlerTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| UpdateJobOnHandlerTests.CreateAndUpdate_EndBeforeStart_IsSaved | Fim antes do início grava (datas livres) | OK |  |
| UpdateJobOnHandlerTests.Update_AddsContextThatDidNotExist_EnsuresApplicableContext | Editar pode acrescentar a BQ que faltava | OK |  |
| UpdateJobOnHandlerTests.Update_ClearsTemporalWindow_OpenProduction | Editar sem datas limpa a janela da produção | OK |  |
| UpdateJobOnHandlerTests.Update_MissingMfTool_RefusedBdUnchanged (Theory x2) | Editar sem MF é recusado e a base fica igual | OK |  |
| UpdateJobOnHandlerTests.Update_NullSlots_PreserveExistingContexts | Slot vazio na edição mantém a ferramenta existente | OK |  |
| UpdateJobOnHandlerTests.Update_ReplansExistingJobOn_SameIdentitySameContextsSameControl | Replanear mantém Job On e controlo; ferramentas novas, antigas ficam no registo | OK |  |
| UpdateJobOnHandlerTests.Update_SwapBq_MovementsStayOnTheOldBq | Trocar BQ: movimentos ficam na BQ antiga, histórico nas duas | OK |  |
| UpdateJobOnHandlerTests.Update_SwapCm_TheOldCmStaysInTheProduction_AndGoingBackReusesItsCmId | Trocar CM guarda a antiga; voltar retoma o cm_id | OK |  |
| UpdateJobOnHandlerTests.Update_SwapMf_TheOldMfStaysInTheProduction_AndGoingBackReusesItsMfId | Trocar MF guarda o antigo; voltar retoma o mf_id | OK |  |
| UpdateJobOnHandlerTests.Update_TresBqsSeguidas_CadaUmaFicaNoSeuBqIdComOsSeusMovimentos_EVoltarAUmaRetomaOBqIdDela | Três BQs seguidas: cada uma com o seu bq_id e movimentos | OK |  |
| UpdateJobOnHandlerTests.Update_TresCmsSeguidas_CadaUmaFicaNoSeuCmId_EVoltarAUmaRetomaOCmIdDela | Três CMs seguidas: cada uma com o seu cm_id | OK |  |
| UpdateJobOnHandlerTests.Update_TresMfsSeguidos_CadaUmFicaNoSeuMfId_EVoltarAUmRetomaOMfIdDele | Três MFs seguidos: cada um com o seu mf_id | OK |  |
| UpdateJobOnHandlerTests.Update_UnknownJobOn_RefusedClean | Editar Job On inexistente é recusado sem gravar | OK |  |
| UpdateJobOnHandlerTests.Update_UnknownMachine_RefusedBdUnchanged | Editar com máquina inexistente é recusado, nada muda | OK |  |
| UpdateJobOnHandlerTests.Update_UnknownTools_RefusedBdUnchanged | Editar com CM, MF ou BQ inexistente é recusado, nada muda | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Programada/ReparacaoProgramadaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoProgramadaTests.Associar_CriaListaDoReparador_ComOQueSeViaDaFerramenta_EAcrescentaAUmaAtiva | Associar cria lista do reparador e não repete ferramentas | OK |  |
| ReparacaoProgramadaTests.Associar_RecusaSemReparador_ReparadorArquivado_EListaFechada | Associar sem reparador, arquivado ou lista fechada é recusado sem gravar | OK |  |
| ReparacaoProgramadaTests.Entrada_DeFerramentaNumaListaAtiva_PropoeReparado_EMudarGravaComAviso | Entrada de ferramenta de lista ativa propõe «Reparado» | OK |  |
| ReparacaoProgramadaTests.Entrada_ForaDeQualquerLista_NaoMudaAProposta | Ferramenta fora de listas não muda o estado proposto | OK |  |
| ReparacaoProgramadaTests.Lista_FechaSozinha_QuandoAUltimaFerramentaDaEntrada | Lista fecha sozinha quando a última ferramenta entra | OK |  |
| ReparacaoProgramadaTests.Procurar_SemFiltros_AbreVazia_EComFiltros_MostraCmEMfComAPosicao | Reparação programada abre vazia e procura CM/MF com posição e % uso | OK |  |
| ReparacaoProgramadaTests.SaidaEEntrada_AtualizamALista_EALinhaQueEntrouMostraDiaPosicaoENome | Saída e entrada no Armazém atualizam a lista programada | OK |  |
| ReparacaoProgramadaTests.Procurar_PelaReferenciaToda_OuPeloNomeTecnico_EncontraACmEOMf | Registar Reparação: a referência toda e o nome técnico encontram a CM e o MF (DMO-579) | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Search/ReferenceMatchTests.ParteBq.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReferenceMatchParteBqTests.Matches_UmTextoComLetra_ComecaNaParteBq (Theory x16) | Procura casa pelo início da referência ou da parte BQ | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Search/ReferenceMatchTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReferenceMatchTests.HistoricoBoquilhas_PesquisaPeloInicioDasReferencias (Theory x6) | Histórico das Boquilhas procura pelo início da referência | OK |  |
| ReferenceMatchTests.Matches_OInicioDaReferenciaOuDaParteBq (Theory x16) | Procura pelo início da referência ou parte BQ, nunca a meio | OK |  |
| ReferenceMatchTests.Predicate_DaOMesmoQueMatches | Filtro na base dá o mesmo que a função em memória | OK |  |
| ReferenceMatchTests.Predicate_SemTexto_Recusa | Filtro sem texto é recusado | OK |  |
| ReferenceMatchTests.Resumo_PeloInicioDaReferenciaOuDoNumero (Theory x7) | Resumo procura pelo início da referência ou do nº | OK |  |
| ReferenceMatchTests.Resumo_PeloInicioDoNumero_ContaEPagina | Resumo pelo início do nº conta e pagina | OK |  |
| ReferenceMatchTests.Revisao_ProcuraPeloInicioDasReferencias (Theory x9) | Revisão procura pelo início das referências, lote e nº por parte | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Search/SuggestionQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SuggestionQueryTests.EmptyTerm_ReturnsNothing (Theory x3) | Sugestões sem texto não devolvem nada | OK |  |
| SuggestionQueryTests.Ferramentas_FilterByType_ExcludeRemoved_AtMostTen | Sugestões de ferramentas: por tipo, sem removidas, máximo 10 | OK |  |
| SuggestionQueryTests.NumerosProducao_IgnoreReferences | Sugestões de nº de produção ignoram referências | OK |  |
| SuggestionQueryTests.OneCharacter_IsEnough_ForReferences | Um carácter chega para sugerir referências | OK |  |
| SuggestionQueryTests.Producoes_MatchProductionNumber_ValueIsTheNumber | Sugestão de produção pelo nº devolve o nº | OK |  |
| SuggestionQueryTests.Producoes_MatchReferenceIgnoringCase_ValueIsTheReference | Sugestão de produção por referência sem maiúsculas | OK |  |
| SuggestionQueryTests.Producoes_UmNumeroAMeioNaoDevolveAReferencia | Sugestões de produções não procuram a meio | OK |  |
| SuggestionQueryTests.ReferenciasEBq_ComMuitasDasDuas_MetadeDeCada | Com muitas sugestões, metade referências e metade BQ | OK |  |
| SuggestionQueryTests.ReferenciasEBq_JuntaAsReferenciasDeProducaoEAsBq | Sugestões juntam referências de produção e BQ | OK |  |
| SuggestionQueryTests.ReferenciasRegisto_NaBq_OQueSeVeEOQueFicaEscrito | Na BQ sugere a referência total, igual ao rótulo | OK |  |
| SuggestionQueryTests.ReferenciasRegisto_SempreAReferenciaToda_EACmComONomeOriginal | Registo sugere a referência toda; CM com nome original | OK |  |
| SuggestionQueryTests.Referencias_AreDistinct | Referências sugeridas não se repetem | OK |  |
| SuggestionQueryTests.Referencias_ComLinha_SoAsQueCorreramNela_SemNenhumaTodas | Referências sugeridas: só as que correram na linha | OK |  |
| SuggestionQueryTests.Referencias_PeloInicioDaReferenciaOuDaParteBq (Theory x8) | Sugestões pelo início da referência ou parte BQ | OK |  |
| SuggestionQueryTests.Reparadores_MatchName | Reparadores sugeridos por parte do nome | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/CsFurosTextoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CsFurosTextoTests.Escrever (Theory x5) | Escrever o texto dos furos abertos/fechados da CS | OK |  |
| CsFurosTextoTests.Ler (Theory x9) | Ler o texto dos furos abertos/fechados da CS | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaPorProducaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FabricoSapCadenciaPorProducaoTests.AMediaPesaOsDias_EAModaEADasOrdensComMaisDias | Cadência: média pesada pelos dias, moda da ordem com mais dias | OK |  |
| FabricoSapCadenciaPorProducaoTests.CadaProducaoDaFerramenta_TemASuaCadencia | Cada produção da ferramenta tem a sua cadência SAP | OK |  |
| FabricoSapCadenciaPorProducaoTests.OJobOnSemCadencia_FicaComADaProducaoAnteriorDaMesmaReferencia | Job On sem SAP usa a cadência da produção anterior | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FabricoSapCadenciaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FabricoSapCadenciaTests.ACadenciaDoJobOn_EADaOrdemComMaisDias_EmpatadaAMaisRecente | Cadência do Job On: ordem com mais dias, empate pela recente | OK |  |
| FabricoSapCadenciaTests.ARelatorioDaZfmd_DaACadenciaDeCadaProducao | Relatório ZFMD dá a cadência de cada produção | OK |  |
| FabricoSapCadenciaTests.Importar_GravaACadencia_ESemElaNaoApagaAJaGravada | Importar grava a cadência; relatório antigo não a apaga | OK |  |
| FabricoSapCadenciaTests.UmRelatorioSemCadencia_DeixaAVazia | Relatório sem cadência deixa-a vazia | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentaHistoricoQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentaHistoricoQueryTests.ABq_MostraSoASuaParteDaReferencia | BQ mostra só a sua parte da referência | OK |  |
| FerramentaHistoricoQueryTests.AProcuraPelaLinhaEPelosDias_SemReferencia | Procura por linha e dias sem referência | OK |  |
| FerramentaHistoricoQueryTests.AProcuraPelaReferenciaEPeloTipo | Procura por referência e tipo | OK |  |
| FerramentaHistoricoQueryTests.AcimaDoLimite_AProcuraFicaCortada | Procura acima do limite fica cortada | OK |  |
| FerramentaHistoricoQueryTests.CadaFerramenta_TemASuaPercentagemDeUso_NaProducao | Cada ferramenta tem o seu % de uso por produção | OK |  |
| FerramentaHistoricoQueryTests.NaBq_OsMfDeCadaJobOnPeloBqContext | Histórico da BQ liga os MF pelo bq_id de cada Job On | OK |  |
| FerramentaHistoricoQueryTests.NoCm_UmaLinhaPorJobOn_ComOUsoEAsOutrasFerramentas | Histórico da CM: uma linha por Job On com uso e MF/BQ | OK |  |
| FerramentaHistoricoQueryTests.OsDias_ContamODiaDoInicio (Theory x2) | Dias de uso contam o dia de início | OK |  |
| FerramentaHistoricoQueryTests.OsDias_IncluemOsLimites_DoInicioEDoFim (Theory x4) | Intervalo de dias inclui os limites do Job On | OK |  |
| FerramentaHistoricoQueryTests.OsDias_SemFim_ContamAteHoje | Sem fim, os dias contam até hoje | OK |  |
| FerramentaHistoricoQueryTests.OsDias_SemInicioOuFimAntesDoInicio_NaoTemDias | Dias de uso sem início ou com fim antes do início ficam vazios | OK |  |
| FerramentaHistoricoQueryTests.SemFiltro_AProcuraFicaVazia | Procura sem filtro fica vazia | OK |  |
| FerramentaHistoricoQueryTests.UmaFerramentaQueNaoExiste_NaoTemHistorico | Ferramenta inexistente ou removida não tem histórico | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFabricoSapTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasFabricoSapTests.ABoquilhaTambemTemAsProducoes_EAListaPagina | BQ também tem produções; lista paginada | OK |  |
| FerramentasFabricoSapTests.ALeituraDeHoje_SoEOUsoDepoisDaUltimaProducao_NaoDasAnteriores | Leitura de hoje só conta como «depois» da última produção | OK | Decisão 27/10 (utilizacao-delta-estimado). |
| FerramentasFabricoSapTests.ALinhaDoSap_LigaSeAoJobOnPelaReferenciaMaquinaEDatas (Theory x10) | Linha SAP liga ao Job On por referência, máquina e datas | OK |  |
| FerramentasFabricoSapTests.AsProducoesDaFerramenta_SaoOsJobOnDelaComOQueOSapDiz | Produções da ferramenta com garrafas e datas do SAP | OK |  |
| FerramentasFabricoSapTests.LerOMesmoRelatorioOutraVez_AtualizaAsMesmasLinhas | Ler o relatório outra vez atualiza, sem duplicar linhas | OK |  |
| FerramentasFabricoSapTests.ODelta_EQuantoAUtilizacaoSubiuEmCadaProducao_EOTotalSomaTodas | Delta do uso por produção e total das produções | OK |  |
| FerramentasFabricoSapTests.OPesoDoSap_EAMediaPelosDiasEAModaOPesoComMaisDias | Peso SAP: média pelos dias e moda do peso com mais dias | OK |  |
| FerramentasFabricoSapTests.OPesoDosJobOns_VemDasOrdensDaMesmaReferenciaMaquinaEDatas | Peso do Job On vem das ordens da mesma referência, linha, datas | OK |  |
| FerramentasFabricoSapTests.ORelatorioDoSap_DaOMaterialALinhaAsDatasEAProducao | Relatório SAP dá material, linha, datas e produção | OK |  |
| FerramentasFabricoSapTests.ORelatorioProducoesReal_LigaAsProducoesDaCmDaB1_ComGarrafas | Relatório real liga as produções da CM da B1 | OK |  |
| FerramentasFabricoSapTests.ORelatorioProducoes_DaAsGarrafasBrutasDeCadaProducao | Relatório «produções» HTML dá as garrafas brutas | OK |  |
| FerramentasFabricoSapTests.OTotal_ContaTodasAsProducoes_NaoSoAPagina | Total das garrafas conta todas as produções, não só a página | OK |  |
| FerramentasFabricoSapTests.OsDiasDeCadaOrdem_SaoOsQueFicamDentroDoJobOn_EAModaEmpatadaEAMaisRecente | Dias de cada ordem contam só dentro do Job On | OK |  |
| FerramentasFabricoSapTests.SemDataDeInicio_NaoSeLiga | Job On sem data de início não liga ao SAP | OK |  |
| FerramentasFabricoSapTests.UmFicheiroSemOCabecalhoDoFabrico_NaoEORelatorio | Ficheiro sem cabeçalho do fabrico não é o relatório | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasFrozenFactsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasFrozenFactsTests.CreateBqRepairTrace_DummyToolIsRefusedTyped | Ferramenta removida não abre registo de BQ | OK |  |
| FerramentasFrozenFactsTests.CreateBqRepairTrace_FreezesToolFactsAtCreation | Registo da BQ congela referência e lote | OK |  |
| FerramentasFrozenFactsTests.CreateJobOn_DummyToolIsRefusedTypedInEverySlot | Ferramenta removida não se escolhe em nenhum slot | OK |  |
| FerramentasFrozenFactsTests.CreateToolScopedPeso_DummyToolIsRefusedTyped | Peso com ferramenta removida é recusado sem gravar | OK |  |
| FerramentasFrozenFactsTests.CreateToolScopedPeso_FreezesToolFactsAtCreation | Peso congela referência e lote da CM e da BQ | OK |  |
| FerramentasFrozenFactsTests.RecordPegamentosMeasurement_DeletedMfInTheProduction_IsRefusedAsNotInProduction | Medir MF apagado da produção é recusado | OK |  |
| FerramentasFrozenFactsTests.RecordPegamentosMeasurement_DummyComponentIsRefusedAsNotInProduction | Medir MF numa produção legada sem MF é recusado | OK |  |
| FerramentasFrozenFactsTests.RecordPegamentosMeasurement_FreezesCombinationFactsAtRegistroCreation | Pegamentos congela referência e lote de CM, BQ e MF | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasHandlersTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasHandlersTests.CreateTool_BqAcceptsMultipleOrZeroMachines | BQ aceita 0, 1 ou várias máquinas e quantidade do lote | OK |  |
| FerramentasHandlersTests.CreateTool_BqSemLote_ERecusada_ENadaEGravado (Theory x3) | BQ sem lote é recusada e nada grava | OK |  |
| FerramentasHandlersTests.CreateTool_DuploClique_DevolveAMesma_SemRecusaNemAviso | Duplo clique em registar devolve a mesma ferramenta | OK |  |
| FerramentasHandlersTests.CreateTool_EmptyLot_SavesNoLot_AndASecondOneIsRefused | Lote vazio grava «s/lote»; segunda igual é recusada | OK |  |
| FerramentasHandlersTests.CreateTool_MintsCanonicalToolId_BackendEmittedNeverDerivedFromFacts | Registar ferramenta emite id novo e liga a máquina | OK |  |
| FerramentasHandlersTests.CreateTool_QualquerFormatoDeReferencia_NuncaRecusa | Qualquer formato de referência é registado, sem recusa | OK | Aceita BQ «t194» e «T194» lote 12 como duas ferramentas; confirmar com «nada repetido». |
| FerramentasHandlersTests.CreateTool_Repetida_ERecusada_EAExistenteFicaComAviso | Ferramenta repetida é recusada e a existente fica com aviso | FRACO | Unicidade (tipo, referência, lote) só em memória; falta teste PostgreSQL do índice único e de dois registos ao mesmo tempo. |
| FerramentasHandlersTests.CreateTool_TypedRefusals_BeforeAnyWrite | Tipo desconhecido, sem referência ou máquina inexistente: nada grava | OK |  |
| FerramentasHandlersTests.DeleteTool_ExistingFrozenFactsArePreserved_BackfillOnlyFillsMissing | Eliminar ferramenta não reescreve factos já congelados | OK |  |
| FerramentasHandlersTests.DeleteTool_TransactionalDummyFlow_PreservesHistoryAndPhysicalDeletes | Eliminar ferramenta passa o histórico para a removida e apaga-a | FRACO | Transação «tudo ou nada» só em memória (regra 7); falta teste PostgreSQL que falhe sem a transação. |
| FerramentasHandlersTests.DeleteTool_TypedRefusals_DummyAndUnknown | Eliminar ferramenta removida ou inexistente é recusado | OK |  |
| FerramentasHandlersTests.DeleteTool_WithoutDummyRow_ThrowsLoudDeploymentError | Sem a linha «removida» a eliminação falha alto e nada muda | OK |  |
| FerramentasHandlersTests.DuplicateTool_CopiesAsEditableDefaults_MintsNewToolId_SourceIntact | Duplicar copia factos, técnica e máquinas; fonte intacta | OK |  |
| FerramentasHandlersTests.DuplicateTool_NewLotRequired_DummyNeverDuplicated | Duplicar exige lote novo; removida não duplica | OK |  |
| FerramentasHandlersTests.Duplicate_DuploClique_DevolveACopia_SemRecusaNemAviso | Duplo clique em duplicar devolve a mesma cópia | OK |  |
| FerramentasHandlersTests.Duplicate_ExistingLotIsRefused_EditCollisionStaysRefused_BothMarkTheExisting | Duplicar ou editar para lote existente é recusado e marca a existente | OK |  |
| FerramentasHandlersTests.EditBq_SeveralMachines_ReplacesCompatibilityWithoutChangingIdentity | Editar BQ troca as máquinas sem mudar a identidade | OK |  |
| FerramentasHandlersTests.EditToolFacts_EmptyLot_SavesNoLot_CollisionWithAnotherNoLotStaysRefused | Editar lote vazio grava «s/lote»; colisão é recusada | OK |  |
| FerramentasHandlersTests.EditToolFacts_FullReplaceSameToolId_AtomicMachineSubstitution | Editar ferramenta mantém o id e substitui as máquinas | OK |  |
| FerramentasHandlersTests.EditToolFacts_MudarOTipoDeUmaFerramentaUsada_GravaEContaAsProducoesDoTipoAnterior | Mudar o tipo de uma ferramenta usada avisa quantas produções afeta | OK |  |
| FerramentasHandlersTests.EditToolFacts_TypedRefusals_DummyNeverAddressable | Editar ferramenta removida, sem referência ou máquina inexistente: nada muda | OK |  |
| FerramentasHandlersTests.EditToolTechnicalValues_FullReplaceEmptyClearsToNull | Campo técnico vazio limpa o valor; campos da CM na BQ ficam | OK |  |
| FerramentasHandlersTests.EditToolTechnicalValues_ImpossibleValues_AreRefused_NothingSaved | Volume, peso ou medida ≤ 0 são recusados; calote 0 grava | OK |  |
| FerramentasHandlersTests.EditToolTechnicalValues_OmittedTpDerivedValues_ArePreserved | Derivados do TP não enviados mantêm-se | OK |  |
| FerramentasHandlersTests.EditToolTechnicalValues_RegistersGenesisSheetTpGeometry_NeverRecalculates | Valores técnicos e geometria do TP gravam-se tal como escritos | OK |  |
| FerramentasHandlersTests.EditToolTechnicalValues_TypedRefusals_DummyNeverAddressable | Valores técnicos de ferramenta removida ou processo desconhecido: recusa | OK |  |
| FerramentasHandlersTests.Repetida_SoComMaquinaEmComum_CriarDuplicarEditar | Repetida só se partilhar máquina (criar, duplicar, editar) | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasQueriesTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasQueriesTests.ToolCandidatePage_ProjectsMachineCodesPerTool | Lista de ferramentas traz as máquinas de cada uma | OK |  |
| FerramentasQueriesTests.ToolCandidateQuery_FullMfReference_AlsoFindsTheToolRegisteredWithTheModelOnly | Referência MF completa encontra o MF antigo só com o modelo | OK |  |
| FerramentasQueriesTests.ToolCandidateQuery_MachineFilter_MatchesViaPersistedLinks | Filtro por máquina usa as ligações gravadas | OK |  |
| FerramentasQueriesTests.ToolCandidateQuery_MfReferenceMatchesCanonicalOrAssociated_WorkedExample | Referência MF encontra a canónica e a associada | OK |  |
| FerramentasQueriesTests.ToolDeletionImpact_ListsAffectedProductionsAndCounts | Aviso de eliminação lista produções, pesos, pegamentos e registos | OK |  |
| FerramentasQueriesTests.ToolLookupQuery_DummyRemovedNeverResolves | Ferramenta removida nunca se resolve como real | OK |  |
| FerramentasQueriesTests.ToolSheet_ComposesFactsMachinesAndTechnical_DummyNeverResolves | Ficha da ferramenta junta factos, máquinas e técnica | OK |  |
| FerramentasQueriesTests.ToolSheet_TechnicalAbsenceIsValidState | Ficha sem linha técnica abre na mesma | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasUsoSapTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasUsoSapTests.AEntradaVindaDaProducao_AbreOLembreteDaFerramentaEDoPar (Theory x3) | Entrada vinda da Produção abre lembrete da ferramenta e do par | OK |  |
| FerramentasUsoSapTests.ALeitura_FechaOLembreteMesmoComOMesmoValor | Leitura do SAP fecha o lembrete mesmo com o mesmo valor | OK |  |
| FerramentasUsoSapTests.ALinhaDoSap_EDaFerramentaComOMesmoNomeTecnicoELote (Theory x7) | Linha SAP liga à ferramenta por nome técnico e lote | OK | Memória 28/10: lotes «MGnn» e BQ NX* ainda não se ligam; não testado. |
| FerramentasUsoSapTests.AListaHtmlDoSap_DaOCodigoEOUsoDeCadaLinha | Lista HTML da ZPPG dá código e % uso | OK |  |
| FerramentasUsoSapTests.EliminarAFerramenta_LevaAUtilizacaoEOLembrete | Eliminar a ferramenta apaga o uso e o lembrete | OK |  |
| FerramentasUsoSapTests.Importar_AcrescentaUmaLeituraPorFerramentaEDizOQueNaoTemFerramenta | Importar do SAP grava uma leitura por ferramenta e lista as sem ferramenta | OK |  |
| FerramentasUsoSapTests.OCodigoAPedirAoSap_EONomeTecnicoComOLoteEmDoisAlgarismos | Código a pedir ao SAP: nome técnico mais lote com 2 algarismos | OK |  |
| FerramentasUsoSapTests.OCsvDoScript_LeOPercDasLinhasEncontradasEDeixaAsOutras | CSV do script lê as encontradas e ignora as com erro | OK |  |
| FerramentasUsoSapTests.OMesmoValorOutraVez_NaoAcrescentaNadaEUmValorNovoAcrescenta | O mesmo % do SAP não repete; valor novo acrescenta | OK |  |
| FerramentasUsoSapTests.OPercentManual_GravaUmaLeituraEFechaOLembrete_IgualNaoGrava | % manual grava leitura e fecha lembrete; igual ou negativo não | OK |  |
| FerramentasUsoSapTests.OQueNaoEUmNumero_FicaDeFora (Theory x3) | Texto que não é número fica de fora | OK |  |
| FerramentasUsoSapTests.OsCodigos_SaoSoOsDasFerramentasComOLembreteAberto | Códigos a pedir são só dos lembretes abertos | OK |  |
| FerramentasUsoSapTests.OsNumerosDoSap_EmFormatoPortugues (Theory x4) | Números do SAP em formato português | OK |  |
| FerramentasUsoSapTests.Pedir_AbreUmLembreteSoUmaVez | Pedir % ao SAP abre um só lembrete | OK |  |
| FerramentasUsoSapTests.PorAtualizar_SaoOsLembretesAbertos_OsMaisAntigosPrimeiro | Por atualizar: lembretes abertos, mais antigos primeiro | OK | Regra «último registo em cima» não se aplica aqui (aviso de pendentes). |
| FerramentasUsoSapTests.TodosOsCodigos_SaoOsDeTodasAsFerramentasComNomeTecnico | «Todos os códigos» são os de todas as ferramentas com nome técnico | OK |  |
| FerramentasUsoSapTests.UmFicheiroQueNaoEALista_NaoGravaNada | Ficheiro que não é a lista não grava nada | OK |  |
| FerramentasUsoSapTests.UmFicheiroQueNaoEALista_NaoTemCabecalho | Ficheiro que não é a lista é reconhecido | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserAvisoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| TechnicalNameParserAvisoTests.UmNomeForaDaRegra_Avisa | Nome técnico fora da regra avisa | OK |  |
| TechnicalNameParserAvisoTests.UmNomeQueBate_NaoTemAviso (Theory x6) | Nome técnico certo para o tipo não avisa | OK |  |
| TechnicalNameParserAvisoTests.UmaBqComONomeDeUmMolde_Avisa | BQ com nome de molde avisa | OK |  |
| TechnicalNameParserAvisoTests.UmaCmComONomeDeUmMf_Avisa | CM com nome de MF avisa | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserGotasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| TechnicalNameParserGotasTests.Molde_DizAsGotasPelaDimensao (Theory x9) | Gotas do molde pela dimensão do nome técnico | OK |  |
| TechnicalNameParserGotasTests.SemNomeAcessorioOuOutraDimensao_NaoDiz (Theory x5) | Sem nome, acessório ou outra dimensão não diz as gotas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/TechnicalNameParserTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| TechnicalNameParserTests.Accessory_ReadsTheReferenceFromTheGrammar (Theory x6) | Acessórios: referência, tipo e família pelo nome técnico | OK |  |
| TechnicalNameParserTests.Mold_KeepsTheModelAndType | Molde: modelo e tipo pelo nome técnico | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateEstadoLocalTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ToolCandidateEstadoLocalTests.Estado_e_o_ultimo_registado | Filtro de estado usa o último estado registado | OK |  |
| ToolCandidateEstadoLocalTests.Fabrica_de_origem_sem_maiusculas_e_antes_da_paginacao | Fábrica de origem filtra sem maiúsculas, antes da paginação | OK |  |
| ToolCandidateEstadoLocalTests.Localizacao_armazem_primeiro_e_sem_movimentos_e_fabrico | Filtro de local: armazém, fabrico, reparação, sucata, empréstimo | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolCandidateQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ToolCandidateQueryTests.FindCandidatesAsync_BlankFilterDimensions_AreTreatedAsNotApplied_NotAsInexactMatches | Filtros em branco contam como não aplicados | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_DummyRemovedLine_IsNeverACandidate | A ferramenta «removida» nunca é candidata | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_EachFilterDimensionOnlyReducesTheCandidateSet | Cada filtro só reduz os candidatos | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_ExactPersistedValuesAreProjectedForEveryType | Candidatos mostram os valores gravados de CM, MF e BQ | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_InexactLotValues_DoNotMatch (Theory x3) | Lote exato não aceita espaços nem prefixo | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_InexactReferenceValues_DoNotMatch (Theory x3) | Referência exata não aceita maiúsculas, espaços ou prefixo | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_IsDeterministic_CanonicalFactsOrdering | Candidatos ordenados sempre da mesma forma (tipo, referência, lote) | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_IsReadOnly_DoesNotTrackOrMutate | Procurar candidatos não grava nada | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_NoFilterApplied_ReturnsEveryRegisteredToolWithCanonicalPersistedIdentity | Sem filtro devolve todas as ferramentas, ordenadas | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_SameReferenceWithDifferentLots_NeverCollapsesIntoOneCandidate | Mesma referência em lotes diferentes dá candidatos distintos | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_SingleRemainingCandidate_StaysAnUnselectedCandidate_NeverAutoSelects | Um só candidato não é escolhido automaticamente | OK |  |
| ToolCandidateQueryTests.FindCandidatesAsync_UnknownFilterValues_ReturnEmptyList_NeverInferIdentity | Valores desconhecidos dão lista vazia | OK |  |
| ToolCandidateQueryTests.JobOn_Companheiras_OrdenamPrimeiroAsUsadasComElas_ESoRelacionadasTiraAsOutras | BQ usada com a CM escolhida vem primeiro | OK |  |
| ToolCandidateQueryTests.MachineFilter_IncluirSemLinha_JuntaAsSemLinha_NuncaAsDeOutraLinha | Filtro de linha pode incluir ferramentas sem linha, nunca de outra | OK |  |
| ToolCandidateQueryTests.Paging_AppliesFiltersBeforeCountingAndSlicing_AndKeepsStableDistinctPages | Paginação filtra antes de contar e não repete itens | OK |  |
| ToolCandidateQueryTests.ProductionReference_FerramentasComSoUmaParte_TambemPertencem | Ferramentas registadas só com uma parte pertencem à referência | OK |  |
| ToolCandidateQueryTests.ProductionSuggestions_UseRecordedContextsAndMachineLinks_WithoutInferringReferences | Sugestões da produção usam contextos e ligações à máquina | OK |  |
| ToolCandidateQueryTests.ReferenceContains_AWholeReferenceAlsoFindsTheToolsRegisteredWithOnlyOnePart | Referência inteira encontra ferramentas registadas só com uma parte | OK |  |
| ToolCandidateQueryTests.ReferenceContains_MatchesTheStartOfTheReferenceOfItsBqPartOrOfTheAssociatedMf | Procura pelo início da referência, parte BQ ou MF associada | OK |  |
| ToolCandidateQueryTests.Search_ReferenceByItsStartOrBqPart_LotAndMachineByPart | Procura: referência pelo início, lote e máquina por parte | OK |  |
| ToolCandidateQueryTests.SplitReference_ModeloEParteBq (Theory x3) | Separar a referência em modelo e parte BQ | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolLookupQueryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ToolLookupQueryTests.FindByReferenceAsync_PicksTheHighestLotOfTheRequestedType | Por referência escolhe o lote mais alto do tipo | OK |  |
| ToolLookupQueryTests.FindByReferenceAsync_SemLote_NuncaGanhaAosLotesNumerados | «s/lote» nunca ganha aos lotes numerados | OK |  |
| ToolLookupQueryTests.FindByToolIdAsync_DescriptiveFactOrInexactValue_DoesNotResolve (Theory x5) | Só o id exato resolve; referência, lote e variações não | OK |  |
| ToolLookupQueryTests.FindByToolIdAsync_ExactCanonicalToolId_ResolvesPersistedTool | Ferramenta encontra-se pelo id exato | OK |  |
| ToolLookupQueryTests.FindByToolIdAsync_IdenticalDescriptiveFacts_RemainDistinctByToolId | Mesmos factos mas ids diferentes continuam distintas | OK |  |
| ToolLookupQueryTests.FindByToolIdAsync_IsReadOnly_DoesNotTrackOrMutate | Procurar por id não grava nada | OK |  |
| ToolLookupQueryTests.FindByToolIdAsync_ResolvesToolOfAnyType_TypeIsNeverAGate (Theory x3) | Procura por id serve BQ, CM e MF | OK |  |
| ToolLookupQueryTests.FindByToolIdAsync_UnknownToolId_ReturnsNull | Id desconhecido não devolve ferramenta | OK |  |
| ToolLookupQueryTests.ListByReferenceAsync_OsLotesDaReferenciaDoTipo_DoMaisAltoAoSemLote | Lotes da referência do tipo, do mais alto ao «s/lote» | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolLotQuantitySettingsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ToolLotQuantitySettingsTests.BadQuantities_AreIgnoredWithWarning_ValidOnesSave_EmptyClearsOnlyTheSubmittedDefinition | Quantidades más ignoradas com aviso; válidas gravam | OK |  |
| ToolLotQuantitySettingsTests.Defaults_AreIndependentByTypeAndMachine_ConflictsNeedChoice_ManualValueWins | Qtd. do lote por tipo e máquina; conflito sem sugestão; manual ganha | OK |  |
| ToolLotQuantitySettingsTests.RepeatedMachineCode_ListsOneRow_SavesAndSuggestsForBoth | Código de máquina repetido lista uma linha e grava para ambas | OK |  |
| ToolLotQuantitySettingsTests.Suggestion_IgnoresAMachineWithoutTotal_ButNotDifferentTotals | Sugestão ignora máquina sem total mas não totais diferentes | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolRegistryIntegrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ToolRegistryIntegrationTests.BqContext_ToolIdWithoutRegisteredTool_DoesNotResolve | Contexto com id sem ferramenta não resolve | FRACO | Join escrito no teste; em memória não há chave estrangeira, a garantia real é do PostgreSQL. |
| ToolRegistryIntegrationTests.BqContext_ToolId_ResolvesCanonicalTool | Contexto da BQ liga à ferramenta pelo id | FRACO | O join está escrito no próprio teste (LINQ), prova só o mapeamento do EF; nada do código da app. |
| ToolRegistryIntegrationTests.Tools_SameReferenceDifferentLot_PersistAsDistinctCanonicalTools | Mesma referência em lotes diferentes são ferramentas distintas | FRACO | Só grava e lê duas linhas em memória; não testa regra nenhuma. |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.FerramentasDefinicoes_AAjudaDoSapAbreAPaginaDeAjuda | Ajuda do SAP abre a página de ajuda | FRACO | Só texto e ids estáticos; os scripts SAP passaram para a app (#274), texto pode estar desatualizado. |
| DefinicoesPorModuloPageTests.FerramentasDefinicoes_OCartaoDoSapTemApagarLidos_EAAjudaExplica | Cartão do SAP tem Apagar lidos e ajuda | FRACO | Verifica texto/ids presentes, não o efeito de apagar. |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.CadenciaPorProducao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OHistorico_MostraACadenciaDeCadaProducao_EAJanelaComMediaModaEDelta | Histórico mostra cadência de cada produção | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.DeltaPeloProduzido.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.ComAsLeiturasAVoltaDaProducao_ODeltaNaoEEstimado | Com leituras à volta, delta não é estimado | OK |  |
| DefinicoesPorModuloPageTests.SemLeiturasAVoltaDaProducao_ODeltaEstimaSePeloProduzido | Sem leituras, delta estima-se pelo produzido com ≈; a explicação fica só na ajuda do título, sem quadro verde (DMO-668) | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FabricoSap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.ODetalhesDaUtilizacao_MostraODeltaDeCadaProducaoEOTotal | Detalhe da utilização mostra delta e total, sem o quadro verde «Hoje» (DMO-668) | OK |  |
| DefinicoesPorModuloPageTests.OHistoricoDeProducoes_DaODeltaDeGarrafasEAJanelaDoPeso | Histórico sem o Δ garrafas (só o bruto) e com a janela do peso (DMO-609) | OK |  |
| DefinicoesPorModuloPageTests.OHistoricoDeProducoes_MostraAsGarrafasOPesoEAPercentagemDeCadaJobOn | Histórico mostra garrafas, peso e % por Job On | OK |  |
| DefinicoesPorModuloPageTests.ORelatorioDeFabrico_MostraOQueCadaJobOnDaFerramentaProduziu | Relatório de fabrico mostra o produzido por Job On | OK |  |
| DefinicoesPorModuloPageTests.SemORelatorioDeFabrico_AProducaoFicaComTraco | Sem relatório de fabrico a produção fica com traço | OK |  |
| DefinicoesPorModuloPageTests.UmaFerramentaSemJobOn_DizQueNaoEsteveEmNenhum | Ferramenta sem Job On diz que não esteve em nenhum | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FaixaDaFicha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.AJanelaDaUtilizacao_LevaAoHistoricoDeProducoes | Janela da utilização leva ao histórico de produções | FRACO | Verifica id e texto de um botão, não a navegação. |
| DefinicoesPorModuloPageTests.ORegistarDoArmazem_ComUmaFerramenta_TemOVoltar | Registar do Armazém tem Voltar e recusa URL externo | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoComArtigo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OHistoricoDeProducoes_MostraOArtigoDeCadaProducao | Histórico mostra o artigo de cada produção, sem o quadro «Referência (n)» (DMO-601) | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoUsoEstimado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OHistorico_SemLeiturasAVolta_EstimaOUsoPeloProduzido | Histórico estima o uso pelo produzido com ≈ | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PastasEmColunas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.AsPastasDesteComputador_FicamCadaUmaNaSuaColuna | Pastas deste computador em colunas | FRACO | Verifica layout/colunas de cartões (regra 10). |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PesoPorProducao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OPesoDoHistorico_AbreAJanelaComTodasAsProducoes_MediaModaEDeltaEmGEPercentagem | Peso do histórico mostra média, moda e delta | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapMaisAntigo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.UmFicheiroDoSapMaisAntigoDoQueOUltimoLido_NaoGrava | Ficheiro SAP mais antigo que o último lido não grava | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoNoPainel.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OHistoricoDoUso_DeUmaFerramentaQueNaoExiste_DaNaoEncontrada | Histórico de ferramenta inexistente dá não encontrada | OK |  |
| DefinicoesPorModuloPageTests.OHistoricoDoUso_MostraAsProducoesEOUsoQueFoiAcumulando | Histórico do uso mostra produções e uso acumulado | OK |  |
| DefinicoesPorModuloPageTests.OUsoNaFichaDoArmazem_AbreOHistoricoDoUso | Uso na ficha do Armazém abre o histórico | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoPorProducao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.ALeituraDeHoje_NaoEOUsoNoFimDasProducoesAnteriores | Leitura de hoje não é o uso das produções anteriores | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UsoSap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.AListaDoSap_GravaOUsoQueAFichaDaFerramentaMostra | Lista do SAP grava o uso mostrado na ficha | OK |  |
| DefinicoesPorModuloPageTests.AtualizarPorReferencia_DaOsCodigosDessaReferenciaEDoTipo | Atualizar por referência dá códigos da referência | OK |  |
| DefinicoesPorModuloPageTests.OsCodigosAPedirAoSap_SaoOsPedidosNaFicha | Códigos a pedir ao SAP vêm dos pedidos da ficha | OK |  |
| DefinicoesPorModuloPageTests.PedirTodas_DaOsCodigosDeTodasAsFerramentasComNomeTecnico | Pedir todas dá códigos de ferramentas com nome técnico | OK | Confirmar com decisão de 28/10 (não usar «*»). |
| DefinicoesPorModuloPageTests.SemOModuloFerramentas_ALeituraNaoGravaNada | Sem módulo Ferramentas a leitura não grava | OK |  |
| DefinicoesPorModuloPageTests.UmFicheiroQueNaoEALista_DizQueNadaFoiGravado | Ficheiro que não é a lista não grava nada | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoPeloArmazem.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.AJanelaDaUtilizacao_MostraOsJobOnDaLinhaEnquantoAFerramentaLaEsteve | Utilização mostra Job On da linha pelo Armazém | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoReferencia.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.AJanelaDaUtilizacao_MostraAReferenciaDeCadaProducao | Utilização mostra a referência de cada produção | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.Admin_TemDefinicoesProprias_SoAApp | Admin tem definições próprias, só da app | FRACO | Muitas verificações de texto/ids e DoesNotContain. |
| DefinicoesPorModuloPageTests.FerramentasDefinicoes_OnlyForWhoHasTheModule_AndLeftTheAdmin | Definições de Ferramentas só para quem tem o módulo | FRACO | Parte do teste só verifica ausência de textos. |
| DefinicoesPorModuloPageTests.FerramentasDefinicoes_TotalsGrid_SavesWithGuardar | Definições dos totais gravam com Guardar | OK |  |
| DefinicoesPorModuloPageTests.HoraDeViragem_CadaUmaNoSeuModulo_ComOsSeusHandlers | Hora de viragem de cada módulo com os seus handlers | OK |  |
| DefinicoesPorModuloPageTests.FerramentasDefinicoes_Codificacao_SeletorDaB2 | Codificação por linha: B2 numa só fila com dropdown Dupla/Tripla; escolher grava o modo e mostra os nomes desse modo (DMO-578, DMO-670) | OK |  |

### `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaEnvioUnicoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoProgramadaEnvioUnicoTests.CriarLista_OMesmoEnvioCriaUmaSo_OutraChaveCriaOutraLista | Mesmo envio cria uma só lista; outra chave outra | OK | Concorrência testada em memória; a garantia de base devia ter também teste em PostgreSQL. |

### `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaHistoricoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoProgramadaHistoricoTests.Imprimir_SoMostraAsPorRetirar_EAsFechadasAbremVazias_EFiltramPeloReparador | Impressão só com as por retirar; fechadas no histórico | OK |  |
| ReparacaoProgramadaHistoricoTests.NovaLista_CriaComOReparador_ESemListaOAdicionarAvisa | Nova lista com reparador; sem lista avisa e não cria | OK |  |

### `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoProgramadaPaginaTests.ArmazemEntrada_DeFerramentaNaLista_SugereReparado_AvisaAoMudar_EALinhaFicaCinza | Entrada no Armazém sugere Reparado e avisa ao mudar | OK |  |
| ReparacaoProgramadaPaginaTests.Ferramentas_VeOSeparador_AProcuraAbreVazia_EAssociaAUmaListaNova | Procura abre vazia e associa a lista nova | OK | Algumas verificações são de classes CSS (dmo-com-ajuda); o essencial lê a base. |
| ReparacaoProgramadaPaginaTests.SemFerramentas_NaoEntra_NemAssocia_NemImprime | Sem módulo Ferramentas não entra nem associa | OK |  |

### `tests/DMO.Alpha.Web.Tests/Ferramentas/ReparacaoProgramadaPaginaTests.Artigo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoProgramadaPaginaTests.ListaEProcura_MostramAReferenciaCompletaAFrenteDaFerramenta | Registar Reparação: em frente à ferramenta, a referência completa do artigo da última produção, na lista e na procura; exporta rp-lista.html para o browser (DMO-674) | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasFichaTrocaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Ficha_AbertaDeUmFluxo_GuardarOEditarMantemORegresso | Guardar na ficha mantém o regresso ao fluxo | OK | Recusa com quantidade -5 não confirma na base que nada foi gravado. |
| FerramentasPagesTests.Ficha_BotoesDeCadaCard_NoCantoSuperiorDireito | Botões de cada card no canto superior direito | FRACO | Regex sobre layout/classes; nenhum efeito verificado. |
| FerramentasPagesTests.Ficha_Guardar_ConfirmaNoCantoASecaoGuardada | Guardar confirma a secção guardada | OK |  |
| FerramentasPagesTests.Ficha_TrocaSoAsZonas_ATecnicaFicaComoOOperadorADeixou | Ficha troca só as zonas; técnica como deixada | FRACO | Verifica HTML exato, ordem de atributos e classes (regra 10). |
| FerramentasPagesTests.Ficha_ValorTecnicoPorLer_FicaNoCampoParaCorrigir_ComOFoco | Valor ilegível fica no campo, com aviso de não gravado | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.BqDoJobOn.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.CriarBqDoJobOn_ComAReferenciaTotal_GuardaETornaAMostrarAReferenciaToda | BQ criada a partir do Job On guarda referência toda | OK |  |
| FerramentasPagesTests.CriarBqDoJobOn_SemLote_ERecusada_ENadaEGravado | BQ sem lote é recusada e nada grava | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CliquesRepetidos.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Duplicar_GravaPelaFilaDaPagina_AJanelaNumaZona | Duplicar grava pela fila da página | FRACO | Só confere atributos HTML; não duplica duas vezes. |
| FerramentasPagesTests.Eliminar_GravaPelaFilaDaPagina | Eliminar grava pela fila da página | FRACO | Só confere atributos HTML; não elimina duas vezes. |
| FerramentasPagesTests.Registar_GravaPelaFilaDaPagina_AsMensagensNumaZona | Registar grava pela fila da página | FRACO | Só confere atributos HTML (data-dmo-parcial); não faz cliques repetidos. |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Decimais.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FichaDecimaisTests.Decimal2_InteirosSemCasas_DecimaisComDuas | Inteiros sem casas, decimais com duas | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.BarraDasFerramentas_PesquisarERegistarReparacao_SemOsNomesAntigos | Barra com Pesquisar e Registar Reparação, sem Histórico | OK |  |
| FerramentasPagesTests.Ficha_TemOBotaoHistorico_QueAbreOsJobOnDaFerramenta | Histórico mostra produções da ferramenta com % uso por id; menu com Job On e, sem acesso, Controlo apagado (DMO-668) | OK | Inclui verificações de classes CSS. |
| FerramentasPagesTests.HistoricoSemFerramenta_VaiParaOPesquisar | Histórico sem ferramenta vai para Pesquisar | OK |  |
| FerramentasPagesTests.Historico_ComControloERi_OMenuTemOsTresDestinos | Menu da linha com Job On, Controlo, Reparação (e Boquilhas, apagada sem BQ); % uso (DMO-668) | OK |  |
| FerramentasPagesTests.Historico_SemAChaveDasFerramentas_NaoAbre | Histórico sem chave Ferramentas não abre | FRACO | Só exige código diferente de 200; um erro 500 passava. |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.MfAssociada.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Ficha_ReferenciaMfAssociada_SoNaCmComReferenciaDiferente | Ref. MF associada só na CM com referência diferente | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.NomeTecnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.NomeTecnico_Desconhecido_SemTipoNemLeitura_EOAnonimoNaoLe | Nome técnico desconhecido sem tipo; anónimo não lê | OK |  |
| FerramentasPagesTests.NomeTecnico_LidoPeloServidor_DaOTipoEOsDetalhesSemReferencia | Nome técnico lido pelo servidor dá tipo sem referência | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.PedidoAbortado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.PostCreate_ClientAbortsAfterTheToolIsRegistered_TheTechnicalValuesAreStillSaved | Cliente aborta após registar: técnica fica gravada | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.RegressoComTab.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.PostCreate_RegressoComTabOuEspaco_GravaEAbreAFicha_SemErro | Regresso com TAB/espaço ignorado, grava e abre ficha | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.CriarEEditar_UmSoFormulario_EditarGravaONomeTecnico | Criar e Editar são o mesmo formulário; grava nome técnico | OK | Nome técnico gravado em maiúsculas lido da base. |
| FerramentasPagesTests.Ficha_BqSemQuantidade_AvisaQueAFabricacaoNaoECalculada | BQ sem quantidade do lote avisa que fabricação não calcula | OK |  |
| FerramentasPagesTests.Ficha_Editar_LinhasCompativeis_InformacaoOpcionalComFabricaDeOrigem | Editar com Informação opcional e fábrica de origem | OK | Metade do teste é ordem de elementos no HTML; o efeito (fábrica MG) é lido da base. |
| FerramentasPagesTests.Ficha_PercentagemDeUso_NoEditar_GravaComOGuardar_SoAvisos | % uso no Editar grava leitura nova; ilegível avisa | OK |  |
| FerramentasPagesTests.Registo_InformacaoTecnicaEmSeccoesComDica_QuantidadeAntesDaRefMf | Registo com secções, dicas e quantidade antes da Ref. MF | FRACO | Ordem de campos, placeholders e classes; DoesNotContain sem «aparece» prévio em alguns casos. |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SemTampaoNaCm.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Ficha_Cm_MostraOVolumeDaPuncao_EGuardaOValorEditado | CM mostra volume da punção e guarda o editado | OK |  |
| FerramentasPagesTests.Ficha_GuardarSemTampaoNemPuncao_MantemOsValoresGravados | Guardar sem tampão nem punção mantém valores gravados | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoCheio.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.UsoCheio_SoMudaACor_NaFichaENoPesquisar_NuncaAvisaNemBloqueia | Uso cheio só muda a cor, nunca avisa nem bloqueia | FRACO | Verifica a classe CSS dmo-uso-cheio (regra 10). |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoEscrito.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Criar_CaixaDoUso_DeixaEscreverOValorTodo | Caixa do % uso deixa escrever o valor todo | FRACO | Só verifica o atributo maxlength do input. |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoNoCriar.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Criar_PercentagemDeUsoIlegivel_NaoGrava_EAvisa | % uso ilegível não grava leitura e avisa | OK |  |
| FerramentasPagesTests.Criar_PercentagemDeUso_GravaAPrimeiraLeitura_SoAvisos | % uso no Criar grava a primeira leitura | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Voltar.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Ficha_OVoltar_VoltaAPaginaDeOndeSeVeio_SemOrigemALista | Voltar da ficha volta à página de origem | FRACO | Só compara o HTML do botão; a troca é feita por JavaScript não testado. |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VoltarJobOn.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Ficha_ComVoltarDoJobOn_MostraOVoltarParaAFolhaExata | Voltar da ficha leva à folha exata do Job On | OK |  |
| FerramentasPagesTests.Ficha_SemVoltarValido_NaoMostraOVoltarAoJobOn | Voltar inválido ou externo é ignorado | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VolumeDaBq.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Criar_TituloDoBloco_VolumeNaBqEPesoNaCm | Bloco técnico: Volume na BQ e Peso na CM | FRACO | Verifica spans com atributo hidden, ou seja, apresentação. |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.AnonymousRequests_ChallengedBeforeAnyReadOrWrite | Anónimo é desafiado antes de ler ou escrever | OK |  |
| FerramentasPagesTests.CreateTool_BackslashOrNoToolIdReturn_OpensTheSheet | Regresso com barra invertida ou sem id abre a ficha | OK |  |
| FerramentasPagesTests.CreateTool_Refusal_KeepsTheTechnicalName | Recusa mantém o nome técnico escrito | FRACO | Não verifica na base que nada foi gravado. |
| FerramentasPagesTests.CreateTool_UsesMachineDefaultWithoutJavaScript_AndKeepsManualOverride | Quantidade do lote sugerida pela máquina; manual prevalece | OK | Coerente com Qtd. lote = BqAccountedQuantity. |
| FerramentasPagesTests.Create_LowercaseReferences_AreSavedInUppercase | Referências em minúsculas gravam-se em maiúsculas | OK |  |
| FerramentasPagesTests.Criar_APartirDoCartaoDoJobOn_OTipoFicaFixo | Criar a partir do cartão do Job On fixa o tipo | FRACO | Verifica atributos HTML, ordem de elementos e classes; não regista nada. |
| FerramentasPagesTests.DeleteFlow_WarningShowsImpact_ExplicitConfirm_PRGToIndex_HistoryPreserved | Eliminar pede confirmação e preserva o histórico (DUMMY) | OK | Transação do eliminar só em memória. |
| FerramentasPagesTests.Ficha_CamposTecnicos_NoMaximoDuasCasas_SemZerosNoFim | Campos técnicos com no máximo duas casas | OK |  |
| FerramentasPagesTests.Ficha_SemMaquinas_AvisaComLinkParaOEditar | Ficha sem máquinas avisa com link para Editar | OK |  |
| FerramentasPagesTests.GetFicha_Bq_ShowsOnlyBqTechnicalFields_AndPostKeepsCmValuesOffTheBq | Ficha da BQ só com campos técnicos da BQ | OK |  |
| FerramentasPagesTests.GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered | Ficha compõe factos e esconde o tampão da CM | FRACO | Dezenas de DoesNotContain de strings e rótulos; apresentação. |
| FerramentasPagesTests.GetFicha_OldRefusalLinks_StillShowTheirMessages | Links antigos de recusa ainda mostram a mensagem | FRACO | Só confere textos de mensagens; sem efeito de negócio. |
| FerramentasPagesTests.GetFicha_UnknownToolId_CleanWarning_NothingInvented | Ficha de id desconhecido avisa sem inventar | OK |  |
| FerramentasPagesTests.GetIndex_FiltersOnlyReduceCandidates_ExcludingDummy | Filtros só reduzem candidatas; DUMMY nunca aparece | OK |  |
| FerramentasPagesTests.GetIndex_ShowsHumanFactsList_ToolIdOnlyInUrl_NeverAField | Lista mostra factos humanos; tool_id só no URL | FRACO | Muitas ausências de strings (sessionStorage, .html, <script>) e atributos data-*; pouco efeito de negócio. |
| FerramentasPagesTests.Pagination_PreservesFilters_AndExportsBothPagesForBrowserChecks | Paginação mantém filtros e conta linhas por página | OK |  |
| FerramentasPagesTests.PostCreate_ContextualReturnTemplate_GetsMintedToolIdBack | Registo contextual devolve o id da nova ferramenta | OK |  |
| FerramentasPagesTests.PostCreate_EmptyLot_SavesNoLotWithWarning_AndASecondOneIsRefused | Lote vazio grava «s/lote» com aviso; segundo recusado | OK |  |
| FerramentasPagesTests.PostCreate_InvalidReturnTemplate_Ignored_LocalOnly | Regresso externo é ignorado e abre a ficha | OK |  |
| FerramentasPagesTests.PostCreate_Refusal_KeepsTheTechnicalValues_AndOpensThem | Recusa mantém os valores técnicos escritos | FRACO | Não verifica na base que nada foi gravado. |
| FerramentasPagesTests.PostCreate_RegistersRealTool_PrgToFicha_MintedBackendId | Registar grava ferramenta com id gerado e abre a ficha | OK |  |
| FerramentasPagesTests.PostCreate_Repetida_EBloqueada_AExistenteFicaComAvisoQueSeLimpa | Ferramenta repetida recusada; existente fica com aviso | OK | Unicidade só testada em memória; falta teste da chave única em PostgreSQL. |
| FerramentasPagesTests.PostCreate_TypedRefusalByPrg_MessageShown_NoWrite | Tipo desconhecido recusa sem gravar nada | OK |  |
| FerramentasPagesTests.PostCreate_UnreadableTechnicalValue_SavesTheTool_AndTheSheetWarns | Valor técnico ilegível: grava ferramenta e avisa | OK |  |
| FerramentasPagesTests.PostCreate_UserWithoutTemplate_IsRefusedAndNothingIsWritten | Sem módulo Ferramentas o registo é recusado e nada grava | OK |  |
| FerramentasPagesTests.PostCreate_WarningWithContextualReturn_OpensTheSheetWithAContinueLink | Aviso no registo contextual abre a ficha com continuar | OK |  |
| FerramentasPagesTests.PostCreate_WithTechnicalValues_SavesThemOnTheNewTool_AndReturnsToTheFlow | Registo grava a informação técnica e volta ao fluxo | OK |  |
| FerramentasPagesTests.PostDuplicate_LotAlreadyRegistered_IsRefused_AndTheExistingGetsTheWarning | Duplicar para lote existente é recusado, existente avisada | OK |  |
| FerramentasPagesTests.PostDuplicate_NewLotRequired_OpensNewToolFicha_SourceIntact | Duplicar exige lote novo e deixa a fonte intacta | OK |  |
| FerramentasPagesTests.PostEditFacts_EmptyLot_SavesNoLotWithWarning_CollisionLinksTheExistingTool | Editar com lote vazio avisa; colisão liga à existente | OK |  |
| FerramentasPagesTests.PostEditFacts_FullReplaceSameToolId_PrgPersisted | Editar factos grava no mesmo id | OK |  |
| FerramentasPagesTests.PostEditFacts_MudarOTipoDeUmaBqUsada_GravaEAvisaComLinkParaOEditar | Mudar o tipo de BQ usada grava e avisa | OK |  |
| FerramentasPagesTests.PostEditFacts_Refused_FormComesBackWithEverythingTyped_NothingSaved | Edição recusada volta com tudo escrito e nada grava | OK |  |
| FerramentasPagesTests.PostEditFacts_UnreadableQuantity_KeepsTheStoredOne_SavesTheRest_AndWarns | Quantidade do lote ilegível mantém a gravada e avisa | OK |  |
| FerramentasPagesTests.PostEditTechnical_EmptyValue_IntentionallyClearsIt | Valor técnico vazio limpa o campo | OK |  |
| FerramentasPagesTests.PostEditTechnical_FieldOfAnotherType_IsNotWarnedAbout | Campo de outro tipo não gera aviso | OK |  |
| FerramentasPagesTests.PostEditTechnical_GenesisSheetValues_PrgPersistedExactly | Editar técnica grava valores exatos e mantém derivados | OK |  |
| FerramentasPagesTests.PostEditTechnical_ImpossibleValue_KeepsTheOldValue_AndWarns | Valor técnico impossível (≤0) mantém o antigo e avisa | OK |  |
| FerramentasPagesTests.PostEditTechnical_UnreadableField_SavesTheReadableOnes_KeepsTheOldValue_AndWarns | Campo técnico ilegível mantém o antigo e avisa | OK |  |
| FerramentasPagesTests.PostEdit_SavedMeanwhileByAnotherUser_RefusesFactsAndTechnical_NothingSaved | Edição com versão antiga é recusada sem gravar | FRACO | Em memória a versão é sempre 0 (regra 7); o token real só em PostgreSQL. |
| FerramentasPagesTests.Registry_PagesTenOrTwentyRows_AndNavigationPreservesFiltersAndReturn | Lista pagina 10 ou 20 mantendo filtros e regresso | OK |  |
| FerramentasPagesTests.Results_ReturnOnlyCandidates_WithoutLoadingMachines_AndPreserveContext | Resultados parciais só candidatas, mantendo o contexto | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasServicesRegistrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasServicesRegistrationTests.FerramentasServices_ResolveAsScopedImplementations | Serviços das Ferramentas registados com vida Scoped | FRACO | Teste de ligação de injeção de dependências; não garante regra de negócio. |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnAvisoArmazemEmEsperaTests.ComOInterruptor_OAvisoDoArmazemVolta | Com interruptor o aviso do Armazém volta | OK |  |
| JobOnAvisoArmazemEmEsperaTests.PorOmissao_OAvisoDoArmazemMostraSe | Por omissão o aviso «não registada no Armazém» mostra-se | OK | Corrigido em DMO-580 (#285), Diogo 28/10. |
| JobOnAvisoArmazemEmEsperaTests.SemOInterruptor_OAvisoDoArmazemNaoAparece_EOsOutrosSim | Sem interruptor o aviso do Armazém não aparece | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnDuplicarFolhaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnDuplicarFolhaTests.Duplicar_MarcaConfirmarNaMaquinaNasFerramentasENasUtilizacoesCopiadas | Duplicar marca «Confirmar» no que foi copiado | OK |  |
| JobOnDuplicarFolhaTests.Duplicar_SemFolhaNaOrigem_AbreAsMedidasPorOmissao_SemMarcasNaFolha | Duplicar sem folha abre medidas por omissão | OK |  |
| JobOnDuplicarFolhaTests.Duplicar_TrazAFolhaDaOrigem_EOGuardarGravaANaProducaoNova | Duplicar traz a folha da origem e grava na nova | OK |  |
| JobOnDuplicarFolhaTests.NovoEEditar_NaoTemMarcasDoDuplicar | Novo e Editar não têm marcas do Duplicar | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnEliminarTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnEliminarTests.ComUmModuloDoControlo_AFichaMostraOControlo | Com módulo Controlo a ficha mostra Controlo | OK |  |
| JobOnEliminarTests.Eliminar_AntesDoInicio_SemRegistos_SoPergunta | Eliminar antes do início só pergunta | OK |  |
| JobOnEliminarTests.Eliminar_DepoisDoInicio_AvisaQuePodeTerProduzido_EDizOQueSePerde_EElimina | Eliminar após o início avisa, diz o que se perde, elimina | OK |  |
| JobOnEliminarTests.SemModuloDoControlo_AFichaNaoMostraOControlo_NemContaOsRegistosDele | Sem módulo Controlo a ficha não mostra Controlo | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFichaTests.AvisoDoArmazem_EmEspera_NaoApareceNaFicha_NemNoEndpoint_MasOsOutrosSim | Com o interruptor desligado, o aviso do Armazém some e os outros ficam | OK | Testa só o interruptor posto a false; por omissão o aviso mostra-se (DMO-580). |
| JobOnFichaTests.CaloteEditavel_GravaPelaEdicao | Calote editável grava pela edição | OK |  |
| JobOnFichaTests.CaloteIgnorada_NaCriacao_GravaOJobOnSemCaloteEAvisa | Calote ilegível na criação grava sem calote e avisa | OK |  |
| JobOnFichaTests.CaloteIgnorada_NaEdicaoSemCaloteAnterior_FicaSemCaloteEAvisa | Calote ilegível sem anterior fica vazia e avisa | OK |  |
| JobOnFichaTests.CaloteIgnorada_NaEdicao_MantemACaloteAnteriorEAvisa | Calote ilegível na edição mantém a anterior e avisa | OK |  |
| JobOnFichaTests.CaloteNaCriacao_PreservaODecimalAoGuardarEReabrir | Calote na criação preserva o decimal | OK |  |
| JobOnFichaTests.CartoesDasFerramentas_AssociarOuCriar_EEscolhidaMostraSoAInformacao | Cartão escolhido mostra só a informação | FRACO | Verifica textos de botões e atributos data-*. |
| JobOnFichaTests.CartoesDasFerramentas_ONomeEAbrirFicha_LevamAFichaDaFerramenta | Nome e «Abrir ficha» levam à ficha da ferramenta | FRACO | Compara HTML com classes dos links; não segue o link. |
| JobOnFichaTests.CartoesDasFerramentas_RetirarSoNoNovoJobOn_ENuncaNoMf | Retirar só no Job On novo e nunca no MF | FRACO | Só atributos e textos de botões; nenhum efeito gravado. |
| JobOnFichaTests.CriarBq_LevaAoRegistoDasFerramentasOTipoBqEAMaquinaDaProducao | Criar BQ leva ao registo com tipo BQ e a linha | OK |  |
| JobOnFichaTests.CriarCmNaCriacao_VoltaComOQueEstavaEscrito | Criar CM na criação volta com o que estava escrito | OK |  |
| JobOnFichaTests.CriarCmNoEditar_VoltaAEdicaoDoMesmoJobOnERegistaATroca | Criar CM no Editar volta ao mesmo Job On e regista troca | OK |  |
| JobOnFichaTests.DuplicarEsteJobOn_CopiaOsDadosParaUmaProducaoNova | Duplicar copia os dados para produção nova | DUPLICADO | Igual a JobOnDuplicarFolhaTests.Duplicar_TrazAFolhaDaOrigem_EOGuardarGravaANaProducaoNova, mas só com HTML. |
| JobOnFichaTests.Editar_DepoisDaAprovacao_OControloAvisaEMantemAAprovacao | Editar após aprovação: Controlo avisa e mantém aprovação | OK |  |
| JobOnFichaTests.Editar_RegistaOQueMudou_EAFichaMostraAAcao | Editar regista o que mudou e a ficha mostra a ação | OK |  |
| JobOnFichaTests.Ficha_AcoesEFactos_AoUsoDoHtml | Ficha mostra ações e factos, avisos só no Editar | FRACO | Muitos DoesNotContain de texto e atributos data-jof; pouco efeito. |
| JobOnFichaTests.Ficha_FerramentasUsadas_AbremAFichaDaFerramenta_SemAvisoDeDadosEmFalta | Ferramentas usadas abrem a ficha, sem aviso de dados | OK |  |
| JobOnFichaTests.Ficha_FerramentasUsadas_MostramOUsoDaUltimaLeitura | Ferramentas usadas mostram a última leitura de % uso | OK |  |
| JobOnFichaTests.Ficha_FerramentasUsadas_OndeEstaNoArmazem_EAvisoSemRegisto | Folha mostra onde está no Armazém e avisa sem registo | OK |  |
| JobOnFichaTests.Ficha_SemColocacaoManualNemRetirarDoPlaneamento | Ficha sem colocação manual nem retirar do planeamento | FRACO | Quase só DoesNotContain de texto. |
| JobOnFichaTests.FimAntesDoInicio_NaEdicao_GravaEAvisa | Fim antes do início na edição grava e avisa | OK |  |
| JobOnFichaTests.FimNoDiaDoInicioOuDepoisNaCriacao_GravaEFicaNoJobOnSemAviso | Fim no dia do início ou depois grava sem aviso | OK |  |
| JobOnFichaTests.FimUmDiaAntesDoInicioNaCriacao_GravaEAvisa | Fim antes do início na criação grava e avisa | OK |  |
| JobOnFichaTests.MudarACm_VerificarOPeso_AbreOPesoDaProducao | Mudar a CM leva a verificar o Peso da produção | OK |  |
| JobOnFichaTests.OQueMudou_SoAsAlteracoesFeitasDepoisDoInicio | «O que mudou» só conta alterações depois do início | OK |  |
| JobOnFichaTests.Opcoes_EditarPrimeiro_SemDuplicarLote_EFecharVoltaAFicha | Editar e Duplicar à vista; sem «Duplicar lote» | OK |  |
| JobOnFichaTests.PrepararProducao_AMaquinaVaziaAbreOJobOnVazio_ComDiaEMaquina | Linha vazia abre Job On vazio com dia e linha | OK |  |
| JobOnFichaTests.ProducoesDoDia_DuasNaMesmaMaquina_NaoMarcamSobreposicao | Duas produções na mesma linha não marcam sobreposição | OK | Usa classe da linha como «aparece». |
| JobOnFichaTests.ProducoesDoDia_LinhaNuncaFicaVermelha_PorProducoesQueSeCruzam | Linha nunca fica vermelha por produções que se cruzam | OK | Parecido com ProducoesDoDia_DuasNaMesmaMaquina_NaoMarcamSobreposicao. |
| JobOnFichaTests.ProducoesDoDia_MostramAProximaMudancaEmCadaMaquina | Cada linha mostra a próxima mudança, com link | OK |  |
| JobOnFichaTests.Resumo_SemOModulo_ALinhaFicaSoComoInformacao | Sem o módulo, a linha do resumo fica só informação | OK |  |
| JobOnFichaTests.ToolFactsEndpoint_AvisosLevamLinkParaOndeSeResolvem | Avisos das ferramentas levam link para onde se resolvem | OK |  |
| JobOnFichaTests.ToolFactsEndpoint_DatasQueNaoSeCruzam_SemAvisoDeUsoParalelo | Datas que não se cruzam não dão aviso de uso paralelo | OK |  |
| JobOnFichaTests.ToolFactsEndpoint_FatosEAvisos_DoBackend | Endpoint de factos dá factos técnicos e aviso de uso paralelo | OK |  |
| JobOnFichaTests.Update_SemAvisoDeSobreposicaoEntreProducoesDaLinha_ENuncaBloqueiaAGravacao | Editar nunca avisa sobreposição entre produções da linha | OK | Coerente com 25/10 (só BQs). |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFolhaPrimeiraFilaTests.Dias_AoCentro_FimMenosInicioMaisUm | Dias da produção = fim menos início mais um | OK |  |
| JobOnFolhaPrimeiraFilaTests.Editar_ModoEditarNoLugarDaProcura_EOProcessoNnpbPs_ComODaCmSugerido | Modo Editar no lugar da procura; processo da CM sugerido | OK |  |
| JobOnFolhaPrimeiraFilaTests.Folha_CartoesNaoColapsam_ComLarguraMinimaEMedidasSemScroll | Cartões da folha não colapsam (largura mínima) | FRACO | Lê o ficheiro CSS e procura texto; verifica estilos (regra 10). |
| JobOnFolhaPrimeiraFilaTests.Folha_PrimeiraFila_CmMfBq_NotasEmTodosOsCartoes_RotulosSemFichaDeMoldes | Primeira fila CM, MF, BQ com notas em todos os cartões | FRACO | Verifica ordem de cartões, classes e atributos data-jof; layout. |
| JobOnFolhaPrimeiraFilaTests.Folha_TpMostraACaloteGravada_NuncaAPalavraBacia | TP mostra a calote gravada, nunca «bacia» | OK |  |
| JobOnFolhaPrimeiraFilaTests.Peso_DuasCasasDecimaisNoMaximo_ComPonto | Peso da folha com duas casas decimais e ponto | OK |  |
| JobOnFolhaPrimeiraFilaTests.SoVer_NaoTemEditar_NemModoEditar_MesmoPedindoOEditar | Só ver não tem Editar nem Modo Editar | OK | Não verifica que o perfil «Job On VER» também não duplica nem vê Controlo/RI/Boquilhas (fluxo-jobon-novo.md). |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaSemBotoesNasLinhasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFolhaSemBotoesNasLinhasTests.OQueMudou_SemOModuloDaAcao_NaoHaBotaoNoCanto | Sem o módulo da ação não há botão no canto | OK |  |
| JobOnFolhaSemBotoesNasLinhasTests.OQueMudou_UmSoBotaoNoCanto_ParaAAlteracaoEscolhida_NenhumNasLinhas | «O que mudou»: um só botão no canto, nenhum nas linhas | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFolhaTests.Criar_GravaAFolhaNaMesmaEscritaDoJobOn | Criar grava a folha na mesma escrita do Job On | OK |  |
| JobOnFolhaTests.Editar_MostraAsFamiliasAosPares_ComAQtdStockEAQtdMaqEmCadaFamiliaENaCal | Editar mostra famílias aos pares com QTD Stock e Máq. | FRACO | Ordem de atributos e rótulos; DoesNotContain «opcional»; layout. |
| JobOnFolhaTests.Folha_ACalMostraAsLinhasGravadas_ComAQtdMaq_NaFolhaNova | CAL mostra as linhas gravadas com QTD Máq. | OK |  |
| JobOnFolhaTests.Folha_MostraOsCamposGravados_NaFolhaNovaBloqueada | Folha bloqueada mostra os campos gravados | OK |  |
| JobOnFolhaTests.Folha_SemNadaEscrito_MostraOsVaziosEOEditar_SemAFichaAntiga | Folha sem nada escrito mostra vazios e Editar | OK |  |
| JobOnFolhaTests.Guardar_GravaAFolhaDoFormulario_EOEditarVoltaAMostrarOsValores | Guardar grava a folha e o Editar mostra os valores | OK |  |
| JobOnFolhaTests.TextoLongoDemais_NoCriar_NaoCriaOJobOn_EAFolhaVoltaComOQueSeEscreveu | Texto longo no Criar não cria o Job On | OK |  |
| JobOnFolhaTests.TextoLongoDemais_NoEditar_ERecusado_EAFolhaVoltaComOQueSeEscreveu | Texto longo no Editar é recusado sem gravar nada | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOn/JobOnRemoverAlteracaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnRemoverAlteracaoTests.QuemSoVeOJobOn_NaoVeAJanela_EUmPostForjadoNaoMudaNada | Quem só vê não remove; POST forjado não muda nada | OK |  |
| JobOnRemoverAlteracaoTests.RemoverAUltima_AMarcaSai_OQueMudouDesaparece_EUmSegundoEnvioNaoFalha | Remover a última limpa «O que mudou»; repetir não falha | OK |  |
| JobOnRemoverAlteracaoTests.Remover_SaiDaMarcaEDoSumario_FicaRiscadaComQuemEQuando_EOsValoresNaoMudam | Remover alteração apaga-a sem mudar os valores do Job On | OK |  |
| JobOnRemoverAlteracaoTests.UmaAlteracaoDeOutroJobOn_NaoSeRemove | Alteração de outro Job On não se remove | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOn/RascunhoEEnterNaProcuraTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| RascunhoEEnterNaProcuraTests.NovoJobOn_AFolhaLevaAChaveDoRascunho_EOScriptQueOGuarda | Novo Job On leva a chave do rascunho e o script | FRACO | Só atributos e src do script; o rascunho em si não é testado. |
| RascunhoEEnterNaProcuraTests.ReparacaoProgramada_OEnterNaReferenciaENoLoteSubmeteAProcura_ENaoOAdicionar | Enter na referência submete a procura, não o Adicionar | FRACO | Procura texto do script na página; o JavaScript não é executado. |

### `tests/DMO.Alpha.Web.Tests/JobOnCabecalhoCssTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnCabecalhoCssTests.CabecalhoDoJobOn_ReferenciaEProducaoNaoSeTocam | Referência e Produção do cabeçalho não se tocam | FRACO | Lê o ficheiro CSS e procura texto (regra 10). |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CadenciaSap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_MostraACadenciaDoSap_DaOrdemComMaisDias | Folha mostra a cadência do SAP da ordem | OK | Coerente com sap-fabrico-zfmd.md. |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CalMm.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_CalibresComMm_OMmVaiParaOTitulo | Calibres com mm: o mm vai para o título | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Duplicar_AProducaoSugerida_EASeguinteAMaior | Produção sugerida ao duplicar é a seguinte à maior | OK |  |
| JobOnPageTests.FolhaNova_AbreComOTemplateSemEditar_SoReferenciaEProducao_EDepoisCria | Folha nova abre com o modelo; referência existente abre a antiga | OK |  |
| JobOnPageTests.FolhaNova_Criar_AbreVaziaEmEditar_EGuardaPeloCreateEVoltaAFolha | Folha nova abre vazia e guarda pelo Create | OK |  |
| JobOnPageTests.FolhaNova_Criar_NumeroRepetido_ERecusadoTipado_ESoAvisaAsDatasTrocadas | Nº de produção repetido recusado; datas trocadas só avisam | OK | Unicidade só em memória; falta teste PostgreSQL. |
| JobOnPageTests.FolhaNova_Criar_SemCmBqOuMf_ERecusado_AFolhaVoltaComOQueSeEscreveu_ENadaEGravado | Criar sem CM, BQ ou MF é recusado e nada grava | OK |  |
| JobOnPageTests.FolhaNova_Duplicar_CopiaOsDados_SemDatas_ComAProducaoSeguinte_ComODiaDoCalendario_EGravaUmaProducaoNova | Duplicar copia dados, sugere produção seguinte e grava | OK |  |
| JobOnPageTests.FolhaNova_JobOnVer_NaoCriaNemDuplica_SoVe | Job On VER não cria nem duplica, só vê | OK | Cobre a regra de fluxo-jobon-novo.md; falta verificar que não vê Controlo/RI/Boquilhas. |
| JobOnPageTests.FolhaNova_TP_ODiametroSemPS_ECampoIgual | TP mostra «Diâmetro» sem PS | FRACO | Só texto de rótulos. |
| JobOnPageTests.Planeamento_EscolherUmaProducao_AbreAFolhaNova_SemAFichaAntiga | Escolher produção no Planeamento abre a folha nova | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolgasDimensoes.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Editar_DoisNumerosNumaFolga_ERecusado | Dois números numa folga são recusados | OK |  |
| JobOnPageTests.Folha_ToleranciaComUmSoSinal_EFolgasUmNumeroPorEspaco | Tolerância com um só sinal; folgas um número cada | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao | Folha abre bloqueada com Editar, Duplicar e Opções | FRACO | Ordem de botões, textos e dezenas de DoesNotContain; layout. |
| JobOnPageTests.FolhaNova_Editar_DesbloqueiaEGuardarVoltaAFolha_SemApagarOQueNaoMudou | Editar desbloqueia e guardar não apaga o que não mudou | OK |  |
| JobOnPageTests.FolhaNova_JobOnVer_VeBloqueadaSemEditar_EOPlaneamentoLevaAFolha | Job On VER vê bloqueada; POST forjado não grava | OK |  |
| JobOnPageTests.FolhaNova_OpcoesBoquilhas_SoComBoquilhas_AbreOsMovimentosDaBQDesteJobOn | Opção Boquilhas só com o módulo, leva à BQ do Job On | OK |  |
| JobOnPageTests.FolhaNova_OpcoesControlo_ComControlo_AparecemControloAntesDaReparacao | Com Controlo, Opções mostram Controlo antes de Reparação | OK |  |
| JobOnPageTests.FolhaNova_ProducoesAnteriores_NoDropdown_AbremAFolhaDeCada | Produções anteriores no menu, a mais recente primeiro | OK |  |
| JobOnPageTests.FolhaNova_SemReferenciaMfMaisBq_SoAvisa | Referência diferente de MF+BQ só avisa | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaUsoCheio.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_FerramentaA100PorCento_ReferenciaELoteComCorLigeira | Ferramenta a 100% com cor ligeira na referência e lote | FRACO | Verifica a classe CSS dmo-uso-cheio (regra 10). |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhasImprimir.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_DecisoesDoDiogo_ComponentesSeparadosAnComLoteCaloteUnicaSemComentarios | Folhas com componentes separados, AN com lote, sem comentários | OK |  |
| JobOnPageTests.Folha_Imprimir_SaemAsQuatroFolhasComoAsDaFabrica | Saem as quatro folhas como as da fábrica | OK |  |
| JobOnPageTests.Folha_Imprimir_SoVer_NaoRecebeAsFolhas | Quem só vê não recebe as folhas de impressão | OK |  |
| JobOnPageTests.Folha_Imprimir_TemOMenuEAsFolhasComOsDadosDoJobOn | Imprimir tem as folhas com os dados do Job On | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.GotaNomeTecnico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_GotaVemDoNomeTecnicoDaCm_PorCimaDaEscrita | Gota vem do nome técnico da CM, por cima da escrita | OK |  |
| JobOnPageTests.Folha_SemNomeNaCm_GotaDoMf_ESemNenhum_AEscrita | Sem nome na CM usa o do MF; sem nenhum, a escrita | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ImprimirPretoBranco.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_ImprimirPretoBranco_CsAbertosFechados_TampaoDiametroXCalote_EPaginas | Impressão P&B: CS abertos/fechados, tampão e páginas | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LoteFf.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.FolhaNova_LoteFf_AbreComOLoteDoMf_EGravaOutro | Lote FF abre com o do MF e grava outro | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.LotesUsados.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Editar_AFerramentaNovaTrazOsDadosDela_UsoA0 | Ferramenta nova no Editar traz os dados dela e uso a 0 | OK |  |
| JobOnPageTests.Folha_DepoisDeAlterarOLoteDaBq_MostraOAtualEOsDeAntes | Depois de alterar o lote da BQ mostra o atual | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelCores.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.FolhaNova_PainelLateral_CadaCartaoLevaACorDaSuaLinha | Painel lateral: cada cartão com a cor da sua linha | FRACO | Verifica apenas classes CSS mch-X (regra 10). |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PlaneamentoSoVer.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Planeamento_JobOnVer_VeOCalendarioEAsLinhas_SemCriarNemHistorico | Job On VER vê calendário e linhas, sem criar nem histórico | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReferenciaNova.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_ReferenciaSemJobOn_AbreOJobOnNovoNaLinhaDeOndeVeio | Referência sem Job On abre Job On novo na mesma linha | OK |  |
| JobOnPageTests.Folha_ReferenciaSemJobOn_QuemSoVe_FicaComOAviso | Quem só vê e procura referência nova fica com aviso | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.ReparadorNaLinha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Folha_ReparadorDepoisDoEstadoEDaPosicao_SemLinhaAParte | Reparador a seguir ao estado e posição; «Sem registo» | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.Ronda.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.Criar_ReferenciaExistente_ComProducaoNova_AbreODuplicarComAProducaoEALinha | Referência existente abre a última para analisar; depois duplica | OK | Cobre «analisa-se e depois duplica-se» (fluxo-jobon-novo.md). |
| JobOnPageTests.Folha_UtilizacaoComPontoEAcimaDe100ComCor | Utilização com ponto e duas casas; acima de 100% a cor | OK | Metade do teste verifica a classe dmo-uso-cheio. |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.SoVerSemFerramentas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.JobOnVer_SoVeOPlaneamentoEOsJobOn_SemDuplicarNemOutrosModulos | Job On VER não duplica nem vê Controlo/RI/Boquilhas | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.TampaoDaFolha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.FolhaNova_DiametroDoTp_EOTampaoDaProducao | Diâmetro do TP grava o tampão da produção | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnPageTests.CalendarDay_NeverRemovesTheProductionFromItsDates | Escolher o dia nunca tira as datas da produção | OK | Guarda contra perda de dados. |
| JobOnPageTests.Calendario_ELinhasDoDia_TemIdsEstaveis_ParaOFocoFicarNoSitio | Calendário e linhas com ids estáveis para o foco | FRACO | Só verifica ids no HTML; o foco real não é testado. |
| JobOnPageTests.Calendario_SoMarcaOsDiasEmQueUmaLinhaMuda | Calendário só marca dias em que uma linha muda | FRACO | Distingue dias só pela classe CSS «has». |
| JobOnPageTests.CreateFlow_ExplicitSelections_EstablishesQueryableContext | Criar Job On com escolhas explícitas grava os três contextos | OK |  |
| JobOnPageTests.CreateFlow_WithOverlapOnSameMachine_SavesWithoutBanner | Sobreposição na mesma linha grava sem aviso | OK | Coerente com 25/10 (só BQs). |
| JobOnPageTests.CreateRefusal_IsTypedAndRedirectsWithPreservedValues | Recusa tipada preserva os valores e não cria nada | OK |  |
| JobOnPageTests.Create_ComOControloCriar_FicaNoJobOn_NaoVaiAoResumo | Criar com Controlo fica no Job On, não vai ao Resumo | OK |  |
| JobOnPageTests.Create_DoubleClick_SecondSubmitOpensTheSavedJobOn | Duplo clique no Criar abre o Job On gravado | OK | Envios em série, não simultâneos; falta PostgreSQL com duas pessoas. |
| JobOnPageTests.Create_DuplicateProductionNumber_RefusedTyped | Nº de produção repetido é recusado, nada se cria | OK | Unicidade só testada em memória. |
| JobOnPageTests.Create_LowercaseReference_IsSavedInUppercase | Referência em minúsculas grava-se em maiúsculas | OK |  |
| JobOnPageTests.Create_SemCmOuSemBq_ERecusado_ENadaEGravado | Criar sem CM, BQ ou MF é recusado e nada grava | OK |  |
| JobOnPageTests.Create_WithoutMfSelection_RefusedTyped_NothingCreated | Criar sem MF é recusado e nada se cria | DUPLICADO | Igual ao caso MissingMfTool de Create_SemCmOuSemBq_ERecusado_ENadaEGravado. |
| JobOnPageTests.EditFlow_MissingMfTool_RefusedTyped_JobOnUntouched | Editar sem MF é recusado e deixa o Job On intacto | OK |  |
| JobOnPageTests.EditFlow_Refused_ReopensTheSheetInEditMode_NotAsANewProduction | Recusa na edição reabre em edição, não como nova | OK |  |
| JobOnPageTests.EditFlow_ReplansTheSameProduction_WindowLineAndTools | Editar replaneia a mesma produção sem criar outra | OK |  |
| JobOnPageTests.EditFlow_SavedMeanwhileByAnotherUser_RefusesAndShowsWhatWasSaved | Edição com versão antiga é recusada e mostra o gravado | FRACO | Em memória a versão é sempre 0 (regra 7); duas pessoas ao mesmo tempo só valem em PostgreSQL. |
| JobOnPageTests.EditFlow_TheSameSaveTwice_GoesWhereTheFirstWent_WithoutTheRefusal | Guardar duas vezes não recusa nem duplica alteração | FRACO | Usa versão falsa «7» em memória; o token real é do PostgreSQL. |
| JobOnPageTests.EditFlow_UnknownJobOn_RefusedTypedWithPreservedForm | Editar Job On inexistente recusa e nada grava | OK |  |
| JobOnPageTests.Edit_PorABqNumJobOnSemBq_NaoEMudancaDeBoquilhas | Pôr a BQ num Job On sem BQ não é mudança | OK |  |
| JobOnPageTests.Edit_SemBq_ERecusado_ENadaMuda | Editar sem BQ é recusado e nada muda | OK |  |
| JobOnPageTests.Ficha_ProducoesDaReferencia_SaiComAFichaAntiga_AFolhaTemODropdown | Link antigo das produções leva à folha com o menu | OK |  |
| JobOnPageTests.Historico_AbreEmBranco_SoComOsFiltros_SemLerProducoes | Histórico abre em branco, só com filtros | OK |  |
| JobOnPageTests.Historico_DataInvalida_IgnoradaComAviso_NaoRecusa | Data inválida no histórico é ignorada com aviso | OK |  |
| JobOnPageTests.Historico_Filtros_TrocamSoOsResultadosEAsProducoes | Filtros do histórico trocam só resultados e produções | FRACO | Compara HTML fora das zonas e atributos data-dmo-*; sem efeito de negócio. |
| JobOnPageTests.Historico_Mostra25ProducoesPorPagina_MaisRecentesPrimeiro | Histórico: 25 produções por página, mais recentes primeiro | OK |  |
| JobOnPageTests.Historico_PorData_MostraAsProducoesCujaJanelaTocaOPeriodo | Histórico por data mostra janelas que tocam o período | OK |  |
| JobOnPageTests.Historico_PorReferencia_DropdownComAsProducoesDela_EFiltraPelaEscolhida | Histórico por referência com menu das produções dela | OK |  |
| JobOnPageTests.Historico_ReferenciaEMaquina_DropdownSoComAsProducoesDessaMaquina | Histórico por referência e linha filtra as produções | OK |  |
| JobOnPageTests.Historico_ReferenciaPeloInicio_ODropdownLevaAReferenciaEFiltraSoEssaProducao | Referência pelo início ou parte BQ, nunca a meio | OK |  |
| JobOnPageTests.Historico_RespostaParcial_PaginaSemLerCalendarioOuRail_EPreservaFiltros | Resposta parcial do histórico pagina sem ler o calendário | OK | Conta leituras da query (detalhe de implementação). |
| JobOnPageTests.HistoryTabListsProductionsWithHumanToolAssociations | Histórico lista produções com ferramentas e sem dia | OK |  |
| JobOnPageTests.PlaneamentoRedirectsToJobOn_TheGridIsGone | /Planeamento redireciona para /JobOn | OK |  |
| JobOnPageTests.Planeamento_CriarJobOn_FicaNaBarraDaProcura_ADireitaDasCaixas_ComODia | Criar Job On à direita da procura, com o dia | FRACO | Posições de elementos, classes e ajuda encostada; layout. |
| JobOnPageTests.Procura_AoEscrever_SemLogin_ERecusada | Procura sem sessão é recusada | FRACO | Só exige código diferente de 200. |
| JobOnPageTests.Procura_EmDoisCampos_AReferenciaEncheODropdownDasProducoes | Referência completa enche o menu com as 5 últimas produções | OK |  |
| JobOnPageTests.Procura_EmDoisCampos_NaoEncontraPelaFerramenta | Procura de produção não encontra pela ferramenta | OK |  |
| JobOnPageTests.Procura_SugereAsReferenciasPorBaixo_ParaOBrowser | Procura sugere as referências por baixo do campo | OK |  |
| JobOnPageTests.ProximaMudanca_FicaANegrito | Próxima mudança a negrito | FRACO | Lê o CSS e procura texto (regra 10). |
| JobOnPageTests.SelectedCalendarDay_ClickUnselects_AndNoDayShowsOnlyTheHint | Clicar no dia escolhido tira-o | FRACO | Só atributos data-dmo-* e classes; o clique é JavaScript. |
| JobOnPageTests.Sheet_RendersOnlyTheChosenTools_NeverTheWholeCatalog | Folha mostra só as ferramentas escolhidas | OK |  |
| JobOnPageTests.ToolCandidates_Cm_FollowsTheChosenMfAndMachine_AndFallsBackToTheProductionReference | CM segue o MF e a linha, ou a referência da produção | OK |  |
| JobOnPageTests.ToolCandidates_ComCmEscolhida_ABqMostraAsRelacionadas_EEscreverMostraTodas | Com CM escolhida a BQ mostra as relacionadas | OK |  |
| JobOnPageTests.ToolCandidates_FilterByMachineAndProduction_AndShowAllRestoresAlternatives | Candidatas filtradas por linha e referência; «todas» repõe | OK |  |
| JobOnPageTests.ToolCandidates_Mf_ShowsOnlyTheMfOfTheChosenCm_AndTheMachine | MF mostra só as da CM escolhida e da linha | OK |  |
| JobOnPageTests.ToolCandidates_ReturnAtMost20_WithTheTotal | Candidatas devolvem no máximo 20 com o total | OK |  |
| JobOnPageTests.ToolCandidates_SearchAsYouType_FindsByPartialReferenceOrLot | Procura ao escrever acha por referência ou lote | OK |  |
| JobOnPageTests.UnauthenticatedRequests_ReceiveNoPageAndNoMutation | Sem sessão não há página nem escrita | OK |  |

### `tests/DMO.Alpha.Web.Tests/JobOnServicesRegistrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnServicesRegistrationTests.JobOnServices_ResolveAsScopedImplementations | Serviços do Job On registados com vida Scoped | FRACO | Teste de injeção de dependências; não garante regra de negócio. |

### `tests/DMO.Alpha.Web.Tests/JobOnSidepanelWeekTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnSidepanelWeekTests.Em_curso_primeiro_e_as_que_ja_acabaram_ficam_de_fora | Em curso primeiro; as acabadas ficam de fora | OK |  |
| JobOnSidepanelWeekTests.Mostra_so_entradas_de_hoje_ate_uma_semana | Painel mostra entradas de hoje até uma semana | OK |  |
| JobOnSidepanelWeekTests.Uma_linha_so_tem_uma_producao_em_curso | Uma linha só tem uma produção em curso | OK |  |

### `tests/DMO.Alpha.Web.Tests/LoteTextoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| LoteTextoTests.OLote_SemZerosAEsquerda | Lote mostra-se sem zeros à esquerda | OK |  |
| LoteTextoTests.ONomeDoHistorico_UsaOLoteSemZero | Nome do histórico usa o lote sem zero | OK |  |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Armazem.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPageTests.ArmazemMovimentos_ParaQuemTemJobOnVer_JobOnCriar_OuArmazem | Sumário mostra movimentos do Armazém a quem vê o Job On | OK |  |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Filtros.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPageTests.Filtro_Controlo_MostraSoOsDoControloNoTopo | Filtro Controlo mostra só os do Controlo | OK |  |
| PlaneamentoSumarioPageTests.Filtro_JobOn_JuntaCriados_Mudancas_EExperiencias_SemOsImportantes | Filtro Job On junta criados, mudanças e experiências | OK |  |
| PlaneamentoSumarioPageTests.Filtro_SoMostraAsOpcoesDosModulosDaConta | Filtro só mostra opções dos módulos da conta | OK |  |
| PlaneamentoSumarioPageTests.Filtro_TemImportante_JobOn_Armazem_EControlo | Filtro do Sumário: Importante, Job On, Armazém, Controlo | FRACO | Só opções e ordem do menu. |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Importante.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPageTests.BqApagados_FicamNoTopoSeteDias | BQ apagados ficam no topo sete dias | OK |  |
| PlaneamentoSumarioPageTests.CadaLinha_TemLink_ParaUmaPaginaQueAContaAbre | Cada linha do sumário tem ligação a página acessível | OK |  |
| PlaneamentoSumarioPageTests.Importante_SoOsQuePedemAcao | Importante são só os que pedem ação | OK |  |
| PlaneamentoSumarioPageTests.NaoAprovado_FicaAteSerAprovado | Não aprovado fica no sumário até aprovação | OK |  |
| PlaneamentoSumarioPageTests.OPonto_ApareceComImportantesPorVer_EDesapareceAoAbrirOSumario | Ponto de importantes por ver some ao abrir sumário | OK |  |
| PlaneamentoSumarioPageTests.OPonto_ContaOsLotesNovosPorCriar_ParaQuemTemFerramentas | Ponto conta lotes novos por criar, só Ferramentas | OK |  |
| PlaneamentoSumarioPageTests.OPonto_NaoApareceSoComInformacao | Ponto não aparece só com informação | OK | Tem par positivo no teste anterior |
| PlaneamentoSumarioPageTests.OsImportantes_FicamNumGrupoNoTopo_AInformacaoPorBaixo | Importantes em grupo no topo, informação por baixo | OK |  |
| PlaneamentoSumarioPageTests.QuemVe_DadosEmFaltaComOControloCriar_UsoELotesComAsFerramentas | Quem vê dados em falta, uso e lotes novos | OK |  |
| PlaneamentoSumarioPageTests.UmImportanteDeOutroDia_FicaAteResolver_EDepoisSai | Importante de outro dia fica até resolver | OK |  |
| PlaneamentoSumarioPageTests.UmImportante_ALinhaInteiraAbreAPaginaQueResolve | Linha inteira de importante abre a página que resolve | OK |  |
| PlaneamentoSumarioPageTests.UsoAtualizado_FicaAteHaverALeituraDoFim_SoQuemTemFerramentas | Uso atualizado fica até leitura do fim, só Ferramentas | OK |  |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.PainelLateral.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPageTests.Sumario_TemOPainelLateralDoPlaneamento_ComAsProximasEntradas | Painel lateral do Planeamento mostra próximas entradas | OK |  |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.SoVer.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPageTests.WithJobOnViewOnly_TheTabsLeadToPagesTheProfileCanOpen | Perfil só ver: separadores levam a páginas abertas | OK |  |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.SoVerJobOn.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPageTests.JobOnVer_RecebeOsJobOnEAsMudancas_ComLinkQueAbre | Job On Ver recebe Job On e mudanças com link | OK |  |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPageTests.AJobOn_IsAnnouncedOnTheDayItWasCreated_NotOnlyOnItsStart | Job On anunciado no dia de criação | OK |  |
| PlaneamentoSumarioPageTests.AMesmaAlteracao_NoMesmoMinuto_FicaNumaSoLinha_EOutraContinua | Mesma alteração no mesmo minuto numa só linha | OK | Nada repetido |
| PlaneamentoSumarioPageTests.Changes_AreListed_AlsoBeforeTheStart_AndTheDaysFilterReachesBack | Alterações listadas antes do início; filtro de dias recua | OK |  |
| PlaneamentoSumarioPageTests.ControloInformation_IsForEitherControloModule | Informação de Controlo vale para qualquer módulo Controlo | OK |  |
| PlaneamentoSumarioPageTests.DeletedBqMovements_AreOnlyForBoquilhas | BQ apagados só para Boquilhas | OK |  |
| PlaneamentoSumarioPageTests.Linhas_TemADataAFrente_AMaquinaComACor_ESemContagens | Linha com data à frente, máquina com cor, sem contagens | FRACO | Pode verificar cor/estilo em vez do efeito (regra 10) |
| PlaneamentoSumarioPageTests.Planeamento_SeesTheProductionsStartingToday_AndTheTabSitsNextToJobOn | Planeamento vê produções do dia, separador junto ao Job On | OK |  |
| PlaneamentoSumarioPageTests.TheCalendarOpensTheChosenDay | Calendário abre o dia escolhido | OK |  |
| PlaneamentoSumarioPageTests.ToolsWithMissingTechnicalData_AreForControloCreate_NotForFerramentas | Dados em falta só para Controlo criar | OK |  |
| PlaneamentoSumarioPageTests.UtilizacaoPorAtualizar_AvisaNoSumario_SoQuemTemFerramentas | Uso por atualizar avisa só quem tem Ferramentas | OK |  |
| PlaneamentoSumarioPageTests.WithJobOnView_ThePageOpensWithOnlyTheAccountModules | Job On Ver abre só com módulos da conta | OK |  |
| PlaneamentoSumarioPageTests.WithoutPlaneamento_ThePageIsRefused | Sem Planeamento a página é recusada | OK |  |

### `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPostgresTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PlaneamentoSumarioPostgresTests.TodosOsTipos_NoPostgres | Sumário com todos os tipos corre em PostgreSQL | OK | Consultas reais em PG |

### `tests/DMO.Alpha.Web.Tests/ToolCandidateQueryRegistrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ToolCandidateQueryRegistrationTests.IToolCandidateQuery_ResolvesAsScopedToolCandidateQuery | Registo da consulta de candidatas | FRACO | Só verifica registo no contentor DI |

### `tests/DMO.Alpha.Web.Tests/ToolLookupQueryRegistrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ToolLookupQueryRegistrationTests.IToolLookupQuery_ResolvesAsScopedToolLookupQuery | Registo da consulta de pesquisa de ferramentas | FRACO | Só verifica registo no contentor DI |

## Admin, login, base de dados e partes comuns

499 linhas: OK 465, FRACO 23, DUPLICADO 11

### `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminAuthorizationServiceTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminAuthorizationServiceTests.AuthorizeTemplateAssignment_AdminEmailUser_IsRefused | Não se atribui template ao ADMIN | OK |  |
| AdminAuthorizationServiceTests.AuthorizeTemplateAssignment_NormalUser_IsAllowed | Utilizador normal pode receber template | OK |  |
| AdminAuthorizationServiceTests.AuthorizeTemplateAssignment_UnknownUser_IsUserNotFound | Atribuir template a utilizador inexistente é recusado | OK |  |
| AdminAuthorizationServiceTests.Resolve_AdminEmailUserWithoutAssignmentRow_AlsoHasNoCapabilities | ADMIN sem atribuição é admin sem permissões de template | OK |  |
| AdminAuthorizationServiceTests.Resolve_AdminEmailUser_IsAlwaysResolvedWithoutTemplate | ADMIN nunca tem template, mesmo com atribuição gravada | OK |  |
| AdminAuthorizationServiceTests.Resolve_NoCurrentTemplate_HasNoTemplateDerivedCapabilities | Utilizador sem template não tem nenhuma permissão | OK |  |
| AdminAuthorizationServiceTests.Resolve_RenamedTemplate_EffectiveAccessIsUnchanged | Renomear uma template não muda o acesso de ninguém | OK |  |
| AdminAuthorizationServiceTests.Resolve_StandBy_DisablesCurrentAccessButPreservesAssignmentFacts | Utilizador arquivado perde o acesso mas mantém a template | OK |  |
| AdminAuthorizationServiceTests.Resolve_TemplateWithBothControlos_GrantsBoth | Template com Controlo criar e aprovar dá as duas permissões | OK |  |
| AdminAuthorizationServiceTests.Resolve_UnknownUser_ReturnsNull | Utilizador inexistente não tem acesso inventado | OK |  |
| AdminAuthorizationServiceTests.Resolve_WithCurrentTemplate_ResolvesTheTemplateCapabilities | Permissões do utilizador vêm exatamente da sua template | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminEmailRuleTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminEmailRuleTests.IsAdminEmail (Theory) | Email do ADMIN compara-se sem maiúsculas nem espaços | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Admin/AdminEmailSettingsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminEmailSettingsTests.Save_SoUmEmailComConta_OAtualOuODeUmUtilizador | Email ADMIN só pode ser de uma conta existente | OK |  |
| AdminEmailSettingsTests.TryClaim_EmptyField_ClaimsIt | Primeiro login por email reivindica o campo ADMIN vazio | OK |  |
| AdminEmailSettingsTests.TryClaim_FieldAlreadySet_NeverReplacesIt | Email ADMIN já gravado nunca é substituído | OK |  |
| AdminEmailSettingsTests.TryClaim_InvalidEmail_ClaimsNothing | Email inválido não reivindica o ADMIN | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Admin/ReporAcessoPrimeiroAAppTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReporAcessoPrimeiroAAppTests.AGravacaoNaAppFalha_APalavraPasseNaoMudaNoProvider | Repor acesso: se a app não grava, o provider não muda | OK |  |
| ReporAcessoPrimeiroAAppTests.OProviderNaoResponde_ASessaoAntigaJaCaiuEAMudancaEObrigatoria | Repor acesso: provider sem resposta fecha já as sessões | OK |  |
| ReporAcessoPrimeiroAAppTests.OProviderRecusa_OUtilizadorFicaComoEstava | Repor acesso: provider recusa e utilizador fica igual | OK |  |
| ReporAcessoPrimeiroAAppTests.ReporComSucesso_FechaAsSessoesEDevolveOPin | Repor acesso fecha sessões e devolve PIN temporário | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Admin/TemplateAdminServiceTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| TemplateAdminServiceTests.CreateAndEditTemplate_BothControlos_AreSaved | Template pode ter Controlo criar e aprovar juntos | OK |  |
| TemplateAdminServiceTests.CreateTemplate_InvariantViolations_AreTypedRefusals (Theory) | Template sem nome ou com permissão desconhecida é recusada sem gravar | OK | Os dois primeiros casos juntam nome vazio e permissão inválida (regra 4: um campo inválido de cada vez); o resultado esperado ainda distingue. |
| TemplateAdminServiceTests.CreateTemplate_MintedKeysAreUniqueAndWellFormed | Chaves de template geradas são únicas e bem formadas | FRACO | 25 chaves aleatórias de 8 hex quase nunca colidem: passa mesmo sem o re-mint; a unicidade real é do índice (Postgres). |
| TemplateAdminServiceTests.CreateTemplate_MintsStableKeyAndPersistsCanonicalCapabilities | Criar template gera chave própria e grava permissões sem repetidas | OK |  |
| TemplateAdminServiceTests.CreateTemplate_TheSameRequestAgain_OpensTheTemplateAlreadyCreated | Clique repetido em Criar template não cria duplicada | OK | Em memória; o índice único/concorrência está nos testes PostgreSQL CriarTemplate. |
| TemplateAdminServiceTests.CreateTemplate_ZeroCapabilities_IsAllowed | Template sem permissões é válida | OK |  |
| TemplateAdminServiceTests.DeleteTemplate_StillAssigned_RefusesWithTheUserCount | Template com utilizadores não se elimina e diz quantos | OK |  |
| TemplateAdminServiceTests.DeleteTemplate_Unassigned_RemovesTheRow | Template sem utilizadores pode ser eliminada | OK |  |
| TemplateAdminServiceTests.DeleteTemplate_UnknownKey_TypedRefusal | Eliminar template inexistente é recusado | OK |  |
| TemplateAdminServiceTests.EditTemplate_RenamesLabelEditsCapabilities_NeverRewritesTheStableKey | Editar template muda nome e permissões, nunca a chave | OK |  |
| TemplateAdminServiceTests.EditTemplate_UnknownStableKey_MissingNameOrNonBetaCapability_TypedRefusals | Editar com chave, nome ou permissão inválidos recusa e não grava | OK |  |
| TemplateAdminServiceTests.ListTemplates_EmptyPersistence_ReturnsEmptyList | Sem templates a lista vem vazia | OK |  |
| TemplateAdminServiceTests.ListTemplates_ProjectsPersistedFactsOrderedByStableKey | Lista de templates mostra nome e permissões, ordenada pela chave | OK |  |
| TemplateAdminServiceTests.ListTemplates_RenamedLabel_IsProjectedCurrentLabelWithStableKey | Template renomeada mostra o nome novo e a mesma chave | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServicePedidoCortadoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| UserAdminServicePedidoCortadoTests.Criar_OBrowserSaiDuranteOSupabase_OUtilizadorFicaCriado | Browser sai a meio de criar: utilizador fica criado, sem apagar | OK |  |
| UserAdminServicePedidoCortadoTests.Eliminar_OBrowserSaiDuranteOSupabase_OUtilizadorSaiDaLista | Browser sai a meio de eliminar: utilizador e identidade saem | OK |  |
| UserAdminServicePedidoCortadoTests.ReporAcesso_OBrowserSaiDuranteOSupabase_ASessaoAntigaCaiEOPinVolta | Browser sai a meio de repor: sessão antiga cai e PIN volta | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| UserAdminServiceTests.CreateDmoUser_DuplicateOperator_IsRefusedBeforeProvisioning | Nº de operador repetido é recusado antes de criar identidade | OK |  |
| UserAdminServiceTests.CreateDmoUser_EmailAlreadyAProviderIdentity_EmailExistsPersistsNothing | Email já existente no Supabase é recusado sem gravar utilizador | OK |  |
| UserAdminServiceTests.CreateDmoUser_EmailIsTheAdmins_NoIdentityCreatedInTheProvider | Email do ADMIN com maiúsculas e espaços também é recusado | DUPLICADO | Mesma regra do teste anterior (EmailIsTheAdmins_RefusedBeforeProvisioning), só muda o provider usado e as maiúsculas. |
| UserAdminServiceTests.CreateDmoUser_EmailIsTheAdmins_RefusedBeforeProvisioning_PersistsNothing | Email do ADMIN não pode ser usado num utilizador novo | OK |  |
| UserAdminServiceTests.CreateDmoUser_EmailOfAnotherUser_RefusedBeforeProvisioningWithOwner | Email de outro utilizador é recusado e diz de quem é | OK |  |
| UserAdminServiceTests.CreateDmoUser_InvariantViolations_AreTypedRefusalsBeforeAnyWrite (Theory) | Nome ou nº de operador inválido recusa sem gravar nada | OK |  |
| UserAdminServiceTests.CreateDmoUser_PersistenceFailureAfterProvisioning_PersistsNothing | Falha ao gravar utilizador não deixa utilizador nem atribuição | FRACO | O interceptor falha TODA a gravação, por isso Users vazio é trivial; não prova o tudo-ou-nada User+atribuição (isso é do Postgres TudoOuNada). |
| UserAdminServiceTests.CreateDmoUser_ProviderFailure_PersistsNothing | Falha do Supabase ao criar não deixa utilizador gravado | OK |  |
| UserAdminServiceTests.CreateDmoUser_SemEmail_CriaAContaQueEntraPeloNumero (Theory) | Email é opcional; conta sem email entra pelo número | OK |  |
| UserAdminServiceTests.CreateDmoUser_UnknownTemplateKey_IsRefusedBeforeProvisioning | Template inexistente ao criar utilizador é recusada sem gravar | OK |  |
| UserAdminServiceTests.CreateDmoUser_ValidCommand_ProvisionsPersistsAtomicallyAndReturnsTheBootstrapToken | Criar utilizador grava, atribui template e devolve PIN de 4 algarismos | OK | Comentário do teste ainda diz que o email não é gravado; hoje é gravado. |
| UserAdminServiceTests.CreateDmoUser_WithoutTemplate_PersistsUserWithoutAssignment | Utilizador pode nascer sem template | OK |  |
| UserAdminServiceTests.DeleteUser_AlreadyMissingAuthIdentity_IsRetryable | Eliminar com identidade já inexistente remove na mesma | OK |  |
| UserAdminServiceTests.DeleteUser_EmptyProviderUserId_RemovesTheRowWithoutCallingTheProvider | Utilizador sem identidade elimina-se sem chamar o Supabase | FRACO | Não verifica que o provider não foi chamado (o nome promete); o provider falso devolve falha por omissão, mas nada conta as chamadas. |
| UserAdminServiceTests.DeleteUser_ProviderFailure_KeepsDmoUserAndAssignment | Falha do Supabase ao eliminar mantém utilizador e template | OK |  |
| UserAdminServiceTests.DeleteUser_RemovesAuthAndAssignment_AllowsSameEmailAndNumberAgain | Eliminar utilizador liberta email e número para recriar | OK |  |
| UserAdminServiceTests.ListManagedUsersPage_ExcludesAdminEmailAndPagesInOperatorOrder | Lista exclui o ADMIN e pagina 25 por página | OK |  |
| UserAdminServiceTests.ListUsers_AdminEmail_FlaggedWithoutTemplateFacts | Utilizador com email do ADMIN aparece marcado e sem template | OK |  |
| UserAdminServiceTests.ListUsers_CurrentTemplateResolvesThroughTheRelationship_NotTheLegacyLabel | Template do utilizador vem da atribuição, não do rótulo antigo | OK |  |
| UserAdminServiceTests.ListUsers_EmptyPersistence_ReturnsEmptyList | Sem utilizadores a lista vem vazia | OK |  |
| UserAdminServiceTests.ListUsers_NoAssignment_CurrentTemplateIsNull | Utilizador sem atribuição aparece sem template | OK |  |
| UserAdminServiceTests.ListUsers_ProjectsApplicationFactsOrderedByOperatorId | Lista de utilizadores ordenada por nº de operador com zeros | OK |  |
| UserAdminServiceTests.ResetUserAccess_AdminAssociatedIdentity_AdminRefused | ADMIN nunca é alvo de reposição de acesso | OK |  |
| UserAdminServiceTests.ResetUserAccess_DoesNotChangeHistoryOrAccessConfiguration | Repor acesso não muda nome, template nem arquivo | OK |  |
| UserAdminServiceTests.ResetUserAccess_GeneratesNewTemporaryPasswordAndReArmsTheForcedChange | Repor acesso dá novo PIN e obriga a mudar | OK |  |
| UserAdminServiceTests.ResetUserAccess_MissingIdentityWithEmail_RecoversSameUserAndTemplate | Repor com email e identidade perdida recupera o mesmo utilizador | OK |  |
| UserAdminServiceTests.ResetUserAccess_MissingProviderIdentity_IdentityNotFoundLeavesUserUntouched | Repor sem email e sem identidade recusa e não altera | OK | Confirma ficha-utilizador.md (27/10): repor sem email e sem identidade continua a recusar. |
| UserAdminServiceTests.ResetUserAccess_ProviderFailure_TypedRefusalLeavesUserUntouched | Falha do Supabase ao repor deixa o utilizador intacto | OK |  |
| UserAdminServiceTests.ResetUserAccess_RecoveryCreateFailure_DoesNotRebindUser (Theory) | Falha ao recriar identidade não religa o utilizador | OK |  |
| UserAdminServiceTests.ResetUserAccess_RecoveryPersistenceFailure_DeletesOnlyNewIdentityAndKeepsOriginalUser | Falha ao gravar a recuperação apaga só a identidade nova | OK |  |
| UserAdminServiceTests.ResetUserAccess_UnknownOperator_UserNotFound | Repor acesso de nº inexistente é recusado | OK |  |
| UserAdminServiceTests.SetCurrentUserTemplate_AdminAssociatedIdentity_AdminRefusedNeverHoldsATemplate | ADMIN nunca recebe template | DUPLICADO | Mesma regra de AdminAuthorizationServiceTests.AuthorizeTemplateAssignment_AdminEmailUser_IsRefused, mas aqui pelo serviço de utilizadores (efeito gravado verificado). |
| UserAdminServiceTests.SetCurrentUserTemplate_AssignsByStableKeyAndReplacesTheSingleRelationRow | Mudar template substitui a atribuição, nunca fica com duas | OK | Em memória; o índice único por utilizador deve estar no Postgres (NadaRepetido). |
| UserAdminServiceTests.SetCurrentUserTemplate_NullKeyRemovesTheAssignment | Sem template remove a atribuição do utilizador | OK |  |
| UserAdminServiceTests.SetCurrentUserTemplate_UnknownUserOrTemplate_TypedRefusals | Atribuir com utilizador ou template inexistente é recusado | OK |  |
| UserAdminServiceTests.SetUserStandBy_AdminAssociatedIdentity_AdminRefused | ADMIN não pode ser arquivado | OK |  |
| UserAdminServiceTests.SetUserStandBy_TogglesAccessState_PreservingUserAndAssignment | Arquivar e reativar mantém utilizador e template | OK |  |
| UserAdminServiceTests.SetUserStandBy_UnknownOperator_UserNotFound | Arquivar nº inexistente é recusado | OK |  |
| UserAdminServiceTests.TemplateWithBothControlos_CanBeUsedForCreationAndAssignment | Template com os dois Controlos serve para criar e atribuir | DUPLICADO | Já coberto por CreateAndEditTemplate_BothControlos_AreSaved e Resolve_TemplateWithBothControlos_GrantsBoth. |

### `tests/DMO.Alpha.Infrastructure.Tests/Authentication/LocalAuthenticationProviderTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| LocalAuthenticationProviderTests.Apagar_TiraOAcesso | Apagar a conta tira o acesso | OK |  |
| LocalAuthenticationProviderTests.Cifra_TemSalDiferenteEVerificaSoACerta | Cifra tem sal próprio e só a palavra-passe certa verifica | OK |  |
| LocalAuthenticationProviderTests.CincoFalhasSeguidas_NaoBloqueiam_ACertaEntraLogo | Falhas seguidas nunca bloqueiam; a certa entra logo | OK | Regra mantida no código (sem bloqueio por tentativas); a decisão não consta da memória do projeto. |
| LocalAuthenticationProviderTests.Criar_EntraComATemporariaEPedeMudanca | Conta nova entra com a temporária e a palavra-passe fica cifrada | OK |  |
| LocalAuthenticationProviderTests.EmailRepetido_EhRecusado | Email repetido (sem maiúsculas) é recusado | OK |  |
| LocalAuthenticationProviderTests.Mudar_TiraATemporariaEAAntigaDeixaDeServir | Mudar palavra-passe tira a temporária e a antiga deixa de servir | OK |  |
| LocalAuthenticationProviderTests.PalavraPasseDoSupabase_Entra_EFicaCifradaNoFormatoDaApp | Palavra-passe vinda do Supabase entra e passa ao formato da app | OK |  |
| LocalAuthenticationProviderTests.PalavraPasseErrada_OuEmailDesconhecido_Falha | Palavra-passe errada ou email desconhecido não entra | OK |  |
| LocalAuthenticationProviderTests.Repor_VoltaATemporaria | Repor acesso volta à palavra-passe temporária | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/AutoriaMapTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AutoriaMapTests.CadaAutorGuardadoComoTexto_EstaNaLista | Todo autor guardado como texto está no mapa de autoria | OK |  |
| AutoriaMapTests.CadaIdentificador_TemIndiceEChaveParaUsersComSetNull | Cada autor tem índice e chave para Users com SET NULL | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| MigrationFoundationTests.Baseline_CreatesEveryModelTable_AndDownDropsThem | Baseline cria e desfaz todas as tabelas | OK |  |
| MigrationFoundationTests.Baseline_EncodesJobOnAndContextRelationships | Um contexto CM/MF/BQ por Job On, ligado a ferramenta | OK | Só lê as operações da migração, a execução real está nos testes PostgreSQL. |
| MigrationFoundationTests.Baseline_EncodesToolAndTraceAnchors | Ferramenta e registo de BQ: âncoras e índice parcial | OK |  |
| MigrationFoundationTests.Baseline_GeneratesValidPostgreSql | Baseline gera SQL com as tabelas e chaves | FRACO | Procura texto no SQL gerado, sem o executar; os testes PostgreSQL já aplicam a migração. |
| MigrationFoundationTests.Baseline_SeedsTheRemovedTool | Baseline cria a ferramenta REMOVED | OK |  |
| MigrationFoundationTests.CurrentModel_HasExactlyTheCanonicalTables | Modelo tem exatamente as tabelas esperadas | OK |  |
| MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt | Lista de migrações começa na Baseline e está completa | FRACO | Compara a lista de nomes das 58 migrações; obriga a editar o teste a cada migração nova e não prova nenhuma regra. |
| MigrationFoundationTests.PesoPerToolCombination_OnePesoPerControloForEachCmAndBq | Um Peso por controlo para cada CM e BQ | OK | Só lê a migração; a execução está em PesoPerToolCombination (PostgreSQL). |
| MigrationFoundationTests.Snapshot_MatchesCurrentModelExactly | Mudar o modelo exige migração nova | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AoMesmoTempo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.AoMesmoTempo_BqApagarUmaSaidaEnquantoOutroRegistaUmaEntrada_ADiscrepanciaContaComAsDuas | BQ: apagar saída e registar entrada juntos, discrepância certa | OK |  |
| PostgresMigrationIntegrationTests.AoMesmoTempo_BqCorrigirUmaSaidaEnquantoOutroRegistaUmaEntrada_ADiscrepanciaContaComAsDuas | BQ: corrigir saída e registar entrada juntos, discrepância certa | OK |  |
| PostgresMigrationIntegrationTests.AoMesmoTempo_Ferramenta_OutraPessoaGravaEntreALeituraEAEscrita_ERecusadoEFicaOQueElaGravou | Ficha da ferramenta: quem grava depois da outra pessoa é recusado | OK |  |
| PostgresMigrationIntegrationTests.AoMesmoTempo_InformacaoTecnica_OutraPessoaGravaEntreALeituraEAEscrita_ERecusadoEFicaOQueElaGravou | Informação técnica: duas pessoas ao mesmo tempo, a segunda é recusada | OK |  |
| PostgresMigrationIntegrationTests.AoMesmoTempo_JobOn_OutraPessoaGravaEntreALeituraEAEscrita_ERecusadoEFicaADataDelaSemLinhaEmOQueMudou | Job On: segunda gravação recusada, data da outra fica, sem linha em «O que mudou» | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Armazem.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Armazem_ABaseRecusaCodigoForaDaRegraEReparadorSemFamilia | Base recusa posição fora da regra e reparador sem CM nem MF | OK |  |
| PostgresMigrationIntegrationTests.Armazem_AMesmaPosicaoAoMesmoTempo_FicaUmaSo | Criar a mesma posição do Armazém ao mesmo tempo fica uma só | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemHistorico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Armazem_OHistoricoFiltraNoPostgreSql | Histórico do Armazém filtra corretamente no PostgreSQL | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMapaEstados.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ArmazemMapa_UltimoEstadoNaBase_IgualAoDeMemoria_ELinhasLidasNaoDependemDaHistoria | Mapa do Armazém mostra o último estado de cada ferramenta | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemMovimentos.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Armazem_ABaseRecusaUmMovimentoComOsDoisReparadores | Base recusa movimento com os dois reparadores | OK |  |
| PostgresMigrationIntegrationTests.Armazem_AMesmaFerramentaEntraAoMesmoTempo_FicaNumaPosicaoSo | Ferramenta que entra em duas posições juntas fica numa só | OK |  |
| PostgresMigrationIntegrationTests.Armazem_DuasSaidasDaMesmaFerramentaAoMesmoTempo_FicaUmaSo | Duas saídas da mesma ferramenta juntas gravam só uma | OK |  |
| PostgresMigrationIntegrationTests.Armazem_OMesmoEnvioAoMesmoTempo_GravaUmMovimento | Mesmo envio duas vezes grava um só movimento | OK |  |
| PostgresMigrationIntegrationTests.Armazem_ProcurarESaida_NoPostgreSql | Procura no Armazém e Saída propõem origem e estado certos | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemPar.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Armazem_ACmEOMfEntramJuntos_NoPostgreSql | CM e MF entram juntos no Armazém numa só transação | OK |  |
| PostgresMigrationIntegrationTests.Armazem_OParEOMfSozinhoAoMesmoTempo_OMfFicaNumaPosicaoSo | Par e MF sozinho ao mesmo tempo: MF fica numa posição | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemProximos.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Armazem_OsProximosJobOn_NoPostgreSql | Armazém mostra os próximos Job On da ferramenta | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ArmazemReparacao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Armazem_AReparacaoLeNoPostgreSql | Armazém lê a reparação das ferramentas no PostgreSQL | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AssociarAoMesmoTempo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Boquilhas_AssociarEnquantoOutraProducaoGravaOPrimeiroMovimento_CadaUmaFicaComOSeuRegisto | Associar enquanto outra produção grava o 1.º movimento: cada uma com o seu registo | OK |  |
| PostgresMigrationIntegrationTests.Boquilhas_AssociarOMesmoRegistoADuasProducoesAoMesmoTempo_FicaSoAPrimeira | Associar o mesmo registo de BQ a duas produções: fica só a primeira | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_AssociarAMesmaFolhaADuasProducoesAoMesmoTempo_FicaSoAPrimeira | Associar a mesma folha a duas produções: fica só a primeira | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_AssociarDuasFolhasAMesmaProducaoAoMesmoTempo_FicaSoAPrimeira | Associar duas folhas à mesma produção: fica só a primeira | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_AssociarUmaFolhaEnquantoSeGravaAPrimeiraLeitura_FicaUmaFolhaComALeitura | Associar folha enquanto se grava a 1.ª leitura: uma folha com a leitura | OK |  |
| PostgresMigrationIntegrationTests.Peso_AssociarOMesmoPesoADuasProducoesAoMesmoTempo_FicaSoAPrimeira | Associar o mesmo Peso a duas produções: fica só a primeira | OK |  |
| PostgresMigrationIntegrationTests.Puncoes_AssociarOMesmoRegistoADuasProducoesAoMesmoTempo_FicaSoAPrimeira | Associar o mesmo registo de Punções a duas produções: só a primeira | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AutorPorUtilizador.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.AutorPorUtilizador_EliminarOUtilizador_OFactoFicaComOTexto | Eliminar utilizador mantém os factos dele com o nome | OK |  |
| PostgresMigrationIntegrationTests.AutorPorUtilizador_UmIdentificadorSemUtilizador_ERecusado | Autor sem utilizador existente é recusado pela base | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasAssociacao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.BoquilhasAssociacao_CadaPedidoMantemOCustoEmConsultasDeLeitura | Registos de BQ e produção a associar sem consultas a mais | OK | Guarda o número de consultas, não uma regra de negócio; também verifica resultados. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasCorrente.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.BoquilhasCorrente_NaoTrazTodasAsSemDatasParaAMemoria | BQ corrente não carrega todas as produções sem datas | OK |  |
| PostgresMigrationIntegrationTests.BoquilhasCorrente_NoPostgres_EscolheAsMesmasProducoes | BQ corrente escolhe as mesmas produções que em memória | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasEscolhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.BoquilhasEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura | Escolhas de BQ lidas com uma consulta cada | OK | Guarda o número de consultas; também verifica resultados. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BoquilhasFicha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.BoquilhasFicha_CadaPedidoFazUmaSoConsultaDeLeitura | Ficha da BQ lida com uma consulta cada | OK | Guarda o número de consultas; também verifica resultados. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BqConcurrency.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.BqMovements_EntradasSimultaneas_RecalculamADiscrepancia | Duas entradas de BQ juntas dão discrepância −10 | OK |  |
| PostgresMigrationIntegrationTests.BqMovements_PrimeirosMovimentosDeDuasProducoesAoMesmoTempo_CadaUmaFicaComOSeuRegisto | Primeiros movimentos de duas produções: cada uma com o seu registo | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BqContextReplaced.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.SwappingTheBq_CreatesANewBqId_TheOldOneKeepsTheOldBq | Trocar a BQ cria novo bq_id; o antigo guarda a BQ antiga | OK |  |
| PostgresMigrationIntegrationTests.TrocarABqTresVezes_CadaUmaFicaNoSeuBqId_EVoltarAUmaRetomaOBqIdDela | Trocar a BQ três vezes: cada uma no seu bq_id, voltar retoma | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CmContextReplaced.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.SwappingTheCm_CreatesANewCmId_TheOldOneKeepsTheOldCm | Trocar a CM cria novo cm_id; o antigo guarda a CM antiga | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Concurrency.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Concurrency_CreateTheSameRepairerTwiceAtOnce_OneSucceeds_TheOtherIsATypedRefusal | Criar o mesmo reparador juntos: um grava, outro é recusado | OK |  |
| PostgresMigrationIntegrationTests.Concurrency_CreateTheSameToolTwiceAtOnce_KeepsOneTool | Criar a mesma ferramenta duas vezes juntas deixa uma só | OK |  |
| PostgresMigrationIntegrationTests.Concurrency_SaveTheSameComparisonThreeTimesAtOnce_KeepsOne | Guardar a mesma comparação três vezes juntas deixa uma | OK |  |
| PostgresMigrationIntegrationTests.Concurrency_StartingTheSameRegisterTwiceAtOnce_KeepsOneEach | Começar o mesmo registo (Punções, Peso, Pegamentos) juntos deixa um | OK |  |
| PostgresMigrationIntegrationTests.Concurrency_TwoFirstPegamentosReadingsAtOnce_ShareOneSheet | Duas primeiras leituras de Pegamentos juntas partilham uma folha | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ContextToolIdentity.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ContextToolIdentity_ContextoSemUmaSoProducaoOuRepetido_ABaseRecusa | Contexto sem produção, com as duas ou repetido é recusado | OK |  |
| PostgresMigrationIntegrationTests.ContextToolIdentity_MudarAFerramentaNoLugar_ABaseRecusa | Mudar a ferramenta de um bq_id no lugar é recusado pela base | OK |  |
| PostgresMigrationIntegrationTests.ContextToolIdentity_TrocarVoltarEApagarAsFerramentas_Passam | Trocar, voltar e apagar ferramentas mantém cm_id/mf_id/bq_id | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ControloAnterior.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ControloAnterior_PeloInicioEPelasMaquinasDaCm_NoPostgres | Controlo anterior encontrado pela referência e máquinas da CM | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CriarTemplate.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Concurrency_CreateTheSameTemplateTwiceAtOnce_KeepsOneTemplate | Criar a mesma template duas vezes juntas deixa uma | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.DuplicarFerramenta.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Concurrency_DuplicateAndCreateTheSameLotAtOnce_KeepsOneTool | Duplicar e criar o mesmo lote juntos deixa uma ferramenta | OK |  |
| PostgresMigrationIntegrationTests.Concurrency_DuplicateTheSameNewLotTwiceAtOnce_KeepsOneTool | Duplicar o mesmo lote novo duas vezes juntas deixa uma | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EdicoesAoMesmoTempo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.EdicoesAoMesmoTempo_Ferramenta_FactosETecnicaComAVersaoVelhaSaoRecusados | Ferramenta: factos e técnica com versão velha são recusados | OK |  |
| PostgresMigrationIntegrationTests.EdicoesAoMesmoTempo_JobOn_ASegundaGravacaoComAVersaoVelhaERecusada | Job On: gravar com a versão velha é recusado | OK |  |
| PostgresMigrationIntegrationTests.EdicoesAoMesmoTempo_JobOn_OMesmoEnvioDuasVezes_GravaUmaVezSemRecusa | Job On: duplo clique em Guardar grava uma vez sem recusa | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EliminarJobOn.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.EliminarJobOn_ApagaAProducaoInteira_ComAsChavesReais | Eliminar Job On apaga a produção inteira e deixa a outra | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EmailListas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.EmailLista_UmaGravacaoConcorrenteNaoApagaAAlteracaoDeOutraPessoa | Lista de email: gravação concorrente não apaga a alteração de outra pessoa | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EstadoDaAprovacao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.EstadoDaAprovacao_DuasDecisoesAoMesmoTempo_OResumoEODaUltima_NoPostgres | Duas decisões juntas: o resumo é o da última | OK |  |
| PostgresMigrationIntegrationTests.EstadoDaAprovacao_SoOPesoDecide_PegamentosDepoisNaoMudam_PesoNovoVoltaAosPendentes_NoPostgres | Só o Peso decide a aprovação; Peso novo volta a pendente | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FabricoSap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.FabricoSap_LerDuasVezesUmaSoLinha_NoPostgreSql | Ler o fabrico do SAP duas vezes atualiza a mesma linha | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.FerramentasRelacionadas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.FerramentasRelacionadas_OrdemEFiltro_NoPostgres | Job On novo: ferramentas relacionadas ordenadas e filtradas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoriaDaProducao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.HistoriaDeUmaProducao_TodosOsModulosVeemOMesmo | História de uma produção igual em todos os módulos | OK |  |
| PostgresMigrationIntegrationTests.JobOnAntigo_MostraQuemReparouCadaFerramenta | Job On antigo mostra quem reparou cada ferramenta | OK |  |
| PostgresMigrationIntegrationTests.JobOn_QuemReparouCadaFerramenta_PeloJobOnGuardadoNoMovimento | Quem reparou cada ferramenta vem do Job On guardado no movimento | OK |  |
| PostgresMigrationIntegrationTests.MudarOJobOn_NaoPerdeNadaDosOutrosModulos | Mudar o Job On não perde dados dos outros módulos | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoricoControlo.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.HistoricoDoControlo_ReferenciaDataEProducao_NoPostgres | Histórico do Controlo filtra por referência, data e produção | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.HistoricoLote.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.HistoricoFerramentas_ProducoesEmLote_IguaisAUmaAUma_ComConsultasFixas | Histórico da ferramenta em lote igual ao um a um | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.IntegrityRules.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Approval_ApproveAndNotApproveAtTheSameTime_LastDecisionWinsWhole | Aprovar e não aprovar juntos: a última decisão ganha inteira | OK |  |
| PostgresMigrationIntegrationTests.EnsureControlo_TwoOpeningsAtTheSameTime_BothGetTheSameControlo | Duas aberturas do controlo juntas ficam com o mesmo | OK |  |
| PostgresMigrationIntegrationTests.IntegrityRules_RefuseDuplicateMachineCode_AndContradictoryApproval | Base recusa máquina repetida e controlo aprovado e não aprovado | OK |  |
| PostgresMigrationIntegrationTests.PegamentosMeasurement_DeleteRenumbers_UnderTheUniquePosition | Apagar leitura de Pegamentos renumera sem repetir posição | OK |  |
| PostgresMigrationIntegrationTests.PesoMeasurement_TwoReadingsAtTheSameTime_GetDifferentPositions | Duas leituras de Peso juntas ficam em posições diferentes | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFicha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.JobOnFicha_CadaPedidoFazSoAsConsultasDeLeitura (Theory) | Ficha do Job On só lê, com consultas contadas | OK | Guarda o número de consultas, não regra de negócio. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnFolha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.JobOnFolha_GravarELerDeNovo_EApagaEmCascataComOJobOn | Folha do Job On grava, lê e apaga com o Job On | OK |  |
| PostgresMigrationIntegrationTests.JobOnFolha_UmaFolhaPorJobOn_ABaseRecusaASegunda | Uma só folha por Job On; a segunda é recusada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnHistorico.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.JobOnHistorico_CadaPedidoFazSoAsConsultasDeLeitura (Theory) | Histórico do Job On só lê, com consultas contadas | OK | Guarda o número de consultas. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnMaquina.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.JobOnMaquina_FazUmaSoConsultaDeLeitura | Máquina do Job On lida com uma consulta | OK | Guarda o número de consultas. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnPagina.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.JobOnPagina_CadaPedidoFazUmaSoConsultaDeLeitura | Consultas da página Job On custam uma cada | OK | Guarda o número de consultas. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnUmaConsulta.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.JobOn_CriarEEditar_VerificamAsTresFerramentasNumaSoConsulta | Criar e editar Job On verifica as 3 ferramentas numa consulta | OK | Guarda o número de consultas. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.LotesDaReferencia.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.LotesDaReferencia_SemLoteEmUltimo_NoPostgres | Lotes de uma referência do mais alto a «s/lote» | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.MachineRestrict.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.DeletingAMachineWithProductions_IsRefused_TheProductionStays | Máquina com produções não se elimina | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.MfContextReplaced.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.SwappingTheMf_CreatesANewMfId_TheOldOneKeepsTheOldMf | Trocar o MF cria novo mf_id; o antigo guarda o MF antigo | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.NadaRepetido.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Boquilhas_UmaBqDaProducaoTemUmSoRegistoDeReparacao_OSegundoERecusado | Uma BQ da produção tem um só registo de reparação | OK |  |
| PostgresMigrationIntegrationTests.EstadoDoDocumento_CadaAreaTemUmSoEstadoPorControlo_ORepetidoERecusado | Cada área tem um só estado por controlo | OK |  |
| PostgresMigrationIntegrationTests.EstadoDoDocumento_DoisPostosGravamAMesmaAreaAoMesmoTempo_FicaUmaLinhaComOUltimo | Dois postos gravam a mesma área juntos: uma linha, a última | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_CadaPosicaoDaFolhaTemUmaSoLeitura_ARepetidaERecusada | Cada posição da folha de Pegamentos tem uma leitura | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_DuasLeiturasGravadasAoMesmoTempo_FicamEmPosicoesDiferentes | Duas leituras de Pegamentos juntas ficam em posições diferentes | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_ToleranciaGravadaPelaPrimeiraVezEmDoisPostosAoMesmoTempo_FicaUmaSoLinha | Tolerância de Pegamentos gravada juntos fica numa linha | OK |  |
| PostgresMigrationIntegrationTests.PesoComparacao_CadaPosicaoTemUmaSoLeitura_ARepetidaERecusada | Cada posição da comparação de Peso tem uma leitura | OK |  |
| PostgresMigrationIntegrationTests.Producao_SoTemUmaFerramentaAtualDeCadaTipo_ASegundaBqCmOuMfERecusada (Theory) | Produção só tem uma BQ, CM e MF atual cada | OK |  |
| PostgresMigrationIntegrationTests.Puncoes_CadaPosicaoDoRegistoTemUmaSoPuncao_ARepetidaERecusada | Cada posição de Punções tem uma só punção | OK |  |
| PostgresMigrationIntegrationTests.Puncoes_DoisPostosComecamORegistoDaProducaoAoMesmoTempo_FicaUmSoRegisto | Dois postos começam Punções juntos: um só registo | OK |  |
| PostgresMigrationIntegrationTests.Puncoes_DuasPuncoesRegistadasAoMesmoTempo_FicamEmPosicoesDiferentes | Duas punções juntas ficam em posições diferentes | OK |  |
| PostgresMigrationIntegrationTests.Puncoes_UmSoRegistoPorProducao_OSegundoERecusado | Um só registo de Punções por produção | OK |  |
| PostgresMigrationIntegrationTests.RegistoDeVisao_DuasAbasAbremAFolhaAoMesmoTempo_FicaUmaSoVisitaDoOperador | Duas abas abertas juntas deixam uma só visita | OK |  |
| PostgresMigrationIntegrationTests.RegistoDeVisao_UmaLinhaPorOperadorEControlo_ARepetidaERecusada | Uma visita por operador e controlo | OK |  |
| PostgresMigrationIntegrationTests.Templates_DuasTemplatesNaoTemAMesmaChave_ARepetidaERecusada | Duas templates não têm a mesma chave | OK |  |
| PostgresMigrationIntegrationTests.Utilizadores_CadaUtilizadorTemUmaSoTemplateAtribuida_ASegundaERecusada | Cada utilizador tem uma só template atribuída | OK |  |
| PostgresMigrationIntegrationTests.Utilizadores_DoisUtilizadoresNaoTemOMesmoNumeroDeOperador_ORepetidoERecusado | Dois utilizadores não têm o mesmo nº de operador | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.OperacoesRegistadas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.OperacaoRegistada_DoisEnviosDiferentes_GravamOsDois | Dois envios diferentes gravam as duas operações | OK |  |
| PostgresMigrationIntegrationTests.OperacaoRegistada_OMesmoEnvioDuasVezes_GravaUmaSoVez | Mesmo envio duas vezes grava a operação uma só vez | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.Overlap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Overlap_FilteredInSql_NuncaAvisaEntreProducoesDaLinha | Sem aviso de sobreposição entre produções da linha | OK | Coerente com Diogo 25/10 (só BQ avisam), citado no teste. |
| PostgresMigrationIntegrationTests.Overlap_NoDiaDaTroca_ENoDiaAntes_NuncaAvisam | Produção que entra no dia em que outra sai não avisa | OK | Comentário do teste («a que entra no dia antes avisa») está desatualizado; o teste espera sem aviso, coerente com Diogo 25/10. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosCorrigir.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Pegamentos_DuasCorrecoesAoMesmoTempo_OHistoricoFicaEmCadeia | Duas correções de Pegamentos juntas: histórico em cadeia | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosEscolhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PegamentosEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura | Escolhas de Pegamentos lidas com uma consulta cada | OK | Guarda o número de consultas. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosFolhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Pegamentos_AssociarComSoAFolhaDeAntesDaTroca_FicaAFolhaDasFerramentasAtuais | Associar após troca fica com a folha das ferramentas atuais | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_DepoisDeTrocarABq_DuasPrimeirasLeiturasAoMesmoTempo_FicamNumaSoFolhaNova | Após trocar a BQ, duas primeiras leituras juntas: uma folha nova | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_SemMf_AsLeiturasFicamNaMesmaFolha_EAFolhaMostradaEADelas | Sem MF as leituras ficam na mesma folha | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosPerToolCombination.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Pegamentos_IndiceFiltrado_ApagarAsDuasCmsDoControlo_Passa | Apagar as duas CM do controlo não viola o índice | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_IndiceFiltrado_AssociarSemJobOnSemMf_AProducaoComFolhaAntigaSemMf_Passa | Folha sem Job On e sem MF associa à produção com folha antiga | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_IndiceFiltrado_SegundaFolhaDaMesmaCombinacao_Recusa | Segunda folha de Pegamentos da mesma combinação é recusada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PegamentosPosicoes.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Pegamentos_ComUmBuracoJaGravadoNasPosicoes_UmaLeituraNovaGrava | Com buraco já gravado, leitura nova grava na mesma | OK |  |
| PostgresMigrationIntegrationTests.Pegamentos_RegistarEApagarAoMesmoTempo_NaoDeixaBuracoNasPosicoes | Registar e apagar juntos não deixa buraco nas posições | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoComparacaoEstado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PesoComparacaoEstado_CadaLeituraSoLeSemAumentarConsultas (Theory) | Estado da comparação de Peso só lê, com consultas contadas | OK | Guarda o número de consultas. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoCorrigir.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PesoLeitura_CorrigirEApagarADoMeio_RenumeraEDeixaRasto | Apagar leitura do meio renumera e deixa rasto | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoEscolhas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PesoEscolhas_CadaPedidoFazUmaSoConsultaDeLeitura | Escolhas do Peso lidas com uma consulta cada | OK | Guarda o número de consultas. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoFolha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PesoFolha_CarregamentoSoLeSemAumentarConsultas (Theory) | Folha do Peso só lê, sem escrever nem mais consultas | OK | Guarda o número de consultas; verifica que não abre o Peso antigo implicitamente. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoPerToolCombination.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Peso_ApagarAsDuasCms_NaoViolaOIndice_EOAtualEOMaisAntigo | Apagar as duas CM não viola o índice; atual é o mais antigo | OK |  |
| PostgresMigrationIntegrationTests.Peso_CriarAoMesmoTempoDepoisDaTroca_FicaUmSoPesoNovo_EVoltarRetomaOAnterior | Peso criado juntos após troca: um só; voltar retoma o anterior | OK |  |
| PostgresMigrationIntegrationTests.Peso_IndiceFiltrado_DoisPesosMesmaProducaoCmDiferente_Aceita | Dois Pesos da mesma produção com CM diferente são aceites | OK |  |
| PostgresMigrationIntegrationTests.Peso_IndiceFiltrado_MesmaCombinacaoNaMesmaProducao_Recusa | Dois Pesos da mesma CM+BQ na produção são recusados | OK |  |
| PostgresMigrationIntegrationTests.Peso_Revisao_UmaLinhaPorControlo_MesmoComDoisPesos | Revisão mostra uma linha por controlo mesmo com dois Pesos | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoProducoes.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PesoProducoes_AnteriorEAComparar_NoPostgres | Peso: produção anterior e a comparar lidas no PostgreSQL | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesoUmEnvio.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PesoLeitura_DuasLeiturasIguaisDeEnviosDiferentes_GravamAsDuas | Duas leituras iguais de envios diferentes gravam as duas | OK |  |
| PostgresMigrationIntegrationTests.PesoLeitura_FalhaAMeioDaGravacao_NaoFicaNemALeituraNemAOperacao_EORepetirGrava | Falha a meio não deixa leitura nem operação; repetir grava | OK |  |
| PostgresMigrationIntegrationTests.PesoLeitura_OMesmoEnvioDuasVezesAoMesmoTempo_FicaUmaSoLeitura | Mesmo envio duas vezes juntas fica uma leitura | OK |  |
| PostgresMigrationIntegrationTests.PesoLeitura_OMesmoEnvioOutraVez_DepoisDeUmReinicio_NaoGravaDuasVezes | Mesmo envio de leitura de Peso após reinício não grava duas vezes | OK |  |
| PostgresMigrationIntegrationTests.PesoLeitura_OMesmoIdentificadorNoutraCoisa_ERecusadoENadaFica | Mesmo identificador de envio noutra coisa é recusado sem gravar | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PesosGuardadosPeloInicio.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PesosGuardados_AReferenciaPeloInicio_NoPostgres | Pesos guardados pesquisam a referência pelo início, nunca a meio | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.PostoRi.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.PostoRi_ListaSoQuemTemRiEPinPorNome_ComAsMesmasConsultasPara5Ou100Reparadores | Posto RI lista só quem tem RI e PIN, ordenado por nome | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProcuraParteBq.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ProcuraPelaParteBq_ComecaNaPrimeiraLetra_NoPostgres | Procura pela parte BQ começa na primeira letra | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProcuraPeloInicio.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ProcuraPeloInicio_TodasAsConsultas_NoPostgres | Procuras de referência começam pelo início | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProducoesDoDia.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ReparacaoInterna_ProducoesDoDia_NumaSoLeituraComOMesmoResultadoQueUmaPorUma | Produções do dia numa só leitura, resultado igual | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ProgramadaConcorrente.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.Programada_AdicionarEnquantoAUltimaFerramentaVolta_NaoAcrescentaAUmaListaFechada | Não se acrescenta a uma lista programada já fechada | OK |  |
| PostgresMigrationIntegrationTests.Programada_AsDuasUltimasFerramentasVoltamAoMesmoTempo_AListaFecha | Duas últimas ferramentas voltam juntas: a lista fecha | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.QuadroDoDia.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.JobOnQuadroDoDia_CadaPedidoFazSoAsConsultasDeLeitura (Theory) | Quadro do dia do Job On só lê, com consultas contadas | OK | Guarda o número de consultas; também verifica notas e próxima produção. |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.RemoverReparador.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.RemoverReparadorComSaidas_AsSaidasFicamComONome | Remover reparador mantém o nome nas saídas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.RemoverReparadorArmazem.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.RemoverReparadorUsadoNoArmazem_OMovimentoFicaComONome | Remover reparador do Armazém mantém o nome no movimento | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInterna.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ReparacaoInterna_ABaseRecusaContextoErradoENumeroComEspacos | Base recusa contexto errado e número com espaços | OK |  |
| PostgresMigrationIntegrationTests.ReparacaoInterna_EliminarOJobOn_LevaAsReparacoes | Eliminar o Job On leva as reparações internas | OK |  |
| PostgresMigrationIntegrationTests.ReparacaoInterna_OMesmoEnvioAoMesmoTempo_GravaUmaReparacao | Mesmo envio de reparação interna juntos grava uma | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaConsulta.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ReparacaoInternaConsulta_OsFiltrosTraduzemSeNaBase | Filtros da consulta de reparação interna funcionam no PostgreSQL | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaHora.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ReparacaoInternaHora_ORastoNaoMudaAsHoras | O rasto da reparação não muda as horas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta_ABaseAceitaAsDuasFormas_ERecusaAMistura | Reparação noutra ferramenta: base aceita duas formas, recusa mistura | OK |  |
| PostgresMigrationIntegrationTests.ReparacaoInternaOutraFerramenta_SemJobOn_AssociarEORasto_NaBaseReal | Reparação sem Job On: associar e rasto na base real | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ReparacaoInternaRasto.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ReparacaoInternaRasto_AnularAoMesmoTempo_AnulaUmaVez | Anular juntos anula uma só vez | OK |  |
| PostgresMigrationIntegrationTests.ReparacaoInternaRasto_EliminarOJobOn_LevaAsReparacoesEORasto | Eliminar Job On leva reparações e rasto | OK |  |
| PostgresMigrationIntegrationTests.ReparacaoInternaRasto_SoSeAcrescenta_EABaseRecusaUmaAcaoErrada | Rasto só se acrescenta; ação errada é recusada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapFabricoCadencia.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.SapFabricoCadencia_GravaSoACadencia_NoPostgreSql | Do relatório ZFMD do SAP só se guarda a cadência | OK | Confirma Diogo 27/10 (sap-fabrico-zfmd.md). |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.TipoAnterior.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.TipoAnterior_ContaAsProducoesDaBq_NoPostgres | Tipo anterior conta as produções da BQ | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ToolFks.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.CreateJobOn_TwoSubmitsAtTheSameTime_OneSucceeds_TheOtherIsATypedRefusal | Dois envios de Criar Job On juntos: um grava, outro recusado | OK |  |
| PostgresMigrationIntegrationTests.ToolFks_DeleteCmUsedInAPesoComparison_MovesTheComparisonToRemoved | Eliminar CM usada em comparação passa-a para REMOVED | OK |  |
| PostgresMigrationIntegrationTests.ToolFks_DeleteToolUsedInAProduction_MovesTheProductionToRemoved | Eliminar ferramenta usada passa a produção para REMOVED | OK |  |
| PostgresMigrationIntegrationTests.ToolFks_RefuseAToolIdThatDoesNotExist | Base recusa um tool_id que não existe | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.ToolsReferenciaMaiusculas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ToolsReferenciaMaiusculas_AsProcurasSemOlharAMaiusculasUsamOIndice | Procura de referência sem maiúsculas usa o índice | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.TresEstados.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.TresEstados_ADecisaoDeCadaParte_EOHistorico_NoPostgres | Decisão de cada parte e linha «Não aprovado» no Histórico | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.TrincoDoPeso.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.LeituraDoPeso_EsperaPeloTrincoDoCorrigirEApagar | Leitura nova do Peso espera pelo trinco de corrigir/apagar | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.TudoOuNada.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ApagarUmaFerramenta_SeAUltimaGravacaoFalhar_AFerramentaFicaEAProducaoContinuaComEla | Apagar ferramenta: se falhar, nada muda | OK |  |
| PostgresMigrationIntegrationTests.Aprovar_SeOResumoDoControloFalharAGravar_NenhumaDecisaoFicaEOControloContinuaPorAprovar | Aprovar: se o resumo falhar, nenhuma decisão fica | OK |  |
| PostgresMigrationIntegrationTests.EditarOJobOn_SeFalharDepoisDeGravar_OJobOnEAsFerramentasFicamComoEstavam | Editar Job On: se falhar depois de gravar, fica como estava | OK |  |
| PostgresMigrationIntegrationTests.TrocarABq_OsMovimentosEORegistoFicamNaBqEmQueForamRegistados | Trocar a BQ mantém movimentos e registo na BQ original | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.UsoSap.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.UsoSap_ImportarFechaOLembreteEUmSoAberto_NoPostgreSql | Importar % uso do SAP fecha o lembrete; só um aberto | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.UsoSapConsultas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.UsoSap_ImportarComAsMesmasConsultasPara5Ou100Ferramentas_NoPostgreSql | Importar uso do SAP: mesmas consultas para 5 ou 100 ferramentas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostgresMigrationIntegrationTests.BqMovementAudit_HandlersWriteWhoAndWhen | Corrigir e apagar movimento de BQ deixa quem e quando | OK | Apagar sem identidade grava «quem» em branco; confirmar se o Diogo quer sempre o autor. |
| PostgresMigrationIntegrationTests.ControloRevisaoQuery_DiaDoRegisto_ProducaoAnterior_E_SemData_NoPostgres | Revisão do Controlo: dia do registo (fuso de Lisboa), anterior e sem data | OK |  |
| PostgresMigrationIntegrationTests.Migrate_AppliesBaseline_AndCreatesTheCanonicalSchema | Migrações criam o esquema com os índices e chaves únicas certos | OK |  |
| PostgresMigrationIntegrationTests.OnlyDisposableTestDatabasesAreMigrated (Theory) | Só se migra uma base descartável com «test» no nome | OK |  |
| PostgresMigrationIntegrationTests.PartialUniqueIndex_EnforcesOneUnresolvedPerTool_AllowsMultipleAssociated | Um só registo de BQ por resolver por ferramenta | OK |  |
| PostgresMigrationIntegrationTests.ProcuraPorReferencia_AgainstPostgres_FiltersProductionsAndPesos | Procura por referência filtra produções e Pesos no PostgreSQL | OK |  |
| PostgresMigrationIntegrationTests.ToolLookupQuery_AgainstPostgres_RoundTripsProjectionAndToolType | Procura de ferramenta devolve o tipo e dados certos | OK |  |
| PostgresMigrationIntegrationTests.WritePath_AgainstPostgres_MatchesCanonicalModelSemantics | Registo de BQ: criar, movimentar, associar e recusar segundo bq_id | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/SqlDoDevPermissoesTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SqlDoDevPermissoesTests.CadaTabelaCriadaNoSqlDoDev_TemGrantAoUtilizadorDaApp | SQL manual do DEV dá permissões às tabelas criadas | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/UniqueViolationRetryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| UniqueViolationRetryTests.OtherFailures_AreNotRetried | Outros erros não repetem | OK |  |
| UniqueViolationRetryTests.SecondUniqueViolation_Propagates | Segunda violação de unicidade sobe o erro | OK |  |
| UniqueViolationRetryTests.UniqueViolation_RunsAgainOnce_WithAClearChangeTracker | Violação de unicidade repete uma vez com o rastreio limpo | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/Runtime/DmoRuntimeFormatTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DmoRuntimeFormatTests.NextValidLocal_KeepsAValidTime (Theory) | Hora válida (limite da mudança de hora) mantém-se | OK |  |
| DmoRuntimeFormatTests.NextValidLocal_MovesATimeInTheMarchGapToTheNextHour | Hora inexistente na mudança de março passa à seguinte | OK |  |
| DmoRuntimeFormatTests.Signed_RoundsBeforeChoosingTheSign (Theory) | Valor com sinal arredonda antes de escolher + ou − | OK |  |
| DmoRuntimeFormatTests.ToLocal_ShowsLisbonTimeForUtcAndOffsetInstants | Horas mostram-se sempre no fuso de Lisboa | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/SupabaseAuthenticationProviderIntegrationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SupabaseAuthenticationProviderIntegrationTests (6 SkippableFact: utilizador criado entra e muda PIN; admin entra; admin password errada; identidade existe/não existe; mudar password) | Contas reais do Supabase criam, entram e mudam palavra-passe | OK | Só correm com credenciais DMO_SUPABASE_* (saltam no CI); sem isso não guardam nada. |

### `tests/DMO.Alpha.Infrastructure.Tests/SupabaseAuthenticationProviderTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SupabaseAuthenticationProviderTests.OLoginEsperaNoMaximo15Segundos | Login espera no máximo 15 segundos | FRACO | Só compara uma constante com ela própria; não prova que o pedido é mesmo cortado ao fim do tempo. |
| SupabaseAuthenticationProviderTests.OServicoRecusaOuFalha_EServicoIndisponivel (Theory) | 429/500/503/401 é serviço indisponível, não credenciais erradas | OK |  |
| SupabaseAuthenticationProviderTests.PalavraPasseCerta_Entra | Palavra-passe certa entra e devolve a identidade | OK |  |
| SupabaseAuthenticationProviderTests.PalavraPasseErrada_SaoCredenciaisErradas (Theory) | 400/422 do Supabase é palavra-passe errada | OK |  |
| SupabaseAuthenticationProviderTests.SemConfiguracao_EServicoIndisponivel | Sem configuração do Supabase é serviço indisponível | OK |  |
| SupabaseAuthenticationProviderTests.SemLigacaoOuTempoEsgotado_EServicoIndisponivel | Sem ligação ou tempo esgotado é serviço indisponível | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/SupabasePasswordTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SupabasePasswordTests.LoginAndChange_WithAPin_SendThePrefixedPassword | Entrar e mudar com PIN envia a palavra-passe com prefixo | OK |  |
| SupabasePasswordTests.ToProvider_PrefixesOnlyPasswordsShorterThanSix (Theory) | PIN de 4 algarismos segue ao Supabase com prefixo; longas não | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/SupabaseUserProvisioningProviderTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SupabaseUserProvisioningProviderTests.CreateAndReset_FourDigitPin_GoesToSupabaseWithTheShortPasswordPrefix | PIN de 4 algarismos vai com prefixo ao criar e repor | OK |  |
| SupabaseUserProvisioningProviderTests.CreateAsync_DuplicateEmail422_IsTypedEmailExists | Email duplicado no Supabase é recusado como «já existe» | OK |  |
| SupabaseUserProvisioningProviderTests.CreateAsync_Other422OrServerError_IsTypedProviderFailure | Outros erros do Supabase ao criar são falha do serviço | OK |  |
| SupabaseUserProvisioningProviderTests.CreateAsync_Success_SendsTheProvenAdminPayload... | Criar conta envia ao Supabase email, PIN e mudança obrigatória | OK |  |
| SupabaseUserProvisioningProviderTests.CreateAsync_UnconfiguredInstallation_IsTypedProviderFailure | Sem configuração não se chama o Supabase ao criar | OK |  |
| SupabaseUserProvisioningProviderTests.DeleteAsync_ProviderResponsesAreTyped (Theory) | Eliminar: 404 e 500 do Supabase são respostas tipadas | OK |  |
| SupabaseUserProvisioningProviderTests.DeleteAsync_UsesCanonicalProviderIdAndSecretKey | Eliminar usa o id do Supabase e a chave secreta | OK |  |
| SupabaseUserProvisioningProviderTests.ResetAsync_404_IsTypedIdentityNotFound | Repor identidade inexistente (404) é recusado | OK |  |
| SupabaseUserProvisioningProviderTests.ResetAsync_ServerError_IsTypedProviderFailure | Erro do servidor ao repor é falha do serviço | OK |  |
| SupabaseUserProvisioningProviderTests.ResetAsync_Success_PutsNewTemporaryPasswordAndRearmsTheForcedChange | Repor envia nova temporária e rearma a mudança obrigatória | OK |  |

### `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesNavTabTests.Admin_SeestTheDefinicoesTab_EncostadoADireita | ADMIN vê o separador Definições à direita; barra Utilizadores e Templates | FRACO | Verifica o estilo inline «margin-inline-start:auto» (regra 10); a parte de links e redireções é útil. |
| DefinicoesNavTabTests.Anonymous_DoesNotSeeTheDefinicoesTab | Sem sessão não aparece o separador Definições | FRACO | Só «não aparece» na página de login, sem «aparece» antes. |
| DefinicoesNavTabTests.Definicoes_Admin_NaoTemOCartaoSap | Definições do ADMIN não mostram o cartão SAP antigo | FRACO | Só «não aparece» (DoesNotContain) sem um «aparece» na mesma zona; passa com a página vazia. |
| DefinicoesNavTabTests.Definicoes_UtilizadorNormal_NaoVeOCartaoSap | Utilizador normal não abre as Definições do Admin | OK |  |
| DefinicoesNavTabTests.NormalUser_DoesNotSeeTheDefinicoesTab | Operador vê o seu módulo mas não o separador Admin | OK |  |

### `tests/DMO.Alpha.Web.Tests/Admin/RegistosPorUtilizadorTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| RegistosPorUtilizadorTests.Registos_AbremVazios_EFiltramPorUtilizadorEModulo | Registos do Admin abrem vazios e filtram por utilizador e módulo | OK |  |
| RegistosPorUtilizadorTests.Registos_SoOAdminEntra | Só o ADMIN abre os Registos | OK |  |

### `tests/DMO.Alpha.Web.Tests/AdminAuthorizationPolicyTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminAuthorizationPolicyTests.AdminFoundationServices_RegisterWithScopedLifetime | Serviços de Admin registados por pedido | FRACO | Testa só o registo de dependências (tipo e tempo de vida), não uma regra do negócio. |
| AdminAuthorizationPolicyTests.AdminPages_AdminEmailSettingRemoved_RevokesAdminCookie | Tirar o email do ADMIN anula a sessão ADMIN em curso | OK |  |
| AdminAuthorizationPolicyTests.AdminPages_ConfiguredInstallation_AdminSessionIsAllowed | ADMIN entra nas páginas Admin; Setup removido dá 404 | OK |  |
| AdminAuthorizationPolicyTests.AdminPages_ConfiguredInstallation_UnauthenticatedAndOperatorAreDenied | Anónimo e operador são mandados para o login nas páginas Admin | OK |  |
| AdminAuthorizationPolicyTests.AdminPages_UnconfiguredInstallation_DoesNotAllowAnonymous | Sem instalação configurada o Admin não abre a anónimos | DUPLICADO | Mesma recusa a anónimos de AdminPages_ConfiguredInstallation_UnauthenticatedAndOperatorAreDenied. |
| AdminAuthorizationPolicyTests.RequireAdminPolicy_PrincipalWithoutAdminClaim_IsDenied | Só quem tem a marca ADMIN passa a política de Admin | OK |  |
| AdminAuthorizationPolicyTests.RequireAdminPolicy_UnconfiguredInstallation_DeniesAnonymous | Anónimo nunca passa a política de Admin | DUPLICADO | O caso anónimo já está em RequireAdminPolicy_PrincipalWithoutAdminClaim_IsDenied. |

### `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.CliquesRepetidos.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminUsersAndTemplatesPagesTests.AdminTemplates_CriarOutraVezDepoisDeUmaFalha_AbreATemplateJaCriada | Clicar duas vezes em Criar template não cria duas | OK | Forma o POST à mão (BuildFormPost) e não com EnvioDoFormulario (regra 9). |

### `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.Gestao.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminUsersAndTemplatesPagesTests.AdminTemplates_FerramentasEArmazem_NaSeccaoGestao | Ferramentas e Armazém aparecem juntos na secção Gestão | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminTemplates_SemCaixas_EscolhidoEscuroComLetraBranca | Permissões da template sem caixas, escolhido escuro com letra branca | FRACO | Compara texto de regras CSS (opacity, background, color) em vez do efeito; a regra de aspeto deve estar na guarda do design (regra 10). |

### `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.NovaTemplate.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminUsersAndTemplatesPagesTests.AdminTemplates_CriarDuasSeguidas_FicamDuas_APrimeiraNaoMuda | Criar duas templates seguidas deixa as duas, a primeira intacta | OK | Forma o POST à mão (regra 9). |
| AdminUsersAndTemplatesPagesTests.AdminTemplates_GuardarAAberta_AtualizaEssa | Guardar a template aberta atualiza essa e não cria outra | OK |  |

### `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AdminUsersAndTemplatesPagesTests.AdminTemplates_CreateAndEdit_KeyMintedDetailAnchorLabelMutable | Criar e editar template: chave fixa, nome mutável, aviso de cria e aprova | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminTemplates_Delete_OnlyWhenNoUserHasIt_FromTheOptionsDialog | Template só se elimina sem utilizadores, pela janela das Opções | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminTemplates_UsersLikeModules_AssociateWithoutTemplate_RemoveExisting_LeaveOthers | Utilizadores da template escolhem-se como módulos; quem tem outra fica | OK | Confirma templates-utilizadores.md (Diogo 26/10). |
| AdminUsersAndTemplatesPagesTests.AdminTemplates_ZeroCapabilitiesAllowed_NonBetaVocabularyRefused | Template sem módulos é válida; módulo desconhecido é recusado | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsersAndTemplatesPages_AreGatedByTheRequireAdminFolderPolicy | Utilizadores e Templates só abrem ao ADMIN | DUPLICADO | Mesma recusa a anónimo e operador de AdminPages_ConfiguredInstallation_UnauthenticatedAndOperatorAreDenied (só muda as rotas). |
| AdminUsersAndTemplatesPagesTests.AdminUsers_AdminAssociatedRow_HiddenFromList_BoundaryStillRendersInFicha | Linha do ADMIN não aparece na lista; ficha diz «não é gerido aqui» | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_CreateRefusals_RenderTypedMessagesViaPRG | Criar utilizador inválido ou repetido mostra a recusa e não grava | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_CreateUserViaApp_FirstLoginForcesPrivateChange_ThenNormalAccess | Utilizador novo: 1.º login força mudar o PIN, depois acesso normal | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_CreateUser_BootstrapPairShowsOnceOnly | Email e PIN temporário só aparecem uma vez | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_CreatedOperator_LoginByNumberWithLeadingZeros_ChangesPassword_AndRelogsByBothIdentifiers | Entrar pelo nº com zeros à esquerda e também pelo email | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_FichaActions_SaveAndStayOnTheFicha_ChangingOnlyTheirZones | Acções da ficha voltam à ficha e só mudam a sua zona | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_FichaOfUnknownOperator_ShowsTheCleanWarning_NoSheetInvented | Ficha de nº inexistente avisa e não inventa ficha | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_FichaShowsHumanFacts_AndTheActionsMovedFromTheTablePostToTheSameIndexHandlers | Ficha mostra nome, template, estado, PIN; Arquivar/Repor/Eliminar nas Opções | OK | Sem email na ficha confirma ficha-utilizador.md; acções em Opções no fim confirma «Eliminar/Opções em último»; verifica algumas classes (dmo-facts). |
| AdminUsersAndTemplatesPagesTests.AdminUsers_ListCount_ShowsTheTotal_NotJustTheCurrentPage | Contagem mostra o total (30) e não só a página (25) | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_ListShowsApplicationFacts_StandByAndTemplateEditing_ThroughTheSingleRelation | Lista mostra nome, nº, template e estado; arquivar e mudar template | OK | Lista ordenada por nº crescente (não «último em cima»); confirmar com o Diogo se vale para esta lista. |
| AdminUsersAndTemplatesPagesTests.AdminUsers_ResetAccess_OldCredentialFails_NewTemporaryForcesChangeThenNormal | Repor acesso: PIN velho falha, o novo força mudança | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_ResetMissingIdentity_RecoversSameUserAndOpensOperationalPages | Repor acesso com identidade perdida recupera o mesmo utilizador | OK |  |
| AdminUsersAndTemplatesPagesTests.AdminUsers_TableHasZeroButtons_RowSelectsInvertedAndDoubleClickOpensTheFicha | Tabela de utilizadores sem botões; duplo clique abre a ficha | FRACO | Mistura regra útil (sem botões em linhas, Diogo) com «não contém .dmu-row-select» e atributos data-dmo-open; o nome promete linha invertida, que não verifica. |
| AdminUsersAndTemplatesPagesTests.Admin_DefinicoesPlaceholdersAreLinked_AndNewPagesAreDressedWithTheAdminShell | Barra do Admin liga Utilizadores e Templates; Setup não existe | FRACO | Muitos «não aparece» (Setup, Canal Jobon, dmo-secondary-nav) e comprova a classe «active»; o Admin passou para Definições › Admin, estrutura já mudada. |
| AdminUsersAndTemplatesPagesTests.DeleteUser_TwoStepConfirmation_RemovesTheRow_AdminNever | Eliminar utilizador pede confirmação; ADMIN nunca se elimina | OK |  |
| AdminUsersAndTemplatesPagesTests.Login_SeedOperatorPathStillWorks_UnattributedEmailKeepsTheTypedRefusal | Login por nº funciona; email sem utilizador diz «sem acesso» | OK |  |

### `tests/DMO.Alpha.Web.Tests/AppVersionTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AppVersionTests.LoginBadge_LinksToVersion_AndVersionPageListsFixesAndPendingWork | Página /versao lista correções recentes e pendentes | OK | Confirma notas de versão em «fixes» e só P02/P03 em pendentes (MEMORY.md). |
| AppVersionTests.Metadata_IdentifiesActualArtifact_WithoutLoginOrDatabase_AndCannotBeRenamedByRuntimeSettings | Metadata da versão sem login, com versão e revisão certas | OK |  |
| AppVersionTests.VersionPage_PagesTheFixes_TenPerPage_NewestFirst | Correções paginadas, dez por página, a mais recente primeiro | OK | Confirma «último registo em cima». |

### `tests/DMO.Alpha.Web.Tests/AuthenticationTests.LoginLocal.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AuthenticationTests.LoginLocal_RepeatedFailures_WarnButNeverLockTheUser | Login próprio: falhas seguidas avisam mas nunca bloqueiam | OK |  |

### `tests/DMO.Alpha.Web.Tests/AuthenticationTests.PalavraPasseAtual.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AuthenticationTests.ChangePassword_WithoutTemporaryPassword_AsksForTheCurrentOne | Mudar palavra-passe sem ser temporária pede a atual | OK |  |

### `tests/DMO.Alpha.Web.Tests/AuthenticationTests.PedidoCortado.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AuthenticationTests.AdminPasswordChange_ClientLeavesAfterTheProviderChange_TheOtherAdminSessionsStillEnd | Browser sai ao mudar a palavra-passe do ADMIN: outras sessões acabam | OK |  |

### `tests/DMO.Alpha.Web.Tests/AuthenticationTests.ServicoIndisponivel.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AuthenticationTests.Login_EmSupabase_OPedidoEsperaNoMaximo15Segundos | Login no Supabase espera no máximo 15 segundos | DUPLICADO | Mesma constante de SupabaseAuthenticationProviderTests.OLoginEsperaNoMaximo15Segundos; só lê http.Timeout. |
| AuthenticationTests.Login_ServicoDeAutenticacaoSemResposta_DizQueOServicoNaoRespondeu | Serviço de login sem resposta diz que não respondeu | OK |  |

### `tests/DMO.Alpha.Web.Tests/AuthenticationTests.Tentativas.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AuthenticationTests.Login_CincoPalavrasPasseErradas_MandaConsultarOAdmin | Na 5.ª palavra-passe errada manda consultar o ADMIN | DUPLICADO | Já coberto por UserLogin_RepeatedFailures_NeverLockTheUser (i < 5 vs ≥ 5). |

### `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| AuthenticationTests.AdminBootstrap_EmptySetting_FirstEmailLoginBecomesAdmin | Campo ADMIN vazio: 1.º login por email fica ADMIN | OK |  |
| AuthenticationTests.AdminBootstrap_EmptySetting_UserEmailLoginDoesNotClaimAdmin | Email de utilizador nunca reivindica o ADMIN vazio | OK |  |
| AuthenticationTests.AdminCookie_EmailChangedRejected_And_OperatorCookieUnaffected | Mudar o email do ADMIN anula a sessão dele, não a dos operadores | OK |  |
| AuthenticationTests.AdminHierarchy_AfterActivation_RequiresAdmin | Rotas /Admin só para ADMIN; Setup removido dá 404 | DUPLICADO | Mesma recusa/entrada de AdminPages_ConfiguredInstallation_* (AdminAuthorizationPolicyTests). |
| AuthenticationTests.AdminLogin_EmailChangedInDefinicoes_OldEmailIsNoLongerAdmin | Email antigo do ADMIN deixa de ser ADMIN | DUPLICADO | Mesma regra de AdminLogin_OnlyTheDefinicoesEmailBecomesAdmin (email que autentica mas não é o do campo). |
| AuthenticationTests.AdminLogin_Failure_NotAssociated | Email que entra mas não é ADMIN nem utilizador é recusado | OK |  |
| AuthenticationTests.AdminLogin_Failure_WrongPassword | ADMIN com palavra-passe errada vê «Credenciais inválidas» | OK |  |
| AuthenticationTests.AdminLogin_NeedsOnlyTheEmailSetting | ADMIN entra só com o email das Definições | OK |  |
| AuthenticationTests.AdminLogin_OnlyTheDefinicoesEmailBecomesAdmin | Só o email das Definições entra como ADMIN | OK |  |
| AuthenticationTests.AdminLogin_Success_RedirectsToAdminArea | ADMIN entra sempre na sua área Admin | OK |  |
| AuthenticationTests.AdminPasswordChange_EndsTheOtherAdminSessions | Mudar a palavra-passe do ADMIN fecha as outras sessões | OK |  |
| AuthenticationTests.AfterSair_TheBrowserDoesNotKeepThePages_SoBackShowsNothing | Páginas vão com no-store; Voltar após Sair não mostra nada | OK |  |
| AuthenticationTests.AuthenticatedActorContext_ExposesProfileAndAdminFlag | Cabeçalho mostra nome e «Perfil: Operador» | OK |  |
| AuthenticationTests.ForcedPasswordChange_FourDigitPin_IsAccepted | PIN de 4 algarismos é aceite | OK |  |
| AuthenticationTests.ForcedPasswordChange_ProviderTemporaryPassword_CanBeCleared | Palavra-passe temporária do provider pode ser mudada | DUPLICADO | Mesmo fluxo de ForcedPasswordChange_UserSetsNewPassword_CanAccessApp, só muda a origem da marca de temporária. |
| AuthenticationTests.ForcedPasswordChange_SecondSubmitOfSameForm_LandsInAppInsteadOfReopeningForm | Duplo clique em Guardar da mudança entra na app | OK |  |
| AuthenticationTests.ForcedPasswordChange_StaleFormAfterLogin_ReopensAndAllowsFreshSubmission | Formulário expirado reabre e deixa enviar de novo | OK |  |
| AuthenticationTests.ForcedPasswordChange_TooShortPassword_ShowsExactRequirement_WithoutChangingPassword | PIN curto demais é recusado e não muda | OK |  |
| AuthenticationTests.ForcedPasswordChange_UserRequiresChange_BlocksOperationalPages | Mudança obrigatória bloqueia as páginas operacionais | OK |  |
| AuthenticationTests.ForcedPasswordChange_UserSetsNewPassword_CanAccessApp | Depois de mudar a palavra-passe o utilizador entra na app | OK |  |
| AuthenticationTests.Login_BehindTheHttpsProxy_SetsTheSessionCookieAsSecure | Atrás do proxy HTTPS o cookie da sessão é Secure | OK |  |
| AuthenticationTests.Login_InASecondTab_AfterTheFirstOneEntered_GoesIn_InsteadOfError400 | Login em segundo separador entra em vez de erro 400 | OK |  |
| AuthenticationTests.Login_Page_HasNoRecoveryNotice | Login não anuncia a recuperação de acesso ADMIN | FRACO | Só «não aparece» sem «aparece» antes. |
| AuthenticationTests.Login_WithoutAValidToken_ShowsTheLoginAgain_InsteadOfError400 | Login sem token válido mostra a página de novo | OK |  |
| AuthenticationTests.Logout_ClearsSession | Sair termina a sessão e vai ao login | OK |  |
| AuthenticationTests.ModuleRoute_Home_IsReachableWhenAuthenticated | Início abre a quem tem sessão | OK |  |
| AuthenticationTests.OperatorCookie_DeletedOrStandBy_IsRejected (Theory) | Utilizador eliminado ou arquivado perde a sessão | OK |  |
| AuthenticationTests.OperatorLogin_EntersAreaOfAssignedTemplate (Theory) | Operador aterra no módulo da sua template | OK |  |
| AuthenticationTests.ResetPassword_OldSessionCookie_StaysRevokedAfterNewPasswordIsChosen | Sessão antiga fica anulada depois de repor e mudar | OK |  |
| AuthenticationTests.ShellUserMenu_ContainsChangePasswordLink_DefinicoesForAdminOnly | Cabeçalho só tem nome, versão e Sair; sem atalho de Definições/Mudar palavra-passe | OK |  |
| AuthenticationTests.Unauthenticated_DeepLink_RedirectsToLoginWithReturnUrl | Sem sessão, ligação direta vai ao login com retorno | OK |  |
| AuthenticationTests.UserEmailLogin_IgnoresSpacesAndCapitals (Theory) | Email do utilizador entra sem maiúsculas nem espaços | OK |  |
| AuthenticationTests.UserLogin_Failure_StandBy | Conta arquivada diz que está arquivada | OK |  |
| AuthenticationTests.UserLogin_Failure_UnknownOperator | Nº desconhecido não revela que operadores existem | OK |  |
| AuthenticationTests.UserLogin_Failure_WrongPassword | Palavra-passe errada mostra «Credenciais inválidas» | OK |  |
| AuthenticationTests.UserLogin_PreservesLeadingZeros | Nº de operador com zeros à esquerda entra | OK |  |
| AuthenticationTests.UserLogin_RepeatedFailures_NeverLockTheUser | Falhas seguidas nunca bloqueiam; a certa entra | OK |  |
| AuthenticationTests.UserLogin_Success_RedirectsToHome | Utilizador entra e vai ao início sem módulos | OK |  |

### `tests/DMO.Alpha.Web.Tests/CabecalhosDeSegurancaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| CabecalhosDeSegurancaTests.Resposta_LevaOsCabecalhosDeSeguranca (Theory) | Respostas levam cabeçalhos de segurança, também nos estáticos | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesNumSoSitioTests.AFaixaEmDesenvolvimento_SoNosModulosEmDesenvolvimento | Faixa «em desenvolvimento» só nos módulos em desenvolvimento | OK |  |
| DefinicoesNumSoSitioTests.APalavraPasse_ComAAtualErrada_AvisaENaoMudaNada | Palavra-passe atual errada avisa e não muda | OK |  |
| DefinicoesNumSoSitioTests.APalavraPasse_MudaComAAtualCerta | Mudar palavra-passe nas Definições com a atual certa | OK |  |
| DefinicoesNumSoSitioTests.ASDefinicoes_EstaoNaBarraDeCima_EJaNaoNaBarraDosModulos | Definições estão na barra de cima, não na dos módulos | OK |  |
| DefinicoesNumSoSitioTests.Armazem_AtrasDoMesmoCadeado_ESemTextoComAListaVazia | Reparadores do Armazém ficam atrás do mesmo cadeado | OK |  |
| DefinicoesNumSoSitioTests.AsPaginasAntigas_AbremAEntradaDoModulo (Theory) | Páginas de definições antigas levam à nova entrada | OK |  |
| DefinicoesNumSoSitioTests.AsPastas_SaoGerais_SoAsAcoesFicamParaTodos | Pastas ficam em Este computador; só as ações ficam livres | OK |  |
| DefinicoesNumSoSitioTests.ComOPin_OGeralEditaSe_SemCadeado_EVoltaAEntrada | Com PIN as Gerais editam-se e voltam à entrada | OK |  |
| DefinicoesNumSoSitioTests.Controlo_TudoNumaSoVista_UmBotaoPorCartao | Definições do Controlo numa só vista, um botão por cartão | OK |  |
| DefinicoesNumSoSitioTests.OAdmin_CriaOPinNasDefinicoesApp_SoOHashFicaGuardado | ADMIN cria o PIN das Definições; só o hash fica guardado | OK |  |
| DefinicoesNumSoSitioTests.OAdmin_MudaOSap_EOsScriptsDaPastaLevamAsDefinicoes | ADMIN guarda o SAP e os scripts levam esses valores | OK |  |
| DefinicoesNumSoSitioTests.OAdmin_SoVeAsDefinicoesDoAdmin_ENaoDosModulos | ADMIN só vê as definições do Admin | OK |  |
| DefinicoesNumSoSitioTests.OCadeado_AbreComOPin_AteSair | Cadeado abre com o PIN até sair | OK |  |
| DefinicoesNumSoSitioTests.OCadeado_Depois4PinsErrados_OCertoAindaAbre | Com 4 PIN errados o certo ainda abre | OK |  |
| DefinicoesNumSoSitioTests.OCadeado_DepoisDe5PinsErrados_Espera | Após 5 PIN errados nem o certo abre por um tempo | OK |  |
| DefinicoesNumSoSitioTests.OCadeado_SemPin_PedeAoAdminParaOCriar | Sem PIN das Definições manda pedir ao ADMIN | OK |  |
| DefinicoesNumSoSitioTests.OModoEscuro_GuardaSeSoParaQuemOMuda | Modo escuro guarda-se só para quem o muda | OK |  |
| DefinicoesNumSoSitioTests.OsSeparadores_AMinhaContaEsteComputadorEGerais_CadaEntradaMostraSoASuaSeccao | Três separadores; cada entrada mostra só a sua secção | OK | Confirma definicoes-estrutura.md. |
| DefinicoesNumSoSitioTests.Pessoal_SoTemAContaAPalavraPasseEAAparencia | A minha conta: conta, aparência e palavra-passe; módulos com acesso | FRACO | Grande parte compara markup de posição (botão no canto, classe dmo-card-topo-acoes, tabela exata) em vez do efeito. |
| DefinicoesNumSoSitioTests.PuNnpb_UmGuardarParaB1EC3_AsOutrasLinhasFicamComoEstao | PU NNPB: Guardar muda só B1 e C3 | OK |  |
| DefinicoesNumSoSitioTests.SemAPermissao_OGeralLeSeComCadeado_ENaoGrava | Sem PIN as Gerais só se leem e nada grava | OK |  |
| DefinicoesNumSoSitioTests.UmModuloInativo_NaoTemSeccao | Módulo sem acesso não tem secção nas Definições | OK |  |
| DefinicoesNumSoSitioTests.UmSoCadeado_PorCimaDasPastasEDasGerais_QuandoSaoSoDeLeitura | Um só aviso «Abrir com PIN» em Este computador e Gerais | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesTrocaTests.E10_BoquilhasDefinicoes_ReparadoresERotasEmZonas_UmaMensagemNoCanto | Reparadores e rotas das Boquilhas em zonas; uma só mensagem | OK |  |
| DefinicoesTrocaTests.E10_CabecalhoDeCartaoComAcoes_NoDesignSystem | Cabeçalho de cartão com ações existe no CSS | FRACO | Só procura texto de classes no ficheiro CSS (nem o efeito). |
| DefinicoesTrocaTests.E10_ControloDefinicoes_ToleranciaNumaZona_APastaDosPdfsFica | Gravar a tolerância muda só a sua zona; a pasta dos PDFs fica | OK |  |
| DefinicoesTrocaTests.E10_FerramentasDefinicoes_QuadroNumaZona_OAvisoVaiParaOCanto | Quadro de totais numa zona; aviso de recusa parcial vai ao canto | OK |  |
| DefinicoesTrocaTests.E10_SetupEReparacao_CartaoNumaZona_MensagemNoCanto | Email do ADMIN e hora de viragem gravam numa zona com mensagem | OK |  |
| DefinicoesTrocaTests.E10_Templates_ListaEDetalheEmZonas_OFocoDepoisDeCriarEEliminar | Templates: lista e detalhe em zonas; foco após criar e eliminar | OK |  |
| DefinicoesTrocaTests.E10_Utilizadores_CriarTrocaAsZonas_OPinFicaNoCanto | Criar utilizador troca só as zonas; PIN fica no canto | OK |  |

### `tests/DMO.Alpha.Web.Tests/DmoNumberFormatTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DmoNumberFormatTests.Decimal2_SemZerosNumInteiro_DuasCasasComDecimais (Theory) | Inteiro sem «.00»; com decimais duas casas | OK |  |
| DmoNumberFormatTests.FolhaTexto_PontoEDuasCasas_TambemDentroDoTexto (Theory) | Folha do Job On com ponto e duas casas, também no texto | OK |  |

### `tests/DMO.Alpha.Web.Tests/EnvioDoFormularioTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| EnvioDoFormularioTests.OBotaoCarregadoLevaONameEValue_EOFormaction | Ajudante de testes: botão carregado leva name, value e formaction | OK |  |
| EnvioDoFormularioTests.ORadioEscolhidoPeloNameEValue_TiraOQueVinhaMarcado | Ajudante de testes: rádio escolhido substitui o marcado | OK |  |
| EnvioDoFormularioTests.OsCamposForaDoFormularioComOFormDeleVao_OsDeDentroDeOutroNao | Ajudante de testes: campos fora do formulário vão pelo atributo form | OK |  |
| EnvioDoFormularioTests.UmCampoForaDoFormulario_EscolheOFormularioPeloForm | Ajudante de testes: formulário escolhido pelo atributo form | OK |  |

### `tests/DMO.Alpha.Web.Tests/ErrorPageTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ErrorPageTests.ErrorPage_DoesNotRedirectWhenPasswordChangeIsRequired | Página de erro não redireciona se a mudança de palavra-passe é obrigatória | OK |  |
| ErrorPageTests.ErrorPage_IsAnonymous_Uncached_AndIndependentOfDatabase (Theory) | Página de erro abre sem sessão nem base e sem cache | OK |  |
| ErrorPageTests.UnhandledLoginException_RendersSafeErrorPage_InsteadOfMissingEndpoint | Erro no login mostra página segura sem detalhes técnicos | OK |  |

### `tests/DMO.Alpha.Web.Tests/FicheirosComVersaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FicheirosComVersaoTests.CadaFolhaEScriptDasPaginas_LevaAMarcaDeVersao | Todas as folhas e scripts levam marca de versão | OK |  |
| FicheirosComVersaoTests.OLogin_PedeCadaFolhaComAMarcaDeVersao | O login pede cada folha com marca de versão | OK |  |
| FicheirosComVersaoTests.UmFicheiroComMarca_GuardaSeUmAno_ESemMarca_PerguntaSempre | Ficheiro com marca guarda-se um ano; sem marca pergunta sempre | OK |  |

### `tests/DMO.Alpha.Web.Tests/InstalacaoInicialTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| InstalacaoInicialTests.BaseVazia_ComOCodigo_CriaOAdmin_QueEntraComAPalavraPasse | Base vazia com o código cria o ADMIN que entra | OK |  |
| InstalacaoInicialTests.CodigoErrado_NaoCriaNada | Código de instalação errado não cria nada | OK |  |
| InstalacaoInicialTests.CodigoSorteado_MudaAoFimDeCincoEnganos | Código de instalação muda após cinco enganos | OK |  |
| InstalacaoInicialTests.ComAdminJaGravado_NaoHaInstalacao | Com ADMIN já gravado não há instalação | OK |  |
| InstalacaoInicialTests.ComPalavrasPasseNaBase_NaoHaInstalacao | Com palavras-passe na base não há instalação | OK |  |
| InstalacaoInicialTests.DadosQueNaoServem_NaoCriamNada (Theory) | Email ou palavra-passe inválidos não criam o ADMIN | OK |  |
| InstalacaoInicialTests.DepoisDeFeita_OCodigoNaoVoltaAServir | Depois de instalar o código não serve outra vez | OK |  |
| InstalacaoInicialTests.SemLoginProprio_NaoHaInstalacao | Sem login próprio não há instalação | OK |  |

### `tests/DMO.Alpha.Web.Tests/ListPaginationPagesTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ListPaginationPagesTests.AdminUsers_ListShows25PerPage_WithPagerKeepingOtherParameters | Lista de utilizadores mostra 25 por página com paginação | OK |  |
| ListPaginationPagesTests.AdminUsers_ShortList_StillShowsThePagerWithOnePage | Lista curta mostra «1 de 1» | OK |  |

### `tests/DMO.Alpha.Web.Tests/LocalUrlTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| LocalUrlTests.CaminhosDaApp_Passam (Theory) | Caminhos da app são aceites no retorno | OK |  |
| LocalUrlTests.OQueARegraAceita_OLocalRedirectTambemAceita (Theory) | O que a regra aceita o redirecionamento local também aceita | OK |  |
| LocalUrlTests.OutrosSitiosOuCaracteresQueOBrowserTira_SaoRecusados (Theory) | Outros sites e truques de caracteres são recusados | OK |  |

### `tests/DMO.Alpha.Web.Tests/LoginSemAcessoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| LoginSemAcessoTests.Operador_NoAdmin_VeSemAcesso_SemFormularioDeEntrada | Operador no Admin vê «Sem acesso» em vez do login | OK |  |
| LoginSemAcessoTests.SemSessao_OLoginMostraOFormulario | Sem sessão o login mostra o formulário | OK |  |

### `tests/DMO.Alpha.Web.Tests/ModuleEntryTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ModuleEntryTests.BothControlos_OpenTheirOwnPages | Controlo tem uma só entrada no menu e abre as duas páginas | OK |  |
| ModuleEntryTests.InsideTheModule_PagesShowNoPermissionNotices | Dentro do módulo não há avisos de permissão | FRACO | Só «não aparece» («capability», «Sem permissão») sem «aparece» antes. |
| ModuleEntryTests.JobOn_ShowsTheSameTools_WhateverElseTheTemplateGives | Job On mostra Criar CM/MF/BQ qualquer que seja a template | OK |  |
| ModuleEntryTests.Login_IgnoresAReturnUrlOutsideTheApp | Login ignora retorno para fora da app | OK |  |
| ModuleEntryTests.OnlyTheTemplateModules_OpenAndAppearInTheMenu | Só os módulos da template abrem e aparecem no menu | OK |  |
| ModuleEntryTests.Suggestions_AnswerOnlyForTheModulesThatUseThem | Sugestões só para os módulos que as usam | OK |  |
| ModuleEntryTests.TemplateChange_AppliesOnTheNextRequest | Mudar a template vale no pedido seguinte | OK |  |

### `tests/DMO.Alpha.Web.Tests/PaginasSemSessaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PaginasSemSessaoTests.PorOmissao_TodasAsPaginasPedemSessao | Por omissão todas as páginas pedem sessão | OK |  |
| PaginasSemSessaoTests.SemSessao_SoAbremAsPaginasPublicas | Sem sessão só abrem as páginas públicas | OK |  |

### `tests/DMO.Alpha.Web.Tests/PersistencePolicyTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PersistencePolicyTests.Beta_NoDatabaseConnection_StillServesNormalLogin_NotSetup | Sem ligação à base mostra o login normal, não a instalação | OK |  |
| PersistencePolicyTests.Beta_WithExplicitDatabaseConnection_UsesNpgsql (Theory) | Com ligação explícita usa PostgreSQL | OK |  |
| PersistencePolicyTests.Development_WithoutDatabase_PreservesLocalAuthenticationAndWriteSeam (Theory) | Em desenvolvimento sem base usa memória | OK |  |
| PersistencePolicyTests.NonDevelopment_AlwaysRegistersRealAuthenticationAndProvisioning (Theory) | Em produção usa sempre autenticação real | OK |  |
| PersistencePolicyTests.NonDevelopment_SemFornecedorOuLocal_UsaOLoginProprio (Theory) | Fora do desenvolvimento sem fornecedor usa o login próprio | OK |  |
| PersistencePolicyTests.NonDevelopment_WithoutDatabase_ServesLoginButCannotSaveVolatileData (Theory) | Sem base em produção o login abre mas não grava | OK |  |

### `tests/DMO.Alpha.Web.Tests/RegraDasDependenciasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| RegraDasDependenciasTests.AsPaginas_RecebemAsDependenciasPeloConstrutor | Páginas recebem dependências pelo construtor | OK | Guarda de código (REGRAS-DE-CODIGO), não regra do negócio. |

### `tests/DMO.Alpha.Web.Tests/SemVigiarFicheirosTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SemVigiarFicheirosTests.UmaAplicacaoDeTeste_ArrancaEAbreOLogin_SemGastarVigiasDeFicheiros | Aplicação de teste não gasta vigias de ficheiros | OK | Infraestrutura de testes (evita erros 500 ao acaso). |

### `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| SharedComponentsTests.AdminUsersToasts_StayClearOfTheCreateButton | Avisos dos Utilizadores não tapam «Criar utilizador» | FRACO | Compara texto de uma regra CSS específica; não prova que não tapa. |
| SharedComponentsTests.Buttons_HaveOnlyTheirThinBorder_NoOutlineAround | Botões só têm a borda fina, sem outline | OK | Guarda do design por texto de CSS. |
| SharedComponentsTests.Buttons_PressShowsALighterBlue | Botão carregado fica azul mais claro | OK | Guarda do design por texto de CSS. |
| SharedComponentsTests.Calendar_DaysAreBuiltOnlyByForMonth | Dias do calendário só construídos por ForMonth | OK | Guarda de código. |
| SharedComponentsTests.Calendar_MarkupLivesOnlyInTheSharedPartial | Calendário só existe no partial partilhado | OK | Guarda de código/markup. |
| SharedComponentsTests.Calendar_SelectedDayHover_KeepsSlateBackground | Dia selecionado com rato mantém o fundo slate | OK | Guarda do design por texto de CSS. |
| SharedComponentsTests.Calendar_Today_IsOnlyBold | Dia de hoje só a negrito | OK | Guarda do design por texto de CSS. |
| SharedComponentsTests.CardDobravel_UmSoTriangulo_EBotaoInvertido_BrancoNoEscuro | Cartão dobrável com um só triângulo; escolhido invertido | OK | Guarda do design por texto de CSS. |
| SharedComponentsTests.Caret_UmaRegraParaTudo_AEsquerdaDoTexto | Triângulo à esquerda do texto, uma regra para tudo | OK | Guarda do design por texto de CSS (Diogo 22/10: ícones só bola e triângulo). |
| SharedComponentsTests.Dropdowns_ApplyOnChoose_WithoutAnApplyButton | Dropdowns aplicam ao escolher, sem botão Aplicar | OK | Guarda de markup. |
| SharedComponentsTests.FerramentasTechnicalNotice_KeepsItsLine | Nota do nome técnico reserva a sua linha | FRACO | Compara texto CSS (min-height:1lh); não mede nada. |
| SharedComponentsTests.FichaWarnings_GoToTheCorner | Avisos da ficha da ferramenta vão para o canto | OK | Conta 7 avisos no .cshtml: brittle quando se acrescenta um. |
| SharedComponentsTests.ForMonth_BuildsTheWholeMonth_WithCountsSelectionAndQuery | Calendário constrói o mês inteiro com contagens e seleção | OK |  |
| SharedComponentsTests.ForMonth_HasRuleCanDifferFromTheCount | Ponto do calendário só nos dias por aprovar | OK |  |
| SharedComponentsTests.HelpTexts_LiveInTheTip_NotWrittenOnTheScreen (Theory, 15 páginas) | Textos de ajuda ficam na dica, não escritos no ecrã | FRACO | Só procura a palavra «clique» nos .cshtml; passa com muitos outros textos explicativos. |
| SharedComponentsTests.HelpTip_IsTheSharedCircle_AndLivesOnlyInThePartial | Dica de ajuda é a bola partilhada, só no partial | OK | Guarda do design por texto; confirma «ícones só bola de ajuda e triângulo». |
| SharedComponentsTests.InfoCards_UseOnlyTheSharedClass | Cartões de informação usam só a classe dmo-fact | OK | Guarda do design por texto. |
| SharedComponentsTests.Lists_OpenWithOneClick | Listas abrem com um clique | OK | Confirma Diogo 17/10 (um clique); guarda de markup e script por texto. |
| SharedComponentsTests.Markup_HasNoInteractiveSummaryContent_NorLabelsWithoutField | Sem conteúdo clicável em summary nem label sem campo | OK | Guarda de markup. |
| SharedComponentsTests.ResultMessages_FloatAboveThePage | Mensagens de resultado flutuam no canto e só «ok» fecha sozinha | OK | Guarda do design por texto de CSS e JS. |
| SharedComponentsTests.SearchFields_HaveNoSearchButton | Campos de procura sem botão Procurar | OK |  |
| SharedComponentsTests.SemItalico_EmTodaAApp | Sem itálico em toda a app | OK | Guarda do design por texto de CSS. |
| SharedComponentsTests.ShellLayout_RendersOnlyWhenTheWholePageArrived | Página só se desenha quando chega inteira (sem flash) | OK |  |
| SharedComponentsTests.Tables_UseOnlyTheSharedClass_WithoutPerModuleSelectionClasses | Tabelas usam só a classe partilhada | OK | Guarda do design por texto. |

### `tests/DMO.Alpha.Web.Tests/StartupConfigurationCheckTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| StartupConfigurationCheckTests.Arranque_EmProducaoSemAsChavesDoSupabase_DeixaNoLogOsNomesDasVariaveis | Arranque sem chaves do Supabase deixa os nomes no log | OK |  |
| StartupConfigurationCheckTests.Development_NoComputador_ComInMemory_NaoDizNada | Desenvolvimento local em memória não avisa | OK |  |
| StartupConfigurationCheckTests.Development_NumServicoAlojado_AvisaQueOsDetalhesDosErrosFicamVisiveis (Theory) | Desenvolvimento num serviço alojado avisa que os erros ficam visíveis | OK |  |
| StartupConfigurationCheckTests.ErroNoPedido_AReferenciaDoEcraFicaNoLog | A referência do erro mostrada no ecrã fica no log | OK |  |
| StartupConfigurationCheckTests.Producao_ComFornecedorDesconhecido_Avisa | Fornecedor de login desconhecido gera aviso | OK |  |
| StartupConfigurationCheckTests.Producao_ComOEnderecoDoSupabaseSemSerUmUrl_ConsideraOEmFalta | Endereço do Supabase que não é URL conta como em falta | OK |  |
| StartupConfigurationCheckTests.Producao_ComTudoConfigurado_NaoDizNada | Produção bem configurada não avisa nada | OK |  |
| StartupConfigurationCheckTests.Producao_SemFornecedorDeclarado_UsaOLoginProprio_SemAviso | Sem fornecedor declarado usa o login próprio sem aviso | OK |  |
| StartupConfigurationCheckTests.Producao_SemUmaVariavel_DizONomeComoErro_NuncaOsValores (Theory) | Variável em falta aparece no log pelo nome, nunca o valor | OK |  |

### `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| UiStyleRuleTests.AutofilledFields_UseTheSameFontAsChromePreview | Campos preenchidos pelo Chrome mantêm a letra | FRACO | Compara texto CSS do autofill; efeito só visível no browser. |
| UiStyleRuleTests.ButtonAndFieldText_IsCentered_WithPinnedFontMetrics | Texto de botões e campos centrado com métricas fixas | FRACO | Compara texto de declarações CSS exatas (ascent-override...); sem regra do Diogo nem efeito. |
| UiStyleRuleTests.Buttons_HaveOneLook_BlueWithWhiteText_HoverInverts | Botões azuis com letra branca, hover inverte | OK | Guarda do design por texto de CSS (Diogo 16/10). |
| UiStyleRuleTests.Buttons_ShareTheFieldHeight_PagerUsesArrowsOnly_PillsTrimText | Botões da altura dos campos; paginação só setas | OK | Guarda do design por texto de CSS. |
| UiStyleRuleTests.CardTopo_PutsTheCardButtonsTopRight_WithTheHouseSpacing | Botões do cartão no canto superior direito | OK | Guarda do design por texto de CSS. |
| UiStyleRuleTests.DarkMode_RedefinesTheTokens_AndNoSheetPaintsLooseWhite | Modo escuro redefine tokens; sem brancos soltos; sem link Admin | OK | Guarda do design; impressão fica clara. |
| UiStyleRuleTests.DisabledPagerButtons_DoNotFadeInline | Botões de paginação desativados sem opacidade inline | FRACO | Só procura «opacity:» num ficheiro. |
| UiStyleRuleTests.LinksNosMiniCartoes_TemACorDoValor_SemSublinhado_NemNoHover | Links nos mini-cartões sem sublinhado | OK | Guarda do design por texto de CSS. |
| UiStyleRuleTests.Palette_IsDefinedOnce_AndDrivesButtonsRowsAndHover | Paleta definida num só sítio | OK | Guarda do design por texto de CSS. |
| UiStyleRuleTests.PdfGenerators_UseThePalette_NotTheOldBlues | PDFs usam a paleta, não os azuis antigos | OK | Guarda de código. |
| UiStyleRuleTests.Pills_CenterTheirText_AndLogoutIsJustTheWord | Pílulas com texto centrado; Sair é só a palavra | OK | Guarda do design por texto de CSS. |
| UiStyleRuleTests.Scripts_DoNotCarryLooseColors | Scripts sem cores soltas | OK | Guarda do design. |
| UiStyleRuleTests.TableRows_AreWhite_HoverLightBlue_SelectedInverted_RoundedInsideTheCard_WithoutVisibleRadio | Linhas brancas, hover azul-claro, escolhida invertida | OK | Guarda do design por texto de CSS. |

## Reparação Interna, JavaScript (ShellProof) e browser (visual)

302 linhas: OK 277, FRACO 24, DUPLICADO 1

### `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacoesInternasConsultaTests.ALinha_AProducaoAFerramentaEQuem_NuncaONomeTecnico | Linha mostra produção, ferramenta e quem, nunca o nome técnico | OK |  |
| ReparacoesInternasConsultaTests.AsAnuladas_FicamDeFora | As anuladas não aparecem na Consulta | OK |  |
| ReparacoesInternasConsultaTests.CadaFiltroSozinho (14 casos) | Cada filtro da Consulta isolado traz as certas | OK |  |
| ReparacoesInternasConsultaTests.DaMaisRecenteParaAMaisAntiga | Consulta com o último registo em cima | OK |  |
| ReparacoesInternasConsultaTests.EmPaginasDe25 (3 casos) | Paginação de 25 com 24, 25 e 26 registos | OK |  |
| ReparacoesInternasConsultaTests.OFiltroQuem_SoOsUtilizadoresQueRegistaram_PorNome | Filtro Quem só lista quem registou | OK |  |
| ReparacoesInternasConsultaTests.OsDias_DeEAteInclusive (9 casos) | De e Até incluem o próprio dia (abaixo, no limite, acima) | OK |  |
| ReparacoesInternasConsultaTests.OsFiltrosJuntos (7 casos) | Filtros combinados cruzam-se | OK |  |
| ReparacoesInternasConsultaTests.ResumoPorPessoa_ContaReparacoesDiasEMedia_SemAsAnuladas | Resumo por pessoa conta reparações, dias e média | FRACO | Rui 3/3 e Ana 2/2 dão ambas 1,00: trocar reparações/dias por dias/reparações passa. Usar uma pessoa com 4 reparações em 2 dias. |
| ReparacoesInternasConsultaTests.SemFiltro_NaoLeNada | Sem filtro a Consulta não lê nada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasCorrigirTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacoesInternasCorrigirTests.Anular_AChefia_ADeOutraPessoaSaiDaListaEDasContagens_FicaNaBase_ComRasto | Anular tira da lista e das contagens, fica na base com rasto | OK |  |
| ReparacoesInternasCorrigirTests.AsAnuladas_NaoContamNasPaginas | Anuladas não contam nas páginas | OK |  |
| ReparacoesInternasCorrigirTests.Corrigir_AChefia_MudaSoONumeroDeOutraPessoa_EDeixaRasto | A chefia corrige só o número de outra pessoa, com rasto | OK |  |
| ReparacoesInternasCorrigirTests.Corrigir_ONumeroTemAte40Caracteres (39/40/41) | Corrigir respeita o limite de 40 caracteres | OK |  |
| ReparacoesInternasCorrigirTests.Corrigir_ParaOMesmoNumero_NadaMuda_SemRasto | Corrigir para o mesmo número não grava rasto | OK |  |
| ReparacoesInternasCorrigirTests.Corrigir_SemNumero_Recusa_ENadaMuda (3 casos) | Corrigir sem número recusa e nada muda | OK |  |
| ReparacoesInternasCorrigirTests.DuasCorrecoes_DoisRastos_PelaOrdem | Duas correções deixam dois rastos por ordem | OK |  |
| ReparacoesInternasCorrigirTests.EliminarOJobOn_ContaTodas_ELevaAsReparacoesEORasto | Eliminar Job On leva reparações, anuladas e rasto | OK |  |
| ReparacoesInternasCorrigirTests.OMesmoEnvioDuasVezes_AplicaUmaVez | Mesmo envio de correção ou anulação aplica-se uma vez | OK | Em memória; ao mesmo tempo só a anulação tem teste PostgreSQL. |
| ReparacoesInternasCorrigirTests.OMotivo_EOpcional_ETemAte200Caracteres (199/200/201) | Motivo opcional até 200 caracteres | OK |  |
| ReparacoesInternasCorrigirTests.OutraPessoaSemAChefia_NaoCorrigeNemAnula_ENadaMuda | Quem não registou e não é chefia não corrige nem anula | OK |  |
| ReparacoesInternasCorrigirTests.SemMotivo_ORastoFicaSemMotivo (3 casos) | Sem motivo o rasto fica sem motivo | OK |  |
| ReparacoesInternasCorrigirTests.UmaAnulada_NaoSeCorrigeNemSeAnulaOutraVez | Anulada não se corrige nem anula de novo | OK |  |
| ReparacoesInternasCorrigirTests.UmaQueJaNaoExiste_NadaMuda | Reparação inexistente não grava nada | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasHoraTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacoesInternasHoraTests.Corrigir_SemHora_DeixaAHora_ENumeroEHoraIguais_NadaMuda | Corrigir sem hora mantém a hora; tudo igual não muda | OK |  |
| ReparacoesInternasHoraTests.Corrigir_SoAHora_MudaAHora_ComRasto_EONumeroFica | Corrigir só a hora deixa rasto antes e depois | OK |  |
| ReparacoesInternasHoraTests.Registar_ComAHoraMudada_GravaAEscrita | A hora mudada pelo operador fica gravada | OK |  |
| ReparacoesInternasHoraTests.Registar_SemHora_GravaAHoraLocalDeAgora_AoMinuto_EORegistadoEmFicaIgual | Sem hora escrita grava a hora local de agora | OK |  |
| ReparacoesInternasHoraTests.UmaDeAntes_SemHora_ListaComHoraNula_ECorrigirDaLheHora | Reparação antiga sem hora pode receber hora | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasOutraFerramentaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacoesInternasOutraFerramentaTests.APaginaMostrouUmaProducaoQueJaNaoConta_Mudou | Produção que deixou de contar recusa sem gravar | OK |  |
| ReparacoesInternasOutraFerramentaTests.AReferencia_TemAte40_SemContarOsEspacosDasPontas (39/40/41) | Referência escrita até 40 caracteres | OK |  |
| ReparacoesInternasOutraFerramentaTests.Associar_AFerramentaDoTipo_GravaQuemEQuando_EORasto | Associar liga pelo tool_id e guarda quem, quando e rasto | OK |  |
| ReparacoesInternasOutraFerramentaTests.Associar_OMesmoEnvioDuasVezes_AssociaUmaVez | Associar repetido só conta uma vez | OK |  |
| ReparacoesInternasOutraFerramentaTests.Associar_OQueNaoServe_Recusa_ENadaMuda | Associar ferramenta errada, anulada ou inexistente recusa sem gravar | OK |  |
| ReparacoesInternasOutraFerramentaTests.Consulta_SoPorAssociar_ComAsFerramentasQueServem | Filtro por associar propõe primeiro as ferramentas do lote | OK |  |
| ReparacoesInternasOutraFerramentaTests.EliminarAFerramentaAssociada_PassaParaAREMOVED_EORastoFica | Eliminar a ferramenta associada passa a REMOVED, rasto fica | OK |  |
| ReparacoesInternasOutraFerramentaTests.OLote_TemAte12_SemContarOsEspacosDasPontas (11/12/13) | Lote escrito até 12 caracteres | OK |  |
| ReparacoesInternasOutraFerramentaTests.OMesmoEnvioDuasVezes_GravaUmaVez_EVoltaComAEscrita | Envio repetido com escrita grava uma vez | OK |  |
| ReparacoesInternasOutraFerramentaTests.OutraCm_GravaAReferenciaEOLoteEscritos_SemContexto_ComOJobOn | Outra CM grava referência e lote escritos, por associar | OK |  |
| ReparacoesInternasOutraFerramentaTests.OutraSemAReferenciaOuOLote_Recusa_ComONumeroEscrito_ENadaSeGrava (4 casos) | Outra sem referência ou sem lote recusa e nada se grava | OK |  |
| ReparacoesInternasOutraFerramentaTests.SemOutra_ARegraDoJobOnFicaIgual_AReferenciaEscritaNaoConta | Sem a marca «outra», a referência escrita é ignorada | OK |  |
| ReparacoesInternasOutraFerramentaTests.SemProducao_GravaSemJobOn_ComAEscrita_EAListaDaMaquinaNoDia | Sem Job On grava com a escrita e lista por máquina e dia | OK |  |

### `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacoesInternasTests.AHoraDeOutroModulo_NaoMudaADaReparacaoInterna | A hora de viragem das Boquilhas não muda a da Reparação | OK |  |
| ReparacoesInternasTests.AHoraDeViragemDaReparacaoInterna_GravaSeAParte (4 casos) | A hora de viragem grava-se à parte, entre 0 e 23 | OK |  |
| ReparacoesInternasTests.ALista_DestaMaquinaEProducao_DaMaisRecente_EmPaginasDe25 | Lista da máquina, último registo em cima, 25 por página | OK |  |
| ReparacoesInternasTests.AntesDaViragem_AProducaoDeOntem | Antes da viragem vale a produção de ontem | OK |  |
| ReparacoesInternasTests.DuasProducoes_AsDuasCandidatas_ADeMaiorJobOnIdEscolhida | Com duas produções escolhe a de maior jobon_id | OK |  |
| ReparacoesInternasTests.DuasProducoes_GravaNaEscolhida | Grava na produção escolhida entre as duas | OK |  |
| ReparacoesInternasTests.EliminarOJobOn_ContaELevaAsReparacoes | Eliminar o Job On conta e leva as suas reparações | OK |  |
| ReparacoesInternasTests.ODiaOperacional_ViraAHoraDaReparacaoInterna (6 casos) | O dia da reparação vira à hora configurada (8h por omissão) | OK |  |
| ReparacoesInternasTests.OMesmoEnvioDuasVezes_GravaUmaVez_OMesmoNumeroOutraVezGravaOutra | Envio repetido grava uma vez; mesmo número noutro envio grava | OK | Em memória; a garantia ao mesmo tempo está em PostgresMigrationIntegrationTests.ReparacaoInterna (fora da área). |
| ReparacoesInternasTests.ONumero_TemAte40Caracteres_SemContarOsEspacosDasPontas | Número até 40 caracteres, sem contar espaços das pontas | OK | O serviço aceita letras e 40; a regra «só algarismos até 3» (Diogo 23/10) só está na página. |
| ReparacoesInternasTests.OQueFaltaOuMudou_Recusa_ComONumeroEscrito | Tipo, máquina, produção ou ferramenta mudados recusam sem gravar | OK |  |
| ReparacoesInternasTests.Registar_GravaALinhaCurta_ComOCmIdDoMomento | Registar grava linha curta ligada ao cm_id do momento | OK |  |
| ReparacoesInternasTests.Registar_NaoMexeEmMaisNada | Registar não altera ferramentas, Job On, BQ nem Armazém | OK |  |
| ReparacoesInternasTests.Registar_UmMf_GuardaSoOMfId | Reparação de MF liga só ao mf_id | OK |  |
| ReparacoesInternasTests.SemNumero_Recusa (3 casos) | Número vazio é recusado e nada se grava | OK |  |
| ReparacoesInternasTests.SemProducaoDatada_SemAFerramentaEscrita_NadaSeGrava | Sem Job On e sem referência escrita nada se grava | OK |  |
| ReparacoesInternasTests.UmUtilizadorEliminado_FicaSemQuem | Utilizador eliminado deixa a reparação sem nome | OK |  |
| ReparacoesInternasTests.UmaProducao_ADeHojeSemFim_AsSemDatasNaoContam | A produção do dia vem do Job On; sem datas não conta | OK |  |

### `tests/DMO.Alpha.Web.Tests/PostoSoRiTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostoSoRiTests.PeloPosto_ComOTempoDasDefinicoes_ASessaoDuraEsseTempo | O tempo do Posto vem das Definições | OK |  |
| PostoSoRiTests.PeloPosto_SemUsoDurante3Minutos_ASessaoFecha | Sem uso 3 minutos a sessão do Posto fecha | OK |  |
| PostoSoRiTests.PeloPosto_SoAReparacaoInterna_PeloLoginTodosOsModulos | Pelo Posto só vê a RI; pelo login vê tudo | OK | IPostoRi simulado; PIN real em PostgresMigrationIntegrationTests.PostoRi. |
| PostoSoRiTests.SairDoPosto_VoltaAoPosto_PeloLoginNaoHaTempo | Sair do Posto volta ao Posto; pelo login não há limite | OK |  |

### `tests/DMO.Alpha.Web.Tests/PostoTravaoTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| PostoTravaoTests.Espera_SoDepoisDaQuintaFalha_ECrescendoAteTrintaSegundos | PIN errado só espera depois da 5.ª falha, até 30 s | OK |  |
| PostoTravaoTests.MesmaOrigem_UmPinDeCadaVez_OutraOrigemNaoEspera | Um PIN de cada vez por origem | OK | Usa Task.Delay(100) para provar que espera: pode ser instável. |
| PostoTravaoTests.Posto_PinsErrados_EsperamCadaVezMais_EOPinCertoEntraNaMesma | PINs errados esperam cada vez mais; o certo entra | OK | Não verifica que os PINs errados não entram além do 200. |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoInternaConsultaPaginaTests.AChaveAntigaSoPermiteRegistar_EConsultarExigeAChaveDosResponsaveis | ri só regista; ri.view só consulta | OK |  |
| ReparacaoInternaConsultaPaginaTests.AProducaoEmDoisPassos_AReferenciaDepoisAsUltimas5 | Referência primeiro, depois as últimas 5 produções | OK |  |
| ReparacaoInternaConsultaPaginaTests.AbreVazia_SoComOsFiltros | A Consulta abre vazia, só com filtros | OK |  |
| ReparacaoInternaConsultaPaginaTests.AsPessoasEOsDias_Filtram | Filtros pessoa e dias na página | OK |  |
| ReparacaoInternaConsultaPaginaTests.ComOJobOn_AProducaoAbreAFicha | Com Job On a produção liga à ficha | OK |  |
| ReparacaoInternaConsultaPaginaTests.DepoisDeFiltrar_AsReparacoes_SemBotoes_ENuncaONomeTecnico | Após filtrar: linhas sem botões, mais recente primeiro, sem nome técnico | OK | Verifica também class dmo-table e cabeçalhos exatos (regra 10, ponto menor). |
| ReparacaoInternaConsultaPaginaTests.Filtrar_SoMudamAsZonas | Filtrar só troca as zonas da página | OK |  |
| ReparacaoInternaConsultaPaginaTests.OResumoPorPessoa_Repartido_ONomeFiltraPorEla | Clicar no nome no resumo filtra por essa pessoa | OK | Assere classes (ativo, ri-resumo-pessoas); o efeito vem de data-ri-pessoa e pessoa=. |
| ReparacaoInternaConsultaPaginaTests.OsDoisSeparadores_RegistarEConsultar | Separadores Registar e Consultar com o certo marcado | FRACO | Só verifica href e aria-current (marcação); nenhum efeito. |
| ReparacaoInternaConsultaPaginaTests.QuemSoConsultaPodeProcurarAsReferencias | Quem só consulta pode procurar referências | OK |  |
| ReparacaoInternaConsultaPaginaTests.SemAChave_NaoEntra | Sem a chave ri.view não entra na Consulta | OK |  |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoInternaCorrigirPaginaTests.AChefia_AnulaADeOutraPessoa_ALinhaSai_EFicaNaBase | Chefia anula a de outra pessoa; sai da lista, fica na base | OK |  |
| ReparacaoInternaCorrigirPaginaTests.AChefia_CorrigeONumeroDeOutraPessoa_ComRasto_EOMesmoEnvioAplicaUmaVez | Chefia corrige a de outra pessoa; envio repetido aplica-se uma vez | OK | Usa o formulário do autor com o token da chefia: nenhum ecrã mostra as reparações dos outros (ri-permissoes.md). |
| ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ODuploCliqueAbreAReparacao_ComCorrigirEAnular | Linhas sem botões; duplo clique abre Corrigir e Anular | FRACO | Grande parte são classes CSS (dmo-dialog ri-janela), ordem de atributos e regex da estrutura (regra 10). |
| ReparacaoInternaCorrigirPaginaTests.AnuladaEntretanto_CorrigirOuAnularAvisa_ENadaMuda | Já anulada entretanto: avisa e nada muda | OK |  |
| ReparacaoInternaCorrigirPaginaTests.CorrigirComNumeroQueNaoESoAlgarismosAte3_Recusa_ENadaMuda (2 casos) | Corrigir só aceita algarismos até 3 | OK |  |
| ReparacaoInternaCorrigirPaginaTests.OutroOperadorSemAChefia_NaoCorrigeNemAnula_ENadaMuda | Sem chefia não corrige nem anula, nada muda | OK |  |
| ReparacaoInternaCorrigirPaginaTests.SemAChave_NaoCorrigeNemAnula | Sem a chave ri não corrige nem anula | OK |  |
| ReparacaoInternaCorrigirPaginaTests.UmNumeroLongo_Recusa_ENadaMuda | Número longo é recusado ao corrigir | OK |  |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHistoricoPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoInternaHistoricoPaginaTests.CorrigirNoHistorico_VoltaAoHistoricoComOsFiltros | Corrigir no Histórico volta ao Histórico; destino externo ignorado | OK |  |
| ReparacaoInternaHistoricoPaginaTests.MudarODia_RegistaNesseDia_ESoAvisa | Mudar o dia regista nesse dia e só avisa | OK |  |
| ReparacaoInternaHistoricoPaginaTests.OHistorico_AbreVazio_SemFiltroDeQuem_EMostraSoAsDestaPessoa | Histórico abre vazio e mostra só as da própria pessoa | OK |  |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaHoraPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoInternaHoraPaginaTests.AHoraMudada_FicaGravada_EAparecenoRegisto | Hora mudada fica gravada e aparece no registo | OK |  |
| ReparacaoInternaHoraPaginaTests.Corrigir_ComUmaHoraInvalida_NadaMuda | Hora inválida ao corrigir não muda nada | OK |  |
| ReparacaoInternaHoraPaginaTests.Corrigir_MudaAHora_ComRasto | Corrigir muda a hora com rasto | OK |  |
| ReparacaoInternaHoraPaginaTests.OCampoHora_AoLadoDoDia_VemComAHoraDeAgora_EGravaSeComOsMinutos | Hora vem com a hora de agora e grava-se | OK |  |
| ReparacaoInternaHoraPaginaTests.OsLimitesDaHora_DaMeiaNoiteAo2359_E24Recusa (3 casos) | Limites da hora: 00:00 e 23:59 gravam, 24:00 recusa | OK |  |
| ReparacaoInternaHoraPaginaTests.UmaDeAntes_SemHora_MostraAHoraDoRegisto | Reparação antiga mostra a hora do registo | OK |  |
| ReparacaoInternaHoraPaginaTests.UmaHoraInvalida_Recusa_ComMensagem_ENadaSeGrava | Hora inválida recusa com mensagem sem gravar | OK |  |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoInternaOutraFerramentaPaginaTests.AFerramentaMostrada_LevaAFicha_SoComAsFerramentas_AEscritaFicaTexto | Ficha só com a chave Ferramentas; escrita fica texto | OK |  |
| ReparacaoInternaOutraFerramentaPaginaTests.Consultar_AJanelaAssociar_GravaUmaVez_EALinhaFicaComAFerramenta | A janela Associar grava uma vez e a linha mostra a ferramenta | OK | Janela tem Cancelar (não Voltar); decidir se vale como janela. |
| ReparacaoInternaOutraFerramentaPaginaTests.Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas | Por associar: marca só texto, a linha abre a janela, filtro, sem botões | OK | Corrigido em DMO-580 (#285). |
| ReparacaoInternaOutraFerramentaPaginaTests.Consultar_SemFerramentaQueSirva_AJanelaAvisaComOLinkParaRegistar | Sem ferramenta que sirva, a janela aponta para registar | OK |  |
| ReparacaoInternaOutraFerramentaPaginaTests.CorrigirParaOMesmoNumero_NadaMuda_ESemAviso | Corrigir para o mesmo número não avisa | FRACO | Faz só um GET com ?correcao=igual e vê que não há toast; nunca corrige nada nem mostra um aviso antes. Duplica Corrigir_ParaOMesmoNumero (infra). |
| ReparacaoInternaOutraFerramentaPaginaTests.GravarComOutraCm_AvisaQueFaltaAssociar_EOCampoVoltaVazioEDesmarcado | Gravar com outra CM avisa que falta associar | OK |  |
| ReparacaoInternaOutraFerramentaPaginaTests.OCartaoDoNumero_TemAMarcaOutraCm_EOsCamposCurtos_ODaProducaoFicaIgual | Cartão tem a marca «É outra CM/MF» e campos curtos | FRACO | Classes ri-referencia/ri-lote, maxlength e string exata do checkbox: marcação, não efeito. |
| ReparacaoInternaOutraFerramentaPaginaTests.OPainel_BAntesDeC_ORegistoSoDestaPessoa_EditarNoCanto | Painel de linhas ordenado; registo só desta pessoa, recente primeiro | OK | Inclui class dmo-button no Editar (ponto menor). |
| ReparacaoInternaOutraFerramentaPaginaTests.OPainel_LogoDepoisDaMeiaNoite_ORegistoDoDiaOperacional | Depois da meia-noite o registo é o do dia operacional | OK |  |
| ReparacaoInternaOutraFerramentaPaginaTests.OutraSemAReferencia_RecusaNoCampo_ENadaSeGrava | Outra sem referência recusa e nada se grava | OK |  |
| ReparacaoInternaOutraFerramentaPaginaTests.RetirarAConsultaRecusaOFormularioDeAssociacaoJaAberto_SemAlterarORegisto | Retirar a chave recusa o Associar já aberto | OK |  |
| ReparacaoInternaOutraFerramentaPaginaTests.RetirarORegistoMantendoAConsultaRecusaOFormularioDeRegistoJaAberto | Retirar a chave ri recusa o registo já aberto | OK |  |
| ReparacaoInternaOutraFerramentaPaginaTests.SemProducao_GravaSemJobOn_AvisaEATabelaMostraAsDoDia | Sem Job On grava, avisa e a tabela mostra as do dia | OK |  |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoInternaPaginaTests.AdminTemplatesECanal_AChaveEAHoraDaReparacaoInterna | Admin vê a chave; hora de viragem da RI separada da das Boquilhas | OK |  |
| ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas | Com a chave abre o menu, o painel de linhas e as zonas | FRACO | Quase tudo marcação (beta-production-rail, class selected, data-dmo-*); só o redirect do início é efeito. |
| ReparacaoInternaPaginaTests.DuasProducoes_AvisaComAsCandidatas_ADeMaiorIdEscolhida | Com duas produções escolhe a maior e deixa mudar | OK |  |
| ReparacaoInternaPaginaTests.EliminarAProducao_AvisaQueSePerdemAsReparacoes | Aviso de eliminação diz quantas reparações se perdem | DUPLICADO | Mesma frase que EliminarJobOnComReparacoes_AJanelaDizQuantasSePerdem (3 reparações), já coberta pela página real. |
| ReparacaoInternaPaginaTests.EliminarJobOnComReparacoes_AJanelaDizQuantasSePerdem (2 casos) | Eliminar o Job On avisa quantas reparações se perdem | OK |  |
| ReparacaoInternaPaginaTests.EscolherAMaquinaEGravar_SoMudamAsZonas | Escolher a máquina e gravar só muda as zonas | OK |  |
| ReparacaoInternaPaginaTests.EscreverONumeroEEnter_Grava_OCampoVoltaVazio_EALinhaApareceNoTopo | Escrever o número e Enter grava, campo vazio, linha no topo | OK |  |
| ReparacaoInternaPaginaTests.OMesmoEnvioDuasVezes_GravaUmaVez | Mesmo envio duas vezes grava uma | OK | Em memória. |
| ReparacaoInternaPaginaTests.ONumero_TresAlgarismos_Grava_EOCampoSoAceitaAlgarismos | Três algarismos gravam | OK |  |
| ReparacaoInternaPaginaTests.SemAChave_NaoEntra | Sem a chave ri não entra | OK |  |
| ReparacaoInternaPaginaTests.SemProducao_AvisaEOFormularioPedeAReferenciaEOLote | Sem produção o formulário pede referência e lote | FRACO | Só verifica texto e atributos required; não envia nem vê o que fica gravado. |
| ReparacaoInternaPaginaTests.UmaRecusa_VoltaComONumeroEscrito_ENadaSeGrava (3 casos) | Número com mais de 3 ou não algarismos recusa e mantém o escrito | OK |  |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ReparacaoInternaZonasPaginaTests.AAutorizacaoEADaPagina_ComECemOCabecalho (2 casos) | Autorização igual com e sem cabeçalho de zonas | OK |  |
| ReparacaoInternaZonasPaginaTests.DepoisDeGravar_RegistoEAvisos_SaoOsMesmosPartialsDaPaginaInteira | Após gravar, as zonas parciais igualam as da página inteira | OK |  |
| ReparacaoInternaZonasPaginaTests.OHistorico_ZonaDosResultados_ESoAsMinhasReparacoes_SaoOsMesmosPartials | Histórico parcial igual e só com as minhas | OK |  |
| ReparacaoInternaZonasPaginaTests.OPainelDasLinhas_PedidoSozinho_ELinhaSemEscolher_SaoOsMesmosPartials | Painel de linhas parcial igual ao da página inteira | OK |  |
| ReparacaoInternaZonasPaginaTests.OsPedidosComZonas_LeemMenosDaBaseQueAPaginaInteira_NoPostgres | Pedidos por zonas leem menos da base | OK | Salta sem DMO_POSTGRES_CONNECTION. |
| ReparacaoInternaZonasPaginaTests.SemOCabecalho_AResposta_EAPaginaInteiraDeSempre | Sem cabeçalho de zonas vem a página inteira | FRACO | Só vê que a página tem html e ids; não compara com resposta com cabeçalho. Parte do que prova já está nos outros testes da classe. |
| ReparacaoInternaZonasPaginaTests.ZonaDesconhecidaOuDeOutraVista_RespondeComAPaginaInteira (6 casos) | Zona desconhecida responde com a página inteira | OK |  |

### `tests/ShellProof/bq-registo.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| bq-registo.test.cjs::gravar um movimento não mexe no scroll | Gravar movimento de BQ não desloca a página | OK |  |
| bq-registo.test.cjs::o registo aberto fora do ecrã vem à vista | Registo aberto fora do ecrã desliza para a vista | OK |  |

### `tests/ShellProof/calendario-filtros.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| calendario-filtros.test.cjs::a seta do mês leva os filtros; «Todas» tira a máquina | Setas do mês mantêm filtros; Todas tira a máquina | OK |  |
| calendario-filtros.test.cjs::o dia do calendário leva os filtros escolhidos | O dia do calendário leva os filtros | OK |  |

### `tests/ShellProof/cliques-repetidos.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| cliques-repetidos.test.cjs::Duplicar para o lote: página inteira sem janela por cima | Duplicar para o lote abre a página inteira | OK |  |
| cliques-repetidos.test.cjs::Duplicar: três cliques fazem um POST | Duplicar com cliques repetidos: um só POST | OK |  |
| cliques-repetidos.test.cjs::Eliminar: três cliques fazem um POST | Eliminar com cliques repetidos: um só POST | OK |  |
| cliques-repetidos.test.cjs::Ficha do utilizador: ?confirm_delete=1 abre a janela | Link com confirm_delete abre a janela de confirmação | OK |  |
| cliques-repetidos.test.cjs::Ficha do utilizador: Eliminar utilizador abre a janela sem pedido | Eliminar utilizador pede confirmação e elimina uma vez | OK |  |
| cliques-repetidos.test.cjs::Ficha do utilizador: Repor palavra-passe três cliques | Repor palavra-passe com cliques repetidos repõe uma vez | OK |  |
| cliques-repetidos.test.cjs::Registar ferramenta: a mesma recusa duas vezes mostra-se outra vez | Mesma recusa duas vezes mostra-se de novo | OK |  |
| cliques-repetidos.test.cjs::Registar ferramenta: três cliques com rede lenta fazem um POST | Cliques repetidos em Registar ferramenta: um só POST | OK |  |

### `tests/ShellProof/dmo-live-toast.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| dmo-live-toast.test.cjs::ao fim do tempo a mensagem esmorece | Mensagem esmorece e sai ao fim do tempo | OK |  |
| dmo-live-toast.test.cjs::fechado o aviso, a mensagem volta | Mensagem fechada volta na falha seguinte | OK |  |
| dmo-live-toast.test.cjs::os tipos seguem os da casa | Tipos erro, aviso e ok empilham-se | OK |  |
| dmo-live-toast.test.cjs::seis falhas mostram um só aviso | Falhas repetidas mostram um só aviso | OK |  |

### `tests/ShellProof/dmo-suggest-abort.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| dmo-suggest-abort.test.cjs::escrever depressa cancela o pedido anterior | Escrever depressa só mostra sugestões do texto atual | OK |  |
| dmo-suggest-abort.test.cjs::limpar o campo cancela o pedido | Limpar o campo cancela o pedido em voo | OK |  |
| dmo-suggest-abort.test.cjs::sair do campo cancela o pedido | Sair do campo cancela o pedido | OK |  |
| dmo-suggest-abort.test.cjs::um pedido cancelado não mostra erro | Pedido cancelado não mostra erro | OK |  |

### `tests/ShellProof/dmo-suggest-sair.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| dmo-suggest-sair.test.cjs::sair da página cancela o pedido | Sair da página cancela o pedido | OK |  |
| dmo-suggest-sair.test.cjs::trocar a zona 25 vezes deixa um só pagehide | Trocar a zona não acumula ouvintes | OK |  |

### `tests/ShellProof/dmo-suggest.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| dmo-suggest.test.cjs::Enter enquanto a procura nova não responde | Enter não escolhe a linha da lista anterior | OK |  |
| dmo-suggest.test.cjs::Enter na procura da BQ antes de a lista abrir não envia a folha | Enter antes da lista abrir não envia a folha | OK |  |
| dmo-suggest.test.cjs::num filtro sem fonte, o Enter escolhe a primeira opção | Enter num filtro escolhe a primeira opção sem enviar | OK |  |

### `tests/ShellProof/erros-do-servidor.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| erros-do-servidor.test.cjs::duas recusas seguidas uma só mensagem | Recusas repetidas deixam uma só mensagem | OK |  |
| erros-do-servidor.test.cjs::erro do servidor na procura | Erro na procura diz para escrever de novo | OK |  |
| erros-do-servidor.test.cjs::sessão terminada ao gravar | Sessão terminada deixa o escrito e dá link para entrar | OK |  |
| erros-do-servidor.test.cjs::sessão terminada, a procura diz para entrar | Procura sem sessão diz para entrar | OK |  |
| erros-do-servidor.test.cjs::um erro do servidor não substitui o ecrã (vários códigos) | Erro do servidor não substitui o ecrã e diz o que ficou gravado | OK |  |
| erros-do-servidor.test.cjs::uma recusa do servidor deixa o escrito (vários códigos) | Recusa do servidor mantém o escrito e manda recarregar | OK |  |

### `tests/ShellProof/escolha-jobon.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| escolha-jobon.test.cjs::escolher a produção abre a folha com o POST de sempre | Escolher a produção abre a folha | OK |  |
| escolha-jobon.test.cjs::escolher a referência e a máquina troca só a produção | Escolher referência e máquina troca só a produção | OK |  |
| escolha-jobon.test.cjs::no Histórico resposta tardia não repõe a produção | Histórico ignora resposta tardia | OK |  |
| escolha-jobon.test.cjs::o Histórico procura ao escrever | Histórico procura ao escrever sem recarregar | OK |  |
| escolha-jobon.test.cjs::outra referência escrita desativa a produção anterior | Outra referência desativa a produção anterior | OK |  |
| escolha-jobon.test.cjs::resposta tardia não ativa a produção anterior | Resposta tardia não ativa produção velha | OK |  |

### `tests/ShellProof/escrito-perdido.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| escrito-perdido.test.cjs::dropdown da anterior não abre produções dela | Dropdown da referência antiga não abre produções | OK |  |
| escrito-perdido.test.cjs::linhas do texto anterior não escolhem | Linhas do texto anterior não escolhem | OK |  |
| escrito-perdido.test.cjs::o PIN de um utilizador criado fica no canto | O PIN criado fica no canto com a criação seguinte | OK |  |

### `tests/ShellProof/excel-bq-escritas.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| excel-bq-escritas.test.cjs::S03 registo durante escrita lenta | Registo durante a escrita do Excel volta a escrever | OK |  |
| excel-bq-escritas.test.cjs::S03 sem registo novo escreve uma vez | Sem registo novo escreve uma só vez | OK |  |
| excel-bq-escritas.test.cjs::S04 dois separadores um de cada vez | Dois separadores escrevem o Excel um de cada vez | OK |  |
| excel-bq-escritas.test.cjs::sem Web Locks escreve como antes | Sem Web Locks continua a escrever | OK |  |

### `tests/ShellProof/ferramentas-live.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ferramentas-live.test.cjs::3.14 nome técnico propõe o tipo | Nome técnico propõe tipo sem mexer na referência | OK |  |
| ferramentas-live.test.cjs::3.14 sem resposta o tipo fica | Sem resposta o tipo fica e avisa | OK |  |
| ferramentas-live.test.cjs::3.14 só a resposta mais recente conta | Só a resposta mais recente do nome técnico conta | OK |  |
| ferramentas-live.test.cjs::a Referência pede as sugestões do tipo | Referência sugere só do tipo escolhido | OK |  |

### `tests/ShellProof/historico-menu.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| historico-menu.test.cjs::abrir outra linha fecha a primeira | Só um menu aberto de cada vez | OK |  |
| historico-menu.test.cjs::clicar na linha abre o menu com os três destinos | Clicar na linha abre menu com Controlo, Reparação e Folha | OK | Menu nas linhas por clique (sem botões); destino Reparação usa referencia+producao. |
| historico-menu.test.cjs::o script da página foi encontrado | O script do Histórico existe | FRACO | Só verifica que o script foi lido; não prova regra. |
| historico-menu.test.cjs::um clique fora da tabela fecha o menu | Clique fora fecha o menu | OK | É um menu, não painel (clique fora só fecha, design-novo.md). |
| historico-menu.test.cjs::um segundo clique na linha fecha o menu | Segundo clique fecha o menu | OK |  |

### `tests/ShellProof/janela-pequena.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| janela-pequena.test.cjs::um link que abre a janela pequena | Link abre a janela sem mudar o endereço; Cancelar fecha | OK |  |

### `tests/ShellProof/jobon-dados-ferramentas.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| jobon-dados-ferramentas.test.cjs::falha de pedido que já não é o último | Falha de pedido antigo não muda nada | OK |  |
| jobon-dados-ferramentas.test.cjs::resposta anterior tardia não escreve dados | Resposta velha não escreve dados nem avisos | OK |  |
| jobon-dados-ferramentas.test.cjs::resposta tardia não apaga o aviso atual | Resposta tardia não apaga o aviso atual | OK |  |
| jobon-dados-ferramentas.test.cjs::sem resposta ou com erro saem os dados anteriores | Sem resposta saem os dados da escolha anterior | OK |  |

### `tests/ShellProof/jobon-folha.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| jobon-folha.test.cjs::Bloqueada: um clique leva à ficha | Folha bloqueada: um clique leva à ficha | OK |  |
| jobon-folha.test.cjs::CS furos no Editar | Dígitos de furos abertos e fechados vão para o campo | OK |  |
| jobon-folha.test.cjs::Editar: a procura da BQ leva a CM e a MF | Procura da BQ leva a CM e a MF escolhidas | OK |  |
| jobon-folha.test.cjs::Editar: cartão vazio, escolher a CM | Escolher a CM mostra referência e lote | OK |  |
| jobon-folha.test.cjs::Editar: escolher outra ferramenta muda o cartão | Trocar ferramenta muda cartão e ficha | OK |  |
| jobon-folha.test.cjs::Editar: um clique abre a procura; duplo clique leva à ficha | Editar: clique abre a procura, duplo clique a ficha | OK |  |
| jobon-folha.test.cjs::Imprimir abre a informação técnica | Imprimir abre a informação técnica e chama window.print | OK |  |
| jobon-folha.test.cjs::O painel das linhas: Linhas abre, o × esconde | Painel das linhas abre e esconde pelo ×; escolha fica | OK | Não há teste de clique fora a manter o painel aberto. |
| jobon-folha.test.cjs::Produções: lista fechada; abrir cai por cima | Escolher produção muda o select e dispara change | OK |  |

### `tests/ShellProof/jobon-rascunho-envio.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| jobon-rascunho-envio.test.cjs::S01 Descartar rascunho | Descartar apaga o rascunho | OK |  |
| jobon-rascunho-envio.test.cjs::S01 na mesma versão o rascunho volta | Na mesma versão o rascunho volta | OK |  |
| jobon-rascunho-envio.test.cjs::S01 rascunho da versão 3 não substitui a 4 | Rascunho de versão antiga não substitui a nova | OK |  |
| jobon-rascunho-envio.test.cjs::S02 envio sem resposta não perde o escrito | Envio sem resposta não perde o escrito | OK |  |
| jobon-rascunho-envio.test.cjs::S02 folha gravada apaga a cópia | Folha gravada apaga a cópia e o rascunho | OK |  |
| jobon-rascunho-envio.test.cjs::S02 voltar atrás traz o escrito | Voltar atrás traz o que se escreveu | OK |  |

### `tests/ShellProof/jobon-rascunho.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| jobon-rascunho.test.cjs::Descartar rascunho repõe os valores do servidor | Descartar repõe os valores do servidor | OK |  |
| jobon-rascunho.test.cjs::campos do endereço ganham ao rascunho | Campos do endereço ganham ao rascunho | OK |  |
| jobon-rascunho.test.cjs::gravação com sucesso apaga o rascunho | Gravar apaga o rascunho | OK |  |
| jobon-rascunho.test.cjs::rascunhos Novo, Duplicar e Editar não se misturam | Rascunhos de Novo, Duplicar e Editar separados | OK |  |
| jobon-rascunho.test.cjs::recarregar repõe os campos | Recarregar repõe os campos com «Rascunho recuperado» | OK |  |
| jobon-rascunho.test.cjs::sem localStorage a folha funciona | Sem localStorage a folha funciona | OK |  |
| jobon-rascunho.test.cjs::servidor recusa, o rascunho volta | Recusa mantém o rascunho | OK |  |

### `tests/ShellProof/jobon-tool-open.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| jobon-tool-open.test.cjs::Cartão da ferramenta: nome e Abrir ficha | Cartão da ferramenta segue a escolhida | OK |  |

### `tests/ShellProof/jobon-tool-picker.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| jobon-tool-picker.test.cjs::Criar BQ abre o registo com a máquina | Criar BQ leva a máquina da folha | OK |  |
| jobon-tool-picker.test.cjs::Editar sem Retirar, Trocar e escolher | Trocar e escolher ferramenta na folha | OK |  |
| jobon-tool-picker.test.cjs::Mostrar todas as ferramentas | Catálogo inteiro só quando marcado | OK |  |
| jobon-tool-picker.test.cjs::Novo Job On: Retirar na CM e na BQ | Retirar limpa a CM e a BQ, não o MF | OK |  |
| jobon-tool-picker.test.cjs::a procura de cada tipo pede sugestões da máquina (3 casos) | Procura por tipo pede sugestões da máquina | OK |  |
| jobon-tool-picker.test.cjs::a página desenha os cartões CM, MF e BQ | Página tem os três cartões e o seletor | FRACO | Só verifica lista de nomes e <script> no cshtml. |

### `tests/ShellProof/painel-apoio-alteracoes.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| painel-apoio-alteracoes.test.cjs::painel com alterações por guardar | Painel com alterações avisa e o apoio não abre por cima | OK | Cumpre «um painel de cada vez». |

### `tests/ShellProof/painel-estrutura.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| painel-estrutura.test.cjs::Escape fecha e DmoPainel abre por código | Escape fecha o painel | OK |  |
| painel-estrutura.test.cjs::Painel com confirmar pede antes de fechar | Com alterações pede confirmação antes de fechar | OK |  |
| painel-estrutura.test.cjs::o botão abre, o Fechar fecha e o foco volta | Painel abre, fecha e devolve o foco | OK | O botão de saída chama-se «Fechar» (código) e a memória diz «Voltar». |

### `tests/ShellProof/painel-propria-pagina.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| painel-propria-pagina.test.cjs::link para a própria página fica na página | Link para a própria página não abre painel | OK | ÓRFÃO: não está na lista test:js do package.json, nunca corre no CI. |

### `tests/ShellProof/painel-um-de-cada-vez.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| painel-um-de-cada-vez.test.cjs::apoio fecha um dmo-painel aberto | Abrir o apoio fecha o outro painel | OK |  |
| painel-um-de-cada-vez.test.cjs::apoio: cliques repetidos não recarregam | Painel de apoio não recarrega com cliques repetidos | OK |  |
| painel-um-de-cada-vez.test.cjs::o mesmo botão não abre outro por cima | Um painel de cada vez | OK |  |

### `tests/ShellProof/pegamentos-previsao.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| pegamentos-previsao.test.cjs::3.12 pré-visualização é a do servidor (casos JSON) | Pré-visualização igual à do servidor | OK |  |
| pegamentos-previsao.test.cjs::3.12 sem corredor nada fica fora | Sem nominal nada fica fora | OK |  |
| pegamentos-previsao.test.cjs::3.12 valor ilegível | Valor ilegível não mostra contas | OK |  |

### `tests/ShellProof/pegamentos-registo.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| pegamentos-registo.test.cjs::(30 testes: Enter troca só o cartão, corrigir/apagar, × confirma, recusas, fila, sem ligação, aviso no canto, foco) | Registo de leituras dos Pegamentos sem perder o escrito | OK | Lido só pelos títulos; cobertura densa, sem fracos óbvios. |

### `tests/ShellProof/peso-calote.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| peso-calote.test.cjs::escrever outra vez invalida o pedido | Escrever invalida o pedido já enviado | OK |  |
| peso-calote.test.cjs::resposta de calote anterior tardia | Resposta velha da calote não substitui a atual | OK |  |
| peso-calote.test.cjs::sem resposta saem os valores da calote | Sem resposta saem os valores da calote anterior | OK |  |

### `tests/ShellProof/peso-comparacao.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| peso-comparacao.test.cjs::Comparar pesos e Guardar comparação trocam só as zonas | Comparar e guardar trocam só as zonas | OK |  |
| peso-comparacao.test.cjs::a ligação cai ao guardar a comparação (vários casos) | Ligação a cair não guarda duas comparações | OK |  |

### `tests/ShellProof/peso-live.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| peso-live.test.cjs::(20 testes: registar troca zonas, recusa mantém escrito, operador rápido, cliques repetidos, sem ligação, comparação, Supomos, sem rede, FE-8) | Registo de leituras do Peso sem perder nem duplicar | OK |  |
| peso-live.test.cjs::a folha de teste usa as marcas das zonas | A folha de teste usa as marcas do Peso.cshtml | FRACO | Procura texto no cshtml (ordem e marcas), não comportamento. |
| peso-live.test.cjs::preview preserves comparison (2 casos) | Pré-visualização mantém a comparação | OK |  |

### `tests/ShellProof/posto-sair.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| posto-sair.test.cjs::com mais tempo nas Definições espera esse tempo | Tempo vem das Definições | OK |  |
| posto-sair.test.cjs::em uso avisa o servidor uma vez por minuto | Aviso ao servidor no máximo 1 vez por minuto | OK |  |
| posto-sair.test.cjs::escrever num campo conta como uso | Escrever conta como uso | OK |  |
| posto-sair.test.cjs::fora do posto não faz nada | Login normal nunca sai | OK |  |
| posto-sair.test.cjs::quem está a usar não é posto fora | Quem usa a página não sai | OK |  |
| posto-sair.test.cjs::sem uso durante os minutos, sai e volta ao posto | Sem uso o Posto sai | OK |  |

### `tests/ShellProof/programada-enter.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| programada-enter.test.cjs::Enter na Referência e no Lote submete o rpFiltros (2 casos) | Enter na Reparação Programada submete os filtros | OK |  |
| programada-enter.test.cjs::o script do Enter está na página | Script do Enter existe | FRACO | Só assert.ok(script). |
| programada-enter.test.cjs::um Enter já tratado não submete outra vez | Enter já tratado não submete de novo | OK |  |

### `tests/ShellProof/puncoes-numero.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| puncoes-numero.test.cjs::10 e 11 não se juntam em 1011 | Números seguidos gravam um a um | OK |  |
| puncoes-numero.test.cjs::42 e 43 com Enter e 44 sem Enter | Sem Enter o número fica no campo | OK |  |
| puncoes-numero.test.cjs::número já registado pede o lote | Número já registado pede o lote da punção | OK |  |
| puncoes-numero.test.cjs::o que se apagou não volta | Apagado não volta ao campo novo | OK |  |
| puncoes-numero.test.cjs::o que se escreve sem Enter durante a gravação | Escrito durante gravação fica no campo novo | OK |  |
| puncoes-numero.test.cjs::sem rede o número fica | Sem rede o número fica no campo | OK |  |

### `tests/ShellProof/troca-escolhas.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| troca-escolhas.test.cjs::OK, NOK e OK: fica a última escolha | Fica gravada a última escolha OK/NOK | OK |  |
| troca-escolhas.test.cjs::com o rato grava logo | Rato grava logo; Enter sem nada não regrava | OK |  |
| troca-escolhas.test.cjs::comentário aberto durante o OK/NOK | Comentário aberto fica aberto com texto e cursor | OK |  |
| troca-escolhas.test.cjs::data escrita só aplica com Enter | Data escrita só aplica com Enter ou ao sair | OK |  |
| troca-escolhas.test.cjs::janela fechada numa zona trocada | Janela fechada continua fechada | OK |  |
| troca-escolhas.test.cjs::o mesmo envio repetido grava uma vez | Envio repetido grava uma vez | OK |  |
| troca-escolhas.test.cjs::setas na Produção da Associar Job On | Setas não associam a primeira produção | OK |  |
| troca-escolhas.test.cjs::setas só gravam com Enter ou ao sair | Setas não gravam sozinhas | OK |  |

### `tests/ShellProof/troca-parcial.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| troca-parcial.test.cjs::(~70 testes do «atualizar só o que muda»: trocar só zonas, duplo envio uma vez, recusa, fila, ligação a cair sem repetir POST, janelas, paginação, calendário, zonas do servidor, Revisão, FE-8) | Atualizar só o que muda sem repetir POST nem perder o escrito | OK | Lido só pelos títulos. «o clique no dia escolhido» e «duplo envio» parcialmente repetidos com a fila (sem prova de duplicação). |

### `tests/ShellProof/vistoria-frontend.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| vistoria-frontend.test.cjs::F2 erro do servidor avisa | Erro do servidor avisa que não foi possível procurar | OK |  |
| vistoria-frontend.test.cjs::F2 filtro ao escrever troca os resultados | Filtro ao escrever troca os resultados | OK |  |
| vistoria-frontend.test.cjs::F2 sem rede repete a procura | Sem rede repete a procura ao voltar | OK |  |
| vistoria-frontend.test.cjs::F2 sessão terminada avisa com link Entrar | Sessão terminada avisa e dá link | OK |  |
| vistoria-frontend.test.cjs::F3 o escrito fica guardado com o dono | Rascunho guardado com o dono | OK |  |
| vistoria-frontend.test.cjs::F3 rascunho com mais de um dia | Rascunho com mais de um dia não volta | OK |  |
| vistoria-frontend.test.cjs::F3 rascunho de outra pessoa sai do browser | Rascunho de outra pessoa não aparece | OK |  |
| vistoria-frontend.test.cjs::F3 rascunho de quem abre volta | Rascunho do dono volta | OK |  |

### `tests/ShellProof/voltar-caminho.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| voltar-caminho.test.cjs::as folhas do controlo contam uma a uma | Folhas do Controlo contam como passos | OK |  |
| voltar-caminho.test.cjs::mesma página com outros filtros não é passo | Filtros ou guardar não contam como passo | OK |  |
| voltar-caminho.test.cjs::primeira página usa a anterior do site | Primeira página volta à anterior do site | OK |  |
| voltar-caminho.test.cjs::páginas do painel deslizante não entram | Páginas do painel não entram no caminho | OK |  |
| voltar-caminho.test.cjs::três páginas seguidas, Voltar recua uma de cada vez | Voltar recua uma página de cada vez | OK |  |

### `tests/ShellProof/voltar.test.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| voltar.test.cjs::depois de guardar ou do Histórico a origem mantém-se | Origem do Voltar mantém-se após guardar | OK |  |
| voltar.test.cjs::sem origem do mesmo site fica o link do servidor | Sem origem fica o link do servidor | OK | Sobrepõe-se em parte com voltar-caminho (não duplicado). |
| voltar.test.cjs::vindo de outra página, Voltar volta com os filtros | Voltar regressa com os filtros | OK |  |

### `tests/visual/ajuda_junto_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ajuda_junto_browser.py (script, 3 casos x claro/escuro) | Ajuda encostada ao que explica, sem círculos de dica | OK | Proíbe a bola de ajuda nestes 3 sítios; a regra do Diogo (28/10 17:40) é só não a pôr ao lado do triângulo de minimizar, o que estes casos respeitam. |

### `tests/visual/boquilhas_catalog_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| boquilhas_catalog_browser.py (script) | Catálogo das Boquilhas: criar, ficha, lista só ao pesquisar | OK | Corre no CI. |

### `tests/visual/calendario_parcial_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| calendario_parcial_browser.py (script) | Calendário e seleção por GET sem recarregar | FRACO | ÓRFÃO (não está no ci-local.sh); precisa de servidor stress local, só confirma erros. |

### `tests/visual/definicoes_parcial_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| definicoes_parcial_browser.py (script) | Definições gravam sem recarregar, um POST | OK | ÓRFÃO (não corre no CI; precisa de servidor stress local). |

### `tests/visual/design-review.cjs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| design-review.cjs (script) | Inspeciona o HTML exportado: largura e erros de JS | OK | Não é teste: ferramenta manual de inspeção do design (DESIGN.md); não corre no CI (fica). |

### `tests/visual/dmo_chromium.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| dmo_chromium.py (módulo auxiliar) | Escolhe o Chromium dos scripts | OK | Não é teste: auxiliar dos scripts do browser (fica). |

### `tests/visual/ecra_pequeno_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ecra_pequeno_browser.py (script) | Peso e Pegamentos sem scroll lateral em ecrã pequeno | FRACO | Mede aspeto (scroll lateral, posições), regra 10/13. Corre no CI. |

### `tests/visual/excel_bq_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| excel_bq_browser.py (script) | Excel das Boquilhas: só envia se a versão mudou | OK | Corre no CI; falha local 2 testes (testes-postgres-nuvem.md). |

### `tests/visual/ferramentas_parcial_servidor_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ferramentas_parcial_servidor_browser.py (script) | Filtro e página das Ferramentas pelo endpoint parcial | FRACO | ÓRFÃO; precisa de servidor stress local. |

### `tests/visual/ficha_avisos_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ficha_avisos_browser.py (script) | Avisos da ficha não empurram a página | FRACO | ÓRFÃO; mede posições; precisa de servidor stress local. |

### `tests/visual/ficha_botoes_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ficha_botoes_browser.py (script) | Botões no canto superior direito de cada cartão | FRACO | ÓRFÃO; mede posições (regra do Diogo 18/10) e precisa de servidor stress local. |

### `tests/visual/gravacoes_zonas_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| gravacoes_zonas_browser.py (script) | Após gravar, o GET pede só as zonas | OK | ÓRFÃO; escreve dados e precisa de servidor stress local. |

### `tests/visual/historicos_parcial_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| historicos_parcial_browser.py (script) | Paginação dos históricos só pede o parcial | OK | ÓRFÃO; precisa de servidor stress local. |

### `tests/visual/jobon_folha_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| jobon_folha_browser.py (script) | Folha do Job On: grelha, notas e O que mudou | FRACO | Maior parte é geometria; nota ampliada fecha com clique fora (confirmar que é janela e não painel). Corre no CI. |

### `tests/visual/nok_agente_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| nok_agente_browser.py (script) | Falhas de ecrã do teste do agente: dica, Lote abc, botões | FRACO | Mistura geometria com 1 efeito («abc» enviado à app). Corre no CI. |

### `tests/visual/paginacao_parcial_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| paginacao_parcial_browser.py (script) | Seguinte e anterior trocam só os resultados | OK | Corre no CI. |

### `tests/visual/painel_cores_linhas_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| painel_cores_linhas_browser.py (script) | Cartões do painel com cores das linhas | FRACO | Compara cores calculadas (aspeto). Corre no CI. |

### `tests/visual/painel_lateral_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| painel_lateral_browser.py (script) | Texto legível ao passar o rato no painel lateral | FRACO | Mede contraste, aspeto. Corre no CI. |

### `tests/visual/pasta_pdf_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| pasta_pdf_browser.py (script) | Pasta dos PDFs: guardar, duplo clique um só ficheiro | OK | Corre no CI. |

### `tests/visual/puncoes_tabela_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| puncoes_tabela_browser.py (script) | Larguras da tabela das punções estáveis | FRACO | Só larguras de colunas. Corre no CI. |

### `tests/visual/resumo_parcial_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| resumo_parcial_browser.py (script) | Paginação do Resumo pede só o parcial | OK | ÓRFÃO; precisa de servidor stress local. |

### `tests/visual/revisao_aprovar_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| revisao_aprovar_browser.py (script) | Aprovar e Não aprovar gravam sem recarregar | OK | ÓRFÃO, apesar de «Aprovar» ser fluxo principal (regra 13); precisa de servidor stress local. |

### `tests/visual/revisao_parcial_servidor_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| revisao_parcial_servidor_browser.py (script) | Filtros da Revisão preservam decisão e nota abertas | OK | ÓRFÃO; precisa de servidor stress local. |

### `tests/visual/ri_hora_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| ri_hora_browser.py (script) | Hora à direita do Dia, mesma linha, HH:mm | FRACO | Geometria (mesmo topo) e atributo form=; o valor gravado já é testado em C#. Corre no CI. |

### `tests/visual/tecnica_alinhada_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| tecnica_alinhada_browser.py (script) | Grupos da Informação técnica alinhados | FRACO | ÓRFÃO; só geometria. |

### `tests/visual/validate_pdf_evidence.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| validate_pdf_evidence.py (script) | Valida o conteúdo dos PDFs de Controlo | OK | Fora do CI por desenho (testes.md: precisa de PDF de referência do Diogo). |


## Testes novos da auditoria de cobertura (28/10/2026, DMO-588)

173 testes novos para as correções DMO-380…577 que a auditoria do agente externo marcou «sem teste» ou «de raspão». Todos OK. A última coluna diz a correção que cada um guarda.

### `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqSaldoNoHistoricoCoberturaA2Tests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| BqSaldoNoHistoricoCoberturaA2Tests.Historico_BqQueSaiuNumJobOnEVoltaNoutro_NaoMostraSaldo_MesmoComUmSaldoAntigoGravado | Histórico ignora saldo antigo gravado e recalcula | OK | DMO-574 |
| BqSaldoNoHistoricoCoberturaA2Tests.Historico_EntraMaisDoQueSaiu_MostraOSaldoNaEntrada_ENoPdf | Excedente mostra saldo na entrada, no ecrã e no PDF | OK | DMO-574 |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoPdfGeneratorTests.CoberturaBPdf.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoPdfGeneratorCoberturaBTests.PdfDoPeso_NaoRepeteRotulosNemExplicacoesRetiradas | O PDF do Peso já não leva «Diferença indisponível» nem as explicações retiradas. | OK | DMO-526 |
| PesoPdfGeneratorCoberturaBTests.ProducaoAnterior_ComMediaEModa_OPdfDizAsDuasEOsDiasDaModa | O PDF do Peso mostra média e moda da produção anterior, com os dias. | OK | DMO-426 |
| PesoPdfGeneratorCoberturaBTests.ProducaoAnterior_SemMediaNemModa_UsaOPesoGuardado_ESemDizerMedia | Sem média nem moda, o PDF usa o peso guardado da produção anterior. | OK | DMO-534 |

### `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesPosicoesPdfCoberturaB.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PuncoesPosicoesPdfCoberturaBTests.PdfDasPuncoes_AsLinhasSaoAsGotasDaLinha_ETresGotasDeixamMaisNaReserva | O PDF das punções tem uma linha por gota e a reserva certa. | OK | DMO-508 |
| PuncoesPosicoesPdfCoberturaBTests.PdfDasPuncoes_SaiNumaSoFolhaA4_ComAReservaCheiaOuNao | O PDF das punções sai numa só folha A4, também com a reserva cheia. | OK | DMO-511 |
| PuncoesPosicoesPdfCoberturaBTests.PdfDasPuncoes_SaiNumaSoFolhaA4_ComAReservaCheiaOuNao | O PDF das punções cabe numa só folha A4 deitada. | OK | DMO-516 |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CoberturaA2TrincoCorrigir.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PostgresMigrationIntegrationTests.ApagarUmaLeituraDoPeso_EsperaPelaLeituraNovaEmCurso_ENaoDeixaBuracoNasPosicoes | Apagar leitura espera pela leitura nova e não deixa buracos | OK | DMO-546 |
| PostgresMigrationIntegrationTests.CorrigirUmaLeituraDoPeso_EsperaPelaLeituraNovaEmCurso | Corrigir leitura espera pela leitura nova em curso | OK | DMO-546 |

### `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.CoberturaBTrincoDoPeso.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PostgresMigrationIntegrationTests.CorrigirEApagarUmaLeituraDoPeso_EsperamPeloTrincoDeQuemRegista | Corrigir e apagar uma leitura do Peso esperam pelo trinco do Peso. | OK | DMO-546 |

### `tests/DMO.Alpha.Infrastructure.Tests/Production/CoberturaA1ProducaoEmCursoTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaA1ProducaoEmCursoTests.ComOMesmoDiaDeInicio_FicaALancadaPorUltimo | Em curso e experiência de um dia: ComOMesmoDiaDeInicio_FicaALancadaPorUltimo | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.DuasExperienciasDeUmDiaNaMesmaLinha_FicaAUltimaLancada | Em curso e experiência de um dia: DuasExperienciasDeUmDiaNaMesmaLinha_FicaAUlti | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.DuasProducoesQueCobremODiaNaMesmaLinha_FicaAQueComecouPorUltimo | Em curso e experiência de um dia: DuasProducoesQueCobremODiaNaMesmaLinha_FicaAQ | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.EntradasPorDia_ExperienciaDeUmDiaEntraAoLadoDaProducaoDessaLinha | Em curso e experiência de um dia: EntradasPorDia_ExperienciaDeUmDiaEntraAoLadoD | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.EntradasPorDia_PelaOrdemDeEntrada_ESemAsQueNaoTemDiaDeInicio | Em curso e experiência de um dia: EntradasPorDia_PelaOrdemDeEntrada_ESemAsQueNa | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.EntradasPorDia_UmaPorLinhaEPorDia_AUltimaLancada | Em curso e experiência de um dia: EntradasPorDia_UmaPorLinhaEPorDia_AUltimaLanc | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.ExperienciaDeUmDiaSozinha_AparecenaLinhaSemProducao | Em curso e experiência de um dia: ExperienciaDeUmDiaSozinha_AparecenaLinhaSemPr | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.ExperienciaDeUmDia_NaoTiraAProducaoNemSubstituiAEscolhaDaLinha | Em curso e experiência de um dia: ExperienciaDeUmDia_NaoTiraAProducaoNemSubstit | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.ExperienciaDeUmDia_SoApareceNoSeuDia | Em curso e experiência de um dia: ExperienciaDeUmDia_SoApareceNoSeuDia | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.ExperienciaQueEntraESaiNoDia_FicaAoLadoDaProducaoDaLinha_SemATirar | Em curso e experiência de um dia: ExperienciaQueEntraESaiNoDia_FicaAoLadoDaProd | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.ExperienciaSemFim_NaoEDeUmDia | Em curso e experiência de um dia: ExperienciaSemFim_NaoEDeUmDia | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.OFimCobreOUltimoDia_ESoNesse_NoLimiteELogoAbaixoELogoAcima | Em curso e experiência de um dia: OFimCobreOUltimoDia_ESoNesse_NoLimiteELogoAba | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.ProducaoSemDiaDeInicio_NaoEstaEmCurso | Em curso e experiência de um dia: ProducaoSemDiaDeInicio_NaoEstaEmCurso | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.ProducaoSemFim_CobreOsDiasDepoisDoInicio_ENaoOsAntes | Em curso e experiência de um dia: ProducaoSemFim_CobreOsDiasDepoisDoInicio_ENao | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.QuadroDoDia_ExperienciaDeDoisDias_NaoFicaAoLado_ECompeteComAProducaoDaLinha | Em curso e experiência de um dia: QuadroDoDia_ExperienciaDeDoisDias_NaoFicaAoLa | OK | DMO-459 |
| CoberturaA1ProducaoEmCursoTests.QuadroDoDia_ExperienciaDeUmDiaFicaAoLadoDaProducao_EVemMarcadaComoExperiencia | Em curso e experiência de um dia: QuadroDoDia_ExperienciaDeUmDiaFicaAoLadoDaPro | OK | DMO-459 |

### `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasDuplicarCoberturaA2Tests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| FerramentasDuplicarCoberturaA2Tests.DuplicarParaLoteNovo_LevaOLinkMcaliperEAFabricaDeOrigem_ESemUtilizacao | Duplicar ferramenta leva link McAliper e fábrica, sem utilização | OK | DMO-403 |

### `tests/DMO.Alpha.Web.Tests/Admin/CoberturaCRegistosPorUtilizadorTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaCRegistosPorUtilizadorTests.OFiltroDosDias_DeEAteIncluemOsDiasEscolhidos_ESoElesTrazemORegistoDoUltimoMinuto | Filtro De/Até inclui os dias, até ao último minuto | OK | DMO-392 |
| CoberturaCRegistosPorUtilizadorTests.OsRegistos_VemDosMaisRecentesParaOsMaisAntigos_ComOQueCorrigiuOuAnulou | Registos do mais recente ao mais antigo, com correções | OK | DMO-392 |

### `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.CoberturaA2Exporta.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| AdminUsersAndTemplatesPagesTests.ListaDosUtilizadores_ComMaisDe25_ParaOBrowser | Exporta lista de utilizadores longa para medir no browser | OK | DMO-447 |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2FiltroLote.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| BoquilhasRegistoPageTests.FichaDaBoquilha_TemOsBotoesNoTopo_ESeguemOsQuatroMiniCartoesDeDados | Ficha da BQ tem botões no topo antes dos mini-cartões | OK | DMO-404 |
| BoquilhasRegistoPageTests.ProcurarBoquilha_OFiltroLoteVemLogoDepoisDaReferencia_EFiltraPorEsseLote | Filtro Lote no catálogo de BQ vem após referência e filtra | OK | DMO-404 |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2LinhaCor.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| BoquilhasRegistoPageTests.PainelDasMaquinas_CadaCartaoLevaALinhaDele_ESoAsLinhasComCor | Cada cartão do painel leva a sua linha; só as com cor | OK | DMO-381 |

### `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.CoberturaA2SemJobOn.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| BoquilhasRegistoPageTests.RegistarSemJobOn_ALinhaVemJaEscolhidaComAUltimaOndeABqTeveMovimentos | Registar sem Job On pré-escolhe a última linha com movimentos | OK | DMO-572 |
| BoquilhasRegistoPageTests.RegistarSemJobOn_UmaBqSemLinhasNaFicha_MostraTodasAsLinhasSemNenhumaEscolhida | BQ sem linhas na ficha mostra todas, nenhuma escolhida | OK | DMO-572 |

### `tests/DMO.Alpha.Web.Tests/CoberturaA1EntradasDaSemanaTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaA1EntradasDaSemanaTests.Faixa_ExperienciaQueEntraESaiNoMesmoDia_FicaAoLadoDaProducaoDessaLinha | Faixa Planeamento da semana: ExperienciaQueEntraESaiNoMesmoDia FicaAoLadoD | OK | DMO-506 |
| CoberturaA1EntradasDaSemanaTests.Faixa_PelaOrdemEmQueEntram | Faixa Planeamento da semana: PelaOrdemEmQueEntram | OK | DMO-506 |
| CoberturaA1EntradasDaSemanaTests.Faixa_SoTemOQueEntraDeHojeAteUmaSemana_OQueJaEntrouSai | Faixa Planeamento da semana: SoTemOQueEntraDeHojeAteUmaSemana OQueJaEntrou | OK | DMO-506 |
| CoberturaA1EntradasDaSemanaTests.Faixa_VariasAEntrarNaMesmaLinhaNoMesmoDia_SoAUltimaLancada | Faixa Planeamento da semana: VariasAEntrarNaMesmaLinhaNoMesmoDia SoAUltima | OK | DMO-506 |
| CoberturaA1EntradasDaSemanaTests.Gaveta_AsEmCurso_UmaPorLinha_VemAntesDasQueEntram | Gaveta Linhas da folha: AsEmCurso UmaPorLinha VemAntesDasQueEntram | OK | DMO-506 |
| CoberturaA1EntradasDaSemanaTests.Gaveta_ExperienciaDeUmDia_NaoEEmCurso | Gaveta Linhas da folha: ExperienciaDeUmDia NaoEEmCurso | OK | DMO-506 |

### `tests/DMO.Alpha.Web.Tests/CoberturaBExportaParaOBrowserTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaBExportaParaOBrowserTests.ExportaAsDefinicoesDoControlo | Guarda as Definições do Controlo para medir as duas colunas. | OK | DMO-467 |
| CoberturaBExportaParaOBrowserTests.ExportaOLoginComAPalavraPasseErrada | Guarda o login com erro para medir o escuro e o erro discreto. | OK | DMO-505 |
| CoberturaBExportaParaOBrowserTests.ExportaORegistoDoArmazemComUmaFerramentaAberta | Guarda o Registo do Armazém para medir o Registar junto ao Entrada. | OK | DMO-487 |

### `tests/DMO.Alpha.Web.Tests/CoberturaCContaETopoTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaCContaETopoTests.ADuracaoDaSessao_OferecePeloMenosUmaHoraAteVinteEQuatro_ESoGravaAsPermitidas_SoDeQuemAEscolhe | Duração da sessão: 1 a 24 horas, só valores permitidos | OK | DMO-398 |
| CoberturaCContaETopoTests.AFichaDoUtilizador_TemOVoltarPorCimaDoTitulo_ENaoRepeteONumeroDeOperadorNosFactos | Ficha: Voltar acima do título, sem número repetido | OK | DMO-480 |
| CoberturaCContaETopoTests.AMinhaConta_EscondeOEmailInventadoDoLoginPorNumero_EMostraOEmailRealDoAdmin | A conta esconde o email inventado e mostra o real | OK | DMO-480 |
| CoberturaCContaETopoTests.ASessaoNova_DuraAsHorasQueOUtilizadorEscolheu_ESemEscolhaDuraUmaHora | A sessão dura as horas escolhidas; sem escolha, uma hora | OK | DMO-398 |
| CoberturaCContaETopoTests.OTopo_MostraONomeEOTituloDoOperadorAntesDoSair_ESemTituloOAdminSoTemONome | Topo mostra nome e título antes do Sair | OK | DMO-414 |

### `tests/DMO.Alpha.Web.Tests/CoberturaCInstalarPaginaTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaCInstalarPaginaTests.ABaseSemAdmin_MostraOFormularioDaPrimeiraInstalacao | Base sem admin mostra o formulário de instalação | OK | DMO-382 |
| CoberturaCInstalarPaginaTests.ComOCodigoCerto_CriaOAdmin_VaiParaOLogin_ADepoisDeFeitaJaNaoServe | Código certo cria o admin; depois a página fecha | OK | DMO-382 |
| CoberturaCInstalarPaginaTests.SemOLoginProprio_NaoHaPrimeiraInstalacao_ELevaAoLogin | Sem login próprio não há primeira instalação | OK | DMO-382 |
| CoberturaCInstalarPaginaTests.UmCampoMalPreenchido_VoltaComOAviso_GuardaOEmailEscrito_ENadaSeCria | Campo inválido avisa, guarda o email e não cria nada | OK | DMO-382 |

### `tests/DMO.Alpha.Web.Tests/CoberturaCPostoDaReparacaoTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaCPostoDaReparacaoTests.OBotaoReparacaoDoLogin_AbreOPosto_EOPostoTemOVoltarAoLogin | Botão Reparação do login abre o posto, com Voltar | OK | DMO-436 |
| CoberturaCPostoDaReparacaoTests.OPinGuardadoPeloAdmin_FicaEmHash_ESoComEleOReparadorEntraNoPosto | PIN do posto guardado em hash; só com ele se entra | OK | DMO-429 |
| CoberturaCPostoDaReparacaoTests.QuemNaoTemAReparacaoInterna_TemPinMasNaoApareceNoPosto | Sem Reparação Interna não aparece no posto | OK | DMO-429 |
| CoberturaCPostoDaReparacaoTests.UmPinNovoSubstituiOAnterior_ENaoSeAceitaUmPinForaDosQuatroAosDozeAlgarismos | PIN novo substitui o antigo; só 4 a 12 algarismos | OK | DMO-429 |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CoberturaBExporta.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoComparacaoPageTests.Comparacao_ComBase_TemALinhaDestacada_ParaOBrowser | Guarda a Comparação de Peso para medir a cor da linha. | OK | DMO-469 |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.CoberturaBUmaLinha.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoComparacaoPageTests.Comparacao_ComecaComUmaSoLinhaDeLeitura_EDepoisDeCompararVoltaComAsEscritas | A comparação abre com uma só linha de leitura. | OK | DMO-407 |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaA2Abrir.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_AbrirLevaAoPesoDaProducaoEscolhidaNoSelect | Abrir produção anterior leva ao peso da produção escolhida | OK | DMO-402 |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaA2ResumoLongo.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.Resumo_ComPesoDaProducaoAnteriorLongo_ParaOBrowser | Exporta resumo com peso anterior longo para o browser | OK | DMO-479 |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBAgrupamento.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBCartao.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_NomeSoNoTitulo_BotaoAbrir_EInicioEFimNoCartaoDoPeso | Produção anterior: nome só no título, botão Abrir, datas no cartão do peso. | OK | DMO-504 |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBExporta.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.Peso_NoPainel_TemOsNumerosEALeituraNova_ParaOBrowser | Guarda o Peso no painel do Resumo para medir a faixa e o Registar. | OK | DMO-502 |

### `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.CoberturaBResumo.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PesoFolhaProducaoAnteriorPageTests.Resumo_TabelaDoPeso_TemAsColunasValorGEPercentagem_EACadaLinhaAsDuasDiferencas | A tabela do Peso no Resumo tem as colunas Valor, g e %; a linha da moda mostra «X de Y dias» em vez da diferença (mudado a 29/10/2026, DMO-673). | OK | DMO-496, DMO-673 |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaA1Aspeto.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.ResumoComPainelDeApoioEBotaoDosPegamentos_ExportadoParaOBrowser | Exporta o Resumo com painel de apoio e botão dos Pegamentos | OK | DMO-485 |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaA2Revisao.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.ConsultaDosPegamentos_VoltarPorCimaDoTitulo_SemOQuadroLeituras_EOMapaDepoisDasTabelas | Consulta de pegamentos: voltar acima do título, mapa no fim | OK | DMO-483 |
| ControloCapabilitiesCutHPageTests.Revisao_TemOTituloRevisao_EAsConsultasJuntoAoTituloDoResumo | Revisão tem título Revisão e consultas junto ao resumo | OK | DMO-483 |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBExporta.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.PesoSemJobOn_ComCmEBq_ParaOBrowser | Guarda o Peso sem Job On para medir a faixa das ferramentas. | OK | DMO-473 |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBLoteCm.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.PesoSemJobOn_CmComVariosLotes_PedeOLote_ESoResolveQuandoEscolhido | Uma CM com vários lotes pede o lote e só resolve ao escolhê-lo. | OK | DMO-400 |

### `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.CoberturaBVoltar.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| ControloCapabilitiesCutHPageTests.PaginasDeRegisto_TemOVoltarAoResumoDaProducao_AntesDoTitulo | Peso, Pegamentos e Punções têm «Voltar» ao Resumo antes do título. | OK | DMO-460 |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaA2Ficha.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| FerramentasPagesTests.Ficha_ParaOBrowser_CabeNoEcraSemDeslizar | Exporta a ficha da ferramenta para medir no browser | OK | DMO-431 |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaA2FiltroLote.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| FerramentasPagesTests.Pesquisar_OFiltroLoteVemLogoDepoisDaReferencia_EFiltraPorEsseLote | Filtro Lote nas ferramentas vem após referência e filtra | OK | DMO-404 |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CoberturaCLinhasCompativeis.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| FerramentasPagesTests.Registar_DaUmaCaixaPorLinhaComOCodigoDela_EVaiParaOBrowser | Uma caixa por linha compatível, com o código dela | OK | DMO-405 |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Browser.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| JobOnPageTests.Folha_Cheia_ComOitoCalibresENotasCompridas_ExportadaParaOBrowser | Exporta folha cheia para o browser | OK | DMO-495 |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaA1Folha.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| JobOnPageTests.Duplicar_TrazOCabecalhoDaOrigem_OPuEACalote_EGravaAProducaoNovaComEles | Duplicar traz cabeçalho, PU e calote da origem | OK | DMO-395 |
| JobOnPageTests.Editar_LoteDoAnEscritoNoEditar_GravaSeEFicaComoLoteDoAn | Lote do AN escrito no Editar fica gravado | OK | DMO-430 |
| JobOnPageTests.Editar_LoteDoAnVazioMostraODaBqComoSugestao_EGuardarOutraCoisaNaoOCopiaParaABase | Editar mostra o lote da BQ só como sugestão | OK | DMO-430 |
| JobOnPageTests.Folha_CaloteDoTp_SemCaloteDaProducaoMostraABaciaPs_ComElaMandaACalote | Calote do TP: bacia PS ou calote da produção | OK | DMO-395 |
| JobOnPageTests.Folha_LoteDoAnEscritoAMao_FicaEMantemSeQuandoATrocaDeBq | Lote do AN escrito à mão mantém-se na troca | OK | DMO-430 |
| JobOnPageTests.Folha_LoteDoAnSemEscrever_ESempreODaBq_NoEcraENaImpressao_MesmoDepoisDeTrocarAsBq | Lote do AN segue o da BQ, mesmo após troca | OK | DMO-430 |
| JobOnPageTests.Folha_NaoTemOContadorDeSessaoNemOBotaoDeDesbloquearDoPainel | Folha sem contador de sessão nem desbloquear | OK | DMO-454 |
| JobOnPageTests.Folha_PosicaoDoArmazem_ELinkParaOArmazemDaFerramenta_SoParaQuemTemOArmazem | Posição do armazém com ligação só com permissão | OK | DMO-557 |
| JobOnPageTests.Imprimir_UsaAsPincasDoCartaoPiInteiras_ComODiametro_SemALinhaTipo | Imprimir usa as pinças inteiras do cartão PI | OK | DMO-478 |
| JobOnPageTests.Painel_FolhaNoPainel_EUmResumoComAbrirFolha_SemOsCartoesDaFolha | Folha no painel é resumo com Abrir folha | OK | DMO-521 |
| JobOnPageTests.Planeamento_FaixaDoJobOn_TemOQueEntraEstaSemana_ENaoOQueJaEntrou | Faixa do Job On só com o que entra | OK | DMO-506 |

### `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CoberturaBExperiencia.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| JobOnPageTests.FolhaNova_Duplicar_NaoTemOBotaoExperiencia | O Duplicar não oferece a Experiência. | OK | DMO-399 |
| JobOnPageTests.FolhaNova_Experiencia_AbreEmBrancoComONumeroAnoZeroZero_EMarcadaComoExperiencia | A experiência abre em branco com o número AAAA00 e marcada. | OK | DMO-399 |
| JobOnPageTests.FolhaNova_TemOsDoisBotoes_ProducaoEExperiencia_ComAProducaoEscolhida | O Job On novo tem os botões Produção e Experiência. | OK | DMO-399 |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.CoberturaA2Mapa.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PegamentosPageTests.FolhaComMedicoes_OMapaDeLimitesFicaPorBaixoDasLeituras_SemTextoDeAjuda | Mapa de limites fica abaixo das leituras, sem texto de ajuda | OK | DMO-563 |

### `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.CoberturaBPassos.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| PegamentosPageTests.Folha_TemAsLeiturasEOMapa_SemOsPassosContextoMedicoesMapa | Os Pegamentos já não têm os passos Contexto, Medições e Mapa. | OK | DMO-482 |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/CoberturaCReparacaoInternaTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| CoberturaCReparacaoInternaTests.ANaoSerReparacao_AConsultaMostraOTrabalhoNaLinha_EFiltraPeloTipoDeTrabalho | Consulta mostra o trabalho e filtra por ele | OK | DMO-415 |
| CoberturaCReparacaoInternaTests.OTrabalhoEscolhidoNoRegisto_FicaGravado_ESemEscolhaOuComUmValorDesconhecidoEReparacao | O trabalho escolhido fica gravado; por omissão Reparação | OK | DMO-415 |
| CoberturaCReparacaoInternaTests.OsFiltrosDaConsulta_VemPelaOrdemPedida_ESemJobOnTemUmaDica | Filtros da consulta pela ordem pedida; dica sem Job On | OK | DMO-428 |
| CoberturaCReparacaoInternaTests.OsFiltrosDoHistorico_VemPelaMesmaOrdem_SemQuemNemCmMf | Filtros do histórico pela mesma ordem, sem Quem nem CM/MF | OK | DMO-428 |

### `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaCoberturaBTests.cs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| ReparacaoInternaConsultaCoberturaBTests.Consulta_ReferenciaPrimeiro_ProducaoLogoAseguir_ETodosOsFiltrosNumSoFormulario | Na Consulta RI a Referência vem primeiro e a Produção a seguir. | OK | DMO-577 |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.SapProcura.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OCodigoSapDaFicha_GanhaATabela_EVazioVoltaAEla | Código SAP escrito na ficha ganha à tabela; vazio volta a ela; já não está no Opções (DMO-668) | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.UtilizacaoVoltar.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.AUtilizacaoPorProducao_TemOVoltarPorCimaESemQuadrosVerdes | Utilização por produção: «Voltar» por cima do título, sem «Fechar» nem quadros verdes (DMO-668) | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoSoBruto.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OHistoricoDeProducoes_SoGarrafasEmBruto_EOsDiasAFrenteDasDatas | Histórico: Produção, Datas com os dias, Garrafas em bruto, % uso, Peso, Cadência; sem Δ garrafas (DMO-609) | OK |  |

### `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.GeralNumaLinha.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| FerramentasPagesTests.Ficha_Geral_LinhasNaVertical_EUtilizacaoNaMesmaLinha | Geral da ficha: Linhas uma por baixo da outra e Utilização no mesmo quadro; o browser confirma uma só linha (DMO-608) | OK |  |

### `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.HistoricoOrdem.cs`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| DefinicoesPorModuloPageTests.OHistoricoDeProducoes_NumeraAsProducoesDaFerramenta_A1EAMaisAntiga | Histórico de produções da ficha: cada produção com o número de ordem na ferramenta, 1 = a mais antiga, também noutro artigo; sem início, sem número (DMO-679) | OK |  |

### `tests/ShellProof/cobertura_a1_ficha-na-lista.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| ficha-na-lista.teste1 | Triângulo da ficha em cada ferramenta da lista | OK | DMO-559 |
| ficha-na-lista.teste2 | Triângulo da ficha em cada ferramenta da lista | OK | DMO-559 |
| ficha-na-lista.teste3 | Triângulo da ficha em cada ferramenta da lista | OK | DMO-559 |
| ficha-na-lista.teste4 | Triângulo da ficha em cada ferramenta da lista | OK | DMO-559 |
| ficha-na-lista.teste5 | Triângulo da ficha em cada ferramenta da lista | OK | DMO-559 |
| ficha-na-lista.teste6 | Triângulo da ficha em cada ferramenta da lista | OK | DMO-559 |

### `tests/ShellProof/cobertura_a1_folha-js.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| folha-js.teste1 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste10 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste11 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste2 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste3 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste4 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste5 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste6 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste7 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste8 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |
| folha-js.teste9 | Comportamentos do jobon-folha.js: notas, calibres, imagem | OK | DMO-509/558/562/417/495/395/410 |

### `tests/ShellProof/cobertura_a2_artigo-recorte.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| Cancelar o recorte não grava nada e a imagem anterior fica como estava | Recorte da imagem do artigo: gravar, cancelar e colar | OK | DMO-445 |
| Colar algo que não é imagem não abre o recorte | Recorte da imagem do artigo: gravar, cancelar e colar | OK | DMO-445 |
| Colar dentro de um campo de escrita fica no campo e não grava imagem | Recorte da imagem do artigo: gravar, cancelar e colar | OK | DMO-445 |
| Colar uma imagem (Ctrl+V) fora dos campos abre o recorte e grava o recorte | Recorte da imagem do artigo: gravar, cancelar e colar | OK | DMO-445 |
| Guardar recorte com imagem anterior: a anterior fica guardada e o artigo.png é o novo | Recorte da imagem do artigo: gravar, cancelar e colar | OK | DMO-445 |
| Guardar recorte grava na pasta da referência o recorte, não a imagem inteira | Recorte da imagem do artigo: gravar, cancelar e colar | OK | DMO-445 |

### `tests/ShellProof/cobertura_a2_bq-sem-jobon.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| As sugestões da BQ só aparecem depois de escrever, e o rótulo é «BQ C002 · Lote 18» | Registar BQ sem Job On: sugestões, linha e Job On anterior | OK | DMO-412 |
| Com a BQ já escolhida ao abrir, a Linha só tem as da ficha e vem com a primeira | Registar BQ sem Job On: sugestões, linha e Job On anterior | OK | DMO-572 |
| Escolher a BQ deixa na Linha só as da ficha dela, com a primeira já escolhida | Registar BQ sem Job On: sugestões, linha e Job On anterior | OK | DMO-572 |
| Job On anterior: a BQ mostra as referências dela, a referência as produções, e a produção põe a linha do Job On | Registar BQ sem Job On: sugestões, linha e Job On anterior | OK | DMO-573 |

### `tests/ShellProof/cobertura_a2_ferramentas-sap.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| A primeira leitura da pasta envia todos os ficheiros do SAP, do mais antigo para o mais novo, e nenhum do DMO | Leitura da pasta do SAP e apagar ficheiros lidos | OK | DMO-418 |
| Apagar lidos: pergunta antes e apaga só os ficheiros do SAP já lidos | Leitura da pasta do SAP e apagar ficheiros lidos | OK | DMO-492 |
| Apagar lidos: se a pessoa não confirma, nada é apagado | Leitura da pasta do SAP e apagar ficheiros lidos | OK | DMO-492 |
| Apagar lidos: sem nada lido ainda, avisa e não apaga nem pergunta | Leitura da pasta do SAP e apagar ficheiros lidos | OK | DMO-492 |
| Com um ficheiro já lido, só os mais novos do que o último lido vão para o servidor | Leitura da pasta do SAP e apagar ficheiros lidos | OK | DMO-418 |
| Numa pasta com mais de dez listas por ler, vão as dez mais recentes | Leitura da pasta do SAP e apagar ficheiros lidos | OK | DMO-418 |
| Se a lista mais recente já foi lida, nada vai para o servidor e o cartão diz isso | Leitura da pasta do SAP e apagar ficheiros lidos | OK | DMO-418 |

### `tests/ShellProof/cobertura_b_peso-faixa.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| A diferença nominal positiva leva «+» e duas casas, mesmo inteira | A diferença nominal leva sinal e duas casas. | OK | DMO-504 |
| A faixa escreve inteiros sem casas e decimais com duas, como o servidor | A faixa do Peso escreve inteiros sem casas e decimais com duas. | OK | DMO-504 |

### `tests/ShellProof/cobertura_b_peso-menos.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| Esc dentro da correção sai da correção e não chega ao resto da página | Esc na correção sai da correção sem fechar o painel. | OK | DMO-499 |
| Um pedido de apagar anterior não fica no endereço do novo | O pedido de apagar não herda parâmetros do anterior. | OK | DMO-499 |
| «−» com duas linhas só tira a linha vazia, sem perguntar nada | O «−» com duas linhas só tira a linha vazia. | OK | DMO-499 |
| «−» com uma só linha abre a pergunta de apagar a última leitura gravada, sem apagar nada | O «−» com uma linha pede para apagar a última leitura gravada. | OK | DMO-499 |
| «−» com uma só linha e nenhuma leitura gravada não faz nada | O «−» sem leituras gravadas não faz nada. | OK | DMO-499 |

### `tests/ShellProof/cobertura_b_ri-consulta.test.cjs`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| A Produção fechada mostra só o número; aberta, o resto por baixo de cada uma | Na Consulta RI a Produção mostra só o número e o resto por baixo. | OK | DMO-577 |
| Carregar numa pessoa do resumo escolhe-a no «Quem» e aplica; uma que não está na lista segue o link | Carregar numa pessoa do resumo aplica-a no filtro Quem. | OK | DMO-577 |
| Esc fecha a lista aberta sem escolher nada | Esc fecha a lista das produções sem escolher. | OK | DMO-577 |
| Escolher uma produção fecha a lista, fica só o número e o select muda e avisa | Escolher uma produção fecha a lista e aplica o filtro. | OK | DMO-577 |

### `tests/visual/cobertura_a1_browser.py`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| cobertura_a1_browser.aspeto_da_folha | Reparador na linha, aviso sob a Utilização, escuro e zoom 125% | OK | DMO-468/570/537/409 |
| cobertura_a1_browser.botoes_e_cabecalho | Botões sem sublinhado e cabeçalho Nome · Título | OK | DMO-489 |
| cobertura_a1_browser.folha | Folha cheia sem deslizar, calibres e Imprimir no A4 | OK | DMO-486 |
| cobertura_a1_browser.painel_e_pegamentos | Painel de apoio cabe a 1366×768 e botão Pegamentos à vista | OK | DMO-485 |
| cobertura_a1_browser.planeamento | Cartão escolhido da faixa Planeamento com aspeto de hover | OK | DMO-561 |
| cobertura_a1_browser.sumario | Linha do Sumário enche-se ao passar o rato | OK | DMO-380 |

### `tests/visual/cobertura_a2_browser.py`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| cobertura_a2_browser.py | Barra de cor de cada linha do painel, claro e escuro | OK | DMO-381 |
| cobertura_a2_browser.py | Ficha da ferramenta cabe no ecrã sem deslizar | OK | DMO-431 |
| cobertura_a2_browser.py | Lista de utilizadores longa cabe e desliza no sítio certo | OK | DMO-447 |
| cobertura_a2_browser.py | Resumo com peso anterior longo não rebenta o layout | OK | DMO-479 |
| cobertura_a2_browser.py | Imprimir a preto e branco legível no browser | OK | DMO-493 |
| cobertura_a2_browser.py | Revisão e consulta de pegamentos medidas no ecrã | OK | DMO-483 |
| cobertura_a2_browser.py | Ecrã de Job On cabe a 1366×768 e 1290×646 | OK | DMO-510 |
| cobertura_a2_browser.py | Ecrã de registo cabe a 1366×768 e 1290×646 | OK | DMO-522 |
| cobertura_a2_browser.py | Ecrã de registo sem deslizar na horizontal | OK | DMO-531 |
| cobertura_a2_browser.py | Mapa de limites dos pegamentos medido no ecrã | OK | DMO-563 |

### `tests/visual/cobertura_b_browser.py`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| cobertura_b_browser.py (1290x646 e 1366x768) | Sem setas nos botões das punções. | OK | DMO-514 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Número e «+ Adicionar punção» na mesma linha, no canto direito. | OK | DMO-484 |
| cobertura_b_browser.py (1290x646 e 1366x768) | A página dos Pegamentos não desliza; só a folha por dentro. | OK | DMO-482 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Leitura nova do Peso estreita, registar com a largura dos campos. | OK | DMO-499 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Login com erro: traço fino, sem caixa, vermelho; escuro mais claro (claro e escuro). | OK | DMO-505 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Peso no painel: números à esquerda da leitura nova, Registar encostado. | OK | DMO-502 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Armazém sem deslizar a 1290x646 e 1366x768; Registar junto ao Entrada \| Saída. | OK | DMO-487 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Peso, Resumo, Definições, Armazém e login sem deslizar; hover da linha muda de cor. | OK | DMO-523 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Controlo nas Definições em duas colunas, sem círculos de ajuda. | OK | DMO-467 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Linha destacada da Comparação com fundo e letra azul; campos estreitos. | OK | DMO-469 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Peso sem Job On: CM e BQ numa só faixa, sem deslizar para os lados. | OK | DMO-473 |
| cobertura_b_browser.py (1290x646 e 1366x768) | Aviso do artigo dentro do cartão do artigo, por cima das Pinças. | OK | DMO-576 |

### `tests/visual/cobertura_c_browser.py`

| Teste | Garante | Estado | Correção |
|---|---|---|---|
| cobertura_c_browser.linhas_compativeis | Linhas compatíveis: cores em repouso, rato e escolhida | OK | DMO-405 |
| cobertura_c_browser.login | Login: botão Reparação, logótipo e cores certas a 1366 e 1290 | OK | DMO-436 |
| cobertura_c_browser.topo_e_ecras | Topo e ecrãs cabem a 1366x768, sem sublinhados | OK | DMO-481 |

### `tests/visual/rp_botoes_browser.py`

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| rp_botoes_browser.py (script, 1366 px) | Imprimir, Listas, Histórico, Adicionar e Limpar filtros com o mesmo espaço entre si (DMO-674) | OK |  |


### Peso › aviso «O Job On mudou» e Approve › diferença da produção (DMO-594, DMO-595)

| Teste | Garante | Estado | Nota |
|---|---|---|---|
| JobOnFichaTests.Peso_OAvisoDeQueOJobOnMudouTemUmXParaFechar | O aviso do Peso tem o ×, e o Resumo não | OK | A-0332 |
| tests/ShellProof/aviso-dispensar.test.cjs (2) | O × fecha o aviso e não volta; uma mudança mais nova traz-o de volta | OK | A-0332 |
| ControloCapabilitiesCutHPageTests.Approve_PesoDaProducaoAnteriorMostraAhDiferenca | «Peso da produção anterior» leva g e % por baixo, no Peso e no Resumo do controlo | OK | A-0332 |

| ControloCapabilitiesCutHPageTests.Approve_BarraDosFiltrosNaoRepeteAEtiquetaPendente | A barra dos filtros do Aprovar não repete a etiqueta «Pendente» | OK | A-0335 (DMO-597) |
| DefinicoesPorModuloPageTests.OPesoDoHistorico_DeltaSaltaAsProducoesAnterioresSemPeso | O Δ do peso por produção salta as anteriores sem peso | OK | A-0341 (DMO-602) |
| JobOnFichaTests.Peso_ComparacaoTemOMaisEOMenosDentroDaCaixa | + e − dentro da caixa da Comparação | OK | A-0417 (DMO-672) |
| PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_DiferencasFicamForaDaTroca_RegistarNaoVoltaAoTopo | As diferenças da produção anterior ficam fora da troca (registar não volta ao topo) | OK | A-0417 (DMO-672) |
| tests/ShellProof/painel-gravou-sem-recarregar.test.cjs | Depois de gravar sem recarregar, o painel fecha sem aviso falso de alterações por guardar | OK | A-0417 (DMO-672) |
| PesoFolhaProducaoAnteriorPageTests.Folha_TabelaDaComparacao_AgrupaPorGrandeza_AtualAnteriorEDiferenca_ComAnteriorACinza | A tabela do Controlo anterior compara por grandeza, com o anterior a cinza | OK | A-0419 (DMO-672) |

| JobOnPageTests.JobOnCriar_HandlersAjax_ContinuamAFuncionar | Lote .241: junta os testes novos do #327 (A-0395) e do #328 (A-0401) | OK | A-0402 |
| JobOnPageTests.JobOnVer_HandlerProcurarProducao_EhRedirecionado | Lote .241: junta os testes novos do #327 (A-0395) e do #328 (A-0401) | OK | A-0402 |
| JobOnPageTests.JobOnVer_HandlerToolCandidates_EhRedirecionado | Lote .241: junta os testes novos do #327 (A-0395) e do #328 (A-0401) | OK | A-0402 |
| JobOnPageTests.JobOnVer_HandlerToolFacts_EhRedirecionado | Lote .241: junta os testes novos do #327 (A-0395) e do #328 (A-0401) | OK | A-0402 |
| PostoFerramentasUtilizacaoTests.PeloPosto_FerramentasUtilizacao_EhRecusada_PeloLoginContinuaAAbrir | Lote .241: junta os testes novos do #327 (A-0395) e do #328 (A-0401) | OK | A-0402 |
| DefinicoesNumSoSitioTests.SemSenhaDoSap_OsScriptsNaoSeDao | DMO-661: sem senha do SAP os scripts param logo e o Instalar recusa; com o SAP aberto a senha ainda se escreve (Diogo 29/10: «prendeu na pass», PC bloqueado) | OK | A-0403 |
| SapScriptsVbsTests.SemSenha_OScriptParaLogo | DMO-661: sem senha do SAP os scripts param logo e o Instalar recusa; com o SAP aberto a senha ainda se escreve (Diogo 29/10: «prendeu na pass», PC bloqueado) | OK | A-0403 |
| tests/ShellProof/jobon-folha-avisos-alerta.test.cjs | Teste novo: os avisos dos cartões da folha do Job On vão para a pilha dos alertas da app (DMO-666) | OK | A-0411 |
| JobOnDiametrosSepararTests.DoisNumeros_VaiOSegundoParaATolVazia_EOGuardarPassa | Teste novo: diâmetros antigos com dois números separam-se em valor e Tol. e o Guardar passa (DMO-669) | OK | A-0414 |
| JobOnDiametrosSepararTests.TolJaEscrita_FicaComoEsta | Teste novo: diâmetros antigos com dois números separam-se em valor e Tol. e o Guardar passa (DMO-669) | OK | A-0414 |
| JobOnDiametrosSepararTests.UmNumero_NaoMuda | Teste novo: diâmetros antigos com dois números separam-se em valor e Tol. e o Guardar passa (DMO-669) | OK | A-0414 |
| AuthB03B04UserAdminServiceTests.ResetUserAccessAsync_ApagaOPinDoPosto | AUTH-B03 (DMO-655): o reset de acesso apaga o PIN do Posto; o rollback do provider repõe o PIN apagado | OK | A-0404 |
| AuthB03B04UserAdminServiceTests.ResetUserAccessAsync_OProviderRecusa_OPinDoPostoVoltaAFicar | AUTH-B03 (DMO-655): o reset de acesso apaga o PIN do Posto; o rollback do provider repõe o PIN apagado | OK | A-0404 |
| AuthB03B04B05Tests.ComRequiresPasswordChange_AEntradaPeloPostoRecusaMesmoComOPinCerto | AUTH-B03 (DMO-655): o reset de acesso apaga o PIN do Posto; o rollback do provider repõe o PIN apagado | OK | A-0404 |
| AuthB03B04B05Tests.MudarOPin_FechaAsSessoesDoPostoAbertasComOPinAnterior | AUTH-B03 (DMO-655): o reset de acesso apaga o PIN do Posto; o rollback do provider repõe o PIN apagado | OK | A-0404 |
| AuthB03B04B05Tests.ResetDeAcesso_ApagaOPinDoPosto_OPinAntigoJaNaoEntra | AUTH-B03 (DMO-655): o reset de acesso apaga o PIN do Posto; o rollback do provider repõe o PIN apagado | OK | A-0404 |
| AuthB03B04UserAdminServiceTests.SetUserStandByAsync_ArquivarEReativar_SobeAVersaoNasDuasVezes | AUTH-B04 (DMO-656): arquivar e reativar um utilizador sobem o SessionVersion nas duas mudanças, para uma cookie antiga não voltar a valer depois de reativar | OK | A-0405 |
| AuthB03B04UserAdminServiceTests.SetUserStandByAsync_Arquivar_SobeAVersaoDaSessao | AUTH-B04 (DMO-656): arquivar e reativar um utilizador sobem o SessionVersion nas duas mudanças, para uma cookie antiga não voltar a valer depois de reativar | OK | A-0405 |
| AuthB03B04UserAdminServiceTests.SetUserStandByAsync_SemMudarOEstado_NaoSobeAVersao | AUTH-B04 (DMO-656): arquivar e reativar um utilizador sobem o SessionVersion nas duas mudanças, para uma cookie antiga não voltar a valer depois de reativar | OK | A-0405 |
| AuthB03B04B05Tests.ArquivarEReativar_ACookieAntigaContinuaRecusadaDepoisDeReativar | AUTH-B04 (DMO-656): arquivar e reativar um utilizador sobem o SessionVersion nas duas mudanças, para uma cookie antiga não voltar a valer depois de reativar | OK | A-0405 |
| PostoSoRiTests.PeloPosto_ChamarAtivo_RenovaOLimitePorMaisNMinutos | AUTH-B05 (DMO-657): cookie próprio do Posto sem deslizar; o endpoint /posto/ativo (só a partir de uma ação real da pessoa) renova o limite, outros pedidos não renovam | OK | A-0406 |
| PostoSoRiTests.PeloPosto_DentroDoLimite_OsPedidosContinuam | AUTH-B05 (DMO-657): cookie próprio do Posto sem deslizar; o endpoint /posto/ativo (só a partir de uma ação real da pessoa) renova o limite, outros pedidos não renovam | OK | A-0406 |
| PostoSoRiTests.PeloPosto_PedidoQueNaoPassaPorAtivo_NaoRenova | AUTH-B05 (DMO-657): cookie próprio do Posto sem deslizar; o endpoint /posto/ativo (só a partir de uma ação real da pessoa) renova o limite, outros pedidos não renovam | OK | A-0406 |
| SapScriptsVbsTests.SemCodigosAEspera_OUsoAcabaSemErro | DMO-664: sem ferramentas à espera o uso do SAP acaba sem erro (Diogo 29/10, «A leitura do SAP falhou») | OK | A-0409 |
| SapScriptsVbsTests.SemCaixaDaSenha_NaoEscrevePeloTeclado | DMO-677: com o SAP já aberto, sem caixa da senha o script não escreve pelo teclado na janela do SAP em uso (Diogo 29/10, «Função impossível») | OK | A-0428 |
| SapScriptsVbsTests.PorData_TambemLeAPercentagemDeUso | DMO-681: o «Atualizar do SAP › Por data» lê as produções e também a % uso de todas (Diogo 29/10, «não tenho de andar a carregar várias vezes») | OK | A-0434 |
| DefinicoesPorModuloPageTests.AJanelaDoSap_EscondeAsPartesQueNaoEstaoEscolhidas | DMO-665: a janela Atualizar do SAP mostra só a parte escolhida (Diogo 29/10) | OK | A-0410 |
| ArmazemMovimentosPaginaTests.Registo_VoltarFicaPorCimaDoTitulo_EAFabricaDoEmprestimoEUmaLista | DMO-667/A-0412 (Diogo 29/10): no Registo do Armazém, «Voltar» por cima do título e a Fábrica do Empréstimo sempre em lista (Definições). 1 teste novo; nenhum antigo mudou | OK | A-0412 |
| JobOnPageTests.Folha_PainelDasLinhas_SoOQueEntraNosProximosCincoDias | Teste novo: o painel das linhas da Folha só mostra o que entra nos próximos 5 dias (DMO-671) | OK | A-0416 |
| PesoCapSupportPageTests.ProductionMode_DadosTecnicos_FicamNumDobravelFechadoPorOmissao | Teste novo: os dados técnicos do Peso (volumes, densidade, TP/calote) ficam num dobrável com triângulo, fechado por omissão (DMO-675, Diogo 29/10) | OK | A-0425 |
## DMO-663 · A-0408 · 29/10/2026

| Teste | O que garante | Estado | Registo |
|---|---|---|---|
| `Zppg_LeAPrimeiraQuantidadeOFornecedorEADataPurchase` | Lê a primeira quantidade da ZPPG, o fornecedor e a aquisição, incluindo zero, limite inteiro e valores inválidos. | OK | A-0408 |
| `Sap_SimGravaNaQuantidadeDoTipoCertoESoNoToolIdEscolhido` | O Sim grava a quantidade na BQ, CM ou MF escolhida, sem alterar outra ferramenta com os mesmos factos. | OK | A-0408 |
| `Sap_NaoMantemQuantidadeEUsoManual_NaoRepeteMesmoValorEMostraNovoValor` | O Não conserva quantidade e uso manual, cala o valor recusado e volta a avisar se o SAP trouxer outro valor. | OK | A-0408 |
| `Sap_SimUsaOPercentDoServidorEDepoisVoltaAAplicarAutomaticamente` | O Sim aceita a percentagem guardada no servidor e permite atualizações automáticas posteriores. | OK | A-0408 |
| `Sap_UmaDecisaoAntigaNaoSubstituiUmaImportacaoMaisRecente` | Uma decisão enviada de uma ficha antiga não aplica dados SAP alterados entretanto. | OK | A-0408 |
| `Sap_AMigracaoSoAcrescentaColunasOpcionais` | A migração só acrescenta colunas opcionais, sem mudar ou apagar dados existentes. | OK | A-0408 |
| `Sap_DuasDecisoesDaMesmaFicha_SoUmaGrava` | Duas decisões simultâneas da mesma ficha só deixam uma gravar no PostgreSQL. | OK | A-0408 |
| `Sap_EditarOutrosFactosDaCmNaoApagaAQuantidadeAceite` | Editar outros factos da CM conserva a quantidade aceite do SAP. | OK | A-0408 |
| `Sap_UmValorManualIgualAoSapContinuaProtegidoQuandoOSapMuda` | Uma percentagem manual igual à do SAP mantém a origem manual; escrever depois de outra importação conserva o retrato mais recente. | OK | A-0408 |
| `Sap_AFichaMostraFornecedorECompraNaInformacaoOpcional_EOSimGravaAPeca` | A ficha mostra fornecedor/aquisição em Informação opcional e o formulário Sim grava a quantidade. | OK | A-0408 |
| `Sap_ONaoMantemAQuantidadeENaoVoltaAMostrarOMesmoAviso` | O formulário Não mantém a quantidade e retira o aviso do mesmo valor. | OK | A-0408 |
| `Sap_UmaDecisaoSemAntiforgeryNaoGrava` | Uma decisão sem o token de antifalsificação é recusada. | OK | A-0408 |
| `O × fecha só o aviso escolhido, sem decidir «Não» nem enviar o formulário` | O × fecha apenas o aviso escolhido, sem enviar uma decisão ao servidor. | OK | A-0408 |
| `Os avisos carregados depois de uma troca parcial também se podem fechar` | O × continua a funcionar nos avisos recebidos numa troca parcial. | OK | A-0408 |
| PesoFolhaProducaoAnteriorPageTests.Folha_TabelaDoControloAnterior_ChamaControloAtualComONumeroDaProducao | Teste novo: a coluna «Controlo atual · n.º da produção» (DMO-676, Diogo 29/10) | OK | A-0427 |
| PesoFolhaProducaoAnteriorPageTests.NovoPesoComJobOn_ASelecaoDeMaquina_SoMostraAsDaReferencia | Teste novo: o Novo Peso com Job On só lista as máquinas da referência (DMO-676, Diogo 29/10) | OK | A-0427 |
| `O número do botão «Linhas» fica num círculo com contorno, centrado com o texto` | Na folha do Job On, o número do botão «Linhas» aparece num círculo visível e alinhado ao centro com o texto. | OK | A-0433 |
| PesoPdfGeneratorCoberturaBTests.ProducaoAnterior_ComMediaEModa_OsCartoesDizemMediaEModa_EAModaMostraOsDiasSemDiferenca | Teste novo: o PDF do Peso diz «Média» e «Moda» nos cartões e a moda mostra os dias, sem diferença (DMO-682, Diogo 29/10) | OK | A-0435 |
| PuncoesHandlerTests.Pdf_PosicoesDaLinha_EOResumoLigaOFicheiroDasPuncoes | O Resumo volta a ter «Abrir PDF das punções →» (DMO-684, Diogo 29/10) | OK | A-0438 |

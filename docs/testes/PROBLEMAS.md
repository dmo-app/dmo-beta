# Problemas encontrados nos testes (28/10/2026)

Ver [REGISTO.md](REGISTO.md) para a lista completa. Aqui: os testes que não estão OK, por área, e as
regras importantes que nenhum teste guarda. Mudar um teste que já existe precisa do «sim» do Diogo
(REGRAS-DOS-TESTES.md, ponto 15); acrescentar testes novos não precisa.

## Testes ERRADOS (contra decisões do Diogo)

- `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.SemBotoesNasLinhas.cs` — EnvioEmailPesoPageTests.Modelos_UmaLinhaEscolhe_EUmSoAbrirNoCanto: Aceita um botão × em cada linha (ctl-def-x); Diogo: «Nunca botões dentro das linhas» (peso-arrumado-pr212.md; A-0226 tirou os × dos Pegamentos/PU em design-164-integracao.md); também verifica classes/atributos HTML
- `tests/DMO.Alpha.Web.Tests/JobOn/JobOnAvisoArmazemEmEsperaTests.cs` — JobOnAvisoArmazemEmEsperaTests.PorOmissao_AAplicacaoNaoOLigaNasDefinicoes: Possível: fixa o aviso «Sem registo no Armazém» desligado por omissão; regra do Diogo 27/10 (MEMORY.md, DADOS OFICIAIS): sem registo → «Sem registo» + aviso. Confirmar.
- `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs` — JobOnFichaTests.AvisoDoArmazem_EmEspera_NaoApareceNaFicha_NemNoEndpoint_MasOsOutrosSim: Possível: exige que «não registada no Armazém» NÃO apareça; regra do Diogo 27/10 (MEMORY.md): sem registo → «Sem registo» + aviso. Confirmar.
- `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs` — ReparacaoInternaOutraFerramentaPaginaTests.Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas: Exige um link «Por associar» (data-dmo-dialog) dentro de cada linha: ação na linha, contra «nunca botões dentro de linhas» (design-novo.md). A confirmar com o Diogo. Também afirma classe dmo-table.
- `tests/visual/ajuda_junto_browser.py` — ajuda_junto_browser.py (script, 3 casos x claro/escuro): Exige que nenhum círculo de ajuda esteja à vista (Diogo 27/10), mas botoes-seletores.md (28/10 08:25) mantém a bola de ajuda como ícone permitido. Corre no CI.

## Testes OBSOLETO

### Boquilhas e Armazém

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs` | ComOPar_ACmEOMfEntramNaMesmaPosicao (Theory x2) | A página já não envia com_par (Diogo 22/10, par saiu do cartão; ver ArmazemEntradaComParPaginaTests.AEntrada_EntraSoACm); ComPar sem uso nas páginas |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs` | SemOPar_EntraSoAFerramenta | A página já não envia com_par (Diogo 22/10, par saiu do cartão; ver ArmazemEntradaComParPaginaTests.AEntrada_EntraSoACm); ComPar sem uso nas páginas |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs` | OParJaNoArmazem_EntraSoAFerramentaEAvisa | A página já não envia com_par (Diogo 22/10, par saiu do cartão; ver ArmazemEntradaComParPaginaTests.AEntrada_EntraSoACm); ComPar sem uso nas páginas |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs` | SemParQueExista_EntraSoAFerramenta (Theory x2) | A página já não envia com_par (Diogo 22/10, par saiu do cartão; ver ArmazemEntradaComParPaginaTests.AEntrada_EntraSoACm); ComPar sem uso nas páginas |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs` | OMesmoEnvioDuasVezes_GravaOParUmaVez | Par sem uso nas páginas; envio único da entrada normal já está em ArmazemMovimentosTests |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemEntradaComParTests.cs` | ASituacao_MostraOParEOndeEsta | A página já não envia com_par (Diogo 22/10, par saiu do cartão; ver ArmazemEntradaComParPaginaTests.AEntrada_EntraSoACm); ComPar sem uso nas páginas |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs` | APosicao_CorredorEstanteEPrateleira (Theory x3) | Mapa sem página: nenhuma página usa ArmazemMapa |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs` | SemArmario_AbreOPrimeiro_ComAOcupacao | Mapa sem página no código; serviço só registado no Program.cs, sem página |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs` | OParDaMesmaReferenciaELote_NaoEMisturado | Mapa sem página no código |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs` | OFiltro_ContaSempreOArmarioInteiro (Theory x3) | Mapa sem página no código |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs` | TodosOsArmarios_OuUmQueNaoExisteAbreOPrimeiro (Theory x5) | Mapa sem página no código |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemMapaTests.cs` | SemPosicoes_UmMapaVazio | Mapa sem página no código |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs` | Criar_QuatroAlgarismos_Grava (Theory x2) | Separador Posições saiu e as posições nascem na Entrada (código); nenhuma página usa ArmazemPosicoes |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs` | Criar_ForaDaRegra_NaoGrava (Theory x11) | Serviço ArmazemPosicoes sem página; a regra dos 4 algarismos na Entrada tem de ter o seu teste |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs` | Criar_OMesmoCodigoComEspacos_JaExiste | Serviço ArmazemPosicoes sem página |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs` | MudarAtiva_ArquivaEReativa_SemApagar | Nenhuma página usa ArmazemPosicoes |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs` | Listar_PeloInicioDoCodigoEPeloEstado_PelaOrdemDoCodigo | Nenhuma página usa ArmazemPosicoes |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemPosicoesTests.cs` | Listar_MaisDoQueUmaPagina_Pagina | Nenhuma página usa ArmazemPosicoes |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs` | AsProducoesDoPeriodo_PelaDataDeInicio | Próximos Job On sem página no código; ArmazemProximos só registado no Program.cs |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs` | CadaFerramenta_OndeEstaEOQueFalta | Próximos Job On sem página no código |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs` | ForaDoArmazemEDoFabrico_NaoDisponivel (Theory x3) | Próximos Job On sem página no código |
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ArmazemProximosTests.cs` | SemProducoes_ListaVazia | Próximos Job On sem página no código |

### Reparação Interna, JavaScript (ShellProof) e browser (visual)

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/visual/dmo_chromium.py` | dmo_chromium.py (módulo auxiliar) | Não é teste, só auxiliar (usado pelos scripts do CI). |
| `tests/visual/design-review.cjs` | design-review.cjs (script) | ÓRFÃO; CONTINUAR.md chama-lhe experimental, nunca corre no CI. |


## Testes FRACO

### Controlo (Peso, Pegamentos, Punções, Resumo, Aprovar)

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AprovacaoNosPdfsTests.cs` | AprovacaoNosPdfsTests.PegamentosPdf_SemSeloDeAprovacao | só DoesNotContain, sem confirmar que o texto do PDF tem conteúdo (ex. título); um PDF vazio passava |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs` | AssociatePesoToControloHandlerTests.Associate_Twice_IsIdempotent | idempotência só provada em memória; sem índice único/trinco do PostgreSQL não prova duas pessoas ao mesmo tempo |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/AssociatePesoToControloHandlerTests.cs` | AssociatePesoToControloHandlerTests.Associate_UnknownIdsAndMissingCm_AreTypedRefusals | só verifica o resultado devolvido, não que nada foi gravado |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs` | ControloResumoSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingControlos | compara lista de tabelas, não um efeito; parte quando se acrescenta tabela legítima |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs` | ControloResumoSchemaTests.ControloEntity_ExactlyIdentityAndRelation_NoFactColumns | verifica nomes de colunas do modelo, não comportamento |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloResumoSchemaTests.cs` | ControloResumoSchemaTests.ControloEntity_JobOnDoesNotGainControloNavigationOrColumn | verifica forma do modelo (nomes), não comportamento |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloViewingHandlerTests.cs` | ControloViewingHandlerTests.Mark_Reopen_UpdatesTheSameLine_NeverDuplicates | «nunca repete» só provado em memória; falta índice único no PostgreSQL e janela de 30 s |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CoresDeEstadoNosPdfsTests.cs` | CoresDeEstadoNosPdfsTests.OsGeradoresDePdf_NaoTemCoresSoltas | verifica cores no código-fonte (estilo), não o efeito; pertence às guardas do sistema de design (regra 10) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreatePesoForControloHandlerTests.cs` | CreatePesoForControloHandlerTests.Create_SavesThePesoAlreadyLinkedAndReturnsItAgain | idempotência só em memória; falta teste PostgreSQL do índice único por combinação |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreatePesoForControloHandlerTests.cs` | CreatePesoForControloHandlerTests.Create_WhenAnotherRequestSavedFirst_ReturnsThatPesoWithoutAnOrphan | simula a corrida em memória (sem índice único nem trinco do PostgreSQL), regra 7 |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPegamentosHandlerTests.cs` | CreateToolScopedPegamentosHandlerTests.RepeatedClicks_OpenTheSameEmptySheet_NewOneOnlyAfterMeasurements | «nada repetido» só em memória; falta PostgreSQL com duas pessoas ao mesmo tempo |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoHandlerTests.cs` | CreateToolScopedPesoHandlerTests.RepeatedClicks_OpenTheSameEmptyPeso_NewOneOnlyAfterReadings | «nada repetido» só em memória; falta teste PostgreSQL (índice único/trinco) com dois pedidos ao mesmo tempo |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PdfRecoveryTests.cs` | PdfRecoveryTests.MapaJuntaOsRotulosDeNominaisIguais | verifica cores e opacidades do SVG (estilo), não o efeito |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PdfRecoveryTests.cs` | PdfRecoveryTests.MapaMostraOsLimitesDeForaTracejadosComoOOriginal | verifica stroke-dasharray (estilo) em vez do efeito |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosEscolhasQueryTests.cs` | PegamentosEscolhasQueryTests.OsDoisPedidos_NaoGravamNada | ChangeTracker vazio não prova que nada foi gravado; devia reler a base |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosRealTargetTests.cs` | PegamentosRealTargetTests.RealCorridors_AreSharedBoundaries | as últimas verificações são médias calculadas no próprio teste; não exercitam código da app |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs` | PegamentosSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPegamentosTables | compara lista de tabelas, não um efeito |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs` | PegamentosSchemaTests.PegamentosEntity_RegistroUnderControlo_ToleranceFrozen_ToolsInherited | verifica nomes de colunas do modelo; só as FKs/obrigatoriedade têm valor |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs` | PegamentosSchemaTests.PegamentosControloIdIndex_IsNotUnique_CardinalityIsNotADomainRestriction | só verifica que o índice não é único (ausência de proteção), não que várias folhas se gravam |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs` | PegamentosSchemaTests.PegamentosMeasurementEntity_AppendOnlyFacts_ContraCosturaNullable | verifica forma do modelo, não comportamento |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PegamentosSchemaTests.cs` | PegamentosSchemaTests.ToolTechnicalContext_GargaloColumnIsNullable | verifica só a nulidade da coluna no modelo |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoEstadoQueryTests.cs` | PesoComparacaoEstadoQueryTests.OsTresPedidos_NaoGravamNada | ChangeTracker vazio não prova que nada foi gravado; devia reler a base |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoHandlerTests.cs` | PesoComparacaoHandlerTests.CompararLeituras_RecusasTipadas | várias recusas juntas num teste (regra 4: um campo de cada vez) e sem ler a base para provar que nada ficou gravado |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparacaoHandlerTests.cs` | PesoComparacaoHandlerTests.Guardar_AMesmaComparacaoTresVezesSeguidas_FicaUmRegisto | «nada repetido» provado em memória; sem trinco/índice do PostgreSQL não prova dois cliques ao mesmo tempo |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs` | PesoEscolhasQueryTests.OsQuatroPedidos_NaoGravamNada | ChangeTracker vazio não prova que nada foi gravado |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoFolhaQueryTests.cs` | PesoFolhaQueryTests.IdentificadorNaoCanonico_MantemORecursoAQueryDoApoio | verifica só pedidos ao serviço de apoio (mock), não o efeito visível |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs` | PesoToolScopedSchemaTests.CurrentModel_ExactlyTheListedTables_IncludingPesoTables | compara lista de tabelas, não um efeito |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoToolScopedSchemaTests.cs` | PesoToolScopedSchemaTests.PesoEntity_HasNoProductionIdentityColumns | verifica nomes de colunas do modelo, não comportamento |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PuncoesHandlerTests.cs` | PuncoesHandlerTests.RepeatedClicks_StartTheRegisterAndRegisterThePunchOnlyOnce | «nada repetido» só em memória; falta teste PostgreSQL com duas pessoas ao mesmo tempo |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RecordPesoMeasurementHandlerTests.cs` | RecordPesoMeasurementHandlerTests.Record_ObservedQuantityRegisteredInFull_NoSignConstraints | só confere um valor positivo normal; não prova a ausência de restrição de sinal que o nome promete |
| `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs` | EnvioEmailPesoPageTests.Lista_UmFormularioAntigoNaoSubstituiAAlteracaoDeOutraAba | duas pessoas ao mesmo tempo só testado em memória; falta PostgreSQL (regra 7) |
| `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.Repeticao.cs` | EnvioEmailPesoPageTests.Email_OMesmoEnvioDevolveOMesmoFicheiroERegistaUmaVez_OutraChavePreparaOutro | «nada repetido» em memória, sem índice único do PostgreSQL |
| `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.SemBotoesNasLinhas.cs` | EnvioEmailPesoPageTests.Modelos_UmSoModelo_VemEscolhido | verifica atributos e classes HTML (class="selected", checked), não o efeito |
| `tests/DMO.Alpha.Web.Tests/Controlo/EnvioEmailPesoPageTests.cs` | EnvioEmailPesoPageTests.Definicoes_ListasDeAntes_SemNomes_MostramSeFechadasComOTraco | verifica CSS inline (.ctl-def-listas{display:grid...}) e classes em vez do efeito |
| `tests/DMO.Alpha.Web.Tests/Controlo/PegamentosSemJobOnPageTests.cs` | PegamentosSemJobOnPageTests.EscolherJobOn_TrocaSoAsZonas_NumaLinha | verifica classes (dmo-filter-bar, dmo-field-producao) e atributos HTML, não o efeito |
| `tests/DMO.Alpha.Web.Tests/Controlo/PegamentosSemJobOnPageTests.cs` | PegamentosSemJobOnPageTests.AssociarJobOn_TrocaSoAsZonasDaEscolha_ComOBotaoNaMesmaLinha | verifica atributos e classes do HTML |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.EnvioUnico.cs` | PesoComparacaoPageTests.GuardarComparacao_SameEnvioAfterTheRepeatWindow_SavesOnceAndAnswersWithTheSameRedirect | «nada repetido» em memória; falta PostgreSQL |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.Folha_OsFormulariosLevamAComparacaoEOSupomos_ERecusaDoSupomosNoCanto | verifica sobretudo regex de HTML (zonas e inputs ocultos) |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.ComparacaoTab_ATerraPreselecionadoPelaFolha | sobretudo textos de rótulos e botões do HTML (+ e −), poucas regras |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.ComparacaoTab_MostraBaseEEfeitoNaMedia | verifica class peso-linha-destaque e atributos data-cmp; só alguns números têm valor |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.RecusasTipadas_PreservamOFormulario | vários campos inválidos no mesmo teste (regra 4) |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.Historico_Filtros_TrocamSoOsResultadosEAsProducoes | verifica atributos e ids de zonas HTML, não o efeito |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.ComparacaoTab_EscolherAReferenciaEAProducao_TrocaSoAsZonas | verifica atributos e ids de zonas HTML |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.GuardarComparacao_CliquesRepetidos_UmSoRegistoESemMensagem | «nada repetido» em memória, sem PostgreSQL; duplica GuardarComparacao_SameEnvioAfterTheRepeatWindow |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.cs` | PesoComparacaoPageTests.CamposDecimais_SaoTextoComTecladoDecimal_ParaAceitarVirgula | verifica atributos HTML (type, inputmode, placeholder), não que a vírgula é aceite |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs` | PesoCompararPageTests.DoHistorico_PelaReferenciaDaCm_EOutraMaquina | sobretudo ids e links no HTML; pouco efeito |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoCompararPageTests.cs` | PesoCompararPageTests.OPesoEOHistorico_TemOBotao_EOPainelLateralAbreAPagina | só verifica a existência de links e uma string de JS, não o fluxo |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs` | PesoFolhaProducaoAnteriorPageTests.Folha_GravaSemRecarregar_ZonasTrocadasELeituraNovaQueFica | verifica classes e atributos HTML das zonas (dmo-card, peso-compare), não o efeito |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs` | PesoFolhaProducaoAnteriorPageTests.Folha_TrocaParcial_ForaDasZonasNadaMuda_EOsFormulariosLevamAComparacao | sobretudo inputs ocultos e classes no HTML |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoJobOnEscolhaPageTests.cs` | PesoJobOnEscolhaPageTests.AssociarJobOn_EscolherTrocaSoAsZonas_NumaLinha | verifica classes e atributos HTML |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoPorCombinacaoPageTests.cs` | PesoPorCombinacaoPageTests.PdfCompleto_ComUmPesoSemLeituras_DeixaOVazioDeFora | só compara tamanhos de PDF; devia ler o texto (regra 12) |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Decisoes.cs` | ControloCapabilitiesCutHPageTests.Revisao_ExportaOResumo_ComoOResumo | verifica rótulos de botões e o tipo PDF; não lê o texto do PDF (regra 12) |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Detalhe.cs` | ControloCapabilitiesCutHPageTests.RevisaoDetalhe_OsPegamentosUsamOFormatoDaFolha | verifica class peg-state-bad e HTML da tabela |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.ErrosDoResumo.cs` | ControloCapabilitiesCutHPageTests.Resumo_ErrosDe27Out_CabecalhoMcaliperRegistarEVoltar | verifica classes (resumo-cabecalho, resumo-linked-grid) e código JS como texto |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs` | ControloCapabilitiesCutHPageTests.Estrutura_PainelDeApoio_TemOEsqueletoFixo | verifica classes e ficheiros CSS/JS carregados, não o efeito (regra 10) |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs` | ControloCapabilitiesCutHPageTests.Estrutura_Pagina_TopoComTituloEAcoesECorpo | compara o HTML exato com classes; aspeto vive nas guardas do design (regra 10) |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.Estrutura.cs` | ControloCapabilitiesCutHPageTests.Estrutura_Folha_SoOCorpoDesliza | procura texto de CSS e JS no código-fonte, não o comportamento (regra 10) |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.PdfPegamentos.cs` | ControloCapabilitiesCutHPageTests.Resumo_PdfPegamentos_ApareceComMedicoes | só confere o texto do botão; sem o «não aparece» sem medições |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs` | ControloCapabilitiesCutHPageTests.Revisao_ArrumadaComoODiogoPediu | verifica rótulos e classes (dmo-button, revisao-section) |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.RevisaoArrumada.cs` | ControloCapabilitiesCutHPageTests.ResumoDoControlo_SeparadorComProcura_MostraOControloInteiro | sobretudo classes e ids de HTML |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs` | ControloCapabilitiesCutHPageTests.Revisao_Calendario_MarcaODiaDoPeso_E_NaoAprovarExigeNota | mistura calendário e nota; verifica classes (dmo-calendar-day has selected) |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs` | ControloCapabilitiesCutHPageTests.Revisao_Decisao_GravaSemRecarregar_ComContextoNoRedirect_SemRepetir | verifica atributos HTML (data-dmo-parcial, campos ocultos) |
| `tests/DMO.Alpha.Web.Tests/ControloDefinicoesPageTests.cs` | ControloDefinicoesPageTests.ControloCriar_SeesThePdfFolderAndTheNorm_InItsOwnTab | só confere textos, nomes de campos e scripts presentes no HTML |
| `tests/DMO.Alpha.Web.Tests/ControloPegamentosServicesRegistrationTests.cs` | ControloPegamentosServicesRegistrationTests.ControloPegamentosServices_ResolveAsScopedImplementations | só verifica registo de serviços (DI), não uma regra do negócio |
| `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs` | ControloResumoPageTests.SecondaryNav_ShowsPesoAndResumo_OnBothControloPages | só links e rótulos no HTML |
| `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs` | ControloResumoPageTests.OpenPreCutJobOn_FolhaShowsCanonicalContext_BackfillsControloIdOnce | verifica classes CSS e ficheiro CSS no HTML |
| `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs` | ControloResumoPageTests.Folha_MarcaAsZonasDaTroca_EGravarVoltaAMesmaPagina | sobretudo atributos e classes do HTML |
| `tests/DMO.Alpha.Web.Tests/ControloResumoPageTests.cs` | ControloResumoPageTests.GravarArea_ZonasPedidas_DesenhaSoAsZonas_SemLerOQueNaoMostram | verifica ids e classes das zonas HTML (resumo-area nok) |
| `tests/DMO.Alpha.Web.Tests/ControloResumoServicesRegistrationTests.cs` | ControloResumoServicesRegistrationTests.ControloResumoServices_ResolveAsScopedImplementations | só verifica registo de serviços (DI), não uma regra do negócio |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.EnvioUnico.cs` | PegamentosPageTests.PostMeasurement_SameEnvioTwice_SavesOnce | «nada repetido» só em memória; falta PostgreSQL (regra 7) |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.Legenda.cs` | PegamentosPageTests.MapaDeLimites_ALegendaMostraAsCoresDoDesenho | compara cores e classes do SVG (estilo), não o efeito |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NaoAvaliavelCinzento.cs` | PegamentosPageTests.Resumo_MedicoesNaoAvaliaveis_OCartaoFicaCinzento | verifica classes e regex de CSS (border-color), não o efeito (regra 10) |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.PedidoAbortado.cs` | PegamentosPageTests.PostMeasurement_SeveralLines_ClientAbortsAfterTheFirstWrite_AllLinesAreSaved | tudo ou nada só em memória (sem transação); falta PostgreSQL (regra 7) |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.GetFolha_ControloId_ShowsInheritedContext_GargaloMissing_MfAlwaysInherited | verifica sobretudo classes CSS (peg-context-grid, peg-section-head) |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.Folha_GravarSemRecarregar_UmaZonaPorCartao | verifica atributos, ids e classes do HTML |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.MixedAxes_SummaryAndPdfUseRowMeans_WithoutHidingAlert | PDF só confere cabeçalho %PDF; células lidas por classe CSS peg-cell-bad |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.HistoricalToolFacts_RemainOnReadingCardAndPdf_AfterCatalogEdit | PDF só confere cabeçalho %PDF (regra 12: ler o texto) |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.RegistarLeitura_ZonasPedidas_DesenhaSoAsZonas_SemLerAsFerramentas | verifica atributos HTML das zonas e botões; poucas regras |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.ResumoPage_LinksToPegamentosSheet_WithEnsuredControloId | só confere links e rótulos «Pegamentos» no HTML |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.RealTarget_IndividualCells_OutsideCorridor_EachPaintedWithItsOwnState | verifica a classe CSS peg-cell-bad nas células; a regra já está em PegamentosRealTargetTests |
| `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.EnvioUnico.cs` | PesoToolScopedPageTests.RegisterMeasurement_SameEnvioTwice_SavesOnce | «nada repetido» só em memória; falta PostgreSQL (regra 7) |
| `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.PedidoAbortado.cs` | PesoToolScopedPageTests.RegisterMeasurement_SeveralLines_ClientAbortsAfterTheFirstWrite_AllLinesAreSaved | tudo ou nada só em memória (sem transação); falta PostgreSQL (regra 7) |
| `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.UmEnvio.cs` | PesoToolScopedPageTests.RegisterMeasurement_OMesmoEnvioDepoisDeUmReinicio_NaoGravaOutraVez_EUmEnvioNovoIgualGrava | «nada repetido» só em memória; duplica RegisterMeasurement_SameEnvioTwice_SavesOnce |
| `tests/DMO.Alpha.Web.Tests/PesoToolScopedPageTests.cs` | PesoToolScopedPageTests.ToolScopedFlow_CreateRecordConsult_ThroughThePage | verifica ficheiro CSS e classes (peso-card-heading) além do fluxo |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.EnvioUnico.cs` | PuncoesPageTests.Add_SameEnvioAfterTheRepeatWindow_SavesOnceWithoutAFalseAlteracao | «nada repetido» só em memória; falta PostgreSQL (regra 7) |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.ErrosControlo.cs` | PuncoesPageTests.Folha_TodasAsCelulasComONumeroEALinhaDoLote | verifica classes e CSS (display:block;height:13px), regra 10 |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.LoteNoBrowser.cs` | PuncoesPageTests.Identificacao_OCampoDoLoteVaiParaOBrowser | só confere que o id e o texto do botão existem; é um teste do browser sem browser |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs` | PuncoesPageTests.UmSoRegistoSemJobOn_VemEscolhido | verifica class="selected" e atributos checked no HTML |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.SemBotoesNasLinhas.cs` | PuncoesPageTests.Vistos_UmaSoPuncao_VemEscolhida | verifica class="selected" e atributos checked no HTML |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.TabelaNoBrowser.cs` | PuncoesPageTests.Tabela_TemLargurasFixas_EVaiParaOBrowser | verifica classes (pu-table-fixa) e <colgroup>, não o efeito (regra 10) |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs` | PuncoesPageTests.WithoutJobOn_TheAssociationSearchAndRefusals_ChangeOnlyTheirZone | verifica ids, atributos e zonas HTML |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs` | PuncoesPageTests.Procura_TrocaSoAsZonas_ComAProducaoAoLadoDaReferencia | verifica atributos e regex do HTML |

### Boquilhas e Armazém

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Armazem/ReparadoresTests.cs` | Criar_NomeLongo_NaoGrava | Não verifica que nada foi gravado (REGRAS 5) |
| `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemEntradaComParPaginaTests.cs` | UmaFerramentaSemPar_NaoMostraAEscolha | Só «não aparece»; sem «aparece» antes na mesma zona (REGRAS 11); redundante com ACm_NaoMostraOPar |
| `tests/DMO.Alpha.Web.Tests/Armazem/ArmazemLinhaEReparadorPaginaTests.cs` | FichaDeUmaBq_NaoTemASeccaoDaCm | Só «não aparece», sem verificar a secção numa ficha de CM no mesmo teste |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasAssociacaoQueryTests.cs` | OsPedidos_NaoGravamNada | Verifica o ChangeTracker vazio e não a base relida |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasEscolhasQueryTests.cs` | OsQuatroPedidos_NaoGravamNada | Verifica o ChangeTracker vazio e não a base relida |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasFichaQueryTests.cs` | OsTresPedidos_NaoGravamNada | Verifica o ChangeTracker vazio e não a base relida |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementIntegrationTests.cs` | AllThreeMovementTypes_PersistWithObservedQuantityAndProducedDiscrepancy | Insere linhas direto no DbContext e relê; não passa por nenhum handler: só prova que o EF grava |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementIntegrationTests.cs` | Movement_ResolvesItsTrace | Só testa o Include do EF, sem regra do negócio |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementIntegrationTests.cs` | MultipleMovements_BelongToOnePreProductionTrace | Só insere e conta linhas; nenhum código da app é exercido |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementIntegrationTests.cs` | Movements_DoNotChangeJobOnOrBqContextData | Nunca chama o handler de registo, por isso nunca falharia se o handler escrevesse no Job On |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairTraceIntegrationTests.cs` | Trace_ResolvesCanonicalToolId | Só insere e lê com Include do EF; nenhum código da app |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairTraceIntegrationTests.cs` | PreProductionTrace_PersistsWithoutBqContext_OneUnresolvedPerToolByIndex | Inspeciona metadados do modelo EF, não a base; o bq_id único já tem prova em PostgreSQL (Data/NadaRepetido), o índice parcial por tool_id não |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairTraceIntegrationTests.cs` | SameTool_ManyProductionTracesAcrossTime | Insere linhas diretas e conta; não exercita código da app |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairTraceIntegrationTests.cs` | ModelShape_UniqueBqContext_AndPartialUniqueToolIdOnUnresolved | Só metadados do modelo EF, não a base real; duplica PreProductionTrace_...OneUnresolvedPerToolByIndex |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqRepairTraceIntegrationTests.cs` | RelationalSchema_UniqueBqContext_AndPartialUniqueToolId | Verifica texto do script gerado, sem PostgreSQL a recusar duplicados |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/RegisterBqMovementHandlerTests.cs` | MovementRegistration_DoesNotDependOnToolType | Não é regra do negócio das Boquilhas (só BQ); não falha se a regra de tipo for estragada |
| `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs` | PdfHistorico_MatrixView_ReturnsTheGridPdf | Só verifica tipo, nome e cabeçalho %PDF; não lê o texto (REGRAS 12) |
| `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs` | PdfHistorico_WithTheMachineFilter_ReturnsTheMachinePdf | Só %PDF e nome do ficheiro; não verifica que só tem a máquina B1 |
| `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs` | PdfHistorico_WithoutTheMachine_ExportsAllMachines | Só %PDF e nome; não verifica o conteúdo |
| `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs` | PdfHistorico_MatrixLongerThan366Days_GeneratesTheFirst366Days | Só %PDF; não verifica o corte |
| `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs` | PdfHistorico_ForAMonth_ReturnsTheMonthPdf | Só %PDF e tamanho > 1000 bytes |
| `tests/DMO.Alpha.Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs` | History_CalendarDay_ChangesOnlyTheZones | Compara atributos data-dmo-regiao e classes de markup, não o efeito para o operador (REGRAS 10) |
| `tests/DMO.Alpha.Web.Tests/BoquilhasAvisosTests.cs` | BoquilhasRegistoPageTests.EditMovement_UnreadableDate_IsRefusedWithAReadableCause | Não verifica que o movimento ficou igual |
| `tests/DMO.Alpha.Web.Tests/BoquilhasProcuraPeloInicioTests.cs` | BoquilhasRegistoPageTests.ProcurarBq_JaNaoResponde_ComALista | Só «não aparece»; a condição inicial aceita 404 ou qualquer página com DOCTYPE |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoSemRegistoTests.cs` | BoquilhasRegistoPageTests.Cartoes_InicioEFimSaoOsDoJobOn_SemInicioAvisoAmarelo | Distingue o aviso pela classe CSS dmo-fact-falta em vez do texto |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoTrocaTests.cs` | BoquilhasRegistoPageTests.Registo_TrocaSoAsZonas_SemFocoAutomaticoNoTipo | Testa classes CSS e ordem de marcação, não comportamento |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoTrocaTests.cs` | BoquilhasRegistoPageTests.Registo_ComUmaJanelaAberta_OFocoVaiParaAJanela | Afirma atributos autofocus e classes; pouco valor de negócio |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs` | BoquilhasRegistoPageTests.CanonicalTraceId_LoadsPersistedTrace | Afirma ficheiro CSS e classe de navegação, não regras |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs` | BoquilhasRegistoPageTests.MachinesPanel_DirectLinkOpensProductionRegisterAndKeepsSearchCollapsed | Muitas asserções de classes CSS; escreve HTML de evidência |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs` | BoquilhasRegistoPageTests.MachinesCard_ShowsTheThreeLiveValues_WhenTheCurrentBqExists | Nome diz três valores, só verifica dois; discrepância só por texto do rótulo |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs` | BoquilhasRegistoPageTests.MachineInUse_ListsOnlyTheMachinesOfTheTool_AndQuantityComesFirst | Testa ordem de marcação, não regra; nome já não corresponde |

### Job On, Ferramentas, Planeamento, SAP

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Production/CreateJobOnHandlerTests.cs` | CreateJobOnHandlerTests.Create_DuplicateProductionNumber_RefusedWithoutPartialWrite | Só em memória: o índice único e a transação (duas pessoas ao mesmo tempo) não são provados; falta teste PostgreSQL. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/EdicoesAoMesmoTempoTests.cs` | EdicoesAoMesmoTempoTests.JobOn_OutraVersao_Recusa_SemGravarNada | Em memória a versão é sempre 0 (regra 7); o token de concorrência só se prova em PostgreSQL. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/EdicoesAoMesmoTempoTests.cs` | EdicoesAoMesmoTempoTests.JobOn_MesmaVersao_Grava | Versão sempre 0 em memória; não prova o token. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/EdicoesAoMesmoTempoTests.cs` | EdicoesAoMesmoTempoTests.Ferramenta_OutraVersao_Recusa_FactosETecnica | Versão sempre 0 em memória (regra 7). |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs` | JobOnFichaQueryTests.AsLeiturasSaoPelaOrdemDaPagina_FactosEFolha | Testa detalhe de implementação (ordem de chamadas), não uma regra do negócio. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnFichaQueryTests.cs` | JobOnFichaQueryTests.NaoLeNotasDeLoteNemAlteracoesNemOQueSePerdeAoEliminar | Verifica por reflexão propriedades e construtor da classe, não comportamento do negócio. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnListQueryTests.cs` | JobOnListQueryTests.SearchHistoryPageAsync_EmptyProduction_ReturnsEmptyList | Trivial: lista vazia devolve vazio, passa com quase qualquer implementação. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs` | JobOnOverlapWarningTests.Create_WithoutOverlap_ZeroWarning | Só verifica «sem aviso»; como o aviso entre linhas foi removido, passa sempre. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnOverlapWarningTests.cs` | JobOnOverlapWarningTests.Create_EntirelyOpenWindow_NothingToCompare_ZeroWarning | Só verifica «sem aviso»; passa sempre. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/JobOnPaginaQueryTests.cs` | JobOnPaginaQueryTests.AvisoDeSobreposicao_ProducaoInexistenteNaoDaAviso | Trivial: devolve null para um id que não existe. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/MachineListQueryTests.cs` | MachineListQueryTests.ListAsync_NoMachinesRegistered_ReturnsEmptyList | Trivial: lista vazia devolve vazia. |
| `tests/DMO.Alpha.Infrastructure.Tests/Production/ProductionContextQueryIntegrationTests.cs` | ProductionContextQueryIntegrationTests.GeneratedSql_Npgsql_RelationalProvider_ProducesSingleLeftJoinQuery | Verifica texto do SQL de uma consulta escrita no próprio teste, não o comportamento do negócio nem a query real. |
| `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasHandlersTests.cs` | FerramentasHandlersTests.CreateTool_Repetida_ERecusada_EAExistenteFicaComAviso | Unicidade (tipo, referência, lote) só em memória; falta teste PostgreSQL do índice único e de dois registos ao mesmo tempo. |
| `tests/DMO.Alpha.Infrastructure.Tests/Tools/FerramentasHandlersTests.cs` | FerramentasHandlersTests.DeleteTool_TransactionalDummyFlow_PreservesHistoryAndPhysicalDeletes | Transação «tudo ou nada» só em memória (regra 7); falta teste PostgreSQL que falhe sem a transação. |
| `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolRegistryIntegrationTests.cs` | ToolRegistryIntegrationTests.BqContext_ToolId_ResolvesCanonicalTool | O join está escrito no próprio teste (LINQ), prova só o mapeamento do EF; nada do código da app. |
| `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolRegistryIntegrationTests.cs` | ToolRegistryIntegrationTests.BqContext_ToolIdWithoutRegisteredTool_DoesNotResolve | Join escrito no teste; em memória não há chave estrangeira, a garantia real é do PostgreSQL. |
| `tests/DMO.Alpha.Infrastructure.Tests/Tools/ToolRegistryIntegrationTests.cs` | ToolRegistryIntegrationTests.Tools_SameReferenceDifferentLot_PersistAsDistinctCanonicalTools | Só grava e lê duas linhas em memória; não testa regra nenhuma. |
| `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs` | DefinicoesPorModuloPageTests.FerramentasDefinicoes_AAjudaDoSapAbreAPaginaDeAjuda | Só texto e ids estáticos; os scripts SAP passaram para a app (#274), texto pode estar desatualizado. |
| `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AjudaSap.cs` | DefinicoesPorModuloPageTests.FerramentasDefinicoes_OCartaoDoSapTemApagarLidos_EAAjudaExplica | Verifica texto/ids presentes, não o efeito de apagar. |
| `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.FaixaDaFicha.cs` | DefinicoesPorModuloPageTests.AJanelaDaUtilizacao_LevaAoHistoricoDeProducoes | Verifica id e texto de um botão, não a navegação. |
| `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.PastasEmColunas.cs` | DefinicoesPorModuloPageTests.AsPastasDesteComputador_FicamCadaUmaNaSuaColuna | Verifica layout/colunas de cartões (regra 10). |
| `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs` | DefinicoesPorModuloPageTests.FerramentasDefinicoes_OnlyForWhoHasTheModule_AndLeftTheAdmin | Parte do teste só verifica ausência de textos. |
| `tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.cs` | DefinicoesPorModuloPageTests.Admin_TemDefinicoesProprias_SoAApp | Muitas verificações de texto/ids e DoesNotContain. |
| `tests/DMO.Alpha.Web.Tests/FerramentasFichaTrocaTests.cs` | FerramentasPagesTests.Ficha_TrocaSoAsZonas_ATecnicaFicaComoOOperadorADeixou | Verifica HTML exato, ordem de atributos e classes (regra 10). |
| `tests/DMO.Alpha.Web.Tests/FerramentasFichaTrocaTests.cs` | FerramentasPagesTests.Ficha_BotoesDeCadaCard_NoCantoSuperiorDireito | Regex sobre layout/classes; nenhum efeito verificado. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CliquesRepetidos.cs` | FerramentasPagesTests.Registar_GravaPelaFilaDaPagina_AsMensagensNumaZona | Só confere atributos HTML (data-dmo-parcial); não faz cliques repetidos. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CliquesRepetidos.cs` | FerramentasPagesTests.Duplicar_GravaPelaFilaDaPagina_AJanelaNumaZona | Só confere atributos HTML; não duplica duas vezes. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.CliquesRepetidos.cs` | FerramentasPagesTests.Eliminar_GravaPelaFilaDaPagina | Só confere atributos HTML; não elimina duas vezes. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Historico.cs` | FerramentasPagesTests.Historico_SemAChaveDasFerramentas_NaoAbre | Só exige código diferente de 200; um erro 500 passava. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.SeccoesTecnicas.cs` | FerramentasPagesTests.Registo_InformacaoTecnicaEmSeccoesComDica_QuantidadeAntesDaRefMf | Ordem de campos, placeholders e classes; DoesNotContain sem «aparece» prévio em alguns casos. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoCheio.cs` | FerramentasPagesTests.UsoCheio_SoMudaACor_NaFichaENoPesquisar_NuncaAvisaNemBloqueia | Verifica a classe CSS dmo-uso-cheio (regra 10). |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.UsoEscrito.cs` | FerramentasPagesTests.Criar_CaixaDoUso_DeixaEscreverOValorTodo | Só verifica o atributo maxlength do input. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.Voltar.cs` | FerramentasPagesTests.Ficha_OVoltar_VoltaAPaginaDeOndeSeVeio_SemOrigemALista | Só compara o HTML do botão; a troca é feita por JavaScript não testado. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.VolumeDaBq.cs` | FerramentasPagesTests.Criar_TituloDoBloco_VolumeNaBqEPesoNaCm | Verifica spans com atributo hidden, ou seja, apresentação. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs` | FerramentasPagesTests.Criar_APartirDoCartaoDoJobOn_OTipoFicaFixo | Verifica atributos HTML, ordem de elementos e classes; não regista nada. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs` | FerramentasPagesTests.GetIndex_ShowsHumanFactsList_ToolIdOnlyInUrl_NeverAField | Muitas ausências de strings (sessionStorage, .html, <script>) e atributos data-*; pouco efeito de negócio. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs` | FerramentasPagesTests.PostCreate_Refusal_KeepsTheTechnicalValues_AndOpensThem | Não verifica na base que nada foi gravado. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs` | FerramentasPagesTests.CreateTool_Refusal_KeepsTheTechnicalName | Não verifica na base que nada foi gravado. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs` | FerramentasPagesTests.GetFicha_ComposesFactsAndGenesisTpGeometry_ShownAsRegistered | Dezenas de DoesNotContain de strings e rótulos; apresentação. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs` | FerramentasPagesTests.PostEdit_SavedMeanwhileByAnotherUser_RefusesFactsAndTechnical_NothingSaved | Em memória a versão é sempre 0 (regra 7); o token real só em PostgreSQL. |
| `tests/DMO.Alpha.Web.Tests/FerramentasPagesTests.cs` | FerramentasPagesTests.GetFicha_OldRefusalLinks_StillShowTheirMessages | Só confere textos de mensagens; sem efeito de negócio. |
| `tests/DMO.Alpha.Web.Tests/FerramentasServicesRegistrationTests.cs` | FerramentasServicesRegistrationTests.FerramentasServices_ResolveAsScopedImplementations | Teste de ligação de injeção de dependências; não garante regra de negócio. |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs` | JobOnFichaTests.Ficha_AcoesEFactos_AoUsoDoHtml | Muitos DoesNotContain de texto e atributos data-jof; pouco efeito. |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs` | JobOnFichaTests.CartoesDasFerramentas_ONomeEAbrirFicha_LevamAFichaDaFerramenta | Compara HTML com classes dos links; não segue o link. |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs` | JobOnFichaTests.CartoesDasFerramentas_AssociarOuCriar_EEscolhidaMostraSoAInformacao | Verifica textos de botões e atributos data-*. |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs` | JobOnFichaTests.CartoesDasFerramentas_RetirarSoNoNovoJobOn_ENuncaNoMf | Só atributos e textos de botões; nenhum efeito gravado. |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs` | JobOnFichaTests.Ficha_SemColocacaoManualNemRetirarDoPlaneamento | Quase só DoesNotContain de texto. |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs` | JobOnFolhaPrimeiraFilaTests.Folha_PrimeiraFila_CmMfBq_NotasEmTodosOsCartoes_RotulosSemFichaDeMoldes | Verifica ordem de cartões, classes e atributos data-jof; layout. |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaPrimeiraFilaTests.cs` | JobOnFolhaPrimeiraFilaTests.Folha_CartoesNaoColapsam_ComLarguraMinimaEMedidasSemScroll | Lê o ficheiro CSS e procura texto; verifica estilos (regra 10). |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFolhaTests.cs` | JobOnFolhaTests.Editar_MostraAsFamiliasAosPares_ComAQtdStockEAQtdMaqEmCadaFamiliaENaCal | Ordem de atributos e rótulos; DoesNotContain «opcional»; layout. |
| `tests/DMO.Alpha.Web.Tests/JobOn/RascunhoEEnterNaProcuraTests.cs` | RascunhoEEnterNaProcuraTests.NovoJobOn_AFolhaLevaAChaveDoRascunho_EOScriptQueOGuarda | Só atributos e src do script; o rascunho em si não é testado. |
| `tests/DMO.Alpha.Web.Tests/JobOn/RascunhoEEnterNaProcuraTests.cs` | RascunhoEEnterNaProcuraTests.ReparacaoProgramada_OEnterNaReferenciaENoLoteSubmeteAProcura_ENaoOAdicionar | Procura texto do script na página; o JavaScript não é executado. |
| `tests/DMO.Alpha.Web.Tests/JobOnCabecalhoCssTests.cs` | JobOnCabecalhoCssTests.CabecalhoDoJobOn_ReferenciaEProducaoNaoSeTocam | Lê o ficheiro CSS e procura texto (regra 10). |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.CriarDuplicar.cs` | JobOnPageTests.FolhaNova_TP_ODiametroSemPS_ECampoIgual | Só texto de rótulos. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaNova.cs` | JobOnPageTests.FolhaNova_AbreBloqueada_ComEditar_OpcoesControloEReparacao_SemNovaProducao | Ordem de botões, textos e dezenas de DoesNotContain; layout. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.FolhaUsoCheio.cs` | JobOnPageTests.Folha_FerramentaA100PorCento_ReferenciaELoteComCorLigeira | Verifica a classe CSS dmo-uso-cheio (regra 10). |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.PainelCores.cs` | JobOnPageTests.FolhaNova_PainelLateral_CadaCartaoLevaACorDaSuaLinha | Verifica apenas classes CSS mch-X (regra 10). |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.Planeamento_CriarJobOn_FicaNaBarraDaProcura_ADireitaDasCaixas_ComODia | Posições de elementos, classes e ajuda encostada; layout. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.EditFlow_SavedMeanwhileByAnotherUser_RefusesAndShowsWhatWasSaved | Em memória a versão é sempre 0 (regra 7); duas pessoas ao mesmo tempo só valem em PostgreSQL. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.EditFlow_TheSameSaveTwice_GoesWhereTheFirstWent_WithoutTheRefusal | Usa versão falsa «7» em memória; o token real é do PostgreSQL. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.SelectedCalendarDay_ClickUnselects_AndNoDayShowsOnlyTheHint | Só atributos data-dmo-* e classes; o clique é JavaScript. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.Calendario_ELinhasDoDia_TemIdsEstaveis_ParaOFocoFicarNoSitio | Só verifica ids no HTML; o foco real não é testado. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.ProximaMudanca_FicaANegrito | Lê o CSS e procura texto (regra 10). |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.Calendario_SoMarcaOsDiasEmQueUmaLinhaMuda | Distingue dias só pela classe CSS «has». |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.Procura_AoEscrever_SemLogin_ERecusada | Só exige código diferente de 200. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.Historico_Filtros_TrocamSoOsResultadosEAsProducoes | Compara HTML fora das zonas e atributos data-dmo-*; sem efeito de negócio. |
| `tests/DMO.Alpha.Web.Tests/JobOnServicesRegistrationTests.cs` | JobOnServicesRegistrationTests.JobOnServices_ResolveAsScopedImplementations | Teste de injeção de dependências; não garante regra de negócio. |
| `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.Filtros.cs` | PlaneamentoSumarioPageTests.Filtro_TemImportante_JobOn_Armazem_EControlo | Só opções e ordem do menu. |
| `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPageTests.cs` | PlaneamentoSumarioPageTests.Linhas_TemADataAFrente_AMaquinaComACor_ESemContagens | Pode verificar cor/estilo em vez do efeito (regra 10) |
| `tests/DMO.Alpha.Web.Tests/ToolCandidateQueryRegistrationTests.cs` | ToolCandidateQueryRegistrationTests.IToolCandidateQuery_ResolvesAsScopedToolCandidateQuery | Só verifica registo no contentor DI |
| `tests/DMO.Alpha.Web.Tests/ToolLookupQueryRegistrationTests.cs` | ToolLookupQueryRegistrationTests.IToolLookupQuery_ResolvesAsScopedToolLookupQuery | Só verifica registo no contentor DI |

### Admin, login, base de dados e partes comuns

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Admin/TemplateAdminServiceTests.cs` | TemplateAdminServiceTests.CreateTemplate_MintedKeysAreUniqueAndWellFormed | 25 chaves aleatórias de 8 hex quase nunca colidem: passa mesmo sem o re-mint; a unicidade real é do índice (Postgres). |
| `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs` | UserAdminServiceTests.CreateDmoUser_PersistenceFailureAfterProvisioning_PersistsNothing | O interceptor falha TODA a gravação, por isso Users vazio é trivial; não prova o tudo-ou-nada User+atribuição (isso é do Postgres TudoOuNada). |
| `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs` | UserAdminServiceTests.DeleteUser_EmptyProviderUserId_RemovesTheRowWithoutCallingTheProvider | Não verifica que o provider não foi chamado (o nome promete); o provider falso devolve falha por omissão, mas nada conta as chamadas. |
| `tests/DMO.Alpha.Infrastructure.Tests/SupabaseAuthenticationProviderTests.cs` | SupabaseAuthenticationProviderTests.OLoginEsperaNoMaximo15Segundos | Só compara uma constante com ela própria; não prova que o pedido é mesmo cortado ao fim do tempo. |
| `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs` | MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt | Compara a lista de nomes das 58 migrações; obriga a editar o teste a cada migração nova e não prova nenhuma regra. |
| `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs` | MigrationFoundationTests.Baseline_GeneratesValidPostgreSql | Procura texto no SQL gerado, sem o executar; os testes PostgreSQL já aplicam a migração. |
| `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs` | DefinicoesNavTabTests.Admin_SeestTheDefinicoesTab_EncostadoADireita | Verifica o estilo inline «margin-inline-start:auto» (regra 10); a parte de links e redireções é útil. |
| `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs` | DefinicoesNavTabTests.Definicoes_Admin_NaoTemOCartaoSap | Só «não aparece» (DoesNotContain) sem um «aparece» na mesma zona; passa com a página vazia. |
| `tests/DMO.Alpha.Web.Tests/Admin/DefinicoesNavTabTests.cs` | DefinicoesNavTabTests.Anonymous_DoesNotSeeTheDefinicoesTab | Só «não aparece» na página de login, sem «aparece» antes. |
| `tests/DMO.Alpha.Web.Tests/AdminAuthorizationPolicyTests.cs` | AdminAuthorizationPolicyTests.AdminFoundationServices_RegisterWithScopedLifetime | Testa só o registo de dependências (tipo e tempo de vida), não uma regra do negócio. |
| `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.Gestao.cs` | AdminUsersAndTemplatesPagesTests.AdminTemplates_SemCaixas_EscolhidoEscuroComLetraBranca | Compara texto de regras CSS (opacity, background, color) em vez do efeito; a regra de aspeto deve estar na guarda do design (regra 10). |
| `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs` | AdminUsersAndTemplatesPagesTests.AdminUsers_TableHasZeroButtons_RowSelectsInvertedAndDoubleClickOpensTheFicha | Mistura regra útil (sem botões em linhas, Diogo) com «não contém .dmu-row-select» e atributos data-dmo-open; o nome promete linha invertida, que não verifica. |
| `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs` | AdminUsersAndTemplatesPagesTests.Admin_DefinicoesPlaceholdersAreLinked_AndNewPagesAreDressedWithTheAdminShell | Muitos «não aparece» (Setup, Canal Jobon, dmo-secondary-nav) e comprova a classe «active»; o Admin passou para Definições › Admin, estrutura já mudada. |
| `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs` | AuthenticationTests.Login_Page_HasNoRecoveryNotice | Só «não aparece» sem «aparece» antes. |
| `tests/DMO.Alpha.Web.Tests/DefinicoesNumSoSitioTests.cs` | DefinicoesNumSoSitioTests.Pessoal_SoTemAContaAPalavraPasseEAAparencia | Grande parte compara markup de posição (botão no canto, classe dmo-card-topo-acoes, tabela exata) em vez do efeito. |
| `tests/DMO.Alpha.Web.Tests/DefinicoesTrocaTests.cs` | DefinicoesTrocaTests.E10_CabecalhoDeCartaoComAcoes_NoDesignSystem | Só procura texto de classes no ficheiro CSS (nem o efeito). |
| `tests/DMO.Alpha.Web.Tests/ModuleEntryTests.cs` | ModuleEntryTests.InsideTheModule_PagesShowNoPermissionNotices | Só «não aparece» («capability», «Sem permissão») sem «aparece» antes. |
| `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs` | SharedComponentsTests.HelpTexts_LiveInTheTip_NotWrittenOnTheScreen (Theory, 15 páginas) | Só procura a palavra «clique» nos .cshtml; passa com muitos outros textos explicativos. |
| `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs` | SharedComponentsTests.AdminUsersToasts_StayClearOfTheCreateButton | Compara texto de uma regra CSS específica; não prova que não tapa. |
| `tests/DMO.Alpha.Web.Tests/SharedComponentsTests.cs` | SharedComponentsTests.FerramentasTechnicalNotice_KeepsItsLine | Compara texto CSS (min-height:1lh); não mede nada. |
| `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs` | UiStyleRuleTests.ButtonAndFieldText_IsCentered_WithPinnedFontMetrics | Compara texto de declarações CSS exatas (ascent-override...); sem regra do Diogo nem efeito. |
| `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs` | UiStyleRuleTests.DisabledPagerButtons_DoNotFadeInline | Só procura «opacity:» num ficheiro. |
| `tests/DMO.Alpha.Web.Tests/UiStyleRuleTests.cs` | UiStyleRuleTests.AutofilledFields_UseTheSameFontAsChromePreview | Compara texto CSS do autofill; efeito só visível no browser. |

### Reparação Interna, JavaScript (ShellProof) e browser (visual)

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/ReparacaoInterna/ReparacoesInternasConsultaTests.cs` | ReparacoesInternasConsultaTests.ResumoPorPessoa_ContaReparacoesDiasEMedia_SemAsAnuladas | Rui 3/3 e Ana 2/2 dão ambas 1,00: trocar reparações/dias por dias/reparações passa. Usar uma pessoa com 4 reparações em 2 dias. |
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaConsultaPaginaTests.cs` | ReparacaoInternaConsultaPaginaTests.OsDoisSeparadores_RegistarEConsultar | Só verifica href e aria-current (marcação); nenhum efeito. |
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaCorrigirPaginaTests.cs` | ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ODuploCliqueAbreAReparacao_ComCorrigirEAnular | Grande parte são classes CSS (dmo-dialog ri-janela), ordem de atributos e regex da estrutura (regra 10). |
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs` | ReparacaoInternaOutraFerramentaPaginaTests.OCartaoDoNumero_TemAMarcaOutraCm_EOsCamposCurtos_ODaProducaoFicaIgual | Classes ri-referencia/ri-lote, maxlength e string exata do checkbox: marcação, não efeito. |
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaOutraFerramentaPaginaTests.cs` | ReparacaoInternaOutraFerramentaPaginaTests.CorrigirParaOMesmoNumero_NadaMuda_ESemAviso | Faz só um GET com ?correcao=igual e vê que não há toast; nunca corrige nada nem mostra um aviso antes. Duplica Corrigir_ParaOMesmoNumero (infra). |
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs` | ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas | Quase tudo marcação (beta-production-rail, class selected, data-dmo-*); só o redirect do início é efeito. |
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs` | ReparacaoInternaPaginaTests.SemProducao_AvisaEOFormularioPedeAReferenciaEOLote | Só verifica texto e atributos required; não envia nem vê o que fica gravado. |
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaZonasPaginaTests.cs` | ReparacaoInternaZonasPaginaTests.SemOCabecalho_AResposta_EAPaginaInteiraDeSempre | Só vê que a página tem html e ids; não compara com resposta com cabeçalho. Parte do que prova já está nos outros testes da classe. |
| `tests/ShellProof/historico-menu.test.cjs` | historico-menu.test.cjs::o script da página foi encontrado | Só verifica que o script foi lido; não prova regra. |
| `tests/ShellProof/jobon-tool-picker.test.cjs` | jobon-tool-picker.test.cjs::a página desenha os cartões CM, MF e BQ | Só verifica lista de nomes e <script> no cshtml. |
| `tests/ShellProof/peso-live.test.cjs` | peso-live.test.cjs::a folha de teste usa as marcas das zonas | Procura texto no cshtml (ordem e marcas), não comportamento. |
| `tests/ShellProof/programada-enter.test.cjs` | programada-enter.test.cjs::o script do Enter está na página | Só assert.ok(script). |
| `tests/visual/calendario_parcial_browser.py` | calendario_parcial_browser.py (script) | ÓRFÃO (não está no ci-local.sh); precisa de servidor stress local, só confirma erros. |
| `tests/visual/ecra_pequeno_browser.py` | ecra_pequeno_browser.py (script) | Mede aspeto (scroll lateral, posições), regra 10/13. Corre no CI. |
| `tests/visual/ferramentas_parcial_servidor_browser.py` | ferramentas_parcial_servidor_browser.py (script) | ÓRFÃO; precisa de servidor stress local. |
| `tests/visual/ficha_avisos_browser.py` | ficha_avisos_browser.py (script) | ÓRFÃO; mede posições; precisa de servidor stress local. |
| `tests/visual/ficha_botoes_browser.py` | ficha_botoes_browser.py (script) | ÓRFÃO; mede posições (regra do Diogo 18/10) e precisa de servidor stress local. |
| `tests/visual/jobon_folha_browser.py` | jobon_folha_browser.py (script) | Maior parte é geometria; nota ampliada fecha com clique fora (confirmar que é janela e não painel). Corre no CI. |
| `tests/visual/nok_agente_browser.py` | nok_agente_browser.py (script) | Mistura geometria com 1 efeito («abc» enviado à app). Corre no CI. |
| `tests/visual/painel_cores_linhas_browser.py` | painel_cores_linhas_browser.py (script) | Compara cores calculadas (aspeto). Corre no CI. |
| `tests/visual/painel_lateral_browser.py` | painel_lateral_browser.py (script) | Mede contraste, aspeto. Corre no CI. |
| `tests/visual/puncoes_tabela_browser.py` | puncoes_tabela_browser.py (script) | Só larguras de colunas. Corre no CI. |
| `tests/visual/ri_hora_browser.py` | ri_hora_browser.py (script) | Geometria (mesmo topo) e atributo form=; o valor gravado já é testado em C#. Corre no CI. |
| `tests/visual/tecnica_alinhada_browser.py` | tecnica_alinhada_browser.py (script) | ÓRFÃO; só geometria. |


## Testes DUPLICADO

### Controlo (Peso, Pegamentos, Punções, Resumo, Aprovar)

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAprovacaoTests.cs` | ControloAprovacaoTests.UmaFolhaNovaDepoisDeAprovarOsPegamentos_ContinuaAprovado | igual a PegamentosRegistadosDepoisDeAprovarOPeso_ContinuaAprovado (mesma regra) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAprovacaoTests.cs` | ControloAprovacaoTests.NaoAprovarUmaParteSemNota_NaoGravaNada_OControloFicaPorAprovar | igual a NaoAprovarSemNota_NaoGravaNada_OControloFicaPorAprovar (só muda o método) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloAprovacaoTests.cs` | ControloAprovacaoTests.NaoAprovarUmaParteComNota_GravaADecisaoDessaParte_EANota | igual a NaoAprovarComNota_GravaADecisaoDoPeso_EANota (Theory só com Peso) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/ControloHistorySearchQueryTests.cs` | ControloHistorySearchQueryTests.SearchAsync_FiltroData_UsaODiaDeLisboa | mesma regra do dia de Lisboa de SearchAsync_FiltroData_PesoPeloDiaDeLisboa_EOsSemCarimboNaoEntram (aqui para Comparação) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/CreateToolScopedPesoHandlerTests.cs` | CreateToolScopedPesoHandlerTests.Create_VolumesOrNominalNonPositive_RefusedTyped | coberto por UmVolumeOuONominalZeroOuNegativoSozinho_ERecusado_NadaFicaGravado (que testa um campo de cada vez) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/EnsureControloForJobOnHandlerTests.cs` | EnsureControloForJobOnHandlerTests.Ensure_PreCutJobOn_GainsControloByBackfillOnce | igual a Ensure_ReExecution_ReturnsSameControloId (idempotência) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoCapCalculationTests.cs` | PesoCapCalculationTests.Cap_Volume_ExatoPelaFormula | mesma conta D50/H10 de Cap_ValoresDeReferencia_D50_H10 (só aperta a margem) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs` | PesoComparisonQueryTests.FindCandidates_ToolLinkedToB1Only_TheDropdownHasNoB3 | mesma regra de FindCandidates_ToolLinkedToB1AndC3_TheDropdownHasC3 |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs` | PesoComparisonQueryTests.FindCandidates_NuncaMaisDe5Anteriores_MaisAPedida | igual a FindCandidates_SoAsUltimas5_EOPedidoMaisAntigoFica |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoComparisonQueryTests.cs` | PesoComparisonQueryTests.FindCandidates_ProductionWithoutCmContext_AllMachinesOfTheReference | igual a FindCandidates_ToolWithoutMachineLinks_AllMachinesOfTheReference |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoEscolhasQueryTests.cs` | PesoEscolhasQueryTests.CodigosDeMaquinas_VemPorOrdemDoCodigo | igual a PegamentosEscolhasQueryTests.CodigosDeMaquinas_VemPorOrdemDoCodigo |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/PesoTampaoDoJobOnTests.cs` | PesoTampaoDoJobOnTests.FromKnown_UsaODiametroDoJobOn | igual a DiametroDoJobOn_MandaSobreODaCm |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RecordPegamentosMeasurementHandlerTests.cs` | RecordPegamentosMeasurementHandlerTests.CosturaOrContraZeroOrLess_IsImpossible_NothingWritten | coberto por CosturaOuContraZeroOuMenosNaPrimeiraLinha_ERecusada_NadaFicaGravado (que também verifica a folha) |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RecordPegamentosMeasurementHandlerTests.cs` | RecordPegamentosMeasurementHandlerTests.Correction_ToZeroOrLess_IsImpossible_ReadingKeepsItsValues | igual a CosturaOuContraZeroOuMenosNaCorrecao_ERecusada_ALeituraFicaIgual |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RevisaoDepoisDaAprovacaoTests.cs` | RevisaoDepoisDaAprovacaoTests.UmPesoNovoDepoisDeAprovarOPeso_TemAprovacaoPropria_EAParteVoltaAPorAprovar | igual a ControloAprovacaoTests.UmPesoNovoDepoisDeAprovar_FicaPorAprovar |
| `tests/DMO.Alpha.Infrastructure.Tests/Controlo/RevisaoDepoisDaAprovacaoTests.cs` | RevisaoDepoisDaAprovacaoTests.UmaFolhaNovaDepoisDeAprovar_ContinuaAprovado | igual a ControloAprovacaoTests.UmaFolhaNovaDepoisDeAprovarOsPegamentos_ContinuaAprovado |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoComparacaoPageTests.ProducaoFixa.cs` | PesoComparacaoPageTests.ProducaoFixa_SegueNoCompararENoGuardar | mesma regra de PeloResumo_AProducaoFicaFixa_SemReferenciaNemProducao, só continua para Comparar/Guardar |
| `tests/DMO.Alpha.Web.Tests/Controlo/PesoFolhaProducaoAnteriorPageTests.cs` | PesoFolhaProducaoAnteriorPageTests.ProducaoAnterior_SoAsAnteriores_SemAPosteriorNemASemDatas | mesma regra de PesoProducaoAnteriorQueryTests.ComInicio_SoAsQueComecaramAntes_SemAsPosterioresNemAsSemDatas |
| `tests/DMO.Alpha.Web.Tests/ControloCapabilitiesCutHPageTests.cs` | ControloCapabilitiesCutHPageTests.PesoGates_CreatePassesForCapable_ReadingRefusedTypedForChefe | igual a ChefeApproveOnly_CreateRefusedTyped_ApproveSucceeds (permissões do chefe) e AreaState_OperacionalSavesOkNokAndComment_ChefeIsRefusedTyped |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.NumeroFerramenta.cs` | PegamentosPageTests.NumeroFerramenta_LinhaSoComONumero_NaoERegistadaEAvisa | igual a NumeroFerramenta_VariasLinhas_CadaUmaComOSeuNumero_SoONumeroContaComoVazia |
| `tests/DMO.Alpha.Web.Tests/PegamentosPageTests.cs` | PegamentosPageTests.PostMeasurement_UnusableCostura_NoWrite_AsksForCorrection | igual a InvalidCostura_StillRefused_PreservesDraftAndSavedReadings_ThenAllowsCorrection |
| `tests/DMO.Alpha.Web.Tests/PuncoesPageTests.cs` | PuncoesPageTests.NumeroRepetido_PeloFormularioDaPagina_GravaOLoteDessaPuncao | igual a PuncoesHandlerTests.NumeroRepetido_ComOLoteDessaPuncao_RegistaComEsseLote_EOLoteDeCimaFica (mas pelo formulário da página) |

### Boquilhas e Armazém

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BoquilhasCurrentQueryTests.cs` | Card_WithoutCurrentBq_HasNoBq | Repete MachineWithoutProduction_ShowsNoProduction |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementLabelsTests.cs` | Of_NuncaDevolveOMnomeDoEnum | Já coberto por Of_TraduzAsDuasVocabularios (Saida→Saída) |
| `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqMovementLabelsTests.cs` | Of_ComOTipoForte_FalaA_mesmaLinguaDaFolha (Theory x3) | Repete Of_TraduzAsDuasVocabularios |
| `tests/DMO.Alpha.Web.Tests/BoquilhasRegistoPageTests.cs` | BoquilhasRegistoPageTests.NativePath_ServerDataIsSourceOfTruth | Repete PostSaida_PersistsThroughHandler |

### Job On, Ferramentas, Planeamento, SAP

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Production/ProductionPickerQueryTests.cs` | ProductionPickerQueryTests.ComReferencia_PeloInicioOuPelaParteBq_NuncaPeloMeio | Coberto pela Theory ComParteDaReferencia_PeloInicioOuPelaParteBq (casos 018 e 350). |
| `tests/DMO.Alpha.Web.Tests/JobOn/JobOnFichaTests.cs` | JobOnFichaTests.DuplicarEsteJobOn_CopiaOsDadosParaUmaProducaoNova | Igual a JobOnDuplicarFolhaTests.Duplicar_TrazAFolhaDaOrigem_EOGuardarGravaANaProducaoNova, mas só com HTML. |
| `tests/DMO.Alpha.Web.Tests/JobOnPageTests.cs` | JobOnPageTests.Create_WithoutMfSelection_RefusedTyped_NothingCreated | Igual ao caso MissingMfTool de Create_SemCmOuSemBq_ERecusado_ENadaEGravado. |

### Admin, login, base de dados e partes comuns

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs` | UserAdminServiceTests.CreateDmoUser_EmailIsTheAdmins_NoIdentityCreatedInTheProvider | Mesma regra do teste anterior (EmailIsTheAdmins_RefusedBeforeProvisioning), só muda o provider usado e as maiúsculas. |
| `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs` | UserAdminServiceTests.SetCurrentUserTemplate_AdminAssociatedIdentity_AdminRefusedNeverHoldsATemplate | Mesma regra de AdminAuthorizationServiceTests.AuthorizeTemplateAssignment_AdminEmailUser_IsRefused, mas aqui pelo serviço de utilizadores (efeito gravado verificado). |
| `tests/DMO.Alpha.Infrastructure.Tests/Admin/UserAdminServiceTests.cs` | UserAdminServiceTests.TemplateWithBothControlos_CanBeUsedForCreationAndAssignment | Já coberto por CreateAndEditTemplate_BothControlos_AreSaved e Resolve_TemplateWithBothControlos_GrantsBoth. |
| `tests/DMO.Alpha.Web.Tests/AdminAuthorizationPolicyTests.cs` | AdminAuthorizationPolicyTests.RequireAdminPolicy_UnconfiguredInstallation_DeniesAnonymous | O caso anónimo já está em RequireAdminPolicy_PrincipalWithoutAdminClaim_IsDenied. |
| `tests/DMO.Alpha.Web.Tests/AdminAuthorizationPolicyTests.cs` | AdminAuthorizationPolicyTests.AdminPages_UnconfiguredInstallation_DoesNotAllowAnonymous | Mesma recusa a anónimos de AdminPages_ConfiguredInstallation_UnauthenticatedAndOperatorAreDenied. |
| `tests/DMO.Alpha.Web.Tests/AdminUsersAndTemplatesPagesTests.cs` | AdminUsersAndTemplatesPagesTests.AdminUsersAndTemplatesPages_AreGatedByTheRequireAdminFolderPolicy | Mesma recusa a anónimo e operador de AdminPages_ConfiguredInstallation_UnauthenticatedAndOperatorAreDenied (só muda as rotas). |
| `tests/DMO.Alpha.Web.Tests/AuthenticationTests.ServicoIndisponivel.cs` | AuthenticationTests.Login_EmSupabase_OPedidoEsperaNoMaximo15Segundos | Mesma constante de SupabaseAuthenticationProviderTests.OLoginEsperaNoMaximo15Segundos; só lê http.Timeout. |
| `tests/DMO.Alpha.Web.Tests/AuthenticationTests.Tentativas.cs` | AuthenticationTests.Login_CincoPalavrasPasseErradas_MandaConsultarOAdmin | Já coberto por UserLogin_RepeatedFailures_NeverLockTheUser (i < 5 vs ≥ 5). |
| `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs` | AuthenticationTests.ForcedPasswordChange_ProviderTemporaryPassword_CanBeCleared | Mesmo fluxo de ForcedPasswordChange_UserSetsNewPassword_CanAccessApp, só muda a origem da marca de temporária. |
| `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs` | AuthenticationTests.AdminLogin_EmailChangedInDefinicoes_OldEmailIsNoLongerAdmin | Mesma regra de AdminLogin_OnlyTheDefinicoesEmailBecomesAdmin (email que autentica mas não é o do campo). |
| `tests/DMO.Alpha.Web.Tests/AuthenticationTests.cs` | AuthenticationTests.AdminHierarchy_AfterActivation_RequiresAdmin | Mesma recusa/entrada de AdminPages_ConfiguredInstallation_* (AdminAuthorizationPolicyTests). |

### Reparação Interna, JavaScript (ShellProof) e browser (visual)

| Ficheiro | Teste | Nota |
|---|---|---|
| `tests/DMO.Alpha.Web.Tests/ReparacaoInterna/ReparacaoInternaPaginaTests.cs` | ReparacaoInternaPaginaTests.EliminarAProducao_AvisaQueSePerdemAsReparacoes | Mesma frase que EliminarJobOnComReparacoes_AJanelaDizQuantasSePerdem (3 reparações), já coberta pela página real. |


## Regras importantes sem teste e piores testes, por área

### Controlo (Peso, Pegamentos, Punções, Resumo, Aprovar)


1. Duas pessoas a gravar o mesmo Peso/Pegamentos/Puncoes ao mesmo tempo (token de concorrência, índice único) só é testado em memória — fonte: docs/architecture/REGRAS-DOS-TESTES.md (regra 7), peso-arrumado-pr212.md — onde testar: DMO.Alpha.Infrastructure.Tests/Controlo/*SchemaTests (PostgreSQL) — como: dois contextos Npgsql a gravar a mesma linha e verificar que o segundo é recusado sem gravar nada.
2. Envio repetido (duplo clique, mesmo envio duas vezes) não escreve duas vezes — fonte: REGRAS-DOS-TESTES.md (regras 5 e 7) — onde testar: PegamentosSchemaTests / PesoToolScopedSchemaTests — como: repetir o envio em PostgreSQL e contar as linhas na base.
3. O PDF dos Pegamentos e do Peso só é validado pelo cabeçalho %PDF ou por tamanho, não pelo texto — fonte: REGRAS-DOS-TESTES.md (regra 12) — onde testar: classe dos testes de PDF de Pegamentos e Peso — como: extrair o texto e verificar valores, «Produção anterior» e ausência de «SAP».
4. O PDF da PU é sempre claro: ninguém guarda — fonte: memória do Diogo (PDF da PU sempre claro) — onde testar: Puncoes PDF tests — como: ler o PDF e confirmar fundo/cores claros independentemente do tema.
5. «SAP» nunca aparece no Peso (aparece «Produção anterior» com Média/Moda, «X de Y dias») — fonte: peso-arrumado-pr212.md — onde testar: PesoPageTests — como: página com dados de SAP; o texto contém «Produção anterior» e nunca «SAP».
6. Nunca botões dentro das linhas (um só botão no canto, radios escondidos) em Pegamentos/PU/Peso — fonte: peso-arrumado-pr212.md, design-164-integracao.md (A-0226) — onde testar: EnvioEmailPesoPageTests e páginas de Pegamentos/PU — como: corrigir o teste ERRADO e afirmar zero botões dentro das linhas.
7. Eliminar/Remover em último e só a bola de ajuda e o triângulo como ícones — fonte: memória UI do Diogo — onde testar: páginas do Controlo — como: verificar a ordem dos botões pelo texto.
8. Ligação por tool_id (cm_id/mf_id/bq_id) em todas as consultas do Controlo, nunca pelo texto — fonte: memória do Diogo — onde testar: queries de comparação — como: ferramentas com o mesmo texto e ids diferentes devolvem apenas a do id.
9. Registo mais recente em cima em todas as listas — fonte: memória do Diogo — onde testar: queries de listagem — como: três registos com datas diferentes e verificar a ordem.
10. As consultas «não gravam nada» só olham para o ChangeTracker — fonte: REGRAS-DOS-TESTES.md (regra 5) — onde testar: PegamentosEscolhasQueryTests, PesoComparacaoEstadoQueryTests, PesoEscolhasQueryTests — como: reler as tabelas da base depois do pedido.

#### Piores testes
- EnvioEmailPesoPageTests.Modelos_UmaLinhaEscolhe_EUmSoAbrirNoCanto (ERRADO): exige botões dentro das linhas (ctl-def-x); contradiz «nunca botões dentro das linhas».
- EnvioEmailPesoPageTests.Modelos_UmSoModelo_VemEscolhido: só verifica classes/atributos.
- Estrutura_Folha_SoOCorpoDesliza e Estrutura_PainelDeApoio_TemOEsqueletoFixo: só CSS/classes (regra 10).
- PegamentosPdf_SemSeloDeAprovacao: só DoesNotContain, sem afirmar que o PDF tem conteúdo.
- PdfCompleto_ComUmPesoSemLeituras_DeixaOVazioDeFora: compara tamanhos em vez do texto.
- PegamentosEscolhasQueryTests.OsDoisPedidos_NaoGravamNada e PesoEscolhasQueryTests.OsQuatroPedidos_NaoGravamNada: só ChangeTracker vazio.
- ControloResumoSchemaTests (3 de 4): só estrutura do modelo EF.
- Testes de idempotência/concorrência (SameEnvioTwice, RepeatedClicks, Create_WhenAnotherRequestSavedFirst): só em memória.

### Boquilhas e Armazém


Base de comparação: memória da equipa, código atual e histórico git (sem o blueprint).

#### Faltas (regras sem teste, ou só em memória)

1. O mesmo envio de um movimento de BQ (mesma chave de envio único, em simultâneo) grava uma só vez, na base real — fonte: src/DMO.Alpha.Infrastructure/Data/DmoDbContext.cs (EnvioUnicoPedidos) — onde testar: Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.BqConcurrency.cs — como: dois envios com a mesma chave via Task.WhenAll em PostgreSQL e contar 1 movimento relido; hoje só há BoquilhasSemJobOn.EnvioUnico e EnvioUnico em memória.
2. Só pode existir um registo sem Job On por BQ (índice parcial por tool_id) — fonte: src/DMO.Alpha.Infrastructure/Data/DmoDbContext.cs (entidade BqRepairTrace) — onde testar: Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.NadaRepetido.cs — como: inserir dois registos sem bq_id para a mesma BQ e esperar recusa; hoje só há o caso do bq_id (Boquilhas_UmaBqDaProducao...) e testes de metadados EF em BqRepairTraceIntegrationTests.
3. O Excel «Atualizar» pára no primeiro dia igual ao da app e recua dia a dia — fonte: src/DMO.Alpha.Web/Pages/Boquilhas/Definicoes.cshtml.cs (handler ExcelAtualizar, nota de 22/10) — onde testar: Web.Tests/BqRepairerSettingsPageTests.Excel.cs — como: três casos (ontem igual não escreve nada, só ontem difere, ontem e anteontem diferem) e verificar a lista de dias atualizados.
4. Os PDFs do Histórico das Boquilhas têm o conteúdo certo (máquina filtrada, corte aos 366 dias, mês) — fonte: src/DMO.Alpha.Web/Pages/Boquilhas (handler PdfHistorico) — onde testar: Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.cs — como: extrair o texto do PDF (como nos PDFs do Controlo) e verificar que só tem a B1, que a grelha longa pára no dia 366 e que o mês tem os movimentos semeados.
5. As consultas de leitura da ficha, associação e escolhas não escrevem na base — fonte: src/DMO.Alpha.Infrastructure/Boquilhas (BoquilhasFichaQuery, EscolhasQuery, AssociacaoQuery) — onde testar: Infrastructure.Tests/Boquilhas/Boquilhas*QueryTests.cs — como: contar linhas e versões de todas as tabelas relendo com um contexto novo, em vez de olhar para o ChangeTracker.
6. Gravar movimentos não altera o Job On nem o bq_id (ao nível do handler) — fonte: src/DMO.Alpha.Infrastructure/Boquilhas/RegisterBqMovementHandler.cs — onde testar: Infrastructure.Tests/Boquilhas/RegisterBqMovementHandlerTests.cs — como: mover o teste de BqMovementIntegrationTests (que insere à mão) para chamar o handler e comparar Job On e contexto antes e depois; hoje só o teste de página cobre isto.
7. Recusa de Corrigir movimento com data ilegível não altera o movimento — fonte: src/DMO.Alpha.Infrastructure/Boquilhas/EditBqMovementHandler.cs — onde testar: Web.Tests/BoquilhasAvisosTests.cs — como: reler o movimento da base depois da recusa e comparar data e quantidade com as originais.
8. Reparador com nome acima do limite recusado sem gravar nada (Armazém) — fonte: src/DMO.Alpha.Infrastructure/Armazem (Reparadores) — onde testar: Infrastructure.Tests/Armazem/ReparadoresTests.cs — como: no teste Criar_NomeLongo_NaoGrava reler a tabela e contar zero reparadores, e acrescentar os casos limite-1, limite e limite+1.

#### Piores testes

- Infrastructure.Tests/Boquilhas/BqMovementIntegrationTests (Movements_DoNotChangeJobOnOrBqContextData, MultipleMovements_BelongToOnePreProductionTrace, Movement_ResolvesItsTrace) — FRACO: inserem linhas direto no DbContext e leem-nas; nenhum código da app corre, por isso nunca falham.
- Infrastructure.Tests/Boquilhas/BqRepairTraceIntegrationTests (ModelShape_UniqueBqContext_..., RelationalSchema_..., PreProductionTrace_...OneUnresolvedPerToolByIndex) — FRACO: verificam metadados do EF ou texto do script, não a recusa na base real.
- Web.Tests/Boquilhas/BoquilhasHistoricoPageTests (cinco testes PdfHistorico_*) — FRACO: só confirmam %PDF, nome e tamanho; não lêem o texto.
- Web.Tests/Boquilhas/BoquilhasHistoricoPageTests.History_CalendarDay_ChangesOnlyTheZones — FRACO: compara atributos e classes de marcação, não o efeito para o operador.
- Web.Tests/BoquilhasRegistoPageTests.MachinesPanel_DirectLinkOpensProductionRegisterAndKeepsSearchCollapsed — FRACO: dezenas de asserções de classes CSS e escreve HTML de evidência.
- Web.Tests/BoquilhasRegistoTrocaTests (Registo_TrocaSoAsZonas_SemFocoAutomaticoNoTipo, Registo_ComUmaJanelaAberta_OFocoVaiParaAJanela) — FRACO: afirmam classes, ordem de marcação e autofocus.
- Web.Tests/BoquilhasRegistoPageTests.MachinesCard_ShowsTheThreeLiveValues_WhenTheCurrentBqExists — FRACO: o nome promete três valores, verifica dois e só pelo texto do rótulo.
- Infrastructure.Tests/Boquilhas/*QueryTests (OsPedidos_NaoGravamNada e semelhantes) — FRACO: verificam o ChangeTracker vazio, não a base relida.

Nota: há 22 testes OBSOLETOS em Infrastructure.Tests/Armazem (ArmazemMapa, ArmazemPosicoes, ArmazemProximos, ArmazemEntradaComPar): os serviços estão registados no Program.cs mas nenhuma página os usa, e a página já não envia com_par. Há 4 DUPLICADOS e 0 ERRADOS.

### Job On, Ferramentas, Planeamento, SAP


Nota: já existem muitos testes PostgreSQL de concorrência em tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.* (EdicoesAoMesmoTempo, Concurrency, EliminarJobOn, ContextToolIdentity, Overlap), por isso trincos/versões de Ferramenta e Job On estão cobertos; faltam os casos abaixo.

1. Aviso «SAP tem X em vez de Y… substituir? Sim/Não» para quantidade de lote e % uso: recusar/substituir não grava nada sem confirmação — fonte: sap-fabrico-zfmd.md, utilizacao-delta-estimado.md — onde testar: DMO.Alpha.Web.Tests/FerramentasPagesTests (nova classe FerramentasSapAvisoTests) — como: gravar valor diferente do SAP, ver aviso com os dois valores, responder Não e reler da BD que nada mudou, Sim e reler o valor SAP. (Texto «SAP tem» não existe no código: confirmar se está implementado.)
2. Eliminar ferramenta (DeleteTool) é tudo-ou-nada e o Job On/produções ligados mantêm o id da ferramenta — fonte: ferramentas-quantidade-nome-tecnico.md (ligação por cm_id/mf_id/bq_id) — onde testar: Infrastructure.Tests/Data/PostgresMigrationIntegrationTests (novo ficheiro EliminarFerramenta) — como: PG, forçar falha a meio e reler que nada foi apagado; eliminar ao mesmo tempo que se associa uma produção.
3. Dois envios simultâneos do mesmo Job On novo (mesmo nº de produção) gravam uma só vez e o segundo recebe recusa tipada — fonte: fluxo-jobon-novo.md — onde testar: PostgresMigrationIntegrationTests.JobOn* — como: duas tarefas em paralelo com CreateJobOnHandler, reler contagem=1 e uma recusa.
4. Aviso «Sem registo» no Armazém ligado por omissão (Dmo:Avisos:ArmazemSemRegisto hoje false por omissão e testado como tal) — fonte: MEMORY.md (Armazém sem registo = «Sem registo» + aviso) — onde testar: JobOn/JobOnAvisoArmazemEmEsperaTests e JobOnFichaTests — como: corrigir o teste para exigir aviso por omissão e testar o interruptor só como exceção.
5. Ferramenta B1 com PSX em vez de NNX no nome técnico: aviso na ficha e no Job On — fonte: ferramentas-quantidade-nome-tecnico.md — onde testar: FerramentasPagesTests (página) além de TechnicalNameParserAvisoTests (só parser) — como: abrir ficha com nome PSX e verificar o aviso na página real.
6. UsoSapCodigo.Corresponde só trata dígitos: lotes MGnn e regra BQ NX* não ligam o uso SAP — fonte: utilizacao-delta-estimado.md, sap-fabrico-zfmd.md — onde testar: Infrastructure.Tests/Tools/FerramentasUsoSapTests — como: casos MG01 e NX* com resultado esperado de ligação por id.
7. Painéis não fecham ao clicar fora; Eliminar/Opções em último — fonte: estrutura-paineis-paginas.md — onde testar: Chromium nos fluxos principais (regra 13), ficheiro de fluxo Job On/Ferramentas — como: abrir painel, clicar fora, afirmar que continua aberto; ler a ordem dos botões do menu.
8. Quantidade de lote AN à espera de Tool.AnQuantity (DMO-531) — fonte: ferramentas-quantidade-nome-tecnico.md, git DMO-531 — onde testar: FerramentasPagesTests — como: criar AN, verificar que a quantidade vem do campo próprio e não do SAP.
9. Ligação ZPPG por nome técnico + lote — fonte: sap-fabrico-zfmd.md — onde testar: Infrastructure.Tests/Tools (SapFabrico) — como: duas ferramentas com o mesmo lote e técnicos diferentes ligam à certa.
10. Perfil Job On Ver nunca vê Controlo/RI/Boquilhas em nenhuma página — fonte: fluxo-jobon-novo.md — onde testar: PlaneamentoSumarioPageTests.SoVer — como: percorrer todas as páginas do perfil e afirmar ausência após afirmar presença nas páginas permitidas.

#### Piores testes

- JobOnAvisoArmazemEmEsperaTests.PorOmissao_AAplicacaoNaoOLigaNasDefinicoes — ERRADO: fixa o aviso «Sem registo» desligado por omissão (MEMORY.md diz aviso sempre).
- JobOnFichaTests.AvisoDoArmazem_EmEspera_NaoApareceNaFicha_NemNoEndpoint_MasOsOutrosSim — ERRADO/FRACO: consagra a ausência do aviso do Armazém, contra a decisão do Diogo.
- EdicoesAoMesmoTempoTests (Infrastructure, em memória) — FRACO: versão é sempre 0 em memória (regra 7); a garantia real está só nos testes PG.
- FerramentasPagesTests.PostEdit_SavedMeanwhileByAnotherUser_RefusesFactsAndTechnical_NothingSaved — FRACO: depende de em memória para a recusa por versão.
- JobOnPageTests.EditFlow_SavedMeanwhileByAnotherUser_RefusesAndShowsWhatWasSaved — FRACO: mesma razão, trinco só em memória.
- FerramentasPagesTests.CliquesRepetidos.* (3 testes) — FRACO: só verificam HTML, não que o segundo envio não grava.
- ToolRegistryIntegrationTests (3) — FRACO: asserts triviais.
- ToolCandidateQueryRegistrationTests / ToolLookupQueryRegistrationTests / JobOnServicesRegistrationTests — FRACO: apenas registo no contentor DI.

### Admin, login, base de dados e partes comuns


Comparado só com a memória do projeto , o código atual e o histórico git. O blueprint não foi usado.

1. Dois ADMIN criam um utilizador com o mesmo nº ao mesmo tempo: a identidade nasce no Supabase e a gravação falha no índice único; a app apaga a identidade mas deixa subir o erro como 500 em vez de «nº já existe» — fonte: UserAdminService.CreateDmoUserAsync (catch que repõe e faz `throw`); REGRAS-DOS-TESTES pontos 7 e 8 (duas pessoas ao mesmo tempo no PostgreSQL) — onde testar: Infrastructure.Tests/Data, nova partial PostgresMigrationIntegrationTests.UtilizadoresAoMesmoTempo.cs — como: dois CreateDmoUserAsync em paralelo com o mesmo nº e dois contextos, esperar um Success e um DuplicateOperator (sem exceção), um só User, uma só identidade no provider falso.
2. Eliminar utilizador: o Supabase apaga a identidade e depois a gravação na base falha; o código devolve PersistenceFailure, mas nenhum teste cobre esse caminho nem que repetir termina o trabalho — fonte: UserAdminService.DeleteUserAsync (catch DbUpdateException) — onde testar: Infrastructure.Tests/Admin/UserAdminServiceTests.cs — como: interceptor que falha só o SaveChanges do Remove, esperar PersistenceFailure, o User ainda lá, e uma segunda chamada com SuccessIdentityAlreadyMissing.
3. Todas as definições gerais (PIN do cadeado) recusam e não gravam sem PIN: só as Boquilhas têm este teste, e há 26 usos de [DefinicaoGeral] (Controlo, Ferramentas, Armazém, Reparação Interna, Admin) — fonte: definicoes-estrutura.md («pastas e gerais continuam a pedir PIN», 26/10) — onde testar: Web.Tests/DefinicoesNumSoSitioTests.cs — como: teste que percorre por reflexão os handlers marcados [DefinicaoGeral], faz POST sem PIN e verifica redireção `geral=sem-permissao` e que nada mudou na base.
4. Eliminar uma template enquanto outra pessoa lhe atribui um utilizador: a contagem «em uso» é lida antes de apagar, e só há teste em memória sequencial — fonte: TemplateAdminService.DeleteTemplateAsync; REGRAS-DOS-TESTES ponto 7 — onde testar: Infrastructure.Tests/Data, partial nova TemplatesAoMesmoTempo — como: delete e SetCurrentUserTemplate em paralelo (com BeforeFirstSave como em AoMesmoTempo), esperar ou template apagada sem utilizadores ou recusa InUse, nunca erro 500 nem utilizador ligado a template inexistente.
5. Duas pessoas editam a mesma template (ou a ficha do utilizador) ao mesmo tempo: as templates e os utilizadores não têm token de versão, a última grava por cima sem aviso, ao contrário do Job On e das ferramentas (xmin) — fonte: DmoDbContext (RowVersion só noutras entidades), MEMORY.md («duas pessoas ao mesmo tempo… testado em PostgreSQL») — onde testar: Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EdicoesAoMesmoTempo.cs — como: teste PostgreSQL que edita a template em dois contextos; hoje documenta o comportamento (ou falha se o Diogo decidir recusar a segunda).
6. Reivindicar o email do ADMIN ao mesmo tempo (dois primeiros logins): o comentário diz que o simultâneo que perde a corrida não substitui o email, mas o teste só corre um depois do outro em memória — fonte: AdminEmailSettingsTests (docstring) — onde testar: Infrastructure.Tests/Data, partial nova AdminEmailAoMesmoTempo — como: dois TryClaimAdminEmailAsync em paralelo em PostgreSQL com emails diferentes, um true e um false, uma só linha em ModuleSettings.
7. Gravar atomicamente User e atribuição de template ao criar: o teste de falha usa um interceptor que falha TODA a gravação, por isso passa com ou sem transação — fonte: UserAdminServiceTests.CreateDmoUser_PersistenceFailureAfterProvisioning_PersistsNothing; REGRAS-DOS-TESTES ponto 7 («gravar tudo ou nada») — onde testar: Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.TudoOuNada.cs — como: falhar só a segunda escrita (como o FalhaDepoisDeGravar existente) e verificar que nem User, nem atribuição ficam e que a identidade do provider foi apagada.
8. Listas com o último registo em cima: a lista de utilizadores e a de templates ordenam por nº/chave crescente e os testes (ListManagedUsersPage_…, ListUsers_ProjectsApplicationFacts…) fixam essa ordem; só a página /versao e o Histórico testam «mais recente primeiro» — fonte: MEMORY.md (UI: «listas: último registo em cima») — onde testar: Infrastructure.Tests/Admin/UserAdminServiceTests.cs — como: perguntar ao Diogo se a regra vale para Utilizadores/Templates (nº de operador crescente parece natural); se sim, inverter e ajustar os testes.
9. Rasto das ações do Admin (criar/eliminar utilizador, mudar template, repor acesso, mudar Definições): ninguém sabe quem fez, e não há teste de «quem/quando/antes/depois» — fonte: registos-modulo.md (Diogo 27/10, «por gravar: Definições, utilizadores, fichas», decisão pendente, Registos em standby) — onde testar: Web.Tests/Admin/RegistosPorUtilizadorTests.cs — como: quando o Diogo decidir, uma ação do Admin tem de aparecer em /Admin/Registos com o autor; antes disso, nada a testar.

#### Piores testes

- UserAdminServiceTests.CreateDmoUser_PersistenceFailureAfterProvisioning_PersistsNothing — FRACO: o interceptor falha todas as gravações, passa mesmo sem garantia de tudo-ou-nada (ver falha 7).
- DefinicoesNavTabTests.Admin_SeestTheDefinicoesTab_EncostadoADireita — FRACO: compara o estilo inline `margin-inline-start:auto` (regra 10) em vez do efeito.
- DefinicoesNavTabTests.Definicoes_Admin_NaoTemOCartaoSap — FRACO: só `DoesNotContain`, passa com a página em branco (regra 11).
- DefinicoesNavTabTests.Anonymous_DoesNotSeeTheDefinicoesTab — FRACO: só «não aparece» na página de login, sem «aparece» antes.
- AuthenticationTests.Login_Page_HasNoRecoveryNotice — FRACO: duas negativas sem nenhuma positiva.
- AdminUsersAndTemplatesPagesTests.AdminTemplates_SemCaixas_EscolhidoEscuroComLetraBranca — FRACO: texto de regras CSS na página; devia estar na guarda do design e verificar o efeito.
- SharedComponentsTests.HelpTexts_LiveInTheTip_NotWrittenOnTheScreen — FRACO: só procura a palavra «clique» em 15 ficheiros; textos explicativos com outras palavras passam.
- MigrationFoundationTests.Migrations_StartAtTheBaseline_WithTheNewOnesAfterIt — FRACO: lista de 58 nomes de migração que obriga a editar o teste em cada migração e não prova regra nenhuma.

### Reparação Interna, JavaScript (ShellProof) e browser (visual)


1. Painéis NÃO fecham ao clicar fora (usam «Voltar») — fonte: design-novo.md — onde testar: ShellProof/painel-estrutura.test.cjs — como: abrir o painel, disparar clique no próprio `<dialog>`/fundo e afirmar que continua aberto; só «Fechar»/Voltar fecha.
2. O painel tem o botão «Fechar» (DmoEstruturaTagHelpers.cs) mas a regra do Diogo diz «Voltar»; nenhum teste fixa o nome — fonte: design-novo.md, estrutura-paineis-paginas.md — onde testar: DMO.Alpha.Web.Tests (teste do tag helper dmo-painel) — como: afirmar o texto do botão de saída do painel e que o fundo não fecha.
3. A chefia (ri.view) não corrige as reparações dos outros pela app: os testes só provam o serviço e um POST montado com o formulário do autor — fonte: ri-permissoes.md (DS-PERM-01) — onde testar: ReparacaoInternaCorrigirPaginaTests — como: quando o Diogo decidir, abrir a Consulta como chefia, ver a linha de outra pessoa e corrigir pelo formulário que a própria Consulta desenha.
4. Corrigir/Associar/Registar ao mesmo tempo por duas pessoas só têm teste PostgreSQL para registar e anular (fora desta área); falta Corrigir e Associar em simultâneo — fonte: REGRAS-DOS-TESTES.md pontos 7 e 8 — onde testar: PostgresMigrationIntegrationTests.ReparacaoInternaRasto — como: dois contextos a corrigir/associar a mesma reparação com o mesmo token e afirmar um só rasto.
5. «Só algarismos, até 3» só vive na página; o serviço e a base aceitam letras e 40 caracteres (Corrigir e Registar por outra via) — fonte: Index.cshtml.cs (Diogo 23/10 e 24/10) — onde testar: ReparacoesInternasTests/CorrigirTests — como: decidir onde a regra mora e testar o serviço com «12a» e «1000» (ou documentar que os testes de 40 caracteres são só do limite da base).
6. Lista de ShellProof corre à mão: painel-propria-pagina.test.cjs não está no `test:js` do package.json e nunca corre — fonte: tests/ShellProof/package.json — onde testar: package.json — como: acrescentá-lo (ou fazer `node --test *.test.cjs`) e um teste que falhe se um ficheiro .test.cjs não estiver na lista.
7. 11 scripts de tests/visual não correm no CI (ver nota abaixo); incluem «Aprovar/Não aprovar» (fluxo principal, regra 13) e gravacoes_zonas — fonte: scripts/ci/ci-local.sh — onde testar: ci-local.sh — como: ligar os que não precisam do servidor stress (revisao_aprovar pelo HTML exportado) ou apagar os que não vão correr.
8. Histórico e Consulta: «último registo em cima» só está provado na lista do Registar e na Consulta de serviço, não na página do Histórico — fonte: design-novo.md (listas com o último em cima) — onde testar: ReparacaoInternaHistoricoPaginaTests — como: registar duas, abrir o Histórico e afirmar a ordem com PosicaoNoHtml.
9. Posto: PIN errado não cria sessão só é provado pelo código 200; falta afirmar que a página seguinte continua a pedir o Posto — fonte: vistoria-backend.md (Posto, 27/10) — onde testar: PostoTravaoTests — como: após os PINs errados, GET /ReparacaoInterna deve redirecionar para /login ou /Posto.
10. Resumo por pessoa na Consulta: média por dia com valores diferentes de 1 — fonte: ReparacoesInternasConsultaTests — onde testar: ReparacoesInternasConsultaTests.ResumoPorPessoa — como: Rui com 4 reparações em 2 dias deve dar 2,00.

#### Nota: os testes visual/*.py são corridos pelo CI?
ci-local.sh corre só 12 scripts: boquilhas_catalog, paginacao_parcial, nok_agente, painel_lateral, painel_cores_linhas, puncoes_tabela, ajuda_junto, ri_hora, jobon_folha, ecra_pequeno, pasta_pdf, excel_bq (CI_SEM_BROWSER=1 salta-os). ÓRFÃOS (nenhum script do CI os chama): calendario_parcial, definicoes_parcial, ferramentas_parcial_servidor, ficha_avisos, ficha_botoes, gravacoes_zonas, historicos_parcial, resumo_parcial, revisao_aprovar, revisao_parcial_servidor, tecnica_alinhada (precisam de um servidor «stress» local à parte), mais design-review.cjs e validate_pdf_evidence.py (este último fora por desenho, testes.md). grupos.py só agrupa os projetos dotnet (TRX), não corre visual. ShellProof: 36 de 37 ficheiros estão no `test:js`; painel-propria-pagina.test.cjs é órfão. Nenhum visual tem funções test_ (são scripts).

#### Piores testes
1. ReparacaoInternaOutraFerramentaPaginaTests.Consultar_PorAssociar_AMarca_AEscrita_EOFiltro_SemBotoesNasLinhas — ERRADO a confirmar: exige link «Por associar» que abre janela dentro de cada linha (nunca botões dentro de linhas).
2. visual/ajuda_junto_browser.py — ERRADO: exige zero círculos de ajuda, mas a bola de ajuda é ícone permitido (botoes-seletores.md 28/10).
3. ReparacaoInternaOutraFerramentaPaginaTests.CorrigirParaOMesmoNumero_NadaMuda_ESemAviso — FRACO: só um GET sem corrigir nada, passa sempre.
4. ReparacaoInternaPaginaTests.ComAChave_AbreComOMenuEAsZonas — FRACO: marcação e classes, quase sem efeito.
5. ReparacaoInternaCorrigirPaginaTests.ATabela_SemBotoes_ODuploCliqueAbreAReparacao_ComCorrigirEAnular — FRACO: classes e regex da estrutura.
6. ReparacoesInternasConsultaTests.ResumoPorPessoa_ContaReparacoesDiasEMedia_SemAsAnuladas — FRACO: média 1,00 em ambas as pessoas.
7. ReparacaoInternaPaginaTests.SemProducao_AvisaEOFormularioPedeAReferenciaEOLote — FRACO: só atributos required, não grava nada.
8. ShellProof peso-live «a folha de teste usa as marcas das zonas», jobon-tool-picker «a página desenha os cartões», programada-enter «o script está na página», historico-menu «o script foi encontrado» — FRACO: só procuram texto nos ficheiros.


# DMO-663 — A-0408 — avisos SAP

Pedido do proprietário de 29/10/2026. Foram acrescentados testes. O proprietário autorizou apenas acrescentar a migração à lista exata de MigrationFoundationTests; as verificações e entradas anteriores ficaram intactas. A lista ShellProof ganhou o ficheiro novo.

O registo oficial e a impressão foram gerados pela guarda em [scripts/ci/testes-alteracoes.md](../../scripts/ci/testes-alteracoes.md); o ID novo A-0408 foi fixado ao reservado pelo proprietário, sem alterar entradas anteriores nem as verificações da guarda.

| Teste novo | Ficheiro | Resultado |
|---|---|---|
| `Zppg_LeAPrimeiraQuantidadeOFornecedorEADataPurchase` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_SimGravaNaQuantidadeDoTipoCertoESoNoToolIdEscolhido` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_NaoMantemQuantidadeEUsoManual_NaoRepeteMesmoValorEMostraNovoValor` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_SimUsaOPercentDoServidorEDepoisVoltaAAplicarAutomaticamente` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_UmaDecisaoAntigaNaoSubstituiUmaImportacaoMaisRecente` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_AMigracaoSoAcrescentaColunasOpcionais` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_DuasDecisoesDaMesmaFicha_SoUmaGrava` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_EditarOutrosFactosDaCmNaoApagaAQuantidadeAceite` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_UmValorManualIgualAoSapContinuaProtegidoQuandoOSapMuda` | [PostgresMigrationIntegrationTests.AvisosSap.cs](../../tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.AvisosSap.cs) | Passou |
| `Sap_AFichaMostraFornecedorECompraNaInformacaoOpcional_EOSimGravaAPeca` | [DefinicoesPorModuloPageTests.AvisosSap.cs](../../tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs) | Passou |
| `Sap_ONaoMantemAQuantidadeENaoVoltaAMostrarOMesmoAviso` | [DefinicoesPorModuloPageTests.AvisosSap.cs](../../tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs) | Passou |
| `Sap_UmaDecisaoSemAntiforgeryNaoGrava` | [DefinicoesPorModuloPageTests.AvisosSap.cs](../../tests/DMO.Alpha.Web.Tests/DefinicoesPorModuloPageTests.AvisosSap.cs) | Passou |
| `O × fecha só o aviso escolhido, sem decidir «Não» nem enviar o formulário` | [ferramentas-avisos-sap.test.cjs](../../tests/ShellProof/ferramentas-avisos-sap.test.cjs) | Passou |
| `Os avisos carregados depois de uma troca parcial também se podem fechar` | [ferramentas-avisos-sap.test.cjs](../../tests/ShellProof/ferramentas-avisos-sap.test.cjs) | Passou |

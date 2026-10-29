# DMO-663 — avisos SAP (peças, % uso, fornecedor/data)

**Base:** `integracao`, 0.3.0-beta.242, `8f1470fd89a24a914986ec26e7b813577fcf6363`. **Registo:** A-0408. **Prioridade de entrada:** 1.

## O que foi feito

- ZPPG: guardar a primeira coluna `No. of pieces`, `Supplier` e `Purchase`. Os títulos foram confirmados no `tabela.HTML` fornecido pelo proprietário; a data de aquisição foi confirmada por ele. O VBS exporta os campos novos em UTF-8 e continua a sair pelo «Instalar» do Admin.
- Guardar o retrato SAP na ferramenta. Avisar quando a quantidade difere: BQ usa `BqAccountedQuantity`; CM/MF usam `AnQuantity`. «Sim» aplica o valor guardado no servidor; «Não» mantém a app e memoriza o valor recusado.
- Manter a percentagem manual até haver «Sim». Sem percentagem manual, a atualização continua automática. Uma leitura SAP igual à manual não lhe muda a origem.
- Mostrar fornecedor e aquisição no bloco existente «Informação opcional», partilhado pelos tipos de ferramenta. Avisos no topo à direita do cartão, com «Sim», «Não» e ×; o × só fecha o aviso nessa vista.
- Decisões por `tool_id`, com antiforgery, módulo Ferramentas, transação, trinco de linha e versão. Uma ficha antiga não aplica uma decisão sobre dados entretanto alterados. A resolução existente dos códigos de equipamento SAP foi mantida.

## Ficheiros

| Parte | Ficheiros principais |
|---|---|
| Contrato e persistência | `Core/Tools/{Tool,UsoSap,AvisosSap}.cs`, `Data/DmoDbContext.cs`, migração `20261029104943_AvisosSapPecasFornecedorData` e snapshot |
| Regras | `Infrastructure/Tools/{FerramentasUsoSap,FerramentasAvisosSap,EditToolFactsHandler}.cs` |
| Interface | `Pages/Ferramentas/{Ficha,AvisosSap,_AvisosSap,_FerramentaFactos,FerramentaFormulario}`, `Program.cs`, `ferramentas-avisos-sap.js/.css` |
| Exportação | `Web/Sap/DMO_USO_SAP.vbs` |
| Provas | `PostgresMigrationIntegrationTests.AvisosSap.cs`, `DefinicoesPorModuloPageTests.AvisosSap.cs`, `ferramentas-avisos-sap.test.cjs`, lista ShellProof e registos dos testes |

## Testes e resultados

**CI local: OK** (`docker compose run --rm ci`, Linux, PostgreSQL 16 descartável). Passaram **2 022 Infrastructure + 1 425 Web = 3 447 .NET**, **346 JavaScript** e todos os grupos Chromium. Os **21 casos novos** passaram (16 Infrastructure, 3 Web, 2 JS); **8/8 mutações** foram detetadas. Os 6 testes externos Supabase continuam ignorados como já estavam, sem alterações aos testes ou respetivas condições.

A execução usou a imagem do dmo-ci com CA do ambiente e Node 22 oficial; a imagem foi compactada para resolver a falta de disco, sem mudar scripts de CI ou desligar verificações.

Os testes e as regras que garantem estão em [testes-alteracoes.md](testes-alteracoes.md) e [REGISTO.md](REGISTO.md). A guarda oficial regista A-0408. No teste antigo `MigrationFoundationTests`, foi acrescentado apenas o nome da migração, com autorização explícita do proprietário nesta conversa; as verificações e entradas anteriores ficaram intactas. Os restantes testes antigos não foram alterados.

## Decisões e limites

- O HTML tem duas colunas `No. of pieces`: foi mantida a primeira, como o script já fazia. `Purchase` é a aquisição, não uma data de produção ou venda.
- A migração só acrescenta sete colunas opcionais a `Tools`; não altera registos existentes. Os ficheiros antigos sem os campos novos continuam aceites e não apagam os dados opcionais já lidos.
- A informação nova acompanha a linha lida separadamente, preservando o contrato antigo `UsoSapLinha`. A edição normal de uma CM/MF conserva a quantidade aceite do SAP.
- Não houve mudança de versão, ReleaseNotes, publicação ou acesso a dados reais. Depois de publicar, aplicar a migração antes do código e atualizar o VBS local pelo «Instalar». Um script personalizado no Admin continua a prevalecer: precisa de receber as colunas novas ou de voltar ao original atualizado.
- Falta validar a execução do VBS no Windows/SAP GUI real; o ambiente de CI é Linux. O parsing, a persistência, os formulários, o JavaScript e a suite completa são verificados localmente.

# Como continuar o estilo DMO
Ler primeiro [HANDOFF-ASTRA.md](HANDOFF-ASTRA.md). A referência é 1366 × 768 e o design contemporâneo aprovado, não a tentativa de recoloração rejeitada.

## Fontes por ordem de utilização
- Decisões atuais do utilizador neste handoff.
- [dmo-beta](https://github.com/dmo-app/dmo-beta): código e testes do comportamento existente.
- [Blueprint — mapa](https://github.com/dmo-app/blueprint/blob/main/COMO-FUNCIONA.md).
- [Frontend comum](https://github.com/dmo-app/blueprint/blob/main/shared/frontend/FRONTEND.md).
- [Contrato frontend/backend](https://github.com/dmo-app/blueprint/blob/main/shared/contracts/FRONTEND_BACKEND.md).
- [Contexto de produção](https://github.com/dmo-app/blueprint/blob/main/shared/contracts/PRODUCTION_CONTEXT.md).
- [Escritas e repetição de envios](https://github.com/dmo-app/blueprint/blob/main/shared/backend/WRITES.md).
- [Segurança e autoria](https://github.com/dmo-app/blueprint/blob/main/shared/contracts/SEGURANCA-E-AUTORIA.md).
- Dentro da app: docs/ui-reference/componentes-partilhados.md e atualizar-so-o-que-muda.md. As regras antigas de aparência são contexto histórico onde contradizem a direção aprovada agora.

## Mapa dos módulos
| Área | Código Razor em src/DMO.Alpha.Web/Pages | Requisitos no blueprint |
|---|---|---|
| Planeamento / Job On | JobOn/Index.cshtml, JobOn/Folha.cshtml, Planeamento/Sumario.cshtml | [Job On](https://github.com/dmo-app/blueprint/tree/main/modules/job-on), [Planeamento](https://github.com/dmo-app/blueprint/tree/main/modules/planeamento) |
| Ferramentas | Ferramentas/Index.cshtml, Ferramentas/Ficha.cshtml | [Ferramentas](https://github.com/dmo-app/blueprint/tree/main/modules/ferramentas) |
| Controlo | Controlo/Resumo.cshtml, Peso.cshtml, Pegamentos.cshtml, Puncoes.cshtml | [Controlo](https://github.com/dmo-app/blueprint/tree/main/modules/controlo) |
| Boquilhas | Boquilhas/Index.cshtml e partials | [Boquilhas](https://github.com/dmo-app/blueprint/tree/main/modules/boquilhas) |
| Armazém | Armazem/Movimentos.cshtml, Ultimos.cshtml | [Armazém](https://github.com/dmo-app/blueprint/tree/main/modules/armazem) |
| Reparação | ReparacaoInterna/Index.cshtml, Consultar.cshtml | [Reparação](https://github.com/dmo-app/blueprint/tree/main/modules/reparacao-interna) |
| Administração | Admin e Definicoes | [Admin](https://github.com/dmo-app/blueprint/tree/main/modules/admin) |

## Componentes a preservar/reutilizar
- Shared/_DmoShellLayout.cshtml, _DmoBranding.cshtml, _DmoSessionUtilities.cshtml, _DmoNavigation.cshtml: shell, logo, utilizador real e tabs.
- Services/ShellStateFactory.cs e Presentation/ShellPresentation.cs: permissões, visibilidade, módulos e funções. Não codificar um menu estático que ignore o Template.
- wwwroot/0_ASSET_LOGO.png: logo verdadeiro BA Glass.
- wwwroot/fonts/inter-latin-wght-normal.woff2: fonte local existente.
- wwwroot/0_ASSET_CANONICAL_INTERACTIONS.js: DmoTroca, regiões parciais, listas e envios.
- wwwroot/dmo-live.js e js/dmo-suggest.js: ligação de listas e sugestões.
- wwwroot/js/jobon-folha.js: alterações de ferramentas, notas, imagem, Linhas.
- JobOn/Folha.cshtml.cs e Core/Production/JobOnFolhaCampos.cs: fonte dos dados e identidades dos campos.
- Painel de apoio: terminar como componente comum; existe apenas um começo experimental no patch.

## Gramática visual
Paleta base: azul #4f6f9c, apoio #779ecb, claro #b7c9e2, navy #1b263b, tinta #0b1320. Superfícies claras e fundos neutros. Estados OK/NOK/alerta e linhas mantêm semântica explícita.

Navegação global no topo; funções do módulo na linha abaixo. Identidade do registo e ações numa faixa compacta. Filtros junto da tabela. Apoio lateral sob pedido, com título, fechar, retorno de foco e contexto da origem preservado.

Agrupar por proximidade, alinhamento e separadores discretos. Não desenhar uma caixa grande por valor. AN/BQ e ARR/PU são pares; artigo não repete peso/secções/processo. Notas vazias não ocupam espaço de leitura. Texto real, sem KPI inventado. Números usam ponto decimal, como a app atual.

## Semântica que os mockups não podem alterar
- Job On é função do Planeamento, não módulo global extra.
- Número de Reparação é identificador, não quantidade.
- Boquilhas: quantidade de lote/fabricação/reparação/discrepância não são um KPI genérico de stock.
- Armazém preserva Entrada/Saída e os campos específicos de cada operação.
- Controlo preserva jobon_id/controlo_id e permissões. Ausência de medições não equivale a NOK.
- Campos novos e obrigatórios não se inventam para preencher áreas vazias.
- PDFs e impressão têm composição própria; não aplicar o CSS de ecrã sem validação.

## Verificação e entrega
Base .NET 10: dotnet build DMO.Alpha.slnx e dotnet test tests/DMO.Alpha.Web.Tests.
Configuração: README original da base e .env.example. Não levar credenciais de produção para o repositório de design. Não há necessidade de mudar Supabase ou migrar dados para redesenhar.

Fixtures visuais: definir DMO_UI_EVIDENCE_DIR=artifacts/design ao executar os testes Web. O script experimental tests/visual/design-review.cjs do patch usa Node + playwright; DMO_CHROMIUM permite escolher browser instalado. O script verifica largura e erros de JS, mas **não impõe ainda altura máxima nem testa todos os fluxos do painel**.

Antes de dar um ecrã por terminado:
- viewport 1366 × 768, 100% de zoom;
- conteúdo representativo e estados vazio, carregamento, erro, leitura e edição;
- informação e ações principais visíveis, sem cortes;
- detalhes e listas extensas acessíveis por painéis/paginação;
- teclado/foco/fechar/regresso e dados por guardar;
- navegação principal fora do painel;
- handler real e permissões intactos;
- capturas revistas visualmente e resultado dos testes registado.

As decisões novas devem entrar neste guia e no handoff. Separar sempre implementado, testado, aprovado pelo utilizador e ainda proposto.


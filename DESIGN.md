# DMO Design — estado

Começar pelo [handoff para Astra](docs/design/HANDOFF-ASTRA.md) e pelo [guia de continuidade](docs/design/CONTINUAR.md).

Este repositório contém a base Razor Pages proveniente do dmo-beta (`3e3b175`, `0.3.0-beta.100`) com a camada de design aplicada no working tree. A primeira tentativa, rejeitada, continua guardada em [rascunho-rejeitado.patch](docs/design/rascunho-rejeitado.patch) e **não** deve ser aplicada.

## Implementado (26/10/2026)

- Navegação de módulo e funções em **tabs no topo**, no cabeçalho, com o logo BA e a conta autenticada reais (`css/dmo-design.css`, `_DmoShellLayout`, `_DmoNavigation`).
- Paleta do Owner — `#4f6f9c`, `#779ecb`, `#b7c9e2`, `#1b263b`, `#0b1320` — em modo claro e escuro. Superfícies planas (sem sombras), ações secundárias em contorno e a **ação principal em navy**. Tabelas com cabeçalho em caixa normal.
- **Painel de apoio deslizante** concluído: `_DmoSupportPanel` + `js/dmo-support.js` + CSS em `dmo-design.css`. Abre pela lateral (620 px) com `?painel=1`, esconde o shell dentro do painel, tem «Abrir página», «Fechar», foco de retorno e confirmação de alterações por guardar. Liga a Ficha da ferramenta, Peso, Pegamentos, PU NNPB, Revisão e Consulta de reparação.
- **Folha do Job On** (`Pages/JobOn/Folha.cshtml`): stock, dimensões, pares AN/BQ e ARR/PU, notas vazias omitidas, «Criar» como ação principal e a composição do mockup.
- Logo e PDFs (`DmoPdfPalette.cs`) alinhados com a paleta; documento em `docs/ui-reference/paleta-oficial.md`.
- `tests/visual/design-review.cjs` para inspecionar os HTML exportados pelos testes.
- **Revisão de 26/10/2026 (pedido do Owner: «não quero este azul em lado nenhum»; «manter o comportamento dos botões como na original»):**
  - Botões: sai a substituição do `dmo-design.css` (contorno nos secundários, navy no principal). Vale o padrão único do blueprint (FRONTEND § Paleta, BP-20261026-001) já em `dmo-normalize.css`: todos sólidos, invertem no hover, steel ao premir. `--dmo-action` passa de `#1b263b` a `var(--dmo-palette-slate)` no `0_ASSET_CANONICAL_DESIGN_SYSTEM.css`.
  - Sem navy no modo claro: o cartão do Peso no Resumo deixa de ser um bloco escuro.
  - Cabeçalho: logo a 40 px com mais espaço; à direita o nome, o nome do Template por baixo e «Sair», separados das tabs por um traço.
  - Factos (`dmo-facts`) numa só faixa com divisórias; títulos e factos do Controlo (Resumo, Peso, PU NNPB, Revisão) sem aresta colorida.
  - Cores das máquinas mantidas como na app (linhas tingidas, painel lateral); o hover de uma linha vazia no painel já não fica escuro.
  - Composição dos mockups (Owner: «como os mockups»): o painel lateral das linhas passa a uma fila de seletores por cima da página (Boquilhas, Planeamento, Reparação), tingidos com a cor da linha e a linha aberta a cheio; calendário com dias simples sobre traços; rótulos em caixa normal (sem maiúsculas pequenas); valores das Boquilhas numa faixa; subtítulos explicativos das páginas escondidos (a ajuda fica no círculo).
  - Cabeçalho por esta ordem: módulos … versão · Definições · nome e Template · Sair.
  - Controlos mais baixos (Owner: «botões demasiado grandes»): `--dmo-control-height` 34 px e 30 px compacto, botões a 13 px; botões e campos continuam com a mesma altura.
  - Armazém › Registo em três colunas (Owner, sobre o mockup 06): tipo e procura à esquerda, registo ao centro, ferramenta selecionada à direita; últimas entradas e saídas por baixo.
  - Controlo › Resumo como o mockup 04: procura numa linha; datas e aprovação numa faixa; combinação numa linha; Peso à esquerda com Pegamentos e PU NNPB empilhados à direita; estado do documento em linhas (área, estado, OK/NOK, comentário); «Visto por» e «Aprovado por» lado a lado.
  - Reparação como o mockup 07: «Linha …» em título, produção/CM/MF numa faixa, formulário numa linha.
  - Resto da app: Peso, PU NNPB e Revisão com valores numa faixa; Definições com as secções em separadores no topo (sem menu lateral nem cadeado); Admin com títulos como as outras páginas e grupos em caixa normal; Login e Versão sem gradiente; sem pictogramas (⚠ dos avisos, ↗ ↙ das Boquilhas — este na marcação).
  - Tudo em `css/dmo-design.css` (só ecrã), sem mudar handlers nem permissões; na marcação só saíram os glifos das Boquilhas.
  - Design system de referência: https://claude.ai/artifact/CaxJKWoDpibvJSCheUbuRp (página App para explorar).

## Verificado

- Build da solução: **0 erros**.
- **22 páginas** pedidas com HTTP 200, todas dentro de **1366 × 768** (documento ≤ viewport, sem overflow horizontal); a matriz do histórico das Boquilhas rola dentro do próprio cartão.
- Painel de apoio testado nos 6 destinos com sessão real: **0 px de overflow** em todos; `Escape` fecha; o foco volta para a linha que abriu; com formulário sujo pede «Continuar a editar / Fechar sem guardar»; as tabs de módulo e de função não abrem no painel.
- Ferramentas com dados reais (ferramentas registadas pelo próprio formulário): lista, seleção e ficha a funcionar com a nova camada.
- Consola: **0 erros JS** nas 22 páginas. Modo escuro revisto.
- Telemóvel (390 × 844): as 23 páginas medidas, incluindo o login, sem overflow.
- Contratos de estilo: paleta num só sítio, nenhum branco solto em `background`, geradores de PDF sem cores soltas.

Corrigido durante esta passagem: foco devolvido ao fechar o painel (antes caía no `<body>`), campos da Ficha que estouravam em superfícies estreitas, o cabeçalho e a fila de seletores do rail em 390 px, e a tabela «Contas por módulo» das Definições que estava fora de `dmo-table-wrap` (defeito pré-existente do dmo-beta: com a regra de base `min-width:760px` abaixo de 720 px fazia a página inteira rolar no telemóvel; agora rola dentro do invólucro, como pede `componentes-partilhados.md`).

## Ainda não verificado aqui

- Suite xUnit completa: o testhost não arranca neste sandbox (acesso negado ao handle do processo pai). Os testes de estilo foram validados por script equivalente; os restantes correm no CI/portátil.
- Aprovação visual do Owner, ensaio com dados reais de produção (Job On, Controlo e Boquilhas precisam de máquinas e produções na base), impressão/PDF com a nova camada e os fluxos de escrita do painel (gravar, recusa, ligação a cair).

## Pendente / decisões

- Boquilhas, Armazém e Reparação mantêm o painel lateral contextual. O mockup sugere uma fila fina de seletores dentro do conteúdo; não foi mudado para não partir a seleção existente.
- O mockup de Planeamento usa linhas em azul-neutro; a app mantém a cor própria de cada linha (regra de 23/10/2026).

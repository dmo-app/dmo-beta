# Índice dos testes

Esta lista é o CI local: `scripts/ci/ci-local.sh` corre cada projeto de testes uma só vez e `scripts/ci/grupos.py` reparte os resultados por esta tabela, de cima para baixo. No fim diz, grupo a grupo, se passou, e termina com «CI local: OK», «CI local: FALHOU» ou «CI local: INCOMPLETO». INCOMPLETO quer dizer que uma parte foi desligada de propósito (`CI_SEM_POSTGRES=1`, `CI_SEM_NODE=1` ou `CI_SEM_BROWSER=1`) e diz qual; os testes dessa parte não correram, por isso não vale como OK.

Antes de compilar, o script corre a **guarda dos testes** (`scripts/ci/guarda-testes.py verificar`): compara cada teste com a impressão digital gravada em `scripts/ci/testes-impressao.json` e falha se um teste mudou, saiu ou entrou sem uma explicação em `scripts/ci/testes-alteracoes.md`. Vigia os testes .NET (cada `[Fact]`/`[Theory]`), os de JavaScript (cada `test(...)`), a parte dos ficheiros de teste fora dos testes (ajudantes), os scripts do browser e os ficheiros do próprio CI, incluindo esta tabela. Comentários e espaços não contam. Como registar uma mudança: ver `docs/architecture/REGRAS-DOS-TESTES.md`, ponto 17.

Como mexer:

- **Retirar um grupo:** trocar `sim` por `não` na coluna «Corre». Os testes desse grupo deixam de contar e o resumo diz que foi retirado.
- **Mudar a ordem:** mudar a linha de sítio.
- **Mudar o que um grupo apanha:** editar as colunas «Infrastructure» e «Web». São partes do nome das classes de teste, separadas por vírgulas (por exemplo `Boquilhas` apanha todas as classes com «Boquilhas» no nome ou na pasta). `-` quer dizer nenhuma.
- Um teste conta uma só vez, no primeiro grupo que o apanha. O que nenhum grupo apanhar conta no fim, em «Outros», para nada ficar esquecido. Os testes de um grupo retirado correm, mas não contam nem fazem falhar o CI.

| Corre | Nome | O que verifica | Usa | Infrastructure | Web |
|---|---|---|---|---|---|
| sim | Base de dados e migrações | Que a migração Baseline cria as tabelas, as chaves e a linha REMOVED, que as constraints do PostgreSQL protegem os dados, e que o modelo da app bate certo com a base. | PostgreSQL descartável criado pelo script | .Data., Schema | - |
| sim | Login e sessão | Entrar com nº de operador ou email, palavra-passe errada, mudar a palavra-passe, a sessão sobreviver aos redeploys, página de erro e versão. | Supabase simulado (os 6 testes com o Supabase real ficam ignorados sem credenciais) | Supabase | Authentication, PersistencePolicy, ErrorPage, AppVersion |
| sim | Permissões | Quem pode abrir cada módulo e cada página, o ADMIN e os templates de acesso. | Base em memória | AdminAuthorization | AdminAuthorizationPolicy, ControloCapabilities, ModuleEntry |
| sim | Administração | Criar, editar e arquivar utilizadores e templates, o email do ADMIN e as Definições. | Base em memória | UserAdmin, TemplateAdmin, AdminEmail, SaveModuleSetting | AdminUsersAndTemplates, DefinicoesNavTab, BqRepairerSettings, DefinicoesPorModulo |
| sim | Job On | Criar e editar produções, calote, avisos de sobreposição, factos das ferramentas e a ficha, e o Sumário do Planeamento. | Base em memória (o Sumário também no PostgreSQL descartável) | .Production. | JobOn, PlaneamentoSumario |
| sim | Ferramentas | Criar, duplicar e editar ferramentas, lotes, quantidades por lote, procura e nome técnico. | Base em memória | .Tools. | Ferramentas, ToolCandidate, ToolLookup |
| sim | Boquilhas | Movimentos, registos de reparação, correções, histórico, grelha e PDFs das Boquilhas, e o número de consultas da página. | Base em memória | Boquilhas, Bq | Boquilhas |
| sim | Peso | Cálculo do peso e da calote, comparação de pesos, densidade da água, histórico e PDF do Peso. | Base em memória | Peso | Peso |
| sim | Pegamentos | Medições, limites, cálculo das médias, folha em branco e PDF dos Pegamentos. | Base em memória | Pegamentos | Pegamentos |
| sim | Armazém | As posições (códigos, arquivar e reativar, procura e páginas), os reparadores de CM e MF, a Entrada, a Saída e o Corrigir localização (ocupação, localização, estado, avisos e envio único), e as páginas do Armazém com a sua chave. | Base em memória | .Armazem. | .Armazem. |
| sim | Resumo e Aprovar | O Resumo da produção, o estado OK/NOK, o Aprovar e os PDFs juntos. | Base em memória | Controlo, PdfRecovery | ControloResumo |
| sim | Procura, números e listas | Sugestões ao escrever, formato dos números e paginação das listas. | Base em memória | Suggestion, ReferenceMatch, Runtime | ListPagination |
| sim | Componentes partilhados | Que o calendário só se desenha pelo componente partilhado e que as tabelas usam só a classe dmo-table. | Leitura dos ficheiros das páginas | - | SharedComponents |

Depois da tabela, o script corre ainda testes fora do .NET:

- **JavaScript da app** (`tests/ShellProof/peso-live.test.cjs` e `troca-parcial.test.cjs`, em Node.js com o jsdom): a pré-visualização da folha do Peso no browser e o «atualizar só o que muda» do Job On (trocar só as zonas que mudaram e abrir a página inteira quando muda mais do que isso). Precisa do Node.js; com `CI_SEM_NODE=1` fica de fora e o resumo diz que não correu.
- **Browser nas Boquilhas** (`tests/visual/boquilhas_catalog_browser.py`): o HTML do catálogo que os testes `ToolCatalog_*` exportam, aberto no Chromium pelo Playwright para Python, a 1920, 768 e 390 px e sem JavaScript: «Criar boquilha» com a máquina do filtro, abrir a ficha e a lista que só aparece com a pesquisa escrita (apagá-la esconde a lista no lugar; a máquina só reduz). Precisa do Python 3 (o script instala o Playwright se faltar) e de um Chromium: o do Playwright ou, se essa versão faltar, o mais recente em `PLAYWRIGHT_BROWSERS_PATH` ou `/opt/pw-browsers`; `DMO_CHROMIUM` escolhe outro. Com `CI_SEM_BROWSER=1` fica de fora. Antes de abrir o HTML, tira o « · local» do selo da versão, que só aparece com alterações por gravar no git, para o resultado não depender do estado do repositório (o mesmo na paginação).

- **Browser na paginação** (`tests/visual/paginacao_parcial_browser.py`): o HTML que os testes das Ferramentas e do catálogo das Boquilhas exportam, no Chromium, a 1400 e a 390 px. «Seguinte» e «anterior» trocam só a zona dos resultados, sem recarregar, e os filtros e o cabeçalho ficam. Usa o mesmo Chromium (`tests/visual/dmo_chromium.py`) e o mesmo `CI_SEM_BROWSER=1`.
- **Browser nas falhas do teste do agente** (`tests/visual/nok_agente_browser.py`): o HTML que os testes `*ParaOBrowser` exportam, no Chromium: a dica «Posta a zero» das Boquilhas fechada até ao rato e sem scroll lateral (1400 e 390 px), «abc» no Lote do PU NNPB enviado à app em vez da bolha do browser, os dois botões do Peso no Resumo na linha do título a 1400 px, e os botões da Revisão do Controlo («Limpar filtros» e os do controlo aberto) à direita, na linha dos filtros, a 1400 px. Usa o mesmo Chromium e o mesmo `CI_SEM_BROWSER=1`.
- **Browser na pasta dos PDFs** (`tests/visual/pasta_pdf_browser.py`): escolher a pasta, guardar por referência/produção, deixar de usar e um duplo clique em «Exportar PDF» (um só pedido, um só ficheiro, uma só mensagem), com a página e o PDF simulados.

Os outros scripts de `tests/visual` (gravações, históricos, Resumo, Revisão, Ficha, calendário) precisam da app local com a base de teste grande (`scripts/stress/arrancar_app.sh`) e correm-se à mão.

O `tests/visual/validate_pdf_evidence.py` não entra: compara os PDFs com um PDF de referência que está no computador do Diogo e precisa do Poppler e do PyMuPDF.

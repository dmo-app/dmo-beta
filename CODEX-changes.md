# CODEX-changes

Alterações de 23/10/2026 sobre **DMO 0.3.0-beta.24**, commit de origem `996cb0e190322aee8f9274ac6f0207c4e0a8b94f`.
A versão da app não foi incrementada. CI completo aprovado na base beta.24; compatibilização com main beta.28 em curso. Sem merge nem deploy. O registo por página fica em [dmo-Improvements](https://github.com/dmo-app/dmo-Improvements).

## Alterações e motivos

| Página / área | O que mudou | Porque melhora |
|---|---|---|
| Job On → Folha → Editar/Criar/Duplicar | Não consulta o painel das próximas produções quando esse modo não o desenha | Evita trabalho e consultas antes de abrir o formulário |
| Job On → factos das ferramentas | Projeção SQL com localização e último estado/uso; três consultas em vez de oito e sem transferir o histórico completo | Reduz latência e o custo de ferramentas com muitos registos |
| Job On → rascunhos | Chave por conta, esquema e versão; expiração de 30 dias; versão antiga consultável e descartável sem substituir a folha atual | Evita misturar trabalho de contas diferentes e sobrepor alterações posteriores |
| Planeamento → Sumário → por aprovar | Consulta própria dos campos apresentados, usando a regra partilhada de aprovação | Evita carregar ferramentas e cálculos de Peso para uma linha de produção/data/autor |
| Planeamento → Sumário → importantes | Consulta por blocos com continuação e desempate por ID; navegação Anterior/Seguinte; mantém filtros e datas | Permite alcançar pendências além do corte inicial de 500 por família |
| Aviso do Sumário | Chave de cache inclui capacidades da conta | Evita reutilizar resultado calculado com outras permissões |
| Boquilhas → Excel local | Fila conserva eventos durante escrita; Web Locks serializa abas compatíveis; volta a verificar a versão no fim | O movimento recebido durante uma escrita chega ao ficheiro sem esperar por um terceiro movimento |
| Boquilhas → estado Excel | Indicação persistente de atualização/pendências, completude na resposta e novas tentativas ao voltar a ligação ou a aba | Uma escrita parcial deixa de ser apresentada como sucesso completo |
| Boquilhas → leitor Excel | Valida estrutura, relações e partes ZIP; limites de expansão/XML; DTD desativado | Recusa livros inválidos com erro controlado e limita consumo de memória |
| Ferramentas → utilização SAP | Duas leituras em lote em vez de duas por ferramenta; transação e trinco antes de ler o estado | Reduz pedidos à base e impede duplicação por importações simultâneas |
| Ferramentas → fabrico SAP | Trinco transacional antes da leitura e dicionário dos registos existentes | Impede colisão entre importações e pesquisa repetida em listas |
| Armazém → mapa | Seleciona o último estado na consulta SQL | Evita carregar todos os estados antigos para mostrar apenas o atual |
| Formulários já marcados EnvioUnico | Resultado e facto persistidos na mesma transação, com trinco por envio, autor e impressão estruturada dos campos/query; conflito 409 se a chave tiver outros dados | Evita duplicação entre instâncias ou reinícios; distingue campos repetidos de campos diferentes |
| Boquilhas → trinco de movimentos | Mantém FOR UPDATE quando já existe uma transação exterior | Preserva a serialização do cálculo das discrepâncias dentro do envio durável |
| Login / palavra-passe | Logs sem corpo de resposta externa nem identificadores; propaga cancelamento | Reduz informação sensível nos logs e distingue cancelamento de indisponibilidade |
| Operação | /health/live e /health/ready com configuração/base/migrações e timeout de 3 s | Distingue processo vivo de aplicação pronta, sem migrar nem chamar Supabase na sonda |
| HTTP | nosniff, Referrer-Policy e cookie Secure obrigatório fora de Development | Reforça proteção mantendo os fluxos locais |
| Build / ambiente | SDK 10.0.402 fixado no global.json, Docker e instalação cloud; runtime Docker 10.0.13 | Reduz diferenças entre compilação local e imagem de publicação |
| CI / navegador | Teste do painel carrega os mesmos ficheiros por HTTP simulado em vez de file://; lista de migrações inclui a migração nova | Permite executar a mesma verificação num Chromium que bloqueia file://; preserva as medições de contraste |

## Provas antes da correção

- Editar: teste autenticado recebia 500 ao tentar carregar o painel que o formulário não mostra; passou após retirar a leitura desnecessária.
- Factos do Job On: o teste com a consulta antiga falhou com `Assert.InRange: Range (1 - 3), Actual 8`; o código corrigido faz três consultas e mantém os últimos valores.
- Medição local da consulta: três ferramentas, 1.000 estados e 1.000 utilizações por ferramenta; mediana de cinco execuções após aquecimento: **8 consultas / 44,2 ms → 3 consultas / 3,1 ms**. Não é o tempo total da página nem uma medição em produção.
- Excel: Chromium falhava com «o movimento que chega durante a escrita também tem de chegar ao Excel»; a fila corrigida passou.
- Rascunhos: os testes recuperavam «DA-CONTA-A» na conta B e «trabalho antigo» sobre «servidor novo» antes do isolamento/versão.
- Excel inválido: estrutura incompleta dava NullReferenceException; um livro com parte expandida excessiva era aceite.
- SAP simultâneo: reproduzidas duas leituras para a mesma ferramenta e violação 23505 no fabrico com dois contextos PostgreSQL; testes passaram depois do trinco.
- Envios: teste HTTP com impressão textual ambígua falhou com `Expected: Conflict, Actual: Found`; a serialização estruturada distingue listas de valores e campos separados.

## Validação

CI oficial na base beta.24: **OK**, sem partes desligadas. Infrastructure: 1.656 aprovados e 6 testes externos separados; Web: 1.043 aprovados; JavaScript: 243 aprovados; todos os cenários Chromium aprovados. Supabase Auth repetido com o código atualizado: **6/6 aprovados**, contas temporárias removidas. A integração com main beta.28 exige nova execução do CI.

Comando reproduzido no ambiente preparado:

```bash
DOTNET_CLI_HOME=/workspace/.dotnet-home \
NUGET_PACKAGES=/workspace/.nuget/packages \
PATH=/workspace/.dotnet:/workspace/.postgres/usr/lib/postgresql/17/bin:$PATH \
LD_LIBRARY_PATH=/workspace/.postgres/usr/lib/x86_64-linux-gnu \
PGDATABASE=postgres \
CI_PG_ADMIN='Host=127.0.0.1;Port=55432;Username=agent' \
DMO_CHROMIUM=/usr/bin/chromium \
NODE_OPTIONS=--test-reporter=tap \
bash scripts/ci/ci-local.sh
```

`NODE_OPTIONS` fixa o formato do relatório que o CI conta. A guarda dos testes foi atualizada pelos comandos oficiais, com a autorização desta conversa e a mudança da lista de migrações registadas em `scripts/ci/testes-alteracoes.md`.

## Migração e publicação

- Migração nova: `20261023105836_EnviosWebPersistidos`, apenas duas colunas opcionais em `OperacoesRegistadas`: `EnvioHash` (64 caracteres) e `EnvioResposta` (16.384).
- Aplicar pelo pré-deploy existente **antes** de iniciar o código novo. Nesta sessão, aplicada apenas em PostgreSQL local descartável.
- A versão anterior ignora as colunas novas; a proteção entre instâncias só é completa quando os processos que atendem esses formulários executam o filtro novo.
- Não foi construída a imagem Docker, aplicado deploy nem alterada a configuração do Railway. As sondas do alojamento têm de usar os endpoints novos para surtirem efeito operacional.

## Decisões e continuação

- O Excel continua local, com ficheiro e anotações manuais preservados. Decisão «5 linhas chega»: mantém-se a grelha de cinco linhas por máquina/dia, sem folha adicional. Se não couber tudo, apresenta pendências.
- Testes destrutivos de migrações só na base PostgreSQL local descartável, nunca no Supabase fornecido. Credenciais não entram no código ou neste registo.
- A navegação do Sumário é por blocos de cada família. Algumas famílias filtram depois do bloco, podendo existir página vazia com continuação. Calendário agregado, consulta leve do ponto do separador e paginação SQL dos lotes/lembretes continuam no plano.
- Produções da ferramenta ainda precisam de paginação SQL; pesquisa/índices precisam de planos medidos; acessibilidade e fluxos completos seguem na revisão página a página.
- Proxy, backups/PITR, restauro e latência no alojamento não foram verificados. Os testes do Excel em Chromium não substituem testar ficheiro aberto no Microsoft Excel em Windows.
- Os registos persistidos dos envios não têm expiração automática nesta alteração; uma política de retenção futura deve preservar o período de repetição necessário.

## Inventário dos ficheiros alterados

- `Dockerfile`
- `global.json`
- `scripts/ci/testes-alteracoes.md`
- `scripts/ci/testes-impressao.json`
- `src/DMO.Alpha.Core/Planeamento/Sumario.cs`
- `src/DMO.Alpha.Core/Runtime/OperacaoRegistada.cs`
- `src/DMO.Alpha.Infrastructure/Armazem/ArmazemMapa.cs`
- `src/DMO.Alpha.Infrastructure/Authentication/SupabaseAuthenticationProvider.cs`
- `src/DMO.Alpha.Infrastructure/Boquilhas/BqExcelLivro.cs`
- `src/DMO.Alpha.Infrastructure/Boquilhas/BqTraceLock.cs`
- `src/DMO.Alpha.Infrastructure/Data/DmoDbContext.cs`
- `src/DMO.Alpha.Infrastructure/Data/EnvioWebPersistido.cs`
- `src/DMO.Alpha.Infrastructure/Migrations/20261023105836_EnviosWebPersistidos.Designer.cs`
- `src/DMO.Alpha.Infrastructure/Migrations/20261023105836_EnviosWebPersistidos.cs`
- `src/DMO.Alpha.Infrastructure/Migrations/DmoDbContextModelSnapshot.cs`
- `src/DMO.Alpha.Infrastructure/Planeamento/SumarioQuery.cs`
- `src/DMO.Alpha.Infrastructure/Production/JobOnToolFactsQuery.cs`
- `src/DMO.Alpha.Infrastructure/Tools/FerramentasFabricoSap.cs`
- `src/DMO.Alpha.Infrastructure/Tools/FerramentasUsoSap.cs`
- `src/DMO.Alpha.Web/Pages/Boquilhas/Definicoes.cshtml.cs`
- `src/DMO.Alpha.Web/Pages/JobOn/Folha.cshtml.cs`
- `src/DMO.Alpha.Web/Pages/Planeamento/Sumario.cshtml`
- `src/DMO.Alpha.Web/Pages/Planeamento/Sumario.cshtml.cs`
- `src/DMO.Alpha.Web/Pages/Shared/_DmoShellLayout.cshtml`
- `src/DMO.Alpha.Web/Program.cs`
- `src/DMO.Alpha.Web/Services/DmoReadinessCheck.cs`
- `src/DMO.Alpha.Web/Services/EnvioRedirect.cs`
- `src/DMO.Alpha.Web/Services/EnvioUnico.cs`
- `src/DMO.Alpha.Web/Services/SumarioNovidades.cs`
- `src/DMO.Alpha.Web/wwwroot/js/dmo-excel-bq.js`
- `src/DMO.Alpha.Web/wwwroot/js/jobon-rascunho.js`
- `tests/DMO.Alpha.Infrastructure.Tests/Boquilhas/BqExcelValidationTests.cs`
- `tests/DMO.Alpha.Infrastructure.Tests/Data/MigrationFoundationTests.cs`
- `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.EnviosWeb.cs`
- `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.JobOnEditar.cs`
- `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SapConcorrente.cs`
- `tests/DMO.Alpha.Infrastructure.Tests/Data/PostgresMigrationIntegrationTests.SumarioPagina.cs`
- `tests/DMO.Alpha.Infrastructure.Tests/SupabaseAuthenticationPrivacyTests.cs`
- `tests/DMO.Alpha.Web.Tests/EnvioUnicoPostgresPageTests.cs`
- `tests/DMO.Alpha.Web.Tests/JobOnPageTests.EditarPerformance.cs`
- `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioPostgresTests.cs`
- `tests/DMO.Alpha.Web.Tests/PlaneamentoSumarioQueryTests.cs`
- `tests/DMO.Alpha.Web.Tests/ReadinessPageTests.cs`
- `tests/ShellProof/jobon-rascunho.test.cjs`
- `tests/visual/excel_bq_browser.py`
- `tests/visual/painel_lateral_browser.py`
- `CODEX-changes.md` (este registo).

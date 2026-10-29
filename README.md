# DMO — aplicação

**Começar aqui, qualquer IA:** [mapa e instruções](AGENTS.md).

Código da aplicação DMO. Para a organização do trabalho, seguir AGENTS.md e o guia comum. A versão do checkout está em version.props; publicação e validação têm de ser confirmadas separadamente.

O trabalho de design tem contexto próprio em [DESIGN.md](DESIGN.md) e [guia de continuação](docs/design/CONTINUAR.md). Não assumir que esse trabalho é o objetivo de uma nova tarefa.

## Aplicação

# DMO

Aplicação ASP.NET Core (.NET 10) com PostgreSQL, em `Dockerfile` e `entrypoint.sh`. Módulos: Job On, Controlo (Peso, Pegamentos, PU NNPB, Resumo e Revisão), Boquilhas, Ferramentas, Armazém, Reparação Interna e Administração.

A versão atual está em `version.props` e as correções incluídas aparecem na página `/versao` da app (fonte: `src/DMO.Alpha.Web/ReleaseNotes.json`). O código, os testes e as ferramentas de desenvolvimento estão todos neste repositório.

## Onde corre

- **Servidor do portátil** (a app em uso): Docker com PostgreSQL ao lado e o login da própria app. Atualiza-se com `scripts/portatil/atualizar.sh main`; ver [scripts/portatil/LEIA-ME.md](scripts/portatil/LEIA-ME.md).
- **Ensaio** no Railway: publica a `main`, com login Supabase.
- **Instalação nova** num computador com Docker Compose: [instalar/LEIA-ME.md](instalar/LEIA-ME.md).

## Estrutura

- `src/DMO.Alpha.Core`: domínio e contratos.
- `src/DMO.Alpha.Infrastructure`: EF Core, PostgreSQL, Supabase e geração de PDFs. As migrações estão em `Migrations/`.
- `src/DMO.Alpha.Web`: Razor Pages, serviços web e ficheiros estáticos.
- `tests/`: testes xUnit, testes jsdom (`tests/ShellProof`) e testes no browser (`tests/visual`).
- `scripts/ci/`: o CI local (`ci-local.sh`) e a guarda dos testes (`guarda-testes.py`, regra em `docs/architecture/REGRAS-DOS-TESTES.md`).
- `docs/ui-reference/`: paleta oficial e componentes partilhados.
- `docs/architecture/`: decisões aprovadas e ainda por implementar.
- Auditorias, decisões e notas antigas: repositório dmo-app/dmo-Improvements.

## Configuração

- A ligação PostgreSQL e o login são definidos por variáveis do ambiente de execução. Os nomes estão em `.env.example`; os valores nunca vão para o Git.
- `Dmo__AuthenticationProvider` vazio ou `Local` usa o login da própria app (palavras-passe cifradas na base); `Supabase` usa o Supabase. Sem `ConnectionStrings__DmoDatabase`, o login e a página de erro continuam acessíveis, mas as operações PostgreSQL falham explicitamente.
- A autenticação InMemory e a base volátil existem apenas em `Development`, para trabalho local.
- O ADMIN é o email guardado nas Definições.
- Os PDFs são gerados em memória e entregues ao browser, que os descarrega ou grava na pasta partilhada escolhida em cada computador; nunca são guardados no servidor.
- O separador decimal é sempre `.` nos campos, resultados e PDFs, sem separadores de milhares.

## Base de dados

- O schema começa na migração `Baseline` e segue nas migrações seguintes em `Migrations/`. Uma base nova é criada com `dotnet ef database update -p src/DMO.Alpha.Infrastructure -s src/DMO.Alpha.Web`.
- A app não aplica migrações ao arrancar. O deploy aplica-as antes de a versão nova arrancar (`migrate.sh`: pré-deploy do Railway e passo do `atualizar.sh` no portátil), com o pacote de migração gerado no build da imagem.
- Uma base criada antes desta baseline tem o histórico de migrações atualizado uma única vez para a `Baseline`, sem mudar tabelas nem dados.
- Como pôr uma base ao nível do código, incluindo o SQL desde a 0.2.0-beta.2 e o acesso do utilizador da app: [docs/base-de-dados/README.md](docs/base-de-dados/README.md).

## Versão e build

O número da versão vem de `version.props`. O commit, o estado da origem, o ID do build e a data UTC são gravados na assembly durante o build (`build/VersionMetadata.targets`). Sem Git disponível, passar `-p:DmoRevision=<sha>` ao publish. No Docker, passar `--build-arg DMO_SOURCE_REVISION=<sha>` (ou `RAILWAY_GIT_COMMIT_SHA`).

```sh
dotnet build DMO.Alpha.slnx
bash scripts/ci/ci-local.sh   # o CI completo; precisa de um PostgreSQL local
```

Nas sessões do Claude na nuvem, `.claude/hooks/session-start.sh` instala o .NET 10 e arranca um PostgreSQL local descartável (base `dmo_test`, só dentro da sessão), por isso todos os testes correm. No GitHub, `.github/workflows/ci.yml` corre o mesmo `ci-local.sh` com um PostgreSQL temporário.

Depois de publicar, validar autenticação, Job On, Controlo, Boquilhas, Ferramentas, Administração e acesso aos dados. Um deploy com estado SUCCESS não prova que os ecrãs funcionam.

Os requisitos funcionais estão no repositório `dmo-app/blueprint`.


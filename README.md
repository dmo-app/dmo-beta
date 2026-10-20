# DMO

Aplicação ASP.NET Core (.NET 10) com PostgreSQL e autenticação Supabase, publicada no Railway com `Dockerfile` e `entrypoint.sh`. Módulos: Job On, Controlo (Peso, Pegamentos, PU NNPB, Resumo e Revisão), Boquilhas, Ferramentas e Administração.

Esta é a baseline limpa DMO-BETA-0.2, feita a partir da 0.1.0-beta.146 e atualizada por envios únicos da `main` da DMO-BETA-DEV (o último: 0.2.0-beta.3, da beta.316, commit `420d5d5`). Tem só o que a app precisa para compilar e correr; os testes e as ferramentas de desenvolvimento ficam no repositório de desenvolvimento. A versão atual está em `version.props` e as correções incluídas aparecem na página `/versao` da app (fonte: `src/DMO.Alpha.Web/ReleaseNotes.json`).

## Estrutura

- `src/DMO.Alpha.Core`: domínio e contratos.
- `src/DMO.Alpha.Infrastructure`: EF Core, PostgreSQL, Supabase e geração de PDFs. As migrações estão em `Migrations/`.
- `src/DMO.Alpha.Web`: Razor Pages, serviços web e ficheiros estáticos.
- `docs/ui-reference/`: paleta oficial e componentes partilhados.
- `docs/architecture/`: decisões aprovadas e ainda por implementar.
- `docs/PENDENCIAS.md`: funcionalidades em falta.

## Configuração

- Autenticação Supabase e ligação PostgreSQL são definidas por variáveis do ambiente de execução. Os nomes estão em `.env.example`; os valores nunca vão para o Git.
- Fora de `Development`, autenticação e provisionamento usam sempre Supabase. Sem `ConnectionStrings__DmoDatabase`, o login e a página de erro continuam acessíveis, mas as operações PostgreSQL falham explicitamente.
- A autenticação InMemory e a base volátil existem apenas em `Development`, para trabalho local.
- O ADMIN é o email guardado nas Definições.
- Os PDFs são gerados em memória e entregues ao browser, que os descarrega ou grava na pasta partilhada escolhida em cada computador; nunca são guardados no servidor.
- O separador decimal é sempre `.` nos campos, resultados e PDFs, sem separadores de milhares.

## Base de dados

- O schema completo está numa única migração, `Baseline`. Uma base nova é criada com `dotnet ef database update -p src/DMO.Alpha.Infrastructure -s src/DMO.Alpha.Web`.
- A app não aplica migrações ao arrancar. O deploy aplica-as antes de a versão nova arrancar (`migrate.sh`, comando de pré-deploy do Railway), com o pacote de migração gerado no build da imagem.
- Uma base criada antes desta baseline tem o histórico de migrações atualizado uma única vez para a `Baseline`, sem mudar tabelas nem dados.
- Alterações futuras ao modelo entram como migrações novas a seguir à `Baseline`.
- Como pôr uma base ao nível do código, incluindo o SQL desde a 0.2.0-beta.2 e o acesso do utilizador da app: [docs/base-de-dados/README.md](docs/base-de-dados/README.md).

## Versão e build

O número da versão vem de `version.props`. O commit, o estado da origem, o ID do build e a data UTC são gravados na assembly durante o build (`build/VersionMetadata.targets`). Sem Git disponível, passar `-p:DmoRevision=<sha>` ao publish. No Docker, passar `--build-arg DMO_SOURCE_REVISION=<sha>` (ou `RAILWAY_GIT_COMMIT_SHA`).

```sh
dotnet build DMO.Alpha.slnx
```

Depois de publicar, validar autenticação, Job On, Controlo, Boquilhas, Ferramentas, Administração e acesso aos dados. Um deploy com estado SUCCESS não prova que os ecrãs funcionam.

Os requisitos funcionais estão no repositório `dmo-app/blueprint`.

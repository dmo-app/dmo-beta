# Base de dados da DMO-BETA-0.2

A 0.2 não tem base própria nem serviço no Railway (verificado a 20/10/2026). Este ficheiro diz como pôr uma base ao nível do código.

## Base nova

Desde a 0.2.0-beta.4, o deploy aplica as migrações sozinho: o comando de pré-deploy do Railway é `/app/migrate.sh`, que corre o pacote de migração (`/app/efbundle`, gerado no build da imagem) com `DMO_MIGRATOR_CONNECTION` (ou, sem ela, `ConnectionStrings__DmoDatabase`). A app continua a não aplicar migrações ao arrancar, e o processo web não recebe `DMO_MIGRATOR_CONNECTION`.

À mão:

`dotnet ef database update -p src/DMO.Alpha.Infrastructure -s src/DMO.Alpha.Web` cria todas as tabelas, da `Baseline` até à última migração da pasta `src/DMO.Alpha.Infrastructure/Migrations` (a 28/10/2026, `20261027125325_SapFabricoSoCadencia`). É este o caminho para atualizar uma base de qualquer versão: o `efbundle` só aplica as migrações que faltam.

## Base que já estava na 0.2.0-beta.2 (histórico)

Só para uma base que tenha parado na 0.2.0-beta.2. Correr [`atualizar-de-0.2.0-beta.2.sql`](atualizar-de-0.2.0-beta.2.sql). É o SQL das migrações 3 a 20, igual ao que foi aplicado no DEV da DMO-BETA-DEV antes de cada merge. Cada bloco só acrescenta (as atualizações que tem preenchem colunas novas) e pode correr-se mais de uma vez. No fim, `__EFMigrationsHistory` tem 20 linhas. Este SQL não leva a base até à versão atual: depois dele, aplicar as migrações que faltam como acima (`efbundle` ou `dotnet ef database update`).

A migração `SapFabricoSoCadencia` apaga onze colunas SAP de propósito. Voltar a uma imagem anterior não repõe esses dados: antes de atualizar, fazer sempre o `pg_dump`.

## Acesso do utilizador da app

A app liga com um utilizador próprio, que não é dono das tabelas. Cada tabela nova precisa de `GRANT` para esse utilizador. O SQL dá-o a `dmo_beta_dev_runtime` quando esse papel existe. Numa base com outro utilizador, dar-lhe o mesmo:

```sql
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO <utilizador_da_app>;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO <utilizador_da_app>;
```

A app não aplica migrações ao arrancar: o SQL tem de estar aplicado antes de publicar o código.

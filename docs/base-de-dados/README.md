# Base de dados da DMO-BETA-0.2

A 0.2 não tem base própria nem serviço no Railway (verificado a 20/10/2026). Este ficheiro diz como pôr uma base ao nível do código.

## Base nova

Desde a 0.2.0-beta.4, o deploy aplica as migrações sozinho: o comando de pré-deploy do Railway é `/app/migrate.sh`, que corre o pacote de migração (`/app/efbundle`, gerado no build da imagem) com `DMO_MIGRATOR_CONNECTION` (ou, sem ela, `ConnectionStrings__DmoDatabase`). A app continua a não aplicar migrações ao arrancar, e o processo web não recebe `DMO_MIGRATOR_CONNECTION`.

À mão:

`dotnet ef database update -p src/DMO.Alpha.Infrastructure -s src/DMO.Alpha.Web` aplica a `Baseline` e todas as migrações posteriores incluídas na versão do código em utilização. A lista está em `src/DMO.Alpha.Infrastructure/Migrations/`; a última migração varia com a versão.

## Base que já estava na 0.2.0-beta.2

Correr [`atualizar-de-0.2.0-beta.2.sql`](atualizar-de-0.2.0-beta.2.sql). É o SQL das migrações 3 a 20, igual ao que foi aplicado no DEV da DMO-BETA-DEV antes de cada merge. Cada bloco só acrescenta (as atualizações que tem preenchem colunas novas) e pode correr-se mais de uma vez. No fim deste SQL, `__EFMigrationsHistory` tem 20 linhas. Esse é um ponto intermédio histórico: para chegar à versão atual, aplicar depois as migrações restantes com o pacote de migração dessa versão ou com o comando `dotnet ef database update` indicado acima.

## Acesso do utilizador da app

A app liga com um utilizador próprio, que não é dono das tabelas. Cada tabela nova precisa de `GRANT` para esse utilizador. O SQL dá-o a `dmo_beta_dev_runtime` quando esse papel existe. Numa base com outro utilizador, dar-lhe o mesmo:

```sql
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO <utilizador_da_app>;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO <utilizador_da_app>;
```

A app não aplica migrações ao arrancar: o SQL tem de estar aplicado antes de publicar o código.

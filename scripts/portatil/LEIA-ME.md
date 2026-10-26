# Servidor do portátil

A app corre no portátil «fedra» (WSL2 Ubuntu-24.04, Docker), com o PostgreSQL
num contentor ao lado e o endereço https://fedra.tail42d2c3.ts.net (Tailscale
Funnel → 127.0.0.1:8080). Login: o da própria app (`Dmo__AuthenticationProvider`
vazio ou `Local`).

| Contentor | O quê |
| --- | --- |
| `dmo-pg` | `postgres:17`, rede `dmo-net`, volume `dmo-pgdata`, base `dmo`, sem portas publicadas |
| `dmo-local` | a app, rede `dmo-net`, porta 8080, `--env-file ~/dmo-local.env` |

## Papéis da base (uma vez, e de novo para trocar palavras-passe)

`papeis.sql` cria `dmo_app` (a app: só lê e grava dados) e `dmo_backup` (o backup:
só lê). As migrações continuam com o dono (`postgres`). As palavras-passe vão na
linha de comando e nunca ficam no ficheiro, e têm de ter pelo menos 12 caracteres
(o script pára com erro se faltar uma ou se for curta):

```sh
docker exec -i dmo-pg psql -U postgres -d dmo -v ON_ERROR_STOP=1 \
  -v app_pw="$APP_PW" -v backup_pw="$BACKUP_PW" < ~/dmo-beta/scripts/portatil/papeis.sql
```

Depois:

- `~/dmo-local.env`: `ConnectionStrings__DmoDatabase` passa a `Username=dmo_app`.
- `~/dmo-migrar.env` (novo, `chmod 600`): `DMO_MIGRATOR_CONNECTION` com `Username=postgres`.
- `~/dmo-backup.sh`: `pg_dump` com `dmo_backup`.

## Atualizar a app

```sh
bash ~/dmo-beta/scripts/portatil/atualizar.sh            # ramo integracao
bash ~/dmo-beta/scripts/portatil/atualizar.sh main       # outro ramo
```

Antes de mexer, confirma que a app não liga com um utilizador que manda na base
(tem de ser `dmo_app`). Depois vai buscar o ramo e constrói a imagem
`dmo-local:<commit>` (marcada «modificada» se a pasta tiver ficheiros mudados).
Tira uma cópia da base para `~/dmo-antes-de-atualizar` (as 10 últimas) e confirma
que tem todas as tabelas.

As migrações correm primeiro numa base à parte (`dmo_ensaio_atualizar`, reposta
da cópia). Se alguma tabela perder linhas, pára sem tocar na base da app. Só
depois aplica as migrações a sério com `migrate.sh`, troca o contentor e espera
pelo `/login`. Se a versão nova não arrancar ou não responder, volta a pôr a
anterior a correr.

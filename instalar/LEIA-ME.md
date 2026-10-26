# Instalar o DMO num servidor

Para um computador novo (o portátil, um PC ou um servidor da empresa) com o
Docker e o Docker Compose instalados.

```sh
git clone https://github.com/dmo-app/dmo-beta.git
cd dmo-beta/instalar
./instalar.sh
```

O `instalar.sh`:

1. da primeira vez, cria o `.env` com palavras-passe sorteadas (só nesta
   máquina; guarde uma cópia em sítio seguro) e uma **base nova e vazia** nesta
   máquina;
2. constrói a app, aplica as migrações com o dono da base e cria os
   utilizadores da base (`dmo_app` para a app, `dmo_backup` para as cópias);
3. arranca a app na porta 8080 e, numa base sem ADMIN, mostra o **código da
   primeira instalação**.

Depois, no browser, abre-se o endereço da app: a página «Primeira instalação»
pede esse código, o email do ADMIN e a palavra-passe. Os utilizadores criam-se
a seguir em Admin › Utilizadores. A base nunca se escolhe no browser.

Para **atualizar**, vai-se buscar o código novo (`git pull`) e corre-se
`./instalar.sh` outra vez: as migrações aplicam-se antes de a app nova arrancar.

## Usar uma base que já existe

No `.env`, apaga-se a linha `COMPOSE_PROFILES=base-local` e preenche-se onde
está a base (o dono tem de poder criar tabelas e papéis):

```
DMO_BASE_HOST=servidor-de-bases
DMO_BASE_PORTA=5432
DMO_BASE_NOME=dmo
DMO_BASE_DONO=dono_da_base
DMO_BASE_DONO_PW=...
```

## O que fica onde

| O quê | Onde |
| --- | --- |
| Dados | volume Docker `dmo_dmo-dados` (base desta máquina) |
| Palavras-passe | `instalar/.env` (fora do git) |
| Chave das sessões | na própria base (`DataProtectionKeys`) |

O portátil «fedra» foi montado à mão antes deste instalador
(`scripts/portatil/`); usa os mesmos `papeis.sql` e `migrate.sh`.

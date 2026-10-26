# Handoff: importar a amostra da Reparação Interna no ENSAIO

Pedido do Diogo (26/10/2026): amostra de 01/07 a 26/10/2026 do Excel «Lista Verificação Serralheiros_DMO-MG_2026.xlsx». Só no ensaio; a base oficial não se toca.

## Onde
- Ensaio: contentor `dmo-ensaio` (127.0.0.1:8081), base `dmo_ensaio` no postgres `dmo-pg` (rede dmo-net); https://app.tail42d2c3.ts.net:8443.
- Antes de correr: a thread Importar Access tem de ter acabado de copiar a oficial para o ensaio; a app do ensaio tem de ter a migração `20261026154757_ReparacaoInternaTrabalho` (PR #160, .101, no integracao). Sem ela o INSERT falha na coluna `Trabalho`.
- Fazer backup do ensaio antes (pg_dump -Fc).

## Ficheiros (esta pasta)
- `importar-amostra.sql`: uma transação. Cria as contas que faltem, liga e insere. Recusa correr duas vezes («A amostra já foi importada»).
- `amostra.csv` (11 955 linhas que entram), `fora.csv` (500 células que ficam de fora e porquê), `resumo.md`.
- `../scripts/serralheiros_amostra.py`: gera tudo a partir do xlsx (`python3 -I ... <xlsx> <pasta> [--sem-jobon]`).

## O que o SQL faz
1. Contas: para cada serralheiro (Edgar Santos, Marco Ambrosio, Orlando Norte, Renato Carrilho, Ricardo Pereira, Rodrigo Matias) sem utilizador com esse nome (sem olhar a maiúsculas), cria um com número de operador aleatório de 4 dígitos livre, sem palavra-passe (não entra até o ADMIN a definir na ficha). Não escrever esses números em chat, ficheiros ou commits.
2. Associar: cada reparação liga-se à conta pelo nome; à produção da linha (Machines.Code) cujo InicioEm/FimEm cobre o dia, a de maior Id que tenha CM (ou MF) e usa esse contexto. MP = CM.
3. Sem produção nesse dia: não entra (o NOTICE mostra quantas). Só com `--sem-jobon` entram como «Sem Job On» (ReferenciaEscrita IMPORTADO, lote s/lote). Recomendado: não usar.
4. Soldar / Torno-Polir: 147 valores sem linha no Excel, ficam de fora (à espera de o Diogo indicar uma linha). Trabalho = Reparação em todas.
5. Marca para desfazer: `RegistadoEmUtc` = dia 12:00:00.000123. Desfazer: `delete from "ReparacoesInternas" where extract(microseconds from "RegistadoEmUtc")::int % 1000000 = 123;` (as contas criadas ficam; apagá-las pelo Admin se for preciso).

## Como correr
`psql -v ON_ERROR_STOP=1 -d dmo_ensaio -f importar-amostra.sql` dentro do contentor dmo-pg. Guardar os NOTICE («Com Job On: X, sem Job On: Y»).

## Depois
Confirmar na app do ensaio: Reparação Interna › Consultar com filtro de dias 01/07–26/10: o «Resumo por pessoa» mostra os 6 nomes, e a lista tem o «Quem» preenchido. Dizer à thread da amostra os números.
Testado só numa base local (Postgres 16) com produções de teste.

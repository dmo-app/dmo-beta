# Regras dos testes

Decisão do proprietário em 19/10/2026, a partir do estudo dos testes do CI (`/mnt/project-files/analises/estudo-testes-2026-10-19.md`). Completa o ponto 5 de [REGRAS-DE-CODIGO.md](REGRAS-DE-CODIGO.md). Vale para os testes novos e para os que já existem quando se mexe neles.

## Cada teste

1. **Nasce da regra do negócio, não do código.** A regra vem do blueprint ou de uma decisão do proprietário, e o nome do teste é essa regra em português («Contra costura 0 é recusada e nada fica gravado»).
2. **Prova-se que falha.** Numa correção, o teste falha no código antigo. Numa regra que já existe, estraga-se a regra de propósito uma vez (por exemplo `<=` para `<`) e o teste tem de falhar numa verificação, com o código estragado a compilar. O PR diz a mutação e copia a mensagem.
3. **Limites com três casos:** no limite, logo abaixo e logo acima.
4. **Um campo inválido de cada vez.** Testes com vários campos inválidos juntos escondem o erro num deles.
5. **Verifica o efeito, lido da base de novo** (sem tracking), e que uma recusa não grava nada.
6. **Os casos combinados que já deram erro têm teste próprio,** por exemplo trocar a BQ duas ou três vezes, folhas antigas, NOK e «não avaliável» juntos.

## Base de dados

7. **Tudo o que depende da base testa-se no PostgreSQL:** nada repetido (índices únicos), duas pessoas ao mesmo tempo (tokens de versão e trincos) e gravar tudo ou nada (transações). Em memória a app salta a transação e os trincos, e a versão é sempre 0, por isso esses testes não provam estas garantias.
8. **Uma proteção nova da base** (um índice, um token, um trinco ou uma transação) entra com o teste PostgreSQL que falha quando ela é tirada.

## Páginas

9. **O teste envia o formulário que a página desenha** (`EnvioDoFormulario`), não um formulário montado à mão. Depois verifica o que ficou gravado, para onde a página levou e o texto que o operador lê, dentro da zona certa (pelo id ou por um `data-*`).
10. **Não se verificam classes, estilos, ordem de atributos nem espaços.** As regras de aspeto ficam nas guardas do sistema de design.
11. **Cada «não aparece» vem depois de um «aparece» na mesma zona,** e os extratores falham quando não encontram a zona. As ordens verificam-se com `PosicaoNoHtml.Posicao`, que falha se o texto não existe.
12. **Os PDFs leem-se pelo texto,** por exemplo «Peso 1 de 2» e o nº da produção.
13. **O Chromium só corre os fluxos principais** (movimento de BQ, leitura de Pegamentos, Peso e Aprovar). O resto testa-se dentro do processo.

## CI e verificação periódica

14. **Só se junta com «CI local: OK».** Ver AGENTS.md e `scripts/ci/testes.md`.
15. **Mudar um teste existente precisa do ok do proprietário.** A exceção é só acertar uma contagem ou uma lista, dito no PR.
16. **No fim de cada sprint, uma verificação dos testes:** estragam-se de propósito 20 a 30 regras importantes, uma de cada vez, e vê-se se o CI apanha cada uma. Cada uma que passe verde ganha um teste. Não é um estudo novo; é a revisão de fim de sprint aplicada aos testes.
17. **Cada mudança aos testes fica explicada no registo.** A guarda dos testes (`scripts/ci/guarda-testes.py`, primeiro passo do CI local) guarda uma impressão digital de cada teste e faz falhar o CI quando um teste muda, sai ou entra sem explicação. Depois de mexer nos testes, corre-se:
    - só testes novos: `python3 scripts/ci/guarda-testes.py registar --porque "a regra que os testes novos provam"`;
    - acertar só uma contagem ou uma lista: `registar --tipo contagem-lista --porque "a contagem X, 5→6"`;
    - qualquer outra mudança ou um teste tirado (ponto 15): `registar --tipo ok-do-diogo --ref "<onde o proprietário deu o ok>" --porque "..."`.

    `mostrar` lista o que mudou sem gravar nada. O registo (`scripts/ci/testes-alteracoes.md`) só cresce no fim e entra no PR com a impressão nova (`scripts/ci/testes-impressao.json`); nunca se mexe nos dois à mão. Num conflito nestes dois ficheiros ao fazer rebase, fica-se com a versão da `main` e corre-se `registar` outra vez. A thread «Guardião dos testes» lê cada entrada nova e avisa o coordenador quando a explicação não justifica a mudança (por exemplo um teste enfraquecido para passar).

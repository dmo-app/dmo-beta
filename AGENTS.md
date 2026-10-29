# AGENTS.md — regras para os agentes de fora (Codex e outros)

Este AGENTS.md aplica-se a qualquer IA, independentemente da ferramenta. O [AGENTS universal da DMO](https://github.com/dmo-app/dmo-Improvements/blob/main/AGENTS.md) contém o processo comum; abaixo ficam as instruções deste repositório.

## Onde continuar

- [Regras de implementação](docs/architecture/REGRAS-DE-CODIGO.md).
- [Garantias e alterações dos testes](docs/architecture/REGRAS-DOS-TESTES.md).
- [Entrega do PR](.github/pull_request_template.md).
- [Versão do código local](version.props).

## O que entregar

Trabalhar no âmbito do pedido e entregar o PR para integracao segundo AGENTS.md. A lista, o histórico e os relatórios de testes consultam-se no dmo-ci; os testes executáveis permanecem aqui até uma migração técnica validada. Não atribuir versões nem publicar.




Vale para qualquer agente que trabalhe neste repositório. Junta-se a
[docs/architecture/REGRAS-DE-CODIGO.md](docs/architecture/REGRAS-DE-CODIGO.md) e
[docs/architecture/REGRAS-DOS-TESTES.md](docs/architecture/REGRAS-DOS-TESTES.md); não as substitui.
Português de Portugal em tudo o que se escreve (PR, relatórios, textos da app).

## Quem faz o quê

1. **A IA de implementação** implementa a mudança (refinamento, correção, funcionalidade) e **escreve os testes**
   dela. Em cada PR (modelo em `.github/pull_request_template.md`) diz:
   - a **prioridade de entrada** (1 = entra primeiro; ordem em que os PRs devem entrar na `integracao`);
   - a **lista de testes**: cada teste com a funcionalidade que garante, em frase do negócio.
     Esta lista fica também registada na guarda dos testes (`guarda-testes.py registar --porque`).
     Cada teste novo ganha também a sua linha em [docs/testes/REGISTO.md](docs/testes/REGISTO.md)
     (o que cada teste garante; os testes fracos ou errados conhecidos estão em `docs/testes/PROBLEMAS.md`).
   Abre o PR para o ramo `integracao` com a etiqueta **`agentes: testar`**. A implementação entrega à validação; o papel de CI segue o dmo-ci.
2. **O agente de fora** trata de tudo o que é CI e PR: pega nos PRs pela prioridade, resolve os
   conflitos com a `integracao`, corre o CI, corrige o que falhar e escreve o **relatório** de como
   tudo foi feito (modelo abaixo) no PR e em `relatorios/` do repositório
   [dmo-app/dmo-ci](https://github.com/dmo-app/dmo-ci), que tem o passo a passo e o script. Muda a etiqueta para
   **`agentes: pronto`** (CI verde) ou **`agentes: ajuda`** (não conseguiu, diz porquê).
3. **A IA de implementação** lê o relatório. Se a correção estiver mal, sugere no PR (comentário) e volta a pôr
   `agentes: testar`. Não a refaz por cima.
4. **Só a thread «Rever»** junta os PRs na `integracao`, em lotes, e dá o número da versão.
   O proprietário (Diogo) decide quando vai para a `main` e para o servidor.

## Como encontrar o trabalho

PRs abertos com a etiqueta `agentes: testar`, pela **prioridade de entrada** escrita no PR
(empate: o mais antigo primeiro): `is:pr is:open label:"agentes: testar"`. Um PR de cada vez;
trabalhar sempre no ramo do próprio PR.

## Conflitos

- Trazer a `integracao` para o ramo do PR com **merge** (`git merge origin/integracao`). Nunca `push --force`,
  nunca reescrever o histórico.
- Num conflito, manter **as duas** mudanças: a que já está na `integracao` e a do PR. Se não der para
  manter as duas, parar e pôr `agentes: ajuda`.
- `scripts/ci/testes-impressao.json` e `scripts/ci/testes-alteracoes.md`: ficar com a versão da
  `integracao` e correr outra vez `python3 scripts/ci/guarda-testes.py registar` com a mesma explicação que o PR já tinha.
- `version.props` e `src/DMO.Alpha.Web/ReleaseNotes.json`: ficar com a versão da `integracao`
  (os números são da thread «Rever»).

## Testes e CI

O CI é **local**, na máquina do agente, e corre **todo dentro de um contentor Linux** (Docker), com a
base PostgreSQL descartável noutro contentor. O passo a passo está no
[dmo-ci](https://github.com/dmo-app/dmo-ci): `docker compose run --rm ci` sobre o ramo do PR.
Não correr o `ci-local.sh` diretamente no Windows (o vstest falha lá). Só vale **«CI local: OK»**;
«INCOMPLETO», testes abortados ou grupos com 0 testes não valem. O GitHub Actions não substitui o CI local.

## O que se pode corrigir

- Erros de compilação, testes que falham por causa da mudança do PR, conflitos.
- A correção é **a mais pequena possível**, no sítio da causa, sem mudar o que o PR quis fazer
  (o aspeto, os textos, a regra do negócio).
- Se o teste falha porque a regra mudou de propósito (o PR diz qual), acertar só uma contagem ou
  uma lista e registar com `registar --tipo contagem-lista --porque "..."`.

## Nunca

- Enfraquecer, desligar, saltar (`Skip`), apagar ou mudar o sentido de um teste para ficar verde.
  Mudar um teste existente precisa do ok do Diogo (`--tipo ok-do-diogo`): sem esse ok, `agentes: ajuda`.
- Mexer na `main`, juntar PRs, aprovar PRs, mudar `version.props`, os números DMO-xxx / A-xxxx
  ou o `ReleaseNotes.json`.
- Tocar na oficial (o servidor do portátil e a sua base) ou em dados reais. Os testes correm no
  PostgreSQL local descartável; para ver a app a funcionar, **só o ensaio**.
- Deixar lixo no ensaio: no fim apaga-se tudo o que o agente criou (Job On, produções 2099…,
  movimentos, registos) e o relatório diz o que foi criado e apagado.
- Escrever palavras-passe, chaves ou ligações reais no código, nos PR ou nos relatórios.
- Tirar proteções (ponto 12 de REGRAS-DE-CODIGO.md) ou criar migrações novas sem o PR o pedir.
- Mudanças fora do PR (limpezas, refactors, «já agora»). Se vir outro erro, escreve-o no relatório.

## Relatório da correção (comentário no PR)

```md
## Relatório do agente
**Agente:** <nome>  ·  **Data:** <AAAA-MM-DD>
**Resultado:** CI local OK / FALHOU — CI do GitHub OK / FALHOU
**Conflitos:** nenhum | <ficheiros e como ficaram>
**O que falhava:** <teste ou erro, com a mensagem copiada>
**Causa:** <uma ou duas frases>
**Correção:** <ficheiros e o que mudou; commit>
**Prioridade:** <a do PR>
**Testes do PR:** <cada teste da lista: passou / falhou>
**Testes mudados:** nenhum | <quais e o registo da guarda>
**Por resolver / dúvidas:** nenhuma | <o quê>
```

Sem conflitos nem falhas, basta: `## Relatório do agente` + Resultado + «Sem correções».

# AGENTS.md — regras para os agentes de fora (Codex e outros)

Vale para qualquer agente que trabalhe neste repositório. Junta-se a
[docs/architecture/REGRAS-DE-CODIGO.md](docs/architecture/REGRAS-DE-CODIGO.md) e
[docs/architecture/REGRAS-DOS-TESTES.md](docs/architecture/REGRAS-DOS-TESTES.md); não as substitui.
Português de Portugal em tudo o que se escreve (PR, relatórios, textos da app).

## Quem faz o quê

1. **O Claude** faz a mudança (refinamento, correção, funcionalidade) e abre o PR para o ramo
   `integracao`, com a etiqueta **`agentes: testar`**.
2. **O agente de fora** pega nesse PR, resolve os conflitos com a `integracao`, corre os testes,
   corrige o que falhar e deixa um **relatório da correção** no PR (modelo abaixo). Muda a etiqueta para
   **`agentes: pronto`** (CI verde) ou **`agentes: ajuda`** (não conseguiu, diz porquê).
3. **O Claude** lê o relatório. Se a correção estiver mal, sugere no PR (comentário) e volta a pôr
   `agentes: testar`. Não a refaz por cima.
4. **Só a thread «Rever»** junta os PRs na `integracao`, em lotes, e dá o número da versão.
   O proprietário (Diogo) decide quando vai para a `main` e para o servidor.

## Como encontrar o trabalho

PRs abertos com a etiqueta `agentes: testar`, do mais antigo para o mais recente:
`is:pr is:open label:"agentes: testar"`. Um PR de cada vez; trabalhar sempre no ramo do próprio PR.

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

Precisa de .NET 10 (`global.json`), PostgreSQL 16 local, Node.js 22, Python 3.12 com
`playwright==1.56.0` e o Chromium (`python3 -m playwright install --with-deps chromium`).

```sh
bash scripts/ci/ci-local.sh
```

- Só vale **«CI local: OK»** na última linha. «INCOMPLETO» (com `CI_SEM_POSTGRES`, `CI_SEM_NODE` ou
  `CI_SEM_BROWSER`) não vale como OK.
- O PostgreSQL é sempre um local descartável (`CI_PG_ADMIN`, por omissão `localhost:5432`, `postgres/postgres`).
- O GitHub corre o mesmo script em `.github/workflows/ci.yml` em cada push ao PR; o relatório
  diz o resultado dos dois.

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
- Ligar-se à base oficial, ao servidor do portátil, ao ensaio, ao Supabase ou ao Railway.
  Só PostgreSQL local descartável.
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
**Testes mudados:** nenhum | <quais e o registo da guarda>
**Por resolver / dúvidas:** nenhuma | <o quê>
```

Sem conflitos nem falhas, basta: `## Relatório do agente` + Resultado + «Sem correções».

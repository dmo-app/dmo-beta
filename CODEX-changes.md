# Alterações desta ronda

23/10/2026, sobre `0.3.0-beta.35` / PR #81:

- A05: correções de Pegamentos com histórico contínuo em concorrência.
- A04: associação e fecho de listas Programada coordenados na transação.
- A08: envio único no movimento BQ sem Job On e na criação de listas.
- Continuação A07: repetir a preparação do email devolve o mesmo `.eml` e
  regista uma vez, com admissão limitada em memória.
- Continuação A06: destinatários protegidos contra formulários antigos e
  gravações concorrentes no PostgreSQL.

Problema, motivo, ficheiros, regressões que falhavam antes e limites:
[Correções das gravações](docs/architecture/CORRECOES-AUDITORIA-GRAVACOES.md).

Detalhes e provas da continuação:
[Email: A06/A07](docs/architecture/CORRECOES-AUDITORIA-EMAIL.md).

# Decisão 001 — estado atual e histórico de revisões

Estado: **aprovada pelo proprietário em 15/10/2026; implementação pendente**. Escolha explícita da terceira alternativa: consultar diretamente o estado atual e conservar o histórico de alterações. Não confundir esta aprovação de desenho com uma funcionalidade já disponível na versão 0.1.0-beta.1.

## Regra

Uma correção mantém a identidade do registo. A mesma operação grava o estado corrigido e uma revisão histórica imutável. Não se recriam Job On, Controlo, ferramentas ou todas as suas relações. Não é necessário reconstruir uma cadeia de diferenças para abrir a folha atual.

Cada revisão identifica o registo afetado, a sequência da revisão, os factos anteriores e posteriores, o autor e o momento UTC. Uma ação que corrige várias leituras pode agrupá-las numa revisão do Pegamentos. Guardar os valores anteriores/posteriores das leituras afetadas é suficiente; não copiar a produção inteira.

O motivo é oferecido, mas opcional. Guardar também versão da app e ID do build que executou a operação, para ligar uma ocorrência ao código utilizado. A revisão dos dados e a versão da app são identidades diferentes.

## Correção e mudança real de contexto

| Ação | Identidade e histórico |
| --- | --- |
| Corrigir costura/contra costura | Mesmo `pegamentos_id` e mesma leitura; revisão com antes/depois; preservar o nominal histórico utilizado, salvo correção explícita desse facto |
| Completar técnico em falta | Seguir o contexto CM/BQ/MF até à ferramenta de origem e completar o `tool_id`; medições antigas não recebem valores retroativamente |
| Trocar realmente CM durante a produção | Preservar o `cm_id` anterior e criar o novo contexto aplicável; leituras anteriores conservam a ligação anterior |
| Introduzir outro lote BQ na produção | Contexto BQ adicional explícito, com identidade e rastreabilidade próprias |
| Fazer um controlo de Pegamentos adicional | Novo `pegamentos_id` por escolha explícita; não se apresenta como correção do anterior |

Não usar um `snapshot_id` genérico como substituto das relações canónicas e das respetivas chaves estrangeiras. Uma revisão agrupa alterações; não apaga a distinção entre ferramenta, contexto de produção, controlo e medição.

## Limites de integridade

- Estado atual e revisão são gravados atomicamente. Uma falha não pode deixar um sem o outro.
- Uma segunda pessoa não pode gravar sobre uma versão entretanto alterada sem ser informada. Comparar a revisão lida com a atual e permitir rever os valores antes de reaplicar a correção.
- As revisões não são editáveis pela operação de correção. Uma nova correção acrescenta história.
- Exclusões funcionais que afetem registos rastreáveis precisam de preservar o facto e o histórico; não apagar revisões em cascata para esconder um registo.
- Registos existentes recebem apenas uma base conhecida na adoção do mecanismo. Não inventar datas, autores ou valores anteriores que não foram preservados. Identificar a base como estado observado na introdução das revisões.
- Não alterar a fórmula ou precisão decimal por introduzir revisões. Leituras e resultados continuam calculados pelo backend.

## Primeiro percurso a implementar: correção de Pegamentos

1. Acrescentar revisão corrente e histórico ligado ao registo/leitura, preservando os IDs existentes e os dados atuais. Preparar e ensaiar a migração apenas numa base local descartável.
2. Criar a operação de corrigir uma leitura, com valores anteriores/posteriores e autoria. Confirmar que o contexto e o nominal não são trocados por valores atuais da ficha durante a correção.
3. Associar a aprovação ao alvo e à revisão que foram efetivamente aprovados. A decisão anterior permanece histórica; uma revisão corrigida deixa de aparecer como atualmente aprovada. Nunca invalidar outro módulo por consequência implícita.
4. Acrescentar formulário e consulta de histórico, com motivo opcional. Folha e PDF correntes mostram a revisão atual; consulta histórica identifica claramente a revisão escolhida.
5. Verificar em PostgreSQL: correção/reabertura, dois utilizadores, rollback após falha, leitura antiga sem nominal, aprovação anterior preservada e isolamento de outros controlos/componentes.

## Confronto com o código atual

`PegamentosMeasurement` contém os factos atuais e o nominal consumido, mas ainda descreve a leitura como append-only e não tem histórico de correção. `RecordPegamentosMeasurementHandler` acrescenta leituras; não implementa edição. `ControloApprovalHandler` guarda apenas o selo atual no Controlo global: a aprovação por alvo/revisão e a preservação das decisões anteriores ainda têm de ser implementadas antes de declarar a correção com histórico completa.

Os contextos `CmContext`, `BqContext` e `MfContext` são ligações mínimas ao catálogo. A revisão não converte automaticamente essas ligações em snapshots versionados: substituições reais continuam um percurso separado, DMO-P02.

Versão destinada à implementação: próximo candidato após 0.1.0-beta.1, a identificar quando houver código e validação. Esta decisão, por si só, não resolve DMO-P02 ou DMO-P03 e não autoriza deploy nem alterações a bases remotas.

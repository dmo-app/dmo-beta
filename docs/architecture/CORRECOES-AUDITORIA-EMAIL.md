# Email — A06 e A07

Continuação de 23/10/2026, sobre `0.3.0-beta.35`, commit
`8d9bfb90576ccced65c34e6a138dbdc41c9efa31`. Pedido do proprietário:
«Gravações: repetição e concorrência» e «continua».

## A07 — Repetir a preparação do email

O filtro de envio único só lembrava redirects; o sucesso de EnviarPeso é um
`FileContentResult`. O mesmo `X-Dmo-Envio` preparava e registava outro email.
EnviarPeso opta agora por lembrar também o ficheiro em memória. Repetições
sequenciais ou simultâneas recebem os mesmos bytes e nome, com um só registo.
Uma chave diferente continua a representar uma preparação nova.

Os ficheiros partilham um orçamento de admissão de 64 MiB. As preparações
com chave são serializadas: depois de atingir o orçamento, pedidos novos
recebem 503 **antes** de preparar ou registar; as repetições já guardadas
continuam disponíveis. O último ficheiro admitido pode ultrapassar o orçamento
pelo seu tamanho. Não há expulsão antecipada que permita voltar a gravar a
mesma chave. O PDF e o email continuam só em memória, sem escrita em disco.

Mantêm-se os limites do mecanismo existente: janela de 30 minutos, uma só
instância, perda das chaves no reinício e envio nativo sem cabeçalho fora desta
proteção. Não oferece idempotência durável nem garante envio pelo Outlook.

## A06 — Não alterar destinatários de uma versão antiga

Os formulários de editar a lista, editar/retirar uma pessoa e apagar a lista
levam a impressão SHA-256 do Id, nome e destinatários que a aba recebeu. Antes
de interpretar a posição da pessoa, o serviço compara essa versão. Uma aba
antiga ou um formulário sem versão recebe `Conflito`, sem gravação, com pedido
para atualizar a página. Assim, repetir «tirar a pessoa 0» não tira a pessoa
que entretanto passou para essa posição. Não foi necessário criar identidades
novas para os destinatários: a posição só é aceite no conteúdo exato que a aba leu.

O EF usa também Nome e Destinatarios como tokens de concorrência no
UPDATE/DELETE. Se outro pedido gravar entre a consulta e a gravação, o
PostgreSQL recusa a escrita antiga e o serviço devolve o mesmo conflito.
Juntar pessoas a uma lista existente também beneficia desta proteção contra
perda de alterações. A migração `EmailListasConcorrencia` atualiza os
metadados do modelo, sem DDL nem transformação dos dados.

A resposta de edição em linha leva a versão nova. O componente atualiza os
outros formulários da mesma lista que ainda tinham a versão enviada; outras
listas e outras abas mantêm as suas versões. Os pedidos em linha deixam de
duplicar campos ocultos quando a linha contém o formulário de editar e o de retirar.

## Provas e validação

- Antes, a regressão HTTP do mesmo email falhou nos dois casos: `Assert.Single`
  encontrou dois EmailEnvio. Depois verifica também a igualdade dos downloads
  e a possibilidade de preparar outro email com outra chave.
- Antes, a regressão PostgreSQL encontrou `Gravado` onde esperava `Conflito`:
  uma remoção antiga apagou o nome que outra pessoa tinha acabado de alterar.
- Antes, o formulário de outra aba voltou com `envio=ok-lista`, em vez de
  `envio=Conflito`.
- O teste JavaScript falhou com o componente anterior: enviou duas versões
  antigas. Com a correção envia uma versão e atualiza só a lista editada.
- Regressões adicionais verificam remoção repetida, falta da versão e recusa
  por memória antes de gravar, mantendo disponível o primeiro download.

Os testes antigos recebem apenas o novo campo obrigatório, com as verificações
preservadas. A guarda regista a adaptação do protocolo e os casos novos.
O inventário do teste de migrações recebe a migração nova (41→42).
Resultados completos e limitações do ambiente constam do registo desta ronda
no repositório `dmo-Improvements`.

Resultado final: build Release concluído; 1661 testes Infrastructure passaram
(6 testes externos Supabase ignorados), 1099 Web passaram e 242 JavaScript
passaram. A primeira execução da infraestrutura encontrou o inventário de
migrações desatualizado; depois de o atualizar, a suite completa passou.
O CI oficial termina com falha: os dois testes de painel usam `file://`,
bloqueado pelo Chromium gerido. Os mesmos scripts e asserções passaram com
as fixtures por HTTP local; os outros dez grupos de browser passaram no CI.
Não houve merge nem deploy.

Estas mudanças não publicam a aplicação nem validam a integração Supabase.

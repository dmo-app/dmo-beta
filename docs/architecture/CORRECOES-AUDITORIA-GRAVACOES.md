# Correções das gravações — A04, A05 e A08

Base: `0.3.0-beta.35`, commit
`614eaa446c5cdeb1bdb5f4ecfcc868d9a24a878c` (PR #81, A01/A02).
Pedido nesta conversa em 23/10/2026: melhorar a app, com prioridade para
«Gravações: repetição e concorrência».

## A05 — Histórico contínuo nos Pegamentos

A edição corre agora numa transação e adquire o mesmo trinco da folha que o
registo e a eliminação já usam, **antes de ler a medição e capturar o rasto**.
A leitura da medição e da folha é renovada depois de adquirir o trinco,
incluindo quando o contexto EF já as tinha em memória. A validação da âncora
(controlo ou folha sem Job On), os valores impossíveis e o comportamento de
guardar sem alterações mantêm-se.

Antes: duas correções sobre 70.10 podiam gravar 70.10→70.20 e 70.10→70.30.
Depois: a segunda vê o resultado da primeira e grava 70.20→70.30. A numeração
e a data que a Revisão usa são verificadas na regressão.

Ficheiro: `src/DMO.Alpha.Infrastructure/Controlo/CorrectPegamentosMeasurementHandler.cs`.

## A04 — Associação e fecho da Programada na mesma fronteira

A associação a uma lista existente e a atualização pelos movimentos do
Armazém adquirem um trinco comum por lista, dentro da transação. O estado da
lista é lido novamente antes de aceitar a associação. A criação de uma lista
e dos seus itens também fica numa só transação.

Os movimentos de um par CM/MF adquirem **todos** os trincos das listas
envolvidas por ordem de Id, antes de atualizar qualquer lista. Os itens são
consultados novamente depois da espera; só são atualizadas as listas abrangidas
pelos trincos. Assim, as duas últimas devoluções simultâneas fecham a lista,
e uma associação que espera por um fecho recebe «ListaFechada».

Ficheiros: `Programada/ReparacaoProgramadaServico.cs` e
`Armazem/ArmazemMovimentos.cs`, em `src/DMO.Alpha.Infrastructure`.

## A08 — O mesmo envio não cria movimentos nem listas adicionais

Os handlers `OnPostRegistarSemJobOnAsync` (Boquilhas) e `OnPostCriarAsync`
(Reparação Programada) passam a usar `[EnvioUnico]`, como os restantes
registos deste tipo. Os formulários reais estão dentro do fluxo DmoTroca,
que envia `X-Dmo-Envio`; as regressões extraem os campos da página com
`EnvioDoFormulario` e repetem a mesma chave. Uma chave diferente continua a
permitir outra operação intencional com os mesmos valores.

As permissões e a antifalsificação continuam a ser aplicadas antes da repetição.
A proteção reutilizada nesta base é em memória, por pessoa/página/handler,
com janela de 30 minutos. **Não garante repetição entre processos ou após um
reinício.** Esta correção não substitui o trabalho de persistência do PR #73.
Sem JavaScript/cabeçalho, conserva-se o comportamento do envio nativo.

## Prova antes da correção

Os testes novos foram compilados e executados antes de alterar os respetivos
caminhos de produção. Falharam em asserções, não por erro de compilação:

| Regra | Falha observada no código anterior |
|---|---|
| A05, com e sem produção | `Expected: Tuple (70.20, 70.30)`; `Actual: Tuple (70.10, 70.30)` |
| A04, duas últimas devoluções | `Assert.NotNull() Failure`: a data de fecho continuava nula |
| A04, associação durante o fecho | `Expected: ListaFechada`; `Actual: Associada` |
| A08, BQ sem Job On | `Assert.Single() Failure`: 2 movimentos sequenciais, ou 3 com repetição simultânea |
| A08, criação de lista | `Assert.Single() Failure`: 2 listas para a mesma chave |

Os testes PostgreSQL usam contextos e transações diferentes. A coordenação
observa se o segundo pedido está bloqueado em `pg_locks` ou se já chegou à
gravação/leitura concorrente; não depende de uma espera fixa para provocar a
corrida. Os contextos são encerrados antes de limpar os dados.

Testes novos:

- `PostgresMigrationIntegrationTests.PegamentosCorrecoesConcorrentes.cs`:
  histórico contínuo nos dois tipos de âncora.
- `PostgresMigrationIntegrationTests.ProgramadaConcorrente.cs`: devoluções
  simultâneas e associação durante o fecho.
- `BoquilhasSemJobOn.EnvioUnico.cs` e
  `Ferramentas/ReparacaoProgramadaEnvioUnicoTests.cs`: mesma chave sequencial
  ou simultânea, e chaves diferentes para operações intencionais.

Não foram alterados testes existentes. O registo A-0087 da guarda foi gerado
pelo comando oficial: 5 métodos novos, sem remoções ou mudanças nas asserções
existentes.

## Validação e limites

Build Release: passou, sem erros. Testes dirigidos após as correções: **15
Infrastructure e 10 Web passaram**, sem testes ignorados nessa seleção.
A suite .NET completa passou: **1.660 Infrastructure e 1.093 Web**; os seis
testes externos Supabase ficaram ignorados por falta de configuração injetada.

O CI oficial terminou com `FALHOU`: um teste JavaScript com espera fixa de
300 ms falhou sob execução paralela, e dois testes sintéticos do painel foram
bloqueados pela política `file://` do Chromium. O mesmo tipo de falha temporal
já tinha sido observado antes destas alterações; os ficheiros JavaScript e os
testes ShellProof não foram alterados. A repetição de toda a seleção JavaScript
com `node --test --test-concurrency=1` executou e aprovou **241 casos**.
Não se apresenta esta repetição como um CI oficial verde.

Os outros dez grupos de browser passaram no CI. Os dois grupos de painel são
também verificados com as mesmas fixtures e asserções por HTTP local, através
do adaptador do ambiente, sem mudar a política do Chromium. As provas finais
estão no registo beta.35 de `dmo-app/dmo-Improvements`.

Supabase: o teste de conectividade HTTPS respondeu 401 por ausência de
`apikey`. Os requisitos das duas chaves existem no rascunho, mas não têm
bindings de valores, e as variáveis não estão no runtime. O proxy TCP indicado
pela máquina para o pooler não está disponível. Autenticação e PostgreSQL
remotos continuam por validar, sem copiar os segredos do chat para ficheiros.

As provas brutas desta sessão estão em
`/workspace/.cloud-setup/evidence/correcoes/`: `a05-antes.log`,
`a04-antes.log`, `a08-antes.log`, `a08-programada-antes.log`,
`concorrencia-depois.log`, `a08-depois.log` e os TRX correspondentes.

Não há migração de schema nem atualização automática de dados existentes.
As garantias novas de concorrência exigem que todos os processos que escrevem
estes dados usem a mesma versão e convenção de trincos. Não foi feita publicação
nem verificação da app alojada. A06/A07 e os restantes pontos da auditoria não
fazem parte destas três correções.

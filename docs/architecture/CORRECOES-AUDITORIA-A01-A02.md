# Correções da auditoria: A01 e A02

Base consultada: `dmo-app/dmo-beta-full-merge`, commit `ea9aee59af9f5989b57378410d9cedf54ac956c9`.
Requisito de separação dos percursos: `dmo-app/blueprint-full`, commit
`af28887f7bbd5ac17876c31605a7d83810948751`, `modules/reparacao-interna/OVERVIEW.md`, §1–2.

## A01 — Decisão no PDF do Peso

A projeção canónica de aprovação aceita agora o Peso selecionado. Uma decisão
positiva anterior à criação desse Peso não o aprova. A geração do PDF e a
preparação do anexo do email usam esta consulta. A consulta corrente do controlo
continua a considerar todos os seus Pesos.

A folha antiga continua a mostrar a decisão guardada quando esta se aplica à
sua data; uma folha posterior não a invalida. As leituras novas não retiram a
aprovação. Uma decisão negativa mantém-se, como antes. A consulta recusa uma
combinação de Peso e controlo que não corresponda.

Continua a existir apenas a última decisão por parte. Esta alteração não cria
um histórico imutável de decisões nem recupera aprovações antigas substituídas.
O tratamento dos registos legados sem decisões por parte mantém-se.

## A02 — Acesso à Reparação Interna

| Chave no Template | Percurso |
|---|---|
| `ri` (chave existente) | Registar, corrigir/anular e consultar o próprio histórico |
| `ri.view` (nova chave) | Consulta dos responsáveis e associação de ferramentas |
| As duas | Ambos os percursos |

O controlo faz-se na entrada de cada página, abrangendo GET e todos os POST.
O menu, a página inicial, as opções de Templates e as sugestões de referência
acompanham as duas chaves. As permissões continuam a ser relidas por pedido.

### Transição dos acessos existentes

Não há alteração automática de dados nem migração de schema. A chave histórica
`ri` mantém o acesso ao registo e passa a deixar de abrir a consulta dos
responsáveis. O ADMIN deve atribuir explicitamente `ri.view` apenas aos Templates
que devem consultar/associar. Se também devem registar, mantém ambas as chaves.
Nenhum Template recebe acesso de responsável automaticamente.

## Verificação e limites

Foram acrescentados testes para a decisão aplicável ao PDF e ao anexo, as folhas
históricas, o limite temporal, a reaprovação, as leituras posteriores, a rejeição,
a âncora Peso/controlo, os acessos separados, as sugestões e a revogação com
formulários abertos. As listas de permissões de testes existentes de consulta
foram atualizadas; as respetivas verificações mantêm-se.

O ambiente de preparação não tem o SDK .NET. Os testes de regressão, a prova de
falha antes da correção, o build e o CI local estão pendentes. A validação dos
testes não pode ser inferida da revisão do código. Confirmar também a execução
da projeção de aprovação em PostgreSQL num ambiente de testes isolado.

O registo dos testes foi gerado pelas funções de `guarda-testes.py` sobre o
snapshot parcial: as impressões originais dos ficheiros presentes foram
conferidas com a base; as unidades dos ficheiros ausentes e não alterados foram
preservadas. A conferência do registo e `git diff --check` passaram nesse âmbito.
Isto não equivale a executar a guarda num checkout completo nem o CI local.

Com o SDK .NET 10 e as dependências disponíveis, executar na raiz:

```powershell
dotnet test tests/DMO.Alpha.Infrastructure.Tests/DMO.Alpha.Infrastructure.Tests.csproj --filter FullyQualifiedName~PesoAprovacaoDocumentoTests
dotnet test tests/DMO.Alpha.Web.Tests/DMO.Alpha.Web.Tests.csproj --filter "FullyQualifiedName~ReparacaoInternaConsultaPaginaTests|FullyQualifiedName~ReparacaoInternaOutraFerramentaPaginaTests"
```

Executar depois o CI local e a prova de regressão segundo as regras do projeto.
Para a prova de A01, manter a API nova e repor temporariamente no builder a
chamada anterior a `ParteAsync`: o código deve compilar e os testes do PDF/anexo
devem falhar nas verificações da aprovação. Para A02, repor temporariamente a
chave `ri` na entrada de `ConsultarModel`: os testes de acesso e revogação devem
falhar. Registar as mensagens reais; estas mutações ainda não foram executadas.
Esta proposta deve continuar em draft até essas verificações terminarem.

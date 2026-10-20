# Componentes partilhados

Os elementos que se repetem entre páginas vivem num sítio só. Uma página nova copia o modelo daqui e não acrescenta CSS próprio para estes elementos. Os testes `SharedComponentsTests` e `UiStyleRuleTests` falham se uma página fugir ao modelo.

| Componente | Onde vive | Quem usa |
|---|---|---|
| Calendário do mês | `Pages/Shared/_DmoCalendar.cshtml`, montado por `DmoCalendarPresentation.ForMonth` (`Pages/Shared/DmoCalendarPresentation.cs`); estilo em `wwwroot/beta-calendar.css` | Job On, Planeamento → Sumário, Boquilhas → Histórico, Controlo → Revisão |
| Tabela | classe `dmo-table` dentro de `dmo-table-wrap`; estilo em `wwwroot/css/dmo-normalize.css` | todas as listas |
| Paginação de tabela | `Pages/Shared/_ListPager.cshtml` com `ListPager` | listas paginadas |
| Cartões de informação | classes `dmo-facts` e `dmo-fact`; estilo em `wwwroot/css/dmo-normalize.css` | Boquilhas → Registo, Ferramentas → Ficha, Pegamentos, Admin → ficha do utilizador |
| Dropdown que aplica ao escolher | atributo `data-dmo-autosubmit` no `<select>`; comportamento em `wwwroot/0_ASSET_CANONICAL_INTERACTIONS.js` | filtros, template do utilizador, associar Job On, Definições |
| Escolha do Job On numa linha (deep sweep 2, 18/10/2026, F-9) | a barra `dmo-filter-bar` com o modificador `dmo-filter-bar-conteudo`: cada campo com a largura do que leva (a máquina `dmo-field-maquina`, a produção `dmo-field-producao`), em vez de esticar; a 600 px ou menos os campos crescem e passam à linha seguinte. A regra geral `.dmo-filter-bar` não muda; estilo em `wwwroot/css/dmo-normalize.css` | a referência, a máquina e a produção da escolha do Job On nos Pegamentos e no Peso («Novo Peso a partir de Job On»); o PU NNPB, sem máquina, põe a produção ao lado da referência na barra dele (`pu-id-form`) |
| Botão | classe `dmo-button` (ou `btn`); estilo em `wwwroot/css/dmo-normalize.css` | toda a app |
| Dica de ajuda (triângulo) | `Pages/Shared/_DmoHelp.cshtml`; estilo em `wwwroot/css/dmo-normalize.css` (`.dmo-help`) | Boquilhas, Job On, Pegamentos (as outras páginas passam a seguir) |
| Atualizar só o que muda | atributos `data-dmo-troca`, `data-dmo-regiao`, `data-dmo-fica` e `data-dmo-parcial`; comportamento em `wwwroot/0_ASSET_CANONICAL_INTERACTIONS.js` (`DmoTroca`); guia em `docs/ui-reference/atualizar-so-o-que-muda.md` | Job On (as outras páginas passam a seguir) |
| Link de um aviso para onde se resolve (Diogo, 19/10/2026: «todos os warning, notices ter um link para levar o user à página onde pode resolver, se possível») | `Pages/Shared/_DmoAvisoLink.cshtml` com `DmoAvisoLink` (`Pages/Shared/DmoAvisoLink.cs`); estilo em `wwwroot/css/dmo-normalize.css` (`.dmo-aviso-link`) | Job On, Peso, Pegamentos, PU NNPB, Boquilhas, Ferramentas, Admin |
| Botões de um card no canto superior direito (regra do Diogo, 18/10/2026: «metem os botões em baixo aumenta espaço, sempre canto superior direito dentro dos cards») | classes `dmo-card-topo` (a linha do título) e `dmo-card-topo-acoes` (os botões, à direita, dentro do card); num card que abre e fecha, o `<summary class="dmo-card-topo">` é a linha do título e as ações vêm a seguir ao `<details>`, num card com `dmo-card-dobravel` (nada interativo dentro do `<summary>`); estilo em `wwwroot/0_ASSET_CANONICAL_DESIGN_SYSTEM.css` | Ferramentas → Ficha (as outras páginas passam a seguir) |

## Calendário

No page model, a página diz quantos registos há em cada dia; o resto (os dias do mês, o hoje, o dia escolhido, o título e a navegação de mês) é do componente.

```csharp
public DmoCalendarPresentation? Calendar { get; private set; }

Calendar = DmoCalendarPresentation.ForMonth(
    ano, mes,
    countForDay: dia => registos.Count(r => r.Dia == dia),
    selected: diaEscolhido,                 // DateOnly? ou null
    navigationPage: "/Modulo/Pagina",       // o page name, nunca um URL à mão
    preservedQuery: filtros)                // opcional; sem ano, mes nem dia
    with { LegendLabel = "Com marcações" };  // opcional (record): o texto da legenda
```

- O ponto do dia acende quando `countForDay` é maior que zero. Quando a regra é outra (a Revisão acende só os dias com controlos por aprovar), passar `hasForDay`. O Job On acende só os dias em que uma produção entra ou sai de uma linha (Diogo, 19/10/2026: o calendário com sinal em todos os dias ficava carregado); o sinal é um traço curto e fino por baixo do número.
- O partial envia `ano`, `mes` e `dia` sozinho; `preservedQuery` leva só os outros filtros da página.
- Um clique escolhe o dia. O duplo clique no dia já escolhido tira a seleção: o partial liga-o a `dia=0` e a página trata `dia=0` como «sem dia» (o Job On mostra «Escolha um dia no calendário.»). Não há botão para isto. Uma página pode dar ao duplo clique uma ação sua: ouve o evento cancelável `dmo:calendar-dblclick` e chama `preventDefault()` (o Job On, com uma produção escolhida na lista, pergunta e retira-a das datas).

Na vista:

```cshtml
<partial name="_DmoCalendar" model="Model.Calendar" />
```

## Tabela

```cshtml
<div class="dmo-table-wrap">
  <table class="dmo-table" data-dmo-list="Nome da lista">
    <thead>
      <tr><th>Referência</th><th>Máquina</th></tr>
    </thead>
    <tbody>
      @foreach (var linha in Model.Linhas)
      {
        <tr><td>@linha.Referencia</td><td>@linha.Maquina</td></tr>
      }
    </tbody>
  </table>
</div>
<partial name="_ListPager" model="Model.Pager" />
```

Linhas brancas, cabeçalho e tipografia iguais em todas as listas. Nada de classes próprias por módulo (`bq-…`, `ferr-…`, `resumo-…`) para o aspeto da tabela.

### Linha selecionável

Um clique seleciona a linha (fundo azul, letra branca), o hover é azul-claro, e o duplo clique ou o Enter abrem a ficha. Os botões ficam fora da tabela. O rádio da primeira célula fica escondido; serve para o formulário saber a linha escolhida.

```cshtml
<tr data-dmo-row data-id="@linha.Id" data-dmo-open="/Modulo/Ficha?id=@linha.Id">
  <td><input type="radio" name="linha_id" value="@linha.Id" aria-label="Linha @linha.Id" /></td>
  <td>@linha.Referencia</td>
  <td>@linha.Maquina</td>
</tr>
```

Sem `data-dmo-open`, a linha só seleciona. A primeira coluna do cabeçalho fica vazia (`<th></th>`).

## Cartões de informação

Um rótulo curto em maiúsculas e o valor por baixo, com a barra azul à esquerda. Mostrar só o que interessa naquela página.

```cshtml
<div class="dmo-facts">
  <div class="dmo-fact"><span>Referência</span><strong>@Model.Referencia</strong></div>
  <div class="dmo-fact"><span>Produção</span><strong>@Model.Producao</strong></div>
</div>
```

- Uma nota pequena por baixo do valor vai num `<small>`.
- Cartões curtos uns por cima dos outros na mesma coluna: embrulhar em `<div class="dmo-fact-stack">`.
- Um cartão que abre outra página é um `<a class="dmo-fact dmo-fact-link">`, com o texto da ação num `<em>`; o hover inverte, como os botões.
- A largura mínima das colunas muda por página com `.<pagina> .dmo-facts{grid-template-columns:…}`.

O Peso ainda tem os cartões próprios (`peso-fact`) e passa quando a folha dele for refeita.

## Dica de ajuda

Regra do Diogo (17/10/2026): textos explicativos não ficam escritos no ecrã. Vão para a dica, um triângulo pequeno da paleta, com o vértice para baixo (para não se confundir com o Δ que a app já usa; Diogo, 17/10/2026), junto ao título, ao rótulo do campo ou à legenda, assente na linha de base do texto ao lado e sempre do mesmo tamanho, seja qual for a letra (Diogo, 17/10/2026). Ao passar o rato (ou com o Tab, ou com um toque) o triângulo inverte a cor, de preenchido (`--dmo-palette-mist`) para contorno (`--dmo-palette-ocean`), e mostra o texto.

```cshtml
<h3>Registar Entrada ou Saída<partial name="_DmoHelp" model="@("Produção, boquilha e lote associados ao movimento.")" /></h3>
<label for="maquina">Máquina em uso<partial name="_DmoHelp" model="textoDaAjuda" /></label>
```

- Num cartão sem título, embrulhar em `<div class="dmo-help-corner">…</div>`: o triângulo fica no canto de cima, à direita.
- O texto abre sempre dentro do ecrã (Diogo, 17/10/2026): o script em `0_ASSET_CANONICAL_INTERACTIONS.js` põe-no por cima de tudo e encosta-o ao lado onde há espaço; sem espaço por baixo, abre por cima. O texto é sempre branco, mesmo dentro de blocos que pintam os `<span>`. O teste no browser das Boquilhas (`tests/visual`) verifica-o a 1920, 768 e 390 px.
- Ficam no ecrã os estados e os avisos (vazio, erro, contagens, «Sem Job On») e as notas dos cartões de informação; vai para a dica o que explica como usar.
- Nada de instruções («um clique seleciona…», «escolha a máquina…») escritas por baixo dos campos ou dos títulos. O teste `HelpTexts_LiveInTheTip_NotWrittenOnTheScreen` vigia as páginas já passadas.

## Dropdown que aplica ao escolher

Escolher uma opção aplica logo; não há botão «Aplicar» nem «Guardar» ao lado (Diogo, 17/10/2026).

```cshtml
<select id="filtro-maquina" name="maquina" data-dmo-autosubmit>…</select>
```

- O script submete o formulário com `requestSubmit`, por isso o `required` e os handlers de submit da página continuam a valer.
- Um formulário só com dropdowns não tem botão. Quando também tem campos de texto, fica um botão «Procurar» para o texto; os dropdowns aplicam sozinhos.
- Com `data-dmo-autosubmit="valor"`, a opção vazia («Produção», «Selecionar…») não submete.
- Nada de `onchange` nem handlers próprios por página.

## Botões

`<button class="dmo-button">` ou `<a class="dmo-button" href="…">`: azul com letra branca; o rato por cima inverte (fundo branco, letra e linha azuis, sem segunda borda); ao carregar fica um azul mais claro da paleta (`--dmo-palette-steel`). Todos os botões funcionam assim (Diogo, 19/10/2026). Desativado com `disabled` ou `aria-disabled="true"`, sem `opacity` em linha. Não há variante branca (`ghost`). Os botões de uma ação ficam juntos numa barra `dmo-list-actions` (lado a lado, com 10 px entre eles), afastada dos campos e das tabelas. Um botão logo a seguir a um `.dmo-field` ou a uma `.dmo-table-wrap` ganha 12 px por cima.

Os botões de um cartão ficam no canto superior direito, dentro do cartão, na linha do título (Diogo, 18/10/2026: «metem os botões em baixo aumenta espaço, sempre canto superior direito dentro dos cards»): `<div class="dmo-card-topo">` com o título e `<div class="dmo-card-topo-acoes">` com os botões (`0_ASSET_CANONICAL_DESIGN_SYSTEM.css`). Um botão que sai de dentro do `<form>` continua a ser dele com `form="id"` (Enter num campo também envia). Sem larguras nem alturas fixas. Já aplicado no Admin (Setup, Utilizadores, Templates), na Revisão do Controlo (o selo, «Consultar Peso», «Consultar Pegamentos», «Não aprovar» e «Aprovar» do controlo aberto), nos cartões do Resumo («Criar Peso», «Associar Peso existente», «Registar Pegamentos», «Registar punções», ao lado do estado), no «Selecionar ferramenta» das Ferramentas vindo do Job On, no «Hoje» e «Todos os dias» do histórico das Boquilhas (por cima do calendário), no «+ Novo registo» do PU NNPB e no «Adicionar leitura» e «Remover leitura» da Comparação do Peso, ao lado de «Comparar pesos» e «Guardar comparação» (deep sweep, 18/10/2026).

Nas tabelas não há botões (Owner, 12/10/2026). Quando a linha escolhida serve uma ação, um clique escolhe a linha (`data-dmo-row` com o rádio escondido) e o botão da ação fica no canto do cartão: um link com `data-dmo-for="<id da lista>"` e `data-href-template` (o «Selecionar ferramenta»), ou um botão `form="id"` que envia o rádio escolhido (o «Associar a esta produção» dos «Pesos sem produção» no Peso: o bloco do canto também leva `data-dmo-for`, por isso um clique escolhe e o duplo clique abre o Peso; com um só Peso a linha já vem escolhida, com vários o rádio é `required`).

## Avisos

Um aviso sobre o estado do que se vê (falta um dado, a template dá os dois Controlos, a produção sobrepõe-se) fica na página, com espaço à volta: nunca colado ao campo, ao formulário ou à tabela vizinha (Diogo, 17/10/2026). O estilo partilhado em `dmo-normalize.css` dá a margem também aos avisos antigos de cada página (`.peso-aviso`, `.jobon-aviso`, …).

```cshtml
<div class="dmo-aviso warn" role="status">A ferramenta não tem nominal do gargalo.</div>
```

- `.warn` amarelo (pede atenção), `.erro` vermelho, sem variante: neutro da paleta. Verde não: o verde é só para valores medidos dentro do bom.
- Um aviso que aparece e sai enquanto se escreve ou escolhe não empurra o que já lá estava (deep sweep 18/10/2026): fica com a linha reservada (a nota do nome técnico nas Ferramentas), vai para o fim do formulário (os avisos das ferramentas no Job On, depois do «Mais detalhes») ou não se repete quando o próprio campo já o diz (a massa de água não utilizável no Peso).

### Link para onde se resolve

Regra do Diogo (19/10/2026): todos os avisos e mensagens de aviso levam um link para a página onde o problema se resolve, sempre que essa página existe. O link vai no fim do texto, dentro do aviso ou da mensagem do canto.

```cshtml
<div class="dmo-aviso warn" role="status">A ferramenta não tem nominal do gargalo.<partial name="_DmoAvisoLink" model="@DmoAvisoLink.FichaFerramenta(toolId, "Abrir a ficha da CM", tecnica: true)" /></div>
```

- `DmoAvisoLink` traz os destinos comuns: `FichaFerramenta` (com `tecnica` e `continuar` para voltar), `RegistarFerramenta` (tipo e referência já escritos), `JobOn` (com `editar`), `CriarJobOn`, `Revisao` (a Revisão do Controlo de uma produção, só a quem aprova) e `DefinicoesDoControlo` (a densidade do vidro e a tolerância dos Pegamentos, a quem tem o Controlo Criar). Um destino novo é um `new DmoAvisoLink(texto, href, modulo)`.
- O link só aparece a quem tem o módulo da página de destino (`Modulo`, uma das `DmoCapabilities`); os outros veem o aviso sem link.
- O texto do link diz o que se vai fazer lá: «Editar as datas», «Abrir a ficha da CM», «Abrir 202602 (C3)».
- Num formulário por gravar (a folha do Job On), o link abre noutro separador (`with { NovoSeparador = true }`), para não se perder o que se escreveu.
- Os avisos que o JavaScript desenha recebem os links do servidor já filtrados pelo módulo (o `avisos` do `?handler=ToolFacts` do Job On) e usam a mesma classe `dmo-aviso-link`.
- Um aviso que se resolve no próprio sítio (o campo ao lado, o formulário aberto) não leva link. Um aviso sem página onde se resolva fica sem link e entra na lista abaixo.

#### Avisos sem link

| Módulo | Aviso | Porquê |
|---|---|---|
| Job On | «A produção indicada não existe» | não há nada a corrigir; a procura está na própria página |
| Job On | as datas trocadas nos filtros do Histórico | resolve-se nos filtros, ao lado |
| Job On | «O Job On mudou» nos módulos (`_JobOnChangeNotice`) | já tem o botão «Ver o que mudou» |
| Peso | «Sem Pesos guardados para…» e «Sem produções para…» na Comparação | resolve-se na procura, ao lado |
| Peso | o mesmo Peso nos dois lados, as leituras que ficaram de fora, a ferramenta diferente da do CM inicial | resolve-se na própria comparação |
| Peso | a calote simulada ilegível | resolve-se no campo ao lado |
| Pegamentos | «a leitura foi para outra folha» depois de trocar a CM, a BQ ou o MF | só informa; não há nada a corrigir |
| Controlo | a tolerância dos Pegamentos fora do habitual ou arredondada, nas Definições | resolve-se no próprio campo, onde o aviso aparece |
| Controlo | a densidade do vidro fora do habitual ou arredondada, nas Definições | resolve-se nos próprios campos, onde o aviso aparece |
| Peso | «Não comparável» numa comparação guardada (a punção ou a densidade era outra) | só informa: a comparação guardada fica com os valores que usou; na comparação ao vivo o aviso leva o link para a ficha da CM ou para as Definições |
| Pegamentos | a medição «não avaliável» e a contra costura ignorada | a «não avaliável» já não acontece (sem nominal do gargalo a medição não grava e o cartão tem o link para a ficha); a contra costura corrige-se na própria leitura |
| PU NNPB | «Esta produção é PS» | só informa; o PU NNPB só existe para NNPB |
| Resumo e Revisão | «A produção indicada não existe»; a aprovação sem operador na sessão | não há página onde se resolva (sair e voltar a entrar) |
| Boquilhas | a referência indicada diferente da da produção | o texto manda contactar o ADMIN; não há página |
| Boquilhas | o período do Resumo diário encurtado | resolve-se nas datas, ao lado |
| Ferramentas | os avisos da própria ficha (lote vazio, valores ilegíveis, ferramenta repetida) | resolvem-se na ficha onde aparecem |
| Admin | «a mesma pessoa cria e aprova» na template; as recusas de repor o acesso, arquivar e eliminar | resolvem-se na própria página ou não têm página (o Supabase) |
| Todos | as recusas de um formulário (campo em falta ou ilegível) | resolvem-se no formulário aberto |
| Todos | «Não foi possível procurar. Escreva de novo.» na procura ao escrever | resolve-se no próprio campo |
| Todos | «O servidor deu um erro…» ao gravar | a página já mostra o que ficou gravado |

## Mensagens de resultado

O resultado de uma ação (guardado, criado, eliminado, recusado) não entra no fluxo da página: aparece no canto, por cima, e a página não se mexe (Diogo, 17/10/2026: «os elementos têm de ficar fixos»).

```cshtml
<div data-dmo-toast="ok" role="status">Template eliminada.</div>
<div data-dmo-toast="erro" role="alert">@Model.DeleteRefusalMessage</div>
<div data-dmo-toast="aviso" role="status">Medição registada sem contra costura: …</div>
```

- O servidor escreve a mensagem onde quiser; o CSS tira-a do fluxo logo à partida e `dmo-live.js` junta-as numa pilha por baixo das barras, com «×». As «ok» fecham sozinhas ao fim de 6 s; as «erro» (uma recusa) e os «aviso» (gravou, mas há uma coisa a ver; com a cor de aviso `--dmo-warning`, como `.warn`) ficam até se fecharem. Numa página que grava sem recarregar, a resposta seguinte do mesmo cartão também os tira (ver «Mensagens antigas» em `atualizar-so-o-que-muda.md`), menos os marcados com `data-dmo-toast-fica`, que só saem ao fechar (o PIN temporário de um utilizador criado ou reposto, mostrado uma vez). Só as «erro» contam como recusa. No telemóvel ficam em baixo, à largura do ecrã.
- As mensagens que o JS compõe (`DMOLive.toast(texto, { kind })`: a pré-visualização do Peso sem rede, a pasta dos PDFs) entram na mesma pilha, com os mesmos tipos (`info`/`success` → `ok`, `warn` → `aviso`, `error` → `erro`) e o «×». A mesma mensagem não se empilha: enquanto estiver à vista, pedi-la outra vez não junta outra (deep sweep de 18/10/2026, regra 109 e B-peso-7). No Peso, o aviso «Pré-visualização indisponível» sai sozinho quando a pré-visualização volta a responder. Já não há a segunda pilha no canto de baixo com cores próprias; os scripts não levam cores soltas (`Scripts_DoNotCarryLooseColors`).
- Na ficha de uma ferramenta, os avisos depois de gravar (lote vazio, quantidade ou valor técnico que não se leu, lote já registado ao duplicar) são `aviso` (beta.199): na grelha empurravam os cartões. `tests/visual/ficha_avisos_browser.py` mede que nada se mexe ao aparecerem, ao saírem e ao fecharem.
- Nos Utilizadores do Admin a pilha fica em baixo, à direita: o aviso do PIN é alto e só fecha no «×», e por baixo das barras tapava «Criar utilizador» no canto do cartão (B-admin-2, deep sweep 18/10/2026).
- Também são `aviso` os avisos depois de gravar nas Boquilhas (a hora que caiu na mudança de hora, a referência indicada que não era a da produção) e a calote ignorada no Job On (deep sweep 18/10/2026, regras 32 e 53).
- O teste `ResultMessages_FloatAboveThePage` vigia o estilo e os valores aceites.

## Porque a tabela não é um partial

Cada tabela tem colunas e conteúdo diferentes; um partial genérico só passaria o HTML de um lado para o outro. O que tem de ser igual (cores, hover, seleção, rádio escondido, paginação) já está na classe `dmo-table`, no `data-dmo-row` e no `_ListPager`.

# Componentes partilhados

Os elementos que se repetem entre páginas vivem num sítio só. Uma página nova copia o modelo daqui e não acrescenta CSS próprio para estes elementos. Os testes `SharedComponentsTests` e `UiStyleRuleTests` falham se uma página fugir ao modelo.

| Componente | Onde vive | Quem usa |
|---|---|---|
| Calendário do mês | `Pages/Shared/_DmoCalendar.cshtml`, montado por `DmoCalendarPresentation.ForMonth` (`Pages/Shared/DmoCalendarPresentation.cs`); estilo em `wwwroot/beta-calendar.css` | Job On, Boquilhas → Histórico, Controlo → Revisão |
| Tabela | classe `dmo-table` dentro de `dmo-table-wrap`; estilo em `wwwroot/css/dmo-normalize.css` | todas as listas |
| Paginação de tabela | `Pages/Shared/_ListPager.cshtml` com `ListPager` | listas paginadas |
| Cartões de informação | classes `dmo-facts` e `dmo-fact`; estilo em `wwwroot/css/dmo-normalize.css` | Boquilhas → Registo, Ferramentas → Ficha, Pegamentos, Admin → ficha do utilizador |
| Dropdown que aplica ao escolher | atributo `data-dmo-autosubmit` no `<select>`; comportamento em `wwwroot/0_ASSET_CANONICAL_INTERACTIONS.js` | filtros, template do utilizador, associar Job On, Definições |
| Botão | classe `dmo-button` (ou `btn`); estilo em `wwwroot/css/dmo-normalize.css` | toda a app |
| Dica de ajuda (triângulo) | `Pages/Shared/_DmoHelp.cshtml`; estilo em `wwwroot/css/dmo-normalize.css` (`.dmo-help`) | Boquilhas, Job On, Pegamentos (as outras páginas passam a seguir) |

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

- O ponto do dia acende quando `countForDay` é maior que zero. Quando a regra é outra (a Revisão acende só os dias com controlos por aprovar), passar `hasForDay`.
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

`<button class="dmo-button">` ou `<a class="dmo-button" href="…">`: azul com letra branca, o hover inverte. Desativado com `disabled` ou `aria-disabled="true"`, sem `opacity` em linha. Não há variante branca (`ghost`). Os botões de uma ação ficam juntos numa barra `dmo-list-actions`, afastada dos campos e das tabelas.

## Porque a tabela não é um partial

Cada tabela tem colunas e conteúdo diferentes; um partial genérico só passaria o HTML de um lado para o outro. O que tem de ser igual (cores, hover, seleção, rádio escondido, paginação) já está na classe `dmo-table`, no `data-dmo-row` e no `_ListPager`.

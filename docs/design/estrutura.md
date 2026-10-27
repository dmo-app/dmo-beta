# Estrutura fixa: páginas e painéis deslizantes

Pedido do Diogo (27/10/2026): «criar uma estrutura para usar sempre nas páginas
deslizantes e outra nas páginas normais». Todas as páginas novas ou revistas usam
uma das duas. Não se inventa outro cabeçalho, outro painel nem outra forma de
deslizar.

| Peça | Onde |
|---|---|
| Marcação (tag helpers) | `src/DMO.Alpha.Web/TagHelpers/DmoEstruturaTagHelpers.cs` |
| Aspeto (claro e escuro) | `wwwroot/css/dmo-estrutura.css` (carrega depois das folhas das páginas) |
| Abrir e fechar dos painéis | `wwwroot/js/dmo-painel.js` (`DmoPainel.abrir/fechar`) |
| Exemplo | `Pages/Shared/_DmoSupportPanel.cshtml` (painel de apoio) |

## Página normal: `<dmo-pagina>`

```cshtml
<dmo-pagina titulo="Pegamentos" titulo-id="peg-title" class="peg-page dmo-workspace" aria-labelledby="peg-title" data-dmo-troca>
  <dmo-voltar><partial name="_VoltarAoResumo" model="..." /></dmo-voltar>   @* opcional *@
  <dmo-topo>...campos ao lado do título (procura do Job On)...</dmo-topo>       @* opcional *@
  <dmo-acoes><a class="dmo-button" href="...">Imprimir</a></dmo-acoes>          @* opcional *@
  ...cartões da página...
</dmo-pagina>
```

Dá:

```html
<main class="peg-page dmo-workspace" aria-labelledby="peg-title" data-dmo-troca>
  <header class="dmo-pagina-topo">
    <div class="dmo-pagina-voltar">← Voltar</div>
    <h1 class="dmo-pagina-titulo" id="peg-title">Pegamentos</h1>
    <div class="dmo-pagina-extra">…</div>
    <div class="dmo-pagina-acoes">…</div>   <!-- canto superior direito -->
  </header>
  <div class="dmo-pagina-corpo">…</div>    <!-- só isto desliza -->
</main>
```

- O `<main>` fica com os atributos da página tal como estavam (classe, `aria-*`,
  `data-dmo-troca`): os testes e o `dmo-live.js` continuam a encontrá-lo. A
  estrutura reconhece-se por `main:has(> .dmo-pagina-topo)`.
- A página ocupa a altura do ecrã por baixo do cabeçalho da app; **só o corpo
  desliza**. A 1366×768 a página inteira nunca desliza e o título e as ações
  ficam sempre à vista. No telemóvel (≤ 720 px) volta a deslizar inteira.
- O corpo é uma grelha de uma coluna, cartões em altura natural, 16 px entre
  eles. A folha da página pode mudar o que está **dentro** do corpo (colunas,
  `gap`), por exemplo `main.peg-page>.dmo-pagina-corpo{gap:8px}`; não mexe no
  topo, no `overflow` nem na altura.
- Os seletores de filhos diretos do `<main>` (`main.x > .y`) passam a
  `main.x > .dmo-pagina-corpo > .y`.
- Dentro do painel de apoio (`?painel=1`) o título e o «← Voltar» saem (o painel
  já tem o título e o «Fechar»); as ações ficam. Sem ações, o topo sai. As
  páginas que ainda têm o `.page-head` antigo têm o mesmo tratamento só por CSS.
- A página do Peso fica como está (Diogo, 27/10/2026); só o título repetido sai
  quando abre no painel.

## Painel deslizante: `<dmo-painel>`

```cshtml
<button type="button" class="dmo-button" data-dmo-painel-abrir="riOutra">É outra CM/MF</button>

<dmo-painel id="riOutra" titulo="Outra CM/MF" largura="estreita" data-dmo-painel-confirmar>
  <dmo-acoes><button type="submit" class="dmo-button">OK</button></dmo-acoes>   @* opcional *@
  ...conteúdo...
</dmo-painel>
```

Dá um `<dialog class="dmo-painel" data-dmo-estrutura="painel">` com:

- `.dmo-painel-topo`: título à esquerda; ações e **«Fechar»** no canto superior
  direito (o «Fechar» vem sempre, não se escreve).
- `.dmo-painel-aviso`: «Existem alterações por guardar neste painel» com
  «Continuar a editar / Fechar sem guardar»; só aparece com
  `data-dmo-painel-confirmar` e um formulário mudado.
- `.dmo-painel-corpo`: o conteúdo; **só isto desliza**.

Atributos: `lado="direita"` (por omissão) ou `"esquerda"`; `largura="estreita"`
(420 px), `"media"` (640 px, por omissão), `"larga"` (até 1040 px) ou `"total"` (o ecrã menos 64 px; o Job On no painel de apoio).

Abre com um botão `data-dmo-painel-abrir="<id>"` ou `DmoPainel.abrir('<id>', botao)`.
Fecha com «Fechar», Escape ou um clique fora; o foco volta ao botão que abriu.
Eventos `dmo-painel:aberto` e `dmo-painel:fechado` para os scripts da página.

Um `<dmo-painel>` dentro de um `<form>` continua a enviar os campos com o
formulário (o `<dialog>` não corta a ligação).

O painel de apoio (iframe com outra página: Ficha da ferramenta, Peso, Pegamentos,
PU NNPB, Revisão, Consulta) usa a mesma marcação, com o seu próprio script
(`data-dmo-painel-proprio`, `js/dmo-support.js`).

O painel das linhas da folha do Job On (`.jof2-gaveta`) mantém a sua marcação e o
seu script (fica aberto entre páginas, sem bloquear a folha), com o aspeto da
estrutura: pela esquerda, estreito, «Planeamento» e «Fechar» no topo, a lista a
deslizar por dentro.

Por migrar: o painel «É outra CM/MF» da Reparação e as janelas `.dmo-dialog`.

## Regras de design que a estrutura já cumpre

Claro e escuro pelos tokens; botões de ação (`dmo-button`) iguais aos outros;
ações no canto superior direito; sem «×» nem círculos de dica; 1366×768 sem a
página inteira deslizar.

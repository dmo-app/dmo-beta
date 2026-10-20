# Atualizar só o que muda

Pedido do Diogo (17/10/2026): «não existe forma de fazer lock nos elementos e apenas refresh o que muda?». Quando um clique só muda uma parte da página, a página não recarrega: o resto fica onde está, sem piscar nem voltar ao topo. O primeiro caso foi o Job On (beta.165, PR #147; a procura passou depois a dois campos na beta.166, PR #145); gravar sem recarregar chegou com o PU NNPB (beta.167). Este guia diz como aplicar o mesmo a outra página.

O comportamento vive num sítio só, `wwwroot/0_ASSET_CANONICAL_INTERACTIONS.js` (`DmoTroca`). Uma página não escreve `fetch` nem handlers próprios para isto: marca o HTML com os atributos abaixo e o componente faz o resto.

## Como funciona

1. Um clique (numa linha, num dropdown `data-dmo-ir` ou num link) dentro de uma zona `data-dmo-parcial` pede ao servidor o mesmo endereço que a navegação normal pediria, com `fetch`. Se há envios à espera (passo 5), o pedido só sai depois deles (com o «A gravar…» no canto se a espera passar de 3 s). De dois cliques seguidos, só a resposta do último se aplica.
2. O componente compara a página nova com a atual. O que fica fora das zonas `data-dmo-regiao` tem de ser igual (o token antifalsificação não conta); as zonas `data-dmo-fica` não se comparam nem se trocam.
3. Se for igual, troca só as zonas `data-dmo-regiao` que mudaram, liga as listas novas (`data-dmo-list`), muda o título e o endereço (`history.pushState`) e, se o endereço tem uma âncora fora do ecrã, desliza até ela. Voltar atrás recarrega o endereço anterior.
4. Se não for igual, ou se uma zona nova trouxer algo que precise dos scripts da página, navega como antes. Nunca fica nada desatualizado.
5. **Gravar.** O envio de um formulário POST dentro de `data-dmo-parcial` vai com `fetch`, com o mesmo corpo (o token antifalsificação e o botão que enviou incluídos). O servidor grava e responde com o redirect para a página (PRG), como sempre; se voltou à mesma página, trocam-se as zonas como no passo 3 e o endereço é substituído (`history.replaceState`: gravar trinta punções não enche o «voltar»). Uma recusa volta à página com a mensagem e a mensagem aparece na pilha do canto. Se o servidor mandar para outra página, ou se a troca não der, abre-se o endereço final com um GET: o POST já foi feito e não se repete. Um ficheiro (um PDF) descarrega-se sem sair da página. Um segundo envio igual a um que ainda espera (o mesmo formulário com o mesmo corpo, sem contar o token) é ignorado; o mesmo formulário com outro corpo (outro número escrito) vai para a fila. Depois de gravar, a zona do formulário e as zonas com mensagens trocam sempre, mesmo que o servidor as desenhe iguais (beta.168): o formulário volta limpo, como numa página nova (a hora volta a «agora», o foco ao primeiro campo), e a mesma recusa duas vezes seguidas mostra-se as duas vezes. As outras zonas trocam se mudaram. Não há exceções: também a zona de um formulário que ainda espera na fila troca (abaixo).
   - **Mensagens antigas.** A resposta a um envio retira do canto as mensagens que lá estavam quando ele saiu e que eram do mesmo cartão (a zona do formulário; o componente marca-as com `data-dmo-origem`) ou da página (as que vieram com ela e o aviso de ligação): uma recusa antiga não fica ao lado da correção gravada a seguir. A recusa de outro cartão fica até se fechar, e as que chegam depois de o envio sair também. Uma mensagem marcada com `data-dmo-toast-fica` nunca sai assim, só quando se fecha: é o PIN temporário de um utilizador criado ou reposto (Admin → Utilizadores e a ficha do utilizador), mostrado uma vez. Antes, criar o utilizador seguinte, ou uma recusa a seguir, tirava o PIN do anterior, e o ADMIN que criava cinco operadores seguidos ficava só com o último (deep sweep, 18/10/2026, B-admin-1).
   - **Envios em fila.** Um envio feito antes de o anterior responder fica em fila, com o corpo lido quando o operador o enviou, e só parte depois de a resposta do anterior estar no ecrã. A resposta de cada envio aplica-se e troca as zonas, também a de um formulário que ainda espera: o ecrã mostra sempre a resposta mais nova (no PU NNPB, a linha de um número gravado aparece e o campo limpa com a resposta dele, mesmo com um visto a seguir na fila; vistos seguidos mudam o ecrã a cada resposta). A recusa de uma correção aparece mesmo com uma leitura nova enviada logo a seguir. Depois de cada troca, cada formulário que ainda espera:
     - é encontrado no elemento novo, pelo mesmo `id` ou, sem `id`, pela ação e pelos campos escondidos sem o token (no PU NNPB, o formulário do visto de cada linha); se só houver um formulário com essa ação, é esse;
     - volta a mostrar nos campos visíveis os valores que foram enviados (por nome e por ordem);
     - fica marcado à espera: `aria-busy="true"` e `data-dmo-a-gravar` no formulário, nos campos escritos e no botão que o enviou, com um estilo discreto em `dmo-normalize.css` (o fundo do campo em `--dmo-brand-050`, o botão a 60 % e o cursor `progress`) que não muda o tamanho de nada; a marca sai quando a resposta dele chega;
     - continua guardado contra o duplo envio: um segundo Enter ou clique igual no elemento novo é ignorado.

     O foco só vai para o `autofocus` de uma resposta se o operador não fez outra coisa depois desse envio (outro envio, um clique); fora disso fica no campo com o mesmo `id` (ver o passo 6). Com a rede lenta, vários envios seguidos fazem uma ida e volta de cada vez, cada um com a sua resposta no ecrã.
   - **Um clique durante a gravação.** Um clique (GET) feito com envios na fila espera por eles: um GET servido antes de a gravação chegar à base mostrava a página sem ela (a leitura gravada não aparecia e o valor ficava no campo). Se um envio sai enquanto o GET está a caminho, a resposta do GET não se aplica e o GET repete-se depois da gravação (só o GET; o POST nunca se repete).
   - **Um envio sem resposta.** Não há prazo: o componente nunca cancela um POST (cancelá-lo não desfaz o que o servidor grava, e a resposta que chega depois mostra o que ficou gravado). Ao fim de 3 s com a fila ocupada aparece no canto uma mensagem fixa, «A gravar… O servidor ainda não respondeu: não repita nem saia da página.» (`role="status"`; a pilha está fora do fluxo, por isso nada se mexe). Fica enquanto a fila estiver ocupada, também durante o GET de confirmação de uma ligação que falhou (abaixo), e sai quando a fila esvazia. Só a falha da ligação e o erro do servidor levam à confirmação. O tempo está em `DmoTroca.tempos.aviso` (ms; os testes encurtam-no).
   - **A ligação falha.** O POST nunca se repete: se a ligação cair depois de o servidor gravar, enviar outra vez gravava duas vezes. O componente pede a página outra vez com um GET e troca só as zonas que mudaram: o que ficou gravado aparece e o que está escrito numa zona que não mudou fica. O GET pede o último endereço que o servidor desenhou sem uma recusa (não o endereço de uma recusa anterior, que o servidor desenharia outra vez com a recusa e os valores recusados; se a página abriu com uma recusa e ainda não mostrou nada sem ela, pede o endereço atual; ver a limitação abaixo) e as mensagens dele não se mostram: as do cartão que estavam no canto saem e fica uma mensagem fixa, «A página mostra agora o que ficou gravado: confirme antes de repetir». Se o GET também falhar, nada se troca, o que está escrito fica e a mensagem diz que não foi possível confirmar se ficou gravado. O envio fica na fila, como com um servidor lento, e o GET repete-se quando a ligação volta (o evento `online` ou, sem ele, ao fim de 5 s, `DmoTroca.tempos.confirmar`), sem repetir o POST: o que ficou gravado aparece sozinho e os envios seguintes partem a seguir. Antes, ficava tudo como estava e um novo clique gravava outra vez (deep sweep, 18/10/2026, B-boquilhas-2). Se a confirmação mostra o que ficou gravado mas o formulário fica com o que estava escrito (a zona dele não mudou), carregar outra vez no botão também não grava duas vezes: cada envio leva uma chave no cabeçalho `X-Dmo-Envio`, o mesmo envio (o mesmo formulário com o mesmo corpo) depois de uma falha leva a mesma chave, enquanto o operador não escrever no formulário (escrever, mesmo o que já lá estava, faz um envio novo: no Peso, a mesma leitura escrita outra vez depois de a confirmação a mostrar gravada é uma leitura nova), e um handler marcado com `[EnvioUnico]` (`Services/EnvioUnico.cs`) grava cada chave uma só vez: lembra-se dela 30 minutos, em memória, e a um envio repetido responde com o mesmo redirect, sem correr o handler; um que chega enquanto o primeiro grava espera por ele. Só se lembra de redirects (uma recusa desenhada com `Page()` corre outra vez). Marcados: o movimento das Boquilhas, a leitura dos Pegamentos, a punção do PU NNPB, a leitura do Peso (deep sweep, 18/10/2026, B-boquilhas-2, B-pegamentos-3, B-peso-3) e o «Guardar comparação» do Peso (B-peso-4). Se a confirmação troca a zona do formulário, o formulário novo começa sem chave: o operador que lá volta a escrever o mesmo (uma leitura igual à que ficou gravada) faz um envio novo. Uma página que repõe no formulário novo o que foi enviado (no `dmo:fim`, com `detail.semResposta`) chama `DmoTroca.mesmoEnvio(antigo, novo)` e o mesmo envio outra vez leva a mesma chave. É o que faz a comparação do Peso: «Comparar pesos» e «Guardar comparação» eram um POST nativo e, com a ligação a cair, o operador via a página de erro do Chrome, a comparação ficava guardada sem ele saber e voltar atrás perdia as massas. Agora vão pelo componente (o formulário em `#pesoComparacaoLeituras`, com `data-dmo-parcial`; o contexto, o resultado e a guardada em `#pesoComparacaoContexto`, `#pesoComparacaoResultado` e `#pesoComparacaoGuardada`); a confirmação é o GET da comparação, que não mostra a guardada, por isso as massas enviadas voltam ao formulário e guardar outra vez mostra a guardada sem guardar outra (deep sweep, 18/10/2026, B-peso-4). Um handler novo que grava a partir de um formulário com DmoTroca deve levar `[EnvioUnico]`. Limitação conhecida: se o operador recarregou a página (F5) com o endereço de uma recusa e a ligação falha no envio seguinte, antes de a página mostrar alguma resposta sem recusa, o único endereço que o componente conhece é o da recusa; o GET de confirmação desenha outra vez essa recusa e os valores recusados (o que ficou gravado aparece na mesma). Não há forma genérica simples de saber o endereço limpo de cada página (os parâmetros da recusa são de cada uma), por isso fica assim; o operador vê a mensagem fixa a pedir para confirmar.
   - **O servidor recusa.** Uma resposta 4xx a um envio (400 sem corpo quando a sessão mudou noutro separador e o token antifalsificação já não serve; 403 quando outra pessoa entrou como ADMIN no mesmo browser) não muda de página e o POST não se repete: a antifalsificação e a porta do ADMIN correm antes do handler, por isso nada foi gravado. O formulário fica com o que estava escrito, as mensagens do cartão saem e aparece no canto uma mensagem fixa, «O servidor recusou o envio. O que escreveu continua no ecrã: recarregue a página e grave outra vez.», com o link «Recarregar a página» para o endereço limpo. Antes, a página abria outra vez com um GET e o escrito perdia-se sem aviso (sweep do Codex, 19/10/2026, FE-1).
   - **A sessão terminou.** A resposta a um envio que acaba em `/login` (o redirect do cookie, que o `fetch` segue) ou um 401: nada foi gravado (o `[Authorize]` corre antes do handler). Como na recusa, com «A sessão terminou e nada foi gravado. O que escreveu continua no ecrã: entre outra vez e grave de novo.» e o link «Entrar» para `/login?ReturnUrl=<esta página>`, noutro separador, para o escrito continuar à vista. Antes, o login abria-se por cima.
   - **Erro do servidor.** Uma resposta 5xx (a página de erro, ou o proxy): não se sabe se gravou, porque o handler pode ter falhado depois de gravar. Segue o caminho da ligação que falha (abaixo): o GET de confirmação mostra o que ficou gravado, com «O servidor deu um erro. A página mostra agora o que ficou gravado: confirme antes de repetir.», e o mesmo envio outra vez leva a mesma chave. Antes, a página de erro ficava no lugar do ecrã. O `document.write` continua só para uma resposta 2xx sem redirect que não cabe nas zonas (uma página desenhada pelo próprio POST); tirá-lo de vez fica para o full.
   - **Um GET pedido por um formulário.** Quando a página manda o submit de um formulário `method=get` para `DmoTroca.ir(endereço, formulário ou botão)` (no Peso, o «Ver a diferença» do Supomos e a escolha da comparação), ou quando o componente o faz sozinho (beta.180: um formulário GET dentro de `data-dmo-parcial` para a mesma página, como as setas do mês do calendário, vai pelo `ir` com o botão que o enviou; o ouvinte está no `window`, por isso uma página que trata o seu GET no `document`, como o Peso, vai primeiro), a mesma recusa duas vezes seguidas mostra-se as duas vezes, como num envio: as mensagens de uma zona igual à anterior voltam ao canto (as antigas dessa zona saem) e a zona não se troca. Num link ou numa linha, só trocam as zonas que mudaram, como sempre.
   - **Eventos para a página.** Cada envio tem um número (`detail.envio`), o mesmo em todos os eventos dele.
     - `dmo:fila`, no formulário (sobe ao `document`), quando o envio entra na fila (um envio igual a um que espera não entra): `detail.form`, `detail.corpo` (o `FormData`), `detail.submitter` e `detail.envio`. Nos Pegamentos, é aqui que começa a ação do cartão.
     - `dmo:enviar`, no `document`, antes de o envio partir: `detail.form`, `detail.corpo` e `detail.envio`. A página pode acertar o corpo com o que as respostas anteriores puseram no ecrã (nos Pegamentos, se a correção aberta noutro cartão ainda está aberta).
     - `dmo:troca`, no `document`, depois de cada troca: `detail.zonas` (os ids trocados), `detail.origem` (o formulário enviado ou o elemento clicado), `detail.envio` (o número do envio a que a resposta responde, ou `null` num clique), `detail.recusa` (se as zonas trazem uma recusa), `detail.ativo` (o elemento que tinha o foco antes da troca; se estava numa zona trocada, já não está na página) e `detail.pagina` (o documento novo inteiro; beta.180, o Job On lê dele o contexto da procura, que fica).
     - `dmo:fim`, no `document`, no fim de cada pedido (aplicado, falhado ou substituído por outro clique): `detail.origem` e `detail.envio`, para a página arrumar o que preparou para ele. Num envio, `detail.semResposta` é `true` quando a ligação falhou antes de chegar a resposta e o que se vê é a confirmação (deep sweep, 18/10/2026, B-peso-4).
6. **Foco.** Depois de trocar, o foco vai para o campo com `autofocus` da zona nova (no PU NNPB, o número da próxima punção) ou, sem ele, para o elemento com o mesmo `id` que tinha o foco, sem deslizar a página. A resposta de um pedido antigo (o operador já fez outra coisa a seguir) não puxa o foco para o `autofocus`: o foco fica no elemento com o mesmo `id` (no PU NNPB, o número que espera na fila, já reposto com o que se enviou). Um botão sem `id` de um formulário que espera (o visto de uma linha) passa para o mesmo botão no elemento novo. Um campo que um script cria depois da troca (o filtro `#<id>-filtro` de um `select` com `data-dmo-filter`, criado pelo `dmo-suggest`) ainda não existe nesse momento: a página guarda o `id` do campo antes do pedido e devolve-lhe o foco no `dmo:troca`, com `DMOSuggest.focar(campo)`, que não abre a lista e deixa o texto selecionado, para escrever substituir a escolha (no Peso, a produção a comparar). Se o operador entretanto foi para outro campo, o foco fica lá. Por isso os elementos que se usam com o teclado numa zona que troca têm `id` estáveis: no calendário partilhado (`_DmoCalendar`, no Job On, no histórico das Boquilhas e na Revisão), as setas `#dmoCalendarioAnterior` e `#dmoCalendarioSeguinte` e cada dia `#dmoDia-<dia>`; na lista do dia do Job On, cada produção `#jobonLinha-<jobon_id>`. Depois de escolher um dia, uma produção ou a seta do mês com o Enter, o foco fica no mesmo elemento da zona nova e o Enter seguinte continua dali; antes ia para o `body` (deep sweep, 18/10/2026, B-jobon-9).

Por omissão, a mesma página Razor serve a navegação normal e a troca. Nas zonas com handler parcial explícito (ver abaixo), o servidor devolve apenas o bloco. Sem JavaScript, tudo continua pelos links GET normais.

## Os atributos

| Atributo | Onde | O que faz |
|---|---|---|
| `data-dmo-troca` | um elemento que envolve tudo o que a página pode trocar (no Job On, `.beta-light-layout`) | liga o componente na página; o que está dentro e fora das zonas é comparado |
| `data-dmo-regiao` + `id` | cada bloco que muda (no Job On, `#jobonAvisos` e `#jobonFolhas`) | trocado quando o HTML novo é diferente |
| `data-dmo-fica` | um bloco que o operador está a usar e que deve ficar como está (no Job On, a procura com a referência e o dropdown das produções) | nem comparado nem trocado |
| `data-dmo-parcial` | à volta dos elementos cujos cliques ou envios trocam só uma parte (a procura, a lista do dia, o «Fechar» da ficha; no PU NNPB, os cartões da identificação e da lista) | sem ele, o clique e o envio navegam como sempre |
| `data-dmo-script` | num elemento que um script da página liga no arranque (no Job On, o formulário «Preparar produção», `#lightForm`) | uma zona nova que o traga faz abrir a página inteira, para o script o ligar |
| `data-dmo-nativo` | num formulário POST dentro de `data-dmo-parcial` (beta.180) | envia-se como sempre, sem gravar pelo componente |
| `data-dmo-completa` | num link, ou à volta dele, dentro de `data-dmo-parcial` (beta.179: a paginação dos registos encontrados na procura das Boquilhas, que está num bloco que fica) | o clique abre a página inteira, como sem `data-dmo-parcial` |
| `data-dmo-zonas` | à volta de formulários POST que estão numa zona (beta.189: no Resumo, as áreas; nos Pegamentos, os cartões), com os ids das outras zonas que a gravação muda | o GET depois de gravar pede ao servidor só a zona do formulário e estas (abaixo, «O servidor desenhar só a zona»); sem ele, vem a página inteira |
| `data-dmo-depende="idDoCampo"` (+ `data-dmo-depende-valor`) | num dropdown (ou botão) que depende do texto de um campo de procura (deep sweep 2, 18/10/2026, F-8 e F-10) | sem `-valor` (os Históricos): escrever no campo põe o dropdown na opção vazia, para a produção da referência anterior não ir na procura seguinte. Com `-valor` (a escolha do Job On dos Pegamentos, do Peso e do PU NNPB): enquanto o texto escrito não é esse valor (sem contar maiúsculas nem espaços), o dropdown e o «Associar» ficam desativados; voltar ao mesmo texto ativa-os. Por delegação: o dropdown vem numa zona trocada. A cada troca de zonas volta a ver-se o texto que está agora no campo (que fica): uma resposta que chega depois de se escrever outra referência traz o dropdown desativado (com `-valor`) ou na opção vazia (sem `-valor`, quando o campo já não tem o texto com que a página nova foi desenhada); antes, Enter numa referência e escrever outra com a resposta a caminho deixava ativas as produções da primeira (revisão, 18/10/2026) |

A procura ao escrever (`data-dmo-procura`, no Job On a referência com o dropdown «Produção» à frente; também no Peso, no Resumo e nas Boquilhas) troca só o seu alvo. Enquanto a lista do texto novo não chega, o que está no alvo é do texto anterior: desde a tecla até à resposta, o alvo fica à espera (`data-dmo-procura-espera` e `aria-busy`), com os dropdowns e os botões desativados e as linhas e a paginação sem efeito; nada se mexe e não aparece mensagem. Sem ligação fica assim e a mesma procura repete-se quando a ligação volta. Antes, escrever outra referência e escolher logo a primeira produção abria uma produção da referência anterior (deep sweep, 18/10/2026, B-jobon-6). Com a espera, a lista fica esbatida (`dmo-normalize.css`, com 0,3 s de atraso para não piscar a cada tecla). Se o servidor responde sem a lista, esperar pela rede não resolve: o alvo continua bloqueado, sem `aria-busy`, e aparece uma mensagem fixa no canto, «A sessão terminou. Entre outra vez.» com o link «Entrar» (no mesmo separador: só se perde o texto da procura) quando a resposta acabou no login, ou «Não foi possível procurar. Escreva de novo.» noutro erro. Escrever de novo procura outra vez. Antes, a procura ficava presa sem mensagem (sweep do Codex, 19/10/2026, FE-3).

Uma zona dentro de um contentor flex ou grid com `gap` leva também a classe `dmo-zona` (`display: contents`): o `div` da zona não cria caixa e os cartões ficam com o espaço de sempre.

Uma zona pode estar dentro de um bloco que fica. É assim que um formulário que o operador está a preencher guarda o que já escreveu e leva, mesmo assim, o contexto atual da página: os campos visíveis ficam (`data-dmo-fica`) e os campos escondidos com o contexto vão numa zona dentro do formulário. No Peso, o bloco da produção anterior (SAP, `#pesoAnterior`) fica e `#pesoAnteriorContexto` troca com a comparação e o Supomos; o «Suporte de produção TP» fica (um `<details>` aberto muda o HTML fora das zonas) e `#pesoCapContexto` troca dentro dele.

Uma zona pode ter zonas dentro (beta.179: no Job On, a ficha `#jobonFolhas` com a tabela `#fichaProducoes` das produções da referência). Se o que é só dela (sem as de dentro) não mudou, fica, e trocam só as de dentro: paginar a tabela não troca o cartão nem os campos dele. Se mudou, troca uma vez, com as de dentro. A paginação partilhada (`_ListPager`) tem `data-dmo-parcial`: numa página com `data-dmo-troca`, a lista paginada tem de estar numa zona (ou levar `data-dmo-completa`).

Um `<details>` que o operador abriu ou fechou fica como ele o deixou quando a secção tem `data-dmo-fica` e `data-dmo-parcial` e as partes que mudam são zonas lá dentro (na ficha da ferramenta, o `<summary>` da Informação técnica e o formulário): o `open` não é comparado nem trocado, e gravar noutra secção não fecha a técnica nem lhe apaga o que lá está escrito.

```cshtml
<div class="beta-light-layout" data-dmo-troca>
  …
  <div id="jobonAvisos" data-dmo-regiao>@* avisos *@</div>
  …
  <div class="jobon-procura-bloco" data-dmo-fica data-dmo-parcial>@* procura *@</div>
  <div id="lightProductionList" data-dmo-list="Produções do dia" data-dmo-parcial>@* linhas *@</div>
  …
  <div id="jobonFolhas" data-dmo-regiao>@* ficha e «Preparar produção» *@</div>
</div>
```

## Passos para uma página nova

1. **Escolher os cliques.** Os que se repetem e só mudam uma parte da página: abrir um registo a partir de uma lista, fechar uma ficha, mudar de página numa tabela. Ver «Onde aplicar», abaixo.
2. **Envolver cada parte que muda** num `<div id="…" data-dmo-regiao>` que existe sempre, mesmo vazio (um `@if` vai para dentro dele, nunca à volta). Um `div` sem classe não muda o layout.
3. **Marcar com `data-dmo-fica`** o que o operador tem nas mãos e não deve ser desenhado de novo (uma procura com texto escrito, um dropdown já aberto).
4. **Pôr `data-dmo-parcial`** à volta das linhas, dropdowns ou links cujos cliques devem trocar só a parte.
5. **Fazer os endereços desses cliques guardar o contexto da página** (mês, dia, filtros, página da tabela). Se o endereço deixa cair um parâmetro, o servidor desenha outra coisa fora das zonas e o componente navega a página inteira. No Job On, as linhas da lista do dia passaram a levar `ano`, `mes` e `dia`. O mesmo vale para os formulários POST: cada um leva o contexto em campos escondidos e o redirect do servidor devolve-o (no Peso, `comparar` e `supor_calote` ao registar uma leitura e ao guardar o SAP, também no Peso aberto pelo Histórico).
6. **Confirmar que nada fora das zonas depende do que se abre.** Uma forma rápida: pedir as duas páginas (antes e depois do clique), tirar as zonas e comparar o resto. Ver «Como validar».
7. **Para gravar:** pôr `data-dmo-parcial` à volta dos formulários POST que voltam à mesma página; dar `autofocus` ao campo onde o operador continua (a próxima leitura); passar os scripts da página que ligam elementos dentro das zonas a ouvintes delegados no `document` (no PU NNPB, o `data-pu-mirror`; nos Pegamentos, `pegamentos-registo.js`, que volta a preparar as zonas novas no `dmo:troca`) ou, se não der, marcá-los com `data-dmo-script`. Se uma tabela crescer por cima do que o operador vê e o Chromium mexer uns píxeis no scroll a cada troca (scroll anchoring), pôr `overflow-anchor: none` na página, como nos Pegamentos; aí, o que está por cima do formulário não pode mudar de altura enquanto o operador grava (o `scrollBy` não compensa nada com a página no topo). Nos Pegamentos, as mensagens vão para a pilha do canto (`data-dmo-toast`, fora do fluxo; os avisos de uma medição gravada, como «Medição registada sem contra costura…» ou «Medição registada como «não avaliável»», são `aviso`: não fecham sozinhos e ficam até o operador os fechar no × ou fazer a ação seguinte nesse cartão; uma leitura escrita durante a espera e registada logo a seguir à resposta que trouxe o aviso não conta, porque foi escrita antes de ele aparecer) e o contexto por cima dos cartões guarda a altura que tinha (`min-height`) até se recarregar a página: a dica da folha em branco sai na primeira medição e os cartões não sobem. O ecrã só acompanha o campo da próxima leitura depois de registar; corrigir, cancelar e apagar não o mexem. Um link que abre uma correção no lugar da linha não leva âncora (`#linha`): a âncora levava a linha para o topo do ecrã, por baixo da barra fixa. Quem escreve a leitura seguinte logo a seguir a registar (Enter), a guardar uma correção (Enter), a cancelá-la (Esc) ou a apagar (×), enquanto o pedido espera, não pode ficar com meio número num campo: nos Pegamentos, os campos enviados por um registo ficam só de leitura e as teclas desse cartão escritas no campo onde o operador estava quando a ação partiu, ou numa linha nova do cartão enquanto um registo dele espera, guardam-se numa fila (Tab passa à contra costura, Enter fecha a leitura e começa a seguinte); quando o cartão volta sem registo à espera, escrevem-se na linha nova e, se tinham Enter, regista-se. Sem registo à espera, a linha nova escreve-se como sempre: Enter numa linha nova já escrita, enquanto uma correção do mesmo cartão grava, regista (vai para a fila do DmoTroca). Várias ações no mesmo cartão (registar e logo apagar outra leitura, apagar e logo registar, guardar uma correção e registar) seguem na fila, cada uma com a sua resposta: a resposta do registo mostra a leitura na tabela e a linha nova vazia, com o foco na costura (ou onde o operador já estiver a escrever na linha nova), e a leitura registada nunca volta como rascunho; enquanto o registo espera atrás de outra ação, a linha nova mostra o que ele enviou, só de leitura e marcada à espera. Se o operador voltou a uma correção do mesmo cartão enquanto o registo esperava e a resposta a fecha a meio do número (antes do Enter que a guarda), o que lá escreveu perde-se (uma correção de cada vez) e o foco não salta para a linha nova: o resto do número não vai parar lá nem se regista. Se já a tinha guardado, a correção segue na fila e grava. Se o operador clicou noutro campo da linha nova durante a espera (a contra costura), o que escreve a seguir vai para esse campo. Numa recusa, o que se escreveu fica em linhas a seguir, sem registar; sem ligação, o registo espera que ela volte (os campos enviados só de leitura e as teclas na fila, como com o servidor lento) e, se a confirmação mostra que nada ficou gravado, o mesmo: os campos voltam a escrever-se e o foco fica na linha onde o operador escrevia (o resto do número continua lá, não no valor enviado). As teclas escritas noutro cartão ou fora da tabela não se guardam. Uma correção de cada vez, como antes: abrir a correção de outra leitura, ou registar ou apagar no cartão da correção, fecha a que estava aberta e o que lá estava escrito perde-se; registar ou apagar noutro cartão mantém-na aberta, com o que lá está escrito (ver `pegamentos-registo.js`). Um painel aberto fora das zonas (o «Associar Job On» dos Pegamentos) fica num invólucro `data-dmo-fica`, para gravar com ele aberto não abrir a página inteira.
8. **Testes:** no teste da página, verificar as marcas (`data-dmo-troca`, as zonas e o `data-dmo-parcial`); no browser (Chromium local com a base do teste de carga), confirmar que a página não recarrega (uma marca em `window` sobrevive ao clique) e que o resto não mexe. Nas verificações de interface (`scripts/stress/ui`), uma gravação que passa a trocar só a zona espera com `C.gravar(page, acao)` em vez de `C.navegar`. `C.gravar` também aceita que a página abra inteira; um passo que quer provar que não recarregou deixa uma marca no `window` e confirma-a depois (no Peso, `semRecarregar`). Nos testes jsdom, a prova é a troca a sério: os scripts reais da página e do componente, com um servidor falso que devolve a página desenhada a partir do mesmo `.cshtml` (`tests/ShellProof/peso-live.test.cjs`), e não uma troca feita à mão no teste.

## Quando a página abre inteira

Mudar de separador ou de módulo abre outra página. Para isso não piscar (Diogo, 18/10/2026: «o flash que acontece quando clicamos em elementos»; «sidepanel não deveria fazer flash, sempre fixo»), o `_DmoShellLayout` tem no `<head>` um `<link rel="expect" href="#dmoFim" blocking="render">` e acaba com `<div id="dmoFim" hidden></div>`, depois dos scripts. O browser só desenha a página nova quando chega a esse marcador: até lá fica a anterior no ecrã, e depois troca de uma vez. O cabeçalho, as barras e o painel lateral estão no mesmo sítio nas duas páginas, por isso não se mexem. Sem o marcador, com o DEV longe, aparecia primeiro o cabeçalho com a página em branco e depois o painel lateral a encher máquina a máquina. O marcador fica sempre o último elemento do `<body>` (`SharedComponentsTests`). Funciona no Chrome e no Edge; os outros browsers ignoram o atributo e abrem como antes.

## O que obriga a abrir a página inteira

Uma zona nova que traga algum destes elementos faz o componente navegar, porque eles só funcionam ligados pelos scripts no arranque da página:

`[data-dmo-script]`, `script`, `dialog:not(.dmo-dialog)`, `dialog[data-dmo-dialog-aberta]`, `[data-dmo-procura]`.

Os formulários POST e as mensagens (`[data-dmo-toast]`) já podem vir nas zonas (beta.167). As janelas da casa (`dialog.dmo-dialog`, aberta por `data-dmo-dialog`) e os dropdowns que aplicam ao escolher (`data-dmo-autosubmit`) também, desde a beta.171: ligam-se por delegação no `document`. Gravar dentro de uma janela que vinha na zona fecha-a com a troca e o foco volta ao botão que a abriu; o `autofocus` de uma janela fechada não conta. Uma janela aberta numa zona que troca por causa de outro formulário (o comentário de uma área do Resumo, aberto enquanto o OK/NOK da mesma área grava) volta a abrir na zona nova com o que o operador escreveu, com o foco e o cursor no mesmo sítio (beta.214). Um dropdown ou uma data com `data-dmo-autosubmit` mudados com o teclado (setas, letras, números) só aplicam com Enter ou ao sair do campo; com o rato aplicam logo (beta.214). Fica na lista a janela que abre com a página (`data-dmo-dialog-aberta`). As sugestões ao escrever (`data-dmo-suggest`) também podem vir nas zonas desde a beta.172: o componente liga-as na zona nova com `DMOSuggest.init`, como no arranque (na ficha da ferramenta, a «Referência MF associada»; nas Boquilhas, o «Associar Job On» de um registo sem Job On). O copiar de um link (`data-dmo-copiar`, os links do mcaliper da Revisão) também, desde a E8: liga-se por delegação no `document`. Os valores editados no sítio (`data-dmo-valor`) também, desde a E10: ligam-se por delegação no `document` (o valor gravado é o do HTML, `defaultValue`, e Esc volta a ele) e uma recusa vai para a pilha do canto; nas Definições das Boquilhas, a lista dos reparadores troca depois de criar um e os nomes novos editam-se logo. Hoje, abrir o «Editar» do Job On (`#lightForm`, ligado pelo script da ficha) ou mudar de dia no calendário continuam a recarregar. Para os incluir, o caminho é tornar a ligação desse elemento delegada (um ouvinte no `document`, como já fazem as dicas `_DmoHelp` e as Opções), ou expor uma função de ligação que o componente chame nas zonas novas, e só depois tirá-lo da lista acima. Uma página que precise de religar o seu próprio script ouve o evento `dmo:troca` (`event.detail.zonas` tem os ids trocados).

## Como validar

O critério do Diogo para cada página: **o ecrã mantém-se, o foco continua no sítio certo, os dados gravados aparecem e não há pedidos duplicados**.

- **Página não recarrega:** no Chromium, `window.__marca = 1` antes do clique; depois do clique, `window.__marca` tem de continuar a 1.
- **Nada fora das zonas muda:** pedir os dois endereços com `fetch`, remover `[data-dmo-troca]` e as zonas, e comparar o resto (incluindo as barras de cima, que estão fora da raiz e não são trocadas).
- **Desempenho:** medir no Chromium local o tempo do clique até a zona aparecer, com e sem a troca. No Job On: 78 ms → 63 ms (mediana de 18). Os estilos e scripts já vinham da cache do browser nas duas formas, por isso a rede faz um pedido nos dois casos; o ganho está no browser não ter de reconstruir a página, e no que o operador vê (nada pisca nem salta).
- **Pedidos:** contar no Chromium os pedidos `document` e `fetch`: uma gravação faz um POST (e o GET do redirect), também com um duplo clique rápido, quando a ligação falha depois de o servidor gravar (aí o pedido seguinte é um GET `fetch` da página, nunca outro POST) e com o servidor lento (o POST espera o que for preciso, com o «A gravar…» no canto, e nunca é cancelado). Um clique feito durante uma gravação sai depois dela; se a gravação saiu com o GET já a caminho, o GET repete-se (dois GET, um POST).
- **Gravar:** medir da tecla Enter até a linha nova aparecer, com a troca e com o envio nativo (`HTMLFormElement.prototype.submit.call(form)` e esperar `load`). No PU NNPB (local, mediana de 15): 62 ms a página inteira, 64 ms só a zona. Gravar é um POST e um GET nas duas formas, por isso localmente não há ganho de tempo; o ganho é a página não piscar, não voltar ao topo e o foco ficar no número.
- `tests/ShellProof/troca-parcial.test.cjs` cobre o componente (jsdom, no CI local): trocar, fechar, abrir a página inteira, gravar, duplo envio, recusa, outra página, PDF, formulário fora de `data-dmo-parcial`, mensagens antigas, dois formulários seguidos, envios na mesma zona nas duas ordens e com três envios, com e sem `id` no formulário (a zona troca a cada resposta, o que espera volta escrito e marcado, o foco fica no mesmo `id` e um segundo Enter não grava duas vezes), vistos seguidos, o mesmo formulário com outro corpo, um clique durante a gravação e um clique ainda a caminho quando sai uma gravação, um servidor lento (nada se cancela e o «A gravar…» fica até a fila esvaziar), a ligação a falhar depois de gravar (com e sem o GET de confirmação, com envios em fila, e depois de uma recusa) e a mesma recusa duas vezes num GET pedido por um formulário. `tests/ShellProof/pegamentos-registo.test.cjs` cobre os Pegamentos: a leitura seguinte escrita durante o envio (também depois de corrigir, de Esc e do ×), o clique na contra costura, dois envios em fila, várias ações no mesmo cartão (registar e apagar, apagar e registar, Enter na linha nova durante uma correção guardada ou recusada, uma recusa com um × pelo meio, uma correção fechada pelo registo a meio do número, a ligação a cair a meio do número seguinte), o aviso de uma medição gravada (fica até à ação seguinte no cartão ou ao ×), a correção aberta noutro cartão e a folha em branco com a página no topo (com um modelo do layout: a dica sai e os cartões não sobem). No browser, as sondas da revisão correm com o servidor a responder 150, 500 e 1500 ms depois de cada pedido.

## O servidor desenhar só a zona

O Resumo usa a lista parcial desde a beta.181. A beta.182 acrescenta
`HistoricoResultados` no Histórico de Job On; a beta.183 acrescenta `Resultados`
na pesquisa/paginação de Ferramentas; a beta.184 acrescenta `Resultados` nos
filtros da Revisão, conservando o controlo aberto e a nota em edição.
Os handlers partilham o HTML com a página
completa e fazem apenas as leituras necessárias ao bloco, com as mesmas
verificações de autenticação e entrada no módulo.

No formulário GET ou no invólucro da paginação, declarar `data-dmo-handler` com
o nome do handler e `data-dmo-alvo` com o id da região. O cliente acrescenta
`handler` só ao pedido fetch; o endereço do browser e os hrefs continuam normais.
O servidor devolve exatamente uma raiz com esse id e `data-dmo-regiao`, mais o
cabeçalho `X-Dmo-Region` igual ao alvo. O componente rejeita alvos diferentes,
raízes adicionais e elementos que precisem de scripts; nesse caso abre o GET
normal. A fotografia da região é atualizada para continuar a suportar outras
trocas da página. O pedido vai pelo mesmo `ir` dos cliques (espera pelas
gravações em fila; de dois pedidos seguidos só o último se aplica) e, depois
de trocar, as mensagens, as sugestões, o foco e o `dmo:troca` são como numa
troca normal. `data-dmo-completa` num link pede a navegação normal, por
exemplo para limpar também os campos que a procura preserva.

**Depois de gravar (beta.189).** O GET que segue o redirect de uma gravação
pode pedir só as zonas que a gravação muda. Um formulário POST dentro de uma
zona e de um elemento com `data-dmo-zonas` envia o cabeçalho `X-Dmo-Zonas` com
o id da sua zona e os ids declarados (sem repetir), lidos quando o envio parte
(a resposta anterior pode ter mudado a lista). O `fetch` leva o cabeçalho
também ao GET do redirect. A página que o conhece (`DmoZonasPedidas.Ler`)
responde a esse GET só com essas zonas, pela ordem pedida, com os mesmos
partials da página completa, `X-Dmo-Region` igual ao pedido e
`Vary: X-Dmo-Zonas`. É o mesmo handler `OnGet`: as mesmas verificações de
autenticação e de entrada no módulo. Uma zona que a página não conhece, ou
que não pode desenhar nesse estado (no Resumo, sem produção), faz responder
com a página inteira, sem `X-Dmo-Region`, como antes. O componente troca as
zonas recebidas numa cópia da fotografia e segue como numa troca normal
(mensagens, foco, «A gravar…», a fila). Se a resposta não cumprir o contrato
(outro `X-Dmo-Region`, uma raiz a mais, a menos ou fora de ordem, uma zona sem
`data-dmo-regiao`, um elemento da lista abaixo), pede a página inteira com um
GET sem o cabeçalho e troca como antes; se isso também falhar, abre a página.
O POST nunca se repete.

- Resumo: OK/NOK de uma área → o cartão da área (`resumo-area-XX`), a
  mensagem (`resumoEstadoAvisos`) e o «Visto por» (`resumoVistoPor`).
- Pegamentos: registar, corrigir ou apagar uma leitura → o cartão
  (`pegCartaoXX`), as mensagens (`pegAvisos`) e o mapa (`pegMapa`); o contexto
  (`pegContexto`) só vai enquanto a folha não tem medições (a dica da folha em
  branco sai na primeira).

Limite: o que está fora das zonas pedidas e foi mudado por outra pessoa (a
aprovação da produção, o aviso de alterações do Job On, os controlos ligados)
só se atualiza na navegação seguinte, como já acontecia com as zonas iguais.

As medições e os testes de cada alteração estão no
[registo de respostas parciais do servidor](../dev/respostas-parciais-servidor/README.md).
São medições locais em PostgreSQL descartável. Abrir a Ficha do Job On e outras
zonas ainda pode preparar a página inteira; escolher o próximo bloco depende
de medir o respetivo pedido e preservar o seu contexto.

## Etapa beta.178: paginações das ferramentas

Ferramentas e Boquilhas → Ficheiros usam agora `DmoTroca` nas setas da
paginação. Os formulários de pesquisa mantêm os campos e partilham a ordem dos
pedidos com a paginação. A lista inicialmente oculta pode tornar-se visível sem
recarregar. O teste `tests/visual/paginacao_parcial_browser.py` valida avançar e
recuar nas duas listas, com HTML exportado pelos testes HTTP, em desktop e móvel.
Os restantes casos da secção 1 abaixo ficam para as etapas seguintes (as gravações POST já estavam feitas na `main`, beta.167 a 172).

## Etapa E10: Definições e Admin

Gravam sem recarregar, cada uma com a raiz `data-dmo-troca`, as zonas abaixo
(com `data-dmo-parcial`) e as mensagens na pilha do canto (`data-dmo-toast`;
antes algumas eram um parágrafo no cartão, que empurrava o resto). Os botões e
os campos têm `id`, para o foco ficar onde estava:

| Página | Gravação ou clique | Zonas | Foco depois |
|---|---|---|---|
| Controlo → Definições | guardar a tolerância dos Pegamentos | `#ctlDefTolerancia` (a pasta dos PDFs fica fora, como o script a deixou) | o campo ou o «Guardar» |
| Ferramentas → Definições | guardar os totais de lote | `#ferrDefTotais` (o aviso do que ficou de fora vai para o canto, `aviso`) | o campo do total ou o «Guardar» |
| Boquilhas → Definições | criar um reparador; escolher a rota de uma máquina | `#bqDefAvisos`, `#bqDefReparadores`, `#bqDefRotas` (uma mensagem só por gravação) | o campo do nome (limpo) ou o dropdown |
| Admin → Setup | guardar o «Email do ADMIN» | `#admEmail` (se o email passou a ser de outra pessoa, a sessão cai e abre-se o login) | o «Guardar» |
| Admin → Canal Jobon | escolher a hora de viragem | `#canalJobonViragem` | o dropdown |
| Admin → Utilizadores | criar um utilizador | `#dmuAvisos`, `#dmuCriar`, `#dmu-resultados` (o email e o PIN temporário vão para o canto como `aviso`: ficam até se fecharem) | o campo ou o «Criar utilizador» |
| Admin → Templates | abrir uma template na lista; criar, guardar, eliminar | `#dmtAvisos`, `#dmtLista`, `#dmtDetalhe` | a template clicada; o nome da template criada (`autofocus`); o «Guardar template»; depois de eliminar, o nome do «Criar template» |
| Admin → Utilizadores → ficha (beta.219, deep sweep, 18/10/2026) | escolher a template; Arquivar ou Ativar; Repor palavra-passe | `#dfuAvisos` (o PIN temporário de um repor vai para o canto como `aviso`; as recusas como `erro`), `#dfuFactos`, `#dfuTemplate`, `#dfuArquivar`; as ações ficam (`data-dmo-fica`: as «Opções» abertas ficam abertas). O «Eliminar utilizador» abre a janela da casa (`dialog.dmo-dialog`, deep sweep de 18/10/2026, regra 118), sem pedido; o «Eliminar mesmo» da janela vai pela fila e a lista abre inteira | o dropdown da template; o «Arquivar»/«Ativar»; o «Repor palavra-passe»; o «Eliminar mesmo» |

No Admin, os botões dos cartões (o «Guardar» do Setup, o «Criar utilizador», o
«Criar template» e o «Guardar template») passaram para o canto superior direito
do cartão (`.dmo-card-topo`, ver «Botões» em `componentes-partilhados.md`), com
`form="id"`; continuam a gravar sem recarregar e com o foco no mesmo sítio.

Ficam como estavam: a pasta dos PDFs (só no browser) e o nome de um reparador
(já gravava no sítio). Prova no browser:
`tests/visual/definicoes_parcial_browser.py` (1 POST por gravação, também com
duplo clique, nenhuma navegação, o resto no mesmo sítio, o foco e os dados).

A ficha do utilizador ficou para a beta.219 (deep sweep, 18/10/2026; antes, a template,
arquivar, repor e eliminar voltavam à lista, outra página). Os formulários
levam `origem=ficha` e os handlers do Index voltam à ficha (a recusa no
endereço, o PIN por `TempData`, mostrado uma vez); sem a origem, voltam à lista
como antes. «Eliminar mesmo» apaga e volta à lista (outra página, que abre
inteira): o utilizador deixa de existir. Vai pela fila (beta.220, B-admin-3):
cliques repetidos fazem um só pedido. Medido no browser com rede lenta: 1 POST e 1 GET
por ação, nenhuma navegação, as «Opções» abertas e o foco no botão usado.

## Etapa E8: Revisão, aprovar e não aprovar

«Aprovar» (ou «Reaprovar») e «Não aprovar» gravam sem recarregar. O formulário
da decisão (`#revisaoDecisao`) deixou de ter `data-dmo-nativo` e tem
`data-dmo-parcial`; os botões continuam no canto superior direito do cartão
(agora com as classes da casa, `.dmo-card-topo` e `.dmo-card-topo-acoes`) e
enviam-no com `form="revisaoDecisao"` (o «Não aprovar» com `formaction`). O
formulário leva o dia (ou `sem_data`), a procura e o estado da lista em campos
escondidos, e o servidor devolve-os no redirect: fora das zonas a página volta
igual e trocam só as que mudaram:

| Zona | O que muda |
|---|---|
| `#revisaoDetalhe` | o selo, «Aprovado/Não aprovado por …», a nota gravada, o botão «Reaprovar», a nota em branco |
| `#revisaoResultados` | o selo do controlo na lista; com o filtro «Pendente», o controlo decidido sai da lista (fica aberto ao lado) |
| `#dmoCalendario` | o ponto do dia, quando era o último por aprovar |
| `#revisaoSemData` (nova, `dmo-zona`) | a contagem «Registos sem data por aprovar» |

O foco fica no botão carregado (`#revisaoAprovar`, `#revisaoNaoAprovar`). Um
duplo clique faz um POST só (a guarda do componente) e o servidor também não
repete nada: o controlo é um só e a decisão é a última. «Não aprovar» sem nota
continua a pedir a nota no browser, sem enviar. A recusa (sem identidade, sem
nota) vem para a pilha do canto. Numa coluna (lista por cima do controlo), o
filtro pode tirar da lista o controlo decidido; o script da página desliza o
mesmo que o controlo subiu, para ele ficar no mesmo sítio do ecrã (e, enquanto
a decisão grava, desliga o ajuste automático do browser, `overflow-anchor`, para
não descontar duas vezes). O copiar dos links do mcaliper passou a ligar-se por
delegação: antes, um controlo com links obrigava a abrir a página inteira.

O GET depois de decidir é a página inteira (sem `data-dmo-zonas`): medido no
PostgreSQL local, 20 a 21 consultas, as mesmas que as zonas precisariam (o
detalhe, a lista, o calendário e a contagem; o cabeçalho não lê nada), e só
pouparia uns 28 % dos bytes (~16 KB em vez de ~22 KB). Prova no browser:
`tests/visual/revisao_aprovar_browser.py` (1 POST também com duplo clique,
nenhuma navegação, o resto no mesmo sítio, o foco, o estado no ecrã e na base,
o copiar no cartão novo, a 1400 e 390 px).

## Onde aplicar

Levantamento de 17/10/2026 (beta.165). As frequências são estimativas pelo uso na fábrica, não medições. Os números de linha são do `.cshtml` de cada página nessa versão.

### 1. Já dá com o componente atual (só GET, só marcar o HTML)

| Página | Clique | Zona que muda | Notas |
|---|---|---|---|
| Ferramentas | páginas da lista (`Ferramentas/Index` L289–292) | `#ferrResultados` | a procura já troca o bloco (`data-dmo-ao-escrever`); falta pôr as setas da paginação a passar pela troca |
| Boquilhas → Ficheiros | páginas do catálogo (`_BqToolCatalog` L120–122) | `#bq-tool-results` | idem |
| Boquilhas → Registo | páginas dos movimentos (`mov_pagina`, L402), dos candidatos (L114) e do histórico de produções (L525); «Onde foi usada» (L132) | cada tabela com a sua paginação | cada tabela + paginação numa zona |
| Admin → Utilizadores | páginas da lista (L196) | a tabela | pouco usado |
| Job On → Histórico | filtros (`data-dmo-autosubmit`, L652–680) e páginas (L725) | avisos, resultados e paginação; o dropdown das produções | feito: páginas na beta.179, filtros na beta.219 (deep sweep, 18/10/2026). O formulário fica (`data-dmo-fica`); o dropdown das produções, que muda com a referência e a máquina, é a zona `#jobonHistProducaoZona` dentro dele (existe sempre, escondida sem produções). A referência aplica-se ao escrever (`data-dmo-ao-escrever`, 350 ms depois da última tecla, `replaceState`; deep sweep 2, F-10) e a produção escolhida para a referência anterior sai (`data-dmo-depende`) |
| Peso → Histórico | filtros (L92–127) e páginas (L193) | a tabela, o dropdown das produções e o detalhe da comparação | feito: páginas na beta.179, filtros na beta.219 (deep sweep, 18/10/2026). O formulário fica; trocam `#pesoHistoricoProducaoZona`, `#pesoHistoricoResultados` e `#pesoHistoricoDetalhe`. Com «Comparar Pesos guardados» aberto (`peso_1`/`peso_2`), um filtro abre a página inteira e fecha a comparação, como antes. A referência e a ferramenta aplicam-se ao escrever (deep sweep 2, F-10), menos com a comparação aberta: aí só com o Enter ou a sugestão, para a página não abrir inteira a meio da palavra (os campos vivem numa função Razor e o formulário tem as duas formas, porque o Razor escreve sempre os `data-*`, também com `null`) |
| Boquilhas → Histórico | filtros (`#bqHistoryFilters`) | a contagem, os totais, a lista, o dia e o calendário | feito na beta.219 (deep sweep, 18/10/2026). Os campos ficam (`#bqHistFiltros`, `data-dmo-fica`); dentro deles trocam o mês e o dia escondidos (`#bqHistContexto`), a contagem, «Limpar filtros» e o PDF (`#bqHistAcoes`, com `data-dmo-completa`: abrem como sempre), a dica da vista e as datas (`#bqHistDesdeZona`, `#bqHistAteZona`: o calendário, o «Hoje» e o «Todos os dias» limpam-nas). O cartão do dia é a zona `#bqHistDiaCartao`, que existe também na vista diária. A pesquisa e o reparador aplicam-se ao escrever (`data-dmo-ao-escrever="bqHistLista"`; deep sweep 2, F-10) |
| Boquilhas → Registo | a procura do «Associar Job On» (a referência) | `#bqRegistoAberto` | feito na beta.219 (deep sweep, 18/10/2026): o formulário GET tem `data-dmo-parcial`; aparecem as produções da referência e o foco volta à referência (`#jobRef`) |
| Controlo → PU NNPB | a procura e as recusas do «Associar a uma produção» (registo sem Job On) | `#puAssociar` (a recusa de uma produção que já tem registo vem em `#puFolha`) | feito na beta.219 (deep sweep, 18/10/2026): a zona só existe no registo sem Job On; a recusa volta com a referência procurada (`procura`); associado, abre-se a folha da produção inteira |
| Controlo → Pegamentos, Peso («Novo Peso a partir de Job On») e PU NNPB | a referência e a máquina da escolha do Job On (deep sweep 2, 18/10/2026, F-8) | a produção (`#pegJobOnProducaoZona`, `#pesoJobOnProducaoZona`, `#puProducaoZona`), a mensagem «Não há Job Ons…» (`#pegJobOnVazia`, `#pesoJobOnVazia`, `#puEscolhaResultados`) e as recusas (`#pegEscolhaAvisos`, `#pesoJobOnAvisos`, `#puEscolhaAvisos`) | feito: a procura fica (`data-dmo-fica` e `data-dmo-parcial`); a referência, a máquina e a produção ficam numa linha (`dmo-filter-bar-conteudo`; o PU NNPB não tem máquina e a produção fica ao lado da referência, na barra `pu-id-form`). O dropdown da produção é do formulário POST de baixo (`form=`), que abre a folha da produção (outro `controlo_id`) como sempre (`data-dmo-nativo`); o mesmo no «Associar Job On» dos Pegamentos sem Job On (`#pegJobOnAssociarZona`, o botão na mesma linha; `#pegAssociar` fica, por isso as zonas estão dentro dele). No PU NNPB, o «+ Novo registo» (`#puNovoAcoes`) depende da referência procurada e é uma zona; sem Job On, a tabela dos registos e o «Criar» também (`#puEscolhaResultados`) |
| Controlo → Peso → Comparação | a referência, a produção e o Peso inicial (deep sweep 2, 18/10/2026, F-8) | `#pesoComparacaoAvisos`, `#pesoComparacaoProducaoZona`, `#pesoComparacaoInicialZona` e `#pesoComparacaoContexto` (a base e o resultado) | feito: o formulário (`#cmpEscolha`) fica e vai pelo DmoTroca; os dropdowns passaram de `data-auto-submit` (`form.submit()`, a página inteira) para `data-dmo-autosubmit`. O foco volta ao filtro do combo da produção (`#cmpProducao-filtro`) pelo `dmo:troca` da página. O dropdown da produção traz as das referências que começam pelo texto, ou cuja parte BQ começa por ele (beta.237), nunca por um pedaço do meio |

Os filtros que aplicam ao escolher (`data-dmo-autosubmit`, e as sugestões com `data-dmo-suggest-submit`) usam `requestSubmit`; o componente trata o `submit` de um GET dentro de `data-dmo-parcial` como `ir(endereço do formulário)`: um pedido, `pushState` com os filtros (F5 e Voltar dão a mesma página). Nos Históricos (deep sweep, 18/10/2026), o formulário dos filtros fica (`data-dmo-fica` e `data-dmo-parcial` à volta dele) e o que o servidor muda dentro dele são zonas: o foco fica no campo, e um filtro escolhido com o pedido anterior a caminho vai no pedido seguinte (antes, a página que chegava apagava-o: B-peso-5). Para isso, fora das zonas a página tem de ser igual com todos os filtros (os testes `…_Filtros_TrocamSo…`/`History_Filters_ChangeOnlyTheZones` comparam com `PesoZonas.ForaDasZonas`).

### 2. Precisa de gravar sem recarregar (o mais usado na fábrica)

Antes da beta.167 a troca só fazia GET. Os registos que o operador faz em série são POST que voltam à mesma página (PRG) e recarregam tudo a cada linha:

| Página | Ação | O que muda | O que impede hoje |
|---|---|---|---|
| Controlo → PU NNPB | acrescentar, marcar visto, retirar punção (POST L260/300/313) | os três contadores (L245) e a tabela (L274–324) | um formulário POST por linha; o script `data-pu-mirror` (L388); mensagens |
| Controlo → Pegamentos | Enter numa leitura nova (POST L406); editar e apagar valor | a tabela e a média desse componente, o mapa (L432) | formulários POST em cada cartão; `pegamentos-registo.js` ligado uma vez no arranque (L458); mensagens |
| Boquilhas → Registo | guardar movimento (POST L252) | os números (L307), os últimos movimentos e a paginação (L352–402), o formulário limpo | formulários POST (também o «Associar Job On», L204); mensagens dentro do cartão; a hora do formulário volta a «agora» |
| Controlo → Peso | registar leitura (POST L1318) | leituras (L1258), resumo, cartões guardados, comparação | `peso-live.js` ligado uma vez à secção inteira; formulários POST; scripts na página (L1636–1730) |
| Controlo → Resumo / Revisão | OK/NOK por área (L306), aprovar (Approve L265) | um cartão de área; o painel de detalhe | janelas (`dialog`), formulários que aplicam ao escolher, «Visto por» muda a cada pedido |

O que o componente precisava de ganhar para estes, uma vez, para todas (feito na beta.167, com o PU NNPB):

1. **Gravar dentro de `data-dmo-parcial`:** o `submit` de um formulário POST envia com `fetch` (o mesmo corpo, com o token), segue o redirect do servidor e, se voltou à mesma página, troca as zonas como num clique. Uma recusa que volta com a mensagem fica na zona; se a resposta não for a mesma página, navega para ela.
2. **Mensagens nas zonas:** `dmo-live.js` passa a juntar à pilha as `[data-dmo-toast]` das zonas novas (uma função que recebe a zona), e `[data-dmo-toast]` sai da lista do que obriga a abrir a página inteira.
3. **Formulários e scripts nas zonas:** os scripts das páginas (`pegamentos-registo.js`, `peso-live.js`, o `data-pu-mirror`) passam a ligar por delegação no `document`, ou a ouvir `dmo:troca` e ligar só as zonas novas. Só depois `form[method=post]` e `script` saem da lista.
4. **Foco:** depois de gravar, o foco volta ao campo onde o operador estava (por exemplo a próxima leitura), para continuar a escrever sem rato.

### 3. Precisa do calendário sem recarregar

Mudar de dia no calendário (Job On, Boquilhas → Histórico, Revisão) muda o calendário, que hoje está fora das zonas e liga o duplo clique dia a dia (`data-dmo-calendar-unselect`). Com a ligação do calendário delegada, o calendário passa a ser uma zona e o clique num dia troca só o calendário e a lista do dia.

### Entregas

O Diogo confirmou a ordem (17/10/2026) e pediu que o trabalho fosse partido em entregas, cada uma desenhada, construída e verificada com o critério acima, num PR próprio:

| Entrega | O quê | Estado |
|---|---|---|
| E1 | Base comum (gravar sem recarregar) + PU NNPB | beta.167 |
| E2 | Controlo → Pegamentos: Enter numa leitura, editar, apagar | beta.169 |
| E3 | Boquilhas → Registo: escolher boquilha, guardar, corrigir e apagar movimento | beta.168 |
| E4 | Controlo → Peso: leituras, comparação, produção anterior | beta.170 |
| E5 | Paginações e filtros (secção 1) | em curso (Codex): beta.178 (Ferramentas, Boquilhas → Ficheiros), beta.179 (Utilizadores, Históricos do Job On e do Controlo, produções da referência na ficha do Job On), beta.181 (procura do Resumo, servida por bloco), beta.219 (deep sweep, 18/10/2026): os filtros dos Históricos do Job On, das Boquilhas e do Controlo, a procura do «Associar Job On» das Boquilhas e a procura e as recusas do «Associar a uma produção» do PU NNPB |
| E6 | Calendário (secção 3) | beta.180 (Codex: Job On e Revisão; nas Boquilhas → Histórico o calendário continua a abrir a página inteira) |
| E7 | Controlo → Resumo: OK/NOK, comentário, link mcaliper | beta.171 |
| E8 | Controlo → Revisão: escolher dia e produção, aprovar | feito: beta.180 (escolher dia e produção sem recarregar, Codex), beta.184 (filtros pelo servidor, com a nota em edição, Codex); aprovar e não aprovar gravam sem recarregar (ver «Etapa E8», acima) |
| E9 | Ferramentas → Ficha: dados gerais e técnicos | beta.172 |
| E10 | Definições dos módulos e Admin (baixa prioridade; os reparadores das Boquilhas já gravam sem recarregar) | feito (ver «Etapa E10», abaixo) |
| E11 | O servidor desenhar só a zona, nas páginas que o DEV mostrar mais lentas | em curso (Codex, medido no PostgreSQL local): beta.181 (procura do Resumo), beta.182 (Histórico do Job On), beta.183 (Ferramentas), beta.184 (filtros da Revisão); beta.189 (depois de gravar, só as zonas no Resumo e nos Pegamentos; o Job On grava com envio nativo; a aprovação da Revisão grava pelo componente desde a E8, com a página inteira no GET, e o Peso, o PU NNPB, as Boquilhas → Registo e a Ficha usam quase tudo o que leem) |

Início, Versão, login e mudança de palavra-passe ganham pouco e ficam como estão; `/Planeamento` redireciona para o Job On.

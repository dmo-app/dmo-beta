(function () {
  const SELECTED = "selected";
  // A fotografia da página tal como o servidor a desenhou, antes de os
  // scripts lhe mexerem (ver «Atualizar só o que muda», mais abaixo).
  const trocaRaiz = document.querySelector("[data-dmo-troca]");
  let trocaFoto = trocaRaiz ? trocaRaiz.cloneNode(true) : null;

  function rows(list) {
    return list.querySelectorAll("[data-dmo-row]");
  }

  function selectRow(list, row) {
    rows(list).forEach((item) => {
      item.classList.toggle(SELECTED, item === row);
      item.setAttribute("aria-selected", item === row ? "true" : "false");
    });
    // O rádio é parte do contrato Razor (model binding e event listeners). A
    // seleção de uma linha tem de atualizar o input real e avisar os
    // consumidores de change. Sem isto a UI divergia do valor submetido.
    const radio = row.querySelector('input[type="radio"]');
    if (radio && !radio.checked) {
      radio.checked = true;
      radio.dispatchEvent(new Event("change", { bubbles: true }));
    }
    list.dispatchEvent(new CustomEvent("dmo:list-select", {
      bubbles: true,
      detail: { id: row.dataset.id || null, row }
    }));
  }

  function unselectRow(list, row) {
    row.classList.remove(SELECTED);
    row.setAttribute("aria-selected", "false");
    const radio = row.querySelector('input[type="radio"]');
    if (radio && radio.checked) {
      radio.checked = false;
      radio.dispatchEvent(new Event("change", { bubbles: true }));
    }
  }

  function openRow(list, row) {
    list.dispatchEvent(new CustomEvent("dmo:list-open", {
      bubbles: true,
      detail: { id: row.dataset.id || null, row }
    }));
  }

  // A lista em que um clique só seleciona (a escolha serve outra ação).
  function selectsOnly(list) {
    return list.hasAttribute("data-dmo-seleciona")
      || (list.id && document.querySelector('[data-dmo-for="' + list.id + '"]') !== null);
  }

  // Uma lista ligada uma só vez. As páginas que trazem uma lista depois de
  // carregadas (a procura ao escrever das Boquilhas) chamam DmoLists.bind.
  function bindList(list) {
    if (list.dataset.dmoBound) return;
    list.dataset.dmoBound = "1";
    // Preserve table semantics: <table>/<tr> must not become listbox/option.
    // Sidepanels e cartões mantêm a semântica de seleção de lista.
    const isTable = list.tagName === "TABLE";
    if (!isTable) list.setAttribute("role", "listbox");
    rows(list).forEach((row) => {
      if (!isTable && row.tagName !== "BUTTON") row.setAttribute("role", "option");
      row.tabIndex = 0;
      // UM CLIQUE ABRE (Diogo, 17/10/2026: «vamos fazer 1 click, não há
      // necessidade para dois»): numa linha com destino (data-dmo-open), o
      // clique seleciona e abre logo. Ficam a selecionar com um clique, e a
      // abrir com duplo clique, só as listas em que a escolha serve outra
      // ação: as marcadas data-dmo-seleciona e as que têm um bloco de ações
      // fora (data-dmo-for, o «Selecionar ferramenta»). A lista do dia do Job
      // On abre com um clique desde 18/10/2026 (data-dmo-fechar, abaixo).
      // O data-dmo-abrir-clique da procura do Resumo deixa de ser preciso.
      row.addEventListener("click", (event) => {
        // Um controlo próprio dentro da linha faz o seu trabalho.
        const control = event.target.closest("button,select,textarea,a");
        if (control && control !== row) return;
        // A lista de uma procura que espera pela resposta ao texto novo é a
        // do texto anterior: não escolhe nem abre (ver procuraEspera).
        if (row.closest("[data-dmo-procura-espera]")) return;
        // CLICAR DE NOVO TIRA A SELEÇÃO (Diogo, 18/10/2026, como no
        // calendário): numa linha com data-dmo-fechar, o clique na linha já
        // escolhida tira-a e vai ao destino sem ela (o Job On fecha a ficha).
        if (row.dataset.dmoFechar && row.classList.contains(SELECTED)) {
          unselectRow(list, row);
          ir(row.dataset.dmoFechar, row);
          return;
        }
        selectRow(list, row);
        if (!selectsOnly(list) && row.dataset.dmoOpen) openRow(list, row);
      });
      row.addEventListener("dblclick", () => {
        if (selectsOnly(list) && !row.closest("[data-dmo-procura-espera]")) openRow(list, row);
      });
      // O ENTER ABRE (regra 2e do contrato da casa). Estava ao contrário: o Enter
      // selecionava e SÓ Ctrl+Enter abria, que não é comportamento de ninguém.
      // Selecionar antes de abrir não é trabalho repetido — é o que diz ao
      // operador qual linha vai abrir, e é o que deixa o ecrã honesto se ele
      // desistir a meio.
      row.addEventListener("keydown", (event) => {
        // O ESPAÇO ESCOLHE (Diogo, 27/10/2026: sem botões nas linhas, a
        // linha escolhe-se e a ação é o botão do canto): só seleciona, não abre.
        if (event.key === " " && event.target === row && row.tagName !== "BUTTON") {
          if (row.closest("[data-dmo-procura-espera]")) return;
          event.preventDefault();
          selectRow(list, row);
          return;
        }
        if (event.key !== "Enter") return;
        // Não interceptar Enter de um controlo nativo dentro da linha.
        if (event.target !== row && event.target.closest("input,button,select,textarea,a")) return;
        if (row.closest("[data-dmo-procura-espera]")) { event.preventDefault(); return; }
        // Em <button> a tecla Enter dispara o click nativo; sem cortar isto a
        // linha selecionar-se-ia duas vezes (aqui e no handler do click).
        event.preventDefault();
        selectRow(list, row);
        openRow(list, row);
      });
    });
  }

  document.querySelectorAll("[data-dmo-list]").forEach(bindList);
  window.DmoLists = { bind: bindList };

  // ATUALIZAR SÓ O QUE MUDA (Diogo, 17/10/2026: «não existe forma de fazer
  // lock nos elementos e apenas refresh o que muda?»). A página marca a raiz
  // com data-dmo-troca e, dentro dela, as zonas que mudam (data-dmo-regiao,
  // com id) e as que ficam como o operador as deixou (data-dmo-fica, por
  // exemplo a procura). Um destino aberto a partir de um elemento dentro de
  // data-dmo-parcial (uma linha, um dropdown data-dmo-ir, um link) é pedido
  // ao servidor e só as zonas que mudaram são trocadas; o resto da página não
  // se mexe e o endereço muda como numa navegação. Um formulário POST dentro
  // de data-dmo-parcial grava da mesma forma: o servidor grava e volta à
  // página (PRG) e só as zonas trocam. Cada envio leva uma chave
  // (X-Dmo-Envio, ver semResposta): num handler com [EnvioUnico] o servidor
  // grava-a uma só vez. Se mudar alguma coisa fora das zonas,
  // ou se uma zona trouxer um elemento que precise dos scripts da página
  // (data-dmo-script, ou os da lista abaixo), abre-se a página inteira.
  // As janelas da casa (dialog.dmo-dialog, data-dmo-dialog) e os dropdowns
  // que aplicam ao escolher (data-dmo-autosubmit) ligam-se por delegação e
  // podem vir numa zona; a janela que abre com a página
  // (data-dmo-dialog-aberta) e uma janela de outro tipo não. As sugestões ao
  // escrever (data-dmo-suggest) também não estão na lista: ligam-se nas zonas
  // novas com DMOSuggest.init (dmo-suggest.js), como no arranque. Os valores
  // editados no sítio (data-dmo-valor) ligam-se por delegação (E10). Os filtros
  // que procuram ao escrever (data-dmo-ao-escrever) ligam-se com o dmo:troca
  // e o dia escolhido do calendário (data-dmo-calendar-unselect) por
  // delegação; também podem vir numa zona (o calendário do Job On e da
  // Revisão é uma zona). O copiar de um link (data-dmo-copiar) também se
  // liga por delegação (E8).
  // Guia: docs/ui-reference/atualizar-so-o-que-muda.md.
  const TROCA_PRECISA_PAGINA = "[data-dmo-script], script, dialog:not(.dmo-dialog), dialog[data-dmo-dialog-aberta],"
    + " [data-dmo-procura]";
  const RECUSA = "[data-dmo-regiao] [data-dmo-toast=\"erro\"]";
  const TOKEN = "__RequestVerificationToken";
  // Os campos que o operador escreve num formulário (texto, número, textarea,
  // select): é o que se repõe num formulário que espera na fila (ver repor).
  const ESCRITOS = "input:not([type=hidden]):not([type=checkbox]):not([type=radio]):not([type=submit]):not([type=button])"
    + ":not([type=reset]):not([type=image]):not([type=file]), textarea, select";
  // Ao fim de tempos.aviso (ms) com envios na fila, aparece no canto que a
  // gravação espera (ver esperarFila). Sem ligação, a confirmação volta a
  // tentar ao fim de tempos.confirmar (ms), se o evento online não vier antes
  // (ver ligacaoDeVolta). Os testes encurtam-nos.
  const tempos = { aviso: 3000, confirmar: 5000 };
  // Cada clique (ir) ou envio (gravar) conta uma vez: a resposta de um pedido
  // só puxa o foco se o operador não fez outra coisa depois dele.
  let operacoes = 0;
  // O clique mais recente: a resposta de um clique antigo não se aplica.
  let irPedido = 0;
  // Os pedidos em curso (aria-busy na raiz).
  let ocupados = 0;
  // O endereço da página sem uma recusa (o último que o servidor desenhou sem
  // [data-dmo-toast="erro"] nas zonas): é o que confirmar pede. null quando a
  // página abriu já com uma recusa.
  let limpo = null;
  if (trocaFoto && !trocaFoto.querySelector(RECUSA)) {
    const aqui = new URL(window.location.href);
    aqui.hash = "";
    limpo = aqui.href;
  }
  // Os envios na fila, por ordem (ver gravar): o que respondeu sai quando
  // acaba. Cada um guarda o formulário (o elemento que está no ecrã), a sua
  // identidade, o corpo e o que o operador escreveu.
  const envios = [];
  let enviados = 0;
  let fila = Promise.resolve();
  let saindo = false;
  // UMA CHAVE POR ENVIO (deep sweep, 18/10/2026, B-boquilhas-2): cada envio
  // leva uma chave no cabeçalho X-Dmo-Envio e o servidor grava uma só vez
  // cada chave (num handler com [EnvioUnico]; a um envio repetido responde
  // como ao primeiro). Um envio que ficou sem resposta (a ligação falhou)
  // deixa a sua chave no formulário: enquanto o formulário mostra o mesmo
  // envio (o mesmo elemento, o mesmo corpo), carregar outra vez no botão leva
  // a mesma chave e não grava duas vezes. A resposta do servidor a essa chave
  // tira-a; um formulário novo (a zona trocou) começa sem nenhuma, e o
  // operador escrever no formulário também a tira: o que enviar a seguir é
  // um envio novo, mesmo que fique igual (no Peso, a mesma leitura escrita
  // outra vez depois de a confirmação a mostrar gravada).
  const semResposta = new WeakMap();
  ["input", "change"].forEach((tipo) => document.addEventListener(tipo, (event) => {
    const form = event.target && event.target.form;
    if (form) semResposta.delete(form);
  }, true));

  function novaChave() {
    const bytes = new Uint8Array(16);
    if (window.crypto && window.crypto.getRandomValues) window.crypto.getRandomValues(bytes);
    else bytes.forEach((_, i) => { bytes[i] = Math.floor(Math.random() * 256); });
    return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
  }

  // O que fica fora das zonas, para comparar a página atual com a nova. O
  // token antifalsificação muda a cada pedido e não conta.
  // inteiro: com o próprio elemento (os atributos de uma zona, por exemplo
  // hidden, também contam).
  function semTokens(copia, inteiro = false) {
    copia.querySelectorAll("input[name=__RequestVerificationToken]").forEach((token) => token.removeAttribute("value"));
    return inteiro ? copia.outerHTML : copia.innerHTML;
  }

  // Cada zona fica reduzida a um marcador com o id: os atributos dela (por
  // exemplo hidden) mudam com a zona, não contam como mudança fora.
  function trocaFora(raiz, inteiro = false) {
    const copia = raiz.cloneNode(true);
    copia.querySelectorAll("[data-dmo-fica]").forEach((zona) => zona.replaceChildren());
    copia.querySelectorAll("[data-dmo-regiao]").forEach((zona) => {
      const marcador = document.createElement("div");
      marcador.id = zona.id;
      zona.replaceWith(marcador);
    });
    return semTokens(copia, inteiro);
  }

  // Uma zona sem as zonas que tem dentro (cada uma reduzida a um marcador com
  // o id): o que é só dela.
  function semInteriores(zona) {
    const copia = zona.cloneNode(true);
    copia.querySelectorAll("[data-dmo-regiao]").forEach((dentro) => {
      const marcador = document.createElement("div");
      marcador.id = dentro.id;
      dentro.replaceWith(marcador);
    });
    return copia;
  }

  function mensagens(raiz) {
    if (window.DMOLive && window.DMOLive.mostrarMensagens) window.DMOLive.mostrarMensagens(raiz);
  }

  // As mensagens de uma zona que não se troca, para o canto.
  function mensagensDe(zona, origem) {
    const caixa = document.createElement("div");
    zona.querySelectorAll("[data-dmo-toast]").forEach((mensagem) => {
      const copia = document.importNode(mensagem, true);
      if (origem !== undefined) copia.setAttribute("data-dmo-origem", origem);
      caixa.appendChild(copia);
    });
    mensagens(caixa);
  }

  // As mensagens que estão no canto e são da zona «origem» (de um envio
  // anterior desse cartão) ou da página (as que vieram com ela e o aviso de
  // ligação): a resposta a um pedido de um formulário retira-as. Uma
  // mensagem com data-dmo-toast-fica fica até se fechar: o PIN temporário
  // de um utilizador criado ou reposto, mostrado uma vez, não pode sair com
  // a criação do utilizador seguinte nem com uma recusa a seguir (deep
  // sweep, 18/10/2026, B-admin-1).
  function mensagensVistas(origem) {
    return Array.from(document.querySelectorAll(".dmo-toast-pilha [data-dmo-toast]:not([data-dmo-toast-fica])"))
      .filter((mensagem) => !mensagem.hasAttribute("data-dmo-origem") || mensagem.getAttribute("data-dmo-origem") === origem);
  }

  // O corpo de um envio em texto, sem o token antifalsificação (muda a cada
  // pedido): é o que distingue dois envios do mesmo formulário.
  function semToken(pares) {
    return Array.from(pares).filter(([nome]) => nome !== TOKEN)
      .map(([nome, valor]) => encodeURIComponent(nome) + "=" + encodeURIComponent(typeof valor === "string" ? valor : `${valor && valor.name}:${valor && valor.size}`))
      .join("&");
  }

  // A identidade de um formulário: o id; sem id, a ação e os campos
  // escondidos (sem o token). Os campos que o operador escreve não contam:
  // o servidor devolve-os vazios, e o formulário continua a ser o mesmo.
  function identidade(form) {
    if (form.id) return "#" + form.id;
    const escondidos = Array.from(form.elements).filter((campo) => campo.type === "hidden" && campo.name)
      .map((campo) => [campo.name, campo.value]);
    return `${form.action} ${semToken(escondidos)}`;
  }

  // O elemento que está agora no ecrã para o formulário de um envio: o mesmo
  // se a zona dele não trocou; senão o novo com o mesmo id, ou (sem id) o
  // com a mesma ação e os mesmos campos escondidos (ou o único com essa ação).
  function encontrar(envio) {
    if (envio.form.isConnected) return envio.form;
    if (envio.id) {
      const novo = document.getElementById(envio.id);
      return novo instanceof HTMLFormElement ? novo : null;
    }
    const iguais = Array.from(trocaRaiz.querySelectorAll("form"))
      .filter((form) => form.action === envio.acao && (form.getAttribute("method") || "").toLowerCase() === "post");
    return iguais.find((form) => identidade(form) === envio.identidade) || (iguais.length === 1 ? iguais[0] : null);
  }

  // Um formulário que espera na fila, num elemento novo (a zona dele trocou
  // com a resposta de outro envio): os campos que o operador escreve mostram
  // o que foi enviado, e o formulário fica marcado à espera (aria-busy e
  // data-dmo-a-gravar, com um estilo que não mexe no layout).
  function repor(envio, form) {
    const usados = new Map();
    Array.from(form.elements).filter((campo) => campo.name && campo.matches(ESCRITOS)).forEach((campo) => {
      const valores = envio.escrito.filter(([nome]) => nome === campo.name).map(([, valor]) => valor);
      if (campo.multiple) {
        Array.from(campo.options).forEach((opcao) => { opcao.selected = valores.includes(opcao.value); });
        return;
      }
      const i = usados.get(campo.name) || 0;
      usados.set(campo.name, i + 1);
      if (i < valores.length && typeof valores[i] === "string") campo.value = valores[i];
    });
    form.setAttribute("aria-busy", "true");
    marcados(form, envio).forEach((elemento) => elemento.setAttribute("data-dmo-a-gravar", ""));
  }

  // O formulário, os campos que o operador escreve e o botão que o enviou.
  function marcados(form, envio) {
    const campos = Array.from(form.elements);
    const botao = envio.botao;
    const botoes = campos.filter((campo) => campo.matches("button, input[type=submit], input[type=image]")
      && (botao && botao.name ? campo.name === botao.name && campo.value === botao.value : form.contains(campo)));
    return [form, ...campos.filter((campo) => campo.matches(ESCRITOS)), ...botoes];
  }

  function desmarcar(form) {
    form.removeAttribute("aria-busy");
    [form, ...Array.from(form.elements)].forEach((elemento) => elemento.removeAttribute("data-dmo-a-gravar"));
  }

  // Os campos que o operador escreve numa janela aberta (ver trocarRaiz): o
  // nome, o valor e, no campo com o foco, a seleção do texto.
  function escritosDe(raiz, ativo) {
    return Array.from(raiz.querySelectorAll(ESCRITOS)).filter((campo) => campo.name).map((campo) => ({
      nome: campo.name,
      valor: campo.value,
      ativo: campo === ativo,
      selecao: campo === ativo && typeof campo.selectionStart === "number"
        ? [campo.selectionStart, campo.selectionEnd, campo.selectionDirection || "none"] : null,
    }));
  }

  // A mesma janela (pelo id) na zona nova, aberta outra vez com o que estava
  // escrito (os campos de nome repetido pela ordem). Devolve o campo que fica
  // com o foco (o que o tinha, senão o primeiro) e a seleção; null se a zona
  // nova não traz a janela.
  function reabrir(aberta, node) {
    const dialog = Array.from(node.querySelectorAll("dialog.dmo-dialog[id]")).find((item) => item.id === aberta.id);
    if (!dialog || dialog.open || typeof dialog.showModal !== "function") return null;
    const usados = new Map();
    let campo = null;
    let selecao = null;
    Array.from(dialog.querySelectorAll(ESCRITOS)).filter((item) => item.name).forEach((item) => {
      const i = usados.get(item.name) || 0;
      usados.set(item.name, i + 1);
      const antes = aberta.campos.filter((escrito) => escrito.nome === item.name)[i];
      if (!antes) return;
      item.value = antes.valor;
      if (antes.ativo) { campo = item; selecao = antes.selecao; }
    });
    dialog.showModal();
    return { campo: campo || dialog.querySelector("textarea, input:not([type=hidden]), select"), selecao };
  }

  // Troca as zonas que mudaram. Opções:
  // - gravou: depois de um envio, o id da zona do formulário que gravou (null
  //   fora de uma zona). Essa zona e as que trazem mensagens trocam mesmo
  //   iguais às de antes (ver gravar); num clique (GET) só trocam as que mudaram.
  // - origem: as mensagens da resposta levam esta zona (data-dmo-origem).
  // - autofoco: false quando o operador já fez outra coisa a seguir (outro
  //   envio, um clique); o foco não salta para a zona desta resposta.
  // - mensagens: "repetir" num GET pedido por um formulário (as mensagens de
  //   uma zona igual à anterior mostram-se outra vez, sem a trocar); "nenhuma"
  //   no confirmar (as mensagens são do endereço pedido, não de um envio).
  // - envio: o envio a que esta resposta responde (os outros esperam na fila).
  // - pedido: o formulário enviado ou o elemento clicado (detail.origem do
  //   dmo:troca).
  // Cada resposta troca as zonas, também as de formulários que ainda esperam
  // na fila: o ecrã mostra sempre a resposta mais nova. Depois, cada
  // formulário que espera volta a mostrar o que foi enviado, marcado à
  // espera (ver repor); o foco fica no campo com o mesmo id, e um segundo
  // envio igual continua ignorado (ver gravar). Devolve false se a troca não
  // deu, ou { recusa } (a página nova traz uma recusa nas zonas).
  function trocar(html, opcoes = {}) {
    const novo = new DOMParser().parseFromString(html, "text/html");
    return trocarRaiz(novo, novo.querySelector("[data-dmo-troca]"), opcoes);
  }

  // O trocar a partir da raiz nova (novaRaiz, do documento novo). novas: só
  // numa resposta com as zonas de uma gravação (trocarZonas), as zonas que o
  // servidor desenhou; a recusa conta só nelas (as outras são a fotografia).
  function trocarRaiz(novo, novaRaiz, opcoes = {}, novas = null) {
    const { gravou, origem, autofoco = true, mensagens: modo = "", envio = null, pedido = null } = opcoes;
    if (!novaRaiz || trocaFora(novaRaiz) !== trocaFora(trocaFoto)) return false;
    const fotos = new Map(Array.from(trocaFoto.querySelectorAll("[data-dmo-regiao]"), (zona) => [zona.id, zona]));
    const mudam = [];
    const repetem = [];
    for (const zona of novaRaiz.querySelectorAll("[data-dmo-regiao]")) {
      // Uma zona dentro de outra que já troca vai com ela.
      if (mudam.some(([, fora]) => fora.contains(zona))) continue;
      const atual = zona.id ? document.getElementById(zona.id) : null;
      const antes = zona.id ? fotos.get(zona.id) : null;
      if (!atual || !antes) return false;
      // Uma zona com zonas dentro (a ficha do Job On com a tabela das
      // produções da referência): se o que é só dela não mudou, fica, e
      // trocam só as de dentro (paginar a tabela não troca o cartão nem os
      // campos dele). As mensagens contam só as que são dela.
      const interiores = zona.querySelector("[data-dmo-regiao]") !== null;
      const propria = interiores ? semInteriores(zona) : zona;
      const forcar = gravou !== undefined && (zona.id === gravou || propria.querySelector("[data-dmo-toast]"));
      if (!forcar && (semTokens(antes.cloneNode(true), true) === semTokens(zona.cloneNode(true), true)
        || interiores && trocaFora(antes, true) === trocaFora(zona, true))) {
        if (modo === "repetir" && propria.querySelector("[data-dmo-toast]")) repetem.push(propria);
        continue;
      }
      if (zona.matches(TROCA_PRECISA_PAGINA) || zona.querySelector(TROCA_PRECISA_PAGINA)) return false;
      mudam.push([atual, zona]);
    }
    // O foco fica onde estava: no campo com autofocus da zona nova (por
    // exemplo o número da próxima punção; o de uma janela fechada não conta)
    // ou no mesmo id de antes. Gravar numa janela que ia na zona fecha-a com
    // a troca: o foco volta ao botão que a abriu, como ao fechar a janela.
    // Num formulário que espera, um botão sem id (o visto do PU NNPB) passa
    // para o mesmo botão do elemento novo.
    const ativo = document.activeElement;
    const focado = ativo && ativo.id;
    const janela = ativo && ativo.closest ? ativo.closest("dialog[open][id]") : null;
    const ativoEspera = ativo && !focado && ativo.form ? envios.find((outro) => outro !== envio && outro.form === ativo.form) : null;
    let foco = null;
    // Uma janela aberta numa zona que troca, quando não foi nela que se
    // gravou (o comentário de uma área do Resumo, aberto enquanto o OK/NOK
    // da mesma área grava): volta a abrir na zona nova com o que o operador
    // escreveu, e o foco fica no mesmo campo, no mesmo ponto do texto. Antes
    // fechava-se e o texto perdia-se (deep sweep, 18/10/2026). Gravar na
    // própria janela fecha-a, como sempre.
    let reaberta = null;
    mudam.forEach(([atual, zona]) => {
      const abertas = Array.from(atual.querySelectorAll("dialog.dmo-dialog[open][id]"))
        .filter((dialog) => !(pedido && pedido.nodeType === 1 && dialog.contains(pedido)))
        .map((dialog) => ({ id: dialog.id, campos: escritosDe(dialog, ativo) }));
      const node = document.importNode(zona, true);
      if (modo === "nenhuma") node.querySelectorAll("[data-dmo-toast]").forEach((mensagem) => mensagem.remove());
      atual.replaceWith(node);
      if (node.matches("[data-dmo-list]")) bindList(node);
      node.querySelectorAll("[data-dmo-list]").forEach(bindList);
      if (origem !== undefined) node.querySelectorAll("[data-dmo-toast]").forEach((mensagem) => mensagem.setAttribute("data-dmo-origem", origem));
      if (window.DMOSuggest) window.DMOSuggest.init(node);
      mensagens(node);
      if (autofoco) foco = foco || Array.from(node.querySelectorAll("[autofocus]")).find((campo) => !campo.closest("dialog:not([open])"));
      abertas.forEach((aberta) => { reaberta = reabrir(aberta, node) || reaberta; });
    });
    // Com a janela aberta outra vez, o resto da página não recebe o foco.
    if (reaberta) foco = reaberta.campo;
    if (modo !== "nenhuma") repetem.forEach((zona) => mensagensDe(zona, origem));
    envios.filter((outro) => outro !== envio && !outro.form.isConnected).forEach((outro) => {
      const form = encontrar(outro);
      if (!form) return;
      outro.form = form;
      repor(outro, form);
    });
    if (!foco && janela && !janela.isConnected) {
      foco = document.querySelector(`[data-dmo-dialog="${janela.id}"]`);
    } else if (!foco && focado) {
      const mesmo = document.getElementById(focado);
      if (mesmo && mudam.some(([, zona]) => document.getElementById(zona.id).contains(mesmo))) foco = mesmo;
    }
    if (!foco && ativoEspera && !ativo.isConnected && ativoEspera.form.isConnected) {
      foco = Array.from(ativoEspera.form.elements).find((campo) => campo.tagName === ativo.tagName
        && campo.type === ativo.type && campo.name === ativo.name && campo.value === ativo.value) || null;
    }
    // Um combo de filtrar (dmo-suggest) abre a lista ao receber o foco: o foco
    // devolvido pela troca volta sem a abrir.
    if (foco && foco.classList.contains("dmo-suggest-combo") && window.DMOSuggest && window.DMOSuggest.focar) window.DMOSuggest.focar(foco);
    else if (foco) foco.focus({ preventScroll: true });
    if (reaberta && reaberta.selecao && foco === reaberta.campo) {
      try { foco.setSelectionRange(...reaberta.selecao); } catch { /* um campo sem seleção (number, select) */ }
    }
    trocaFoto = novaRaiz.cloneNode(true);
    if (novo.title) document.title = novo.title;
    const recusa = novas ? novas.some((zona) => zona.querySelector("[data-dmo-toast=\"erro\"]") !== null)
      : novaRaiz.querySelector(RECUSA) !== null;
    // detail.ativo: o elemento que tinha o foco antes da troca (se estava
    // numa zona trocada, já não está na página). detail.pagina: o documento
    // novo inteiro (o Job On lê dele o contexto da procura, que fica).
    document.dispatchEvent(new CustomEvent("dmo:troca", { detail: { zonas: mudam.map(([, zona]) => zona.id), origem: pedido, envio: envio ? envio.vez : null, recusa, ativo, pagina: novo } }));
    return { recusa };
  }

  // O SERVIDOR DESENHA SÓ A ZONA (contrato explícito, Codex): um link ou um
  // formulário GET dentro de [data-dmo-handler][data-dmo-alvo] pede o mesmo
  // endereço com handler=<data-dmo-handler> (só no fetch; o endereço do
  // browser fica o normal). A resposta tem de ser uma única raiz com o id do
  // alvo e data-dmo-regiao, com o cabeçalho X-Dmo-Region igual ao alvo, e
  // sem nada que precise dos scripts da página; senão, abre-se a página
  // inteira (o GET normal). A fotografia da zona é atualizada, para as
  // trocas seguintes da página compararem com o que está no ecrã. O resto
  // segue o trocar: mensagens no canto, sugestões ligadas, foco no
  // autofocus ou no mesmo id, dmo:troca com o mesmo detail. Devolve false
  // se a resposta não cumpre o contrato, ou { recusa }.
  function trocarBloco(html, id, opcoes = {}) {
    const { origem, autofoco = true, mensagens: modo = "", pedido = null } = opcoes;
    const novo = new DOMParser().parseFromString(html, "text/html");
    const zona = novo.body.firstElementChild;
    const atual = document.getElementById(id);
    const antes = Array.from(trocaFoto.querySelectorAll("[data-dmo-regiao]")).find((item) => item.id === id);
    if (!zona || novo.body.children.length !== 1 || novo.head.children.length !== 0
      || Array.from(novo.body.childNodes).some((node) => node.nodeType === 3 && node.textContent.trim())
      || Array.from(novo.querySelectorAll("[id]")).filter((node) => node.id === id).length !== 1
      || zona.id !== id || !zona.hasAttribute("data-dmo-regiao") || !atual || !antes || !trocaRaiz.contains(atual)
      || zona.matches(TROCA_PRECISA_PAGINA) || zona.querySelector(TROCA_PRECISA_PAGINA)) return false;
    const recusa = zona.querySelector("[data-dmo-toast=\"erro\"]") !== null;
    if (semTokens(antes.cloneNode(true), true) === semTokens(zona.cloneNode(true), true)) {
      if (modo === "repetir" && zona.querySelector("[data-dmo-toast]")) mensagensDe(zona, origem);
      return { recusa };
    }
    const ativo = document.activeElement;
    const focado = ativo && ativo.id;
    const node = document.importNode(zona, true);
    atual.replaceWith(node);
    antes.replaceWith(zona.cloneNode(true));
    if (node.matches("[data-dmo-list]")) bindList(node);
    node.querySelectorAll("[data-dmo-list]").forEach(bindList);
    if (origem !== undefined) node.querySelectorAll("[data-dmo-toast]").forEach((mensagem) => mensagem.setAttribute("data-dmo-origem", origem));
    if (window.DMOSuggest) window.DMOSuggest.init(node);
    mensagens(node);
    let foco = autofoco ? node.querySelector("[autofocus]") : null;
    if (!foco && focado) {
      const mesmo = document.getElementById(focado);
      if (mesmo && node.contains(mesmo)) foco = mesmo;
    }
    if (foco && foco.classList.contains("dmo-suggest-combo") && window.DMOSuggest && window.DMOSuggest.focar) window.DMOSuggest.focar(foco);
    else if (foco) foco.focus({ preventScroll: true });
    document.dispatchEvent(new CustomEvent("dmo:troca", { detail: { zonas: [id], origem: pedido, envio: null, recusa, ativo, pagina: novo } }));
    return { recusa };
  }

  // O SERVIDOR DESENHA SÓ AS ZONAS DE UMA GRAVAÇÃO (E11, beta.189): um
  // formulário POST dentro de [data-dmo-zonas="id …"] envia o cabeçalho
  // X-Dmo-Zonas com a zona dele e as declaradas, e o fetch leva-o também ao
  // GET do redirect (PRG). A página que o conhece responde a esse GET só com
  // essas zonas, pela mesma ordem, e com X-Dmo-Region igual ao pedido; a que
  // não o conhece responde com a página inteira, sem X-Dmo-Region, e troca-se
  // como sempre. A resposta tem de ser só as raízes pedidas (id e
  // data-dmo-regiao, uma de cada); senão devolve false e quem pediu abre a
  // página inteira com um GET (o POST não se repete). As zonas novas entram
  // numa cópia da fotografia e o resto é o trocar de sempre: a zona do
  // formulário troca sempre, as outras se mudaram, com as mensagens, a fila,
  // o foco e o dmo:troca (detail.pagina é o documento com as zonas).
  function trocarZonas(html, ids, opcoes = {}) {
    const novo = new DOMParser().parseFromString(html, "text/html");
    const zonas = Array.from(novo.body.children);
    if (novo.head.children.length !== 0 || zonas.length !== ids.length
      || Array.from(novo.body.childNodes).some((node) => node.nodeType === 3 && node.textContent.trim())
      || zonas.some((zona, i) => zona.id !== ids[i] || !zona.hasAttribute("data-dmo-regiao")
        || Array.from(novo.querySelectorAll("[id]")).filter((node) => node.id === zona.id).length !== 1)) return false;
    const novaRaiz = trocaFoto.cloneNode(true);
    const fotos = zonas.map((zona) => Array.from(novaRaiz.querySelectorAll("[data-dmo-regiao]")).filter((item) => item.id === zona.id));
    // Cada zona existe uma vez na fotografia e nenhuma está dentro de outra
    // pedida (a de fora já a traria).
    if (fotos.some((iguais) => iguais.length !== 1)
      || fotos.some(([foto]) => fotos.some(([outra]) => outra !== foto && outra.contains(foto)))) return false;
    const novas = zonas.map((zona, i) => {
      const copia = zona.cloneNode(true);
      fotos[i][0].replaceWith(copia);
      return copia;
    });
    return trocarRaiz(novo, novaRaiz, opcoes, novas);
  }

  // A página inteira (GET, sem X-Dmo-Zonas) quando a resposta só com as
  // zonas não cumpriu o contrato. null se não deu.
  async function paginaInteira(url) {
    try {
      const response = await fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "fetch" } });
      const final = new URL(response.url || url, window.location.href);
      return response.ok && final.pathname === window.location.pathname && !response.headers.get("X-Dmo-Region")
        ? await response.text() : null;
    } catch {
      return null;
    }
  }

  // As zonas que o servidor desenha depois de gravar este formulário (ver
  // trocarZonas): a do formulário e as de data-dmo-zonas, sem repetir. null
  // sem data-dmo-zonas ou fora de uma zona (a página inteira, como sempre).
  // Lido quando o envio parte, no formulário que está no ecrã: a resposta
  // anterior pode ter mudado a lista (nos Pegamentos, o contexto só vai
  // antes da primeira medição).
  function zonasDe(form) {
    const zona = form.closest("[data-dmo-regiao]");
    const declaradas = form.closest("[data-dmo-zonas]");
    if (!zona || !zona.id || !declaradas) return null;
    const ids = [zona.id, ...declaradas.getAttribute("data-dmo-zonas").split(/\s+/)].filter(Boolean);
    return ids.filter((id, i) => ids.indexOf(id) === i).join(" ");
  }

  // Com uma âncora (#lightFicha), a página só desliza até ela quando ela não
  // está à vista; se já se vê, nada se mexe.
  function mostrar(hash) {
    const alvo = hash && hash.length > 1 ? document.getElementById(decodeURIComponent(hash.slice(1))) : null;
    if (!alvo) return;
    const topo = alvo.getBoundingClientRect().top;
    if (topo < 0 || topo > window.innerHeight - 120) alvo.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function ocupar() {
    ocupados++;
    trocaRaiz.setAttribute("aria-busy", "true");
  }

  function desocupar() {
    ocupados = Math.max(0, ocupados - 1);
    if (!ocupados) trocaRaiz.removeAttribute("aria-busy");
  }

  // Um pedido acabou (aplicado, deitado fora ou falhado): a página pode
  // arrumar o que preparou para ele. detail.origem é o formulário enviado ou
  // o elemento que abriu o destino; detail.envio, num envio, o número dele
  // (o mesmo do dmo:fila, do dmo:enviar e do dmo:troca da resposta).
  // detail.semResposta, num envio: a ligação falhou antes de chegar a
  // resposta e o que se vê é a confirmação (ver confirmar); a comparação do
  // Peso devolve então ao formulário o que foi enviado, com a mesma chave
  // (ver mesmoEnvio; deep sweep, 18/10/2026, B-peso-4).
  function acabou(origem, numero = null, semResposta = false) {
    document.dispatchEvent(new CustomEvent("dmo:fim", { detail: { origem, envio: numero, semResposta } }));
  }

  // Um GET pedido por um formulário (o submit de um form method=get, que a
  // página manda para ir com o form ou com o botão que o enviou).
  function formularioGet(origem) {
    const form = origem instanceof HTMLFormElement ? origem
      : origem && origem.form && /^(submit|image)$/i.test(origem.type || "") ? origem.form : null;
    return form && (form.getAttribute("method") || "get").toLowerCase() === "get" ? form : null;
  }

  // Um clique (GET). Se há gravações pendentes na página, o pedido espera por
  // elas: um GET servido antes de a gravação ficar na base mostrava a página
  // sem ela (a leitura gravada não aparecia e o valor ficava no campo). Se
  // uma gravação sai enquanto o GET está a caminho, a resposta dele pode já
  // não ter essa gravação: pede-se outra vez, depois dela. Num GET pedido por
  // um formulário, a mesma recusa duas vezes seguidas mostra-se as duas
  // vezes, como num envio; num link, só trocam as zonas que mudaram.
  // substituirHistorico: o endereço novo substitui o atual (a procura ao
  // escrever não enche o «voltar» do browser com cada tecla).
  // Um calendário ao lado de filtros que trocam só a lista
  // (data-dmo-filtros="idDoFormulário campo…", a Revisão): os dias e as setas
  // do mês levam o valor atual desses campos, não o de quando a página abriu.
  function comFiltros(url, origem) {
    const zona = origem && origem.closest && origem.closest("[data-dmo-filtros]");
    const [formId, ...campos] = zona ? zona.dataset.dmoFiltros.split(" ") : [];
    const form = formId && document.getElementById(formId);
    if (!form) return url;
    const destino = new URL(url, window.location.href);
    const dados = new FormData(form);
    campos.forEach((campo) => {
      const valor = String(dados.get(campo) ?? "").trim();
      if (valor) destino.searchParams.set(campo, valor);
      else destino.searchParams.delete(campo);
    });
    return destino.href;
  }

  async function ir(url, origem, substituirHistorico = false) {
    const destino = new URL(comFiltros(url, origem), window.location.href);
    const parcial = trocaRaiz && origem && origem.closest && !origem.closest("[data-dmo-completa]") && origem.closest("[data-dmo-parcial]")
      && destino.origin === window.location.origin && destino.pathname === window.location.pathname
      && typeof DOMParser !== "undefined";
    if (!parcial) {
      window.location.href = destino.href;
      return;
    }
    const pedido = ++irPedido;
    const vez = ++operacoes;
    const form = formularioGet(origem);
    const zonaForm = form && form.closest("[data-dmo-regiao]");
    const zona = zonaForm ? zonaForm.id : "";
    const vistas = form ? mensagensVistas(zona) : [];
    // O servidor desenha só a zona (ver trocarBloco): o handler vai só no
    // pedido; o endereço do browser fica o normal.
    const fonte = origem.closest("[data-dmo-handler][data-dmo-alvo]");
    const pedir = new URL(destino.href);
    if (fonte) pedir.searchParams.set("handler", fonte.dataset.dmoHandler);
    ocupar();
    try {
      for (;;) {
        while (envios.length) await fila;
        if (pedido !== irPedido) return;
        const gravacoes = enviados;
        let html = null;
        try {
          const response = await fetch(pedir.href, { credentials: "same-origin", headers: { "X-Requested-With": "fetch" } });
          const final = new URL(response.url || pedir.href, window.location.href);
          if (response.ok && final.origin === destino.origin && final.pathname === destino.pathname
            && (!fonte || response.headers.get("X-Dmo-Region") === fonte.dataset.dmoAlvo)) html = await response.text();
        } catch {
          html = null;
        }
        if (pedido !== irPedido) return;
        if (enviados !== gravacoes) continue;
        const opcoes = form
          ? { origem: zona, mensagens: "repetir", autofoco: vez === operacoes, pedido: origem }
          : { autofoco: vez === operacoes, pedido: origem };
        const trocou = html && (fonte ? trocarBloco(html, fonte.dataset.dmoAlvo, opcoes) : trocar(html, opcoes));
        if (!trocou) {
          while (envios.length) await fila;
          if (pedido === irPedido) abrirInteira(destino.href);
          return;
        }
        vistas.forEach((mensagem) => mensagem.remove());
        window.history[substituirHistorico ? "replaceState" : "pushState"]({ dmoTroca: true }, "", destino.href);
        if (!trocou.recusa) {
          const sem = new URL(destino.href);
          sem.hash = "";
          limpo = sem.href;
        }
        mostrar(destino.hash);
        return;
      }
    } finally {
      desocupar();
      acabou(origem);
    }
  }

  // Gravar sem recarregar: o mesmo envio que o browser faria, com fetch. O
  // servidor grava e responde com o redirect para a página (PRG); se voltou
  // à mesma página, trocam-se só as zonas e o endereço é substituído (gravar
  // trinta punções não enche o «voltar» do browser). Noutra página, ou se a
  // troca não der, abre-se o endereço final com um GET: o POST já foi feito
  // e não se repete. Um ficheiro (um PDF) descarrega-se. A zona do formulário
  // troca sempre, mesmo que o servidor a desenhe igual (a mesma hora no campo,
  // a mesma recusa): o formulário volta limpo, como numa página nova. Sem
  // isto ficava o que o operador escreveu, como se não tivesse gravado. Uma
  // zona com mensagens também troca sempre: a mensagem é a resposta a este
  // envio, e a mesma recusa duas vezes seguidas mostra-se as duas vezes.
  //
  // A resposta a um envio retira do canto as mensagens que lá estavam quando
  // ele saiu e que eram da mesma zona (de um envio anterior desse cartão) ou
  // da página (as que vieram com ela e o aviso de ligação): uma recusa antiga
  // não fica ao lado do que se gravou a seguir. A recusa de outro cartão fica
  // até se fechar, e as que chegam depois de o envio sair também.
  //
  // Os envios seguem em fila, um de cada vez, com o corpo lido quando o
  // operador envia (a página pode acertá-lo quando parte, com dmo:enviar): o
  // segundo só parte depois de a resposta do primeiro estar no ecrã, por isso
  // uma resposta mais antiga nunca fica por cima de uma mais nova. Cada
  // resposta troca as zonas (ver trocar), também a de um formulário que ainda
  // espera: esse volta a mostrar o que foi enviado, marcado à espera, até a
  // resposta dele chegar. Um envio igual ao último desse formulário que
  // ainda não respondeu (o mesmo formulário, pelo id ou pela ação e pelos
  // campos escondidos, com o mesmo corpo) é ignorado: um segundo Enter ou
  // clique não grava duas vezes. Um envio diferente vai para a fila, também
  // um igual a um anterior com outro pelo meio (OK, NOK e OK outra vez antes
  // de o primeiro responder): fica gravada a última escolha. Um clique feito
  // durante a gravação espera por ela (ver ir). A fila só se perde se a
  // página abrir inteira.
  //
  // Não há prazo: um servidor lento acaba por responder, e a resposta
  // aparece. Ao fim de tempos.aviso com envios na fila aparece uma mensagem
  // fixa no canto («A gravar…»), que fica até a fila esvaziar (também
  // enquanto se confirma uma ligação que falhou). O POST nunca se repete: se
  // a ligação falhar antes de chegar a resposta, não se sabe se o servidor
  // gravou, e voltar a enviar gravaria duas vezes. Pede-se a página outra
  // vez com um GET (ver confirmar) e os envios em fila partem a seguir. Sem
  // ligação, o GET repete-se quando ela volta: até lá o envio fica na fila,
  // como num servidor lento (deep sweep, 18/10/2026, B-boquilhas-2).
  //
  // Uma resposta de erro nunca muda de página e o POST nunca se repete (ver
  // tipoDeResposta). Numa recusa (4xx) ou com a sessão terminada, nada foi
  // gravado: o que se escreveu fica e o canto diz o que fazer, com o link
  // para recarregar a página ou para entrar noutro separador. Num erro do
  // servidor (5xx) não se sabe se gravou, e segue-se o caminho da ligação
  // que falha (ver confirmar), com outro texto. Antes, uma recusa abria a
  // página outra vez, um erro punha a página de erro no lugar do ecrã e o
  // login abria-se por cima, e o escrito perdia-se sem aviso (sweep do Codex,
  // 19/10/2026, FE-1).

  // A página abre-se inteira (com um GET): os envios em fila já não partem,
  // porque a resposta deles não teria onde aparecer.
  function abrirInteira(url) {
    saindo = true;
    window.location.href = url;
  }

  // Uma mensagem fixa no canto sobre a ligação ou a resposta do servidor
  // (uma só: a nova substitui a anterior), com um link no fim para onde se
  // resolve, quando há (ver _DmoAvisoLink).
  function avisarFixo(texto, link = null) {
    document.querySelectorAll("[data-dmo-ligacao]").forEach((antes) => antes.remove());
    const aviso = document.createElement("div");
    aviso.setAttribute("data-dmo-toast", "erro");
    aviso.setAttribute("data-dmo-ligacao", "");
    aviso.setAttribute("role", "alert");
    aviso.textContent = texto;
    if (link) {
      const a = document.createElement("a");
      a.className = "dmo-aviso-link";
      a.href = link.href;
      a.textContent = link.texto;
      if (link.novoSeparador) { a.target = "_blank"; a.rel = "noopener"; }
      aviso.append(" ", a);
    }
    const caixa = document.createElement("div");
    caixa.appendChild(aviso);
    mensagens(caixa);
    if (!aviso.isConnected) document.body.appendChild(aviso);
  }

  // A fila espera pelo servidor: uma mensagem fixa no canto (a pilha está
  // fora do fluxo, por isso nada se mexe).
  function avisarEspera() {
    if (document.querySelector("[data-dmo-espera]")) return;
    const aviso = document.createElement("div");
    aviso.setAttribute("data-dmo-toast", "espera");
    aviso.setAttribute("data-dmo-espera", "");
    aviso.setAttribute("data-dmo-origem", "espera");
    aviso.setAttribute("role", "status");
    aviso.textContent = "A gravar… O servidor ainda não respondeu: não repita nem saia da página.";
    const caixa = document.createElement("div");
    caixa.appendChild(aviso);
    mensagens(caixa);
    if (!aviso.isConnected) document.body.appendChild(aviso);
  }

  // O relógio do «A gravar…»: começa quando a fila deixa de estar vazia e
  // pára (a mensagem sai) quando ela esvazia.
  let relogioEspera = 0;
  function esperarFila() {
    clearTimeout(relogioEspera);
    relogioEspera = setTimeout(avisarEspera, tempos.aviso);
  }

  function filaVazia() {
    clearTimeout(relogioEspera);
    relogioEspera = 0;
    document.querySelectorAll("[data-dmo-espera]").forEach((aviso) => aviso.remove());
  }

  const LIGACAO_FALHOU = "A ligação falhou antes de chegar a resposta. A página mostra agora o que ficou gravado: confirme antes de repetir.";
  const SEM_CONFIRMAR = "A ligação falhou: não foi possível confirmar se ficou gravado. O que escreveu continua no ecrã. Quando a ligação voltar, a página mostra o que ficou gravado.";
  const SERVIDOR_FALHOU = "O servidor deu um erro. A página mostra agora o que ficou gravado: confirme antes de repetir.";
  const SERVIDOR_SEM_CONFIRMAR = "O servidor deu um erro e não foi possível confirmar se ficou gravado. O que escreveu continua no ecrã; a página volta a tentar sozinha.";
  const RECUSOU = "O servidor recusou o envio. O que escreveu continua no ecrã: recarregue a página e grave outra vez.";
  const SESSAO_AO_GRAVAR = "A sessão terminou e nada foi gravado. O que escreveu continua no ecrã: entre outra vez e grave de novo.";
  const SESSAO_NA_PROCURA = "A sessão terminou. Entre outra vez.";
  const PROCURA_FALHOU = "Não foi possível procurar. Escreva de novo.";

  // O que o servidor respondeu a um pedido do motor (final: o endereço onde
  // a resposta acabou, depois dos redirects):
  // - "sessao": a sessão terminou (8 h sem uso, ou rejeitada). As páginas têm
  //   [Authorize], e o cookie responde com um redirect para o login (o fetch
  //   segue-o) ou, se um dia for assim configurado, com 401. Nada correu.
  // - "servidor": um erro do servidor (5xx, a página de erro ou o proxy). O
  //   handler pode ter falhado depois de gravar.
  // - "recusa": outro 4xx. A antifalsificação (a sessão mudou noutro
  //   separador: 400 sem corpo) e a porta do ADMIN (403) correm antes do
  //   handler, por isso nada foi gravado.
  // - "ok": o resto. Decide-se primeiro pelo ok: uma resposta com ok e sem
  //   status (as dos testes) é "ok", salvo se acabou no login.
  function tipoDeResposta(response, final) {
    if (final.pathname.toLowerCase() === "/login" || response.status === 401) return "sessao";
    if (response.ok) return "ok";
    return response.status >= 500 ? "servidor" : "recusa";
  }

  // O link «Entrar», de volta a esta página depois do login.
  function entrar(novoSeparador) {
    return { texto: "Entrar", href: "/login?ReturnUrl=" + encodeURIComponent(window.location.pathname + window.location.search), novoSeparador };
  }

  // Espera que a ligação volte: o evento online ou, sem ele (uma rede que
  // cai sem o browser dar por isso), tempos.confirmar ms.
  function ligacaoDeVolta() {
    return new Promise((resolve) => {
      const voltou = () => {
        clearTimeout(relogio);
        window.removeEventListener("online", voltou);
        resolve();
      };
      const relogio = setTimeout(voltou, tempos.confirmar);
      window.addEventListener("online", voltou);
    });
  }

  // A ligação falhou antes de chegar a resposta (ou a resposta não se leu).
  // Pede-se com um GET o endereço limpo da página (sem uma recusa antiga,
  // que o servidor desenharia outra vez com os valores recusados) e trocam-se
  // só as zonas que mudaram: o que ficou gravado aparece e o resto fica como
  // o operador o deixou. As mensagens desse GET não se mostram (são do
  // endereço, não deste envio) e as que estavam no canto desse cartão saem;
  // fica a mensagem fixa a pedir para confirmar. Sem ligação (ou com um erro
  // do servidor), nada se troca: o que está escrito fica, com uma mensagem
  // fixa no canto, e o GET repete-se quando a ligação volta (ver
  // ligacaoDeVolta), sem repetir o POST. Até lá o envio continua na fila: a
  // página mostra-o à espera e os envios seguintes esperam por ele. Antes,
  // ficava tudo como estava e um novo clique gravava outra vez (deep sweep,
  // 18/10/2026, B-boquilhas-2, B-pegamentos-3, B-peso-3). Se a página abriu
  // já com uma recusa no endereço (F5 depois de uma recusa) e ainda não
  // desenhou nada sem ela, o único endereço que se conhece é esse: o GET traz
  // a recusa e os valores recusados de volta (limitação conhecida, ver o guia).
  async function confirmar({ vistas, envio, motivo = "ligacao" }) {
    for (let tentativa = 0; ; tentativa++) {
      if (tentativa > 0) await ligacaoDeVolta();
      if (saindo) return;
      // O browser sabe que está sem rede: espera outra vez, sem pedir.
      if (tentativa > 0 && navigator.onLine === false) continue;
      const aqui = new URL(limpo || window.location.href);
      aqui.hash = "";
      try {
        const response = await fetch(aqui.href, { credentials: "same-origin", headers: { "X-Requested-With": "fetch" } });
        if (!response.ok) throw new Error("sem resposta");
        const final = new URL(response.url || aqui.href, window.location.href);
        if (final.pathname !== aqui.pathname) { abrirInteira(final.href); return; }
        const html = await response.text();
        const trocou = trocar(html, { autofoco: envio.vez === operacoes, mensagens: "nenhuma", envio, pedido: envio.form });
        if (!trocou) { abrirInteira(aqui.href); return; }
        vistas.forEach((mensagem) => mensagem.remove());
        window.history.replaceState(null, "", final.href);
        if (!trocou.recusa) limpo = final.href;
        avisarFixo(motivo === "servidor" ? SERVIDOR_FALHOU : LIGACAO_FALHOU);
        return;
      } catch {
        // Uma mensagem só, mesmo com várias tentativas.
        if (tentativa === 0) avisarFixo(motivo === "servidor" ? SERVIDOR_SEM_CONFIRMAR : SEM_CONFIRMAR);
      }
    }
  }

  function gravar(form, submitter) {
    let corpo;
    try { corpo = new FormData(form, submitter || undefined); } catch { corpo = new FormData(form); }
    if (submitter && submitter.name && !corpo.has(submitter.name)) corpo.append(submitter.name, submitter.value);
    // O formaction do botão (o «Não aprovar» da Revisão), resolvido como o
    // browser o resolve (o jsdom dos testes não tem submitter.formAction).
    const destino = submitter && submitter.hasAttribute("formaction")
      ? new URL(submitter.getAttribute("formaction"), window.location.href).href : form.action;
    const quem = identidade(form);
    const chave = `${quem} ${destino} ${semToken(corpo.entries())}`;
    // Só o último envio deste formulário conta: comparar com todos deixava
    // de fora o terceiro de OK, NOK, OK, e ficava gravado o NOK (deep sweep,
    // 18/10/2026).
    const ultimo = envios.filter((outro) => outro.identidade === quem).pop();
    if (ultimo && ultimo.chave === chave) return;
    const vez = ++operacoes;
    const zonaForm = form.closest("[data-dmo-regiao]");
    const origem = zonaForm ? zonaForm.id : "";
    const repetido = semResposta.get(form);
    const envio = {
      vez, form, chave, id: form.id, acao: form.action, identidade: quem,
      escrito: Array.from(corpo.entries()),
      botao: submitter ? { name: submitter.name, value: submitter.value } : null,
      idEnvio: repetido && repetido.chave === chave ? repetido.idEnvio : novaChave(),
    };
    if (!envios.length) esperarFila();
    envios.push(envio);
    enviados++;
    // Com ficheiros (multipart) vai o FormData tal como está; sem, o mesmo
    // corpo urlencoded que o browser enviaria.
    const multipart = (submitter && submitter.getAttribute("formenctype") || form.enctype) === "multipart/form-data";
    const vistas = mensagensVistas(origem);
    ocupar();
    // Aplica a resposta; devolve false se a ligação falhou antes de a ter.
    const aplicar = async (response, zonas) => {
      const final = new URL(response.url || destino, window.location.href);
      const tipo = response.headers.get("Content-Type") || "";
      if (!tipo.includes("text/html")) {
        const nome = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(response.headers.get("Content-Disposition") || "");
        if (response.ok && nome) {
          const link = document.createElement("a");
          link.href = URL.createObjectURL(await response.blob());
          link.download = decodeURIComponent(nome[1]);
          document.body.appendChild(link);
          link.click();
          link.remove();
          setTimeout(() => URL.revokeObjectURL(link.href), 10000);
        } else {
          abrirInteira(final.href);
        }
        return;
      }
      const html = await response.text();
      // Só as zonas pedidas (ver trocarZonas): X-Dmo-Region diz que o
      // servidor desenhou só elas.
      const regiao = response.headers.get("X-Dmo-Region") || null;
      return async () => {
        const mesmaPagina = response.ok && final.pathname === window.location.pathname;
        if (mesmaPagina) vistas.forEach((mensagem) => mensagem.remove());
        const opcoes = { gravou: zonaForm ? zonaForm.id : null, origem, autofoco: vez === operacoes, envio, pedido: envio.form };
        let trocou = false;
        if (mesmaPagina && zonas && regiao !== null) {
          trocou = regiao === zonas && trocarZonas(html, zonas.split(" "), opcoes);
          if (!trocou) {
            // O contrato falhou: a página inteira, com um GET (o POST não se
            // repete), troca-se como sempre; se nem isso der, abre-se.
            const inteira = await paginaInteira(final.href);
            trocou = inteira !== null && trocar(inteira, opcoes);
            if (!trocou) { abrirInteira(final.href); return; }
          }
        } else {
          trocou = mesmaPagina && trocar(html, opcoes);
        }
        if (trocou) {
          if (response.redirected) {
            window.history.replaceState(null, "", final.href);
            if (!trocou.recusa) limpo = final.href;
          }
          return;
        }
        if (response.redirected || regiao !== null) {
          abrirInteira(final.href);
        } else {
          // Uma resposta 2xx sem redirect que não cabe nas zonas (uma página
          // desenhada pelo POST, com a parte de fora diferente): mostra-se
          // como o browser a mostraria. Um erro já não chega aqui (ver
          // tipoDeResposta).
          saindo = true;
          document.open();
          document.write(html);
          document.close();
        }
      };
    };
    const enviar = async () => {
      if (saindo) return;
      // A página pode acertar o corpo no momento em que o envio parte, já
      // com as respostas dos envios anteriores no ecrã (nos Pegamentos, a
      // correção aberta noutro cartão): dmo:enviar, com o form e o FormData.
      document.dispatchEvent(new CustomEvent("dmo:enviar", { detail: { form: envio.form, corpo, envio: vez } }));
      let mostrarResposta = null;
      let falhou = false;
      let motivo = "ligacao";
      // As zonas que o servidor pode desenhar sozinhas (ver trocarZonas); o
      // fetch leva o cabeçalho também ao GET do redirect.
      const zonas = zonasDe(envio.form);
      const headers = { "X-Requested-With": "fetch", "X-Dmo-Envio": envio.idEnvio };
      if (zonas) headers["X-Dmo-Zonas"] = zonas;
      try {
        const response = await fetch(destino, { method: "POST", body: multipart ? corpo : new URLSearchParams(corpo), credentials: "same-origin", headers });
        const tipo = tipoDeResposta(response, new URL(response.url || destino, window.location.href));
        if (tipo === "servidor") {
          // Não se sabe se gravou: como a ligação que falha, confirma-se
          // com um GET, e a página de erro nunca substitui o ecrã.
          falhou = true;
          motivo = "servidor";
        } else if (tipo !== "ok") {
          // Recusa ou sessão terminada: nada foi gravado. A página fica (o
          // que se escreveu também) e o canto diz o que fazer.
          mostrarResposta = async () => {
            vistas.forEach((mensagem) => mensagem.remove());
            if (tipo === "sessao") {
              // O login noutro separador, para o escrito continuar à vista.
              avisarFixo(SESSAO_AO_GRAVAR, entrar(true));
            } else {
              const aqui = new URL(limpo || window.location.href);
              aqui.hash = "";
              avisarFixo(RECUSOU, { texto: "Recarregar a página", href: aqui.href });
            }
          };
        } else {
          mostrarResposta = await aplicar(response, zonas);
        }
      } catch {
        falhou = true;
      }
      // Sem resposta, a chave fica no formulário para o mesmo envio outra vez
      // (ver semResposta); com resposta, sai.
      const guardada = semResposta.get(envio.form);
      if (falhou) semResposta.set(envio.form, { chave, idEnvio: envio.idEnvio });
      else if (guardada && guardada.idEnvio === envio.idEnvio) semResposta.delete(envio.form);
      if (!falhou && mostrarResposta) {
        try { await mostrarResposta(); } catch { falhou = true; }
      }
      envio.semResposta = falhou;
      if (falhou) await confirmar({ vistas, envio, motivo });
    };
    fila = fila
      .then(enviar)
      .catch(() => {})
      .then(() => {
        envios.splice(envios.indexOf(envio), 1);
        if (!envios.length) filaVazia();
        if (envio.form.isConnected) desmarcar(envio.form);
        desocupar();
        acabou(envio.form, vez, envio.semResposta === true);
      });
    // A página fica a saber que o envio entrou na fila (um envio igual a um
    // que espera não entra): nos Pegamentos, é aí que a ação do cartão começa.
    form.dispatchEvent(new CustomEvent("dmo:fila", { bubbles: true, detail: { form, corpo, submitter: submitter || null, envio: vez } }));
  }
  // O formulário novo (a zona trocou com a confirmação) continua o envio sem
  // resposta do antigo: com o mesmo corpo, leva a mesma chave (ver
  // semResposta). Só a página sabe que repôs lá o que foi enviado (a
  // comparação do Peso, no dmo:fim); um formulário novo vazio onde o
  // operador volta a escrever o mesmo é um envio novo (deep sweep,
  // 18/10/2026, B-peso-4).
  function mesmoEnvio(antigo, novo) {
    if (antigo && novo && antigo !== novo && semResposta.has(antigo)) semResposta.set(novo, semResposta.get(antigo));
  }
  window.DmoTroca = { ir, tempos, mesmoEnvio };

  if (trocaRaiz) {
    // Voltar atrás ou à frente mostra o endereço desse momento, desenhado de
    // novo pelo servidor.
    window.addEventListener("popstate", () => window.location.reload());
    // O dia já escolhido do calendário tem o seu ouvinte (ver abaixo).
    document.addEventListener("click", (event) => {
      const link = event.target.closest && event.target.closest("a[href]");
      if (!link || link.closest("[data-dmo-completa]") || link.hasAttribute("data-dmo-calendar-unselect")
        || !link.closest("[data-dmo-parcial]") || event.defaultPrevented || event.button !== 0
        || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || link.target || link.hasAttribute("download")) return;
      event.preventDefault();
      ir(link.href, link);
    });
    // Depois dos ouvintes em captura (a pasta dos PDFs) e dos da página: um
    // envio que outro já tratou (defaultPrevented) não se toca.
    document.addEventListener("submit", (event) => {
      const form = event.target;
      const submitter = event.submitter;
      const metodo = (submitter && submitter.getAttribute("formmethod")) || form.getAttribute("method") || "";
      if (event.defaultPrevented || !(form instanceof HTMLFormElement) || !form.closest("[data-dmo-parcial]")
        || form.hasAttribute("data-dmo-nativo")
        || metodo.toLowerCase() !== "post" || form.target || (submitter && submitter.formTarget)) return;
      event.preventDefault();
      gravar(form, event.submitter);
    });
    // Um formulário GET dentro de data-dmo-parcial (as setas do mês do
    // calendário, os filtros da Revisão) abre como um clique: pede o
    // endereço que o browser pediria (com o botão que o enviou) e troca só
    // as zonas (ir). Fica no window, depois dos ouvintes da página no
    // document: uma página que trata o seu GET (o Peso, que guarda o campo
    // com o foco) chama ela o ir e este já não lhe toca (defaultPrevented).
    // Um GET para outra página ou para outra janela segue como sempre.
    window.addEventListener("submit", (event) => {
      const form = event.target;
      const button = event.submitter;
      if (event.defaultPrevented || !(form instanceof HTMLFormElement) || !trocaRaiz.contains(form)
        || !form.closest("[data-dmo-parcial]")) return;
      const metodo = (button && button.getAttribute("formmethod")) || form.getAttribute("method") || "get";
      const alvo = (button && button.getAttribute("formtarget")) || form.target;
      if (metodo.toLowerCase() !== "get" || alvo && alvo !== "_self") return;
      const url = new URL((button && button.getAttribute("formaction")) || form.action, window.location.href);
      if (url.origin !== window.location.origin || url.pathname !== window.location.pathname) return;
      url.search = new URLSearchParams(new FormData(form, button || undefined)).toString();
      event.preventDefault();
      ir(url.href, button || form);
    });
  }

  // A procura ao escrever com resultados numa lista (Diogo, 17/10/2026):
  // <input data-dmo-procura="/Pagina?handler=X" data-dmo-procura-alvo="id">.
  // 250 ms depois da última tecla, pede ao servidor a lista (HTML) para o
  // texto escrito e mostra-a no alvo; campo vazio, alvo vazio. A lista que
  // chega é uma lista da casa: um clique ou Enter abre.
  //
  // ENQUANTO A RESPOSTA NÃO CHEGA, O QUE ESTÁ NO ALVO É DO TEXTO ANTERIOR
  // (deep sweep, 18/10/2026, B-jobon-6): escrever outra referência deixava o
  // dropdown com as produções da anterior, e escolher uma abria a produção
  // de outra referência. Agora o alvo fica à espera (data-dmo-procura-espera
  // e aria-busy) desde a tecla até chegar a lista nova: os dropdowns e os
  // botões dele ficam desativados e as linhas não escolhem nem abrem. Sem
  // ligação, fica assim, e a procura repete-se quando a ligação volta. Se o
  // servidor responde sem a lista (a sessão terminou, ou um erro), fica
  // também bloqueado, mas sem esperar pela rede: o canto diz «A sessão
  // terminou», com o link para entrar, ou «Não foi possível procurar», e
  // escrever de novo procura outra vez. Antes, a procura ficava presa sem
  // mensagem (sweep do Codex, 19/10/2026, FE-3).
  function procuraEspera(target, espera) {
    if (espera) {
      target.setAttribute("data-dmo-procura-espera", "");
      target.setAttribute("aria-busy", "true");
      target.querySelectorAll("select, button").forEach((controlo) => { controlo.disabled = true; });
    } else {
      target.removeAttribute("data-dmo-procura-espera");
      target.removeAttribute("aria-busy");
    }
  }
  document.querySelectorAll("[data-dmo-procura]").forEach((field) => {
    const target = document.getElementById(field.dataset.dmoProcuraAlvo || "");
    if (!target) return;
    let timer = 0;
    let latest = 0;
    field.setAttribute("autocomplete", "off");
    field.addEventListener("input", () => {
      clearTimeout(timer);
      const term = field.value.trim();
      // data-dmo-procura-sempre: o campo vazio também pede ao servidor (o
      // alvo tem um estado vazio próprio, como o dropdown desativado do Job On).
      if (!term && !field.hasAttribute("data-dmo-procura-sempre")) {
        latest++;
        target.replaceChildren();
        procuraEspera(target, false);
        return;
      }
      // Invalida logo os pedidos anteriores, ainda durante o debounce, e o
      // que está no alvo deixa de se poder escolher.
      latest++;
      procuraEspera(target, true);
      timer = setTimeout(() => {
        const url = field.dataset.dmoProcura + (field.dataset.dmoProcura.includes("?") ? "&" : "?") + "q=" + encodeURIComponent(term);
        carregar(url);
      }, 250);
    });
    function carregar(url, href) {
      const request = ++latest;
      const destino = new URL(url, window.location.href);
      // Houve resposta, mas não é a lista: "sessao" ou "erro".
      let recusou = null;
      fetch(destino.href, { credentials: "same-origin", headers: { "X-Requested-With": "fetch" } })
        .then((response) => {
          const final = new URL(response.url || destino.href, window.location.href);
          const tipo = tipoDeResposta(response, final);
          if (tipo !== "ok" || final.origin !== destino.origin || final.pathname !== destino.pathname) {
            recusou = tipo === "sessao" ? "sessao" : "erro";
            throw new Error("Não foi possível carregar os resultados");
          }
          return response.text();
        })
        .then((html) => {
          if (request !== latest || !target.isConnected) return;
          target.innerHTML = html;
          procuraEspera(target, false);
          target.querySelectorAll("[data-dmo-list]").forEach(bindList);
          if (href) window.history.replaceState(null, "", href);
        })
        .catch(() => {
          if (request !== latest) return;
          if (href) { window.location.href = href; return; }
          if (recusou) {
            // O servidor respondeu sem a lista: esperar pela rede não
            // resolve. O alvo continua bloqueado (a lista é do texto
            // anterior, B-jobon-6), já sem pedido, e o canto diz o que fazer;
            // escrever de novo faz outra procura.
            target.removeAttribute("aria-busy");
            if (recusou === "sessao") avisarFixo(SESSAO_NA_PROCURA, entrar(false));
            else avisarFixo(PROCURA_FALHOU);
            return;
          }
          // Sem resposta: o alvo continua à espera e a mesma procura
          // repete-se quando a ligação volta (se entretanto não houve outra).
          window.addEventListener("online", () => { if (request === latest) carregar(url); }, { once: true });
        });
    }
    target.addEventListener("click", (event) => {
      const link = event.target.closest && event.target.closest("a[data-dmo-procura-pagina]");
      if (!link || !target.contains(link) || event.defaultPrevented || event.button !== 0 || link.target
        || link.hasAttribute("download") || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
      // A página de uma procura que espera pelo texto novo é do anterior.
      if (target.hasAttribute("data-dmo-procura-espera")) { event.preventDefault(); return; }
      const endpoint = new URL(link.dataset.dmoProcuraPagina, window.location.href);
      const href = new URL(link.href, window.location.href);
      if (endpoint.origin !== window.location.origin || endpoint.pathname !== window.location.pathname
        || href.origin !== window.location.origin || href.pathname !== window.location.pathname) return;
      // Paginar a procura não fecha a folha que já está aberta por baixo.
      const current = new URL(window.location.href);
      if (current.searchParams.has("jobon_id")) href.searchParams.set("jobon_id", current.searchParams.get("jobon_id"));
      event.preventDefault();
      clearTimeout(timer);
      carregar(endpoint.href, href.href);
    });

    // Seta para baixo passa do campo para a primeira linha encontrada.
    field.addEventListener("keydown", (event) => {
      if (event.key !== "ArrowDown") return;
      const first = target.querySelector("[data-dmo-row]");
      if (first) { event.preventDefault(); first.focus(); }
    });
  });

  // Um dropdown que abre (Diogo, 17/10/2026: escolher uma opção aplica logo,
  // sem botão ao lado): <select data-dmo-ir>, o valor de cada opção é o
  // endereço que se abre. A opção vazia não vai a lado nenhum.
  document.addEventListener("change", (event) => {
    const select = event.target.closest && event.target.closest("select[data-dmo-ir]");
    // Um dropdown desativado (a procura à espera do texto novo) não abre.
    if (select && select.value && !select.disabled) ir(select.value, select);
  });

  // Os filtros que procuram ao escrever (Diogo, 17/10/2026: sem botão
  // «Procurar»): <form method="get" data-dmo-ao-escrever="idDosResultados">.
  // 350 ms depois da última tecla num campo de texto, ou logo ao mudar um
  // dropdown ou carregar em Enter, pede a mesma página com os filtros e troca
  // só o bloco dos resultados, sem recarregar. O endereço fica com os
  // filtros, para o voltar e o recarregar mostrarem o mesmo.
  // Um filtro que chega numa zona trocada liga-se com o dmo:troca.
  const filtrosLigados = new WeakSet();
  function ligarFiltro(form) {
    if (filtrosLigados.has(form)) return;
    filtrosLigados.add(form);
    const id = form.dataset.dmoAoEscrever;
    let timer = 0;
    let latest = 0;
    function procurar() {
      clearTimeout(timer);
      if (!form.isConnected) return;
      const request = ++latest;
      const params = new URLSearchParams(new FormData(form));
      const url = (form.getAttribute("action") || window.location.pathname) + "?" + params.toString();
      // Dentro de data-dmo-parcial, a procura vai pelo ir: partilha a
      // fotografia e a ordem dos pedidos com a paginação (uma resposta antiga
      // não repõe os resultados anteriores) e espera pelas gravações em fila.
      if (trocaRaiz && form.closest("[data-dmo-parcial]")) {
        ir(url, form, true);
        return;
      }
      // Sem a lista na resposta (a sessão terminou, um erro do servidor) ou
      // sem resposta, o canto diz o que fazer, como na procura das listas;
      // antes os resultados ficavam parados sem aviso (vistoria ao frontend
      // F2, 27/10/2026). Sem rede, a mesma procura repete-se quando a
      // ligação volta.
      const destino = new URL(url, window.location.href);
      let recusou = null;
      fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "fetch" } })
        .then((response) => {
          const final = new URL(response.url || destino.href, window.location.href);
          const tipo = tipoDeResposta(response, final);
          if (tipo !== "ok" || final.origin !== destino.origin || final.pathname !== destino.pathname) {
            recusou = tipo === "sessao" ? "sessao" : "erro";
            throw new Error("Não foi possível carregar os resultados");
          }
          return response.text();
        })
        .then((html) => {
          if (request !== latest || !form.isConnected) return;
          const fresh = new DOMParser().parseFromString(html, "text/html").getElementById(id);
          const current = document.getElementById(id);
          if (!current) return;
          if (!fresh) {
            recusou = "erro";
            throw new Error("A resposta não traz os resultados");
          }
          const node = document.importNode(fresh, true);
          current.replaceWith(node);
          if (node.matches("[data-dmo-list]")) bindList(node);
          node.querySelectorAll("[data-dmo-list]").forEach(bindList);
          window.history.replaceState(null, "", url);
        })
        .catch(() => {
          if (request !== latest || !form.isConnected) return;
          if (recusou === "sessao") avisarFixo(SESSAO_NA_PROCURA, entrar(false));
          else if (recusou) avisarFixo(PROCURA_FALHOU);
          else window.addEventListener("online", () => { if (request === latest) procurar(); }, { once: true });
        });
    }
    form.addEventListener("input", (event) => {
      if (!event.target.matches("input:not([type=date]):not([type=radio]):not([type=checkbox]):not([type=hidden])")) return;
      clearTimeout(timer);
      timer = setTimeout(procurar, 350);
    });
    form.addEventListener("submit", (event) => {
      event.preventDefault();
      procurar();
    });
  }
  document.querySelectorAll("form[data-dmo-ao-escrever]").forEach(ligarFiltro);
  document.addEventListener("dmo:troca", (event) => {
    event.detail.zonas.forEach((id) => {
      const zona = document.getElementById(id);
      if (!zona) return;
      if (zona.matches("form[data-dmo-ao-escrever]")) ligarFiltro(zona);
      zona.querySelectorAll("form[data-dmo-ao-escrever]").forEach(ligarFiltro);
    });
  });

  // O QUE DEPENDE DE UM CAMPO DE TEXTO (deep sweep 2, 18/10/2026, F-8 e
  // F-10): <select data-dmo-depende="idDoCampo"> tem as opções que o
  // servidor desenhou para o texto desse campo (as produções de uma
  // referência). Escrever no campo volta o dropdown à opção vazia, sem
  // aplicar: nos Históricos, que aplicam ao escrever, a produção escolhida
  // para a referência anterior não vai no pedido com o texto novo. Com
  // data-dmo-depende-valor (o texto para que as opções foram desenhadas), o
  // dropdown, ou o botão, fica desativado enquanto o campo tiver outro texto:
  // na escolha do Job On, a produção de outra referência não se abre nem se
  // associa antes de a referência nova se aplicar (Enter ou sugestão).
  // Voltar ao mesmo texto ativa-o outra vez. Por delegação: o dropdown vem
  // numa zona trocada.
  //
  // A RESPOSTA QUE CHEGA DEPOIS DE SE ESCREVER OUTRO TEXTO (revisão do deep
  // sweep 2, 18/10/2026): Enter na 7079C337 e, com a resposta a caminho,
  // escrever a 9322Y152 trazia o dropdown das produções da 7079C337 ativo,
  // com a 9322Y152 no campo; escolher uma abria uma produção da 7079C337
  // (confirmado no Chromium, nos Pegamentos, no Peso e no PU NNPB). Por isso
  // cada troca de zonas volta a ver os dependentes contra o texto que está
  // agora no campo: com -valor, fica desativado; sem -valor, se o campo já
  // não tem o texto com que a página nova foi desenhada, volta à opção vazia
  // (a produção escolhida para a referência anterior não vai na procura
  // seguinte).
  function mesmoTexto(a, b) {
    return String(a || "").trim().toLocaleUpperCase("pt-PT") === String(b || "").trim().toLocaleUpperCase("pt-PT");
  }
  function dependeDoTexto(dependente, campo) {
    const outro = !mesmoTexto(campo.value, dependente.getAttribute("data-dmo-depende-valor"));
    if (outro && !dependente.disabled) {
      dependente.disabled = true;
      dependente.setAttribute("data-dmo-depende-desativado", "");
    } else if (!outro && dependente.hasAttribute("data-dmo-depende-desativado")) {
      dependente.disabled = false;
      dependente.removeAttribute("data-dmo-depende-desativado");
    }
  }
  function semEscolha(dependente) {
    if (dependente.matches("select") && dependente.value !== ""
      && Array.from(dependente.options).some((opcao) => opcao.value === "")) dependente.value = "";
  }
  document.addEventListener("input", (event) => {
    const campo = event.target;
    if (!campo || !campo.id || !campo.matches || !campo.matches("input")) return;
    document.querySelectorAll("[data-dmo-depende]").forEach((dependente) => {
      if (dependente.getAttribute("data-dmo-depende") !== campo.id) return;
      if (dependente.hasAttribute("data-dmo-depende-valor")) dependeDoTexto(dependente, campo);
      else semEscolha(dependente);
    });
  });
  document.addEventListener("dmo:troca", (event) => {
    const pagina = event.detail && event.detail.pagina;
    document.querySelectorAll("[data-dmo-depende]").forEach((dependente) => {
      const campo = document.getElementById(dependente.getAttribute("data-dmo-depende"));
      if (!campo || !campo.matches("input")) return;
      if (dependente.hasAttribute("data-dmo-depende-valor")) {
        dependeDoTexto(dependente, campo);
        return;
      }
      // O texto com que a página nova foi desenhada (o campo fica, por isso o
      // que está no ecrã pode já ser outro). Uma resposta só com uma zona não
      // traz o campo: nada muda.
      const desenhado = pagina && pagina.getElementById ? pagina.getElementById(campo.id) : null;
      if (desenhado && !mesmoTexto(campo.value, desenhado.getAttribute("value"))) semEscolha(dependente);
    });
  });

  // O dia já escolhido do calendário partilhado: um clique tira a seleção
  // (Diogo, 18/10/2026: «desnecessário 2», antes era o duplo clique).
  // Só tira o dia: nenhuma página faz outra ação neste clique (bug de
  // 18/10/2026: o Job On retirava a produção aberta das datas).
  // Por delegação: o calendário pode vir numa zona trocada. Tirar a
  // seleção vai pelo ir (dentro de data-dmo-parcial troca só as zonas).
  document.addEventListener("click", (event) => {
    const day = event.target.closest && event.target.closest("[data-dmo-calendar-unselect]");
    if (!day || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    ir(day.dataset.dmoCalendarUnselect, day);
  });

  // Escolher uma opção num dropdown aplica logo, sem botão «Aplicar» ao lado
  // (Diogo, 17/10/2026). O requestSubmit mantém a validação (required) e os
  // handlers de submit da página. Com data-dmo-autosubmit="valor", a opção
  // vazia («Produção», «Selecionar…») não submete. Uma data também aplica
  // logo, quando fica completa, e um mês também (os Históricos do Job On e
  // das Boquilhas, sem «Procurar»). Por delegação no document: um dropdown
  // que chega numa zona trocada (o OK/NOK de uma área do Resumo) também
  // aplica.
  //
  // Com o teclado, um dropdown fechado muda a cada seta (ou letra) e uma data
  // muda a cada número escrito: aplicar a cada mudança associava o registo
  // das Boquilhas à primeira produção a que a seta chegava, e uma data
  // escrita aplicava-se a meio (o ano «0002»). Uma mudança feita com uma
  // tecla fica por aplicar (data-dmo-por-aplicar) e aplica-se com Enter ou
  // ao sair do campo; com o rato, ou escolhida na lista aberta, aplica logo
  // (deep sweep, 18/10/2026).
  // Uma caixa de marcar também aplica ao marcar (o «Só por associar» da
  // Consulta da Reparação Interna).
  const AUTOSUBMIT = ":is(select,input[type=date],input[type=month],input[type=checkbox])[data-dmo-autosubmit]";
  let teclaEm = null;
  function aplicarEscolha(campo) {
    campo.removeAttribute("data-dmo-por-aplicar");
    if (campo.dataset.dmoAutosubmit === "valor" && !campo.value) return;
    if (campo.form) campo.form.requestSubmit();
  }
  document.addEventListener("keydown", (event) => {
    const campo = event.target;
    if (!campo.matches || !campo.matches(AUTOSUBMIT)) return;
    if (event.key !== "Enter") { teclaEm = campo; return; }
    if (!campo.hasAttribute("data-dmo-por-aplicar")) return;
    event.preventDefault();
    aplicarEscolha(campo);
  }, true);
  document.addEventListener("keyup", () => { teclaEm = null; }, true);
  document.addEventListener("pointerdown", () => { teclaEm = null; }, true);
  document.addEventListener("focusout", (event) => {
    const campo = event.target;
    if (campo.matches && campo.matches(AUTOSUBMIT) && campo.hasAttribute("data-dmo-por-aplicar")) aplicarEscolha(campo);
  }, true);
  document.addEventListener("change", (event) => {
    const campo = event.target;
    if (!campo.matches || !campo.matches(AUTOSUBMIT)) return;
    if (teclaEm === campo) campo.setAttribute("data-dmo-por-aplicar", "");
    else aplicarEscolha(campo);
  });

  // Editar no próprio valor (Diogo, 17/10/2026: «para editar, clica-se no
  // valor da linha e muda-se ali mesmo»): um input.dmo-valor[data-dmo-valor]
  // dentro de um form grava ao sair do campo, sem botão nem recarregar. Envia
  // o token, os campos escondidos da mesma linha e o próprio campo; o
  // servidor responde { ok, valor, aviso, resumo }. Enter grava, Esc desfaz.
  // Sem JavaScript, Enter submete o form inteiro. Por delegação no document
  // (E10): um campo que chega numa zona trocada (a lista dos reparadores
  // depois de criar um) também grava. O valor gravado é o do HTML
  // (defaultValue), atualizado a cada gravação. Uma recusa vai para a pilha
  // do canto (data-dmo-toast), sem empurrar a tabela; a gravação seguinte do
  // valor tira-a.
  //
  // Cada campo grava em fila (sweep de 19/10/2026, FE-8): duas mudanças
  // rápidas no mesmo campo partiam juntas, e o servidor podia gravá-las por
  // outra ordem; o ecrã mostrava um nome e a base ficava com outro. A
  // segunda só parte depois da resposta à primeira, e só a resposta à última
  // mexe no que o campo mostra (não enquanto o operador escreve outro valor
  // nele).
  const filasValor = new WeakMap();
  function campoValor(event) {
    const input = event.target;
    return input && input.matches && input.matches("input[data-dmo-valor]") && input.form ? input : null;
  }
  document.addEventListener("keydown", (event) => {
    const input = campoValor(event);
    if (!input) return;
    if (event.key === "Enter") { event.preventDefault(); input.blur(); }
    if (event.key === "Escape") { input.value = input.defaultValue; input.blur(); }
  });
  document.addEventListener("change", (event) => {
    const input = campoValor(event);
    if (!input) return;
    const fila = filasValor.get(input) || { vez: Promise.resolve(), envios: 0 };
    const envio = ++fila.envios;
    const valor = input.value;
    const gravar = () => gravarValor(input, valor, () => envio === fila.envios);
    fila.vez = fila.vez.then(gravar, gravar);
    filasValor.set(input, fila);
  });
  async function gravarValor(input, valor, ultimo) {
    const form = input.form;
    const body = new FormData();
    const token = form.querySelector("input[name=__RequestVerificationToken]");
    if (token) body.append(token.name, token.value);
    const row = input.closest("tr") || input.parentElement;
    row.querySelectorAll("input[type=hidden]:not([name=__RequestVerificationToken])").forEach((hidden) => {
      if (!body.has(hidden.name)) body.append(hidden.name, hidden.value);
    });
    body.append(input.name, valor);
    try {
      const response = await fetch(form.action, {
        method: "POST", body, credentials: "same-origin", headers: { Accept: "application/json" }
      });
      const data = await response.json();
      const mostrar = ultimo() && !(document.activeElement === input && input.value !== valor);
      document.querySelectorAll("[data-dmo-valor-recusa]").forEach((antes) => antes.remove());
      if (data.ok) {
        // A versão nova vale para os outros formulários da mesma lista, só
        // se ainda trazem a versão deste pedido. Outra aba mantém a antiga.
        const grupo = form.closest("[data-dmo-versao-grupo]");
        if (grupo && typeof data.versao === "string" && body.has("versao")) {
          grupo.querySelectorAll('input[name="versao"]').forEach((hidden) => {
            if (hidden.value === body.get("versao")) hidden.value = data.versao;
          });
        }
        input.defaultValue = data.valor ?? valor;
        if (mostrar) {
          input.value = input.defaultValue;
          input.removeAttribute("aria-invalid");
          input.classList.add("dmo-valor-gravado");
          setTimeout(() => input.classList.remove("dmo-valor-gravado"), 1200);
        }
      } else {
        if (mostrar) {
          input.value = input.defaultValue;
          input.setAttribute("aria-invalid", "true");
        }
        if (data.aviso && window.DMOLive && window.DMOLive.mostrarMensagens) {
          const aviso = document.createElement("p");
          aviso.setAttribute("data-dmo-toast", "erro");
          aviso.setAttribute("data-dmo-valor-recusa", "");
          aviso.setAttribute("role", "alert");
          aviso.textContent = data.aviso;
          const caixa = document.createElement("div");
          caixa.appendChild(aviso);
          window.DMOLive.mostrarMensagens(caixa);
        }
      }
    } catch {
      form.requestSubmit();
    }
  }

  // Copiar um link sem o mostrar (Diogo, 17/10/2026): data-dmo-copiar="url";
  // o rótulo [data-dmo-copiar-label] diz «Copiado» por momentos. Por
  // delegação no document (E8): o botão pode chegar numa zona trocada (os
  // links do mcaliper do controlo aberto na Revisão, depois de aprovar).
  document.addEventListener("click", (event) => {
    const button = event.target.closest && event.target.closest("[data-dmo-copiar]");
    if (!button) return;
    const text = button.dataset.dmoCopiar;
    const label = button.querySelector("[data-dmo-copiar-label]");
    const done = () => {
      button.classList.add("copied");
      if (!label) return;
      const original = label.dataset.original || label.textContent;
      label.dataset.original = original;
      label.textContent = "Copiado";
      setTimeout(() => { label.textContent = original; button.classList.remove("copied"); }, 1600);
    };
    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).then(done).catch(() => window.prompt("Copie o link:", text));
    } else {
      window.prompt("Copie o link:", text);
    }
  });

  // A janela pequena (Diogo, 17/10/2026): data-dmo-dialog="id" abre o
  // <dialog class="dmo-dialog"> com esse id; data-dmo-dialog-close (ou um
  // clique fora) fecha-o. Por delegação no document: a janela e o botão que a
  // abre podem chegar numa zona trocada (o comentário e o link do mcaliper
  // de cada área do Resumo).
  document.addEventListener("click", (event) => {
    if (!event.target.closest) return;
    const opener = event.target.closest("[data-dmo-dialog]");
    const dialog = opener && document.getElementById(opener.dataset.dmoDialog);
    if (dialog && typeof dialog.showModal === "function") {
      // Aberta a partir das «Opções», o dropdown fecha por trás da janela.
      // Um cartão dobrável (o Histórico de produções da ficha) fica aberto:
      // fechado, escondia a tabela e a janela que lá está.
      const menu = opener.closest("details[open]");
      if (menu && !menu.matches(".dmo-card-dobravel > details")) menu.removeAttribute("open");
      // Um <a href="#id"> que abre a janela não segue o link: a âncora nova
      // dispararia o popstate, que recarrega a página (Eliminar Job On).
      event.preventDefault();
      dialog.showModal();
      dialog.querySelector("textarea, input:not([type=hidden]), select")?.focus();
      return;
    }
    const janela = event.target.closest("dialog.dmo-dialog[open]");
    if (janela && (event.target === janela || event.target.closest("[data-dmo-dialog-close]"))) janela.close();
  });

  // A FICHA DA LINHA (Diogo, 21/10/2026: «para opções entrar na ficha do
  // reparador clicando duas vezes»; nas tabelas nunca há botões repetidos).
  // Uma linha com data-dmo-ficha="id" abre a janela com esse id ao clique
  // (a regra da casa: um clique abre, 17/10/2026; o duplo clique também) ou
  // ao Enter; as ações da linha vivem lá dentro.
  const abrirFicha = (linha) => {
    const ficha = document.getElementById(linha.dataset.dmoFicha);
    if (!ficha || typeof ficha.showModal !== "function") return;
    ficha.showModal();
    ficha.querySelector("input:not([type=hidden]), select, textarea")?.focus();
  };
  document.addEventListener("click", (event) => {
    const linha = event.target.closest && event.target.closest("[data-dmo-ficha]");
    if (linha && !linha.closest("dialog") && !document.querySelector("dialog.dmo-dialog[open]")) abrirFicha(linha);
  });
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Enter" || !event.target.matches || !event.target.matches("[data-dmo-ficha]")) return;
    event.preventDefault();
    abrirFicha(event.target);
  });
  // O campo que também filtra (data-dmo-filtra="id da tabela"): escrever
  // mostra só as linhas que contêm o texto (sem acentos nem maiúsculas).
  const semAcentos = (texto) => (texto || "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().trim();
  document.addEventListener("input", (event) => {
    const campo = event.target.closest && event.target.closest("[data-dmo-filtra]");
    if (campo) {
      const tabela = document.getElementById(campo.dataset.dmoFiltra);
      const procura = semAcentos(campo.value);
      tabela?.querySelectorAll("tbody tr").forEach((linha) => {
        linha.hidden = procura !== "" && !semAcentos(linha.textContent).includes(procura);
      });
    }
  });
  // O texto que mostra o valor de um campo (data-dmo-espelho="id do campo"),
  // por exemplo o nome na linha quando muda na ficha.
  document.addEventListener("change", (event) => {
    if (!event.target.id) return;
    document.querySelectorAll(`[data-dmo-espelho="${event.target.id}"]`).forEach((alvo) => { alvo.textContent = event.target.value.trim(); });
  });

  // Um formulário que devolve um ficheiro (data-dmo-dialog-fecha-ao-enviar,
  // o email do «Enviar» do Resumo): a página fica onde está, por isso a
  // janela fecha depois de o browser mandar o pedido.
  document.addEventListener("submit", (event) => {
    const form = event.target.closest && event.target.closest("form[data-dmo-dialog-fecha-ao-enviar]");
    const janela = form && form.closest("dialog.dmo-dialog[open]");
    if (janela) setTimeout(() => janela.close(), 0);
  });

  // Uma janela que tem de abrir com a página (data-dmo-dialog-aberta), por
  // exemplo a confirmação de eliminar ou um erro a corrigir. Com
  // data-dmo-dialog-voltar, fechar volta a esse endereço.
  document.querySelectorAll("dialog.dmo-dialog[data-dmo-dialog-aberta=\"1\"]").forEach((dialog) => {
    if (typeof dialog.showModal !== "function") return;
    if (dialog.dataset.dmoDialogVoltar) {
      dialog.addEventListener("close", () => { window.location.href = dialog.dataset.dmoDialogVoltar; });
    }
    dialog.showModal();
    (dialog.querySelector("textarea, input:not([type=hidden]), select") || dialog.querySelector(".dmo-dialog-actions > *"))?.focus();
  });

  // A dica de ajuda (_DmoHelp) dentro de um <summary>: tocar no círculo
  // mostra o texto sem abrir nem fechar a secção. Aí a dica não é focável
  // (o browser avisa de elementos interativos no <summary>), por isso o toque
  // abre-a com uma classe, e qualquer outro toque fecha-a.
  document.addEventListener("click", (event) => {
    const help = event.target.closest(".dmo-help");
    document.querySelectorAll(".dmo-help.dmo-help-aberta").forEach((open) => {
      if (open !== help) open.classList.remove("dmo-help-aberta");
    });
    if (help && help.closest("summary")) {
      event.preventDefault();
      help.classList.toggle("dmo-help-aberta");
      placeHelpTip(help);
    }
  });

  // A dica fica sempre dentro do ecrã (Diogo, 17/10/2026): ao abrir, o texto
  // passa a fixed (não é cortado pelos cartões) e encosta-se ao lado onde há
  // espaço; sem espaço por baixo, abre por cima do círculo.
  const HELP_MARGIN = 8;
  function placeHelpTip(help) {
    const tip = help.querySelector(".dmo-help-tip");
    if (!tip || !help.matches(":hover, :focus, :focus-within, .dmo-help-aberta, summary:focus-visible .dmo-help")) return;
    tip.style.position = "fixed";
    tip.style.right = "auto";
    tip.style.maxWidth = `min(320px, ${window.innerWidth - HELP_MARGIN * 2}px)`;
    const mark = help.getBoundingClientRect();
    const width = tip.offsetWidth;
    const height = tip.offsetHeight;
    const left = Math.max(HELP_MARGIN, Math.min(mark.left - 8, window.innerWidth - HELP_MARGIN - width));
    let top = mark.bottom + 6;
    if (top + height > window.innerHeight - HELP_MARGIN && mark.top - 6 - height >= HELP_MARGIN) {
      top = mark.top - 6 - height;
    }
    tip.style.left = `${left}px`;
    tip.style.top = `${top}px`;
  }
  ["mouseover", "focusin"].forEach((type) => {
    document.addEventListener(type, (event) => {
      const help = event.target.closest?.(".dmo-help")
        ?? (event.target.matches?.("summary") ? event.target.querySelector(".dmo-help") : null);
      if (help) placeHelpTip(help);
    });
  });
  window.addEventListener("scroll", () => {
    document.querySelectorAll(".dmo-help").forEach(placeHelpTip);
  }, { capture: true, passive: true });
  window.addEventListener("resize", () => {
    document.querySelectorAll(".dmo-help").forEach(placeHelpTip);
  });
})();


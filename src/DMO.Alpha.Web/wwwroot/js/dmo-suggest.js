/*
 * dmo-suggest.js — sugestões ao escrever (DMO-044)
 *
 * O Owner (2026-10-16): «ao procurar deveria mostrar com base no que escrevo
 * e começar a filtrar, aliás isso deveria afetar todos os campos» e «a
 * comparação de peso mostra um dropdown enorme, devia funcionar como filtro».
 * Um só componente, dois modos:
 *
 *   <input data-dmo-suggest="producoes|referencias|numeros|ferramentas|reparadores|referencias_bq"
 *          [data-dmo-suggest-tipo="cm|mf|bq"] [data-dmo-suggest-submit]>
 *     A partir de 1 carácter (2 nos reparadores), 250 ms depois da última
 *     tecla, pede ao servidor (GET /Sugestoes) no máximo 10 sugestões. As
 *     referências encontram-se pelo início (Diogo, 18/10/2026: «0» sugere a
 *     0132T173), por isso um carácter chega. Escolher preenche o campo e,
 *     com data-dmo-suggest-submit, envia o formulário.
 *
 *   <select data-dmo-filter>
 *     Escrever filtra as opções que a página já tem, sem ir ao servidor. O
 *     select original continua no formulário e recebe a escolha (com o
 *     evento change, para os auto-submits continuarem a funcionar).
 *     Se a página der ao select uma fonte (select.dmoSource = function (termo)
 *     que devolve uma Promise de { items: [{ value, label }], empty, note }),
 *     as opções vêm dessa fonte em vez das que a página tem (o Job On).
 *
 * Progressive enhancement: sem este ficheiro os campos e os selects nativos
 * funcionam como antes.
 */
(function () {
  "use strict";

  // O mínimo de caracteres de cada fonte; os nomes dos reparadores
  // procuram-se a meio do nome e pedem 2 (o mesmo que o servidor).
  var MIN_CHARS = 1;
  var MIN_CHARS_FONTE = { reparadores: 2 };
  var WAIT_MS = 250;
  var GAP = 4;        // distância entre o campo e a lista, e da lista à borda do ecrã
  var MIN_ROOM = 120; // altura mínima da lista, mesmo num ecrã apertado
  var counter = 0;

  function normalize(text) {
    return String(text || "").toLocaleUpperCase("pt-PT").normalize("NFD").replace(/[̀-ͯ]/g, "");
  }

  /* Uma lista partilhada pelos dois modos: abrir, desenhar, navegar, escolher. */
  function createList(field, onPick, pickFirstOnEnter, waiting) {
    var wrapper = document.createElement("div");
    wrapper.className = "dmo-suggest";
    field.parentNode.insertBefore(wrapper, field);
    wrapper.appendChild(field);

    var list = document.createElement("ul");
    list.className = "dmo-suggest-list";
    list.id = "dmo-suggest-" + (++counter);
    list.setAttribute("role", "listbox");
    list.hidden = true;
    wrapper.appendChild(list);

    field.setAttribute("role", "combobox");
    field.setAttribute("aria-autocomplete", "list");
    field.setAttribute("aria-expanded", "false");
    field.setAttribute("aria-controls", list.id);
    field.setAttribute("autocomplete", "off");

    var items = [];
    var active = -1;

    // A lista é fixed (os cartões cortam o que sai deles): segue o campo.
    // Cabe abaixo do campo até 320 px (ou meio ecrã) e nunca sai do ecrã:
    // com pouco espaço em baixo, abre para cima se houver mais espaço lá.
    function place() {
      var rect = field.getBoundingClientRect();
      var cap = Math.min(320, window.innerHeight / 2);
      var want = Math.min(list.scrollHeight, cap);
      var below = window.innerHeight - rect.bottom - GAP * 2;
      var above = rect.top - GAP * 2;
      var up = below < want && above > below;
      var room = Math.max(MIN_ROOM, Math.min(cap, up ? above : below));
      list.style.left = rect.left + "px";
      list.style.width = rect.width + "px";
      list.style.maxHeight = room + "px";
      list.style.top = up
        ? (rect.top - GAP - Math.min(list.scrollHeight, room)) + "px"
        : (rect.bottom + GAP) + "px";
    }

    // Ao abrir no fundo da página (o Diogo, 17/10/2026: a lista dos lotes do
    // Job On ficava cortada), a página ganha o espaço que falta no fim e
    // desce o suficiente para a lista caber inteira por baixo do campo, sem
    // tirar o campo do ecrã. O espaço fica até sair da página.
    function makeRoom() {
      var rect = field.getBoundingClientRect();
      var want = Math.min(list.scrollHeight, Math.min(320, window.innerHeight / 2));
      var shift = Math.min(want - (window.innerHeight - rect.bottom - GAP * 2), rect.top - GAP * 2);
      if (shift <= 0) return;
      var spacer = document.getElementById("dmo-suggest-room");
      if (!spacer) {
        spacer = document.createElement("div");
        spacer.id = "dmo-suggest-room";
        spacer.setAttribute("aria-hidden", "true");
        // No fim do conteúdo da página (o <main>), para as colunas ao lado crescerem com ele.
        (field.closest("main") || document.body).appendChild(spacer);
      }
      var root = document.documentElement;
      var missing = window.scrollY + window.innerHeight + shift - root.scrollHeight;
      if (missing > 0) spacer.style.height = (spacer.offsetHeight + Math.ceil(missing)) + "px";
      window.scrollBy(0, shift);
    }

    function close() {
      window.removeEventListener("scroll", place, true);
      window.removeEventListener("resize", place);
      list.hidden = true;
      field.setAttribute("aria-expanded", "false");
      field.removeAttribute("aria-activedescendant");
      active = -1;
    }

    function highlight(index) {
      var nodes = list.children;
      if (active >= 0 && nodes[active]) nodes[active].classList.remove("active");
      active = index;
      if (active >= 0 && nodes[active]) {
        nodes[active].classList.add("active");
        field.setAttribute("aria-activedescendant", nodes[active].id);
        nodes[active].scrollIntoView({ block: "nearest" });
      } else {
        field.removeAttribute("aria-activedescendant");
      }
    }

    function render(newItems, emptyText, note) {
      items = newItems;
      list.replaceChildren();
      active = -1;
      if (!items.length && !emptyText && !note) { close(); return; }
      items.forEach(function (item, index) {
        var li = document.createElement("li");
        li.id = list.id + "-" + index;
        li.setAttribute("role", "option");
        li.textContent = item.label;
        if (item.selected) li.setAttribute("aria-selected", "true");
        // mousedown (antes do blur): a escolha acontece antes de o campo fechar a lista.
        li.addEventListener("mousedown", function (event) {
          event.preventDefault();
          onPick(item);
          close();
        });
        list.appendChild(li);
      });
      if (!items.length && emptyText) {
        var empty = document.createElement("li");
        empty.className = "dmo-suggest-empty";
        empty.textContent = emptyText;
        list.appendChild(empty);
      }
      if (note) {
        var extra = document.createElement("li");
        extra.className = "dmo-suggest-empty";
        extra.textContent = note;
        list.appendChild(extra);
      }
      var opening = list.hidden;
      list.hidden = false;
      makeRoom();
      place();
      if (opening) {
        window.addEventListener("scroll", place, true);
        window.addEventListener("resize", place);
      }
      field.setAttribute("aria-expanded", "true");
    }

    field.addEventListener("keydown", function (event) {
      if (list.hidden) return;
      if (event.key === "ArrowDown") {
        event.preventDefault();
        highlight(Math.min(active + 1, items.length - 1));
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        highlight(Math.max(active - 1, 0));
      } else if (event.key === "Enter" && (active >= 0 || (pickFirstOnEnter && items.length && !(waiting && waiting())))) {
        // No filtro de um select, Enter escolhe a primeira opção filtrada,
        // mas não a da lista anterior enquanto a procura não responde.
        event.preventDefault();
        onPick(items[active >= 0 ? active : 0]);
        close();
      } else if (event.key === "Escape") {
        event.preventDefault();
        close();
      }
    });
    field.addEventListener("blur", close);

    return { render: render, close: close, isOpen: function () { return !list.hidden; } };
  }

  /* ---------- modo 1: campo de texto com sugestões do servidor ---------- */
  function enhanceInput(input) {
    var request = 0;
    var timer = null;
    var list = createList(input, function (item) {
      input.value = item.value;
      input.dispatchEvent(new Event("input", { bubbles: true }));
      if (input.hasAttribute("data-dmo-suggest-submit") && input.form) {
        if (input.form.requestSubmit) input.form.requestSubmit();
        else input.form.submit();
      }
    });

    // Sem «Procurar» (Diogo, 17/10/2026), o Enter num campo que procura tem
    // de submeter mesmo num formulário com vários campos e sem botão, onde o
    // browser não submete sozinho. Quando o Enter escolheu uma sugestão, a
    // lista já submeteu.
    if (input.hasAttribute("data-dmo-suggest-submit")) {
      input.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" || event.defaultPrevented || !input.form) return;
        event.preventDefault();
        if (input.form.requestSubmit) input.form.requestSubmit();
        else input.form.submit();
      });
    }

    function fetchNow() {
      var term = input.value.trim();
      var mine = ++request;
      var fonte = input.getAttribute("data-dmo-suggest");
      if (term.length < (MIN_CHARS_FONTE[fonte] || MIN_CHARS)) { list.close(); return; }
      var query = new URLSearchParams({ fonte: fonte, q: term });
      var tipo = input.getAttribute("data-dmo-suggest-tipo");
      if (tipo) query.set("tipo", tipo);
      fetch("/Sugestoes?" + query, { credentials: "same-origin", headers: { Accept: "application/json" } })
        .then(function (response) { return response.ok ? response.json() : []; })
        .then(function (data) {
          // Só a resposta da última tecla conta, e só com o campo ainda ativo.
          if (mine !== request || document.activeElement !== input) return;
          list.render(Array.isArray(data) ? data : [], "Sem sugestões");
        })
        .catch(function () { if (mine === request) list.close(); });
    }

    input.addEventListener("input", function (event) {
      if (!event.isTrusted) return;
      clearTimeout(timer);
      ++request; // invalida uma resposta pendente já durante a espera
      timer = setTimeout(fetchNow, WAIT_MS);
    });
  }

  /* ---------- modo 2: select longo transformado em filtro local ---------- */
  function enhanceSelect(select) {
    var input = document.createElement("input");
    input.type = "text";
    input.className = "dmo-suggest-input dmo-suggest-combo";
    input.id = (select.id || ("dmo-filter-" + (++counter))) + "-filtro";
    var label = select.id ? document.querySelector('label[for="' + select.id + '"]') : null;
    if (label) label.setAttribute("for", input.id);
    else if (select.getAttribute("aria-label")) input.setAttribute("aria-label", select.getAttribute("aria-label"));
    if (select.style.width) input.style.width = select.style.width;

    select.parentNode.insertBefore(input, select);
    select.classList.add("dmo-suggest-native");
    select.tabIndex = -1;
    select.setAttribute("aria-hidden", "true");

    var list = createList(input, function (item) {
      // Uma sugestão da fonte remota passa a ser opção do select.
      if (!Array.prototype.some.call(select.options, function (o) { return o.value === item.value; })) {
        select.add(new Option(item.label, item.value));
      }
      select.value = item.value;
      sync();
      select.dispatchEvent(new Event("change", { bubbles: true }));
    }, true, function () { return waiting; });
    // O select original vive dentro do mesmo invólucro (o formulário não muda).
    input.parentNode.appendChild(select);

    function placeholderOption() {
      return Array.prototype.find.call(select.options, function (o) { return o.value === ""; });
    }

    function sync() {
      var chosen = select.selectedOptions[0];
      var empty = placeholderOption();
      input.value = chosen && chosen.value !== "" ? chosen.text.trim() : "";
      input.placeholder = empty ? empty.text.trim() : "Escreva para filtrar";
      input.disabled = select.disabled;
    }

    var remoteRequest = 0;
    var remoteTimer = null;
    // Até a fonte responder à última tecla, a lista à vista é a da procura
    // anterior: o Enter não escolhe a primeira linha dela (a ferramenta de
    // outra referência ou de outro lote).
    var waiting = false;
    function showRemote(filter, wait) {
      var mine = ++remoteRequest;
      waiting = true;
      clearTimeout(remoteTimer);
      remoteTimer = setTimeout(function () {
        Promise.resolve(select.dmoSource(filter.trim()))
          .then(function (result) {
            // Só a resposta da última tecla conta, e só com o campo ainda ativo.
            if (mine !== remoteRequest || document.activeElement !== input) return;
            waiting = false;
            var items = (result && result.items) || [];
            var empty = placeholderOption();
            // Com o campo vazio, a primeira linha limpa a escolha.
            if (!filter.trim() && select.value !== "" && empty) {
              items = [{ value: "", label: empty.text.trim() }].concat(items);
            }
            items.forEach(function (item) { item.selected = item.value !== "" && item.value === select.value; });
            list.render(items, result && result.empty, result && result.note);
          })
          .catch(function () {
            if (mine !== remoteRequest) return;
            waiting = false;
            list.render([], "Não foi possível procurar. Tente de novo.");
          });
      }, wait ? WAIT_MS : 0);
    }

    function show(filter, typing) {
      if (typeof select.dmoSource === "function") { showRemote(filter, typing); return; }
      var words = normalize(filter).split(/\s+/).filter(Boolean);
      var items = [];
      Array.prototype.forEach.call(select.options, function (option) {
        if (option.disabled && option.value === "") return;
        var text = option.text.trim();
        if (option.value === "" && words.length) return;
        var haystack = normalize(text);
        if (words.every(function (w) { return haystack.indexOf(w) >= 0; })) {
          items.push({ value: option.value, label: text, selected: option.selected && option.value !== "" });
        }
      });
      list.render(items, "Sem resultados para «" + filter.trim() + "»");
    }

    input.addEventListener("focus", function () { if (quiet) return; input.select(); show(""); });
    input.addEventListener("click", function () {
      // Depois de focar(), o campo já tinha o foco e o clique não passa pelo
      // focus: seleciona aqui, para escrever substituir a escolha.
      if (input.dataset.dmoSuggestSelecionar) { delete input.dataset.dmoSuggestSelecionar; input.select(); }
      if (!list.isOpen()) show("");
    });
    input.addEventListener("input", function () { show(input.value, true); });
    // O campo só filtra o select: o Enter nunca envia o formulário. Com a
    // lista fechada (a procura ainda não respondeu), o browser enviava a
    // folha do Job On sem a ferramenta escrita (deep sweep, 18/10/2026).
    input.addEventListener("keydown", function (event) {
      if (event.key === "Enter") event.preventDefault();
    });
    input.addEventListener("blur", function () { delete input.dataset.dmoSuggestSelecionar; ++remoteRequest; waiting = false; clearTimeout(remoteTimer); sync(); });
    select.addEventListener("change", sync);
    // O Job On reconstrói as opções dos seletores CM/MF/BQ ao filtrar por máquina.
    new MutationObserver(sync).observe(select, { childList: true, subtree: true, attributes: true, attributeFilter: ["disabled"] });
    sync();
  }

  function init(root) {
    (root || document).querySelectorAll("input[data-dmo-suggest]").forEach(function (input) {
      if (input.dataset.dmoSuggestReady) return;
      input.dataset.dmoSuggestReady = "1";
      enhanceInput(input);
    });
    (root || document).querySelectorAll("select[data-dmo-filter]").forEach(function (select) {
      if (select.dataset.dmoSuggestReady || select.multiple) return;
      select.dataset.dmoSuggestReady = "1";
      enhanceSelect(select);
    });
  }

  // O foco devolvido por uma página depois de trocar a zona do campo (o
  // Peso, ao escolher a comparação): volta ao campo de filtrar sem abrir a
  // lista, que o operador acabou de fechar ao escolher. O texto fica
  // selecionado, como num foco normal, para escrever substituir a escolha.
  var quiet = false;
  function focar(field) {
    if (!field) return;
    quiet = true;
    try { field.focus({ preventScroll: true }); } finally { quiet = false; }
    if (typeof field.select === "function") field.select();
    if (field.dataset) field.dataset.dmoSuggestSelecionar = "1";
  }

  window.DMOSuggest = { init: init, focar: focar };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", function () { init(); });
  else init();
})();

/*
 * Referências em maiúsculas enquanto se escreve: <input data-dmo-maiusculas>.
 * «5447t173» fica logo «5447T173». Durante a composição do teclado (Android)
 * espera pelo fim da palavra, para não duplicar letras.
 */
(function () {
  "use strict";

  function upper(field) {
    var value = field.value;
    var wanted = value.toLocaleUpperCase("pt-PT");
    if (value === wanted) return;
    var start = field.selectionStart;
    var end = field.selectionEnd;
    field.value = wanted;
    try { field.setSelectionRange(start, end); } catch (ignored) { /* tipos sem cursor */ }
  }

  function handle(event) {
    var field = event.target;
    if (!field || !field.hasAttribute || !field.hasAttribute("data-dmo-maiusculas")) return;
    if (event.isComposing) return;
    upper(field);
  }

  document.addEventListener("input", handle);
  document.addEventListener("compositionend", handle);
  document.addEventListener("change", handle);
})();

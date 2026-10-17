(function () {
  const SELECTED = "selected";

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
      // ação: as marcadas data-dmo-seleciona (a lista do dia do Job On, onde
      // a produção escolhida se retira das datas no calendário) e as que têm
      // um bloco de ações fora (data-dmo-for, o «Selecionar ferramenta»).
      // O data-dmo-abrir-clique da procura do Resumo deixa de ser preciso.
      row.addEventListener("click", (event) => {
        // Um controlo próprio dentro da linha faz o seu trabalho.
        const control = event.target.closest("button,select,textarea,a");
        if (control && control !== row) return;
        selectRow(list, row);
        if (!selectsOnly(list) && row.dataset.dmoOpen) openRow(list, row);
      });
      row.addEventListener("dblclick", () => {
        if (selectsOnly(list)) openRow(list, row);
      });
      // O ENTER ABRE (regra 2e do contrato da casa). Estava ao contrário: o Enter
      // selecionava e SÓ Ctrl+Enter abria, que não é comportamento de ninguém.
      // Selecionar antes de abrir não é trabalho repetido — é o que diz ao
      // operador qual linha vai abrir, e é o que deixa o ecrã honesto se ele
      // desistir a meio.
      row.addEventListener("keydown", (event) => {
        if (event.key !== "Enter") return;
        // Não interceptar Enter de um controlo nativo dentro da linha.
        if (event.target !== row && event.target.closest("input,button,select,textarea,a")) return;
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

  // A procura ao escrever com resultados numa lista (Diogo, 17/10/2026):
  // <input data-dmo-procura="/Pagina?handler=X" data-dmo-procura-alvo="id">.
  // 250 ms depois da última tecla, pede ao servidor a lista (HTML) para o
  // texto escrito e mostra-a no alvo; campo vazio, alvo vazio. A lista que
  // chega é uma lista da casa: um clique ou Enter abre.
  document.querySelectorAll("[data-dmo-procura]").forEach((field) => {
    const target = document.getElementById(field.dataset.dmoProcuraAlvo || "");
    if (!target) return;
    let timer = 0;
    let latest = 0;
    field.setAttribute("autocomplete", "off");
    field.addEventListener("input", () => {
      clearTimeout(timer);
      // Escrever de novo volta ao primeiro passo: o dropdown do segundo
      // passo (data-dmo-procura-passo2) era da referência anterior.
      if (field.form) field.form.querySelectorAll("[data-dmo-procura-passo2]").forEach((step) => step.remove());
      const term = field.value.trim();
      if (!term) {
        latest++;
        target.replaceChildren();
        return;
      }
      timer = setTimeout(() => {
        const request = ++latest;
        const url = field.dataset.dmoProcura + (field.dataset.dmoProcura.includes("?") ? "&" : "?") + "q=" + encodeURIComponent(term);
        fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "fetch" } })
          .then((response) => (response.ok ? response.text() : ""))
          .then((html) => {
            if (request !== latest) return;
            target.innerHTML = html;
            target.querySelectorAll("[data-dmo-list]").forEach(bindList);
          })
          .catch(() => {});
      }, 250);
    });
    // data-dmo-procura-valor="id": um clique numa linha ESCOLHE (Diogo,
    // 17/10/2026): o campo escondido recebe o data-id, o campo mostra o
    // data-dmo-rotulo e a lista fecha. Escrever de novo desfaz a escolha.
    // Num form data-dmo-procura-submete, quando todos os campos escondidos
    // das procuras estão escolhidos, o form submete sozinho.
    const valueField = document.getElementById(field.dataset.dmoProcuraValor || "");
    if (valueField) {
      field.addEventListener("input", () => { valueField.value = ""; });
      target.addEventListener("dmo:list-select", (event) => {
        const row = event.detail && event.detail.row;
        if (!row) return;
        valueField.value = row.dataset.id || "";
        field.value = row.dataset.dmoRotulo || row.textContent.trim();
        latest++;
        target.replaceChildren();
        valueField.dispatchEvent(new Event("change", { bubbles: true }));
        const form = field.form;
        if (form && form.hasAttribute("data-dmo-procura-submete")) {
          const chosen = Array.from(form.querySelectorAll("[data-dmo-procura-valor]"))
            .map((input) => document.getElementById(input.dataset.dmoProcuraValor));
          if (chosen.every((input) => input && input.value)) form.requestSubmit();
        }
      });
    }
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
    if (select && select.value) window.location.href = select.value;
  });

  // Os filtros que procuram ao escrever (Diogo, 17/10/2026: sem botão
  // «Procurar»): <form method="get" data-dmo-ao-escrever="idDosResultados">.
  // 350 ms depois da última tecla num campo de texto, ou logo ao mudar um
  // dropdown ou carregar em Enter, pede a mesma página com os filtros e troca
  // só o bloco dos resultados, sem recarregar. O endereço fica com os
  // filtros, para o voltar e o recarregar mostrarem o mesmo.
  document.querySelectorAll("form[data-dmo-ao-escrever]").forEach((form) => {
    const id = form.dataset.dmoAoEscrever;
    let timer = 0;
    let latest = 0;
    function procurar() {
      clearTimeout(timer);
      const request = ++latest;
      const params = new URLSearchParams(new FormData(form));
      const url = (form.getAttribute("action") || window.location.pathname) + "?" + params.toString();
      fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "fetch" } })
        .then((response) => (response.ok ? response.text() : null))
        .then((html) => {
          if (!html || request !== latest) return;
          const fresh = new DOMParser().parseFromString(html, "text/html").getElementById(id);
          const current = document.getElementById(id);
          if (!fresh || !current) return;
          const node = document.importNode(fresh, true);
          current.replaceWith(node);
          if (node.matches("[data-dmo-list]")) bindList(node);
          node.querySelectorAll("[data-dmo-list]").forEach(bindList);
          window.history.replaceState(null, "", url);
        })
        .catch(() => {});
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
  });

  // O dia já escolhido do calendário partilhado: um clique não recarrega
  // (já está escolhido) e o duplo clique tira a seleção (Diogo, 17/10/2026).
  // Antes, o evento cancelável dmo:calendar-dblclick deixa a página fazer
  // a sua ação no dia (o Job On retira a produção escolhida das datas).
  document.querySelectorAll("[data-dmo-calendar-unselect]").forEach((day) => {
    day.addEventListener("click", (event) => event.preventDefault());
    day.addEventListener("dblclick", (event) => {
      event.preventDefault();
      const proceed = day.dispatchEvent(new CustomEvent("dmo:calendar-dblclick", {
        bubbles: true,
        cancelable: true
      }));
      if (proceed) window.location.href = day.dataset.dmoCalendarUnselect;
    });
  });

  // Escolher uma opção num dropdown aplica logo, sem botão «Aplicar» ao lado
  // (Diogo, 17/10/2026). O requestSubmit mantém a validação (required) e os
  // handlers de submit da página. Com data-dmo-autosubmit="valor", a opção
  // vazia («Produção», «Selecionar…») não submete. Uma data também aplica
  // logo, quando fica completa, e um mês também (os Históricos do Job On e
  // das Boquilhas, sem «Procurar»).
  document.querySelectorAll(":is(select,input[type=date],input[type=month])[data-dmo-autosubmit]").forEach((select) => {
    select.addEventListener("change", () => {
      if (select.dataset.dmoAutosubmit === "valor" && !select.value) return;
      if (select.form) select.form.requestSubmit();
    });
  });

  // Editar no próprio valor (Diogo, 17/10/2026: «para editar, clica-se no
  // valor da linha e muda-se ali mesmo»): um input.dmo-valor[data-dmo-valor]
  // dentro de um form grava ao sair do campo, sem botão nem recarregar. Envia
  // o token, os campos escondidos da mesma linha e o próprio campo; o
  // servidor responde { ok, valor, aviso, resumo }. Enter grava, Esc desfaz.
  // Sem JavaScript, Enter submete o form inteiro.
  document.querySelectorAll("input[data-dmo-valor]").forEach((input) => {
    const form = input.form;
    if (!form) return;
    input.dataset.dmoValorGuardado = input.value;
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") { event.preventDefault(); input.blur(); }
      if (event.key === "Escape") { input.value = input.dataset.dmoValorGuardado; input.blur(); }
    });
    input.addEventListener("change", async () => {
      const body = new FormData();
      const token = form.querySelector("input[name=__RequestVerificationToken]");
      if (token) body.append(token.name, token.value);
      const row = input.closest("tr") || input.parentElement;
      row.querySelectorAll("input[type=hidden]:not([name=__RequestVerificationToken])").forEach((hidden) => body.append(hidden.name, hidden.value));
      body.append(input.name, input.value);
      const aviso = form.querySelector("[data-dmo-valor-aviso]");
      const resumo = form.querySelector("[data-dmo-valor-resumo]");
      try {
        const response = await fetch(form.action, {
          method: "POST", body, credentials: "same-origin", headers: { Accept: "application/json" }
        });
        const data = await response.json();
        if (data.ok) {
          input.value = data.valor ?? input.value;
          input.dataset.dmoValorGuardado = input.value;
          input.removeAttribute("aria-invalid");
          input.classList.add("dmo-valor-gravado");
          setTimeout(() => input.classList.remove("dmo-valor-gravado"), 1200);
        } else {
          input.value = input.dataset.dmoValorGuardado;
          input.setAttribute("aria-invalid", "true");
        }
        if (aviso) { aviso.textContent = data.ok ? "" : (data.aviso || ""); aviso.hidden = !!data.ok || !data.aviso; }
        if (resumo && data.resumo) resumo.textContent = data.resumo;
      } catch {
        form.requestSubmit();
      }
    });
  });

  // Copiar um link sem o mostrar (Diogo, 17/10/2026): data-dmo-copiar="url";
  // o rótulo [data-dmo-copiar-label] diz «Copiado» por momentos.
  document.querySelectorAll("[data-dmo-copiar]").forEach((button) => {
    button.addEventListener("click", () => {
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
  });

  // A janela pequena (Diogo, 17/10/2026): data-dmo-dialog="id" abre o
  // <dialog class="dmo-dialog"> com esse id; data-dmo-dialog-close fecha-o.
  document.querySelectorAll("[data-dmo-dialog]").forEach((opener) => {
    const dialog = document.getElementById(opener.dataset.dmoDialog);
    if (!dialog || typeof dialog.showModal !== "function") return;
    opener.addEventListener("click", () => {
      // Aberta a partir das «Opções», o dropdown fecha por trás da janela.
      opener.closest("details[open]")?.removeAttribute("open");
      dialog.showModal();
      dialog.querySelector("textarea, input:not([type=hidden]), select")?.focus();
    });
    dialog.addEventListener("click", (event) => {
      if (event.target === dialog || event.target.closest("[data-dmo-dialog-close]")) dialog.close();
    });
  });

  // Uma janela que tem de abrir com a página (data-dmo-dialog-aberta), por
  // exemplo a confirmação de eliminar ou um erro a corrigir. Com
  // data-dmo-dialog-voltar, fechar volta a esse endereço.
  document.querySelectorAll("dialog.dmo-dialog[data-dmo-dialog-aberta=\"1\"]").forEach((dialog) => {
    if (typeof dialog.showModal !== "function") return;
    if (!dialog.matches("[data-dmo-dialog]") && !document.querySelector(`[data-dmo-dialog="${dialog.id}"]`)) {
      dialog.addEventListener("click", (event) => {
        if (event.target === dialog || event.target.closest("[data-dmo-dialog-close]")) dialog.close();
      });
    }
    if (dialog.dataset.dmoDialogVoltar) {
      dialog.addEventListener("close", () => { window.location.href = dialog.dataset.dmoDialogVoltar; });
    }
    dialog.showModal();
    (dialog.querySelector("textarea, input:not([type=hidden]), select") || dialog.querySelector(".dmo-dialog-actions > *"))?.focus();
  });

  document.querySelectorAll("[data-dmo-calendar]").forEach((calendar) => {
    calendar.addEventListener("click", (event) => {
      const day = event.target.closest("[data-date]");
      if (!day || day.disabled) return;
      calendar.querySelectorAll("[data-date]").forEach((item) => {
        item.classList.toggle(SELECTED, item === day);
        item.setAttribute("aria-pressed", item === day ? "true" : "false");
      });
      calendar.dispatchEvent(new CustomEvent("dmo:date-select", {
        bubbles: true,
        detail: { date: day.dataset.date }
      }));
    });
  });
  // A dica de ajuda (_DmoHelp) dentro de um <summary>: tocar no triângulo
  // mostra o texto sem abrir nem fechar a secção.
  document.addEventListener("click", (event) => {
    const help = event.target.closest(".dmo-help");
    if (help && help.closest("summary")) event.preventDefault();
  });

  // A dica fica sempre dentro do ecrã (Diogo, 17/10/2026): ao abrir, o texto
  // passa a fixed (não é cortado pelos cartões) e encosta-se ao lado onde há
  // espaço; sem espaço por baixo, abre por cima do triângulo.
  const HELP_MARGIN = 8;
  function placeHelpTip(help) {
    const tip = help.querySelector(".dmo-help-tip");
    if (!tip || !help.matches(":hover, :focus, :focus-within")) return;
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
      const help = event.target.closest?.(".dmo-help");
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


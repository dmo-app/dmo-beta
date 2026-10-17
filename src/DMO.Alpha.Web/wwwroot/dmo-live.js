/*
 * dmo-live.js — utilidades vivas comuns (F0, design pass §2.1)
 * Camada comum de comportamento vivo disponível em qualquer página, sem tocar
 * na persistência. Progressive enhancement: SEM este ficheiro, tudo funciona
 * pelos POSTs nativos.
 *
 * API: DMOLive.toast(message, opts?) · DMOLive.escapeHtml(v) ·
 *      DMOLive.el(tag, className, text) · DMOLive.swapHTML(container, html) ·
 *      DMOLive.debounce(fn, wait) · DMOLive.formatNumber(value, decimals)
 * Nada aqui calcula física industrial: o cálculo é sempre do backend.
 */
(function (global) {
  "use strict";

  /* ---------- toast ---------- */
  var region = null;

  function ensureRegion() {
    if (region && document.body.contains(region)) return region;
    region = document.createElement("div");
    region.className = "dmo-live-region";
    region.setAttribute("aria-live", "polite");
    region.setAttribute("role", "status");
    document.body.appendChild(region);
    return region;
  }

  function injectStylesOnce() {
    if (document.getElementById("dmo-live-styles")) return;
    var style = document.createElement("style");
    style.id = "dmo-live-styles";
    // Classes com prefixo dmo-live: o design system canónico já define
    // .dmo-toast (revisão F-5) — o toast vivo não colide com esse namespace.
    style.textContent =
      ".dmo-live-region{position:fixed;right:16px;bottom:16px;display:flex;flex-direction:column;gap:8px;z-index:9999;pointer-events:none}" +
      ".dmo-live-toast{background:#1f3a5f;color:#fff;padding:10px 16px;border-radius:8px;box-shadow:0 6px 24px rgba(0,0,0,.18);font-size:14px;max-width:420px;opacity:0;transform:translateY(8px);transition:opacity .18s ease,transform .18s ease;pointer-events:auto}" +
      ".dmo-live-toast.dmo-live-toast-visible{opacity:1;transform:none}" +
      ".dmo-live-toast[data-kind='info']{background:#1f3a5f}" +
      ".dmo-live-toast[data-kind='warn']{background:#8a5a00}" +
      ".dmo-live-toast[data-kind='error']{background:#8a1c1c}";
    document.head.appendChild(style);
  }

  function toast(message, opts) {
    opts = opts || {};
    injectStylesOnce();
    var host = ensureRegion();
    var node = document.createElement("div");
    node.className = "dmo-live-toast";
    node.setAttribute("data-kind", opts.kind || "info");
    node.textContent = message;
    host.appendChild(node);
    requestAnimationFrame(function () { node.classList.add("dmo-live-toast-visible"); });
    var life = typeof opts.timeout === "number" ? opts.timeout : 3500;
    setTimeout(function () {
      node.classList.remove("dmo-toast-visible");
      setTimeout(function () { if (node.parentNode) node.parentNode.removeChild(node); }, 250);
    }, life);
    return node;
  }

  /* ---------- helpers estado -> HTML (funções puras) ---------- */
  function escapeHtml(value) {
    if (value === null || value === undefined) return "";
    return String(value)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined && text !== null) node.textContent = text;
    return node;
  }

  function swapHTML(container, html) {
    if (!container) return;
    container.innerHTML = html; // html já escapado por quem compõe (funções puras)
  }

  function debounce(fn, wait) {
    var timer = null;
    return function () {
      var self = this, args = arguments;
      if (timer) clearTimeout(timer);
      timer = setTimeout(function () { timer = null; fn.apply(self, args); }, wait);
    };
  }

  function formatNumber(value, decimals) {
    if (value === null || value === undefined || isNaN(value)) return "—";
    var n = Number(value);
    var d = typeof decimals === "number" ? decimals : 2;
    return n.toLocaleString("en-US", { useGrouping: false, minimumFractionDigits: d, maximumFractionDigits: d });
  }

  /* ---------- O ABRIR CANÓNICO (regra 2 do contrato de UI da casa) ----------
   * Um clique OU o Enter ABREM (Diogo, 17/10/2026); nas listas em que a escolha
   * serve outra ação, um clique seleciona e o duplo clique abre.
   *
   * Este elo é que não existia: o 0_ASSET_CANONICAL_INTERACTIONS.js já marcava
   * a classe `selected` e já despachava `dmo:list-open`, mas NINGUÉM ouvia o
   * evento — por isso cada folha tinha escrito o seu próprio ondblclick/onkeydown
   * (12 ondblclick e 9 onkeydown espalhados pelo código, com o script canónico
   * carregado e desaproveitado). A regra 3 do contrato é justamente «zero
   * handlers reimplementados por página»: há agora UM ouvinte para tudo.
   *
   * A fronteira com a referência HTML (o Owner 2026-10-14: «o html foi feito
   * para uso local»): lá abrir era mudar um `view.jobId` em estado de browser.
   * Aqui abrir é NAVEGAR — é o servidor que recompõe a folha a partir do Job On.
   * Porta-se o comportamento, não o modelo de dados.
   *
   * Progressive enhancement: sem este ficheiro as linhas continuam a ser
   * seleção honesta e nada parte; as ações de página seguem nos links e POSTs
   * nativos, como sempre.
   */
  document.addEventListener("dmo:list-open", function (event) {
    var row = event.detail && event.detail.row;
    if (!row) return;
    var destino = row.getAttribute("data-dmo-open");
    // Linha sem destino (a máquina «Sem produção», o dia vazio): não há nada a
    // abrir, e não se inventa um sítio para onde ir.
    if (!destino) return;
    global.location.href = destino;
  });

  /* ---------- AS AÇÕES FORA DA LISTA (regra 2a: «os botoes vivem sempre fora») ----------
   * O outro caminho que o Owner quer na app (2026-10-14: «também quero ter o
   * cenário de selecionar e depois fazer a ação... devíamos criar shared element
   * para usar na app"). É UM ligador para toda a app:
   *
   *   · a lista canónica despacha `dmo:list-select` com o `data-id` da linha;
   *   · o bloco de ações declara para qual lista existe (`data-dmo-for` = o id
   *     da lista) e cada ação declara o destino como padrão (`{id}`);
   *   · aqui troca-se o `{id}` pela seleção e a ação deixa de estar desativada.
   *
   * Sem seleção, a ação fica `aria-disabled` e sem href — não se finge
   * disponível. E nenhuma página escreve um handler, nem compõe um href: é o
   * contrato que o faz, uma vez.
   *
   * A fronteira com a referência HTML mantém-se: o `{id}` só serve para compor o
   * URL. É o servidor que, recebido esse URL, recompõe o contexto a partir do
   * Job On — nunca um estado guardado no browser.
   */
  function aplicarSeleccaoAsAccoes(lista, id) {
    if (!lista || !lista.id) return;
    document.querySelectorAll('[data-dmo-for="' + lista.id + '"]').forEach(function (bloco) {
      bloco.querySelectorAll("[data-dmo-action]").forEach(function (acao) {
        var modelo = acao.getAttribute("data-href-template");
        if (!modelo) return;
        if (id) {
          acao.setAttribute("href", modelo.replace("{id}", encodeURIComponent(id)));
          acao.removeAttribute("aria-disabled");
        } else {
          acao.setAttribute("href", "");
          acao.setAttribute("aria-disabled", "true");
        }
      });
    });
  }

  document.addEventListener("dmo:list-select", function (event) {
    aplicarSeleccaoAsAccoes(event.target, event.detail && event.detail.id);
  });

  // Uma ação desativada não é clicável nem por teclado: o CSS corta o rato, mas
  // sem nisto o Enter/teclado ainda a seguiria para um href vazio.
  document.addEventListener("click", function (event) {
    var acao = event.target && event.target.closest ? event.target.closest("[data-dmo-action]") : null;
    if (acao && acao.getAttribute("aria-disabled") === "true") {
      event.preventDefault();
    }
  }, true);

  /* ---------- separador com dropdown (barra de funções) ---------- */
  // O <details> abre e fecha sozinho; aqui fecha-se ao clicar fora ou com Esc.
  document.addEventListener("click", function (event) {
    document.querySelectorAll("details[data-dmo-tab-menu][open]").forEach(function (menu) {
      if (!menu.contains(event.target)) menu.removeAttribute("open");
    });
  });
  document.addEventListener("keydown", function (event) {
    if (event.key !== "Escape") return;
    document.querySelectorAll("details[data-dmo-tab-menu][open]").forEach(function (menu) {
      menu.removeAttribute("open");
      var summary = menu.querySelector("summary");
      if (summary) summary.focus();
    });
  });

  global.DMOLive = {
    toast: toast,
    escapeHtml: escapeHtml,
    el: el,
    swapHTML: swapHTML,
    debounce: debounce,
    formatNumber: formatNumber,
    aplicarSeleccaoAsAccoes: aplicarSeleccaoAsAccoes
  };
})(window);

/*
 * dmo-live.js — utilidades vivas comuns (F0, design pass §2.1)
 * Camada comum de comportamento vivo disponível em qualquer página, sem tocar
 * na persistência. Progressive enhancement: SEM este ficheiro, tudo funciona
 * pelos POSTs nativos.
 *
 * API: DMOLive.toast(message, opts?) · DMOLive.escapeHtml(v) ·
 *      DMOLive.debounce(fn, wait) · DMOLive.formatNumber(value, decimals)
 * Nada aqui calcula física industrial: o cálculo é sempre do backend.
 */
(function (global) {
  "use strict";

  /* ---------- toast ----------
   * As mensagens que o JS compõe (a pré-visualização do Peso sem rede, a pasta
   * dos PDFs) entram na mesma pilha que as data-dmo-toast do servidor (ver
   * «mensagens de resultado» mais abaixo): o mesmo canto, as cores da paleta,
   * o «×». Antes tinham uma segunda pilha no canto de baixo, com cores soltas
   * e sem esmorecer ao sair (deep sweep de 18/10/2026, regra 109).
   *
   * A mesma mensagem não se empilha: enquanto estiver à vista, pedir outra vez
   * devolve a que lá está (B-peso-7: sem rede, cada tecla juntava um aviso).
   * Os tipos são os da casa: «ok» fecha ao fim de 6 s (ou opts.timeout); «erro»
   * e «aviso» ficam até se fecharem, salvo opts.timeout. */
  var TIPOS = { info: "ok", success: "ok", ok: "ok", warn: "aviso", aviso: "aviso", error: "erro", erro: "erro" };

  function textoDaMensagem(mensagem) {
    var texto = "";
    Array.prototype.forEach.call(mensagem.childNodes, function (n) {
      if (n.classList && n.classList.contains("dmo-toast-fechar")) return;
      texto += n.textContent;
    });
    return texto.trim();
  }

  function toast(message, opts) {
    opts = opts || {};
    var tipo = TIPOS[opts.kind] || "ok";
    var texto = String(message === null || message === undefined ? "" : message);
    var pilha = document.querySelector(".dmo-toast-pilha");
    var igual = pilha && Array.prototype.find.call(
      pilha.querySelectorAll("[data-dmo-toast]:not(.a-sair)"),
      function (m) { return m.getAttribute("data-dmo-toast") === tipo && textoDaMensagem(m) === texto.trim(); });
    if (igual) {
      if (igual.__dmoReiniciar) igual.__dmoReiniciar(opts.timeout);
      return igual;
    }
    var node = document.createElement("div");
    node.setAttribute("data-dmo-toast", tipo);
    node.setAttribute("role", tipo === "erro" ? "alert" : "status");
    node.textContent = texto;
    juntarAPilha(node, opts.timeout);
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
    // Dentro de uma zona data-dmo-parcial, troca só o que muda na página.
    if (global.DmoTroca) global.DmoTroca.ir(destino, row);
    else global.location.href = destino;
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

  /* ---------- mensagens de resultado (data-dmo-toast) ----------
   * O servidor escreve a mensagem onde ela pertence; aqui junta-se numa pilha
   * no canto, com «×». As «ok» fecham sozinhas; as «erro» e os «aviso» ficam
   * até se fechar, para não se perder o motivo da recusa nem o aviso (nos
   * Pegamentos, «Medição registada sem contra costura…»). O CSS já as tira do
   * fluxo, por isso a página não se mexe nem antes deste script correr. */
  function obterPilha() {
    // Uma pilha só: as mensagens de uma zona trocada (DmoTroca) e as do
    // DMOLive.toast juntam-se às que já lá estão.
    var pilha = document.querySelector(".dmo-toast-pilha");
    if (!pilha) {
      pilha = document.createElement("div");
      pilha.className = "dmo-toast-pilha";
      // Por baixo das barras de navegação, que mudam de altura entre módulos.
      var barras = document.querySelector(".dmo-shell-navigation");
      if (barras && window.matchMedia && window.matchMedia("(min-width: 641px)").matches) {
        pilha.style.top = Math.max(12, barras.getBoundingClientRect().bottom + 12) + "px";
      }
      document.body.appendChild(pilha);
    }
    return pilha;
  }

  function juntarAPilha(mensagem, tempo) {
    var temporizador = null;
    var fechar = function () {
      if (temporizador) clearTimeout(temporizador);
      mensagem.classList.add("a-sair");
      setTimeout(function () { if (mensagem.parentNode) mensagem.parentNode.removeChild(mensagem); }, 220);
    };
    var botao = document.createElement("button");
    botao.type = "button";
    botao.className = "dmo-toast-fechar";
    botao.setAttribute("aria-label", "Fechar");
    botao.textContent = "×";
    botao.addEventListener("click", fechar);
    mensagem.appendChild(botao);
    obterPilha().appendChild(mensagem);
    // Pedida outra vez enquanto está à vista, a mensagem conta o tempo de novo.
    mensagem.__dmoReiniciar = function (novoTempo) {
      if (typeof novoTempo === "number") tempo = novoTempo;
      if (temporizador) clearTimeout(temporizador);
      temporizador = null;
      var vida = typeof tempo === "number" ? tempo : null;
      // Só as «ok» fecham sozinhas (uma do DMOLive.toast pode pedir o seu tempo).
      if (vida === null && mensagem.getAttribute("data-dmo-toast") === "ok") vida = 6000;
      if (vida !== null) temporizador = setTimeout(fechar, vida);
    };
    mensagem.__dmoReiniciar();
  }

  function montarToasts(raiz) {
    var mensagens = (raiz && raiz.querySelectorAll ? raiz : document).querySelectorAll("[data-dmo-toast]");
    if (mensagens.length === 0) return;
    var pilha = obterPilha();
    Array.prototype.forEach.call(mensagens, function (mensagem) {
      // As que já estão na pilha já foram montadas.
      if (mensagem.parentNode === pilha) return;
      juntarAPilha(mensagem);
    });
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", function () { montarToasts(); });
  else montarToasts();

  /* ---------- O VOLTAR (Diogo, 26/10/2026: «o voltar quando entramos numa página a partir de outra não volta à página anterior») ----------
   * Um link [data-dmo-voltar="<chave>"] volta à página de onde se veio, com os
   * filtros que lá estavam. A origem é a página anterior do mesmo site
   * (document.referrer) e fica guardada no separador (sessionStorage) por
   * página + chave: guardar ou abrir uma página filha (data-dmo-voltar-ignorar)
   * e voltar não a perde. Sem origem conhecida, o link fica como o servidor o
   * escreveu (a lista do módulo). */
  function montarVoltar() {
    var links = document.querySelectorAll("a[data-dmo-voltar]");
    if (links.length === 0) return;
    var aqui = global.location.pathname.toLowerCase();
    var anterior = null;
    try { anterior = document.referrer ? new URL(document.referrer) : null; } catch (e) { anterior = null; }
    Array.prototype.forEach.call(links, function (link) {
      var chave = "dmo-voltar:" + aqui + ":" + link.getAttribute("data-dmo-voltar");
      var ignorar = (link.getAttribute("data-dmo-voltar-ignorar") || "").toLowerCase().split(/\s+/).filter(Boolean).concat([aqui, "/login", "/"]);
      var origem = null;
      if (anterior && anterior.origin === global.location.origin && ignorar.indexOf(anterior.pathname.toLowerCase()) < 0) {
        origem = anterior.pathname + anterior.search;
      }
      try {
        if (origem) global.sessionStorage.setItem(chave, origem);
        else origem = global.sessionStorage.getItem(chave);
      } catch (e) { /* sem sessionStorage: fica o link do servidor */ }
      if (!origem) return;
      link.setAttribute("href", origem);
      var texto = link.getAttribute("data-dmo-voltar-texto");
      if (texto) link.textContent = texto;
    });
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", montarVoltar);
  else montarVoltar();
  // Uma zona trocada sem recarregar pode trazer o link de novo como o servidor o escreveu.
  document.addEventListener("dmo:troca", montarVoltar);

  global.DMOLive = {
    toast: toast,
    escapeHtml: escapeHtml,
    debounce: debounce,
    formatNumber: formatNumber,
    aplicarSeleccaoAsAccoes: aplicarSeleccaoAsAccoes,
    mostrarMensagens: montarToasts
  };
})(window);

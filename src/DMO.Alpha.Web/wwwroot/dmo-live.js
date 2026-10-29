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
    // A linha que abre uma janela da casa (data-dmo-abre-janela="id"; Diogo,
    // 27/10/2026, sem botões nas linhas): o Enter ou dois cliques abrem-na,
    // como o botão do canto.
    var janelaId = row.getAttribute("data-dmo-abre-janela");
    if (janelaId) {
      var janela = document.getElementById(janelaId);
      if (janela && typeof janela.showModal === "function" && !janela.open) {
        janela.showModal();
        var campo = janela.querySelector("textarea, input:not([type=hidden]), select");
        if (campo) campo.focus();
      }
      return;
    }
    var destino = row.getAttribute("data-dmo-open");
    // Linha sem destino (a máquina «Sem produção», o dia vazio): não há nada a
    // abrir, e não se inventa um sítio para onde ir.
    if (!destino) return;
    if (global.DmoSupport && global.DmoSupport.tryOpen(destino, row)) return;
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
      // SEM BOTÕES NAS LINHAS (Diogo, 27/10/2026): o botão do canto que
      // precisa de uma linha escolhida (data-dmo-precisa-escolha) fica
      // desativado até haver uma; o que abre a janela da escolhida
      // (data-dmo-dialog-modelo="…{id}…") passa a abrir a dela.
      bloco.querySelectorAll("button[data-dmo-precisa-escolha]").forEach(function (botao) {
        botao.disabled = !id;
      });
      bloco.querySelectorAll("[data-dmo-dialog-modelo]").forEach(function (botao) {
        if (id) botao.setAttribute("data-dmo-dialog", botao.getAttribute("data-dmo-dialog-modelo").replace("{id}", id));
        else botao.removeAttribute("data-dmo-dialog");
      });
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

  /* ---------- clicar fora só fecha (Diogo, 28/10/2026) ----------
   * «Ao clicar fora para fechar estou a abrir outras sem querer»: com um menu
   * ou uma lista aberta (Opções, Funções, Exportar, a ajuda «?», a lista das
   * produções, a Informação técnica da folha), um clique fora só a fecha. Esse
   * clique não chega ao que está por baixo, e o duplo clique que o segue também
   * não. As janelas e os painéis já são modais: o que está por baixo nunca recebe. */
  var ABERTOS = [
    "details[data-dmo-tab-menu][open]", "details.dmo-options[open]", "details.menu[open]",
    "details.tp-support[open]", ".sec > details.more[open]", ".dmo-help.dmo-help-aberta",
    ".prod-drop > .prod-list:not([hidden])"
  ].join(",");
  function fecharAberto(el) {
    if (el.matches(".dmo-help")) { el.classList.remove("dmo-help-aberta"); return; }
    if (el.matches(".prod-list")) {
      el.hidden = true;
      var botao = el.parentElement && el.parentElement.querySelector("[aria-expanded]");
      if (botao) botao.setAttribute("aria-expanded", "false");
      return;
    }
    el.removeAttribute("open");
  }
  var engolidoEm = 0;
  window.addEventListener("click", function (event) {
    if (event.button !== 0 || !(event.target instanceof Element)) return;
    var abertos = Array.prototype.filter.call(document.querySelectorAll(ABERTOS), function (el) {
      // A lista das produções conta como aberta junto com o seu botão.
      var zona = el.matches(".prod-list") ? el.parentElement : el;
      return !zona.contains(event.target);
    });
    if (!abertos.length) return;
    abertos.forEach(fecharAberto);
    event.preventDefault();
    event.stopImmediatePropagation();
    engolidoEm = Date.now();
  }, true);
  window.addEventListener("dblclick", function (event) {
    if (Date.now() - engolidoEm < 700) { event.preventDefault(); event.stopImmediatePropagation(); }
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

  /* ---------- O VOLTAR (Diogo, 26/10/2026: «o voltar quando entramos numa página a partir de outra não volta à página anterior»;
   * 28/10/2026: «voltar sempre às páginas anteriores independentemente de ser 1 ou mais páginas que navegamos») ----------
   * O «← Voltar» ([data-dmo-voltar] e o .dmo-voltar-texto do canto esquerdo)
   * anda para trás no caminho que se fez neste separador, um passo de cada
   * vez, como o «voltar» do browser: A → B → C, o Voltar de C vai a B e o de
   * B vai a A. O caminho fica no separador (sessionStorage): cada página
   * junta-se ao fim; chegar a uma página que já está no caminho (pelo Voltar,
   * pelo «voltar» do browser ou por um link) corta o que vinha depois dela.
   * A mesma página com outros filtros ou depois de guardar substitui-se a si
   * mesma (volta-se com os filtros do último momento). A primeira página do
   * separador começa o caminho pela página anterior do mesmo site
   * (document.referrer). As páginas dentro do painel deslizante não entram:
   * o painel fecha-se com o «Fechar» e o Voltar da página continua igual.
   * Sem página anterior, o link fica como o servidor o escreveu. */
  var CAMINHO = "dmo-voltar:caminho";
  var CAMINHO_MAX = 40;
  function dentroDoPainel(url) {
    if (url.searchParams.has("painel")) return true;
    try { return global.self !== global.top; } catch (e) { return true; }
  }
  function caminhoDe(endereco) {
    var u = new URL(endereco, global.location.href);
    return u.pathname.toLowerCase();
  }
  function lerCaminho() {
    try {
      var lido = JSON.parse(global.sessionStorage.getItem(CAMINHO) || "[]");
      return Array.isArray(lido) ? lido.filter(function (x) { return typeof x === "string"; }) : [];
    } catch (e) { return null; }
  }
  function acertarCaminho() {
    var aqui = new URL(global.location.href);
    if (dentroDoPainel(aqui)) return null;
    var caminho = lerCaminho();
    if (caminho === null) return null;
    var onde = aqui.pathname.toLowerCase();
    // Entrar de novo (login) começa um caminho novo.
    if (onde === "/login") {
      try { global.sessionStorage.removeItem(CAMINHO); } catch (e) { /* nada a limpar */ }
      return null;
    }
    var i = caminho.map(caminhoDe).lastIndexOf(onde);
    if (i >= 0) {
      caminho = caminho.slice(0, i);
    } else {
      var anterior = null;
      try { anterior = document.referrer ? new URL(document.referrer) : null; } catch (e) { anterior = null; }
      if (anterior && anterior.origin === aqui.origin && !anterior.searchParams.has("painel")
        && ["/login", onde].indexOf(anterior.pathname.toLowerCase()) < 0) {
        var j = caminho.map(caminhoDe).lastIndexOf(anterior.pathname.toLowerCase());
        if (j >= 0) caminho = caminho.slice(0, j);
        caminho.push(anterior.pathname + anterior.search);
      }
    }
    caminho.push(aqui.pathname + aqui.search);
    if (caminho.length > CAMINHO_MAX) caminho = caminho.slice(caminho.length - CAMINHO_MAX);
    try { global.sessionStorage.setItem(CAMINHO, JSON.stringify(caminho)); } catch (e) { /* sem sessionStorage: fica o link do servidor */ }
    return caminho;
  }
  function montarVoltar() {
    var caminho = acertarCaminho();
    var links = document.querySelectorAll("a[data-dmo-voltar], a.dmo-voltar-texto");
    if (!caminho) return;
    var origem = caminho.length > 1 ? caminho[caminho.length - 2] : null;
    if (!origem || caminhoDe(origem) === "/login") return;
    // Todas as páginas (Diogo, 29/10/2026: «já criámos o Voltar para todas as páginas?»):
    // a que não o escreveu ganha o link no canto superior esquerdo, desde que
    // venha de outra página (a primeira não tem para onde voltar).
    if (links.length === 0 && !/imprimir/i.test(global.location.pathname)) {
      var principal = document.querySelector("main");
      if (!principal) return;
      var novo = document.createElement("a");
      novo.className = "dmo-voltar-texto";
      novo.setAttribute("data-dmo-voltar", "auto");
      novo.setAttribute("href", origem);
      novo.textContent = "Voltar";
      principal.insertBefore(novo, principal.firstChild);
      links = [novo];
    }
    Array.prototype.forEach.call(links, function (link) {
      link.setAttribute("href", origem);
      var texto = link.getAttribute("data-dmo-voltar-texto");
      if (texto) link.textContent = texto;
    });
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", montarVoltar);
  else montarVoltar();
  // Uma zona trocada sem recarregar pode trazer o link de novo como o servidor o
  // escreveu e mudar os filtros do endereço.
  document.addEventListener("dmo:troca", montarVoltar);
  // O «voltar» do browser pode mostrar a página guardada sem a correr de novo.
  global.addEventListener("pageshow", function (event) { if (event.persisted) montarVoltar(); });

  global.DMOLive = {
    toast: toast,
    escapeHtml: escapeHtml,
    debounce: debounce,
    formatNumber: formatNumber,
    aplicarSeleccaoAsAccoes: aplicarSeleccaoAsAccoes,
    mostrarMensagens: montarToasts
  };
})(window);

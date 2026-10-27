/*
 * peso-live.js — comportamento vivo do Peso tool-scoped (F1, design pass §2.4)
 * Progressive enhancement: SEM este ficheiro tudo funciona pelo POST nativo.
 * COM este ficheiro, escrever responde ao vivo.
 *
 * Contrato (retoque do design review — decisões do Owner):
 *  - O cálculo industrial é SEMPRE do backend: o preview chama o endpoint
 *    read-only ?handler=Preview (Peso.cshtml.cs), que reutiliza a resolução
 *    canónica do PesoQuery e o PesoCalculation. Zero fórmula ou tabela de
 *    densidade em JS (PESO.md L180 — proibido).
 *  - Zero escrita fora do POST nativo: as leituras em curso são as linhas do
 *    formulário («+» acrescenta, «−» tira a última — o Owner 2026-10-17) e
 *    cada uma mostra logo o seu volume e peso; o registo real acontece
 *    exclusivamente no POST nativo «Registar leitura», que grava todas as
 *    linhas com massa de água.
 *  - Recusas do endpoint (invalid_water_weight, result_non_positive) apontam
 *    para a linha visível e ficam sem bloquear o resto; a linguagem é da
 *    oficina (sem códigos de engenharia).
 *  - Rede indisponível: toast «pré-visualização indisponível» e os últimos
 *    valores continuam visíveis.
 *  - Gravar sem recarregar (Diogo, 17/10/2026): «Registar leitura» troca só
 *    as zonas da folha (DmoTroca); os ouvintes são delegados no document e
 *    as peças procuram-se de cada vez. Ao enviar, a leitura enviada passa
 *    para os cartões, marcada «A gravar…», a leitura nova fica vazia e o
 *    foco vai para o N.º CM da próxima leitura: o operador escreve a
 *    seguinte enquanto a anterior grava, e a resposta não lhe toca (B-peso-1,
 *    deep sweep 18/10/2026). Se não ficou gravada, volta para corrigir.
 */
(function () {
  "use strict";

  var L = window.DMOLive;
  if (!L) return;

  // Linguagem da oficina: nenhuma enumeração ou jargão de implementação
  // chega ao operador (revisões H-6/C-8). Ambas as recusas dizem o que fazer.
  var REFUSAL_MESSAGES = {
    invalid_water_weight: "Massa de água não utilizável — introduza um valor maior que 0.",
    result_non_positive: "Massa de água não utilizável — introduza um valor maior que 0."
  };

  function factHtml(label, value, extraClass) {
    var strongClass = extraClass ? " class=\"" + extraClass + "\"" : "";
    return '<div class="peso-fact"><span>' + L.escapeHtml(label) + "</span><strong" + strongClass + ">" + L.escapeHtml(value) + "</strong></div>";
  }

  function signed(value, unit, decimals) {
    if (value === null || value === undefined) return "—";
    var n = Number(value);
    var prefix = n > 0 ? "+" : "";
    return prefix + L.formatNumber(n, decimals) + (unit ? " " + unit : "");
  }

  // As peças da folha procuram-se de cada vez que são precisas: a gravação
  // sem recarregar (DmoTroca, data-dmo-regiao) troca o resumo e os cartões
  // gravados por elementos novos. A leitura nova (data-dmo-fica) fica.
  function folha() {
    var root = document.querySelector("[data-peso-live]");
    if (!root) return null;
    var form = root.querySelector("[data-peso-rows-form]");
    var f = {
      pesoId: root.getAttribute("data-peso-id") || "",
      previewUrl: root.getAttribute("data-preview-url") || "",
      strip: root.querySelector("[data-peso-strip]"),
      summary: root.querySelector("[data-peso-summary]"),
      form: form,
      rowsHost: form && form.querySelector("[data-peso-rows]")
    };
    return f.strip && f.summary && f.rowsHost && f.previewUrl ? f : null;
  }

  var inflight = null;
  // O aviso «Pré-visualização indisponível» fica até se fechar (as mensagens
  // da casa, regra 109 do deep sweep): sai quando a pré-visualização volta.
  var avisoSemPreview = null;
  // Estado inicial: o persistido, sem leitura em curso.
  var lastSent = JSON.stringify([]);
  // Os envios de «Registar leitura» ainda sem fim, pelo número do DmoTroca
  // (dmo:fila): as linhas enviadas, o campo com o foco no envio e o N.º CM
  // para onde o foco foi; gravou quando a resposta trouxe a leitura gravada.
  var pendentes = [];

  function normalize(raw) {
    var value = (raw || "").replace(/\s/g, "");
    if (value === "") return "";
    return value.replace(",", ".");
  }

  function rows(f) {
    return Array.prototype.slice.call(f.rowsHost.querySelectorAll("[data-peso-row]"));
  }

  function cmOf(row) { return row.querySelector("input[name='cm_subject']"); }
  function waterOf(row) { return row.querySelector("input[name='water_weight']"); }
  function resultOf(row) { return row.querySelector("[data-peso-row-result]"); }

  // As linhas com massa de água, pela ordem do formulário; o índice do
  // preview (1, 2, …) aponta para a linha de origem.
  function draftRows(f) {
    return rows(f).filter(function (row) { return normalize(waterOf(row).value) !== ""; });
  }

  function draftLines(f) {
    return draftRows(f).map(function (row) {
      return { cm_subject: (cmOf(row).value || "").trim(), water_weight: normalize(waterOf(row).value) };
    });
  }

  function request() {
    var f = folha();
    if (!f) return;
    var key = JSON.stringify(draftLines(f));
    if (key === lastSent) return;
    var body = new URLSearchParams();
    body.set("peso_id", f.pesoId);
    body.set("lines", key);
    var tokenInput = f.form.querySelector("input[name='__RequestVerificationToken']");
    if (tokenInput && tokenInput.value) {
      body.set("__RequestVerificationToken", tokenInput.value);
    }
    if (inflight) inflight.abort(); // cancelamento in-flight
    inflight = new AbortController();
    fetch(f.previewUrl, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
      body: body.toString(),
      signal: inflight.signal,
      credentials: "same-origin"
    })
      .then(function (response) {
        if (!response.ok) throw new Error("http " + response.status);
        return response.json();
      })
      .then(function (data) {
        inflight = null;
        if (avisoSemPreview) {
          var fechar = avisoSemPreview.querySelector(".dmo-toast-fechar");
          if (fechar && avisoSemPreview.isConnected) fechar.click();
          avisoSemPreview = null;
        }
        // Só conta a resposta ao que está escrito agora (sweep de 19/10/2026,
        // FE-8): o pedido antigo só se cancela quando sai o seguinte, 300 ms
        // depois da última tecla, e o renderRows põe os valores por posição;
        // ao apagar a massa da linha 2, a linha 3 mostrava o volume da 2. A
        // tecla que mudou as linhas já marcou o pedido seguinte.
        var atual = folha();
        if (!atual || JSON.stringify(draftLines(atual)) !== key) return;
        render(data);
        lastSent = key;
      })
      .catch(function (error) {
        if (error && error.name === "AbortError") return;
        inflight = null;
        avisoSemPreview = L.toast("Pré-visualização indisponível — os valores podem estar desatualizados; o registo continua no botão «Registar leitura»", { kind: "warn" });
      });
  }

  var debouncedRequest = L.debounce(request, 300); // janela 250–400 ms

  // Cada linha mostra logo o seu volume e peso (o Owner 2026-10-17:
  // «a leitura regista e fica visível a tempo real o peso associado
  // àquela CM»). Uma linha vazia não mostra nada.
  function renderRows(f, data) {
    var lines = (data && data.lines) || [];
    var drafts = draftRows(f);
    rows(f).forEach(function (row) {
      var out = resultOf(row);
      if (!out) return;
      out.textContent = "";
      out.classList.remove("is-refused");
    });
    drafts.forEach(function (row, i) {
      var out = resultOf(row);
      var line = lines[i];
      if (!out || !line) return;
      if (line.status === "ok") {
        out.textContent = L.formatNumber(line.capacityCm3, 3) + " cm³ · " + L.formatNumber(line.glassWeightGrams, 2) + " g";
      } else {
        out.textContent = REFUSAL_MESSAGES[line.status] || "Massa de água não utilizável — introduza um valor maior que 0.";
        out.classList.add("is-refused");
      }
    });
  }

  function render(data) {
    var f = folha();
    if (!f || !data) return;
    if (data.refusal) {
      L.toast("Peso indicado não existe — pré-visualização indisponível", { kind: "error" });
      return;
    }

    var totals = data.totals || {};
    var context = data.context || {};
    // A comparação pertence aos Pesos guardados; o preview só recalcula
    // o rascunho e não tem informação para substituir este facto.
    var comparisonFact = f.summary.querySelector("[data-peso-comparison]");

    f.strip.innerHTML = [
      factHtml("Leituras válidas", totals.sampleCount === undefined || totals.sampleCount === null ? "—" : String(totals.sampleCount)),
      factHtml("Média em água", totals.averageWaterWeightGrams === null || totals.averageWaterWeightGrams === undefined ? "—" : L.formatNumber(totals.averageWaterWeightGrams, 2) + " g"),
      factHtml("Peso estimado em vidro", totals.averageGlassWeightGrams === null || totals.averageGlassWeightGrams === undefined ? "—" : L.formatNumber(totals.averageGlassWeightGrams, 2) + " g"),
      factHtml("Capacidade média", totals.averageCapacityCm3 === null || totals.averageCapacityCm3 === undefined ? "—" : L.formatNumber(totals.averageCapacityCm3, 3) + " cm³")
    ].join("");

    f.summary.innerHTML = [
      factHtml("Peso nominal", context.nominalWeightGrams === null || context.nominalWeightGrams === undefined ? "—" : L.formatNumber(context.nominalWeightGrams, 2) + " g"),
      factHtml("Diferença nominal", totals.desvioNominalG === null || totals.desvioNominalG === undefined ? "—" : signed(totals.desvioNominalG, "g", 2), Number(totals.desvioNominalG) < 0 ? "peso-valor-negativo" : undefined)
    ].join("");
    if (comparisonFact) f.summary.appendChild(comparisonFact);

    renderRows(f, data);
  }

  // A leitura nova volta a ser uma linha vazia, como o servidor a desenha
  // (a primeira linha, com os ids; as acrescentadas pelo «+» saem).
  function limparRascunho(f) {
    var all = rows(f);
    all.slice(1).forEach(function (row) { row.remove(); });
    all[0].querySelectorAll("input").forEach(function (input) { input.value = ""; });
    var out = resultOf(all[0]);
    if (out) { out.textContent = ""; out.classList.remove("is-refused"); }
  }

  function rascunhoVazio(f) {
    return rows(f).every(function (row) { return !cmOf(row).value.trim() && !waterOf(row).value.trim(); });
  }

  // Uma linha vazia a seguir à última (o «+»): sem os ids nem o required da
  // primeira.
  function acrescentarLinha(f) {
    var all = rows(f);
    var last = all[all.length - 1];
    var clone = last.cloneNode(true);
    clone.querySelectorAll("input").forEach(function (input) {
      input.value = "";
      input.removeAttribute("id");
      input.removeAttribute("required");
    });
    clone.querySelectorAll("label").forEach(function (label) { label.removeAttribute("for"); });
    var out = resultOf(clone);
    if (out) { out.textContent = ""; out.classList.remove("is-refused"); }
    last.after(clone);
    return clone;
  }

  // O foco vai para o N.º CM da próxima leitura, sem deslizar a página; só
  // se a tabela e os cartões, que crescem por cima, a tiraram do ecrã é que
  // a página desliza o mínimo para a mostrar. Se o operador já foi para
  // outro campo fora do formulário, o foco fica onde ele está.
  function focarProximaLeitura(f) {
    var ativo = document.activeElement;
    if (ativo && ativo !== document.body && !f.form.contains(ativo)) return;
    var campo = cmOf(rows(f)[0]);
    if (!campo) return;
    campo.focus({ preventScroll: true });
    mostrarCampo(campo);
  }

  function mostrarCampo(campo) {
    var caixa = campo.getBoundingClientRect();
    if (caixa.top < 0 || caixa.bottom > window.innerHeight) campo.scrollIntoView({ block: "nearest" });
  }

  // Depois de gravar, a tabela e os cartões crescem por cima: o campo onde
  // o operador escreve a leitura seguinte fica com o foco e à vista. Só um
  // foco perdido (no body) ou num botão da leitura nova ainda vazia (um
  // clique repetido em «Registar leitura») volta ao N.º CM.
  function manterLeituraNova(f) {
    var ativo = document.activeElement;
    if (!ativo || ativo === document.body || (f.form.contains(ativo) && ativo.matches("button") && rascunhoVazio(f))) {
      focarProximaLeitura(f);
    } else if (f.rowsHost.contains(ativo)) {
      mostrarCampo(ativo);
    }
  }

  function pendenteDe(envio) {
    if (envio === null || envio === undefined) return null;
    for (var i = 0; i < pendentes.length; i++) if (pendentes[i].envio === envio) return pendentes[i];
    return null;
  }

  // As leituras a gravar ficam no fim dos cartões gravados, onde o cartão do
  // servidor vai aparecer, marcadas «A gravar…» (só de leitura, fora do
  // Tab). A resposta troca a zona dos cartões: as que ainda esperam voltam a
  // aparecer no fim (ver dmo:troca).
  function mostrarPendentes() {
    document.querySelectorAll("[data-peso-leitura-pendente]").forEach(function (card) { card.remove(); });
    var zona = document.getElementById("pesoLeiturasGravadas");
    if (!zona) return;
    pendentes.forEach(function (p) {
      if (p.gravou) return;
      p.linhas.forEach(function (linha) {
        if (normalize(linha.agua) !== "") zona.appendChild(cartaoPendente(linha));
      });
    });
  }

  function cartaoPendente(linha) {
    var card = document.createElement("div");
    card.className = "peso-leitura-row is-saved";
    card.setAttribute("data-peso-leitura-pendente", "");
    card.setAttribute("aria-busy", "true");
    card.innerHTML = '<div class="dmo-field"><label>N.º CM</label><input readonly tabindex="-1" aria-label="N.º CM a gravar" /></div>'
      + '<div class="dmo-field"><label>Massa de água (g)</label><input readonly tabindex="-1" aria-label="Massa de água a gravar" /></div>'
      + '<output class="peso-leitura-chip">A gravar…</output>';
    var campos = card.querySelectorAll("input");
    campos[0].value = linha.cm.trim() || "—";
    campos[1].value = normalize(linha.agua);
    return card;
  }

  // A leitura enviada não ficou gravada (uma recusa, a ligação que falhou):
  // volta à leitura nova para corrigir ou repetir. Se o operador ainda não
  // escreveu nada, volta como estava, com o foco no campo de onde enviou;
  // se já escreveu a seguinte, vem a seguir a ela, em linhas a mais.
  function devolver(f, p) {
    var linhas = p.linhas.filter(function (l) { return l.cm.trim() !== "" || l.agua.trim() !== ""; });
    if (!linhas.length) return;
    var vazio = rascunhoVazio(f);
    var livres = vazio ? rows(f) : [];
    linhas.forEach(function (linha, i) {
      var row = livres[i] || acrescentarLinha(f);
      cmOf(row).value = linha.cm;
      waterOf(row).value = linha.agua;
    });
    var ativo = document.activeElement;
    if (vazio && !pendentes.length && (ativo === p.focado || ativo === document.body) && p.foco && p.foco.isConnected) {
      p.foco.focus({ preventScroll: true });
    }
    request();
  }

  // Os ouvintes são delegados no document: servem as linhas que o «+»
  // acrescenta e continuam a servir depois de uma troca de zonas.
  function linhaNova(el) {
    return el && el.closest ? el.closest("[data-peso-live] [data-peso-rows]") : null;
  }

  document.addEventListener("input", function (event) {
    var field = event.target;
    if (linhaNova(field) && field.matches("input[name='water_weight'], input[name='cm_subject']")) debouncedRequest();
  });

  // O blur não sobe: ouve-se na captura. Normalização decimal no blur.
  document.addEventListener("blur", function (event) {
    var water = event.target;
    if (!linhaNova(water) || !water.matches("input[name='water_weight']")) return;
    var normalized = normalize(water.value);
    if (normalized !== water.value) water.value = normalized;
    request();
  }, true);

  // «+» acrescenta uma linha vazia; «−» tira a última. Com uma só linha, o
  // «−» é a opção para apagar a última leitura gravada: abre a pergunta
  // «Apagar a leitura da CM …?» da correção, nada se apaga sem confirmar
  // (Diogo, 27/10/2026).
  document.addEventListener("click", function (event) {
    var button = event.target.closest && event.target.closest("[data-peso-add-row], [data-peso-remove-row]");
    var f = button && folha();
    if (!f || !f.form.contains(button)) return;
    var all = rows(f);
    if (button.hasAttribute("data-peso-add-row")) {
      cmOf(acrescentarLinha(f)).focus();
    } else if (all.length > 1) {
      all[all.length - 1].remove();
      request();
    } else {
      apagarUltimaGravada(button);
    }
  });

  function apagarUltimaGravada(origem) {
    var linhas = document.querySelectorAll("[data-peso-leituras] tbody tr[data-peso-leitura-id]");
    var ultima = linhas[linhas.length - 1];
    if (!ultima) return;
    var url = new URL(window.location.href);
    ["corrigir_leitura", "apagar_leitura", "correcao", "correcao_cm", "correcao_agua", "handler"].forEach(function (chave) { url.searchParams.delete(chave); });
    url.searchParams.set("corrigir_leitura", ultima.getAttribute("data-peso-leitura-id"));
    url.searchParams.set("apagar_leitura", "1");
    url.hash = "";
    if (window.DmoTroca && typeof window.DmoTroca.ir === "function") window.DmoTroca.ir(url.href, origem);
    else window.location.href = url.href;
  }

  // O Esc dentro da correção sai da correção e fica na folha (no painel, o
  // Esc fechava o painel todo).
  document.addEventListener("keydown", function (event) {
    if (event.key !== "Escape") return;
    var caixa = event.target.closest && event.target.closest("[data-peso-correcao]");
    var sair = caixa && caixa.querySelector("[data-peso-correcao-sair]");
    if (!sair) return;
    event.preventDefault();
    event.stopPropagation();
    sair.click();
  }, true);

  // GRAVAR SEM RECARREGAR (Diogo, 17/10/2026): «Registar leitura» vai por
  // fetch (DmoTroca) e só as zonas trocam. O envio entra na fila (dmo:fila,
  // com o corpo já lido): a leitura enviada passa logo para os cartões, «A
  // gravar…», a leitura nova fica vazia e o foco vai para o N.º CM da
  // próxima. Antes, a leitura nova só era limpa quando chegava a resposta, e
  // levava com ela a leitura seguinte que o operador já tinha escrito
  // (B-peso-1). Quando a gravação passou, os cartões gravados
  // (#pesoLeiturasGravadas) trocam e o que o operador escreveu entretanto
  // fica, com o foco. Numa recusa a resposta traz a marca
  // data-peso-registo-recusado (a mensagem vai para o canto), mesmo que os
  // cartões tenham mudado por uma leitura gravada entretanto noutro
  // separador; aí, ou se a ligação falhou e a confirmação não a mostra
  // gravada, a leitura volta para corrigir (ver devolver, no dmo:fim). Sem
  // ligação, fica «A gravar…» até à confirmação, quando a ligação volta
  // (deep sweep, 18/10/2026, B-peso-3). Escolher a comparação troca o
  // resumo: com uma leitura em curso, a pré-visualização volta.
  document.addEventListener("dmo:fila", function (event) {
    var f = folha();
    if (!f || event.target !== f.form) return;
    var p = {
      envio: event.detail && event.detail.envio,
      linhas: rows(f).map(function (row) { return { cm: cmOf(row).value, agua: waterOf(row).value }; }),
      foco: document.activeElement,
      focado: null,
      gravou: false
    };
    pendentes.push(p);
    // A faixa fica com a pré-visualização que já contava esta leitura.
    if (inflight) { inflight.abort(); inflight = null; }
    limparRascunho(f);
    lastSent = JSON.stringify([]);
    focarProximaLeitura(f);
    p.focado = document.activeElement;
    mostrarPendentes();
  });

  document.addEventListener("dmo:troca", function (event) {
    var detail = event.detail || {};
    var zonas = detail.zonas || [];
    var f = folha();
    if (!f) return;
    var p = pendenteDe(detail.envio);
    if (p) {
      var recusado = document.querySelector("[data-peso-live] [data-peso-registo-recusado]") !== null;
      p.gravou = !recusado && zonas.indexOf("pesoLeiturasGravadas") >= 0;
    }
    mostrarPendentes();
    if (p && p.gravou) manterLeituraNova(f);
    if (zonas.indexOf("pesoLeituraResumo") >= 0) {
      if (inflight) { inflight.abort(); inflight = null; }
      lastSent = JSON.stringify([]);
      request();
    }
  });

  // O envio acabou (gravado, recusado ou sem resposta): o que não ficou
  // gravado volta à leitura nova.
  document.addEventListener("dmo:fim", function (event) {
    var p = pendenteDe(event.detail && event.detail.envio);
    if (!p) return;
    pendentes.splice(pendentes.indexOf(p), 1);
    var f = folha();
    if (f && !p.gravou) devolver(f, p);
    mostrarPendentes();
  });

  // Um clique repetido em «Registar leitura» (ou outro Enter) enquanto a
  // leitura enviada grava encontra a leitura nova vazia: sem a bolha
  // «Preencha este campo» (cliques repetidos não mostram mensagens, Diogo).
  document.addEventListener("invalid", function (event) {
    var f = folha();
    if (f && pendentes.length && f.form.contains(event.target) && rascunhoVazio(f)) event.preventDefault();
  }, true);

  // Uma folha aberta com leituras escritas (o echo de uma recusa) mostra
  // logo a pré-visualização delas.
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", request);
  } else {
    request();
  }
})();

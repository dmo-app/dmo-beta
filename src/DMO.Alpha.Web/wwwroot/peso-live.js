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

  // Densidades: precisão de metrologia (a tabela real da fábrica tem 5
  // casas — decisão Owner 1b; o display não padroniza a mínima).
  function formatVariable(value, maxDecimals) {
    if (value === null || value === undefined || isNaN(Number(value))) return "—";
    return Number(value).toLocaleString("en-US", { useGrouping: false, maximumFractionDigits: maxDecimals });
  }

  function init() {
    var root = document.querySelector("[data-peso-live]");
    if (!root) return;
    var pesoId = root.getAttribute("data-peso-id") || "";
    var previewUrl = root.getAttribute("data-preview-url") || "";
    var strip = root.querySelector("[data-peso-strip]");
    var summary = root.querySelector("[data-peso-summary]");
    var refusals = root.querySelector("[data-peso-refusals]");
    var registerForm = root.querySelector("[data-peso-rows-form]");
    var rowsHost = registerForm && registerForm.querySelector("[data-peso-rows]");
    var addButton = registerForm && registerForm.querySelector("[data-peso-add-row]");
    var removeButton = registerForm && registerForm.querySelector("[data-peso-remove-row]");
    var tokenInput = registerForm && registerForm.querySelector("input[name='__RequestVerificationToken']");
    if (!strip || !summary || !rowsHost || !previewUrl) return;

    // A comparação pertence aos Pesos guardados; o preview só recalcula
    // o rascunho e não tem informação para substituir este facto.
    var comparisonFact = summary.querySelector("[data-peso-comparison]");

    var inflight = null;
    var lastSent = null;

    function normalize(raw) {
      var value = (raw || "").replace(/\s/g, "");
      if (value === "") return "";
      return value.replace(",", ".");
    }

    function rows() {
      return Array.prototype.slice.call(rowsHost.querySelectorAll("[data-peso-row]"));
    }

    function cmOf(row) { return row.querySelector("input[name='cm_subject']"); }
    function waterOf(row) { return row.querySelector("input[name='water_weight']"); }
    function resultOf(row) { return row.querySelector("[data-peso-row-result]"); }

    // As linhas com massa de água, pela ordem do formulário; o índice do
    // preview (1, 2, …) aponta para a linha de origem.
    function draftRows() {
      return rows().filter(function (row) { return normalize(waterOf(row).value) !== ""; });
    }

    function draftLines() {
      return draftRows().map(function (row) {
        return { cm_subject: (cmOf(row).value || "").trim(), water_weight: normalize(waterOf(row).value) };
      });
    }

    function stateKey() {
      return JSON.stringify(draftLines());
    }

    function request() {
      var key = stateKey();
      if (key === lastSent) return;
      var body = new URLSearchParams();
      body.set("peso_id", pesoId);
      body.set("lines", JSON.stringify(draftLines()));
      if (tokenInput && tokenInput.value) {
        body.set("__RequestVerificationToken", tokenInput.value);
      }
      if (inflight) inflight.abort(); // cancelamento in-flight
      inflight = new AbortController();
      fetch(previewUrl, {
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
          render(data);
          lastSent = key;
        })
        .catch(function (error) {
          if (error && error.name === "AbortError") return;
          inflight = null;
          L.toast("Pré-visualização indisponível — os valores podem estar desatualizados; o registo continua no botão «Registar leitura»", { kind: "warn" });
        });
    }

    var debouncedRequest = L.debounce(request, 300); // janela 250–400 ms

    // Cada linha mostra logo o seu volume e peso (o Owner 2026-10-17:
    // «a leitura regista e fica visível a tempo real o peso associado
    // àquela CM»). Uma linha vazia não mostra nada.
    function renderRows(data) {
      var lines = (data && data.lines) || [];
      var drafts = draftRows();
      rows().forEach(function (row) {
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
      if (!data) return;
      if (data.refusal) {
        L.toast("Peso indicado não existe — pré-visualização indisponível", { kind: "error" });
        return;
      }

      var totals = data.totals || {};
      var context = data.context || {};

      strip.innerHTML = [
        factHtml("Leituras válidas", totals.sampleCount === undefined || totals.sampleCount === null ? "—" : String(totals.sampleCount)),
        factHtml("Média em água", totals.averageWaterWeightGrams === null || totals.averageWaterWeightGrams === undefined ? "—" : L.formatNumber(totals.averageWaterWeightGrams, 2) + " g"),
        factHtml("Peso estimado em vidro", totals.averageGlassWeightGrams === null || totals.averageGlassWeightGrams === undefined ? "—" : L.formatNumber(totals.averageGlassWeightGrams, 2) + " g"),
        factHtml("Capacidade média", totals.averageCapacityCm3 === null || totals.averageCapacityCm3 === undefined ? "—" : L.formatNumber(totals.averageCapacityCm3, 3) + " cm³")
      ].join("");

      summary.innerHTML = [
        factHtml("Peso nominal", context.nominalWeightGrams === null || context.nominalWeightGrams === undefined ? "—" : L.formatNumber(context.nominalWeightGrams, 2) + " g"),
        factHtml("Diferença nominal", totals.desvioNominalG === null || totals.desvioNominalG === undefined ? "—" : signed(totals.desvioNominalG, "g", 2), Number(totals.desvioNominalG) < 0 ? "peso-valor-negativo" : undefined)
      ].join("");
      if (comparisonFact) summary.appendChild(comparisonFact);

      if (refusals) {
        var refused = (data.lines || []).filter(function (line) { return line.status !== "ok"; });
        refusals.hidden = refused.length === 0;
        refusals.innerHTML = refused.length === 0 ? "" : "Há leituras com massa de água não utilizável — corrija-as antes de registar.";
      }

      renderRows(data);
    }

    function bindRow(row) {
      var water = waterOf(row);
      var cm = cmOf(row);
      water.addEventListener("input", debouncedRequest);
      water.addEventListener("blur", function () {
        var normalized = normalize(water.value);
        if (normalized !== water.value) water.value = normalized; // normalização decimal no blur
        request();
      });
      cm.addEventListener("input", debouncedRequest);
    }

    rows().forEach(bindRow);

    // «+» acrescenta uma linha vazia; «−» tira a última (fica sempre uma).
    if (addButton) {
      addButton.addEventListener("click", function () {
        var all = rows();
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
        bindRow(clone);
        cmOf(clone).focus();
      });
    }
    if (removeButton) {
      removeButton.addEventListener("click", function () {
        var all = rows();
        if (all.length <= 1) return;
        all[all.length - 1].remove();
        request();
      });
    }

    // Estado inicial: o persistido, sem leitura em curso.
    lastSent = JSON.stringify([]);
    request();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();

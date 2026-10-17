/*
 * ferramentas-live.js — o vivo do NOME TÉCNICO no form das Ferramentas
 * (o corte do parser, o Owner 2026-10-11: «se introduzirmos o nome
 * tecnico, ou seja o que esta no sap. apresenta logo tudo»).
 * Progressive enhancement: SEM este ficheiro o form funciona pelo POST
 * nativo; COM ele, escrever o código decodifica e preenche o tipo e a
 * referência ao vivo — o preenchimento é PROPOSTA que o operador
 * confirma (o decode canónico completo vive na ficha, pelo
 * TechnicalNameParser do backend).
 */
(function () {
  "use strict";

  var technical = document.getElementById("ferr-new-technical");
  var notice = document.getElementById("ferr-technical-notice");
  var type = document.getElementById("ferr-new-type");
  var reference = document.getElementById("ferr-new-reference");
  if (!technical || !type || !reference) return;

  // A «Referência MF associada» só se aplica à CM (beta.34).
  var mfField = document.getElementById("ferr-new-mf-field");
  if (mfField) {
    var syncMf = function () { mfField.hidden = type.value !== "CM"; };
    type.addEventListener("change", syncMf);
    syncMf();
  }

  technical.addEventListener("input", function () {
    var name = technical.value.trim().toUpperCase();
    if (!name) {
      if (notice) notice.textContent = "";
      return;
    }

    var decodedType = "";
    var decodedRef = "";

    if (name[0] === "M" && name.length > 6) {
      // M + modelo(4 dígitos — a base) + mm + TT + PP + V: o TT diz o
      // tipo; a base do desenho é a referência.
      if (name.indexOf("MF") > 1) { decodedType = "MF"; }
      else if (name.indexOf("MP") > 1 || name.indexOf("ST") > 1) { decodedType = "CM"; }
      var rest = name.slice(1);
      var model = "";
      while (rest.length && rest[0] >= "0" && rest[0] <= "9" && model.length < 4) { model += rest[0]; rest = rest.slice(1); }
      decodedRef = model;
    } else if (name[0] === "A" && name.length > 5) {
      // A + T + XXXX + ...: o B (neck ring) e o S (blow head/marisa) são
      // BQ; o XXXX é a referência.
      var t = name[1];
      if (t === "B" || t === "S") { decodedType = "BQ"; }
      else if (t === "P" || t === "T") { decodedType = "CM"; }
      var afterT = name.slice(2);
      var pm = afterT.indexOf("PM");
      decodedRef = pm > 0 ? afterT.slice(0, pm) : afterT.slice(0, 4);
    }

    if (notice) {
      notice.textContent = decodedType || decodedRef
        ? "Decodificado: " + decodedType + (decodedRef ? " · " + decodedRef : "") + " (confirme)"
        : "Nome técnico guardado como está — o decode aparece na ficha.";
    }

    if (decodedType) { type.value = decodedType; type.dispatchEvent(new Event("change", { bubbles: true })); }
    if (decodedRef) { reference.value = decodedRef; }
  });
})();

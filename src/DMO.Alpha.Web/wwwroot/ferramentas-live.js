/*
 * ferramentas-live.js — o vivo do NOME TÉCNICO no form das Ferramentas
 * (o corte do parser, o Owner 2026-10-11: «se introduzirmos o nome
 * tecnico, ou seja o que esta no sap. apresenta logo tudo»).
 * Progressive enhancement: SEM este ficheiro o form funciona pelo POST
 * nativo; COM ele, escrever o código pede a leitura ao servidor (o
 * TechnicalNameParser, a mesma da ficha) e propõe o tipo, que o operador
 * confirma. A referência nunca vem do nome técnico (Diogo, 19/10/2026:
 * «sempre o nome da referência, e não do nome técnico»): escreve-se toda,
 * à parte, como no desenho.
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

  // As sugestões da Referência são do tipo escolhido (Diogo, 19/10/2026:
  // a referência sempre toda; a CM também pela Ref. MF associada).
  var syncSuggest = function () { reference.setAttribute("data-dmo-suggest-tipo", type.value.toLowerCase()); };
  type.addEventListener("change", syncSuggest);
  syncSuggest();

  // A informação técnica do registo mostra só os campos do tipo escolhido
  // (os escondidos não são enviados: o servidor grava só os do tipo).
  var techFields = document.querySelectorAll("[data-ferr-tech-tipo]");
  if (techFields.length) {
    var syncTech = function () {
      techFields.forEach(function (field) {
        // «CM BQ»: um traço que separa componentes vale para mais de um tipo.
        var applies = field.getAttribute("data-ferr-tech-tipo").split(" ").indexOf(type.value) >= 0;
        field.hidden = !applies;
        field.querySelectorAll("input,select").forEach(function (input) { input.disabled = !applies; });
      });
    };
    type.addEventListener("change", syncTech);
    syncTech();
  }

  // A leitura do nome técnico (sweep de 19/10/2026, 3.14): o browser lia o
  // nome à sua maneira e propunha outra coisa que a ficha («A.S.S023.S.F»
  // dava CM «S.S0»). Agora pergunta ao servidor e só a resposta mais
  // recente conta: um pedido antigo cancela-se e, se chegar, não se usa.
  var leitura = 0;
  var pedido = null;
  technical.addEventListener("input", function () {
    var name = technical.value.trim();
    var atual = ++leitura;
    if (pedido) pedido.abort();
    pedido = null;
    if (!name) {
      if (notice) notice.textContent = "";
      return;
    }

    pedido = new AbortController();
    var query = new URLSearchParams({ handler: "NomeTecnico", nome: name });
    fetch("/Ferramentas?" + query, { credentials: "same-origin", signal: pedido.signal })
      .then(function (response) {
        if (!response.ok) throw new Error("Leitura do nome técnico indisponível");
        return response.json();
      })
      .then(function (lido) {
        if (atual !== leitura) return;
        if (notice) {
          notice.textContent = lido.leitura
            ? "Decodificado: " + lido.leitura + " (confirme)"
            : "Nome técnico guardado como está — o decode aparece na ficha.";
        }
        // Vindo do cartão do Job On, o tipo fica o escolhido.
        if (lido.tipo && !type.getAttribute("data-ferr-tipo-fixo") && type.value !== lido.tipo) {
          type.value = lido.tipo;
          type.dispatchEvent(new Event("change", { bubbles: true }));
        }
      })
      .catch(function () {
        // Sem resposta (rede ou sessão): o nome técnico grava-se na mesma e
        // a ficha mostra a leitura; o tipo fica o que está escolhido.
        if (atual === leitura && notice) notice.textContent = "Não foi possível ler o nome técnico agora: grava-se como está e a leitura aparece na ficha.";
      });
  });
})();

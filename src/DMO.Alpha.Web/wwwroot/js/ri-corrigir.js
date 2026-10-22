// A reparação na Reparação Interna (Diogo, 22/10/2026): sem botões nas
// linhas (padrão da beta). Na tabela, um clique escolhe a linha e o duplo
// clique (ou o Enter) abre-a; «Editar», no canto do cartão, abre a escolhida
// (ou a mais recente). Há uma janela só, fora das zonas que trocam: o
// abrir enche-a com a reparação e o número da linha, com a linha, a
// produção, o dia e a página da tabela (ou a vista do Histórico, para
// voltar a ela) e com um envio novo (operacao_id: um duplo envio grava uma
// vez). Os valores vão pela propriedade value, não pelo atributo: a página
// continua igual à que o servidor desenhou fora das zonas. Gravar (Guardar
// ou Anular) fecha a janela e troca só o que muda.
(function () {
  function novoEnvio() {
    if (window.crypto && typeof window.crypto.randomUUID === "function") return window.crypto.randomUUID();
    const bytes = new Uint8Array(16);
    if (window.crypto && window.crypto.getRandomValues) window.crypto.getRandomValues(bytes);
    else bytes.forEach((_, i) => { bytes[i] = Math.floor(Math.random() * 256); });
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const h = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
    return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
  }

  function abrir(linha) {
    const janela = document.getElementById("riCorrigirJanela");
    if (!janela || typeof janela.showModal !== "function") return;
    const form = janela.querySelector("form");
    const tabela = linha.closest("table");
    const dados = (tabela && tabela.dataset) || {};
    const por = (nome, valor) => {
      const campo = form.elements.namedItem(nome);
      if (campo) campo.value = valor ?? "";
    };
    por("operacao_id", novoEnvio());
    por("reparacao_id", linha.dataset.riReparacao);
    por("maquina", dados.riMaquina);
    por("producao", dados.riProducao);
    por("tipo", dados.riTipo);
    por("dia", dados.riDia);
    por("pagina", dados.riPagina);
    por("volta", dados.riVolta);
    por("numero", linha.dataset.riNumero);
    por("motivo", "");
    janela.querySelectorAll("[data-ri-numero-texto]").forEach((texto) => { texto.textContent = linha.dataset.riNumero; });
    janela.showModal();
    const numero = form.elements.namedItem("numero");
    if (numero) {
      numero.focus();
      numero.select();
    }
  }

  // O abrir canónico das listas (duplo clique ou Enter numa lista que só escolhe).
  document.addEventListener("dmo:list-open", (event) => {
    const linha = event.detail && event.detail.row;
    if (linha && linha.dataset && linha.dataset.riReparacao) abrir(linha);
  });

  // «Editar»: a linha escolhida da tabela, ou a primeira (a mais recente).
  // Em captura: corre antes do clique parcial da casa.
  document.addEventListener("click", (event) => {
    const editar = event.target.closest && event.target.closest("[data-ri-editar]");
    if (!editar) return;
    event.preventDefault();
    const tabela = document.getElementById(editar.dataset.riEditar);
    if (!tabela) return;
    const linha = tabela.querySelector("tr[data-ri-reparacao].selected") || tabela.querySelector("tr[data-ri-reparacao]");
    if (linha) abrir(linha);
  }, true);
})();

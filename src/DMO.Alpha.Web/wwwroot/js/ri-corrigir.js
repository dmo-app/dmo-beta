// Corrigir e anular na Reparação Interna (peça 2). Sem botões nas linhas
// (padrão da beta): clicar no número abre a janela de corrigir e o «×» a
// de anular. Há uma janela de cada, fora das zonas que trocam (como a do ×
// dos Pegamentos): o clique enche-a com a reparação e o número da linha, com
// a máquina, a produção e a página da tabela, e com um envio novo
// (operacao_id: um duplo envio grava uma vez). Os valores vão pela
// propriedade value, não pelo atributo: a página continua igual à que o
// servidor desenhou fora das zonas. Gravar fecha a janela
// (data-dmo-dialog-fecha-ao-enviar) e troca só a tabela e a mensagem.
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

  function abrir(janela, ligacao) {
    const form = janela.querySelector("form");
    const tabela = ligacao.closest("[data-ri-maquina]");
    const por = (nome, valor) => {
      const campo = form.elements.namedItem(nome);
      if (campo) campo.value = valor ?? "";
    };
    por("operacao_id", novoEnvio());
    por("reparacao_id", ligacao.dataset.riReparacao);
    por("maquina", tabela && tabela.dataset.riMaquina);
    por("producao", tabela && tabela.dataset.riProducao);
    por("tipo", tabela && tabela.dataset.riTipo);
    por("pagina", tabela && tabela.dataset.riPagina);
    por("numero", ligacao.dataset.riNumero);
    por("motivo", "");
    janela.querySelectorAll("[data-ri-numero-texto]").forEach((texto) => { texto.textContent = ligacao.dataset.riNumero; });
    janela.showModal();
    const numero = form.elements.namedItem("numero");
    if (numero) {
      numero.focus();
      numero.select();
    } else {
      form.elements.namedItem("motivo")?.focus();
    }
  }

  // Em captura: corre antes do clique parcial da casa, que de outro modo
  // seguia o href da linha (está dentro de data-dmo-parcial).
  document.addEventListener("click", (event) => {
    const ligacao = event.target.closest && event.target.closest("[data-ri-abre]");
    const janela = ligacao && document.getElementById(ligacao.dataset.riAbre);
    if (!janela || typeof janela.showModal !== "function") return;
    // O href (#riCorrigirJanela) não se segue: a âncora nova faria scroll.
    event.preventDefault();
    abrir(janela, ligacao);
  }, true);
})();

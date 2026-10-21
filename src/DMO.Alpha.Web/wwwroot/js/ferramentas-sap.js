// Utilização pelo SAP (Diogo, 20–21/10/2026): a pasta onde o SAP grava a
// lista (a ZPPF exportada ou o CSV do script da ZPPG) fica ligada neste
// browser (Chrome/Edge, File System Access). Ao abrir as Ferramentas, lê-se o
// ficheiro mais recente da pasta e, se for mais novo do que o último lido,
// vai para o servidor, que o lê e grava a utilização de cada ferramenta. Na
// pasta fica também o dmo-codigos.txt com os códigos a pedir ao SAP (nome
// técnico + lote das ferramentas à espera da utilização), que o script usa.
(function () {
  "use strict";

  const URL_SAP = "/Ferramentas/Definicoes?handler=UsoSap";
  const URL_IMPORTAR = "/Ferramentas/Definicoes?handler=ImportarUsoSap";
  const CODIGOS = "dmo-codigos.txt";
  const EXTENSOES = /\.(html?|txt|csv|xls|mhtml?)$/i;
  const BASE = "dmo-sap";
  const LOJA = "pasta";

  const cartao = document.querySelector("[data-ferr-sap]");
  const automatico = document.querySelector("[data-ferr-sap-auto]");
  if (!cartao && !automatico) return;

  const suportado = typeof window.showDirectoryPicker === "function" && "indexedDB" in window;
  const estado = cartao ? cartao.querySelector("[data-ferr-sap-estado]") : null;
  let aLer = false;

  function mostrarEstado(texto) {
    if (estado) estado.textContent = texto;
  }

  // As mensagens vão para a pilha do canto, como as do servidor.
  function mensagem(tipo, texto) {
    const caixa = document.createElement("div");
    const p = document.createElement("p");
    p.setAttribute("data-dmo-toast", tipo);
    p.setAttribute("role", "status");
    p.textContent = texto;
    caixa.appendChild(p);
    if (window.DMOLive && window.DMOLive.mostrarMensagens) window.DMOLive.mostrarMensagens(caixa);
    else mostrarEstado(texto);
  }

  function abrirBase() {
    return new Promise((resolve, reject) => {
      const pedido = indexedDB.open(BASE, 1);
      pedido.onupgradeneeded = () => pedido.result.createObjectStore(LOJA);
      pedido.onsuccess = () => resolve(pedido.result);
      pedido.onerror = () => reject(pedido.error);
    });
  }

  async function pastaGuardada() {
    const base = await abrirBase();
    return new Promise((resolve, reject) => {
      const pedido = base.transaction(LOJA, "readonly").objectStore(LOJA).get("pasta");
      pedido.onsuccess = () => resolve(pedido.result || null);
      pedido.onerror = () => reject(pedido.error);
    });
  }

  async function guardarPasta(pasta) {
    const base = await abrirBase();
    return new Promise((resolve, reject) => {
      const transacao = base.transaction(LOJA, "readwrite");
      transacao.objectStore(LOJA).put(pasta, "pasta");
      transacao.oncomplete = () => resolve();
      transacao.onerror = () => reject(transacao.error);
    });
  }

  function token() {
    const campo = document.querySelector("input[name='__RequestVerificationToken']");
    return campo ? campo.value : "";
  }

  async function infoServidor() {
    const resposta = await fetch(URL_SAP, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!resposta.ok) throw new Error("O servidor não respondeu (" + resposta.status + ").");
    return resposta.json();
  }

  // O ficheiro mais recente da pasta que pode ser a lista do SAP.
  async function maisRecente(pasta) {
    let melhor = null;
    for await (const entrada of pasta.values()) {
      if (entrada.kind !== "file" || entrada.name === CODIGOS || /^leia-me/i.test(entrada.name) || !EXTENSOES.test(entrada.name)) continue;
      const ficheiro = await entrada.getFile();
      if (!melhor || ficheiro.lastModified > melhor.lastModified) melhor = ficheiro;
    }
    return melhor;
  }

  async function escreverCodigos(pasta, codigos) {
    const ficheiro = await pasta.getFileHandle(CODIGOS, { create: true });
    const escrita = await ficheiro.createWritable();
    await escrita.write(codigos.join("\r\n") + (codigos.length ? "\r\n" : ""));
    await escrita.close();
  }

  // Ler a pasta: os códigos para o SAP e a lista mais recente. «forcar» lê
  // mesmo que o ficheiro já tenha sido lido (o «Ler agora»).
  async function sincronizar(pasta, forcar) {
    if (aLer) return;
    aLer = true;
    try {
      const info = await infoServidor();
      await escreverCodigos(pasta, info.codigos || []);
      const ficheiro = await maisRecente(pasta);
      if (!ficheiro) {
        mostrarEstado("Pasta «" + pasta.name + "» ligada. Ainda não tem nenhuma lista do SAP.");
        if (forcar) mensagem("aviso", "A pasta «" + pasta.name + "» ainda não tem nenhuma lista do SAP.");
        return;
      }
      if (!forcar && info.ficheiroModificadoEm && ficheiro.lastModified <= info.ficheiroModificadoEm) {
        mostrarEstado("Pasta «" + pasta.name + "» ligada. A lista mais recente («" + ficheiro.name + "») já foi lida.");
        return;
      }
      const corpo = new URLSearchParams();
      corpo.set("__RequestVerificationToken", token());
      corpo.set("conteudo", await ficheiro.text());
      corpo.set("ficheiro", ficheiro.name);
      corpo.set("modificado", String(ficheiro.lastModified));
      const resposta = await fetch(URL_IMPORTAR, {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/x-www-form-urlencoded", Accept: "application/json" },
        body: corpo
      });
      if (!resposta.ok) throw new Error("O servidor recusou a lista (" + resposta.status + ").");
      const resultado = await resposta.json();
      mostrarEstado("Pasta «" + pasta.name + "» ligada. Última lista lida: «" + ficheiro.name + "».");
      mensagem(resultado.gravado ? "ok" : "aviso", resultado.mensagem);
    } finally {
      aLer = false;
    }
  }

  async function permissao(pasta, pedir) {
    const modo = { mode: "readwrite" };
    if ((await pasta.queryPermission(modo)) === "granted") return true;
    return pedir && (await pasta.requestPermission(modo)) === "granted";
  }

  function falhou(erro) {
    mensagem("erro", "Não foi possível ler a pasta do SAP: " + (erro && erro.message ? erro.message : erro));
  }

  async function lerComPedido(forcar) {
    const pasta = await pastaGuardada();
    if (!pasta) {
      mensagem("aviso", "Ligue primeiro a pasta do SAP («Ligar pasta»).");
      return;
    }
    if (!(await permissao(pasta, true))) {
      mostrarEstado("Pasta «" + pasta.name + "» ligada, mas o browser não deu acesso. Carregue em «Ler agora» e escolha «Permitir».");
      return;
    }
    await sincronizar(pasta, forcar);
  }

  async function ligarPasta() {
    const pasta = await window.showDirectoryPicker({ id: "dmo-sap", mode: "readwrite" });
    await guardarPasta(pasta);
    await sincronizar(pasta, true);
  }

  async function copiarCodigos() {
    const info = await infoServidor();
    const codigos = info.codigos || [];
    if (codigos.length === 0) {
      mensagem("aviso", "Nenhuma ferramenta à espera da utilização tem nome técnico e lote.");
      return;
    }
    await navigator.clipboard.writeText(codigos.join("\r\n"));
    mensagem("ok", codigos.length + (codigos.length === 1 ? " código copiado." : " códigos copiados.") + " No SAP, cole-os no Equipment Code com «Carregar da área de transferência».");
  }

  // Ao abrir: sem pedir nada ao utilizador, só se o browser já deu acesso.
  async function aoAbrir() {
    const pasta = await pastaGuardada();
    if (!pasta) return;
    if (await permissao(pasta, false)) {
      await sincronizar(pasta, false);
    } else {
      mostrarEstado("Pasta «" + pasta.name + "» ligada. Carregue em «Ler agora» para o browser dar acesso outra vez.");
    }
  }

  if (cartao) {
    const ligar = cartao.querySelector("[data-ferr-sap-pasta]");
    const ler = cartao.querySelector("[data-ferr-sap-ler]");
    const copiar = cartao.querySelector("[data-ferr-sap-codigos]");
    copiar.addEventListener("click", () => copiarCodigos().catch(falhou));
    if (!suportado) {
      ligar.disabled = true;
      ler.disabled = true;
      mostrarEstado("Este browser não deixa ligar uma pasta. Use o Chrome ou o Edge; o «Copiar códigos» funciona na mesma.");
      return;
    }
    ligar.addEventListener("click", () => ligarPasta().catch((erro) => {
      if (erro && erro.name === "AbortError") return;
      falhou(erro);
    }));
    ler.addEventListener("click", () => lerComPedido(true).catch(falhou));
  }

  if (suportado) aoAbrir().catch(falhou);
})();

// Utilização pelo SAP (Diogo, 20–21/10/2026): a pasta onde o SAP grava a
// lista (a ZPPF exportada ou o CSV do script da ZPPG) fica ligada neste
// browser (Chrome/Edge, File System Access). Ao abrir as Ferramentas, lê-se o
// ficheiro mais recente da pasta e, se for mais novo do que o último lido,
// vai para o servidor, que o lê e grava a utilização de cada ferramenta. Na
// pasta fica também o dmo-codigos.txt com os códigos a pedir ao SAP (nome
// técnico + lote das ferramentas à espera da utilização, ou de todas com o
// «Pedir todas» ligado), que o script usa.
// O relatório de fabrico do SAP (o que cada produção fez) vem pela mesma
// pasta: vão para o servidor todos os ficheiros mais novos do que o último
// lido, do mais antigo para o mais novo, e o servidor reconhece cada um. O
// «Ler pasta» e a primeira leitura enviam todos (Diogo, 27/10/2026: só o
// mais recente deixava o das produções por ler quando o da % de uso era mais
// novo); o servidor não grava um ficheiro mais antigo do que o último lido
// do mesmo tipo.
// «ATUALIZAR DO SAP» (Diogo, 28/10/2026: «um actualizar deve ir tudo ao sap e
// meter nas ferramentas»): o DMO escreve na pasta os scripts com as
// definições do Admin › SAP, abre o DMO_SAP_DIARIO.vbs pelo endereço
// dmo-sap: (o INSTALAR.cmd liga-o uma vez em cada PC) e, quando o
// dmo-sap-estado.txt diz «feito», lê a pasta e grava tudo.
(function () {
  "use strict";

  // O cartão do Admin › SAP pede os scripts à página dele (data-ferr-sap-base)
  // e não grava pelo browser: o script envia tudo direto (DMO-582).
  const cartaoBase = document.querySelector("[data-ferr-sap]");
  const doAdmin = !!(cartaoBase && cartaoBase.hasAttribute("data-ferr-sap-admin"));
  const PAGINA = (cartaoBase && cartaoBase.getAttribute("data-ferr-sap-base")) || "/Ferramentas/Definicoes";
  const URL_SAP = "/Ferramentas/Definicoes?handler=UsoSap";
  const URL_IMPORTAR = "/Ferramentas/Definicoes?handler=ImportarUsoSap";
  const URL_SCRIPTS = PAGINA + "?handler=ScriptsSap";
  const URL_CODIGOS_REF = PAGINA + "?handler=CodigosSap";
  const PEDIDO = "dmo-codigos-pedido.txt";
  const ESTADO_SAP = "dmo-sap-estado.txt";
  // Os ficheiros que o DMO e os scripts escrevem na pasta: nunca vão para o servidor.
  const DO_DMO = /^(dmo-sap[-.]|dmo-codigos)/i;
  const CODIGOS = "dmo-codigos.txt";
  const EXTENSOES = /\.(html?|txt|csv|xls|mhtml?)$/i;
  const BASE = "dmo-sap";
  const LOJA = "pasta";

  const cartao = document.querySelector("[data-ferr-sap]");
  const automatico = document.querySelector("[data-ferr-sap-auto]");
  const admin = document.querySelector("[data-sap-admin-pasta]");
  if (!cartao && !automatico && !admin) return;

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

  // «Pedir todas» fica guardado neste browser, ao lado da pasta.
  async function pedirTodas() {
    const base = await abrirBase();
    return new Promise((resolve, reject) => {
      const pedido = base.transaction(LOJA, "readonly").objectStore(LOJA).get("todas");
      // Ligado por omissão (Diogo, 28/10/2026: «as ferramentas todas com % uso»).
      pedido.onsuccess = () => resolve(pedido.result !== false);
      pedido.onerror = () => reject(pedido.error);
    });
  }

  async function guardarTodas(todas) {
    const base = await abrirBase();
    return new Promise((resolve, reject) => {
      const transacao = base.transaction(LOJA, "readwrite");
      transacao.objectStore(LOJA).put(todas, "todas");
      transacao.oncomplete = () => resolve();
      transacao.onerror = () => reject(transacao.error);
    });
  }

  function mostrarTodas(todas) {
    const botao = cartao ? cartao.querySelector("[data-ferr-sap-todas]") : null;
    if (botao) botao.textContent = todas ? "Atualizar" : "Pedir todas";
  }

  async function esquecerPasta() {
    const base = await abrirBase();
    return new Promise((resolve, reject) => {
      const transacao = base.transaction(LOJA, "readwrite");
      transacao.objectStore(LOJA).delete("pasta");
      transacao.oncomplete = () => resolve();
      transacao.onerror = () => reject(transacao.error);
    });
  }

  // «Remover» só aparece com uma pasta ligada neste computador.
  function mostrarRemover(tem) {
    const remover = cartao ? cartao.querySelector("[data-ferr-sap-remover]") : null;
    if (remover) remover.hidden = !tem;
  }

  function token() {
    const campo = document.querySelector("input[name='__RequestVerificationToken']");
    return campo ? campo.value : "";
  }

  async function infoServidor() {
    const todas = suportado && (await pedirTodas());
    const resposta = await fetch(URL_SAP + (todas ? "&todas=true" : ""), { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!resposta.ok) throw new Error("O servidor não respondeu (" + resposta.status + ").");
    return resposta.json();
  }

  const MAXIMO = 10;

  // Os ficheiros da pasta que podem vir do SAP, do mais antigo para o mais novo.
  async function ficheirosDoSap(pasta) {
    const todos = [];
    for await (const entrada of pasta.values()) {
      if (entrada.kind !== "file" || DO_DMO.test(entrada.name) || /^leia-me/i.test(entrada.name) || !EXTENSOES.test(entrada.name)) continue;
      todos.push(await entrada.getFile());
    }
    return todos.sort((a, b) => a.lastModified - b.lastModified);
  }

  async function enviar(ficheiro) {
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
    return resposta.json();
  }

  async function escreverCodigos(pasta, codigos) {
    const ficheiro = await pasta.getFileHandle(CODIGOS, { create: true });
    const escrita = await ficheiro.createWritable();
    await escrita.write(codigos.join("\r\n") + (codigos.length ? "\r\n" : ""));
    await escrita.close();
  }

  async function escreverFicheiro(pasta, nome, dados) {
    const ficheiro = await pasta.getFileHandle(nome, { create: true });
    const escrita = await ficheiro.createWritable();
    await escrita.write(dados);
    await escrita.close();
  }

  // Os scripts do SAP com as definições do Admin › SAP, sempre os da app.
  async function escreverScripts(pasta) {
    const resposta = await fetch(URL_SCRIPTS, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!resposta.ok) throw new Error("O servidor não deu os scripts do SAP (" + resposta.status + ").");
    for (const f of await resposta.json()) {
      const bytes = Uint8Array.from(atob(f.base64), (c) => c.charCodeAt(0));
      await escreverFicheiro(pasta, f.nome, bytes);
    }
  }

  async function lerEstado(pasta) {
    try {
      const ficheiro = await (await pasta.getFileHandle(ESTADO_SAP)).getFile();
      return { texto: (await ficheiro.text()).trim(), quando: ficheiro.lastModified };
    } catch (erro) {
      return null;
    }
  }

  // Ler a pasta: os códigos para o SAP e os ficheiros novos. «forcar» lê
  // todos, mesmo os já lidos (o «Ler pasta»).
  async function sincronizar(pasta, forcar) {
    if (aLer) return;
    aLer = true;
    try {
      const info = await infoServidor();
      await escreverCodigos(pasta, info.codigos || []);
      try {
        await escreverScripts(pasta);
      } catch (erro) {
        // Sem os scripts novos a leitura das listas continua.
      }
      const ficheiros = await ficheirosDoSap(pasta);
      const ficheiro = ficheiros.length ? ficheiros[ficheiros.length - 1] : null;
      if (!ficheiro) {
        mostrarEstado("«" + pasta.name + "» · ainda sem listas do SAP");
        if (forcar) mensagem("aviso", "A pasta «" + pasta.name + "» ainda não tem nenhuma lista do SAP.");
        return;
      }
      if (!forcar && info.ficheiroModificadoEm && ficheiro.lastModified <= info.ficheiroModificadoEm) {
        mostrarEstado("«" + pasta.name + "» · a lista mais recente («" + ficheiro.name + "») já foi lida");
        return;
      }
      const novos = (forcar || !info.ficheiroModificadoEm
        ? ficheiros
        : ficheiros.filter((f) => f.lastModified > info.ficheiroModificadoEm)).slice(-MAXIMO);
      for (const novo of novos) {
        const resultado = await enviar(novo);
        mensagem(resultado.gravado ? "ok" : "aviso", resultado.mensagem);
      }
      mostrarEstado("«" + pasta.name + "» · última lista lida «" + ficheiro.name + "»");
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

  // A pasta ligada com acesso, ou a escolhida agora (o primeiro «Atualizar do SAP»).
  async function pastaComAcesso() {
    let pasta = await pastaGuardada();
    if (!pasta) {
      pasta = await window.showDirectoryPicker({ id: "dmo-sap", mode: "readwrite" });
      await guardarPasta(pasta);
      mostrarRemover(true);
    }
    if (!(await permissao(pasta, true))) {
      mostrarEstado("«" + pasta.name + "» · sem acesso: carregue outra vez e escolha «Permitir»");
      return null;
    }
    return pasta;
  }

  // O endereço dmo-sap: abre o DMO_SAP_DIARIO.vbs da pasta (o INSTALAR.cmd).
  function abrirSap(endereco) {
    const ligacao = document.createElement("a");
    ligacao.href = endereco;
    ligacao.hidden = true;
    document.body.appendChild(ligacao);
    ligacao.click();
    ligacao.remove();
  }

  let aAtualizar = false;

  // «Atualizar do SAP»: modo normal, data (inicio/fim aaaa-mm) ou ref.
  async function atualizar(pasta, pedido) {
    if (aAtualizar) {
      mensagem("aviso", "A leitura do SAP ainda está a correr.");
      return;
    }
    const partes = ["modo=" + pedido.modo];
    if (pedido.modo === "data") {
      if (!pedido.inicio) {
        mensagem("aviso", "Escolha o mês do início.");
        return;
      }
      partes.push("inicio=" + pedido.inicio);
      if (pedido.fim) {
        if (pedido.fim < pedido.inicio) {
          mensagem("aviso", "O fim é antes do início.");
          return;
        }
        partes.push("fim=" + pedido.fim);
      }
    }
    if (pedido.modo === "ref") {
      const url = URL_CODIGOS_REF + "&referencia=" + encodeURIComponent(pedido.referencia) + "&tipo=" + encodeURIComponent(pedido.tipo);
      const resposta = await fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } });
      if (!resposta.ok) throw new Error("O servidor não respondeu (" + resposta.status + ").");
      const codigos = (await resposta.json()).codigos || [];
      if (codigos.length === 0) {
        mensagem("aviso", "Nenhuma ferramenta da referência «" + pedido.referencia + "» tem nome técnico e lote.");
        return;
      }
      await escreverFicheiro(pasta, PEDIDO, codigos.join("\r\n") + "\r\n");
    }
    const comeco = Date.now();
    abrirSap("dmo-sap:" + partes.join("&"));
    aAtualizar = true;
    mostrarEstado("«" + pasta.name + "» · a ler do SAP…");
    mensagem("ok", "A ler do SAP. O DMO grava tudo quando acabar; pode continuar a trabalhar nesta página.");
    try {
      let arrancou = false;
      // Até 70 minutos (cada script tem no máximo 30).
      for (let volta = 0; volta < 840; volta++) {
        await new Promise((r) => setTimeout(r, 5000));
        const estadoSap = await lerEstado(pasta);
        if (!estadoSap || estadoSap.quando < comeco - 2000) {
          if (!arrancou && volta === 12) {
            mensagem("aviso", "O SAP não arrancou. Neste PC, carregue uma vez em «Instalar».");
            mostrarEstado("«" + pasta.name + "» · o SAP não arrancou");
            return;
          }
          continue;
        }
        arrancou = true;
        if (estadoSap.texto.startsWith("feito")) {
          if (!doAdmin) await sincronizar(pasta, false);
          mensagem("ok", "Leitura do SAP feita e gravada.");
          return;
        }
      }
      mensagem("aviso", "A leitura do SAP passou de 70 minutos. Veja o dmo-sap.log na pasta.");
    } finally {
      aAtualizar = false;
    }
  }

  // «Instalar neste PC»: os scripts na pasta e o INSTALAR.cmd para o duplo clique.
  async function instalar() {
    const pasta = await pastaComAcesso();
    if (!pasta) return;
    await escreverScripts(pasta);
    mensagem("ok", "Abra a pasta «" + pasta.name + "» e faça duplo clique no INSTALAR.cmd (só uma vez neste PC).");
  }

  // Apagar da pasta os ficheiros do SAP que o DMO já leu (Diogo, 22/10/2026:
  // «depois uma opção para apagar»). Só os que vieram do SAP e são tão ou
  // mais antigos do que o último lido; o dmo-codigos.txt, o LEIA-ME, o
  // script e o atalho ficam.
  async function apagarLidos() {
    const pasta = await pastaGuardada();
    if (!pasta) {
      mensagem("aviso", "Ligue primeiro a pasta do SAP (Admin › Definições › SAP).");
      return;
    }
    if (!(await permissao(pasta, true))) return;
    const info = await infoServidor();
    const lidos = info.ficheiroModificadoEm
      ? (await ficheirosDoSap(pasta)).filter((f) => f.lastModified <= info.ficheiroModificadoEm)
      : [];
    if (lidos.length === 0) {
      mensagem("aviso", "Não há ficheiros lidos para apagar na pasta «" + pasta.name + "».");
      return;
    }
    if (!window.confirm("Apagar " + lidos.length + (lidos.length === 1 ? " ficheiro já lido" : " ficheiros já lidos") + " da pasta «" + pasta.name + "»?")) return;
    for (const f of lidos) await pasta.removeEntry(f.name);
    mensagem("ok", lidos.length + (lidos.length === 1 ? " ficheiro apagado" : " ficheiros apagados") + " da pasta «" + pasta.name + "».");
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

  // Ligar ou desligar o «Pedir todas» e escrever logo o dmo-codigos.txt.
  async function alternarTodas() {
    const todas = !(await pedirTodas());
    await guardarTodas(todas);
    mostrarTodas(todas);
    const pasta = await pastaGuardada();
    let total = null;
    if (pasta && (await permissao(pasta, true))) {
      const codigos = (await infoServidor()).codigos || [];
      await escreverCodigos(pasta, codigos);
      total = codigos.length;
    }
    const escritos = total === null ? "" : " (" + total + (total === 1 ? " código" : " códigos") + " no dmo-codigos.txt)";
    mensagem("ok", (todas
      ? "A próxima leitura do SAP traz o uso de todas as ferramentas com nome técnico"
      : "A próxima leitura do SAP traz só as ferramentas à espera da utilização") + escritos + ".");
  }

  // Ao abrir: sem pedir nada ao utilizador, só se o browser já deu acesso.
  async function aoAbrir() {
    if (cartao) mostrarTodas(await pedirTodas());
    const pasta = await pastaGuardada();
    if (!pasta) return;
    mostrarRemover(true);
    if (doAdmin) {
      mostrarEstado("«" + pasta.name + "»");
      if (await permissao(pasta, false)) await escreverScripts(pasta);
      return;
    }
    if (await permissao(pasta, false)) {
      await sincronizar(pasta, false);
    } else {
      mostrarEstado("«" + pasta.name + "» · peça a um admin para ligar a pasta outra vez em Admin › Definições › SAP");
    }
  }

  function cancelado(erro) {
    if (erro && erro.name === "AbortError") return;
    falhou(erro);
  }

  // Admin › Definições › SAP: ligar a pasta deste computador.
  if (admin) {
    const estadoAdmin = admin.querySelector("[data-sap-admin-estado]");
    const botao = admin.querySelector("[data-sap-admin-ligar]");
    const mostrar = (pasta) => {
      if (estadoAdmin) estadoAdmin.textContent = pasta ? "«" + pasta.name + "»" : "Nenhuma pasta ligada";
    };
    if (!suportado) {
      botao.disabled = true;
      if (estadoAdmin) estadoAdmin.textContent = "este browser não liga pastas (use o Chrome ou o Edge)";
    } else {
      pastaGuardada().then(mostrar).catch(falhou);
      botao.addEventListener("click", () => window.showDirectoryPicker({ id: "dmo-sap", mode: "readwrite" })
        .then((pasta) => guardarPasta(pasta).then(() => {
          mostrar(pasta);
          mensagem("ok", "Pasta «" + pasta.name + "» ligada neste computador.");
        }))
        .catch(cancelado));
    }
  }

  if (cartao) {
    // O cartão das Ferramentas (SEC-01, 29/10/2026) já não tem «Atualizar do
    // SAP» nem «Instalar»: só o Admin › Definições › SAP tem os botões que
    // escrevem a senha nos scripts.
    const atualizarBotao = cartao.querySelector("[data-ferr-sap-atualizar]");
    const janela = cartao.querySelector("[data-ferr-sap-janela]");
    const instalarBotao = cartao.querySelector("[data-ferr-sap-instalar]");
    const copiar = cartao.querySelector("[data-ferr-sap-codigos]");
    const apagar = cartao.querySelector("[data-ferr-sap-apagar]");
    const todas = cartao.querySelector("[data-ferr-sap-todas]");
    const remover = cartao.querySelector("[data-ferr-sap-remover]");
    if (copiar) copiar.addEventListener("click", () => copiarCodigos().catch(falhou));
    if (!suportado) {
      if (atualizarBotao) atualizarBotao.disabled = true;
      if (instalarBotao) instalarBotao.disabled = true;
      if (apagar) apagar.disabled = true;
      if (todas) todas.disabled = true;
      mostrarEstado("este browser não liga pastas (use o Chrome ou o Edge); o «Copiar códigos» funciona na mesma");
      return;
    }
    if (atualizarBotao && janela && instalarBotao) {
      let modo = "normal";
      let datas = "entre";
      const escolher = (grupo, valor) => {
        janela.querySelectorAll("[" + grupo + "]").forEach((b) => {
          const ativo = b.getAttribute(grupo) === valor;
          b.classList.toggle("rp-modo-ativo", ativo);
          b.setAttribute("aria-pressed", ativo ? "true" : "false");
        });
      };
      janela.querySelectorAll("[data-ferr-sap-modo]").forEach((b) => b.addEventListener("click", () => {
        modo = b.getAttribute("data-ferr-sap-modo");
        escolher("data-ferr-sap-modo", modo);
        janela.querySelectorAll("[data-ferr-sap-parte]").forEach((p) => { p.hidden = p.getAttribute("data-ferr-sap-parte") !== modo; });
      }));
      janela.querySelectorAll("[data-ferr-sap-datas]").forEach((b) => b.addEventListener("click", () => {
        datas = b.getAttribute("data-ferr-sap-datas");
        escolher("data-ferr-sap-datas", datas);
        janela.querySelector("[data-ferr-sap-fim-campo]").hidden = datas === "desde";
      }));
      atualizarBotao.addEventListener("click", () => janela.showModal());
      janela.querySelector("[data-ferr-sap-correr]").addEventListener("click", () => {
        const pedido = {
          modo: modo,
          inicio: janela.querySelector("[data-ferr-sap-inicio]").value,
          fim: datas === "desde" ? "" : janela.querySelector("[data-ferr-sap-fim]").value,
          referencia: janela.querySelector("[data-ferr-sap-referencia]").value.trim(),
          tipo: janela.querySelector("[data-ferr-sap-tipo]").value
        };
        if (modo === "ref" && !pedido.referencia) {
          mensagem("aviso", "Escreva a referência.");
          return;
        }
        janela.close();
        pastaComAcesso()
          .then((pasta) => pasta && atualizar(pasta, pedido))
          .catch(cancelado);
      });
      instalarBotao.addEventListener("click", () => instalar().catch(cancelado));
    }
    if (apagar) apagar.addEventListener("click", () => apagarLidos().catch(falhou));
    if (todas) todas.addEventListener("click", () => alternarTodas().catch(falhou));
    if (remover) remover.addEventListener("click", () => esquecerPasta().then(() => {
      mostrarRemover(false);
      mostrarEstado("Nenhuma pasta ligada");
    }).catch(falhou));
  }

  if (suportado) aoAbrir().catch(falhou);
})();

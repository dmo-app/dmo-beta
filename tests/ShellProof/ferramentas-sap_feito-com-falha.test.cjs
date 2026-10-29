const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A2/SAP-01 (sweep Codex 29/10/2026): o dmo-sap-estado.txt dizia «feito» mesmo
// quando um script ficou preso ou saiu com erro (Sap/DMO_SAP_DIARIO.vbs); o
// browser mostrava sucesso sem ter gravado nada novo. Agora o diário escreve
// «erro ...» nesse caso, e o «Atualizar do SAP» tem de distinguir as duas.

function pastaFalsa(ficheiros) {
  const mapa = new Map(Object.entries(ficheiros).map(([nome, lastModified]) => [nome, { lastModified, texto: 'conteudo de ' + nome }]));
  return {
    name: 'SAP',
    mapa,
    queryPermission: async () => 'granted',
    requestPermission: async () => 'granted',
    async *values() {
      for (const [nome, f] of [...mapa]) {
        yield { kind: 'file', name: nome, getFile: async () => ({ name: nome, lastModified: f.lastModified, text: async () => f.texto }) };
      }
    },
    async getFileHandle(nome) {
      return {
        getFile: async () => {
          const f = mapa.get(nome);
          if (!f) throw new Error('não existe: ' + nome);
          return { name: nome, lastModified: f.lastModified, text: async () => f.texto };
        },
        createWritable: async () => ({ write: async v => { mapa.set(nome, { lastModified: Date.now(), texto: String(v) }); }, close: async () => {} })
      };
    },
    async removeEntry(nome) { mapa.delete(nome); }
  };
}

function indexedDBFalso(guardado) {
  return {
    open() {
      const pedido = {};
      setTimeout(() => {
        pedido.result = {
          createObjectStore() {},
          transaction() {
            const tx = {};
            tx.objectStore = () => ({
              get(chave) { const r = { result: guardado[chave] }; setTimeout(() => r.onsuccess && r.onsuccess(), 0); return r; },
              put(valor, chave) { guardado[chave] = valor; setTimeout(() => tx.oncomplete && tx.oncomplete(), 0); },
              delete(chave) { delete guardado[chave]; setTimeout(() => tx.oncomplete && tx.oncomplete(), 0); }
            });
            return tx;
          }
        };
        if (pedido.onupgradeneeded) pedido.onupgradeneeded();
        pedido.onsuccess();
      }, 0);
      return pedido;
    }
  };
}

const espera = ms => new Promise(r => setTimeout(r, ms));
async function ate(condicao, tentativas = 400) {
  for (let i = 0; i < tentativas; i++) { if (condicao()) return; await espera(10); }
  throw new Error('a condição nunca se cumpriu');
}

async function pagina(ficheiros) {
  const pasta = pastaFalsa(ficheiros);
  const dom = new JSDOM(`<!doctype html><body>
  <section data-ferr-sap>
    <p data-ferr-sap-estado></p>
    <button data-ferr-sap-atualizar>Atualizar do SAP</button>
    <dialog data-ferr-sap-janela>
      <div class="rp-modo"><button type="button" data-ferr-sap-modo="normal" aria-pressed="true">Como nas definições</button></div>
      <div data-ferr-sap-parte="normal"></div>
      <div data-ferr-sap-parte="data" hidden><label data-ferr-sap-fim-campo><input data-ferr-sap-inicio /><input data-ferr-sap-fim /></label></div>
      <div data-ferr-sap-parte="ref" hidden><input data-ferr-sap-referencia /><select data-ferr-sap-tipo></select></div>
      <button type="button" data-dmo-dialog-close>Cancelar</button>
      <button type="button" data-ferr-sap-correr>Atualizar</button>
    </dialog>
    <button data-ferr-sap-instalar></button>
    <button data-ferr-sap-codigos></button>
    <button data-ferr-sap-apagar>Apagar lidos</button>
    <button data-ferr-sap-todas></button>
    <button data-ferr-sap-remover hidden></button>
  </section></body>`, { url: 'http://dmo.test/Ferramentas/Definicoes', runScripts: 'outside-only' });
  const w = dom.window;
  // O jsdom não tem showModal: abrir e fechar mexem só no atributo open.
  w.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  w.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
  w.showDirectoryPicker = () => {};
  Object.defineProperty(w, 'indexedDB', { value: indexedDBFalso({ pasta }), configurable: true });
  const mensagens = [];
  w.DMOLive = { mostrarMensagens: caixa => mensagens.push({ tipo: caixa.querySelector('p').getAttribute('data-dmo-toast'), texto: caixa.textContent }) };
  w.fetch = async (url) => {
    const json = valor => ({ ok: true, status: 200, json: async () => valor });
    if (url.includes('handler=ScriptsSap')) return json([]);
    return json({ codigos: [], ficheiroModificadoEm: null });
  };
  w.eval(fs.readFileSync(path.join(assets, 'js/ferramentas-sap.js'), 'utf8'));
  return { w, doc: w.document, pasta, mensagens };
}

test('Atualizar do SAP: quando o diário escreve «erro», o cartão mostra falha e não «feito»', async () => {
  const { w, doc, pasta, mensagens } = await pagina({});
  doc.querySelector('[data-ferr-sap-atualizar]').click();
  doc.querySelector('[data-ferr-sap-correr]').click();
  // O diário (DMO_SAP_DIARIO.vbs) escreveria aqui "a correr ..." e depois
  // "erro DMO_USO_SAP.vbs:codigo1 ..." quando um script sai com erro.
  await espera(50);
  await pasta.getFileHandle('dmo-sap-estado.txt').then(h => h.createWritable()).then(w2 => w2.write('erro DMO_USO_SAP.vbs:codigo1 2026-10-29 10:00'));
  await ate(() => mensagens.some(m => m.tipo === 'erro' && /falhou/.test(m.texto)), 800);
  assert.ok(!mensagens.some(m => /feita e gravada/.test(m.texto)), 'não deve mostrar sucesso quando o diário falhou');
  assert.match(doc.querySelector('[data-ferr-sap-estado]').textContent, /falhou/);
  w.close();
}, 15000);

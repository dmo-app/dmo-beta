const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// DMO-445 (Diogo, 27/10/2026): imagem do artigo escolhida ou colada (Ctrl+V)
// passa pela janela do recorte; «Guardar recorte» grava logo artigo.png na
// pasta da referência e a anterior fica guardada. Cancelar não grava nada.

// Uma pasta geral de mentira, em memória, com a mesma forma da File System Access API.
function pastaFalsa(inicial) {
  const ficheiros = new Map(Object.entries(inicial || {}));
  const dir = {
    ficheiros,
    async *values() {
      for (const nome of [...ficheiros.keys()]) {
        yield { kind: 'file', name: nome, getFile: async () => ({ conteudo: ficheiros.get(nome), type: 'image/png' }) };
      }
    },
    async getFileHandle(nome) {
      return {
        createWritable: async () => ({
          write: async blob => { ficheiros.set(nome, blob && blob.conteudo !== undefined ? blob.conteudo : blob); },
          close: async () => {}
        })
      };
    },
    async removeEntry(nome) { ficheiros.delete(nome); }
  };
  const subpastas = new Map();
  const raiz = {
    subpastas,
    queryPermission: async () => 'granted',
    requestPermission: async () => 'granted',
    async getDirectoryHandle(nome, opcoes) {
      if (!subpastas.has(nome)) {
        if (!(opcoes && opcoes.create)) throw new Error('NotFound');
        subpastas.set(nome, dir);
      }
      return subpastas.get(nome);
    }
  };
  if (inicial) subpastas.set('5810T188', dir);
  return { raiz, dir };
}

function indexedDBFalso(valor) {
  return {
    open() {
      const pedido = {};
      setTimeout(() => {
        const db = {
          createObjectStore() {},
          close() {},
          transaction() {
            const tx = {};
            tx.objectStore = () => ({ get: () => { const p = { result: valor }; return p; } });
            setTimeout(() => tx.oncomplete && tx.oncomplete(), 0);
            return tx;
          }
        };
        pedido.result = db;
        if (pedido.onupgradeneeded) pedido.onupgradeneeded();
        pedido.onsuccess();
      }, 0);
      return pedido;
    }
  };
}

async function folha(raiz, recorte) {
  const dom = new JSDOM(`<!doctype html><body><form data-jof-folha="editar">
    <input type="text" id="campo" />
    <button type="button" data-jof-artigo-retirar hidden>x</button>
    <label data-jof-artigo="5810T188" data-jof-artigo-pode="1">
      <input type="file" data-jof-artigo-ficheiro />
      <img data-jof-artigo-img hidden />
      <svg data-jof-artigo-vazio></svg>
    </label>
    <p data-jof-artigo-aviso hidden></p>
  </form></body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
  const w = dom.window;
  w.showDirectoryPicker = () => {};
  Object.defineProperty(w, 'indexedDB', { value: indexedDBFalso(raiz), configurable: true });
  w.URL.createObjectURL = () => 'blob:falso';
  w.URL.revokeObjectURL = () => {};
  // A imagem «carrega» sempre; o canvas não existe no jsdom e a folha grava a imagem como veio.
  w.Image = class { set src(v) { this._s = v; setTimeout(() => this.onload && this.onload(), 0); } get src() { return this._s; } get naturalWidth() { return 10; } get naturalHeight() { return 10; } };
  w.HTMLCanvasElement.prototype.getContext = () => null;
  w.fetch = async src => ({ blob: async () => ({ conteudo: src }) });
  const pedidos = [];
  w.dmoRecortarArtigo = async (src, referencia) => { pedidos.push({ src, referencia }); return recorte(src); };
  w.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  await espera(150);
  return { w, doc: w.document, pedidos };
}
const espera = ms => new Promise(r => setTimeout(r, ms));
async function ate(condicao) {
  for (let i = 0; i < 300; i++) { if (condicao()) return; await espera(10); }
  throw new Error('a condição nunca se cumpriu');
}
function escolher(w, doc, conteudo) {
  const entrada = doc.querySelector('[data-jof-artigo-ficheiro]');
  const ficheiro = new w.File([conteudo], 'foto.png', { type: 'image/png' });
  Object.defineProperty(entrada, 'files', { value: [ficheiro], configurable: true });
  entrada.dispatchEvent(new w.Event('change', { bubbles: true }));
}
function colar(w, alvo, tipo) {
  const e = new w.Event('paste', { bubbles: true, cancelable: true });
  const ficheiro = new w.File(['colado'], 'colado.png', { type: tipo });
  e.clipboardData = { items: [{ kind: 'file', type: tipo, getAsFile: () => ficheiro }] };
  alvo.dispatchEvent(e);
  return e;
}

test('Guardar recorte grava na pasta da referência o recorte, não a imagem inteira', async () => {
  const { raiz, dir } = pastaFalsa();
  const { w, doc, pedidos } = await folha(raiz, () => 'data:image/png;base64,RECORTE');
  escolher(w, doc, 'inteira');
  await ate(() => dir.ficheiros.has('artigo.png'));
  assert.equal(pedidos.length, 1);
  assert.equal(pedidos[0].referencia, '5810T188');
  assert.match(pedidos[0].src, /^data:image\/png;base64,/);
  assert.equal(dir.ficheiros.get('artigo.png'), 'data:image/png;base64,RECORTE');
  assert.equal([...raiz.subpastas.keys()].join(), '5810T188');
  // E a imagem aparece no cartão.
  const img = doc.querySelector('[data-jof-artigo-img]');
  assert.equal(img.hidden, false);
  assert.ok(img.getAttribute('src'));
  w.close();
});

test('Guardar recorte com imagem anterior: a anterior fica guardada e o artigo.png é o novo', async () => {
  const { raiz, dir } = pastaFalsa({ 'artigo.png': 'ANTIGA' });
  const { w, doc } = await folha(raiz, () => 'data:image/png;base64,NOVA');
  escolher(w, doc, 'qualquer');
  await ate(() => dir.ficheiros.get('artigo.png') === 'data:image/png;base64,NOVA');
  const arquivadas = [...dir.ficheiros.entries()].filter(([nome]) => /^artigo-anterior-.+\.png$/.test(nome));
  assert.equal(arquivadas.length, 1);
  assert.equal(arquivadas[0][1], 'ANTIGA');
  w.close();
});

test('Cancelar o recorte não grava nada e a imagem anterior fica como estava', async () => {
  const { raiz, dir } = pastaFalsa({ 'artigo.png': 'ANTIGA' });
  const { w, doc, pedidos } = await folha(raiz, () => null);
  escolher(w, doc, 'qualquer');
  await ate(() => pedidos.length === 1);
  await espera(150);
  assert.deepEqual([...dir.ficheiros.keys()], ['artigo.png']);
  assert.equal(dir.ficheiros.get('artigo.png'), 'ANTIGA');
  w.close();
});

test('Colar uma imagem (Ctrl+V) fora dos campos abre o recorte e grava o recorte', async () => {
  const { raiz, dir } = pastaFalsa();
  const { w, doc, pedidos } = await folha(raiz, () => 'data:image/png;base64,COLADO');
  const e = colar(w, doc.body, 'image/png');
  assert.equal(e.defaultPrevented, true);
  await ate(() => dir.ficheiros.get('artigo.png') === 'data:image/png;base64,COLADO');
  assert.equal(pedidos.length, 1);
  w.close();
});

test('Colar dentro de um campo de escrita fica no campo e não grava imagem', async () => {
  const { raiz, dir } = pastaFalsa();
  const { w, doc, pedidos } = await folha(raiz, () => 'data:image/png;base64,COLADO');
  const e = colar(w, doc.getElementById('campo'), 'image/png');
  await espera(150);
  assert.equal(e.defaultPrevented, false);
  assert.equal(pedidos.length, 0);
  assert.equal(dir.ficheiros.size, 0);
  w.close();
});

test('Colar algo que não é imagem não abre o recorte', async () => {
  const { raiz, dir } = pastaFalsa();
  const { w, doc, pedidos } = await folha(raiz, () => 'x');
  colar(w, doc.body, 'text/plain');
  await espera(150);
  assert.equal(pedidos.length, 0);
  assert.equal(dir.ficheiros.size, 0);
  w.close();
});

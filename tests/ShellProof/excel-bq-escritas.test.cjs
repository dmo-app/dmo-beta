// O Excel das Boquilhas, achados S03 e S04 do relatório do dmo-Improvements
// de 28/10/2026 (0.3.0-beta.211), com o script da página
// (wwwroot/js/dmo-excel-bq.js) e um ficheiro, um IndexedDB e um servidor a
// fingir. O servidor só responde quando o teste deixa (gate).
// S03: um registo durante uma escrita lenta também chega ao Excel.
// S04: dois separadores não gravam uma resposta antiga por cima da nova.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const source = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/js/dmo-excel-bq.js'), 'utf8');
const settle = async () => { for (let i = 0; i < 40; i++) await new Promise((r) => setImmediate(r)); };
function deferred() { let resolve; const promise = new Promise((r) => { resolve = r; }); return { promise, resolve }; }

// Web Locks a fingir, partilhado pelos separadores do mesmo browser.
function locksDoBrowser() {
  let fila = Promise.resolve();
  return {
    request(nome, trabalho) {
      const vez = fila.then(() => trabalho());
      fila = vez.catch(() => {});
      return vez;
    }
  };
}

function excel({ comTrinco = true } = {}) {
  let record;
  let serverVersion = 'v1';
  let fileContent = 'livro-inicial';
  const posts = [];
  const handle = {
    name: 'Saida Entrada BQ 2026.xlsx',
    queryPermission: async () => 'granted',
    getFile: async () => ({ name: 'Saida Entrada BQ 2026.xlsx', content: fileContent }),
    createWritable: async () => { let pending; return { write: async (b) => { pending = b; }, close: async () => { fileContent = pending; } }; }
  };
  record = { handle, desde: '2026-10-28', versao: 'v0', escritas: [] };
  const indexedDB = { open() {
    const request = {};
    queueMicrotask(() => {
      request.result = { close() {}, transaction() {
        const tx = { objectStore() { return {
          get() { return { result: { ...record } }; },
          put(value) { record = { ...value }; return {}; }
        }; } };
        queueMicrotask(() => tx.oncomplete && tx.oncomplete());
        return tx;
      } };
      request.onsuccess();
    });
    return request;
  } };
  const locks = locksDoBrowser();
  function separador() {
    const eventos = new Map();
    const document = { currentScript: null, querySelector: () => null, addEventListener: (n, f) => eventos.set(n, f) };
    const window = { showOpenFilePicker() {}, showDirectoryPicker() {}, indexedDB, DMOLive: { toast() {} } };
    const fetch = async (url, options = {}) => {
      if (options.method !== 'POST') return { ok: true, json: async () => ({ versao: serverVersion }) };
      const gate = deferred();
      const captured = serverVersion;
      posts.push({ gate, version: captured, input: options.body.values.get('livro').content });
      await gate.promise;
      return { ok: true, headers: { get: (k) => (k === 'X-Dmo-Excel-Versao' ? captured : null) }, blob: async () => 'livro-' + captured };
    };
    class FormData { constructor() { this.values = new Map(); } append(k, v) { this.values.set(k, v); } }
    const navigator = comTrinco ? { locks } : {};
    vm.runInNewContext(source, { window, document, navigator, indexedDB, fetch, FormData, Date, Promise, JSON, String, decodeURIComponent });
    return { eventos };
  }
  return { separador, posts, setVersion: (v) => { serverVersion = v; }, get file() { return fileContent; }, get record() { return record; } };
}

test('S03: um registo durante uma escrita lenta volta a escrever e o Excel fica com ele', async () => {
  const f = excel();
  const a = f.separador();
  await settle();
  assert.equal(f.posts.length, 1);
  f.setVersion('v2');
  a.eventos.get('dmo:troca')({ detail: {} });
  await settle();
  assert.equal(f.posts.length, 1, 'espera pela escrita que está a decorrer');
  f.posts[0].gate.resolve();
  await settle();
  assert.equal(f.posts.length, 2, 'escreve outra vez');
  assert.equal(f.posts[1].input, 'livro-v1', 'a partir do livro acabado de gravar');
  f.posts[1].gate.resolve();
  await settle();
  assert.equal(f.file, 'livro-v2');
  assert.equal(f.record.versao, 'v2');
});

test('S03: sem registo novo durante a escrita, escreve uma só vez', async () => {
  const f = excel();
  f.separador();
  await settle();
  f.posts[0].gate.resolve();
  await settle();
  assert.equal(f.posts.length, 1);
  assert.equal(f.file, 'livro-v1');
});

test('S04: dois separadores escrevem um de cada vez, e o Excel fica com o mais novo', async () => {
  const f = excel();
  f.separador();
  await settle();
  f.setVersion('v2');
  f.separador();
  await settle();
  assert.equal(f.posts.length, 1, 'o segundo espera pelo primeiro antes de ler o livro');
  f.posts[0].gate.resolve();
  await settle();
  assert.equal(f.posts.length, 2);
  assert.equal(f.posts[1].input, 'livro-v1', 'o segundo lê o livro já gravado pelo primeiro');
  f.posts[1].gate.resolve();
  await settle();
  assert.equal(f.file, 'livro-v2');
  assert.equal(f.record.versao, 'v2');
});

test('sem Web Locks no browser, um separador escreve como antes', async () => {
  const f = excel({ comTrinco: false });
  f.separador();
  await settle();
  f.posts[0].gate.resolve();
  await settle();
  assert.equal(f.file, 'livro-v1');
  assert.equal(f.record.versao, 'v1');
});

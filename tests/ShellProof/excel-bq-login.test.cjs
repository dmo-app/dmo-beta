const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const source = fs.readFileSync(process.env.BQ_EXCEL_SCRIPT || path.resolve(__dirname,
  '../../src/DMO.Alpha.Web/wwwroot/js/dmo-excel-bq.js'), 'utf8');
const settle = async () => { for (let i = 0; i < 50; i++) await new Promise(r => setImmediate(r)); };

function pagina({ ligado = false, html = true, redirecionado = false } = {}) {
  const eventos = new Map(), avisos = [];
  let conteudo = ligado ? 'excel-original' : null, criados = 0, escritas = 0;
  const handle = {
    name: 'Saida Entrada BQ 2026.xlsx',
    queryPermission: async () => 'granted', requestPermission: async () => 'granted',
    getFile: async () => ({ name: handle.name, size: conteudo ? conteudo.length : 0 }),
    createWritable: async () => { escritas++; return { write: async b => { conteudo = b; }, close: async () => {} }; }
  };
  let record = ligado ? { handle, desde: '2026-11-01', versao: 'anterior' } : null;
  const indexedDB = { open() {
    const request = {};
    queueMicrotask(() => {
      request.result = { close() {}, transaction() {
        const tx = { objectStore: () => ({
          get: () => ({ result: record && { ...record } }),
          put: value => { record = { ...value }; return {}; }
        }) };
        queueMicrotask(() => tx.oncomplete?.());
        return tx;
      } };
      request.onsuccess();
    }); return request;
  } };
  const window = { indexedDB, DMOLive: { toast: msg => avisos.push(msg) },
    showOpenFilePicker() {}, showDirectoryPicker: async () => ({
      getFileHandle: async (nom, options) => {
        if (!conteudo && !options?.create) throw Object.assign(new Error(), { name: 'NotFoundError' });
        if (!conteudo) criados++;
        return handle;
      }
    }) };
  const fetch = async url => url.includes('ExcelVersao')
    ? { ok: true, json: async () => ({ versao: 'nova' }) }
    : { ok: true, redirected: redirecionado, headers: { get: key => key === 'Content-Type'
      ? html ? 'text/html; charset=utf-8' : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : null },
      blob: async () => html ? '<html>Entrar — Portal DMO</html>' : 'excel-valido' };
  const document = { currentScript: null, querySelector: () => null, addEventListener: (n, f) => eventos.set(n, f) };
  class FormData { append() {} }
  vm.runInNewContext(source, { window, document, indexedDB, navigator: {}, fetch, FormData,
    Date, Intl, Promise, JSON, String, Number, decodeURIComponent, setInterval() {} });
  return { window, avisos, get conteudo() { return conteudo; }, get criados() { return criados; },
    get escritas() { return escritas; }, get versao() { return record?.versao; },
    escolher: () => eventos.get('click')({ target: { closest: () => ({ hasAttribute: n => n === 'data-bq-excel-pasta' }) } }) };
}

test('Escolher a pasta com a sessão terminada não cria um falso Excel com a página de login', async () => {
  const p = pagina({ redirecionado: true });
  p.escolher(); await settle();
  assert.equal(p.criados, 0);
  assert.equal(p.escritas, 0);
  assert.equal(p.conteudo, null);
  assert.ok(p.avisos.some(a => /Volte a entrar/.test(a)));
});

test('Atualizar com uma resposta HTML preserva o Excel existente e não marca os movimentos como escritos', async () => {
  const p = pagina({ ligado: true });
  await settle(); await p.window.DMOExcelBq.escrever(true, true); await settle();
  assert.equal(p.conteudo, 'excel-original');
  assert.equal(p.escritas, 0);
  assert.equal(p.versao, 'anterior');
  assert.ok(p.avisos.some(a => /Volte a entrar/.test(a)));
});

test('Escolher uma pasta vazia com uma resposta Excel válida cria o livro', async () => {
  const p = pagina({ html: false });
  p.escolher(); await settle();
  assert.equal(p.criados, 1);
  assert.equal(p.conteudo, 'excel-valido');
  assert.ok(p.avisos.some(a => /Criado o Excel/.test(a)));
});

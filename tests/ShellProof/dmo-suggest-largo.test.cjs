const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Diogo, 04/11/2026: «expandir largura do dropdown para mostrar tudo». A lista das
// ferramentas do Job On (data-dmo-suggest-largo) fica tão larga quanto o texto, sem sair
// do ecrã; as outras listas continuam com a largura do campo.
// O jsdom não desenha: o campo e a largura natural da lista são fingidos.
async function abrir({ largo, ecra, campo: caixa, texto }) {
  const dom = new JSDOM(`<!doctype html><body>
    <div ${largo ? 'data-dmo-suggest-largo' : ''}><select name="bq_tool_id" data-dmo-filter><option value="">Escreva</option></select></div>
    </body>`, { url: 'http://dmo.test/JobOn/Folha', runScripts: 'outside-only' });
  const { window } = dom;
  Object.defineProperty(window.document.documentElement, 'clientWidth', { configurable: true, get: () => ecra });
  window.eval(fs.readFileSync(path.join(assets, 'js/dmo-suggest.js'), 'utf8'));
  window.DMOSuggest.init();
  const select = window.document.querySelector('select');
  select.dmoSource = async () => ({ items: [{ value: 'T1', label: 'T269 · Lote 10 · B3 · 72%' }] });
  const campo = window.document.querySelector('input.dmo-suggest-combo');
  const lista = window.document.querySelector('.dmo-suggest-list');
  const rect = (left, width) => ({ left, right: left + width, width, top: 300, bottom: 334, height: 34 });
  campo.getBoundingClientRect = () => rect(caixa.left, caixa.width);
  // «max-content» dá a largura do texto; sem ela, a que ficou escrita.
  lista.getBoundingClientRect = () => rect(0, lista.style.width === 'max-content' ? texto : parseFloat(lista.style.width) || 0);
  campo.focus();
  await new Promise((pronto) => window.setTimeout(pronto, 20));
  return { dom, lista };
}

test('a lista das ferramentas do Job On fica com a largura do texto todo', async () => {
  const { dom, lista } = await abrir({ largo: true, ecra: 1366, campo: { left: 452, width: 178 }, texto: 300 });
  try {
    assert.equal(lista.hidden, false);
    assert.equal(lista.style.width, '300px', 'mais larga do que o campo, para mostrar tudo');
    assert.equal(lista.style.left, '452px', 'começa onde começa o campo');
  } finally { dom.window.close(); }
});

test('a meio ecrã a lista larga encosta à esquerda para não sair do ecrã', async () => {
  const { dom, lista } = await abrir({ largo: true, ecra: 683, campo: { left: 500, width: 160 }, texto: 300 });
  try {
    assert.equal(lista.style.width, '300px');
    assert.equal(lista.style.left, (683 - 4 - 300) + 'px', 'a borda direita fica 4 px dentro do ecrã');
  } finally { dom.window.close(); }
});

test('com texto mais largo do que o ecrã a lista fica com a largura do ecrã', async () => {
  const { dom, lista } = await abrir({ largo: true, ecra: 320, campo: { left: 100, width: 160 }, texto: 500 });
  try {
    assert.equal(lista.style.width, (320 - 8) + 'px');
    assert.equal(lista.style.left, '4px');
  } finally { dom.window.close(); }
});

test('com texto curto a lista nunca fica mais estreita do que o campo', async () => {
  const { dom, lista } = await abrir({ largo: true, ecra: 1366, campo: { left: 452, width: 178 }, texto: 120 });
  try {
    assert.equal(lista.style.width, '178px');
  } finally { dom.window.close(); }
});

test('as outras listas continuam com a largura do campo', async () => {
  const { dom, lista } = await abrir({ largo: false, ecra: 1366, campo: { left: 452, width: 178 }, texto: 300 });
  try {
    assert.equal(lista.style.width, '178px');
    assert.equal(lista.style.left, '452px');
  } finally { dom.window.close(); }
});

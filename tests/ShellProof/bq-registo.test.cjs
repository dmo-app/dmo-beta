const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Boquilhas → Registo em miniatura: o painel lateral e o registo aberto
// (zona); sem a procura (Diogo, 22/10/2026). Abrir outra máquina no painel
// troca só o registo (DmoTroca) e traz o registo à vista; gravar um
// movimento não mexe no scroll.
function abrirPagina() {
  const dom = new JSDOM(`<!doctype html><body>
    <aside id="bqCurrentLines"><div id="bqCurrentLines-list" data-dmo-list="Linhas"><button data-dmo-row id="b1">B1</button></div></aside>
    <section id="registo" data-dmo-troca>
      <div id="bqRegistoAberto" data-dmo-regiao><div class="card register-open">Registo</div></div>
      <div id="bqRegistoMovimento" data-dmo-regiao><form><input id="quantity"></form></div>
    </section></body>`, { url: 'http://dmo.test/Boquilhas?trace_id=287', runScripts: 'outside-only' });
  const { window } = dom;
  const deslizou = [];
  window.Element.prototype.scrollIntoView = function () { deslizou.push(this.className); };
  window.eval(fs.readFileSync(path.join(assets, 'js/bq-registo.js'), 'utf8'));
  const abrir = (id) => window.document.getElementById(id).closest('[data-dmo-list]')
    .dispatchEvent(new window.CustomEvent('dmo:list-open', { bubbles: true, detail: { row: window.document.getElementById(id) } }));
  const trocou = (zonas) => window.document.dispatchEvent(new window.CustomEvent('dmo:troca', { detail: { zonas } }));
  return { dom, window, document: window.document, abrir, trocou, deslizou };
}

test('gravar um movimento não mexe no scroll', () => {
  const { dom, trocou, deslizou } = abrirPagina();
  try {
    trocou(['bqRegistoMovimento']);
    assert.deepEqual(deslizou, []);
  } finally {
    dom.window.close();
  }
});

test('o registo aberto fora do ecrã vem à vista', () => {
  const { dom, window, abrir, trocou, deslizou } = abrirPagina();
  try {
    window.HTMLElement.prototype.getBoundingClientRect = function () { return { top: -400 }; };
    abrir('b1');
    trocou(['bqRegistoAberto']);
    assert.deepEqual(deslizou, ['card register-open']);
  } finally {
    dom.window.close();
  }
});

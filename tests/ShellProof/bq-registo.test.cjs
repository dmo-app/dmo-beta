const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Boquilhas → Registo em miniatura: o painel lateral, a procura (fica) e o
// registo aberto (zona). Abrir outra boquilha no painel ou na procura troca
// só o registo (DmoTroca); a procura fecha, como numa página nova. Gravar um
// movimento não mexe na procura.
function abrirPagina() {
  const dom = new JSDOM(`<!doctype html><body>
    <aside id="bqCurrentLines"><div id="bqCurrentLines-list" data-dmo-list="Linhas"><button data-dmo-row id="b1">B1</button></div></aside>
    <section id="registo" data-dmo-troca>
      <div data-dmo-fica><details class="bq-register-search" open><div id="bqProcura"><table id="res" data-dmo-list="Boquilhas"><tr data-dmo-row id="linha"><td>P379</td></tr></table></div></details></div>
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

test('abrir uma boquilha encontrada fecha a procura depois da troca', () => {
  const { dom, document, abrir, trocou } = abrirPagina();
  try {
    const procura = document.querySelector('details');
    abrir('linha');
    assert.equal(procura.open, true, 'só fecha quando o registo novo chega');
    trocou(['bqRegistoAberto', 'bqRegistoMovimento']);
    assert.equal(procura.open, false);
    assert.ok(document.querySelector('#res'), 'os resultados ficam para a próxima procura');
  } finally {
    dom.window.close();
  }
});

test('abrir uma máquina no painel lateral também fecha a procura', () => {
  const { dom, document, abrir, trocou } = abrirPagina();
  try {
    abrir('b1');
    trocou(['bqRegistoAberto']);
    assert.equal(document.querySelector('details').open, false);
  } finally {
    dom.window.close();
  }
});

test('gravar um movimento não mexe na procura nem no scroll', () => {
  const { dom, document, trocou, deslizou } = abrirPagina();
  try {
    trocou(['bqRegistoMovimento']);
    assert.equal(document.querySelector('details').open, true);
    assert.deepEqual(deslizou, []);
  } finally {
    dom.window.close();
  }
});

test('o registo aberto fora do ecrã vem à vista', () => {
  const { dom, window, document, abrir, trocou, deslizou } = abrirPagina();
  try {
    window.HTMLElement.prototype.getBoundingClientRect = function () { return { top: -400 }; };
    abrir('b1');
    trocou(['bqRegistoAberto']);
    assert.deepEqual(deslizou, ['card register-open']);
  } finally {
    dom.window.close();
  }
});

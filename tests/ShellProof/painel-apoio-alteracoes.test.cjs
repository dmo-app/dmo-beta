const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Relatório de frontend do Improvements (28/10/2026, FE10): com um <dmo-painel> com
// alterações por guardar, um link do painel de apoio abria-o por cima (dois painéis).
function painel(id, extra = '') {
  return `<dialog class="dmo-painel${extra.includes('proprio') ? ' dmo-support' : ''}" id="${id}" data-dmo-estrutura="painel" ${extra}>
    <header class="dmo-painel-topo"><h2 class="dmo-painel-titulo">${id}</h2>
      <div class="dmo-painel-acoes"><a id="dmoSupportFull" class="dmo-button">Abrir página</a><button type="button" class="dmo-button" data-dmo-painel-fechar>Fechar</button></div></header>
    <div class="dmo-painel-aviso" hidden><p>Existem alterações por guardar neste painel.</p>
      <button type="button" data-dmo-painel-continuar>Continuar a editar</button><button type="button" data-dmo-painel-descartar>Fechar sem guardar</button></div>
    <div class="dmo-painel-corpo">${extra.includes('proprio') ? '<p class="dmo-support-loading">A carregar…</p><iframe class="dmo-support-frame"></iframe>' : '<form method="post"><input name="nota"></form>'}</div>
  </dialog>`;
}
function pagina() {
  const dom = new JSDOM(`<!doctype html><body><main>
    <a id="peso" href="/Controlo/Peso?controlo_id=1">Peso</a>
    <a id="peg" href="/Controlo/Pegamentos?controlo_id=1">Pegamentos</a>
    <button type="button" id="abreA" data-dmo-painel-abrir="a">A</button>
    <button type="button" id="abreB" data-dmo-painel-abrir="b">B</button></main>
    ${painel('a')}${painel('b')}${painel('dmoSupport', 'data-dmo-painel-proprio')}
    <div class="dmo-support-refresh" hidden><button data-support-refresh></button><button data-support-dismiss></button></div>
  </body>`, { url: 'http://dmo.test/Controlo/Resumo', runScripts: 'outside-only', pretendToBeVisual: true });
  const w = dom.window;
  if (!w.HTMLDialogElement.prototype.showModal) {
    w.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
    w.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
    Object.defineProperty(w.HTMLDialogElement.prototype, 'open', { get() { return this.hasAttribute('open'); } });
  }
  w.eval(fs.readFileSync(path.join(assets, 'js/dmo-painel.js'), 'utf8'));
  w.eval(fs.readFileSync(path.join(assets, 'js/dmo-support.js'), 'utf8'));
  return w;
}
const abertos = d => [...d.querySelectorAll('dialog[open]')].map(x => x.id);

test('Painel de apoio: um <dmo-painel> com alterações por guardar fica com o aviso e o apoio não abre por cima', () => {
  const w = pagina();
  const d = w.document;
  d.getElementById('a').setAttribute('data-dmo-painel-confirmar', '');
  d.getElementById('abreA').click();
  const campo = d.querySelector('#a input[name="nota"]');
  campo.value = 'escrito';
  campo.dispatchEvent(new w.Event('input', { bubbles: true }));
  assert.equal(w.DmoSupport.tryOpen('/Controlo/Peso?peso_id=1', d.getElementById('peso')), true);
  assert.deepEqual(abertos(d), ['a']);
  assert.equal(d.querySelector('#a .dmo-painel-aviso').hidden, false);
  assert.equal(d.querySelector('#dmoSupport iframe').hasAttribute('src'), false);
  assert.equal(campo.value, 'escrito');
  // «Fechar sem guardar» e depois o link abre o apoio normalmente.
  d.querySelector('#a [data-dmo-painel-descartar]').click();
  d.getElementById('peso').click();
  assert.deepEqual(abertos(d), ['dmoSupport']);
  w.close();
});

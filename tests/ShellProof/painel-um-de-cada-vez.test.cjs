const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Um painel deslizante de cada vez (Diogo, 27/10/2026: «consigo spammar sliding
// windows… se clicar constantemente em cima de um elemento que abre sliding»).
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

test('Um painel de cada vez: o mesmo botão não abre outro por cima e outro painel fecha o que está aberto', () => {
  const w = pagina();
  const d = w.document;
  d.getElementById('abreA').click();
  d.getElementById('abreA').click();
  assert.deepEqual(abertos(d), ['a']);
  assert.equal(w.DmoPainel.abrir('a'), false);
  d.getElementById('abreB').click();
  assert.deepEqual(abertos(d), ['b']);
  w.close();
});

test('Painel de apoio: cliques repetidos na mesma página não a voltam a carregar e outra página troca a que lá está', () => {
  const w = pagina();
  const d = w.document;
  const apoio = d.getElementById('dmoSupport'), frame = apoio.querySelector('iframe');
  d.getElementById('peso').click();
  const src = frame.getAttribute('src');
  assert.match(src, /\/Controlo\/Peso\?controlo_id=1&painel=1$/);
  let cargas = 0;
  new w.MutationObserver(m => { cargas += m.length; }).observe(frame, { attributes: true, attributeFilter: ['src'] });
  for (let i = 0; i < 5; i++) assert.equal(w.DmoSupport.tryOpen('/Controlo/Peso?controlo_id=1', d.getElementById('peso')), true);
  return new Promise(r => setTimeout(r, 0)).then(() => {
    assert.equal(cargas, 0);
    assert.deepEqual(abertos(d), ['dmoSupport']);
    assert.equal(w.DmoSupport.tryOpen('/Controlo/Pegamentos?controlo_id=1', d.getElementById('peg')), true);
    assert.match(frame.getAttribute('src'), /\/Controlo\/Pegamentos\?controlo_id=1&painel=1$/);
    assert.equal(apoio.querySelector('h2').textContent, 'Pegamentos');
    assert.deepEqual(abertos(d), ['dmoSupport']);
    // Com o painel de apoio aberto, um <dmo-painel> não abre por cima.
    assert.equal(w.DmoPainel.abrir('a'), false);
    assert.deepEqual(abertos(d), ['dmoSupport']);
    w.close();
  });
});

test('Painel de apoio: abrir fecha um <dmo-painel> que esteja aberto', () => {
  const w = pagina();
  const d = w.document;
  d.getElementById('abreA').click();
  d.getElementById('peso').click();
  assert.deepEqual(abertos(d), ['dmoSupport']);
  w.close();
});

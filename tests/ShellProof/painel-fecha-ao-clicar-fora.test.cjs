const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Os painéis deslizantes fecham ao clicar fora (Diogo, 29/10/2026: «sliding
// window não fecha com click fora»), e o «Voltar» é um link no canto superior
// esquerdo do painel de apoio quando há uma página anterior.
function painel(id, extra = '') {
  const apoio = extra.includes('proprio');
  return `<dialog class="dmo-painel" id="${id}" data-dmo-estrutura="painel" ${extra}>
    <header class="dmo-painel-topo"><h2 class="dmo-painel-titulo">${id}</h2>
      <div class="dmo-painel-acoes"><a id="dmoSupportFull" class="dmo-button">Abrir página</a><button type="button" class="dmo-button" data-dmo-painel-fechar>Fechar</button></div></header>
    <div class="dmo-painel-aviso" hidden><p>Alterações por guardar.</p>
      <button type="button" data-dmo-painel-continuar>Continuar a editar</button><button type="button" data-dmo-painel-descartar>Fechar sem guardar</button></div>
    <div class="dmo-painel-corpo">${apoio ? '<p class="dmo-support-loading">A carregar…</p><iframe class="dmo-support-frame"></iframe>' : '<form method="post"><input name="nota"></form>'}</div>
  </dialog>`;
}
function pagina() {
  const dom = new JSDOM(`<!doctype html><body><main>
    <a id="peso" href="/Controlo/Peso?controlo_id=1">Peso</a>
    <a id="peg" href="/Controlo/Pegamentos?controlo_id=1">Pegamentos</a>
    <button type="button" id="abreA" data-dmo-painel-abrir="a">A</button></main>
    ${painel('a', 'data-dmo-painel-confirmar')}${painel('dmoSupport', 'data-dmo-painel-proprio')}
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
// Um clique no fundo do <dialog>: o alvo é o próprio <dialog>, fora da sua caixa.
function cliqueFora(w, dialog, dentro = false) {
  dialog.getBoundingClientRect = () => ({ left: 100, right: 500, top: 0, bottom: 400 });
  dialog.dispatchEvent(new w.MouseEvent('click', { bubbles: true, clientX: dentro ? 200 : 20, clientY: 50 }));
}

test('um clique fora fecha o painel; dentro da caixa não', () => {
  const w = pagina(), d = w.document;
  d.getElementById('abreA').click();
  assert.deepEqual(abertos(d), ['a']);
  cliqueFora(w, d.getElementById('a'), true);
  assert.deepEqual(abertos(d), ['a']);
  cliqueFora(w, d.getElementById('a'));
  assert.deepEqual(abertos(d), []);
  w.close();
});

test('com alterações por guardar, um clique fora mostra o aviso em vez de fechar', () => {
  const w = pagina(), d = w.document;
  d.getElementById('abreA').click();
  const input = d.querySelector('#a input');
  input.dispatchEvent(new w.Event('input', { bubbles: true }));
  cliqueFora(w, d.getElementById('a'));
  assert.deepEqual(abertos(d), ['a']);
  assert.equal(d.querySelector('#a .dmo-painel-aviso').hidden, false);
  w.close();
});

test('painel de apoio: um clique fora fecha-o', () => {
  const w = pagina(), d = w.document;
  d.getElementById('peso').click();
  assert.deepEqual(abertos(d), ['dmoSupport']);
  cliqueFora(w, d.getElementById('dmoSupport'));
  assert.deepEqual(abertos(d), []);
  w.close();
});

test('painel de apoio: «Voltar» é um link no canto esquerdo, antes do título, e volta à página que lá estava', () => {
  const w = pagina(), d = w.document;
  const apoio = d.getElementById('dmoSupport');
  const voltar = apoio.querySelector('a[data-dmo-painel-voltar]');
  assert.ok(voltar, 'existe o link Voltar');
  assert.equal(voltar.nextElementSibling, apoio.querySelector('h2'), 'fica logo antes do título');
  assert.equal(voltar.classList.contains('dmo-button'), false, 'não tem aspeto de botão');
  assert.equal(voltar.hidden, true, 'sem página anterior não aparece');
  d.getElementById('peso').click();
  assert.equal(voltar.hidden, true);
  d.getElementById('peg').click();
  assert.equal(voltar.hidden, false, 'outra página abriu por cima da primeira');
  voltar.click();
  assert.match(apoio.querySelector('iframe').getAttribute('src'), /\/Controlo\/Peso\?controlo_id=1&painel=1$/);
  assert.equal(voltar.hidden, true, 'já está na primeira');
  w.close();
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A estrutura fixa dos painéis deslizantes (Diogo, 27/10/2026): o mesmo abrir e
// fechar em todos (js/dmo-painel.js), com a marcação do <dmo-painel>.
function pagina(confirmar) {
  const dom = new JSDOM(`<!doctype html><body>
    <button type="button" id="abre" data-dmo-painel-abrir="ficha">Ficha</button>
    <dialog class="dmo-painel" id="ficha" data-dmo-estrutura="painel" data-lado="direita" data-largura="media" aria-labelledby="fichaTitulo"${confirmar ? ' data-dmo-painel-confirmar' : ''}>
      <header class="dmo-painel-topo"><h2 class="dmo-painel-titulo" id="fichaTitulo">Ficha</h2>
        <div class="dmo-painel-acoes"><button type="button" class="dmo-button" data-dmo-painel-fechar>Fechar</button></div></header>
      <div class="dmo-painel-aviso" hidden><p>Existem alterações por guardar neste painel.</p>
        <button type="button" data-dmo-painel-continuar>Continuar a editar</button><button type="button" data-dmo-painel-descartar>Fechar sem guardar</button></div>
      <div class="dmo-painel-corpo"><form method="post"><input name="nota" id="nota"></form></div>
    </dialog></body>`, { url: 'http://dmo.test/', runScripts: 'outside-only' });
  const w = dom.window;
  // O jsdom não tem o showModal do <dialog>: o mínimo para o script.
  if (!w.HTMLDialogElement.prototype.showModal) {
    w.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
    w.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
    Object.defineProperty(w.HTMLDialogElement.prototype, 'open', { get() { return this.hasAttribute('open'); } });
  }
  w.eval(fs.readFileSync(path.join(assets, 'js/dmo-painel.js'), 'utf8'));
  return w;
}

test('Painel: o botão abre, o «Fechar» fecha e o foco volta ao botão', () => {
  const w = pagina(false);
  const d = w.document, painel = d.getElementById('ficha'), abre = d.getElementById('abre');
  abre.click();
  assert.equal(painel.open, true);
  assert.equal(abre.getAttribute('aria-expanded'), 'true');
  assert.equal(d.documentElement.classList.contains('dmo-painel-aberto'), true);
  assert.equal(d.activeElement, d.getElementById('nota'));
  painel.querySelector('[data-dmo-painel-fechar]').click();
  assert.equal(painel.open, false);
  assert.equal(abre.getAttribute('aria-expanded'), 'false');
  assert.equal(d.documentElement.classList.contains('dmo-painel-aberto'), false);
  assert.equal(d.activeElement, abre);
  w.close();
});

test('Painel: Escape fecha (o «cancel» do <dialog>) e DmoPainel abre por código', () => {
  const w = pagina(false);
  const d = w.document, painel = d.getElementById('ficha');
  assert.equal(w.DmoPainel.abrir('ficha'), true);
  const cancel = new w.Event('cancel', { cancelable: true });
  painel.dispatchEvent(cancel);
  assert.equal(cancel.defaultPrevented, true);
  assert.equal(painel.open, false);
  w.close();
});

test('Painel com confirmar: com o formulário mudado pede antes de fechar', () => {
  const w = pagina(true);
  const d = w.document, painel = d.getElementById('ficha'), aviso = painel.querySelector('.dmo-painel-aviso');
  d.getElementById('abre').click();
  const nota = d.getElementById('nota');
  nota.value = 'x';
  nota.dispatchEvent(new w.Event('input', { bubbles: true }));
  painel.querySelector('[data-dmo-painel-fechar]').click();
  assert.equal(painel.open, true);
  assert.equal(aviso.hidden, false);
  aviso.querySelector('[data-dmo-painel-continuar]').click();
  assert.equal(aviso.hidden, true);
  assert.equal(painel.open, true);
  painel.querySelector('[data-dmo-painel-fechar]').click();
  aviso.querySelector('[data-dmo-painel-descartar]').click();
  assert.equal(painel.open, false);
  w.close();
});

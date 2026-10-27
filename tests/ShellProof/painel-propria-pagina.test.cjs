const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Nos Pegamentos, carregar num valor abria a própria página no painel de apoio;
// apagar lá deixava a linha na página por trás (Diogo, 27/10/2026, DMO-512).
const abertos = d => [...d.querySelectorAll('dialog[open]')].map(x => x.id);
const painel = `<dialog class="dmo-painel dmo-support" id="dmoSupport" data-dmo-estrutura="painel" data-dmo-painel-proprio>
    <header class="dmo-painel-topo"><h2 class="dmo-painel-titulo">dmoSupport</h2>
      <div class="dmo-painel-acoes"><a id="dmoSupportFull" class="dmo-button">Abrir página</a><button type="button" class="dmo-button" data-dmo-painel-fechar>Fechar</button></div></header>
    <div class="dmo-painel-aviso" hidden><p>Existem alterações por guardar neste painel.</p>
      <button type="button" data-dmo-painel-continuar>Continuar a editar</button><button type="button" data-dmo-painel-descartar>Fechar sem guardar</button></div>
    <div class="dmo-painel-corpo"><p class="dmo-support-loading">A carregar…</p><iframe class="dmo-support-frame"></iframe></div>
  </dialog>`;

test('Painel de apoio: um link para a própria página (corrigir uma leitura nos Pegamentos) fica na página, sem painel', () => {
  const dom = new JSDOM(`<!doctype html><body><main>
    <a id="corrigir" href="/Controlo/Pegamentos?controlo_id=1&editar_medicao=7&campo=costura">70.10</a>
    <a id="peso" href="/Controlo/Peso?controlo_id=1">Peso</a></main>
    ${painel}
    <div class="dmo-support-refresh" hidden><button data-support-refresh></button><button data-support-dismiss></button></div>
  </body>`, { url: 'http://dmo.test/Controlo/Pegamentos?controlo_id=1', runScripts: 'outside-only', pretendToBeVisual: true });
  const w = dom.window;
  if (!w.HTMLDialogElement.prototype.showModal) {
    w.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
    w.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
    Object.defineProperty(w.HTMLDialogElement.prototype, 'open', { get() { return this.hasAttribute('open'); } });
  }
  w.eval(fs.readFileSync(path.join(assets, 'js/dmo-support.js'), 'utf8'));
  const d = w.document;
  assert.equal(w.DmoSupport.tryOpen(d.getElementById('corrigir').href, d.getElementById('corrigir')), false);
  assert.deepEqual(abertos(d), []);
  // Outra página continua a abrir no painel.
  assert.equal(w.DmoSupport.tryOpen(d.getElementById('peso').href, d.getElementById('peso')), true);
  assert.deepEqual(abertos(d), ['dmoSupport']);
  w.close();
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Diogo, 29/10/2026: «quando tento sair aparece para sair sem gravar» — com as leituras do Peso já
// registadas (sem recarregar), o painel continuava a dizer que havia alterações por guardar.
test('Painel de apoio: depois de gravar sem recarregar, sem campos alterados, fecha sem aviso', () => {
  const dom = new JSDOM(`<!doctype html><body><main><a id="peso" href="/Controlo/Peso?controlo_id=1">Peso</a></main>
    <dialog class="dmo-painel dmo-support" id="dmoSupport" data-dmo-estrutura="painel" data-dmo-painel-proprio>
      <header class="dmo-painel-topo"><h2 class="dmo-painel-titulo">Peso</h2>
        <div class="dmo-painel-acoes"><a id="dmoSupportFull" class="dmo-button">Abrir página</a><button type="button" class="dmo-button" data-dmo-painel-fechar>Fechar</button></div></header>
      <div class="dmo-painel-aviso" hidden><p>Existem alterações por guardar neste painel.</p>
        <button type="button" data-dmo-painel-continuar>Continuar a editar</button><button type="button" data-dmo-painel-descartar>Fechar sem guardar</button></div>
      <div class="dmo-painel-corpo"><p class="dmo-support-loading">A carregar…</p><iframe class="dmo-support-frame"></iframe></div>
    </dialog>
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
  const d = w.document;
  const frame = d.querySelector('#dmoSupport iframe');
  w.DmoSupport.tryOpen('/Controlo/Peso?controlo_id=1', d.getElementById('peso'));
  const doc = frame.contentDocument;
  doc.open(); doc.write('<!doctype html><body><form method="post"><input name="cm_subject"><input name="water_weight"></form></body>'); doc.close();
  frame.dispatchEvent(new w.Event('load'));
  const campo = doc.querySelector('input[name="cm_subject"]');
  campo.value = '44';
  campo.dispatchEvent(new w.Event('input', { bubbles: true }));
  // A leitura registou-se: a zona trocou e a leitura nova ficou vazia.
  campo.value = '';
  doc.dispatchEvent(new w.CustomEvent('dmo:troca'));
  d.querySelector('#dmoSupport [data-dmo-painel-fechar]').click();
  assert.equal(d.querySelector('#dmoSupport .dmo-painel-aviso').hidden, true);
  assert.equal(d.getElementById('dmoSupport').hasAttribute('open'), false);
  w.close();
});

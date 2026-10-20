const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// «Eliminar Job On» (teste do agente de 19/10/2026, T84fb13d): nas «Opções» da
// ficha é um <a href="#jobon-delete-dialog" data-dmo-dialog>. Abrir a janela
// não pode seguir o link: a âncora nova dispara o popstate, que recarrega a
// página, e a janela nunca chega a ver-se.
function abrirPagina() {
  const dom = new JSDOM(`<!doctype html><title>Job On</title><body><div data-dmo-troca>
    <details class="dmo-options" open><summary>Opções</summary>
      <a id="eliminar" href="#jobon-delete-dialog" role="button" data-dmo-dialog="jobon-delete-dialog">Eliminar Job On</a>
    </details>
    <dialog id="jobon-delete-dialog" class="dmo-dialog"><p>Quer mesmo eliminar?</p>
      <button type="button" id="cancelar" data-dmo-dialog-close>Cancelar</button></dialog>
  </div></body>`, { url: 'http://dmo.test/JobOn?jobon_id=321', runScripts: 'outside-only' });
  const { window } = dom;
  // O jsdom não tem showModal: abrir e fechar mexem só no atributo open.
  window.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  window.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  return { dom, window, document: window.document };
}

test('um link que abre a janela pequena abre-a e não muda o endereço', () => {
  const { dom, window, document } = abrirPagina();
  try {
    const janela = document.getElementById('jobon-delete-dialog');
    assert.equal(janela.hasAttribute('open'), false);
    const clique = new window.MouseEvent('click', { bubbles: true, cancelable: true });
    const seguiu = document.getElementById('eliminar').dispatchEvent(clique);
    assert.equal(janela.hasAttribute('open'), true, 'a janela abre');
    assert.equal(seguiu, false, 'o link não é seguido');
    assert.equal(window.location.hash, '', 'o endereço não ganha a âncora');
    assert.equal(document.querySelector('details.dmo-options').hasAttribute('open'), false, 'as «Opções» fecham');

    document.getElementById('cancelar').click();
    assert.equal(janela.hasAttribute('open'), false, 'Cancelar fecha');
  } finally {
    dom.window.close();
  }
});

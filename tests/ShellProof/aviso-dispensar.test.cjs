const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const script = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/dmo-aviso-dispensar.js'), 'utf8');

// O «×» do aviso «O Job On mudou depois de este Peso ser gravado» (Diogo,
// 29/10/2026): fecha-o e não volta para essas mudanças; uma mudança mais
// nova traz o aviso de volta.
function abrir(ate, guardado) {
  const dom = new JSDOM(`<!doctype html><body><div class="jobon-change-notice" data-dmo-aviso-dispensar="peso-7" data-dmo-aviso-ate="${ate}">
    <button type="button" data-dmo-aviso-fechar>×</button><strong>O Job On mudou depois de este Peso ser gravado.</strong></div></body>`,
  { url: 'http://dmo.test/Controlo/Peso?peso_id=7', runScripts: 'outside-only' });
  if (guardado) dom.window.localStorage.setItem('dmo-aviso-mudou-peso-7', String(guardado));
  dom.window.eval(script);
  return dom;
}

test('o × fecha o aviso e ele não volta ao reabrir a página', () => {
  const dom = abrir(500);
  const aviso = dom.window.document.querySelector('.jobon-change-notice');
  assert.equal(aviso.hidden, false);
  dom.window.document.querySelector('[data-dmo-aviso-fechar]').click();
  assert.equal(aviso.hidden, true);
  assert.equal(dom.window.localStorage.getItem('dmo-aviso-mudou-peso-7'), '500');
  const outra = new JSDOM(dom.serialize(), { url: 'http://dmo.test/Controlo/Peso?peso_id=7', runScripts: 'outside-only' });
  outra.window.localStorage.setItem('dmo-aviso-mudou-peso-7', '500');
  outra.window.document.querySelector('.jobon-change-notice').hidden = false;
  outra.window.eval(script);
  assert.equal(outra.window.document.querySelector('.jobon-change-notice').hidden, true);
});

test('uma mudança mais nova do que a que se fechou traz o aviso de volta', () => {
  const dom = abrir(900, 500);
  assert.equal(dom.window.document.querySelector('.jobon-change-notice').hidden, false);
});

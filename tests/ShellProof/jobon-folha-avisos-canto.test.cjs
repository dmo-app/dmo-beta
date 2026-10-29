const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Os avisos dos cartões (Diogo, 29/10/2026: «meter estes alertas aparecer no canto superior direito, com x para
// remover»): no ver, saem do cartão para a pilha do canto, com o botão que já tinham e um × que os fecha.
test('Ver: avisos do CM e da BQ vão para o canto, com o botão e um × que fecha', async () => {
  const dom = new JSDOM(`<!doctype html><body><main class="jof2">
    <article class="sec" data-jof-familia="CM"><div class="jobon-aviso warn" data-dmo-aviso-armazem>CM 9395 / lote 34 não está registada no Armazém<a class="dmo-aviso-link" href="/Ferramentas/Ficha?tool_id=C1">Registar na ferramenta</a></div></article>
    <article class="sec" data-jof-familia="BQ"><div class="jobon-aviso warn" data-dmo-aviso-b2>BQ T194 / lote 198 trabalha na B2 sem B2 Dupla<a class="dmo-aviso-link" href="/Ferramentas/Ficha?tool_id=B1">Escolher na ferramenta</a></div></article>
  </main></body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
  const { window } = dom, doc = window.document;
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (doc.readyState === 'loading') await new Promise(r => window.addEventListener('DOMContentLoaded', r));

  const pilha = doc.querySelector('[data-jof-avisos-canto]');
  assert.ok(pilha, 'falta a pilha do canto');
  assert.equal(doc.querySelectorAll('.sec .jobon-aviso').length, 0, 'os avisos ficaram dentro dos cartões');
  const avisos = pilha.querySelectorAll('.jobon-aviso');
  assert.equal(avisos.length, 2);
  assert.ok(avisos[1].querySelector('a.dmo-aviso-link[href="/Ferramentas/Ficha?tool_id=B1"]'), 'o botão do aviso perdeu-se');
  avisos[0].querySelector('.jof-aviso-fechar').click();
  assert.equal(pilha.querySelectorAll('.jobon-aviso').length, 1);
  pilha.querySelector('.jof-aviso-fechar').click();
  assert.equal(doc.querySelector('[data-jof-avisos-canto]'), null, 'a pilha vazia devia sair');
  window.close();
});

test('Editar: os avisos ficam onde estão', async () => {
  const dom = new JSDOM(`<!doctype html><body><main class="jof2 editing"><article class="sec"><div class="jobon-aviso warn" data-dmo-aviso-b2>aviso</div></article></main></body>`,
    { url: 'http://dmo.test/JobOn/Folha?jobon_id=1&editar=1', runScripts: 'outside-only' });
  const { window } = dom, doc = window.document;
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (doc.readyState === 'loading') await new Promise(r => window.addEventListener('DOMContentLoaded', r));
  assert.equal(doc.querySelector('[data-jof-avisos-canto]'), null);
  assert.equal(doc.querySelectorAll('.sec .jobon-aviso').length, 1);
  window.close();
});

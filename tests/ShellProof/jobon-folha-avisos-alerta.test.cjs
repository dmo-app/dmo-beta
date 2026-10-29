const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Os avisos dos cartões saem como os outros alertas da app (Diogo, 29/10/2026: «alerta ficou no sítio errado»,
// «deve aparecer como aparecem os alertas»): na pilha do DMOLive, com o botão que já tinham e o × dos alertas.
test('Ver: avisos do CM e da BQ vão para a pilha dos alertas da app, com o botão e o ×', async () => {
  const dom = new JSDOM(`<!doctype html><body><main class="jof2">
    <article class="sec" data-jof-familia="CM"><div class="jobon-aviso warn" data-dmo-aviso-armazem>CM 2032 / lote 1 não está registada no Armazém<a class="dmo-aviso-link" href="/Ferramentas/Ficha?tool_id=C1">Registar na ferramenta</a></div></article>
    <article class="sec" data-jof-familia="BQ"><div class="jobon-aviso warn" data-dmo-aviso-b2>BQ T194 / lote 198 trabalha na B2 sem B2 Dupla</div></article>
  </main></body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
  const { window } = dom, doc = window.document;
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (doc.readyState === 'loading') await new Promise(r => window.addEventListener('DOMContentLoaded', r));

  const pilha = doc.querySelector('.dmo-toast-pilha');
  assert.ok(pilha, 'falta a pilha dos alertas');
  assert.equal(doc.querySelector('[data-jof-avisos-canto]'), null, 'não devia haver pilha própria');
  assert.equal(doc.querySelectorAll('.sec [data-dmo-aviso-armazem], .sec [data-dmo-aviso-b2]').length, 0);
  const avisos = pilha.querySelectorAll('[data-dmo-toast="aviso"]');
  assert.equal(avisos.length, 2);
  assert.ok(avisos[0].querySelector('a.dmo-aviso-link'), 'o botão do aviso perdeu-se');
  assert.ok(avisos[0].querySelector('.dmo-toast-fechar'), 'falta o ×');
  window.close();
});

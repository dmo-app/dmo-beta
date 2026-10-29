const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// No Editar, os Dias mudam logo ao mudar o Início ou o Fim, sem gravar (Diogo, 29/10/2026).
test('Editar: mudar o Início ou o Fim atualiza os Dias na hora', async () => {
  const dom = new JSDOM(`<!doctype html><body><main class="jof2 editing">
    <input id="lightStart" type="date" value="2026-11-02"><input id="lightEnd" type="date" value="2026-11-09">
    <strong data-jof-dias>8</strong></main></body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1&editar=1', runScripts: 'outside-only' });
  const { window } = dom, doc = window.document;
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (doc.readyState === 'loading') await new Promise(r => window.addEventListener('DOMContentLoaded', r));
  const fim = doc.getElementById('lightEnd');
  fim.value = '2026-11-12';
  fim.dispatchEvent(new window.Event('input'));
  assert.equal(doc.querySelector('[data-jof-dias]').textContent, '11');
  const inicio = doc.getElementById('lightStart');
  inicio.value = '2026-11-13';
  inicio.dispatchEvent(new window.Event('change'));
  assert.equal(doc.querySelector('[data-jof-dias]').textContent, '—');
  window.close();
});

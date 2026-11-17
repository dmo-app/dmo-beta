const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// AUDIT EXTERNO, 16/11/2026 (E1): a pilha dos avisos media a
// .dmo-shell-navigation, que tem display:contents (sem caixa, bottom 0), e
// ficava a 12px do topo, por cima do «Sair», das Definições e das abas.
// Agora fica por baixo do cabeçalho inteiro (.dmo-shell-header).

test('A pilha dos avisos fica por baixo do cabeçalho, não por cima do «Sair»', () => {
  const dom = new JSDOM(`<!doctype html><body><header class="dmo-shell-header"><nav class="dmo-shell-navigation"></nav><a class="dmo-shell-logout">Sair</a></header><main></main></body>`,
    { url: 'http://dmo.test/JobOn/Folha', runScripts: 'outside-only' });
  const { window } = dom;
  window.matchMedia = () => ({ matches: true });
  const caixa = (bottom) => () => ({ top: 0, left: 0, right: 1366, width: 1366, height: bottom, bottom });
  window.document.querySelector('.dmo-shell-header').getBoundingClientRect = caixa(95);
  window.document.querySelector('.dmo-shell-navigation').getBoundingClientRect = caixa(0);
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));

  window.DMOLive.toast('Aviso do Armazém', { kind: 'aviso' });

  assert.equal(window.document.querySelector('.dmo-toast-pilha').style.top, '107px');
  dom.window.close();
});

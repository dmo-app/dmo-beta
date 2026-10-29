const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const script = fs.readFileSync(path.join(__dirname, '../../src/DMO.Alpha.Web/wwwroot/js/ferramentas-avisos-sap.js'), 'utf8');

test('O × fecha só o aviso escolhido, sem decidir «Não» nem enviar o formulário', () => {
  const dom = new JSDOM('<div data-sap-aviso id="pecas"><button data-sap-fechar><span>×</span></button><form><button>Não</button></form></div><div data-sap-aviso id="uso"></div>', { runScripts: 'outside-only' });
  let enviados = 0;
  dom.window.document.addEventListener('submit', e => { e.preventDefault(); enviados++; });
  dom.window.eval(script);
  dom.window.document.querySelector('[data-sap-fechar] span').click();
  assert.equal(dom.window.document.getElementById('pecas').hidden, true);
  assert.equal(dom.window.document.getElementById('uso').hidden, false);
  assert.equal(enviados, 0);
  dom.window.close();
});

test('Os avisos carregados depois de uma troca parcial também se podem fechar', () => {
  const dom = new JSDOM('<main></main>', { runScripts: 'outside-only' });
  dom.window.eval(script);
  dom.window.document.querySelector('main').innerHTML = '<div data-sap-aviso><button data-sap-fechar>×</button></div>';
  dom.window.document.querySelector('[data-sap-fechar]').click();
  assert.equal(dom.window.document.querySelector('[data-sap-aviso]').hidden, true);
  dom.window.close();
});

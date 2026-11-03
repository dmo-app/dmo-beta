const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

// DMO-704 (Diogo, 02/11/2026): a % de uso foi lida e gravada, mas a app dizia
// «A leitura do SAP falhou». O aviso diz agora o passo que falhou, porquê, e
// o que correu bem, a partir do dmo-sap-estado.txt.

function erroSap() {
  const dom = new JSDOM('<!doctype html><body></body>', { url: 'http://dmo.test/', runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/js/ferramentas-sap.js'), 'utf8'));
  return dom.window.DMOSapErro;
}

test('As produções falham e a % de uso corre bem: aviso, não erro, e diz as duas coisas', () => {
  const r = erroSap()('erro DMO_FABRICO_SAP.vbs:codigo1 ok:DMO_USO_SAP.vbs 2026-11-02 10:22');
  assert.equal(r.parcial, true);
  assert.equal(r.texto, 'A % de uso foi lida do SAP. Mas não foi possível ler as produções (o script parou com erro, código 1). Veja o dmo-sap.log na pasta.');
});

test('Tudo falhou: diz cada passo e o motivo', () => {
  const r = erroSap()('erro DMO_FABRICO_SAP.vbs:tempo DMO_USO_SAP.vbs:falta 2026-11-02 10:22');
  assert.equal(r.parcial, false);
  assert.equal(r.texto, 'A leitura do SAP falhou: não foi possível ler as produções (passou de 30 minutos) nem a % de uso (o script não está na pasta). Veja o dmo-sap.log na pasta.');
});

test('Um estado antigo, sem motivo, continua a dizer que falhou', () => {
  const r = erroSap()('erro 2026-11-02 10:22');
  assert.equal(r.parcial, false);
  assert.match(r.texto, /^A leitura do SAP falhou: o motivo não ficou escrito\./);
});

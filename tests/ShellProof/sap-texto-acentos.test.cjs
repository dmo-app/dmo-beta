const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

// DMO-740 (Diogo, 04/11/2026): Admin › SAP mostrava «SAP: a ler as produ��es (ZFMD)».
// O dmo-sap-passo.txt vinha em Windows-1252 (CreateTextFile do VBScript) e a app lia-o
// como UTF-8. Agora os scripts gravam o passo em Unicode e a app lê os dois.

const raiz = path.resolve(__dirname, '../..');

function textoSap() {
  const dom = new JSDOM('<!doctype html><body></body>', { url: 'http://dmo.test/', runScripts: 'outside-only' });
  dom.window.TextDecoder = TextDecoder;
  dom.window.eval(fs.readFileSync(path.join(raiz, 'src/DMO.Alpha.Web/wwwroot/js/ferramentas-sap.js'), 'utf8'));
  return dom.window.DMOSapTexto;
}

const frase = 'a ler as produções (ZFMD)';

test('O passo gravado em Windows-1252 pelo VBScript mostra os acentos certos', () => {
  const bytes = new Uint8Array([...Buffer.from(frase, 'latin1'), 0x0d, 0x0a]);
  assert.equal(textoSap()(bytes).trim(), frase);
});

test('O passo gravado em Unicode (UTF-16 com BOM) mostra os acentos certos', () => {
  const bytes = new Uint8Array([0xff, 0xfe, ...Buffer.from(frase + '\r\n', 'utf16le')]);
  assert.equal(textoSap()(bytes).trim(), frase);
});

test('Os ficheiros em UTF-8, com ou sem BOM, ficam como estavam', () => {
  assert.equal(textoSap()(new Uint8Array(Buffer.from(frase, 'utf8'))), frase);
  assert.equal(textoSap()(new Uint8Array([0xef, 0xbb, 0xbf, ...Buffer.from(frase, 'utf8')])), frase);
});

test('A app lê os ficheiros da pasta pelos bytes, não com .text() (que supõe UTF-8)', () => {
  const js = fs.readFileSync(path.join(raiz, 'src/DMO.Alpha.Web/wwwroot/js/ferramentas-sap.js'), 'utf8');
  assert.doesNotMatch(js, /await ficheiro\.text\(\)/);
  assert.match(js, /textoDosBytes\(new Uint8Array\(await ficheiro\.arrayBuffer\(\)\)\)/);
});

test('Os scripts do SAP gravam o dmo-sap-passo.txt em Unicode', () => {
  for (const nome of ['DMO_FABRICO_SAP.vbs', 'DMO_USO_SAP.vbs']) {
    const vbs = fs.readFileSync(path.join(raiz, 'src/DMO.Alpha.Web/Sap', nome), 'utf8');
    assert.match(vbs, /CreateTextFile\(fso\.BuildPath\(pasta, "dmo-sap-passo\.txt"\), True, True\)/, nome);
  }
});

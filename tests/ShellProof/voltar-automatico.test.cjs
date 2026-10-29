const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

test('a página sem Voltar escrito ganha-o no canto, só se vier de outra página', async () => {
  const montar = async (endereco, referrer) => {
    const dom = new JSDOM('<!doctype html><title>DMO</title><body><main><h1>x</h1></main></body>',
      { url: 'https://dmo.test' + endereco, referrer: referrer && 'https://dmo.test' + referrer, runScripts: 'outside-only' });
    dom.window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
    await esperar();
    return dom.window.document.querySelector('main > a.dmo-voltar-texto');
  };
  assert.equal(await montar('/Planeamento/Sumario'), null, 'primeira página: nada para onde voltar');
  const link = await montar('/Planeamento/Sumario', '/Ferramentas');
  assert.equal(link.getAttribute('href'), '/Ferramentas');
  assert.equal(link.textContent, 'Voltar');
  assert.equal(await montar('/Ferramentas/ReparacaoProgramadaImprimir', '/Ferramentas'), null, 'impressão não leva Voltar');
});

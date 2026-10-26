const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// O VOLTAR (Diogo, 26/10/2026: «o voltar quando entramos numa página a partir
// de outra não volta à página anterior»): o link [data-dmo-voltar] volta à
// página de onde se veio, com os filtros; guardar ou abrir uma página filha
// (data-dmo-voltar-ignorar) não perde a origem; sem origem, fica a lista.
const link = '<a id="voltar" href="/Ferramentas/Index" data-dmo-voltar="T1" data-dmo-voltar-ignorar="/Ferramentas/Ficha /Ferramentas/Historico" data-dmo-voltar-texto="Voltar">Voltar à lista</a>';

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

async function abrir(endereco, referrer, storage) {
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${link}</body>`,
    { url: endereco, referrer, runScripts: 'outside-only' });
  const { window } = dom;
  if (storage) Object.entries(storage).forEach(([k, v]) => window.sessionStorage.setItem(k, v));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  await esperar();
  return window;
}

test('vindo de outra página, o Voltar volta lá com os filtros', async () => {
  const w = await abrir('https://dmo.test/Ferramentas/Ficha?tool_id=T1', 'https://dmo.test/Armazem/Movimentos?linha=B1&dia=2026-10-26');
  const a = w.document.getElementById('voltar');
  assert.equal(a.getAttribute('href'), '/Armazem/Movimentos?linha=B1&dia=2026-10-26');
  assert.equal(a.textContent, 'Voltar');
});

test('depois de guardar (vindo da própria ficha) ou do Histórico, a origem mantém-se', async () => {
  const primeiro = await abrir('https://dmo.test/Ferramentas/Ficha?tool_id=T1', 'https://dmo.test/Planeamento/Sumario?dia=2026-10-26');
  const guardado = {};
  for (let i = 0; i < primeiro.sessionStorage.length; i++) {
    const k = primeiro.sessionStorage.key(i); guardado[k] = primeiro.sessionStorage.getItem(k);
  }
  for (const de of ['https://dmo.test/Ferramentas/Ficha?tool_id=T1&guardado=1', 'https://dmo.test/Ferramentas/Historico?tool_id=T1']) {
    const w = await abrir('https://dmo.test/Ferramentas/Ficha?tool_id=T1', de, guardado);
    assert.equal(w.document.getElementById('voltar').getAttribute('href'), '/Planeamento/Sumario?dia=2026-10-26');
  }
});

test('sem origem do mesmo site, fica o link do servidor', async () => {
  for (const de of ['', 'https://outro.site/Armazem', 'https://dmo.test/login?ReturnUrl=%2F']) {
    const w = await abrir('https://dmo.test/Ferramentas/Ficha?tool_id=T1', de || undefined);
    const a = w.document.getElementById('voltar');
    assert.equal(a.getAttribute('href'), '/Ferramentas/Index');
    assert.equal(a.textContent, 'Voltar à lista');
  }
});

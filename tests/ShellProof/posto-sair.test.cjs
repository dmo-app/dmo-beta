const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test, afterEach } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// O POSTO DA RI (Diogo, 27/10/2026: «3min auto e opções nas definições para
// aumentar»): o mesmo computador para várias pessoas. Ao fim de N minutos sem
// uso a sessão do posto fecha e volta ao posto; enquanto alguém usa a página,
// o servidor é avisado (no máximo uma vez por minuto) para a sessão não acabar
// a meio de um registo. Sem o atributo (login normal) não faz nada.
// O relógio do posto (setInterval) fica a correr: cada janela fecha no fim do teste.
const abertas = [];
afterEach(() => abertas.splice(0).forEach((w) => w.close()));

function abrir(minutos) {
  const atributo = minutos === undefined ? '' : ` data-dmo-posto-minutos="${minutos}"`;
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body${atributo}><input id="campo"></body>`,
    { url: 'https://dmo.test/ReparacaoInterna', runScripts: 'outside-only' });
  const { window } = dom;
  abertas.push(window);
  const avisos = [];
  window.fetch = (url) => { avisos.push(url); return Promise.resolve({ ok: true }); };
  window.eval(fs.readFileSync(path.join(assets, 'js/dmo-posto.js'), 'utf8'));
  const saidas = [];
  if (window.DmoPosto) window.DmoPosto.sair = () => saidas.push(true);
  return { window, avisos, saidas };
}

test('sem uso durante os minutos do posto, sai e volta ao posto', () => {
  const { window, saidas } = abrir(3);
  const inicio = Date.now();
  assert.equal(window.DmoPosto.verificar(inicio + 2 * 60000), false);
  assert.equal(saidas.length, 0);
  assert.equal(window.DmoPosto.verificar(inicio + 3 * 60000 + 1000), true);
  assert.equal(saidas.length, 1);
});

test('quem está a usar a página não é posto fora', () => {
  const { window, saidas } = abrir(3);
  const inicio = Date.now();
  window.DmoPosto.usar(inicio + 2 * 60000);
  assert.equal(window.DmoPosto.verificar(inicio + 4 * 60000), false);
  assert.equal(window.DmoPosto.verificar(inicio + 5 * 60000 + 1000), true);
  assert.equal(saidas.length, 1);
});

test('escrever num campo conta como uso', () => {
  const { window, saidas } = abrir(3);
  const campo = window.document.getElementById('campo');
  campo.dispatchEvent(new window.KeyboardEvent('keydown', { bubbles: true, key: 'a' }));
  assert.equal(window.DmoPosto.verificar(Date.now() + 2 * 60000), false);
  assert.equal(saidas.length, 0);
});

test('em uso, avisa o servidor no máximo uma vez por minuto', () => {
  const { window, avisos } = abrir(3);
  const inicio = Date.now();
  window.DmoPosto.usar(inicio + 10000);
  window.DmoPosto.usar(inicio + 30000);
  assert.deepEqual(avisos, []);
  window.DmoPosto.usar(inicio + 61000);
  window.DmoPosto.usar(inicio + 90000);
  assert.deepEqual(avisos, ['/posto/ativo']);
  window.DmoPosto.usar(inicio + 125000);
  assert.deepEqual(avisos, ['/posto/ativo', '/posto/ativo']);
});

test('com mais tempo nas Definições, espera esse tempo', () => {
  const { window, saidas } = abrir(10);
  const inicio = Date.now();
  assert.equal(window.DmoPosto.verificar(inicio + 9 * 60000), false);
  assert.equal(window.DmoPosto.verificar(inicio + 10 * 60000 + 1000), true);
  assert.equal(saidas.length, 1);
});

test('fora do posto (login normal) não faz nada', () => {
  const { window } = abrir(undefined);
  assert.equal(window.DmoPosto, undefined);
});

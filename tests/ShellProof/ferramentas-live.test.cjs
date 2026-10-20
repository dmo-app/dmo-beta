const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// As sugestões da Referência do «Registar ferramenta real» seguem o Tipo
// (Diogo, 19/10/2026: a referência sempre toda).
// O servidor em miniatura: a leitura do nome técnico (handler NomeTecnico),
// com as respostas dadas pelo teste; cada pedido fica à espera até o teste o
// soltar, para se poder trocar a ordem das respostas.
function servidorDeNomes(window, respostas, { falhar = false } = {}) {
  const pedidos = [];
  window.fetch = (url, opcoes = {}) => new Promise((resolve, reject) => {
    const nome = new URL(String(url), 'http://dmo.test').searchParams.get('nome');
    const pedido = { url: String(url), nome, abortado: false };
    pedidos.push(pedido);
    if (opcoes.signal) opcoes.signal.addEventListener('abort', () => { pedido.abortado = true; });
    pedido.soltar = () => (falhar
      ? reject(new TypeError('Failed to fetch'))
      : resolve({ ok: true, json: async () => respostas[nome] }));
  });
  return pedidos;
}

const esperar = () => new Promise((resolve) => setTimeout(resolve, 0));

test('a Referência pede as sugestões do tipo escolhido', async () => {
  const dom = new JSDOM(`
    <form id="ferr-create-form">
      <select id="ferr-new-type" name="type_token">
        <option value="CM" selected>CM</option><option value="BQ">BQ</option><option value="MF">MF</option>
      </select>
      <input id="ferr-new-reference" name="reference" data-dmo-suggest="referencias_registo" data-dmo-suggest-tipo="cm">
      <div id="ferr-new-mf-field"><input id="ferr-new-mf"></div>
      <input id="ferr-new-technical"><p id="ferr-technical-notice"></p>
    </form>`, { runScripts: 'outside-only' });
  const { window } = dom;
  const document = window.document;
  window.eval(fs.readFileSync(path.join(assets, 'ferramentas-live.js'), 'utf8'));
  const type = document.getElementById('ferr-new-type');
  const reference = document.getElementById('ferr-new-reference');
  assert.equal(reference.getAttribute('data-dmo-suggest-tipo'), 'cm');

  type.value = 'BQ';
  type.dispatchEvent(new window.Event('change', { bubbles: true }));
  assert.equal(reference.getAttribute('data-dmo-suggest-tipo'), 'bq');
  assert.equal(document.getElementById('ferr-new-mf-field').hidden, true);

  // O nome técnico que muda o tipo também muda a procura. Desde o sweep de
  // 19/10/2026 (3.14) o tipo vem da leitura do servidor, a mesma da ficha.
  const pedidos = servidorDeNomes(window, { M107544MFNNV: { tipo: 'MF', leitura: 'blow/final mold · tipo MF' } });
  const technical = document.getElementById('ferr-new-technical');
  technical.value = 'M107544MFNNV';
  technical.dispatchEvent(new window.Event('input', { bubbles: true }));
  pedidos[0].soltar();
  await esperar();
  assert.equal(type.value, 'MF');
  assert.equal(reference.getAttribute('data-dmo-suggest-tipo'), 'mf');
  dom.window.close();
});

// Sweep de 19/10/2026, 3.14 (F-2): o browser lia o nome técnico à sua
// maneira e propunha outra coisa que a ficha. Agora a leitura é a do
// servidor, só a resposta mais recente conta, e a referência nunca vem do
// nome técnico (Diogo, 19/10/2026: «sempre o nome da referência»).
function formulario(window) {
  window.document.body.innerHTML = `
    <form id="ferr-create-form">
      <select id="ferr-new-type" name="type_token">
        <option value="CM" selected>CM</option><option value="BQ">BQ</option><option value="MF">MF</option>
      </select>
      <input id="ferr-new-reference" name="reference" value="1075C142">
      <input id="ferr-new-technical"><p id="ferr-technical-notice"></p>
    </form>`;
  window.eval(fs.readFileSync(path.join(assets, 'ferramentas-live.js'), 'utf8'));
  const document = window.document;
  return {
    type: document.getElementById('ferr-new-type'),
    reference: document.getElementById('ferr-new-reference'),
    technical: document.getElementById('ferr-new-technical'),
    notice: document.getElementById('ferr-technical-notice'),
  };
}

function escreverNome(window, technical, nome) {
  technical.value = nome;
  technical.dispatchEvent(new window.Event('input', { bubbles: true }));
}

test('3.14: o nome técnico propõe o tipo lido pelo servidor e nunca mexe na referência', async () => {
  const dom = new JSDOM('<!doctype html><body></body>', { runScripts: 'outside-only' });
  const { window } = dom;
  const pedidos = servidorDeNomes(window, { 'A.S.S023.S.F': { tipo: 'BQ', leitura: 'blow head (marisa) · tipo BQ' } });
  const { type, reference, technical, notice } = formulario(window);

  escreverNome(window, technical, 'A.S.S023.S.F');
  assert.equal(pedidos.length, 1);
  assert.match(pedidos[0].url, /handler=NomeTecnico/);
  assert.equal(type.value, 'CM', 'o tipo só muda com a resposta do servidor');
  pedidos[0].soltar();
  await esperar();
  assert.equal(type.value, 'BQ');
  assert.equal(notice.textContent, 'Decodificado: blow head (marisa) · tipo BQ (confirme)');
  assert.equal(reference.value, '1075C142', 'a referência fica a que o operador escreveu');
  dom.window.close();
});

test('3.14: só a resposta ao nome técnico mais recente conta', async () => {
  const dom = new JSDOM('<!doctype html><body></body>', { runScripts: 'outside-only' });
  const { window } = dom;
  const pedidos = servidorDeNomes(window, {
    AT0020SF0: { tipo: 'CM', leitura: 'baffle (tampão) · tipo CM' },
    ABS023SC4713: { tipo: 'BQ', leitura: 'neck ring · tipo BQ' },
  });
  const { type, notice, technical } = formulario(window);

  escreverNome(window, technical, 'AT0020SF0');
  escreverNome(window, technical, 'ABS023SC4713');
  assert.equal(pedidos[0].abortado, true, 'o pedido antigo cancela-se');
  pedidos[1].soltar();
  await esperar();
  // A resposta antiga chega depois (o servidor já a tinha mandado): não conta.
  pedidos[0].soltar();
  await esperar();
  assert.equal(type.value, 'BQ');
  assert.equal(notice.textContent, 'Decodificado: neck ring · tipo BQ (confirme)');
  dom.window.close();
});

test('3.14: sem resposta do servidor, o tipo fica e o aviso diz que a leitura aparece na ficha', async () => {
  const dom = new JSDOM('<!doctype html><body></body>', { runScripts: 'outside-only' });
  const { window } = dom;
  const pedidos = servidorDeNomes(window, {}, { falhar: true });
  const { type, notice, technical } = formulario(window);

  escreverNome(window, technical, 'M107544MFNNV');
  pedidos[0].soltar();
  await esperar();
  assert.equal(type.value, 'CM');
  assert.equal(notice.textContent, 'Não foi possível ler o nome técnico agora: grava-se como está e a leitura aparece na ficha.');
  dom.window.close();
});

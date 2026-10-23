// PERF-10: as sugestões de um campo de texto cancelam o pedido anterior
// (AbortController) quando o texto muda, o campo fica vazio ou perde o foco.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

function abrirCampo() {
  const dom = new JSDOM(`<!doctype html><body>
    <form method="get" action="/Pesquisa"><input id="q" name="q" data-dmo-suggest="clientes"></form>
    <button id="outro">Outro</button></body>`, { url: 'http://dmo.test/Pesquisa', runScripts: 'outside-only' });
  const { window } = dom;
  // O campo só reage a teclas de verdade (event.isTrusted): o teste entrega
  // ao ouvinte o evento com isTrusted verdadeiro.
  const original = window.HTMLInputElement.prototype.addEventListener;
  window.HTMLInputElement.prototype.addEventListener = function (tipo, ouvinte, opcoes) {
    if (tipo !== 'input') return original.call(this, tipo, ouvinte, opcoes);
    return original.call(this, tipo, function (evento) {
      const confiavel = new Proxy(evento, {
        get(alvo, nome) {
          if (nome === 'isTrusted') return true;
          const valor = Reflect.get(alvo, nome, alvo);
          return typeof valor === 'function' ? valor.bind(alvo) : valor;
        },
      });
      return ouvinte.call(this, confiavel);
    }, opcoes);
  };
  const pedidos = [];
  window.fetch = (url, opcoes = {}) => new Promise((responder, falhar) => {
    const pedido = { termo: new URL(String(url), 'http://dmo.test').searchParams.get('q'), signal: opcoes.signal, responder };
    pedidos.push(pedido);
    if (opcoes.signal) {
      opcoes.signal.addEventListener('abort', () => {
        const erro = new Error('The operation was aborted.');
        erro.name = 'AbortError';
        falhar(erro);
      });
    }
  });
  window.eval(fs.readFileSync(path.join(assets, 'js/dmo-suggest.js'), 'utf8'));
  window.DMOSuggest.init();
  const campo = window.document.getElementById('q');
  const lista = () => campo.closest('.dmo-suggest').querySelector('ul');
  const esperar = (ms) => new Promise((pronto) => window.setTimeout(pronto, ms));
  const escrever = (texto) => {
    campo.value = texto;
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
  };
  const resposta = (itens) => ({ ok: true, json: async () => itens });
  return { dom, window, campo, lista, pedidos, esperar, escrever, resposta };
}

test('escrever depressa cancela o pedido anterior e só o texto atual mostra sugestões', async () => {
  const { dom, campo, lista, pedidos, esperar, escrever, resposta } = abrirCampo();
  try {
    campo.focus();
    escrever('ab');
    await esperar(300);
    assert.equal(pedidos.length, 1);
    escrever('abc');
    assert.equal(pedidos[0].signal.aborted, true, 'o texto mudou: o pedido de «ab» é cancelado');
    await esperar(300);
    assert.equal(pedidos.length, 2);
    assert.equal(pedidos[1].termo, 'abc');
    assert.equal(pedidos[1].signal.aborted, false);
    // Mesmo que a resposta antiga chegue, não é mostrada.
    pedidos[0].responder(resposta([{ value: 'velho', label: 'Velho' }]));
    await esperar(10);
    assert.equal(lista().hidden, true);
    pedidos[1].responder(resposta([{ value: 'abc1', label: 'ABC 1' }]));
    await esperar(10);
    assert.equal(lista().hidden, false);
    assert.deepEqual(Array.from(lista().querySelectorAll('li'), (li) => li.textContent.trim()), ['ABC 1']);
  } finally {
    dom.window.close();
  }
});

test('limpar o campo cancela o pedido em voo', async () => {
  const { dom, campo, lista, pedidos, esperar, escrever } = abrirCampo();
  try {
    campo.focus();
    escrever('ab');
    await esperar(300);
    assert.equal(pedidos.length, 1);
    escrever('');
    assert.equal(pedidos[0].signal.aborted, true);
    await esperar(300);
    assert.equal(pedidos.length, 1, 'campo vazio não pede sugestões');
    assert.equal(lista().hidden, true);
  } finally {
    dom.window.close();
  }
});

test('sair do campo cancela o pedido em voo', async () => {
  const { dom, window, campo, pedidos, esperar, escrever } = abrirCampo();
  try {
    campo.focus();
    escrever('ab');
    await esperar(300);
    window.document.getElementById('outro').focus();
    campo.blur();
    assert.equal(pedidos[0].signal.aborted, true);
  } finally {
    dom.window.close();
  }
});

test('um pedido cancelado não mostra mensagem de erro', async () => {
  const { dom, campo, lista, pedidos, esperar, escrever, resposta } = abrirCampo();
  try {
    campo.focus();
    escrever('ab');
    await esperar(300);
    pedidos[0].responder(resposta([{ value: 'ab1', label: 'AB 1' }]));
    await esperar(10);
    assert.equal(lista().hidden, false);
    escrever('abc'); // cancela o pedido seguinte antes de responder
    await esperar(300);
    escrever('abcd');
    await esperar(10);
    assert.equal(pedidos[1].signal.aborted, true);
    assert.equal(lista().querySelector('.dmo-suggest-empty'), null, 'sem mensagem de falha');
    assert.doesNotMatch(lista().textContent, /Não foi possível/);
  } finally {
    dom.window.close();
  }
});

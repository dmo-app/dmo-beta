// Relatório de frontend do Improvements (28/10/2026, FE15): cada campo de sugestões
// juntava um «pagehide» à janela e os campos de uma zona trocada ficavam lá presos.
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

test('trocar a zona do campo 25 vezes deixa um só «pagehide» e sair da página ainda cancela o pedido', async () => {
  const dom = new JSDOM('<!doctype html><body><div id="zona"></div></body>', { url: 'http://dmo.test/Pesquisa', runScripts: 'outside-only' });
  const { window } = dom;
  let pagehide = 0;
  const juntar = window.addEventListener.bind(window);
  window.addEventListener = (tipo, ...resto) => { if (tipo === 'pagehide') pagehide += 1; return juntar(tipo, ...resto); };
  window.eval(fs.readFileSync(path.join(assets, 'js/dmo-suggest.js'), 'utf8'));
  const zona = window.document.getElementById('zona');
  for (let i = 0; i < 25; i++) {
    zona.innerHTML = '<input id="q" name="q" data-dmo-suggest="clientes">';
    window.DMOSuggest.init(zona);
  }
  assert.equal(pagehide, 1);
  window.close();
});

test('sair da página cancela o pedido em voo', async () => {
  const { dom, window, campo, pedidos, esperar, escrever } = abrirCampo();
  try {
    campo.focus();
    escrever('ab');
    await esperar(300);
    assert.equal(pedidos.length, 1);
    window.dispatchEvent(new window.Event('pagehide'));
    assert.equal(pedidos[0].signal.aborted, true);
  } finally { dom.window.close(); }
});

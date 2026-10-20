// Deep sweep, 18/10/2026, tema 8: o que se escreveu e se perde.
// B-admin-1 (o PIN mostrado uma vez sai com o envio seguinte) e B-jobon-6
// (enquanto a procura do texto novo não responde, a lista do anterior abre).
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');
const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

function abrir(html, endereco, fetch) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${html}</body>`,
    { url: endereco, runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  window.fetch = (url, opcoes) => fetch(window, String(url), opcoes || {});
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  return { dom, window, document: window.document, navegou };
}

// Admin → Utilizadores em miniatura: as mensagens (#dmuAvisos) e o formulário
// (#dmuCriar) são zonas; o PIN é um «aviso» com data-dmo-toast-fica.
function utilizadores({ pin = '', recusa = '', pagina = '' } = {}) {
  return `<main data-dmo-troca>
    <h2 id="titulo">Utilizadores</h2>
    <div id="dmuAvisos" data-dmo-regiao>
      ${pin ? `<section data-dmo-toast="aviso" data-dmo-toast-fica role="status">Utilizador criado — PIN temporário ${pin}</section>` : ''}
      ${pagina ? `<div data-dmo-toast="ok" role="status">${pagina}</div>` : ''}
    </div>
    <section id="dmuCriar" data-dmo-regiao data-dmo-parcial>
      ${recusa ? `<div data-dmo-toast="erro" role="alert">${recusa}</div>` : ''}
      <form method="post" action="/Admin/Users?handler=Create">
        <input id="nome" name="nome" value="">
        <button id="criar">Criar utilizador</button>
      </form>
    </section>
  </main>`;
}

const pilha = (document) => Array.from(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]'), (m) => m.textContent.replace('×', '').trim());

test('o PIN de um utilizador criado fica no canto com a criação seguinte e com uma recusa a seguir (B-admin-1)', async () => {
  const respostas = [
    utilizadores({ pin: 'B-2222' }),
    utilizadores({ recusa: 'Já existe um utilizador com este email.' }),
  ];
  const { dom, window, document, navegou } = abrir(utilizadores({ pin: 'A-1111', pagina: 'Template guardada.' }),
    'http://dmo.test/Admin/Users', async () => {
      const corpo = respostas.shift();
      return {
        ok: true, redirected: true, url: 'http://dmo.test/Admin/Users',
        headers: { get: (nome) => (nome === 'Content-Type' ? 'text/html; charset=utf-8' : null) },
        text: async () => `<!doctype html><title>DMO</title><body>${corpo}</body>`,
      };
    });
  try {
    await esperar();
    assert.deepEqual(pilha(document), ['Utilizador criado — PIN temporário A-1111', 'Template guardada.']);

    document.getElementById('nome').value = 'B';
    document.querySelector('#dmuCriar form').requestSubmit(document.getElementById('criar'));
    await esperar();
    assert.deepEqual(navegou, []);
    // A mensagem da página sai (como sempre); o PIN de A fica, ao lado do de B.
    assert.deepEqual(pilha(document), ['Utilizador criado — PIN temporário A-1111', 'Utilizador criado — PIN temporário B-2222']);

    document.getElementById('nome').value = 'C';
    document.querySelector('#dmuCriar form').requestSubmit(document.getElementById('criar'));
    await esperar();
    assert.deepEqual(pilha(document), [
      'Utilizador criado — PIN temporário A-1111',
      'Utilizador criado — PIN temporário B-2222',
      'Já existe um utilizador com este email.',
    ]);

    // Fechar no × tira-o.
    const pinA = Array.from(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]')).find((m) => m.textContent.includes('A-1111'));
    pinA.querySelector('.dmo-toast-fechar').click();
    await esperar(300);
    assert.equal(pilha(document).some((texto) => texto.includes('A-1111')), false);
  } finally {
    dom.window.close();
  }
});

// O Job On em miniatura: a procura em dois campos, a referência e o
// dropdown «Produção» à frente (data-dmo-procura-sempre).
const VAZIO = '<select id="lightProcuraProducoes" disabled><option value="">Produção</option></select>';
const producoes = (ref, id) => `<select id="lightProcuraProducoes" data-dmo-ir><option value="">Produção</option><option value="/JobOn?jobon_id=${id}">${ref} 2026${id} B2</option></select>`;
function jobOn() {
  return `<div data-dmo-troca>
    <div id="jobonFolhas" data-dmo-regiao></div>
    <div class="procura" data-dmo-fica data-dmo-parcial>
      <form method="get"><input id="campo" data-dmo-procura="/JobOn?handler=ProcurarProducao" data-dmo-procura-alvo="alvo" data-dmo-procura-sempre></form>
      <span id="alvo">${VAZIO}</span>
    </div>
  </div>`;
}

function escrever(window, texto) {
  const campo = window.document.getElementById('campo');
  campo.value = texto;
  campo.dispatchEvent(new window.Event('input', { bubbles: true }));
}

test('enquanto a referência nova não responde, o dropdown da anterior não abre produções dela (B-jobon-6)', async () => {
  const pedidos = [];
  let soltar = null;
  let semRede = false;
  const { dom, window, document, navegou } = abrir(jobOn(), 'http://dmo.test/JobOn?ano=2026&mes=10&dia=18', async (_, url) => {
    pedidos.push(url);
    if (url.includes('jobon_id=')) return { ok: true, url, text: async () => `<!doctype html><body>${jobOn()}</body>` };
    if (semRede) throw new TypeError('Failed to fetch');
    if (url.endsWith('q=9322Y152')) await new Promise((resolve) => { soltar = resolve; });
    const corpo = url.endsWith('q=7079C337') ? producoes('7079C337', 993) : url.endsWith('q=9322Y152') ? producoes('9322Y152', 966) : VAZIO;
    return { ok: true, url, text: async () => corpo };
  });
  try {
    escrever(window, '7079C337');
    await esperar(300);
    const antigo = document.getElementById('lightProcuraProducoes');
    assert.equal(antigo.disabled, false, 'as produções da 7079C337 escolhem-se');
    assert.equal(document.getElementById('alvo').hasAttribute('aria-busy'), false);

    // Outra referência: logo à primeira tecla, o dropdown antigo deixa de abrir.
    escrever(window, '9322Y152');
    assert.equal(antigo.disabled, true, 'o dropdown da referência anterior fica desativado');
    assert.equal(document.getElementById('alvo').getAttribute('aria-busy'), 'true');
    antigo.value = '/JobOn?jobon_id=993';
    antigo.dispatchEvent(new window.Event('change', { bubbles: true }));
    await esperar(300);
    assert.equal(pedidos.some((url) => url.includes('jobon_id=993')), false, 'não abre a produção da 7079C337');
    assert.equal(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]').length, 0, 'sem mensagens novas');

    // A resposta chega: o dropdown novo escolhe-se.
    soltar();
    await esperar();
    const novo = document.getElementById('lightProcuraProducoes');
    assert.notEqual(novo, antigo);
    assert.equal(novo.disabled, false);
    assert.match(novo.textContent, /9322Y152/);
    assert.equal(document.getElementById('alvo').hasAttribute('aria-busy'), false);

    // Sem rede: fica desativado, sem mensagem, e a procura repete-se quando a rede volta.
    semRede = true;
    escrever(window, '7079C337');
    await esperar(300);
    assert.equal(document.getElementById('lightProcuraProducoes').disabled, true);
    assert.equal(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]').length, 0);
    semRede = false;
    const antes = pedidos.length;
    window.dispatchEvent(new window.Event('online'));
    await esperar();
    assert.equal(pedidos.length, antes + 1);
    assert.ok(pedidos.at(-1).endsWith('q=7079C337'));
    assert.equal(document.getElementById('lightProcuraProducoes').disabled, false);
    assert.match(document.getElementById('lightProcuraProducoes').textContent, /7079C337/);
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('numa procura com linhas, as linhas do texto anterior não escolhem enquanto o novo não responde', async () => {
  let soltar = null;
  const linhas = (texto) => `<div data-dmo-list="Pesos" data-dmo-seleciona><div data-dmo-row data-id="${texto}" data-dmo-rotulo="${texto}">${texto}</div></div>`;
  const html = `<div><input id="campo" data-dmo-procura="/Controlo/Peso?handler=ProcurarPesos" data-dmo-procura-alvo="alvo" data-dmo-procura-valor="valor">
    <input type="hidden" id="valor"><div id="alvo"></div></div>`;
  const { dom, window, document } = abrir(html, 'http://dmo.test/Controlo/Peso', async (_, url) => {
    if (url.endsWith('q=B')) await new Promise((resolve) => { soltar = resolve; });
    return { ok: true, url, text: async () => linhas(url.endsWith('q=B') ? 'B' : 'A') };
  });
  try {
    escrever(window, 'A');
    await esperar(300);
    escrever(window, 'B');
    document.querySelector('#alvo [data-dmo-row]').click();
    assert.equal(document.getElementById('valor').value, '', 'a linha do A não escolhe');
    await esperar(300);
    document.querySelector('#alvo [data-dmo-row]').click();
    assert.equal(document.getElementById('valor').value, '', 'nem durante o pedido');
    soltar();
    await esperar();
    document.querySelector('#alvo [data-dmo-row]').click();
    assert.equal(document.getElementById('valor').value, 'B');
  } finally {
    dom.window.close();
  }
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// DMO-559: no Editar do Job On, cada ferramenta da lista da procura tem um
// triângulo à frente que abre a ficha dela no painel, sem a escolher.
function abrirLista({ comFicha, painel }) {
  const dom = new JSDOM(`<!doctype html><body>
    <form id="lightForm" method="post" action="/JobOn?handler=Update">
      <select name="cm_tool_id" aria-label="Procurar CM" data-dmo-filter><option value="">Escolher CM</option></select>
    </form></body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
  const { window } = dom;
  window.eval(fs.readFileSync(path.join(assets, 'js/dmo-suggest.js'), 'utf8'));
  window.DMOSuggest.init();
  const abertos = [];
  const noPainel = [];
  window.open = (...args) => { abertos.push(args); return null; };
  if (painel) window.DmoSupport = { tryOpen: (url) => { noPainel.push(url); return true; } };
  const select = window.document.querySelector('select');
  if (comFicha) select.dmoFicha = id => '/Ferramentas/Ficha?tool_id=' + encodeURIComponent(id);
  select.dmoSource = async () => ({
    items: [{ value: 'CM 5809/1', label: '5809 · Lote 1' }, { value: 'CM-2', label: '5809 · Lote 2' }],
  });
  const campo = window.document.querySelector('input.dmo-suggest-combo');
  const esperar = (ms) => new Promise((pronto) => window.setTimeout(pronto, ms));
  return { dom, window, select, campo, abertos, noPainel, esperar };
}

async function mostrarLista(ctx) {
  ctx.campo.focus();
  await ctx.esperar(20);
  return ctx.window.document.querySelectorAll('.dmo-suggest ul li');
}

function mousedown(window, alvo) {
  alvo.dispatchEvent(new window.MouseEvent('mousedown', { bubbles: true, cancelable: true }));
}

test('cada ferramenta da lista tem o triângulo que aponta para a ficha dela', async () => {
  const ctx = abrirLista({ comFicha: true });
  try {
    const linhas = await mostrarLista(ctx);
    assert.equal(linhas.length, 2);
    const fichas = [...linhas].map(li => li.querySelector('a.dmo-suggest-ficha'));
    assert.ok(fichas.every(Boolean), 'falta o triângulo numa linha');
    assert.equal(fichas[0].getAttribute('href'), '/Ferramentas/Ficha?tool_id=CM%205809%2F1');
    assert.equal(fichas[1].getAttribute('href'), '/Ferramentas/Ficha?tool_id=CM-2');
    assert.match(fichas[0].getAttribute('aria-label'), /5809 · Lote 1/);
    assert.equal(linhas[0].querySelector('.dmo-suggest-texto').textContent, '5809 · Lote 1');
  } finally {
    ctx.dom.window.close();
  }
});

test('clicar no triângulo abre a ficha no painel e não escolhe a ferramenta', async () => {
  const ctx = abrirLista({ comFicha: true, painel: true });
  try {
    const linhas = await mostrarLista(ctx);
    const triangulo = linhas[1].querySelector('a.dmo-suggest-ficha .dmo-caret');
    mousedown(ctx.window, triangulo);
    assert.deepEqual(ctx.noPainel, ['/Ferramentas/Ficha?tool_id=CM-2']);
    assert.equal(ctx.abertos.length, 0, 'com o painel não se abre outro separador');
    assert.equal(ctx.select.value, '', 'o triângulo não escolhe a ferramenta');
    assert.equal(ctx.window.document.querySelector('.dmo-suggest ul').hidden, true, 'a lista fecha');
  } finally {
    ctx.dom.window.close();
  }
});

test('sem painel de apoio, o triângulo abre a ficha noutro separador sem escolher', async () => {
  const ctx = abrirLista({ comFicha: true });
  try {
    const linhas = await mostrarLista(ctx);
    mousedown(ctx.window, linhas[0].querySelector('a.dmo-suggest-ficha'));
    assert.deepEqual(ctx.abertos, [['/Ferramentas/Ficha?tool_id=CM%205809%2F1', '_blank']]);
    assert.equal(ctx.select.value, '');
  } finally {
    ctx.dom.window.close();
  }
});

test('clicar no texto da ferramenta escolhe-a e não abre a ficha', async () => {
  const ctx = abrirLista({ comFicha: true, painel: true });
  try {
    const linhas = await mostrarLista(ctx);
    mousedown(ctx.window, linhas[1].querySelector('.dmo-suggest-texto'));
    assert.equal(ctx.select.value, 'CM-2');
    assert.equal(ctx.campo.value, '5809 · Lote 2');
    assert.equal(ctx.noPainel.length, 0);
    assert.equal(ctx.abertos.length, 0);
  } finally {
    ctx.dom.window.close();
  }
});

test('uma procura sem endereço de ficha não desenha triângulo', async () => {
  const ctx = abrirLista({ comFicha: false });
  try {
    const linhas = await mostrarLista(ctx);
    assert.equal(linhas.length, 2);
    assert.equal(ctx.window.document.querySelectorAll('a.dmo-suggest-ficha').length, 0);
  } finally {
    ctx.dom.window.close();
  }
});

test('a linha que limpa a escolha não tem triângulo', async () => {
  const ctx = abrirLista({ comFicha: true });
  try {
    await mostrarLista(ctx);
    // Escolhe uma ferramenta; ao voltar ao campo vazio a primeira linha limpa a escolha.
    mousedown(ctx.window, ctx.window.document.querySelectorAll('.dmo-suggest ul li')[0].querySelector('.dmo-suggest-texto'));
    assert.equal(ctx.select.value, 'CM 5809/1');
    ctx.campo.blur();
    ctx.campo.focus();
    await ctx.esperar(20);
    const linhas = ctx.window.document.querySelectorAll('.dmo-suggest ul li');
    assert.equal(linhas.length, 3);
    assert.equal(linhas[0].querySelector('a.dmo-suggest-ficha'), null, 'a linha «Escolher CM» não tem ficha');
    assert.ok(linhas[1].querySelector('a.dmo-suggest-ficha'));
  } finally {
    ctx.dom.window.close();
  }
});

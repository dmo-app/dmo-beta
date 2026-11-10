// DMO-751 (A-0525): na procura do Job On, o Enter entra na produção
// escolhida; com só o início da referência, completa-a e enche o dropdown.
// Diogo, 10/11/2026: «ao carregar Enter com referência e número de produção
// deveria entrar».
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');
const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

const vazio = '<select id="lightProcuraProducoes" disabled><option value="" selected>Produção</option></select>';
const com3245 = '<select id="lightProcuraProducoes" data-dmo-ir><option value="">Escolher produção</option>'
  + '<option value="/JobOn/Folha?jobon_id=138" selected>202604 B1</option>'
  + '<option value="/JobOn/Folha?jobon_id=92">202603 B1</option></select>';

function abrir(inicial, referencias) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><body><form method="get" action="/JobOn">
    <input id="lightSearch" type="search" name="procura">
    <span id="lightProcura">${inicial}</span>
    <a id="jobonCriar" href="/JobOn/Folha?novo=1">Criar Job On</a></form></body>`,
  { url: 'http://dmo.test/JobOn?ano=2026&mes=11&dia=10', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url) => {
    const final = new URL(String(url), 'http://dmo.test');
    pedidos.push(final);
    const q = final.searchParams.get('q');
    return { ok: true, json: async () => referencias.filter((r) => r.includes(q)).map((r) => ({ value: r, label: r })) };
  };
  // A procura ao escrever (simplificada): o dropdown da referência completa.
  const campo = window.document.getElementById('lightSearch');
  const alvo = window.document.getElementById('lightProcura');
  campo.addEventListener('input', () => {
    alvo.setAttribute('data-dmo-procura-espera', '');
    setTimeout(() => {
      alvo.innerHTML = campo.value === '3245C002' ? com3245 : vazio;
      alvo.removeAttribute('data-dmo-procura-espera');
    }, 30);
  });
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-procura-enter.js'), 'utf8'));
  const enter = (alvoTecla) => {
    const evento = new window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    alvoTecla.dispatchEvent(evento);
    return evento;
  };
  return { dom, window, campo, alvo, enter, navegou, pedidos };
}

test('com a referência completa e a produção no dropdown, o Enter entra (não recarrega a página)', async () => {
  const s = abrir(com3245, ['3245C002']);
  try {
    s.campo.value = '3245C002';
    const evento = s.enter(s.campo);
    assert.equal(evento.defaultPrevented, true);
    await esperar();
    assert.equal(s.navegou.length, 1);
    assert.equal(s.pedidos.length, 0);
  } finally { s.dom.window.close(); }
});

test('Enter no dropdown entra na produção escolhida', async () => {
  const s = abrir(com3245, ['3245C002']);
  try {
    const select = s.window.document.getElementById('lightProcuraProducoes');
    select.value = '/JobOn/Folha?jobon_id=92';
    assert.equal(s.enter(select).defaultPrevented, true);
    assert.equal(s.navegou.length, 1);
  } finally { s.dom.window.close(); }
});

test('com só o início («3245»), o Enter completa a referência, enche o dropdown e passa-lhe o foco', async () => {
  const s = abrir(vazio, ['3245C002', '7151S022']);
  try {
    s.campo.focus();
    s.campo.value = '3245';
    assert.equal(s.enter(s.campo).defaultPrevented, true);
    await esperar(500);
    assert.equal(s.campo.value, '3245C002');
    const select = s.window.document.getElementById('lightProcuraProducoes');
    assert.equal(select.disabled, false);
    assert.equal(s.window.document.activeElement, select);
    assert.equal(s.navegou.length, 0, 'completar não abre nada sozinho');
    s.enter(select);
    assert.equal(s.navegou.length, 1);
  } finally { s.dom.window.close(); }
});

test('com várias referências possíveis, o Enter não escolhe nenhuma nem cria Job On', async () => {
  const s = abrir(vazio, ['3245C002', '3245C010']);
  try {
    s.campo.value = '3245';
    s.enter(s.campo);
    await esperar(500);
    assert.equal(s.campo.value, '3245');
    assert.equal(s.navegou.length, 0);
  } finally { s.dom.window.close(); }
});

test('o Enter com a resposta da procura a caminho espera pela lista do texto atual', async () => {
  const s = abrir(vazio, ['3245C002']);
  try {
    s.campo.value = '3245C002';
    s.campo.dispatchEvent(new s.window.Event('input', { bubbles: true }));
    s.enter(s.campo);
    assert.equal(s.navegou.length, 0, 'não abre a lista anterior');
    await esperar(200);
    assert.equal(s.navegou.length, 1);
  } finally { s.dom.window.close(); }
});

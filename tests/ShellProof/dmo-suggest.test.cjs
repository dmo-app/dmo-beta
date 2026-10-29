const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A folha do Job On em miniatura: o seletor da BQ com procura no servidor
// (select.dmoSource), dentro do formulário que grava a folha.
function abrirFolha() {
  const dom = new JSDOM(`<!doctype html><body>
    <form id="lightForm" method="post" action="/JobOn?handler=Create">
      <select name="bq_tool_id" aria-label="Procurar BQ" data-dmo-filter><option value="">Escolher BQ</option></select>
      <button type="submit" id="lightSave">Guardar Job On</button>
    </form></body>`, { url: 'http://dmo.test/JobOn?novo=1', runScripts: 'outside-only' });
  const { window } = dom;
  window.eval(fs.readFileSync(path.join(assets, 'js/dmo-suggest.js'), 'utf8'));
  window.DMOSuggest.init(); // o jsdom ainda está em «loading»
  const select = window.document.querySelector('select');
  const pedidos = [];
  // Cada procura fica à espera até o teste responder por ela.
  select.dmoSource = (termo) => new Promise((responder) => pedidos.push({ termo, responder }));
  const campo = window.document.querySelector('input.dmo-suggest-combo');
  const esperar = (ms) => new Promise((pronto) => window.setTimeout(pronto, ms));
  const escrever = (texto) => {
    campo.value = texto;
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
  };
  const enter = () => {
    const evento = new window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    campo.dispatchEvent(evento);
    return evento;
  };
  return { dom, window, select, campo, pedidos, esperar, escrever, enter };
}

test('Enter na procura da BQ antes de a lista abrir não envia a folha', async () => {
  const { dom, campo, pedidos, esperar, escrever, enter } = abrirFolha();
  try {
    campo.focus();
    await esperar(5);
    escrever('C337');
    assert.equal(campo.closest('.dmo-suggest').querySelector('ul').hidden, true, 'a procura ainda não respondeu');
    assert.ok(pedidos.length >= 1);
    assert.equal(enter().defaultPrevented, true, 'o Enter não pode enviar o formulário do Job On');
  } finally {
    dom.window.close();
  }
});

test('Enter enquanto a procura nova não responde não escolhe a linha da lista anterior', async () => {
  const { dom, select, campo, pedidos, esperar, escrever, enter } = abrirFolha();
  try {
    campo.focus();
    await esperar(5);
    pedidos[0].responder({ items: [{ value: 'T-lote-1', label: '5447 · Lote 1' }] });
    await esperar(5);
    escrever('C3');
    assert.equal(enter().defaultPrevented, true);
    assert.equal(select.value, '', 'a primeira linha era da procura anterior');

    await esperar(300);
    pedidos[pedidos.length - 1].responder({ items: [{ value: 'T-C337-2', label: 'C337 · Lote 2' }] });
    await esperar(5);
    assert.equal(enter().defaultPrevented, true);
    assert.equal(select.value, 'T-C337-2', 'com a resposta à vista, o Enter escolhe a primeira linha');
  } finally {
    dom.window.close();
  }
});

test('num filtro sem fonte, o Enter continua a escolher a primeira opção filtrada sem enviar', () => {
  const dom = new JSDOM(`<!doctype html><body>
    <form method="get"><select name="maquina" data-dmo-filter><option value="">Todas</option><option value="1">B1</option><option value="2">B2</option></select></form></body>`,
  { url: 'http://dmo.test/Controlo/Peso', runScripts: 'outside-only' });
  const { window } = dom;
  try {
    window.eval(fs.readFileSync(path.join(assets, 'js/dmo-suggest.js'), 'utf8'));
    window.DMOSuggest.init();
    const select = window.document.querySelector('select');
    const campo = window.document.querySelector('input.dmo-suggest-combo');
    campo.focus();
    campo.value = 'B2';
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
    const evento = new window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    campo.dispatchEvent(evento);
    assert.equal(evento.defaultPrevented, true);
    assert.equal(select.value, '2');
  } finally {
    window.close();
  }
});

test('Enter sem navegar nas flechas confirma a ferramenta já escolhida, não a limpa (auditoria do Codex, 29/10/2026)', async () => {
  const { dom, select, campo, pedidos, esperar, enter } = abrirFolha();
  try {
    dom.window.Element.prototype.scrollIntoView = function () {};
    campo.focus();
    await esperar(5);
    pedidos[0].responder({ items: [{ value: 'T-5447-3', label: '5447 · Lote 3' }, { value: 'T-5448-9', label: '5448 · Lote 9' }] });
    await esperar(5);
    // Escolhe «5447 · Lote 3» navegando com a flecha, como um utilizador normal.
    campo.dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    enter();
    assert.equal(select.value, 'T-5447-3', 'a escolha ficou feita');

    // Reabrir o combo (sair e voltar ao campo) com a ferramenta já escolhida:
    // a procura volta a correr com o filtro vazio e a app põe a linha de
    // «limpar» à frente da já escolhida (showRemote).
    campo.dispatchEvent(new dom.window.Event('blur', { bubbles: true }));
    campo.focus();
    await esperar(5);
    pedidos[pedidos.length - 1].responder({ items: [{ value: 'T-5447-3', label: '5447 · Lote 3' }, { value: 'T-5448-9', label: '5448 · Lote 9' }] });
    await esperar(5);

    // Enter já, sem tocar nas flechas: antes do bug, escolhia a linha de
    // «limpar» que ficou à frente e apagava a CM em silêncio.
    assert.equal(enter().defaultPrevented, true);
    assert.equal(select.value, 'T-5447-3', 'o Enter confirmou a ferramenta já escolhida, não limpou a CM');
  } finally {
    dom.window.close();
  }
});

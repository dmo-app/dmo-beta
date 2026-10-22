const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// PU NNPB com a rede lenta (deep sweep 18/10/2026, B-pegamentos-2): «10» Enter
// e logo «11» Enter gravava «1011», porque o campo ficava com «10» enquanto o
// POST esperava e o que se escrevia juntava-se; o que se escrevia sem Enter
// perdia-se quando a folha trocava. A folha em miniatura, como a da página:
// #puFolha é a zona e o formulário «+ Adicionar punção» não tem id.
const VOLTA = 'http://dmo.test/Controlo/Puncoes?controlo_id=125';

function pagina(linhas, vez) {
  return `<main data-dmo-troca><h1 id="titulo">PU NNPB</h1>
    <div id="puFolha" class="dmo-zona" data-dmo-regiao>
      <section id="puncoes-lista" data-dmo-parcial>
        <form method="post" action="/Controlo/Puncoes?handler=Add" class="pu-add-form">
          <input type="hidden" name="__RequestVerificationToken" value="token-${vez}">
          <input type="hidden" name="controlo_id" value="125">
          <input type="hidden" name="referencia" value="1075" data-pu-mirror="pu-referencia">
          <input type="hidden" name="lote" value="1" data-pu-mirror="pu-lote">
          <input id="pu-numero" name="numero" maxlength="40" required value="" autofocus>
          <button type="submit">+ Adicionar punção</button>
        </form>
        <p id="linhas">${linhas.join(',')}</p>
      </section>
    </div></main>`;
}

// O servidor guarda cada POST à espera até o teste o soltar (a rede lenta) e
// depois grava o número e volta à folha (PRG). sem rede: o fetch falha.
async function abrirPu({ semRede = false } = {}) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>PU NNPB</title><body>${pagina([], 0)}</body>`,
    { url: VOLTA, runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const s = { linhas: [], posts: [], portas: [], vez: 0 };
  window.fetch = async (url, opcoes = {}) => {
    if (opcoes.method === 'POST') {
      const numero = new URLSearchParams(String(opcoes.body)).get('numero');
      s.posts.push(numero);
      if (semRede) throw new TypeError('Failed to fetch');
      await new Promise((soltar) => s.portas.push(soltar));
      s.linhas.push(numero);
    } else if (semRede) {
      throw new TypeError('Failed to fetch');
    }
    const corpo = `<!doctype html><title>PU NNPB</title><body>${pagina(s.linhas, ++s.vez)}</body>`;
    return {
      ok: true, url: VOLTA, redirected: opcoes.method === 'POST',
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? 'text/html; charset=utf-8' : null) },
      text: async () => corpo,
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'js/puncoes-numero.js'), 'utf8'));
  const { document } = window;
  document.getElementById('pu-numero').focus();
  const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));
  // Solta o POST que está no servidor e espera que a resposta seja aplicada.
  s.soltar = async () => { await esperar(); s.portas.shift()(); await esperar(); };
  // Teclas no campo com o foco, como no browser: cada uma substitui o que
  // está selecionado ou entra onde está o cursor.
  const escrever = (texto) => {
    const campo = document.activeElement;
    assert.equal(campo && campo.id, 'pu-numero', 'o foco está no número da punção');
    for (const tecla of texto) {
      const { selectionStart: inicio, selectionEnd: fim, value } = campo;
      campo.value = value.slice(0, inicio) + tecla + value.slice(fim);
      campo.setSelectionRange(inicio + 1, inicio + 1);
      campo.dispatchEvent(new window.InputEvent('input', { bubbles: true }));
    }
  };
  const apagarTudo = () => {
    const campo = document.activeElement;
    campo.value = '';
    campo.dispatchEvent(new window.InputEvent('input', { bubbles: true }));
  };
  const enter = () => document.activeElement.form.requestSubmit();
  const campo = () => document.getElementById('pu-numero');
  await esperar();
  return { dom, document, s, escrever, apagarTudo, enter, campo, esperar, navegou };
}

test('o número escrito logo a seguir ao Enter não se junta ao anterior: grava 10 e 11, não 1011', async () => {
  const { dom, s, escrever, enter, campo, navegou } = await abrirPu();
  try {
    escrever('10');
    enter();
    escrever('11');
    assert.equal(campo().value, '11', 'o 11 substitui o 10 enviado');
    enter();
    await s.soltar();
    await s.soltar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(s.posts, ['10', '11']);
    assert.equal(campo().value, '');
  } finally {
    dom.window.close();
  }
});

test('42 e 43 com Enter e 44 sem Enter: gravam 42 e 43, e o 44 fica no campo', async () => {
  const { dom, s, escrever, enter, campo, document } = await abrirPu();
  try {
    escrever('42');
    enter();
    escrever('43');
    enter();
    escrever('44');
    await s.soltar();
    assert.equal(campo().value, '43', 'o 43 ainda espera e mostra-se no campo novo');
    assert.equal(campo().form.getAttribute('aria-busy'), 'true');
    assert.deepEqual([campo().selectionStart, campo().selectionEnd], [0, 2], 'selecionado: o que se escreve substitui-o');
    await s.soltar();
    assert.deepEqual(s.posts, ['42', '43']);
    assert.equal(document.getElementById('linhas').textContent, '42,43');
    assert.equal(campo().value, '44', 'o que se escreveu sem Enter volta ao campo novo');
    assert.equal(document.activeElement, campo());
    enter();
    await s.soltar();
    assert.deepEqual(s.posts, ['42', '43', '44']);
  } finally {
    dom.window.close();
  }
});

test('o que se escreve sem Enter enquanto o número anterior grava fica no campo novo', async () => {
  const { dom, s, escrever, enter, campo, document } = await abrirPu();
  try {
    escrever('45');
    enter();
    const antes = campo();
    escrever('46');
    await s.soltar();
    assert.notEqual(campo(), antes, 'a folha trocou');
    assert.equal(document.getElementById('linhas').textContent, '45');
    assert.equal(campo().value, '46');
    assert.equal(document.activeElement, campo());
    escrever('7');
    assert.equal(campo().value, '467', 'continua a escrever a seguir');
    assert.deepEqual(s.posts, ['45']);
  } finally {
    dom.window.close();
  }
});

test('o que se escreveu e se apagou não volta ao campo novo', async () => {
  const { dom, s, escrever, apagarTudo, enter, campo } = await abrirPu();
  try {
    escrever('45');
    enter();
    escrever('46');
    apagarTudo();
    await s.soltar();
    assert.equal(campo().value, '');
  } finally {
    dom.window.close();
  }
});

test('sem rede (a gravação falha), o número escrito fica no campo', async () => {
  const { dom, s, escrever, enter, campo, esperar, navegou } = await abrirPu({ semRede: true });
  try {
    escrever('20');
    const antes = campo();
    enter();
    await esperar(50);
    assert.deepEqual(navegou, []);
    assert.deepEqual(s.posts, ['20']);
    assert.equal(campo(), antes, 'nada trocou');
    assert.equal(campo().value, '20');
  } finally {
    dom.window.close();
  }
});

// Diogo, 21/10/2026: «quando o numero de um puncao é diferente deveria pedir o
// lote». O número que já está na lista mostra «Lote desta punção» e torna-o
// obrigatório; um número novo esconde-o e não o envia.
test('um número já registado pede o lote dessa punção; um número novo não', () => {
  const dom = new JSDOM(`<!doctype html><body>
    <form class="pu-add-form" data-pu-numeros="41|42">
      <input id="pu-numero" name="numero">
      <div class="dmo-field pu-lote-puncao" hidden><input id="pu-lote-puncao" name="lote_puncao"></div>
    </form></body>`, { runScripts: 'outside-only' });
  const { window } = dom;
  window.eval(fs.readFileSync(path.join(assets, 'js/puncoes-numero.js'), 'utf8'));
  const { document } = window;
  const numero = document.getElementById('pu-numero');
  const lote = document.getElementById('pu-lote-puncao');
  const escrever = (valor) => { numero.value = valor; numero.dispatchEvent(new window.Event('input', { bubbles: true })); };

  escrever('41');
  assert.equal(lote.closest('.pu-lote-puncao').hidden, false);
  assert.equal(lote.disabled, false);
  assert.equal(lote.required, true);

  escrever('43');
  assert.equal(lote.closest('.pu-lote-puncao').hidden, true);
  assert.equal(lote.disabled, true);
  assert.equal(lote.required, false);
});

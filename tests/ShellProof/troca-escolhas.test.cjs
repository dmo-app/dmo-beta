const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Deep sweep, 18/10/2026, com a rede lenta: escolhas feitas enquanto uma
// gravação ainda não respondeu. Um Resumo do Controlo em miniatura, como
// _ResumoArea.cshtml: uma área com o OK/NOK (grava ao escolher) e o
// comentário numa janela, na mesma zona.
function areaResumo({ estado = '', comentario = '' }) {
  const opcao = (valor, texto) => `<option value="${valor}"${estado === valor ? ' selected' : ''}>${texto}</option>`;
  return `<div id="area-CM" data-dmo-regiao>
      <div class="resumo-area">
        <strong id="estado-texto">${estado || 'não registado'}</strong>
        <form method="post" action="/Controlo/Resumo?handler=AreaState">
          <input type="hidden" name="jobon_id" value="9"><input type="hidden" name="area" value="CM">
          <select id="estado-CM" name="estado" data-dmo-autosubmit>${opcao('', '—')}${opcao('OK', 'OK')}${opcao('NOK', 'NOK')}</select>
        </form>
        <button type="button" id="nota" data-dmo-dialog="janela-CM">${comentario || 'Comentário (opcional)'}</button>
        <dialog id="janela-CM" class="dmo-dialog">
          <form method="post" action="/Controlo/Resumo?handler=AreaState">
            <input type="hidden" name="jobon_id" value="9"><input type="hidden" name="area" value="CM">
            <textarea id="texto-CM" name="comentario" autofocus>${comentario}</textarea>
            <button type="button" id="cancelar" data-dmo-dialog-close>Cancelar</button>
            <button type="submit" id="guardar">Guardar</button>
          </form>
        </dialog>
      </div>
    </div>`;
}

function paginaResumo(area) {
  return `<main data-dmo-troca><h2 id="titulo">Resumo do Controlo</h2>
    <div class="resumo-areas" data-dmo-parcial>${areaResumo(area)}</div></main>`;
}

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

// Cada POST fica à espera até o teste o soltar (a rede lenta); o servidor
// grava quando o recebe e volta à mesma página (PRG). Cada formulário só
// envia o seu campo: o outro fica como está.
async function abrirResumo(inicial = {}) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const guardado = { estado: '', comentario: '', ...inicial };
  const dom = new JSDOM(`<!doctype html><title>Resumo</title><body>${paginaResumo(guardado)}</body>`,
    { url: 'http://dmo.test/Controlo/Resumo?jobon_id=9', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  // O jsdom não tem showModal: abrir e fechar mexem só no atributo open.
  window.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  window.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
  const pedidos = [];
  const portas = [];
  window.fetch = async (url, opcoes = {}) => {
    const corpo = new URLSearchParams(String(opcoes.body || ''));
    const metodo = opcoes.method || 'GET';
    pedidos.push({ metodo, estado: corpo.get('estado'), comentario: corpo.get('comentario') });
    if (metodo === 'POST') {
      await new Promise((soltar) => portas.push(soltar));
      if (corpo.has('estado')) guardado.estado = corpo.get('estado');
      if (corpo.has('comentario')) guardado.comentario = corpo.get('comentario');
    }
    return {
      ok: true, redirected: metodo === 'POST', url: 'http://dmo.test/Controlo/Resumo?jobon_id=9',
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? 'text/html; charset=utf-8' : null) },
      text: async () => `<!doctype html><title>Resumo</title><body>${paginaResumo(guardado)}</body>`
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  const soltar = async () => { await esperar(); portas.shift()(); await esperar(); };
  return { dom, window, document: window.document, pedidos, portas, soltar, guardado, navegou };
}

// Uma escolha com o rato: o clique na lista e o change.
function escolherComRato(window, valor) {
  const select = window.document.getElementById('estado-CM');
  select.focus();
  select.dispatchEvent(new window.Event('pointerdown', { bubbles: true }));
  select.value = valor;
  select.dispatchEvent(new window.Event('change', { bubbles: true }));
}

// Uma tecla num campo: o browser muda o valor e lança o change entre o
// keydown e o keyup (as setas num dropdown fechado, um número numa data).
function tecla(window, campo, key, valor) {
  const baixo = new window.KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
  campo.dispatchEvent(baixo);
  if (valor !== undefined && !baixo.defaultPrevented) {
    campo.value = valor;
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
    campo.dispatchEvent(new window.Event('change', { bubbles: true }));
  }
  campo.dispatchEvent(new window.KeyboardEvent('keyup', { key, bubbles: true }));
  return baixo;
}

const postsDe = (pedidos) => pedidos.filter((p) => p.metodo === 'POST').map((p) => p.estado ?? `comentário ${p.comentario}`);

test('OK, NOK e OK outra vez antes de a primeira gravação responder: fica gravada a última escolha', async () => {
  const s = await abrirResumo({ estado: 'NOK' });
  try {
    escolherComRato(s.window, 'OK');
    await esperar();
    escolherComRato(s.window, 'NOK');
    await esperar();
    escolherComRato(s.window, 'OK');
    await esperar();
    while (s.portas.length) await s.soltar();
    assert.deepEqual(s.navegou, []);
    assert.deepEqual(postsDe(s.pedidos), ['OK', 'NOK', 'OK'], 'a terceira escolha não é a repetição de um clique');
    assert.equal(s.guardado.estado, 'OK');
    assert.equal(s.document.getElementById('estado-CM').value, 'OK');
    assert.equal(s.document.getElementById('estado-texto').textContent, 'OK');
  } finally {
    s.dom.window.close();
  }
});

test('o mesmo envio repetido enquanto o anterior não responde continua a gravar uma vez', async () => {
  const s = await abrirResumo();
  try {
    const select = s.document.getElementById('estado-CM');
    escolherComRato(s.window, 'NOK');
    select.form.requestSubmit();
    await esperar();
    escolherComRato(s.window, 'OK');
    select.form.requestSubmit();
    await esperar();
    while (s.portas.length) await s.soltar();
    assert.deepEqual(postsDe(s.pedidos), ['NOK', 'OK'], 'o segundo envio igual ao último que espera é ignorado');
    assert.equal(s.guardado.estado, 'OK');
  } finally {
    s.dom.window.close();
  }
});

test('o comentário aberto enquanto o OK/NOK da mesma área grava fica aberto, com o texto e o cursor no sítio', async () => {
  const s = await abrirResumo();
  try {
    escolherComRato(s.window, 'NOK');
    await esperar();
    s.document.getElementById('nota').click();
    const texto = s.document.getElementById('texto-CM');
    assert.equal(s.document.activeElement, texto);
    texto.value = 'Rebarba no gargalo da CM';
    texto.setSelectionRange(11, 11);
    await s.soltar();
    assert.deepEqual(s.navegou, []);
    assert.equal(s.document.getElementById('estado-texto').textContent, 'NOK', 'o OK/NOK gravado aparece');
    const novo = s.document.getElementById('texto-CM');
    assert.notEqual(novo, texto, 'a zona trocou');
    assert.ok(s.document.getElementById('janela-CM').hasAttribute('open'), 'a janela continua aberta');
    assert.equal(novo.value, 'Rebarba no gargalo da CM', 'o que se escreveu fica');
    assert.equal(s.document.activeElement, novo, 'o foco fica no texto, não no botão que abre a janela');
    assert.deepEqual([novo.selectionStart, novo.selectionEnd], [11, 11], 'o cursor fica onde estava');

    // Gravar na própria janela fecha-a, como antes.
    novo.value = 'Rebarba no gargalo da CM, ver lote';
    s.document.getElementById('guardar').click();
    await s.soltar();
    assert.deepEqual(postsDe(s.pedidos), ['NOK', 'comentário Rebarba no gargalo da CM, ver lote']);
    assert.equal(s.guardado.estado, 'NOK');
    assert.equal(s.guardado.comentario, 'Rebarba no gargalo da CM, ver lote', 'o comentário fica inteiro');
    assert.ok(!s.document.getElementById('janela-CM').hasAttribute('open'), 'gravar na janela fecha-a');
    assert.equal(s.document.activeElement.id, 'nota', 'o foco volta ao botão que a abriu');
  } finally {
    s.dom.window.close();
  }
});

test('uma janela fechada numa zona trocada continua fechada', async () => {
  const s = await abrirResumo();
  try {
    escolherComRato(s.window, 'OK');
    await s.soltar();
    assert.ok(!s.document.getElementById('janela-CM').hasAttribute('open'));
    assert.equal(s.document.activeElement.id, 'estado-CM');
  } finally {
    s.dom.window.close();
  }
});

test('com as setas, o OK/NOK só grava com Enter ou ao sair do campo', async () => {
  const s = await abrirResumo();
  try {
    const select = s.document.getElementById('estado-CM');
    select.focus();
    tecla(s.window, select, 'ArrowDown', 'OK');
    tecla(s.window, select, 'ArrowDown', 'NOK');
    await esperar();
    assert.deepEqual(postsDe(s.pedidos), [], 'cada seta só mostra a opção seguinte');
    assert.ok(select.hasAttribute('data-dmo-por-aplicar'));
    const enter = tecla(s.window, select, 'Enter');
    assert.equal(enter.defaultPrevented, true);
    await esperar();
    assert.deepEqual(postsDe(s.pedidos), ['NOK'], 'Enter grava a opção à vista');
    await s.soltar();
    assert.equal(s.guardado.estado, 'NOK');

    const outro = s.document.getElementById('estado-CM');
    outro.focus();
    tecla(s.window, outro, 'ArrowUp', 'OK');
    await esperar();
    assert.deepEqual(postsDe(s.pedidos), ['NOK']);
    outro.blur();
    await esperar();
    assert.deepEqual(postsDe(s.pedidos), ['NOK', 'OK'], 'sair do campo grava a opção à vista');
    await s.soltar();
    assert.equal(s.guardado.estado, 'OK');
    assert.ok(!s.document.getElementById('estado-CM').hasAttribute('data-dmo-por-aplicar'));
  } finally {
    s.dom.window.close();
  }
});

test('com o rato, o OK/NOK grava logo; Enter sem nada por aplicar não grava outra vez', async () => {
  const s = await abrirResumo();
  try {
    escolherComRato(s.window, 'OK');
    await esperar();
    assert.deepEqual(postsDe(s.pedidos), ['OK']);
    const select = s.document.getElementById('estado-CM');
    tecla(s.window, select, 'Enter');
    await esperar();
    assert.deepEqual(postsDe(s.pedidos), ['OK']);
    await s.soltar();
  } finally {
    s.dom.window.close();
  }
});

// A Associar Job On das Boquilhas (Index.cshtml:232-248): o dropdown
// «Produção» associa ao escolher e não se desfaz.
test('na «Produção» da Associar Job On, as setas não associam a primeira produção a que chegam', async () => {
  const virtualConsole = new VirtualConsole();
  const dom = new JSDOM(`<!doctype html><title>Boquilhas</title><body><main data-dmo-troca>
    <div data-dmo-parcial><div id="registo" data-dmo-regiao>
      <form method="post" action="/Boquilhas?handler=AssociateJobOn">
        <input type="hidden" name="trace_id" value="1051">
        <select id="jobNumber" name="jobon_id" data-dmo-autosubmit="valor">
          <option value="">Produção</option><option value="1013">AG03</option><option value="1012">AG02</option>
        </select>
      </form></div></div></main></body>`, { url: 'http://dmo.test/Boquilhas?trace_id=1051', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const enviados = [];
  window.fetch = async (url, opcoes = {}) => {
    enviados.push(new URLSearchParams(String(opcoes.body || '')).get('jobon_id'));
    return new Promise(() => {});
  };
  try {
    window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
    const select = window.document.getElementById('jobNumber');
    select.focus();
    tecla(window, select, 'ArrowDown', '1013');
    tecla(window, select, 'ArrowDown', '1012');
    await esperar();
    assert.deepEqual(enviados, []);
    tecla(window, select, 'Enter');
    await esperar();
    assert.deepEqual(enviados, ['1012'], 'associa a produção confirmada com Enter');
  } finally {
    window.close();
  }
});

// Os filtros por data dos Históricos (Job On, Boquilhas): escrever a data
// número a número não aplica a meio.
test('uma data escrita ao teclado só aplica com Enter ou ao sair do campo; escolhida no calendário aplica logo', async () => {
  const dom = new JSDOM(`<!doctype html><title>Histórico</title><body><main data-dmo-troca>
    <div data-dmo-parcial><form method="get" action="/Boquilhas">
      <input type="hidden" name="vista" value="grupos">
      <input type="date" id="desde" name="desde" data-dmo-autosubmit>
    </form><div id="lista" data-dmo-regiao></div></div></main></body>`,
  { url: 'http://dmo.test/Boquilhas?vista=grupos', runScripts: 'outside-only', virtualConsole: new VirtualConsole() });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url) => {
    pedidos.push(new URL(String(url), window.location.href).searchParams.get('desde'));
    return new Promise(() => {});
  };
  try {
    window.Element.prototype.scrollIntoView = function () {};
    window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
    const data = window.document.getElementById('desde');
    data.focus();
    tecla(window, data, '2', '0002-10-18');
    tecla(window, data, '0', '0020-10-18');
    tecla(window, data, '2', '0202-10-18');
    tecla(window, data, '6', '2026-10-18');
    await esperar();
    assert.deepEqual(pedidos, [], 'o ano escrito a meio não aplica');
    data.blur();
    await esperar();
    assert.deepEqual(pedidos, ['2026-10-18']);

    data.dispatchEvent(new window.Event('pointerdown', { bubbles: true }));
    data.value = '2026-10-01';
    data.dispatchEvent(new window.Event('change', { bubbles: true }));
    await esperar();
    assert.deepEqual(pedidos, ['2026-10-18', '2026-10-01'], 'o dia escolhido no calendário aplica logo');
  } finally {
    window.close();
  }
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// CLIQUES REPETIDOS NÃO MOSTRAM MENSAGENS (Diogo, 18/10/2026; deep sweep):
// o servidor não cria nada repetido e a resposta a um clique repetido mostra
// o mesmo que a primeira. Os formulários que eram POST nativos gravam pela
// fila da página (data-dmo-parcial): um envio igual a outro que ainda não
// respondeu não sai. As páginas em miniatura seguem a marcação das reais
// (os testes da Web confirmam a marcação de cada uma).

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

// respostas(url, opcoes) devolve (ou promete) { url, body, status? }.
async function abrir(html, endereco, respostas, scripts = []) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${html}</body>`,
    { url: endereco, runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url, opcoes = {}) => {
    const metodo = opcoes.method || 'GET';
    pedidos.push({ url: String(url), metodo, corpo: String(opcoes.body || '') });
    const r = await respostas(String(url), opcoes);
    const final = new URL(r.url || String(url), window.location.href).href;
    return {
      ok: (r.status || 200) < 400,
      url: final,
      redirected: final !== new URL(String(url), window.location.href).href,
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? r.tipo || 'text/html; charset=utf-8' : null) },
      text: async () => r.body,
      json: async () => JSON.parse(r.body),
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  window.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  scripts.forEach((script) => window.eval(fs.readFileSync(path.join(assets, script), 'utf8')));
  const posts = () => pedidos.filter((p) => p.metodo === 'POST');
  return { dom, window, document: window.document, pedidos, posts, navegou };
}

// Uma resposta que só chega quando o teste a solta (a rede lenta).
function lenta() {
  let soltar;
  const pronta = new Promise((resolve) => { soltar = resolve; });
  return { pronta, soltar };
}

// ------------------------------------------------------------------------
// Ferramentas → Registar (B-ferramentas-2): o botão no canto do card, fora do
// formulário (form="ferr-create-form"); as mensagens numa zona.
// ------------------------------------------------------------------------
function paginaRegistar(avisos = '', referencia = '') {
  return `<main data-dmo-troca class="ferr-page dmo-workspace">
    <div class="page-head"><h2 id="ferr-title">Ferramentas</h2></div>
    <div id="ferrAvisos" class="dmo-zona" data-dmo-regiao>${avisos}</div>
    <section class="dmo-card ferr-card">
      <div class="dmo-card-topo"><h3>Registar ferramenta real</h3>
        <div class="dmo-card-topo-acoes"><button type="submit" form="ferr-create-form" id="registar" class="dmo-button">Registar ferramenta</button></div>
      </div>
      <form id="ferr-create-form" method="post" action="/Ferramentas?handler=Create" class="ferr-create-form" data-dmo-parcial>
        <input type="hidden" name="__RequestVerificationToken" value="token-1">
        <input id="ferr-new-reference" name="reference" value="${referencia}">
        <input id="ferr-new-lot" name="lot" value="1">
        <input id="ferr-new-qty" name="bq_quantity" value="">
      </form>
    </section>
  </main>`;
}

test('Registar ferramenta: três cliques com a rede lenta fazem um POST e a ficha abre uma vez', async () => {
  const rede = lenta();
  const { dom, document, posts, navegou } = await abrir(paginaRegistar(), 'http://dmo.test/Ferramentas?criar=1',
    async (url, opcoes) => {
      if (opcoes.method !== 'POST') return { body: '' };
      await rede.pronta;
      return { url: 'http://dmo.test/Ferramentas/Ficha?tool_id=T1&created=1', body: '<!doctype html><body><main data-dmo-troca><h2>Ficha</h2></main></body>' };
    });
  try {
    document.getElementById('ferr-new-reference').value = '9931';
    const registar = document.getElementById('registar');
    registar.click();
    await esperar(5);
    registar.click();
    registar.click();
    await esperar();
    assert.equal(posts().length, 1, 'os cliques iguais enquanto o primeiro não respondeu não saem');
    assert.equal(new URLSearchParams(posts()[0].corpo).get('reference'), '9931');
    rede.soltar();
    await esperar();
    assert.equal(navegou.length, 1, 'a ficha (outra página) abre-se inteira, uma vez');
    registar.click();
    await esperar();
    assert.equal(posts().length, 1, 'com a ficha a abrir, um clique mais não grava outra vez');
  } finally {
    dom.window.close();
  }
});

test('Registar ferramenta: a mesma recusa duas vezes seguidas mostra-se outra vez', async () => {
  const recusa = '<div data-dmo-toast="erro" class="ferr-aviso" role="alert">Use uma quantidade inteira igual ou superior a 0. O valor introduzido foi mantido.</div>';
  const endereco = 'http://dmo.test/Ferramentas?reference=9931&lot=1&bq_quantity=-1&create_refusal=InvalidLotQuantity';
  const { dom, document, posts, navegou } = await abrir(paginaRegistar(recusa, '9931'), endereco,
    () => ({ url: endereco, body: `<!doctype html><body>${paginaRegistar(recusa, '9931')}</body>` }));
  try {
    const pilha = () => Array.from(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast="erro"]'));
    assert.equal(pilha().length, 1, 'a recusa da página está no canto');
    const antes = pilha()[0];
    document.getElementById('registar').click();
    await esperar();
    assert.equal(posts().length, 1);
    assert.deepEqual(navegou, [], 'a mesma página: sem recarregar');
    assert.equal(pilha().length, 1, 'uma recusa só, a desta resposta');
    assert.notEqual(pilha()[0], antes, 'a mensagem é a resposta a este envio');
    assert.match(pilha()[0].textContent, /Use uma quantidade inteira/);
  } finally {
    dom.window.close();
  }
});

// ------------------------------------------------------------------------
// Ferramentas → Ficha → Duplicar para lote novo (B-ferramentas-3): a janela
// numa zona própria (#ferrDuplicar); a pergunta de eliminar, fora das zonas,
// tem o nome da ferramenta (outra ferramenta abre a página inteira). As duas
// janelas levam data-dmo-dialog-aberta (vazio quando não abrem com a página),
// como o Razor as desenha.
// ------------------------------------------------------------------------
function paginaFicha(toolId, nome, avisos = '') {
  return `<main data-dmo-troca class="ferr-page dmo-workspace">
    <div class="page-head"><h2 id="ferr-title">Ficha de ferramenta</h2></div>
    <div id="ferrDuplicar" class="dmo-zona" data-dmo-regiao>
      <dialog id="ferr-duplicate-dialog" class="dmo-dialog" data-dmo-dialog-aberta="">
        <form method="post" action="/Ferramentas/Ficha?handler=Duplicate" data-dmo-parcial>
          <input type="hidden" name="tool_id" value="${toolId}">
          <input id="ferr-new-lot" name="new_lot" required>
          <button type="submit" id="duplicar" class="dmo-button">Duplicar</button>
        </form>
      </dialog>
    </div>
    <dialog id="ferr-delete-dialog" class="dmo-dialog" data-dmo-dialog-aberta="">
      <p class="dmo-aviso-discreto">Quer mesmo eliminar ${nome}?</p>
      <form method="post" action="/Ferramentas/Ficha?handler=Delete" data-dmo-parcial>
        <input type="hidden" name="tool_id" value="${toolId}">
        <button type="submit" id="eliminar" class="dmo-button ferr-danger">Eliminar</button>
      </form>
    </dialog>
    <div id="ferrAvisos" class="dmo-zona" data-dmo-regiao>${avisos}</div>
    <section id="ferrGeral" class="dmo-card ferr-card" data-dmo-regiao>
      <h3>Geral — ${nome}</h3>
      <button type="button" id="abrir-duplicar" data-dmo-dialog="ferr-duplicate-dialog">Duplicar para lote novo</button>
      <button type="button" id="abrir-eliminar" data-dmo-dialog="ferr-delete-dialog">Eliminar ferramenta</button>
    </section>
  </main>`;
}

test('Duplicar: três cliques com a rede lenta fazem um POST e a ficha nova abre uma vez', async () => {
  const rede = lenta();
  const { dom, document, posts, navegou } = await abrir(paginaFicha('T1', 'CM · 5204C090 · lote 1'), 'http://dmo.test/Ferramentas/Ficha?tool_id=T1',
    async (url, opcoes) => {
      if (opcoes.method !== 'POST') return { body: '' };
      await rede.pronta;
      return {
        url: 'http://dmo.test/Ferramentas/Ficha?tool_id=T7&created=1',
        body: `<!doctype html><body>${paginaFicha('T7', 'CM · 5204C090 · lote 7', '<div data-dmo-toast="ok" class="ferr-nota" role="status">Ferramenta registada.</div>')}</body>`,
      };
    });
  try {
    document.getElementById('abrir-duplicar').click();
    document.getElementById('ferr-new-lot').value = '7';
    const duplicar = document.getElementById('duplicar');
    duplicar.click();
    await esperar(5);
    duplicar.click();
    duplicar.click();
    await esperar();
    assert.equal(posts().length, 1, 'os cliques iguais enquanto o primeiro não respondeu não saem');
    assert.equal(new URLSearchParams(posts()[0].corpo).get('new_lot'), '7');
    rede.soltar();
    await esperar();
    assert.equal(navegou.length, 1, 'a ficha de outra ferramenta abre-se inteira, uma vez');
  } finally {
    dom.window.close();
  }
});

test('Duplicar para o lote desta ferramenta: a resposta abre a página inteira, sem a janela aberta por cima', async () => {
  // A resposta fica nesta ficha, com o aviso do lote que já estava
  // registado. Sem a zona da janela, só os avisos trocavam e a janela ficava
  // aberta; com ela, a zona é uma janela que abre com a página e o
  // componente abre a página inteira (a janela fecha, como antes).
  const aviso = '<div data-dmo-toast="aviso" class="ferr-aviso warn" role="status">O lote «1» já estava registado para esta referência: abriu-se a ferramenta existente. Nada foi duplicado nem alterado.</div>';
  const { dom, document, posts, navegou } = await abrir(paginaFicha('T1', 'CM · 5204C090 · lote 1'), 'http://dmo.test/Ferramentas/Ficha?tool_id=T1',
    () => ({ url: 'http://dmo.test/Ferramentas/Ficha?tool_id=T1&lote_existente=1', body: `<!doctype html><body>${paginaFicha('T1', 'CM · 5204C090 · lote 1', aviso)}</body>` }));
  try {
    document.getElementById('abrir-duplicar').click();
    assert.ok(document.getElementById('ferr-duplicate-dialog').hasAttribute('open'));
    document.getElementById('ferr-new-lot').value = '1';
    const duplicar = document.getElementById('duplicar');
    duplicar.click();
    duplicar.click();
    await esperar();
    assert.equal(posts().length, 1);
    assert.equal(navegou.length, 1, 'a página abre-se inteira, uma vez');
    assert.equal(document.querySelector('.dmo-toast-pilha [data-dmo-toast="aviso"]'), null, 'nada trocou por baixo da janela');
  } finally {
    dom.window.close();
  }
});

// Ferramentas → Ficha → Eliminar (B-ferramentas-4): a janela fora das zonas.
test('Eliminar: três cliques com a rede lenta fazem um POST e a lista abre uma vez', async () => {
  const rede = lenta();
  const { dom, document, posts, navegou } = await abrir(paginaFicha('T1', 'MF · 9901 · lote 1'), 'http://dmo.test/Ferramentas/Ficha?tool_id=T1',
    async (url, opcoes) => {
      if (opcoes.method !== 'POST') return { body: '' };
      await rede.pronta;
      return { url: 'http://dmo.test/Ferramentas?deleted=1', body: '<!doctype html><body><main data-dmo-troca><h2>Ferramentas</h2></main></body>' };
    });
  try {
    document.getElementById('abrir-eliminar').click();
    const eliminar = document.getElementById('eliminar');
    eliminar.click();
    await esperar(5);
    eliminar.click();
    eliminar.click();
    await esperar();
    assert.equal(posts().length, 1, 'os cliques iguais enquanto o primeiro não respondeu não saem');
    assert.equal(new URLSearchParams(posts()[0].corpo).get('tool_id'), 'T1');
    rede.soltar();
    await esperar();
    assert.equal(navegou.length, 1, 'a lista (outra página) abre-se inteira, uma vez');
    eliminar.click();
    await esperar();
    assert.equal(posts().length, 1, 'com a lista a abrir, um clique mais não pede outra eliminação');
  } finally {
    dom.window.close();
  }
});

// ------------------------------------------------------------------------
// Admin → Utilizadores → ficha (B-admin-3): as ações ficam (data-dmo-fica) e
// gravam pela fila; «Eliminar mesmo» volta à lista, que abre inteira. A
// confirmação é a janela da casa (regra 118 do deep sweep de 18/10/2026).
// ------------------------------------------------------------------------
function paginaFichaUtilizador(confirmar = false) {
  return `<main class="dfu-page" data-dmo-troca>
    <div id="dfuAvisos" class="dmo-zona" data-dmo-regiao></div>
    <div class="dmo-zona" data-dmo-fica data-dmo-parcial>
      <section class="dmo-card dfu-card">
        <div class="dfu-actions">
          <details class="dmo-options" data-dmo-tab-menu open>
            <summary class="dmo-button">Opções</summary>
            <div class="dmo-options-list">
              <form method="post" action="/Admin/Users?handler=Reset">
                <input type="hidden" name="__RequestVerificationToken" value="token-1">
                <input type="hidden" name="operator_id" value="4848">
                <input type="hidden" name="origem" value="ficha">
                <button type="submit" id="dfu-repor">Repor palavra-passe</button>
              </form>
              <button type="button" class="dmo-options-item" id="dfu-eliminar" data-dmo-dialog="dfu-delete-dialog">Eliminar utilizador</button>
            </div>
          </details>
          <dialog id="dfu-delete-dialog" class="dmo-dialog" aria-labelledby="dfu-delete-title"${confirmar ? ' data-dmo-dialog-aberta="1"' : ''}>
            <h3 id="dfu-delete-title">Eliminar o utilizador Utilizador Da Troca (nº 4848)?</h3>
            <form method="post" action="/Admin/Users?handler=Delete">
              <input type="hidden" name="__RequestVerificationToken" value="token-1">
              <input type="hidden" name="operator_id" value="4848">
              <input type="hidden" name="confirm_delete" value="1">
              <div class="dmo-dialog-actions">
                <button type="button" class="dmo-button dmo-dialog-cancel" data-dmo-dialog-close>Cancelar</button>
                <button type="submit" id="eliminar-mesmo" class="dmo-button dfu-danger">Eliminar mesmo</button>
              </div>
            </form>
          </dialog>
        </div>
      </section>
    </div>
  </main>`;
}

test('Ficha do utilizador: três cliques em «Repor palavra-passe» com a rede lenta repõem uma vez', async () => {
  const rede = lenta();
  const { dom, document, posts } = await abrir(paginaFichaUtilizador(), 'http://dmo.test/Admin/Users/Ficha?operator_id=4848',
    async (url, opcoes) => {
      if (opcoes.method !== 'POST') return { body: '' };
      await rede.pronta;
      return { url: 'http://dmo.test/Admin/Users/Ficha?operator_id=4848', body: `<!doctype html><body>${paginaFichaUtilizador()}</body>` };
    });
  try {
    const repor = document.getElementById('dfu-repor');
    repor.click();
    await esperar(5);
    repor.click();
    repor.click();
    await esperar();
    assert.equal(posts().length, 1, 'os cliques iguais enquanto o primeiro não respondeu não saem: um só PIN novo');
    rede.soltar();
    await esperar();
  } finally {
    dom.window.close();
  }
});

test('Ficha do utilizador: «Eliminar utilizador» abre a janela sem pedido; três cliques em «Eliminar mesmo» com a rede lenta eliminam uma vez e a lista abre uma vez', async () => {
  const rede = lenta();
  const { dom, document, pedidos, posts, navegou } = await abrir(paginaFichaUtilizador(), 'http://dmo.test/Admin/Users/Ficha?operator_id=4848',
    async (url, opcoes) => {
      if (opcoes.method !== 'POST') return { body: '' };
      await rede.pronta;
      return { url: 'http://dmo.test/Admin/Users?deleted=4848', body: '<!doctype html><body><main data-dmo-troca><h2>Utilizadores</h2></main></body>' };
    });
  try {
    const janela = document.getElementById('dfu-delete-dialog');
    assert.equal(janela.hasAttribute('open'), false);
    document.getElementById('dfu-eliminar').click();
    await esperar();
    assert.equal(janela.hasAttribute('open'), true, 'o primeiro passo abre a janela da casa');
    assert.equal(document.querySelector('details.dmo-options').hasAttribute('open'), false, 'as «Opções» fecham por trás da janela');
    assert.equal(pedidos.length, 0, 'o primeiro passo não recarrega a página nem pede nada');
    assert.equal(navegou.length, 0);

    // «Cancelar» fecha sem pedir nada; abre-se outra vez para eliminar.
    janela.querySelector('[data-dmo-dialog-close]').click();
    assert.equal(janela.hasAttribute('open'), false);
    document.getElementById('dfu-eliminar').click();

    const eliminar = document.getElementById('eliminar-mesmo');
    eliminar.click();
    await esperar(5);
    eliminar.click();
    eliminar.click();
    await esperar();
    assert.equal(posts().length, 1, 'o segundo e o terceiro cliques não pedem outra eliminação (que diria «já não existe»)');
    assert.equal(new URLSearchParams(posts()[0].corpo).get('confirm_delete'), '1');
    rede.soltar();
    await esperar();
    assert.equal(navegou.length, 1, 'a lista (outra página) abre-se inteira, uma vez');
  } finally {
    dom.window.close();
  }
});

test('Ficha do utilizador: com ?confirm_delete=1 no endereço a janela abre com a página', async () => {
  const { dom, document } = await abrir(paginaFichaUtilizador(true), 'http://dmo.test/Admin/Users/Ficha?operator_id=4848&confirm_delete=1',
    () => ({ body: '' }));
  try {
    assert.equal(document.getElementById('dfu-delete-dialog').hasAttribute('open'), true);
  } finally {
    dom.window.close();
  }
});

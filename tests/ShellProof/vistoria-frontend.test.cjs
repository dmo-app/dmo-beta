// Vistoria ao frontend (27/10/2026, /mnt/project-files/todo/vistoria-frontend.md):
// F2 — o filtro que procura ao escrever diz quando a sessão terminou ou o
// servidor falhou, em vez de deixar os resultados parados sem aviso;
// F3 — o rascunho da folha do Job On é de quem o escreveu e dura um dia.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');
const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

// F2 ------------------------------------------------------------------------
// A procura das Ferramentas em miniatura: o filtro troca só #resultados.
function paginaFiltro(lista = 'nada') {
  return `<main data-dmo-troca>
    <form id="filtro" method="get" action="/Ferramentas" data-dmo-ao-escrever="resultados">
      <input id="referencia" name="q" value="">
    </form>
    <section id="resultados" data-dmo-regiao><p id="lista">${lista}</p></section>
  </main>`;
}

function abrirFiltro(responder) {
  const dom = new JSDOM(`<!doctype html><title>Ferramentas</title><body>${paginaFiltro()}</body>`,
    { url: 'http://dmo.test/Ferramentas', runScripts: 'outside-only' });
  const { window } = dom;
  window.fetch = async (url) => responder(new URL(String(url), window.location.href));
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  return { dom, window, document: window.document };
}

function escreverNoFiltro(window, document, texto) {
  const campo = document.getElementById('referencia');
  campo.value = texto;
  campo.dispatchEvent(new window.Event('input', { bubbles: true }));
}

const aviso = (document) => document.querySelector('[data-dmo-ligacao]');

test('F2: o filtro ao escrever troca os resultados quando a resposta traz a lista', async () => {
  const { dom, window, document } = abrirFiltro((url) => ({ ok: true, status: 200, url: url.href,
    text: async () => `<!doctype html><title>Ferramentas</title><body>${paginaFiltro('5447T173')}</body>` }));
  try {
    escreverNoFiltro(window, document, '5447');
    await esperar(450);
    assert.equal(document.getElementById('lista').textContent, '5447T173');
    assert.equal(aviso(document), null, 'sem aviso quando correu bem');
  } finally {
    dom.window.close();
  }
});

test('F2: com a sessão terminada, o filtro avisa e dá o link «Entrar»', async () => {
  const { dom, window, document } = abrirFiltro(() => ({ ok: true, status: 200, url: 'http://dmo.test/login?ReturnUrl=%2FFerramentas',
    text: async () => '<!doctype html><title>Entrar</title><body><form id="login"></form></body>' }));
  try {
    escreverNoFiltro(window, document, '5447');
    await esperar(450);
    assert.ok(aviso(document), 'aparece um aviso');
    assert.match(aviso(document).textContent, /A sessão terminou/);
    assert.equal(aviso(document).querySelector('a').getAttribute('href'), '/login?ReturnUrl=' + encodeURIComponent('/Ferramentas'));
    assert.equal(document.getElementById('lista').textContent, 'nada', 'os resultados não mudam');
  } finally {
    dom.window.close();
  }
});

test('F2: com um erro do servidor, o filtro avisa que não foi possível procurar', async () => {
  const { dom, window, document } = abrirFiltro((url) => ({ ok: false, status: 500, url: url.href, text: async () => 'erro' }));
  try {
    escreverNoFiltro(window, document, '5447');
    await esperar(450);
    assert.ok(aviso(document), 'aparece um aviso');
    assert.match(aviso(document).textContent, /Não foi possível procurar/);
  } finally {
    dom.window.close();
  }
});

test('F2: sem rede, o filtro repete a mesma procura quando a ligação volta', async () => {
  const pedidos = [];
  let rede = false;
  const { dom, window, document } = abrirFiltro(async (url) => {
    pedidos.push(url.search);
    if (!rede) throw new TypeError('Failed to fetch');
    return { ok: true, status: 200, url: url.href,
      text: async () => `<!doctype html><title>Ferramentas</title><body>${paginaFiltro('5447T173')}</body>` };
  });
  try {
    escreverNoFiltro(window, document, '5447');
    await esperar(450);
    assert.equal(pedidos.length, 1);
    assert.equal(aviso(document), null, 'sem rede não se mostra «não foi possível»: espera pela ligação');
    rede = true;
    window.dispatchEvent(new window.Event('online'));
    await esperar(50);
    assert.deepEqual(pedidos, ['?q=5447', '?q=5447']);
    assert.equal(document.getElementById('lista').textContent, '5447T173');
  } finally {
    dom.window.close();
  }
});

// F3 ------------------------------------------------------------------------
const rascunhoScript = fs.readFileSync(path.join(assets, 'js', 'jobon-rascunho.js'), 'utf8');

function folha(dono) {
  return `<form id="lightForm" data-jobon-rascunho="novo"${dono ? ` data-jobon-rascunho-dono="${dono}"` : ''} method="post" action="/JobOn?handler=Create">
    <input id="lightReference" name="reference" value="">
  </form>`;
}

async function abrirFolha(dono, guardados) {
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${folha(dono)}</body>`,
    { url: 'http://dmo.test/JobOn?novo=1', runScripts: 'outside-only' });
  const { window } = dom;
  for (const [chave, valor] of Object.entries(guardados)) window.localStorage.setItem(chave, JSON.stringify(valor));
  window.eval(rascunhoScript);
  await esperar(10);
  return { dom, window, document: window.document };
}

const campos = (texto) => [{ n: 'reference', v: texto }];
const agora = () => new Date().toISOString();

test('F3: o rascunho de quem abre a folha volta', async () => {
  const { dom, document } = await abrirFolha('7', { 'dmo-jobon-rascunho:novo': { guardado: agora(), dono: '7', campos: campos('5447T173') } });
  try {
    assert.equal(document.getElementById('lightReference').value, '5447T173');
    assert.ok(document.getElementById('jobonRascunhoNota'));
  } finally {
    dom.window.close();
  }
});

test('F3: o rascunho de outra pessoa não aparece e sai do browser', async () => {
  const { dom, window, document } = await abrirFolha('8', {
    'dmo-jobon-rascunho:novo': { guardado: agora(), dono: '7', campos: campos('5447T173') },
    'dmo-jobon-rascunho:editar:77': { guardado: agora(), dono: '7', campos: campos('9001') },
  });
  try {
    assert.equal(document.getElementById('lightReference').value, '');
    assert.equal(document.getElementById('jobonRascunhoNota'), null);
    assert.equal(window.localStorage.getItem('dmo-jobon-rascunho:novo'), null);
    assert.equal(window.localStorage.getItem('dmo-jobon-rascunho:editar:77'), null, 'também os das outras folhas');
  } finally {
    dom.window.close();
  }
});

test('F3: um rascunho com mais de um dia não volta; um de 23 h volta', async () => {
  const velho = new Date(Date.now() - 25 * 60 * 60 * 1000).toISOString();
  const recente = new Date(Date.now() - 23 * 60 * 60 * 1000).toISOString();
  let { dom, document } = await abrirFolha('7', { 'dmo-jobon-rascunho:novo': { guardado: velho, dono: '7', campos: campos('5447T173') } });
  try {
    assert.equal(document.getElementById('lightReference').value, '');
  } finally {
    dom.window.close();
  }
  ({ dom, document } = await abrirFolha('7', { 'dmo-jobon-rascunho:novo': { guardado: recente, dono: '7', campos: campos('5447T173') } }));
  try {
    assert.equal(document.getElementById('lightReference').value, '5447T173');
  } finally {
    dom.window.close();
  }
});

test('F3: o que se escreve fica guardado com o dono', async () => {
  const { dom, window, document } = await abrirFolha('7', {});
  try {
    const campo = document.getElementById('lightReference');
    campo.value = '5447T173';
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
    const guardado = JSON.parse(window.localStorage.getItem('dmo-jobon-rascunho:novo'));
    assert.equal(guardado.dono, '7');
    assert.deepEqual(guardado.campos, campos('5447T173'));
  } finally {
    dom.window.close();
  }
});

// Sweep do Codex, 19/10/2026: FE-1 (gravar quando o servidor recusa, dá um
// erro ou a sessão terminou) e FE-3 (a procura com a sessão terminada).
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

const html = (corpo) => `<!doctype html><title>DMO</title><body>${corpo}</body>`;
const resposta = ({ ok, status, redirected = false, url, tipo = null, corpo = '' }) => ({
  ok, status, redirected, url,
  headers: { get: (nome) => (nome === 'Content-Type' ? tipo : null) },
  text: async () => corpo,
});
const pilha = (document) => Array.from(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]'));
const textoDe = (mensagem) => mensagem.textContent.replace('×', '').trim();

// O Peso em miniatura: o formulário de uma leitura, numa zona.
const PESO = `<main data-dmo-troca>
  <section id="zona" data-dmo-regiao data-dmo-parcial>
    <form method="post" action="/Controlo/Peso?handler=RegisterMeasurement">
      <input type="hidden" name="__RequestVerificationToken" value="t">
      <input id="massa" name="massa">
      <button id="gravar">Gravar</button>
    </form>
  </section>
</main>`;
const ENDERECO = 'http://dmo.test/Controlo/Peso?controlo_id=7';

// Abre o Peso; cada POST recebe a resposta seguinte de «respostas», e um GET
// recebe a página como está.
function peso(respostas) {
  const posts = [];
  const gets = [];
  const aberto = abrir(PESO, ENDERECO, async (_, url, opcoes) => {
    if ((opcoes.method || 'GET').toUpperCase() === 'POST') {
      posts.push({ url, envio: opcoes.headers['X-Dmo-Envio'] });
      return respostas.shift();
    }
    gets.push(url);
    return resposta({ ok: true, status: 200, url, tipo: 'text/html; charset=utf-8', corpo: html(PESO) });
  });
  const fins = [];
  aberto.document.addEventListener('dmo:fim', (event) => fins.push(event.detail));
  return { ...aberto, posts, gets, fins };
}

function gravar(document, valor) {
  document.getElementById('massa').value = valor;
  document.querySelector('#zona form').requestSubmit(document.getElementById('gravar'));
}

for (const status of [400, 403]) {
  test(`uma recusa do servidor deixa o escrito e diz para recarregar (${status} sem corpo)`, async () => {
    const { dom, document, navegou, posts, gets, fins } = peso([
      resposta({ ok: false, status, url: 'http://dmo.test/Controlo/Peso?handler=RegisterMeasurement' }),
    ]);
    try {
      const massa = document.getElementById('massa');
      gravar(document, '812,4');
      await esperar();
      assert.deepEqual(navegou, [], 'a página não abre outra vez');
      assert.equal(document.getElementById('massa'), massa, 'o mesmo campo');
      assert.equal(massa.value, '812,4', 'o que se escreveu fica');
      assert.equal(posts.length, 1);
      assert.equal(gets.length, 0, 'nada se pede outra vez');
      const mensagens = pilha(document);
      assert.equal(mensagens.length, 1);
      assert.match(textoDe(mensagens[0]), /^O servidor recusou o envio\. O que escreveu continua no ecrã: recarregue a página e grave outra vez\./);
      const link = mensagens[0].querySelector('a.dmo-aviso-link');
      assert.equal(link.textContent, 'Recarregar a página');
      assert.equal(link.getAttribute('href'), ENDERECO);
      assert.equal(link.hasAttribute('target'), false, 'no mesmo separador');
      const form = document.querySelector('#zona form');
      assert.equal(form.hasAttribute('aria-busy'), false);
      assert.equal(form.hasAttribute('data-dmo-a-gravar'), false);
      assert.equal(fins.at(-1).semResposta, false);
    } finally {
      dom.window.close();
    }
  });
}

for (const [status, pagina] of [[500, '<h1>Erro</h1><p>Ocorreu um erro ao processar o pedido.</p>'], [502, '<h1>502 Bad Gateway</h1>']]) {
  test(`um erro do servidor não substitui o ecrã e mostra o que ficou gravado (${status})`, async () => {
    const { dom, document, navegou, posts, gets } = peso([
      resposta({ ok: false, status, url: 'http://dmo.test/Controlo/Peso?handler=RegisterMeasurement', tipo: 'text/html; charset=utf-8', corpo: html(pagina) }),
      resposta({ ok: true, status: 200, redirected: true, url: ENDERECO, tipo: 'text/html; charset=utf-8', corpo: html(PESO) }),
    ]);
    try {
      const raiz = document.querySelector('main[data-dmo-troca]');
      gravar(document, '812,4');
      await esperar();
      assert.deepEqual(navegou, []);
      assert.equal(document.querySelector('main[data-dmo-troca]'), raiz, 'a página continua');
      assert.doesNotMatch(document.body.textContent, /Erro|Bad Gateway/, 'a página de erro não aparece');
      assert.equal(posts.length, 1);
      assert.deepEqual(gets, [ENDERECO], 'confirma com um GET ao endereço limpo');
      const mensagens = pilha(document);
      assert.equal(mensagens.length, 1);
      assert.equal(textoDe(mensagens[0]), 'O servidor deu um erro. A página mostra agora o que ficou gravado: confirme antes de repetir.');

      // O mesmo envio outra vez leva a mesma chave: o servidor reconhece-o.
      gravar(document, '812,4');
      await esperar();
      assert.equal(posts.length, 2);
      assert.equal(posts[1].envio, posts[0].envio);
    } finally {
      dom.window.close();
    }
  });
}

test('a sessão terminada ao gravar deixa o escrito e dá o link para entrar noutro separador', async () => {
  const { dom, document, navegou, posts, gets } = peso([
    resposta({
      ok: true, status: 200, redirected: true,
      url: 'http://dmo.test/login?ReturnUrl=%2FControlo%2FPeso%3Fhandler%3DRegisterMeasurement',
      tipo: 'text/html; charset=utf-8', corpo: html('<form method="post"><input name="email"></form>'),
    }),
  ]);
  try {
    gravar(document, '812,4');
    await esperar();
    assert.deepEqual(navegou, [], 'o login não se abre por cima');
    assert.equal(document.getElementById('massa').value, '812,4');
    assert.equal(posts.length, 1);
    assert.equal(gets.length, 0);
    const mensagens = pilha(document);
    assert.equal(mensagens.length, 1);
    assert.match(textoDe(mensagens[0]), /^A sessão terminou e nada foi gravado\./);
    const link = mensagens[0].querySelector('a.dmo-aviso-link[target=_blank]');
    assert.equal(link.textContent, 'Entrar');
    assert.equal(link.getAttribute('href'), '/login?ReturnUrl=%2FControlo%2FPeso%3Fcontrolo_id%3D7');
    assert.equal(link.getAttribute('rel'), 'noopener');
  } finally {
    dom.window.close();
  }
});

test('duas recusas seguidas deixam uma só mensagem no canto', async () => {
  const recusa = () => resposta({ ok: false, status: 400, url: 'http://dmo.test/Controlo/Peso?handler=RegisterMeasurement' });
  const { dom, document, posts } = peso([recusa(), recusa()]);
  try {
    gravar(document, '812,4');
    await esperar();
    gravar(document, '812,5');
    await esperar();
    assert.equal(posts.length, 2);
    assert.equal(pilha(document).filter((mensagem) => !mensagem.classList.contains('a-sair')).length, 1);
  } finally {
    dom.window.close();
  }
});

// O Job On em miniatura: a referência e o dropdown «Produção».
const VAZIO = '<select id="lightProcuraProducoes" disabled><option value="">Produção</option></select>';
const producoes = (ref, id) => `<select id="lightProcuraProducoes" data-dmo-ir><option value="">Produção</option><option value="/JobOn?jobon_id=${id}">${ref} 2026${id} B2</option></select>`;
const JOBON = `<div data-dmo-troca>
  <div id="jobonFolhas" data-dmo-regiao></div>
  <div class="procura" data-dmo-fica data-dmo-parcial>
    <form method="get"><input id="campo" data-dmo-procura="/JobOn?handler=ProcurarProducao" data-dmo-procura-alvo="alvo" data-dmo-procura-sempre></form>
    <span id="alvo">${VAZIO}</span>
  </div>
</div>`;

function escrever(window, texto) {
  const campo = window.document.getElementById('campo');
  campo.value = texto;
  campo.dispatchEvent(new window.Event('input', { bubbles: true }));
}

// Cada procura recebe o que «responder» devolver para a referência.
function procura(responder) {
  const pedidos = [];
  const aberto = abrir(JOBON, 'http://dmo.test/JobOn?ano=2026&mes=10&dia=19', async (_, url) => {
    pedidos.push(url);
    if (url.includes('jobon_id=')) return resposta({ ok: true, status: 200, url, tipo: 'text/html', corpo: html(JOBON) });
    return responder(url, decodeURIComponent(url.split('q=')[1] || ''));
  });
  return { ...aberto, pedidos };
}

test('com a sessão terminada, a procura diz para entrar e a lista anterior não abre', async () => {
  let sessao = true;
  const { dom, window, document, navegou, pedidos } = procura((url, ref) => {
    if (ref === '9322Y152' && sessao) {
      return resposta({ ok: true, status: 200, redirected: true, url: 'http://dmo.test/login?ReturnUrl=%2FJobOn%3Fhandler%3DProcurarProducao', tipo: 'text/html', corpo: html('<form></form>') });
    }
    return resposta({ ok: true, status: 200, url, tipo: 'text/html', corpo: producoes(ref, ref === '7079C337' ? 993 : 966) });
  });
  try {
    escrever(window, '7079C337');
    await esperar(300);
    escrever(window, '9322Y152');
    await esperar(300);
    const alvo = document.getElementById('alvo');
    const mensagens = pilha(document);
    assert.equal(mensagens.length, 1);
    assert.match(textoDe(mensagens[0]), /^A sessão terminou\. Entre outra vez\./);
    const link = mensagens[0].querySelector('a.dmo-aviso-link');
    assert.equal(link.textContent, 'Entrar');
    assert.equal(link.getAttribute('href'), '/login?ReturnUrl=%2FJobOn%3Fano%3D2026%26mes%3D10%26dia%3D19');
    assert.equal(link.hasAttribute('target'), false, 'no mesmo separador');
    assert.equal(alvo.hasAttribute('data-dmo-procura-espera'), true, 'a lista anterior continua bloqueada');
    assert.equal(alvo.hasAttribute('aria-busy'), false, 'já não há pedido');

    // A produção da referência anterior não abre.
    const antigo = document.getElementById('lightProcuraProducoes');
    assert.equal(antigo.disabled, true);
    antigo.value = '/JobOn?jobon_id=993';
    antigo.dispatchEvent(new window.Event('change', { bubbles: true }));
    await esperar(300);
    assert.equal(pedidos.some((url) => url.includes('jobon_id=993')), false);

    // Esperar pela rede não resolve: o online não pede outra vez.
    const antes = pedidos.length;
    window.dispatchEvent(new window.Event('online'));
    await esperar();
    assert.equal(pedidos.length, antes);

    // Escrever de novo procura outra vez e a lista nova escolhe-se.
    sessao = false;
    escrever(window, '9322Y152');
    await esperar(300);
    assert.equal(pedidos.length, antes + 1);
    assert.equal(alvo.hasAttribute('data-dmo-procura-espera'), false);
    assert.equal(document.getElementById('lightProcuraProducoes').disabled, false);
    assert.match(document.getElementById('lightProcuraProducoes').textContent, /9322Y152/);
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('um erro do servidor na procura diz para escrever de novo', async () => {
  let erro = true;
  const { dom, window, document, navegou, pedidos } = procura((url, ref) => (erro
    ? resposta({ ok: false, status: 500, url, tipo: 'text/html', corpo: html('<h1>Erro</h1>') })
    : resposta({ ok: true, status: 200, url, tipo: 'text/html', corpo: producoes(ref, 993) })));
  try {
    escrever(window, '7079C337');
    await esperar(300);
    escrever(window, '9322Y152');
    await esperar(300);
    const alvo = document.getElementById('alvo');
    const mensagens = pilha(document).filter((mensagem) => !mensagem.classList.contains('a-sair'));
    assert.equal(mensagens.length, 1, 'duas falhas seguidas deixam uma só mensagem');
    assert.equal(textoDe(mensagens[0]), 'Não foi possível procurar. Escreva de novo.');
    assert.equal(mensagens[0].querySelector('a'), null, 'resolve-se no campo, sem link');
    assert.equal(alvo.hasAttribute('data-dmo-procura-espera'), true);
    assert.equal(alvo.hasAttribute('aria-busy'), false);

    const antes = pedidos.length;
    window.dispatchEvent(new window.Event('online'));
    await esperar();
    assert.equal(pedidos.length, antes, 'o online não pede outra vez');

    erro = false;
    escrever(window, '7079C337');
    await esperar(300);
    assert.equal(alvo.hasAttribute('data-dmo-procura-espera'), false);
    assert.equal(document.getElementById('lightProcuraProducoes').disabled, false);
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A página do Job On em miniatura: a procura fica (data-dmo-fica), os avisos e
// as folhas são zonas (data-dmo-regiao); o resto não pode mudar.
function pagina({ fora = 'Produções de 26/11/2026', folhas = '', avisos = '' } = {}) {
  return `<div data-dmo-troca>
    <h2 id="dia">${fora}</h2>
    <div id="avisos" data-dmo-regiao>${avisos}</div>
    <div class="procura" data-dmo-fica data-dmo-parcial>
      <form method="get"><input id="campo" data-dmo-procura="/JobOn?handler=ProcurarProducao" data-dmo-procura-alvo="alvo"></form>
      <div id="alvo"></div>
    </div>
    <div id="folhas" data-dmo-regiao>${folhas}</div>
  </div>`;
}

const FICHA = '<section id="lightFicha"><h2>Job On</h2><a href="/JobOn?dia=26" data-dmo-parcial id="fechar">Fechar</a></section>';

async function abrirPagina(html, respostas) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>Job On</title><body>${html}</body>`,
    { url: 'http://dmo.test/JobOn?ano=2026&mes=11&dia=26', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url) => {
    pedidos.push(String(url));
    const corpo = respostas(String(url));
    return { ok: true, url: new URL(String(url), window.location.href).href, text: async () => corpo };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  return { dom, window, document: window.document, pedidos, navegou };
}

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

function escolherProducao(window, valor) {
  const select = window.document.createElement('select');
  select.setAttribute('data-dmo-ir', '');
  select.innerHTML = `<option value="">Produção</option><option value="${valor}">2026143 C1</option>`;
  window.document.getElementById('campo').after(select);
  select.value = valor;
  select.dispatchEvent(new window.Event('change', { bubbles: true }));
}

test('escolher uma produção troca só as folhas, sem recarregar a página', async () => {
  const { dom, window, document, navegou } = await abrirPagina(pagina(),
    (url) => url.includes('jobon_id=')
      ? `<!doctype html><title>Job On 2026143</title><body>${pagina({ folhas: FICHA })}</body>`
      : `<!doctype html><title>Job On</title><body>${pagina()}</body>`);
  try {
    const dia = document.getElementById('dia');
    const campo = document.getElementById('campo');
    campo.value = '7245C070';
    escolherProducao(window, '/JobOn?ano=2026&mes=11&dia=26&jobon_id=321#lightFicha');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.ok(document.getElementById('lightFicha'), 'a ficha aparece');
    assert.equal(document.getElementById('dia'), dia, 'o que fica fora das zonas não é desenhado de novo');
    assert.equal(document.getElementById('campo'), campo, 'a procura fica como estava');
    assert.equal(campo.value, '7245C070');
    assert.equal(window.location.search, '?ano=2026&mes=11&dia=26&jobon_id=321');
    assert.equal(document.title, 'Job On 2026143');

    // «Fechar» (um link data-dmo-parcial) também só tira a ficha.
    document.getElementById('fechar').click();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.getElementById('lightFicha'), null);
    assert.equal(document.getElementById('dia'), dia);
  } finally {
    dom.window.close();
  }
});

for (const [caso, nova] of [
  ['muda alguma coisa fora das zonas', pagina({ fora: 'Produções de 27/11/2026', folhas: FICHA })],
  ['a zona traz um elemento ligado por um script da página', pagina({ folhas: '<form method="post" id="lightForm" data-dmo-script></form>' })],
  ['a zona traz um script', pagina({ folhas: '<script>window.x = 1;</script>' })],
]) {
  test(`abre a página inteira quando ${caso}`, async () => {
    const { dom, window, document, navegou } = await abrirPagina(pagina(), () => nova);
    try {
      escolherProducao(window, '/JobOn?ano=2026&mes=11&dia=26&jobon_id=321');
      await esperar();
      assert.equal(navegou.length, 1, 'navega como antes');
      assert.equal(document.getElementById('lightFicha'), null);
    } finally {
      dom.window.close();
    }
  });
}

test('a referência escrita enche o dropdown à frente do campo; o campo vazio repõe-no desativado', async () => {
  const VAZIO = '<select id="lightProcuraProducoes" disabled><option value="">Produção</option></select>';
  const html = pagina().replace('data-dmo-procura-alvo="alvo">', 'data-dmo-procura-alvo="alvo" data-dmo-procura-sempre>')
    .replace('<div id="alvo"></div>', `<span id="alvo">${VAZIO}</span>`);
  const { dom, window, document, pedidos, navegou } = await abrirPagina(html, (url) => url.endsWith('q=7245C070')
    ? '<select id="lightProcuraProducoes" data-dmo-ir><option value="">Produção</option><option value="/JobOn?jobon_id=321">2026143 C1</option></select>'
    : VAZIO);
  const escrever = async (texto) => {
    const campo = document.getElementById('campo');
    campo.value = texto;
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 300));
  };
  try {
    await escrever('7245C070');
    assert.deepEqual(navegou, []);
    const cheio = document.querySelector('#alvo #lightProcuraProducoes');
    assert.equal(cheio.disabled, false);
    assert.equal(cheio.options.length, 2);
    assert.equal(document.querySelectorAll('#alvo table, #alvo [data-dmo-row]').length, 0, 'sem lista por baixo do campo');
    await escrever('');
    assert.ok(pedidos.some((url) => url.endsWith('q=')), 'o campo vazio também pede ao servidor');
    assert.equal(document.querySelector('#alvo #lightProcuraProducoes').disabled, true);
  } finally {
    dom.window.close();
  }
});

// GRAVAR SEM RECARREGAR: um PU NNPB em miniatura. O servidor grava e volta à
// mesma página (PRG); só a folha troca.
function folhaPu(total, extra = '', numero = '') {
  return `<section id="lista" data-dmo-parcial>
      <form method="post" action="/Controlo/Puncoes?handler=Add" id="add">
        <input type="hidden" name="__RequestVerificationToken" value="token-${total}">
        <input type="hidden" name="controlo_id" value="107">
        <input id="pu-numero" name="numero" value="${numero}" autofocus>
        <button id="adicionar">+ Adicionar punção</button>
      </form>
      <p id="total">${total}</p>${extra}
    </section>`;
}

function paginaPu(total, extra = '', numero = '') {
  return `<main data-dmo-troca><h2 id="titulo">PU NNPB</h2><div id="folha" data-dmo-regiao>${folhaPu(total, extra, numero)}</div>
    <form method="post" action="/Controlo/Puncoes?handler=Create" id="fora"><button>Criar</button></form></main>`;
}

// respostas(url, opcoes) devolve (ou promete) { url, body, tipo?, disposicao?, status? };
// se lançar um erro, o fetch rejeita, como numa falha de rede.
async function abrirPu(respostas, { html = paginaPu(15), endereco = 'http://dmo.test/Controlo/Puncoes?controlo_id=107' } = {}) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>PU NNPB</title><body>${html}</body>`,
    { url: endereco, runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const pedidos = [];
  const descarregados = [];
  window.fetch = async (url, opcoes = {}) => {
    pedidos.push({ url: String(url), metodo: opcoes.method || 'GET', corpo: String(opcoes.body || '') });
    const r = await respostas(String(url), opcoes);
    const final = new URL(r.url || String(url), window.location.href).href;
    return {
      ok: (r.status || 200) < 400,
      url: final,
      redirected: final !== new URL(String(url), window.location.href).href,
      headers: { get: (nome) => ({ 'content-type': r.tipo || 'text/html; charset=utf-8', 'content-disposition': r.disposicao || null })[nome.toLowerCase()] },
      text: async () => r.body,
      json: async () => JSON.parse(r.body),
      blob: async () => new window.Blob([r.body]),
    };
  };
  window.URL.createObjectURL = () => 'blob:http://dmo.test/1';
  window.URL.revokeObjectURL = () => {};
  window.HTMLAnchorElement.prototype.click = function () { descarregados.push(this.download); };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  return { dom, window, document: window.document, pedidos, navegou, descarregados };
}

const VOLTA = 'http://dmo.test/Controlo/Puncoes?controlo_id=107';

test('gravar dentro de data-dmo-parcial troca só a folha, com o foco no campo seguinte', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirPu((url) => (url.includes('handler=Add')
    ? { url: VOLTA, body: `<!doctype html><title>PU NNPB</title><body>${paginaPu(16)}</body>` }
    : { body: '' }));
  try {
    const titulo = document.getElementById('titulo');
    const historico = window.history.length;
    document.getElementById('pu-numero').value = '41';
    document.getElementById('add').requestSubmit(document.getElementById('adicionar'));
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(pedidos.length, 1);
    assert.equal(pedidos[0].metodo, 'POST');
    const corpo = new URLSearchParams(pedidos[0].corpo);
    assert.equal(corpo.get('numero'), '41');
    assert.equal(corpo.get('controlo_id'), '107');
    assert.equal(corpo.get('__RequestVerificationToken'), 'token-15', 'o token vai no corpo');
    assert.equal(document.getElementById('total').textContent, '16', 'o que se gravou aparece');
    assert.equal(document.getElementById('titulo'), titulo, 'o resto da página não é desenhado de novo');
    assert.equal(document.activeElement.id, 'pu-numero', 'o foco fica no número da próxima punção');
    assert.equal(document.getElementById('pu-numero').value, '');
    assert.equal(window.history.length, historico, 'gravar não acrescenta entradas ao «voltar»');
    assert.equal(window.location.href, VOLTA);
  } finally {
    dom.window.close();
  }
});

test('um duplo envio grava uma vez só', async () => {
  const { dom, document, pedidos } = await abrirPu(() => ({ url: VOLTA, body: `<!doctype html><body>${paginaPu(16)}</body>` }));
  try {
    const form = document.getElementById('add');
    form.requestSubmit();
    form.requestSubmit();
    await esperar();
    assert.equal(pedidos.filter((p) => p.metodo === 'POST').length, 1);
  } finally {
    dom.window.close();
  }
});

test('uma recusa volta à página com a mensagem, sem recarregar', async () => {
  const recusa = '<div data-dmo-toast="erro" class="pu-aviso" role="alert">Indique o número da punção. Nada foi registado.</div>';
  const { dom, document, navegou } = await abrirPu(() => ({
    url: VOLTA + '&recusa=MissingNumber', body: `<!doctype html><body>${paginaPu(15, recusa)}</body>` }));
  try {
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.deepEqual(navegou, []);
    const mensagem = document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]');
    assert.ok(mensagem, 'a mensagem vai para a pilha');
    assert.match(mensagem.textContent, /Indique o número da punção/);
  } finally {
    dom.window.close();
  }
});

test('a mesma recusa duas vezes seguidas mostra a mensagem as duas vezes', async () => {
  const recusa = '<div data-dmo-toast="erro" class="pu-aviso" role="alert">Indique o número da punção. Nada foi registado.</div>';
  const { dom, document, navegou } = await abrirPu(() => ({
    url: VOLTA + '&recusa=MissingNumber', body: `<!doctype html><body>${paginaPu(15, recusa)}</body>` }));
  try {
    document.getElementById('add').requestSubmit();
    await esperar();
    document.querySelector('.dmo-toast-pilha .dmo-toast-fechar').click();
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast="erro"]:not(.a-sair)').length, 1, 'a segunda recusa também aparece');
  } finally {
    dom.window.close();
  }
});

test('se o servidor mandar para outra página, abre-a com um GET', async () => {
  // Outra página que não o login: o login é a sessão que terminou, e fica
  // na página (ver erros-do-servidor.test.cjs).
  const { dom, document, pedidos, navegou } = await abrirPu(() => ({ url: 'http://dmo.test/Controlo/Resumo', body: '<!doctype html><body>Resumo</body>' }));
  try {
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.equal(pedidos.length, 1, 'o POST não se repete');
    assert.equal(navegou.length, 1, 'navega para o endereço final');
  } finally {
    dom.window.close();
  }
});

test('um PDF descarrega sem sair da página', async () => {
  const { dom, document, navegou, descarregados } = await abrirPu(() => ({
    body: '%PDF', tipo: 'application/pdf', disposicao: 'attachment; filename=PU-NNPB-9672P379.pdf' }));
  try {
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(descarregados, ['PU-NNPB-9672P379.pdf']);
    assert.equal(document.getElementById('total').textContent, '15');
  } finally {
    dom.window.close();
  }
});

test('um formulário fora de data-dmo-parcial envia-se como sempre', async () => {
  const { dom, window, document, pedidos } = await abrirPu(() => ({ body: '' }));
  try {
    const evento = new window.Event('submit', { bubbles: true, cancelable: true });
    document.getElementById('fora').dispatchEvent(evento);
    await esperar();
    assert.equal(evento.defaultPrevented, false);
    assert.equal(pedidos.length, 0);
  } finally {
    dom.window.close();
  }
});

// Gravar: o servidor volta à mesma página (PRG). A zona do formulário que
// gravou troca sempre, mesmo que o servidor a desenhe igual (a mesma hora no
// campo): o formulário volta limpo, como numa página nova. As outras zonas
// iguais ficam como estavam.
function paginaRegisto(linhas) {
  return `<div data-dmo-troca>
    <div id="movimento" data-dmo-regiao data-dmo-parcial>
      <form method="post" action="/Boquilhas?handler=RegisterMovement">
        <input type="radio" name="tipo" value="saida" checked autofocus id="tipoSaida">
        <input id="quantidade" name="quantidade" value="">
        <button id="guardar">Guardar movimento</button>
      </form>
    </div>
    <div id="numeros" data-dmo-regiao>Em reparação 4</div>
    <div id="movimentos" data-dmo-regiao>${linhas}</div>
  </div>`;
}

test('gravar troca sempre a zona do formulário, mesmo igual, e o formulário volta limpo', async () => {
  const html = paginaRegisto('<p>saída 4</p>');
  const virtualConsole = new VirtualConsole();
  const navegou = [];
  virtualConsole.on('jsdomError', (error) => { if (/navigation/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>Boquilhas</title><body>${html}</body>`,
    { url: 'http://dmo.test/Boquilhas?trace_id=7', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const { document } = window;
  const pedidos = [];
  window.fetch = async (url, opcoes) => {
    pedidos.push(`${(opcoes && opcoes.method) || 'GET'} ${url}`);
    return {
      ok: true, redirected: true, url: 'http://dmo.test/Boquilhas?trace_id=7',
      headers: { get: (nome) => (nome === 'Content-Type' ? 'text/html; charset=utf-8' : null) },
      text: async () => `<!doctype html><title>Boquilhas</title><body>${html}</body>`
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  try {
    const zonas = [];
    document.addEventListener('dmo:troca', (event) => zonas.push(...event.detail.zonas));
    const form = document.querySelector('#movimento form');
    const numeros = document.getElementById('numeros');
    document.getElementById('quantidade').value = '3';
    form.requestSubmit(document.getElementById('guardar'));
    form.requestSubmit(document.getElementById('guardar'));
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos, ['POST http://dmo.test/Boquilhas?handler=RegisterMovement'], 'um só POST com dois envios seguidos');
    assert.deepEqual(zonas, ['movimento'], 'só a zona do formulário troca; as iguais ficam');
    assert.notEqual(document.querySelector('#movimento form'), form, 'o formulário é o novo do servidor');
    assert.equal(document.getElementById('quantidade').value, '', 'a quantidade volta vazia');
    assert.equal(document.getElementById('numeros'), numeros);
    assert.equal(document.activeElement && document.activeElement.id, 'tipoSaida', 'o foco vai para o primeiro campo');
  } finally {
    dom.window.close();
  }
});

// A LIGAÇÃO FALHA DEPOIS DE O SERVIDOR GRAVAR (C-01/T3). O POST chega e grava,
// mas a resposta perde-se. O POST nunca se repete: a página pede-se outra vez
// com um GET para mostrar o que ficou gravado; sem ligação, fica o que está
// escrito, com uma mensagem fixa no canto.
const mensagensNoCanto = (document, tipo) => Array.from(
  document.querySelectorAll(`.dmo-toast-pilha [data-dmo-toast="${tipo}"]:not(.a-sair)`), (m) => m.textContent);

test('a ligação falha depois de o servidor gravar: o POST não se repete e a página mostra o que ficou gravado', async () => {
  let gravadas = 0;
  const { dom, window, document, pedidos, navegou } = await abrirPu((url, opcoes) => {
    if (opcoes.method === 'POST') { gravadas++; throw new TypeError('Failed to fetch'); }
    return { body: `<!doctype html><title>PU NNPB</title><body>${paginaPu(15 + gravadas)}</body>` };
  });
  let nativo = 0;
  window.HTMLFormElement.prototype.submit = function () { nativo++; };
  try {
    const titulo = document.getElementById('titulo');
    document.getElementById('pu-numero').value = '41';
    document.getElementById('add').requestSubmit(document.getElementById('adicionar'));
    await esperar();
    assert.equal(nativo, 0, 'o browser não volta a enviar o formulário');
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.url}`),
      ['POST http://dmo.test/Controlo/Puncoes?handler=Add', `GET ${VOLTA}`], 'um POST só, e a página pedida outra vez com um GET');
    assert.equal(gravadas, 1, 'gravado uma vez');
    assert.equal(document.getElementById('total').textContent, '16', 'a página mostra o que ficou gravado');
    assert.equal(document.getElementById('titulo'), titulo, 'sem recarregar a página');
    const avisos = mensagensNoCanto(document, 'erro');
    assert.equal(avisos.length, 1);
    assert.match(avisos[0], /mostra agora o que ficou gravado/);
  } finally {
    dom.window.close();
  }
});

test('sem ligação nenhuma: o POST não se repete, o que está escrito fica e aparece uma mensagem fixa', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirPu(() => { throw new TypeError('Failed to fetch'); });
  let nativo = 0;
  window.HTMLFormElement.prototype.submit = function () { nativo++; };
  try {
    const numero = document.getElementById('pu-numero');
    numero.value = '41';
    numero.focus();
    document.getElementById('add').requestSubmit(document.getElementById('adicionar'));
    await esperar();
    assert.equal(nativo, 0, 'o browser não volta a enviar o formulário');
    assert.deepEqual(navegou, []);
    assert.equal(pedidos.filter((p) => p.metodo === 'POST').length, 1);
    assert.equal(document.getElementById('pu-numero'), numero, 'o formulário fica');
    assert.equal(numero.value, '41', 'o que está escrito não se apaga');
    assert.equal(document.activeElement, numero, 'o foco fica no campo');
    const avisos = mensagensNoCanto(document, 'erro');
    assert.equal(avisos.length, 1);
    assert.match(avisos[0], /não foi possível confirmar se ficou gravado/);
    // Um segundo envio igual, à espera da ligação, não empilha a mesma mensagem.
    document.getElementById('add').requestSubmit(document.getElementById('adicionar'));
    await esperar();
    assert.equal(mensagensNoCanto(document, 'erro').length, 1);
  } finally {
    dom.window.close();
  }
});

// A LIGAÇÃO VOLTA (deep sweep, 18/10/2026, B-boquilhas-2): o POST gravou, a
// resposta perdeu-se e o GET de confirmação também falhou. Antes, ficava tudo
// como estava (o número escrito e o total antigo) e um novo clique gravava
// outra vez. Agora o envio espera na fila e, quando a ligação volta (o evento
// online ou, sem ele, tempos.confirmar), a página confirma sozinha com um GET:
// o que ficou gravado aparece. O POST nunca se repete, e o envio seguinte
// parte a seguir à confirmação.
test('a ligação volta depois de o GET de confirmação falhar: a página confirma sozinha, sem repetir o POST', async () => {
  for (const comoVolta of ['online', 'tempo']) {
    const rede = { cortada: false };
    const servidor = { linhas: [] };
    const { dom, window, document, pedidos, navegou } = await abrirPu((url, opcoes) => {
      if (rede.cortada) throw new TypeError('Failed to fetch');
      if (opcoes.method === 'POST') {
        servidor.linhas.push(new URLSearchParams(String(opcoes.body)).get('numero'));
        if (servidor.linhas.length === 1) {
          // O servidor gravou, mas a ligação cai antes de a resposta chegar.
          rede.cortada = true;
          throw new TypeError('Failed to fetch');
        }
        return { url: VOLTA, body: `<!doctype html><body>${paginaPu(15 + servidor.linhas.length)}</body>` };
      }
      return { body: `<!doctype html><body>${paginaPu(15 + servidor.linhas.length)}</body>` };
    });
    try {
      window.DmoTroca.tempos.aviso = 30;
      window.DmoTroca.tempos.confirmar = comoVolta === 'tempo' ? 400 : 60000;
      const metodos = () => pedidos.map((p) => p.metodo);
      document.getElementById('pu-numero').value = '41';
      document.getElementById('add').requestSubmit(document.getElementById('adicionar'));
      await esperar(60);
      assert.deepEqual(metodos(), ['POST', 'GET'], comoVolta);
      assert.equal(document.getElementById('total').textContent, '15', 'sem ligação, nada se troca');
      assert.equal(document.getElementById('pu-numero').value, '41');
      assert.match(mensagensNoCanto(document, 'erro')[0], /não foi possível confirmar/);
      assert.ok(document.querySelector('[data-dmo-espera]'), 'o envio espera, com o «A gravar…» no canto');
      // Um clique igual enquanto espera não grava outra vez; um número novo espera na fila.
      document.getElementById('add').requestSubmit(document.getElementById('adicionar'));
      await esperar();
      assert.deepEqual(metodos(), ['POST', 'GET'], 'o clique igual não envia nada');
      document.getElementById('pu-numero').value = '42';
      document.getElementById('add').requestSubmit(document.getElementById('adicionar'));
      await esperar();
      assert.deepEqual(metodos(), ['POST', 'GET'], 'o número novo espera pela confirmação');

      rede.cortada = false;
      if (comoVolta === 'online') window.dispatchEvent(new window.Event('online'));
      await esperar(comoVolta === 'tempo' ? 600 : 60);
      assert.deepEqual(navegou, []);
      assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.url}`), [
        'POST http://dmo.test/Controlo/Puncoes?handler=Add', `GET ${VOLTA}`, `GET ${VOLTA}`, 'POST http://dmo.test/Controlo/Puncoes?handler=Add'],
      'a confirmação é um GET; o POST que falhou não se repete e o número novo parte a seguir');
      assert.deepEqual(servidor.linhas, ['41', '42'], 'cada número gravado uma vez');
      assert.equal(document.getElementById('total').textContent, '17', 'o que ficou gravado aparece');
      assert.equal(document.getElementById('pu-numero').value, '');
      assert.equal(document.querySelector('[data-dmo-espera]'), null, 'a fila esvaziou');
      assert.equal(mensagensNoCanto(document, 'erro').filter((m) => /não foi possível confirmar/.test(m)).length, 0, 'a mensagem de sem ligação sai');
    } finally {
      dom.window.close();
    }
  }
});

// UMA CHAVE POR ENVIO (deep sweep, 18/10/2026, B-boquilhas-2). O movimento
// gravou, a resposta perdeu-se e a confirmação mostra-o na lista; o formulário
// (zona igual) fica com a quantidade escrita. Carregar outra vez no botão era
// um segundo movimento. Agora o mesmo envio leva a mesma chave (X-Dmo-Envio) e
// o servidor, que se lembra dela, não grava outra vez. Outra quantidade, a
// mesma escrita outra vez pelo operador (um movimento novo igual), ou um
// envio depois de uma resposta, é um envio novo, com uma chave nova.
test('a ligação cai depois de gravar e o operador carrega outra vez: o mesmo envio leva a mesma chave e não grava duas vezes', async () => {
  for (const depois of ['igual', 'outra', 'reescrita']) {
    const servidor = { linhas: [], chaves: [], gravadas: new Set() };
    const rede = { cairDepoisDoPost: true };
    const desenhar = () => `<!doctype html><title>Boquilhas</title><body>${paginaRegisto(servidor.linhas.map((linha) => `<p>${linha}</p>`).join(''))}</body>`;
    const dom = new JSDOM(desenhar(), { url: 'http://dmo.test/Boquilhas?trace_id=7', runScripts: 'outside-only' });
    const { window } = dom;
    const { document } = window;
    window.fetch = async (url, opcoes = {}) => {
      const post = opcoes.method === 'POST';
      if (post) {
        // O servidor com [EnvioUnico]: uma chave já gravada não grava outra vez.
        const chave = opcoes.headers && opcoes.headers['X-Dmo-Envio'];
        servidor.chaves.push(chave);
        if (!chave || !servidor.gravadas.has(chave)) {
          servidor.linhas.push(`saída ${new URLSearchParams(String(opcoes.body)).get('quantidade')}`);
          if (chave) servidor.gravadas.add(chave);
        }
        if (rede.cairDepoisDoPost) { rede.cairDepoisDoPost = false; throw new TypeError('Failed to fetch'); }
      }
      return {
        ok: true, redirected: post, url: 'http://dmo.test/Boquilhas?trace_id=7',
        headers: { get: (nome) => (nome === 'Content-Type' ? 'text/html; charset=utf-8' : null) },
        text: async () => desenhar()
      };
    };
    window.Element.prototype.scrollIntoView = function () {};
    window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
    window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
    try {
      const guardar = () => document.querySelector('#movimento form').requestSubmit(document.getElementById('guardar'));
      const form = document.querySelector('#movimento form');
      document.getElementById('quantidade').value = '3';
      guardar();
      await esperar(60);
      assert.deepEqual(servidor.linhas, ['saída 3'], depois);
      assert.equal(document.getElementById('movimentos').textContent, 'saída 3', 'a confirmação mostra o que ficou gravado');
      assert.equal(document.querySelector('#movimento form'), form, 'o formulário fica, com o que está escrito');
      assert.equal(document.getElementById('quantidade').value, '3');

      if (depois === 'outra') document.getElementById('quantidade').value = '4';
      if (depois === 'reescrita') {
        const quantidade = document.getElementById('quantidade');
        quantidade.value = '3';
        quantidade.dispatchEvent(new window.Event('input', { bubbles: true }));
      }
      guardar();
      await esperar(60);
      if (depois === 'igual') {
        assert.equal(servidor.chaves[1], servidor.chaves[0], 'o mesmo envio outra vez leva a mesma chave');
        assert.deepEqual(servidor.linhas, ['saída 3'], 'e não grava duas vezes');
      } else if (depois === 'outra') {
        assert.notEqual(servidor.chaves[1], servidor.chaves[0], 'outra quantidade é outro envio');
        assert.deepEqual(servidor.linhas, ['saída 3', 'saída 4']);
      } else {
        assert.notEqual(servidor.chaves[1], servidor.chaves[0], 'escrita outra vez, é outro movimento');
        assert.deepEqual(servidor.linhas, ['saída 3', 'saída 3']);
      }
      assert.equal(document.getElementById('quantidade').value, '', 'com a resposta, o formulário volta limpo');

      // Depois de uma resposta, a mesma quantidade é um movimento novo.
      document.getElementById('quantidade').value = '3';
      guardar();
      await esperar(60);
      assert.equal(new Set(servidor.chaves).size, depois === 'igual' ? 2 : 3);
      assert.equal(servidor.linhas.length, depois === 'igual' ? 2 : 3, 'um envio novo grava');
      servidor.chaves.forEach((chave) => assert.match(String(chave), /^[0-9a-f]{32}$/, 'cada envio leva uma chave'));
    } finally {
      dom.window.close();
    }
  }
});

// DOIS FORMULÁRIOS SEGUIDOS (T1): um segundo envio, noutro formulário, antes de
// chegar a resposta do primeiro. A resposta do primeiro (uma recusa) não se
// deita fora: as gravações seguem em fila, cada uma com a sua resposta.
// Como nos Pegamentos: a correção recusada volta aberta com autofocus; o campo
// da leitura nova não tem autofocus (o foco fica pelo mesmo id).
function paginaDoisCartoes({ aviso = '', bq = 0 } = {}) {
  return `<main data-dmo-troca><h2 id="titulo">Pegamentos</h2>
    <div id="avisos" data-dmo-regiao>${aviso}</div>
    <div id="cartaoCm" data-dmo-regiao><section data-dmo-parcial>
      <form method="post" action="/Controlo/Pegamentos?handler=EditMeasurement" id="corrigir"><input name="costura" id="cm" value="70.05"${aviso ? ' autofocus' : ''}></form>
    </section></div>
    <div id="cartaoBq" data-dmo-regiao><section data-dmo-parcial>
      <p id="bq-total">${bq}</p>
      <form method="post" action="/Controlo/Pegamentos?handler=RegisterMeasurement" id="registar"><input name="costura" id="bq"></form>
    </section></div>
  </main>`;
}

const PEG = 'http://dmo.test/Controlo/Pegamentos?controlo_id=153';
const RECUSA_CM = '<div data-dmo-toast="erro" role="alert">Indique a costura com um valor numérico. Nada foi alterado.</div>';

test('um segundo formulário enviado antes da resposta do primeiro não deita fora a recusa do primeiro', async () => {
  let soltar;
  const primeira = new Promise((resolve) => { soltar = resolve; });
  const { dom, document, pedidos, navegou } = await abrirPu(async (url) => {
    if (url.includes('handler=EditMeasurement')) {
      await primeira;
      return { url: PEG + '&editar_medicao=2711&correcao=valor-invalido', body: `<!doctype html><body>${paginaDoisCartoes({ aviso: RECUSA_CM })}</body>` };
    }
    return { url: PEG, body: `<!doctype html><body>${paginaDoisCartoes({ bq: 1 })}</body>` };
  }, { html: paginaDoisCartoes(), endereco: PEG });
  try {
    document.getElementById('cm').value = 'abc';
    document.getElementById('corrigir').requestSubmit();
    const bq = document.getElementById('bq');
    bq.value = '0.12';
    bq.focus();
    document.getElementById('registar').requestSubmit();
    await esperar();
    soltar();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => p.url), ['/Controlo/Pegamentos?handler=EditMeasurement', '/Controlo/Pegamentos?handler=RegisterMeasurement'].map((u) => 'http://dmo.test' + u));
    assert.deepEqual(mensagensNoCanto(document, 'erro').map((m) => m.replace('×', '')),
      ['Indique a costura com um valor numérico. Nada foi alterado.'], 'a recusa do primeiro aparece');
    assert.equal(document.getElementById('bq-total').textContent, '1', 'o segundo também fica gravado e aparece');
    assert.equal(document.activeElement && document.activeElement.id, 'bq', 'o foco fica no cartão onde o operador já estava');
  } finally {
    dom.window.close();
  }
});

test('um clique durante a gravação espera por ela: a recusa aparece e o clique abre a seguir', async () => {
  let soltar;
  const primeira = new Promise((resolve) => { soltar = resolve; });
  const { dom, window, document, pedidos, navegou } = await abrirPu(async (url, opcoes) => {
    if (opcoes.method === 'POST') {
      await primeira;
      return { url: PEG + '&correcao=valor-invalido', body: `<!doctype html><body>${paginaDoisCartoes({ aviso: RECUSA_CM })}</body>` };
    }
    return { body: `<!doctype html><body>${paginaDoisCartoes({ bq: 7 })}</body>` };
  }, { html: paginaDoisCartoes(), endereco: PEG });
  try {
    document.getElementById('cm').value = 'abc';
    document.getElementById('corrigir').requestSubmit();
    window.DmoTroca.ir(PEG + '&pagina=2', document.getElementById('bq'));
    await esperar();
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST'], 'o GET do clique ainda não saiu');
    soltar();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'GET']);
    assert.equal(document.getElementById('bq-total').textContent, '7', 'fica o que o clique abriu');
    assert.equal(window.location.href, PEG + '&pagina=2');
    assert.equal(mensagensNoCanto(document, 'erro').length, 1, 'a recusa da gravação aparece');
  } finally {
    dom.window.close();
  }
});

// UM CLIQUE DURANTE UMA GRAVAÇÃO (R5, E4-07): o servidor ainda não gravou
// quando o GET do clique chega. O GET espera pela gravação: a leitura gravada
// aparece e o campo fica limpo, por isso o operador não a repete.
function servidorDoisCartoes() {
  const s = { bq: 0 };
  s.responder = (url, opcoes = {}) => {
    if (opcoes.method === 'POST') s.bq++;
    return { url: opcoes.method === 'POST' ? PEG : url, body: `<!doctype html><body>${paginaDoisCartoes({ bq: s.bq })}</body>` };
  };
  return s;
}

test('um clique durante uma gravação não esconde a leitura gravada: o GET só sai depois de ela ficar na base', async () => {
  let soltar;
  const lento = new Promise((resolve) => { soltar = resolve; });
  const s = servidorDoisCartoes();
  const { dom, window, document, pedidos, navegou } = await abrirPu(async (url, opcoes) => {
    if (opcoes.method === 'POST') await lento;
    return s.responder(url, opcoes);
  }, { html: paginaDoisCartoes(), endereco: PEG });
  try {
    const bq = document.getElementById('bq');
    bq.value = '0.12';
    document.getElementById('registar').requestSubmit();
    window.DmoTroca.ir(PEG + '&pagina=2', document.getElementById('cm'));
    await esperar();
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST'], 'o GET espera pela gravação');
    soltar();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.url}`), ['POST http://dmo.test/Controlo/Pegamentos?handler=RegisterMeasurement', `GET ${PEG}&pagina=2`]);
    assert.equal(s.bq, 1, 'gravado uma vez');
    assert.equal(document.getElementById('bq-total').textContent, '1', 'a leitura gravada aparece');
    assert.equal(document.getElementById('bq').value, '', 'o campo fica limpo: não há nada para repetir');
    assert.equal(window.location.href, PEG + '&pagina=2', 'e o clique abre a seguir');
  } finally {
    dom.window.close();
  }
});

test('um clique ainda a caminho quando sai uma gravação: a resposta dele, desenhada antes, não esconde a gravação', async () => {
  let soltar;
  const lento = new Promise((resolve) => { soltar = resolve; });
  const s = servidorDoisCartoes();
  let gets = 0;
  const { dom, window, document, pedidos } = await abrirPu(async (url, opcoes) => {
    const r = s.responder(url, opcoes);
    // O primeiro GET é desenhado já (sem a gravação) e só chega depois dela.
    if (opcoes.method !== 'POST' && ++gets === 1) await lento;
    return r;
  }, { html: paginaDoisCartoes(), endereco: PEG });
  try {
    window.DmoTroca.ir(PEG + '&pagina=2', document.getElementById('cm'));
    await esperar();
    document.getElementById('bq').value = '0.12';
    document.getElementById('registar').requestSubmit();
    await esperar();
    assert.equal(document.getElementById('bq-total').textContent, '1');
    soltar();
    await esperar();
    assert.deepEqual(pedidos.map((p) => p.metodo), ['GET', 'POST', 'GET'], 'o clique pede-se outra vez depois da gravação');
    assert.equal(document.getElementById('bq-total').textContent, '1', 'a leitura gravada continua à vista');
    assert.equal(window.location.href, PEG + '&pagina=2');
  } finally {
    dom.window.close();
  }
});

// FORMULÁRIOS À ESPERA NA MESMA ZONA (R1, 3.ª ronda): no PU NNPB, os «vistos»
// e o número da punção estão na mesma zona (#puFolha) e os formulários não
// têm id. Cada resposta troca a zona (o ecrã mostra sempre a resposta mais
// nova); um formulário que ainda espera volta a mostrar o que foi enviado,
// marcado à espera, e um segundo envio igual é ignorado.
function paginaVistos(linhas, vistos, { comId = true } = {}) {
  const lista = Array.isArray(vistos) ? vistos : [vistos];
  const vistosHtml = lista.map((visto, i) => `<form method="post" action="/Controlo/Puncoes?handler=Seen"${comId && i === 0 ? ' id="visto"' : ''}>
      <input type="hidden" name="__RequestVerificationToken" value="t-${linhas.length}-${lista.join('')}">
      <input type="hidden" name="inspecao_id" value="${i + 1}"><input type="hidden" name="visto" value="${visto ? 'false' : 'true'}">
      <button${i === 0 ? ' id="bv"' : ''} class="pu-check" aria-pressed="${visto}">Visto</button></form>`).join('');
  return `<main data-dmo-troca><h2 id="titulo">PU NNPB</h2><div id="puFolha" data-dmo-regiao><section data-dmo-parcial>
    <p id="linhas">${linhas.join(',')}</p>${vistosHtml}
    <form method="post" action="/Controlo/Puncoes?handler=Add"${comId ? ' id="add"' : ''}>
      <input type="hidden" name="__RequestVerificationToken" value="t-${linhas.length}">
      <input type="hidden" name="controlo_id" value="107"><input name="numero" id="pu-numero" autofocus><button id="badd">+</button></form>
  </section></div></main>`;
}

// O servidor do PU em miniatura: cada POST espera pela sua porta (o teste
// solta-as por ordem) e grava; um GET devolve a página.
function servidorVistos(vistos, { comId = true } = {}) {
  const s = { linhas: [], vistos: [...vistos], portas: [], posts: [] };
  s.responder = async (url, opcoes) => {
    if (opcoes.method === 'POST') {
      const corpo = new URLSearchParams(String(opcoes.body));
      s.posts.push(`${new URL(url).searchParams.get('handler')} ${corpo.get('numero') || corpo.get('inspecao_id')}`);
      await new Promise((soltar) => s.portas.push(soltar));
      if (url.includes('handler=Seen')) s.vistos[Number(corpo.get('inspecao_id')) - 1] = corpo.get('visto') === 'true';
      if (url.includes('handler=Add')) s.linhas.push(corpo.get('numero'));
    }
    return { url: VOLTA, body: `<!doctype html><body>${paginaVistos(s.linhas, s.vistos, { comId })}</body>` };
  };
  // Solta o POST que está no servidor e espera que a resposta seja aplicada.
  s.soltar = async () => { await esperar(); s.portas.shift()(); await esperar(); };
  return s;
}

const numeroForm = (document) => document.getElementById('pu-numero').form;
const aEspera = (form) => form.getAttribute('aria-busy') === 'true' && form.hasAttribute('data-dmo-a-gravar');

for (const comId of [true, false]) {
  const com = comId ? 'com id' : 'sem id (pela ação e pelos campos escondidos, como no PU NNPB)';
  test(`um número enviado enquanto o visto grava: a resposta do visto troca a zona, o número volta escrito e marcado à espera, e um segundo Enter não o grava duas vezes (${com})`, async () => {
    const s = servidorVistos([false], { comId });
    const { dom, document, pedidos, navegou } = await abrirPu(s.responder, { html: paginaVistos([], false, { comId }) });
    try {
      document.querySelector('#bv').form.requestSubmit();
      const numero = document.getElementById('pu-numero');
      numero.value = 'C1F';
      numero.focus();
      numeroForm(document).requestSubmit();
      await s.soltar();
      assert.equal(document.getElementById('bv').getAttribute('aria-pressed'), 'true', 'a resposta do visto aparece logo');
      const novo = document.getElementById('pu-numero');
      assert.notEqual(novo, numero, 'a zona trocou');
      assert.equal(novo.value, 'C1F', 'o número enviado continua escrito');
      assert.ok(aEspera(novo.form) && novo.hasAttribute('data-dmo-a-gravar'), 'marcado à espera');
      assert.equal(document.activeElement, novo, 'o foco fica no campo com o mesmo id');
      novo.form.requestSubmit();
      await s.soltar();
      assert.deepEqual(navegou, []);
      assert.deepEqual(s.linhas, ['C1F'], 'gravado uma vez só');
      assert.equal(pedidos.filter((p) => p.metodo === 'POST').length, 2);
      assert.equal(document.getElementById('linhas').textContent, 'C1F');
      assert.equal(document.getElementById('pu-numero').value, '', 'o formulário volta limpo');
      assert.equal(aEspera(numeroForm(document)), false);
      assert.equal(document.querySelectorAll('[data-dmo-a-gravar]').length, 0);
      assert.equal(document.activeElement.id, 'pu-numero');
    } finally {
      dom.window.close();
    }
  });

  test(`o número enviado antes de um visto: a resposta do número mostra a linha e limpa o campo logo, sem esperar pelo visto (${com})`, async () => {
    const s = servidorVistos([false], { comId });
    const { dom, document, navegou } = await abrirPu(s.responder, { html: paginaVistos([], false, { comId }) });
    try {
      const numero = document.getElementById('pu-numero');
      numero.value = 'C1F';
      numero.focus();
      numeroForm(document).requestSubmit();
      document.querySelector('#bv').form.requestSubmit();
      await s.soltar();
      assert.deepEqual(s.linhas, ['C1F']);
      assert.equal(document.getElementById('linhas').textContent, 'C1F', 'a linha gravada aparece logo');
      assert.equal(document.getElementById('pu-numero').value, '', 'o campo limpa logo');
      assert.ok(aEspera(document.querySelector('#bv').form), 'o visto que espera fica marcado');
      // Como na sonda do cético: o operador só repete se ainda vir o número no campo sem a linha.
      if (document.getElementById('pu-numero').value === 'C1F') numeroForm(document).requestSubmit();
      await s.soltar();
      assert.deepEqual(navegou, []);
      assert.deepEqual(s.linhas, ['C1F'], 'gravado uma vez');
      assert.deepEqual(s.posts, ['Add C1F', 'Seen 1']);
      assert.equal(document.getElementById('bv').getAttribute('aria-pressed'), 'true');
    } finally {
      dom.window.close();
    }
  });
}

test('três envios na mesma zona (visto, número, visto): cada resposta aparece, o que espera fica marcado e cada um grava uma vez', async () => {
  const s = servidorVistos([false, false], { comId: false });
  const { dom, document, pedidos, navegou } = await abrirPu(s.responder, { html: paginaVistos([], [false, false], { comId: false }) });
  const vistos = () => Array.from(document.querySelectorAll('button.pu-check'), (b) => b.getAttribute('aria-pressed')).join(',');
  try {
    document.querySelectorAll('button.pu-check')[0].form.requestSubmit();
    const numero = document.getElementById('pu-numero');
    numero.value = 'C1F';
    numero.focus();
    numeroForm(document).requestSubmit();
    document.querySelectorAll('button.pu-check')[1].form.requestSubmit();
    await s.soltar();
    assert.equal(vistos(), 'true,false', 'o primeiro visto aparece');
    assert.equal(document.getElementById('pu-numero').value, 'C1F');
    assert.ok(aEspera(numeroForm(document)));
    assert.ok(aEspera(document.querySelectorAll('button.pu-check')[1].form), 'o segundo visto também espera, marcado');
    // Um segundo clique no visto que espera e um segundo Enter no número: ignorados.
    document.querySelectorAll('button.pu-check')[1].form.requestSubmit();
    numeroForm(document).requestSubmit();
    await s.soltar();
    assert.equal(document.getElementById('linhas').textContent, 'C1F', 'o número aparece com a resposta dele');
    assert.equal(document.getElementById('pu-numero').value, '');
    assert.ok(aEspera(document.querySelectorAll('button.pu-check')[1].form));
    await s.soltar();
    assert.deepEqual(navegou, []);
    assert.equal(vistos(), 'true,true');
    assert.deepEqual(s.posts, ['Seen 1', 'Add C1F', 'Seen 2'], 'um POST por envio, sem repetidos');
    assert.equal(pedidos.length, 3);
    assert.equal(document.querySelectorAll('[data-dmo-a-gravar], [aria-busy]').length, 0, 'nada fica marcado');
  } finally {
    dom.window.close();
  }
});

test('vistos seguidos (R-e): o ecrã muda a cada resposta e um segundo clique num visto que espera não manda outro POST', async () => {
  const s = servidorVistos([true, true, true, true], { comId: false });
  const { dom, document, navegou } = await abrirPu(s.responder, { html: paginaVistos([], [true, true, true, true], { comId: false }) });
  const botoes = () => Array.from(document.querySelectorAll('button.pu-check'));
  const vistos = () => botoes().map((b) => (b.getAttribute('aria-pressed') === 'true' ? 'V' : '-')).join('');
  try {
    botoes().forEach((botao) => botao.form.requestSubmit());
    // O último clicado tem o foco (no browser, o clique dá-lho).
    botoes()[3].focus();
    await s.soltar();
    assert.equal(vistos(), '-VVV', 'o primeiro muda com a resposta dele');
    assert.equal(document.activeElement, botoes()[3], 'o foco passa para o mesmo visto, no elemento novo');
    assert.deepEqual(botoes().map((b) => b.hasAttribute('data-dmo-a-gravar')), [false, true, true, true], 'os que esperam ficam marcados');
    botoes()[2].form.requestSubmit();
    await s.soltar();
    assert.equal(vistos(), '--VV');
    await s.soltar();
    await s.soltar();
    assert.deepEqual(navegou, []);
    assert.equal(vistos(), '----');
    assert.deepEqual(s.posts, ['Seen 1', 'Seen 2', 'Seen 3', 'Seen 4'], 'quatro cliques, quatro POST');
  } finally {
    dom.window.close();
  }
});

test('o mesmo formulário com outro corpo vai para a fila; com o mesmo corpo é ignorado', async () => {
  const s = servidorVistos([false]);
  const { dom, document } = await abrirPu(s.responder, { html: paginaVistos([], false) });
  try {
    const numero = document.getElementById('pu-numero');
    numero.value = 'C1F';
    numeroForm(document).requestSubmit();
    numeroForm(document).requestSubmit();
    numero.value = 'C1G';
    numeroForm(document).requestSubmit();
    await s.soltar();
    assert.equal(document.getElementById('pu-numero').value, 'C1G', 'o que espera mostra o que foi enviado');
    await s.soltar();
    assert.deepEqual(s.linhas, ['C1F', 'C1G']);
    assert.equal(document.getElementById('pu-numero').value, '');
  } finally {
    dom.window.close();
  }
});

// UM SERVIDOR LENTO (R-c): não há prazo. A resposta chega quando chegar e
// aparece; o «A gravar…» aparece ao fim de tempos.aviso com a fila ocupada e
// sai quando ela esvazia. Antes, ao fim do prazo, o fetch era cancelado e a
// confirmação chegava antes da gravação: o dado ficava sem aparecer.
test('um servidor lento: nada se cancela, a resposta aparece quando chega e o «A gravar…» fica até a fila esvaziar', async () => {
  const s = servidorVistos([false]);
  const sinais = [];
  const { dom, window, document, pedidos, navegou } = await abrirPu((url, opcoes) => {
    sinais.push(opcoes.signal);
    return s.responder(url, opcoes);
  }, { html: paginaVistos([], false) });
  try {
    window.DmoTroca.tempos.aviso = 30;
    window.DmoTroca.tempos.prazo = 60;
    document.getElementById('pu-numero').value = 'C1J';
    numeroForm(document).requestSubmit();
    document.querySelector('#bv').form.requestSubmit();
    await esperar(150);
    const espera = document.querySelector('[data-dmo-espera]');
    assert.ok(espera && espera.closest('.dmo-toast-pilha'), 'o sinal aparece na pilha fixa do canto');
    assert.equal(espera.getAttribute('role'), 'status');
    assert.equal(espera.closest('[data-dmo-troca]'), null, 'fora da página: não empurra nada');
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST'], 'sem GET de confirmação: o envio continua à espera');
    await s.soltar();
    assert.equal(document.getElementById('linhas').textContent, 'C1J', 'a gravação lenta aparece');
    assert.ok(document.querySelector('[data-dmo-espera]'), 'o visto ainda espera: o sinal fica');
    await s.soltar();
    assert.deepEqual(navegou, []);
    assert.equal(document.querySelector('[data-dmo-espera]'), null, 'a fila esvaziou: o sinal sai');
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST', 'POST']);
    assert.equal(sinais.filter(Boolean).length, 0, 'nenhum pedido leva um AbortSignal');
    assert.deepEqual(mensagensNoCanto(document, 'erro'), []);
  } finally {
    dom.window.close();
  }
});

// UMA LIGAÇÃO QUE FALHA COM A FILA OCUPADA (R2, R-d): o POST do visto falha
// na rede; a confirmação (um GET, lento) e o número em fila vêm a seguir. O
// «A gravar…» fica durante a confirmação e só sai quando a fila esvazia.
test('a ligação falha com envios em fila: o «A gravar…» fica durante a confirmação, o POST não se repete e o envio seguinte parte', async () => {
  const servidor = { linhas: [], visto: false };
  let soltarGet;
  const { dom, window, document, pedidos, navegou } = await abrirPu(async (url, opcoes) => {
    if (opcoes.method === 'POST' && url.includes('handler=Seen')) {
      servidor.visto = !servidor.visto;
      // O servidor gravou, mas a resposta perde-se.
      await esperar(60);
      throw new TypeError('Failed to fetch');
    }
    if (opcoes.method === 'POST') servidor.linhas.push(new URLSearchParams(String(opcoes.body)).get('numero'));
    else if (!soltarGet) await new Promise((resolve) => { soltarGet = resolve; });
    return { url: VOLTA, body: `<!doctype html><body>${paginaVistos(servidor.linhas, servidor.visto)}</body>` };
  }, { html: paginaVistos([], false) });
  try {
    window.DmoTroca.tempos.aviso = 30;
    document.querySelector('#bv').form.requestSubmit();
    document.getElementById('pu-numero').value = 'C1H';
    numeroForm(document).requestSubmit();
    await esperar(150);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.url}`), ['POST http://dmo.test/Controlo/Puncoes?handler=Seen', `GET ${VOLTA}`]);
    assert.ok(document.querySelector('[data-dmo-espera]'), 'durante a confirmação, o sinal fica no canto');
    assert.equal(document.getElementById('pu-numero').value, 'C1H', 'o número espera escrito');
    soltarGet();
    await esperar(60);
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.url}`), [
      'POST http://dmo.test/Controlo/Puncoes?handler=Seen', `GET ${VOLTA}`, 'POST http://dmo.test/Controlo/Puncoes?handler=Add'],
    'o POST que falhou não se repete; a página confirma com um GET e o número parte a seguir');
    assert.equal(document.querySelector('[data-dmo-espera]'), null, 'o sinal sai');
    assert.deepEqual(servidor.linhas, ['C1H']);
    assert.equal(document.getElementById('linhas').textContent, 'C1H');
    assert.equal(document.getElementById('bv').getAttribute('aria-pressed'), 'true', 'o que ficou gravado aparece');
    const avisos = mensagensNoCanto(document, 'erro');
    assert.equal(avisos.length, 1);
    assert.match(avisos[0], /mostra agora o que ficou gravado/);
  } finally {
    dom.window.close();
  }
});

// A LIGAÇÃO FALHA DEPOIS DE UMA RECUSA (R3): o endereço ficou com a recusa
// (?recusa=…&numero=…&lote=x). A confirmação pede o endereço limpo da
// página: a recusa não volta e o formulário não traz os valores recusados.
test('a ligação falha depois de uma recusa: a confirmação não traz a recusa de volta nem os valores recusados', async () => {
  const recusa = '<div data-dmo-toast="erro" class="pu-aviso" role="alert">Indique o lote. Nada foi registado.</div>';
  let gravadas = 15;
  let envios = 0;
  const { dom, window, document, pedidos, navegou } = await abrirPu((url, opcoes) => {
    const endereco = new URL(url, VOLTA);
    if (opcoes.method === 'POST') {
      if (++envios === 1) return { url: VOLTA + '&recusa=InvalidLot&numero=C1G&lote=x', body: `<!doctype html><body>${paginaPu(gravadas, recusa, 'C1G')}</body>` };
      gravadas++;
      throw new TypeError('Failed to fetch');
    }
    if (endereco.searchParams.get('recusa')) return { body: `<!doctype html><body>${paginaPu(gravadas, recusa, endereco.searchParams.get('numero'))}</body>` };
    return { body: `<!doctype html><body>${paginaPu(gravadas)}</body>` };
  });
  try {
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.equal(document.getElementById('pu-numero').value, 'C1G');
    assert.equal(mensagensNoCanto(document, 'erro').length, 1);
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(pedidos.at(-1).url, VOLTA, 'o GET pede o endereço sem a recusa');
    assert.equal(document.getElementById('total').textContent, '16', 'o que ficou gravado aparece');
    assert.equal(document.getElementById('pu-numero').value, '', 'sem os valores recusados no formulário');
    const avisos = mensagensNoCanto(document, 'erro');
    assert.equal(avisos.length, 1, 'a recusa antiga não volta nem fica ao lado do que se gravou');
    assert.match(avisos[0], /mostra agora o que ficou gravado/);
    assert.equal(window.location.href, VOLTA);
  } finally {
    dom.window.close();
  }
});

// UM GET PEDIDO POR UM FORMULÁRIO (o «Supomos» do Peso): a página manda o
// submit de um form method=get para DmoTroca.ir com o botão que o enviou. A
// mesma recusa duas vezes seguidas mostra-se as duas vezes, como num envio;
// um link para o mesmo endereço continua a trocar só o que mudou.
function paginaSupor(valor) {
  const recusa = valor && !/^\d+([.,]\d+)?$/.test(valor) ? `<div data-dmo-toast="erro" role="alert">A calote suposta «${valor}» não é um número válido.</div>` : '';
  return `<main data-dmo-troca><h2 id="titulo">Peso</h2><div id="supomos" data-dmo-regiao><section data-dmo-parcial>
    <form method="get" action="/Controlo/Peso" id="supor"><input type="hidden" name="controlo_id" value="252"><input name="supor_calote" id="calote" value="${valor}"><button id="ver">Ver a diferença</button></form>
    ${recusa}<a id="mesmo" href="/Controlo/Peso?controlo_id=252&supor_calote=${valor}">mesmo endereço</a>
  </section></div></main>`;
}

test('num GET pedido por um formulário, a mesma recusa duas vezes seguidas mostra-se as duas vezes; num link não', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirPu((url) => ({
    body: `<!doctype html><body>${paginaSupor(new URL(url).searchParams.get('supor_calote') || '')}</body>` }),
  { html: paginaSupor(''), endereco: 'http://dmo.test/Controlo/Peso?controlo_id=252' });
  // Como o Peso: o submit de um form GET dentro de data-dmo-parcial vai para ir.
  document.addEventListener('submit', (event) => {
    const form = event.target;
    if ((form.getAttribute('method') || 'get').toLowerCase() !== 'get' || !form.closest('[data-dmo-parcial]')) return;
    event.preventDefault();
    const url = new URL(form.getAttribute('action'), window.location.href);
    url.search = new URLSearchParams(new window.FormData(form)).toString();
    window.DmoTroca.ir(url.href, event.submitter || form);
  });
  const ver = async () => { document.getElementById('supor').requestSubmit(document.getElementById('ver')); await esperar(); };
  try {
    document.getElementById('calote').value = 'abc';
    await ver();
    assert.equal(mensagensNoCanto(document, 'erro').length, 1, 'a recusa aparece');
    await ver();
    assert.equal(mensagensNoCanto(document, 'erro').length, 1, 'outra vez, sem a fechar: não fica repetida');
    document.querySelector('.dmo-toast-pilha .dmo-toast-fechar').click();
    assert.equal(mensagensNoCanto(document, 'erro').length, 0);
    await ver();
    assert.equal(mensagensNoCanto(document, 'erro').length, 1, 'fechada a recusa, o mesmo valor mostra-a outra vez');
    assert.equal(document.getElementById('calote').value, 'abc', 'o campo fica como estava');
    document.querySelector('.dmo-toast-pilha .dmo-toast-fechar').click();
    // (o abrirPu troca o click() dos links pela descarga de ficheiros: aqui um clique a sério)
    document.getElementById('mesmo').dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
    await esperar();
    assert.equal(mensagensNoCanto(document, 'erro').length, 0, 'um link para o mesmo endereço não a repete');
    assert.equal(pedidos.length, 4);
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

// UMA RECUSA ANTIGA (C-1): a gravação seguinte responde e a recusa que estava à
// vista sai do canto, como saía quando a página abria inteira.
test('a recusa que estava à vista sai do canto quando a gravação seguinte corre bem', async () => {
  const recusa = '<div data-dmo-toast="erro" class="pu-aviso" role="alert">Indique o número da punção. Nada foi registado.</div>';
  let envios = 0;
  const { dom, document, navegou } = await abrirPu(() => (++envios === 1
    ? { url: VOLTA + '&recusa=MissingNumber', body: `<!doctype html><body>${paginaPu(15, recusa)}</body>` }
    : { url: VOLTA, body: `<!doctype html><body>${paginaPu(16)}</body>` }));
  try {
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.equal(mensagensNoCanto(document, 'erro').length, 1, 'a recusa aparece');
    document.getElementById('pu-numero').value = '41';
    document.getElementById('add').requestSubmit();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.getElementById('total').textContent, '16');
    assert.deepEqual(mensagensNoCanto(document, 'erro'), [], 'a recusa antiga já não fica ao lado do que se gravou');
  } finally {
    dom.window.close();
  }
});

test('a recusa de um cartão fica no canto quando outro cartão grava', async () => {
  const { dom, document, navegou } = await abrirPu((url) => (url.includes('handler=EditMeasurement')
    ? { url: PEG + '&editar_medicao=2711&correcao=valor-invalido', body: `<!doctype html><body>${paginaDoisCartoes({ aviso: RECUSA_CM })}</body>` }
    : { url: PEG, body: `<!doctype html><body>${paginaDoisCartoes({ bq: 1 })}</body>` }), { html: paginaDoisCartoes(), endereco: PEG });
  try {
    document.getElementById('cm').value = 'abc';
    document.getElementById('corrigir').requestSubmit();
    await esperar();
    assert.equal(mensagensNoCanto(document, 'erro').length, 1, 'a recusa do CM aparece');
    document.getElementById('bq').value = '0.12';
    document.getElementById('registar').requestSubmit();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.getElementById('bq-total').textContent, '1');
    assert.equal(mensagensNoCanto(document, 'erro').length, 1, 'a recusa do CM continua: a correção não foi feita');
  } finally {
    dom.window.close();
  }
});

// As janelas (data-dmo-dialog) e os dropdowns que aplicam ao escolher
// (data-dmo-autosubmit) ligam-se por delegação: podem vir numa zona trocada.
// Um Resumo do Controlo em miniatura: uma área com o OK/NOK, o comentário numa
// janela e o servidor que grava e volta à mesma página (PRG).
function areaResumo({ estado = '', comentario = '' }) {
  const opcao = (valor, texto) => `<option value="${valor}"${estado === valor ? ' selected' : ''}>${texto}</option>`;
  return `<div id="area-CM" data-dmo-regiao>
      <div class="resumo-area ${estado.toLowerCase()}">
        <strong id="estado-texto">${estado || 'não registado'}</strong>
        <form method="post" action="/Controlo/Resumo?handler=AreaState">
          <input type="hidden" name="area" value="CM"><input type="hidden" name="comentario" value="${comentario}">
          <select id="estado-CM" name="estado" data-dmo-autosubmit>${opcao('', '—')}${opcao('OK', 'OK')}${opcao('NOK', 'NOK')}</select>
        </form>
        <button type="button" id="nota" data-dmo-dialog="janela-CM">${comentario || 'Comentário (opcional)'}</button>
        <dialog id="janela-CM" class="dmo-dialog">
          <form method="post" action="/Controlo/Resumo?handler=AreaState">
            <input type="hidden" name="area" value="CM"><input type="hidden" name="estado" value="${estado}">
            <textarea id="texto-CM" name="comentario" autofocus>${comentario}</textarea>
            <button type="button" id="cancelar" data-dmo-dialog-close>Cancelar</button>
            <button type="submit" id="guardar">Guardar</button>
          </form>
        </dialog>
      </div>
    </div>`;
}

function paginaResumo(area, extra = '') {
  return `<main data-dmo-troca><h2 id="titulo">Resumo do Controlo</h2>
    <div class="resumo-areas" data-dmo-parcial>${areaResumo(area)}</div>${extra}</main>`;
}

async function abrirResumo({ inicial = '', resposta = '', volta = 'http://dmo.test/Controlo/Resumo?jobon_id=9' } = {}) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>Resumo</title><body>${paginaResumo({}, inicial)}</body>`,
    { url: 'http://dmo.test/Controlo/Resumo?jobon_id=9', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  // O jsdom não tem showModal: abrir e fechar mexem só no atributo open.
  window.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  window.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
  const guardado = { estado: '', comentario: '' };
  const pedidos = [];
  window.fetch = async (url, opcoes = {}) => {
    const corpo = new URLSearchParams(String(opcoes.body || ''));
    pedidos.push({ metodo: opcoes.method || 'GET', estado: corpo.get('estado'), comentario: corpo.get('comentario') });
    if (opcoes.method === 'POST') { guardado.estado = corpo.get('estado') || ''; guardado.comentario = corpo.get('comentario') || ''; }
    return {
      ok: true, redirected: true, url: volta,
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? 'text/html; charset=utf-8' : null) },
      text: async () => `<!doctype html><title>Resumo</title><body>${paginaResumo(guardado, resposta)}</body>`
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  return { dom, window, document: window.document, pedidos, navegou };
}

function escolherEstado(window, valor) {
  const select = window.document.getElementById('estado-CM');
  select.focus();
  select.value = valor;
  select.dispatchEvent(new window.Event('change', { bubbles: true }));
}

test('o OK/NOK numa zona grava sem recarregar, o foco fica no dropdown e o dropdown novo volta a aplicar', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirResumo();
  try {
    const titulo = document.getElementById('titulo');
    escolherEstado(window, 'NOK');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.estado}`), ['POST NOK']);
    assert.equal(document.getElementById('estado-texto').textContent, 'NOK', 'o que se gravou aparece');
    assert.equal(document.activeElement.id, 'estado-CM', 'o foco fica no OK/NOK da área');
    assert.equal(document.getElementById('titulo'), titulo, 'o resto não é desenhado de novo');
    escolherEstado(window, 'OK');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.estado}`), ['POST NOK', 'POST OK'], 'o dropdown trocado também grava');
    assert.equal(document.getElementById('estado-texto').textContent, 'OK');
  } finally {
    dom.window.close();
  }
});

test('a janela numa zona trocada abre e fecha; gravar nela fecha-a e o foco volta ao botão que a abriu', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirResumo();
  try {
    escolherEstado(window, 'NOK');
    await esperar();
    // A janela e o botão são os da zona nova.
    document.getElementById('nota').click();
    assert.ok(document.getElementById('janela-CM').hasAttribute('open'), 'a janela abre');
    assert.equal(document.activeElement.id, 'texto-CM', 'o foco vai para o texto');
    document.getElementById('cancelar').click();
    assert.ok(!document.getElementById('janela-CM').hasAttribute('open'), 'Cancelar fecha');
    document.getElementById('nota').click();
    document.getElementById('texto-CM').value = 'Rebarba no gargalo';
    const guardar = document.getElementById('guardar');
    guardar.click();
    guardar.click();
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.filter((p) => p.metodo === 'POST').map((p) => `${p.estado} ${p.comentario}`),
      ['NOK ', 'NOK Rebarba no gargalo'], 'um POST com dois cliques; o estado vai com o comentário');
    assert.equal(document.getElementById('nota').textContent, 'Rebarba no gargalo');
    assert.equal(document.querySelectorAll('dialog[open]').length, 0, 'a janela fecha com a troca');
    assert.equal(document.activeElement.id, 'nota', 'o foco volta ao botão do comentário (o autofocus da janela fechada não conta)');
  } finally {
    dom.window.close();
  }
});

test('DMO-636: um link «Por associar» que também abre uma janela não pede a página nem cria histórico', async () => {
  // «Por associar» (Pages/ReparacaoInterna/Consultar.cshtml): um <a href="#...">
  // com data-dmo-dialog, dentro da zona parcial (#riConsultaResultados). Só o
  // showModal deve correr: nem fetch, nem # no endereço, nem histórico novo.
  const virtualConsole = new VirtualConsole();
  const navegou = [];
  virtualConsole.on('jsdomError', (error) => { if (/navigation/i.test(error.message)) navegou.push(error.message); });
  const html = `<main data-dmo-troca><h2 id="titulo">Reparação Interna</h2>
    <div id="riConsultaResultados" data-dmo-parcial>
      <a href="#riAssociar-1" id="porAssociar" data-dmo-dialog="riAssociar-1">Por associar</a>
    </div>
    <dialog id="riAssociar-1" class="dmo-dialog"><button type="button" data-dmo-dialog-close>Fechar</button></dialog>
  </main>`;
  const dom = new JSDOM(`<!doctype html><title>Reparação Interna</title><body>${html}</body>`,
    { url: 'http://dmo.test/ReparacaoInterna/Consultar', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const document = window.document;
  window.HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  window.HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
  const pedidos = [];
  window.fetch = async (url) => { pedidos.push(String(url)); return { ok: true, url: String(url), text: async () => '' }; };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  try {
    const historico = window.history.length;
    document.getElementById('porAssociar').click();
    await esperar();
    assert.ok(document.getElementById('riAssociar-1').hasAttribute('open'), 'a janela abre');
    assert.deepEqual(pedidos, [], 'não vai buscar a página');
    assert.equal(window.location.hash, '', 'o # não entra no endereço');
    assert.equal(window.history.length, historico, 'não cria entrada no histórico');
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('uma zona que traz uma janela aberta com a página (data-dmo-dialog-aberta) abre a página inteira', async () => {
  const { dom, window, navegou } = await abrirResumo({
    inicial: '<div id="aviso" data-dmo-regiao></div>',
    resposta: '<div id="aviso" data-dmo-regiao><dialog class="dmo-dialog" id="erro" data-dmo-dialog-aberta="1"></dialog></div>',
    volta: 'http://dmo.test/Controlo/Resumo?jobon_id=9&recusa=1' });
  try {
    escolherEstado(window, 'NOK');
    await esperar();
    assert.equal(navegou.length, 1, 'navega para a página inteira');
  } finally {
    dom.window.close();
  }
});

// Uma zona com sugestões ao escrever (data-dmo-suggest; na ficha da
// ferramenta, a «Referência MF associada» do Editar) troca-se como as outras:
// as sugestões ligam-se na zona nova com DMOSuggest.init, como no arranque.
function paginaFicha(mf) {
  return `<main data-dmo-troca><h2 id="titulo">Ficha de ferramenta</h2>
    <section id="editar" data-dmo-regiao data-dmo-parcial>
      <form method="post" action="/Ferramentas/Ficha?handler=EditFacts">
        <input id="mf" name="mf_reference" value="${mf}" data-dmo-suggest="ferramentas" data-dmo-suggest-tipo="mf">
        <button id="guardar">Guardar</button>
      </form>
    </section>
  </main>`;
}

test('uma zona com sugestões troca-se sem recarregar e as sugestões ligam-se na zona nova', async () => {
  const virtualConsole = new VirtualConsole();
  const navegou = [];
  virtualConsole.on('jsdomError', (error) => { if (/navigation/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>Ficha</title><body>${paginaFicha('')}</body>`,
    { url: 'http://dmo.test/Ferramentas/Ficha?tool_id=T1', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const { document } = window;
  const pedidos = [];
  window.fetch = async (url, opcoes) => {
    pedidos.push(`${(opcoes && opcoes.method) || 'GET'} ${url}`);
    return {
      ok: true, redirected: true, url: 'http://dmo.test/Ferramentas/Ficha?tool_id=T1&guardado=geral',
      headers: { get: (nome) => (nome === 'Content-Type' ? 'text/html; charset=utf-8' : null) },
      text: async () => `<!doctype html><title>Ficha</title><body>${paginaFicha('5811')}</body>`
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'js/dmo-suggest.js'), 'utf8'));
  try {
    await esperar();
    const titulo = document.getElementById('titulo');
    const antes = document.getElementById('mf');
    assert.equal(antes.getAttribute('role'), 'combobox', 'as sugestões ligam-se no arranque');
    antes.value = '5811';
    document.querySelector('#editar form').requestSubmit(document.getElementById('guardar'));
    await esperar();
    assert.deepEqual(navegou, [], 'não abre a página inteira');
    assert.deepEqual(pedidos, ['POST http://dmo.test/Ferramentas/Ficha?handler=EditFacts']);
    const depois = document.getElementById('mf');
    assert.notEqual(depois, antes, 'o Editar é o novo do servidor');
    assert.equal(depois.value, '5811', 'o que se gravou aparece');
    assert.equal(depois.getAttribute('role'), 'combobox', 'as sugestões ligam-se na zona nova');
    assert.ok(depois.parentElement.classList.contains('dmo-suggest'));
    assert.equal(document.getElementById('titulo'), titulo, 'o resto da página não é desenhado de novo');
    assert.equal(window.location.search, '?tool_id=T1&guardado=geral');
  } finally {
    dom.window.close();
  }
});

function catalogo({ pagina = 1, vazio = false } = {}) {
  return `<main data-dmo-troca>
    <h2 id="cabecalho">Ferramentas</h2>
    <form method="get" action="/JobOn" data-dmo-fica data-dmo-parcial data-dmo-ao-escrever="resultados">
      <input id="referencia" name="q" value="BQ">
    </form>
    <section id="resultados" data-dmo-regiao ${vazio ? 'hidden' : ''}>
      ${vazio ? '' : `<table data-dmo-list><tbody><tr data-dmo-row data-id="${pagina}"><td>Página ${pagina}</td></tr></tbody></table>
      <nav data-dmo-parcial><a id="outra" href="/JobOn?q=BQ&pagina=${pagina === 1 ? 2 : 1}">Outra página</a></nav>`}
    </section>
  </main>`;
}

test('pesquisar e paginar mantém o campo e o foco, mostra a região oculta e permite voltar à primeira página', async () => {
  const { dom, window, document, navegou } = await abrirPagina(catalogo({ vazio: true }),
    (url) => catalogo({ pagina: url.includes('pagina=2') ? 2 : 1 }));
  try {
    const campo = document.getElementById('referencia');
    const cabecalho = document.getElementById('cabecalho');
    campo.focus();
    campo.form.requestSubmit();
    await esperar();
    assert.equal(document.getElementById('resultados').hidden, false);
    assert.equal(document.activeElement, campo);
    assert.match(document.getElementById('resultados').textContent, /Página 1/);
    document.getElementById('outra').click();
    await esperar();
    assert.match(document.getElementById('resultados').textContent, /Página 2/);
    document.getElementById('outra').click();
    await esperar();
    assert.match(document.getElementById('resultados').textContent, /Página 1/);
    assert.equal(document.getElementById('referencia'), campo);
    assert.equal(document.getElementById('cabecalho'), cabecalho);
    assert.deepEqual(navegou, []);
    const linha = document.querySelector('[data-dmo-row]');
    linha.click();
    assert.equal(linha.getAttribute('aria-selected'), 'true', 'a lista nova está ligada');
  } finally { dom.window.close(); }
});

test('a pesquisa atrasada não substitui a página escolhida depois', async () => {
  const { dom, window, document, navegou } = await abrirPagina(catalogo(), () => '');
  try {
    let responderPesquisa;
    window.fetch = (url) => url.includes('pagina=2')
      ? Promise.resolve({ ok: true, url, text: async () => catalogo({ pagina: 2 }) })
      : new Promise((resolve) => { responderPesquisa = () => resolve({ ok: true, url, text: async () => catalogo() }); });
    document.getElementById('referencia').form.requestSubmit();
    document.getElementById('outra').click();
    await esperar();
    responderPesquisa();
    await esperar();
    assert.match(document.getElementById('resultados').textContent, /Página 2/);
    assert.match(window.location.search, /pagina=2/);
    assert.equal(document.querySelector('[data-dmo-troca]').hasAttribute('aria-busy'), false);
    assert.deepEqual(navegou, []);
  } finally { dom.window.close(); }
});

function fichaComPagina({ pagina = 1, titulo = 'Ficha', campo = '' } = {}) {
  return `<main data-dmo-troca>
    <section id="ficha" data-dmo-regiao><h2 id="tituloFicha">${titulo}</h2>
      <input id="rascunho" value="${campo}">
      <div id="producoes" data-dmo-regiao>
        <span>${pagina}</span><nav data-dmo-parcial><a id="paginar" href="/JobOn?pagina_ref=${pagina + 1}">Seguinte</a></nav>
      </div>
    </section>
  </main>`;
}

test('paginar uma região interior mantém a ficha e o rascunho sem trocar o cartão', async () => {
  const { dom, window, document, navegou } = await abrirPagina(fichaComPagina(), () => fichaComPagina({ pagina: 2 }));
  try {
    const ficha = document.getElementById('ficha');
    const titulo = document.getElementById('tituloFicha');
    const campo = document.getElementById('rascunho');
    campo.value = 'rascunho em curso';
    document.getElementById('paginar').click();
    await esperar();
    assert.equal(document.querySelector('#producoes span').textContent, '2');
    assert.equal(document.getElementById('ficha'), ficha);
    assert.equal(document.getElementById('tituloFicha'), titulo);
    assert.equal(document.getElementById('rascunho'), campo);
    assert.equal(campo.value, 'rascunho em curso');
    assert.deepEqual(navegou, []);
  } finally { dom.window.close(); }
});

test('quando o invólucro muda, troca-o uma só vez com as regiões interiores corretas', async () => {
  const { dom, window, document, navegou } = await abrirPagina(fichaComPagina(), () => fichaComPagina({ pagina: 2, titulo: 'Ficha atualizada' }));
  try {
    const anterior = document.getElementById('ficha');
    document.getElementById('paginar').click();
    await esperar();
    assert.notEqual(document.getElementById('ficha'), anterior);
    assert.equal(document.getElementById('tituloFicha').textContent, 'Ficha atualizada');
    assert.equal(document.querySelector('#producoes span').textContent, '2');
    assert.equal(document.querySelectorAll('#producoes').length, 1);
    assert.deepEqual(navegou, []);
  } finally { dom.window.close(); }
});

// Integração (beta.179): uma paginação dentro de um bloco que fica não está
// numa zona; com data-dmo-completa abre a página inteira em vez de trocar nada.
test('um link dentro de data-dmo-completa abre a página inteira, sem pedir a troca', async () => {
  const html = `<main data-dmo-troca>
    <div data-dmo-fica><div data-dmo-completa><nav data-dmo-parcial><a id="pag" href="/JobOn?registos_pagina=2">→</a></nav></div></div>
    <div id="zona" data-dmo-regiao>Registo</div>
  </main>`;
  const { dom, document, pedidos } = await abrirPagina(html, () => html);
  try {
    const clique = new dom.window.MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    document.getElementById('pag').dispatchEvent(clique);
    await esperar();
    assert.equal(clique.defaultPrevented, false, 'o browser segue o link');
    assert.deepEqual(pedidos, []);
  } finally { dom.window.close(); }
});


function calendario({ mes = 1, dia = 1 } = {}) {
  return `<main data-dmo-troca><h1 id="fixo">Job On</h1>
    <div id="calendario" data-dmo-regiao data-dmo-parcial>
      <form method="get" action="/JobOn"><input name="ano" value="2026"><input name="mes" value="${mes + 1}"><button name="navegar" value="mes">Seguinte</button></form>
      <a id="outroDia" href="/JobOn?mes=${mes}&dia=2">2</a>
      ${dia ? `<a id="selecionado" href="/JobOn?mes=${mes}&dia=${dia}" data-dmo-calendar-unselect="/JobOn?mes=${mes}&dia=0">${dia}</a>` : ''}
    </div><div id="dia" data-dmo-regiao>${dia}</div>
    <form method="post" data-dmo-nativo data-dmo-regiao id="retirar"><input name="dia" value="${dia}"></form>
  </main>`;
}

test('o calendário navega por GET, inclui o botão e mantém o resto; dias novos continuam ligados', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirPagina(calendario(), url => {
    const params = new URL(url).searchParams;
    return calendario({ mes: Number(params.get('mes')), dia: Number(params.get('dia') ?? 1) });
  });
  try {
    const fixo = document.getElementById('fixo');
    const form = document.querySelector('#calendario form');
    form.dispatchEvent(new window.SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: form.querySelector('button') }));
    await esperar();
    assert.equal(new URL(pedidos[0]).searchParams.get('navegar'), 'mes');
    assert.match(window.location.search, /mes=2/);
    document.getElementById('outroDia').click();
    await esperar();
    assert.equal(document.getElementById('dia').textContent, '2');
    assert.equal(document.querySelector('#retirar input').value, '2', 'o formulário nativo acompanha o dia');
    // Um clique no dia escolhido tira a seleção (Diogo, 18/10/2026).
    document.getElementById('selecionado').click();
    await esperar();
    assert.equal(document.getElementById('dia').textContent, '0');
    assert.equal(document.getElementById('fixo'), fixo);
    assert.deepEqual(navegou, []);
  } finally { dom.window.close(); }
});

test('o clique no dia escolhido só tira o dia: nenhuma página o pode trocar por outra ação', async () => {
  const { dom, window, document, pedidos } = await abrirPagina(calendario(), () => calendario({ dia: 0 }));
  try {
    document.addEventListener('dmo:calendar-unselect', event => event.preventDefault());
    document.getElementById('selecionado').click();
    await esperar();
    assert.equal(pedidos.length, 1);
    assert.match(pedidos[0], /dia=0/);
    assert.equal(document.getElementById('selecionado'), null);
  } finally { dom.window.close(); }
});

test('o handler de consulta não interceta POSTs nem forms GET para outra página', async () => {
  const { dom, window, document, pedidos } = await abrirPagina(calendario(), () => '');
  try {
    const form = document.querySelector('#calendario form');
    // Na main, um POST dentro de data-dmo-parcial grava sem recarregar
    // (gravar); com data-dmo-nativo envia-se como sempre.
    form.method = 'post';
    form.setAttribute('data-dmo-nativo', '');
    assert.equal(form.dispatchEvent(new window.SubmitEvent('submit', { bubbles: true, cancelable: true })), true);
    form.removeAttribute('data-dmo-nativo');
    form.method = 'get'; form.action = '/OutraPagina';
    assert.equal(form.dispatchEvent(new window.SubmitEvent('submit', { bubbles: true, cancelable: true })), true);
    assert.deepEqual(pedidos, []);
  } finally { dom.window.close(); }
});

// Integração (beta.180): o handler de consulta (GET) do componente fica no
// window, depois dos ouvintes da página no document. Uma página que trata o
// seu GET (o Peso) chama ela o ir; o componente não pede outra vez.
test('um GET que a página já tratou não é pedido outra vez pelo componente', async () => {
  const { dom, window, document, pedidos } = await abrirPagina(calendario(), (url) => calendario({ mes: Number(new URL(url).searchParams.get('mes')) }));
  try {
    let daPagina = 0;
    document.addEventListener('submit', (event) => {
      daPagina++;
      event.preventDefault();
      window.DmoTroca.ir('/JobOn?ano=2026&mes=2', event.target);
    });
    const form = document.querySelector('#calendario form');
    form.dispatchEvent(new window.SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: form.querySelector('button') }));
    await esperar();
    assert.equal(daPagina, 1);
    assert.equal(pedidos.length, 1);
    assert.equal(new URL(pedidos[0]).searchParams.get('navegar'), null, 'o pedido é o da página');
  } finally { dom.window.close(); }
});


function resultadosResumo(pagina = 1) {
  return `<span id="paginaResumo">${pagina}</span><a id="resumoSeguinte"
    href="/JobOn?pagina=${pagina + 1}&reference=BQ"
    data-dmo-procura-pagina="/JobOn?handler=Procurar&q=BQ&pagina=${pagina + 1}">Mais antigas</a>`;
}

test('a paginação da procura pede só o endpoint parcial e mantém a folha e o campo', async () => {
  const inicial = pagina({ folhas: '<section id="folhaAberta">Ficha</section>' }).replace('<div id="alvo"></div>', `<div id="alvo">${resultadosResumo()}</div>`);
  const { dom, window, document, pedidos, navegou } = await abrirPagina(inicial, () => resultadosResumo(2));
  try {
    window.history.replaceState(null, '', '/JobOn?jobon_id=123');
    const campo = document.getElementById('campo');
    const folha = document.getElementById('folhaAberta');
    campo.value = 'BQ';
    document.getElementById('resumoSeguinte').click();
    await esperar();
    assert.equal(pedidos.length, 1);
    assert.match(pedidos[0], /handler=Procurar/);
    assert.match(pedidos[0], /pagina=2/);
    assert.equal(document.getElementById('paginaResumo').textContent, '2');
    assert.equal(document.getElementById('campo'), campo);
    assert.equal(document.getElementById('folhaAberta'), folha);
    assert.equal(new URL(window.location.href).searchParams.get('jobon_id'), '123');
    assert.deepEqual(navegou, []);
  } finally { dom.window.close(); }
});

test('escrever outra procura invalida imediatamente a paginação pendente', async () => {
  const inicial = pagina().replace('<div id="alvo"></div>', `<div id="alvo">${resultadosResumo()}</div>`);
  const { dom, window, document } = await abrirPagina(inicial, () => '');
  try {
    let responder;
    window.fetch = () => new Promise(resolve => { responder = resolve; });
    document.getElementById('resumoSeguinte').click();
    const campo = document.getElementById('campo');
    campo.value = 'OUTRA';
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
    responder({ ok: true, url: 'http://dmo.test/JobOn?handler=Procurar', text: async () => resultadosResumo(99) });
    await esperar();
    assert.equal(document.getElementById('paginaResumo').textContent, '1', 'a resposta antiga não substitui a pesquisa nova');
  } finally { dom.window.close(); }
});

test('uma recusa ao paginar a procura segue o link GET normal sem inserir outra página no bloco', async () => {
  const inicial = pagina().replace('<div id="alvo"></div>', `<div id="alvo">${resultadosResumo()}</div>`);
  const { dom, window, document, navegou } = await abrirPagina(inicial, () => '');
  try {
    window.fetch = async () => ({ ok: true, url: 'http://dmo.test/login', text: async () => '<h1>Login</h1>' });
    document.getElementById('resumoSeguinte').click();
    await esperar();
    assert.equal(navegou.length, 1);
    assert.equal(document.getElementById('paginaResumo').textContent, '1');
    assert.doesNotMatch(document.getElementById('alvo').textContent, /Login/);
  } finally { dom.window.close(); }
});

function conteudoBloco(pagina = 1) {
  return `<span id="paginaBloco">${pagina}</span><nav data-dmo-parcial data-dmo-handler="Resultados" data-dmo-alvo="avisos"><a id="blocoSeguinte" href="/JobOn?pagina=${pagina + 1}">Seguinte</a></nav>`;
}
function respostaBloco(html, id = 'avisos') {
  return { ok: true, url: 'http://dmo.test/JobOn?handler=Resultados', headers: { get: () => id }, text: async () => html };
}

test('o contrato de região pede o handler, troca só o alvo e mantém o endereço normal', async () => {
  const { dom, window, document, navegou } = await abrirPagina(pagina({ avisos: conteudoBloco() }), () => '');
  try {
    const campo = document.getElementById('campo');
    let pedido;
    window.fetch = async url => { pedido = url; return respostaBloco(`<div id="avisos" data-dmo-regiao>${conteudoBloco(2)}</div>`); };
    document.getElementById('blocoSeguinte').click();
    await esperar();
    assert.match(pedido, /handler=Resultados/);
    assert.equal(window.location.search, '?pagina=2');
    assert.equal(document.getElementById('paginaBloco').textContent, '2');
    assert.equal(document.getElementById('campo'), campo);
    // A fotografia atualizada permite voltar a uma troca da página inteira.
    window.fetch = async () => ({ ok: true, url: 'http://dmo.test/JobOn?jobon_id=1', text: async () => pagina({ avisos: conteudoBloco(2), folhas: FICHA }) });
    escolherProducao(window, '/JobOn?jobon_id=1');
    await esperar();
    assert.ok(document.getElementById('lightFicha'));
    assert.deepEqual(navegou, []);
  } finally { dom.window.close(); }
});

for (const [caso, body, header] of [
  ['cabeçalho de outro alvo', '<div id="avisos" data-dmo-regiao>2</div>', 'folhas'],
  ['id de outro alvo', '<div id="folhas" data-dmo-regiao>2</div>', 'avisos'],
  ['região sem marca', '<div id="avisos">2</div>', 'avisos'],
  ['id duplicado', '<div id="avisos" data-dmo-regiao><span id="avisos">2</span></div>', 'avisos'],
  ['mais de uma raiz', '<div id="avisos" data-dmo-regiao>2</div><p>Extra</p>', 'avisos'],
  ['script na região', '<div id="avisos" data-dmo-regiao><script>ligar()</script></div>', 'avisos'],
]) {
  test(`a resposta parcial inválida abre o GET normal: ${caso}`, async () => {
    const { dom, window, document, navegou } = await abrirPagina(pagina({ avisos: conteudoBloco() }), () => '');
    try {
      window.fetch = async () => respostaBloco(body, header);
      document.getElementById('blocoSeguinte').click();
      await esperar();
      assert.equal(navegou.length, 1);
      assert.equal(document.getElementById('paginaBloco').textContent, '1');
    } finally { dom.window.close(); }
  });
}

test('limpar filtros marcado para navegação completa mantém o GET nativo', async () => {
  const html = pagina({ avisos: '<div data-dmo-parcial><a id="limpar" data-dmo-completa href="/JobOn">Limpar</a></div>' });
  const { dom, window, document, pedidos } = await abrirPagina(html, () => '');
  try {
    const link = document.getElementById('limpar');
    const event = new window.MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    link.dispatchEvent(event);
    assert.equal(event.defaultPrevented, false);
    assert.deepEqual(pedidos, []);
  } finally { dom.window.close(); }
});

// O SERVIDOR DESENHA SÓ AS ZONAS DE UMA GRAVAÇÃO (E11, beta.189): um Resumo
// com duas áreas, os avisos e o «Visto por». O formulário está dentro de
// data-dmo-zonas: o POST leva X-Dmo-Zonas e o GET do redirect responde só com
// essas zonas e X-Dmo-Region igual.
function areaZonas(area, estado) {
  return `<div id="zona-${area}" data-dmo-regiao>
      <strong id="texto-${area}">${estado || 'não registado'}</strong>
      <form method="post" action="/Controlo/Resumo?handler=AreaState" id="form-${area}">
        <input type="hidden" name="area" value="${area}">
        <select id="estado-${area}" name="estado"><option value="">—</option><option value="OK">OK</option><option value="NOK">NOK</option></select>
        <button id="gravar-${area}">Gravar</button>
      </form>
    </div>`;
}

function zonasResumo(guardado, recusa = '') {
  return {
    'zona-CM': areaZonas('CM', guardado.CM),
    'zona-BQ': areaZonas('BQ', guardado.BQ),
    avisosZonas: `<div id="avisosZonas" data-dmo-regiao>${recusa}</div>`,
    vistoZonas: `<div id="vistoZonas" data-dmo-regiao><p id="visto">${guardado.vistos}</p></div>`,
  };
}

function paginaZonas(guardado, { fora = 'Produção 2026143', recusa = '', declaradas = 'avisosZonas vistoZonas' } = {}) {
  const z = zonasResumo(guardado, recusa);
  return `<main data-dmo-troca><h2 id="foraZonas">${fora}</h2>${z.avisosZonas}
    <div class="resumo-areas" data-dmo-parcial data-dmo-zonas="${declaradas}">${z['zona-CM']}${z['zona-BQ']}</div>${z.vistoZonas}</main>`;
}

// resposta(pedido) devolve { body, regiao } para o GET do redirect (o POST
// grava e é seguido pelo fetch, como no browser); por omissão, o servidor
// cumpre o contrato.
async function abrirZonas({ resposta = null, declaradas } = {}) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const guardado = { CM: '', BQ: '', vistos: 1, fora: 'Produção 2026143' };
  const dom = new JSDOM(`<!doctype html><title>Resumo</title><body>${paginaZonas(guardado, { declaradas })}</body>`,
    { url: 'http://dmo.test/Controlo/Resumo?jobon_id=9', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const pedidos = [];
  const volta = 'http://dmo.test/Controlo/Resumo?jobon_id=9#estado-documento';
  const contrato = (zonas) => {
    const z = zonasResumo(guardado);
    return { body: zonas.split(' ').map((id) => z[id]).join(''), regiao: zonas };
  };
  window.fetch = async (url, opcoes = {}) => {
    const zonas = (opcoes.headers || {})['X-Dmo-Zonas'] || null;
    pedidos.push({ metodo: opcoes.method || 'GET', zonas, corpo: String(opcoes.body || '') });
    if (opcoes.method === 'POST') {
      const corpo = new URLSearchParams(String(opcoes.body || ''));
      guardado[corpo.get('area')] = corpo.get('estado');
    }
    guardado.vistos++;
    const r = zonas ? (resposta ? await resposta({ zonas, guardado, contrato }) : contrato(zonas))
      : { body: `<!doctype html><title>Resumo</title><body>${paginaZonas(guardado, { fora: guardado.fora, declaradas })}</body>`, regiao: null };
    return {
      ok: true, redirected: opcoes.method === 'POST', url: volta,
      headers: { get: (nome) => ({ 'content-type': 'text/html; charset=utf-8', 'x-dmo-region': r.regiao })[nome.toLowerCase()] ?? null },
      text: async () => r.body,
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  return { dom, window, document: window.document, pedidos, navegou, guardado };
}

function gravarArea(window, area, estado) {
  const select = window.document.getElementById(`estado-${area}`);
  select.focus();
  select.value = estado;
  window.document.getElementById(`form-${area}`).requestSubmit(window.document.getElementById(`gravar-${area}`));
}

test('gravar com data-dmo-zonas: o servidor desenha só as zonas pedidas e só elas trocam', async () => {
  const { dom, window, document, pedidos, navegou } = await abrirZonas();
  try {
    const fora = document.getElementById('foraZonas');
    const outraArea = document.getElementById('zona-BQ');
    const historico = window.history.length;
    const trocas = [];
    document.addEventListener('dmo:troca', (evento) => trocas.push(evento.detail.zonas.join(' ')));
    gravarArea(window, 'CM', 'NOK');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.zonas}`), ['POST zona-CM avisosZonas vistoZonas'],
      'um POST com a zona do formulário e as declaradas; nenhum GET a mais');
    assert.equal(document.getElementById('texto-CM').textContent, 'NOK', 'o que se gravou aparece');
    assert.equal(document.getElementById('visto').textContent, '2', 'a zona declarada que mudou troca');
    assert.equal(document.getElementById('zona-BQ'), outraArea, 'a zona não pedida fica');
    assert.equal(document.getElementById('foraZonas'), fora, 'o resto não é desenhado de novo');
    assert.equal(document.title, 'Resumo', 'a resposta sem título não muda o título');
    assert.equal(document.activeElement.id, 'estado-CM', 'o foco fica no mesmo id');
    assert.equal(window.history.length, historico);
    assert.equal(window.location.hash, '#estado-documento', 'o endereço é o do redirect');
    assert.deepEqual(trocas, ['zona-CM vistoZonas']);

    // A fotografia ficou com as zonas novas: a gravação seguinte, noutra
    // área, troca essa e o «Visto por», e deixa a primeira como está.
    const primeira = document.getElementById('zona-CM');
    gravarArea(window, 'BQ', 'OK');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(document.getElementById('texto-BQ').textContent, 'OK');
    assert.equal(document.getElementById('zona-CM'), primeira);
    assert.equal(document.getElementById('visto').textContent, '3');
  } finally {
    dom.window.close();
  }
});

test('sem data-dmo-zonas, ou com o servidor a responder com a página inteira, troca-se como sempre', async () => {
  for (const [caso, opcoes] of [
    ['sem data-dmo-zonas', { declaradas: undefined }],
    ['servidor sem o contrato', { resposta: async ({ guardado }) => ({ body: `<!doctype html><title>Resumo</title><body>${paginaZonas(guardado)}</body>`, regiao: null }) }],
  ]) {
    const { dom, window, document, pedidos, navegou } = await abrirZonas(opcoes);
    try {
      if (caso === 'sem data-dmo-zonas') document.querySelector('[data-dmo-zonas]').removeAttribute('data-dmo-zonas');
      gravarArea(window, 'CM', 'OK');
      await esperar();
      assert.deepEqual(navegou, [], caso);
      assert.equal(pedidos.length, 1, `${caso}: um pedido`);
      assert.equal(pedidos[0].zonas, caso === 'sem data-dmo-zonas' ? null : 'zona-CM avisosZonas vistoZonas');
      assert.equal(document.getElementById('texto-CM').textContent, 'OK', caso);
      assert.equal(document.getElementById('visto').textContent, '2', caso);
    } finally {
      dom.window.close();
    }
  }
});

for (const [caso, resposta] of [
  ['X-Dmo-Region diferente do pedido', ({ contrato, zonas }) => ({ ...contrato(zonas), regiao: 'zona-CM' })],
  ['uma raiz a mais', ({ contrato, zonas }) => ({ ...contrato(zonas), body: contrato(zonas).body + '<p>extra</p>' })],
  ['uma zona sem data-dmo-regiao', ({ contrato, zonas }) => ({ ...contrato(zonas), body: contrato(zonas).body.replace('id="vistoZonas" data-dmo-regiao', 'id="vistoZonas"') })],
  ['as zonas por outra ordem', ({ contrato }) => ({ ...contrato('vistoZonas avisosZonas zona-CM'), regiao: 'zona-CM avisosZonas vistoZonas' })],
  ['um script numa zona', ({ contrato, zonas }) => ({ ...contrato(zonas), body: contrato(zonas).body.replace('<p id="visto">', '<script>x()</script><p id="visto">') })],
  ['a página inteira com X-Dmo-Region', ({ guardado, zonas }) => ({ body: `<!doctype html><body>${paginaZonas(guardado)}</body>`, regiao: zonas })],
]) {
  test(`gravar: a resposta só com as zonas não cumpre o contrato (${caso}): a página inteira com um GET, sem repetir o POST`, async () => {
    const { dom, window, document, pedidos, navegou } = await abrirZonas({ resposta });
    try {
      const fora = document.getElementById('foraZonas');
      gravarArea(window, 'CM', 'NOK');
      await esperar();
      assert.deepEqual(navegou, []);
      assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.zonas}`), ['POST zona-CM avisosZonas vistoZonas', 'GET null'],
        'o GET da página inteira vai sem X-Dmo-Zonas; o POST não se repete');
      assert.equal(document.getElementById('texto-CM').textContent, 'NOK');
      assert.equal(document.getElementById('foraZonas'), fora, 'troca-se como sempre, sem recarregar');
    } finally {
      dom.window.close();
    }
  });
}

test('gravar: o contrato falha e a página inteira também não troca (mudou fora das zonas): abre-se a página', async () => {
  const { dom, window, pedidos, navegou, guardado } = await abrirZonas({
    resposta: ({ contrato, zonas }) => ({ ...contrato(zonas), regiao: 'outra' }) });
  try {
    guardado.fora = 'Produção 2026144';
    gravarArea(window, 'CM', 'NOK');
    await esperar();
    assert.equal(navegou.length, 1, 'abre a página inteira');
    assert.equal(pedidos.filter((p) => p.metodo === 'POST').length, 1);
  } finally {
    dom.window.close();
  }
});

test('gravar com zonas: uma recusa vai para o canto e o envio seguinte, já em fila, lê as zonas quando parte', async () => {
  const recusa = '<div data-dmo-toast="erro" role="alert">Estado desconhecido. Nada foi gravado.</div>';
  let vez = 0;
  const { dom, window, document, pedidos, navegou } = await abrirZonas({
    resposta: async ({ contrato, zonas }) => {
      vez++;
      await esperar(10);
      const r = contrato(zonas);
      return vez === 1 ? { ...r, body: r.body.replace('<div id="avisosZonas" data-dmo-regiao></div>', `<div id="avisosZonas" data-dmo-regiao>${recusa}</div>`) } : r;
    } });
  try {
    gravarArea(window, 'CM', 'NOK');
    await esperar(2);
    // Enquanto o primeiro espera, a página passa a declarar outra lista.
    document.querySelector('[data-dmo-zonas]').setAttribute('data-dmo-zonas', 'vistoZonas');
    gravarArea(window, 'BQ', 'OK');
    await esperar(60);
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.zonas}`),
      ['POST zona-CM avisosZonas vistoZonas', 'POST zona-BQ vistoZonas'], 'cada envio pede as zonas que estão no ecrã quando parte');
    assert.match(document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]').textContent, /Estado desconhecido/);
    assert.equal(document.getElementById('texto-BQ').textContent, 'OK');
    assert.equal(document.getElementById('visto').textContent, '3');
  } finally {
    dom.window.close();
  }
});

// As linhas das produções (Diogo, 18/10/2026): um clique escolhe e abre o Job
// On, outro clique na mesma linha tira-o, um clique noutra linha troca.
function linhas(aberta = 0) {
  const linha = (id) => `<div class="linha${aberta === id ? ' selected' : ''}" id="l${id}" data-dmo-row data-id="${id}"
      data-dmo-open="/JobOn?ano=2026&mes=11&dia=26&jobon_id=${id}" data-dmo-fechar="/JobOn?ano=2026&mes=11&dia=26"
      aria-selected="${aberta === id}"><input type="radio" name="p" value="${id}"${aberta === id ? ' checked' : ''}></div>`;
  return `<div data-dmo-troca><div id="lista" data-dmo-regiao data-dmo-parcial data-dmo-list>${linha(1)}${linha(2)}</div>
    <div id="folhas" data-dmo-regiao>${aberta ? FICHA : ''}</div></div>`;
}

test('linhas das produções: um clique abre, outro na mesma tira, um noutra troca', async () => {
  const { dom, document, pedidos, navegou } = await abrirPagina(linhas(), (url) => {
    const id = Number(new URL(url, 'http://dmo.test').searchParams.get('jobon_id') ?? 0);
    return `<!doctype html><title>Job On</title><body>${linhas(id)}</body>`;
  });
  try {
    document.getElementById('l1').click();
    await esperar();
    assert.ok(document.getElementById('l1').classList.contains('selected'));
    assert.ok(document.getElementById('lightFicha'), 'a ficha abre');
    assert.match(pedidos.at(-1), /jobon_id=1/);

    document.getElementById('l1').click();
    await esperar();
    assert.ok(!document.getElementById('l1').classList.contains('selected'));
    assert.equal(document.getElementById('lightFicha'), null, 'a ficha fecha');
    assert.doesNotMatch(pedidos.at(-1), /jobon_id/);

    document.getElementById('l2').click();
    await esperar();
    document.getElementById('l1').click();
    await esperar();
    assert.ok(document.getElementById('l1').classList.contains('selected'));
    assert.ok(!document.getElementById('l2').classList.contains('selected'));
    assert.match(pedidos.at(-1), /jobon_id=1/);
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

// E10, Definições das Boquilhas: os nomes dos reparadores editam-se no próprio
// valor (data-dmo-valor, JSON) e criar um reparador troca a zona da lista. O
// valor editado no sítio liga-se por delegação: um campo que chega numa zona
// trocada também grava, e a zona troca-se sem abrir a página inteira.
function paginaReparadores(nomes, aviso = '') {
  const linhas = nomes.map((nome, i) => `<tr><td><input type="hidden" name="repairer_id" value="${i + 1}">`
    + `<input id="rep-${i + 1}" class="dmo-valor" data-dmo-valor name="name" value="${nome}"></td></tr>`).join('');
  // O espelho (data-dmo-espelho): o nome repete-se noutra lista fora do form,
  // e segue o campo em "change" (DMO-634).
  const espelhos = nomes.map((nome, i) => `<li data-dmo-espelho="rep-${i + 1}">${nome}</li>`).join('');
  return `<main data-dmo-troca><h2 id="titulo">Definições das Boquilhas</h2>
    <div id="bqAvisos" class="dmo-zona" data-dmo-regiao>${aviso}</div>
    <section id="bqReparadores" data-dmo-regiao data-dmo-parcial>
      <form method="post" action="/Boquilhas/Definicoes?handler=CreateRepairer">
        <input name="__RequestVerificationToken" type="hidden" value="tok">
        <input id="novo" name="name"><button id="criar">Criar reparador</button>
      </form>
      <form method="post" action="/Boquilhas/Definicoes?handler=EditRepairer">
        <input name="__RequestVerificationToken" type="hidden" value="tok">
        <table><tbody>${linhas}</tbody></table>
      </form>
      <ul id="bqEspelhos">${espelhos}</ul>
    </section>
  </main>`;
}

const DEFINICOES_BQ = 'http://dmo.test/Boquilhas/Definicoes';

function mudarValor(window, campo, valor) {
  campo.focus();
  campo.value = valor;
  campo.dispatchEvent(new window.Event('change', { bubbles: true }));
}

test('A06: editar atualiza a versão dos formulários da mesma lista e isola as outras listas', async () => {
  const lista = (id, versao) => `<section data-dmo-versao-grupo><table><tbody><tr><td>
    <form id="editar-${id}" action="/Controlo/Definicoes?handler=MudarPessoa" method="post">
      <input type="hidden" name="__RequestVerificationToken" value="tok">
      <input type="hidden" name="lista_id" value="${id}"><input type="hidden" name="versao" value="${versao}">
      <input id="pessoa-${id}" name="nome" data-dmo-valor value="Ana">
    </form></td><td><form><input type="hidden" name="versao" value="${versao}"></form></td></tr></tbody></table></section>`;
  const enviados = [];
  const { dom, window, document } = await abrirPu((url, opcoes) => {
    enviados.push(opcoes.body.getAll('versao'));
    return { tipo: 'application/json', body: JSON.stringify({ ok: true, valor: 'Ana Silva', versao: 'nova' }) };
  }, { html: lista(1, 'antiga') + lista(2, 'outra'), endereco: DEFINICOES_BQ });
  try {
    mudarValor(window, document.getElementById('pessoa-1'), 'Ana Silva');
    await esperar();
    assert.deepEqual(enviados, [['antiga']], 'o pedido só leva uma versão');
    assert.deepEqual([...document.querySelectorAll('[data-dmo-versao-grupo]')].map(grupo =>
      [...grupo.querySelectorAll('input[name="versao"]')].map(input => input.value)), [['nova', 'nova'], ['outra', 'outra']]);
    mudarValor(window, document.getElementById('pessoa-1'), 'Ana Maria');
    await esperar();
    assert.deepEqual(enviados, [['antiga'], ['nova']], 'a edição seguinte leva a versão atualizada');
  } finally { dom.window.close(); }
});

test('E10: criar um reparador troca a lista com os valores editáveis, sem abrir a página inteira', async () => {
  const nomes = ['Vidroart'];
  const desenhar = () => `<!doctype html><title>Definições</title><body>${paginaReparadores(nomes,
    '<p data-dmo-toast="ok" role="status">Definição gravada.</p>')}</body>`;
  const { dom, window, document, pedidos, navegou } = await abrirPu((url, opcoes) => {
    if (url.includes('handler=CreateRepairer')) {
      // O fetch segue o redirect (PRG): a resposta é a página do GET.
      nomes.push(new URLSearchParams(String(opcoes.body)).get('name'));
      return { url: `${DEFINICOES_BQ}?save=ok-reparador`, body: desenhar() };
    }
    if (url.includes('handler=EditRepairer')) {
      const corpo = new URLSearchParams();
      for (const [nome, valor] of opcoes.body.entries()) corpo.append(nome, valor);
      nomes[Number(corpo.get('repairer_id')) - 1] = corpo.get('name');
      return { tipo: 'application/json', body: JSON.stringify({ ok: true, valor: corpo.get('name') }) };
    }
    return { body: desenhar() };
  }, { html: paginaReparadores(nomes), endereco: DEFINICOES_BQ });
  try {
    const titulo = document.getElementById('titulo');
    const novo = document.getElementById('novo');
    novo.focus();
    novo.value = 'Moldes Norte';
    novo.form.requestSubmit(document.getElementById('criar'));
    novo.form.requestSubmit(document.getElementById('criar'));
    await esperar();
    assert.deepEqual(navegou, [], 'não abre a página inteira');
    assert.deepEqual(pedidos.map((p) => p.metodo), ['POST'], 'um POST só (o fetch segue o redirect)');
    assert.equal(document.getElementById('titulo'), titulo, 'o resto fica');
    assert.equal(document.getElementById('rep-2').value, 'Moldes Norte', 'o reparador novo aparece');
    assert.equal(document.getElementById('novo').value, '', 'o campo volta limpo');
    assert.equal(document.activeElement, document.getElementById('novo'), 'o foco fica no campo');
    assert.ok(document.querySelector('.dmo-toast-pilha [data-dmo-toast="ok"]'), 'a mensagem vai para o canto');
    assert.equal(window.location.search, '?save=ok-reparador');

    // O valor que chegou na zona nova grava no sítio (delegação), em JSON.
    const campo = document.getElementById('rep-2');
    mudarValor(window, campo, 'Moldes do Norte');
    await esperar();
    assert.equal(pedidos.length, 2);
    assert.equal(pedidos[1].url, `${DEFINICOES_BQ}?handler=EditRepairer`);
    assert.equal(nomes[1], 'Moldes do Norte');
    assert.equal(campo.defaultValue, 'Moldes do Norte', 'o valor gravado passa a ser o do campo');
    assert.equal(document.getElementById('rep-2'), campo, 'gravar no sítio não troca nada');

    // Esc desfaz o que se escreveu depois, para o último valor gravado.
    campo.value = 'Outro';
    campo.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    assert.equal(campo.value, 'Moldes do Norte');
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('E10: uma recusa ao editar no sítio vai para o canto e repõe o valor; a gravação seguinte tira-a', async () => {
  let resposta = { ok: false, aviso: 'Já existe um reparador com esse nome.' };
  const { dom, window, document, navegou } = await abrirPu(() => ({ tipo: 'application/json', body: JSON.stringify(resposta) }),
    { html: paginaReparadores(['Vidroart', 'Moldes']), endereco: DEFINICOES_BQ });
  try {
    const tabela = document.querySelector('table');
    const elementos = tabela.parentElement.querySelectorAll('*').length;
    const campo = document.getElementById('rep-2');
    mudarValor(window, campo, 'Vidroart');
    await esperar();
    assert.equal(campo.value, 'Moldes', 'o valor gravado volta');
    assert.equal(campo.getAttribute('aria-invalid'), 'true');
    const aviso = document.querySelector('.dmo-toast-pilha [data-dmo-toast="erro"]');
    assert.ok(aviso, 'a recusa vai para a pilha do canto');
    assert.match(aviso.textContent, /Já existe um reparador com esse nome\./);
    assert.equal(tabela.parentElement.querySelectorAll('*').length, elementos, 'nada entra no formulário');

    resposta = { ok: true, valor: 'Moldes Sul' };
    mudarValor(window, campo, 'Moldes Sul');
    await esperar();
    assert.equal(document.querySelector('[data-dmo-valor-recusa]'), null, 'a recusa antiga sai');
    assert.equal(campo.hasAttribute('aria-invalid'), false);
    assert.equal(campo.defaultValue, 'Moldes Sul');
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('DMO-634: uma recusa ao editar no sítio também repõe o espelho da linha', async () => {
  const resposta = { ok: false, aviso: 'Já existe um reparador com esse nome.' };
  const { dom, window, document } = await abrirPu(() => ({ tipo: 'application/json', body: JSON.stringify(resposta) }),
    { html: paginaReparadores(['Bia', 'Moldes']), endereco: DEFINICOES_BQ });
  try {
    const campo = document.getElementById('rep-1');
    const espelho = document.querySelector('[data-dmo-espelho="rep-1"]');
    assert.equal(espelho.textContent, 'Bia');

    mudarValor(window, campo, 'Ana');
    await esperar();

    assert.equal(campo.value, 'Bia', 'o campo volta ao valor antigo');
    assert.equal(espelho.textContent, 'Bia', 'o espelho da linha também volta, não fica em "Ana"');
  } finally {
    dom.window.close();
  }
});

// E8: a Revisão em miniatura. Os botões no canto do cartão enviam o
// formulário da decisão (form=), um deles com formaction; o cartão traz o
// copiar do link do mcaliper (data-dmo-copiar, ligado por delegação).
function paginaRevisao(estado) {
  return `<main data-dmo-troca><h1 id="titulo">Controlo</h1>
    <div id="revisaoResultados" data-dmo-regiao><a class="revisao-item">2026143 · ${estado}</a></div>
    <section id="revisaoDetalhe" data-dmo-regiao>
      <div class="dmo-card-topo"><h2>5447</h2><div class="dmo-card-topo-acoes">
        <span id="selo">${estado}</span>
        <button type="submit" form="revisaoDecisao" id="revisaoNaoAprovar" formaction="/Controlo/Approve?handler=NaoAprovar">Não aprovar</button>
        <button type="submit" form="revisaoDecisao" id="revisaoAprovar">Aprovar</button>
      </div></div>
      <button type="button" id="copiar" data-dmo-copiar="https://mcaliper.test/cm"><span data-dmo-copiar-label>CM</span></button>
      <form method="post" action="/Controlo/Approve?handler=Approve" data-dmo-parcial id="revisaoDecisao">
        <input type="hidden" name="jobon_id" value="9"><textarea id="revisao-nota" name="nota"></textarea>
      </form>
    </section>
  </main>`;
}

test('aprovar e não aprovar na Revisão: um POST, só as zonas trocam, o foco fica no botão e o copiar funciona na zona nova', async () => {
  const virtualConsole = new VirtualConsole();
  const navegou = [];
  virtualConsole.on('jsdomError', (error) => { if (/navigation/i.test(error.message)) navegou.push(error.message); });
  const dom = new JSDOM(`<!doctype html><title>Controlo</title><body>${paginaRevisao('Pendente')}</body>`,
    { url: 'http://dmo.test/Controlo/Approve?jobon_id=9&ano=2026&mes=10&dia=18', runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const { document } = window;
  const pedidos = [];
  let resposta = 'Aprovado';
  window.fetch = async (url, opcoes) => {
    pedidos.push(`${(opcoes && opcoes.method) || 'GET'} ${url}`);
    await esperar(5);
    return {
      ok: true, redirected: true, url: 'http://dmo.test/Controlo/Approve?jobon_id=9&ano=2026&mes=10&dia=18',
      headers: { get: (nome) => (nome === 'Content-Type' ? 'text/html; charset=utf-8' : null) },
      text: async () => `<!doctype html><title>Controlo</title><body>${paginaRevisao(resposta)}</body>`
    };
  };
  const copiados = [];
  window.prompt = (_, texto) => { copiados.push(texto); };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  try {
    const titulo = document.getElementById('titulo');
    const aprovar = document.getElementById('revisaoAprovar');
    aprovar.focus();
    const form = document.getElementById('revisaoDecisao');
    form.requestSubmit(aprovar);
    form.requestSubmit(aprovar); // o duplo clique
    await esperar(40);
    assert.deepEqual(navegou, [], 'não abre a página inteira');
    assert.deepEqual(pedidos, ['POST http://dmo.test/Controlo/Approve?handler=Approve'], 'um POST só');
    assert.equal(document.getElementById('selo').textContent, 'Aprovado', 'o estado novo aparece');
    assert.match(document.getElementById('revisaoResultados').textContent, /Aprovado/, 'a lista também troca');
    assert.notEqual(document.getElementById('revisaoAprovar'), aprovar, 'o cartão é o novo do servidor');
    assert.equal(document.activeElement.id, 'revisaoAprovar', 'o foco fica no botão');
    assert.equal(document.getElementById('titulo'), titulo, 'o resto não é desenhado de novo');

    // O copiar do cartão novo (antes obrigava a abrir a página inteira).
    document.getElementById('copiar').click();
    assert.deepEqual(copiados, ['https://mcaliper.test/cm']);

    resposta = 'Não aprovado';
    document.getElementById('revisao-nota').value = 'Rever';
    const naoAprovar = document.getElementById('revisaoNaoAprovar');
    naoAprovar.focus();
    document.getElementById('revisaoDecisao').requestSubmit(naoAprovar);
    await esperar(40);
    assert.deepEqual(navegou, []);
    assert.deepEqual(pedidos.slice(1), ['POST http://dmo.test/Controlo/Approve?handler=NaoAprovar'], 'o formaction do botão');
    assert.equal(document.getElementById('selo').textContent, 'Não aprovado');
    assert.equal(document.activeElement.id, 'revisaoNaoAprovar');
    assert.equal(document.getElementById('titulo'), titulo);
  } finally {
    dom.window.close();
  }
});

// Sweep de 19/10/2026, FE-8: o valor editado no sítio grava em fila por
// campo. Duas mudanças rápidas partiam juntas e o servidor podia gravá-las
// por outra ordem: o ecrã mostrava um nome e a base ficava com outro. As
// respostas ficam presas até o teste as soltar, como numa rede lenta.
function servidorDeReparadores(nomes) {
  const presos = [];
  const enviados = [];
  const responder = (_url, opcoes) => new Promise((resolve) => {
    const corpo = new URLSearchParams();
    for (const [nome, valor] of opcoes.body.entries()) corpo.append(nome, valor);
    enviados.push(corpo.get('name'));
    presos.push(() => {
      nomes[Number(corpo.get('repairer_id')) - 1] = corpo.get('name');
      resolve({ tipo: 'application/json', body: JSON.stringify({ ok: true, valor: corpo.get('name') }) });
    });
  });
  return { presos, enviados, responder };
}

test('FE-8: duas mudanças rápidas no mesmo valor gravam pela ordem, e a base e o campo ficam com a última', async () => {
  const nomes = ['Vidroart'];
  const servidor = servidorDeReparadores(nomes);
  const { dom, window, document, navegou } = await abrirPu(servidor.responder,
    { html: paginaReparadores(nomes), endereco: DEFINICOES_BQ });
  try {
    const campo = document.getElementById('rep-1');
    mudarValor(window, campo, 'Ana');
    mudarValor(window, campo, 'Bruno');
    await esperar();
    assert.deepEqual(servidor.enviados, ['Ana'], 'a segunda espera pela resposta à primeira');

    servidor.presos.shift()();
    await esperar();
    assert.equal(campo.value, 'Bruno', 'a resposta à primeira não põe o nome anterior no campo');
    assert.deepEqual(servidor.enviados, ['Ana', 'Bruno'], 'a segunda parte depois da resposta à primeira');

    servidor.presos.shift()();
    await esperar();
    assert.deepEqual(nomes, ['Bruno'], 'a base fica com a última');
    assert.equal(campo.value, 'Bruno');
    assert.equal(campo.defaultValue, 'Bruno', 'o valor gravado é o da base');
    assert.deepEqual(navegou, []);
  } finally {
    dom.window.close();
  }
});

test('FE-8: a resposta a um valor gravado não apaga o que o operador já escreve a seguir no campo', async () => {
  const nomes = ['Vidroart'];
  const servidor = servidorDeReparadores(nomes);
  const { dom, window, document } = await abrirPu(servidor.responder,
    { html: paginaReparadores(nomes), endereco: DEFINICOES_BQ });
  try {
    const campo = document.getElementById('rep-1');
    mudarValor(window, campo, 'Ana');
    await esperar();
    campo.value = 'Car';
    servidor.presos.shift()();
    await esperar();
    assert.equal(campo.value, 'Car', 'o que se escreve fica');
    assert.equal(campo.defaultValue, 'Ana', 'o valor gravado é o que o servidor gravou');
    assert.deepEqual(nomes, ['Ana']);
  } finally {
    dom.window.close();
  }
});

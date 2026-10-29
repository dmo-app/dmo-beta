const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A folha nova do Job On (Diogo, 22/10/2026): no Editar, um clique muda a
// ferramenta e o duplo clique leva à ficha; bloqueada, um clique leva à ficha.
async function folha(modo) {
  const procura = modo === 'editar'
    ? `<div class="jof-tool-procura" data-jof-tool-procura hidden><select name="cm_tool_id" data-facts-type="CM" data-dmo-filter>
         <option value="">Escreva</option><option value="T3" selected>5447 · Lote 3</option><option value="T9">5448 · Lote 09</option></select></div>`
    : '';
  const dom = new JSDOM(`<!doctype html><body><form data-jof-folha="${modo}">
    <input type="hidden" id="lightReference" value="5810T188" />
    <div class="jof-tool-slot" data-jof-tool-slot="CM">
      <button type="button" class="jof-tool" data-jof-tool="CM" data-jof-ficha="/Ferramentas/Ficha?tool_id=T3"><span class="hv"><b data-jof-tool-ref>5447</b></span><span class="hv"><b data-jof-tool-lote>3</b></span></button>
      ${procura}
    </div></form></body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
  const idas = [];
  dom.window.dmoIr = url => idas.push(url);
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
  return { window: dom.window, doc: dom.window.document, idas };
}
const espera = ms => new Promise(r => setTimeout(r, ms));
function duplo(window, el) {
  el.dispatchEvent(new window.MouseEvent('click', { bubbles: true }));
  el.dispatchEvent(new window.MouseEvent('click', { bubbles: true }));
  el.dispatchEvent(new window.MouseEvent('dblclick', { bubbles: true }));
}

// Bloqueada, um clique leva à ficha (Diogo, 28/10/2026: «abrir ferramenta com 1 clique em vez de 2»).
test('Bloqueada: um clique leva à ficha', async () => {
  const { window, doc, idas } = await folha('bloqueada');
  const botao = doc.querySelector('[data-jof-tool]');
  botao.click();
  assert.deepEqual(idas, ['/Ferramentas/Ficha?tool_id=T3']);
  window.close();
});

test('Editar: um clique abre a procura; o duplo clique leva à ficha sem abrir a procura', async () => {
  const { window, doc, idas } = await folha('editar');
  const botao = doc.querySelector('[data-jof-tool]');
  const procura = doc.querySelector('[data-jof-tool-procura]');
  duplo(window, botao);
  await espera(320);
  assert.equal(procura.hidden, true);
  assert.deepEqual(idas, ['/Ferramentas/Ficha?tool_id=T3']);
  botao.click();
  await espera(320);
  assert.equal(procura.hidden, false);
  window.close();
});

test('Editar: escolher outra ferramenta muda o cartão e a ficha do duplo clique', async () => {
  const { window, doc, idas } = await folha('editar');
  const select = doc.querySelector('select[name="cm_tool_id"]');
  assert.equal(typeof select.dmoSource, 'function');
  select.value = 'T9';
  select.dispatchEvent(new window.Event('change', { bubbles: true }));
  assert.equal(doc.querySelector('[data-jof-tool-ref]').textContent, '5448');
  // «Lote 9», sem o zero (Diogo, 22/10/2026).
  assert.equal(doc.querySelector('[data-jof-tool-lote]').textContent, '9');
  assert.equal(doc.querySelector('[data-jof-tool-procura]').hidden, true);
  duplo(window, doc.querySelector('[data-jof-tool]'));
  // A ficha leva o regresso à folha (o «Voltar ao Job On», Diogo 23/10/2026).
  assert.deepEqual(idas, ['/Ferramentas/Ficha?tool_id=T9&voltar=%2FJobOn%2FFolha%3Fjobon_id%3D1']);
  window.close();
});

test('O painel das linhas: «Linhas» abre, o × esconde, e a escolha fica no browser', async () => {
  const html = `<!doctype html><body><div class="rail-backdrop" data-jof-rail-fundo hidden></div>
    <aside class="rail" data-jof-rail aria-hidden="true" inert><button type="button" data-jof-rail-fechar>×</button><a class="rail-item selected" href="/JobOn/Folha?jobon_id=1">B2</a></aside>
    <button type="button" data-jof-rail-abrir aria-expanded="false">Linhas</button></body>`;
  const abrirPagina = async storage => {
    const dom = new JSDOM(html, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
    if (storage) Object.entries(storage).forEach(([k, v]) => dom.window.localStorage.setItem(k, v));
    dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
    if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
    return dom.window;
  };
  let w = await abrirPagina();
  const rail = w.document.querySelector('[data-jof-rail]');
  assert.equal(rail.classList.contains('open'), false);
  w.document.querySelector('[data-jof-rail-abrir]').click();
  assert.equal(rail.classList.contains('open'), true);
  assert.equal(w.document.querySelector('[data-jof-rail-fundo]').hidden, false);
  assert.equal(w.localStorage.getItem('dmo-jobon-folha-painel'), '1');
  w.document.querySelector('[data-jof-rail-fechar]').click();
  assert.equal(rail.classList.contains('open'), false);
  assert.equal(w.localStorage.getItem('dmo-jobon-folha-painel'), '0');
  w.close();
  w = await abrirPagina({ 'dmo-jobon-folha-painel': '1' });
  assert.equal(w.document.querySelector('[data-jof-rail]').classList.contains('open'), true);
  w.close();
});

test('Imprimir: abre a informação técnica e chama window.print(); o gancho dmoJobOnImprimir substitui-o', async () => {
  const html = `<!doctype html><body><form data-jof-folha="bloqueada"><button type="button" data-jof-imprimir>Imprimir</button>
    <details class="more" data-jof-tecnica="CM"><summary>Informação técnica</summary><p>x</p></details></form></body>`;
  const dom = new JSDOM(html, { url: 'http://dmo.test/JobOn/Folha?jobon_id=7', runScripts: 'outside-only' });
  let impressoes = 0;
  dom.window.print = () => { impressoes++; };
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
  const doc = dom.window.document;
  doc.querySelector('[data-jof-imprimir]').click();
  assert.equal(impressoes, 1);
  assert.equal(doc.querySelector('[data-jof-tecnica]').open, true);
  const pedidos = [];
  dom.window.dmoJobOnImprimir = id => pedidos.push(id);
  doc.querySelector('[data-jof-imprimir]').click();
  assert.deepEqual(pedidos, [7]);
  assert.equal(impressoes, 1);
  dom.window.close();
});

test('CS furos no Editar: um dígito de abertos e outro de fechados vão para o campo como «Abertos 4 / Fechados 2»', async () => {
  const html = `<!doctype html><body><form data-jof-folha="editar"><span data-jof-furos>
    <input type="hidden" name="Folha.cs_furos" value="4 × 4" data-jof-furos-valor />
    <input type="text" maxlength="1" data-jof-furos-abertos /><input type="text" maxlength="1" data-jof-furos-fechados /></span></form></body>`;
  const dom = new JSDOM(html, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1&editar=1', runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
  const d = dom.window.document;
  const valor = d.querySelector('[data-jof-furos-valor]');
  const a = d.querySelector('[data-jof-furos-abertos]');
  const f = d.querySelector('[data-jof-furos-fechados]');
  assert.equal(valor.value, '4 × 4');
  a.value = '4'; a.dispatchEvent(new dom.window.Event('input'));
  assert.equal(valor.value, 'Abertos 4');
  f.value = 'x2'; f.dispatchEvent(new dom.window.Event('input'));
  assert.equal(f.value, '2');
  assert.equal(valor.value, 'Abertos 4 / Fechados 2');
  dom.window.close();
});

// DMO-637: apagar os dois dígitos (em duas edições separadas) devia limpar o
// campo escondido; sem a correção, ficava com «Fechados 2» por trás.
test('CS furos no Editar: apagar abertos e depois fechados limpa o campo escondido cs_furos', async () => {
  const html = `<!doctype html><body><form data-jof-folha="editar"><span data-jof-furos>
    <input type="hidden" name="Folha.cs_furos" value="Abertos 4 / Fechados 2" data-jof-furos-valor />
    <input type="text" maxlength="1" value="4" data-jof-furos-abertos /><input type="text" maxlength="1" value="2" data-jof-furos-fechados /></span></form></body>`;
  const dom = new JSDOM(html, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1&editar=1', runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
  const d = dom.window.document;
  const valor = d.querySelector('[data-jof-furos-valor]');
  const a = d.querySelector('[data-jof-furos-abertos]');
  const f = d.querySelector('[data-jof-furos-fechados]');
  assert.equal(valor.value, 'Abertos 4 / Fechados 2');

  a.value = ''; a.dispatchEvent(new dom.window.Event('input'));
  assert.equal(valor.value, 'Fechados 2', 'só o abertos foi apagado');

  f.value = ''; f.dispatchEvent(new dom.window.Event('input'));
  assert.equal(valor.value, '', 'os dois vazios: o campo escondido também fica vazio, não com o valor antigo');
  dom.window.close();
});

test('Produções: lista fechada; abrir cai por cima; escolher outra muda a <select> e dispara change', async () => {
  const html = `<!doctype html><body><form data-jof-folha="bloqueada"><div class="pick-row">
    <select id="jofProducoes" data-dmo-ir aria-label="Produção"><option value="/JobOn/Folha?jobon_id=2" selected>202602 · B2</option><option value="/JobOn/Folha?jobon_id=1">202540 · C1</option></select>
    </div></form></body>`;
  const dom = new JSDOM(html, { url: 'http://dmo.test/JobOn/Folha?jobon_id=2', runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
  const d = dom.window.document;
  const sel = d.getElementById('jofProducoes');
  const botao = d.querySelector('[data-jof-producoes-abrir]');
  const lista = d.querySelector('.prod-list');
  assert.equal(sel.hidden, true);
  assert.equal(botao.textContent, '202602 · B2');
  assert.equal(lista.hidden, true);
  botao.click();
  assert.equal(lista.hidden, false);
  let mudou = null;
  sel.addEventListener('change', () => { mudou = sel.value; });
  lista.querySelectorAll('li')[1].click();
  assert.equal(mudou, '/JobOn/Folha?jobon_id=1');
  assert.equal(lista.hidden, true);
  dom.window.close();
});

test('Editar: no cartão vazio, escolher a CM mostra a referência e o lote e tira a CM do aviso', async () => {
  // Verificação antes da apresentação (25/10/2026): no Job On novo o cartão vazio só
  // tinha «Sem contra molde»; escolher a CM deixava o cartão e o aviso como estavam.
  const dom = new JSDOM(`<!doctype html><body><div class="jobon-aviso warn" data-jof-falta>Falta CM, BQ: o Job On precisa de CM, MF e BQ. Clique no cartão para escolher.</div>
    <form data-jof-folha="editar"><input type="hidden" id="lightReference" value="5447T173" />
    <div class="jof-tool-slot" data-jof-tool-slot="CM">
      <button type="button" class="tool-head tool-empty" data-jof-tool="CM"><span class="code">CM</span><span class="muted">Sem contra molde</span></button>
      <div class="jof-tool-procura" data-jof-tool-procura hidden><select name="cm_tool_id" data-facts-type="CM" data-dmo-filter>
        <option value="">Escreva</option><option value="T4">5447T173 · Lote 04</option></select></div>
    </div>
    <div class="jof-tool-slot" data-jof-tool-slot="BQ"><button type="button" class="tool-head tool-empty" data-jof-tool="BQ"><span class="code">BQ</span><span class="muted">Sem boquilha</span></button></div>
    </form></body>`, { url: 'http://dmo.test/JobOn/Folha?novo=1', runScripts: 'outside-only' });
  const { window } = dom;
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (window.document.readyState === 'loading') await new Promise(r => window.addEventListener('DOMContentLoaded', r));
  const doc = window.document;
  const select = doc.querySelector('select[name="cm_tool_id"]');
  select.value = 'T4';
  select.dispatchEvent(new window.Event('change', { bubbles: true }));
  const botao = doc.querySelector('[data-jof-tool="CM"]');
  assert.equal(botao.querySelector('[data-jof-tool-ref]').textContent, '5447T173');
  assert.equal(botao.querySelector('[data-jof-tool-lote]').textContent, '4');
  assert.equal(botao.querySelector('.muted'), null);
  assert.equal(botao.classList.contains('tool-empty'), false);
  assert.match(doc.querySelector('[data-jof-falta]').textContent, /^Falta BQ: o Job On precisa de CM, MF e BQ\./);
  window.close();
});

test('Editar: a procura da BQ leva a CM e a MF escolhidas, e a lista mostra a linha e o filtro', async () => {
  // O Diogo (26/10/2026): «um CM já inserido deveria filtrar com base no que faz
  // parte relacionando máquina e referência total».
  const dom = new JSDOM(`<!doctype html><body>
    <form data-jof-folha="editar"><input type="hidden" id="lightReference" value="5447T173" />
    <select id="lightMachine"><option value="1" selected>B1</option></select>
    <div class="jof-tool-slot" data-jof-tool-slot="CM"><button type="button" class="tool-head" data-jof-tool="CM"><span class="code">CM</span></button>
      <div class="jof-tool-procura" data-jof-tool-procura hidden><select name="cm_tool_id" data-facts-type="CM" data-dmo-filter><option value="">Escreva</option><option value="TCM" selected>5447 · Lote 12</option></select></div></div>
    <div class="jof-tool-slot" data-jof-tool-slot="MF"><button type="button" class="tool-head tool-empty" data-jof-tool="MF"><span class="code">MF</span><span class="muted">Sem molde</span></button>
      <div class="jof-tool-procura" data-jof-tool-procura hidden><select name="mf_tool_id" data-facts-type="MF" data-dmo-filter><option value="" selected>Escreva</option></select></div></div>
    <div class="jof-tool-slot" data-jof-tool-slot="BQ"><button type="button" class="tool-head tool-empty" data-jof-tool="BQ"><span class="code">BQ</span><span class="muted">Sem boquilha</span></button>
      <div class="jof-tool-procura" data-jof-tool-procura hidden><select name="bq_tool_id" data-facts-type="BQ" data-dmo-filter><option value="" selected>Escreva</option></select></div></div>
    </form></body>`, { url: 'http://dmo.test/JobOn/Folha?novo=1', runScripts: 'outside-only' });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async url => {
    pedidos.push(new URL(url, 'http://dmo.test'));
    return { ok: true, json: async () => ({ items: [{ toolId: 'TBQ', reference: 'T173', lot: '033', machineCodes: 'B1 · C2' }], total: 1, filtro: 'Filtrado por máquina e referência · só as relacionadas; escreva para ver mais 6' }) };
  };
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (window.document.readyState === 'loading') await new Promise(r => window.addEventListener('DOMContentLoaded', r));
  const doc = window.document;
  const bq = doc.querySelector('select[name="bq_tool_id"]');
  const resposta = await bq.dmoSource('');
  const pedido = pedidos[0];
  assert.equal(pedido.searchParams.get('type'), 'BQ');
  assert.equal(pedido.searchParams.get('cm_tool_id'), 'TCM', 'a CM escolhida vai na procura');
  assert.equal(pedido.searchParams.has('mf_tool_id'), false, 'sem MF escolhida, não vai');
  assert.equal(pedido.searchParams.has('bq_tool_id'), false, 'a própria BQ nunca vai');
  assert.equal(pedido.searchParams.get('machine_id'), '1');
  assert.equal(pedido.searchParams.get('reference'), '5447T173');
  assert.equal(JSON.stringify(resposta.items), JSON.stringify([{ value: 'TBQ', label: 'T173 · Lote 33 · B1 · C2' }]));
  assert.equal(resposta.note, 'Escreva para ver mais 6.');

  // Com um filtro normal, a nota não repete o filtro.
  window.fetch = async () => ({ ok: true, json: async () => ({ items: [], total: 0, filtro: 'Filtrado por máquina e referência' }) });
  assert.equal((await bq.dmoSource('T1')).note, '');
  window.close();
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A folha nova do Job On (Diogo, 22/10/2026): o duplo clique numa
// ferramenta leva à ficha (sempre); um clique só muda a ferramenta no Editar.
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

test('Bloqueada: um clique não faz nada; o duplo clique leva à ficha', async () => {
  const { window, doc, idas } = await folha('bloqueada');
  const botao = doc.querySelector('[data-jof-tool]');
  botao.click();
  await espera(320);
  assert.deepEqual(idas, []);
  duplo(window, botao);
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
  assert.deepEqual(idas, ['/Ferramentas/Ficha?tool_id=T9']);
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

test('CS furos no Editar: Aberto/Fechado + número vão para o campo como «Fechado · 4»', async () => {
  const html = `<!doctype html><body><form data-jof-folha="editar"><span data-jof-furos>
    <input type="hidden" name="Folha.cs_furos" value="4 × 4" data-jof-furos-valor />
    <select data-jof-furos-estado><option value=""></option><option>Aberto</option><option>Fechado</option></select>
    <input type="number" data-jof-furos-numero /></span></form></body>`;
  const dom = new JSDOM(html, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1&editar=1', runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
  const d = dom.window.document;
  const valor = d.querySelector('[data-jof-furos-valor]');
  assert.equal(valor.value, '4 × 4');
  d.querySelector('[data-jof-furos-estado]').value = 'Fechado';
  d.querySelector('[data-jof-furos-estado]').dispatchEvent(new dom.window.Event('change'));
  assert.equal(valor.value, 'Fechado');
  const n = d.querySelector('[data-jof-furos-numero]');
  n.value = '4'; n.dispatchEvent(new dom.window.Event('input'));
  assert.equal(valor.value, 'Fechado · 4');
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

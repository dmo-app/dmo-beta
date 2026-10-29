const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');
const espera = ms => new Promise(r => setTimeout(r, ms));

// Abre uma página com o jobon-folha.js carregado, como as de jobon-folha.test.cjs.
async function abrir(corpo) {
  const dom = new JSDOM(`<!doctype html><body>${corpo}</body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  if (dom.window.document.readyState === 'loading') await new Promise(r => dom.window.addEventListener('DOMContentLoaded', r));
  return { window: dom.window, doc: dom.window.document };
}
const clique = (window, el) => {
  const evento = new window.MouseEvent('click', { bubbles: true, cancelable: true });
  el.dispatchEvent(evento);
  return evento;
};

const FOLHA_COM_NOTAS = (modo) => `
  <form data-jof-folha="${modo}">
    <section class="sec" data-jof-familia="CM">
      <div class="tool-head">
        <span class="hv"><small>Referência</small><b>5809</b></span>
        <span class="hv"><small>Lote</small><b>1</b></span>
      </div>
      <div class="note-box" id="notaCm" data-jof-nota><span>Notas</span>${modo === 'editar' ? '<textarea id="textoCm">TRATAR · RODAR</textarea>' : '<p>TRATAR · RODAR</p>'}</div>
    </section>
    <section class="sec" data-jof-familia="MF">
      <div class="note-box" id="notaMf" data-jof-nota><span>Notas</span>${modo === 'editar' ? '<textarea id="textoMf">EMPARELHAR</textarea>' : '<p>EMPARELHAR</p>'}</div>
    </section>
  </form>
  <button type="button" id="fora">fora</button>`;

// DMO-509: a nota abre por cima, com o lugar guardado para o resto não subir.
test('a nota ampliada diz de que ferramenta é e deixa o lugar guardado no cartão', async () => {
  const { window, doc } = await abrir(FOLHA_COM_NOTAS('ver'));
  const nota = doc.getElementById('notaCm');
  clique(window, nota);
  assert.ok(nota.classList.contains('is-ampliada'));
  assert.equal(nota.querySelector(':scope > span').textContent, 'CM 5809 · Lote 1 · Notas');
  const lugar = nota.previousElementSibling;
  assert.ok(lugar && lugar.classList.contains('jof-nota-lugar'), 'o lugar da nota fica guardado antes dela');
  assert.equal(doc.querySelectorAll('.jof-nota-lugar').length, 1);
  nota.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
  assert.equal(nota.classList.contains('is-ampliada'), false);
  assert.equal(nota.querySelector(':scope > span').textContent, 'Notas');
  assert.equal(doc.querySelectorAll('.jof-nota-lugar').length, 0, 'fechada, o lugar guardado sai');
  window.close();
});

// DMO-558: com uma nota aberta, o clique fora só a fecha.
test('com uma nota ampliada, o clique noutra nota só fecha a primeira e não abre a outra', async () => {
  const { window, doc } = await abrir(FOLHA_COM_NOTAS('ver'));
  const cm = doc.getElementById('notaCm');
  const mf = doc.getElementById('notaMf');
  let recebeuOBotao = 0;
  doc.getElementById('fora').addEventListener('click', () => { recebeuOBotao++; });
  clique(window, cm);
  assert.ok(cm.classList.contains('is-ampliada'));

  const evento = clique(window, mf);
  assert.equal(cm.classList.contains('is-ampliada'), false, 'o clique fora fecha a primeira');
  assert.equal(mf.classList.contains('is-ampliada'), false, 'e não abre a segunda');
  assert.equal(evento.defaultPrevented, true);

  // Depois de fechada, um clique abre a outra; e o clique num botão por baixo só fecha.
  clique(window, mf);
  assert.ok(mf.classList.contains('is-ampliada'));
  clique(window, doc.getElementById('fora'));
  assert.equal(mf.classList.contains('is-ampliada'), false);
  assert.equal(recebeuOBotao, 0, 'o clique que fecha não carrega no que está por baixo');
  window.close();
});

test('com uma nota ampliada, o rato premido fora não põe o cursor noutra caixa', async () => {
  const { window, doc } = await abrir(FOLHA_COM_NOTAS('editar'));
  clique(window, doc.getElementById('notaCm'));
  const premir = new window.MouseEvent('mousedown', { bubbles: true, cancelable: true });
  doc.getElementById('textoMf').dispatchEvent(premir);
  assert.equal(premir.defaultPrevented, true);
  const dentro = new window.MouseEvent('mousedown', { bubbles: true, cancelable: true });
  doc.getElementById('textoCm').dispatchEvent(dentro);
  assert.equal(dentro.defaultPrevented, false, 'dentro da nota aberta o rato escreve normalmente');
  window.close();
});

test('no Editar, a nota abre pronta a escrever com o cursor no fim do texto', async () => {
  const { window, doc } = await abrir(FOLHA_COM_NOTAS('editar'));
  const texto = doc.getElementById('textoCm');
  clique(window, doc.getElementById('notaCm').querySelector(':scope > span'));
  assert.equal(doc.activeElement, texto);
  assert.equal(texto.selectionStart, texto.value.length);
  assert.equal(texto.selectionEnd, texto.value.length);
  // Escrever fica na caixa, que continua aberta, e o clique fora fecha sem apagar nada.
  texto.value += ' OK';
  clique(window, doc.getElementById('fora'));
  assert.equal(doc.getElementById('notaCm').classList.contains('is-ampliada'), false);
  assert.equal(texto.value, 'TRATAR · RODAR OK');
  window.close();
});

// DMO-562: a Informação técnica abre de cada vez e fecha ao clicar fora ou com Esc.
test('a Informação técnica abre de uma em uma, e o clique fora ou o Esc fecham-na', async () => {
  const { window, doc } = await abrir(`
    <form data-jof-folha="ver">
      <section class="sec"><details class="more" id="tCm"><summary>Informação técnica</summary><p>dentro CM</p></details></section>
      <section class="sec"><details class="more" id="tMf"><summary>Informação técnica</summary><p>dentro MF</p></details></section>
    </form><button type="button" id="fora">fora</button>`);
  const cm = doc.getElementById('tCm');
  const mf = doc.getElementById('tMf');
  cm.open = true;
  await espera(30);
  mf.open = true;
  await espera(30);
  assert.equal(mf.open, true);
  assert.equal(cm.open, false, 'abrir a segunda fecha a primeira');

  clique(window, mf.querySelector('p'));
  assert.equal(mf.open, true, 'um clique dentro não a fecha');
  clique(window, doc.getElementById('fora'));
  assert.equal(mf.open, false, 'um clique fora fecha-a');

  cm.open = true;
  await espera(30);
  doc.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
  assert.equal(cm.open, false, 'o Esc fecha-a');
  window.close();
});

// DMO-417 e DMO-495: os valores só quebram nas barras, nunca a meio de um número.
test('Calibres: «P 59.20 / M 58.60» quebra só na barra, em dois segmentos inteiros', async () => {
  const { window, doc } = await abrir(`
    <div class="jof2"><table data-jof-cal><tbody>
      <tr><td id="duas">P 59.20 / M 58.60</td></tr>
      <tr><td id="uma">34.95</td></tr>
    </tbody></table></div>
    <div class="jof2 editing"><table data-jof-cal><tbody><tr><td id="editar">P 59.20 / M 58.60</td></tr></tbody></table></div>`);
  const segmentos = [...doc.querySelectorAll('#duas .calv-seg')].map(s => s.textContent);
  assert.deepEqual(segmentos, ['P 59.20 /', 'M 58.60']);
  assert.equal(doc.getElementById('duas').textContent, 'P 59.20 / M 58.60', 'o texto lido é o mesmo');
  assert.equal(doc.querySelectorAll('#uma .calv-seg').length, 0, 'um só valor fica como está');
  assert.equal(doc.querySelectorAll('#editar .calv-seg').length, 0, 'no Editar a caixa de escrever não se mexe');
  window.close();
});

test('Dimensões compridas: só se pode quebrar depois de cada barra', async () => {
  const { window, doc } = await abrir(`
    <div class="jof2"><div class="jof-dim longo"><span class="jof-dim-valor" id="longo">54.02/52.02/54.02/44.10</span></div>
    <div class="jof-dim"><span class="jof-dim-valor" id="curto">117.2</span></div></div>
    <div class="jof2 editing"><div class="jof-dim longo"><span class="jof-dim-valor" id="editar">54.02/52.02</span></div></div>`);
  const longo = doc.getElementById('longo');
  assert.equal(longo.textContent, '54.02/52.02/54.02/44.10');
  assert.equal(longo.querySelectorAll('wbr').length, 3, 'uma quebra possível depois de cada barra');
  for (const wbr of longo.querySelectorAll('wbr')) assert.equal(wbr.previousSibling.nodeValue.endsWith('/'), true);
  assert.equal(doc.getElementById('curto').querySelectorAll('wbr').length, 0);
  assert.equal(doc.getElementById('editar').querySelectorAll('wbr').length, 0);
  window.close();
});

// DMO-559: no Editar, a procura de cada ferramenta dá o endereço da ficha ao triângulo.
test('Editar: a procura da CM dá à lista o endereço da ficha de cada ferramenta', async () => {
  const { window, doc } = await abrir(`
    <form data-jof-folha="editar">
      <input type="hidden" id="lightReference" value="5810T188" />
      <div data-jof-tool-slot="CM">
        <button type="button" data-jof-tool="CM" data-jof-ficha="/Ferramentas/Ficha?tool_id=T3"><span class="hv"><b data-jof-tool-ref>5447</b></span><span class="hv"><b data-jof-tool-lote>3</b></span></button>
        <div data-jof-tool-procura hidden><select name="cm_tool_id" data-dmo-filter><option value="">Escreva</option></select></div>
      </div>
    </form>`);
  const select = doc.querySelector('select[name="cm_tool_id"]');
  assert.equal(typeof select.dmoFicha, 'function');
  assert.equal(select.dmoFicha('CM 5809/1'), '/Ferramentas/Ficha?tool_id=CM%205809%2F1');
  window.close();
});

// DMO-395: um clique na procura da Referência seleciona-a toda.
test('um clique na procura da Referência seleciona-a toda, para o Backspace a apagar', async () => {
  const { window, doc } = await abrir(`
    <form data-jof-folha="editar"><input id="jofReferencia" value="5447T173"></form>`);
  const campo = doc.getElementById('jofReferencia');
  campo.focus();
  assert.equal(campo.selectionStart, 0);
  assert.equal(campo.selectionEnd, campo.value.length);
  const solto = new window.MouseEvent('mouseup', { bubbles: true, cancelable: true });
  campo.dispatchEvent(solto);
  assert.equal(solto.defaultPrevented, true, 'o clique que focou não desfaz a seleção');
  const outro = new window.MouseEvent('mouseup', { bubbles: true, cancelable: true });
  campo.dispatchEvent(outro);
  assert.equal(outro.defaultPrevented, false, 'com o campo já focado, o rato posiciona o cursor');
  window.close();
});

// DMO-410: a imagem do artigo é a da referência; sem ela, o desenho de reserva.
const ARTIGO = (referencia) => `
  <form data-jof-folha="ver">
    <label data-jof-artigo="${referencia}" data-jof-artigo-pode="1">
      <input type="file" data-jof-artigo-ficheiro>
      <img data-jof-artigo-img alt="Imagem do artigo" hidden>
      <svg data-jof-artigo-vazio viewBox="0 0 120 230" role="img" aria-label="Silhueta do artigo"></svg>
    </label>
    <button type="button" data-jof-artigo-retirar hidden>&times;</button>
    <p data-jof-artigo-aviso hidden></p>
  </form>`;

test('o artigo sem imagem mostra o desenho de reserva e não oferece retirar', async () => {
  const { window, doc } = await abrir(ARTIGO('9395T194'));
  await espera(30);
  assert.equal(doc.querySelector('[data-jof-artigo-img]').hidden, true);
  assert.notEqual(doc.querySelector('[data-jof-artigo-vazio]').style.display, 'none');
  assert.equal(doc.querySelector('[data-jof-artigo-retirar]').hidden, true);
  window.close();
});

test('o artigo com imagem da referência mostra-a no lugar do desenho e oferece retirar', async () => {
  const dom = new JSDOM(`<!doctype html><body>${ARTIGO('9395T194')}</body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=1', runScripts: 'outside-only' });
  const imagem = 'data:image/png;base64,iVBORw0KGgo=';
  dom.window.localStorage.setItem('dmo-jobon-artigo:9395T194', imagem);
  dom.window.localStorage.setItem('dmo-jobon-artigo:OUTRA', 'data:image/png;base64,AAAA');
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/jobon-folha.js'), 'utf8'));
  await espera(30);
  const doc = dom.window.document;
  assert.equal(doc.querySelector('[data-jof-artigo-img]').hidden, false);
  assert.equal(doc.querySelector('[data-jof-artigo-img]').getAttribute('src'), imagem, 'a imagem é a desta referência e não a de outra');
  assert.equal(doc.querySelector('[data-jof-artigo-vazio]').style.display, 'none');
  assert.equal(doc.querySelector('[data-jof-artigo-retirar]').hidden, false);

  // Retirar tira a imagem desta referência, volta o desenho e não mexe nas outras.
  doc.querySelector('[data-jof-artigo-retirar]').click();
  await espera(30);
  assert.equal(doc.querySelector('[data-jof-artigo-img]').hidden, true);
  assert.notEqual(doc.querySelector('[data-jof-artigo-vazio]').style.display, 'none');
  assert.equal(dom.window.localStorage.getItem('dmo-jobon-artigo:9395T194'), null);
  assert.equal(dom.window.localStorage.getItem('dmo-jobon-artigo:OUTRA'), 'data:image/png;base64,AAAA');
  dom.window.close();
});

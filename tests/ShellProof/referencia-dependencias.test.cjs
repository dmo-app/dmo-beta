const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A janela «Registar sem Job On» (bq-sem-jobon.js): DMO-412 (sugestões só
// depois de escrever, rótulo «BQ C002 · Lote 18»), DMO-572 (a Linha só com as
// da ficha da BQ, já escolhida) e DMO-573 (Job On anterior: referência,
// produção e a linha dela).

const HTML = (escolhida = '') => `<!doctype html><body><form>
  <label for="bqSemJobOnBq">BQ · lote</label>
  <select id="bqSemJobOnBq" name="tool_id" data-bq-escolha required>
    <option value="">Escolher</option>
    <option value="TC" data-procura="c002 lote 018 c1" data-linhas="C1 B2"${escolhida === 'TC' ? ' selected' : ''}>C002 · Lote 018 · Máq. C1, B2 · MF 5447</option>
    <option value="TD" data-procura="d004 lote 7 c3" data-linhas="C3"${escolhida === 'TD' ? ' selected' : ''}>D004 · Lote 7 · Máq. C3</option>
    <option value="TS" data-procura="s009 lote 2"${escolhida === 'TS' ? ' selected' : ''}>S009 · Lote 2</option>
  </select>
  <select id="bqSemJobOnLinha" name="movement_machine_id" required data-bq-linhas-da="bqSemJobOnBq">
    <option value="">—</option>
    <option value="1">B2</option><option value="2">C1</option><option value="3">C3</option><option value="4">B1</option>
  </select>
  <div data-bq-jobon-da="bqSemJobOnBq">
    <select id="bqSemJobOnReferencia" data-bq-referencias disabled>
      <option value="">Sem Job On</option>
      <option value="5447T1" data-bq="TC">5447T1</option>
      <option value="9000T2" data-bq="TC">9000T2</option>
      <option value="1111T3" data-bq="TD">1111T3</option>
    </select>
    <select id="bqSemJobOnProducao" name="bq_id" disabled data-bq-producoes>
      <option value="">—</option>
      <option value="11" data-bq="TC" data-referencia="5447T1" data-linha="4" hidden>202601 · 05/01/2026 · B1</option>
      <option value="12" data-bq="TC" data-referencia="5447T1" data-linha="2" hidden>202602 · 01/02/2026 · C1</option>
      <option value="13" data-bq="TC" data-referencia="9000T2" data-linha="1" hidden>202603 · 01/03/2026 · B2</option>
      <option value="14" data-bq="TD" data-referencia="1111T3" data-linha="3" hidden>202604 · 01/04/2026 · C3</option>
    </select>
  </div>
</form></body>`;

function abrir(escolhida) {
  const dom = new JSDOM(HTML(escolhida), { url: 'http://dmo.test/Boquilhas', runScripts: 'outside-only' });
  dom.window.Element.prototype.scrollIntoView = () => {};
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/bq-sem-jobon.js'), 'utf8'));
  const d = dom.window.document;
  const q = id => d.getElementById(id);
  const escrever = (texto) => {
    const input = q('bqSemJobOnBqProcura');
    input.value = texto;
    input.dispatchEvent(new dom.window.Event('input', { bubbles: true }));
  };
  const escolherNaLista = valor => {
    const li = [...d.querySelectorAll('#bqSemJobOnBqLista li')].find(x => x.dataset.value === valor);
    li.dispatchEvent(new dom.window.MouseEvent('mousedown', { bubbles: true }));
  };
  const trocar = (el, valor) => { el.value = valor; el.dispatchEvent(new dom.window.Event('change', { bubbles: true })); };
  const visiveis = sel => [...sel.options].filter(o => o.value && !o.hidden && !o.disabled).map(o => o.textContent.trim());
  return { dom, d, q, escrever, escolherNaLista, trocar, visiveis };
}

test('Escrever outra referência limpa a produção e as linhas da BQ anterior', () => {
  const { dom, q, escrever, escolherNaLista, trocar, visiveis } = abrir();
  escrever('c002'); escolherNaLista('TC');
  trocar(q('bqSemJobOnReferencia'), '5447T1');
  assert.notEqual(q('bqSemJobOnProducao').value, '');
  escrever('d004');
  assert.equal(q('bqSemJobOnBq').value, '');
  assert.equal(q('bqSemJobOnProducao').value, '');
  assert.equal(q('bqSemJobOnReferencia').value, '');
  assert.deepEqual(visiveis(q('bqSemJobOnLinha')), []);
  dom.window.close();
});

test('Retirar o Job On anterior repõe apenas as linhas da ficha da BQ', () => {
  const { dom, q, escrever, escolherNaLista, trocar, visiveis } = abrir();
  escrever('c002'); escolherNaLista('TC');
  trocar(q('bqSemJobOnReferencia'), '5447T1');
  assert.ok(visiveis(q('bqSemJobOnLinha')).includes('B1'));
  trocar(q('bqSemJobOnReferencia'), '');
  assert.deepEqual(visiveis(q('bqSemJobOnLinha')).sort(), ['B2','C1']);
  assert.equal(q('bqSemJobOnLinha').selectedOptions[0].textContent, 'C1');
  dom.window.close();
});

test('Uma ferramenta sem linhas configuradas não propõe linhas compatíveis', () => {
  const { dom, q, escrever, escolherNaLista, visiveis } = abrir();
  escrever('c002'); escolherNaLista('TC');
  assert.ok(visiveis(q('bqSemJobOnLinha')).length > 0);
  escrever('s009'); escolherNaLista('TS');
  assert.deepEqual(visiveis(q('bqSemJobOnLinha')), []);
  assert.equal(q('bqSemJobOnLinha').value, '');
  dom.window.close();
});

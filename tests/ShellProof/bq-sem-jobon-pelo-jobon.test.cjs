const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// «Registar sem Job On» pelo Job On já feito (DMO-719, Diogo 02/11/2026):
// escolher «referência · produção · BQ · Lote» preenche a BQ·lote, a
// referência, a produção e a linha dela.

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
  <input type="search" id="bqSemJobOnPorJobOn" list="lst" data-bq-por-jobon />
  <datalist id="lst"><option value="1111T3 · 202604 · BQ D004 · Lote 7" data-bq="TD" data-bq-id="14"></option><option value="5447T1 · 202602 · BQ C002 · Lote 018" data-bq="TC" data-bq-id="12"></option></datalist>
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

test('Escolher um Job On já feito preenche a BQ, a referência, a produção e a linha', () => {
  const dom = new JSDOM(HTML(''), { url: 'http://dmo.test/Boquilhas', runScripts: 'outside-only' });
  dom.window.Element.prototype.scrollIntoView = () => {};
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/bq-sem-jobon.js'), 'utf8'));
  const d = dom.window.document;
  const q = id => d.getElementById(id);
  const campo = q('bqSemJobOnPorJobOn');
  campo.value = '5447T1 · 202602 · BQ C002 · Lote 018';
  campo.dispatchEvent(new dom.window.Event('input', { bubbles: true }));
  assert.equal(q('bqSemJobOnBq').value, 'TC');
  assert.equal(q('bqSemJobOnBqProcura').value, 'BQ C002 · Lote 18');
  assert.equal(q('bqSemJobOnReferencia').value, '5447T1');
  assert.equal(q('bqSemJobOnProducao').value, '12');
  assert.equal(q('bqSemJobOnLinha').selectedOptions[0].textContent, 'C1');
  campo.value = '1111T3 · 202604 · BQ D004 · Lote 7';
  campo.dispatchEvent(new dom.window.Event('input', { bubbles: true }));
  assert.equal(q('bqSemJobOnBq').value, 'TD');
  assert.equal(q('bqSemJobOnProducao').value, '14');
  assert.equal(q('bqSemJobOnLinha').selectedOptions[0].textContent, 'C3');
  dom.window.close();
});

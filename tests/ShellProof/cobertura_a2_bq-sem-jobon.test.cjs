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

test('As sugestões da BQ só aparecem depois de escrever, e o rótulo é «BQ C002 · Lote 18»', () => {
  const { dom, q, escrever, escolherNaLista } = abrir();
  const lista = q('bqSemJobOnBqLista');
  const input = q('bqSemJobOnBqProcura');
  assert.equal(lista.hidden, true);
  input.dispatchEvent(new dom.window.Event('focus'));
  assert.equal(lista.hidden, true, 'focar sem escrever não abre a lista');

  escrever('c002');
  assert.equal(lista.hidden, false);
  const ao = [...lista.children].filter(li => !li.hidden);
  assert.deepEqual(ao.map(li => li.dataset.value), ['TC']);
  // O lote sem zeros à esquerda e a referência com «BQ» à frente; as máquinas ficam de fora (já estão na Linha).
  assert.equal(ao[0].querySelector('strong').textContent, 'BQ C002 · Lote 18');
  assert.equal(ao[0].querySelector('span').textContent, 'MF 5447');

  escrever('');
  assert.equal(lista.hidden, true, 'apagar o que se escreveu fecha a lista');

  escrever('BQ d004');
  assert.deepEqual([...lista.children].filter(li => !li.hidden).map(li => li.dataset.value), ['TD']);
  escolherNaLista('TD');
  assert.equal(q('bqSemJobOnBq').value, 'TD');
  assert.equal(input.value, 'BQ D004 · Lote 7');
  assert.equal(lista.hidden, true);
  dom.window.close();
});

test('Escolher a BQ deixa na Linha só as da ficha dela, com a primeira já escolhida', () => {
  const { dom, q, escrever, escolherNaLista, visiveis } = abrir();
  const linha = q('bqSemJobOnLinha');

  escrever('c002'); escolherNaLista('TC');
  // A ficha diz C1 e B2: só essas, e a primeira (C1) já vem escolhida.
  assert.deepEqual(visiveis(linha).sort(), ['B2', 'C1']);
  assert.equal(linha.selectedOptions[0].textContent, 'C1');

  // Com uma só linha na ficha, fica logo essa.
  escrever('d004'); escolherNaLista('TD');
  assert.deepEqual(visiveis(linha), ['C3']);
  assert.equal(linha.selectedOptions[0].textContent, 'C3');

  // Nova regra do Diogo (29/10/2026): sem associações não se presume compatibilidade.
  escrever('s009'); escolherNaLista('TS');
  assert.deepEqual(visiveis(linha), []);
  assert.equal(linha.value, '');
  dom.window.close();
});

test('Com a BQ já escolhida ao abrir, a Linha só tem as da ficha e vem com a primeira', () => {
  const { dom, q, visiveis } = abrir('TC');
  const linha = q('bqSemJobOnLinha');
  assert.deepEqual(visiveis(linha).sort(), ['B2', 'C1']);
  assert.equal(linha.selectedOptions[0].textContent, 'C1');
  dom.window.close();
});

test('Job On anterior: a BQ mostra as referências dela, a referência as produções, e a produção põe a linha do Job On', () => {
  const { dom, q, escrever, escolherNaLista, trocar, visiveis } = abrir();
  const referencia = q('bqSemJobOnReferencia');
  const producao = q('bqSemJobOnProducao');
  const linha = q('bqSemJobOnLinha');
  // Sem BQ, nada a escolher.
  assert.equal(referencia.disabled, true);
  assert.equal(producao.disabled, true);

  escrever('c002'); escolherNaLista('TC');
  assert.equal(referencia.disabled, false);
  assert.deepEqual(visiveis(referencia), ['5447T1', '9000T2']);
  assert.equal(referencia.value, '', 'por omissão, sem Job On');
  assert.equal(producao.disabled, true);
  assert.equal(producao.required, false);

  trocar(referencia, '5447T1');
  assert.deepEqual(visiveis(producao), ['202601 · 05/01/2026 · B1', '202602 · 01/02/2026 · C1']);
  assert.equal(producao.disabled, false);
  assert.equal(producao.required, true);
  // A primeira produção vem escolhida e põe a linha dela, mesmo que não seja da ficha da BQ (B1).
  assert.equal(producao.value, '11');
  assert.equal(linha.value, '4');
  assert.equal(linha.selectedOptions[0].textContent, 'B1');
  assert.equal(linha.selectedOptions[0].disabled, false);

  // Outra produção: a linha dela.
  trocar(producao, '12');
  assert.equal(linha.selectedOptions[0].textContent, 'C1');

  // Outra referência: só as produções dela.
  trocar(referencia, '9000T2');
  assert.deepEqual(visiveis(producao), ['202603 · 01/03/2026 · B2']);
  assert.equal(producao.value, '13');

  // Voltar a «Sem Job On»: a produção esvazia, fecha e deixa de ser obrigatória.
  trocar(referencia, '');
  assert.equal(producao.value, '');
  assert.equal(producao.disabled, true);
  assert.equal(producao.required, false);

  // Trocar a BQ limpa a referência e mostra as da outra BQ.
  trocar(referencia, '5447T1');
  escrever('d004'); escolherNaLista('TD');
  assert.equal(referencia.value, '');
  assert.deepEqual(visiveis(referencia), ['1111T3']);
  assert.equal(producao.disabled, true);
  dom.window.close();
});

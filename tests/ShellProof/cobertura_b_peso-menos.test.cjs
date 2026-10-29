const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// DMO-499 (Diogo, 27/10/2026): com uma só linha de leitura nova, o «−» é a
// opção para apagar a última leitura gravada: abre a pergunta da correção
// (nada se apaga sem confirmar). O Esc dentro da correção sai da correção.
async function folha(gravadas, url = 'http://dmo.test/Controlo/Peso?controlo_id=3&painel=1') {
  const dom = new JSDOM(`<!doctype html><body>
    <section data-peso-live data-peso-id="7" data-preview-url="/Controlo/Peso?handler=Preview">
      <div data-peso-strip></div><div data-peso-summary></div>
      <table data-peso-leituras><tbody>${gravadas.map((id) => `<tr data-peso-leitura-id="${id}"><td>CM ${id}</td></tr>`).join('')}</tbody></table>
      <form class="peso-leitura-form" data-peso-rows-form>
        <div data-peso-rows>
          <div data-peso-row><input name="cm_subject"><input name="water_weight"><div data-peso-row-result></div></div>
        </div>
        <button type="button" data-peso-add-row>+</button><button type="button" data-peso-remove-row>−</button>
      </form>
      <div data-peso-correcao><input id="campo"><button type="button" data-peso-correcao-sair>Cancelar</button></div>
    </section></body>`, { url, runScripts: 'outside-only' });
  const { window } = dom;
  window.fetch = async () => ({ ok: true, json: async () => ({ context: {}, totals: {}, lines: [] }) });
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'peso-live.js'), 'utf8'));
  await new Promise((resolve) => window.addEventListener('load', resolve, { once: true }));
  return dom;
}

test('«−» com uma só linha abre a pergunta de apagar a última leitura gravada, sem apagar nada', async () => {
  const dom = await folha([41, 42, 43]);
  try {
    const { window } = dom;
    const idas = [];
    window.DmoTroca = { ir: (href) => idas.push(href) };
    window.document.querySelector('[data-peso-remove-row]').click();

    assert.equal(idas.length, 1);
    const url = new URL(idas[0]);
    assert.equal(url.pathname, '/Controlo/Peso');
    assert.equal(url.searchParams.get('corrigir_leitura'), '43', 'a última gravada');
    assert.equal(url.searchParams.get('apagar_leitura'), '1');
    assert.equal(url.searchParams.get('controlo_id'), '3', 'fica na mesma folha');
    assert.equal(url.searchParams.get('painel'), '1', 'e no mesmo painel');
    assert.equal(window.document.querySelectorAll('[data-peso-leituras] tbody tr').length, 3, 'nada se apaga sem confirmar');
    assert.equal(window.document.querySelectorAll('[data-peso-row]').length, 1);
  } finally {
    dom.window.close();
  }
});

test('«−» com duas linhas só tira a linha vazia, sem perguntar nada', async () => {
  const dom = await folha([41, 42]);
  try {
    const { window } = dom;
    const idas = [];
    window.DmoTroca = { ir: (href) => idas.push(href) };
    window.document.querySelector('[data-peso-add-row]').click();
    assert.equal(window.document.querySelectorAll('[data-peso-row]').length, 2);
    window.document.querySelector('[data-peso-remove-row]').click();
    assert.equal(window.document.querySelectorAll('[data-peso-row]').length, 1);
    assert.deepEqual(idas, []);
  } finally {
    dom.window.close();
  }
});

test('«−» com uma só linha e nenhuma leitura gravada não faz nada', async () => {
  const dom = await folha([]);
  try {
    const { window } = dom;
    const idas = [];
    window.DmoTroca = { ir: (href) => idas.push(href) };
    window.document.querySelector('[data-peso-remove-row]').click();
    assert.deepEqual(idas, []);
    assert.equal(window.document.querySelectorAll('[data-peso-row]').length, 1);
  } finally {
    dom.window.close();
  }
});

test('Um pedido de apagar anterior não fica no endereço do novo', async () => {
  const dom = await folha([41, 42], 'http://dmo.test/Controlo/Peso?controlo_id=3&corrigir_leitura=41&apagar_leitura=1&correcao_cm=12&handler=X');
  try {
    const { window } = dom;
    const idas = [];
    window.DmoTroca = { ir: (href) => idas.push(href) };
    window.document.querySelector('[data-peso-remove-row]').click();
    const url = new URL(idas[0]);
    assert.equal(url.searchParams.get('corrigir_leitura'), '42');
    assert.equal(url.searchParams.getAll('apagar_leitura').length, 1);
    assert.equal(url.searchParams.get('correcao_cm'), null);
    assert.equal(url.searchParams.get('handler'), null);
  } finally {
    dom.window.close();
  }
});

test('Esc dentro da correção sai da correção e não chega ao resto da página', async () => {
  const dom = await folha([41]);
  try {
    const { window } = dom;
    const { document } = window;
    let saiu = 0;
    document.querySelector('[data-peso-correcao-sair]').addEventListener('click', () => { saiu++; });
    let chegouAoDocumento = 0;
    window.addEventListener('keydown', () => { chegouAoDocumento++; });
    const esc = new window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    document.getElementById('campo').dispatchEvent(esc);

    assert.equal(saiu, 1);
    assert.equal(esc.defaultPrevented, true);
    assert.equal(chegouAoDocumento, 0, 'no painel, o Esc não fecha o painel todo');

    // Fora da correção, o Esc não faz nada aqui.
    document.querySelector('[data-peso-strip]').dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    assert.equal(saiu, 1);
  } finally {
    dom.window.close();
  }
});

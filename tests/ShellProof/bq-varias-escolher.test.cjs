const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// BUG DO CARTÃO (Diogo, 11/11/2026, DMO-754): o cartão de uma linha abre o
// registo de várias linhas (?escolher=<bq_id>) com essa linha já escolhida,
// e o endereço fica limpo (recarregar não a escolhe outra vez).
function abrirPagina(url) {
  const dom = new JSDOM(`<!doctype html><body>
    <section id="registo">
      <aside id="bqCurrentLines"><div class="beta-production-lines">
        <button data-dmo-row data-id="12"><strong>B1</strong><span>BQ T194 · Lote 214</span></button>
        <button data-dmo-row data-id="13"><strong>C2</strong><span>BQ T200</span></button>
      </div></aside>
      <form id="bqVarias">
        <button type="submit" disabled>Registar</button>
        <label><input type="radio" name="movement_type" value="saida" checked /></label>
        <p class="bq-varias-vazio">Carregue nos cartões.</p>
        <div class="bq-varias-linhas"></div>
        <div id="bqVariasUltimos"></div>
      </form>
      <div id="bqRegistoAberto"></div>
    </section></body>`, { url, runScripts: 'outside-only', pretendToBeVisual: true });
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/bq-varias.js'), 'utf8'));
  return dom;
}

test('o cartão de outra vista chega com a linha dele já escolhida', async () => {
  const dom = await openAndCheck('http://dmo.test/Boquilhas?escolher=12');
  try {
    const d = dom.window.document;
    const linhas = [...d.querySelectorAll('.bq-varias-linha')].map((l) => l.dataset.id);
    assert.deepEqual(linhas, ['12']);
    assert.equal(d.querySelector('button[data-id="12"]').getAttribute('aria-pressed'), 'true');
    assert.equal(d.querySelector('.bq-varias-vazio').hidden, true);
    assert.equal(d.querySelector('#bqVarias button[type=submit]').disabled, false);
    assert.equal(dom.window.location.search, '');
  } finally {
    dom.window.close();
  }
});

test('sem ?escolher nenhuma linha vem escolhida', async () => {
  const dom = await openAndCheck('http://dmo.test/Boquilhas');
  try {
    const d = dom.window.document;
    assert.equal(d.querySelector('.bq-varias-vazio').hidden, false);
    assert.equal(d.querySelectorAll('.bq-varias-linha').length, 0);
  } finally {
    dom.window.close();
  }
});

// O bq-varias.js arranca no DOMContentLoaded: espera-se que a página acabe de carregar.
async function openAndCheck(url) {
  const dom = abrirPagina(url);
  if (dom.window.document.readyState === 'loading') {
    await new Promise((r) => dom.window.document.addEventListener('DOMContentLoaded', r));
  }
  assert.ok(dom.window.document.getElementById('bqVarias'));
  return dom;
}

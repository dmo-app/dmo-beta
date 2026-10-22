const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// «Ao clicar na ferramenta deveria ir para a página da mesma» (Diogo,
// 21/10/2026): no cartão da ferramenta do Job On, o nome e «Abrir ficha»
// levam à ficha da ferramenta escolhida e seguem a troca.
test('Cartão da ferramenta: o nome e «Abrir ficha» seguem a ferramenta escolhida', async () => {
  const dom = new JSDOM(`<!doctype html><body><form id="lightForm" method="post">
    <input id="lightReference" name="reference" value="1075C142" />
    <select id="lightMachine" name="machine_id"><option value="1" selected>B1</option></select>
    <div class="job-tool-filter-bar"><label><input type="checkbox" id="lightShowAllTools" /></label><span id="lightToolFilterStatus"></span></div>
    <div class="beta-light-tools" id="lightTools">
      <section class="job-tool-card" data-tool-card="CM">
        <details class="dmo-options job-tool-menu"><summary class="job-tool-card-head"><span data-tool-hint></span></summary>
          <div class="dmo-options-list">
            <a class="dmo-options-item" data-tool-open href="/Ferramentas/Ficha?tool_id=T3">Abrir ficha CM</a>
            <button type="button" class="dmo-options-item" data-tool-associate>Trocar CM</button>
            <button type="button" class="dmo-options-item" data-tool-clear>Retirar CM</button>
          </div>
        </details>
        <div class="job-tool-chosen" data-tool-chosen>
          <a class="job-tool-card-link" data-tool-open href="/Ferramentas/Ficha?tool_id=T3"><strong class="job-tool-card-title" data-tool-title>5447 · Lote 3</strong></a>
        </div>
        <div class="dmo-field job-tool-input" data-tool-search>
          <select name="cm_tool_id" data-facts-type="CM" data-dmo-filter>
            <option value="">Escreva a referência da CM</option>
            <option value="T3" selected="selected">5447 · Lote 3 · B1</option>
            <option value="N 9">NOVA · Lote 9 · B1</option>
          </select>
        </div>
      </section>
    </div></form></body>`, { url: 'http://dmo.test/JobOn?novo=1', runScripts: 'outside-only' });
  const { window } = dom;
  try {
    window.eval(fs.readFileSync(path.join(assets, 'js/jobon-tool-picker.js'), 'utf8'));
    const doc = window.document;
    if (doc.readyState === 'loading') await new Promise((pronto) => doc.addEventListener('DOMContentLoaded', pronto));
    const links = () => [...doc.querySelectorAll('[data-tool-open]')];
    const select = doc.querySelector('select[data-facts-type]');
    const escolher = (valor) => { select.value = valor; select.dispatchEvent(new window.Event('change', { bubbles: true })); };

    // A procura traz a ferramenta nova como opção, e o clique escolhe-a.
    const nova = doc.createElement('option');
    nova.value = 'N 9';
    nova.textContent = 'NOVA · Lote 9 · B1';
    select.append(nova);
    escolher('N 9');
    assert.equal(select.value, 'N 9');
    for (const link of links()) assert.equal(link.getAttribute('href'), '/Ferramentas/Ficha?tool_id=N%209');
    assert.equal(links()[0].hidden, false);

    doc.querySelector('[data-tool-clear]').click();
    for (const link of links()) assert.equal(link.hasAttribute('href'), false, 'sem ferramenta, nada para abrir');
    assert.equal(links()[0].hidden, true, '«Abrir ficha» esconde-se sem ferramenta');
  } finally {
    window.close();
  }
});

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// DMO-504 (Diogo, 27/10/2026): a faixa ao vivo escreve os números como o
// servidor (DmoNumberFormat): um inteiro sem casas, decimais sempre com
// duas, as diferenças com sinal («−» e duas casas); ao gravar, o valor não
// muda de largura nem de forma.
async function faixaCom(totals, context) {
  const dom = new JSDOM(`<!doctype html><body>
    <section data-peso-live data-peso-id="7" data-preview-url="/Controlo/Peso?handler=Preview">
      <div data-peso-strip></div><div data-peso-summary></div>
      <form class="peso-leitura-form" data-peso-rows-form>
        <div data-peso-rows><div data-peso-row><input id="cm" name="cm_subject"><input id="agua" name="water_weight"><div data-peso-row-result></div></div></div>
      </form>
    </section></body>`, { runScripts: 'outside-only' });
  const { window } = dom;
  window.fetch = async () => ({ ok: true, json: async () => ({
    context, totals, lines: [{ cmSubject: '12', status: 'ok', capacityCm3: 200, glassWeightGrams: 450.52 }]
  }) });
  window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
  window.eval(fs.readFileSync(path.join(assets, 'peso-live.js'), 'utf8'));
  await new Promise((resolve) => window.addEventListener('load', resolve, { once: true }));
  window.document.getElementById('cm').value = '12';
  const agua = window.document.getElementById('agua');
  agua.value = '199.64';
  agua.dispatchEvent(new window.Event('blur'));
  await new Promise((resolve) => setImmediate(resolve));
  return dom;
}

const texto = (dom) => ['[data-peso-strip]', '[data-peso-summary]']
  .map((s) => dom.window.document.querySelector(s).textContent).join(' ').replace(/\s+/g, ' ');

test('A faixa escreve inteiros sem casas e decimais com duas, como o servidor', async () => {
  const dom = await faixaCom(
    { sampleCount: 3, averageWaterWeightGrams: 150, averageCapacityCm3: 150.5, averageGlassWeightGrams: 325.4, desvioNominalG: -0.5 },
    { waterDensityGramsPerCm3: 0.9982, nominalWeightGrams: 200 });
  try {
    const t = texto(dom);
    assert.match(t, /150 g(?!\.)/, 'a média em água inteira, sem casas');
    assert.match(t, /150\.50 cm³/, 'a capacidade com duas casas, não três');
    assert.match(t, /325\.40 g/);
    assert.match(t, /200 g(?!\.)/, 'o peso nominal inteiro, sem casas');
    assert.match(t, /−0\.50 g/, 'a diferença com o sinal «−» e duas casas');
    assert.doesNotMatch(t, /150\.00|200\.00/);
  } finally {
    dom.window.close();
  }
});

test('A diferença nominal positiva leva «+» e duas casas, mesmo inteira', async () => {
  const dom = await faixaCom(
    { sampleCount: 1, averageWaterWeightGrams: 100.25, averageCapacityCm3: 100.5, averageGlassWeightGrams: 250, desvioNominalG: 50 },
    { waterDensityGramsPerCm3: 0.9982, nominalWeightGrams: 200 });
  try {
    const t = texto(dom);
    assert.match(t, /\+50\.00 g/);
    assert.match(t, /250 g(?!\.)/);
    assert.match(t, /100\.25 g/);
  } finally {
    dom.window.close();
  }
});

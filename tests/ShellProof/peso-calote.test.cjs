const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

// O APOIO TP/CALOTE AO ESCREVER (sweep do Codex, 19/10/2026, ponto 1): a
// calote simulada pede o cálculo ao servidor 300 ms depois da última tecla.
// A resposta de uma calote anterior que chegasse depois da atual mostrava os
// valores dela com a calote atual no campo; sem resposta, ficavam à vista os
// valores da calote anterior. O script real do Peso.cshtml, com um fetch de
// teste que responde quando o teste manda.
const PAGINA = path.resolve(__dirname, '../../src/DMO.Alpha.Web/Pages/Controlo/Peso.cshtml');
const cshtml = fs.readFileSync(PAGINA, 'utf8');

function scriptDaCalote() {
  const seccao = cshtml.slice(cshtml.indexOf('@section Scripts {')).replace(/@\*[\s\S]*?\*@/g, '');
  const bloco = [...seccao.matchAll(/<script>([\s\S]*?)<\/script>/g)]
    .map((m) => m[1])
    .find((script) => script.includes('.tp-support[data-cap-url]'));
  assert.ok(bloco, 'o script do apoio TP/calote está no Peso.cshtml');
  return bloco;
}

const HTML = `<!doctype html><title>Peso</title><body>
<details class="tp-support" data-cap-url="/Controlo/Peso?handler=CapSupport" data-cap-params="controlo_id=7" open>
  <summary class="tp-button"><span>TP / calote · Apoio separado</span></summary>
  <div class="tp-support-body">
    <div class="peso-facts" data-cap-result>
      <div class="peso-fact"><span>R calculado</span><strong>36.250 mm</strong></div>
      <div class="peso-fact"><span>Volume</span><strong>10.338 cm³</strong></div>
      <div class="peso-fact"><span>Δ R / referência</span><strong>0.000 mm</strong></div>
      <div class="peso-fact"><span>Δ V / referência</span><strong>0.000 cm³</strong></div>
    </div>
    <form method="get" class="tp-sim-form"><input id="capSimulada" name="cap_h" value="10" /></form>
  </div>
</details>
</body>`;

// O apoio que o servidor devolve para uma calote (só os campos que a página mostra).
const apoio = (raio, volume) => ({
  temReferencia: true, raioCalculadoMm: raio, volumeEfetivoCm3: volume, deltaRaioMm: raio - 36.25, deltaVolumeCm3: volume - 10.338
});

function abrir() {
  const dom = new JSDOM(HTML, { url: 'http://dmo.test/Controlo/Peso?controlo_id=7', runScripts: 'outside-only' });
  const { window } = dom;
  // Cada pedido fica à espera até o teste lhe dar a resposta.
  const pedidos = [];
  window.fetch = (url) => new Promise((resolve, reject) => {
    const calote = new URL(String(url), window.location.href).searchParams.get('calote');
    pedidos.push({
      calote,
      responder: (dados, status = 200) => resolve({ ok: status < 400, status, json: async () => dados }),
      falhar: () => reject(new window.TypeError('Failed to fetch'))
    });
  });
  window.eval(scriptDaCalote());
  const document = window.document;
  const campo = document.getElementById('capSimulada');
  const escrever = (valor) => {
    campo.value = valor;
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
  };
  // O texto como se vê (o browser junta os espaços seguidos).
  const valores = () => [...document.querySelectorAll('[data-cap-result] strong')].map((s) => s.textContent.replace(/\s+/g, ' ').trim());
  return { dom, pedidos, escrever, valores };
}

const esperar = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const DEPOIS_DA_ESPERA = 360;
const respostasAplicadas = () => esperar(10);

test('a resposta de uma calote anterior que chega depois da atual não substitui os valores da atual', async () => {
  const { dom, pedidos, escrever, valores } = abrir();
  try {
    escrever('5');
    await esperar(DEPOIS_DA_ESPERA);
    escrever('5,2');
    await esperar(DEPOIS_DA_ESPERA);
    assert.deepEqual(pedidos.map((p) => p.calote), ['5', '5.2'], 'um pedido por calote, com o ponto decimal');

    pedidos[1].responder(apoio(65.103, 5.201));
    await respostasAplicadas();
    assert.deepEqual(valores(), ['65.103 mm', '5.201 cm³', '+28.853 mm', '-5.137 cm³']);

    pedidos[0].responder(apoio(65.625, 5.0));
    await respostasAplicadas();
    assert.deepEqual(valores(), ['65.103 mm', '5.201 cm³', '+28.853 mm', '-5.137 cm³'], 'a resposta de 5 chegou tarde e não conta');
  } finally {
    dom.window.close();
  }
});

test('escrever outra vez durante a espera invalida logo o pedido que já saiu', async () => {
  const { dom, pedidos, escrever, valores } = abrir();
  try {
    escrever('5');
    await esperar(DEPOIS_DA_ESPERA);
    escrever('6');
    pedidos[0].responder(apoio(65.625, 5.0));
    await respostasAplicadas();
    assert.deepEqual(valores(), ['36.250 mm', '10.338 cm³', '0.000 mm', '0.000 cm³'], 'o 5 já não é o que está no campo');

    await esperar(DEPOIS_DA_ESPERA);
    assert.deepEqual(pedidos.map((p) => p.calote), ['5', '6']);
    pedidos[1].responder(apoio(55.083, 6.0));
    await respostasAplicadas();
    assert.deepEqual(valores(), ['55.083 mm', '6.000 cm³', '+18.833 mm', '-4.338 cm³']);
  } finally {
    dom.window.close();
  }
});

test('sem resposta, com erro do servidor ou sem apoio, os valores da calote anterior saem («—»)', async () => {
  const { dom, pedidos, escrever, valores } = abrir();
  const vazios = ['—', '—', '—', '—'];
  try {
    escrever('5');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[0].falhar();
    await respostasAplicadas();
    assert.deepEqual(valores(), vazios, 'sem ligação: não se sabe o apoio desta calote');

    escrever('6');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[1].responder(apoio(55.083, 6.0));
    await respostasAplicadas();
    assert.deepEqual(valores(), ['55.083 mm', '6.000 cm³', '+18.833 mm', '-4.338 cm³'], 'a tecla seguinte volta a calcular');

    escrever('7');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[2].responder(null, 500);
    await respostasAplicadas();
    assert.deepEqual(valores(), vazios, 'erro do servidor');

    escrever('8');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[3].responder({ temReferencia: false });
    await respostasAplicadas();
    assert.deepEqual(valores(), vazios, 'sem referência TP');

    // Uma falha de um pedido antigo não apaga os valores do atual.
    escrever('9');
    await esperar(DEPOIS_DA_ESPERA);
    escrever('9,5');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[5].responder(apoio(50.0, 7.0));
    await respostasAplicadas();
    pedidos[4].falhar();
    await respostasAplicadas();
    assert.deepEqual(valores(), ['50.000 mm', '7.000 cm³', '+13.750 mm', '-3.338 cm³']);
  } finally {
    dom.window.close();
  }
});

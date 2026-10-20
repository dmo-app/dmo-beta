const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

// OS DADOS E OS AVISOS DAS FERRAMENTAS NA FOLHA DO JOB ON (sweep de
// 19/10/2026): escolher uma ferramenta pede ao servidor os dados técnicos e os
// avisos 250 ms depois. A resposta de uma escolha anterior que chegasse depois
// da atual escrevia os dados e os avisos dela; sem resposta, ficavam os de
// outra escolha e a mesma escolha não voltava a ser pedida. O script real do
// Index.cshtml, com um fetch de teste que responde quando o teste manda.
const PAGINA = path.resolve(__dirname, '../../src/DMO.Alpha.Web/Pages/JobOn/Index.cshtml');
const cshtml = fs.readFileSync(PAGINA, 'utf8');

function scriptDosDados() {
  const seccao = cshtml.slice(cshtml.indexOf('@section Scripts {')).replace(/@\*[\s\S]*?\*@/g, '');
  const bloco = [...seccao.matchAll(/<script>([\s\S]*?)<\/script>/g)]
    .map((m) => m[1])
    .find((script) => script.includes('handler=ToolFacts'));
  assert.ok(bloco, 'o script dos dados das ferramentas está no Index.cshtml do Job On');
  return bloco;
}

const HTML = `<!doctype html><title>Job On</title><body data-beta-page="jobon">
<form id="lightForm" method="post">
  <select id="lightMachine" name="machine_id"><option value="">Selecionar</option><option value="3" selected>B2</option></select>
  <input id="lightStart" name="inicio_em" type="date" value="2026-10-20" />
  <input id="lightEnd" name="fim_em" type="date" value="2026-10-25" />
  <div class="beta-light-tools" id="lightTools">
    <section data-tool-card="CM"><div data-facts-slot="CM"></div>
      <select name="cm_tool_id" data-facts-type="CM"><option value="CM-1" selected>5447 · Lote 1</option></select></section>
    <section data-tool-card="MF"><div data-facts-slot="MF"></div>
      <select name="mf_tool_id" data-facts-type="MF"><option value="MF-1" selected>5447 · Lote 1</option></select></section>
    <section data-tool-card="BQ"><div data-facts-slot="BQ"></div>
      <select name="bq_tool_id" data-facts-type="BQ">
        <option value="BQ-X" selected>T194 · Lote 1</option><option value="BQ-Y">T194 · Lote 2</option>
        <option value="BQ-Z">T194 · Lote 3</option><option value="BQ-W">T194 · Lote 4</option>
      </select></section>
  </div>
  <details class="jobon-mais-detalhes"><summary>Mais detalhes (opcional)</summary><input id="lightCalote" name="calote_mm" type="number" /></details>
  <div id="lightToolWarnings" class="job-tool-warnings"></div>
</form>
</body>`;

// O que o servidor devolve para cada BQ (só os campos que a página mostra):
// a BQ-Y está noutra produção nas mesmas datas.
const PECAS = { 'BQ-X': 20, 'BQ-Y': 30, 'BQ-Z': 40, 'BQ-W': 50 };
const AVISO_Y = 'A BQ T194 lote 2 está na produção 2026999 (B2) nas mesmas datas.';
function dados(bq) {
  return [
    { type: 'CM', processo: 'NNPB', volumePuncaoCm3: 10, volumeMarisaCm3: null, diametroGargaloMm: 26, avisos: [] },
    { type: 'MF', processo: null, volumePuncaoCm3: null, volumeMarisaCm3: null, diametroGargaloMm: 26, avisos: [] },
    {
      type: 'BQ', processo: null, volumePuncaoCm3: null, volumeMarisaCm3: 11, diametroGargaloMm: 26, quantidade: PECAS[bq],
      avisos: bq === 'BQ-Y' ? [{ texto: AVISO_Y, links: [{ texto: 'Abrir 2026999 (B2)', href: '/JobOn?jobon_id=999' }] }] : []
    }
  ];
}

function abrir() {
  const dom = new JSDOM(HTML, { url: 'http://dmo.test/JobOn?novo=1', runScripts: 'outside-only' });
  const { window } = dom;
  // Cada pedido fica à espera até o teste lhe dar a resposta.
  const pedidos = [];
  window.fetch = (url) => new Promise((resolve, reject) => {
    const bq = new URL(String(url), window.location.href).searchParams.get('bq_tool_id');
    pedidos.push({
      bq,
      responder: (status = 200) => resolve({ ok: status < 400, status, json: async () => (status < 400 ? dados(bq) : null) }),
      falhar: () => reject(new window.TypeError('Failed to fetch'))
    });
  });
  window.eval(scriptDosDados());
  const document = window.document;
  // O script arranca no DOMContentLoaded: o do jsdom, se ainda não passou.
  if (document.readyState !== 'loading') document.dispatchEvent(new window.Event('DOMContentLoaded'));
  const escolherBq = (valor) => {
    const select = document.querySelector('select[data-facts-type="BQ"]');
    select.value = valor;
    select.dispatchEvent(new window.Event('change', { bubbles: true }));
  };
  // O texto como se vê (o browser junta os espaços seguidos).
  const lote = () => {
    const b = [...document.querySelectorAll('[data-facts-slot="BQ"] span')].find((s) => s.textContent.startsWith('Lote'));
    return b ? b.textContent.replace(/\s+/g, ' ').trim() : null;
  };
  const avisos = () => [...document.querySelectorAll('#lightToolWarnings .jobon-aviso')].map((a) => a.textContent.replace(/\s+/g, ' ').trim());
  return { dom, pedidos, escolherBq, lote, avisos };
}

const esperar = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const DEPOIS_DA_ESPERA = 300;
const respostasAplicadas = () => esperar(10);
const FALHA = 'Não foi possível obter os dados e os avisos das ferramentas. Tente de novo.';

// A folha abre com a BQ-X e pede logo os dados dela.
async function abertaComX() {
  const folha = abrir();
  await esperar(DEPOIS_DA_ESPERA);
  assert.deepEqual(folha.pedidos.map((p) => p.bq), ['BQ-X']);
  folha.pedidos[0].responder();
  await respostasAplicadas();
  assert.equal(folha.lote(), 'Lote 20 peças');
  return folha;
}

test('a resposta de uma escolha anterior que chega depois da atual não escreve os dados nem os avisos dela', async () => {
  const { dom, pedidos, escolherBq, lote, avisos } = await abertaComX();
  try {
    escolherBq('BQ-Y');
    await esperar(DEPOIS_DA_ESPERA);
    escolherBq('BQ-Z');
    await esperar(DEPOIS_DA_ESPERA);
    assert.deepEqual(pedidos.map((p) => p.bq), ['BQ-X', 'BQ-Y', 'BQ-Z'], 'um pedido por escolha');

    pedidos[2].responder();
    await respostasAplicadas();
    assert.equal(lote(), 'Lote 40 peças');
    assert.deepEqual(avisos(), []);

    pedidos[1].responder();
    await respostasAplicadas();
    assert.equal(lote(), 'Lote 40 peças', 'a resposta da BQ-Y chegou tarde e não conta');
    assert.deepEqual(avisos(), [], 'o aviso da BQ-Y não aparece com a BQ-Z escolhida');
  } finally {
    dom.window.close();
  }
});

test('a resposta tardia de uma escolha anterior não apaga o aviso da escolha atual', async () => {
  const { dom, pedidos, escolherBq, lote, avisos } = await abertaComX();
  try {
    escolherBq('BQ-Z');
    await esperar(DEPOIS_DA_ESPERA);
    escolherBq('BQ-Y');
    await esperar(DEPOIS_DA_ESPERA);

    pedidos[2].responder();
    await respostasAplicadas();
    assert.equal(lote(), 'Lote 30 peças');
    assert.deepEqual(avisos(), [AVISO_Y + ' Abrir 2026999 (B2)']);

    pedidos[1].responder();
    await respostasAplicadas();
    assert.equal(lote(), 'Lote 30 peças');
    assert.deepEqual(avisos(), [AVISO_Y + ' Abrir 2026999 (B2)'], 'a BQ-Y continua escolhida e o aviso dela fica');
  } finally {
    dom.window.close();
  }
});

test('sem resposta ou com erro do servidor, saem os dados e os avisos da escolha anterior e a mesma escolha volta a ser pedida', async () => {
  const { dom, pedidos, escolherBq, lote, avisos } = await abertaComX();
  try {
    escolherBq('BQ-Y');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[1].responder();
    await respostasAplicadas();
    assert.deepEqual(avisos(), [AVISO_Y + ' Abrir 2026999 (B2)']);

    escolherBq('BQ-W');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[2].responder(500);
    await respostasAplicadas();
    assert.equal(lote(), null, 'os dados da BQ-Y saem');
    assert.deepEqual(avisos(), [FALHA], 'o aviso da BQ-Y sai e fica a falha');

    escolherBq('BQ-W');
    await esperar(DEPOIS_DA_ESPERA);
    assert.deepEqual(pedidos.map((p) => p.bq), ['BQ-X', 'BQ-Y', 'BQ-W', 'BQ-W'], 'escolher outra vez volta a pedir');
    pedidos[3].falhar();
    await respostasAplicadas();
    assert.deepEqual(avisos(), [FALHA], 'sem rede, o mesmo');

    escolherBq('BQ-W');
    await esperar(DEPOIS_DA_ESPERA);
    pedidos[4].responder();
    await respostasAplicadas();
    assert.equal(lote(), 'Lote 50 peças');
    assert.deepEqual(avisos(), []);
  } finally {
    dom.window.close();
  }
});

test('a falha de um pedido que já não é o último não muda nada', async () => {
  const { dom, pedidos, escolherBq, lote, avisos } = await abertaComX();
  try {
    escolherBq('BQ-Y');
    await esperar(DEPOIS_DA_ESPERA);
    escolherBq('BQ-Z');
    await esperar(DEPOIS_DA_ESPERA);

    pedidos[1].falhar();
    await respostasAplicadas();
    assert.equal(lote(), 'Lote 20 peças', 'ainda nada chegou da BQ-Z');
    assert.deepEqual(avisos(), []);

    pedidos[2].responder();
    await respostasAplicadas();
    assert.equal(lote(), 'Lote 40 peças');
    assert.deepEqual(avisos(), []);
  } finally {
    dom.window.close();
  }
});

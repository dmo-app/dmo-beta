const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Revisão → Aprovar (teste do agente de 19/10/2026, T04fe55b): os filtros
// trocam só a lista; o calendário ao lado foi desenhado com os filtros de
// quando a página abriu. Um dia ou uma seta do mês têm de levar os filtros
// que estão escolhidos agora, e as datas «De»/«Até» não (o dia substitui-as).
function pagina() {
  return `<div data-dmo-troca>
    <section id="revisaoFiltros" data-dmo-regiao>
      <form id="revisaoFiltrosForm" method="get" action="/Controlo/Approve" data-dmo-parcial data-dmo-handler="Resultados" data-dmo-alvo="revisaoResultados">
        <input id="procura" type="search" name="procura" value="">
        <select id="estado" name="estado"><option value="">Todos</option><option value="pendente">Pendente</option></select>
        <select id="maquina" name="maquina"><option value="">Todas</option><option value="B2">B2</option></select>
        <input id="de" type="date" name="de" value="2026-10-01">
      </form>
    </section>
    <div class="card dmo-calendar" id="dmoCalendario" data-dmo-regiao data-dmo-parcial data-dmo-filtros="revisaoFiltrosForm procura estado maquina">
      <form method="get" action="/Controlo/Approve"><input type="hidden" name="maquina" value="C2"><input type="hidden" name="mes" value="11"><input type="hidden" name="ano" value="2026"><button id="seguinte" type="submit">›</button></form>
      <a id="dia19" href="/Controlo/Approve?maquina=C2&dia=19&ano=2026&mes=10">19</a>
    </div>
    <div id="revisaoResultados" data-dmo-regiao></div>
  </div>`;
}

function abrirPagina() {
  const dom = new JSDOM(`<!doctype html><title>Controlo</title><body>${pagina()}</body>`,
    { url: 'http://dmo.test/Controlo/Approve?maquina=C2', runScripts: 'outside-only' });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url) => {
    pedidos.push(new URL(String(url), window.location.href));
    return { ok: true, url: new URL(String(url), window.location.href).href, headers: { get: () => null },
      text: async () => `<!doctype html><title>Controlo</title><body>${pagina()}</body>` };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  return { dom, window, document: window.document, pedidos };
}

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

function escolher(document, window, id, valor) {
  document.getElementById(id).value = valor;
}

test('o dia do calendário leva os filtros escolhidos depois de a página abrir', async () => {
  const { dom, window, document, pedidos } = abrirPagina();
  try {
    escolher(document, window, 'estado', 'pendente');
    escolher(document, window, 'maquina', 'B2');
    document.getElementById('dia19').click();
    await esperar();
    assert.equal(pedidos.length, 1);
    const pedido = pedidos[0].searchParams;
    assert.equal(pedido.get('estado'), 'pendente');
    assert.equal(pedido.get('maquina'), 'B2');
    assert.equal(pedido.get('dia'), '19');
    assert.equal(pedido.get('mes'), '10');
    assert.equal(pedido.has('procura'), false, 'um filtro vazio não vai');
    assert.equal(pedido.has('de'), false, 'as datas não vão com o dia');
  } finally {
    dom.window.close();
  }
});

test('a seta do mês leva os filtros escolhidos; «Todas» tira a máquina', async () => {
  const { dom, window, document, pedidos } = abrirPagina();
  try {
    escolher(document, window, 'maquina', '');
    escolher(document, window, 'procura', 'Q902');
    document.getElementById('seguinte').click();
    await esperar();
    assert.equal(pedidos.length, 1);
    const pedido = pedidos[0].searchParams;
    assert.equal(pedido.has('maquina'), false);
    assert.equal(pedido.get('procura'), 'Q902');
    assert.equal(pedido.get('mes'), '11');
  } finally {
    dom.window.close();
  }
});

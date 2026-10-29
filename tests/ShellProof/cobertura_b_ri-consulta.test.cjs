const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// DMO-577: na Consulta da Reparação, a Produção mostra só o número (202603); aberta,
// cada produção tem o número e, por baixo, a referência, a linha e o início.
// Carregar numa pessoa do resumo escolhe-a no «Quem» e aplica.
const PAGINA = `
  <form id="riConsultaFiltros">
    <select id="riConsultaPessoa" name="pessoa">
      <option value="" selected>Todos</option><option value="u1">Ana</option><option value="u2">Rui</option>
    </select>
    <div id="riConsultaProducaoZona">
      <select id="riConsultaProducao" name="producao">
        <option value="" selected>Todas</option>
        <option value="11">5538 · 202603 · B1 · 02/10/2026</option>
        <option value="12">5538 · 202601 · C2 · 01/09/2026</option>
      </select>
    </div>
  </form>
  <ul>
    <li><a href="/ReparacaoInterna/Consultar?pessoa=u1" data-ri-pessoa="u1">Ana</a></li>
    <li><a href="/ReparacaoInterna/Consultar?pessoa=nao-existe" data-ri-pessoa="nao-existe">Fantasma</a></li>
  </ul>`;

function lerScript() {
  return fs.readFileSync(path.join(assets, 'js', 'ri-consulta.js'), 'utf8');
}

async function pagina() {
  const dom = new JSDOM(`<!doctype html><body>${PAGINA}</body>`, { runScripts: 'outside-only' });
  dom.window.eval(lerScript());
  return dom;
}

test('A Produção fechada mostra só o número; aberta, o resto por baixo de cada uma', async () => {
  const dom = await pagina();
  try {
    const { document } = dom.window;
    const botao = document.getElementById('riConsultaProducaoBotao');
    const lista = document.querySelector('.ri-prod-lista');
    assert.ok(botao, 'o botão da lista desenhada existe');
    assert.equal(botao.textContent, 'Todas');
    assert.equal(lista.hidden, true);

    botao.click();
    assert.equal(lista.hidden, false);
    assert.equal(botao.getAttribute('aria-expanded'), 'true');
    const opcoes = [...lista.querySelectorAll('li')];
    assert.deepEqual(opcoes.map((li) => li.querySelector('.ri-prod-numero').textContent), ['Todas', '202603', '202601']);
    assert.equal(opcoes[0].querySelector('.ri-prod-resto'), null, '«Todas» não tem resto');
    assert.equal(opcoes[1].querySelector('.ri-prod-resto').textContent, '5538 · B1 · 02/10/2026');
    assert.equal(opcoes[2].querySelector('.ri-prod-resto').textContent, '5538 · C2 · 01/09/2026');
  } finally {
    dom.window.close();
  }
});

test('Escolher uma produção fecha a lista, fica só o número e o select muda e avisa', async () => {
  const dom = await pagina();
  try {
    const { document } = dom.window;
    const sel = document.getElementById('riConsultaProducao');
    let mudou = 0;
    sel.addEventListener('change', () => { mudou++; });
    const botao = document.getElementById('riConsultaProducaoBotao');
    botao.click();
    document.querySelectorAll('.ri-prod-lista li')[2].click();

    assert.equal(sel.value, '12');
    assert.equal(mudou, 1);
    assert.equal(botao.textContent, '202601', 'fechada, só o número');
    assert.equal(document.querySelector('.ri-prod-lista').hidden, true);
    assert.equal(sel.hidden, true, 'o select fica escondido, é a lista que se vê');
  } finally {
    dom.window.close();
  }
});

test('Esc fecha a lista aberta sem escolher nada', async () => {
  const dom = await pagina();
  try {
    const { document, KeyboardEvent } = dom.window;
    const botao = document.getElementById('riConsultaProducaoBotao');
    botao.click();
    assert.equal(document.querySelector('.ri-prod-lista').hidden, false);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    assert.equal(document.querySelector('.ri-prod-lista').hidden, true);
    assert.equal(document.getElementById('riConsultaProducao').value, '');
  } finally {
    dom.window.close();
  }
});

test('Carregar numa pessoa do resumo escolhe-a no «Quem» e aplica; uma que não está na lista segue o link', async () => {
  const dom = await pagina();
  try {
    const { document, MouseEvent } = dom.window;
    const quem = document.getElementById('riConsultaPessoa');
    let mudou = 0;
    quem.addEventListener('change', () => { mudou++; });

    const ana = document.querySelector('a[data-ri-pessoa="u1"]');
    const clique = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    ana.dispatchEvent(clique);
    assert.equal(quem.value, 'u1');
    assert.equal(mudou, 1);
    assert.equal(clique.defaultPrevented, true, 'não abre o link: aplica no mesmo sítio');

    const fantasma = document.querySelector('a[data-ri-pessoa="nao-existe"]');
    const outro = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    fantasma.dispatchEvent(outro);
    assert.equal(outro.defaultPrevented, false, 'sem essa pessoa no «Quem», o link segue');
    assert.equal(quem.value, 'u1');
    assert.equal(mudou, 1);

    // Com Ctrl carregado, o link abre à parte, como sempre.
    const comCtrl = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ctrlKey: true });
    document.querySelector('a[data-ri-pessoa="u1"]').dispatchEvent(comCtrl);
    assert.equal(comCtrl.defaultPrevented, false);
  } finally {
    dom.window.close();
  }
});

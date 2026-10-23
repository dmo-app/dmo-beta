// Histórico das ferramentas (A-0053): clicar numa linha abre o menu dela
// (Controlo · Reparação · Folha do Job On), um segundo clique ou um clique
// fora fecha-o, e abrir outra linha fecha a primeira. O script é o da
// página, tirado de Pages/Ferramentas/Historico.cshtml.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const pagina = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/Pages/Ferramentas/Historico.cshtml'), 'utf8');
const script = (pagina.match(/<script>((?:(?!<\/script>)[\s\S])*?histLista[\s\S]*?)<\/script>/) || [])[1];

const linha = (id) => `<tr data-hist-linha="${id}">
  <td>5447T173</td>
  <td><details class="dmo-options hist-menu"><summary>20260${id}</summary>
    <div class="dmo-options-list" role="menu">
      <a class="dmo-options-item" href="/Controlo/Resumo?jobon_id=${id}">Controlo</a>
      <a class="dmo-options-item" href="/ReparacaoInterna/Consultar?referencia=5447&amp;producao=${id}">Reparação</a>
      <a class="dmo-options-item" href="/JobOn?jobon_id=${id}">Folha do Job On</a>
    </div></details></td><td>B1</td>
  <td id="data-${id}">01/03/2025 – 09/03/2025</td><td>9</td><td>8 %</td></tr>`;

function abrir() {
  const dom = new JSDOM(`<!doctype html><body><h1 id="fora">Histórico</h1><table id="histLista"><tbody>${linha(1)}${linha(2)}</tbody></table></body>`,
    { url: 'http://dmo.test/Ferramentas/Historico?tool_id=T1', runScripts: 'outside-only' });
  dom.window.eval(script);
  return dom.window;
}

const clicar = (w, el) => el.dispatchEvent(new w.MouseEvent('click', { bubbles: true }));
const aberto = (w, id) => w.document.querySelector(`tr[data-hist-linha="${id}"] details`).open;

test('o script da página foi encontrado', () => assert.ok(script));

test('clicar na linha abre o menu com os três destinos desse Job On', () => {
  const w = abrir();
  clicar(w, w.document.getElementById('data-1'));
  assert.equal(aberto(w, 1), true);
  const links = [...w.document.querySelectorAll('tr[data-hist-linha="1"] .dmo-options-list a')].map(a => [a.textContent, a.getAttribute('href')]);
  assert.deepEqual(links, [
    ['Controlo', '/Controlo/Resumo?jobon_id=1'],
    ['Reparação', '/ReparacaoInterna/Consultar?referencia=5447&producao=1'],
    ['Folha do Job On', '/JobOn?jobon_id=1']
  ]);
  assert.equal(w.document.querySelectorAll('table button').length, 0);
});

test('um segundo clique na linha fecha o menu', () => {
  const w = abrir();
  clicar(w, w.document.getElementById('data-1'));
  clicar(w, w.document.getElementById('data-1'));
  assert.equal(aberto(w, 1), false);
});

test('um clique fora da tabela fecha o menu', () => {
  const w = abrir();
  clicar(w, w.document.getElementById('data-1'));
  clicar(w, w.document.getElementById('fora'));
  assert.equal(aberto(w, 1), false);
});

test('abrir outra linha fecha a primeira', () => {
  const w = abrir();
  clicar(w, w.document.getElementById('data-1'));
  clicar(w, w.document.getElementById('data-2'));
  assert.equal(aberto(w, 1), false);
  assert.equal(aberto(w, 2), true);
});

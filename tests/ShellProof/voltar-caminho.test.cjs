const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// O VOLTAR PASSO A PASSO (Diogo, 28/10/2026: «voltar sempre às páginas
// anteriores independentemente de ser 1 ou mais páginas que navegamos»): o
// «← Voltar» anda para trás no caminho feito no separador, um passo de cada
// vez; a mesma página com outros filtros substitui-se; o painel não conta.
const links = '<a id="voltar" href="/Servidor" data-dmo-voltar="x">Voltar</a>'
  + '<a id="canto" class="dmo-voltar-texto" href="/Servidor">← Voltar</a>';

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

// Um separador: cada abrir é uma página nova com o mesmo sessionStorage.
function separador() {
  let guardado = {};
  let anterior;
  async function abrir(endereco, referrer = anterior) {
    const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${links}</body>`,
      { url: 'https://dmo.test' + endereco, referrer: referrer && 'https://dmo.test' + referrer, runScripts: 'outside-only' });
    const { window } = dom;
    Object.entries(guardado).forEach(([k, v]) => window.sessionStorage.setItem(k, v));
    window.eval(fs.readFileSync(path.join(assets, 'dmo-live.js'), 'utf8'));
    await esperar();
    guardado = {};
    for (let i = 0; i < window.sessionStorage.length; i++) {
      const k = window.sessionStorage.key(i); guardado[k] = window.sessionStorage.getItem(k);
    }
    anterior = endereco;
    const voltar = window.document.getElementById('voltar').getAttribute('href');
    assert.equal(window.document.getElementById('canto').getAttribute('href'), voltar, 'o Voltar do canto vai ao mesmo sítio');
    return voltar;
  }
  return { abrir };
}

test('três páginas seguidas: o Voltar recua uma de cada vez até à primeira', async () => {
  const s = separador();
  assert.equal(await s.abrir('/Planeamento/Sumario?dia=2026-10-28'), '/Servidor');
  assert.equal(await s.abrir('/JobOn/Folha?jobon_id=7'), '/Planeamento/Sumario?dia=2026-10-28');
  assert.equal(await s.abrir('/Ferramentas/Ficha?tool_id=T1'), '/JobOn/Folha?jobon_id=7');
  assert.equal(await s.abrir('/Ferramentas/Historico?tool_id=T1'), '/Ferramentas/Ficha?tool_id=T1');
  // Voltar, voltar, voltar.
  assert.equal(await s.abrir('/Ferramentas/Ficha?tool_id=T1'), '/JobOn/Folha?jobon_id=7');
  assert.equal(await s.abrir('/JobOn/Folha?jobon_id=7'), '/Planeamento/Sumario?dia=2026-10-28');
  assert.equal(await s.abrir('/Planeamento/Sumario?dia=2026-10-28'), '/Servidor');
});

test('as folhas do controlo também contam uma a uma', async () => {
  const s = separador();
  await s.abrir('/Controlo/Resumo?jobon_id=3');
  assert.equal(await s.abrir('/Controlo/Peso?jobon_id=3'), '/Controlo/Resumo?jobon_id=3');
  assert.equal(await s.abrir('/Controlo/Pegamentos?jobon_id=3'), '/Controlo/Peso?jobon_id=3');
  assert.equal(await s.abrir('/Controlo/Peso?jobon_id=3'), '/Controlo/Resumo?jobon_id=3');
});

test('a mesma página com outros filtros ou depois de guardar não é um passo', async () => {
  const s = separador();
  await s.abrir('/Armazem/Movimentos');
  await s.abrir('/Ferramentas?pesquisa=a');
  await s.abrir('/Ferramentas?pesquisa=ab');
  assert.equal(await s.abrir('/Ferramentas/Ficha?tool_id=T1'), '/Ferramentas?pesquisa=ab');
  assert.equal(await s.abrir('/Ferramentas/Ficha?tool_id=T1&guardado=1'), '/Ferramentas?pesquisa=ab');
  assert.equal(await s.abrir('/Ferramentas?pesquisa=ab'), '/Armazem/Movimentos');
});

test('as páginas abertas no painel deslizante não entram no caminho', async () => {
  const s = separador();
  await s.abrir('/Controlo/Resumo?jobon_id=3');
  await s.abrir('/JobOn/Folha?jobon_id=3');
  await s.abrir('/Controlo/Peso?jobon_id=3&painel=1', '/JobOn/Folha?jobon_id=3');
  // Do painel abre-se uma página inteira: volta à página que estava por baixo.
  assert.equal(await s.abrir('/Ferramentas/Ficha?tool_id=T1', '/Controlo/Peso?jobon_id=3&painel=1'), '/JobOn/Folha?jobon_id=3');
});

test('a primeira página do separador usa a página anterior do site; o login começa de novo', async () => {
  const s = separador();
  assert.equal(await s.abrir('/Ferramentas/Ficha?tool_id=T1', '/Armazem/Ultimos?linha=B1'), '/Armazem/Ultimos?linha=B1');
  await s.abrir('/login');
  assert.equal(await s.abrir('/'), '/Servidor');
});

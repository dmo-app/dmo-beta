// O rascunho da folha do Job On, achados S01 e S02 do relatório do
// dmo-Improvements de 28/10/2026 (0.3.0-beta.211):
// S01: um rascunho escrito na versão 3 não entra sozinho na folha da versão 4.
// S02: o que se enviou não se perde enquanto não houver a folha gravada.
// O script é o da página (wwwroot/js/jobon-rascunho.js). Recarregar é abrir
// outra janela com o mesmo localStorage (o mesmo browser).
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const script = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/js/jobon-rascunho.js'), 'utf8');

// A folha nova do Editar 77 (Pages/JobOn/Folha.cshtml): versão e voltar=folha.
function editar77({ versao = '3', notas = 'Do servidor' } = {}) {
  return `<form id="lightForm" data-jobon-rascunho="editar:77" data-jobon-rascunho-dono="7" method="post" action="/JobOn?handler=Update">
    <input type="hidden" name="voltar" value="folha">
    <input type="hidden" name="jobon_edit_id" value="77"><input type="hidden" name="versao" value="${versao}">
    <textarea id="cmNotas" name="folha_cm_notas">${notas}</textarea>
  </form>`;
}
// A folha gravada, bloqueada, com «Job On guardado.».
const gravada = '<div data-dmo-toast="ok" role="status" data-jobon-gravado>Job On guardado.</div><form id="jofLeitura"></form>';
const outraPagina = '<h1>Boquilhas</h1>';

function browser() {
  const local = new Map();
  const erros = [];
  async function abrir(corpo) {
    const virtualConsole = new VirtualConsole();
    virtualConsole.on('jsdomError', (e) => { if (!/navigation/i.test(e.message)) erros.push(e); });
    const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${corpo}</body>`, { url: 'http://dmo.test/JobOn/Folha?jobon_id=77', runScripts: 'outside-only', virtualConsole });
    const { window } = dom;
    for (const [k, v] of local) window.localStorage.setItem(k, v);
    window.eval(script);
    if (window.document.readyState === 'loading') await new Promise((r) => window.document.addEventListener('DOMContentLoaded', () => setTimeout(r)));
    const fechar = () => {
      local.clear();
      for (let i = 0; i < window.localStorage.length; i++) { const k = window.localStorage.key(i); local.set(k, window.localStorage.getItem(k)); }
      window.close();
    };
    return { window, document: window.document, fechar };
  }
  return { abrir, local, erros };
}
function escrever(j, seletor, valor) {
  const campo = j.document.querySelector(seletor);
  campo.value = valor;
  campo.dispatchEvent(new j.window.Event('input', { bubbles: true }));
}
function gravar(j) {
  const form = j.document.getElementById('lightForm');
  form.addEventListener('submit', (e) => e.preventDefault());
  form.requestSubmit();
}
const notas = (j) => j.document.getElementById('cmNotas').value;
const nota = (j) => j.document.getElementById('jobonRascunhoNota');

test('S01: o rascunho da versão 3 não substitui sozinho a versão 4; «Usar o rascunho» traz-o', async () => {
  const b = browser();
  let j = await b.abrir(editar77({ versao: '3' }));
  escrever(j, '#cmNotas', 'Rascunho antigo');
  j.fechar();

  // Outra pessoa gravou: a folha abre na versão 4.
  j = await b.abrir(editar77({ versao: '4', notas: 'Gravado por outra pessoa' }));
  assert.equal(notas(j), 'Gravado por outra pessoa', 'fica o que está gravado');
  assert.match(nota(j).textContent, /outra pessoa gravou esta produção depois/);
  assert.ok(b.local.has('dmo-jobon-rascunho:editar:77'), 'o rascunho não se perde');
  j.document.getElementById('jobonRascunhoUsar').click();
  assert.equal(notas(j), 'Rascunho antigo');
  assert.equal(nota(j), null);
  assert.equal(j.document.querySelector('[name=versao]').value, '4');
  j.fechar();
  assert.equal(JSON.parse(b.local.get('dmo-jobon-rascunho:editar:77')).versao, '4', 'escolhido, passa a ser da versão 4');
  assert.deepEqual(b.erros, []);
});

test('S01: «Descartar rascunho» numa versão nova apaga-o', async () => {
  const b = browser();
  let j = await b.abrir(editar77({ versao: '3' }));
  escrever(j, '#cmNotas', 'Rascunho antigo');
  j.fechar();
  j = await b.abrir(editar77({ versao: '4', notas: 'Gravado' }));
  j.document.getElementById('jobonRascunhoDescartar').click();
  assert.equal(notas(j), 'Gravado');
  j.fechar();
  assert.equal(b.local.has('dmo-jobon-rascunho:editar:77'), false);
});

test('S01: na mesma versão o rascunho volta como antes', async () => {
  const b = browser();
  let j = await b.abrir(editar77());
  escrever(j, '#cmNotas', 'Por gravar');
  j.fechar();
  j = await b.abrir(editar77());
  assert.equal(notas(j), 'Por gravar');
  assert.match(nota(j).textContent, /^Rascunho recuperado/);
  j.fechar();
});

test('S02: um envio sem resposta (rede) e depois outra página não perde o que se escreveu', async () => {
  const b = browser();
  let j = await b.abrir(editar77());
  escrever(j, '#cmNotas', 'Escrito antes de gravar');
  gravar(j);
  j.fechar();

  j = await b.abrir(outraPagina);
  j.fechar();

  j = await b.abrir(editar77());
  assert.equal(notas(j), 'Escrito antes de gravar', 'volta ao abrir a folha');
  assert.ok(nota(j));
  j.fechar();
  assert.deepEqual(b.erros, []);
});

test('S02: voltar atrás à mesma folha depois de um envio falhado traz o que se escreveu', async () => {
  const b = browser();
  let j = await b.abrir(editar77());
  escrever(j, '#cmNotas', 'Escrito');
  gravar(j);
  j.fechar();
  j = await b.abrir(editar77());
  assert.equal(notas(j), 'Escrito');
  j.fechar();
});

test('S02: a folha gravada apaga a cópia do envio, e o rascunho não volta', async () => {
  const b = browser();
  let j = await b.abrir(editar77());
  escrever(j, '#cmNotas', 'Gravado agora');
  gravar(j);
  j.fechar();
  j = await b.abrir(gravada);
  j.fechar();
  assert.deepEqual([...b.local.keys()], [], 'nada fica neste browser');
  j = await b.abrir(editar77({ versao: '4', notas: 'Gravado agora' }));
  assert.equal(nota(j), null);
  j.fechar();
});

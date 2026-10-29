const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const script = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/js/dmo-reference-filters.js'), 'utf8');
const wait = (ms = 200) => new Promise(resolve => setTimeout(resolve, ms));
function page(fetch) {
  const dom = new JSDOM(`<form method="get" data-dmo-reference-scope="armazem" data-dmo-reference-name="referencia">
    <input name="referencia"><select name="linha"><option value="">Todas</option><option value="B1">B1</option><option value="C2">C2</option></select>
    <input name="reparador" data-dmo-suggest="reparadores"><input name="lote"></form>`, { url:'http://dmo.test/Armazem/Ultimos',runScripts:'outside-only' });
  dom.window.fetch = fetch;
  let submits = 0;
  dom.window.HTMLFormElement.prototype.requestSubmit = () => { submits++; };
  dom.window.eval(script);
  const field = name => dom.window.document.querySelector(`[name="${name}"]`);
  const write = (name, value) => { field(name).value = value; field(name).dispatchEvent(new dom.window.Event('input', { bubbles:true })); };
  const options = () => [...field('linha').options].filter(o => o.value && !o.disabled && !o.hidden).map(o => o.value);
  return { dom, field, write, options, submits:()=>submits };
}
const response = values => ({ok:true,json:async()=>values});

test('Uma referência restringe linhas e reparadores e elimina a linha anterior incompatível', async () => {
  const p = page(async()=>response({linha:['C2'],reparador:['Oficina C']}));
  p.field('linha').value='B1';p.field('reparador').value='Oficina B';
  assert.deepEqual(p.options(),['B1','C2']);
  p.write('referencia','T173');await wait();
  assert.deepEqual(p.options(),['C2']);assert.equal(p.field('linha').value,'');assert.equal(p.field('reparador').value,'');
  assert.equal(p.submits(),1);
  const list=p.dom.window.document.getElementById(p.field('reparador').getAttribute('list'));
  assert.deepEqual([...list.options].map(o=>o.value),['Oficina C']);p.dom.window.close();
});

test('Uma escolha ainda válida conserva-se e apagar a referência repõe as opções gerais', async () => {
  const p = page(async()=>response({linha:['C2']}));
  p.field('linha').value='C2';p.write('referencia','T173');await wait();
  assert.equal(p.field('linha').value,'C2');assert.equal(p.submits(),0);
  p.write('referencia','');await wait();assert.deepEqual(p.options(),['B1','C2']);p.dom.window.close();
});

test('A resposta atrasada da referência anterior não substitui as opções da referência atual', async () => {
  let old;
  const p=page(url=>url.includes('reference=A')?new Promise(resolve=>{old=resolve;}):Promise.resolve(response({linha:['C2']})));
  p.write('referencia','A');await wait();p.write('referencia','B');await wait();
  old(response({linha:['B1']}));await wait(10);
  assert.deepEqual(p.options(),['C2']);p.dom.window.close();
});

test('Uma consulta sem resultados não oferece linhas de outras referências', async () => {
  const p=page(async()=>response({linha:[]}));
  assert.equal(p.options().length,2);p.write('referencia','NAO-EXISTE');await wait();
  assert.deepEqual(p.options(),[]);p.dom.window.close();
});

test('Falha de leitura bloqueia escolhas antigas e avisa; uma nova pesquisa recupera', async () => {
  let fail=true;const p=page(async()=>{if(fail)throw new Error('offline');return response({linha:['C2']});});
  p.write('referencia','A');await wait();assert.equal(p.field('linha').disabled,true);
  assert.match(p.dom.window.document.querySelector('[role=status]').textContent,/Não foi possível/);
  fail=false;p.write('referencia','B');await wait();assert.equal(p.field('linha').disabled,false);
  assert.deepEqual(p.options(),['C2']);p.dom.window.close();
});

test('Enter no lote envia os filtros mesmo sem botão no formulário', () => {
  const p=page(async()=>response({}));p.field('lote').value='3';
  p.field('lote').dispatchEvent(new p.dom.window.KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true}));
  assert.equal(p.submits(),1);p.dom.window.close();
});

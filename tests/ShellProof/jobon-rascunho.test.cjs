// O rascunho da folha do Job On (teste no browser real, 22/10/2026: recarregar
// apagava a folha por gravar sem aviso). O script é o da página
// (wwwroot/js/jobon-rascunho.js); a folha em miniatura tem as marcas que
// Pages/JobOn/Index.cshtml desenha. Recarregar é abrir outra janela com o
// mesmo localStorage e sessionStorage (o mesmo browser).
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const script = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/js/jobon-rascunho.js'), 'utf8');

// O que o servidor desenha: a folha nova vazia, a duplicada com os valores da
// produção de origem, a de edição com os valores gravados (referência e
// número só de leitura). recusa: a mensagem de erro que volta com a folha.
function folha({ chave = 'novo', valores = {}, edicao = false, recusa = '', mfOpcao = null, conta = 'operador-a', versao = '3' } = {}) {
  const v = (nome) => valores[nome] ?? '';
  const mf = mfOpcao ? `<option value="${mfOpcao.id}" selected>${mfOpcao.texto}</option>` : '';
  return `<section id="lightSheet" data-dmo-account="${conta}">
    ${recusa ? `<div data-dmo-toast="erro" class="jobon-aviso" role="alert">${recusa}</div>` : ''}
    <form id="lightForm" data-dmo-script data-jobon-rascunho="${chave}" method="post" action="/JobOn?handler=${edicao ? 'Update' : 'Create'}">
      <input name="__RequestVerificationToken" type="hidden" value="tok">
      ${edicao ? '<input type="hidden" id="lightEditId" name="jobon_edit_id" value="77"><input type="hidden" name="versao" value="' + versao + '">' : ''}
      <input id="lightReference" name="reference" value="${v('reference')}"${edicao ? ' readonly' : ''}>
      <input id="lightProduction" name="production_number" value="${v('production_number')}"${edicao ? ' readonly' : ''}>
      <select id="lightMachine" name="machine_id"><option value="">Selecionar</option>
        <option value="1"${v('machine_id') === '1' ? ' selected' : ''}>B1</option><option value="2"${v('machine_id') === '2' ? ' selected' : ''}>C3</option></select>
      <input id="lightStart" name="inicio_em" type="date" value="${v('inicio_em')}">
      <input type="checkbox" id="lightShowAllTools">
      <select name="cm_tool_id" data-facts-type="CM"><option value="">Escreva a referência da CM</option>${v('cm_tool_id') ? `<option value="${v('cm_tool_id')}" selected>${v('cm_tool_id')}</option>` : ''}</select>
      <select name="mf_tool_id" data-facts-type="MF"><option value="">Escreva a referência do MF</option>${mf}</select>
      <select name="bq_tool_id" data-facts-type="BQ"><option value="">Escreva a referência da BQ</option>${v('bq_tool_id') ? `<option value="${v('bq_tool_id')}" selected>${v('bq_tool_id')}</option>` : ''}</select>
      <textarea id="cmNotas" name="folha_cm_notas">${v('folha_cm_notas')}</textarea>
    </form>
  </section>`;
}
// A ficha de um Job On gravado: sem folha aberta.
const ficha = '<section data-dmo-account="operador-a" id="lightFicha"><h2>Job On</h2></section>';

// Um browser: o armazenamento fica entre as janelas (recarregar, navegar).
function browser({ semArmazenamento = false } = {}) {
  const local = new Map();
  const sessao = new Map();
  const erros = [];
  async function abrir(corpo, endereco) {
    const virtualConsole = new VirtualConsole();
    virtualConsole.on('jsdomError', (e) => { if (!/navigation/i.test(e.message)) erros.push(e); });
    const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${corpo}</body>`, { url: endereco, runScripts: 'outside-only', virtualConsole });
    const { window } = dom;
    window.addEventListener('error', (e) => erros.push(e.error || e.message));
    for (const [area, mapa] of [['localStorage', local], ['sessionStorage', sessao]]) {
      if (semArmazenamento) {
        Object.defineProperty(window, area, { configurable: true, get() { throw new window.DOMException('blocked', 'SecurityError'); } });
      } else {
        for (const [k, v] of mapa) window[area].setItem(k, v);
      }
    }
    window.eval(script);
    // O script liga-se no DOMContentLoaded, como na página.
    if (window.document.readyState === 'loading') await new Promise((r) => window.document.addEventListener('DOMContentLoaded', () => setTimeout(r)));
    const guardar = () => {
      if (semArmazenamento) return;
      for (const [area, mapa] of [['localStorage', local], ['sessionStorage', sessao]]) {
        mapa.clear();
        for (let i = 0; i < window[area].length; i++) { const k = window[area].key(i); mapa.set(k, window[area].getItem(k)); }
      }
    };
    // O armazenamento do browser segue a janela a cada evento.
    window.document.addEventListener('input', () => setTimeout(guardar), true);
    window.document.addEventListener('change', () => setTimeout(guardar), true);
    window.document.addEventListener('submit', () => setTimeout(guardar), true);
    window.document.addEventListener('click', () => setTimeout(guardar));
    return { window, document: window.document, fechar: () => { guardar(); window.close(); } };
  }
  return { abrir, local, sessao, erros };
}

const tick = () => new Promise((r) => setTimeout(r, 5));
function escrever(janela, seletor, valor) {
  const campo = janela.document.querySelector(seletor);
  campo.value = valor;
  campo.dispatchEvent(new janela.window.Event('input', { bubbles: true }));
  campo.dispatchEvent(new janela.window.Event('change', { bubbles: true }));
}
const valor = (janela, seletor) => janela.document.querySelector(seletor).value;
const nota = (janela) => janela.document.getElementById('jobonRascunhoNota');
const rascunhos = (b) => [...b.local.keys()].filter((k) => k.startsWith('dmo-jobon-rascunho:')).sort();
// O envio: o browser manda o formulário; a navegação a seguir é outra janela.
function gravar(janela) {
  const form = janela.document.getElementById('lightForm');
  form.addEventListener('submit', (e) => e.preventDefault());
  form.requestSubmit();
}

test('recarregar a folha repõe os campos, com «Rascunho recuperado»', async () => {
  const b = browser();
  let j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  escrever(j, '#lightReference', '5447T173');
  escrever(j, '#lightProduction', '999111');
  escrever(j, '#lightMachine', '2');
  escrever(j, '#cmNotas', 'Trocar a junta');
  // A CM veio da procura: a opção não existe na página recarregada.
  const cm = j.document.querySelector('[name=cm_tool_id]');
  cm.add(new j.window.Option('5447T173 · Lote 3 · B1', 't-cm-5447-3'));
  escrever(j, '[name=cm_tool_id]', 't-cm-5447-3');
  await tick(); j.fechar();

  j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  assert.equal(valor(j, '#lightReference'), '5447T173');
  assert.equal(valor(j, '#lightProduction'), '999111');
  assert.equal(valor(j, '#lightMachine'), '2');
  assert.equal(valor(j, '#cmNotas'), 'Trocar a junta');
  assert.equal(valor(j, '[name=cm_tool_id]'), 't-cm-5447-3');
  assert.equal(j.document.querySelector('[name=cm_tool_id]').selectedOptions[0].text, '5447T173 · Lote 3 · B1');
  assert.ok(nota(j), 'a nota aparece');
  assert.match(nota(j).textContent, /^Rascunho recuperado/);
  assert.ok(j.document.getElementById('jobonRascunhoDescartar'));
  assert.equal(nota(j).nextElementSibling, j.document.getElementById('lightForm'), 'a nota fica por cima da folha');
  assert.deepEqual(b.erros, []);
  j.fechar();
});

test('«Descartar rascunho» repõe os valores do servidor e apaga o rascunho', async () => {
  const b = browser();
  const servidor = { reference: '5447T173', production_number: '202601', machine_id: '1', folha_cm_notas: 'Do servidor' };
  let j = await b.abrir(folha({ chave: 'duplicar:41', valores: servidor }), 'http://dmo.test/JobOn?jobon_id=41&duplicar=1');
  escrever(j, '#lightProduction', '202699');
  escrever(j, '#lightMachine', '2');
  escrever(j, '#cmNotas', 'Do rascunho');
  await tick(); j.fechar();

  j = await b.abrir(folha({ chave: 'duplicar:41', valores: servidor }), 'http://dmo.test/JobOn?jobon_id=41&duplicar=1');
  assert.equal(valor(j, '#lightProduction'), '202699');
  j.document.getElementById('jobonRascunhoDescartar').click();
  assert.equal(valor(j, '#lightReference'), '5447T173');
  assert.equal(valor(j, '#lightProduction'), '202601');
  assert.equal(valor(j, '#lightMachine'), '1');
  assert.equal(valor(j, '#cmNotas'), 'Do servidor');
  assert.equal(nota(j), null, 'a nota sai');
  await tick();
  assert.deepEqual(rascunhos(b), [], 'o rascunho foi apagado');
  j.fechar();

  j = await b.abrir(folha({ chave: 'duplicar:41', valores: servidor }), 'http://dmo.test/JobOn?jobon_id=41&duplicar=1');
  assert.equal(valor(j, '#lightProduction'), '202601', 'não volta depois de recarregar');
  assert.equal(nota(j), null);
  j.fechar();
});

test('uma gravação com sucesso apaga o rascunho, e ele não volta depois', async () => {
  const b = browser();
  let j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  escrever(j, '#lightReference', '5447T173');
  escrever(j, '#lightProduction', '999113');
  escrever(j, '#lightMachine', '1');
  for (const [nome, id] of [['cm_tool_id', 't-cm-5447-3'], ['mf_tool_id', 't-mf-5447-3'], ['bq_tool_id', 't-bq-5447-1']]) {
    j.document.querySelector(`[name=${nome}]`).add(new j.window.Option(id, id));
    escrever(j, `[name=${nome}]`, id);
  }
  await tick();
  assert.deepEqual(rascunhos(b), ['dmo-jobon-rascunho:operador-a:novo']);
  gravar(j);
  await tick(); j.fechar();
  assert.deepEqual(rascunhos(b), [], 'o envio apaga o rascunho');

  // O servidor gravou e mostra a ficha do Job On novo (PRG): sem folha.
  j = await b.abrir(ficha, 'http://dmo.test/JobOn?jobon_id=88');
  await tick(); j.fechar();
  assert.deepEqual([...b.sessao.keys()], [], 'a cópia do envio sai');

  j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  assert.equal(valor(j, '#lightReference'), '');
  assert.equal(valor(j, '#lightProduction'), '');
  assert.equal(nota(j), null);
  assert.deepEqual(b.erros, []);
  j.fechar();
});

test('quando o servidor recusa a gravação, o rascunho volta', async () => {
  const b = browser();
  let j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  escrever(j, '#lightReference', '5447T173');
  escrever(j, '#lightProduction', '999112');
  escrever(j, '#lightMachine', '1');
  escrever(j, '#cmNotas', 'Notas que o endereço não leva');
  gravar(j);
  await tick(); j.fechar();
  assert.deepEqual(rascunhos(b), []);

  // A recusa volta à folha com os valores principais no endereço (PRG) e sem as notas.
  const endereco = 'http://dmo.test/JobOn?production_number=999112&reference=5447T173&machine_id=1&create_refusal=MissingMfTool';
  const recusada = folha({ valores: { reference: '5447T173', production_number: '999112', machine_id: '1' }, recusa: 'O MF (molde final) é obrigatório. Nenhum Job On foi criado.' });
  j = await b.abrir(recusada, endereco);
  assert.equal(valor(j, '#cmNotas'), 'Notas que o endereço não leva');
  assert.equal(valor(j, '#lightReference'), '5447T173');
  assert.ok(nota(j));
  j.fechar();
  assert.deepEqual(rascunhos(b), ['dmo-jobon-rascunho:operador-a:novo'], 'volta a ser rascunho');

  // E fica: recarregar a folha nova traz tudo.
  j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  assert.equal(valor(j, '#lightProduction'), '999112');
  assert.equal(valor(j, '#cmNotas'), 'Notas que o endereço não leva');
  j.fechar();
});

test('os campos que chegam no endereço ganham ao rascunho (o regresso de «Criar CM»)', async () => {
  const b = browser();
  let j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  escrever(j, '#lightReference', '5447T173');
  escrever(j, '#lightMachine', '2');
  escrever(j, '#cmNotas', 'Só no rascunho');
  j.document.querySelector('[name=cm_tool_id]').add(new j.window.Option('ANTIGA', 't-cm-antiga'));
  escrever(j, '[name=cm_tool_id]', 't-cm-antiga');
  await tick(); j.fechar();

  // As Ferramentas criaram a CM e voltam com ela (e com a máquina 1) no endereço.
  const endereco = 'http://dmo.test/JobOn?novo=1&retoma=1&reference=5447T173&machine_id=1&cm_tool_id=t-cm-nova#lightSheet';
  j = await b.abrir(folha({ valores: { reference: '5447T173', machine_id: '1', cm_tool_id: 't-cm-nova' } }), endereco);
  assert.equal(valor(j, '[name=cm_tool_id]'), 't-cm-nova', 'a CM criada fica');
  assert.equal(valor(j, '#lightMachine'), '1', 'a máquina do endereço fica');
  assert.equal(valor(j, '#cmNotas'), 'Só no rascunho', 'o resto volta do rascunho');
  assert.ok(nota(j));
  j.fechar();
});

test('sem localStorage a folha funciona, sem erros', async () => {
  const b = browser({ semArmazenamento: true });
  let j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  escrever(j, '#lightReference', '5447T173');
  escrever(j, '#cmNotas', 'x');
  let enviado = false;
  j.document.getElementById('lightForm').addEventListener('submit', () => { enviado = true; });
  gravar(j);
  const saida = new j.window.Event('beforeunload', { cancelable: true });
  j.window.dispatchEvent(saida);
  await tick(); j.fechar();
  assert.equal(enviado, true, 'o envio segue');
  j = await b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  assert.equal(valor(j, '#lightReference'), '');
  assert.equal(nota(j), null);
  escrever(j, '#lightReference', 'Y');
  const sair = new j.window.Event('beforeunload', { cancelable: true });
  j.window.dispatchEvent(sair);
  assert.equal(sair.defaultPrevented, true, 'sair com alterações ainda avisa');
  await tick(); j.fechar();
  assert.deepEqual(b.erros, []);
});

test('os rascunhos do Novo, do Duplicar X e do Editar Y não se misturam', async () => {
  const b = browser();
  const dup = { reference: '6120T044', production_number: '202640', machine_id: '1' };
  const edi = { reference: '5809T020', production_number: '202650', machine_id: '1', folha_cm_notas: 'Gravado' };
  const novo = () => b.abrir(folha(), 'http://dmo.test/JobOn?novo=1');
  const duplicar = (id) => b.abrir(folha({ chave: `duplicar:${id}`, valores: dup }), `http://dmo.test/JobOn?jobon_id=${id}&duplicar=1`);
  const editar = (id) => b.abrir(folha({ chave: `editar:${id}`, valores: edi, edicao: true }), `http://dmo.test/JobOn?jobon_id=${id}&editar=1`);

  let j = await novo(); escrever(j, '#cmNotas', 'NOVO'); await tick(); j.fechar();
  j = await duplicar(40); escrever(j, '#cmNotas', 'DUP40'); await tick(); j.fechar();
  j = await editar(77); escrever(j, '#cmNotas', 'EDIT77'); escrever(j, '#lightMachine', '2'); await tick(); j.fechar();
  assert.deepEqual(rascunhos(b), ['dmo-jobon-rascunho:operador-a:duplicar:40', 'dmo-jobon-rascunho:operador-a:editar:77', 'dmo-jobon-rascunho:operador-a:novo']);

  j = await novo(); assert.equal(valor(j, '#cmNotas'), 'NOVO'); j.fechar();
  j = await duplicar(40); assert.equal(valor(j, '#cmNotas'), 'DUP40'); assert.equal(valor(j, '#lightProduction'), '202640'); j.fechar();
  j = await duplicar(41); assert.equal(valor(j, '#cmNotas'), '', 'outra duplicada vem limpa'); assert.equal(nota(j), null); j.fechar();
  j = await editar(77);
  assert.equal(valor(j, '#cmNotas'), 'EDIT77');
  assert.equal(valor(j, '#lightMachine'), '2');
  assert.equal(valor(j, '#lightReference'), '5809T020', 'a referência só de leitura fica a do servidor');
  j.fechar();
  j = await editar(78); assert.equal(valor(j, '#cmNotas'), 'Gravado', 'outro Job On fica como está gravado'); assert.equal(nota(j), null); j.fechar();

  // Gravar o Editar 77 só apaga o dele.
  j = await editar(77); gravar(j); await tick(); j.fechar();
  assert.deepEqual(rascunhos(b), ['dmo-jobon-rascunho:operador-a:duplicar:40', 'dmo-jobon-rascunho:operador-a:novo']);
  assert.deepEqual(b.erros, []);
});


test('O rascunho de uma conta não aparece noutra no mesmo browser', async () => {
  const b = browser();
  let w = await b.abrir(folha({conta: 'a'}), 'http://dmo.local/JobOn');
  w.document.querySelector('#lightReference').value = 'DA-CONTA-A';
  w.document.querySelector('#lightReference').dispatchEvent(new w.window.Event('input', {bubbles:true}));
  w.fechar();
  w = await b.abrir(folha({conta: 'b'}), 'http://dmo.local/JobOn');
  assert.equal(w.document.querySelector('#lightReference').value, '');
  w = await b.abrir(folha({conta: 'a'}), 'http://dmo.local/JobOn');
  assert.equal(w.document.querySelector('#lightReference').value, 'DA-CONTA-A');
});

test('Um rascunho de outra versão não substitui automaticamente o servidor', async () => {
  const b = browser();
  let w = await b.abrir(folha({chave:'editar:77', edicao:true, versao:'3'}), 'http://dmo.local/JobOn');
  w.document.querySelector('#cmNotas').value = 'trabalho antigo';
  w.document.querySelector('#cmNotas').dispatchEvent(new w.window.Event('input', {bubbles:true}));
  w.fechar();
  w = await b.abrir(folha({chave:'editar:77', edicao:true, versao:'4', valores:{folha_cm_notas:'servidor novo'}}), 'http://dmo.local/JobOn');
  assert.equal(w.document.querySelector('#cmNotas').value, 'servidor novo');
  assert.match(w.document.querySelector('#jobonRascunhoNota').textContent, /versão anterior/);
});

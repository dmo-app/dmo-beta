const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// A COMPARAÇÃO DE PESO SEM RECARREGAR (deep sweep, 18/10/2026, B-peso-4).
// «Comparar pesos» e «Guardar comparação» eram um POST nativo: com a ligação
// a cair, o operador via a página de erro do Chrome, a comparação ficava
// guardada sem ele saber e voltar atrás perdia as massas. A tab da comparação
// em miniatura, com as zonas lidas do Peso.cshtml, a troca real (DmoTroca),
// os scripts da página e um servidor de teste que responde como o PesoModel:
// «Comparar pesos» desenha a página com o resultado (sem redirect) e
// «Guardar comparação» guarda e volta por PRG, uma vez por chave X-Dmo-Envio
// (o [EnvioUnico]).
const PAGINA = path.resolve(__dirname, '../../src/DMO.Alpha.Web/Pages/Controlo/Peso.cshtml');
const cshtml = fs.readFileSync(PAGINA, 'utf8');

function scriptsDaPagina() {
  const seccao = cshtml.slice(cshtml.indexOf('@section Scripts {')).replace(/@\*[\s\S]*?\*@/g, '');
  return [...seccao.matchAll(/<script>([\s\S]*?)<\/script>/g)].map((m) => m[1]);
}

// As zonas da tab, como o Peso.cshtml as marca (sem elas, um <div> simples).
const zona = (id) => {
  const m = cshtml.match(new RegExp(`<div id="${id}"[^>]*>`));
  return m ? m[0] : '<div>';
};
const Z = {
  contexto: zona('pesoComparacaoContexto'),
  leituras: zona('pesoComparacaoLeituras'),
  resultado: zona('pesoComparacaoResultado'),
  guardada: zona('pesoComparacaoGuardada')
};

const BASE = 'http://dmo.test/Controlo/Peso';
const INICIO = `${BASE}?comparacao=1&referencia=7079C337&jobon_id=993&peso_inicial=901`;
const vazias = () => Array.from({ length: 4 }, () => ({ cm: '', agua: '' }));

// A tab como o Peso.cshtml a desenha: eco são os valores do formulário (os
// enviados, num POST desenhado; os de omissão, num GET).
function desenhar(db, { eco = null, resultado = null, guardada = null } = {}) {
  const f = eco || { tool: 'T1', temp: '20', nota: '', linhas: vazias() };
  const token = `<input name="__RequestVerificationToken" type="hidden" value="tok-${++db.tokens}" />`;
  const opcao = (valor) => `<option value="${valor}"${f.tool === valor ? ' selected' : ''}>${valor}</option>`;
  return `<main class="peso-page dmo-workspace" aria-labelledby="peso-title" data-dmo-troca>
  <div class="page-head"><h1 id="peso-title">Peso</h1></div>
  ${Z.contexto}
    <section class="dmo-card peso-card"><h3 id="peso-comparacao-title">Comparação de Peso</h3>
      <form method="get" action="/Controlo/Peso" class="dmo-filter-bar">
        <input type="hidden" name="comparacao" value="1" />
        <select id="cmpInicial" name="peso_inicial" data-auto-submit><option value="901" selected>901</option></select>
      </form>
      ${guardada ? '' : '<div data-cmp-base>Base · média 330.1 g</div>'}
    </section>
  </div>
  ${Z.leituras}
    <section class="dmo-card peso-card">
      <div class="dmo-card-topo-acoes">
        <button type="button" class="dmo-button" form="cmpForm" data-cmp-add-row>Adicionar leitura</button>
        <button type="button" class="dmo-button" form="cmpForm" data-cmp-remove-row>Remover leitura</button>
        <button type="submit" class="dmo-button" form="cmpForm" id="comparar">Comparar pesos</button>
        <button type="submit" formaction="/Controlo/Peso?handler=GuardarComparacao" class="dmo-button" form="cmpForm" id="guardar">Guardar comparação</button>
      </div>
      <form method="post" action="/Controlo/Peso?handler=Comparar" class="peso-form-grid" id="cmpForm">
        <input type="hidden" name="jobon_id" value="993" />
        <input type="hidden" name="peso_inicial" value="901" />
        ${guardada ? `<input type="hidden" name="comparacao_guardada" value="${guardada}" />` : ''}
        <select id="cmpTool" name="cm_tool_id"><option value="">Selecionar ferramenta CM</option>${opcao('T1')}${opcao('T2')}</select>
        <input id="cmpTemperature" name="water_temperature" value="${f.temp}" />
        <div data-cmp-readings>${f.linhas.map((l, i) => `<div data-cmp-row><input id="cmpSubject-${i}" name="cm_subject" value="${l.cm}" /><input id="cmpWater-${i}" name="water_weight" value="${l.agua}" /></div>`).join('')}</div>
        <input id="cmpNota" name="comparacao_note" value="${f.nota}" />
        ${token}
      </form>
    </section>
  </div>
  ${Z.resultado}${resultado ? `<section class="dmo-card peso-card" data-cmp-resultado>Pares válidos ${resultado}</section>` : ''}</div>
  ${Z.guardada}${guardada ? `<section class="dmo-card peso-card"><h3 id="peso-comparacao-guardada-title">Comparação guardada ${guardada}</h3></section>` : ''}</div>
</main>`;
}

const documento = (db, opcoes) => `<!doctype html><title>Peso</title><body>${desenhar(db, opcoes)}</body>`;

function ecoDe(corpo) {
  const f = new URLSearchParams(corpo);
  const cms = f.getAll('cm_subject');
  const aguas = f.getAll('water_weight');
  return { tool: f.get('cm_tool_id') || '', temp: f.get('water_temperature') || '', nota: f.get('comparacao_note') || '',
    linhas: cms.map((cm, i) => ({ cm, agua: aguas[i] || '' })) };
}

// rede.cortada: os pedidos falham como sem ligação. rede.cairDepoisDoPost: o
// POST seguinte é tratado pelo servidor e a ligação cai antes da resposta.
async function abrirComparacao() {
  const db = { tokens: 0, comparacoes: [], chaves: new Map() };
  const rede = { cortada: false, cairDepoisDoPost: false };
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (e) => { if (/navigation|submit/i.test(e.message)) navegou.push(e.message); });
  const dom = new JSDOM(documento(db), { url: INICIO, runScripts: 'outside-only', virtualConsole, pretendToBeVisual: true });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (url, options = {}) => {
    const u = new URL(String(url), window.location.href);
    const metodo = options.method || 'GET';
    const chave = options.headers && options.headers['X-Dmo-Envio'];
    pedidos.push({ metodo, handler: u.searchParams.get('handler'), chave });
    if (rede.cortada) throw new window.TypeError('Failed to fetch');
    let final = u.href;
    let html;
    const corpo = String(options.body || '');
    if (metodo === 'POST' && u.searchParams.get('handler') === 'Comparar') {
      html = documento(db, { eco: ecoDe(corpo), resultado: 2 });
    } else if (metodo === 'POST' && u.searchParams.get('handler') === 'GuardarComparacao') {
      const f = new URLSearchParams(corpo);
      if (chave && db.chaves.has(chave)) {
        final = db.chaves.get(chave);
      } else if (f.get('comparacao_guardada') && f.getAll('water_weight').every((a) => !a.trim())) {
        final = `${BASE}?comparacao=1&jobon_id=993&peso_inicial=901&comparacao_id=${f.get('comparacao_guardada')}`;
      } else {
        db.comparacoes.push(ecoDe(corpo));
        final = `${BASE}?comparacao=1&jobon_id=993&peso_inicial=901&comparacao_id=${70 + db.comparacoes.length}`;
        if (chave) db.chaves.set(chave, final);
      }
      if (rede.cairDepoisDoPost) {
        rede.cairDepoisDoPost = false;
        rede.cortada = true;
        throw new window.TypeError('Failed to fetch');
      }
    } else if (metodo !== 'GET') {
      throw new Error(`${metodo} ${u.search}`);
    }
    if (!html) {
      const guardada = new URL(final).searchParams.get('comparacao_id');
      html = documento(db, { guardada });
    }
    return { ok: true, redirected: final !== u.href, url: final,
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? 'text/html; charset=utf-8' : null) },
      text: async () => html };
  };
  window.Element.prototype.scrollIntoView = function () {};
  for (const ficheiro of ['0_ASSET_CANONICAL_INTERACTIONS.js', 'dmo-live.js', 'js/dmo-suggest.js', 'peso-live.js']) {
    window.eval(fs.readFileSync(path.join(assets, ficheiro), 'utf8'));
  }
  for (const bloco of scriptsDaPagina()) window.eval(bloco);
  await new Promise((resolve) => window.addEventListener('load', resolve, { once: true }));
  window.__marca = 'a mesma página';
  return { dom, window, document: window.document, db, pedidos, navegou, rede };
}

const esperar = (ms = 40) => new Promise((resolve) => setTimeout(resolve, ms));
const id = (document, valor) => document.getElementById(valor);
const noCanto = (document) => [...document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast]:not(.a-sair)')].map((t) => t.textContent);
const carregar = (document, botao) => id(document, 'cmpForm').requestSubmit(id(document, botao));

function preencher(document) {
  id(document, 'cmpTool').value = 'T2';
  id(document, 'cmpTemperature').value = '22';
  id(document, 'cmpSubject-0').value = '76';
  id(document, 'cmpWater-0').value = '131.7';
  id(document, 'cmpSubject-1').value = '33';
  id(document, 'cmpWater-1').value = '127.7';
  id(document, 'cmpNota').value = 'teste rede';
}

function formulario(document) {
  return {
    tool: id(document, 'cmpTool').value,
    temp: id(document, 'cmpTemperature').value,
    nota: id(document, 'cmpNota').value,
    linhas: [...document.querySelectorAll('[data-cmp-row]')].map((row) => [...row.querySelectorAll('input')].map((i) => i.value).join('=')).filter((l) => l !== '=')
  };
}
const ESCRITO = { tool: 'T2', temp: '22', nota: 'teste rede', linhas: ['76=131.7', '33=127.7'] };

test('«Comparar pesos» e «Guardar comparação» trocam só as zonas, sem recarregar; as linhas adicionam-se no formulário novo', async () => {
  const { dom, window, document, db, pedidos, navegou } = await abrirComparacao();
  try {
    preencher(document);
    carregar(document, 'comparar');
    await esperar();
    assert.deepEqual(navegou, [], 'sem POST nativo');
    assert.equal(window.__marca, 'a mesma página');
    assert.ok(document.querySelector('[data-cmp-resultado]'), 'o resultado aparece');
    assert.deepEqual(formulario(document), ESCRITO, 'o formulário fica com o que foi comparado');
    assert.equal(window.location.href, INICIO, 'o endereço fica o da comparação');

    carregar(document, 'guardar');
    await esperar();
    assert.deepEqual(navegou, []);
    assert.equal(db.comparacoes.length, 1);
    assert.match(id(document, 'peso-comparacao-guardada-title').textContent, /Comparação guardada 71/);
    assert.equal(document.querySelector('[data-cmp-resultado]'), null);
    assert.match(window.location.search, /comparacao_id=71/, 'o endereço do PRG');
    assert.deepEqual(formulario(document).linhas, [], 'o formulário volta limpo');

    // Um clique repetido no formulário da guardada: nada novo, nenhuma mensagem.
    carregar(document, 'guardar');
    await esperar();
    assert.equal(db.comparacoes.length, 1);
    assert.deepEqual(noCanto(document), []);

    // Os botões das linhas, no formulário novo (delegados no document). Estão
    // no canto do cartão, fora do <form>, e são dele pelo form= (regra 74 do
    // deep sweep de 18/10/2026).
    assert.match(cshtml, /<div class="dmo-card-topo-acoes">\s*<button type="button" class="dmo-button" form="cmpForm" data-cmp-add-row>Adicionar leitura<\/button>\s*<button type="button" class="dmo-button" form="cmpForm" data-cmp-remove-row>Remover leitura<\/button>/);
    assert.equal(document.querySelector('[data-cmp-add-row]').closest('form'), null);
    const linhas = () => document.querySelectorAll('[data-cmp-row]').length;
    document.querySelector('[data-cmp-add-row]').click();
    assert.equal(linhas(), 5);
    document.querySelector('[data-cmp-remove-row]').click();
    document.querySelector('[data-cmp-remove-row]').click();
    assert.equal(linhas(), 3);
    assert.equal(window.__marca, 'a mesma página');
    assert.deepEqual(pedidos.map((p) => `${p.metodo} ${p.handler || ''}`.trim()), ['POST Comparar', 'POST GuardarComparacao', 'POST GuardarComparacao']);
  } finally {
    dom.window.close();
  }
});

// A ligação cai a meio de «Guardar comparação»: a página fica, sem a página de
// erro do Chrome, com as massas escritas e a mensagem de ligação no canto.
// Quando a ligação volta, a confirmação mostra a comparação (o GET dela) e as
// massas enviadas continuam no formulário (voltam a ele, se a confirmação o
// trocar: depois de «Comparar pesos» o formulário desenhado mudou). Carregar
// outra vez em «Guardar comparação» é o mesmo envio, com a mesma chave: se o
// servidor já tinha guardado, mostra a guardada sem guardar outra; se o POST
// não chegou, guarda agora. Nunca duas.
for (const caso of ['guardou, depois de comparar', 'guardou, sem comparar', 'o POST não chegou']) {
  test(`a ligação cai ao guardar a comparação (${caso}): a página fica, as massas ficam e guardar outra vez não guarda duas`, async () => {
    const { dom, window, document, db, pedidos, navegou, rede } = await abrirComparacao();
    try {
      window.DmoTroca.tempos.aviso = 30;
      window.DmoTroca.tempos.confirmar = 60000;
      preencher(document);
      if (caso === 'guardou, depois de comparar') {
        carregar(document, 'comparar');
        await esperar();
        assert.ok(document.querySelector('[data-cmp-resultado]'));
      }
      if (caso === 'o POST não chegou') rede.cortada = true;
      else rede.cairDepoisDoPost = true;
      carregar(document, 'guardar');
      await esperar(80);
      assert.deepEqual(navegou, [], 'sem POST nativo, sem página de erro');
      assert.equal(window.__marca, 'a mesma página');
      assert.equal(db.comparacoes.length, caso === 'o POST não chegou' ? 0 : 1);
      assert.deepEqual(formulario(document), ESCRITO, 'sem ligação, as massas ficam');
      assert.equal(noCanto(document).filter((m) => /não foi possível confirmar/.test(m)).length, 1, 'a mensagem de ligação no canto');

      rede.cortada = false;
      window.dispatchEvent(new window.Event('online'));
      await esperar(80);
      assert.deepEqual(navegou, []);
      assert.deepEqual(formulario(document), ESCRITO, 'com a ligação de volta, as massas continuam no formulário');
      assert.equal(document.querySelector('[data-cmp-resultado]'), null, 'a confirmação é o GET da comparação');
      const guardar = pedidos.filter((p) => p.handler === 'GuardarComparacao');
      assert.equal(guardar.length, 1, 'o POST não se repete sozinho');

      carregar(document, 'guardar');
      await esperar(80);
      const repetidos = pedidos.filter((p) => p.handler === 'GuardarComparacao');
      assert.equal(repetidos.length, 2);
      assert.equal(repetidos[1].chave, repetidos[0].chave, 'o mesmo envio leva a mesma chave');
      assert.match(String(repetidos[0].chave), /^[0-9a-f]{32}$/);
      assert.equal(db.comparacoes.length, 1, 'guardada uma vez');
      assert.deepEqual(db.comparacoes[0], { tool: 'T2', temp: '22', nota: 'teste rede',
        linhas: [{ cm: '76', agua: '131.7' }, { cm: '33', agua: '127.7' }, { cm: '', agua: '' }, { cm: '', agua: '' }] });
      assert.match(id(document, 'peso-comparacao-guardada-title').textContent, /Comparação guardada 71/, 'a guardada aparece');
      assert.deepEqual(formulario(document).linhas, [], 'e o formulário volta limpo');
      assert.equal(window.__marca, 'a mesma página');
    } finally {
      dom.window.close();
    }
  });
}

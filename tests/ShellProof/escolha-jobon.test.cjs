const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM, VirtualConsole } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Deep sweep 2, 18/10/2026 (F-8 e F-10): a escolha do Job On nos Pegamentos,
// no Peso e no PU NNPB recarregava a página a cada referência e a cada
// máquina, e os Históricos só procuravam com o Enter. A escolha em miniatura,
// como _PegamentosEscolhaJobOn.cshtml: a procura fica (data-dmo-fica) e vai
// pelo DmoTroca (data-dmo-parcial); a produção e a mensagem são zonas; o
// dropdown da produção é do formulário POST de baixo (form=), que abre a
// folha como sempre (data-dmo-nativo).
const JOBONS = [
  { id: 11, referencia: '5810', producao: '2026001', maquina: 'M1' },
  { id: 12, referencia: '5810', producao: '2026002', maquina: 'M2' },
  { id: 21, referencia: '7245', producao: '2026101', maquina: 'M1' },
];

// O segundo passo da escolha do Job On (IJobOnListQuery.ListByReferenceAsync,
// como na main): as produções da referência escrita, a inteira, sem
// distinguir maiúsculas; a sugestão é que procura pelo início. A Comparação
// do Peso usa o IProductionPickerQuery, pelo início (PesoComparacaoPageTests).
function escolha({ referencia = '', maquina = '' } = {}) {
  const producoes = JOBONS.filter((jobOn) => referencia && jobOn.referencia === referencia.toUpperCase()
    && (!maquina || jobOn.maquina === maquina));
  const tem = producoes.length > 0;
  const maquinas = ['M1', 'M2'].map((m) => `<option value="${m}"${maquina === m ? ' selected' : ''}>${m}</option>`).join('');
  const opcoes = producoes.map((jobOn) => `<option value="${jobOn.id}">${jobOn.producao} · ${jobOn.maquina}</option>`).join('');
  const vazia = referencia && !tem ? `<div role="status" id="semJobOns">Não há Job Ons da referência ${referencia}.</div>` : '';
  return `<main data-dmo-troca>
    <h1 id="titulo">Pegamentos</h1>
    <div id="pegEscolhaAvisos" class="dmo-zona" data-dmo-regiao></div>
    <section>
      <div class="dmo-zona" data-dmo-fica data-dmo-parcial>
        <form method="get" action="/Controlo/Pegamentos" id="pegJobOnProcura">
          <input type="hidden" name="modo" value="jobon">
          <input id="pegJobOnReferencia" name="referencia" value="${referencia}" required>
          <select id="pegJobOnMaquina" name="maquina" data-dmo-autosubmit><option value="">Todas</option>${maquinas}</select>
          <div id="pegJobOnProducaoZona" data-dmo-regiao${tem ? '' : ' hidden'}>${tem
            ? `<label for="pegJobOn">Produção</label><select id="pegJobOn" name="jobon_id" form="pegJobOnEscolha" required data-dmo-autosubmit="valor" data-dmo-depende="pegJobOnReferencia" data-dmo-depende-valor="${referencia}"><option value="">Escolher produção</option>${opcoes}</select>`
            : ''}</div>
        </form>
        <div id="pegJobOnVazia" data-dmo-regiao>${vazia}</div>
        <form method="post" action="/Controlo/Pegamentos?handler=AbrirJobOn" id="pegJobOnEscolha" data-dmo-nativo></form>
      </div>
    </section>
  </main>`;
}

// O Histórico em miniatura, como o do Job On e o do Controlo: os filtros
// ficam (data-dmo-fica), procuram ao escrever (data-dmo-ao-escrever) e o
// dropdown das produções é uma zona; a produção escolhida para a referência
// anterior sai ao escrever (data-dmo-depende sem valor). A tabela e o
// dropdown procuram a referência pelo início (beta.237, ReferenceMatch),
// nunca por um pedaço do meio.
const peloInicio = (jobOn, referencia) => Boolean(referencia) && jobOn.referencia.startsWith(referencia.toUpperCase());

function historico({ referencia = '', producao = '' } = {}) {
  const producoes = [...new Set(JOBONS.filter((jobOn) => peloInicio(jobOn, referencia))
    .map((jobOn) => jobOn.producao))];
  const linhas = JOBONS.filter((jobOn) => peloInicio(jobOn, referencia)
    && (!producao || jobOn.producao === producao))
    .map((jobOn) => `<li>${jobOn.referencia} ${jobOn.producao}</li>`).join('');
  const opcoes = producoes.map((p) => `<option value="${p}"${producao === p ? ' selected' : ''}>${p}</option>`).join('');
  return `<main data-dmo-troca>
    <h1 id="titulo">Histórico</h1>
    <div class="dmo-zona" data-dmo-fica data-dmo-parcial>
      <form method="get" action="/JobOn/Index" id="histFiltros" data-dmo-ao-escrever="histResultados">
        <input type="hidden" name="vista" value="historico">
        <input id="histReferencia" name="referencia" value="${referencia}">
        <div id="histProducaoZona" data-dmo-regiao>${referencia
          ? `<select id="histProducao" name="producao" data-dmo-autosubmit data-dmo-depende="histReferencia"><option value="">Todas</option>${opcoes}</select>`
          : '<select id="histProducao" name="producao" disabled><option value="">Escreva primeiro a referência</option></select>'}</div>
      </form>
    </div>
    <div id="histResultados" data-dmo-regiao><ul>${linhas}</ul></div>
  </main>`;
}

const esperar = (ms = 20) => new Promise((resolve) => setTimeout(resolve, ms));

async function abrir(url, desenhar) {
  const navegou = [];
  const virtualConsole = new VirtualConsole();
  virtualConsole.on('jsdomError', (error) => { if (/navigation|submit/i.test(error.message)) navegou.push(error.message); });
  const inicial = new URL(url);
  const pagina = (endereco) => desenhar(Object.fromEntries(new URL(endereco).searchParams));
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${pagina(inicial.href)}</body>`,
    { url: inicial.href, runScripts: 'outside-only', virtualConsole });
  const { window } = dom;
  const pedidos = [];
  window.fetch = async (endereco, opcoes = {}) => {
    const final = new URL(String(endereco), window.location.href);
    pedidos.push({ metodo: opcoes.method || 'GET', url: final });
    return {
      ok: true, url: final.href,
      headers: { get: (nome) => (nome.toLowerCase() === 'content-type' ? 'text/html; charset=utf-8' : null) },
      text: async () => `<!doctype html><title>DMO</title><body>${pagina(final.href)}</body>`
    };
  };
  window.Element.prototype.scrollIntoView = function () {};
  window.eval(fs.readFileSync(path.join(assets, '0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8'));
  return { dom, window, document: window.document, pedidos, navegou };
}

// Uma escolha com o rato: o clique na lista e o change.
function escolherComRato(window, id, valor) {
  const select = window.document.getElementById(id);
  select.focus();
  select.dispatchEvent(new window.Event('pointerdown', { bubbles: true }));
  select.value = valor;
  select.dispatchEvent(new window.Event('change', { bubbles: true }));
}

function escrever(window, id, texto) {
  const campo = window.document.getElementById(id);
  campo.focus();
  campo.value = texto;
  campo.dispatchEvent(new window.Event('input', { bubbles: true }));
  return campo;
}

const producoesDe = (document) => Array.from(document.querySelectorAll('#pegJobOn option'), (opcao) => opcao.textContent);

test('escolher a referência e a máquina troca só a produção, sem recarregar a página', async () => {
  const s = await abrir('http://dmo.test/Controlo/Pegamentos?modo=jobon', escolha);
  try {
    const titulo = s.document.getElementById('titulo');
    const zona = s.document.getElementById('pegJobOnProducaoZona');
    assert.ok(zona.hidden, 'sem referência, a produção não aparece');

    // A referência com o Enter (ou a sugestão): só a produção troca.
    const campo = escrever(s.window, 'pegJobOnReferencia', '5810');
    campo.form.requestSubmit();
    await esperar();
    assert.deepEqual(s.navegou, []);
    assert.deepEqual(s.pedidos.map((p) => p.metodo), ['GET']);
    assert.equal(s.pedidos[0].url.searchParams.get('referencia'), '5810');
    assert.equal(s.document.getElementById('titulo'), titulo, 'o que fica fora das zonas não é desenhado de novo');
    assert.equal(s.document.getElementById('pegJobOnReferencia'), campo, 'a procura fica como estava');
    assert.equal(s.document.activeElement, campo, 'o foco fica na referência');
    assert.equal(s.document.getElementById('pegJobOnProducaoZona').hidden, false);
    assert.deepEqual(producoesDe(s.document), ['Escolher produção', '2026001 · M1', '2026002 · M2']);
    assert.equal(s.window.location.search, '?modo=jobon&referencia=5810&maquina=');

    // A máquina aplica ao escolher: também só a produção troca.
    escolherComRato(s.window, 'pegJobOnMaquina', 'M2');
    await esperar();
    assert.deepEqual(s.navegou, []);
    assert.equal(s.pedidos.length, 2);
    assert.equal(s.pedidos[1].url.searchParams.get('maquina'), 'M2');
    assert.deepEqual(producoesDe(s.document), ['Escolher produção', '2026002 · M2']);
    assert.equal(s.document.activeElement, s.document.getElementById('pegJobOnMaquina'), 'o foco fica na máquina');

    // Uma referência sem Job Ons: a produção esconde-se e aparece a mensagem.
    escrever(s.window, 'pegJobOnReferencia', '9999').form.requestSubmit();
    await esperar();
    assert.deepEqual(s.navegou, []);
    assert.ok(s.document.getElementById('pegJobOnProducaoZona').hidden);
    assert.equal(s.document.getElementById('pegJobOn'), null);
    assert.match(s.document.getElementById('semJobOns').textContent, /Não há Job Ons da referência 9999/);
    assert.equal(s.document.getElementById('titulo'), titulo);
  } finally {
    s.dom.window.close();
  }
});

test('escolher a produção abre a folha com o POST de sempre, fora do DmoTroca', async () => {
  const s = await abrir('http://dmo.test/Controlo/Pegamentos?modo=jobon&referencia=5810', escolha);
  try {
    // A opção vazia não abre nada.
    escolherComRato(s.window, 'pegJobOn', '');
    await esperar();
    assert.deepEqual(s.navegou, []);

    escolherComRato(s.window, 'pegJobOn', '12');
    await esperar();
    assert.equal(s.pedidos.filter((p) => p.metodo === 'POST').length, 0, 'abrir outra folha não vai por fetch');
    assert.equal(s.navegou.length, 1, 'o browser envia o formulário (abre a folha da produção)');
    const corpo = new s.window.FormData(s.document.getElementById('pegJobOnEscolha'));
    assert.equal(corpo.get('jobon_id'), '12', 'o dropdown vai com o formulário POST (form=)');
  } finally {
    s.dom.window.close();
  }
});

test('outra referência escrita desativa a produção da anterior até ser aplicada', async () => {
  const s = await abrir('http://dmo.test/Controlo/Pegamentos?modo=jobon&referencia=5810', escolha);
  try {
    const producao = s.document.getElementById('pegJobOn');
    escrever(s.window, 'pegJobOnReferencia', '72');
    assert.ok(producao.disabled, 'uma produção da 5810 não se abre com a 72 escrita');
    escrever(s.window, 'pegJobOnReferencia', ' 5810 ');
    assert.equal(producao.disabled, false, 'voltar ao mesmo texto ativa-a outra vez');
    escrever(s.window, 'pegJobOnReferencia', '7245');
    assert.ok(producao.disabled);
    assert.deepEqual(s.pedidos, [], 'escrever não procura (só com o Enter ou a sugestão)');

    s.document.getElementById('pegJobOnReferencia').form.requestSubmit();
    await esperar();
    assert.deepEqual(s.navegou, []);
    const nova = s.document.getElementById('pegJobOn');
    assert.notEqual(nova, producao);
    assert.equal(nova.disabled, false, 'as produções da referência aplicada escolhem-se');
    assert.deepEqual(producoesDe(s.document), ['Escolher produção', '2026101 · M1']);
  } finally {
    s.dom.window.close();
  }
});

// As respostas ficam presas até soltar(): o que o operador faz com o pedido a
// caminho (a rede da fábrica, lenta).
function segurar(s) {
  const original = s.window.fetch;
  const presos = [];
  s.window.fetch = (...args) => new Promise((resolve) => presos.push(() => resolve(original(...args))));
  return {
    soltar: () => presos.splice(0).forEach((soltar) => soltar()),
    repor: () => { s.window.fetch = original; },
  };
}

// Revisão do deep sweep 2 (18/10/2026): Enter na 7079C337 e, com a resposta a
// caminho, escrever a 9322Y152 trazia o dropdown da 7079C337 ativo, com a
// 9322Y152 no campo (confirmado no Chromium, nos Pegamentos, no Peso e no PU
// NNPB); escolher uma produção abria uma da 7079C337.
test('a resposta que chega depois de se escrever outra referência não ativa a produção da anterior', async () => {
  const s = await abrir('http://dmo.test/Controlo/Pegamentos?modo=jobon', escolha);
  try {
    const preso = segurar(s);
    escrever(s.window, 'pegJobOnReferencia', '5810').form.requestSubmit();
    await esperar();
    escrever(s.window, 'pegJobOnReferencia', '7245');
    preso.soltar();
    await esperar();
    assert.deepEqual(s.navegou, []);
    assert.deepEqual(producoesDe(s.document), ['Escolher produção', '2026001 · M1', '2026002 · M2']);
    const producao = s.document.getElementById('pegJobOn');
    assert.ok(producao.disabled, 'uma produção da 5810 não se abre com a 7245 no campo');
    escrever(s.window, 'pegJobOnReferencia', '5810 ');
    assert.equal(producao.disabled, false, 'voltar ao texto das produções ativa-as');
    escrever(s.window, 'pegJobOnReferencia', '7245');
    assert.ok(producao.disabled);

    preso.repor();
    s.document.getElementById('pegJobOnReferencia').form.requestSubmit();
    await esperar();
    assert.deepEqual(s.navegou, []);
    assert.equal(s.document.getElementById('pegJobOn').disabled, false, 'as produções da referência aplicada escolhem-se');
    assert.deepEqual(producoesDe(s.document), ['Escolher produção', '2026101 · M1']);
  } finally {
    s.dom.window.close();
  }
});

test('no Histórico, a resposta que chega depois de se escrever não repõe a produção da referência anterior', async () => {
  const s = await abrir('http://dmo.test/JobOn/Index?vista=historico&referencia=5810', historico);
  try {
    const preso = segurar(s);
    escolherComRato(s.window, 'histProducao', '2026002');
    await esperar();
    // A escolha da produção ainda a caminho: o operador escreve outra referência.
    escrever(s.window, 'histReferencia', '72');
    preso.soltar();
    await esperar();
    assert.equal(s.document.getElementById('histProducao').value, '', 'a produção da 5810 não volta com a resposta');
    preso.repor();
    await esperar(400);
    assert.deepEqual(s.navegou, []);
    const ultimo = s.pedidos.at(-1).url.searchParams;
    assert.equal(ultimo.get('referencia'), '72');
    assert.equal(ultimo.get('producao'), '', 'a procura da 72 não leva a produção escolhida para a 5810');
  } finally {
    s.dom.window.close();
  }
});

test('o Histórico procura ao escrever, sem recarregar, e a produção da referência anterior sai', async () => {
  const s = await abrir('http://dmo.test/JobOn/Index?vista=historico&referencia=5810&producao=2026002', historico);
  try {
    const titulo = s.document.getElementById('titulo');
    const historia = s.window.history.length;
    assert.equal(s.document.getElementById('histProducao').value, '2026002');

    const campo = escrever(s.window, 'histReferencia', '7');
    assert.equal(s.document.getElementById('histProducao').value, '', 'a produção da 5810 sai logo ao escrever');
    await esperar(100);
    escrever(s.window, 'histReferencia', '72');
    await esperar(100);
    assert.deepEqual(s.pedidos, [], 'espera que se pare de escrever');
    await esperar(400);
    assert.deepEqual(s.navegou, []);
    assert.equal(s.pedidos.length, 1, 'uma procura só, 350 ms depois da última tecla');
    assert.equal(s.pedidos[0].url.searchParams.get('referencia'), '72');
    assert.equal(s.pedidos[0].url.searchParams.get('producao'), '');
    assert.equal(s.document.getElementById('titulo'), titulo);
    assert.equal(s.document.getElementById('histReferencia'), campo);
    assert.equal(s.document.activeElement, campo, 'o foco fica no campo');
    assert.equal(campo.value, '72');
    assert.match(s.document.getElementById('histResultados').textContent, /7245 2026101/);
    assert.doesNotMatch(s.document.getElementById('histResultados').textContent, /5810/);
    assert.equal(s.window.location.search, '?vista=historico&referencia=72&producao=');
    assert.equal(s.window.history.length, historia, 'cada tecla não enche o «voltar»');

    // O início da referência já traz as produções dela no dropdown (nunca as
    // de uma referência com «72» a meio); escolher uma aplica logo.
    assert.deepEqual(Array.from(s.document.querySelectorAll('#histProducao option'), (opcao) => opcao.value), ['', '2026101']);
    escolherComRato(s.window, 'histProducao', '2026101');
    await esperar();
    assert.deepEqual(s.navegou, []);
    assert.equal(s.pedidos.at(-1).url.searchParams.get('producao'), '2026101');
    assert.equal(s.document.getElementById('histProducao').value, '2026101');
  } finally {
    s.dom.window.close();
  }
});

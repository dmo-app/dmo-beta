const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');

// Os cartões CM, MF e BQ da folha do Job On em miniatura, com as marcas que
// a página desenha (Pages/JobOn/Index.cshtml). «Retirar» só vem no Novo Job
// On e nunca no MF (deep sweep, 18/10/2026, A1-M3): no Editar gravava com
// sucesso e não retirava nada, e no MF era sempre recusado. Sem o botão, o
// cartão tem de funcionar na mesma (Trocar, escolher).
function cartao(tipo, { escolhida, retirar }) {
  const opcao = escolhida ? `<option value="${escolhida.id}" selected="selected">${escolhida.texto}</option>` : '';
  return `<section class="job-tool-card" data-tool-card="${tipo}">
    <details class="dmo-options job-tool-menu">
      <summary class="job-tool-card-head"><span class="job-tool-badge">${tipo}</span><span class="job-tool-card-hint" data-tool-hint>${escolhida ? '' : 'Associar ou criar'}</span></summary>
      <div class="dmo-options-list">
        <button type="button" class="dmo-options-item" data-tool-associate>${escolhida ? 'Trocar' : 'Associar'} ${tipo}</button>
        <a class="dmo-options-item" href="/Ferramentas?criar=1&amp;type=${tipo}" data-tool-create>Criar ${tipo}</a>
        ${retirar ? `<button type="button" class="dmo-options-item" data-tool-clear${escolhida ? '' : ' hidden'}>Retirar ${tipo}</button>` : ''}
      </div>
    </details>
    <div class="job-tool-chosen" data-tool-chosen${escolhida ? '' : ' hidden'}>
      <strong class="job-tool-card-title" data-tool-title>${escolhida ? escolhida.texto.split(' · ').slice(0, 2).join(' · ') : ''}</strong>
    </div>
    <div class="dmo-field job-tool-input" data-tool-search>
      <select aria-label="Procurar ${tipo}" name="${tipo.toLowerCase()}_tool_id" data-facts-type="${tipo}" data-dmo-filter>
        <option value="">Escreva a referência da ${tipo}</option>${opcao}
        <option value="N-${tipo}">NOVA · Lote 9 · B1</option>
      </select>
    </div>
  </section>`;
}

const FERRAMENTAS = {
  CM: { id: 'T3', texto: '5447 · Lote 3 · B1 · C3' },
  MF: { id: 'MF-TOOL-9', texto: '5810 · Lote 1 · B1' },
  BQ: { id: 'BQ-TOOL-9', texto: 'T194 · Lote 12 · B1' },
};

async function abrirFolha({ edicao }) {
  const cartoes = ['CM', 'MF', 'BQ']
    .map((tipo) => cartao(tipo, { escolhida: FERRAMENTAS[tipo], retirar: !edicao && tipo !== 'MF' }))
    .join('');
  const dom = new JSDOM(`<!doctype html><body>
    <form id="lightForm" method="post">
      ${edicao ? '<input type="hidden" id="lightEditId" name="jobon_edit_id" value="1010" />' : ''}
      <input id="lightReference" name="reference" value="1075C142" />
      <select id="lightMachine" name="machine_id"><option value="">Escolha</option><option value="1" selected>B1</option><option value="2">C3</option></select>
      <div class="job-tool-filter-bar"><label><input type="checkbox" id="lightShowAllTools" /></label><span id="lightToolFilterStatus"></span></div>
      <div class="beta-light-tools" id="lightTools">${cartoes}</div>
    </form></body>`, { url: 'http://dmo.test/JobOn?jobon_id=1010&editar=1', runScripts: 'outside-only' });
  const { window } = dom;
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-tool-picker.js'), 'utf8'));
  const doc = window.document;
  // O script liga os cartões no DOMContentLoaded, como na página.
  if (doc.readyState === 'loading') await new Promise((pronto) => doc.addEventListener('DOMContentLoaded', pronto));
  const card = (tipo) => doc.querySelector(`[data-tool-card="${tipo}"]`);
  const parte = (tipo, seletor) => card(tipo).querySelector(seletor);
  const escolher = (tipo, valor) => {
    const select = parte(tipo, 'select');
    select.value = valor;
    select.dispatchEvent(new window.Event('change', { bubbles: true }));
  };
  return { dom, doc, card, parte, escolher };
}

test('Editar: sem «Retirar» em nenhum cartão, e «Trocar» e escolher funcionam na mesma', async () => {
  const { dom, doc, card, parte, escolher } = await abrirFolha({ edicao: true });
  try {
    assert.equal(doc.querySelectorAll('[data-tool-clear]').length, 0);
    for (const tipo of ['CM', 'MF', 'BQ']) {
      assert.equal(parte(tipo, '[data-tool-search]').hidden, true, `${tipo}: a procura começa fechada`);
      parte(tipo, '[data-tool-associate]').click();
      assert.ok(card(tipo).classList.contains('is-associating'), `${tipo}: «Trocar» abre a procura`);
      assert.equal(parte(tipo, '[data-tool-search]').hidden, false);
      escolher(tipo, `N-${tipo}`);
      assert.equal(parte(tipo, '[data-tool-title]').textContent, 'NOVA · Lote 9', `${tipo}: o cartão mostra a escolhida`);
      assert.equal(parte(tipo, '[data-tool-search]').hidden, true, `${tipo}: escolhida, a procura fecha`);
      assert.equal(parte(tipo, '[data-tool-associate]').textContent, `Trocar ${tipo}`);
    }
  } finally {
    dom.window.close();
  }
});

test('Novo Job On: «Retirar» na CM e na BQ limpa a escolha; o MF não o tem', async () => {
  const { dom, parte } = await abrirFolha({ edicao: false });
  try {
    assert.equal(parte('MF', '[data-tool-clear]'), null);
    for (const tipo of ['CM', 'BQ']) {
      const retirar = parte(tipo, '[data-tool-clear]');
      assert.equal(retirar.hidden, false, `${tipo}: escolhida, «Retirar» aparece`);
      retirar.click();
      assert.equal(parte(tipo, 'select').value, '');
      assert.equal(parte(tipo, '[data-tool-title]').textContent, '');
      assert.equal(parte(tipo, '[data-tool-chosen]').hidden, true);
      assert.equal(parte(tipo, '[data-tool-hint]').textContent, 'Associar ou criar');
      assert.equal(parte(tipo, '[data-tool-associate]').textContent, `Associar ${tipo}`);
      assert.equal(retirar.hidden, true, `${tipo}: sem escolha, «Retirar» some`);
    }
    assert.equal(parte('MF', '[data-tool-search]').hidden, true, 'MF: o cartão está ligado (a procura começa fechada)');
    parte('MF', '[data-tool-associate]').click();
    assert.equal(parte('MF', '[data-tool-search]').hidden, false, 'MF: «Trocar» abre a procura');
  } finally {
    dom.window.close();
  }
});

test('«Criar BQ» abre o registo das Ferramentas com a máquina da folha no momento do clique', async () => {
  // O Diogo, 18/10/2026: «estamos no jobon 6018Y232 C2, mudar boquilha já
  // vem com C2 BQ filtrado». Uma só página de registar ferramenta, que abre
  // com o tipo e a máquina da produção já escolhidos.
  const { dom, doc, parte } = await abrirFolha({ edicao: false });
  try {
    // Sem navegar: o teste só lê o link que o clique refaz.
    doc.addEventListener('click', (event) => event.preventDefault());
    const criar = parte('BQ', '[data-tool-create]');
    const maquina = doc.getElementById('lightMachine');
    maquina.value = '2';
    criar.click();
    const link = new URL(criar.href);
    assert.equal(link.pathname, '/Ferramentas');
    assert.equal(link.searchParams.get('type'), 'BQ');
    assert.deepEqual(link.searchParams.getAll('machine_ids'), ['2']);
    const regresso = link.searchParams.get('return');
    assert.match(regresso, /^\/JobOn\?novo=1&retoma=1&reference=1075C142&machine_id=2&/);
    assert.ok(regresso.endsWith('&bq_tool_id={tool_id}#lightSheet'), regresso);

    maquina.value = '';
    criar.click();
    assert.equal(new URL(criar.href).searchParams.has('machine_ids'), false, 'sem máquina na folha, o registo abre sem máquina');
  } finally {
    dom.window.close();
  }
});

// A PROCURA DE CADA CARTÃO (o Diogo, 16/10/2026): as sugestões que o servidor
// devolve seguem a máquina e a referência da folha, na CM a MF escolhida e na
// MF a CM escolhida; o catálogo inteiro só com «Mostrar todas as
// ferramentas». A folha de teste tem as marcas do Index.cshtml do Job On
// (lidas do ficheiro): se a página mudar um id ou um data-*, o teste muda com
// ela. O dmo-suggest chama select.dmoSource(texto); aqui chama-se igual.
const PAGINA = path.resolve(__dirname, '../../src/DMO.Alpha.Web/Pages/JobOn/Index.cshtml');
const cshtml = fs.readFileSync(PAGINA, 'utf8');

// A marca de abertura como o Index.cshtml a desenha: @tipoSlot e @campoSlot
// com os do cartão, e sem os atributos que o Razor preenche com o modelo.
function marca(re, nome, tipo) {
  const m = cshtml.match(re);
  if (!m) throw new Error(`${nome} não está no Index.cshtml do Job On`);
  let tag = m[0];
  if (tipo) tag = tag.replaceAll('@tipoSlot', tipo).replaceAll('@campoSlot', `${tipo.toLowerCase()}_tool_id`);
  tag = tag.replace(/\s[\w-]+="[^"]*@[^"]*"/g, '');
  assert.ok(!tag.includes('@'), `${nome}: ficou Razor por tirar em ${tag}`);
  return tag;
}
const porId = (id) => marca(new RegExp(`<\\w+\\b[^>]*\\bid="${id}"[^>]*>`), id);

// Os cartões que a página desenha, pela ordem do @foreach.
const SLOTS = [...cshtml.matchAll(/\("(\w+)", "[^"]*", Model\.Requested\w+ToolId\)/g)].map((m) => m[1]);
// O que a procura de cada cartão segue, além da máquina e da referência.
const SEGUE = { CM: 'MF', MF: 'CM', BQ: null };

async function abrirProcura() {
  const cartoes = SLOTS.map((tipo) => `${marca(/<section\b[^>]*\bdata-tool-card="@tipoSlot"[^>]*>/, 'o cartão', tipo)}
      ${marca(/<div\b[^>]*\bdata-tool-search\b[^>]*>/, 'a procura do cartão')}
        ${marca(/<select\b[^>]*\bdata-facts-type="@tipoSlot"[^>]*>/, 'o select do cartão', tipo)}
          <option value="">Escreva a referência da ${tipo}</option><option value="${tipo}-5447-2">5447 · Lote 2 · B2</option>
        </select>
      </div>
    </section>`).join('');
  const dom = new JSDOM(`<!doctype html><body>
    ${porId('lightReference')}
    ${porId('lightMachine')}<option value="">Selecionar</option><option value="1">B1</option><option value="3">B2</option></select>
    ${marca(/<div class="job-tool-filter-bar"[^>]*>/, 'a barra do filtro')}
      <label>${porId('lightShowAllTools')} Mostrar todas as ferramentas</label>
      ${porId('lightToolFilterStatus')}</span>
    </div>
    ${porId('lightTools')}${cartoes}</div>
    </body>`, { url: 'http://dmo.test/JobOn?novo=1', runScripts: 'outside-only' });
  const { window } = dom;
  const doc = window.document;
  // O servidor de teste regista cada pedido e devolve uma sugestão.
  const pedidos = [];
  window.fetch = async (url) => {
    pedidos.push(new URL(String(url), window.location.href));
    return { ok: true, json: async () => ({ items: [{ toolId: 'T-1', reference: '5447', lot: 2, machineCodes: 'B2' }], total: 1 }) };
  };
  window.eval(fs.readFileSync(path.join(assets, 'js/jobon-tool-picker.js'), 'utf8'));
  if (doc.readyState === 'loading') await new Promise((pronto) => doc.addEventListener('DOMContentLoaded', pronto));
  const select = (tipo) => {
    const campo = doc.querySelector(`#lightTools select[data-facts-type="${tipo}"]`);
    assert.ok(campo, `o cartão ${tipo} não está na folha de teste`);
    return campo;
  };
  const mudar = (campo, valor) => {
    campo.value = valor;
    campo.dispatchEvent(new window.Event('change', { bubbles: true }));
  };
  const escrever = (campo, valor) => {
    campo.value = valor;
    campo.dispatchEvent(new window.Event('input', { bubbles: true }));
  };
  // Procura como o dmo-suggest: devolve o pedido que chegou ao servidor.
  const procurar = async (tipo, texto) => {
    const antes = pedidos.length;
    const resultado = await select(tipo).dmoSource(texto);
    assert.equal(pedidos.length, antes + 1, `${tipo}: uma procura, um pedido`);
    assert.deepEqual(resultado.items.map((item) => item.value), ['T-1'], `${tipo}: a sugestão do servidor chega à lista`);
    return pedidos[pedidos.length - 1];
  };
  const folha = {
    maquina: doc.getElementById('lightMachine'),
    referencia: doc.getElementById('lightReference'),
    todas: doc.getElementById('lightShowAllTools'),
  };
  return { dom, select, mudar, escrever, procurar, folha };
}

test('a página desenha os cartões CM, MF e BQ, e a procura de cada um está aqui', () => {
  assert.deepEqual(SLOTS, Object.keys(SEGUE));
  assert.match(cshtml, /<script src="~\/js\/jobon-tool-picker\.js"/, 'o Index.cshtml carrega o seletor');
});

for (const tipo of Object.keys(SEGUE)) {
  const outra = SEGUE[tipo];
  const daOutra = outra && `${outra.toLowerCase()}_tool_id`;
  const regra = outra ? `, da referência escrita e da ${outra} escolhida` : ' e da referência escrita, sem a CM nem a MF';
  test(`a procura da ${tipo} pede as sugestões da máquina escolhida${regra}`, async () => {
    const { dom, select, mudar, escrever, procurar, folha } = await abrirProcura();
    try {
      mudar(folha.maquina, '3');
      escrever(folha.referencia, '5447C001');
      for (const slot of Object.keys(SEGUE).filter((s) => s !== tipo)) mudar(select(slot), `${slot}-5447-2`);

      let pedido = await procurar(tipo, '5447');
      assert.equal(pedido.pathname, '/JobOn');
      assert.equal(pedido.searchParams.get('handler'), 'ToolCandidates');
      assert.equal(pedido.searchParams.get('type'), tipo);
      assert.equal(pedido.searchParams.get('q'), '5447', 'o que se escreveu na procura');
      assert.equal(pedido.searchParams.get('machine_id'), '3', 'a máquina escolhida na folha');
      assert.equal(pedido.searchParams.get('reference'), '5447C001', 'a referência escrita na folha');
      for (const [slot, parametro] of [['MF', 'mf_tool_id'], ['CM', 'cm_tool_id']]) {
        if (slot === outra) assert.equal(pedido.searchParams.get(parametro), `${slot}-5447-2`, `a ${slot} escolhida`);
        else assert.equal(pedido.searchParams.has(parametro), false, `a procura da ${tipo} não segue a ${slot}`);
      }
      assert.notEqual(pedido.searchParams.get('all'), 'true', 'sem «Mostrar todas», só as sugestões filtradas');

      // A procura segue a folha no momento em que se procura.
      mudar(folha.maquina, '1');
      escrever(folha.referencia, '0109S022');
      pedido = await procurar(tipo, '');
      assert.equal(pedido.searchParams.get('machine_id'), '1');
      assert.equal(pedido.searchParams.get('reference'), '0109S022');

      // Sem máquina na folha, a procura não leva máquina; o resto fica.
      mudar(folha.maquina, '');
      pedido = await procurar(tipo, '');
      assert.equal(pedido.searchParams.has('machine_id'), false, 'sem máquina na folha, a procura não leva máquina');
      assert.equal(pedido.searchParams.get('reference'), '0109S022');
      if (outra) assert.equal(pedido.searchParams.get(daOutra), `${outra}-5447-2`);

      // Sem a outra ferramenta escolhida, a procura não a leva; a máquina fica.
      if (outra) {
        mudar(folha.maquina, '3');
        mudar(select(outra), '');
        pedido = await procurar(tipo, '');
        assert.equal(pedido.searchParams.has(daOutra), false, `sem ${outra} escolhida, a procura não a leva`);
        assert.equal(pedido.searchParams.get('machine_id'), '3');
      }
    } finally {
      dom.window.close();
    }
  });
}

test('«Mostrar todas as ferramentas» só pede o catálogo inteiro quando está marcado', async () => {
  const { dom, procurar, folha } = await abrirProcura();
  try {
    for (const tipo of Object.keys(SEGUE)) {
      folha.todas.checked = true;
      folha.todas.dispatchEvent(new dom.window.Event('change', { bubbles: true }));
      assert.equal((await procurar(tipo, '5447')).searchParams.get('all'), 'true', `${tipo}: marcado, o catálogo inteiro`);
      folha.todas.checked = false;
      folha.todas.dispatchEvent(new dom.window.Event('change', { bubbles: true }));
      assert.notEqual((await procurar(tipo, '5447')).searchParams.get('all'), 'true', `${tipo}: desmarcado, só as sugestões filtradas`);
    }
  } finally {
    dom.window.close();
  }
});

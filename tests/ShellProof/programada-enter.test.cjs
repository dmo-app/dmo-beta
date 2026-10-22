// Reparação Programada (teste no browser real, 22/10/2026): o Enter na
// Referência não procurava. O rpFiltros tem dois campos de texto e nenhum
// botão seu (o «Adicionar» é do rpAssociar, por form=), e aí o browser não
// submete com Enter. O script é o da página, tirado de
// Pages/Ferramentas/ReparacaoProgramada.cshtml.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const pagina = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/Pages/Ferramentas/ReparacaoProgramada.cshtml'), 'utf8');
const script = (pagina.match(/<script>((?:(?!<\/script>)[\s\S])*?rpEnterLigado[\s\S]*?)<\/script>/) || [])[1];
const canonico = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/0_ASSET_CANONICAL_INTERACTIONS.js'), 'utf8');

// A linha da procura como a página a desenha, com a lista escolhida.
const html = `<main data-dmo-troca>
  <form id="rpFiltros" method="get" action="/Ferramentas/ReparacaoProgramada" class="rp-filtros" role="search">
    <input type="hidden" name="lista" value="3" />
    <div class="dmo-field"><label for="rpReferencia">Referência</label><input id="rpReferencia" name="referencia" value="" autocomplete="off" data-dmo-maiusculas /></div>
    <div class="dmo-field"><label for="rpLote">Lote</label><input id="rpLote" name="lote" value="" autocomplete="off" data-dmo-maiusculas /></div>
    <div class="dmo-field"><label for="rpTipo">Tipo</label><select id="rpTipo" name="tipo" data-dmo-autosubmit><option value="" selected>Todos</option><option value="CM">CM</option></select></div>
    <button type="submit" form="rpAssociar" class="dmo-button" id="rpAssociarBotao">Adicionar</button>
    <a class="dmo-button" id="rpLimpar" href="/Ferramentas/ReparacaoProgramada?lista=3">Limpar filtros</a>
  </form>
  <form id="rpAssociar" method="post" action="/Ferramentas/ReparacaoProgramada?handler=Associar">
    <input type="hidden" name="referencia" value="" />
    <input type="checkbox" name="ferramenta" value="t-cm-5447-3" checked />
  </form>
</main>`;

function abrir({ comCanonico }) {
  const dom = new JSDOM(`<!doctype html><title>DMO</title><body>${html}</body>`, { url: 'http://dmo.test/Ferramentas/ReparacaoProgramada?lista=3', runScripts: 'outside-only' });
  const { window } = dom;
  window.Element.prototype.scrollIntoView = function () {};
  window.fetch = async () => { throw new Error('sem rede no teste'); };
  if (comCanonico) window.eval(canonico);
  window.eval(script);
  const envios = [];
  // Cada envio fica registado com o formulário e o que levaria; nada navega.
  window.document.addEventListener('submit', (event) => {
    const form = event.target;
    envios.push({ form: form.id, campos: Object.fromEntries(new window.FormData(form, event.submitter || null)), submitter: event.submitter ? event.submitter.id : null });
    event.preventDefault();
  }, true);
  return { dom, window, document: window.document, envios };
}

function enter(window, campo) {
  const evento = new window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
  campo.dispatchEvent(evento);
  return evento;
}

test('o script do Enter está na página', () => {
  assert.ok(script, 'o <script> com rpEnterLigado existe na ReparacaoProgramada.cshtml');
});

for (const comCanonico of [false, true]) {
  test(`Enter na Referência e no Lote submete o rpFiltros, nunca o rpAssociar${comCanonico ? ' (com o script da casa)' : ''}`, () => {
    const { dom, window, document, envios } = abrir({ comCanonico });
    try {
      const referencia = document.getElementById('rpReferencia');
      referencia.value = '5809';
      const e1 = enter(window, referencia);
      assert.equal(e1.defaultPrevented, true);
      assert.equal(envios.length, 1);
      assert.equal(envios[0].form, 'rpFiltros');
      assert.equal(envios[0].submitter, null, 'sem o «Adicionar» como botão');
      assert.equal(envios[0].campos.referencia, '5809');
      assert.equal(envios[0].campos.lote, '');
      assert.equal(envios[0].campos.lista, '3', 'a lista escolhida segue');

      const lote = document.getElementById('rpLote');
      lote.value = '1';
      enter(window, lote);
      assert.equal(envios.length, 2);
      assert.equal(envios[1].form, 'rpFiltros');
      assert.deepEqual([envios[1].campos.referencia, envios[1].campos.lote], ['5809', '1']);

      assert.equal(envios.some((envio) => envio.form === 'rpAssociar'), false, 'o Enter nunca adiciona');
      // Outras teclas não submetem.
      referencia.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true }));
      assert.equal(envios.length, 2);
    } finally {
      dom.window.close();
    }
  });
}

test('um Enter que outro script já tratou (a sugestão escolhida) não submete outra vez', () => {
  const { dom, window, document, envios } = abrir({ comCanonico: false });
  try {
    const referencia = document.getElementById('rpReferencia');
    referencia.addEventListener('keydown', (event) => event.preventDefault());
    enter(window, referencia);
    assert.equal(envios.length, 0);
  } finally {
    dom.window.close();
  }
});

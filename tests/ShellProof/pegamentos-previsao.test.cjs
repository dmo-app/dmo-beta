const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const { JSDOM } = require('jsdom');

const assets = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot');
const { casos } = JSON.parse(fs.readFileSync(path.join(__dirname, 'pegamentos-previsao.casos.json'), 'utf8'));

// Sweep de 19/10/2026, 3.12 (B-pegamentos-6): a pré-visualização da linha
// nova dos Pegamentos não marcava fora um valor no limite exato, nunca
// marcava a costura nem a contra costura, e mostrava «-0.00» onde o servidor
// mostra «+0.00». Os casos são os mesmos que o servidor verifica
// (PegamentosPrevisaoCasosTests): a pré-visualização é o que fica gravado.
function linhaNova(caso) {
  const dom = new JSDOM(`<!doctype html><body class="peg-page">
    <table data-peg-registo data-peg-tolerancia="${caso.tolerancia}" data-peg-limite-inferior="${caso.limiteInferior}" data-peg-limite-superior="${caso.limiteSuperior}"><tbody>
      <tr class="peg-linha-nova" data-peg-linha-nova><td>1</td>
        <td><input id="costura" data-peg-costura></td>
        <td><input id="contra" data-peg-contra></td>
        <td id="oval" data-peg-oval>—</td><td id="media" data-peg-media>—</td><td>Enter regista</td><td></td></tr>
    </tbody></table></body>`, { runScripts: 'outside-only' });
  dom.window.eval(fs.readFileSync(path.join(assets, 'js/pegamentos-registo.js'), 'utf8'));
  return dom;
}

function escrever(window, campo, valor) {
  campo.value = valor;
  campo.dispatchEvent(new window.Event('input', { bubbles: true }));
}

for (const caso of casos) {
  test(`3.12: a pré-visualização é a do servidor: ${caso.nome}`, () => {
    const dom = linhaNova(caso);
    const { window } = dom;
    const document = window.document;
    try {
      escrever(window, document.getElementById('costura'), caso.costura);
      escrever(window, document.getElementById('contra'), caso.contra);
      const fora = (id) => document.getElementById(id).classList.contains('peg-cell-bad');
      assert.equal(document.getElementById('oval').textContent, caso.oval, 'ovalização');
      assert.equal(document.getElementById('media').textContent, caso.media, 'média');
      assert.equal(document.getElementById('costura').parentNode.classList.contains('peg-cell-bad'), caso.foraCostura, 'costura fora');
      assert.equal(document.getElementById('contra').parentNode.classList.contains('peg-cell-bad'), caso.foraContra, 'contra costura fora');
      assert.equal(fora('media'), caso.foraMedia, 'média fora');
      assert.equal(fora('oval'), caso.ovalVermelha, 'ovalização a vermelho');
    } finally {
      dom.window.close();
    }
  });
}

test('3.12: um valor ilegível não mostra contas nem marca nada', () => {
  const dom = linhaNova(casos[0]);
  const { window } = dom;
  const document = window.document;
  try {
    escrever(window, document.getElementById('costura'), '70.20');
    escrever(window, document.getElementById('contra'), '70,1,2');
    assert.equal(document.getElementById('oval').textContent, '—');
    assert.equal(document.getElementById('media').textContent, '70.20', 'sem contra legível, a média é a costura');
    assert.equal(document.getElementById('contra').parentNode.classList.contains('peg-cell-bad'), false);
    escrever(window, document.getElementById('costura'), 'abc');
    assert.equal(document.getElementById('media').textContent, '—');
    assert.equal(document.getElementById('costura').parentNode.classList.contains('peg-cell-bad'), false);
    assert.equal(document.getElementById('media').classList.contains('peg-cell-bad'), false);
  } finally {
    dom.window.close();
  }
});

test('3.12: sem corredor (ferramenta sem nominal) nada fica fora', () => {
  const dom = linhaNova({ tolerancia: '0.20', limiteInferior: '', limiteSuperior: '' });
  const { window } = dom;
  const document = window.document;
  try {
    escrever(window, document.getElementById('costura'), '99.00');
    assert.equal(document.getElementById('media').textContent, '99.00');
    assert.equal(document.getElementById('costura').parentNode.classList.contains('peg-cell-bad'), false);
    assert.equal(document.getElementById('media').classList.contains('peg-cell-bad'), false);
  } finally {
    dom.window.close();
  }
});

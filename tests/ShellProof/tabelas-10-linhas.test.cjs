const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

// DMO-747 (A-0520), Diogo 04/11/2026: «tabelas no mínimo deveriam mostrar 10» (Boquilhas › Registos desta
// boquilha só mostrava 1 linha depois de se tirar a altura presa à janela). Cada tabela mostra pelo menos
// 10 linhas antes de deslizar por dentro, e o que a leva cresce com ela (a página desliza, não fica presa).
const css = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/css/dmo-estrutura.css'), 'utf8');

test('as tabelas mostram pelo menos 10 linhas e os cartões delas crescem com elas', () => {
  assert.match(css, /--dmo-tabela-10-linhas:400px/);
  assert.match(css, /html:root body \.dmo-table-wrap:not\(#_\):not\(#_\)\{max-height:max\(var\(--dmo-tabela-10-linhas\),[^}]*\}/);
  assert.match(css, /html:root body :is\(main,main \*\):has\(\.dmo-table-wrap\):not\(#_\):not\(#_\)\{min-height:min-content\}/);
  // A regra das tabelas não prende a página: nada de overflow:hidden nem altura fixa à janela.
  const bloco = css.slice(css.indexOf('TABELAS: pelo menos 10 linhas'));
  assert.doesNotMatch(bloco, /overflow\s*:\s*hidden|[^-]height\s*:\s*calc\(100vh/);
});

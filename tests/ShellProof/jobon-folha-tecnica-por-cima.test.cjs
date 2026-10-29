const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

const css = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/css/dmo-jobon-folha-1366.css'), 'utf8');

// A «Informação técnica» abre por baixo do cartão, por cima do que está atrás, sem empurrar nada (Diogo, 29/10/2026).
test('No ver, a Informação técnica aberta fica por cima (absoluta, por baixo do cartão) e não faz a folha crescer', () => {
  const corpo = css.match(/:not\(\.editing\) \.sec>details\.more\[open\]>\.more-body:not\(#_\)\{([^}]*)\}/);
  assert.ok(corpo, 'falta a regra do corpo aberto');
  assert.match(corpo[1], /position:absolute/);
  assert.match(corpo[1], /top:calc\(100% \+ 2px\)/);
  assert.doesNotMatch(css, /:not\(\.editing\):has\(\.sec>details\.more\[open\]\)[^{]*\{[^}]*height:auto/, 'a folha não pode crescer');
  assert.doesNotMatch(css, /:not\(\.editing\):has\(\.sec>details\.more\[open\]\)[^{]*\{[^}]*flex:none/, 'as zonas não podem encolher');
});

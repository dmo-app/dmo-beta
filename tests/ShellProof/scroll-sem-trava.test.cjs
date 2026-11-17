const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

// DMO-735 (A-0508), Diogo 04/11/2026: «scroll ficou preso» no Job On (oficial .254).
// Uma caixa que cresce com o conteúdo, com overflow:auto e overscroll-behavior:contain,
// não desliza nem deixa a página deslizar com a roda do rato. Estas caixas não o podem ter.
const css = (f) => fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/css', f), 'utf8');

test('a folha do Job On e os corpos das páginas não prendem a roda do rato', () => {
  const regras = [
    [css('dmo-jobon-folha-1366.css'), /main\.jof2\.jof2\.jof2>\.wrap>\.sheet\{[^}]*\}/g],
    [css('dmo-estrutura.css'), /\.dmo-pagina-corpo\{[^}]*\}/g],
    [css('dmo-estrutura.css'), /\.dmo-painel-corpo\{[^}]*\}/g],
  ];
  for (const [txt, re] of regras) {
    const achadas = txt.match(re) || [];
    assert.ok(achadas.length > 0, String(re));
    for (const r of achadas) assert.doesNotMatch(r, /overscroll-behavior\s*:\s*contain/, r);
  }
});

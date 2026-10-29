const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

const css = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/css/dmo-jobon-folha.css'), 'utf8');

// O número do botão «Linhas» num círculo visível, centrado na vertical com o texto (Diogo, 29/10/2026).
test('O número do botão «Linhas» fica num círculo com contorno, centrado com o texto', () => {
  const botao = css.match(/\.jof2 \.rail-toggle:not\(#_\)\{([^}]*)\}/);
  assert.ok(botao, 'falta a regra do botão');
  assert.match(botao[1], /align-items:center/);
  const numero = css.match(/\.jof2 \.rail-toggle \.count:not\(#_\)\{([^}]*)\}/);
  assert.ok(numero, 'falta a regra do número');
  for (const regra of [/align-items:center/, /justify-content:center/, /border-radius:999px/, /border:1px solid currentColor/, /line-height:1/]) {
    assert.match(numero[1], regra);
  }
});

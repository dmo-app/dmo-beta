const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

// DEFINIÇÕES › A MINHA CONTA A 683PX (testador, 16/11/2026; DMO-773): a linha
// dos módulos (Planeamento … Reparação Interna) saía do cartão e a página
// deslizava para o lado, porque o invólucro da tabela tinha overflow:visible.
// Agora desliza por dentro do cartão, como as outras tabelas (dmo-table-wrap).
// Medido no Chromium a 683: documento 683px, invólucro com scroll próprio.

const css = fs.readFileSync(path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/css/d1366-armazem-reparacao-admin.css'), 'utf8');

test('A tabela dos módulos da conta desliza dentro do cartão, não alarga a página', () => {
  const regras = css.split('\n').filter((l) => l.includes('.dmo-table-wrap:has(>.def-conta-modulos)'));
  assert.ok(regras.length > 0, 'a regra do invólucro existe');
  for (const regra of regras) {
    assert.doesNotMatch(regra, /overflow:\s*visible/, regra);
  }
  assert.ok(regras.some((r) => /overflow-x:\s*auto/.test(r)), 'o invólucro desliza na horizontal');
});

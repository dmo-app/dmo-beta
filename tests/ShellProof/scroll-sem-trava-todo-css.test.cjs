const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');

// DMO-735 (A-0508), Diogo 04/11/2026: Planeamento › Job On também ficava preso.
// Nenhum CSS da app pode ter overscroll-behavior:contain (prende a roda do rato).
test('nenhum CSS da app usa overscroll-behavior:contain', () => {
  const dir = path.resolve(__dirname, '../../src/DMO.Alpha.Web/wwwroot/css');
  for (const f of fs.readdirSync(dir).filter((n) => n.endsWith('.css'))) {
    assert.doesNotMatch(fs.readFileSync(path.join(dir, f), 'utf8'), /overscroll-behavior\s*:\s*contain/, f);
  }
});

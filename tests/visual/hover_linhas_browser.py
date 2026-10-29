"""O hover das linhas das tabelas com a letra sempre legível (Diogo, 29/10/2026: «hover deixa font invisível»).

Página sintética com as folhas de estilo reais (sem servidor):
  python tests/visual/hover_linhas_browser.py

Em claro e em escuro, com o Histórico do Controlo (linhas alternadas) e uma tabela normal:
passar o rato em cada linha dá letra com contraste >= 4,5:1 sobre o fundo da célula.
"""
import sys
import tempfile
from pathlib import Path

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium

www = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
CSS = ['0_ASSET_CANONICAL_DESIGN_SYSTEM.css', '0_ASSET_SHELL.css', 'beta-shell.css', 'css/dmo-normalize.css', 'beta-adapter.css',
       'css/dmo-design.css']


def linhas(n):
    return ''.join(f'<tr data-dmo-row data-id="r{i}"><td><input type="radio" name="abrir"></td><td>CM 2032 · Lote 1</td>'
                   f'<td>Resumo</td><td>2032C002 · 20260{i} · B1</td><td>—</td><td>Por aprovar</td></tr>' for i in range(1, n + 1))


TABELA = '<div class="dmo-table-wrap"><table class="dmo-table" {attr}><thead><tr><th></th><th>Ferramenta</th><th>Tipo</th><th>Referência</th><th>Data</th><th>Estado</th></tr></thead><tbody>' + linhas(5) + '</tbody></table></div>'
HTML = '<!doctype html><html><head><meta charset="utf-8">' + ''.join(f'<link rel="stylesheet" href="{(www / c).as_uri()}">' for c in CSS) \
    + '</head><body><main class="peso-page dmo-workspace">' + TABELA.replace('{attr}', 'data-dmo-list="Histórico do Controlo" id="hist"') \
    + TABELA.replace('{attr}', 'id="normal"') + '</main></body></html>'

MEDIR = '''(sel) => {
  const lum = c => { const m = c.match(/[\\d.]+/g).map(Number); const v = c.startsWith('color(') ? m.slice(0,3).map(x => x*255) : m.slice(0,3);
    const f = x => { x /= 255; return x <= .03928 ? x/12.92 : ((x+.055)/1.055) ** 2.4; };
    return .2126*f(v[0]) + .7152*f(v[1]) + .0722*f(v[2]); };
  const k = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + .05) / (y + .05); };
  const td = document.querySelector(sel + ' tbody tr:hover > td:nth-child(2)');
  if (!td) return null;
  const s = getComputedStyle(td);
  return { cor: s.color, fundo: s.backgroundColor, contraste: k(s.color, s.backgroundColor) };
}'''

falhas = []
pagina = Path(tempfile.mkdtemp()) / 'hover.html'
pagina.write_text(HTML, encoding='utf-8')
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for tema in ('light', 'dark'):
        page = browser.new_page(viewport={'width': 1366, 'height': 900}, color_scheme=tema)
        page.goto(pagina.as_uri())
        page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
        page.wait_for_timeout(300)
        for tabela in ('#hist', '#normal'):
            for i in (1, 2, 3, 4):
                page.locator(f'{tabela} tbody tr').nth(i - 1).hover()
                page.wait_for_timeout(250)  # a transição do fundo acaba
                m = page.evaluate(MEDIR, tabela)
                print(tema, tabela, i, m)
                if m is None:
                    falhas.append(f'{tema} {tabela} linha {i}: sem hover')
                elif m['contraste'] < 4.5:
                    falhas.append(f'{tema} {tabela} linha {i}: letra {m["cor"]} sobre {m["fundo"]} (contraste {m["contraste"]:.2f})')
        page.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Hover das linhas das tabelas legível, claro e escuro: ok')

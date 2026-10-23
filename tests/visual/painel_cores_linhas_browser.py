"""O painel lateral da Folha com as cores das linhas do Planeamento (Diogo, 23/10/2026).

Página sintética com as folhas de estilo reais da Folha (sem servidor):
  python tests/visual/painel_cores_linhas_browser.py

Em claro e em escuro:
- dois cartões de linhas diferentes (B1, C2) têm fundos calculados diferentes,
  e diferentes do cartão sem a classe da linha;
- o cartão escolhido mantém a cor de escolhido (a mesma de um cartão escolhido
  sem linha), não a da linha;
- o texto do selo está centrado (a folga de cima e a de baixo diferem <= 1px).
"""
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium

www = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
CSS = ['0_ASSET_CANONICAL_DESIGN_SYSTEM.css', '0_ASSET_SHELL.css', 'beta-shell.css', 'beta-production-overview.css',
       'css/dmo-normalize.css', 'beta-adapter.css', 'css/dmo-jobon-folha.css']


def cartao(id, linha, escolhido=False):
    classes = 'rail-item' + (f' dmo-linha-cor mch-{linha}' if linha else '') + (' selected' if escolhido else '')
    selo = f'<span class="mch dmo-linha-cor mch-{linha or "B1"}" id="{id}-selo">{linha or "B1"}</span>'
    return f'<a class="{classes}" id="{id}" href="#"><b>6018Y232{selo}</b><small>Produção 202603 · 09/09</small></a>'


HTML = '<!doctype html><html><head><meta charset="utf-8">' + ''.join(
    f'<link rel="stylesheet" href="/{c}">' for c in CSS) + '''</head>
<body><div class="jof2-gaveta"><aside class="rail is-open" id="jofRail" style="transform:none;visibility:visible">
<div class="rail-list">''' + cartao('b1', 'B1') + cartao('c2', 'C2') + cartao('sem', None) + cartao('esc', 'C3', True) \
    + cartao('escsem', None, True) + '</div></aside></div></body></html>'

MEDIR = '''() => {
  const bg = id => getComputedStyle(document.getElementById(id)).backgroundColor;
  const selo = document.getElementById('c2-selo'), r = selo.getBoundingClientRect();
  const t = document.createRange(); t.selectNodeContents(selo); const q = t.getBoundingClientRect();
  return { b1: bg('b1'), c2: bg('c2'), sem: bg('sem'), esc: bg('esc'), escsem: bg('escsem'),
           cima: q.top - r.top, baixo: r.bottom - q.bottom, carregou: getComputedStyle(selo).display };
}'''

falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for tema in ('light', 'dark'):
        page = browser.new_page(viewport={'width': 400, 'height': 700}, color_scheme=tema)
        def servir(route):
            path = urlparse(route.request.url).path
            if path == '/painel-cores.html':
                route.fulfill(content_type='text/html; charset=utf-8', body=HTML)
                return
            asset = (www / unquote(path).lstrip('/')).resolve()
            if asset.is_relative_to(www) and asset.is_file() and asset.suffix == '.css':
                route.fulfill(content_type='text/css', body=asset.read_bytes())
            else:
                route.fulfill(status=404, body='')

        page.route('**/*', servir)
        page.goto('http://dmo.test/painel-cores.html')
        page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
        page.wait_for_load_state('load')
        page.wait_for_timeout(400)  # as transições de fundo do cartão acabam
        m = page.evaluate(MEDIR)
        print(tema, m)
        if m['carregou'] not in ('grid', 'inline-grid'):
            falhas.append(f'{tema}: CSS da Folha não carregou')
        if m['b1'] == m['c2']:
            falhas.append(f'{tema}: B1 e C2 com o mesmo fundo {m["b1"]}')
        if m['sem'] in (m['b1'], m['c2']):
            falhas.append(f'{tema}: a cor da linha não muda o fundo do cartão')
        if m['esc'] != m['escsem']:
            falhas.append(f'{tema}: o escolhido perdeu a cor de escolhido ({m["esc"]} em vez de {m["escsem"]})')
        if abs(m['cima'] - m['baixo']) > 1:
            falhas.append(f'{tema}: selo descentrado (cima {m["cima"]:.1f}px, baixo {m["baixo"]:.1f}px)')
        page.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Painel lateral com as cores das linhas: ok')

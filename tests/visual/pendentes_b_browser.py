"""Verificações do browser que a app ainda não cumpre (NÃO entra no CI).

Mesmo HTML dos outros scripts:  python tests/visual/pendentes_b_browser.py <pasta ui>
Sai com 1 enquanto a app falhar a regra; quando a app for corrigida, passa-se a verificação
para tests/visual/cobertura_b_browser.py.

DMO-406 (Diogo): os cartões do Resumo (Início, Fim, ...) ficam compactos, com o rótulo e o valor na
mesma linha. Hoje o Início e o Fim têm o rótulo POR CIMA do valor: .resumo-mini-card herda
flex-direction:column de dmo-resumo-prototype.css e nenhuma regra de d1366-controlo.css /
dmo-design.css a põe em row (só o cartão da Aprovação tem flex-direction:row).
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura, altura in ((1290, 646), (1366, 768)):
        context = browser.new_context(viewport={'width': largura, 'height': altura})
        page = context.new_page()
        html = sem_selo_local((fixtures / 'resumo-sem-peso.html').read_text(encoding='utf-8'))

        def servir(route):
            url = urlparse(route.request.url)
            if url.path == '/Controlo/Resumo':
                route.fulfill(content_type='text/html', body=html)
                return
            asset = (assets / unquote(url.path).lstrip('/')).resolve()
            if asset.is_relative_to(assets) and asset.is_file():
                route.fulfill(content_type=mimetypes.guess_type(str(asset))[0] or 'application/octet-stream', body=asset.read_bytes())
            else:
                route.fulfill(status=404, body='')

        context.route('**/*', servir)
        page.goto('http://dmo.test/Controlo/Resumo')
        page.wait_for_load_state('load')
        cartoes = page.evaluate("""() => [...document.querySelectorAll('main.resumo-page .resumo-context-grid > .resumo-mini-card:not(.resumo-approval)')]
            .map(c => { const s = c.querySelector(':scope > span')?.getBoundingClientRect(), v = c.querySelector(':scope > strong')?.getBoundingClientRect();
                return !!(s && v && Math.abs((s.top + s.bottom) / 2 - (v.top + v.bottom) / 2) < 14); })""")
        if not cartoes:
            falhas.append(f'{largura}x{altura}: sem cartões do Resumo')
        if not all(cartoes):
            falhas.append(f'DMO-406 {largura}x{altura}: o rótulo e o valor dos cartões do Resumo não estão na mesma linha')
        context.close()
    browser.close()
if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('pendentes B: passaram')

"""Check partial pagination against HTML exported by the real Razor HTTP tests.

DMO_UI_EVIDENCE_DIR=<directory> dotnet test tests/DMO.Alpha.Web.Tests \
  --filter 'FullyQualifiedName~FerramentasPagesTests|FullyQualifiedName~ToolCatalog'
python tests/visual/paginacao_parcial_browser.py <directory>
Only local fixtures and assets are served; no DEV requests are made.
"""
import mimetypes
import re
import os
import sys
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

from playwright.sync_api import sync_playwright, expect

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'

with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for width in (1400, 390):
        for path, start, end, region, field, initial, following in (
            ('/Ferramentas', 'ferr-page1', 'ferr-page2', '#ferrResultados', '#ferr-reference', 1, 2),
            ('/Boquilhas', 'catalog', 'catalog-next', '#bq-tool-results', '#bq-tool-search', 2, 3),
        ):
            context = browser.new_context(viewport={'width': width, 'height': 900})
            page = context.new_page()
            errors = []
            page.on('pageerror', lambda error: errors.append(str(error)))

            def serve(route):
                url = urlparse(route.request.url)
                if url.path == path:
                    number = int(parse_qs(url.query).get('pagina', [str(initial)])[0])
                    name = end if number == following else start
                    body = sem_selo_local((fixtures / (name + '.html')).read_text())
                    headers = {}
                    if path == '/Ferramentas' and parse_qs(url.query).get('handler') == ['Resultados']:
                        body = re.search(r'<section\b[^>]*id="ferrResultados"[^>]*>[\s\S]*?</section>', body).group(0)
                        headers['X-Dmo-Region'] = 'ferrResultados'
                    route.fulfill(content_type='text/html', headers=headers, body=body)
                    return
                asset = (assets / unquote(url.path).lstrip('/')).resolve()
                if asset.is_relative_to(assets) and asset.is_file():
                    route.fulfill(content_type=mimetypes.guess_type(str(asset))[0] or 'application/octet-stream', body=asset.read_bytes())
                else:
                    route.fulfill(status=404, body='Not a local fixture')

            context.route('**/*', serve)
            page.goto('http://dmo.test' + path + ('?ficheiros=1' if path == '/Boquilhas' else ''))
            expect(page.locator(region)).to_be_visible()
            page.evaluate('''(selector) => {
                window.__marca = 1;
                window.__campo = document.querySelector(selector);
                window.__header = document.querySelector('.dmo-shell-header');
            }''', field)
            page.locator(region).get_by_label('Página seguinte', exact=True).click()
            expect(page.locator(region + ' nav span')).to_have_text(f'{following} de {following}')
            assert page.evaluate('window.__marca') == 1, 'A página recarregou'
            assert page.evaluate('(selector) => document.querySelector(selector) === window.__campo', field)
            assert page.evaluate("document.querySelector('.dmo-shell-header') === window.__header")
            page.locator(region).get_by_label('Página anterior', exact=True).click()
            expect(page.locator(region + ' nav span')).to_have_text(f'{initial} de {following}')
            assert page.evaluate('window.__marca') == 1
            if path == '/Ferramentas' and width >= 1400:
                # Os filtros e os dois botões numa só linha (Diogo, 19/10/2026),
                # com os campos curtos e os botões encostados à direita do card.
                linha = page.evaluate('''() => {
                    const form = document.querySelector('.ferr-filters');
                    const caixas = [...form.querySelectorAll(':scope > .dmo-field > :is(input,select), .ferr-filter-actions .dmo-button')]
                        .map(el => el.getBoundingClientRect());
                    const card = form.closest('.dmo-card').getBoundingClientRect();
                    const acoes = form.querySelector('.ferr-filter-actions').getBoundingClientRect();
                    return { n: caixas.length, bottoms: caixas.map(c => Math.round(c.bottom)),
                             largest: Math.max(...caixas.slice(0, 4).map(c => c.width)),
                             right: card.right - acoes.right, overflow: form.scrollWidth - form.clientWidth };
                }''')
                # «Por página» é opção da página, junto da paginação (Diogo, 23/10/2026).
                assert linha['n'] == 6, linha
                assert max(linha['bottoms']) - min(linha['bottoms']) <= 2, f'Os filtros e os botões não estão na mesma linha: {linha}'
                assert linha['largest'] <= 200, f'Um campo ocupa demasiado: {linha}'
                assert linha['right'] <= 40 and linha['overflow'] <= 0, linha
                # «Criar ferramenta» abre a página inteira, não a zona dos resultados.
                page.get_by_role('link', name='Criar ferramenta', exact=True).click()
                page.wait_for_load_state('load')
                assert page.evaluate('window.__marca') is None, 'Criar ferramenta trocou só a zona'
                assert urlparse(page.url).query == 'criar=1', page.url
            assert not errors, errors
            print(f'PASS {path}: next/back without reload; filters and header retained ({width}px)')
            context.close()
    browser.close()

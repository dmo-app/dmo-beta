"""Read-only pagination checks against the documented local stress environment.
DMO_UI_TEST_URL=http://127.0.0.1:5299 python tests/visual/historicos_parcial_browser.py
"""
import os
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('localhost', '127.0.0.1'):
    raise SystemExit('Use only the disposable local stress environment.')

with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'), args=['--no-sandbox'])
    for width in (1400, 390):
        for url, region, user in (
            ('/Admin/Users', '#dmu-resultados', 'admin@local.test'),
            ('/JobOn?historico=1&maquina=1', '#jobonHistoricoResultados', 'stress@local.test'),
            ('/Controlo/Peso?historico=1&tipo=Peso', '#pesoHistoricoResultados', 'stress@local.test'),
        ):
            context = browser.new_context(viewport={'width': width, 'height': 900})
            page = context.new_page()
            errors = []
            page.on('pageerror', lambda error: errors.append(str(error)))
            page.goto(base + '/login')
            page.locator('#identifier').fill(user)
            page.locator('#password').fill('stress-1234')
            with page.expect_navigation():
                page.locator('#password').press('Enter')
            page.goto(base + url)
            expect(page.locator(region)).to_be_visible()
            first = page.locator(region + ' [data-dmo-row]').first.get_attribute('data-id')
            page.evaluate('''() => {
                window.__marca = 1;
                window.__header = document.querySelector('.dmo-shell-header');
                window.__form = document.querySelector('main form');
            }''')
            if region == '#jobonHistoricoResultados':
                with page.expect_response(lambda response: 'handler=HistoricoResultados' in response.url) as partial:
                    page.locator(region).get_by_label('Página seguinte', exact=True).click()
                assert partial.value.headers.get('x-dmo-region') == 'jobonHistoricoResultados'
                assert '<html' not in partial.value.text().lower()
            else:
                page.locator(region).get_by_label('Página seguinte', exact=True).click()
            expect(page).to_have_url(__import__('re').compile(r'pagina=2'))
            assert page.locator(region + ' [data-dmo-row]').first.get_attribute('data-id') != first
            assert page.evaluate('window.__marca') == 1, 'The page reloaded'
            assert page.evaluate("document.querySelector('.dmo-shell-header') === window.__header")
            assert page.evaluate("document.querySelector('main form') === window.__form")
            page.locator(region).get_by_label('Página anterior', exact=True).click()
            expect(page.locator(region + ' [data-dmo-row]').first).to_have_attribute('data-id', first)
            assert page.evaluate('window.__marca') == 1
            assert not errors, errors
            print(f'PASS {url} {width}px: next/back without reload; filters/form and shell retained')
            context.close()
    browser.close()

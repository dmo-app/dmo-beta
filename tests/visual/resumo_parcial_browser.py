"""Local stress environment: Resumo pagination requests only the server partial."""
import os
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')
with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'), args=['--no-sandbox'])
    for width in (1400, 390):
        context = browser.new_context(viewport={'width': width, 'height': 900})
        page = context.new_page()
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(base + '/login')
        page.locator('#identifier').fill('stress@local.test')
        page.locator('#password').fill('stress-1234')
        with page.expect_navigation():
            page.locator('#password').press('Enter')
        response = page.goto(base + '/Controlo/Resumo?reference=1&jobon_id=1')
        full_size = len(response.body())
        expect(page.locator('.resumo-sheet')).to_be_visible()
        first_row = page.locator('#resumoProcura [data-dmo-row]').first.get_attribute('data-id')
        page.evaluate('''() => {
            window.__marca = 1;
            window.__field = document.querySelector('#reference');
            window.__sheet = document.querySelector('.resumo-sheet');
        }''')
        with page.expect_response(lambda response: 'handler=Procurar' in response.url and 'pagina=2' in response.url) as requested:
            page.get_by_label('Página mais antiga', exact=True).click()
        partial = requested.value
        assert '<html' not in partial.text().lower()
        expect(page.locator('.resumo-pager-info')).to_have_text(__import__('re').compile(r'^2 de '))
        assert page.locator('#resumoProcura [data-dmo-row]').first.get_attribute('data-id') != first_row
        assert page.evaluate('window.__marca') == 1
        assert page.evaluate("document.querySelector('#reference') === window.__field")
        assert page.evaluate("document.querySelector('.resumo-sheet') === window.__sheet")
        assert 'jobon_id=1' in page.url
        page.get_by_label('Página mais recente', exact=True).click()
        expect(page.locator('#resumoProcura [data-dmo-row]').first).to_have_attribute('data-id', first_row)
        assert page.evaluate('window.__marca') == 1
        assert not errors, errors
        print(f'PASS Resumo {width}px: next/back without reload, same sheet/field; response {len(partial.body())} bytes vs full page {full_size}')
        context.close()
    browser.close()

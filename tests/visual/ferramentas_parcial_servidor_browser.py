"""Local stress environment: filter/page using the server's results-only endpoint."""
import os
import re
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')


def login(page):
    page.goto(base + '/login')
    page.locator('#identifier').fill('stress@local.test')
    page.locator('#password').fill('stress-1234')
    with page.expect_navigation():
        page.locator('#password').press('Enter')


with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'), args=['--no-sandbox'])
    for width in (1400, 390):
        context = browser.new_context(viewport={'width': width, 'height': 900})
        page = context.new_page()
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        login(page)
        page.goto(base + '/Ferramentas?type=BQ')
        page.evaluate("window.__marca=1;window.__field=document.querySelector('#ferr-reference');window.__header=document.querySelector('.dmo-shell-header')")

        def result(action):
            with page.expect_response(lambda response: 'handler=Resultados' in response.url) as captured:
                action()
            response = captured.value
            assert response.status == 200
            assert response.headers.get('x-dmo-region') == 'ferrResultados'
            assert '<html' not in response.text().lower()
            expect(page.locator('[data-dmo-troca]')).not_to_have_attribute('aria-busy', 'true')
            assert page.evaluate('window.__marca') == 1
            assert page.evaluate("document.querySelector('#ferr-reference')===window.__field")
            assert page.evaluate("document.querySelector('.dmo-shell-header')===window.__header")

        result(lambda: page.locator('#ferr-type').select_option('CM'))
        expect(page.locator('#ferrToolList tbody tr').first).to_contain_text('CM ·')
        first = page.locator('#ferrToolList tbody tr').first.get_attribute('data-id')
        result(lambda: page.get_by_label('Página seguinte', exact=True).click())
        expect(page).to_have_url(re.compile('pagina=2'))
        assert page.locator('#ferrToolList tbody tr').first.get_attribute('data-id') != first
        result(lambda: page.locator('#ferr-page-size').select_option('20'))
        expect(page.locator('#ferrToolList tbody tr')).to_have_count(20)
        reference = page.locator('#ferrToolList tbody tr').first.inner_text().split(' · ')[1]
        result(lambda: page.locator('#ferr-reference').fill(reference))
        expect(page.locator('#ferr-reference')).to_be_focused()
        assert all(reference in text for text in page.locator('#ferrToolList tbody tr').all_inner_texts())
        result(lambda: page.locator('#ferr-reference').fill('INEXISTENTE-TESTE-PARCIAL'))
        expect(page.locator('.ferr-vazia')).to_contain_text('Nenhuma ferramenta')
        with page.expect_navigation():
            page.get_by_role('link', name='Limpar filtros', exact=True).click()
        expect(page.locator('#ferr-reference')).to_have_value('')
        expect(page.locator('#ferrResultados')).to_be_hidden()
        assert not errors, errors
        print(f'PASS Ferramentas {width}px: server partial for type, page, size, reference and empty results; native clear resets fields')
        context.close()

    context = browser.new_context(java_script_enabled=False)
    page = context.new_page()
    login(page)
    page.goto(base + '/Ferramentas?type=BQ')
    first = page.locator('#ferrToolList tbody tr').first.get_attribute('data-id')
    with page.expect_navigation():
        page.get_by_label('Página seguinte', exact=True).click()
    assert 'pagina=2' in page.url and 'handler=' not in page.url
    assert page.locator('#ferrToolList tbody tr').first.get_attribute('data-id') != first
    print('PASS Ferramentas without JavaScript: normal GET pagination')
    context.close()
    browser.close()

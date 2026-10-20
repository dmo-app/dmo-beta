"""Review filters on the disposable local server: preserve the open decision and note."""
import os
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
        # The documented synthetic seed has controls on this day; discover the row id.
        page.goto(base + '/Controlo/Approve?ano=2026&mes=4&dia=16')
        link = page.locator('.revisao-item').first.get_attribute('href')
        assert link, 'Seed needs a control on 16/04/2026'
        page.goto(base + link)
        note = page.locator('#revisao-nota')
        note.fill('Nota em edição — não gravar neste teste')
        selected = page.locator('.revisao-item[aria-current=true]')
        expect(selected).to_have_count(1)
        job_id = page.locator('.revisao-filters input[name=jobon_id]').input_value()
        page.evaluate("window.__marca=1;window.__note=document.querySelector('#revisao-nota');window.__detail=document.querySelector('#revisaoDetalhe');window.__calendar=document.querySelector('[data-dmo-calendar]');window.__search=document.querySelector('#revisao-procura')")

        def result(action):
            with page.expect_response(lambda response: 'handler=Resultados' in response.url) as captured:
                action()
            response = captured.value
            assert response.status == 200
            assert response.headers.get('x-dmo-region') == 'revisaoResultados'
            assert '<html' not in response.text().lower()
            expect(page.locator('[data-dmo-troca]')).not_to_have_attribute('aria-busy', 'true')
            assert page.evaluate('window.__marca') == 1
            for selector, saved in (('#revisao-nota', '__note'), ('#revisaoDetalhe', '__detail'), ('[data-dmo-calendar]', '__calendar'), ('#revisao-procura', '__search')):
                assert page.evaluate('([selector,saved]) => document.querySelector(selector) === window[saved]', [selector, saved])
            expect(note).to_have_value('Nota em edição — não gravar neste teste')
            assert 'handler=' not in page.url
            assert 'jobon_id=' + job_id in page.url

        result(lambda: page.locator('#revisao-procura').fill('INEXISTENTE-TESTE-PARCIAL'))
        expect(page.locator('#revisaoResultados')).to_contain_text('Nenhum controlo com estes filtros.')
        expect(page.locator('#revisao-procura')).to_be_focused()
        result(lambda: page.locator('#revisao-procura').fill(''))
        expect(page.locator('.revisao-item[aria-current=true]')).to_have_count(1)
        result(lambda: page.locator('#revisao-estado').select_option('aprovado'))
        result(lambda: page.locator('#revisao-estado').select_option(''))
        expect(page.locator('.revisao-item[aria-current=true]')).to_have_count(1)
        # A normal GET to the canonical URL must reopen the same production and filters.
        current_url = page.url
        page.goto(current_url)
        expect(page.locator('.revisao-filters input[name=jobon_id]')).to_have_value(job_id)
        expect(page.locator('.revisao-item[aria-current=true]')).to_have_count(1)
        expect(page.locator('#revisao-nota')).to_have_value('')
        assert not errors, errors
        print(f'PASS Revisão {width}px: result-only filters preserve detail, note, calendar, focus and canonical selected production')
        context.close()

    context = browser.new_context(java_script_enabled=False)
    page = context.new_page()
    login(page)
    page.goto(base + '/Controlo/Approve?ano=2026&mes=4&dia=16')
    page.goto(base + page.locator('.revisao-item').first.get_attribute('href'))
    job_id = page.locator('.revisao-filters input[name=jobon_id]').input_value()
    page.locator('#revisao-procura').fill('INEXISTENTE-TESTE-PARCIAL')
    with page.expect_navigation():
        page.locator('#revisao-procura').press('Enter')
    expect(page.locator('#revisaoResultados')).to_contain_text('Nenhum controlo com estes filtros.')
    expect(page.locator('.revisao-filters input[name=jobon_id]')).to_have_value(job_id)
    expect(page.locator('#revisao-nota')).to_be_visible()
    assert 'handler=' not in page.url
    print('PASS Revisão without JavaScript: native filter GET keeps the selected production')
    context.close()
    browser.close()

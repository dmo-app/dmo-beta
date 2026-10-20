"""Local stress environment only: calendar GET navigation and review selection.
DMO_UI_TEST_URL=http://127.0.0.1:5299 python tests/visual/calendario_parcial_browser.py
"""
import os
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')
with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'), args=['--no-sandbox'])
    for width in (1400, 390):
        for path in ('/JobOn', '/Controlo/Approve'):
            context = browser.new_context(viewport={'width': width, 'height': 900})
            page = context.new_page()
            errors = []
            page.on('pageerror', lambda error: errors.append(str(error)))
            page.goto(base + '/login')
            page.locator('#identifier').fill('stress@local.test')
            page.locator('#password').fill('stress-1234')
            with page.expect_navigation():
                page.locator('#password').press('Enter')
            page.goto(base + path + '?ano=2026&mes=10&dia=1')
            page.evaluate("window.__marca = 1; window.__header = document.querySelector('.dmo-shell-header')")
            original = page.locator('#dmo-calendar-title').text_content()
            page.get_by_role('button', name='Mês seguinte', exact=True).click()
            expect(page.locator('#dmo-calendar-title')).not_to_have_text(original)
            assert page.evaluate('window.__marca') == 1, 'Month navigation reloaded the document'
            page.get_by_role('button', name='Mês anterior', exact=True).click()
            expect(page.locator('#dmo-calendar-title')).to_have_text(original)
            page.locator('.dmo-calendar-days a').filter(has_text=__import__('re').compile(r'^2$')).click()
            expect(page.locator('.dmo-calendar-days [aria-current=date]')).to_have_text('2')
            assert page.evaluate('window.__marca') == 1, 'Day selection reloaded the document'
            if path == '/JobOn':
                expect(page.locator('#lightDayTitle')).to_contain_text('02/10/2026')
                expect(page.locator('#lightSearch')).to_have_attribute('data-dmo-procura', __import__('re').compile('dia=2'))
                expect(page.locator('#lightSearch').locator('..').locator('input[name=dia]')).to_have_value('2')
            else:
                expect(page.locator('#revisao-day-title')).to_contain_text('02/10/2026')
                # Filtros introduzidos depois da mudança continuam ligados.
                page.locator('#revisao-estado').select_option('aprovado')
                expect(page).to_have_url(__import__('re').compile('estado=aprovado'))
            page.locator('.dmo-calendar-days [aria-current=date]').click()
            expect(page.locator('.dmo-calendar-days [aria-current=date]')).to_have_count(0)
            assert page.evaluate('window.__marca') == 1, 'Unselecting the day reloaded the document'
            assert page.evaluate("document.querySelector('.dmo-shell-header') === window.__header")
            assert not errors, errors
            print(f'PASS {path} {width}px: month/day/unselect without reload; context and handlers retained')
            context.close()
    browser.close()

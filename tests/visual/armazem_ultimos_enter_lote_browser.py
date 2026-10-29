"""Armazém › Últimas entradas e saídas: Enter no campo Lote aplica o filtro (DMO-649, auditoria 29/10/2026).
Corre contra um servidor local descartável:
  DMO_UI_TEST_URL=http://127.0.0.1:5111 DMO_UI_USER=... DMO_UI_PASSWORD=... python tests/visual/armazem_ultimos_enter_lote_browser.py
"""
import os
from urllib.parse import urlparse

from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only a local disposable environment.')

with sync_playwright() as playwright:
    launch = {'args': ['--no-sandbox']}
    if os.environ.get('DMO_CHROMIUM'):
        launch['executable_path'] = os.environ['DMO_CHROMIUM']
    browser = playwright.chromium.launch(**launch)
    page = browser.new_page(viewport={'width': 1290, 'height': 646})
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.goto(base + '/login')
    page.locator('#identifier').fill(os.environ.get('DMO_UI_USER', 'stress@local.test'))
    page.locator('#password').fill(os.environ.get('DMO_UI_PASSWORD', 'stress-1234'))
    with page.expect_navigation():
        page.locator('#password').press('Enter')

    page.goto(base + '/Armazem/Ultimos')
    page.locator('#armMovLote').click()
    page.keyboard.type('99999')
    page.locator('#armMovLote').press('Enter')
    page.wait_for_function("location.search.includes('lote=99999')", timeout=5000)
    expect(page.locator('#armMovLote')).to_have_value('99999')
    expect(page.locator('#armMovLimpar')).to_be_visible()
    assert not errors, errors
    print('PASS Armazém Últimas: Enter no Lote aplica o filtro')
    browser.close()

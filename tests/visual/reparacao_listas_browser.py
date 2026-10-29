"""Listas de reparação no Chromium a 1290×646 (DMO-591, Diogo 29/10/2026).

O campo «Lista» mostra só o reparador (o dia só na lista aberta) e cabe numa
linha, sem passar por cima do «Reparador»; a Referência procura ao escrever
(«nao aparece nada» com 1075 sem Enter), só a zona dos resultados troca e o
campo fica com o foco e o texto. Corre contra um servidor local descartável:
  DMO_UI_TEST_URL=http://127.0.0.1:5111 DMO_UI_USER=... DMO_UI_PASSWORD=... \
    python tests/visual/reparacao_listas_browser.py [referência]
A referência tem de existir como CM ou MF nesse servidor (por omissão 5447).
"""
import os
import sys
from urllib.parse import urlparse

from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only a local disposable environment.')
referencia = sys.argv[1] if len(sys.argv) > 1 else '5447'
PAGINA = base + '/Ferramentas/ReparacaoProgramada'

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

    # Uma lista ativa para o campo «Lista» (cria uma se não houver).
    page.goto(PAGINA)
    if page.locator('#rpListaCabecalho').count() == 0:
        page.goto(PAGINA + '?lista=0')
        page.locator('#rpReparador').select_option(index=1)
        with page.expect_navigation():
            page.locator('#rpCriarBotao').click()
        page.goto(PAGINA)

    campo = page.locator('#rpListaEscolha')
    reparador = page.locator('#rpListaCabecalho strong').first.inner_text().strip()
    # O dia está no markup da opção, escondido no campo fechado.
    assert campo.locator('selectedcontent').inner_text().strip() == reparador
    caixa = campo.bounding_box()
    cabecalho = page.locator('#rpListaCabecalho').bounding_box()
    # Uma só linha (a altura dos botões) e nada por cima do «Reparador».
    assert caixa['height'] <= 36, caixa
    assert caixa['y'] + caixa['height'] <= cabecalho['y'], (caixa, cabecalho)
    texto = campo.locator('option:checked').inner_text()
    assert texto.startswith(reparador + ' · '), texto

    page.evaluate("window.__campo=document.querySelector('#rpReferencia');window.__lista=document.querySelector('#rpListaLinha')")
    with page.expect_response(lambda r: 'handler=Resultados' in r.url) as captured:
        page.locator('#rpReferencia').click()
        page.keyboard.type(referencia, delay=80)
    response = captured.value
    assert response.headers.get('x-dmo-region') == 'rpResultados'
    expect(page.locator('#rpCandidatas tbody tr').first).to_be_visible()
    expect(page.locator('#rpReferencia')).to_be_focused()
    expect(page.locator('#rpReferencia')).to_have_value(referencia)
    assert page.evaluate("document.querySelector('#rpReferencia')===window.__campo")
    assert page.evaluate("document.querySelector('#rpListaLinha')===window.__lista")
    expect(page.locator('#rpAssociar input[name=referencia]')).to_have_value(referencia)
    assert 'referencia=' + referencia in page.url

    page.locator('#rpReferencia').fill('ZZZ999')
    expect(page.locator('#rpProcuraNada')).to_be_visible()
    page.locator('#rpReferencia').fill(referencia)
    page.locator('#rpReferencia').press('Enter')
    expect(page.locator('#rpCandidatas tbody tr').first).to_be_visible()

    with page.expect_navigation():
        page.locator('#rpLimpar').click()
    expect(page.locator('#rpReferencia')).to_have_value('')
    assert page.locator('#rpCandidatas').count() == 0
    assert not errors, errors
    print('PASS Listas de reparação 1290×646: «Lista» só com o reparador; procura ao escrever, só os resultados trocam')
    browser.close()

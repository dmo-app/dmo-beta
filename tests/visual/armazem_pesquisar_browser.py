"""Armazém › Pesquisar no Chromium a 1290×646 (DMO-641) e o link da «% uso» (DMO-643), Diogo 29/10/2026.

A pesquisa corre ao escrever (sem Enter), só a zona dos resultados troca e o campo fica com o foco; a página não desliza;
um clique numa linha abre o registo da ferramenta; o link da «% uso» tem a cor da paleta e nenhum sublinhado, também com o rato
por cima. Servidor local descartável:
  DMO_UI_TEST_URL=http://127.0.0.1:5111 DMO_UI_USER=... DMO_UI_PASSWORD=... python tests/visual/armazem_pesquisar_browser.py [referência]
A referência tem de existir (por omissão 5447).
"""
import os
import sys
from urllib.parse import urlparse

from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only a local disposable environment.')
referencia = sys.argv[1] if len(sys.argv) > 1 else '5447'

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

    page.goto(base + '/Armazem/Pesquisar')
    expect(page.locator('#armPesqLista')).to_have_count(0)
    page.evaluate("window.__campo=document.querySelector('#armPesqTexto')")
    with page.expect_response(lambda r: 'handler=Resultados' in r.url) as captured:
        page.locator('#armPesqTexto').click()
        page.keyboard.type(referencia, delay=80)
    assert captured.value.headers.get('x-dmo-region') == 'armPesqResultados'
    expect(page.locator('#armPesqLista tbody tr').first).to_be_visible()
    expect(page.locator('#armPesqTexto')).to_be_focused()
    assert page.evaluate("document.querySelector('#armPesqTexto')===window.__campo")
    assert page.evaluate("document.documentElement.scrollHeight <= innerHeight"), 'a página desliza'
    assert page.locator('#armPesqLista button').count() == 0, 'botões dentro das linhas'

    # O link da «% uso»: injeta um, porque o servidor de teste pode não ter leituras.
    cor = page.evaluate("""() => {
      const td = document.querySelector('#armPesqLista tbody tr td:last-child');
      td.innerHTML = '<a class="dmo-uso-link" href="/Ferramentas/Utilizacao?tool_id=x">18 %</a>';
      const a = td.firstElementChild; const k = getComputedStyle(a);
      return {cor: k.color, sublinhado: k.textDecorationLine, azulDoBrowser: k.color === 'rgb(0, 0, 238)'};
    }""")
    assert cor['sublinhado'] == 'none' and not cor['azulDoBrowser'], cor
    page.locator('#armPesqLista .dmo-uso-link').hover()
    hover = page.evaluate("getComputedStyle(document.querySelector('#armPesqLista .dmo-uso-link')).textDecorationLine")
    assert hover == 'none', hover

    page.locator('#armPesqLista tbody tr').first.click()
    page.wait_for_url('**/Armazem/Movimentos?ferramenta=*')
    assert 'acao=' in page.url, page.url

    page.goto(base + '/Armazem/Pesquisar?q=ZZZ999')
    expect(page.locator('#armPesqNada')).to_be_visible()
    assert not errors, errors
    print('PASS Armazém Pesquisar 1290×646: procura ao escrever, só os resultados trocam, linha abre o registo, link da % uso sem azul nem sublinhado')
    browser.close()

"""Local stress environment (E11, beta.189): after a save, the GET that follows
the redirect asks for the zones (X-Dmo-Zonas) and the server answers only with
them (X-Dmo-Region). Resumo (OK/NOK of an area) and Pegamentos (Enter on a new
reading). Writes data: use a disposable copy of the stress database.

DMO_UI_TEST_URL (default http://127.0.0.1:5299), DMO_CHROMIUM,
DMO_ZONAS_JOBON (Resumo, default 296), DMO_ZONAS_CONTROLO (Pegamentos with
measurements and a CM gargalo, default 296).
"""
import os
import re
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')
jobon = os.environ.get('DMO_ZONAS_JOBON', '296')
controlo = os.environ.get('DMO_ZONAS_CONTROLO', '296')


def pedidos(page):
    vistos = []
    page.on('request', lambda request: vistos.append(request)
            if request.resource_type in ('document', 'fetch', 'xhr') else None)
    return vistos


def zonas_do_get(vistos, path):
    gets = [r for r in vistos if r.method == 'GET' and urlparse(r.url).path == path]
    posts = [r for r in vistos if r.method == 'POST']
    assert len(posts) == 1, [r.url for r in posts]
    assert len(gets) == 1, [r.url for r in gets]
    assert all(r.resource_type != 'document' for r in vistos), 'a página não recarrega'
    resposta = gets[0].response()
    return gets[0].all_headers().get('x-dmo-zonas'), resposta.headers.get('x-dmo-region'), len(resposta.body())


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

        # Resumo: o OK/NOK de uma área grava e o servidor desenha só o
        # cartão dessa área, a mensagem e o «Visto por».
        full = page.goto(f'{base}/Controlo/Resumo?jobon_id={jobon}')
        full_size = len(full.body())
        estado = page.locator('#resumo-estado-CM')
        novo = 'NOK' if estado.input_value() != 'NOK' else 'OK'
        page.evaluate('''() => {
            window.__marca = 1;
            window.__bq = document.getElementById('resumo-area-BQ');
            window.__procura = document.querySelector('.resumo-browser');
        }''')
        vistos = pedidos(page)
        estado.focus()
        with page.expect_response(lambda r: r.request.method == 'GET' and 'x-dmo-region' in r.headers):
            estado.select_option(novo)
        expect(page.locator('#resumo-area-CM strong')).to_have_text(novo)
        page.wait_for_timeout(200)
        pedidas, regiao, size = zonas_do_get(vistos, '/Controlo/Resumo')
        assert pedidas == 'resumo-area-CM resumoEstadoAvisos resumoVistoPor', pedidas
        assert regiao == pedidas, regiao
        assert page.evaluate('window.__marca') == 1
        assert page.evaluate("document.getElementById('resumo-area-BQ') === window.__bq"), 'a outra área fica'
        assert page.evaluate("document.querySelector('.resumo-browser') === window.__procura"), 'a procura fica'
        assert page.evaluate("document.activeElement && document.activeElement.id") == 'resumo-estado-CM'
        assert f'jobon_id={jobon}' in page.url
        print(f'PASS Resumo {width}px: OK/NOK sem recarregar, 1 POST + 1 GET só com as zonas ({size} bytes contra {full_size} da página)')

        # Pegamentos: Enter numa leitura nova da CM grava e o servidor desenha
        # só o cartão da CM, as mensagens e o mapa (com medições, o contexto
        # não vai).
        full = page.goto(f'{base}/Controlo/Pegamentos?controlo_id={controlo}')
        full_size = len(full.body())
        linhas = page.locator('#pegCartaoCM tbody tr').count()
        page.evaluate('''() => {
            window.__marca = 1;
            window.__bq = document.getElementById('pegCartaoBQ');
            window.__contexto = document.getElementById('pegContexto');
        }''')
        vistos = pedidos(page)
        page.locator('#costura-Cm').fill('70.04')
        page.locator('#contra-Cm').fill('69.98')
        with page.expect_response(lambda r: r.request.method == 'GET' and 'x-dmo-region' in r.headers):
            page.locator('#contra-Cm').press('Enter')
        expect(page.locator('#pegCartaoCM tbody tr')).to_have_count(linhas + 1)
        page.wait_for_timeout(200)
        pedidas, regiao, size = zonas_do_get(vistos, '/Controlo/Pegamentos')
        assert pedidas == 'pegCartaoCM pegAvisos pegMapa', pedidas
        assert regiao == pedidas, regiao
        assert page.evaluate('window.__marca') == 1
        assert page.evaluate("document.getElementById('pegCartaoBQ') === window.__bq"), 'o outro cartão fica'
        assert page.evaluate("document.getElementById('pegContexto') === window.__contexto"), 'o contexto fica'
        expect(page.locator('#costura-Cm')).to_be_focused()
        expect(page.locator('#costura-Cm')).to_have_value('')
        assert re.search(r'70[.,]04', page.locator('#pegCartaoCM tbody').inner_text())
        print(f'PASS Pegamentos {width}px: leitura sem recarregar, 1 POST + 1 GET só com as zonas ({size} bytes contra {full_size} da página)')

        assert not errors, errors
        context.close()
    browser.close()

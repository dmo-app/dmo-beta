"""Pendentes A2 (não entra no CI): o que a app ainda tem mal à luz da regra do Diogo.

DMO-522: «o "Sem data no Job On" deixa de ficar cortado com "…"». A 1290×646 (o Chrome do Diogo) o cartão
«Início» do Registo das Boquilhas, sem data de início no Job On, sai cortado 2px com reticências.
(O CSS dá espaço ao texto mais comprido só a 1366×768.)

    python3 tests/visual/pendentes_a2_browser.py <pasta com cobertura-a2-bq-registo-producao.html>
O HTML é o do teste RegistoComProducao_MostraBqLoteEOsCartoesDaProducao_ParaOBrowser (DMO_UI_EVIDENCE_DIR).
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

www = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
fixtures = Path(sys.argv[1]).resolve()
FACTOS = '''() => [...document.querySelectorAll('main .dmo-fact')].filter(f => f.getClientRects().length).map(f => {
  const s = f.querySelector('strong'); return { rotulo: (f.querySelector('span') || f).textContent.trim(), texto: s ? s.textContent.trim() : '',
    cortado: s ? s.scrollWidth - s.clientWidth : 0 }; })'''
falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura, altura in ((1366, 768), (1290, 646)):
        context = browser.new_context(viewport={'width': largura, 'height': altura}, locale='pt-PT', timezone_id='Europe/Lisbon')
        html = sem_selo_local((fixtures / 'cobertura-a2-bq-registo-producao.html').read_text(encoding='utf-8'))

        def servir(route, _req=None, html=html):
            url = urlparse(route.request.url)
            if route.request.method == 'GET' and url.path == '/Boquilhas':
                route.fulfill(content_type='text/html', body=html)
                return
            alvo = (www / unquote(url.path).lstrip('/')).resolve()
            if alvo.is_relative_to(www) and alvo.is_file():
                route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
            else:
                route.fulfill(status=404, body='')

        context.route('**/*', servir)
        page = context.new_page()
        page.goto('http://dmo.test/Boquilhas')
        page.wait_for_load_state('load')
        for f in page.evaluate(FACTOS):
            if f['cortado'] > 1:
                falhas.append(f'DMO-522 {largura}×{altura}: «{f["rotulo"]}» sai cortado ({f["texto"]}, {f["cortado"]}px)')
        context.close()
    browser.close()
print('\n'.join(falhas) or 'sem pendentes')
sys.exit(1 if falhas else 0)

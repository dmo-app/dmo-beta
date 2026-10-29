"""Verificações da cobertura A1 que a app ainda não cumpre (NÃO entra no CI).

DMO-495 (e DMO-521, ecrã do Diogo de 1290×646): na folha do Job On (Ver) com os oito
calibres, a 1290×646 o cartão dos Calibres tem uma caixa de 116 px com overflow
escondido e a 8.ª linha («CS P Interna») passa para uma segunda coluna fora do cartão:
três células ficam cortadas e sem barra para as ver. A 1366×768 os oito cabem.

Uso: python3 tests/visual/pendentes_a1_browser.py <pasta do DMO_UI_EVIDENCE_DIR>
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

www = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
html = sem_selo_local((Path(sys.argv[1]).resolve() / 'cobertura-a1-folha.html').read_text(encoding='utf-8'))


def servir(route):
    url = urlparse(route.request.url)
    if url.path == '/JobOn/Folha':
        route.fulfill(content_type='text/html', body=html)
        return
    alvo = (www / unquote(url.path).lstrip('/')).resolve()
    if alvo.is_relative_to(www) and alvo.is_file():
        route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
    else:
        route.fulfill(status=404, body='')


falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    page = browser.new_context(viewport={'width': 1290, 'height': 646}, locale='pt-PT').new_page()
    page.route('**/*', servir)
    page.goto('http://dmo.test/JobOn/Folha?jobon_id=1')
    page.wait_for_timeout(300)
    fora = page.evaluate("""() => { const w = document.querySelector('.cal-card .table-wrap').getBoundingClientRect();
      return [...document.querySelectorAll('table[data-jof-cal] tbody th, table[data-jof-cal] tbody td')]
        .filter(c => c.getBoundingClientRect().right > w.right + 1).length; }""")
    if fora:
        falhas.append(f'DMO-495: a 1290×646 há {fora} células dos Calibres fora do cartão (cortadas)')
    # DMO-509 a 1290×646: a nota aberta cai por cima (posições absolutas, somando o deslize dos pais).
    ABS = """() => Object.fromEntries(['MF','BQ','AN','PU','ARR','TP','CS'].map(f => { const e = document.querySelector(`[data-jof-familia="${f}"]`);
      let y = e.getBoundingClientRect().top, p = e.parentElement; for (; p; p = p.parentElement) y += p.scrollTop; return [f, Math.round(y)]; }))"""
    nota = page.locator('[data-jof-familia="CM"] [data-jof-nota]')
    h0 = page.evaluate("document.querySelector('[data-jof-familia=\"CM\"] [data-jof-nota]').getBoundingClientRect().height")
    antes = page.evaluate(ABS)
    nota.click()
    page.wait_for_timeout(200)
    h1 = page.evaluate("document.querySelector('[data-jof-familia=\"CM\"] [data-jof-nota]').getBoundingClientRect().height")
    depois = page.evaluate(ABS)
    print('nota', h0, h1, antes, depois)
    if antes != depois:
        falhas.append(f'DMO-509: a 1290×646 a nota aberta empurra os cartões ({antes} → {depois})')
    if h1 <= h0 + 1:
        falhas.append(f'DMO-509: a 1290×646 a nota aberta não fica maior ({h0:.0f} → {h1:.0f})')
    page.keyboard.press('Escape')
    page.wait_for_timeout(200)
    if page.evaluate(ABS) != antes:
        falhas.append('DMO-509: a 1290×646 depois de fechar a nota os cartões não voltam ao sítio')
    browser.close()
print('\n'.join(falhas) or 'pendentes A1: nada a assinalar')
sys.exit(1 if falhas else 0)

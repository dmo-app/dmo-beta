"""A tabela das punções no Chromium (Diogo, 24/10/2026).

HTML exportado pelo teste HTTP real (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~Tabela_TemLargurasFixas_EVaiParaOBrowser'
  python tests/visual/puncoes_tabela_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

A 1400 e 768 px: as larguras das colunas são as mesmas com o estado «Visto»
e depois de o trocar para «Por verificar» (e de volta).
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
LARGURAS = "() => [...document.querySelectorAll('#puncoes-lista .pu-table thead th')].map(th => Math.round(th.getBoundingClientRect().width * 10) / 10)"
# O que o servidor devolve depois de carregar no «Visto» da tabela (R16, Diogo 30/10/2026):
# a célula troca de classe e o botão de texto.
TROCAR = '''(visto) => document.querySelectorAll('#puncoes-lista .pu-estado').forEach(c => {
    c.classList.toggle('on', visto);
    (c.querySelector('.pu-check') || c).textContent = visto ? 'Visto' : 'Por verificar'; })'''

falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura in (1400, 768):
        context = browser.new_context(viewport={'width': largura, 'height': 900})
        page = context.new_page()
        html = sem_selo_local((fixtures / 'puncoes-tabela.html').read_text(encoding='utf-8'))

        def servir(route):
            url = urlparse(route.request.url)
            if url.path == '/Controlo/Puncoes':
                route.fulfill(content_type='text/html', body=html)
                return
            ficheiro = (assets / unquote(url.path).lstrip('/')).resolve()
            if ficheiro.is_relative_to(assets) and ficheiro.is_file():
                route.fulfill(content_type=mimetypes.guess_type(str(ficheiro))[0] or 'application/octet-stream', body=ficheiro.read_bytes())
            else:
                route.fulfill(status=404, body='')

        context.route('**/*', servir)
        page.goto('http://dmo.test/Controlo/Puncoes')
        page.wait_for_load_state('load')
        assert page.locator('#puncoes-lista .pu-estado').count() >= 4, 'Sem punções na tabela'
        # R16 (Diogo, 30/10/2026): sem botões no canto; o «Visto» da tabela troca o estado e o × remove.
        linhas = page.locator('#puncoes-lista tbody tr').count()
        assert page.locator('#puncoes-lista tbody td.pu-estado form .pu-check').count() == 2 * linhas, 'Falta o «Visto» clicável nas linhas'
        assert page.locator('#puncoes-lista tbody .pu-remove').count() == linhas, 'Falta o × nas linhas'
        assert page.locator('#puncoes-lista [data-dmo-precisa-escolha]').count() == 0, 'Ainda há botões no canto'
        antes = page.evaluate(LARGURAS)
        page.locator('#puncoes-lista tbody tr').nth(1).locator('td.pu-lot').click()
        if not page.locator('#puncoes-lista tbody tr').nth(1).locator('input[type=radio][name=inspecao_id]').is_checked():
            falhas.append(f'{largura}px: o clique na linha não a escolheu')
        escolhida = page.evaluate(LARGURAS)
        if any(abs(a - b) > 0.5 for a, b in zip(antes, escolhida)):
            falhas.append(f'{largura}px: as colunas mudaram ao escolher a linha: {antes} -> {escolhida}')
        page.evaluate(TROCAR, False)
        depois = page.evaluate(LARGURAS)
        page.evaluate(TROCAR, True)
        volta = page.evaluate(LARGURAS)
        print(f'{largura}px: {antes} / {depois} / {volta}')
        for nome, medida in (('Por verificar', depois), ('de volta a Visto', volta)):
            if any(abs(a - b) > 0.5 for a, b in zip(antes, medida)):
                falhas.append(f'{largura}px: as colunas mudaram com «{nome}»: {antes} -> {medida}')
        context.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Tabela das punções: ok')

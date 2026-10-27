"""A ajuda encostada ao que explica, no Chromium (Diogo, 24/10/2026).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~CriarJobOn|FullyQualifiedName~Ferramentas_VeOSeparador_AProcuraAbreVazia'
  python tests/visual/ajuda_junto_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

No Planeamento (caixa «Produção») e no Registar Reparação («Histórico» e
«Criar»), a 1400 px em claro e em escuro: sem círculos de dica à vista
(Diogo, 27/10/2026) e, ao passar o rato, o texto da ajuda é o title do dono.
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
# (ficheiro, caminho, o dono da ajuda, a ajuda, o elemento seguinte ou None)
CASOS = [
    ('jobon-ajuda.html', '/JobOn', '#lightProcura select, #lightProcura', '#lightProcura + .dmo-help', '#jobonCriar'),
    ('rp-ajuda.html', '/Ferramentas/ReparacaoProgramada', '#rpModoHistorico', '#rpModoHistorico + .dmo-help', None),
    ('rp-ajuda.html', '/Ferramentas/ReparacaoProgramada', '#rpCriarBotao', '#rpCriarBotao + .dmo-help', None),
]
# Sem círculos de dica (Diogo, 27/10/2026: «em lado nenhum»): nenhuma ajuda
# fica à vista, e o texto dela passa ao title do dono ao passar o rato.
VISIVEIS = "() => [...document.querySelectorAll('.dmo-help')].filter(a => a.getClientRects().length).map(a => getComputedStyle(a).display + ' ' + a.outerHTML.slice(0, 200))"
TEXTO = "a => (a.querySelector('.dmo-help-tip')?.textContent || a.getAttribute('aria-label') || '').replace(/^Ajuda:\\s*/, '').trim()"

falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for ficheiro, caminho, dono, ajuda, seguinte in CASOS:
        for tema in ('light', 'dark'):
            context = browser.new_context(viewport={'width': 1400, 'height': 900}, color_scheme=tema)
            page = context.new_page()
            html = sem_selo_local((fixtures / ficheiro).read_text(encoding='utf-8'))

            def fazer(html, caminho):
                return lambda route: servir(route, html, caminho)

            def servir(route, html, caminho):
                url = urlparse(route.request.url)
                if url.path == caminho:
                    route.fulfill(content_type='text/html', body=html)
                    return
                alvo = (assets / unquote(url.path).lstrip('/')).resolve()
                if alvo.is_relative_to(assets) and alvo.is_file():
                    route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
                else:
                    route.fulfill(status=404, body='')

            context.route('**/*', fazer(html, caminho))
            page.goto('http://dmo.test' + caminho)
            page.wait_for_load_state('load')
            visiveis = page.evaluate(VISIVEIS)
            if visiveis:
                falhas.append(f'{caminho} {tema}: {len(visiveis)} círculo(s) de dica à vista: {visiveis}')
            alvos = [d.strip() for d in dono.split(',')]
            alvo = next((d for d in alvos if page.locator(d).count()), alvos[-1])
            if not page.locator(ajuda).count():
                falhas.append(f'{caminho} {dono} {tema}: sem a ajuda')
            else:
                texto = page.locator(ajuda).first.evaluate(TEXTO)
                page.locator(alvo).first.hover()
                titulo = page.locator(alvo).first.evaluate("e => e.closest('[title]')?.getAttribute('title') || ''")
                print(f'{caminho} {dono} {tema}: title «{titulo[:40]}»')
                if not texto or titulo != texto:
                    falhas.append(f'{caminho} {dono} {tema}: o texto da ajuda não passa ao title ({titulo!r} != {texto!r})')
            context.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Ajuda encostada: ok')

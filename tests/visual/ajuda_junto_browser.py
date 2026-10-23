"""A ajuda encostada ao que explica, no Chromium (Diogo, 24/10/2026).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~CriarJobOn|FullyQualifiedName~Ferramentas_VeOSeparador_AProcuraAbreVazia'
  python tests/visual/ajuda_junto_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

No Planeamento (caixa «Produção» e «Criar Job On») e no Registar Reparação
(«Histórico» e «Criar»), a 1400 px em claro e em escuro: o
círculo fica a <= 0,5em da caixa ou do botão a que pertence, bem mais longe
do que do seguinte, e com o topo acima do meio dele (elevado).
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
MEDIR = '''([dono, ajuda, seguinte]) => {
  const d = document.querySelector(dono), a = document.querySelector(ajuda);
  if (!d || !a) return null;
  const caixa = d.matches('label') ? (() => { const r = document.createRange(); r.selectNodeContents(d.firstChild); return r.getBoundingClientRect(); })() : d.getBoundingClientRect();
  const circulo = a.querySelector('.dmo-help-mark').getBoundingClientRect();
  const em = parseFloat(getComputedStyle(a).fontSize);
  const s = seguinte ? document.querySelector(seguinte).getBoundingClientRect() : null;
  return { gapEm: (circulo.left - caixa.right) / em, ateSeguinte: s ? (s.left - circulo.right) / em : null,
           elevado: d.matches('label') ? circulo.top < caixa.top + caixa.height / 2 : circulo.top <= caixa.top + caixa.height * 0.25, visivel: circulo.width > 0 };
}'''

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
            alvos = [d.strip() for d in dono.split(',')]
            m = page.evaluate(MEDIR, [next((d for d in alvos if page.locator(d).count()), alvos[-1]), ajuda, seguinte])
            print(f'{caminho} {dono} {tema}: {m}')
            if m is None or not m['visivel']:
                falhas.append(f'{caminho} {dono} {tema}: sem a ajuda')
            else:
                if not -0.05 <= m['gapEm'] <= 0.5:
                    falhas.append(f'{caminho} {dono} {tema}: a ajuda a {m["gapEm"]:.2f}em (máx. 0,5em)')
                if m['ateSeguinte'] is not None and m['ateSeguinte'] < 2 * m['gapEm'] + 0.4:
                    falhas.append(f'{caminho} {dono} {tema}: a ajuda está pouco afastada do seguinte ({m["ateSeguinte"]:.2f}em)')
                if not m['elevado']:
                    falhas.append(f'{caminho} {dono} {tema}: a ajuda não está elevada')
            context.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Ajuda encostada: ok')

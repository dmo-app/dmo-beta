"""O mesmo espaço entre os botões do Registar Reparação, no Chromium (Diogo, 29/10/2026).

HTML exportado pelo teste HTTP real (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~ListaEProcura_MostramAReferenciaCompletaAFrenteDaFerramenta'
  python tests/visual/rp_botoes_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

Com uma lista aberta, a 1366 px: Imprimir, Listas e Histórico, e Adicionar e
Limpar filtros, ficam todos com o mesmo espaço entre si (DMO-674).
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
CAMINHO = '/Ferramentas/ReparacaoProgramada'
ESPACOS = """() => {
  const g = ids => {
    const r = ids.map(i => document.getElementById(i)?.getBoundingClientRect());
    return r.includes(undefined) ? null : r.slice(1).map((x, i) => Math.round(x.left - r[i].right));
  };
  return { topo: g(['rpImprimir', 'rpModoListas', 'rpModoHistorico']), filtros: g(['rpAssociarBotao', 'rpLimpar']) };
}"""

html = sem_selo_local((fixtures / 'rp-lista.html').read_text(encoding='utf-8'))


def servir(route):
    url = urlparse(route.request.url)
    if url.path == CAMINHO:
        route.fulfill(content_type='text/html', body=html)
        return
    alvo = (assets / unquote(url.path).lstrip('/')).resolve()
    if alvo.is_relative_to(assets) and alvo.is_file():
        route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
    else:
        route.fulfill(status=404, body='')


falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    context = browser.new_context(viewport={'width': 1366, 'height': 768})
    context.route('**/*', servir)
    page = context.new_page()
    page.goto('http://dmo.test' + CAMINHO)
    page.wait_for_load_state('load')
    espacos = page.evaluate(ESPACOS)
    print(f'espaços entre os botões: {espacos}')
    if espacos['topo'] is None or espacos['filtros'] is None:
        falhas.append(f'faltam botões: {espacos}')
    elif len(set(espacos['topo'] + espacos['filtros'])) != 1:
        falhas.append(f'espaços diferentes entre os botões: {espacos}')
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)

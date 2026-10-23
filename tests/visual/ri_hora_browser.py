"""O Dia e a Hora na mesma linha do Registar Reparação, no Chromium (Diogo, 23/10/2026).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~OCampoHora_AoLadoDoDia'
  python tests/visual/ri_hora_browser.py <pasta> [captura.png]
Só ficheiros locais; nada vai ao DEV.

A 1400 e a 768 px, em claro e em escuro: «Hora» fica à direita de «Dia»,
na mesma linha (o mesmo topo), com o valor HH:mm, e pertence ao formulário
do registo (form=). Com o segundo argumento, guarda a captura a 1400 px.
"""
import mimetypes
import os
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

# O campo time desenha-se na língua do Chromium: em português, 24 h.
os.environ['LANGUAGE'] = 'pt_PT'
os.environ['LANG'] = 'pt_PT.UTF-8'

fixtures = Path(sys.argv[1]).resolve()
captura = Path(sys.argv[2]) if len(sys.argv) > 2 else None
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
html = sem_selo_local((fixtures / 'ri-hora.html').read_text(encoding='utf-8'))
MEDIR = '''() => {
  const dia = document.querySelector('input[id^="riDia-"]'), hora = document.querySelector('input[id^="riHora-"]');
  if (!dia || !hora) return null;
  const d = dia.getBoundingClientRect(), h = hora.getBoundingClientRect();
  return { dTop: d.top, hTop: h.top, dRight: d.right, hLeft: h.left, valor: hora.value,
           doRegisto: hora.form && hora.form.id.startsWith('riRegistar-'), visivel: h.width > 0 && h.height > 0,
           scrollLateral: document.documentElement.scrollWidth > document.documentElement.clientWidth + 1 };
}'''


def servir(route):
    url = urlparse(route.request.url)
    if url.path == '/ReparacaoInterna':
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
    for largura in (1400, 768):
        for tema in ('light', 'dark'):
            context = browser.new_context(viewport={'width': largura, 'height': 900}, color_scheme=tema, locale='pt-PT', timezone_id='Europe/Lisbon')
            context.route('**/*', servir)
            page = context.new_page()
            page.goto('http://dmo.test/ReparacaoInterna')
            page.wait_for_load_state('load')
            m = page.evaluate(MEDIR)
            print(f'{largura} {tema}: {m}')
            nome = f'{largura} {tema}'
            if m is None or not m['visivel']:
                falhas.append(f'{nome}: sem o campo Hora')
            else:
                if abs(m['dTop'] - m['hTop']) > 2:
                    falhas.append(f'{nome}: a Hora não está na linha do Dia ({m["dTop"]:.0f} / {m["hTop"]:.0f})')
                if m['hLeft'] < m['dRight']:
                    falhas.append(f'{nome}: a Hora não está à direita do Dia')
                if len(m['valor']) != 5 or m['valor'][2] != ':':
                    falhas.append(f'{nome}: a Hora não vem em HH:mm («{m["valor"]}»)')
                if not m['doRegisto']:
                    falhas.append(f'{nome}: a Hora não vai com o registo')
                if m['scrollLateral']:
                    falhas.append(f'{nome}: scroll lateral')
            if captura is not None and largura == 1400 and tema == 'light':
                captura.parent.mkdir(parents=True, exist_ok=True)
                page.locator('.ri-card').first.screenshot(path=str(captura))
            context.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Dia e Hora na mesma linha: ok')

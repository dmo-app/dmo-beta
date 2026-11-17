"""O Enter na procura do Job On entra na produção, no Chromium (DMO-751, A-0525).

Diogo, 10/11/2026: «ao carregar Enter com referência e número de produção
deveria entrar». HTML exportado pelo teste HTTP real (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~JobOn_OEnterNaProcuraEntraNaProducao'
  python tests/visual/jobon_procura_enter_browser.py <pasta> [pasta-das-imagens]
Só ficheiros locais. A 1366 e a meio ecrã (683), em claro e em escuro.
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
imagens = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else None
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
pagina = sem_selo_local((fixtures / 'jobon-enter.html').read_text(encoding='utf-8'))
producoes = (fixtures / 'jobon-enter-producoes.html').read_text(encoding='utf-8')
sugestoes = (fixtures / 'jobon-enter-sugestoes.json').read_text(encoding='utf-8')
VAZIO = '<select id="lightProcuraProducoes" aria-label="Produção (escreva primeiro a referência)" disabled><option value="" selected>Produção</option></select>'


def servir(route):
    url = urlparse(route.request.url)
    q = parse_qs(url.query)
    if url.path == '/JobOn' and q.get('handler') == ['ProcurarProducao']:
        route.fulfill(content_type='text/html', body=producoes if q.get('q', [''])[0].upper() == '3245C002' else VAZIO)
    elif url.path == '/JobOn':
        route.fulfill(content_type='text/html', body=pagina)
    elif url.path == '/Sugestoes':
        termo = q.get('q', [''])[0].upper()
        route.fulfill(content_type='application/json', body=sugestoes if termo and '3245C002'.startswith(termo) else '[]')
    elif url.path == '/JobOn/Folha':
        route.fulfill(content_type='text/html', body='<!doctype html><title>Folha</title><h1>Folha</h1>')
    else:
        alvo = (assets / unquote(url.path).lstrip('/')).resolve()
        if alvo.is_relative_to(assets) and alvo.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
        else:
            route.fulfill(status=404, body='')


falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura in (1366, 683):
        for tema in ('light', 'dark'):
            nome = f'{largura}-{tema}'
            context = browser.new_context(viewport={'width': largura, 'height': 768}, color_scheme=tema)
            context.route('**/*', servir)
            page = context.new_page()
            erros = []
            page.on('pageerror', lambda erro: erros.append(str(erro)))
            page.goto('http://dmo.test/JobOn?ano=2026&mes=11&dia=10')
            page.wait_for_load_state('load')
            # 1) Só o início da referência: o Enter completa-a e passa ao dropdown.
            campo = page.locator('#lightSearch')
            campo.click()
            campo.press_sequentially('3245', delay=40)
            page.wait_for_timeout(400)
            campo.press('Enter')
            page.wait_for_function("document.activeElement && document.activeElement.id === 'lightProcuraProducoes'", timeout=5000)
            if campo.input_value() != '3245C002':
                falhas.append(f'{nome}: a referência não ficou completa ({campo.input_value()!r})')
            if imagens:
                imagens.mkdir(parents=True, exist_ok=True)
                page.keyboard.press('Escape')
                page.screenshot(path=str(imagens / f'{nome}-1-referencia-completa.png'))
            if 'Folha' in page.url:
                falhas.append(f'{nome}: completar a referência abriu logo a folha')
            # 2) Outro Enter, já no dropdown, entra na produção escolhida (a mais recente).
            with page.expect_navigation():
                page.keyboard.press('Enter')
            if '/JobOn/Folha?jobon_id=' not in page.url:
                falhas.append(f'{nome}: o Enter no dropdown não abriu a folha ({page.url})')
            # 3) Referência completa escrita: o Enter no campo entra logo.
            page.goto('http://dmo.test/JobOn?ano=2026&mes=11&dia=10')
            campo = page.locator('#lightSearch')
            campo.click()
            campo.press_sequentially('3245C002', delay=20)
            with page.expect_navigation():
                campo.press('Enter')
            if '/JobOn/Folha?jobon_id=' not in page.url:
                falhas.append(f'{nome}: o Enter com a referência completa não abriu a folha ({page.url})')
            if erros:
                falhas.append(f'{nome}: erros na página: {erros}')
            print(f'{nome}: ok' if not any(f.startswith(nome) for f in falhas) else f'{nome}: FALHOU')
            context.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)

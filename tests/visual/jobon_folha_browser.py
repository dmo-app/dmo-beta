"""A folha do Job On no Chromium (Diogo, 23/10/2026).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~JobOnFolhaPrimeiraFilaTests'
  python tests/visual/jobon_folha_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

A 1440 e a 390 px:
- os cartões da primeira fila (CM, MF e BQ) têm a mesma altura quando estão lado a lado;
- um clique nas notas amplia-as com «CM 5809 · Lote 1 · Notas» no título; Esc e um clique fora fecham;
- clicar numa alteração de «O que mudou» abre «Remover por engano»; a removida não abre;
- «Modo Editar» aparece no Editar e não na folha.
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
paginas = {
    'folha': sem_selo_local((fixtures / 'jobon-folha.html').read_text(encoding='utf-8')),
    'editar': sem_selo_local((fixtures / 'jobon-folha-editar.html').read_text(encoding='utf-8')),
}
atual = {'pagina': 'folha'}


def servir(route):
    url = urlparse(route.request.url)
    if url.path == '/JobOn/Folha':
        route.fulfill(content_type='text/html', body=paginas[atual['pagina']])
        return
    alvo = (assets / unquote(url.path).lstrip('/')).resolve()
    if alvo.is_relative_to(assets) and alvo.is_file():
        route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
    else:
        route.fulfill(status=404, body='')


FILA = '''() => [...document.querySelectorAll('#gMoldes > .card')].map(c => {
  const r = c.getBoundingClientRect(); return { top: Math.round(r.top), h: Math.round(r.height) };
})'''
NOTA = '''() => { const c = document.querySelector('[data-jof-familia="CM"] [data-jof-nota]');
  return { aberta: c.classList.contains('is-ampliada'), titulo: c.querySelector(':scope > span').textContent.trim() }; }'''

falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura in (1440, 390):
        nome = f'{largura}px'
        context = browser.new_context(viewport={'width': largura, 'height': 900}, locale='pt-PT', timezone_id='Europe/Lisbon')
        context.route('**/*', servir)
        page = context.new_page()
        erros = []
        page.on('pageerror', lambda e: erros.append(str(e)))

        atual['pagina'] = 'folha'
        page.goto('http://dmo.test/JobOn/Folha?jobon_id=1&vista=alteracoes')
        page.wait_for_load_state('load')

        # A primeira fila: lado a lado, a mesma altura.
        fila = page.evaluate(FILA)
        print(f'{nome} fila: {fila}')
        if len(fila) != 3:
            falhas.append(f'{nome}: a primeira fila não tem 3 cartões ({fila})')
        else:
            linhas = {}
            for c in fila:
                linhas.setdefault(c['top'], []).append(c['h'])
            for top, alturas in linhas.items():
                if max(alturas) - min(alturas) > 2:
                    falhas.append(f'{nome}: cartões da mesma fila com alturas diferentes ({alturas})')
            if largura == 1440 and len(linhas) != 1:
                falhas.append(f'{nome}: CM, MF e BQ não estão na mesma fila ({fila})')

        # As notas ampliam e fecham.
        page.locator('[data-jof-familia="CM"] [data-jof-nota]').click()
        nota = page.evaluate(NOTA)
        if not nota['aberta'] or 'CM 5809' not in nota['titulo'] or 'Lote 1' not in nota['titulo']:
            falhas.append(f'{nome}: a nota não ampliou com «CM 5809 · Lote 1» ({nota})')
        page.keyboard.press('Escape')
        if page.evaluate(NOTA)['aberta']:
            falhas.append(f'{nome}: Esc não fechou a nota')
        if page.evaluate(NOTA)['titulo'] != 'Notas':
            falhas.append(f'{nome}: o título não voltou a «Notas»')
        page.locator('[data-jof-familia="CM"] [data-jof-nota]').click()
        page.mouse.click(5, 5)
        if page.evaluate(NOTA)['aberta']:
            falhas.append(f'{nome}: um clique fora não fechou a nota')

        # «O que mudou»: a alteração abre a janela, a removida não.
        removida = page.locator('li[data-jof-alteracao-removida]').first
        removida.scroll_into_view_if_needed()
        removida.click()
        if page.evaluate("document.querySelector('[data-jof-remover]').open"):
            falhas.append(f'{nome}: a alteração removida abriu a janela')
        linha = page.locator('li[data-jof-alteracao]:not([data-jof-alteracao=""])').first
        linha.scroll_into_view_if_needed()
        linha.click()
        if not page.evaluate("document.querySelector('[data-jof-remover]').open"):
            falhas.append(f'{nome}: a alteração não abriu a janela Remover por engano')
        elif not page.get_by_role('button', name='Remover por engano').is_visible():
            falhas.append(f'{nome}: a janela não mostra «Remover por engano»')
        page.keyboard.press('Escape')

        if page.locator('[data-jof-modo-editar]').count() != 0:
            falhas.append(f'{nome}: «Modo Editar» na folha')

        # No Editar, o aviso grande no lugar da procura.
        atual['pagina'] = 'editar'
        page.goto('http://dmo.test/JobOn/Folha?jobon_id=1&editar=1')
        page.wait_for_load_state('load')
        aviso = page.locator('[data-jof-modo-editar]')
        if aviso.count() != 1 or not aviso.is_visible():
            falhas.append(f'{nome}: sem «Modo Editar» no Editar')
        if page.locator('#jofReferencia').count() != 0:
            falhas.append(f'{nome}: a procura ficou no Editar')
        if page.evaluate('document.documentElement.scrollWidth > document.documentElement.clientWidth + 1'):
            falhas.append(f'{nome}: scroll lateral no Editar')

        if erros:
            falhas.append(f'{nome}: erros de JavaScript {erros}')
        context.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Folha do Job On: ok')

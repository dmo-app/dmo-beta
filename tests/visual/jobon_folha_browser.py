"""A folha do Job On no Chromium (Diogo, 23/10/2026).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~JobOnFolhaPrimeiraFilaTests'
  python tests/visual/jobon_folha_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

A 1440, 1366 (o ecrã base do Diogo), 1280 (o mesmo com zoom ou barra lateral) e 390 px:
- a grelha em quatro colunas iguais (CM+TP, MF+CS, BQ+AN, PU+ARR), os Calibres por baixo de BQ+AN e PU+ARR e a referência e o lote do cabeçalho inteiros;
- um clique nas notas amplia-as com «CM 5809 · Lote 1 · Notas» no título; Esc e um clique fora fecham;
- clicar numa alteração de «O que mudou» abre «Remover por engano»; a removida não aparece;
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


FILA = '''() => ['CM', 'MF', 'BQ'].map(f => document.querySelector(`.zones [data-jof-familia="${f}"]`).closest('.fcol > .card, .fcol > .pair, .fcol > article') || document.querySelector(`[data-jof-familia="${f}"]`)).map(c => {
  const r = c.getBoundingClientRect(); return { top: Math.round(r.top), h: Math.round(r.height) };
})'''
NOTA = '''() => { const c = document.querySelector('[data-jof-familia="CM"] [data-jof-nota]');
  return { aberta: c.classList.contains('is-ampliada'), titulo: c.querySelector(':scope > span').textContent.trim() }; }'''

falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura in (1440, 1366, 1280, 390):
        nome = f'{largura}px'
        context = browser.new_context(viewport={'width': largura, 'height': 768 if largura == 1366 else 900}, locale='pt-PT', timezone_id='Europe/Lisbon')
        context.route('**/*', servir)
        page = context.new_page()
        erros = []
        page.on('pageerror', lambda e: erros.append(str(e)))

        atual['pagina'] = 'folha'
        page.goto('http://dmo.test/JobOn/Folha?jobon_id=1&vista=alteracoes')
        page.wait_for_load_state('load')

        # A grelha (Diogo, 26/10/2026): quatro colunas iguais e independentes — CM+TP, MF+CS, BQ+AN,
        # PU+ARR —, os Calibres por baixo de BQ+AN e PU+ARR, o PI à direita por baixo das Notas gerais.
        # Nenhum cartão fica estreito, nem com a referência cortada nem o «Lote» partido.
        if largura in (1440, 1366, 1280):
            pos = page.evaluate("""() => Object.fromEntries(['CM', 'TP', 'MF', 'CS', 'BQ', 'AN', 'PU', 'ARR', 'PI'].map(f => {
                const r = document.querySelector(`[data-jof-familia="${f}"]`).getBoundingClientRect();
                return [f, { left: Math.round(r.left), top: Math.round(r.top), w: Math.round(r.width) }];
              }).concat([['CAL', (() => { const r = document.querySelector('.cal-card').getBoundingClientRect(); return { left: Math.round(r.left), top: Math.round(r.top), w: Math.round(r.width) }; })()],
                         ['NOTAS', (() => { const r = document.querySelector('#notesTitle').getBoundingClientRect(); return { left: Math.round(r.left), top: Math.round(r.top), w: Math.round(r.width) }; })()]]))""")
            print(f'{nome} grelha: {pos}')
            larguras = [pos[f]['w'] for f in ('CM', 'TP', 'MF', 'CS', 'BQ', 'AN', 'PU', 'ARR')]
            if max(larguras) - min(larguras) > 6 or min(larguras) < 190:
                falhas.append(f'{nome}: os cartões não têm a mesma largura ({larguras})')
            if not (abs(pos['CM']['left'] - pos['TP']['left']) <= 3 and abs(pos['MF']['left'] - pos['CS']['left']) <= 3 and abs(pos['BQ']['left'] - pos['AN']['left']) <= 3 and abs(pos['PU']['left'] - pos['ARR']['left']) <= 3):
                falhas.append(f'{nome}: as colunas não estão alinhadas ({pos})')
            if not (pos['CM']['left'] < pos['MF']['left'] < pos['BQ']['left'] < pos['PU']['left'] < pos['PI']['left']):
                falhas.append(f'{nome}: a ordem das colunas não é CM, MF, BQ, PU, PI ({pos})')
            if not (abs(pos['CAL']['left'] - pos['AN']['left']) <= 3 and pos['CAL']['top'] > pos['AN']['top'] and pos['CAL']['top'] > pos['ARR']['top']):
                falhas.append(f'{nome}: os Calibres não estão por baixo de BQ+AN e PU+ARR ({pos})')
            if not (pos['PI']['top'] > pos['NOTAS']['top']):
                falhas.append(f'{nome}: o PI não está por baixo das Notas gerais ({pos})')
            cortados = page.evaluate("""() => [...document.querySelectorAll('.tool-head [data-jof-tool-ref], .tool-head [data-jof-tool-lote]')]
                .filter(e => e.scrollWidth > e.clientWidth + 1 || e.getClientRects().length > 1).map(e => e.textContent.trim())""")
            if cortados:
                falhas.append(f'{nome}: referência ou lote cortados no cabeçalho ({cortados})')
            medidas = page.evaluate("() => [...document.querySelectorAll('.table-wrap')].some(t => t.scrollWidth > t.clientWidth + 1)")
            if medidas:
                falhas.append(f'{nome}: uma tabela de medidas tem scroll horizontal')

        if largura in (1440, 1366, 1280):
            # A produção do cabeçalho numa só linha (Diogo, 23/10/2026).
            prod = page.evaluate("""() => { const e = document.querySelector('.pick-row .prod-trigger, .pick-row .prod-drop, .pick-row select.prod-select');
              const cs = getComputedStyle(e); const lh = parseFloat(cs.lineHeight) || parseFloat(cs.fontSize) * 1.2;
              const t = e.querySelector('.prod-trigger') || e;
              const r = document.createRange(); r.selectNodeContents(t); const linhas = new Set([...r.getClientRects()].map(x => Math.round(x.top))).size;
              return { linhas, h: Math.round(t.getBoundingClientRect().height), lh, texto: t.textContent.trim() }; }""")
            print(f'{nome} produção: {prod}')
            if prod['linhas'] > 1:
                falhas.append(f'{nome}: a produção parte em duas linhas ({prod})')
            # Com a Informação técnica da CM aberta, as notas da MF não esticam.
            antes = page.evaluate("() => Math.round(document.querySelector('[data-jof-familia=\"MF\"] [data-jof-nota]').getBoundingClientRect().height)")
            page.locator('[data-jof-tecnica="CM"] summary').click()
            depois = page.evaluate("() => Math.round(document.querySelector('[data-jof-familia=\"MF\"] [data-jof-nota]').getBoundingClientRect().height)")
            print(f'{nome} notas MF: fechada {antes}, aberta {depois}')
            if depois > antes + 2:
                falhas.append(f'{nome}: com a Informação técnica aberta, as notas da MF esticaram ({antes} → {depois})')
            page.locator('[data-jof-tecnica="CM"] summary').click()

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

        # «O que mudou»: a removida não aparece (Diogo, 23/10/2026); a outra abre a janela.
        if page.locator('.jobon-resumo li').count() != 1 or 'T188' in page.locator('.jobon-resumo').inner_text():
            falhas.append(f'{nome}: a alteração removida aparece em «O que mudou»')
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

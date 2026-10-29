"""Boquilhas › Movimentos: Registo do dia e calendário no Chromium a 1290×646 (DMO-596, Diogo 29/10/2026).

As barras B1/B2/B3… mostram a ficha (BQ, lote, produção), estão alinhadas em colunas, escolhem-se (B1 incluída) e não mexem
ao mudar de dia; o dia escolhido aparece marcado; o quadro Saídas/Entradas saiu; os calendários do Job On, Sumário e Revisão
têm os dias com a mesma altura. Servidor local descartável:
  DMO_UI_TEST_URL=http://127.0.0.1:5111 DMO_UI_USER=... DMO_UI_PASSWORD=... python tests/visual/bq_registo_dia_calendario_browser.py
Precisa de movimentos em dois dias de outubro de 2026 (por omissão os dias 4 e 15).
"""
import os
from urllib.parse import urlparse

from playwright.sync_api import sync_playwright

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only a local disposable environment.')
DIA_COM = os.environ.get('DMO_UI_DIA', '4')
DIA_OUTRO = os.environ.get('DMO_UI_DIA_OUTRO', '15')
MOV = base + '/Boquilhas?historico=1&vista=grupos&ano=2026&mes=10&dia='

with sync_playwright() as playwright:
    launch = {'args': ['--no-sandbox']}
    if os.environ.get('DMO_CHROMIUM'):
        launch['executable_path'] = os.environ['DMO_CHROMIUM']
    browser = playwright.chromium.launch(**launch)
    page = browser.new_page(viewport={'width': 1290, 'height': 646})
    page.goto(base + '/login')
    page.locator('#identifier').fill(os.environ.get('DMO_UI_USER', 'stress@local.test'))
    page.locator('#password').fill(os.environ.get('DMO_UI_PASSWORD', 'stress-1234'))
    with page.expect_navigation():
        page.locator('#password').press('Enter')

    def medir(dia):
        page.goto(MOV + dia)
        return page.evaluate("""() => {
          const d = document.querySelector('#bqHistDia').getBoundingClientRect();
          const barras = [...document.querySelectorAll('#bqHistDia .beta-light-production')].map(p => {
            const b = p.getBoundingClientRect();
            const alvo = document.elementFromPoint(b.x + b.width / 2, b.y + b.height / 2);
            return {x: [...p.children].filter(c => c.tagName === 'SPAN' || c.tagName === 'STRONG').map(c => Math.round(c.getBoundingClientRect().x)),
                    h: Math.round(b.height), textos: [...p.querySelectorAll('span')].map(s => s.getBoundingClientRect().width > 8 && s.textContent.trim()).filter(Boolean).length,
                    clicavel: !!alvo && p.contains(alvo), visivel: b.bottom <= d.bottom + 1};
          });
          const sel = document.querySelector('#dmoCalendario .dmo-calendar-day.selected');
          const dias = document.querySelector('#dmoCalendario .dmo-calendar-day:not(.empty)').getBoundingClientRect().height;
          return {dy: Math.round(d.y), dh: Math.round(d.height), barras,
                  sel: sel && getComputedStyle(sel).backgroundColor, dias: Math.round(dias),
                  totais: document.querySelector('#bqHistTotais') !== null};
        }""")

    com = medir(DIA_COM)
    outro = medir(DIA_OUTRO)
    sem = medir('0')
    assert not com['totais'], 'o quadro de totais devia ter saído'
    assert com['barras'], 'sem barras no dia'
    assert com['sel'] and com['sel'] != 'rgba(0, 0, 0, 0)', ('dia escolhido sem marca', com['sel'])
    for barra in com['barras']:
        assert barra['textos'] >= 3, ('barra vazia', barra)
        assert barra['h'] == com['barras'][0]['h'], 'barras com alturas diferentes'
        assert barra['x'] == com['barras'][0]['x'], ('barras desalinhadas', barra['x'], com['barras'][0]['x'])
    # Só as barras visíveis dentro da caixa se podem clicar (a caixa desliza); a primeira, B1, tem de se escolher.
    assert com['barras'][0]['clicavel'], 'B1 não se pode escolher'
    assert (com['dy'], com['dh']) == (outro['dy'], outro['dh']) == (sem['dy'], sem['dh']), ('o Registo do dia mexe', com, outro, sem)
    page.goto(MOV + DIA_COM)
    with page.expect_navigation():
        page.locator('#bqHistDia .beta-light-production').first.click()
    assert 'trace_id=' in page.url, page.url

    alturas = {}
    for nome, caminho in (('jobon', '/JobOn?ano=2026&mes=10&dia=' + DIA_COM), ('sumario', '/Planeamento/Sumario?ano=2026&mes=10&dia=' + DIA_COM),
                          ('revisao', '/Controlo/Approve?ano=2026&mes=10&dia=' + DIA_COM), ('boquilhas', '/Boquilhas?historico=1&vista=grupos&ano=2026&mes=10&dia=' + DIA_COM)):
        page.goto(base + caminho)
        alturas[nome] = page.evaluate("""() => { const c = document.querySelector('#dmoCalendario');
          const d = c.querySelector('.dmo-calendar-day:not(.empty)').getBoundingClientRect(); const s = c.querySelector('.dmo-calendar-day.selected');
          return [Math.round(d.height), s ? getComputedStyle(s).backgroundColor : null, getComputedStyle(c.querySelector('.dmo-calendar-week')).fontSize]; }""")
    assert len({tuple(v) for v in alturas.values()}) == 1, ('calendários diferentes', alturas)
    print('PASS Boquilhas Movimentos 1290×646: barras com texto, alinhadas e escolhíveis (B1), sem saltar ao mudar de dia; dia marcado; sem totais; calendários iguais')
    browser.close()

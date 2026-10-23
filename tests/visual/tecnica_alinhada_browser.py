"""Registar ferramenta real: os grupos da Informação técnica alinhados (Diogo, 23/10/2026).

«dados técnicos Peso Gargalo Tampão não têm a mesma altura»: em cada largura
e em cada tipo, os títulos, os rótulos e as caixas (inputs e o select do
Processo) dos grupos lado a lado têm o mesmo topo e a mesma altura, e os
traços entre os grupos começam no topo dos títulos. Claro e escuro.

  DMO_UI_TEST_URL=http://127.0.0.1:5299 python3 tests/visual/tecnica_alinhada_browser.py
  (ou DMO_CRIAR_PAGE=<url de um HTML do /Ferramentas?criar=1 exportado>)
"""
import os
import sys
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
pagina = os.environ.get('DMO_CRIAR_PAGE')
if not pagina and urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')

MEDIR = """() => [...document.querySelectorAll('.ferr-create-tech .ferr-tech-bloco')].filter(b => b.getClientRects().length).map(b => {
  const caixas = s => [...b.querySelectorAll(s)].filter(e => e.getClientRects().length).map(e => { const r = e.getBoundingClientRect(); return [Math.round(r.top), Math.round(r.height)]; });
  return { titulo: caixas('legend'), rotulos: caixas('label'), campos: caixas('input,select') };
})"""
SEPS = """() => [...document.querySelectorAll('.ferr-create-tech .ferr-tech-sep')].filter(e => e.getClientRects().length).map(e => { const r = e.getBoundingClientRect(); return [Math.round(r.top), Math.round(r.height)]; })"""
# Os grupos de cada tipo (a MF só tem o Gargalo).
GRUPOS = {'CM': 3, 'BQ': 2, 'MF': 1}

falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for tema in ('light', 'dark'):
        for largura in (1400, 1200, 820):
            for tipo in ('CM', 'BQ', 'MF'):
                page = browser.new_page(viewport={'width': largura, 'height': 900}, color_scheme=tema)
                if pagina:
                    page.goto(pagina)
                else:
                    page.goto(base + '/login')
                    page.locator('#identifier').fill('stress@local.test')
                    page.locator('#password').fill('stress-1234')
                    with page.expect_navigation():
                        page.locator('#password').press('Enter')
                    page.goto(base + '/Ferramentas?criar=1')
                page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
                page.select_option('#ferr-new-type', tipo)
                page.evaluate("document.querySelectorAll('details.ferr-create-tech').forEach(d => d.open = true)")
                page.wait_for_timeout(200)
                grupos = page.evaluate(MEDIR)
                if len(grupos) != GRUPOS[tipo]:
                    falhas.append(f'{tema} {largura} {tipo}: {len(grupos)} grupos visíveis (esperados {GRUPOS[tipo]})')
                # Os grupos na mesma linha (o mesmo topo do título) comparam-se entre si.
                linhas = {}
                for g in grupos:
                    linhas.setdefault(g['titulo'][0][0], []).append(g)
                # Com mais de um grupo, a 1200 px ou mais ficam todos lado a lado.
                if GRUPOS[tipo] > 1 and largura >= 1200 and max((len(gs) for gs in linhas.values()), default=0) < 2:
                    falhas.append(f'{tema} {largura} {tipo}: menos de 2 grupos lado a lado')
                for topo, gs in linhas.items():
                    for chave in ('rotulos', 'campos'):
                        primeiros = {tuple(g[chave][0]) for g in gs}
                        dentro = {tuple(c) for g in gs for c in g[chave] if abs(c[0] - g[chave][0][0]) < 30}
                        if len(primeiros) > 1 or len(dentro) > 1:
                            falhas.append(f'{tema} {largura} {tipo}: {chave} desalinhados {sorted(dentro)}')
                # Cada traço pertence à linha de grupos mais próxima: começa no topo dos
                # títulos dela e acaba no fundo das caixas dela (±1px).
                for sep_topo, sep_altura in page.evaluate(SEPS):
                    if not linhas:
                        falhas.append(f'{tema} {largura} {tipo}: traço sem grupos')
                        break
                    topo = min(linhas, key=lambda t: abs(t - sep_topo))
                    fundo = max(c[0] + c[1] for g in linhas[topo] for c in g['campos'])
                    if abs(sep_topo - topo) > 1 or abs(sep_topo + sep_altura - fundo) > 1:
                        falhas.append(f'{tema} {largura} {tipo}: traço de {sep_topo} a {sep_topo + sep_altura}, a linha vai de {topo} a {fundo}')
                print(tema, largura, tipo, [(g['titulo'][0], g['rotulos'][0], g['campos'][0]) for g in grupos])
                page.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Informação técnica alinhada: ok')

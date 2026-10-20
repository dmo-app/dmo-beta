"""Ferramentas → Ficha: os avisos não empurram a página (Diogo, 17/10/2026).

Só no ambiente local descartável (scripts/stress/arrancar_app.sh). Mede com
getBoundingClientRect os elementos da ficha antes e depois de cada aviso
aparecer e desaparecer, a 1400 px:
- os avisos que a ficha abre depois de gravar (lote vazio, quantidade e valor
  técnico ilegíveis, lote já existente na duplicação), pelo URL do PRG;
- a quantidade e o valor técnico ilegíveis gravados a sério no Editar e na
  Informação técnica (troca parcial), e o aviso a sair na gravação seguinte.
Nenhum elemento se pode mexer (nem ao aparecer, nem ao sair, nem ao fechar
no «×»); o aviso tem de estar visível na pilha do canto (fora do fluxo), com
folga em relação às barras e à margem do ecrã.

  DMO_UI_TEST_URL=http://127.0.0.1:5350 FICHA_TOOL=<tool_id de uma CM> \\
  python3 tests/visual/ficha_avisos_browser.py
"""
import os
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')
tool = os.environ.get('FICHA_TOOL', 'T0392728151')
ficha = f'{base}/Ferramentas/Ficha?tool_id={tool}&tecnica=1'

# Os elementos que não se podem mexer.
ELEMENTOS = {
    'cabeçalho': '.ferr-page .page-head',
    'Opções e Voltar à lista': '#ferrGeral .dmo-card-topo-acoes',
    'Geral': '#ferrGeral',
    'Editar': '#ferrEditar',
    'Lote (Editar)': '#ferr-edit-lot',
    'Guardar (Editar)': '#ferr-edit-guardar',
    'Informação técnica': 'section[data-dmo-fica]',
    'Peso nominal': '#ferr-tech-nominal',
    'Guardar (técnica)': '#ferr-tech-guardar',
}
FOLGA = 8  # px mínimos entre o aviso e as barras / a margem do ecrã

MEDIR = """(sels) => Object.fromEntries(Object.entries(sels).map(([nome, sel]) => {
  const e = document.querySelector(sel);
  if (!e) return [nome, null];
  const r = e.getBoundingClientRect();
  return [nome, [r.x + scrollX, r.y + scrollY, r.width, r.height].map(v => Math.round(v * 10) / 10)];
}))"""

AVISOS = """() => {
  const barras = document.querySelector('.dmo-shell-navigation');
  const fundo = barras ? barras.getBoundingClientRect().bottom : 0;
  return [...document.querySelectorAll('.ferr-aviso.warn, [data-dmo-toast="aviso"]')]
    .filter(e => e.getClientRects().length && !e.classList.contains('a-sair'))
    .map(e => { const r = e.getBoundingClientRect();
      return { texto: e.textContent.replace('×', '').trim().slice(0, 70),
               naPilha: !!e.closest('.dmo-toast-pilha') && getComputedStyle(e.closest('.dmo-toast-pilha')).position === 'fixed',
               fechar: !!e.querySelector('.dmo-toast-fechar'),
               abaixoDasBarras: Math.round(r.top - fundo), margemDireita: Math.round(innerWidth - r.right) }; });
}"""


def login(page):
    page.goto(base + '/login')
    page.locator('#identifier').fill('stress@local.test')
    page.locator('#password').fill('stress-1234')
    with page.expect_navigation():
        page.locator('#password').press('Enter')


def medir(page):
    return page.evaluate(MEDIR, ELEMENTOS)


def mexeu(antes, depois):
    return {nome: (antes[nome], depois[nome]) for nome in antes
            if antes[nome] != depois[nome] and (antes[nome] is None or depois[nome] is None
                                                 or max(abs(a - b) for a, b in zip(antes[nome], depois[nome])) > 0.5)}


falhas = []


def verificar(cenario, antes, depois, page, aviso_esperado):
    deslocados = mexeu(antes, depois)
    avisos = page.evaluate(AVISOS)
    print(f'{cenario}:')
    if deslocados:
        for nome, (a, d) in deslocados.items():
            print(f'  MEXEU {nome}: y {a[1]} → {d[1]} (Δ{round(d[1] - a[1], 1)} px), altura {a[3]} → {d[3]}')
        falhas.append(f'{cenario}: {", ".join(deslocados)} mexeram')
    else:
        print('  nada se mexeu')
    if aviso_esperado:
        if not avisos:
            falhas.append(f'{cenario}: o aviso não está visível')
            return
        for aviso in avisos:
            print(f'  aviso «{aviso["texto"]}…» na pilha={aviso["naPilha"]} ×={aviso["fechar"]} '
                  f'{aviso["abaixoDasBarras"]} px abaixo das barras, {aviso["margemDireita"]} px da margem')
            if not (aviso['naPilha'] and aviso['fechar']):
                falhas.append(f'{cenario}: o aviso não está na pilha do canto com «×»')
            if aviso['abaixoDasBarras'] < FOLGA or aviso['margemDireita'] < FOLGA:
                falhas.append(f'{cenario}: o aviso fica colado às barras ou à margem')
    elif avisos:
        falhas.append(f'{cenario}: o aviso continua visível ({avisos[0]["texto"]})')


with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'), args=['--no-sandbox'])
    context = browser.new_context(viewport={'width': 1400, 'height': 900})
    page = context.new_page()
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    login(page)
    page.goto(ficha)
    expect(page.locator('#ferr-tech-guardar')).to_be_visible()
    limpo = medir(page)

    # 1. Os avisos que a ficha abre depois de gravar (o PRG traz o aviso no URL).
    for cenario, query in (
        ('lote vazio (sem_lote)', '&sem_lote=1'),
        ('quantidade ilegível (Editar)', '&quantidade_ilegivel=abc'),
        ('lote já registado (Duplicar)', '&lote_existente=1'),
        ('valor técnico ilegível', '&ilegivel_nominal_weight=abc'),
        ('três avisos juntos', '&sem_lote=1&quantidade_ilegivel=abc&ilegivel_nominal_weight=abc'),
    ):
        page.goto(ficha + query)
        expect(page.locator('#ferr-tech-guardar')).to_be_visible()
        page.wait_for_timeout(150)
        verificar(f'GET {cenario}', limpo, medir(page), page, True)

    # Fechar os avisos no «×» também não mexe nada.
    antes = medir(page)
    while page.locator('.dmo-toast-pilha [data-dmo-toast="aviso"]:not(.a-sair) .dmo-toast-fechar').count():
        page.locator('.dmo-toast-pilha [data-dmo-toast="aviso"]:not(.a-sair) .dmo-toast-fechar').first.click()
    page.wait_for_timeout(300)
    verificar('fechar os três avisos no «×»', antes, medir(page), page, False)

    # 2. Gravar a sério (troca parcial): o aviso aparece e sai na gravação seguinte.
    page.goto(ficha)
    expect(page.locator('#ferr-tech-guardar')).to_be_visible()

    def gravar(botao):
        page.evaluate("window.__trocou=0;document.addEventListener('dmo:troca',()=>{window.__trocou=1},{once:true})")
        page.locator(botao).click()
        page.wait_for_function('window.__trocou')
        page.wait_for_timeout(200)

    quantidade = page.locator('#ferr-edit-qty').input_value()
    antes = medir(page)
    page.locator('#ferr-edit-qty').fill('abc')
    gravar('#ferr-edit-guardar')
    verificar('Editar: quantidade «abc» gravada', antes, medir(page), page, True)
    antes = medir(page)
    page.locator('#ferr-edit-qty').fill(quantidade)
    gravar('#ferr-edit-guardar')
    verificar('Editar: gravar de novo (o aviso sai)', antes, medir(page), page, False)

    nominal = page.locator('#ferr-tech-nominal').input_value()
    antes = medir(page)
    page.locator('#ferr-tech-nominal').fill('12,3,4')
    gravar('#ferr-tech-guardar')
    verificar('Técnica: peso nominal «12,3,4» gravado', antes, medir(page), page, True)
    antes = medir(page)
    page.locator('#ferr-tech-nominal').fill(nominal)
    gravar('#ferr-tech-guardar')
    verificar('Técnica: gravar de novo (o aviso sai)', antes, medir(page), page, False)

    assert not errors, errors
    context.close()
    browser.close()

if falhas:
    raise SystemExit('FALHAS:\n- ' + '\n- '.join(falhas))
print('PASS Ficha 1400px: os avisos aparecem, saem e fecham sem mexer nenhum elemento, na pilha do canto')

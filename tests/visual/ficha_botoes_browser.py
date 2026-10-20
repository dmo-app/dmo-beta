"""Ferramentas → Ficha: os botões de cada card no canto superior direito (Diogo, 18/10/2026).

«Metem os botões em baixo aumenta espaço, sempre canto superior direito
dentro dos cards.» Só no ambiente local descartável
(scripts/stress/arrancar_app.sh). A 1400 px, com getBoundingClientRect:
- cada botão está dentro do seu card, na linha do título (centro vertical
  dentro da altura do título), encostado à direita (a 30 px ou menos da
  margem direita do card) e acima de todos os campos do card;
- «Opções» e «Voltar à lista» no primeiro card (Geral); o «Guardar» do Editar
  e o da Informação técnica na linha do título de cada um;
- o «Guardar» da Informação técnica não abre nem fecha o card (aberta e
  fechada), e clicar no título continua a abrir e a fechar;
- gravar continua a funcionar pelos botões novos: 1 POST, sem recarregar a
  página, o foco fica no «Guardar» e a confirmação vai para a pilha;
- mede (e só reporta) a sobreposição da pilha de avisos sobre os botões, com
  a página no topo e rolada.

  DMO_UI_TEST_URL=http://127.0.0.1:5350 FICHA_TOOL=<tool_id de uma CM> \\
  python3 tests/visual/ficha_botoes_browser.py
"""
import os
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5299')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')
tool = os.environ.get('FICHA_TOOL', 'T0392728151')
ficha = f'{base}/Ferramentas/Ficha?tool_id={tool}'

# botão → (card, título do card, campos do card que têm de ficar abaixo do botão)
BOTOES = {
    'Opções': ('#ferrGeral summary.dmo-button', '#ferrGeral', '#ferr-facts-title', '#ferrGeral .dmo-facts'),
    'Voltar à lista': ('#ferrGeral .dmo-card-topo-acoes a.dmo-button', '#ferrGeral', '#ferr-facts-title', '#ferrGeral .dmo-facts'),
    'Guardar (Editar)': ('#ferr-edit-guardar', '#ferrEditar', '#ferr-edit-facts-title', '#ferr-edit-type'),
    'Guardar (técnica)': ('#ferr-tech-guardar', 'section[data-dmo-fica]', '#ferr-tech-title', '#ferr-tech-process'),
}
MARGEM_DIREITA = 30  # px máximos entre o botão e a borda direita do card

POSICAO = """([botao, card, titulo, campo]) => {
  const r = s => { const e = document.querySelector(s); if (!e || !e.getClientRects().length) return null;
    const b = e.getBoundingClientRect(); return { top: b.top, bottom: b.bottom, left: b.left, right: b.right, h: b.height, w: b.width }; };
  const b = document.querySelector(botao);
  const grupo = b && b.closest('.dmo-card-topo-acoes');
  return { botao: r(botao), card: r(card), titulo: r(titulo), campo: r(campo),
           grupo: grupo ? (({ right }) => ({ right }))(grupo.getBoundingClientRect()) : null,
           dentro: !!document.querySelector(card) && document.querySelector(card).contains(document.querySelector(botao)) };
}"""

PILHA = """(sels) => {
  const pilha = document.querySelector('.dmo-toast-pilha');
  const avisos = pilha ? [...pilha.querySelectorAll('[data-dmo-toast]:not(.a-sair)')].map(e => e.getBoundingClientRect()) : [];
  const caixa = avisos.length ? { left: Math.round(Math.min(...avisos.map(a => a.left))), top: Math.round(Math.min(...avisos.map(a => a.top))),
    right: Math.round(Math.max(...avisos.map(a => a.right))), bottom: Math.round(Math.max(...avisos.map(a => a.bottom))), n: avisos.length } : null;
  return Object.fromEntries([['__pilha', caixa], ...Object.entries(sels).map(([nome, sel]) => {
    const e = document.querySelector(sel);
    if (!e || !e.getClientRects().length) return [nome, null];
    const b = e.getBoundingClientRect();
    const tapa = avisos.map(a => Math.max(0, Math.min(a.right, b.right) - Math.max(a.left, b.left)) * Math.max(0, Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top)))
      .reduce((x, y) => x + y, 0);
    return [nome, { tapadoPx2: Math.round(tapa), area: Math.round(b.width * b.height),
      folgaHorizontal: avisos.length ? Math.round(Math.min(...avisos.map(a => a.left - b.right))) : null,
      top: Math.round(b.top), bottom: Math.round(b.bottom), right: Math.round(b.right) }];
  })]);
}"""


def login(page):
    page.goto(base + '/login')
    page.locator('#identifier').fill('stress@local.test')
    page.locator('#password').fill('stress-1234')
    with page.expect_navigation():
        page.locator('#password').press('Enter')


falhas = []


def confirmar_cantos(page, cenario):
    print(f'{cenario}:')
    for nome, (botao, card, titulo, campo) in BOTOES.items():
        p = page.evaluate(POSICAO, [botao, card, titulo, campo])
        b, c, t = p['botao'], p['card'], p['titulo']
        if not (b and c and t):
            falhas.append(f'{cenario}: {nome} não está visível')
            continue
        centro = (b['top'] + b['bottom']) / 2
        # O grupo de botões do card encosta à direita (o «Opções» fica ao lado do «Voltar à lista»).
        direita = round(c['right'] - (p['grupo'] or b)['right'], 1)
        linha = t['top'] - 1 <= centro <= t['bottom'] + 1 or (b['top'] <= (t['top'] + t['bottom']) / 2 <= b['bottom'])
        acima = p['campo'] is None or b['bottom'] <= p['campo']['top'] + 0.5
        topo = round(b['top'] - c['top'], 1)
        print(f'  {nome}: {topo} px abaixo do topo do card, grupo a {direita} px da direita, na linha do título={linha}, acima dos campos={acima}')
        if not p['dentro']:
            falhas.append(f'{cenario}: {nome} está fora do card')
        if not p['grupo'] or not (0 <= direita <= MARGEM_DIREITA):
            falhas.append(f'{cenario}: {nome} não está encostado à direita do card ({direita} px)')
        if not linha:
            falhas.append(f'{cenario}: {nome} não está na linha do título')
        if not acima:
            falhas.append(f'{cenario}: {nome} está abaixo dos campos do card')
        if b['left'] < t['right'] - 0.5:
            falhas.append(f'{cenario}: {nome} sobrepõe-se ao título')


def medir_pilha(page, cenario):
    sels = {nome: v[0] for nome, v in BOTOES.items()}
    r = page.evaluate(PILHA, sels)
    print(f'  pilha vs botões ({cenario}, scrollY={page.evaluate("Math.round(scrollY)")}):')
    caixa = r.pop('__pilha')
    if caixa is None:
        print('    pilha: vazia')
    else:
        print(f'    pilha: {caixa["n"]} mensagem(ns), x {caixa["left"]}–{caixa["right"]}, y {caixa["top"]}–{caixa["bottom"]}')
    for nome, m in r.items():
        if m is None:
            print(f'    {nome}: fora do ecrã/escondido')
        else:
            print(f'    {nome}: tapado {m["tapadoPx2"]} px² de {m["area"]} px², folga horizontal até ao aviso {m["folgaHorizontal"]} px (botão y {m["top"]}–{m["bottom"]}, direita {m["right"]})')
    return r


with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'), args=['--no-sandbox'])
    context = browser.new_context(viewport={'width': 1400, 'height': 900})
    page = context.new_page()
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    pedidos = []
    page.on('request', lambda r: pedidos.append((r.method, r.resource_type)) if r.resource_type in ('document', 'fetch') else None)
    login(page)

    # 1. A técnica aberta: os quatro botões no canto de cada card.
    page.goto(ficha + '&tecnica=1')
    expect(page.locator('#ferr-tech-guardar')).to_be_visible()
    confirmar_cantos(page, 'técnica aberta')
    # Nenhum botão de ação ficou no fundo dos cards nem no cabeçalho da página.
    restos = page.evaluate("""() => ({ cabecalho: document.querySelectorAll('.ferr-page > .page-head :is(a,button,summary)').length,
      fundo: document.querySelectorAll('.ferr-actions, .ferr-tech-actions').length })""")
    print(f'  botões no cabeçalho da página: {restos["cabecalho"]}; blocos de botões em baixo: {restos["fundo"]}')
    if restos['cabecalho'] or restos['fundo']:
        falhas.append(f'ficaram botões fora do canto dos cards: {restos}')

    # 2. O «Guardar» da técnica não abre nem fecha o card; o título sim.
    aberta = lambda: page.evaluate("document.querySelector('section[data-dmo-fica] > details').open")
    page.locator('#ferr-tech-title').click(position={'x': 5, 'y': 5})
    if aberta():
        falhas.append('clicar no título não fechou a Informação técnica')
    expect(page.locator('#ferr-tech-guardar')).to_be_visible()
    p =page.evaluate(POSICAO, list(BOTOES['Guardar (técnica)']))
    print(f'técnica fechada: Guardar {round(p["card"]["right"] - p["botao"]["right"], 1)} px da direita do card, '
          f'{round(p["botao"]["top"] - p["card"]["top"], 1)} px do topo; card com {round(p["card"]["h"], 1)} px de altura')

    def gravar(botao):
        page.evaluate("window.__marca=1;window.__trocou=0;document.addEventListener('dmo:troca',()=>{window.__trocou=1},{once:true})")
        pedidos.clear()
        page.locator(botao).click()
        page.wait_for_function('window.__trocou')
        page.wait_for_timeout(200)
        posts = [p for p in pedidos if p[0] == 'POST']
        docs = [p for p in pedidos if p[1] == 'document']
        estado = page.evaluate("""() => ({ marca: window.__marca, foco: document.activeElement && document.activeElement.id,
          aberta: document.querySelector('section[data-dmo-fica] > details').open,
          ok: [...document.querySelectorAll('.dmo-toast-pilha [data-dmo-toast="ok"]:not(.a-sair)')].map(e => e.textContent.replace('×', '').trim()) })""")
        return posts, docs, estado

    # Gravar com a técnica fechada: grava e o card continua fechado.
    posts, docs, estado = gravar('#ferr-tech-guardar')
    print(f'Guardar (técnica) com o card fechado: {len(posts)} POST, {len(docs)} documento, aberta={estado["aberta"]}, foco={estado["foco"]}, ok={estado["ok"]}')
    if len(posts) != 1 or docs or estado['marca'] != 1:
        falhas.append(f'Guardar (técnica) fechada: {len(posts)} POST, {len(docs)} documento, recarregou={estado["marca"] != 1}')
    if estado['aberta']:
        falhas.append('o Guardar abriu a Informação técnica')
    if estado['foco'] != 'ferr-tech-guardar':
        falhas.append(f'foco depois de gravar a técnica: {estado["foco"]}')
    if 'Informação técnica guardada.' not in estado['ok']:
        falhas.append('sem confirmação da técnica na pilha')
    medir_pilha(page, 'depois de gravar a técnica, página no topo')

    # Abrir pelo título; gravar com o card aberto: continua aberto.
    page.locator('#ferr-tech-title').click(position={'x': 5, 'y': 5})
    if not aberta():
        falhas.append('clicar no título não abriu a Informação técnica')
    page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
    page.wait_for_timeout(100)
    posts, docs, estado = gravar('#ferr-tech-guardar')
    print(f'Guardar (técnica) com o card aberto e a página rolada: {len(posts)} POST, {len(docs)} documento, aberta={estado["aberta"]}, foco={estado["foco"]}')
    if len(posts) != 1 or docs or estado['marca'] != 1 or not estado['aberta'] or estado['foco'] != 'ferr-tech-guardar':
        falhas.append(f'Guardar (técnica) aberta: {len(posts)} POST, {len(docs)} documento, estado {estado}')
    medir_pilha(page, 'depois de gravar a técnica, página rolada')

    # 3. Guardar do Editar.
    page.evaluate("window.scrollTo(0, 0)")
    page.wait_for_timeout(100)
    posts, docs, estado = gravar('#ferr-edit-guardar')
    print(f'Guardar (Editar): {len(posts)} POST, {len(docs)} documento, foco={estado["foco"]}, ok={estado["ok"]}')
    if len(posts) != 1 or docs or estado['marca'] != 1 or estado['foco'] != 'ferr-edit-guardar' or 'Dados gerais guardados.' not in estado['ok']:
        falhas.append(f'Guardar (Editar): {len(posts)} POST, {len(docs)} documento, estado {estado}')
    medir_pilha(page, 'depois de gravar o Editar, página no topo')
    confirmar_cantos(page, 'depois de gravar')

    # 4. A pilha com um aviso aberto (fica até ao «×»), página no topo e rolada.
    page.goto(ficha + '&tecnica=1&sem_lote=1&quantidade_ilegivel=abc&ilegivel_nominal_weight=abc')
    expect(page.locator('#ferr-tech-guardar')).to_be_visible()
    page.wait_for_timeout(200)
    medir_pilha(page, 'três avisos abertos, página no topo')
    page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
    page.wait_for_timeout(100)
    medir_pilha(page, 'três avisos abertos, página rolada até ao fim')

    assert not errors, errors
    context.close()
    browser.close()

if falhas:
    raise SystemExit('FALHAS:\n- ' + '\n- '.join(falhas))
print('PASS Ficha 1400px: os botões de cada card no canto superior direito, gravar continua a 1 POST sem recarregar')

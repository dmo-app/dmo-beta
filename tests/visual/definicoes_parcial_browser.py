"""E10 (Atualizar só o que muda): as Definições dos módulos e do Admin gravam sem recarregar.

Ambiente local descartável (scripts/stress/arrancar_app.sh <base> <porta>), nunca o DEV.
Em cada página alterada grava-se uma vez e confirma-se:
- 1 POST (também com um duplo clique) e nenhum pedido «document» (a página não recarrega:
  a marca em window sobrevive);
- os dados gravados aparecem;
- o que fica fora das zonas é o mesmo elemento, no mesmo sítio;
- o foco fica no sítio certo;
- a mensagem vai para a pilha do canto (não empurra os cartões).

Uso: DMO_UI_TEST_URL=http://127.0.0.1:5360 DMO_CHROMIUM=... python3 tests/visual/definicoes_parcial_browser.py
"""
import os
import random
import re
import subprocess
import time
from urllib.parse import urlparse

from playwright.sync_api import expect, sync_playwright

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5360')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')
base_dados = os.environ.get('DMO_UI_TEST_DB', 'dmo_e10')
operador = os.environ.get('STRESS_EMAIL', 'stress@local.test')
admin_email = os.environ.get('STRESS_ADMIN', 'admin@local.test')
password = os.environ.get('STRESS_PASSWORD', 'stress-1234')
resultados = []


def sql(consulta):
    return subprocess.run(['psql', '-h', 'localhost', '-U', 'postgres', '-d', base_dados, '-Atc', consulta],
                          env={**os.environ, 'PGPASSWORD': os.environ.get('PGPASSWORD', 'postgres')},
                          capture_output=True, text=True, check=True).stdout.strip()


def entrar(browser, quem):
    context = browser.new_context(viewport={'width': 1400, 'height': 900}, locale='pt-PT')
    page = context.new_page()
    erros = []
    page.on('pageerror', lambda error: erros.append(str(error)))
    pedidos = []
    page.on('request', lambda r: pedidos.append((r.method, r.resource_type, r.url))
            if r.url.startswith(base) and r.resource_type in ('document', 'fetch', 'xhr') else None)
    page.goto(base + '/login')
    page.locator('#identifier').fill(quem)
    page.locator('#password').fill(password)
    with page.expect_navigation():
        page.locator('#password').press('Enter')
    return page, pedidos, erros


def caixa(page, seletor):
    """O sítio do elemento na página (não no ecrã: o clique do Playwright pode deslizar até ao botão)."""
    return tuple(page.eval_on_selector(seletor, '''(e) => {
        const b = e.getBoundingClientRect();
        return [b.x + window.scrollX, b.y + window.scrollY, b.width, b.height].map(Math.round);
    }'''))


def gravar(page, pedidos, acao, fixos, nome, posts=1, gets=None, sem_troca=False, sitio=()):
    """Faz a ação e espera a resposta aplicada (dmo:troca, ou o JSON do valor no sítio).
    fixos: seletores fora das zonas que têm de ficar o mesmo elemento, no mesmo sítio.
    sitio: seletores dentro das zonas que são desenhados de novo mas não mudam de sítio."""
    page.evaluate('''(fixos) => {
        window.__marca = 1;
        window.__trocou = 0;
        window.__fixos = fixos.map((s) => document.querySelector(s));
        document.addEventListener('dmo:troca', () => { window.__trocou++; });
    }''', fixos)
    antes = [caixa(page, s) for s in (*fixos, *sitio)]
    del pedidos[:]
    acao()
    if not sem_troca:
        page.wait_for_function('window.__trocou >= 1', timeout=10000)
    page.wait_for_function("!document.querySelector('[data-dmo-troca][aria-busy=true]')", timeout=10000)
    page.wait_for_timeout(250)
    assert page.evaluate('window.__marca') == 1, f'{nome}: a página recarregou'
    metodos = [m for m, t, _ in pedidos]
    tipos = [t for _, t, _ in pedidos]
    assert metodos.count('POST') == posts, f'{nome}: {metodos.count("POST")} POST, esperado {posts}: {pedidos}'
    assert 'document' not in tipos, f'{nome}: houve navegação: {pedidos}'
    if gets is not None:
        assert metodos.count('GET') == gets, f'{nome}: {metodos.count("GET")} GET, esperado {gets}: {pedidos}'
    iguais = page.evaluate('(fixos) => fixos.map((s, i) => document.querySelector(s) === window.__fixos[i])', fixos)
    assert all(iguais), f'{nome}: um elemento fora das zonas foi desenhado de novo: {list(zip(fixos, iguais))}'
    depois = [caixa(page, s) for s in (*fixos, *sitio)]
    assert antes == depois, f'{nome}: o resto mexeu: {antes} -> {depois}'
    resultados.append(f'{nome}: {metodos.count("POST")} POST, {metodos.count("GET")} GET fetch, 0 document')


def foco(page):
    return page.evaluate('document.activeElement && (document.activeElement.id || document.activeElement.tagName)')


def no_canto(page, botao, cartao, campo):
    """Regra do Diogo (18/10/2026): o botão do cartão fica no canto superior direito, dentro
    do cartão, na linha do título; por cima dos campos; tamanho só da letra e do padding."""
    m = page.evaluate('''([b, c, f]) => {
        const r = (s) => document.querySelector(s).getBoundingClientRect();
        const botao = r(b), cartao = r(c), campo = r(f), titulo = document.querySelector(c).querySelector('h3').getBoundingClientRect();
        const s = getComputedStyle(document.querySelector(b));
        // A barra das ações do canto (com o «Opções» ao lado, nas Templates) encosta à direita.
        const acoes = document.querySelector(b).closest('.dmo-card-topo-acoes').getBoundingClientRect();
        return { direita: cartao.right - acoes.right, topo: botao.top - cartao.top, dentro: botao.left > cartao.left && botao.bottom < cartao.bottom,
                 titulo: Math.abs((botao.top + botao.bottom) / 2 - (titulo.top + titulo.bottom) / 2), acima: botao.bottom <= campo.top,
                 largura: document.querySelector(b).style.width || '', altura: document.querySelector(b).style.height || '' };
    }''', [botao, cartao, campo])
    assert m['dentro'] and 0 <= m['direita'] <= 40 and 0 <= m['topo'] <= 40 and m['titulo'] <= 12 and m['acima'] \
        and not m['largura'] and not m['altura'], f'{botao} não está no canto superior direito de {cartao}: {m}'
    resultados.append(f'{botao}: no canto superior direito do cartão ({m["direita"]:.0f}px da direita, {m["topo"]:.0f}px do topo)')


def toast(page, texto):
    mensagem = page.locator('.dmo-toast-pilha [data-dmo-toast]', has_text=texto)
    expect(mensagem).to_have_count(1)
    return mensagem


# A ajuda de um título medida no Chromium: o topo do círculo em relação à altura das
# maiúsculas da fonte do título (um span de medição com «H» no próprio título) e a distância
# horizontal, em em, ao que vem antes (o texto ou a etiqueta «Só de leitura»).
MEDIR_AJUDA = """(h) => {
    const marca = h.querySelector(':scope > .dmo-help .dmo-help-mark, :scope > .dmo-com-ajuda > .dmo-help .dmo-help-mark');
    const medida = document.createElement('span');
    medida.textContent = 'H';
    medida.style.cssText = 'visibility:hidden;display:inline-block;width:0;white-space:nowrap';
    const base = document.createElement('span');
    base.style.cssText = 'display:inline-block;width:0;height:0;vertical-align:baseline';
    medida.appendChild(base);
    h.insertBefore(medida, h.firstChild);
    const ctx = document.createElement('canvas').getContext('2d');
    ctx.font = getComputedStyle(medida).font;
    const topo = base.getBoundingClientRect().bottom - ctx.measureText('H').actualBoundingBoxAscent;
    const topoMarca = marca.getBoundingClientRect().top;
    medida.remove();
    const ajuda = marca.closest('.dmo-help');
    // O que vem antes da ajuda: o texto do título ou o elemento anterior (a etiqueta do cadeado).
    let antes = ajuda.previousSibling;
    while (antes && antes.nodeType === 3 && !antes.textContent.trim()) antes = antes.previousSibling;
    let direita;
    if (antes.nodeType === 3) { const r = document.createRange(); r.selectNodeContents(antes); direita = r.getBoundingClientRect().right; }
    else direita = antes.getBoundingClientRect().right;
    const em = parseFloat(getComputedStyle(h).fontSize);
    return { topo: topoMarca - topo, folga: (marca.getBoundingClientRect().left - direita) / em };
}"""


def ajuda_no_sitio(page, seletor, folga_esperada=None):
    m = page.eval_on_selector(seletor, MEDIR_AJUDA)
    assert abs(m['topo']) <= 1.5, f'{seletor}: topo da ajuda a {m["topo"]:.1f}px da altura das maiúsculas'
    if folga_esperada is not None:
        assert abs(m['folga'] - folga_esperada) <= 0.03, f'{seletor}: ajuda a {m["folga"]:.2f}em (os outros títulos: {folga_esperada:.2f}em)'
    return m


with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'),
                                         args=['--no-sandbox'])

    # ------------------------------------------------------------------ módulos
    page, pedidos, erros = entrar(browser, operador)

    # A ajuda do «GERAL · Só de leitura» (sessão nova: as Definições Gerais estão fechadas)
    # fica encostada e elevada como nos outros títulos (Diogo, 23/10/2026).
    page.goto(base + '/Definicoes?secao=controlo&sub=pegamentos')
    folga_cadeado = ajuda_no_sitio(page, '#sec-controlo h3.def-parte-titulo:has(.def-cadeado)')
    resultados.append(f'Ajuda do cadeado: topo a {folga_cadeado["topo"]:.1f}px das maiúsculas, a {folga_cadeado["folga"]:.2f}em da etiqueta')

    # Controlo → Definições: a tolerância; duplo clique no «Guardar» = 1 POST, 1 mensagem.
    page.goto(base + '/Controlo/Definicoes')
    atual = page.input_value('#pegamentosTolerance')
    novo = '0.25' if atual != '0.25' else '0.30'
    page.fill('#pegamentosTolerance', novo)
    gravar(page, pedidos, lambda: page.dblclick('#ctlDefGuardar'),
           ['#ctl-def-title', '[data-dmo-pasta-pdf]'], 'Controlo, tolerância (duplo clique)')
    assert page.input_value('#pegamentosTolerance') == novo
    toast(page, 'Tolerância guardada')
    assert foco(page) == 'ctlDefGuardar', f'foco em {foco(page)}'
    assert float(sql('''select "ToleranceMm" from "PegamentosTolerances"''')) == float(novo), 'a tolerância não ficou na base'

    # Ferramentas → Definições: Enter num total; o foco fica no campo.
    page.goto(base + '/Ferramentas/Definicoes')
    campo = '#ferr-total-BQ-B1'
    novo = str(100 + random.randint(1, 80))
    page.fill(campo, novo)
    gravar(page, pedidos, lambda: page.press(campo, 'Enter'), ['#ferr-def-title'], 'Ferramentas, totais (Enter)')
    assert page.input_value(campo) == novo
    toast(page, 'Totais guardados.')
    assert foco(page) == 'ferr-total-BQ-B1', f'foco em {foco(page)}'
    # Uma quantidade má: o aviso vai para o canto, o quadro não se mexe.
    page.fill(campo, 'xx')
    gravar(page, pedidos, lambda: page.click('#ferrDefGuardar'), ['#ferr-def-title'], 'Ferramentas, aviso',
           sitio=['.ferr-totais', '#ferrDefGuardar'])
    toast(page, 'exceto: BQ na B1')
    assert page.input_value(campo) == novo
    assert foco(page) == 'ferrDefGuardar'

    # Boquilhas → Definições: criar um reparador, escolher a rota, mudar o nome no sítio.
    page.goto(base + '/Boquilhas/Definicoes')
    nome = f'Reparador E10 {int(time.time())}'
    page.fill('#repairerName', nome)
    gravar(page, pedidos, lambda: page.press('#repairerName', 'Enter'), ['#bq-def-title'], 'Boquilhas, criar reparador (Enter)')
    toast(page, 'Definição gravada.')
    novo_id = sql(f'''select "Id" from "BqRepairers" where "Name" = '{nome}' ''')
    assert novo_id, 'o reparador não ficou na base'
    expect(page.locator(f'#bq-reparador-{novo_id}')).to_have_value(nome)
    assert page.input_value('#repairerName') == ''
    assert foco(page) == 'repairerName', f'foco em {foco(page)}'
    assert page.locator(f'#machine-route-B1 option[value="B1|{novo_id}"]').count() == 1, 'o reparador novo não está nas rotas'

    # O campo do reparador também filtra (Diogo, 21/10/2026: «nome reparador
    # pode servir como filtro, caso não exista o operador cria»): com dois
    # reparadores, parte de um nome deixa só esse; um nome novo cria-o.
    outro = f'Oficina Filtro {int(time.time())}'
    page.fill('#repairerName', outro)
    gravar(page, pedidos, lambda: page.press('#repairerName', 'Enter'), ['#bq-def-title'], 'Boquilhas, criar o segundo reparador')
    outro_id = sql(f'''select "Id" from "BqRepairers" where "Name" = '{outro}' ''')
    assert outro_id, 'o segundo reparador não ficou na base'
    expect(page.locator(f'tr[data-dmo-ficha="bqDefFicha-{outro_id}"]')).to_be_visible()
    page.fill('#repairerName', outro.lower().replace('oficina', 'ofícina'))
    expect(page.locator(f'tr[data-dmo-ficha="bqDefFicha-{outro_id}"]')).to_be_visible()
    expect(page.locator(f'tr[data-dmo-ficha="bqDefFicha-{novo_id}"]')).to_be_hidden()
    assert page.locator('#bqDefTabelaReparadores tbody tr:visible').count() == 1, 'o filtro deixa só um reparador'
    page.fill('#repairerName', '')
    page.dispatch_event('#repairerName', 'input')
    expect(page.locator(f'tr[data-dmo-ficha="bqDefFicha-{novo_id}"]')).to_be_visible()

    # O operador abre o dropdown (o foco vai para ele) e escolhe.
    page.focus('#machine-route-B1')
    gravar(page, pedidos, lambda: page.select_option('#machine-route-B1', f'B1|{novo_id}'),
           ['#bq-def-title', '#bqDefReparadores'], 'Boquilhas, rota da B1')
    assert page.input_value('#machine-route-B1') == f'B1|{novo_id}'
    assert foco(page) == 'machine-route-B1', f'foco em {foco(page)}'
    assert sql('''select "SettingValue" from "ModuleSettings" where "SettingKey" = 'Boquilhas:RepairerB1' ''') == novo_id

    # O nome muda na ficha do reparador (um clique na linha; Diogo,
    # 21/10/2026) e grava no sítio (JSON), sem recarregar.
    page.click(f'tr[data-dmo-ficha="bqDefFicha-{novo_id}"]')
    campo = f'#bq-reparador-{novo_id}'
    page.fill(campo, nome + ' x')
    with page.expect_response(lambda r: 'handler=EditRepairer' in r.url):
        gravar(page, pedidos, lambda: page.press(campo, 'Tab'), ['#bq-def-title', '#bqDefReparadores', '#bqDefRotas'],
               'Boquilhas, nome no sítio', sem_troca=True)
    assert sql(f'''select "Name" from "BqRepairers" where "Id" = {novo_id}''') == nome + ' x'
    expect(page.locator(f'td[data-dmo-espelho="bq-reparador-{novo_id}"]')).to_have_text(nome + ' x')
    assert not erros, erros
    page.context.close()

    # -------------------------------------------------------------------- Admin
    page, pedidos, erros = entrar(browser, admin_email)

    # Definições → Admin → App: o email do ADMIN (o mesmo, para a sessão continuar).
    page.goto(base + '/Definicoes?secao=admin&sub=app')
    no_canto(page, '#admEmailGuardar', '#admEmail', '#adminEmail')
    page.fill('#adminEmail', admin_email)
    page.fill('#adminEmailConfirm', admin_email)
    gravar(page, pedidos, lambda: page.click('#admEmailGuardar'), ['#def-title'], 'App, email do ADMIN')
    toast(page, 'Email do ADMIN guardado.')
    assert page.input_value('#adminEmail') == admin_email
    assert page.input_value('#adminEmailConfirm') == ''
    assert foco(page) == 'admEmailGuardar', f'foco em {foco(page)}'

    # Canal Jobon: a hora grava ao escolher; o foco fica no dropdown.
    page.goto(base + '/Definicoes?secao=admin&sub=canal')
    hora = str((int(page.input_value('#dayChangeHour')) + 1) % 24)
    page.focus('#dayChangeHour')
    gravar(page, pedidos, lambda: page.select_option('#dayChangeHour', hora), ['#def-title'], 'Canal Jobon, hora')
    toast(page, 'Hora de viragem gravada.')
    assert page.input_value('#dayChangeHour') == hora
    assert foco(page) == 'dayChangeHour', f'foco em {foco(page)}'
    assert sql('''select "SettingValue" from "ModuleSettings" where "SettingKey" = 'Boquilhas:DayChangeHour' ''') == hora

    # Menu lateral: um clique em «Controlo» abre o grupo, outro fecha-o; o triângulo roda.
    page.goto(base + '/Definicoes?secao=admin&sub=canal')
    titulo = '[data-dmo-def-grupo="controlo"] [data-dmo-def-grupo-titulo]'
    sub = '#defMenu-controlo'
    rodar = f'{titulo} .dmo-caret'
    angulo = lambda: page.eval_on_selector(rodar, "e => getComputedStyle(e).transform")
    assert page.get_attribute(titulo, 'aria-expanded') == 'false'
    assert page.eval_on_selector(sub, 'e => e.hidden') is True
    fechado = angulo()
    page.click(titulo)
    expect(page.locator(titulo)).to_have_attribute('aria-expanded', 'true')
    assert page.eval_on_selector(sub, 'e => e.hidden') is False
    page.wait_for_timeout(300)
    aberto = angulo()
    assert aberto != fechado and aberto not in ('none', ''), f'o triângulo não rodou ({fechado} -> {aberto})'
    page.click(titulo)
    expect(page.locator(titulo)).to_have_attribute('aria-expanded', 'false')
    assert page.eval_on_selector(sub, 'e => e.hidden') is True
    page.wait_for_timeout(300)
    assert angulo() == fechado, 'o triângulo não voltou'
    resultados.append('Menu lateral: «Controlo» abre e fecha com um clique; o triângulo roda')

    # Triângulo e ajuda medidos no Chromium: o triângulo acaba antes do texto (0 a 0,5em);
    # o topo do círculo da ajuda fica na altura das maiúsculas (±1,5 px).
    medir_caret = """(s) => {
        const c = s.querySelector('.dmo-caret');
        const t = document.createTreeWalker(s, NodeFilter.SHOW_TEXT, { acceptNode: n => n.textContent.trim() ? 1 : 3 });
        let n = t.nextNode();
        while (n && !(c.compareDocumentPosition(n) & Node.DOCUMENT_POSITION_FOLLOWING)) n = t.nextNode();
        const r = document.createRange(); r.selectNodeContents(n);
        const em = parseFloat(getComputedStyle(n.parentElement).fontSize);
        return { folga: r.getBoundingClientRect().left - c.getBoundingClientRect().right, em };
    }"""
    page.goto(base + '/Definicoes?secao=boquilhas')
    m = page.eval_on_selector('summary:has(> .dmo-caret)', medir_caret)
    assert 0 <= m['folga'] <= 0.5 * m['em'], f'triângulo do <summary> a {m["folga"]:.1f}px do texto (1em = {m["em"]}px)'
    m2 = page.eval_on_selector(titulo, medir_caret)
    assert 0 <= m2['folga'] <= 0.5 * m2['em'], f'triângulo do menu a {m2["folga"]:.1f}px do texto'
    page.goto(base + '/Definicoes?secao=admin&sub=app')
    ajuda = ajuda_no_sitio(page, '#admin-email-title')
    desvio = ajuda['topo']
    assert abs(ajuda['folga'] - folga_cadeado['folga']) <= 0.03, f'ajuda do cadeado a {folga_cadeado["folga"]:.2f}em, nos títulos a {ajuda["folga"]:.2f}em'
    resultados.append(f'Triângulo a {m["folga"]:.1f}px/{m2["folga"]:.1f}px do texto; topo da ajuda a {desvio:.1f}px da altura das maiúsculas')

    # Utilizadores: criar; o PIN fica no canto; o formulário limpa; a lista mostra-o.
    page.goto(base + '/Admin/Users')
    no_canto(page, '#dmuCriarBotao', '#dmuCriar', '#dmu-name')
    while True:
        op = str(random.randint(2000, 9999))
        if sql(f'''select count(*) from "Users" where "OperatorId" = '{op}' ''') == '0':
            break
    contagem = page.inner_text('#dmu-resultados .card-note')
    page.fill('#dmu-name', f'Utilizador E10 {op}')
    page.fill('#dmu-operator', op)
    page.fill('#dmu-email', f'e10.{op}.{int(time.time())}@local.test')
    gravar(page, pedidos, lambda: page.dblclick('#dmuCriarBotao'), ['#dmu-title'], 'Utilizadores, criar (duplo clique)')
    entrega = page.locator('.dmo-toast-pilha section[aria-label="Utilizador criado"]')
    expect(entrega).to_have_count(1)
    assert re.search(r'\d{4}', entrega.inner_text()), 'sem PIN na entrega'
    assert page.inner_text('#dmu-resultados .card-note') != contagem, 'a lista não mudou'
    assert page.input_value('#dmu-name') == '' and page.input_value('#dmu-operator') == ''
    assert foco(page) == 'dmuCriarBotao', f'foco em {foco(page)}'
    assert sql(f'''select count(*) from "Users" where "OperatorId" = '{op}' ''') == '1', 'o utilizador não ficou uma vez na base'
    # A entrega está fora do fluxo: o formulário não desceu.
    assert page.evaluate("getComputedStyle(document.querySelector('.dmo-toast-pilha')).position") == 'fixed'

    # Templates: criar (Enter), abrir outra na lista, guardar, eliminar.
    page.goto(base + '/Admin/Templates')
    no_canto(page, '#dmtCriar', '#dmtDetalhe', '#dmt-new-name')
    nome = f'E10 {int(time.time())}'
    page.fill('#dmt-new-name', nome)
    gravar(page, pedidos, lambda: page.press('#dmt-new-name', 'Enter'), ['#dmt-title'], 'Templates, criar (Enter)')
    toast(page, 'Template criada.')
    expect(page.locator('.dmt-choice.active')).to_contain_text(nome)
    assert page.input_value('#dmt-edit-name') == nome
    assert foco(page) == 'dmt-edit-name', f'foco em {foco(page)}'
    no_canto(page, '#dmtGuardar', '#dmtDetalhe', '#dmt-edit-name')
    chave = re.search(r'template=([^&#]+)', page.url).group(1)

    outra = page.locator('.dmt-choice-link').filter(has_not_text=nome).first
    outra_id = outra.get_attribute('id')
    gravar(page, pedidos, lambda: outra.click(), ['#dmt-title'], 'Templates, abrir outra na lista', posts=0, gets=1)
    assert foco(page) == outra_id, f'foco em {foco(page)}'
    expect(page.locator(f'#{outra_id}').locator('xpath=..')).to_have_class(re.compile('active'))

    page.click(f'#dmt-escolha-{chave}')
    page.wait_for_function(f"document.querySelector('#dmt-edit-name') && document.querySelector('#dmt-edit-name').value === '{nome}'")
    page.fill('#dmt-edit-name', nome + ' b')
    gravar(page, pedidos, lambda: page.click('#dmtGuardar'), ['#dmt-title'], 'Templates, guardar')
    toast(page, 'Template guardada.')
    expect(page.locator(f'#dmt-escolha-{chave}')).to_contain_text(nome + ' b')
    assert foco(page) == 'dmtGuardar', f'foco em {foco(page)}'

    page.click('summary:has-text("Opções")')
    page.click('button.dmo-options-item:has-text("Eliminar template")')
    gravar(page, pedidos, lambda: page.click('#dmt-delete-dialog button[type=submit]'), ['#dmt-title'], 'Templates, eliminar')
    toast(page, 'Template eliminada.')
    assert page.locator(f'#dmt-escolha-{chave}').count() == 0
    assert foco(page) == 'dmt-new-name', f'foco em {foco(page)}'
    assert sql(f'''select count(*) from "Templates" where "TemplateKey" = '{chave}' ''') == '0'
    assert not erros, erros
    page.context.close()
    browser.close()

for linha in resultados:
    print('PASS', linha)
print(f'PASS Definições E10: {sum(1 for r in resultados if " POST, " in r)} gravações e cliques sem recarregar')

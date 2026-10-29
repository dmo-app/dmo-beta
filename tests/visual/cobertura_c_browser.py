"""O aspeto que a auditoria de cobertura (C) deixou sem prova, medido no Chromium (outubro de 2026).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~CoberturaCExportaParaOBrowserTests|FullyQualifiedName~Registar_DaUmaCaixaPorLinha|FullyQualifiedName~RevisaoArrumada'
  python tests/visual/cobertura_c_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV. Mede o efeito no ecrã (posição, cor real,
sublinhado, altura), nunca o texto do CSS.

- DMO-436, DMO-470: o login. O botão «Reparação» fica à vista no canto do quadro; o logo
  do ecrã carrega; a coluna da esquerda tem degradê; o «Entrar» tem o azul da paleta nova
  (#515F7A), em claro e em escuro.
- DMO-436, DMO-414: o topo. Os separadores têm 52 px; o utilizador é «Nome · Título» numa
  só linha e o «Sair» fica por baixo, com letra pequena.
- DMO-456, DMO-480, DMO-481: a 1366×768 as Definições, a ficha do utilizador e a Reparação
  Interna (Registar) não têm scroll (nem lateral nem vertical), em claro e em escuro; a
  Consulta só não tem scroll lateral.
- DMO-480: o «← Voltar» da ficha fica por cima do título.
- DMO-481: os links da ficha da CM/MF, da produção e da ferramenta não têm sublinhado
  (nem com o rato por cima).
- DMO-405: as linhas compatíveis (B1, B2…) mudam de cor com o rato por cima e, escolhidas,
  ficam invertidas (fundo claro e letra azul), também com o rato por cima.
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
TEMAS = ('light', 'dark')
AZUL_DA_PALETA = (0x51, 0x5f, 0x7a)
BRANCO = (255, 255, 255)

# A cor real de uma expressão CSS, como RGB (resolve color-mix e variáveis).
COR = '''(css) => { const x = document.createElement('canvas'); x.width = x.height = 1;
    const g = x.getContext('2d'); g.fillStyle = '#000'; g.fillStyle = css; g.fillRect(0, 0, 1, 1);
    return [...g.getImageData(0, 0, 1, 1).data].slice(0, 3); }'''
ESTILO = '''([sel, pseudo]) => { const e = document.querySelector(sel); if (!e) return null;
    const c = getComputedStyle(e, pseudo || null); const r = e.getBoundingClientRect();
    return { fundo: c.backgroundColor, letra: c.color, contorno: c.borderTopColor, imagem: c.backgroundImage,
             sublinhado: c.textDecorationLine, tamanho: parseFloat(c.fontSize), peso: c.fontWeight, conteudo: c.content,
             margens: [c.marginTop, c.marginRight, c.marginBottom, c.marginLeft], largura: parseFloat(c.width), altura: parseFloat(c.height),
             caixa: { left: r.left, top: r.top, right: r.right, bottom: r.bottom, width: r.width, height: r.height } }; }'''
# A app esconde o que passa do ecrã (overflow: hidden no .app e na página): o que não cabe vê-se no
# scrollHeight do documento, do body, do .app e da página (main), ou num cartão que acaba por baixo do ecrã
# (fora das caixas que deslizam de propósito, como as tabelas).
SCROLL = '''() => {
    const desliza = (e) => { for (let p = e.parentElement; p && p !== document.body; p = p.parentElement) {
        const o = getComputedStyle(p).overflowY;
        if ((o === 'auto' || o === 'scroll') && p.scrollHeight > p.clientHeight) return true; } return false; };
    const cartoes = [...document.querySelectorAll('.app > main, .app > main .dmo-card')].filter(e => {
        const r = e.getBoundingClientRect(); const c = getComputedStyle(e);
        return r.width > 0 && r.height > 0 && c.visibility !== 'hidden' && c.position !== 'fixed' && r.bottom > innerHeight + 1 && !desliza(e); });
    return { lateral: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        vertical: Math.max(...[document.documentElement, document.body, document.querySelector('.app'), document.querySelector('.app > main')]
            .filter(Boolean).map(e => e.scrollHeight - e.clientHeight)),
        cartoesPorBaixo: cartoes.map(e => e.tagName.toLowerCase() + (e.id ? '#' + e.id : '') + ' acaba a ' + Math.round(e.getBoundingClientRect().bottom)) }; }'''

falhas = []


def luminancia(rgb):
    r, g, b = [v / 255 for v in rgb]
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def rgb(page, css):
    return tuple(page.evaluate(COR, css))


def estilo(page, seletor, pseudo=None):
    medido = page.evaluate(ESTILO, [seletor, pseudo])
    assert medido is not None, f'a página não tem {seletor}'
    for chave in ('fundo', 'letra', 'contorno'):
        medido[chave] = rgb(page, medido[chave])
    return medido


def falhar(onde, texto):
    falhas.append(f'{onde}: {texto}')


def abrir(browser, ficheiro, caminho, largura, altura, tema):
    ficheiro_html = fixtures / ficheiro
    assert ficheiro_html.is_file(), f'falta {ficheiro} (corra os testes Web com DMO_UI_EVIDENCE_DIR)'
    context = browser.new_context(viewport={'width': largura, 'height': altura}, locale='pt-PT')
    page = context.new_page()
    erros = []
    page.on('pageerror', lambda erro: erros.append(str(erro)))
    html = sem_selo_local(ficheiro_html.read_text(encoding='utf-8'))

    def servir(route):
        url = urlparse(route.request.url)
        if route.request.method == 'GET' and url.path == caminho:
            route.fulfill(content_type='text/html', body=html)
            return
        ficheiro_estatico = (assets / unquote(url.path).lstrip('/')).resolve()
        if ficheiro_estatico.is_relative_to(assets) and ficheiro_estatico.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(ficheiro_estatico))[0] or 'application/octet-stream',
                          body=ficheiro_estatico.read_bytes())
        else:
            route.fulfill(status=404, body='')

    context.route('**/*', servir)
    page.goto('http://dmo.test' + caminho)
    page.evaluate(f"document.documentElement.dataset.theme = '{tema}'")
    page.evaluate('document.fonts.ready')
    return context, page, erros


def fecha(onde, context, erros):
    if erros:
        falhar(onde, f'erros de JavaScript: {erros}')
    context.close()


def login(browser):
    for largura, altura in ((1290, 646), (1366, 768)):
        for tema in TEMAS:
            onde = f'login {largura}x{altura} {tema}'
            context, page, erros = abrir(browser, 'cobertura-c-login.html', '/login', largura, altura, tema)
            if page.evaluate(SCROLL)['lateral'] > 0:
                falhar(onde, 'o login tem scroll lateral')

            # DMO-436: «Reparação» à vista, no canto inferior direito do quadro, depois do «Entrar».
            posto = estilo(page, '#login-posto')
            entrar = estilo(page, '[data-login-submit]')
            quadro = estilo(page, '.login-shell')['caixa']
            caixa = posto['caixa']
            if caixa['width'] <= 0 or caixa['left'] < 0 or caixa['right'] > largura + 0.5:
                falhar(onde, f'o botão «Reparação» não cabe no ecrã: {caixa}')
            if quadro['right'] - caixa['right'] > 80:
                falhar(onde, f'o botão «Reparação» devia estar encostado à direita do quadro: {quadro["right"] - caixa["right"]:.0f}px de folga')
            if caixa['top'] < entrar['caixa']['top']:
                falhar(onde, 'o botão «Reparação» devia ficar à altura do «Entrar» ou por baixo dele')
            if caixa['bottom'] > quadro['bottom'] + 0.5:
                falhar(onde, 'o botão «Reparação» saiu do quadro por baixo')

            # DMO-470: logo do ecrã, degradê e azul da paleta nova.
            logo = page.evaluate('''() => { const i = document.querySelector('img.brand-logo');
                return { src: i.getAttribute('src'), carregou: i.complete && i.naturalWidth > 0, largura: i.getBoundingClientRect().width }; }''')
            if '0_ASSET_LOGO_ECRA.png' not in logo['src'] or not logo['carregou'] or logo['largura'] < 60:
                falhar(onde, f'o logo do ecrã não aparece: {logo}')
            if not estilo(page, '.identity')['imagem'].startswith('linear-gradient'):
                falhar(onde, 'a coluna da esquerda do login perdeu o degradê')
            if entrar['fundo'] != AZUL_DA_PALETA or entrar['letra'] != BRANCO:
                falhar(onde, f'o «Entrar» devia ser #515F7A com letra branca: fundo {entrar["fundo"]}, letra {entrar["letra"]}')

            fecha(onde, context, erros)


def topo_e_ecras(browser):
    # DMO-436, DMO-414, DMO-456, DMO-480: o topo e o encaixe a 1366×768.
    paginas = (('cobertura-c-definicoes.html', '/Definicoes', 'Definições', True),
               ('cobertura-c-ficha.html', '/Admin/Users/Ficha', 'Ficha do utilizador', True),
               ('cobertura-c-ri.html', '/ReparacaoInterna', 'Reparação Interna', True),
               ('cobertura-c-consultar.html', '/ReparacaoInterna/Consultar', 'Consulta da Reparação', False))
    for ficheiro, caminho, nome, cabe in paginas:
        for tema in TEMAS:
            onde = f'{nome} 1366x768 {tema}'
            context, page, erros = abrir(browser, ficheiro, caminho, 1366, 768, tema)
            scroll = page.evaluate(SCROLL)
            if scroll['lateral'] > 0:
                falhar(onde, f'tem scroll lateral ({scroll["lateral"]}px)')
            if cabe and scroll['vertical'] > 1:
                falhar(onde, f'não cabe em 1366×768: passa {scroll["vertical"]}px na vertical')
            if cabe and scroll['cartoesPorBaixo']:
                falhar(onde, f'não cabe em 1366×768: ficam por baixo do ecrã {scroll["cartoesPorBaixo"]}')

            alturas = page.evaluate('''() => [...document.querySelectorAll('.dmo-shell-header .dmo-primary-nav > a')]
                .filter(a => a.getBoundingClientRect().width > 0).map(a => Math.round(a.getBoundingClientRect().height))''')
            if not alturas or any(h != 52 for h in alturas):
                falhar(onde, f'os separadores do topo deviam ter 52 px: {alturas}')

            # DMO-414: «Nome · Título» numa só linha e o «Sair» por baixo, em letra pequena.
            nome_el = estilo(page, '[data-user-profile-name]')
            sair = estilo(page, '.dmo-shell-logout')
            if nome_el['tamanho'] > 14 or sair['tamanho'] > 14:
                falhar(onde, f'a sessão do topo devia ter letra pequena: nome {nome_el["tamanho"]}px, sair {sair["tamanho"]}px')
            if sair['caixa']['top'] < nome_el['caixa']['bottom'] - 2:
                falhar(onde, 'o «Sair» devia ficar por baixo do nome')
            if page.locator('[data-user-profile-title]').count():
                titulo = estilo(page, '[data-user-profile-title]')
                if abs((titulo['caixa']['top'] + titulo['caixa']['bottom']) / 2 - (nome_el['caixa']['top'] + nome_el['caixa']['bottom']) / 2) > 4:
                    falhar(onde, 'o título devia estar na mesma linha do nome')
                separador = estilo(page, '[data-user-profile-title]', '::before')['conteudo']
                if '·' not in separador:
                    falhar(onde, f'faltava o «·» entre o nome e o título: {separador}')
            cabecalho = estilo(page, '.dmo-shell-header')['caixa']
            # A linha de cima (logo, separadores e conta) cabe em ~56 px; a barra dos sub-separadores
            # (Registar, Consultar…), quando a página a tem, é outra linha por baixo.
            sub = page.evaluate("() => { const e = document.querySelector('.dmo-shell-header .dmo-secondary-nav'); return e ? e.getBoundingClientRect().height : 0; }")
            if cabecalho['height'] - sub > 60:
                falhar(onde, f'a linha de cima do cabeçalho ficou alta demais: {cabecalho["height"] - sub:.0f}px')

            if ficheiro == 'cobertura-c-ficha.html':
                # DMO-480: «← Voltar» por cima do título.
                voltar = estilo(page, '#dfuVoltar')
                titulo = estilo(page, '#dfu-title')
                if not voltar['caixa']['bottom'] <= titulo['caixa']['top'] + 2:
                    falhar(onde, 'o «← Voltar» devia estar por cima do título')
                if voltar['sublinhado'] != 'none':
                    falhar(onde, 'o «← Voltar» não devia ter sublinhado')
            if ficheiro in ('cobertura-c-ri.html', 'cobertura-c-consultar.html'):
                # DMO-481: links sem sublinhado, também com o rato por cima.
                seletor = '.ri-ficha' if ficheiro == 'cobertura-c-ri.html' else '.ri-card .dmo-table td a'
                n = page.locator(seletor).count()
                if n < 1:
                    falhar(onde, f'a página não tem links {seletor}')
                for i in range(n):
                    link = page.locator(seletor).nth(i)
                    link.scroll_into_view_if_needed()
                    antes = link.evaluate('e => getComputedStyle(e).textDecorationLine')
                    link.hover()
                    page.wait_for_timeout(150)
                    com_rato = link.evaluate('e => getComputedStyle(e).textDecorationLine')
                    if antes != 'none' or com_rato != 'none':
                        falhar(onde, f'o link {i + 1} de {seletor} tem sublinhado ({antes} / com o rato {com_rato})')
            fecha(onde, context, erros)


def linhas_compativeis(browser):
    # DMO-405: B1, B2… mudam de cor com o rato por cima e, escolhidas, ficam invertidas (também com o rato por cima).
    for tema in TEMAS:
        onde = f'linhas compatíveis 1366x768 {tema}'
        context, page, erros = abrir(browser, 'cobertura-c-ferramentas.html', '/Ferramentas', 1366, 768, tema)
        caixas = page.locator('#ferr-create-form .ferr-checks > .ferr-check')
        if caixas.count() < 2:
            falhar(onde, 'a página não tem as linhas compatíveis')
            fecha(onde, context, erros)
            continue
        caixa = caixas.nth(0)
        botao = '#ferr-create-form .ferr-checks > .ferr-check:first-child > span'
        cores = lambda m: (m['fundo'], m['letra'])
        page.mouse.move(5, 5)
        em_repouso = estilo(page, botao)
        if not luminancia(em_repouso['fundo']) < luminancia(em_repouso['letra']):
            falhar(onde, f'em repouso a linha devia ser azul com letra clara: {em_repouso}')
        caixa.hover()
        page.wait_for_timeout(250)
        if cores(estilo(page, botao)) == cores(em_repouso):
            falhar(onde, f'o rato por cima não muda as cores da linha: {em_repouso}')
        caixa.click()
        if not page.locator('#ferr-create-form .ferr-checks > .ferr-check:first-child input').is_checked():
            falhar(onde, 'o clique não escolheu a linha')
        page.wait_for_timeout(250)
        com_rato = estilo(page, botao)
        page.mouse.move(5, 5)
        page.wait_for_timeout(250)
        escolhida = estilo(page, botao)
        if cores(escolhida) == cores(em_repouso):
            falhar(onde, f'a linha escolhida tem as mesmas cores da que não está escolhida: {escolhida}')
        if cores(com_rato) != cores(escolhida):
            falhar(onde, f'a linha escolhida perde a inversão com o rato por cima: {com_rato} / {escolhida}')
        if tema == 'light' and not luminancia(escolhida['fundo']) > luminancia(escolhida['letra']):
            falhar(onde, f'no claro a linha escolhida devia ter fundo claro e letra azul: {escolhida}')
        fecha(onde, context, erros)


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    login(browser)
    topo_e_ecras(browser)
    linhas_compativeis(browser)
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Cobertura C no browser: ok')

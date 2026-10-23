"""As falhas de ecrã do teste do agente de 19/10/2026, no Chromium.

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~ParaOBrowser|FullyQualifiedName~OCampoDoLoteVaiParaOBrowser'
  python tests/visual/nok_agente_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

- T1f15887/Tcb734f2: numa BQ posta a zero, a dica «Metida a zero por…» da
  Discrepância fica fechada até ao rato, abre dentro do ecrã, e a página não
  tem scroll lateral (1400 e 390 px).
- T597a9e6: «abc» no Lote do PU NNPB e «Guardar identificação» envia o
  formulário (a recusa é da app, no canto), sem a bolha do browser.
- T20123bb: no Resumo sem Peso, a 1400 px, «Associar Peso existente» e
  «Criar Peso» ficam na linha do título do cartão Peso.
- Diogo, 19/10/2026: na Revisão do Controlo, «Limpar filtros», os outros
  botões e o estado do controlo aberto ficam à direita: a 1920 px na linha dos
  campos e centrados com eles; a 1400 px, onde não cabem, numa linha por
  baixo encostada à direita.
- Diogo, 20/10/2026 («já pedi para arranjar o filtro do Planeamento 5
  vezes»): no Job On, escrever «60» na referência mostra por baixo as
  referências que começam assim e o dropdown espera; escolher uma enche-o com
  as produções dela, sem repetir a referência.
- Diogo, 20/10/2026: o texto dos campos, dos selects e dos botões da Revisão
  ao centro da caixa (no máximo 0,5 px de desvio).
"""
import base64
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
posts = []


def abrir(browser, nome, caminho, largura, respostas=None, escala=1):
    context = browser.new_context(viewport={'width': largura, 'height': 900}, device_scale_factor=escala)
    page = context.new_page()
    erros = []
    page.on('pageerror', lambda erro: erros.append(str(erro)))

    def serve(route):
        url = urlparse(route.request.url)
        if route.request.method == 'POST':
            posts.append(url.path + '?' + url.query)
            route.fulfill(status=204, body='')
            return
        resposta = respostas and respostas(url)
        if resposta:
            route.fulfill(content_type=resposta[0], body=(fixtures / resposta[1]).read_text())
            return
        if url.path == caminho:
            route.fulfill(content_type='text/html', body=sem_selo_local((fixtures / nome).read_text()))
            return
        asset = (assets / unquote(url.path).lstrip('/')).resolve()
        if asset.is_relative_to(assets) and asset.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(asset))[0] or 'application/octet-stream', body=asset.read_bytes())
        else:
            route.fulfill(status=404, body='')

    context.route('**/*', serve)
    page.goto('http://dmo.test' + caminho)
    return context, page, erros


# Desvio vertical (px CSS, positivo = abaixo) do centro da tinta do texto em
# relação ao centro da caixa, a partir da captura (a 2x) do elemento; «corte»
# ignora os píxeis da direita (a seta de um select).
TINTA = '''async ([png, corte]) => {
    const img = new Image();
    img.src = 'data:image/png;base64,' + png;
    await img.decode();
    const canvas = document.createElement('canvas');
    canvas.width = img.width; canvas.height = img.height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(img, 0, 0);
    const { data, width, height } = ctx.getImageData(0, 0, img.width, img.height);
    const luz = (x, y) => { const i = (y * width + x) * 4; return 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2]; };
    const fundo = luz(Math.floor(width / 2), 4);
    const linhas = [];
    for (let y = 3; y < height - 3; y++) {
        for (let x = 6; x < width - corte; x++) {
            if (Math.abs(luz(x, y) - fundo) > 45) { linhas.push(y); break; }
        }
    }
    if (!linhas.length) return null;
    return ((linhas[0] + linhas[linhas.length - 1]) / 2 - height / 2) / 2;
}'''

SCROLL_LATERAL = 'document.documentElement.scrollWidth - document.documentElement.clientWidth'

with sync_playwright() as playwright:
    browser = launch_chromium(playwright)

    for largura in (1400, 390):
        context, page, erros = abrir(browser, 'boquilha-zero.html', '/Boquilhas', largura)
        dica = page.locator('#bqMetrics .lot-data .dmo-help').first
        texto = dica.locator('.dmo-help-tip')
        assert not texto.is_visible(), f'{largura}px: a dica «Metida a zero» está aberta sem o rato'
        assert page.evaluate(SCROLL_LATERAL) <= 0, f'{largura}px: a página tem scroll lateral ({page.evaluate(SCROLL_LATERAL)} px)'
        dica.hover()
        assert texto.is_visible(), f'{largura}px: a dica não abre com o rato'
        caixa = texto.bounding_box()
        assert caixa['x'] >= 0 and caixa['x'] + caixa['width'] <= largura, f'{largura}px: a dica sai do ecrã {caixa}'
        assert page.evaluate(SCROLL_LATERAL) <= 0, f'{largura}px: a dica aberta cria scroll lateral'
        assert not erros, erros
        context.close()

    context, page, erros = abrir(browser, 'puncoes.html', '/Controlo/Puncoes', 1400)
    lote = page.locator('#pu-lote')
    lote.fill('abc')
    # Lido antes do clique: o envio navega e a página deixa de responder.
    assert lote.evaluate('campo => campo.validity.patternMismatch') is False
    with page.expect_request(lambda pedido: pedido.method == 'POST'):
        # O botão está no canto do cartão, fora do formulário (form="pu-id-form").
        page.locator('#pu-id-guardar').click()
    assert not erros, erros
    context.close()

    context, page, erros = abrir(browser, 'resumo-sem-peso.html', '/Controlo/Resumo', 1400)
    medidas = page.evaluate('''() => {
        const cabeca = document.querySelector('.resumo-linked-peso .resumo-linked-card-head');
        const titulo = cabeca.querySelector(':scope > span').getBoundingClientRect();
        const cartao = cabeca.closest('.resumo-linked-card').getBoundingClientRect();
        return [...cabeca.querySelectorAll('.dmo-button')].map(b => {
            const r = b.getBoundingClientRect();
            return { texto: b.textContent.trim(), centro: r.top + r.height / 2, tituloTop: titulo.top - 8, tituloBottom: titulo.bottom + 8,
                     dentro: r.left >= cartao.left && r.right <= cartao.right };
        });
    }''')
    assert len(medidas) == 2, medidas
    for botao in medidas:
        assert botao['tituloTop'] <= botao['centro'] <= botao['tituloBottom'], f'1400px: «{botao["texto"]}» fora da linha do título {botao}'
        assert botao['dentro'], f'1400px: «{botao["texto"]}» sai do cartão'
    assert not erros, erros
    context.close()

    # Largo: na linha dos campos, centrados com eles. Mais estreito, quando
    # não cabem ao lado: uma linha por baixo, encostada à direita do cartão.
    # Sem o «Consultar» (Diogo, 19/10/2026: as consultas passaram a links no
    # Resumo do Controlo) os botões já cabem ao lado a 1400px; passam para
    # baixo a 1100px.
    for largura, mesma_linha in ((1920, True), (1400, True), (1100, False)):
        context, page, erros = abrir(browser, 'revisao-filtros.html', '/Controlo/Approve', largura)
        medidas = page.evaluate('''() => {
            const caixa = e => e.getBoundingClientRect();
            const cartao = document.querySelector('#revisaoFiltros');
            const dentro = caixa(cartao).right - parseFloat(getComputedStyle(cartao).paddingRight);
            const campo = caixa(document.querySelector('#revisao-procura'));
            const ultimo = caixa([...document.querySelectorAll('#revisaoFiltrosForm > .dmo-field')].pop());
            const botoes = [...document.querySelectorAll('#revisaoAcoes > .revisao-pill, #revisaoAcoes > .dmo-button, #revisaoAcoes > details > summary')]
                .map(b => ({ texto: b.textContent.trim(), centro: caixa(b).top + caixa(b).height / 2, left: caixa(b).left, right: caixa(b).right }));
            return { botoes, campoCentro: campo.top + campo.height / 2, campoBottom: campo.bottom, ultimoRight: ultimo.right, dentro };
        }''')
        botoes = medidas['botoes']
        assert any(b['texto'] == 'Limpar filtros' for b in botoes), medidas
        assert abs(botoes[-1]['right'] - medidas['dentro']) <= 1, f'{largura}px: os botões não acabam à direita do cartão {medidas}'
        for botao in botoes:
            assert abs(botao['centro'] - botoes[0]['centro']) <= 1, f'{largura}px: «{botao["texto"]}» desalinhado dos outros {medidas}'
            if mesma_linha:
                assert abs(botao['centro'] - medidas['campoCentro']) <= 1, f'{largura}px: «{botao["texto"]}» fora do centro dos campos {medidas}'
                assert botao['left'] > medidas['ultimoRight'], f'{largura}px: «{botao["texto"]}» por cima dos campos {medidas}'
            else:
                assert botao['centro'] > medidas['campoBottom'], f'{largura}px: «{botao["texto"]}» devia passar para baixo {medidas}'
        assert not erros, erros
        context.close()
    def procura_jobon(url):
        if url.path == '/Sugestoes' and 'q=60' in url.query:
            return 'application/json', 'jobon-sugestoes-60.json'
        if 'handler=ProcurarProducao' in url.query and 'q=6018Y232' in url.query:
            return 'text/html', 'jobon-producoes-6018Y232.html'
        if 'handler=ProcurarProducao' in url.query and 'q=60' in url.query:
            return 'text/html', 'jobon-producoes-60.html'
        return None

    context, page, erros = abrir(browser, 'jobon-procura.html', '/JobOn', 1400, procura_jobon)
    page.locator('#lightSearch').press_sequentially('60', delay=50)
    sugestao = page.locator('.dmo-suggest-list [role=option]', has_text='6018Y232')
    sugestao.wait_for(state='visible', timeout=3000)
    producoes = page.locator('#lightProcuraProducoes')
    page.wait_for_function("() => !document.querySelector('#lightProcura').hasAttribute('aria-busy')", timeout=3000)
    assert producoes.is_disabled(), 'com «60» o dropdown enche-se antes de escolher a referência'
    sugestao.click()
    producoes.locator('option', has_text='202602').wait_for(state='attached', timeout=3000)
    textos = producoes.locator('option').all_inner_texts()
    assert not any('6018Y232' in texto for texto in textos), f'o dropdown repete a referência: {textos}'
    assert page.input_value('#lightSearch') == '6018Y232', page.input_value('#lightSearch')
    assert producoes.is_enabled(), 'o dropdown das produções ficou desativado'
    assert not erros, erros
    context.close()

    # Diogo, 20/10/2026 («verificar centramento de todos»): o texto do campo
    # Pesquisar, do select Estado e de «Limpar filtros» ao centro da caixa,
    # medido nos píxeis da letra (maiúsculas e algarismos, a 2x). Antes, nos
    # campos, ficava 0,75 px acima.
    context, page, erros = abrir(browser, 'revisao-filtros.html', '/Controlo/Approve', 1400, escala=2)
    page.evaluate("""() => {
        document.querySelector('#revisao-procura').value = 'TODOS 5447';
        const estado = document.querySelector('#revisao-estado');
        estado.options[estado.selectedIndex].text = 'TODOS 5447';
    }""")
    for seletor, corte in (('#revisao-procura', 0), ('#revisao-estado', 80), ('#revisaoAcoes .dmo-button[href="/Controlo/Approve"]', 0)):
        png = base64.b64encode(page.locator(seletor).screenshot()).decode()
        desvio = page.evaluate(TINTA, [png, corte])
        assert desvio is not None and abs(desvio) <= 0.5, f'{seletor}: texto {desvio} px fora do centro'
    assert not erros, erros
    context.close()
    browser.close()

print('nok_agente_browser: OK')

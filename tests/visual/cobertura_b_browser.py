"""Medidas no Chromium que faltavam a correções só de aspeto (cobertura B).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR), os mesmos dos outros scripts:
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~Tabela_TemLargurasFixas_EVaiParaOBrowser|FullyQualifiedName~ToolScopedFlow_CreateRecordConsult_ThroughThePage|FullyQualifiedName~PostMeasurement_CreatesRegistro_PRG|FullyQualifiedName~CoberturaB|FullyQualifiedName~ParaOBrowser|FullyQualifiedName~Ultimas_Todas_DoMaisRecente|FullyQualifiedName~JobOnFolhaPrimeiraFilaTests|FullyQualifiedName~ResumoNoBrowser'
  python tests/visual/cobertura_b_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

A 1290x646 (o Chrome do Diogo) e a 1366x768:
- DMO-514: os botões não têm seta (o triângulo fica só nos títulos que abrem e fecham).
- DMO-484: nas punções, o número e o «+ Adicionar punção» ficam na mesma linha, à direita
  do cartão; a página não desliza nem para os lados nem para baixo.
- DMO-482: nos Pegamentos, a página não desliza (desliza a folha por dentro).
- DMO-505, 502, 487, 523, 467, 469, 473, 576: a segunda volta, mais abaixo (login com erro,
  Peso no painel, Armazém, Definições, Resumo, Comparação, Peso sem Job On e folha do Job On).
- DMO-499: no Peso, a leitura nova é estreita e o «Registar leitura» tem a largura dos dois campos.
"""
import mimetypes
import re
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'

ECRAS = [(1290, 646), (1366, 768)]
SCROLL_LATERAL = 'document.documentElement.scrollWidth - document.documentElement.clientWidth'
SCROLL_VERTICAL = 'document.documentElement.scrollHeight - document.documentElement.clientHeight'
# O que o operador vê de cada triângulo de um botão: o desenho some quando display é none.
SETAS_NOS_BOTOES = '''() => [...document.querySelectorAll('.dmo-button .dmo-caret, .btn .dmo-caret, .button .dmo-caret')]
    .map(c => ({ botao: (c.closest('.dmo-button, .btn, .button').textContent || '').trim(), display: getComputedStyle(c).display,
                 largura: c.getBoundingClientRect().width }))'''
RETANGULO = '''(seletor) => { const e = document.querySelector(seletor); if (!e) return null;
    const r = e.getBoundingClientRect(); return { l: r.left, t: r.top, r: r.right, b: r.bottom, w: r.width, h: r.height }; }'''

falhas = []


def abrir(browser, ficheiro, caminho, largura, altura):
    context = browser.new_context(viewport={'width': largura, 'height': altura})
    page = context.new_page()
    html = sem_selo_local((fixtures / ficheiro).read_text(encoding='utf-8'))

    def servir(route):
        url = urlparse(route.request.url)
        if route.request.method == 'GET' and url.path == caminho:
            route.fulfill(content_type='text/html', body=html)
            return
        asset = (assets / unquote(url.path).lstrip('/')).resolve()
        if asset.is_relative_to(assets) and asset.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(asset))[0] or 'application/octet-stream', body=asset.read_bytes())
        else:
            route.fulfill(status=404, body='')

    context.route('**/*', servir)
    page.goto('http://dmo.test' + caminho)
    page.wait_for_load_state('load')
    return context, page


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura, altura in ECRAS:
        ecra = f'{largura}x{altura}'

        # As punções: sem setas nos botões, o canto superior direito e a página sem deslizar.
        context, page = abrir(browser, 'puncoes-tabela.html', '/Controlo/Puncoes', largura, altura)
        setas = page.evaluate(SETAS_NOS_BOTOES)
        if not setas:
            falhas.append(f'puncoes {ecra}: sem o botão «Opções» com triângulo no HTML (nada para medir)')
        for seta in setas:
            if seta['display'] != 'none' or seta['largura'] > 0:
                falhas.append(f'puncoes {ecra}: o botão «{seta["botao"]}» mostra uma seta {seta}')
        numero = page.evaluate(RETANGULO, '#pu-numero')
        adicionar = page.evaluate(RETANGULO, '.pu-topo .pu-add-form > .dmo-button')
        topo = page.evaluate(RETANGULO, '.pu-topo')
        cartao = page.evaluate('''() => { const c = document.querySelector('.pu-topo')?.closest('.pu-card'); if (!c) return null;
            const r = c.getBoundingClientRect(); return { l: r.left, t: r.top, r: r.right, b: r.bottom, w: r.width, h: r.height }; }''')
        if not (numero and adicionar and topo and cartao):
            falhas.append(f'puncoes {ecra}: falta o número, o «+ Adicionar punção», o .pu-topo ou o cartão')
        else:
            if abs((numero['t'] + numero['b']) / 2 - (adicionar['t'] + adicionar['b']) / 2) > 4:
                falhas.append(f'puncoes {ecra}: o número e o «+ Adicionar punção» não estão na mesma linha: {numero} / {adicionar}')
            if abs(numero['h'] - adicionar['h']) > 2:
                falhas.append(f'puncoes {ecra}: o campo e o botão não têm a mesma altura ({numero["h"]} / {adicionar["h"]})')
            if adicionar['r'] > cartao['r'] + 0.5 or topo['r'] > cartao['r'] + 0.5:
                falhas.append(f'puncoes {ecra}: o canto sai do cartão')
            # O cartão tem de dar o canto direito a quem adiciona: o botão fica na metade direita.
            # O canto é o grupo todo (número, «+ Adicionar» e os botões do canto), encostado à direita do cartão.
            if topo['r'] < cartao['r'] - 40:
                falhas.append(f'puncoes {ecra}: o canto do cartão não está encostado à direita: {topo} / {cartao}')
        if page.evaluate(SCROLL_LATERAL) > 0:
            falhas.append(f'puncoes {ecra}: a página desliza para os lados ({page.evaluate(SCROLL_LATERAL)} px)')
        if page.evaluate(SCROLL_VERTICAL) > 1:
            falhas.append(f'puncoes {ecra}: a página desliza para baixo ({page.evaluate(SCROLL_VERTICAL)} px)')
        context.close()

        # Os Pegamentos: a página não desliza; só a folha, por dentro.
        context, page = abrir(browser, 'pegamentos-folha.html', '/Controlo/Pegamentos', largura, altura)
        if page.locator('.peg-folha').count() == 0:
            falhas.append(f'pegamentos {ecra}: sem .peg-folha no HTML (nada para medir)')
        else:
            if page.evaluate(SCROLL_VERTICAL) > 1:
                falhas.append(f'pegamentos {ecra}: a página desliza para baixo ({page.evaluate(SCROLL_VERTICAL)} px)')
            if page.evaluate(SCROLL_LATERAL) > 0:
                falhas.append(f'pegamentos {ecra}: a página desliza para os lados ({page.evaluate(SCROLL_LATERAL)} px)')
            if page.eval_on_selector('.peg-folha', 'e => getComputedStyle(e).overflowY') not in ('auto', 'scroll'):
                falhas.append(f'pegamentos {ecra}: a folha não desliza por dentro')
            folha = page.evaluate(RETANGULO, '.peg-folha')
            if folha and folha['b'] > altura + 0.5:
                falhas.append(f'pegamentos {ecra}: a folha sai do ecrã por baixo ({folha["b"]})')
        context.close()

        # O Peso: a leitura nova estreita e o «Registar leitura» com a largura dos dois campos.
        context, page = abrir(browser, 'peso-folha.html', '/Controlo/Peso', largura, altura)
        cm = page.evaluate(RETANGULO, '.peso-leitura-nova input[name=cm_subject]')
        agua = page.evaluate(RETANGULO, '.peso-leitura-nova input[name=water_weight]')
        registar = page.evaluate(RETANGULO, '.peso-leitura-nova .peso-registar')
        if not (cm and agua and registar):
            falhas.append(f'peso {ecra}: falta o N.º CM, a massa de água ou o «Registar leitura» na leitura nova')
        else:
            if abs(cm['t'] - agua['t']) > 2:
                falhas.append(f'peso {ecra}: o N.º CM e a massa de água não estão lado a lado: {cm} / {agua}')
            if cm['w'] > 90 or agua['w'] > 110:
                falhas.append(f'peso {ecra}: os campos da leitura nova não são estreitos (CM {cm["w"]}, água {agua["w"]})')
            if registar['t'] <= agua['b'] - 1:
                falhas.append(f'peso {ecra}: o «Registar leitura» devia ficar por baixo dos campos')
            largura_campos = agua['r'] - cm['l']
            if abs(registar['w'] - largura_campos) > 6:
                falhas.append(f'peso {ecra}: o «Registar leitura» ({registar["w"]}) não tem a largura dos dois campos ({largura_campos})')
        if page.evaluate(SCROLL_LATERAL) > 0:
            falhas.append(f'peso {ecra}: a página desliza para os lados ({page.evaluate(SCROLL_LATERAL)} px)')
        # Os cartões «a gravar» ficam no DOM mas não se veem.
        escondidos = page.evaluate('''() => [...document.querySelectorAll('[data-peso-leitura-pendente]')]
            .filter(e => getComputedStyle(e).display !== 'none').length''')
        if escondidos:
            falhas.append(f'peso {ecra}: {escondidos} leituras «a gravar» à vista')
        context.close()

    # ---- Segunda volta: as correções só de aspeto (DMO-502, 505, 487, 523, 467, 406, 469, 473, 576) ----
    def canais(cor):
        """Os três canais (0-255) de rgb(...) ou de color(srgb r g b) (color-mix devolve este)."""
        numeros = [float(v) for v in re.findall(r'[\d.]+', cor)[:3]]
        return [n * 255 for n in numeros] if cor.startswith('color(srgb') else numeros

    def soma(rgb):
        return sum(canais(rgb))

    def sem_deslizar(page, onde):
        if page.evaluate(SCROLL_VERTICAL) > 1:
            falhas.append(f'{onde}: a página desliza para baixo ({page.evaluate(SCROLL_VERTICAL)} px)')
        if page.evaluate(SCROLL_LATERAL) > 0:
            falhas.append(f'{onde}: a página desliza para os lados ({page.evaluate(SCROLL_LATERAL)} px)')

    for largura, altura in ECRAS:
        ecra = f'{largura}x{altura}'

        # DMO-505: o escuro mais claro e o erro do login discreto, em claro e em escuro.
        for tema in ('light', 'dark'):
            context, page = abrir(browser, 'cobertura-b-login-erro.html', '/login', largura, altura)
            page.evaluate(f"document.documentElement.dataset.theme = '{tema}'")
            erro = page.evaluate("""() => { const e = document.querySelector('.login-form .form-message.alert-danger');
                if (!e) return null; const c = getComputedStyle(e);
                return { fundo: c.backgroundColor, esquerda: c.borderLeftWidth, cima: c.borderTopWidth, direita: c.borderRightWidth, letra: c.color }; }""")
            if erro is None:
                falhas.append(f'login {ecra} {tema}: sem a mensagem de erro no HTML')
            else:
                if not erro['fundo'].startswith('rgba(0, 0, 0, 0'):
                    falhas.append(f'login {ecra} {tema}: o erro tem caixa cheia ({erro["fundo"]})')
                if erro['esquerda'] != '2px' or erro['cima'] != '0px' or erro['direita'] != '0px':
                    falhas.append(f'login {ecra} {tema}: o erro devia ter só um traço fino à esquerda: {erro}')
                vermelho = canais(erro['letra'])
                # «vermelho apagado» (DMO-505): puxa ao vermelho sem ser um vermelho vivo.
                if not (vermelho[0] > vermelho[1] + 20 and vermelho[0] > vermelho[2] + 20 and vermelho[1] > 60):
                    falhas.append(f'login {ecra} {tema}: a letra do erro não é um vermelho apagado ({erro["letra"]})')
            if tema == 'dark':
                cores = page.evaluate("""() => { const t = v => { const d = document.createElement('div'); d.style.cssText = 'background:var(' + v + ')';
                    document.body.appendChild(d); const c = getComputedStyle(d).backgroundColor; d.remove(); return c; };
                    return { pagina: getComputedStyle(document.body).backgroundColor, cartao: t('--dmo-card'), linha: t('--dmo-border') }; }""")
                if soma(cores['pagina']) < 110 or soma(cores['cartao']) < 140 or soma(cores['linha']) < 225:
                    falhas.append(f'login {ecra}: o escuro devia ser mais claro: {cores}')
            if page.evaluate(SCROLL_VERTICAL) > 1:
                falhas.append(f'login {ecra} {tema}: a página de entrada desliza ({page.evaluate(SCROLL_VERTICAL)} px)')
            context.close()

        # DMO-502: o Peso no painel do Resumo: os números à esquerda da leitura nova, o Registar encostado.
        for l_painel in (1000, largura):
            context, page = abrir(browser, 'cobertura-b-peso-painel.html', '/Controlo/Peso', l_painel, altura)
            onde = f'peso painel {l_painel}x{altura}'
            faixa = page.evaluate(RETANGULO, '#pesoLeituraResumo')
            nova = page.evaluate(RETANGULO, '.peso-leitura-nova')
            agua = page.evaluate(RETANGULO, '.peso-leitura-nova input[name=water_weight]')
            registar = page.evaluate(RETANGULO, '.peso-leitura-nova .peso-registar')
            if not (faixa and nova and agua and registar):
                falhas.append(f'{onde}: falta a faixa dos números, a leitura nova, a massa de água ou o «Registar leitura»')
            else:
                if faixa['r'] > nova['l'] + 2:
                    falhas.append(f'{onde}: os números não estão à esquerda da leitura nova: {faixa} / {nova}')
                if not (faixa['t'] < nova['b'] and nova['t'] < faixa['b']):
                    falhas.append(f'{onde}: os números e a leitura nova não estão na mesma fila: {faixa} / {nova}')
                if registar['t'] - agua['b'] > 14:
                    falhas.append(f'{onde}: o «Registar leitura» está afastado dos campos ({registar["t"] - agua["b"]} px)')
            chips = page.evaluate("""() => [...document.querySelectorAll('.peso-leitura-nova .peso-leitura-chip:not(.is-refused)')]
                .filter(c => getComputedStyle(c).display !== 'none').length""")
            if chips:
                falhas.append(f'{onde}: {chips} resultados ao vivo por baixo dos campos (só a leitura recusada devia aparecer)')
            if page.evaluate(SCROLL_LATERAL) > 0:
                falhas.append(f'{onde}: a página desliza para os lados')
            context.close()

        # DMO-487: o Armazém sem deslizar e o «Registar» junto ao Entrada | Saída.
        context, page = abrir(browser, 'cobertura-b-armazem-registo.html', '/Armazem/Movimentos', largura, altura)
        onde = f'armazem registo {ecra}'
        sem_deslizar(page, onde)
        registar = page.evaluate(RETANGULO, '#armEntRegistar')
        entrada = page.evaluate(RETANGULO, '#armProcuraModoEntrada')
        if not (registar and entrada):
            falhas.append(f'{onde}: falta o «Registar» ou o «Entrada»')
        else:
            if abs((registar['t'] + registar['b']) / 2 - (entrada['t'] + entrada['b']) / 2) > 6:
                falhas.append(f'{onde}: o «Registar» não está na linha do Entrada | Saída: {registar} / {entrada}')
            if not (registar['r'] <= entrada['l'] and entrada['l'] - registar['r'] <= 48):
                falhas.append(f'{onde}: o «Registar» devia estar logo antes do Entrada ({entrada["l"] - registar["r"]} px)')
        context.close()

        context, page = abrir(browser, 'armazem-movimentos.html', '/Armazem/Ultimos', largura, altura)
        onde = f'armazem movimentos {ecra}'
        sem_deslizar(page, onde)
        # DMO-523: o hover da linha enche-a com a cor da linha, em claro e em escuro.
        for tema in ('light', 'dark'):
            page.evaluate(f"document.documentElement.dataset.theme = '{tema}'")
            linha = page.locator('tr[data-dmo-row]').first
            if linha.count() == 0:
                falhas.append(f'{onde}: sem linhas para o hover')
                break
            page.mouse.move(0, 0)
            antes = page.evaluate("() => getComputedStyle(document.querySelector('tr[data-dmo-row] > td')).backgroundColor")
            linha.hover()
            depois = page.evaluate("() => getComputedStyle(document.querySelector('tr[data-dmo-row] > td')).backgroundColor")
            if antes == depois:
                falhas.append(f'{onde} {tema}: o hover da linha não muda a cor ({antes})')
        context.close()

        # DMO-523: as outras páginas que deixaram de deslizar a 1290x646 / 1366x768.
        for ficheiro, caminho, nome in (
                ('peso-folha.html', '/Controlo/Peso', 'peso'),
                ('resumo-sem-peso.html', '/Controlo/Resumo', 'resumo do controlo'),
                ('cobertura-b-definicoes-controlo.html', '/Definicoes', 'definicoes')):
            context, page = abrir(browser, ficheiro, caminho, largura, altura)
            sem_deslizar(page, f'{nome} {ecra}')
            context.close()

        # DMO-467: o Controlo das Definições numa só vista, em duas colunas, sem círculos de ajuda.
        context, page = abrir(browser, 'cobertura-b-definicoes-controlo.html', '/Definicoes', largura, altura)
        onde = f'definicoes controlo {ecra}'
        cartoes = page.evaluate("""() => ['ctlDefDensidade','ctlDefTolerancia','ctlDefPuncoesSeccoes','ctlDefListas','ctlDefModelos']
            .map(id => { const e = document.getElementById(id); if (!e) return null; const r = e.getBoundingClientRect();
                return { id, l: Math.round(r.left), w: Math.round(r.width) }; })""")
        if any(c is None for c in cartoes) or any(c['w'] == 0 for c in cartoes):
            falhas.append(f'{onde}: faltam cartões do Controlo ou estão escondidos: {cartoes}')
        else:
            colunas = sorted({round(c['l'] / 20) for c in cartoes})
            if len(colunas) != 2:
                falhas.append(f'{onde}: os cartões do Controlo não estão em duas colunas: {cartoes}')
        ajudas = page.evaluate("() => document.querySelectorAll('#sec-controlo h3 .dmo-help').length")
        if ajudas:
            falhas.append(f'{onde}: {ajudas} círculos de ajuda nos títulos')
        context.close()

        # DMO-469: a linha que mais puxa fica só pela cor (cinzento claro, letra azul-escura); campos estreitos.
        context, page = abrir(browser, 'cobertura-b-peso-comparacao.html', '/Controlo/Peso', largura, altura)
        onde = f'comparacao {ecra}'
        cores = page.evaluate("""() => { const d = document.querySelector('tr.peso-linha-destaque > td');
            if (!d) return null; const cd = getComputedStyle(d);
            return { fundo: cd.backgroundColor, letra: cd.color }; }""")
        if cores is None:
            falhas.append(f'{onde}: sem a linha destacada para medir')
        else:
            letra = canais(cores['letra'])
            if cores['fundo'] in ('rgba(0, 0, 0, 0)', 'rgb(255, 255, 255)'):
                falhas.append(f'{onde}: a linha destacada não tem a cor de fundo ({cores["fundo"]})')
            if not letra[2] > letra[0] + 20:
                falhas.append(f'{onde}: a letra da linha destacada não é azul ({cores["letra"]})')
        campos = page.evaluate("""() => ['input[name=cm_subject]', 'input[name=water_weight]']
            .map(s => document.querySelector('#cmpForm ' + s)?.getBoundingClientRect().width ?? null)""")
        if None in campos or campos[0] > 100 or campos[1] > 130:
            falhas.append(f'{onde}: os campos da comparação não são estreitos: {campos}')
        context.close()

        # DMO-473: o Peso sem Job On arrumado: a CM e a BQ numa só faixa e a página sem deslizar para os lados.
        context, page = abrir(browser, 'cobertura-b-peso-sem-jobon.html', '/Controlo/Peso', largura, altura)
        onde = f'peso sem job on {ecra}'
        factos = page.evaluate("""() => [...document.querySelectorAll('[data-peso-ferramentas] > .peso-fact')]
            .map(f => Math.round(f.getBoundingClientRect().top))""")
        if len(factos) < 2:
            falhas.append(f'{onde}: sem a faixa das ferramentas para medir')
        elif max(factos) - min(factos) > 6:
            falhas.append(f'{onde}: a CM e a BQ não estão numa só faixa: {factos}')
        if page.evaluate(SCROLL_LATERAL) > 0:
            falhas.append(f'{onde}: a página desliza para os lados')
        context.close()

        # DMO-576: o aviso do artigo fica dentro do cartão do artigo, por cima da foto e das Pinças.
        context, page = abrir(browser, 'jobon-folha.html', '/JobOn/Folha', largura, altura)
        onde = f'folha do job on {ecra}'
        aviso = page.evaluate("""() => { const a = document.querySelector('[data-jof-artigo-aviso]');
            const cartao = a?.closest('.article'); if (!a || !cartao) return null;
            a.textContent = 'Associe primeiro a pasta das imagens em Definições, Job On.'; a.hidden = false;
            a.scrollIntoView({ block: 'center' });
            const r = a.getBoundingClientRect(), c = cartao.getBoundingClientRect();
            // A barra fixa «O que mudou» cobre o fundo da folha inteira; não conta para o aviso do artigo.
            const pilha = document.elementsFromPoint((r.left + r.right) / 2, (r.top + r.bottom) / 2)
                .filter(e => !e.closest('.jobon-mudancas'));
            const topo = pilha[0];
            return { dentro: r.left >= c.left - 1 && r.right <= c.right + 1 && r.top >= c.top - 1 && r.bottom <= c.bottom + 1,
                     porCima: topo === a || a.contains(topo), largura: Math.round(r.width) }; }""")
        if aviso is None:
            falhas.append(f'{onde}: sem o aviso do artigo dentro de um .article')
        else:
            if not aviso['dentro']:
                falhas.append(f'{onde}: o aviso não está dentro do cartão do artigo: {aviso}')
            if not aviso['porCima']:
                falhas.append(f'{onde}: o aviso está por trás de outro cartão (Pinças)')
        context.close()

    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Cobertura B no browser: passaram')

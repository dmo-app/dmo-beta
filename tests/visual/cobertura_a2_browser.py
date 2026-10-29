"""Cobertura A2: o aspeto que a auditoria deixou sem prova, medido no Chromium (outubro de 2026).

Duas partes:
- uma página sintética com as folhas de estilo reais da app (sem servidor):
    python tests/visual/cobertura_a2_browser.py
  - DMO-381: o cartão do painel lateral leva a cor da sua linha na barra da esquerda (a de B1 não é a de C2),
    em claro e em escuro.
- o HTML que os testes HTTP reais exportaram (DMO_UI_EVIDENCE_DIR):
    DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
      --filter 'FullyQualifiedName~Ficha_ParaOBrowser|FullyQualifiedName~ListaDosUtilizadores_ComMaisDe25|FullyQualifiedName~Resumo_ComPesoDaProducaoAnteriorLongo|FullyQualifiedName~RegistarSemJobOn_ALinhaVemJaEscolhida|RegistoComProducao_MostraBqLote|FullyQualifiedName~JobOnFolhaPrimeiraFilaTests|FullyQualifiedName~RevisaoArrumada|FullyQualifiedName~PostaAZero|FullyQualifiedName~PegamentosPageTests'
    python tests/visual/cobertura_a2_browser.py <pasta>
  A 1366×768 e a 1290×646 (o Chrome do Diogo no trabalho):
  - DMO-431: no Job On, «Linhas» e «Imprimir» ficam dentro do ecrã e a Referência e a Produção não se tocam;
  - DMO-447: o Imprimir sai a preto e branco (nenhuma cor de letra, fundo ou linha tem matiz);
  - DMO-479 e DMO-493: a ficha da ferramenta e a lista dos Utilizadores não deslizam na vertical, também com a
    Informação técnica aberta, e o «← Voltar» da ficha fica por cima do título;
  - DMO-483: a Revisão não desliza na vertical;
  - DMO-510: no Resumo, os números do cartão do Peso (ex.: 1130.00 g) não saem cortados nem do cartão;
  - DMO-522: no Registo das Boquilhas, nenhum dado do registo («BQ · lote») sai cortado;
  - DMO-531: na janela «Registar sem Job On», a Linha e a Peça ficam em cima, o «Registar» antes da Saída e a
    Quantidade, a Data e a Hora na mesma linha;
  - DMO-563: nos Pegamentos o Mapa de limites fica por baixo das leituras e os valores do contexto são grandes e
    ao centro.
Só ficheiros locais; nada vai ao DEV. Mede o efeito no ecrã (posição, cor real, corte), nunca o texto do CSS.
"""
import mimetypes
import sys
import tempfile
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

www = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
ECRAS = ((1366, 768), (1290, 646))
falhas = []


# ---------------------------------------------------------------- DMO-381 (sintética)
CSS_PAINEL = ['0_ASSET_CANONICAL_DESIGN_SYSTEM.css', '0_ASSET_SHELL.css', 'beta-shell.css',
              'beta-production-overview.css', 'css/dmo-normalize.css']
LINHAS = ('B1', 'B2', 'B3', 'C1', 'C2', 'C3')
PAINEL = '<!doctype html><html><head><meta charset="utf-8">' + ''.join(
    f'<link rel="stylesheet" href="{(www / c).as_uri()}">' for c in CSS_PAINEL) + \
    '</head><body><aside class="beta-production-rail"><div class="beta-production-lines" data-dmo-list="Linhas">' + \
    ''.join(f'<button type="button" data-dmo-row data-dmo-linha-cor="{l}" id="l{l}"><strong>{l}</strong><span>1075 · Lote 13</span></button>'
            for l in LINHAS) + \
    '<button type="button" data-dmo-row id="semcor"><strong>B10</strong><span>Sem cor</span></button>' + \
    '</div></aside><i id="sonda" style="position:absolute;left:-99px"></i></body></html>'

BARRAS = '''(linhas) => {
  const sonda = document.getElementById('sonda');
  const cor = v => { sonda.style.background = v; return getComputedStyle(sonda).backgroundColor; };
  const r = {};
  for (const l of linhas) {
    const e = document.getElementById('l' + l);
    r[l] = { barra: getComputedStyle(e).borderLeftColor, larg: getComputedStyle(e).borderLeftWidth,
             esperada: cor(`var(--dmo-machine-${l.toLowerCase()})`) };
  }
  r.semcor = { barra: getComputedStyle(document.getElementById('semcor')).borderLeftColor };
  r.marca = cor('var(--dmo-brand-600)');
  return r;
}'''


def painel_lateral(browser):
    pagina = Path(tempfile.mkdtemp()) / 'painel-linha-cor.html'
    pagina.write_text(PAINEL, encoding='utf-8')
    for tema in ('light', 'dark'):
        page = browser.new_page(viewport={'width': 400, 'height': 700}, color_scheme=tema)
        page.goto(pagina.as_uri())
        page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
        page.wait_for_load_state('load')
        page.wait_for_timeout(400)
        m = page.evaluate(BARRAS, list(LINHAS))
        print(f'painel {tema}: ' + ' '.join(f'{l}={m[l]["barra"]}' for l in LINHAS))
        for l in LINHAS:
            if m[l]['larg'] != '4px':
                falhas.append(f'painel {tema}: a barra da {l} não está lá ({m[l]["larg"]})')
            if m[l]['barra'] != m[l]['esperada']:
                falhas.append(f'painel {tema}: a barra da {l} é {m[l]["barra"]} e devia ser a cor da linha {m[l]["esperada"]}')
        if len({m[l]['barra'] for l in LINHAS}) < 5:
            falhas.append(f'painel {tema}: as linhas não têm cores diferentes ({[m[l]["barra"] for l in LINHAS]})')
        if m['semcor']['barra'] in {m[l]['barra'] for l in LINHAS}:
            falhas.append(f'painel {tema}: um cartão sem linha tem a cor de uma linha')
        page.close()


# ---------------------------------------------------------------- páginas exportadas
def abrir(browser, fixtures, ficheiro, caminho, largura, altura, tema='light'):
    context = browser.new_context(viewport={'width': largura, 'height': altura}, locale='pt-PT', timezone_id='Europe/Lisbon',
                                  color_scheme=tema)
    html = sem_selo_local((fixtures / ficheiro).read_text(encoding='utf-8'))

    def servir(route):
        url = urlparse(route.request.url)
        if route.request.method == 'GET' and url.path == caminho:
            route.fulfill(content_type='text/html', body=html)
            return
        alvo = (www / unquote(url.path).lstrip('/')).resolve()
        if alvo.is_relative_to(www) and alvo.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
        else:
            route.fulfill(status=404, body='')

    context.route('**/*', servir)
    page = context.new_page()
    erros = []
    page.on('pageerror', lambda e: erros.append(str(e)))
    page.goto('http://dmo.test' + caminho)
    page.wait_for_load_state('load')
    page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
    return context, page, erros


SEM_DESLIZAR = '''() => ({ vertical: document.documentElement.scrollHeight - innerHeight,
                           lateral: document.documentElement.scrollWidth - innerWidth })'''


def encaixa(page, onde):
    m = page.evaluate(SEM_DESLIZAR)
    if m['vertical'] > 1:
        falhas.append(f'{onde}: a página desliza na vertical ({m["vertical"]}px)')
    if m['lateral'] > 1:
        falhas.append(f'{onde}: a página desliza para o lado ({m["lateral"]}px)')


# DMO-431
CABECALHO = '''() => {
  const r = s => { const e = document.querySelector(s); if (!e || !e.getClientRects().length) return null;
    const b = e.getBoundingClientRect(); return { left: b.left, right: b.right, top: b.top, bottom: b.bottom }; };
  const ref = document.querySelector('.page-head .pick-row .ref-search'), producao = document.querySelector('.page-head .pick-row .prod-select');
  const campo = document.getElementById('jofReferencia');
  return { linhas: r('[data-jof-rail-abrir]'), imprimir: r('[data-jof-imprimir]'),
           ref: ref && ref.getClientRects().length ? (({ left, right, top, bottom }) => ({ left, right, top, bottom }))(ref.getBoundingClientRect()) : null,
           producao: r('.page-head .pick-row .prod-trigger') || r('.page-head .pick-row .prod-select'),
           referenciaCortada: campo ? campo.scrollWidth - campo.clientWidth : null,
           largura: innerWidth, altura: innerHeight };
}'''


def jobon_cabecalho(browser, fixtures):
    for largura, altura in ECRAS:
        context, page, erros = abrir(browser, fixtures, 'jobon-folha.html', '/JobOn/Folha', largura, altura)
        onde = f'Job On {largura}×{altura}'
        m = page.evaluate(CABECALHO)
        print(f'{onde}: {m}')
        for nome in ('linhas', 'imprimir'):
            b = m[nome]
            if b is None:
                falhas.append(f'{onde}: falta o botão «{nome}» no cabeçalho')
            elif b['left'] < 0 or b['right'] > m['largura'] + 0.5 or b['top'] < 0 or b['bottom'] > m['altura'] + 0.5:
                falhas.append(f'{onde}: o botão «{nome}» sai do ecrã ({b})')
        ref, prod = m['ref'], m['producao']
        if ref is None or prod is None:
            falhas.append(f'{onde}: faltam a Referência ou a Produção no cabeçalho')
        elif min(ref['right'], prod['right']) - max(ref['left'], prod['left']) > 0.5 and \
                min(ref['bottom'], prod['bottom']) - max(ref['top'], prod['top']) > 0.5:
            falhas.append(f'{onde}: a Referência e a Produção sobrepõem-se ({ref} · {prod})')
        if m['referenciaCortada'] is not None and m['referenciaCortada'] > 1:
            falhas.append(f'{onde}: a referência não se vê inteira (corte de {m["referenciaCortada"]}px)')
        if erros:
            falhas.append(f'{onde}: erros na página {erros}')
        context.close()


# DMO-447
IMPRIMIR = '''() => {
  window.print = () => {};
  window.dmoJobOnImprimir(1, 'todas');
  return document.querySelectorAll('.jof-impressao-print [data-jof-folha]').length;
}'''
CORES = '''() => {
  const rgb = s => (s.match(/[\\d.]+/g) || []).map(Number);
  const colorido = s => { const c = rgb(s); if (c.length < 3 || (c.length >= 4 && c[3] <= 0.05)) return false;
    const max = Math.max(c[0], c[1], c[2]), min = Math.min(c[0], c[1], c[2]);
    // Cinzento azulado (ardósia) imprime-se como cinzento; cor a sério tem mais de 15% de saturação.
    return (max - min) > 6 && (max - min) / max > 0.15; };
  const maus = [];
  for (const e of document.querySelectorAll('.jof-impressao-print *')) {
    if (!e.getClientRects().length) continue;
    const cs = getComputedStyle(e);
    const texto = [...e.childNodes].some(n => n.nodeType === 3 && n.nodeValue.trim());
    if (texto && colorido(cs.color)) maus.push(e.tagName + ' letra ' + cs.color + ' «' + e.textContent.trim().slice(0, 20) + '»');
    if (colorido(cs.backgroundColor)) maus.push(e.tagName + ' fundo ' + cs.backgroundColor);
    for (const lado of ['Top', 'Right', 'Bottom', 'Left']) {
      if (parseFloat(cs['border' + lado + 'Width']) > 0 && cs['border' + lado + 'Style'] !== 'none' && colorido(cs['border' + lado + 'Color']))
        maus.push(e.tagName + ' linha ' + cs['border' + lado + 'Color']);
    }
  }
  return maus.slice(0, 8);
}'''


def jobon_imprimir(browser, fixtures):
    context, page, erros = abrir(browser, fixtures, 'jobon-folha.html', '/JobOn/Folha', 1366, 768)
    folhas = page.evaluate(IMPRIMIR)
    if folhas < 1:
        falhas.append('Imprimir: o Imprimir não pôs nenhuma folha para imprimir')
    else:
        page.emulate_media(media='print')
        page.wait_for_timeout(200)
        maus = page.evaluate(CORES)
        print(f'Imprimir: {folhas} folhas; a cores: {maus}')
        if maus:
            falhas.append(f'Imprimir: não está a preto e branco ({maus})')
    context.close()


# DMO-479, DMO-493
def ficha_e_utilizadores(browser, fixtures):
    for largura, altura in ECRAS:
        for ficheiro, caminho, nome in (('cobertura-a2-ferr-ficha.html', '/Ferramentas/Ficha', 'Ficha'),
                                        ('cobertura-a2-admin-users.html', '/Admin/Users', 'Utilizadores')):
            for tema in ('light', 'dark'):
                context, page, erros = abrir(browser, fixtures, ficheiro, caminho, largura, altura, tema)
                onde = f'{nome} {largura}×{altura} {tema}'
                encaixa(page, onde)
                if nome == 'Ficha':
                    voltar = page.evaluate('''() => { const v = document.getElementById('ferrVoltarTexto'), t = document.querySelector('h1');
                      if (!v || !t || !v.getClientRects().length) return null;
                      return { voltarFundo: v.getBoundingClientRect().bottom, tituloTopo: t.getBoundingClientRect().top }; }''')
                    if voltar is None:
                        falhas.append(f'{onde}: faltam o «← Voltar» ou o título')
                    elif voltar['voltarFundo'] > voltar['tituloTopo'] + 1:
                        falhas.append(f'{onde}: o «← Voltar» não está por cima do título ({voltar})')
                    # Com a Informação técnica aberta continua a caber.
                    abertos = page.evaluate('''() => { const d = [...document.querySelectorAll('main details')]; d.forEach(x => { x.open = true; });
                      return d.length; }''')
                    page.wait_for_timeout(150)
                    encaixa(page, onde + ' (informação técnica aberta)')
                if erros:
                    falhas.append(f'{onde}: erros na página {erros}')
                context.close()


# DMO-483
def revisao(browser, fixtures):
    for largura, altura in ECRAS:
        context, page, erros = abrir(browser, fixtures, 'revisao-filtros.html', '/Controlo/Approve', largura, altura)
        encaixa(page, f'Revisão {largura}×{altura}')
        context.close()


# DMO-510
CELULAS = '''() => {
  const tabela = document.querySelector('[data-resumo-peso-tabela]');
  if (!tabela) return null;
  const cartao = tabela.closest('section, .dmo-card, article') || tabela.parentElement;
  const caixa = cartao.getBoundingClientRect();
  const cortadas = [...tabela.querySelectorAll('th, td')].filter(c => c.scrollWidth > c.clientWidth + 1).map(c => c.textContent.trim().slice(0, 30));
  const reticencias = [...tabela.querySelectorAll('*')].filter(e => getComputedStyle(e).textOverflow === 'ellipsis' && e.scrollWidth > e.clientWidth + 1).length;
  return { cortadas, reticencias, fora: tabela.getBoundingClientRect().right - caixa.right,
           longo: [...tabela.querySelectorAll('td')].some(c => /^1130/.test(c.textContent.trim())) };
}'''


def resumo_peso(browser, fixtures):
    for largura, altura in ECRAS:
        context, page, erros = abrir(browser, fixtures, 'cobertura-a2-resumo-peso.html', '/Controlo/Resumo', largura, altura)
        onde = f'Resumo {largura}×{altura}'
        m = page.evaluate(CELULAS)
        if m is None or not m['longo']:
            falhas.append(f'{onde}: falta a tabela do Peso com o número longo')
        else:
            print(f'{onde}: {m}')
            if m['cortadas'] or m['reticencias']:
                falhas.append(f'{onde}: números cortados {m["cortadas"]} (reticências {m["reticencias"]})')
            if m['fora'] > 1:
                falhas.append(f'{onde}: a tabela do Peso sai do cartão ({m["fora"]}px)')
        context.close()


# DMO-522
FACTOS = '''() => [...document.querySelectorAll('main .dmo-fact')].filter(f => f.getClientRects().length).map(f => {
  const s = f.querySelector('strong'); return { rotulo: (f.querySelector('span') || f).textContent.trim().slice(0, 30), texto: s ? s.textContent.trim().slice(0, 40) : '',
    cortado: s ? s.scrollWidth - s.clientWidth : 0 };
})'''


def registo_bq(browser, fixtures):
    # A 1290×646 o «Início: Sem data no Job On» sai cortado 2px: está em tests/visual/pendentes_a2_browser.py (DMO-522).
    for largura, altura in ECRAS[:1]:
        context, page, erros = abrir(browser, fixtures, 'cobertura-a2-bq-registo-producao.html', '/Boquilhas', largura, altura)
        onde = f'Registo BQ {largura}×{altura}'
        factos = page.evaluate(FACTOS)
        print(f'{onde}: {len(factos)} dados')
        if not factos:
            falhas.append(f'{onde}: o registo não tem dados')
        for f in factos:
            if f['cortado'] > 1:
                falhas.append(f'{onde}: «{f["rotulo"]}» sai cortado ({f["texto"]}, {f["cortado"]}px)')
        context.close()


# DMO-531
JANELA = '''() => {
  const dialog = document.getElementById('bqSemJobOnDialog');
  dialog.showModal();
  const r = id => { const e = document.getElementById(id); if (!e) return null; const b = e.getBoundingClientRect();
    return { left: b.left, right: b.right, top: b.top, bottom: b.bottom }; };
  const registar = dialog.querySelector('.bq-sem-jobon-registar'), saida = dialog.querySelector('input[name=movement_type][value=saida]');
  const q = e => { const b = e.getBoundingClientRect(); return { left: b.left, right: b.right, top: b.top, bottom: b.bottom }; };
  return { linha: r('bqSemJobOnLinha'), peca: r('bqSemJobOnPeca'), qtd: r('bqSemJobOnQtd'), dia: r('bqSemJobOnDia'), hora: r('bqSemJobOnHora'),
           registar: q(registar), saida: q(saida.closest('label')) };
}'''


def sem_jobon(browser, fixtures):
    for largura, altura in ECRAS:
        context, page, erros = abrir(browser, fixtures, 'cobertura-a2-bq-sem-jobon.html', '/Boquilhas', largura, altura)
        onde = f'Registar sem Job On {largura}×{altura}'
        m = page.evaluate(JANELA)
        print(f'{onde}: {m}')
        if abs(m['linha']['top'] - m['peca']['top']) > 3:
            falhas.append(f'{onde}: a Linha e a Peça não estão na mesma linha')
        if not (m['linha']['bottom'] <= m['registar']['top'] + 1 and m['linha']['bottom'] <= m['qtd']['top'] + 1):
            falhas.append(f'{onde}: a Linha e a Peça não estão por cima do resto')
        if not (m['registar']['right'] <= m['saida']['left'] + 1 and abs((m['registar']['top'] + m['registar']['bottom']) / 2
                                                                           - (m['saida']['top'] + m['saida']['bottom']) / 2) < 12):
            falhas.append(f'{onde}: o «Registar» não está antes da Saída na mesma linha ({m["registar"]} · {m["saida"]})')
        if abs(m['qtd']['top'] - m['dia']['top']) > 3 or abs(m['dia']['top'] - m['hora']['top']) > 3:
            falhas.append(f'{onde}: a Quantidade, a Data e a Hora não estão na mesma linha')
        context.close()


# DMO-563
PEGAMENTOS = '''() => {
  const mapa = document.querySelector('#pegMapa .peg-map-card');
  const tabelas = [...document.querySelectorAll('[data-peg-registo]')].filter(t => t.getClientRects().length);
  const fundo = tabelas.length ? Math.max(...tabelas.map(t => t.getBoundingClientRect().bottom)) : null;
  const factos = [...document.querySelectorAll('.peg-context-grid > .dmo-fact')].filter(f => f.getClientRects().length);
  return { mapaTopo: mapa && mapa.getClientRects().length ? mapa.getBoundingClientRect().top : null, fundoDasLeituras: fundo,
           letra: factos.map(f => parseFloat(getComputedStyle(f.querySelector('strong')).fontSize)),
           alinhamento: factos.map(f => getComputedStyle(f).textAlign) };
}'''


def pegamentos(browser, fixtures):
    for largura, altura in ECRAS:
        context, page, erros = abrir(browser, fixtures, 'cobertura-a2-pegamentos-mapa.html', '/Controlo/Pegamentos', largura, altura)
        onde = f'Pegamentos {largura}×{altura}'
        m = page.evaluate(PEGAMENTOS)
        print(f'{onde}: {m}')
        if m['mapaTopo'] is None or m['fundoDasLeituras'] is None:
            falhas.append(f'{onde}: faltam o mapa ou as leituras')
        elif m['mapaTopo'] < m['fundoDasLeituras'] - 1:
            falhas.append(f'{onde}: o Mapa de limites não está por baixo das leituras ({m["mapaTopo"]} < {m["fundoDasLeituras"]})')
        if not m['letra']:
            falhas.append(f'{onde}: falta o contexto da produção')
        elif min(m['letra']) < 17 or any(a != 'center' for a in m['alinhamento']):
            falhas.append(f'{onde}: os valores do contexto não são grandes e ao centro ({m["letra"]} · {m["alinhamento"]})')
        context.close()


# DMO-608: o Geral da ficha numa só linha, também à largura do painel lateral
# (Diogo, 29/10/2026): as Linhas uma por baixo da outra, a Utilização na mesma linha.
def ficha_geral(browser, fixtures):
    for largura in (1366, 1100, 900):
        context, page, erros = abrir(browser, fixtures, 'cobertura-a2-ferr-ficha-geral.html', '/Ferramentas/Ficha', largura, 768)
        onde = f'Ficha Geral {largura}px'
        m = page.evaluate('''() => {
          const f = document.querySelector('#ferrGeral > .dmo-facts');
          if (!f) return null;
          const caixas = [...f.children].filter(e => e.getClientRects().length).map(e => e.getBoundingClientRect());
          const linhas = [...document.querySelectorAll('#ferrLinhas strong > span')].map(e => e.getBoundingClientRect());
          return { n: caixas.length, topos: caixas.map(c => Math.round(c.top)),
                   fora: f.scrollWidth - f.clientWidth,
                   linhas: linhas.map(l => ({ x: Math.round(l.left), y: Math.round(l.top) })),
                   util: !!f.querySelector('#ferrUtilizacao') };
        }''')
        if m is None:
            falhas.append(f'{onde}: falta o quadro do Geral')
        else:
            if not m['util'] or m['n'] < 5:
                falhas.append(f'{onde}: faltam dados no Geral ({m})')
            if max(m['topos']) - min(m['topos']) > 1 or m['fora'] > 0:
                falhas.append(f'{onde}: o Geral não está numa só linha ({m})')
            if len(m['linhas']) != 2 or m['linhas'][0]['x'] != m['linhas'][1]['x'] or m['linhas'][1]['y'] <= m['linhas'][0]['y']:
                falhas.append(f'{onde}: as Linhas não estão uma por baixo da outra ({m["linhas"]})')
        if erros:
            falhas.append(f'{onde}: erros na página {erros}')
        context.close()


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    painel_lateral(browser)
    if len(sys.argv) > 1:
        fixtures = Path(sys.argv[1]).resolve()
        for parte in (jobon_cabecalho, jobon_imprimir, ficha_e_utilizadores, ficha_geral, revisao, resumo_peso, registo_bq, sem_jobon, pegamentos):
            parte(browser, fixtures)
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Cobertura A2: ok')

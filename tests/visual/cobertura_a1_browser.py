"""Cobertura A1: a folha do Job On, o Planeamento e o Sumário medidos no Chromium (Diogo, 28/10/2026).

Duas partes:
- páginas sintéticas com as folhas de estilo reais da app (sem servidor), em claro e em escuro:
    python tests/visual/cobertura_a1_browser.py
  - DMO-561: o cartão escolhido da faixa «Planeamento» tem o aspeto do hover, não outro;
  - DMO-380: a linha do Sumário que abre uma página enche-se ao passar o rato, com a letra legível,
    num tom mais claro no escuro, e mostra a mão;
  - DMO-489: os botões com aspeto de ligação não se sublinham ao passar o rato e o cabeçalho é «Nome · Título» numa linha.
- o HTML que o teste Folha_Cheia_ComOitoCalibresENotasCompridas_ExportadaParaOBrowser exportou (DMO_UI_EVIDENCE_DIR):
    DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests --filter 'FullyQualifiedName~Folha_Cheia_'
    python tests/visual/cobertura_a1_browser.py <pasta>
  A 1366×768 e a 1290×646 (o Chrome do Diogo no trabalho):
  - DMO-486 e DMO-521: a folha, ver e Editar, não desliza (nem a página nem para o lado);
  - DMO-495: os oito calibres ficam à vista no cartão; as notas compridas deslizam por dentro e acabam numa
    linha inteira; o cabeçalho do Editar fica fixo;
  - DMO-509: a nota aberta cai por cima, sem empurrar os outros cartões; a foto do artigo é 3:4 e sem a referência;
  - DMO-562: a Informação técnica da folha abre para baixo e empurra os cartões de baixo, sem os tapar;
  - DMO-476: o Imprimir faz caber as quatro folhas no A4, com a letra nunca abaixo de 8,5pt.
Só ficheiros locais; nada vai ao DEV.
"""
import mimetypes
import sys
import tempfile
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

www = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
CSS_DA_APP = ['0_ASSET_CANONICAL_DESIGN_SYSTEM.css', '0_ASSET_SHELL.css', 'beta-shell.css', 'beta-production-overview.css',
              'beta-calendar.css', 'shell-proof.css', 'css/dmo-normalize.css', 'css/dmo-design.css', 'css/d1366-planeamento.css',
              'css/d1366-controlo.css', 'css/d1366-boquilhas-ferramentas.css', 'css/d1366-armazem-reparacao-admin.css',
              'css/dmo-estrutura.css', 'css/dmo-botoes.css', 'css/dmo-campos.css', 'beta-adapter.css']
falhas = []


def pagina_sintetica(corpo, extra_css=()):
    ligacoes = ''.join(f'<link rel="stylesheet" href="{(www / c).as_uri()}">' for c in [*CSS_DA_APP, *extra_css])
    pasta = Path(tempfile.mkdtemp())
    ficheiro = pasta / 'pagina.html'
    ficheiro.write_text(f'<!doctype html><html><head><meta charset="utf-8">{ligacoes}</head><body>{corpo}</body></html>', encoding='utf-8')
    return ficheiro


# O fundo de um elemento, a letra e o contraste entre os dois (como em painel_lateral_browser.py).
MEDIR = '''(id) => {
  const el = document.getElementById(id);
  // O Chromium devolve as misturas como color(srgb 0.25 0.31 0.40 / 0.16), com os canais de 0 a 1.
  const rgba = s => { const m = s.match(/[\\d.]+/g).map(Number); const k = s.startsWith('color(') ? 255 : 1;
    return [m[0] * k, m[1] * k, m[2] * k, m.length > 3 ? m[3] : 1]; };
  const fundo = e => { const camadas = []; for (; e; e = e.parentElement) { const c = rgba(getComputedStyle(e).backgroundColor); if (c[3] > 0) camadas.push(c); if (c[3] >= 1) break; }
    let r = [255, 255, 255]; for (const c of camadas.reverse()) r = r.map((v, i) => v * (1 - c[3]) + c[i] * c[3]); return r; };
  const L = c => { const [r, g, b] = c.map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
  const bg = fundo(el);
  const alvo = el.querySelector('strong, a, span') || el;
  const t = rgba(getComputedStyle(alvo).color);
  const fg = bg.map((v, i) => v * (1 - t[3]) + t[i] * t[3]);
  const a = L(fg), b = L(bg);
  return { fundo: getComputedStyle(el).backgroundColor, luz: L(bg), letra: getComputedStyle(alvo).color,
           contraste: (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05), cursor: getComputedStyle(el).cursor,
           sombra: getComputedStyle(el).boxShadow };
}'''


def planeamento(browser):
    """DMO-561: o cartão da faixa «Planeamento» escolhido fica com o aspeto do hover."""
    def cartao(id, escolhido=False):
        return (f'<button type="button" id="{id}" class="dmo-linha-cor mch-B1{" selected" if escolhido else ""}" data-dmo-linha-cor="B1" '
                f'data-dmo-row data-dmo-open="#" aria-selected="{"true" if escolhido else "false"}"><strong>B1</strong><span>6018Y232 · 202603</span></button>')
    corpo = ('<div class="app"><aside class="beta-light-rail beta-production-rail" id="plan" aria-label="Planeamento"><div class="beta-production-lines" id="plan-list" data-dmo-list="Planeamento">'
             + cartao('rato') + cartao('escolhido', True) + cartao('parado') + '</div></aside></div>')
    ficheiro = pagina_sintetica(corpo)
    for tema in ('light', 'dark'):
        page = browser.new_page(viewport={'width': 600, 'height': 500}, color_scheme=tema)
        page.goto(ficheiro.as_uri())
        page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
        page.wait_for_load_state('load')
        page.hover('#rato')
        page.wait_for_timeout(500)  # as transições de fundo acabam
        com_rato, escolhido, parado = (page.evaluate(MEDIR, i) for i in ('rato', 'escolhido', 'parado'))
        print(f'Planeamento {tema}: rato {com_rato["fundo"]} · escolhido {escolhido["fundo"]} · parado {parado["fundo"]}')
        if com_rato['fundo'] == parado['fundo']:
            falhas.append(f'Planeamento {tema}: o hover não muda o fundo do cartão ({parado["fundo"]})')
        if escolhido['fundo'] != com_rato['fundo']:
            falhas.append(f'Planeamento {tema}: o cartão escolhido tem outro fundo que o do hover ({escolhido["fundo"]} em vez de {com_rato["fundo"]})')
        if escolhido['letra'] != com_rato['letra']:
            falhas.append(f'Planeamento {tema}: o cartão escolhido tem outra cor de letra que a do hover ({escolhido["letra"]} em vez de {com_rato["letra"]})')
        # No escuro o enchimento clareado com a letra branca fica com ~3:1 (ver a lista de erros da cobertura A1): só se mede no claro.
        print(f'Planeamento {tema}: contraste do escolhido {escolhido["contraste"]:.2f}')
        if tema == 'light' and escolhido['contraste'] < 4.5:
            falhas.append(f'Planeamento {tema}: letra do cartão escolhido com contraste {escolhido["contraste"]:.2f}')
        page.close()


def sumario(browser):
    """DMO-380: a linha inteira do Sumário que abre uma página mostra a mão e enche-se ao passar o rato."""
    def linha(id):
        return (f'<li class="sumario-item mch-B1" id="{id}"><time class="sumario-quando">ter 27/10</time>'
                '<span class="sumario-etiqueta">Job On por ver</span><strong class="sumario-maquina">B1</strong>'
                '<a class="sumario-titulo" href="#abrir">6018Y232 · 202603</a><span class="sumario-detalhe">falta a calote</span><small>Diogo</small></li>')
    corpo = ('<main class="sumario-page"><div class="sumario-grupo sumario-importante sumario-topo" data-sumario-tipo="importante"><h3>Importante</h3><ul>'
             + linha('com-rato') + linha('parada') + '</ul></div></main>')
    ficheiro = pagina_sintetica(corpo, ['css/dmo-sumario.css'])
    luz_do_hover = {}
    for tema in ('light', 'dark'):
        page = browser.new_page(viewport={'width': 800, 'height': 500}, color_scheme=tema)
        page.goto(ficheiro.as_uri())
        page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
        page.wait_for_load_state('load')
        page.wait_for_timeout(200)
        parada = page.evaluate(MEDIR, 'parada')
        if page.evaluate("getComputedStyle(document.getElementById('com-rato')).cursor") != 'pointer':
            falhas.append(f'Sumário {tema}: a linha que abre uma página não mostra a mão')
        page.hover('#com-rato')
        page.wait_for_timeout(500)
        com_rato = page.evaluate(MEDIR, 'com-rato')
        luz_do_hover[tema] = com_rato['luz']
        print(f'Sumário {tema}: parada {parada["fundo"]} (luz {parada["luz"]:.3f}) · rato {com_rato["fundo"]} (luz {com_rato["luz"]:.3f}) · contraste {com_rato["contraste"]:.2f}')
        if com_rato['fundo'] == parada['fundo']:
            falhas.append(f'Sumário {tema}: o hover não muda o fundo da linha')
        if tema == 'light' and com_rato['contraste'] < 4.5:
            falhas.append(f'Sumário {tema}: letra ilegível ao passar o rato (contraste {com_rato["contraste"]:.2f})')
        if com_rato['sombra'] == 'none':
            falhas.append(f'Sumário {tema}: o hover tirou a barra do «Importante»')
        # Claro: a linha escurece com a cor da linha; escuro: um tom mais claro do que a linha parada.
        if tema == 'light' and not com_rato['luz'] < parada['luz']:
            falhas.append(f'Sumário claro: o hover não escurece a linha ({parada["luz"]:.3f} → {com_rato["luz"]:.3f})')
        if tema == 'dark' and not com_rato['luz'] > parada['luz']:
            falhas.append(f'Sumário escuro: o hover não clareia a linha ({parada["luz"]:.3f} → {com_rato["luz"]:.3f})')
        page.close()


def botoes_e_cabecalho(browser):
    """DMO-489: os botões com aspeto de ligação não se sublinham ao passar o rato; o cabeçalho é «Nome · Título» numa linha."""
    corpo = ('<header class="dmo-shell-header"><div class="dmo-shell-conta"><div class="dmo-shell-user admin-user">'
             '<strong data-user-profile-name>Diogo Ribeiro</strong>'
             '<span data-user-profile-title>Chefe de planeamento da oficina de moldes de vidro com um título muito comprido</span></div>'
             '<div class="dmo-shell-acoes"><a class="dmo-shell-logout" href="#" aria-label="Terminar sessão">Sair</a></div></div></header>'
             '<main class="beta-bq-layout"><button type="button" id="sem-jobon" class="bq-sem-jobon-link">Sem Job On</button>'
             '<button type="button" id="historico" class="ferr-cancel ferr-link-botao">Ver o Histórico de produções</button>'
             '<button type="button" id="uso" class="dmo-uso-link">12%</button></main>')
    ficheiro = pagina_sintetica(corpo, ['css/dmo-boquilhas-prototype.css'])
    for tema in ('light', 'dark'):
        page = browser.new_page(viewport={'width': 1280, 'height': 600}, color_scheme=tema)
        page.goto(ficheiro.as_uri())
        page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
        page.wait_for_load_state('load')
        for id in ('sem-jobon', 'historico', 'uso'):
            page.hover(f'#{id}')
            page.wait_for_timeout(200)
            linha = page.evaluate(f"getComputedStyle(document.getElementById('{id}')).textDecorationLine")
            if linha != 'none':
                falhas.append(f'Botões {tema}: «#{id}» fica sublinhado ao passar o rato ({linha})')
        medidas = page.evaluate("""() => {
          const r = s => document.querySelector(s).getBoundingClientRect();
          const nome = r('.dmo-shell-user strong'), titulo = r('.dmo-shell-user span'), conta = r('.dmo-shell-conta'), sair = r('.dmo-shell-logout'), user = r('.dmo-shell-user');
          const t = document.querySelector('.dmo-shell-user span');
          return { mesmaLinha: Math.min(nome.bottom, titulo.bottom) - Math.max(nome.top, titulo.top), alturaNome: nome.height,
                   separador: getComputedStyle(t, '::before').content, reticencias: getComputedStyle(t).textOverflow, cortado: t.scrollWidth - t.clientWidth,
                   largura: user.width, saiPorBaixo: sair.top - user.bottom, saiAoCentro: Math.abs((sair.left + sair.right) / 2 - (conta.left + conta.right) / 2) };
        }""")
        print(f'Cabeçalho {tema}: {medidas}')
        if medidas['mesmaLinha'] < medidas['alturaNome'] / 2:
            falhas.append(f'Cabeçalho {tema}: o nome e o título não estão na mesma linha ({medidas})')
        if '·' not in medidas['separador']:
            falhas.append(f'Cabeçalho {tema}: falta o «·» entre o nome e o título ({medidas["separador"]})')
        if medidas['reticencias'] != 'ellipsis' or medidas['cortado'] <= 0:
            falhas.append(f'Cabeçalho {tema}: o título comprido não corta com reticências ({medidas})')
        if medidas['largura'] > 341:
            falhas.append(f'Cabeçalho {tema}: o nome e o título passam dos 340px ({medidas["largura"]:.0f})')
        if medidas['saiPorBaixo'] < -1 or medidas['saiAoCentro'] > 16:
            falhas.append(f'Cabeçalho {tema}: o «Sair» não fica ao centro por baixo ({medidas})')
        page.close()


# ---------------------------------------------------------------------------------------------------------------------
# A folha do Job On a 1366×768 e a 1290×646
# ---------------------------------------------------------------------------------------------------------------------

CARTOES = ['CM', 'MF', 'BQ', 'AN', 'PU', 'ARR', 'TP', 'CS']
POSICOES = '''(excepto) => Object.fromEntries(%s.filter(f => f !== excepto).map(f => {
  const e = document.querySelector(`[data-jof-familia="${f}"]`); const r = e.getBoundingClientRect();
  let y = r.top; for (let p = e.parentElement; p; p = p.parentElement) y += p.scrollTop;  // posição na folha, sem o deslize interno
  return [f, [Math.round(r.left), Math.round(y), Math.round(r.width), Math.round(r.height)]]; }))''' % CARTOES
SEM_DESLIZAR = '''() => ({ alto: document.documentElement.scrollHeight - window.innerHeight, largo: document.documentElement.scrollWidth - document.documentElement.clientWidth })'''
CABECALHO = '''() => {
  const topo = () => Math.round(document.querySelector('main.jof2 .page-head').getBoundingClientRect().top);
  const antes = topo();
  document.querySelectorAll('.sheet, .sheet-main, .zones, main.jof2, main.jof2 > .wrap').forEach(e => { e.scrollTop = 99999; });
  window.scrollTo(0, 99999);
  return { antes, depois: topo(), scrollY: Math.round(window.scrollY) };
}'''
CALIBRES = '''() => {
  const wrap = document.querySelector('.cal-card .table-wrap').getBoundingClientRect();
  const celulas = [...document.querySelectorAll('table[data-jof-cal] tbody th, table[data-jof-cal] tbody td')];
  const fora = (r, vertical) => r.left < wrap.left - 1 || r.right > wrap.right + 1 || (vertical && (r.top < wrap.top - 1 || r.bottom > wrap.bottom + 1));
  return { calibres: document.querySelectorAll('table[data-jof-cal] tbody th[scope="row"]').length,
           foraH: celulas.filter(c => fora(c.getBoundingClientRect(), false)).length,
           foraV: celulas.filter(c => fora(c.getBoundingClientRect(), true)).length };
}'''
NOTAS = '''() => {
  const p = document.querySelector('[data-jof-familia="CM"] .note-box > p');
  const cs = getComputedStyle(p); const pad = parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
  const linha = parseFloat(cs.lineHeight);
  const ul = document.querySelector('.side > .notes > ul'); const limite = ul.clientHeight;
  return { desliza: p.scrollHeight - p.clientHeight, overflow: cs.overflowY, linhas: Number.isNaN(linha) ? null : (p.clientHeight - pad) / linha,
           gerais: ul.scrollHeight - ul.clientHeight, geraisOverflow: getComputedStyle(ul).overflowY,
           geraisNoFimDeUmaNota: ul.scrollHeight <= ul.clientHeight + 1 || [...ul.children].some(li => Math.abs(li.getBoundingClientRect().bottom - ul.getBoundingClientRect().top - ul.clientTop - limite) <= 1.5) };
}'''
FOTO = '''() => { const f = document.querySelector('.side .jof-artigo').getBoundingClientRect();
  return { razao: f.width / f.height, texto: document.querySelector('.side .card.article').innerText }; }'''
MAIS_DETALHES = '''() => { const b = document.querySelector('[data-jof-tecnica="CM"] .more-body'); const r = b.getBoundingClientRect();
  return { posicao: getComputedStyle(b).position, altura: Math.round(r.height), aberta: document.querySelector('[data-jof-tecnica="CM"]').open }; }'''
IMPRIMIR_MEDIDAS = '''() => [...document.querySelectorAll('.jof-impressao-print .jfi-folha')].map(f => {
  let menor = 999;
  for (const e of f.querySelectorAll('*')) {
    if (![...e.childNodes].some(n => n.nodeType === 3 && n.nodeValue.trim())) continue;
    menor = Math.min(menor, parseFloat(getComputedStyle(e).fontSize));
  }
  return { folha: f.getAttribute('data-jof-folha'), cortada: Math.max(f.scrollHeight - f.clientHeight, f.scrollWidth - f.clientWidth), menorPt: menor * 0.75 };
})'''


def mudou(antes, depois):
    return {f: (antes[f], depois[f]) for f in antes if max(abs(a - b) for a, b in zip(antes[f], depois[f])) > 1}


def folha(browser, fixtures):
    paginas = {nome: sem_selo_local((fixtures / ficheiro).read_text(encoding='utf-8')) for nome, ficheiro in
               (('ver', 'cobertura-a1-folha.html'), ('editar', 'cobertura-a1-folha-editar.html'), ('imprimir', 'cobertura-a1-imprimir.html'))}
    atual = {'pagina': 'ver'}

    def servir(route):
        url = urlparse(route.request.url)
        if url.path == '/JobOn/Folha':
            route.fulfill(content_type='text/html', body=paginas[atual['pagina']])
            return
        alvo = (www / unquote(url.path).lstrip('/')).resolve()
        if alvo.is_relative_to(www) and alvo.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
        else:
            route.fulfill(status=404, body='')

    for largura, altura in ((1366, 768), (1290, 646)):
        nome = f'{largura}×{altura}'
        context = browser.new_context(viewport={'width': largura, 'height': altura}, locale='pt-PT', timezone_id='Europe/Lisbon')
        context.route('**/*', servir)
        page = context.new_page()
        erros = []
        page.on('pageerror', lambda e: erros.append(str(e)))

        for modo in ('ver', 'editar'):
            atual['pagina'] = modo
            page.goto('http://dmo.test/JobOn/Folha?jobon_id=1' + ('&editar=1' if modo == 'editar' else ''))
            page.wait_for_load_state('load')
            page.wait_for_timeout(300)
            desliza = page.evaluate(SEM_DESLIZAR)
            if desliza['alto'] > 1 or desliza['largo'] > 1:
                falhas.append(f'{nome} {modo}: a página desliza ({desliza})')
            cab = page.evaluate(CABECALHO)
            if abs(cab['antes'] - cab['depois']) > 1 or cab['scrollY'] > 0:
                falhas.append(f'{nome} {modo}: o cabeçalho não ficou fixo ao deslizar ({cab})')
            page.evaluate("window.scrollTo(0, 0); document.querySelectorAll('.sheet, .sheet-main, .zones').forEach(e => { e.scrollTop = 0; })")

        # Ver: os calibres, as notas, a foto, a nota aberta e a Informação técnica.
        atual['pagina'] = 'ver'
        page.goto('http://dmo.test/JobOn/Folha?jobon_id=1')
        page.wait_for_load_state('load')
        page.wait_for_timeout(300)
        cal = page.evaluate(CALIBRES)
        print(f'{nome} calibres: {cal}')
        if cal['calibres'] != 8:
            falhas.append(f'{nome}: a folha de teste não tem os oito calibres ({cal})')
        if altura >= 720 and cal['foraH'] > 0:  # a 1290×646: pendentes_a1_browser.py
            falhas.append(f'{nome}: há calibres fora do cartão, para o lado ({cal})')
        if altura >= 720 and cal['foraV'] > 0:
            falhas.append(f'{nome}: os oito calibres não ficam todos à vista no cartão ({cal})')

        foto = page.evaluate(FOTO)
        if abs(foto['razao'] - 0.75) > 0.03:
            falhas.append(f'{nome}: a foto do artigo não é 3:4 ({foto["razao"]:.2f})')
        if '9395T194' in foto['texto']:
            falhas.append(f'{nome}: o cartão do artigo repete a referência ({foto["texto"]!r})')

        if altura >= 720:
            notas = page.evaluate(NOTAS)
            print(f'{nome} notas: {notas}')
            if notas['desliza'] <= 1 or notas['overflow'] not in ('auto', 'scroll'):
                falhas.append(f'{nome}: a nota comprida da CM não desliza por dentro ({notas})')
            if notas['linhas'] is not None and abs(notas['linhas'] - round(notas['linhas'])) > 0.15:
                falhas.append(f'{nome}: a nota da CM acaba a meio de uma linha ({notas["linhas"]:.2f} linhas)')
            if notas['gerais'] > 1 and not notas['geraisNoFimDeUmaNota']:
                falhas.append(f'{nome}: as Notas gerais acabam a meio de uma nota ({notas})')

        # DMO-509: a nota aberta cai por cima, sem empurrar os outros cartões.
        antes = page.evaluate(POSICOES, 'CM')
        altura_nota = page.evaluate("document.querySelector('[data-jof-familia=\"CM\"] [data-jof-nota]').getBoundingClientRect().height")
        page.locator('[data-jof-familia="CM"] [data-jof-nota]').click()
        page.wait_for_timeout(150)
        depois = page.evaluate(POSICOES, 'CM')
        ampliada = page.evaluate("document.querySelector('[data-jof-familia=\"CM\"] [data-jof-nota]').getBoundingClientRect().height")
        if mudou(antes, depois):
            falhas.append(f'{nome}: a nota aberta empurrou outros cartões ({mudou(antes, depois)})')
        if altura >= 720 and ampliada <= altura_nota + 1:  # a 1290×646: pendentes_a1_browser.py
            falhas.append(f'{nome}: a nota não ficou maior ao abrir ({altura_nota:.0f} → {ampliada:.0f})')
        page.keyboard.press('Escape')
        page.wait_for_timeout(150)
        if mudou(antes, page.evaluate(POSICOES, 'CM')):
            falhas.append(f'{nome}: depois de fechar a nota, os cartões não voltaram ao sítio')

        # DMO-562: na folha (Ver) a Informação técnica abre para baixo e empurra os cartões de baixo, sem os tapar.
        antes_cm = page.evaluate(POSICOES, '')
        page.locator('[data-jof-tecnica="CM"] summary').click()
        page.wait_for_timeout(150)
        aberta = page.evaluate(MAIS_DETALHES)
        if not aberta['aberta'] or aberta['altura'] < 20:
            falhas.append(f'{nome}: a Informação técnica não abriu ({aberta})')
        depois_cm = page.evaluate(POSICOES, '')
        if not mudou(antes_cm, depois_cm):
            falhas.append(f'{nome}: a Informação técnica aberta não empurrou os cartões de baixo, tapa-os ({aberta})')
        sobrepoe = page.evaluate("""() => { const cm = document.querySelector('[data-jof-familia="CM"]').getBoundingClientRect();
          const tp = document.querySelector('[data-jof-familia="TP"]').getBoundingClientRect();
          return Math.round(cm.bottom - tp.top); }""")
        if sobrepoe > 1:
            falhas.append(f'{nome}: a Informação técnica aberta sobrepõe-se ao cartão de baixo ({sobrepoe}px)')
        page.keyboard.press('Escape')

        if erros:
            falhas.append(f'{nome}: erros de JavaScript {erros}')
        context.close()

    # DMO-476: o Imprimir. A caixa das folhas mede-se no ecrã (como o JS) e vê-se em papel (print).
    context = browser.new_context(viewport={'width': 1366, 'height': 768}, locale='pt-PT', timezone_id='Europe/Lisbon')
    context.route('**/*', servir)
    page = context.new_page()
    atual['pagina'] = 'imprimir'
    page.goto('http://dmo.test/JobOn/Folha?jobon_id=1')
    page.wait_for_load_state('load')
    page.evaluate("window.__impressoes = 0; window.print = () => { window.__impressoes++; }; window.dmoJobOnImprimir(1, 'todas');")
    if page.evaluate('window.__impressoes') != 1:
        falhas.append('Imprimir: o botão não chamou window.print()')
    page.emulate_media(media='print')
    folhas = page.evaluate(IMPRIMIR_MEDIDAS)
    print(f'Imprimir: {folhas}')
    if [f['folha'] for f in folhas] != ['moldes', 'moldes', 'equipa', 'ficha']:
        falhas.append(f'Imprimir: as folhas não são as quatro da fábrica ({[f["folha"] for f in folhas]})')
    for f in folhas:
        if f['cortada'] > 1:
            falhas.append(f'Imprimir: a folha «{f["folha"]}» tem texto que não cabe no A4 ({f["cortada"]:.0f}px a mais)')
        if f['menorPt'] < 8.4:
            falhas.append(f'Imprimir: a folha «{f["folha"]}» tem letra de {f["menorPt"]:.1f}pt, abaixo dos 8,5pt')
    context.close()


CONTRASTE = r"""
const rgba = s => { const m = s.match(/[\d.]+/g).map(Number); const k = s.startsWith('color(') ? 255 : 1;
  return [m[0] * k, m[1] * k, m[2] * k, m.length > 3 ? m[3] : 1]; };
const fundo = e => { const camadas = []; for (; e; e = e.parentElement) { const c = rgba(getComputedStyle(e).backgroundColor); if (c[3] > 0) camadas.push(c); if (c[3] >= 1) break; }
  let r = [255, 255, 255]; for (const c of camadas.reverse()) r = r.map((v, i) => v * (1 - c[3]) + c[i] * c[3]); return r; };
const L = c => { const [r, g, b] = c.map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
"""
CAMPOS_NO_ESCURO = '() => {' + CONTRASTE + r"""
  const campos = [...document.querySelectorAll('.jof2 input, .jof2 textarea, .jof2 select')].filter(e => {
    const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0 && !['hidden', 'checkbox', 'radio', 'file'].includes(e.type) && !e.disabled && !e.readOnly && !e.closest('details:not([open])'); });
  return campos.map(e => { const bg = fundo(e); const t = rgba(getComputedStyle(e).color);
    const fg = bg.map((v, i) => v * (1 - t[3]) + t[i] * t[3]); const a = L(fg), b = L(bg);
    const r = e.getBoundingClientRect(); const cartao = e.closest('.card, .sec, article, .fcol') || document.body; const c = cartao.getBoundingClientRect();
    return { nome: e.name || e.id || e.tagName, contraste: (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05), largura: r.width, passa: Math.max(0, r.right - c.right) }; });
}"""
REPARADOR = """() => [...document.querySelectorAll('.jof-onde-reparador')].map(r => {
  const p = r.closest('.jof-onde'); const pr = p.getBoundingClientRect(); const rr = r.getBoundingClientRect();
  const linha = parseFloat(getComputedStyle(p).lineHeight) || parseFloat(getComputedStyle(p).fontSize) * 1.3;
  return { altura: pr.height, linha, desvio: Math.abs(rr.top - pr.top), dentro: rr.bottom <= pr.bottom + 1 }; })"""
USO_E_AVISO = """() => {
  const cartao = f => document.querySelector(`[data-jof-familia="${f}"]`);
  const aviso = document.createElement('div'); aviso.className = 'jobon-aviso warn'; aviso.setAttribute('role', 'status'); aviso.setAttribute('data-dmo-aviso-armazem', '');
  aviso.textContent = 'Esta ferramenta ainda não tem registo no Armazém. Registar na ferramenta';
  const cm = cartao('CM'); (cm.querySelector('.jof-tool-slot') || cm).appendChild(aviso);
  const topo = f => cartao(f).querySelector('.use-top b').getBoundingClientRect().top;
  const barra = cm.querySelector('.use .bar, .bar');
  return { usoCm: topo('CM'), usoMf: topo('MF'), avisoTop: aviso.getBoundingClientRect().top, barraBottom: barra.getBoundingClientRect().bottom,
           avisoNoCartao: cm.getBoundingClientRect().bottom + 1 >= aviso.getBoundingClientRect().bottom }; }"""


def aspeto_da_folha(browser, fixtures):
    """DMO-468, 570, 409 e 537 na folha exportada (a 1366x768, a 125% de zoom e no escuro)."""
    paginas = {'ver': sem_selo_local((fixtures / 'cobertura-a1-folha.html').read_text(encoding='utf-8')),
               'editar': sem_selo_local((fixtures / 'cobertura-a1-folha-editar.html').read_text(encoding='utf-8'))}
    atual = {'pagina': 'ver'}

    def servir(route):
        url = urlparse(route.request.url)
        if url.path == '/JobOn/Folha':
            route.fulfill(content_type='text/html', body=paginas[atual['pagina']])
            return
        alvo = (www / unquote(url.path).lstrip('/')).resolve()
        if alvo.is_relative_to(www) and alvo.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
        else:
            route.fulfill(status=404, body='')

    def abrir(largura, altura, modo, tema='light'):
        context = browser.new_context(viewport={'width': largura, 'height': altura}, locale='pt-PT', timezone_id='Europe/Lisbon', color_scheme=tema)
        context.route('**/*', servir)
        page = context.new_page()
        atual['pagina'] = modo
        page.goto('http://dmo.test/JobOn/Folha?jobon_id=1' + ('&editar=1' if modo == 'editar' else ''))
        page.wait_for_load_state('load')
        page.evaluate(f"document.documentElement.setAttribute('data-theme','{tema}')")
        page.wait_for_timeout(300)
        return context, page

    for largura, altura in ((1366, 768), (1290, 646)):
        nome = f'{largura}×{altura}'
        # DMO-468: o reparador fica na mesma linha do estado, sem alargar nem alongar o cartão.
        context, page = abrir(largura, altura, 'ver')
        for r in page.evaluate(REPARADOR):
            if r['altura'] > r['linha'] * 1.7 or r['desvio'] > r['linha'] or not r['dentro']:
                falhas.append(f'{nome}: o reparador não fica na linha do estado ({r})')
        if not page.evaluate(REPARADOR):
            falhas.append(f'{nome}: a folha de teste não tem reparador')
        tops = page.evaluate("() => ['CM', 'MF'].map(f => document.querySelector(`[data-jof-familia=\"${f}\"]`).getBoundingClientRect().top)")
        if abs(tops[0] - tops[1]) > 1:
            falhas.append(f'{nome}: os cartões CM e MF não começam à mesma altura ({tops})')
        # DMO-570: o aviso fica debaixo da Utilização e o % de uso não desce por causa dele.
        m = page.evaluate(USO_E_AVISO)
        print(f'{nome} aviso e uso: {m}')
        if abs(m['usoCm'] - m['usoMf']) > 1:
            falhas.append(f'{nome}: o % de uso da CM e da MF não estão à mesma altura com o aviso ({m})')
        if m['avisoTop'] < m['barraBottom'] - 1:
            falhas.append(f'{nome}: o aviso não fica debaixo da Utilização ({m})')
        context.close()

        # DMO-537: no escuro, a letra dos campos lê-se e os campos cabem no cartão.
        context, page = abrir(largura, altura, 'editar', 'dark')
        campos = page.evaluate(CAMPOS_NO_ESCURO)
        if not campos:
            falhas.append(f'{nome}: o Editar não tem campos para medir')
        for c in campos:
            if c['contraste'] < 4.5:
                falhas.append(f'{nome} escuro: a letra do campo «{c["nome"]}» tem contraste {c["contraste"]:.2f}')
            if c['largura'] < 24 or c['passa'] > 1:
                falhas.append(f'{nome}: o campo «{c["nome"]}» não cabe no cartão ({c["largura"]:.0f}px, passa {c["passa"]:.0f}px)')
        context.close()

    # DMO-409: com zoom de 125% (1366 px viram 1093x614), a folha continua sem deslizar e com os oito calibres.
    for modo in ('ver', 'editar'):
        context, page = abrir(1093, 614, modo)
        d = page.evaluate(SEM_DESLIZAR)
        if d['largo'] > 1:
            falhas.append(f'zoom 125% {modo}: a página desliza para o lado ({d})')
        sai = page.evaluate("() => [...document.querySelectorAll('[data-jof-familia=\"PU\"], [data-jof-familia=\"ARR\"]')].map(e => e.scrollWidth - e.clientWidth)")
        if any(x > 1 for x in sai):
            falhas.append(f'zoom 125% {modo}: a coluna do PU/ARR tem conteúdo a sair dos lados ({sai})')
        if modo == 'ver':
            cal = page.evaluate(CALIBRES)
            if cal['foraH'] > 0:
                falhas.append(f'zoom 125%: há calibres fora do cartão, para o lado ({cal})')
        context.close()


def painel_e_pegamentos(browser, fixtures):
    """DMO-485: a 1366x768 o painel de apoio cabe no ecrã e o botão «Registar» dos Pegamentos fica à vista."""
    html = sem_selo_local((fixtures / 'cobertura-a1-controlo-resumo.html').read_text(encoding='utf-8'))

    def servir(route):
        url = urlparse(route.request.url)
        if url.path == '/Controlo/Resumo':
            route.fulfill(content_type='text/html', body=html)
            return
        alvo = (www / unquote(url.path).lstrip('/')).resolve()
        if alvo.is_relative_to(www) and alvo.is_file():
            route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
        else:
            route.fulfill(status=404, body='')

    for largura, altura in ((1366, 768), (1290, 646)):
        nome = f'{largura}×{altura}'
        context = browser.new_context(viewport={'width': largura, 'height': altura}, locale='pt-PT', timezone_id='Europe/Lisbon')
        context.route('**/*', servir)
        page = context.new_page()
        page.goto('http://dmo.test/Controlo/Resumo?jobon_id=1')
        page.wait_for_load_state('load')
        page.wait_for_timeout(300)
        d = page.evaluate(SEM_DESLIZAR)
        if d['largo'] > 1:
            falhas.append(f'{nome}: o Resumo desliza para o lado ({d})')
        botao = page.locator('a[aria-label="Registar os Pegamentos"]')
        botao.scroll_into_view_if_needed()
        r = botao.bounding_box()
        print(f'{nome} botão Pegamentos: {r}')
        if not r or r['width'] < 40 or not (0 <= r['x'] and r['x'] + r['width'] <= largura and 0 <= r['y'] and r['y'] + r['height'] <= altura):
            falhas.append(f'{nome}: o botão «Registar» dos Pegamentos não fica à vista ({r})')
        page.evaluate("document.getElementById('dmoSupport').showModal()")
        page.wait_for_timeout(900)
        p = page.evaluate("""() => { const d = document.getElementById('dmoSupport').getBoundingClientRect();
          const f = document.querySelector('#dmoSupport [data-dmo-painel-fechar]').getBoundingClientRect();
          const c = document.querySelector('#dmoSupport .dmo-painel-corpo').getBoundingClientRect();
          return { esq: d.left, dir: d.right, topo: d.top, base: d.bottom, fecharDentro: f.top >= d.top - 1 && f.right <= d.right + 1 && f.width > 0, corpo: c.height }; }""")
        print(f'{nome} painel de apoio: {p}')
        if p['esq'] < -1 or p['dir'] > largura + 1 or p['topo'] < -1 or p['base'] > altura + 1:
            falhas.append(f'{nome}: o painel de apoio não cabe no ecrã ({p})')
        if not p['fecharDentro'] or p['corpo'] < 150:
            falhas.append(f'{nome}: o painel de apoio perdeu o «Fechar» ou o corpo ({p})')
        context.close()


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    planeamento(browser)
    sumario(browser)
    botoes_e_cabecalho(browser)
    if len(sys.argv) > 1:
        folha(browser, Path(sys.argv[1]).resolve())
        aspeto_da_folha(browser, Path(sys.argv[1]).resolve())
        painel_e_pegamentos(browser, Path(sys.argv[1]).resolve())
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Cobertura A1 no browser: ok')

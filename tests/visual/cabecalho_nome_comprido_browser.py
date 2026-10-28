"""O cabeçalho com um nome comprido no Chromium (auditoria do Codex de 28/10/2026, C02).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~JobOnFolhaPrimeiraFilaTests'
  python tests/visual/cabecalho_nome_comprido_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

Com os seis módulos no menu e «TESTE CODEX completo A · TESTE CODEX completo» na conta, a 1290 × 646
(o ecrã do Diogo), 1366 × 768 e 1024 × 646, em claro e em escuro: a conta fica na fila dos módulos, o
cabeçalho tem a mesma altura que com um nome curto e a folha do Job On não ganha scroll na página.
O nome fica inteiro; o título corta com reticências quando não cabe.
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
MODULOS = ''.join(f'<a href="/{m}">{m}</a> ' for m in ('Controlo', 'Boquilhas', 'Ferramentas', 'Armazém', 'Reparação Interna'))
folha = sem_selo_local((fixtures / 'jobon-folha.html').read_text(encoding='utf-8'))
assert '<a href="/Definicoes"' in folha, 'a folha exportada não tem o «Definições» no menu'
folha = folha.replace('<a href="/Definicoes"', MODULOS + '<a href="/Definicoes"', 1)

CONTA = '''([nome, titulo]) => {
    const n = document.querySelector('[data-user-profile-name]'); n.textContent = nome;
    let t = document.querySelector('[data-user-profile-title]');
    if (!t) { t = document.createElement('span'); t.setAttribute('data-user-profile-title', ''); n.after(t); }
    t.textContent = titulo; }'''
MEDIR = '''() => {
    const caixa = s => document.querySelector(s).getBoundingClientRect();
    const nome = document.querySelector('[data-user-profile-name]');
    return { cabecalho: caixa('.dmo-shell-header').height, conta: caixa('.dmo-shell-conta').top, menu: caixa('.dmo-primary-nav > a').top,
             direita: caixa('.dmo-shell-conta').right, scroll: document.scrollingElement.scrollHeight - document.scrollingElement.clientHeight,
             nomeInteiro: nome.scrollWidth <= nome.clientWidth + 0.5 }; }'''


def servir(route):
    url = urlparse(route.request.url)
    if url.path == '/JobOn/Folha':
        route.fulfill(content_type='text/html', body=folha)
        return
    alvo = (assets / unquote(url.path).lstrip('/')).resolve()
    if alvo.is_relative_to(assets) and alvo.is_file():
        route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
    else:
        route.fulfill(status=404, body='')


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura, altura in ((1290, 646), (1366, 768), (1024, 646)):
        for tema in ('light', 'dark'):
            medidas = {}
            for caso, conta in (('curto', ['Diogo', 'Chefe']), ('comprido', ['TESTE CODEX completo A', 'TESTE CODEX completo'])):
                context = browser.new_context(viewport={'width': largura, 'height': altura})
                page = context.new_page()
                erros = []
                page.on('pageerror', lambda erro: erros.append(str(erro)))
                context.route('**/*', servir)
                page.goto('http://dmo.test/JobOn/Folha')
                page.evaluate(f"document.documentElement.dataset.theme = '{tema}'")
                page.evaluate(CONTA, conta)
                medidas[caso] = page.evaluate(MEDIR)
                assert not erros, erros
                context.close()
            onde = f'{largura}×{altura} {tema}'
            curto, comprido = medidas['curto'], medidas['comprido']
            assert abs(comprido['conta'] - curto['conta']) < 1, f'{onde}: a conta com o nome comprido mudou de fila ({curto} / {comprido})'
            assert abs(comprido['cabecalho'] - curto['cabecalho']) < 1, f'{onde}: o cabeçalho cresceu com o nome comprido ({curto} / {comprido})'
            assert comprido['direita'] <= largura + 0.5, f'{onde}: a conta sai do ecrã ({comprido})'
            assert comprido['nomeInteiro'], f'{onde}: o nome ficou cortado ({comprido})'
            assert comprido['scroll'] <= curto['scroll'], f'{onde}: a página ganhou scroll com o nome comprido ({curto} / {comprido})'
            if (largura, altura) == (1290, 646):
                assert comprido['scroll'] <= 0, f'{onde}: a folha desliza {comprido["scroll"]} px'
    browser.close()
print('cabeçalho com nome comprido: passaram')

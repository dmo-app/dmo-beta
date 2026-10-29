"""A lista de Movimentos das Boquilhas a 1290×646 e 1366×768 (auditoria do Codex de 28/10/2026, C03).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~BoquilhasRegistoPageTests.SoVer'
  python tests/visual/bq_historico_lista_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

Boquilhas > Movimentos (vista «Movimentos detalhados»): a lista fica sempre com espaço para
o cabeçalho e pelo menos uma linha completa, mesmo quando o painel das linhas de produção e
os filtros ocupam mais espaço (o relatório mediu só 4 px úteis a 1290×646). A página não desliza.
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
pagina = sem_selo_local((fixtures / 'boquilhas-ver.html').read_text(encoding='utf-8'))
assert 'bqHistLista' in pagina, 'a página exportada não tem a vista de Movimentos'

# Mais linhas de produção no painel, para forçar o caso em que ele cresce mais do que na
# captura de um só lote (o relatório confirmou o problema com dados reais mais cheios).
MAIS_LINHAS = '''() => {
    const lista = document.getElementById('bqCurrentLines-list');
    if (!lista || !lista.firstElementChild) return;
    for (let i = 0; i < 5; i++) lista.appendChild(lista.firstElementChild.cloneNode(true));
}'''
MEDIR = '''() => {
    const wrap = document.querySelector('#bqHistLista .movement-card .dmo-table-wrap');
    const thead = wrap && wrap.querySelector('thead');
    const linha = wrap && wrap.querySelector('tbody tr');
    if (!wrap) return null;
    return { altura: wrap.getBoundingClientRect().height,
             cabecalhoVisivel: thead ? thead.getBoundingClientRect().height > 0 : false,
             linhaInteira: linha ? (linha.getBoundingClientRect().bottom <= wrap.getBoundingClientRect().bottom + 0.5) : null,
             scroll: document.scrollingElement.scrollHeight - document.scrollingElement.clientHeight };
}'''


def servir(route):
    url = urlparse(route.request.url)
    if url.path == '/Boquilhas':
        route.fulfill(content_type='text/html', body=pagina)
        return
    alvo = (assets / unquote(url.path).lstrip('/')).resolve()
    if alvo.is_relative_to(assets) and alvo.is_file():
        route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or 'application/octet-stream', body=alvo.read_bytes())
    else:
        route.fulfill(status=404, body='')


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for largura, altura in ((1290, 646), (1366, 768), (1100, 646)):
        for tema in ('light', 'dark'):
            for caso in ('normal', 'mais_linhas'):
                context = browser.new_context(viewport={'width': largura, 'height': altura})
                page = context.new_page()
                erros = []
                page.on('pageerror', lambda erro: erros.append(str(erro)))
                context.route('**/*', servir)
                page.goto('http://dmo.test/Boquilhas')
                page.evaluate(f"document.documentElement.dataset.theme = '{tema}'")
                if caso == 'mais_linhas':
                    page.evaluate(MAIS_LINHAS)
                medida = page.evaluate(MEDIR)
                onde = f'{largura}×{altura} {tema} {caso}'
                assert medida is not None, f'{onde}: sem a lista de movimentos'
                assert medida['altura'] >= 60, f'{onde}: lista com só {medida["altura"]} px úteis'
                assert medida['cabecalhoVisivel'], f'{onde}: o cabeçalho da tabela não aparece'
                assert medida['linhaInteira'] is not False, f'{onde}: a linha fica cortada'
                assert medida['scroll'] <= 0, f'{onde}: a página desliza {medida["scroll"]} px'
                assert not erros, erros
                context.close()
    browser.close()
print('lista de Movimentos das Boquilhas: passaram')

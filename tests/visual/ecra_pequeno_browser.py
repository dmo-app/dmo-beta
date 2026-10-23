"""O Peso e os Pegamentos num ecrã pequeno, no Chromium (teste de uso real de 22/10/2026).

HTML exportado pelos testes HTTP reais (DMO_UI_EVIDENCE_DIR):
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter 'FullyQualifiedName~ToolScopedFlow_CreateRecordConsult_ThroughThePage|FullyQualifiedName~PostMeasurement_CreatesRegistro_PRG'
  python tests/visual/ecra_pequeno_browser.py <pasta>
Só ficheiros locais; nada vai ao DEV.

A 360, 768 e 1400 px, em claro e em escuro: a página não tem scroll lateral, o
«Sair» do topo cabe no ecrã e as tabelas largas deslizam dentro da sua caixa.
No Peso, o registo da leitura fica fixo à direita e à vista ao descer (no fluxo abaixo de 600 px).
"""
import mimetypes
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

fixtures = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'

FOLHAS = [('peso-folha.html', '/Controlo/Peso'), ('pegamentos-folha.html', '/Controlo/Pegamentos')]
SCROLL_LATERAL = 'document.documentElement.scrollWidth - document.documentElement.clientWidth'
FORA_DO_ECRA = '''() => [...document.querySelectorAll('.dmo-shell-header > *, .dmo-secondary-nav > *, main .dmo-card-topo-acoes')]
    .filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && (r.left < 0 || r.right > innerWidth + 0.5); })
    .map(e => e.className + ' ' + Math.round(e.getBoundingClientRect().right))'''
TABELAS = '''() => [...document.querySelectorAll('main .dmo-table-wrap')].map(w => {
    const r = w.getBoundingClientRect(); return { dentro: r.right <= innerWidth + 0.5, desliza: getComputedStyle(w).overflowX }; })'''

with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for nome, caminho in FOLHAS:
        for largura in (360, 768, 1400):
            for tema in ('light', 'dark'):
                context = browser.new_context(viewport={'width': largura, 'height': 900})
                page = context.new_page()
                erros = []
                page.on('pageerror', lambda erro: erros.append(str(erro)))

                def serve(route):
                    url = urlparse(route.request.url)
                    if route.request.method == 'GET' and url.path == caminho:
                        route.fulfill(content_type='text/html', body=sem_selo_local((fixtures / nome).read_text()))
                        return
                    asset = (assets / unquote(url.path).lstrip('/')).resolve()
                    if asset.is_relative_to(assets) and asset.is_file():
                        route.fulfill(content_type=mimetypes.guess_type(str(asset))[0] or 'application/octet-stream', body=asset.read_bytes())
                    else:
                        route.fulfill(status=404, body='')

                context.route('**/*', serve)
                page.goto('http://dmo.test' + caminho)
                page.evaluate(f"document.documentElement.dataset.theme = '{tema}'")
                onde = f'{nome} {largura}px {tema}'
                assert page.evaluate(SCROLL_LATERAL) <= 0, f'{onde}: a página tem scroll lateral ({page.evaluate(SCROLL_LATERAL)} px)'
                fora = page.evaluate(FORA_DO_ECRA)
                assert not fora, f'{onde}: saem do ecrã {fora}'
                tabelas = page.evaluate(TABELAS)
                assert tabelas, f'{onde}: sem tabelas'
                for tabela in tabelas:
                    assert tabela['dentro'] and tabela['desliza'] in ('auto', 'scroll'), f'{onde}: tabela {tabela}'
                if nome == 'peso-folha.html':
                    # O registo da leitura fica fixo à direita e à vista ao descer (Diogo, 23/10/2026);
                    # abaixo de 600 px volta ao fluxo normal.
                    registo = '.peso-leitura-nova'
                    posicao = page.eval_on_selector(registo, 'e => getComputedStyle(e).position')
                    if largura <= 600:
                        assert posicao == 'static', f'{onde}: o registo devia estar no fluxo ({posicao})'
                    else:
                        assert posicao == 'fixed', f'{onde}: o registo devia estar fixo ({posicao})'
                        for descer in (0, 600, 100000):
                            page.evaluate(f'window.scrollTo(0, {descer})')
                            caixa = page.eval_on_selector(registo, 'e => { const r = e.getBoundingClientRect(); return [r.top, r.bottom, r.right]; }')
                            assert 0 <= caixa[0] and caixa[1] <= 900 + 0.5 and caixa[2] <= largura + 0.5, f'{onde}: registo fora do ecrã ao descer {descer}: {caixa}'
                assert not erros, erros
                context.close()
    browser.close()
print('ecrã pequeno: passaram')

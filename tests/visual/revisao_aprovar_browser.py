"""E8 (Atualizar só o que muda): na Revisão, «Aprovar» e «Não aprovar» gravam sem recarregar.

Ambiente local descartável (scripts/stress/arrancar_app.sh <base> <porta>), nunca o DEV.
Para um controlo por aprovar com Peso (e o link do mcaliper, que antes obrigava a abrir a
página inteira), com o filtro «Pendente», a 1400 e 390 px:
- «Aprovar» com um duplo clique: 1 POST, nenhum pedido «document» (a marca no window fica);
- o selo diz «Aprovado», o botão «Reaprovar», o controlo sai da lista (o filtro tira-o) e a
  base tem a aprovação;
- o que está fora das zonas é o mesmo elemento, no mesmo sítio; o foco fica no botão;
- «Não aprovar» sem nota não envia nada; com nota, 1 POST, «Não aprovado», a nota e o
  foco no botão;
- o copiar do link do mcaliper do cartão novo funciona.

Uso: DMO_UI_TEST_URL=http://127.0.0.1:5370 DMO_UI_TEST_DB=dmo_e8 DMO_CHROMIUM=... \
     python3 tests/visual/revisao_aprovar_browser.py
"""
import os
import subprocess
from urllib.parse import urlparse

from playwright.sync_api import expect, sync_playwright

base = os.environ.get('DMO_UI_TEST_URL', 'http://127.0.0.1:5370')
if urlparse(base).hostname not in ('127.0.0.1', 'localhost'):
    raise SystemExit('Use only the local disposable stress environment.')
base_dados = os.environ.get('DMO_UI_TEST_DB', 'dmo_e8')
if 'supabase' in base_dados.lower():
    raise SystemExit('Só uma base local.')
operador = os.environ.get('STRESS_EMAIL', 'stress@local.test')
password = os.environ.get('STRESS_PASSWORD', 'stress-1234')


def sql(consulta):
    return subprocess.run(['psql', '-h', 'localhost', '-U', 'postgres', '-d', base_dados, '-Atc', consulta],
                          env={**os.environ, 'PGPASSWORD': os.environ.get('PGPASSWORD', 'postgres')},
                          capture_output=True, text=True, check=True).stdout.strip()


def por_aprovar():
    """Dois controlos com Peso datado e um link do mcaliper na CM (os mais recentes), postos
    outra vez por aprovar (só na base local), para o teste se poder repetir."""
    linhas = sql('''select c."JobOnId", c."Id", to_char(p."RegistadoEmUtc" at time zone 'Europe/Lisbon', 'YYYY-MM-DD')
        from "Controlos" c join "Pesos" p on p."ControloId" = c."Id"
        join "CmContexts" cm on cm."JobOnId" = c."JobOnId"
        join "ToolTechnicalContexts" t on t."ToolId" = cm."ToolId" and t."McaliperUrl" is not null
        where p."RegistadoEmUtc" is not null
        order by p."RegistadoEmUtc" desc limit 2''')
    controlos = [linha.split('|') for linha in linhas.splitlines()]
    for _, controlo_id, _ in controlos:
        sql(f'''update "Controlos" set "ApprovedAtUtc" = null, "ApprovedByOperatorId" = null, "NotApprovedAtUtc" = null,
            "NotApprovedByOperatorId" = null, "ReviewNote" = null where "Id" = {controlo_id}''')
    return controlos


def caixa(page, seletor):
    return tuple(page.eval_on_selector(seletor, '''(e) => {
        const b = e.getBoundingClientRect();
        return [b.x + window.scrollX, b.y + window.scrollY, b.width, b.height].map(Math.round);
    }'''))


FIXOS = ('#approve-title', '#revisao-procura', '#revisao-estado', '#revisaoDia > h2')
# Zonas por cima do controlo que trocam mas não mudam de sítio na página.
PAGINA = ('#dmoCalendario',)
# Desenhados de novo (zonas), mas à mesma altura do ecrã (o cartão pode crescer para
# baixo com «Aprovado por …» e a nota).
SITIO = ('#revisaoDetalhe', '#revisaoAprovar-peso')


def canto(page, seletor):
    """A altura no ecrã (a largura do botão muda: «Aprovar» passa a «Reaprovar»). Numa coluna
    a lista está por cima do controlo e perde a linha que o filtro tira: a página desliza o
    mesmo, e o controlo fica onde estava no ecrã."""
    return round(page.eval_on_selector(seletor, '(e) => e.getBoundingClientRect().top'))


def gravar(page, pedidos, botao, nome):
    # O clique do Playwright desliza até ao botão: desliza-se antes de medir.
    page.locator(botao).scroll_into_view_if_needed()
    page.evaluate('''(fixos) => {
        window.__marca = 1;
        window.__trocou = 0;
        window.__fixos = fixos.map((s) => document.querySelector(s));
        document.addEventListener('dmo:troca', () => { window.__trocou++; });
    }''', list(FIXOS))
    antes = [caixa(page, s) for s in FIXOS]
    sitio = [canto(page, s) for s in SITIO]
    pagina = [caixa(page, s)[:2] for s in PAGINA]
    del pedidos[:]
    page.locator(botao).dblclick()  # o duplo clique: um envio só
    page.wait_for_function('window.__trocou >= 1', timeout=10000)
    page.wait_for_function("!document.querySelector('[data-dmo-troca][aria-busy=true]')", timeout=10000)
    page.wait_for_timeout(300)
    assert page.evaluate('window.__marca') == 1, f'{nome}: a página recarregou'
    posts = [p for p in pedidos if p[0] == 'POST']
    documentos = [p for p in pedidos if p[1] == 'document']
    assert len(posts) == 1, f'{nome}: {len(posts)} POST: {pedidos}'
    assert not documentos, f'{nome}: abriu a página: {documentos}'
    assert page.evaluate('(fixos) => fixos.every((s, i) => document.querySelector(s) === window.__fixos[i])', list(FIXOS)), \
        f'{nome}: o que está fora das zonas foi desenhado de novo'
    depois = [caixa(page, s) for s in FIXOS]
    assert depois == antes, f'{nome}: o resto mexeu: {antes} -> {depois}'
    sitio_depois = [canto(page, s) for s in SITIO]
    assert sitio_depois == sitio, f'{nome}: as zonas mudaram de sítio: {sitio} -> {sitio_depois}'
    pagina_depois = [caixa(page, s)[:2] for s in PAGINA]
    assert pagina_depois == pagina, f'{nome}: o calendário mudou de sítio: {pagina} -> {pagina_depois}'
    return pedidos[:]


with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path=os.environ.get('DMO_CHROMIUM', '/usr/bin/chromium'), args=['--no-sandbox'])
    controlos = por_aprovar()
    assert len(controlos) == 2, 'a base precisa de dois controlos por aprovar com Peso datado e link do mcaliper'
    for largura, (job_id, controlo_id, dia) in zip((1400, 390), controlos):
        ano, mes, d = (int(x) for x in dia.split('-'))
        context = browser.new_context(viewport={'width': largura, 'height': 900}, locale='pt-PT')
        context.grant_permissions(['clipboard-read', 'clipboard-write'], origin=base)
        page = context.new_page()
        erros = []
        page.on('pageerror', lambda error: erros.append(str(error)))
        pedidos = []
        page.on('request', lambda r: pedidos.append((r.method, r.resource_type, r.url))
                if r.url.startswith(base) and r.resource_type in ('document', 'fetch', 'xhr') else None)
        page.goto(base + '/login')
        page.locator('#identifier').fill(operador)
        page.locator('#password').fill(password)
        with page.expect_navigation():
            page.locator('#password').press('Enter')

        page.goto(f'{base}/Controlo/Approve?jobon_id={job_id}&ano={ano}&mes={mes}&dia={d}&estado=pendente')
        item = page.locator('.revisao-item[aria-current=true]')
        expect(item).to_have_count(1)
        expect(page.locator('.revisao-pill.big')).to_have_text('Pendente')
        expect(page.locator('.revisao-mcaliper-copy')).not_to_have_count(0)
        # Os botões no topo do cartão, à direita, antes da primeira secção (no telemóvel a
        # linha do título dobra e eles ficam por baixo do título, ainda no topo).
        topo = page.eval_on_selector('#revisaoDetalhe', '''(card) => {
            const c = card.getBoundingClientRect();
            const a = document.getElementById('revisaoAprovar-peso').getBoundingClientRect();
            const s = card.querySelector('h3.revisao-section').getBoundingClientRect();
            return { dentro: a.right <= c.right + 0.5 && a.top >= c.top, antes: a.bottom <= s.top, direita: c.right - a.right };
        }''')
        assert topo['dentro'] and topo['antes'] and (largura < 600 or topo['direita'] < 40), f'{largura}px: os botões não estão no topo do cartão: {topo}'

        # Aprovar com duplo clique.
        gravar(page, pedidos, '#revisaoAprovar-peso', f'{largura}px aprovar')
        expect(page.locator('.revisao-pill.big')).to_have_text('Aprovado')
        expect(page.locator('#revisaoAprovar-peso')).to_have_text('Reaprovar')
        expect(page.locator('#revisaoDetalhe')).to_contain_text('Aprovado por')
        expect(page.locator('.revisao-item[aria-current=true]')).to_have_count(0)  # o filtro «Pendente» tira-o
        expect(page.locator('#revisaoAprovar-peso')).to_be_focused()
        assert sql(f'select "ApprovedByOperatorId" is not null and "NotApprovedAtUtc" is null from "Controlos" where "Id"={controlo_id}') == 't', \
            'a aprovação não ficou na base'
        assert sql(f'select count(*) from "Controlos" where "JobOnId"={job_id}') == '1', 'controlo repetido'
        assert f'jobon_id={job_id}' in page.url and 'estado=pendente' in page.url, page.url

        # O copiar do cartão novo (data-dmo-copiar por delegação).
        page.locator('.revisao-mcaliper-copy').first.click()
        expect(page.locator('.revisao-mcaliper-copy').first).to_contain_text('Copiado')
        assert page.evaluate('window.__marca') == 1

        # Não aprovar sem nota: o browser pede a nota e nada sai.
        del pedidos[:]
        page.locator('#revisaoNaoAprovar-peso').click()
        page.wait_for_timeout(400)
        assert not [p for p in pedidos if p[0] == 'POST'], f'enviou sem nota: {pedidos}'
        assert page.eval_on_selector('#revisao-nota', 'e => e.validationMessage'), 'o browser não pediu a nota'

        page.locator('#revisao-nota').fill('Rever a boquilha (E8)')
        gravar(page, pedidos, '#revisaoNaoAprovar-peso', f'{largura}px não aprovar')
        expect(page.locator('.revisao-pill.big')).to_have_text('Não aprovado')
        expect(page.locator('.revisao-note')).to_have_text('Rever a boquilha (E8)')
        expect(page.locator('#revisao-nota')).to_have_value('')
        expect(page.locator('#revisaoNaoAprovar-peso')).to_be_focused()
        assert sql(f'''select "NotApprovedByOperatorId" is not null and "ApprovedAtUtc" is null and "ReviewNote" = 'Rever a boquilha (E8)'
            from "Controlos" where "Id"={controlo_id}''') == 't', 'o «não aprovado» não ficou na base'

        assert not erros, erros
        print(f'PASS Revisão {largura}px: aprovar e não aprovar com 1 POST (duplo clique incluído), sem recarregar, '
              'o resto no mesmo sítio, foco no botão, estado novo no ecrã e na base, copiar no cartão novo')
        context.close()
    browser.close()

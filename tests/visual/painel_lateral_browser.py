"""Os cartões do painel lateral das linhas, no Chromium (Diogo, 24/10/2026).

Página sintética com as folhas de estilo reais da app (sem servidor, sem DEV):
  python tests/visual/painel_lateral_browser.py

Em claro e em escuro:
- ao passar o rato, o texto lê-se (contraste >= 4.5:1), também no cartão sem
  produção (bq-line-empty, meio apagado);
- ao escolher um cartão, logo na imagem seguinte ao clique o texto já tem
  contraste >= 4.5:1 com o fundo (não há clarão escuro de texto escuro em fundo
  escuro enquanto o fundo anima).
"""
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium

www = Path(__file__).resolve().parents[2] / 'src/DMO.Alpha.Web/wwwroot'
CSS = ['0_ASSET_CANONICAL_DESIGN_SYSTEM.css', '0_ASSET_SHELL.css', 'beta-shell.css',
       'beta-production-overview.css', 'css/dmo-normalize.css']
HTML = '<!doctype html><html><head><meta charset="utf-8">' + ''.join(
    f'<link rel="stylesheet" href="/{c}">' for c in CSS) + '''</head>
<body><aside class="beta-production-rail"><div class="beta-production-lines" data-dmo-list="Linhas">
<button type="button" data-dmo-row id="a"><strong>B1</strong><span>1075 · Lote 13</span></button>
<button type="button" data-dmo-row id="b"><strong>B2</strong><span>2040 · Lote 4</span></button>
<button type="button" data-dmo-row id="v" class="bq-line-empty"><strong>B3</strong><span>Sem produção</span></button>
</div></aside></body></html>'''

CONTRASTE = '''(id) => {
  const el = document.getElementById(id);
  const rgba = s => { const m = s.match(/[\\d.]+/g).map(Number); return [m[0], m[1], m[2], m.length > 3 ? m[3] : 1]; };
  const fundo = e => { const camadas = []; for (; e; e = e.parentElement) { const c = rgba(getComputedStyle(e).backgroundColor); if (c[3] > 0) camadas.push(c); if (c[3] >= 1) break; }
    let r = [255, 255, 255]; for (const c of camadas.reverse()) r = r.map((v, i) => v * (1 - c[3]) + c[i] * c[3]); return r; };
  const atras = fundo(el.parentElement), bg = fundo(el), op = Number(getComputedStyle(el).opacity);
  const t = rgba(getComputedStyle(el.querySelector('span')).color);
  let fg = bg.map((v, i) => v * (1 - t[3]) + t[i] * t[3]);
  const mix = c => c.map((v, i) => v * op + atras[i] * (1 - op));
  const L = c => { const [r, g, b] = c.map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
  const a = L(mix(fg)), b = L(mix(bg));
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}'''

falhas = []
with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for tema in ('light', 'dark'):
        page = browser.new_page(viewport={'width': 400, 'height': 500}, color_scheme=tema)
        def servir(route):
            path = urlparse(route.request.url).path
            if path == '/painel.html':
                route.fulfill(content_type='text/html; charset=utf-8', body=HTML)
                return
            asset = (www / unquote(path).lstrip('/')).resolve()
            if asset.is_relative_to(www) and asset.is_file() and asset.suffix == '.css':
                route.fulfill(content_type='text/css', body=asset.read_bytes())
            else:
                route.fulfill(status=404, body='')

        page.route('**/*', servir)
        page.goto('http://dmo.test/painel.html')
        assert page.evaluate("getComputedStyle(document.getElementById('a')).height") == '72px', 'CSS não carregou'
        page.wait_for_load_state('load')
        for id in ('a', 'v'):
            page.hover(f'#{id}')
            page.wait_for_timeout(400)
            c = page.evaluate(CONTRASTE, id)
            print(f'{tema} hover {id}: {c:.2f}')
            if c < 4.5:
                falhas.append(f'{tema}: hover em #{id} com contraste {c:.2f}')
        # Escolher: o clique muda .selected (como selectRow); medir nas imagens seguintes.
        page.hover('#b')
        page.wait_for_timeout(400)
        medidas = page.evaluate('''async (f) => { const contraste = eval(f); const el = document.getElementById('b');
            el.classList.add('selected'); const r = [];
            for (let i = 0; i < 12; i++) { await new Promise(requestAnimationFrame); r.push(contraste('b')); } return r; }''', CONTRASTE)
        print(f'{tema} escolher, imagem a imagem: ' + ' '.join(f'{m:.2f}' for m in medidas))
        if min(medidas) < 4.5:
            falhas.append(f'{tema}: ao escolher, contraste mínimo {min(medidas):.2f} nas imagens a seguir ao clique')
        page.close()
    browser.close()

if falhas:
    print('\n'.join(falhas))
    sys.exit(1)
print('Painel lateral: ok')

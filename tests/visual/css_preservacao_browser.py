"""Guarda de preservação do CSS (PLANO-CSS, passo 1; A-0452).

A limpeza do CSS não pode mudar o aspeto. Esta guarda abre o HTML real que os
testes Web exportam (DMO_UI_EVIDENCE_DIR), serve a sequência real de folhas de
estilo e compara os estilos computados e a geometria com uma linha de base
gravada em tests/visual/css_preservacao_base.json.

Casos iniciais (o primeiro passo é o Sumário):
- Sumário populado, lista longa e vazio; repouso, hover importante, hover
  normal, foco de teclado e fim da lista; claro e escuro; 1366x768, 1290x646,
  1100x768, 1099x768 e 800x500.
- Sentinelas congelados: Peso completo, Peso no painel de apoio e Comparação
  de Peso; PU NNPB para os botões e seletores. 1366x768 e 1290x646.

Uso:
  DMO_UI_EVIDENCE_DIR=<pasta> dotnet test tests/DMO.Alpha.Web.Tests \
    --filter FullyQualifiedName~CssPreservacaoExportaParaOBrowserTests
  python3 tests/visual/css_preservacao_browser.py <pasta> [--evidencia <pasta>]

  --escrever-base       grava a linha de base (só com uma alteração aprovada)
  --candidato <pasta>   serve o CSS dessa pasta em vez do wwwroot (antes/depois)

Sem --escrever-base, falha se um estilo, uma medida ou um recurso (CSS/fonte)
mudar, se faltar uma fixture, se a página inteira deslizar a 1366/1290/1100/1099
(exceto o Peso no painel, que é um documento próprio) ou se aparecer um erro JS
novo. Só ficheiros locais; nada vai ao DEV.
"""
import argparse
import importlib.metadata
import json
import mimetypes
import re
import subprocess
import sys
from datetime import date
from pathlib import Path
from urllib.parse import unquote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium, sem_selo_local

RAIZ = Path(__file__).resolve().parents[2]
WWW = RAIZ / "src/DMO.Alpha.Web/wwwroot"
BASE = Path(__file__).with_name("css_preservacao_base.json")
EXTENSOES = (".css", ".woff2", ".woff", ".png", ".jpg", ".jpeg", ".svg", ".ico")

CASOS = [
    {
        "nome": "sumario-populado",
        "html": "css-preservacao-sumario-populado.html",
        "caminho": "/Planeamento/Sumario",
        "estados": ("repouso", "hover-importante", "hover-normal", "foco-normal"),
        "ecras": ((1366, 768), (1290, 646), (1100, 768), (1099, 768), (800, 500)),
    },
    {
        "nome": "sumario-longo",
        "html": "css-preservacao-sumario-longo.html",
        "caminho": "/Planeamento/Sumario?dias=7",
        "estados": ("repouso", "fim"),
        "ecras": ((1366, 768), (1290, 646)),
    },
    {
        "nome": "sumario-vazio",
        "html": "css-preservacao-sumario-vazio.html",
        "caminho": "/Planeamento/Sumario",
        "estados": ("repouso",),
        "ecras": ((1366, 768), (1290, 646)),
    },
    {
        "nome": "peso-completo",
        "html": "peso-folha.html",
        "caminho": "/Controlo/Peso",
        "estados": ("repouso",),
        "ecras": ((1366, 768), (1290, 646)),
    },
    {
        "nome": "peso-painel",
        "html": "cobertura-b-peso-painel.html",
        "caminho": "/Controlo/Peso?painel=1",
        "estados": ("repouso",),
        "ecras": ((1366, 768), (1290, 646)),
    },
    {
        "nome": "peso-comparacao",
        "html": "cobertura-b-peso-comparacao.html",
        "caminho": "/Controlo/PesoComparar",
        "estados": ("repouso",),
        "ecras": ((1366, 768), (1290, 646)),
    },
    {
        "nome": "puncoes-botoes",
        "html": "puncoes-tabela.html",
        "caminho": "/Controlo/Puncoes",
        "estados": ("repouso",),
        "ecras": ((1366, 768), (1290, 646)),
    },
]

MEDIR = r"""() => {
  const de = document.documentElement, b = document.body;
  const visivel = e => { const r = e.getBoundingClientRect(), c = getComputedStyle(e);
    return r.width > 0 && r.height > 0 && c.visibility !== 'hidden' && c.display !== 'none'; };
  const r = e => { const x = e.getBoundingClientRect();
    return [+x.left.toFixed(1), +x.top.toFixed(1), +x.width.toFixed(1), +x.height.toFixed(1)]; };
  const estilo = e => { const c = getComputedStyle(e), x = e.getBoundingClientRect();
    return {fundo: c.backgroundColor, cor: c.color, sombra: c.boxShadow, posicao: c.position,
            contorno: c.outlineStyle + ' ' + c.outlineWidth + ' ' + c.outlineColor,
            cursor: c.cursor, decoracao: c.textDecorationLine,
            fonte: c.fontSize + '/' + c.fontWeight, altura: +x.height.toFixed(1)}; };
  const botoes = [...document.querySelectorAll('.dmo-button,.btn,.button')].filter(visivel).slice(0, 30)
    .map(e => ({id: e.id, classe: String(e.className).slice(0, 80), ...estilo(e), r: r(e)}));
  const selects = [...document.querySelectorAll('select')].filter(visivel).slice(0, 20)
    .map(e => ({id: e.id, ...estilo(e), r: r(e)}));
  const linhas = [...document.querySelectorAll('.sumario-item')];
  const amostra = linhas.length <= 8 ? linhas : [...linhas.slice(0, 3), ...linhas.slice(-3)];
  const estiloFilho = e => { const c = getComputedStyle(e), x = e.getBoundingClientRect();
    return {cor: c.color, fonte: c.fontSize + '/' + c.fontWeight, altura: +x.height.toFixed(1)}; };
  const linha = e => ({classe: e.className, r: r(e), ...estilo(e),
    filhos: [...e.querySelectorAll(':scope > a, :scope > strong, :scope > time, :scope > span, :scope > small')].map(f =>
      ({tag: f.tagName, classe: f.className, ...estiloFilho(f)}))});
  const tabelas = [...document.querySelectorAll('.dmo-table-wrap')].filter(visivel).slice(0, 10)
    .map(e => ({r: r(e), scrollH: e.scrollHeight, clientH: e.clientHeight, scrollW: e.scrollWidth, clientW: e.clientWidth}));
  const cartoes = [...document.querySelectorAll('.dmo-card')].filter(visivel).slice(0, 14)
    .map(e => ({classe: e.className, r: r(e)}));
  const lista = document.querySelector('#sumarioLista');
  return {
    doc: {w: de.scrollWidth, h: de.scrollHeight, cw: de.clientWidth, ch: de.clientHeight,
          dx: de.scrollWidth - de.clientWidth, dy: de.scrollHeight - de.clientHeight},
    body: {w: b.scrollWidth, h: b.scrollHeight},
    main: document.querySelector('main') ? r(document.querySelector('main')) : null,
    h1: document.querySelector('h1') ? r(document.querySelector('h1')) : null,
    lista: lista ? {r: r(lista), scrollH: lista.scrollHeight, clientH: lista.clientHeight,
                    scrollTop: Math.round(lista.scrollTop), scrollW: lista.scrollWidth, clientW: lista.clientWidth} : null,
    linhas: amostra.map(linha), totalLinhas: linhas.length,
    botoes, selects, tabelas, cartoes
  };
}"""


def recursos(html: str):
    for m in re.finditer(r'<(?:link|script|img)\b[^>]+(?:href|src)="([^"]+)"', html):
        url = m.group(1)
        if url.startswith(("http://", "https://", "//", "data:")):
            continue
        caminho = unquote(urlparse(url).path).lstrip("/")
        if caminho.lower().endswith(EXTENSOES):
            yield caminho


def ficheiro_de(caminho: str, candidato: Path | None) -> Path | None:
    if candidato is not None:
        p = (candidato / caminho).resolve()
        if p.is_file():
            return p
    p = (WWW / caminho).resolve()
    return p if p.is_file() else None


def aplicar_estado(page, caso, estado):
    if estado == "repouso":
        return
    linhas = page.locator("li.sumario-item:has(> a.sumario-titulo)")
    if estado.startswith("hover"):
        indice = 0 if estado.endswith("importante") else 1
        linhas.nth(indice).hover()
        page.wait_for_timeout(400)
    elif estado == "foco-normal":
        linhas.nth(1).locator("a.sumario-titulo").focus()
        page.wait_for_timeout(400)
    elif estado == "fim":
        page.evaluate("""() => { const l = document.querySelector('#sumarioLista');
            if (l) l.scrollTop = l.scrollHeight; }""")
        page.wait_for_timeout(300)
    else:
        raise SystemExit(f"estado desconhecido: {estado}")


def medir(browser, caso, estado, largura, altura, tema, candidato, evidencia, falhas):
    ficheiro = FIXTURES / caso["html"]
    if not ficheiro.is_file():
        falhas.append(f"falta a fixture {ficheiro.name}")
        return None
    html = sem_selo_local(ficheiro.read_text(encoding="utf-8"))
    pedidos_falhados = []
    erros_js = []
    context = browser.new_context(viewport={"width": largura, "height": altura}, device_scale_factor=1,
                                  locale="pt-PT", timezone_id="Europe/Lisbon",
                                  color_scheme="light" if tema == "claro" else "dark")
    page = context.new_page()
    page.on("pageerror", lambda e: erros_js.append(str(e)))

    def servir(route):
        url = urlparse(route.request.url)
        if route.request.method == "GET" and url.path == urlparse(caso["caminho"]).path:
            route.fulfill(content_type="text/html; charset=utf-8", body=html)
            return
        caminho = unquote(url.path).lstrip("/")
        alvo = ficheiro_de(caminho, candidato)
        if alvo is not None:
            route.fulfill(content_type=mimetypes.guess_type(str(alvo))[0] or "application/octet-stream",
                          body=alvo.read_bytes())
        else:
            if caminho.lower().endswith(EXTENSOES):
                pedidos_falhados.append(caminho)
            route.fulfill(status=404, body="")

    context.route("**/*", servir)
    page.goto("http://dmo.test" + caso["caminho"])
    page.wait_for_load_state("load")
    page.evaluate("document.fonts && document.fonts.ready")
    page.wait_for_timeout(500)
    page.evaluate("""() => Promise.all([...document.images].filter(i => !i.complete)
        .map(i => new Promise(r => { i.onload = i.onerror = r; })))""")
    page.evaluate(f"document.documentElement.setAttribute('data-theme','{'light' if tema == 'claro' else 'dark'}')")
    page.wait_for_timeout(250)
    aplicar_estado(page, caso, estado)

    medida = page.evaluate(MEDIR)
    medida["fontes"] = page.evaluate("""() => ({estado: document.fonts.status,
        inter: [...document.fonts].filter(f => f.family.includes('Inter')).map(f => f.status)})""")
    medida["erros_js"] = sorted(set(erros_js))
    medida["recursos_falhados"] = sorted(set(pedidos_falhados))
    if evidencia is not None:
        pasta = evidencia / caso["nome"]
        pasta.mkdir(parents=True, exist_ok=True)
        page.screenshot(path=str(pasta / f"{estado}-{tema}-{largura}x{altura}.png"), full_page=False)
    context.close()
    return medida


def comparar(a, b, caminho=""):
    if type(a) is not type(b):
        yield f"{caminho}: tipo {type(a).__name__} != {type(b).__name__}"
        return
    if isinstance(a, dict):
        for chave in sorted(set(a) | set(b)):
            if chave not in a or chave not in b:
                yield f"{caminho}/{chave}: só num dos lados"
            else:
                yield from comparar(a[chave], b[chave], f"{caminho}/{chave}")
    elif isinstance(a, list):
        if len(a) != len(b):
            yield f"{caminho}: {len(a)} != {len(b)} elementos"
        for i, (x, y) in enumerate(zip(a, b)):
            yield from comparar(x, y, f"{caminho}[{i}]")
    elif a != b:
        yield f"{caminho}: {a!r} != {b!r}"


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("fixtures", type=Path)
    p.add_argument("--evidencia", type=Path)
    p.add_argument("--escrever-base", action="store_true")
    p.add_argument("--candidato", type=Path)
    a = p.parse_args()

    global FIXTURES
    FIXTURES = a.fixtures.resolve()
    if not FIXTURES.is_dir():
        print(f"Falta a pasta de fixtures: {FIXTURES}")
        return 1

    candidato = a.candidato.resolve() if a.candidato else None
    if candidato is not None and not candidato.is_dir():
        print(f"Falta a pasta candidata: {candidato}")
        return 1

    faltam = [c["html"] for c in CASOS if not (FIXTURES / c["html"]).is_file()]
    if faltam:
        print("Faltam fixtures: " + ", ".join(faltam))
        return 1
    for c in CASOS:
        html = sem_selo_local((FIXTURES / c["html"]).read_text(encoding="utf-8"))
        for caminho in recursos(html):
            if ficheiro_de(caminho, candidato) is None:
                print(f"Falta o recurso {caminho} (em {c['html']})")
                return 1

    falhas = []
    medidas = {}
    with sync_playwright() as pw:
        browser = launch_chromium(pw)
        for c in CASOS:
            for largura, altura in c["ecras"]:
                for tema in ("claro", "escuro"):
                    for estado in c["estados"]:
                        chave = f"{c['nome']}|{estado}|{tema}|{largura}x{altura}"
                        medida = medir(browser, c, estado, largura, altura, tema, candidato, a.evidencia, falhas)
                        if medida is None:
                            continue
                        medidas[chave] = medida
                        if medida["fontes"]["estado"] != "loaded" or not medida["fontes"]["inter"]:
                            falhas.append(f"{chave}: a fonte Inter não carregou ({medida['fontes']})")
                        if medida["recursos_falhados"]:
                            falhas.append(f"{chave}: recursos em falta {medida['recursos_falhados'][:6]}")
                        # O Peso é sentinela congelada: regista-se o deslize atual
                        # (o painel é um documento próprio) e compara-se com a base;
                        # não se lhe exige a regra de página sem deslizar.
                        if not c["nome"].startswith("peso"):
                            if largura >= 1100:
                                if medida["doc"]["dx"] or medida["doc"]["dy"]:
                                    falhas.append(f"{chave}: a página desliza {medida['doc']['dx']}x{medida['doc']['dy']} px")
                            elif medida["doc"]["dx"]:
                                falhas.append(f"{chave}: a página desliza para o lado {medida['doc']['dx']} px")
                        print(f"{chave}: doc {medida['doc']['w']}x{medida['doc']['h']}"
                              f" (desliza {medida['doc']['dx']}x{medida['doc']['dy']})"
                              + (f" · lista {medida['lista']['scrollH']}/{medida['lista']['clientH']}" if medida["lista"] else ""))
        versao = browser.version
        browser.close()

    meta = {
        "commit": subprocess.run(["git", "-C", str(RAIZ), "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip(),
        "browser": versao,
        "playwright": importlib.metadata.version("playwright"),
        "data": date.today().isoformat(),
        "nota": "Linha de base da guarda de preservação do CSS (A-0452); só muda com uma alteração de aspeto aprovada.",
    }

    if a.escrever_base:
        if falhas:
            print(f"\nNão se grava a linha de base com {len(falhas)} falhas:")
            for f in falhas[:60]:
                print(" ", f)
            return 1
        BASE.write_text(json.dumps({"_meta": meta, "casos": medidas}, ensure_ascii=False, indent=1, sort_keys=True),
                        encoding="utf-8")
        print(f"\nLinha de base gravada em {BASE} ({len(medidas)} estados).")
        return 0

    if not BASE.is_file():
        print(f"Falta a linha de base {BASE}; corre primeiro com --escrever-base.")
        return 1
    base = json.loads(BASE.read_text(encoding="utf-8"))["casos"]
    if set(base) != set(medidas):
        for chave in sorted(set(base) - set(medidas)):
            falhas.append(f"{chave}: estado da base não foi medido")
        for chave in sorted(set(medidas) - set(base)):
            falhas.append(f"{chave}: estado novo sem linha de base")
    for chave in sorted(set(base) & set(medidas)):
        for diferenca in comparar(base[chave], medidas[chave]):
            falhas.append(f"{chave}: {diferenca}")

    if a.evidencia is not None:
        (a.evidencia / "medicoes-atuais.json").write_text(
            json.dumps(medidas, ensure_ascii=False, indent=1, sort_keys=True), encoding="utf-8")

    print(f"\n{len(medidas)} estados medidos; {len(falhas)} diferenças.")
    if falhas:
        for f in falhas[:60]:
            print(" ", f)
        print("A aparência mudou ou falta uma proteção; ver as capturas de evidência.")
        return 1
    print("Sem diferenças: a aparência e a geometria mantêm-se.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

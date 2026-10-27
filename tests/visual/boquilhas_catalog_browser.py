"""Chromium interaction checks on Razor HTML exported by ToolCatalog_* HTTP tests.

Run with: python tests/visual/boquilhas_catalog_browser.py <DMO_UI_EVIDENCE_DIR>
The HTTP tests exercise persistence; this script serves their HTML and local
assets through Playwright routes. The Ferramentas registration is a stub here:
the HTTP tests cover it. It does not contact DEV or authenticate against a
live deployment.
"""
import json
import mimetypes
import os
import re
import sys
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

from playwright.sync_api import sync_playwright, expect

from dmo_chromium import launch_chromium, sem_selo_local

evidence = Path(sys.argv[1]).resolve()
assets = Path(__file__).resolve().parents[2] / "src/DMO.Alpha.Web/wwwroot"
results = []
RETURN = "/Boquilhas?ficheiros=1&ferramenta={tool_id}&saved=created#bq-tool-sheet"


def create_link(page):
    """O «Criar boquilha» (Diogo, 18/10/2026): o registo das Ferramentas com BQ e a máquina do filtro."""
    link = page.locator("#bqCriarBoquilha a")
    expect(link).to_have_text("Criar boquilha")
    query = parse_qs(urlparse(link.get_attribute("href")).query)
    assert urlparse(link.get_attribute("href")).path == "/Ferramentas", link.get_attribute("href")
    assert query["criar"] == ["1"] and query["type"] == ["BQ"] and query["return"] == [RETURN], query
    return link, query.get("machine_ids", [])


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    for width, height in [(1920, 1080), (768, 1024), (390, 844)]:
        context = browser.new_context(viewport={"width": width, "height": height})
        page = context.new_page()
        errors = []
        page.on("pageerror", lambda error: errors.append(str(error)))

        def serve(route):
            url = urlparse(route.request.url)
            query = parse_qs(url.query, keep_blank_values=True)
            if url.path == "/Ferramentas":
                route.fulfill(content_type="text/html", body="<!doctype html><title>Ferramentas</title><p>Registar ferramenta</p>")
                return
            if url.path == "/Boquilhas":
                # Mudar a máquina do filtro para «Todas» pede o catálogo sem máquina.
                # Apagar a pesquisa pede a página só com a máquina, que não tem
                # lista (deep sweep 2, 18/10/2026, F-16: a máquina só reduz).
                name = ("sheet" if "ferramenta" in query else "catalog-maquina" if query.get("q") == [""]
                        else "catalog-all" if query.get("machine_id") == [""] else "catalog")
                route.fulfill(content_type="text/html", body=sem_selo_local((evidence / (name + ".html")).read_text()))
                return
            path = (assets / unquote(url.path).lstrip("/")).resolve()
            if path.is_relative_to(assets) and path.is_file():
                route.fulfill(content_type=mimetypes.guess_type(str(path))[0] or "application/octet-stream", body=path.read_bytes())
            else:
                route.fulfill(status=404, body="Not a local fixture")

        context.route("**/*", serve)
        page.goto("http://dmo.test/Boquilhas?ficheiros=1")
        # Sem círculos de dica em lado nenhum (Diogo, 27/10/2026): há dicas
        # na página, mas nenhum círculo à vista (o texto vai para o title).
        assert page.locator(".dmo-help").count() > 0
        assert page.locator(".dmo-help:visible").count() == 0

        # Sem o formulário curto: «Criar boquilha» no canto superior direito
        # do cartão da procura, com a máquina do filtro.
        assert page.locator("#bq-tool-create").count() == 0
        link, machines = create_link(page)
        assert len(machines) == 1, machines
        assert machines[0] == page.locator("#bq-tool-machine").input_value(), machines
        head = page.locator(".bq-tool-search-topo").bounding_box()
        title = page.locator(".bq-tool-search-topo h3").bounding_box()
        button = link.bounding_box()
        search = page.locator(".bq-tool-search").bounding_box()
        assert abs(button["x"] + button["width"] - (head["x"] + head["width"])) <= 1, (width, head, button)
        assert button["y"] < title["y"] + title["height"] and title["y"] < button["y"] + button["height"], (width, title, button)
        assert button["y"] + button["height"] <= search["y"], (width, button, search)
        assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), width
        page.screenshot(path=str(evidence / f"catalog-{width}.png"), full_page=True)

        # Mudar a máquina do filtro troca o botão com a lista: vai sem máquina.
        page.locator("#bq-tool-machine").select_option("")
        expect(page.locator("#bqCriarBoquilha a")).not_to_have_attribute("href", re.compile("machine_ids="))
        assert create_link(page)[1] == []

        # Só a máquina não mostra a lista (deep sweep 2, 18/10/2026, F-16; a
        # regra das Ferramentas, Diogo: «não deve existir lista até procurar»).
        # Apagar a pesquisa esconde-a no lugar, sem recarregar; escrever outra
        # vez mostra-a, já reduzida à máquina.
        page.goto("http://dmo.test/Boquilhas?ficheiros=1")
        expect(page.locator("#bq-tool-results")).to_be_visible()
        page.evaluate("window.__marca = 1")
        field = page.locator("#bq-tool-search")
        field.fill("")
        expect(page.locator("#bq-tool-results")).to_be_hidden()
        assert page.locator("#bqToolList").count() == 0
        assert len(create_link(page)[1]) == 1
        field.fill("t17")
        expect(page.locator("#bqToolList tbody tr").first).to_be_visible()
        assert page.evaluate("window.__marca") == 1, "A página recarregou"
        expect(field).to_be_focused()

        page.goto("http://dmo.test/Boquilhas?ficheiros=1")
        row = page.locator("#bqToolList tbody tr").first
        # Um clique abre a ficha da linha (beta.113: «listas abrem com um clique»).
        destino = row.get_attribute("data-dmo-open")
        # Diogo, 26/10/2026: «carregar aqui na boquilha deverias abrir a folha da ferramenta dela».
        assert "/Ferramentas/Ficha" in destino and "tool_id=" in destino
        tool_id = destino.split("tool_id=")[1].split("&")[0]
        row.click()
        page.wait_for_url("**/Ferramentas/Ficha**")
        # A ficha em página (Editar/Registos/Movimentos) continua a abrir por ?ferramenta=.
        page.goto(f"http://dmo.test/Boquilhas?ficheiros=1&ferramenta={tool_id}&saved=created#bq-tool-sheet")
        expect(page.locator("#bq-tool-sheet")).to_be_visible()
        expect(page.locator("[data-dmo-toast]").first).to_contain_text("Boquilha guardada")
        # «Registar sem Job On» (Diogo, 22/10/2026): a janela abre e fecha.
        page.locator(".bq-sem-jobon-link").click()
        expect(page.locator("#bqSemJobOnDialog")).to_be_visible()
        page.locator("#bqSemJobOnDialog [data-dmo-dialog-close]").click()
        expect(page.locator("#bqSemJobOnDialog")).to_be_hidden()
        assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), width
        page.screenshot(path=str(evidence / f"sheet-{width}.png"), full_page=True)

        page.goto("http://dmo.test/Boquilhas?ficheiros=1")
        page.locator("#bqToolList tbody tr").first.press("Enter")
        page.wait_for_url("**/Ferramentas/Ficha**")
        assert not errors, errors
        results.append({"width": width, "create_link": "passed", "create_link_follows_machine_filter": "passed", "machine_only_hides_list": "passed", "row_click_and_enter_open": "passed", "horizontal_overflow": False, "javascript_errors": errors})
        context.close()
    context = browser.new_context(java_script_enabled=False, viewport={"width": 390, "height": 844})
    context.route("**/*", serve)
    page = context.new_page()
    page.goto("http://dmo.test/Boquilhas?ficheiros=1")
    link, machines = create_link(page)
    assert len(machines) == 1, machines
    link.click()
    page.wait_for_url("**/Ferramentas?criar=1&type=BQ&**")
    expect(page.locator("p")).to_have_text("Registar ferramenta")
    results.append({"javascript": "disabled", "create_link_opens_registration": "passed"})
    context.close()
    browser.close()

(evidence / "browser-checks.json").write_text(json.dumps({"mode": "compiled Razor fixtures; local assets; Ferramentas registration stub", "checks": results}, indent=2))
print(json.dumps(results, indent=2))

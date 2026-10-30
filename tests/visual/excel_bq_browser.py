"""Chromium checks for js/dmo-excel-bq.js (o Excel das Boquilhas do chefe).

Run with: python tests/visual/excel_bq_browser.py [EVIDENCE_DIR]
The page and the server replies are fixtures served through Playwright routes;
the real handlers (versão e escrita do livro) are covered by the HTTP tests.
The file the user would pick with showOpenFilePicker is stood in for by a file
in the browser's private file system (navigator.storage.getDirectory), which
Chromium treats the same way. Nothing contacts DEV.
"""
import json
import sys
from pathlib import Path
from urllib.parse import quote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium

evidence = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else None
assets = Path(__file__).resolve().parents[2] / "src/DMO.Alpha.Web/wwwroot"
ORIGIN = "http://localhost"  # contexto seguro: a File System Access API exige-o

PAGE = """<!DOCTYPE html><html lang="pt-PT"><head><meta charset="utf-8"><title>Excel das Boquilhas</title></head>
<body>
  <input type="hidden" name="__RequestVerificationToken" value="tok-1">
  <section data-bq-excel>
    <button type="button" data-bq-excel-pasta>Escolher a pasta</button>
    <details data-bq-excel-opcoes><summary>Opções</summary>
      <button type="button" data-bq-excel-escolher>Escolher um Excel que já existe</button>
      <button type="button" data-bq-excel-atualizar data-bq-excel-ligado hidden>Atualizar o Excel agora</button>
      <button type="button" data-bq-excel-esquecer data-bq-excel-ligado hidden>Deixar de usar</button>
    </details>
    <p><strong>Neste computador:</strong> <span data-bq-excel-estado>Nenhum Excel</span></p>
    <p data-bq-excel-sem-suporte hidden>Sem suporte</p>
  </section>
  <script src="/dmo-live.js"></script>
  <script src="/js/dmo-excel-bq.js" data-bq-excel-url="/Boquilhas/Definicoes"></script>
</body></html>"""

state = {"versao": "1.1.0", "recusa": None}
posts = []
novos = []


def handle(route):
    request = route.request
    url = urlparse(request.url)
    if url.path == "/cap":
        return route.fulfill(body=PAGE, content_type="text/html; charset=utf-8")
    if url.path == "/Boquilhas/Definicoes" and "handler=ExcelVersao" in url.query:
        return route.fulfill(body=json.dumps({"versao": state["versao"]}), content_type="application/json")
    if url.path == "/Boquilhas/Definicoes" and "handler=ExcelNovo" in url.query:
        novos.append(url.query)
        return route.fulfill(body=b"NOVO", content_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
    if url.path == "/Boquilhas/Definicoes" and "handler=Excel" in url.query and request.method == "POST":
        posts.append(request.post_data_buffer or b"")
        if state["recusa"]:
            return route.fulfill(status=422, body=state["recusa"], content_type="text/plain; charset=utf-8")
        return route.fulfill(body=b"ESCRITO-" + state["versao"].encode(), headers={
            "Content-Type": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "X-Dmo-Excel-Versao": state["versao"],
            "X-Dmo-Excel-Avisos": quote("03/10 · B1: 6 boquilhas no dia, só cabem 5; ficaram de fora T6/1.", safe=""),
        })
    local = assets / url.path.lstrip("/")
    if local.is_file():
        kind = "application/javascript" if local.suffix == ".js" else "text/css"
        return route.fulfill(body=local.read_bytes(), content_type=kind)
    return route.fulfill(status=404, body="")


READ_FILE = """async () => {
  const root = await navigator.storage.getDirectory();
  const handle = await root.getFileHandle('Saida Entrada BQ 2026.xlsx');
  return await (await handle.getFile()).text();
}"""

# O ficheiro que o Diogo escolheria, ligado como o «Escolher o Excel» o liga
# (o handle, o dia de hoje como início e sem versão escrita).
CHOOSE_PRIVATE_FILE = """async () => {
  const root = await navigator.storage.getDirectory();
  const handle = await root.getFileHandle('Saida Entrada BQ 2026.xlsx', { create: true });
  const w = await handle.createWritable(); await w.write('ANTIGO'); await w.close();
  await new Promise((resolve, reject) => {
    const open = indexedDB.open('dmo-excel-bq', 1);
    open.onupgradeneeded = () => open.result.createObjectStore('excel');
    open.onsuccess = () => {
      const tx = open.result.transaction('excel', 'readwrite');
      tx.objectStore('excel').put({ handle, desde: '2026-10-20', versao: null }, 'ligado');
      tx.oncomplete = () => { open.result.close(); resolve(); };
      tx.onerror = () => reject(tx.error);
    };
    open.onerror = () => reject(open.error);
  });
}"""


def toasts(page):
    # Sem o «×» de fechar de cada mensagem.
    return [t.rstrip("×").strip() for t in page.locator("[data-dmo-toast]").all_inner_texts()]


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    context = browser.new_context()
    page = context.new_page()
    # R8 (Diogo, 30/10/2026): a escrita automática só às 14:00 e às 16:20 de
    # Portugal. O relógio da página começa às 15:00 (já passou a das 14:00).
    page.clock.install(time="2026-10-30T15:00:00Z")
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.route(f"{ORIGIN}/**", handle)

    # 1. Sem Excel escolhido: o cartão diz-o, «Opções» só tem escolher um
    #    Excel que já existe, e nada é enviado.
    page.goto(f"{ORIGIN}/cap")
    page.wait_for_timeout(400)
    assert page.locator("[data-bq-excel-estado]").inner_text() == "Nenhum Excel"
    assert page.locator("[data-bq-excel-pasta]").inner_text() == "Escolher a pasta"
    page.click("[data-bq-excel-opcoes] summary")
    assert page.locator("[data-bq-excel-escolher]").is_visible()
    assert not page.locator("[data-bq-excel-atualizar]").is_visible()
    page.click("[data-bq-excel-opcoes] summary")
    assert posts == [], "sem Excel, nada é enviado"

    # 2. Com o Excel ligado: ao abrir, a versão mudou desde a última escrita,
    #    por isso envia o ficheiro (com o início e o token) e grava a resposta
    #    por cima. Os avisos do servidor aparecem no canto.
    page.evaluate(CHOOSE_PRIVATE_FILE)
    page.reload()
    page.wait_for_function("document.querySelector('[data-dmo-toast]') !== null", timeout=5000)
    page.wait_for_timeout(400)
    assert len(posts) == 1, posts
    assert b"ANTIGO" in posts[0] and b'name="desde"\r\n\r\n2026-10-20' in posts[0], posts[0][:400]
    assert b'name="__RequestVerificationToken"\r\n\r\ntok-1' in posts[0]
    assert page.evaluate(READ_FILE) == "ESCRITO-1.1.0"
    assert "Excel das Boquilhas atualizado." in toasts(page), toasts(page)
    assert any("ficaram de fora T6/1." in t for t in toasts(page)), toasts(page)
    assert page.locator("[data-bq-excel-estado]").inner_text() == \
        "«Saida Entrada BQ 2026.xlsx» · escreve os movimentos a partir de 20/10/2026."
    assert page.locator("[data-bq-excel-pasta]").inner_text() == "Escolher outra pasta"
    if evidence:
        evidence.mkdir(parents=True, exist_ok=True)
        page.screenshot(path=str(evidence / "excel-bq-ligado.png"))

    # 3. A mesma versão: abrir de novo não reenvia o ficheiro.
    page.reload()
    page.wait_for_timeout(600)
    assert len(posts) == 1, "com a versão igual, o ficheiro não é reenviado"

    # 4. Um registo novo: uma troca recusada não escreve; a troca gravada sim.
    state["versao"] = "2.2.0"
    page.evaluate("document.dispatchEvent(new CustomEvent('dmo:troca', { detail: { recusa: true } }))")
    page.wait_for_timeout(400)
    assert len(posts) == 1, "uma gravação recusada não escreve o Excel"
    page.evaluate("document.dispatchEvent(new CustomEvent('dmo:troca', { detail: { recusa: false } }))")
    page.wait_for_timeout(600)
    assert len(posts) == 1, "antes das 16:20 a troca gravada não escreve o Excel"
    # Às 16:20 escreve sozinho (o relógio de minuto a minuto), com a página aberta.
    page.clock.set_system_time("2026-10-30T16:21:00Z")
    page.clock.run_for(61000)
    page.wait_for_timeout(800)
    assert len(posts) == 2, posts
    assert b'name="escritas"' in posts[1], "as linhas que a app escreveu vão no pedido"
    assert page.evaluate(READ_FILE) == "ESCRITO-2.2.0"

    # 5. Uma recusa do servidor (não é um Excel): a mensagem dele no canto e o
    #    ficheiro fica como estava; «Atualizar o Excel agora» tenta de novo.
    state["versao"] = "3.3.0"
    state["recusa"] = "O ficheiro escolhido não é um Excel (.xlsx) que a app consiga ler."
    page.click("[data-bq-excel-opcoes] summary")
    page.click("[data-bq-excel-atualizar]")
    page.wait_for_timeout(800)
    assert len(posts) == 3, posts
    assert state["recusa"] in toasts(page), toasts(page)
    assert page.evaluate(READ_FILE) == "ESCRITO-2.2.0"

    # 6. Deixar de usar: o cartão volta ao início e nada mais é enviado.
    state["recusa"] = None
    page.click("[data-bq-excel-opcoes] summary")
    page.click("[data-bq-excel-esquecer]")
    page.wait_for_timeout(400)
    assert page.locator("[data-bq-excel-estado]").inner_text() == "Nenhum Excel"
    page.reload()
    page.wait_for_timeout(600)
    assert len(posts) == 3, posts

    # 7. Escolher a pasta sem Excel lá dentro: a app cria o do ano (pede o
    #    livro novo ao servidor), liga-o e escreve logo os movimentos.
    page.evaluate("""async () => {
      const root = await navigator.storage.getDirectory();
      for await (const nome of root.keys()) await root.removeEntry(nome);
      window.showDirectoryPicker = () => Promise.resolve(root);
    }""")
    page.click("[data-bq-excel-pasta]")
    page.wait_for_timeout(1000)
    ano = page.evaluate("new Date().getFullYear()")
    assert len(novos) == 1 and "dia=" in novos[0], novos
    assert len(posts) == 4 and b"NOVO" in posts[3], posts[3:][:1]
    assert any(t.startswith(f"Criado o Excel «Saida Entrada BQ {ano}.xlsx»") for t in toasts(page)), toasts(page)
    assert page.locator("[data-bq-excel-estado]").inner_text().startswith(f"«Saida Entrada BQ {ano}.xlsx»")

    # 8. A mesma pasta com o Excel já lá: usa-o como está, sem criar outro.
    page.click("[data-bq-excel-pasta]")
    page.wait_for_timeout(800)
    assert len(novos) == 1, "o Excel que já existe na pasta não é substituído"

    assert errors == [], errors
    browser.close()

print("Excel das Boquilhas: sem Excel, escrita ao abrir, versão igual, troca recusada e gravada só às horas marcadas, recusa do servidor, deixar de usar e criar na pasta: passaram")

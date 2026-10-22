"""Chromium checks for js/dmo-pasta-pdf.js (a pasta dos PDFs em cada computador).

Run with: python tests/visual/pasta_pdf_browser.py [EVIDENCE_DIR]
The page and the PDF reply are fixtures served through Playwright routes; the
real PDF handlers and the X-Dmo-Pdf-Pasta header are covered by the HTTP tests.
The folder the user would pick with showDirectoryPicker is stood in for by the
browser's private file system (navigator.storage.getDirectory), which Chromium
treats the same way. Nothing contacts DEV.
"""
import os
import sys
from pathlib import Path
from urllib.parse import quote, urlparse

from playwright.sync_api import sync_playwright

from dmo_chromium import launch_chromium

evidence = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else None
assets = Path(__file__).resolve().parents[2] / "src/DMO.Alpha.Web/wwwroot"
ORIGIN = "http://localhost"  # contexto seguro: a File System Access API exige-o

PAGE = """<!DOCTYPE html><html lang="pt-PT"><head><meta charset="utf-8"><title>Pasta dos PDFs</title></head>
<body>
  <div data-dmo-pasta-pdf>
    <span data-dmo-pasta-pdf-nome>Nenhuma</span>
    <p data-dmo-pasta-pdf-sem-suporte hidden>Sem suporte</p>
    <button type="button" data-dmo-pasta-pdf-escolher hidden>Escolher pasta dos PDFs</button>
    <button type="button" data-dmo-pasta-pdf-esquecer hidden>Deixar de usar a pasta</button>
  </div>
  <form method="post" action="/Controlo/Resumo?handler=GeneratePdf">
    <input type="hidden" name="jobon_id" value="7">
    <button type="submit" name="apenas_resumo" value="true" id="exportar">PDF do Resumo</button>
  </form>
  <form method="post" action="/Controlo/Resumo?handler=GeneratePdf" data-dmo-pdf-acompanha="puncoes">
    <input type="hidden" name="jobon_id" value="8">
    <input type="hidden" name="__RequestVerificationToken" value="tok">
    <button type="submit" id="completo">PDF completo</button>
  </form>
  <script src="/dmo-live.js"></script>
  <script src="/js/dmo-pasta-pdf.js"></script>
</body></html>"""

PDF = b"%PDF-1.4\n% DMO fixture\n%%EOF\n"
posts = []
pu_posts = []


def handle(route):
    request = route.request
    path = urlparse(request.url).path
    if path == "/cap":
        return route.fulfill(body=PAGE, content_type="text/html; charset=utf-8")
    if path == "/Controlo/Resumo" and request.method == "POST":
        body = request.post_data or ""
        headers = {
            "Content-Type": "application/pdf",
            "Content-Disposition": "attachment; filename=RESUMO-5447T173-202601.pdf; filename*=UTF-8''RESUMO-5447T173-202601.pdf",
            "X-Dmo-Pdf-Pasta": quote("5447T173/202601", safe=""),
        }
        if "jobon_id=8" in body:
            # O PDF completo de uma produção com punções NNPB (22/10/2026).
            headers["X-Dmo-Pdf-Acompanha"] = "/Controlo/Puncoes?handler=GeneratePdf&controlo_id=3"
        else:
            posts.append(body)
        return route.fulfill(body=PDF, headers=headers)
    if path == "/Controlo/Puncoes" and request.method == "POST":
        pu_posts.append((request.url, request.post_data or ""))
        return route.fulfill(body=PDF, headers={
            "Content-Type": "application/pdf",
            "Content-Disposition": "attachment; filename=PU-NNPB-5447T173-202601.pdf; filename*=UTF-8''PU-NNPB-5447T173-202601.pdf",
            "X-Dmo-Pdf-Pasta": quote("5447T173/202601", safe=""),
        })
    local = assets / path.lstrip("/")
    if local.is_file():
        kind = "application/javascript" if local.suffix == ".js" else "text/css"
        return route.fulfill(body=local.read_bytes(), content_type=kind)
    return route.fulfill(status=404, body="")


LIST_FILES = """async () => {
  const root = await navigator.storage.getDirectory();
  const out = [];
  async function walk(dir, prefix) {
    for await (const [name, handle] of dir.entries()) {
      if (handle.kind === 'directory') await walk(handle, prefix + name + '/');
      else out.push(prefix + name + ':' + (await (await handle.getFile()).text()).slice(0, 8));
    }
  }
  await walk(root, '');
  return out.sort();
}"""

CHOOSE_PRIVATE_FOLDER = """async () => {
  const root = await navigator.storage.getDirectory();
  await new Promise((resolve, reject) => {
    const open = indexedDB.open('dmo-pasta-pdf', 1);
    open.onupgradeneeded = () => open.result.createObjectStore('pastas');
    open.onsuccess = () => {
      const tx = open.result.transaction('pastas', 'readwrite');
      tx.objectStore('pastas').put(root, 'controlo');
      tx.oncomplete = () => { open.result.close(); resolve(); };
      tx.onerror = () => reject(tx.error);
    };
    open.onerror = () => reject(open.error);
  });
}"""


with sync_playwright() as playwright:
    browser = launch_chromium(playwright)
    context = browser.new_context(accept_downloads=True)
    page = context.new_page()
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.route(f"{ORIGIN}/**", handle)

    # 1. Sem pasta escolhida: o cartão diz que vai para as Transferências e o
    #    formulário segue o envio normal (um download do navegador).
    page.goto(f"{ORIGIN}/cap")
    page.wait_for_timeout(300)
    assert page.locator("[data-dmo-pasta-pdf-escolher]").is_visible(), "o botão de escolher aparece no Chromium"
    assert not page.locator("[data-dmo-pasta-pdf-esquecer]").is_visible()
    with page.expect_download() as download:
        page.click("#exportar")
    assert download.value.suggested_filename == "RESUMO-5447T173-202601.pdf"
    assert page.evaluate(LIST_FILES) == [], "sem pasta, nada é gravado numa pasta"
    if evidence:
        evidence.mkdir(parents=True, exist_ok=True)
        page.screenshot(path=str(evidence / "pasta-pdf-sem-pasta.png"))

    # 2. Com a pasta escolhida: o PDF vai para <pasta>/<referência>/<produção>/,
    #    sem download, e um segundo PDF igual não apaga o primeiro.
    page.evaluate(CHOOSE_PRIVATE_FOLDER)
    page.reload()
    page.wait_for_timeout(300)
    assert page.locator("[data-dmo-pasta-pdf-esquecer]").is_visible(), "com pasta, aparece «Deixar de usar a pasta»"
    downloads = []
    page.on("download", lambda d: downloads.append(d))
    for _ in range(2):
        page.click("#exportar")
        page.wait_for_function("document.querySelector('.dmo-toast, [role=status]') !== null", timeout=5000)
        page.wait_for_timeout(400)
    files = page.evaluate(LIST_FILES)
    assert files == [
        "5447T173/202601/RESUMO-5447T173-202601 (2).pdf:%PDF-1.4",
        "5447T173/202601/RESUMO-5447T173-202601.pdf:%PDF-1.4",
    ], files
    assert downloads == [], "com pasta, o PDF não vai para as Transferências"
    # O botão carregado vai no pedido (o Resumo distingue «apenas_resumo»).
    assert all("apenas_resumo=true" in p and "jobon_id=7" in p for p in posts), posts
    if evidence:
        page.screenshot(path=str(evidence / "pasta-pdf-gravado.png"))

    # 3. Deixar de usar: volta às Transferências.
    page.click("[data-dmo-pasta-pdf-esquecer]")
    page.wait_for_timeout(300)
    page.reload()
    page.wait_for_timeout(300)
    assert not page.locator("[data-dmo-pasta-pdf-esquecer]").is_visible()
    with page.expect_download():
        page.click("#exportar")

    # 4. Duplo clique com a pasta escolhida (sweep de 19/10/2026, FE-7): o
    #    segundo envio parte antes de o primeiro acabar e é ignorado, como no
    #    envio sem pasta. Um pedido, um ficheiro novo e uma mensagem.
    page.evaluate(CHOOSE_PRIVATE_FOLDER)
    page.reload()
    page.wait_for_timeout(300)
    posts_before = len(posts)
    files_before = page.evaluate(LIST_FILES)
    page.evaluate("""() => {
      const form = document.querySelector('form[action*="GeneratePdf"]');
      const button = document.getElementById('exportar');
      form.requestSubmit(button);
      form.requestSubmit(button);
    }""")
    page.wait_for_function("document.querySelector('[data-dmo-toast]') !== null", timeout=5000)
    page.wait_for_timeout(600)
    new_files = [f for f in page.evaluate(LIST_FILES) if f not in files_before]
    assert new_files == ["5447T173/202601/RESUMO-5447T173-202601 (3).pdf:%PDF-1.4"], new_files
    assert len(posts) == posts_before + 1, posts
    toasts = page.locator("[data-dmo-toast]").all_inner_texts()
    assert len(toasts) == 1, toasts

    # 5. O PDF das punções acompanha o PDF completo (Diogo, 22/10/2026): pedido
    #    a seguir, com o mesmo token, e gravado à parte na mesma pasta.
    files_before = page.evaluate(LIST_FILES)
    toasts_before = page.locator("[data-dmo-toast]").count()
    page.click("#completo")
    page.wait_for_function(f"document.querySelectorAll('[data-dmo-toast]').length > {toasts_before}", timeout=5000)
    page.wait_for_timeout(400)
    new_files = sorted(f for f in page.evaluate(LIST_FILES) if f not in files_before)
    assert new_files == [
        "5447T173/202601/PU-NNPB-5447T173-202601.pdf:%PDF-1.4",
        "5447T173/202601/RESUMO-5447T173-202601 (4).pdf:%PDF-1.4",
    ], new_files
    assert len(pu_posts) == 1 and pu_posts[0][0].endswith("/Controlo/Puncoes?handler=GeneratePdf"), pu_posts
    assert "controlo_id=3" in pu_posts[0][1] and "__RequestVerificationToken=tok" in pu_posts[0][1], pu_posts

    # 6. Sem pasta: os dois PDFs vão para as Transferências.
    page.click("[data-dmo-pasta-pdf-esquecer]")
    page.wait_for_timeout(300)
    page.reload()
    page.wait_for_timeout(300)
    names = []
    page.on("download", lambda d: names.append(d.suggested_filename))
    page.click("#completo")
    page.wait_for_timeout(1200)
    assert sorted(names) == ["PU-NNPB-5447T173-202601.pdf", "RESUMO-5447T173-202601.pdf"], names

    assert errors == [], errors
    browser.close()

print("pasta dos PDFs: sem pasta, com pasta (referência/produção, sem apagar), deixar de usar, duplo clique e o PDF das punções a acompanhar: passaram")

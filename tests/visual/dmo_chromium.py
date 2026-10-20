"""O Chromium dos testes de browser, igual em todos os scripts de tests/visual.

DMO_CHROMIUM aponta para outro Chromium. Sem ela usa-se o do Playwright e, se
essa versão não estiver instalada, o Chromium mais recente em
PLAYWRIGHT_BROWSERS_PATH ou em /opt/pw-browsers (ambientes de nuvem).
"""
import os
import re
from pathlib import Path


def sem_selo_local(html):
    """Tira o « · local» do selo da versão no HTML exportado pelos testes.

    Com alterações por gravar no git, o selo ganha « · local» e fica mais largo;
    os testes de browser verificam a página, não o estado do repositório.
    """
    return re.sub(r"(data-dmo-version=[^>]*>[^<]*?) · local<", r"\1<", html)


def launch_chromium(playwright):
    chosen = os.environ.get("DMO_CHROMIUM")
    if chosen:
        return playwright.chromium.launch(executable_path=chosen, args=["--no-sandbox"])
    try:
        return playwright.chromium.launch(args=["--no-sandbox"])
    except Exception:
        roots = {Path(os.environ.get("PLAYWRIGHT_BROWSERS_PATH") or "/opt/pw-browsers"), Path("/opt/pw-browsers")}
        found = sorted((c for r in roots for c in r.glob("chromium-*/chrome-linux*/chrome")),
                       key=lambda c: int(c.parts[-3].split("-")[-1]))
        if not found:
            raise
        return playwright.chromium.launch(executable_path=str(found[-1]), args=["--no-sandbox"])

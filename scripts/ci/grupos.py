#!/usr/bin/env python3
"""Reparte os resultados dos testes .NET pelos grupos de scripts/ci/testes.md.

Uso: grupos.py <testes.md> <resumo> <infra.trx> <web.trx>

Os dois projetos de teste correm uma só vez cada um; este script lê os
relatórios TRX e escreve, grupo a grupo e pela ordem da tabela, o mesmo que o
CI local escrevia quando corria um dotnet test por grupo. Um teste conta no
primeiro grupo cujas partes aparecem no nome completo (como o
FullyQualifiedName~ do dotnet test, sem distinguir maiúsculas); o que nenhum
grupo apanha conta em «Outros». Os testes de um grupo retirado não contam.
As linhas do resumo final vão para o ficheiro <resumo>. Sai com 1 se algum
grupo falhou.
"""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def results(trx):
    """(nome completo, resultado, mensagem) de cada execução no relatório."""
    root = ET.parse(trx).getroot()
    names = {}
    for test in root.iterfind("t:TestDefinitions/t:UnitTest", NS):
        method = test.find("t:TestMethod", NS)
        names[test.get("id")] = method.get("className") + "." + method.get("name")
    for result in root.iterfind("t:Results/t:UnitTestResult", NS):
        message = result.findtext("t:Output/t:ErrorInfo/t:Message", "", NS)
        yield names[result.get("testId")], result.get("outcome"), message, result.get("testName")


def parts(column):
    column = column.strip()
    if column in ("", "-"):
        return []
    return [p.strip().lower() for p in column.split(",") if p.strip()]


def groups(index):
    for line in Path(index).read_text(encoding="utf-8").splitlines():
        cells = [c.strip() for c in line.split("|")]
        if len(cells) < 8 or cells[1] not in ("sim", "não"):
            continue
        yield {"runs": cells[1] == "sim", "name": cells[2], "about": cells[3],
               "infra": parts(cells[5]), "web": parts(cells[6])}


def main():
    index, summary_path, infra_trx, web_trx = sys.argv[1:5]
    table = list(groups(index))
    others = {"runs": True, "name": "Outros", "about": "Testes que nenhum grupo do índice apanha.",
              "infra": [], "web": []}
    tally = {id(g): {"p": 0, "f": 0, "s": 0, "failures": []} for g in table + [others]}

    for project, trx in (("infra", infra_trx), ("web", web_trx)):
        if not Path(trx).is_file():
            continue
        for name, outcome, message, display in results(trx):
            lowered = name.lower()
            group = next((g for g in table if any(p in lowered for p in g[project])), others)
            if not group["runs"]:
                continue
            counts = tally[id(group)]
            if outcome == "Passed":
                counts["p"] += 1
            elif outcome in ("NotExecuted", "Inconclusive"):
                counts["s"] += 1
            else:
                counts["f"] += 1
                counts["failures"].append((display, message))

    summary, failed = [], False
    for group in table + [others]:
        if not group["runs"]:
            print(f"\n== {group['name']}: retirado (não corre)")
            summary.append(f"--      {group['name']} (retirado)")
            continue
        counts = tally[id(group)]
        print(f"\n== {group['name']}\n   {group['about']}")
        for display, message in counts["failures"][:10]:
            print(f"  Failed {display}")
            for line in message.strip().splitlines()[:4]:
                print(f"   {line}")
        text = f"{counts['p']} passaram"
        if counts["f"]:
            text += f", {counts['f']} falharam"
        if counts["s"]:
            text += f", {counts['s']} ignorados"
        print(f"   {text}")
        state = "FALHOU  " if counts["f"] else "OK      "
        failed = failed or counts["f"] > 0
        summary.append(f"{state}{group['name']} ({text})")

    Path(summary_path).write_text("\n".join(summary) + "\n", encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())

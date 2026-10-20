#!/usr/bin/env python3
"""Guarda dos testes: uma impressão digital de cada teste e um registo com a
explicação de cada mudança.

  python3 scripts/ci/guarda-testes.py verificar
      Compara os testes de agora com a impressão gravada. Sai com 0 se tudo bate
      certo; com 1 se um teste mudou, saiu ou entrou sem explicação registada.
  python3 scripts/ci/guarda-testes.py mostrar
      Lista o que mudou desde a impressão gravada, sem mexer em nada.
  python3 scripts/ci/guarda-testes.py registar --tipo TIPO --porque "..." [--ref "..."]
      Acrescenta uma entrada ao registo com a lista do que mudou e grava a
      impressão nova. Os testes novos entram sempre como «teste-novo»; o TIPO
      vale para os que mudaram ou saíram:
        contagem-lista  só acertou um número ou uma lista; o que o teste verifica fica igual
        ok-do-diogo     mudança aprovada pelo Diogo; --ref diz onde (mensagem, cartão, PR)
      Só com testes novos, --tipo pode ficar de fora.

Num conflito na impressão ou no registo ao fazer rebase: fica-se com a versão da
main nos dois ficheiros e corre-se «registar» outra vez.

Uma «unidade» é um teste (.NET: cada método com [Fact]/[Theory]; JavaScript:
cada test(...)), a parte do ficheiro fora dos testes (ajudantes, montagem) ou
um ficheiro inteiro (scripts do browser, o índice e o próprio CI). A impressão
ignora comentários e espaços, por isso só conta o que o código faz.
"""
import argparse
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
IMPRESSAO = "scripts/ci/testes-impressao.json"
REGISTO = "scripts/ci/testes-alteracoes.md"
TIPOS = ("contagem-lista", "ok-do-diogo")
# Ficheiros vigiados por inteiro, além de tudo o que está em tests/.
FICHEIROS_DO_CI = ("scripts/ci/ci-local.sh", "scripts/ci/grupos.py", "scripts/ci/testes.md",
                   "scripts/ci/guarda-testes.py")
EXTENSOES = (".cs", ".cjs", ".js", ".mjs", ".py", ".json", ".sh", ".md", ".csproj", ".html")


def h(texto):
    return hashlib.sha256(texto.encode("utf-8")).hexdigest()[:16]


# ---------------------------------------------------------------- leitura do código

def sem_comentarios(src, estilo):
    """Devolve (código, máscara): o código sem comentários e, para cada carácter,
    True se está dentro de uma string (para não contar chavetas dentro de textos)."""
    out, mask = [], []
    i, n = 0, len(src)

    def put(s, m):
        out.append(s)
        mask.extend([m] * len(s))

    while i < n:
        c = src[i]
        two = src[i:i + 2]
        if estilo == "py":
            if c == "#":
                while i < n and src[i] != "\n":
                    i += 1
                continue
            if src[i:i + 3] in ('"""', "'''"):
                q = src[i:i + 3]
                j = src.find(q, i + 3)
                j = n if j < 0 else j + 3
                put(src[i:j], True)
                i = j
                continue
            if c in "\"'":
                j = i + 1
                while j < n and src[j] != c and src[j] != "\n":
                    j += 2 if src[j] == "\\" else 1
                put(src[i:j + 1], True)
                i = j + 1
                continue
            put(c, False)
            i += 1
            continue
        # C# e JavaScript
        if two == "//":
            while i < n and src[i] != "\n":
                i += 1
            continue
        if two == "/*":
            j = src.find("*/", i + 2)
            i = n if j < 0 else j + 2
            continue
        if estilo == "cs":
            m = re.match(r'\$*("{3,})', src[i:i + 12])
            if m:  # raw string """..."""
                q = m.group(1)
                j = src.find(q, i + m.end())
                j = n if j < 0 else j + len(q)
                put(src[i:j], True)
                i = j
                continue
            m = re.match(r'(\$@|@\$|@)"', src[i:i + 3])
            if m:  # verbatim "" escapa a aspa
                j = i + m.end()
                while j < n:
                    if src[j] == '"':
                        if src[j + 1:j + 2] == '"':
                            j += 2
                            continue
                        break
                    j += 1
                put(src[i:j + 1], True)
                i = j + 1
                continue
        quotes = "\"'`" if estilo == "js" else "\"'"
        if c in quotes:
            j = i + 1
            while j < n and src[j] != c:
                if src[j] == "\\":
                    j += 2
                    continue
                if c != "`" and src[j] == "\n":
                    break
                j += 1
            put(src[i:j + 1], True)
            i = j + 1
            continue
        put(c, False)
        i += 1
    return "".join(out), mask


def normal(texto):
    return re.sub(r"\s+", " ", texto).strip()


def fecho(code, mask, i, abre, fecha):
    """Posição a seguir ao fecho do bloco que abre em i."""
    d = 0
    for j in range(i, len(code)):
        if mask[j]:
            continue
        if code[j] == abre:
            d += 1
        elif code[j] == fecha:
            d -= 1
            if d == 0:
                return j + 1
    return len(code)


ATRIBUTO_TESTE = re.compile(r"\[\s*(Fact|Theory|SkippableFact|SkippableTheory)\b")


def testes_cs(code, mask):
    """Cada método de teste: (classe.método, início, fim)."""
    unidades, i = [], 0
    while True:
        m = ATRIBUTO_TESTE.search(code, i)
        if not m:
            return unidades
        if mask[m.start()]:
            i = m.end()
            continue
        # recua até ao início do bloco de atributos (atributos anteriores, ex.: [Trait])
        ini = m.start()
        while True:
            k = code.rfind("]", 0, ini)
            if k < 0 or code[k + 1:ini].strip():
                break
            a = k
            d = 0
            while a >= 0:
                if not mask[a]:
                    if code[a] == "]":
                        d += 1
                    elif code[a] == "[":
                        d -= 1
                        if d == 0:
                            break
                a -= 1
            if a < 0:
                break
            ini = a
        j = m.start()
        while True:  # salta os atributos seguintes
            while j < len(code) and code[j].isspace():
                j += 1
            if j < len(code) and code[j] == "[":
                j = fecho(code, mask, j, "[", "]")
            else:
                break
        p = code.find("(", j)
        nome = re.findall(r"[A-Za-z_]\w*", code[j:p])
        nome = nome[-1] if nome else "?"
        k = fecho(code, mask, p, "(", ")")
        resto = code[k:k + 400]
        mb = re.search(r"[{;]|=>", resto)
        if mb and mb.group(0) == "{":
            fim = fecho(code, mask, k + mb.start(), "{", "}")
        else:
            fim = code.find(";", k)
            while fim >= 0 and mask[fim]:
                fim = code.find(";", fim + 1)
            fim = len(code) if fim < 0 else fim + 1
        classes = [c for c in re.finditer(r"\b(?:class|record)\s+(\w+)", code[:ini])]
        classe = classes[-1].group(1) if classes else "?"
        unidades.append((f"{classe}.{nome}", ini, fim))
        i = fim


CHAMADA_TESTE_JS = re.compile(r"(?<![\w.$])(test|it)\s*\(")


def testes_js(code, mask):
    unidades, i = [], 0
    while True:
        m = CHAMADA_TESTE_JS.search(code, i)
        if not m:
            return unidades
        if mask[m.start()]:
            i = m.end()
            continue
        p = m.end() - 1
        fim = fecho(code, mask, p, "(", ")")
        ms = re.match(r"\s*([`'\"])(.*?)\1", code[p + 1:p + 400], re.S)
        nome = normal(ms.group(2)) if ms else "?"
        unidades.append((nome, m.start(), fim))
        i = fim


def unidades_do_ficheiro(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="replace") as f:
        src = f.read()
    ext = os.path.splitext(rel)[1]
    if ext == ".cs":
        estilo, achar = "cs", testes_cs
    elif ext in (".cjs", ".js", ".mjs"):
        estilo, achar = "js", testes_js
    elif ext == ".py":
        estilo, achar = "py", None
    else:
        return {rel: h(normal(src))}
    code, mask = sem_comentarios(src, estilo)
    if not achar:
        return {rel: h(normal(code))}
    res, resto, ultimo = {}, [], 0
    for nome, ini, fim in achar(code, mask):
        chave = f"{rel} :: {nome}"
        n = 2
        while chave in res:
            chave = f"{rel} :: {nome} #{n}"
            n += 1
        res[chave] = h(normal(code[ini:fim]))
        resto.append(code[ultimo:ini])
        ultimo = fim
    resto.append(code[ultimo:])
    res[f"{rel} :: (resto do ficheiro)"] = h(normal("".join(resto)))
    return res


def ficheiros():
    try:
        saida = subprocess.run(["git", "ls-files", "-co", "--exclude-standard", "tests"],
                               cwd=ROOT, capture_output=True, text=True, check=True).stdout
        lista = saida.split()
    except (OSError, subprocess.CalledProcessError):
        lista = []
        for base, dirs, fs in os.walk(os.path.join(ROOT, "tests")):
            dirs[:] = [d for d in dirs if d not in ("bin", "obj", "node_modules")]
            lista += [os.path.relpath(os.path.join(base, f), ROOT).replace(os.sep, "/") for f in fs]
    lista = [f for f in lista if f.endswith(EXTENSOES) and "/node_modules/" not in f
             and "/bin/" not in f and "/obj/" not in f and os.path.isfile(os.path.join(ROOT, f))]
    lista += [f for f in FICHEIROS_DO_CI if os.path.isfile(os.path.join(ROOT, f))]
    return sorted(set(lista))


def impressao_atual():
    res = {}
    for rel in ficheiros():
        res.update(unidades_do_ficheiro(rel))
    return dict(sorted(res.items()))


def selo(unidades):
    return h(json.dumps(unidades, sort_keys=True, ensure_ascii=False))


# ---------------------------------------------------------------- impressão e registo

def ler_impressao():
    caminho = os.path.join(ROOT, IMPRESSAO)
    if not os.path.isfile(caminho):
        return None
    with open(caminho, encoding="utf-8") as f:
        dados = json.load(f)
    if dados.get("selo") != selo(dados["unidades"]):
        dados["unidades"]["(impressão mexida à mão)"] = dados.get("selo")
    return dados["unidades"]


def gravar_impressao(unidades):
    with open(os.path.join(ROOT, IMPRESSAO), "w", encoding="utf-8") as f:
        f.write('{\n  "selo": "%s",\n  "unidades": {\n' % selo(unidades))
        linhas = [f"    {json.dumps(k, ensure_ascii=False)}: {json.dumps(v)}" for k, v in unidades.items()]
        f.write(",\n".join(linhas))
        f.write("\n  }\n}\n")


def entradas():
    caminho = os.path.join(ROOT, REGISTO)
    if not os.path.isfile(caminho):
        return []
    with open(caminho, encoding="utf-8") as f:
        texto = f.read()
    res = []
    for bloco in re.split(r"(?m)^(?=## A-\d+)", texto):
        m = re.match(r"## (A-\d+) · (\S+) · (\S+)", bloco)
        if not m:
            continue
        s = re.search(r"(?m)^Selo depois: `(\w+)`", bloco)
        res.append({"id": m.group(1), "tipo": m.group(3), "selo": s.group(1) if s else None, "texto": bloco})
    return res


def diferencas(antes, agora):
    novos = [k for k in agora if k not in antes]
    sairam = [k for k in antes if k not in agora]
    mudaram = [k for k in agora if k in antes and antes[k] != agora[k]]
    return novos, mudaram, sairam


def e_teste(chave):
    return " :: " in chave and not chave.endswith("(resto do ficheiro)")


def mostrar_dif(novos, mudaram, sairam, limite=60):
    for titulo, lista in (("Testes novos", novos), ("Mudaram", mudaram), ("Saíram", sairam)):
        if lista:
            print(f"  {titulo} ({len(lista)}):")
            for k in lista[:limite]:
                print(f"    {k}")
            if len(lista) > limite:
                print(f"    ... e mais {len(lista) - limite}")


def verificar_registo_so_acrescenta():
    """Se houver git e origin/main, o registo só pode ganhar linhas no fim."""
    try:
        base = subprocess.run(["git", "merge-base", "HEAD", "origin/main"], cwd=ROOT,
                              capture_output=True, text=True, check=True).stdout.strip()
        antigo = subprocess.run(["git", "show", f"{base}:{REGISTO}"], cwd=ROOT,
                                capture_output=True, text=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return True
    with open(os.path.join(ROOT, REGISTO), encoding="utf-8") as f:
        atual = f.read()
    return atual.startswith(antigo.rstrip("\n"))


def cmd_verificar(_):
    gravada = ler_impressao()
    agora = impressao_atual()
    if gravada is None:
        print("Guarda dos testes: FALHOU. Falta a impressão dos testes; corra «registar».")
        return 1
    erros = []
    reg = entradas()
    if not reg:
        erros.append(f"o registo {REGISTO} não tem entradas.")
    elif reg[-1]["selo"] != selo(gravada):
        erros.append("a impressão gravada não corresponde à última entrada do registo "
                     "(a impressão foi mudada à mão ou o registo perdeu a entrada). Corra «registar».")
    if reg and not verificar_registo_so_acrescenta():
        erros.append("o registo perdeu ou mudou entradas antigas; só se pode acrescentar no fim.")
    for e in reg:
        if e["tipo"] == "ok-do-diogo" and not re.search(r"(?m)^Referência: \S", e["texto"]):
            erros.append(f"{e['id']} é «ok-do-diogo» sem referência.")
    novos, mudaram, sairam = diferencas(gravada, agora)
    if novos or mudaram or sairam:
        erros.append("há testes que mudaram sem explicação registada:")
    if erros:
        print("Guarda dos testes: FALHOU")
        for e in erros:
            print("  " + e)
        mostrar_dif(novos, mudaram, sairam)
        if novos or mudaram or sairam:
            print('  Para registar: python3 scripts/ci/guarda-testes.py registar --tipo <contagem-lista|ok-do-diogo> '
                  '--porque "..." [--ref "..."] (só testes novos: sem --tipo).')
        return 1
    n = sum(1 for k in agora if e_teste(k))
    print(f"Guarda dos testes: OK ({n} testes vigiados, última explicação {reg[-1]['id']})")
    return 0


def cmd_mostrar(_):
    gravada = ler_impressao() or {}
    novos, mudaram, sairam = diferencas(gravada, impressao_atual())
    if not (novos or mudaram or sairam):
        print("Nada mudou desde a impressão gravada.")
        return 0
    mostrar_dif(novos, mudaram, sairam, limite=10 ** 6)
    return 0


def cmd_registar(a):
    gravada = ler_impressao()
    agora = impressao_atual()
    reg = entradas()
    inicio = gravada is None
    gravada = gravada or {}
    novos, mudaram, sairam = diferencas(gravada, agora)
    if not (novos or mudaram or sairam) and not inicio:
        print("Nada mudou desde a impressão gravada; nada a registar.")
        return 0
    if not a.porque or not a.porque.strip():
        print("Falta --porque: a explicação da mudança.")
        return 1
    # Os testes novos, e as partes de ficheiros que ainda não existiam, contam como testes novos.
    ficheiros_antigos = {k.split(" :: ")[0] for k in gravada}
    tocados = mudaram + sairam
    so_novos = all(k.split(" :: ")[0] not in ficheiros_antigos or e_teste(k) for k in novos) and not tocados
    if inicio:
        tipo = "inicio"
    elif so_novos:
        tipo = a.tipo or "teste-novo"
        if a.tipo and a.tipo not in TIPOS + ("teste-novo",):
            print(f"--tipo tem de ser um de: teste-novo, {', '.join(TIPOS)}.")
            return 1
    else:
        if a.tipo not in TIPOS:
            print(f"Há testes que mudaram ou saíram: --tipo tem de ser um de: {', '.join(TIPOS)}.")
            mostrar_dif(novos, mudaram, sairam)
            return 1
        tipo = a.tipo
    if tipo == "ok-do-diogo" and not (a.ref and a.ref.strip()):
        print("«ok-do-diogo» precisa de --ref: onde o Diogo deu o ok (mensagem, cartão ou PR).")
        return 1
    num = 1 + max([int(e["id"][2:]) for e in reg] or [0])
    data = datetime.date.today().isoformat()
    linhas = ["", f"## A-{num:04d} · {data} · {tipo}", "", f"Porquê: {normal(a.porque)}"]
    if a.ref:
        linhas.append(f"Referência: {normal(a.ref)}")
    if inicio:
        n = sum(1 for k in agora if e_teste(k))
        linhas.append(f"Unidades: {len(agora)} ({n} testes)")
    else:
        linhas.append(f"Novos: {len(novos)} · mudaram: {len(mudaram)} · saíram: {len(sairam)}")
        linhas.append("")
        linhas += [f"- novo: `{k}`" for k in novos]
        linhas += [f"- mudou: `{k}`" for k in mudaram]
        linhas += [f"- saiu: `{k}`" for k in sairam]
    linhas += ["", f"Selo depois: `{selo(agora)}`", ""]
    caminho = os.path.join(ROOT, REGISTO)
    if not os.path.isfile(caminho):
        with open(caminho, "w", encoding="utf-8") as f:
            f.write(CABECALHO)
    with open(caminho, "a", encoding="utf-8") as f:
        f.write("\n".join(linhas))
    gravar_impressao(agora)
    print(f"Registado A-{num:04d} ({tipo}): {len(novos)} novos, {len(mudaram)} mudaram, {len(sairam)} saíram.")
    return 0


CABECALHO = """# Registo das alterações aos testes

Cada entrada explica uma mudança nos testes: o que entrou, o que mudou e o que saiu, e porquê.
É escrito por `python3 scripts/ci/guarda-testes.py registar` e só cresce no fim; nunca se apagam
nem se mudam entradas antigas. O CI local falha se um teste mudar sem uma entrada aqui.

Tipos:

- `teste-novo`: só testes novos (sempre permitido).
- `contagem-lista`: só se acertou um número ou uma lista; o que o teste verifica fica igual.
- `ok-do-diogo`: mudança a um teste existente aprovada pelo Diogo; a «Referência» diz onde.
- `inicio`: o ponto de partida da guarda.

A thread «Guardião dos testes» lê cada entrada nova e avisa o coordenador quando a explicação
não justifica a mudança (por exemplo um teste enfraquecido para passar).
"""


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("verificar")
    sub.add_parser("mostrar")
    r = sub.add_parser("registar")
    r.add_argument("--tipo", choices=("teste-novo",) + TIPOS)
    r.add_argument("--porque")
    r.add_argument("--ref")
    a = p.parse_args()
    return {"verificar": cmd_verificar, "mostrar": cmd_mostrar, "registar": cmd_registar}[a.cmd](a)


if __name__ == "__main__":
    sys.exit(main())

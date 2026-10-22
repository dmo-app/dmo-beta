/*
 * dmo-pasta-pdf.js — a pasta dos PDFs em cada computador (Diogo, 17/10/2026).
 *
 * A fábrica tem um servidor com pastas partilhadas. Em cada computador
 * escolhe-se uma vez a pasta (por exemplo «Controlo 2026») nas Definições do
 * Controlo; a partir daí, os PDFs do Controlo gravam-se lá, dentro de
 * <referência>/<nº de produção>/, que a app cria. A subpasta vem do servidor
 * no cabeçalho X-Dmo-Pdf-Pasta (ControloDocumentPaths).
 *
 * O PDF continua a ser gerado em memória e nunca fica no servidor: a pasta é
 * guardada só neste navegador (IndexedDB), através da File System Access API
 * (Chrome e Edge). Sem pasta escolhida, ou num navegador sem esta API, o
 * formulário segue o caminho de sempre e o PDF vai para as Transferências.
 */
(function () {
  "use strict";

  var DB_NAME = "dmo-pasta-pdf";
  var STORE = "pastas";
  var KEY = "controlo";
  var FOLDER_HEADER = "X-Dmo-Pdf-Pasta";
  var COMPANION_HEADER = "X-Dmo-Pdf-Acompanha";

  var supported = typeof window.showDirectoryPicker === "function" && "indexedDB" in window;

  function toast(message, kind) {
    if (window.DMOLive && window.DMOLive.toast) window.DMOLive.toast(message, { kind: kind || "info" });
  }

  function withStore(mode, action) {
    return new Promise(function (resolve, reject) {
      var open = indexedDB.open(DB_NAME, 1);
      open.onupgradeneeded = function () { open.result.createObjectStore(STORE); };
      open.onerror = function () { reject(open.error); };
      open.onsuccess = function () {
        var db = open.result;
        var tx = db.transaction(STORE, mode);
        var request = action(tx.objectStore(STORE));
        tx.oncomplete = function () { db.close(); resolve(request && request.result); };
        tx.onerror = function () { db.close(); reject(tx.error); };
      };
    });
  }

  function loadFolder() {
    if (!supported) return Promise.resolve(null);
    return withStore("readonly", function (store) { return store.get(KEY); }).catch(function () { return null; });
  }

  function saveFolder(handle) {
    return withStore("readwrite", function (store) { return store.put(handle, KEY); });
  }

  function forgetFolder() {
    return withStore("readwrite", function (store) { return store.delete(KEY); });
  }

  // O navegador pede licença de escrita uma vez por sessão; tem de ser
  // pedida logo no clique, antes de qualquer espera pela rede.
  function ensurePermission(handle) {
    var options = { mode: "readwrite" };
    return handle.queryPermission(options).then(function (state) {
      if (state === "granted") return true;
      return handle.requestPermission(options).then(function (asked) { return asked === "granted"; });
    });
  }

  function fileNameOf(response) {
    var header = response.headers.get("Content-Disposition") || "";
    var star = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(header);
    if (star) return decodeURIComponent(star[1].trim());
    var plain = /filename\s*=\s*"?([^";]+)"?/i.exec(header);
    return plain ? plain[1].trim() : "documento.pdf";
  }

  function subfoldersOf(response) {
    var header = response.headers.get(FOLDER_HEADER);
    if (!header) return [];
    return decodeURIComponent(header).split("/").filter(function (part) { return part && part !== "." && part !== ".."; });
  }

  // Um PDF com o mesmo nome não é apagado: o novo fica «NOME (2).pdf».
  function freeName(folder, name) {
    var dot = name.lastIndexOf(".");
    var base = dot > 0 ? name.slice(0, dot) : name;
    var ext = dot > 0 ? name.slice(dot) : "";
    function attempt(n) {
      var candidate = n === 1 ? name : base + " (" + n + ")" + ext;
      return folder.getFileHandle(candidate).then(
        function () { return attempt(n + 1); },
        function (error) {
          if (error && error.name === "NotFoundError") return candidate;
          throw error;
        });
    }
    return attempt(1);
  }

  function writeInto(root, parts, name, blob) {
    var chain = Promise.resolve(root);
    parts.forEach(function (part) {
      chain = chain.then(function (folder) { return folder.getDirectoryHandle(part, { create: true }); });
    });
    return chain.then(function (folder) {
      return freeName(folder, name).then(function (finalName) {
        return folder.getFileHandle(finalName, { create: true }).then(function (file) {
          return file.createWritable().then(function (writable) {
            return writable.write(blob).then(function () { return writable.close(); });
          }).then(function () { return parts.concat(finalName).join("\\"); });
        });
      });
    });
  }

  function downloadBlob(blob, name) {
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
  }

  // O PDF QUE ACOMPANHA (Diogo, 22/10/2026): o PDF do Controlo de uma
  // produção com registo das punções NNPB traz no cabeçalho
  // X-Dmo-Pdf-Acompanha o endereço do PDF das punções. Pede-se a seguir, com
  // o mesmo token antifalsificação, e grava-se à parte na mesma pasta (o PDF
  // do Controlo liga-o pelo nome) ou vai para as Transferências.
  function companionRequest(response, body) {
    var header = response.headers.get(COMPANION_HEADER);
    if (!header || header.charAt(0) !== "/" || header.charAt(1) === "/") return null;
    var url = new URL(header, window.location.origin);
    if (url.origin !== window.location.origin) return null;
    var fields = new URLSearchParams();
    var token = body.get("__RequestVerificationToken");
    if (token) fields.set("__RequestVerificationToken", token);
    url.searchParams.forEach(function (value, key) { if (key !== "handler") fields.set(key, value); });
    var handler = url.searchParams.get("handler");
    return { action: url.pathname + (handler ? "?handler=" + encodeURIComponent(handler) : ""), body: fields };
  }

  // Grava um PDF recebido: na pasta (com licença) ou nas Transferências.
  // Devolve a mensagem do que aconteceu.
  function store(folder, allowed, response) {
    var name = fileNameOf(response);
    var parts = subfoldersOf(response);
    return response.blob().then(function (blob) {
      if (!folder) {
        downloadBlob(blob, name);
        return { ok: true, text: name };
      }
      if (!allowed) {
        downloadBlob(blob, name);
        return { ok: false, text: "Sem licença para gravar na pasta escolhida: o PDF foi para as Transferências." };
      }
      return writeInto(folder, parts, name, blob).then(function (path) {
        return { ok: true, text: folder.name + "\\" + path };
      }, function () {
        downloadBlob(blob, name);
        return { ok: false, text: "Não foi possível gravar na pasta escolhida: o PDF foi para as Transferências." };
      });
    });
  }

  function isPdf(response) {
    return response.ok && (response.headers.get("Content-Type") || "").indexOf("application/pdf") >= 0;
  }

  // Os formulários dos PDFs do Controlo (Peso, Pegamentos, PU NNPB e Resumo).
  // Com pasta, todos; sem pasta, só os que trazem um PDF que acompanha
  // (data-dmo-pdf-acompanha), para os dois irem para as Transferências.
  // Um envio igual ao que ainda não acabou (o mesmo formulário, com o mesmo
  // corpo) é ignorado, como no envio sem pasta: um duplo clique grava um só
  // ficheiro e mostra uma só mensagem (sweep de 19/10/2026, FE-7). Gerar
  // outra vez depois de acabar continua a dar o «NOME (2).pdf».
  function bindPdfForms(folder) {
    var emCurso = new WeakMap();
    document.addEventListener("submit", function (event) {
      var form = event.target;
      if (!(form instanceof HTMLFormElement)) return;
      var action = form.getAttribute("action") || "";
      if (action.indexOf("handler=GeneratePdf") < 0 || event.defaultPrevented) return;
      if (!folder && !form.getAttribute("data-dmo-pdf-acompanha")) return;
      event.preventDefault();
      // Como o envio normal: campos codificados para URL, com o botão carregado.
      var body = new URLSearchParams(new FormData(form, event.submitter || undefined));
      var chave = body.toString();
      if (emCurso.get(form) === chave) return;
      emCurso.set(form, chave);
      function acabou() {
        if (emCurso.get(form) === chave) emCurso.delete(form);
      }

      (folder ? ensurePermission(folder) : Promise.resolve(false)).then(function (allowed) {
        return fetch(form.action, { method: "POST", body: body, credentials: "same-origin" }).then(function (response) {
          if (!isPdf(response)) {
            // Uma recusa (sem leituras, sem permissão…) volta como página:
            // o envio normal mostra-a como sempre.
            form.submit();
            return;
          }
          var companion = companionRequest(response, body);
          return store(folder, allowed, response).then(function (main) {
            if (!companion) {
              if (folder) toast(main.ok ? "PDF guardado em " + main.text + "." : main.text, main.ok ? "success" : "warn");
              return;
            }
            return fetch(companion.action, { method: "POST", body: companion.body, credentials: "same-origin" }).then(function (extra) {
              if (!isPdf(extra)) {
                toast("O PDF das punções não foi gerado. Gere-o na folha PU NNPB.", "warn");
                return;
              }
              return store(folder, allowed, extra).then(function (second) {
                if (!folder) {
                  toast("PDFs descarregados: " + main.text + " e " + second.text + ".", "success");
                } else if (main.ok && second.ok) {
                  toast("PDFs guardados em " + main.text + " e " + second.text + ".", "success");
                } else {
                  toast(main.ok ? second.text : main.text, "warn");
                }
              });
            }, function () {
              toast("O PDF das punções não foi gerado. Gere-o na folha PU NNPB.", "warn");
            });
          });
        });
      }).catch(function () {
        form.submit();
      }).then(acabou, acabou);
    }, true);
  }

  // O cartão das Definições do Controlo: escolher, ver e deixar de usar.
  function bindSettings(folder) {
    var card = document.querySelector("[data-dmo-pasta-pdf]");
    if (!card) return;
    var name = card.querySelector("[data-dmo-pasta-pdf-nome]");
    var choose = card.querySelector("[data-dmo-pasta-pdf-escolher]");
    var forget = card.querySelector("[data-dmo-pasta-pdf-esquecer]");
    var unsupported = card.querySelector("[data-dmo-pasta-pdf-sem-suporte]");
    // A pasta esperada (Definições → Controlo → Geral): a escolhida neste
    // computador com outro nome dá aviso, mas fica (nunca bloqueia).
    var different = card.querySelector("[data-dmo-pasta-pdf-diferente]");

    // Lida de cada vez: gravar o nome troca a zona sem recarregar.
    function expectedName() {
      var holder = card.querySelector("[data-dmo-pasta-pdf-esperada]");
      return holder ? (holder.getAttribute("data-dmo-pasta-pdf-esperada") || "").trim() : "";
    }

    function differs(current) {
      var expected = expectedName();
      return !!(current && expected && current.name.trim().toLowerCase() !== expected.toLowerCase());
    }

    function show(current) {
      if (name) name.textContent = current ? current.name : "Nenhuma (Transferências)";
      if (forget) forget.hidden = !current;
      if (different) different.hidden = !differs(current);
    }

    if (!supported) {
      if (unsupported) unsupported.hidden = false;
      if (choose) choose.hidden = true;
      if (forget) forget.hidden = true;
      if (name) name.textContent = "Nenhuma (Transferências)";
      return;
    }

    show(folder);
    // Depois de gravar o nome da pasta, o aviso acompanha o nome novo.
    document.addEventListener("dmo:troca", function () { show(folder); });
    if (choose) {
      choose.hidden = false;
      choose.addEventListener("click", function () {
        window.showDirectoryPicker({ id: "dmo-pdfs", mode: "readwrite" }).then(function (handle) {
          return saveFolder(handle).then(function () {
            folder = handle;
            show(handle);
            if (differs(handle)) toast("A pasta escolhida («" + handle.name + "») não tem o nome da pasta definida («" + expectedName() + "»). Os PDFs gravam-se na escolhida.", "aviso");
            else toast("Pasta dos PDFs associada: " + handle.name + ".", "success");
          });
        }).catch(function (error) {
          if (error && error.name === "AbortError") return;
          toast("Não foi possível guardar a pasta escolhida.", "error");
        });
      });
    }
    if (forget) {
      forget.addEventListener("click", function () {
        forgetFolder().then(function () {
          folder = null;
          show(null);
          toast("Os PDFs voltam a ir para as Transferências do navegador.", "info");
        });
      });
    }
  }

  loadFolder().then(function (folder) {
    bindSettings(folder || null);
    // Sem pasta, só os PDFs que trazem outro a acompanhar (precisa do fetch).
    if (folder || typeof window.fetch === "function") bindPdfForms(folder || null);
  });
})();

/*
 * dmo-excel-bq.js — o Excel das Boquilhas do chefe (Diogo, 21/10/2026).
 *
 * «Cada registo que faço passa a guardar a informação nesse excel que está
 * numa pasta do pc.» O ficheiro escolhe-se uma vez neste computador, nas
 * Definições das Boquilhas, e fica guardado só neste navegador (IndexedDB,
 * File System Access API: Chrome e Edge), tal como a pasta dos PDFs.
 *
 * Depois de cada registo (e ao abrir as Boquilhas), pergunta ao servidor a
 * versão dos movimentos; se mudou desde a última escrita, envia o ficheiro,
 * recebe-o com os dias escritos (desde o dia em que foi escolhido) e grava-o
 * por cima. Com o Excel aberto o Windows não deixa gravar: avisa e tenta de
 * novo no registo seguinte. O ficheiro nunca fica no servidor.
 */
(function () {
  "use strict";

  if (window.DMOExcelBq) return;

  var DB_NAME = "dmo-excel-bq";
  var STORE = "excel";
  var script = document.currentScript;
  var base = (script && script.getAttribute("data-bq-excel-url")) || "/Boquilhas/Definicoes";
  var supported = typeof window.showOpenFilePicker === "function" && typeof window.showDirectoryPicker === "function" && "indexedDB" in window;
  var aEscrever = null;
  var avisouFalha = false;
  var pendente = false;
  var manualPendente = false;
  var atualizarPendente = false;
  var estadoAtual = "";

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

  function load() {
    if (!supported) return Promise.resolve(null);
    return withStore("readonly", function (store) { return store.get("ligado"); }).catch(function () { return null; });
  }

  function save(ligado) {
    return withStore("readwrite", function (store) { return store.put(ligado, "ligado"); });
  }

  function forget() {
    return withStore("readwrite", function (store) { return store.delete("ligado"); });
  }

  function hoje() {
    var d = new Date();
    var mm = String(d.getMonth() + 1).padStart(2, "0");
    var dd = String(d.getDate()).padStart(2, "0");
    return d.getFullYear() + "-" + mm + "-" + dd;
  }

  function dataPt(iso) {
    var p = String(iso || "").split("-");
    return p.length === 3 ? p[2] + "/" + p[1] + "/" + p[0] : iso;
  }

  function token() {
    var campo = document.querySelector("input[name=__RequestVerificationToken]");
    return campo ? campo.value : "";
  }

  function permission(handle, ask) {
    var options = { mode: "readwrite" };
    return handle.queryPermission(options).then(function (state) {
      if (state === "granted") return true;
      if (!ask) return false;
      return handle.requestPermission(options).then(function (asked) { return asked === "granted"; });
    });
  }

  function versaoAtual() {
    return fetch(base + "?handler=ExcelVersao", { headers: { Accept: "application/json" }, credentials: "same-origin" })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (j) { return j && j.versao; })
      .catch(function () { return null; });
  }

  // manual: o clique em «Atualizar» ou na escolha do ficheiro (pode pedir a
  // licença de escrita e escreve mesmo com a versão igual).
  // atualizar: o botão «Atualizar» (Diogo, 22/10/2026): o servidor escreve
  // hoje e recua a partir de ontem, um dia de cada vez, até a um dia igual.
  function escrever(manual, atualizar) {
    pendente = true;
    manualPendente = manualPendente || !!manual;
    atualizarPendente = atualizarPendente || !!atualizar;
    if (aEscrever) return aEscrever;
    async function fila() {
      var resultado = null;
      while (pendente) {
        var pedir = manualPendente;
        var reconciliar = atualizarPendente;
        pendente = manualPendente = atualizarPendente = false;
        estadoAtual = "A atualizar…";
        mostrar();
        resultado = await escreverUmaVez(pedir, reconciliar);
        if (!resultado) break; // Uma recusa não se repete sem um novo evento/clique.
        var ligado = await load();
        var versao = await versaoAtual();
        if (ligado && versao && versao !== ligado.versao) pendente = true;
      }
      return resultado;
    }
    // O ficheiro e os metadados são lidos DEPOIS do trinco: outra aba pode
    // ter acabado de escrever. O trinco nunca substitui a fila desta aba.
    aEscrever = Promise.resolve().then(function () {
      return navigator.locks && navigator.locks.request
        ? navigator.locks.request("dmo-excel-bq-escrita", fila) : fila();
    }).then(function (resultado) {
      aEscrever = null;
      estadoAtual = resultado ? "" : "Por atualizar";
      mostrar();
      return resultado;
    }, function () {
      aEscrever = null;
      estadoAtual = "Por atualizar";
      mostrar();
      return null;
    });
    return aEscrever;
  }

  function escreverUmaVez(manual, atualizar) {
    return load().then(function (ligado) {
      if (!ligado || !ligado.handle) return null;
      return permission(ligado.handle, manual).then(function (ok) {
        if (!ok) {
          return versaoAtual().then(function (versao) {
            if (versao && versao !== ligado.versao && !avisouFalha) {
              avisouFalha = true;
              toast("O Excel das Boquilhas está por atualizar: nas Definições das Boquilhas, carregue em «Atualizar».", "aviso");
            }
            return null;
          });
        }
        return versaoAtual().then(function (versao) {
          if (!manual && versao && versao === ligado.versao) return true;
          var mensagem = null;
          return ligado.handle.getFile().then(function (ficheiro) {
            var corpo = new FormData();
            corpo.append("livro", ficheiro, ficheiro.name);
            if (!atualizar) corpo.append("desde", ligado.desde || hoje());
            // As linhas que a app já escreveu: só essas volta a mexer.
            corpo.append("escritas", JSON.stringify(ligado.escritas || []));
            corpo.append("__RequestVerificationToken", token());
            return fetch(base + "?handler=" + (atualizar ? "ExcelAtualizar" : "Excel"), { method: "POST", body: corpo, credentials: "same-origin" });
          }).then(function (resposta) {
            if (!resposta.ok) {
              return resposta.text().then(function (texto) {
                toast(resposta.status === 422 && texto ? texto : "O Excel das Boquilhas não foi atualizado: o servidor não respondeu.", "erro");
                return null;
              });
            }
            var novaVersao = resposta.headers.get("X-Dmo-Excel-Versao") || versao;
            var avisos = resposta.headers.get("X-Dmo-Excel-Avisos");
            var escritas = resposta.headers.get("X-Dmo-Excel-Escritas");
            var texto = resposta.headers.get("X-Dmo-Excel-Mensagem");
            var completo = resposta.headers.get("X-Dmo-Excel-Completo") !== "false" && !avisos;
            if (texto) mensagem = decodeURIComponent(texto);
            return resposta.blob().then(function (livro) {
              return ligado.handle.createWritable().then(function (escrita) {
                return escrita.write(livro).then(function () { return escrita.close(); });
              });
            }).then(function () {
              ligado.versao = novaVersao;
              if (escritas) ligado.escritas = JSON.parse(decodeURIComponent(escritas));
              ligado.escritoEm = new Date().toISOString();
              ligado.pendencias = !completo;
              avisouFalha = false;
              return save(ligado).then(function () {
                toast(completo ? (mensagem || "Excel das Boquilhas atualizado.") : "Excel das Boquilhas atualizado com pendências: nem todos os movimentos foram escritos.", completo ? "ok" : "aviso");
                if (avisos) decodeURIComponent(avisos).split("\n").forEach(function (aviso) { toast(aviso, "aviso"); });
                mostrar();
                return true;
              });
            });
          });
        });
      });
    }).catch(function () {
      if (manual || !avisouFalha) {
        avisouFalha = true;
        toast("Não foi possível gravar o Excel das Boquilhas. Se estiver aberto, feche-o e use «Atualizar» nas Definições das Boquilhas.", "erro");
      }
      return null;
    });
  }

  // O cartão das Definições das Boquilhas.
  function mostrar() {
    var card = document.querySelector("[data-bq-excel]");
    if (!card) return;
    var estado = card.querySelector("[data-bq-excel-estado]");
    var ligadas = card.querySelectorAll("[data-bq-excel-ligado]");
    var escolher = card.querySelector("[data-bq-excel-pasta]");
    if (!supported) {
      card.querySelector("[data-bq-excel-sem-suporte]").hidden = false;
      estado.hidden = true;
      escolher.disabled = true;
      return;
    }
    load().then(function (ligado) {
      var tem = !!(ligado && ligado.handle);
      Array.prototype.forEach.call(ligadas, function (item) { item.hidden = !tem; });
      escolher.textContent = tem ? "Escolher outra pasta" : "Escolher a pasta";
      var sincronizacao = card.querySelector("[data-bq-excel-sincronizacao]");
      if (!sincronizacao) {
        sincronizacao = document.createElement("p");
        sincronizacao.setAttribute("data-bq-excel-sincronizacao", "");
        sincronizacao.setAttribute("role", "status");
        card.appendChild(sincronizacao);
      }
      sincronizacao.hidden = !tem;
      sincronizacao.textContent = estadoAtual || (ligado && ligado.pendencias
        ? "Atualizado com pendências: consulte os avisos e use Atualizar."
        : ligado && ligado.escritoEm ? "Atualizado em " + new Date(ligado.escritoEm).toLocaleString("pt-PT") + "." : "Por atualizar");
      estado.textContent = tem
        ? "«" + ligado.handle.name + "» · escreve os movimentos a partir de " + dataPt(ligado.desde) + "."
        : "Nenhum Excel escolhido neste computador.";
    });
  }

  function ligar(handle) {
    return permission(handle, true).then(function (ok) {
      if (!ok) {
        toast("Sem licença para gravar no Excel. Escolha-o de novo e carregue em «Permitir».", "erro");
        return;
      }
      return save({ handle: handle, desde: hoje(), versao: null }).then(function () {
        mostrar();
        return escrever(true);
      });
    });
  }

  document.addEventListener("click", function (event) {
    var alvo = event.target.closest && event.target.closest("[data-bq-excel-pasta],[data-bq-excel-escolher],[data-bq-excel-atualizar],[data-bq-excel-esquecer]");
    if (!alvo || !supported) return;
    if (alvo.hasAttribute("data-bq-excel-pasta")) {
      // A pasta: o Excel do ano fica lá; se ainda não existe, a app cria-o
      // vazio no formato do Excel das Boquilhas.
      var nome = "Saida Entrada BQ " + new Date().getFullYear() + ".xlsx";
      window.showDirectoryPicker({ mode: "readwrite" }).then(function (pasta) {
        return pasta.getFileHandle(nome, { create: true }).then(function (handle) {
          return handle.getFile().then(function (ficheiro) {
            if (ficheiro.size > 0) return handle;
            return fetch(base + "?handler=ExcelNovo&dia=" + hoje(), { credentials: "same-origin" }).then(function (r) {
              if (!r.ok) throw new Error("novo");
              return r.blob();
            }).then(function (livro) {
              return handle.createWritable().then(function (escrita) {
                return escrita.write(livro).then(function () { return escrita.close(); });
              });
            }).then(function () {
              toast("Criado o Excel «" + nome + "» na pasta escolhida.", "ok");
              return handle;
            });
          });
        });
      }).then(function (handle) { return ligar(handle); }, function (erro) {
        if (erro && erro.name === "AbortError") return;
        toast("Não foi possível criar o Excel nessa pasta. Escolha outra pasta.", "erro");
      });
    } else if (alvo.hasAttribute("data-bq-excel-escolher")) {
      alvo.closest("details") && alvo.closest("details").removeAttribute("open");
      window.showOpenFilePicker({
        multiple: false,
        types: [{ description: "Excel", accept: { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx"] } }]
      }).then(function (handles) { return ligar(handles[0]); })
        .catch(function () { /* a escolha cancelada não faz nada */ });
    } else if (alvo.hasAttribute("data-bq-excel-atualizar")) {
      alvo.closest("details") && alvo.closest("details").removeAttribute("open");
      escrever(true, true);
    } else {
      alvo.closest("details") && alvo.closest("details").removeAttribute("open");
      forget().then(function () {
        toast("O Excel das Boquilhas deixou de ser usado neste computador.", "ok");
        mostrar();
      });
    }
  });

  // Depois de cada gravação nas Boquilhas (a troca das zonas sem recusa).
  document.addEventListener("dmo:troca", function (event) {
    if (event.detail && event.detail.recusa) return;
    mostrar();
    escrever(false);
  });

  window.addEventListener("online", function () { escrever(false); });
  document.addEventListener("visibilitychange", function () { if (!document.hidden) escrever(false); });

  window.DMOExcelBq = { escrever: escrever };
  mostrar();
  escrever(false);
})();

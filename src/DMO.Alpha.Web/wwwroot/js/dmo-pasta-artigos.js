/*
 * dmo-pasta-artigos.js — a pasta das imagens dos artigos neste computador
 * (Diogo, 26/10/2026), no mesmo molde da dos PDFs do Controlo: a pasta fica
 * guardada só neste navegador (IndexedDB, File System Access API). A Folha do
 * Job On (jobon-folha.js) lê a mesma chave.
 */
(function () {
  "use strict";
  var card = document.querySelector("[data-dmo-pasta-artigos]");
  if (!card) return;
  var DB = "dmo-pasta-pdf", STORE = "pastas", KEY = "artigos";
  var supported = typeof window.showDirectoryPicker === "function" && "indexedDB" in window;
  var nome = card.querySelector("[data-dmo-pasta-artigos-nome]");
  var escolher = card.querySelector("[data-dmo-pasta-artigos-escolher]");
  var esquecer = card.querySelector("[data-dmo-pasta-artigos-esquecer]");
  var semSuporte = card.querySelector("[data-dmo-pasta-artigos-sem-suporte]");
  var aviso = card.querySelector("[data-dmo-pasta-artigos-aviso]");

  function db(mode, action) {
    return new Promise(function (resolve, reject) {
      var open = indexedDB.open(DB, 1);
      open.onupgradeneeded = function () { open.result.createObjectStore(STORE); };
      open.onerror = function () { reject(open.error); };
      open.onsuccess = function () {
        var d = open.result, tx = d.transaction(STORE, mode), req = action(tx.objectStore(STORE));
        tx.oncomplete = function () { d.close(); resolve(req && req.result); };
        tx.onerror = function () { d.close(); reject(tx.error); };
      };
    });
  }
  function say(texto) { if (aviso) { aviso.textContent = texto; aviso.hidden = !texto; } }
  function show(handle) {
    nome.textContent = handle ? handle.name : "Nenhuma";
    if (esquecer) esquecer.hidden = !handle;
  }

  if (!supported) {
    if (semSuporte) semSuporte.hidden = false;
    return;
  }
  escolher.hidden = false;
  db("readonly", function (s) { return s.get(KEY); }).then(show, function () { show(null); });
  escolher.addEventListener("click", function () {
    window.showDirectoryPicker({ id: "dmo-artigos", mode: "readwrite" }).then(function (handle) {
      return db("readwrite", function (s) { return s.put(handle, KEY); }).then(function () {
        show(handle);
        say("Pasta das imagens associada: " + handle.name + ".");
      });
    }).catch(function (error) {
      if (error && error.name === "AbortError") return;
      say("Não foi possível guardar a pasta escolhida.");
    });
  });
  if (esquecer) {
    esquecer.addEventListener("click", function () {
      db("readwrite", function (s) { return s.delete(KEY); }).then(function () {
        show(null);
        say("As imagens dos artigos voltam a ficar só neste navegador.");
      });
    });
  }
})();

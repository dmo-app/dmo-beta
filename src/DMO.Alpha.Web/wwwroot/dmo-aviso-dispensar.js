// O «×» do aviso «O Job On mudou depois de este registo ser gravado»
// (Diogo, 29/10/2026): fecha o aviso e lembra, neste browser, até que
// alteração o operador já viu; uma mudança mais nova faz o aviso voltar.
(function () {
  'use strict';
  var PREFIXO = 'dmo-aviso-mudou-';

  function lido(chave) {
    try { return Number(window.localStorage.getItem(PREFIXO + chave)) || 0; } catch (e) { return 0; }
  }

  function guardar(chave, ate) {
    try { window.localStorage.setItem(PREFIXO + chave, String(ate)); } catch (e) { /* sem armazenamento: fecha só desta vez */ }
  }

  function ate(aviso) { return Number(aviso.getAttribute('data-dmo-aviso-ate')) || 0; }

  function esconderVistos(raiz) {
    Array.prototype.forEach.call((raiz || document).querySelectorAll('[data-dmo-aviso-dispensar]'), function (aviso) {
      aviso.hidden = lido(aviso.getAttribute('data-dmo-aviso-dispensar')) >= ate(aviso);
    });
  }

  document.addEventListener('click', function (event) {
    var botao = event.target.closest && event.target.closest('[data-dmo-aviso-fechar]');
    var aviso = botao && botao.closest('[data-dmo-aviso-dispensar]');
    if (!aviso) { return; }
    guardar(aviso.getAttribute('data-dmo-aviso-dispensar'), ate(aviso));
    aviso.hidden = true;
  });

  document.addEventListener('dmo:troca', function () { esconderVistos(document); });
  esconderVistos(document);
})();

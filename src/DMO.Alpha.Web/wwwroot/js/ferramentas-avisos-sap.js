// O × fecha só este aviso nesta vista; «Não» é a decisão persistente no servidor.
(function () {
  'use strict';
  document.addEventListener('click', function (event) {
    const fechar = event.target.closest && event.target.closest('[data-sap-fechar]');
    if (!fechar) return;
    const aviso = fechar.closest('[data-sap-aviso]');
    if (aviso) aviso.hidden = true;
  });
})();

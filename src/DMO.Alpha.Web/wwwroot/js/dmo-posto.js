// O posto da Reparação Interna: o mesmo computador para várias pessoas
// (Diogo, 27/10/2026: «3min auto e opções nas definições para aumentar»).
// Uma sessão aberta pelo posto fecha sozinha ao fim de N minutos sem uso
// (N vem das Definições da Reparação, data-dmo-posto-minutos) e volta ao
// posto para a pessoa seguinte. Enquanto alguém usa a página, avisa o
// servidor de vez em quando para a sessão não acabar a meio de um registo.
(() => {
  'use strict';
  const minutos = Number(document.body && document.body.dataset.dmoPostoMinutos);
  if (!Number.isFinite(minutos) || minutos <= 0) return;
  const limite = minutos * 60000;
  const intervaloAviso = Math.min(60000, limite / 3);
  let ultimoUso = Date.now();
  let ultimoAviso = ultimoUso; // abrir a página já renovou a sessão

  function usar(agora = Date.now()) {
    ultimoUso = agora;
    if (agora - ultimoAviso < intervaloAviso) return;
    ultimoAviso = agora;
    if (typeof window.fetch === 'function') {
      window.fetch('/posto/ativo', { credentials: 'same-origin', cache: 'no-store' }).catch(() => {});
    }
  }

  function verificar(agora = Date.now()) {
    if (agora - ultimoUso < limite) return false;
    window.DmoPosto.sair();
    return true;
  }

  function sair() {
    window.location.assign('/logout?posto=1');
  }

  ['pointerdown', 'keydown', 'input', 'wheel', 'touchstart'].forEach((tipo) =>
    document.addEventListener(tipo, () => usar(), { capture: true, passive: true }));
  setInterval(() => verificar(), 5000);
  window.DmoPosto = { verificar, usar, sair };
})();

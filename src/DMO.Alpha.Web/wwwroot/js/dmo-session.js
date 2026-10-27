// Session expiry stays sourced from the authenticated cookie, not a mock user.
(() => {
  'use strict';
  const label = document.querySelector('[data-dmo-sessao-fim]');
  if (!label) return;
  const end = new Date(label.dataset.dmoSessaoFim);
  if (!Number.isFinite(end.getTime())) return;
  const pad = n => String(n).padStart(2, '0');
  function update() {
    const minutes = Math.max(0, Math.round((end - new Date()) / 60000));
    label.textContent = `Sessão até ${pad(end.getHours())}:${pad(end.getMinutes())} · faltam ${Math.floor(minutes / 60)} h ${pad(minutes % 60)} min`;
  }
  update();
  setInterval(update, 30000);
})();

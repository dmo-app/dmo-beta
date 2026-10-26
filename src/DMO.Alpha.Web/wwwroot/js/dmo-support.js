// Contextual pages slide in beside the workspace. Main navigation never enters here.
(() => {
  'use strict';
  const panel = document.getElementById('dmoSupport');
  if (!panel || window.frameElement?.classList.contains('dmo-support-frame')) return;
  const frame = panel.querySelector('iframe');
  const title = panel.querySelector('h2');
  const full = panel.querySelector('#dmoSupportFull');
  const confirm = panel.querySelector('.dmo-support-confirm');
  const loading = panel.querySelector('.dmo-support-loading');
  const refresh = document.querySelector('.dmo-support-refresh');
  let opener, dirty = false, operation = false;
  const destinations = new Map([
    ['/Ferramentas/Ficha', 'Ficha da ferramenta'],
    ['/Controlo/Peso', 'Peso'],
    ['/Controlo/Pegamentos', 'Pegamentos'],
    ['/Controlo/Puncoes', 'PU NNPB'],
    ['/Controlo/Approve', 'Revisão do controlo'],
    ['/ReparacaoInterna/Consultar', 'Consulta de reparação']
  ]);
  function close() {
    panel.close(); frame.removeAttribute('src'); dirty = false;
    document.documentElement.classList.remove('dmo-support-open');
    confirm.hidden = true;
    if (operation) refresh.hidden = false;
    devolverFoco();
  }
  // O foco volta para quem abriu o painel. Uma linha de tabela não é
  // focável: sem isto o foco caía no <body> e o teclado perdia o lugar.
  function devolverFoco() {
    opener?.focus?.({ preventScroll: true });
    if (document.activeElement && document.activeElement !== document.body) return;
    const lista = opener?.closest?.('[data-dmo-list]') || opener?.querySelector?.('[data-dmo-list]');
    if (lista) { lista.tabIndex = -1; lista.focus({ preventScroll: true }); return; }
    opener?.querySelector?.(':is(a,button,input,select,textarea,[tabindex])')?.focus?.({ preventScroll: true });
  }
  function requestClose() {
    if (!dirty) return close();
    confirm.hidden = false;
    confirm.querySelector('button').focus();
  }
  function tryOpen(href, source) {
    if (source?.closest('.dmo-shell-header,nav') || panel.open) return false;
    let url;
    try { url = new URL(href, location.href); } catch { return false; }
    if (url.origin !== location.origin || !destinations.has(url.pathname)) return false;
    opener = source || document.activeElement;
    dirty = false; operation = false; confirm.hidden = true;
    title.textContent = destinations.get(url.pathname);
    full.href = url.href;
    url.searchParams.set('painel', '1');
    loading.hidden = false;
    frame.src = url.href;
    panel.showModal();
    document.documentElement.classList.add('dmo-support-open');
    return true;
  }
  frame.addEventListener('load', () => {
    if (!panel.open || !frame.hasAttribute('src')) return;
    loading.hidden = true;
    let doc;
    try { doc = frame.contentDocument; } catch { return; }
    if (!doc) return;
    doc.documentElement.dataset.dmoEmbedded = 'true';
    dirty = false;
    full.href = frame.contentWindow.location.href.replace(/([?&])painel=1(&|$)/, (_, before, after) => after ? before : '');
    doc.addEventListener('input', event => { if (event.target.closest('form[method="post"],form[method="POST"]')) dirty = true; });
    doc.addEventListener('change', event => { if (event.target.closest('form[method="post"],form[method="POST"]')) dirty = true; });
    doc.addEventListener('submit', event => { if (event.target.method.toLowerCase() === 'post') operation = true; });
    // Escape from inside the frame closes the outer drawer only if no local dialog is open.
    doc.addEventListener('keydown', event => {
      if (event.key === 'Escape' && !doc.querySelector('dialog[open],.dmo-modal-backdrop.open')) {event.preventDefault(); requestClose();}
    });
  });
  panel.addEventListener('cancel', event => {event.preventDefault(); requestClose();});
  panel.querySelector('[data-support-close]').addEventListener('click', requestClose);
  panel.querySelector('[data-support-keep]').addEventListener('click', () => {confirm.hidden=true; frame.focus();});
  panel.querySelector('[data-support-discard]').addEventListener('click', close);
  refresh.querySelector('[data-support-refresh]').addEventListener('click', () => location.reload());
  refresh.querySelector('[data-support-dismiss]').addEventListener('click', () => {refresh.hidden=true;});
  document.addEventListener('click', event => {
    if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const link = event.target.closest('a[href]');
    if (!link || link.target || link.hasAttribute('download')) return;
    if (tryOpen(link.href, link)) {event.preventDefault(); event.stopImmediatePropagation();}
  }, true);
  window.DmoSupport = {tryOpen};
})();

// Painéis deslizantes: o abrir e fechar de todos os <dmo-painel> (a estrutura fixa,
// docs/design/estrutura.md). Abre com um botão data-dmo-painel-abrir="id" ou com
// DmoPainel.abrir('id'); fecha com «Fechar», Escape ou um clique fora. Com
// data-dmo-painel-confirmar, um formulário mudado dentro do painel pede
// «Continuar a editar / Fechar sem guardar» antes de fechar. O painel de apoio
// (iframe) tem o seu próprio script: fica de fora (data-dmo-painel-proprio).
(() => {
  'use strict';
  const raiz = document.documentElement;
  const estado = new WeakMap();
  const doPainel = el => (el instanceof Element && el.closest('dialog[data-dmo-estrutura="painel"]:not([data-dmo-painel-proprio])')) || null;
  const painelDe = alvo => typeof alvo === 'string' ? document.getElementById(alvo) : alvo;
  const info = painel => { if (!estado.has(painel)) estado.set(painel, { opener: null, mudou: false }); return estado.get(painel); };
  const aviso = painel => painel.querySelector(':scope > .dmo-painel-aviso');

  // Um painel de cada vez (Diogo, 27/10/2026: «consigo spammar sliding
  // windows»): o mesmo não volta a abrir por cima de si próprio e outro fecha
  // o que está aberto (com alterações por guardar, fica o aviso e nada abre).
  // O painel de apoio, aberto, também fica: abre-se primeiro o que se fecha.
  function abrir(alvo, opener) {
    const painel = painelDe(alvo);
    if (!painel || painel.open) return false;
    if (document.querySelector('dialog[data-dmo-estrutura="painel"][data-dmo-painel-proprio][open]')) return false;
    if (!fecharTodos()) return false;
    const i = info(painel);
    i.opener = opener || document.activeElement; i.mudou = false;
    aviso(painel).hidden = true;
    painel.showModal();
    raiz.classList.add('dmo-painel-aberto');
    i.opener?.setAttribute?.('aria-expanded', 'true');
    // O foco vai para o primeiro campo do corpo; sem campos, para o próprio corpo.
    const corpo = painel.querySelector(':scope > .dmo-painel-corpo');
    const campo = corpo?.querySelector('input:not([type=hidden]):not([disabled]),select:not([disabled]),textarea:not([disabled])');
    if (campo) campo.focus({ preventScroll: true });
    else if (corpo) { corpo.tabIndex = -1; corpo.focus({ preventScroll: true }); }
    painel.dispatchEvent(new CustomEvent('dmo-painel:aberto', { bubbles: true }));
    return true;
  }

  function fechar(alvo, forcar) {
    const painel = painelDe(alvo);
    if (!painel || !painel.open) return false;
    const i = info(painel);
    if (!forcar && i.mudou && painel.hasAttribute('data-dmo-painel-confirmar')) {
      const a = aviso(painel); a.hidden = false; a.querySelector('button')?.focus();
      return false;
    }
    painel.close();
    if (!document.querySelector('dialog[data-dmo-estrutura="painel"][open]')) raiz.classList.remove('dmo-painel-aberto');
    i.mudou = false;
    i.opener?.setAttribute?.('aria-expanded', 'false');
    i.opener?.focus?.({ preventScroll: true });
    painel.dispatchEvent(new CustomEvent('dmo-painel:fechado', { bubbles: true }));
    return true;
  }

  // Fecha os painéis abertos desta estrutura; false se algum ficou com o aviso
  // das alterações por guardar.
  function fecharTodos() {
    return [...document.querySelectorAll('dialog[data-dmo-estrutura="painel"]:not([data-dmo-painel-proprio])[open]')]
      .every(outro => fechar(outro));
  }

  document.addEventListener('click', event => {
    const abre = event.target.closest('[data-dmo-painel-abrir]');
    if (abre) { event.preventDefault(); abrir(abre.dataset.dmoPainelAbrir, abre); return; }
    const painel = doPainel(event.target);
    if (!painel) return;
    if (event.target.closest('[data-dmo-painel-fechar]')) return void fechar(painel);
    if (event.target.closest('[data-dmo-painel-continuar]')) { aviso(painel).hidden = true; return; }
    if (event.target.closest('[data-dmo-painel-descartar]')) return void fechar(painel, true);
    // Clicar fora (no fundo do <dialog>) fecha.
    if (event.target === painel) {
      const r = painel.getBoundingClientRect();
      if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) fechar(painel);
    }
  });
  document.addEventListener('cancel', event => {
    const painel = doPainel(event.target);
    if (painel && event.target === painel) { event.preventDefault(); fechar(painel); }
  }, true);
  const marcar = event => {
    const painel = doPainel(event.target);
    if (painel && event.target.closest('form')) info(painel).mudou = true;
  };
  document.addEventListener('input', marcar);
  document.addEventListener('change', marcar);

  window.DmoPainel = { abrir, fechar, fecharTodos };
})();

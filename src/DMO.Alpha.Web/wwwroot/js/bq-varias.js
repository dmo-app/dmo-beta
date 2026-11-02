// VÁRIAS LINHAS DE UMA VEZ (Diogo, 02/11/2026, DMO-721): com «Várias linhas»
// ligado, um clique no cartão de uma linha escolhe-o em vez de o abrir, e o
// formulário ganha a linha dele. O tipo (Saída ou Entrada), a data e a hora
// são os mesmos para todas: um tipo de movimento por registo. Com Entrada,
// cada linha tem «Reparadas» e «Sem reparação» (ex.: 30 e 22 no mesmo registo).
(function () {
  'use strict';
  let ligado = false;

  function form() { return document.getElementById('bqVarias'); }
  function cartoes() { return document.querySelectorAll('#registo .beta-production-lines button[data-id]'); }
  function tipo() { return form()?.querySelector('input[name=movement_type]:checked')?.value || 'saida'; }

  function atualizar() {
    const f = form();
    if (!f) return;
    const n = f.querySelectorAll('.bq-varias-linha').length;
    f.querySelector('.bq-varias-vazio').hidden = n > 0;
    const entrada = tipo() === 'entrada';
    f.querySelectorAll('.bq-varias-sr').forEach((c) => { c.hidden = !entrada; });
    f.querySelectorAll('.bq-varias-qtd-rotulo').forEach((r) => { r.textContent = entrada ? 'Reparadas' : 'Qtd. BQ'; });
    const b = f.querySelector('button[type=submit]');
    b.disabled = n === 0;
    b.textContent = n > 1 ? `Registar ${n} linhas` : 'Registar';
  }

  function linhaHtml(id, codigo) {
    const d = document.createElement('div');
    d.className = 'bq-movement-line bq-varias-linha';
    d.dataset.id = id;
    d.innerHTML = `
      <input type="hidden" name="linha_bq_id" value="${id}" />
      <strong class="bq-varias-codigo"></strong>
      <div class="field dmo-field bq-line-quantity"><label class="bq-varias-qtd-rotulo">Qtd. BQ</label><input name="linha_qtd" type="number" lang="en" min="0" placeholder="Ex.: 1" /></div>
      <div class="field dmo-field bq-line-quantity bq-varias-sr" hidden><label>Sem reparação</label><input name="linha_qtd_sr" type="number" lang="en" min="0" placeholder="Ex.: 1" /></div>
      <div class="field dmo-field bq-line-quantity"><label>Qtd. AN</label><input name="linha_qtd_an" type="number" lang="en" min="0" placeholder="Ex.: 1" /></div>`;
    d.querySelector('.bq-varias-codigo').textContent = codigo;
    return d;
  }

  // O rótulo da linha veste o cartão dela: a mesma cor e o mesmo formato, mais baixo.
  // Lê a cor de uma cópia do cartão, para não apanhar a cor do rato por cima.
  function vestir(rotulo, cartao) {
    const copia = cartao.cloneNode(true);
    copia.classList.remove('bq-varias-escolhido', 'selected');
    copia.style.cssText = 'position:absolute;visibility:hidden;pointer-events:none';
    cartao.parentNode.appendChild(copia);
    const c = getComputedStyle(copia);
    rotulo.style.background = c.backgroundColor;
    rotulo.style.color = c.color;
    rotulo.style.borderRadius = c.borderRadius;
    copia.remove();
  }

  function alternar(cartao) {
    const f = form();
    const id = cartao.dataset.id;
    const existente = f.querySelector(`.bq-varias-linha[data-id="${id}"]`);
    if (existente) {
      existente.remove();
      cartao.classList.remove('bq-varias-escolhido');
      cartao.setAttribute('aria-pressed', 'false');
    } else {
      const spans = cartao.querySelectorAll('span');
      const producao = spans.length > 1 ? spans[spans.length - 1].textContent.trim() : '';
      const codigo = [cartao.querySelector('strong')?.textContent || '', producao].filter(Boolean).join(' · ');
      const nova = linhaHtml(id, codigo);
      f.querySelector('.bq-varias-linhas').appendChild(nova);
      vestir(nova.querySelector('.bq-varias-codigo'), cartao);
      cartao.classList.add('bq-varias-escolhido');
      cartao.setAttribute('aria-pressed', 'true');
      f.querySelector(`.bq-varias-linha[data-id="${id}"] input[name=linha_qtd]`).focus();
    }
    atualizar();
  }

  function ligar(sim) {
    ligado = sim;
    const f = form();
    const botao = document.getElementById('bqVariasLigar');
    if (!f || !botao) return;
    botao.setAttribute('aria-pressed', sim ? 'true' : 'false');
    f.hidden = !sim;
    document.getElementById('bqRegistoAberto')?.toggleAttribute('hidden', sim);
    if (!sim) {
      f.querySelector('.bq-varias-linhas').innerHTML = '';
      cartoes().forEach((c) => { c.classList.remove('bq-varias-escolhido'); c.removeAttribute('aria-pressed'); });
    }
    atualizar();
  }

  // Depois de registar, a página volta com «Várias linhas» ligado.
  if (new URLSearchParams(location.search).get('varias') === '1') {
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => ligar(true));
    else ligar(true);
  }

  document.addEventListener('change', (e) => {
    if (e.target.closest && e.target.closest('#bqVarias') && e.target.name === 'movement_type') atualizar();
  });
  // Em captura, antes do ouvinte que abre a linha: ligado, o clique escolhe.
  document.addEventListener('click', (e) => {
    if (e.target.closest && e.target.closest('#bqVariasLigar')) {
      ligar(!ligado);
      return;
    }
    if (!ligado) return;
    const cartao = e.target.closest && e.target.closest('#registo .beta-production-lines button[data-id]');
    if (!cartao || !cartao.dataset.id || cartao.classList.contains('bq-line-empty')) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    alternar(cartao);
  }, true);
  document.addEventListener('keydown', (e) => {
    if (!ligado || e.key !== 'Enter') return;
    const cartao = e.target.closest && e.target.closest('#registo .beta-production-lines button[data-id]');
    if (!cartao) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    alternar(cartao);
  }, true);
})();

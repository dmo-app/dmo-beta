// VÁRIAS LINHAS DE UMA VEZ (Diogo, 02/11/2026, DMO-721): com «Várias linhas»
// ligado, um clique no cartão de uma linha escolhe-o em vez de o abrir, e
// o formulário ganha a linha dele (Saída/Entrada, Qtd. BQ, Qtd. AN). A data e
// a hora são as mesmas para todas; «Registar» grava tudo de uma vez.
(function () {
  'use strict';
  let ligado = false;

  function form() { return document.getElementById('bqVarias'); }
  function cartoes() { return document.querySelectorAll('#registo .beta-production-lines button[data-id]'); }

  function atualizar() {
    const f = form();
    if (!f) return;
    const n = f.querySelectorAll('.bq-varias-linha').length;
    f.querySelector('.bq-varias-vazio').hidden = n > 0;
    const b = f.querySelector('button[type=submit]');
    b.disabled = n === 0;
    b.textContent = n > 1 ? `Registar ${n} linhas` : 'Registar';
  }

  function linhaHtml(id, codigo, detalhe) {
    const d = document.createElement('div');
    d.className = 'bq-movement-line bq-varias-linha';
    d.dataset.id = id;
    const nome = `t${id}`;
    d.innerHTML = `
      <input type="hidden" name="linha_bq_id" value="${id}" />
      <strong class="bq-varias-codigo"></strong>
      <fieldset class="bq-movement-types">
        <legend class="visually-hidden">Movimento</legend>
        <label><input type="radio" name="${nome}" value="saida" checked /><span>Saída</span></label>
        <label><input type="radio" name="${nome}" value="entrada" /><span>Entrada</span></label>
        <label><input type="radio" name="${nome}" value="entrada_sem_reparacao" /><span>Entrada sem reparação</span></label>
      </fieldset>
      <input type="hidden" name="linha_tipo" value="saida" />
      <div class="field dmo-field bq-line-quantity"><label>Qtd. BQ</label><input name="linha_qtd" type="number" lang="en" min="1" required placeholder="Ex.: 1" /></div>
      <div class="field dmo-field bq-line-quantity"><label>Qtd. AN</label><input name="linha_qtd_an" type="number" lang="en" min="0" placeholder="Ex.: 1" /></div>`;
    d.querySelector('.bq-varias-codigo').textContent = codigo;
    d.querySelector('.bq-varias-codigo').title = detalhe;
    d.addEventListener('change', (e) => {
      if (e.target.name === nome) d.querySelector('input[name=linha_tipo]').value = e.target.value;
    });
    return d;
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
      f.querySelector('.bq-varias-linhas').appendChild(linhaHtml(id, codigo, cartao.title || ''));
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

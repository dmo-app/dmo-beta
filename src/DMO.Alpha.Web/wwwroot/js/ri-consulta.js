// Consulta da Reparação: a Produção mostra só o número (202603); aberta, cada
// produção tem o número e, por baixo, a referência, a linha e o início (Diogo,
// 28/10/2026: «dropdown da produção deve ser apenas os 202601, depois ao expandir
// em baixo mostrar o resto»). O <select> fica (sem JS funciona e é ele que vai no
// formulário e aplica ao escolher); por cima desenha-se a lista. Com a lista
// aberta, um clique fora só a fecha.
(() => {
  'use strict';
  // O rótulo é «referência · nº · linha · início».
  function partes(opcao) {
    const p = opcao.textContent.trim().split(' · ');
    return opcao.value && p.length >= 3
      ? { numero: p[1], resto: [p[0]].concat(p.slice(2)).join(' · ') }
      : { numero: p.join(' · '), resto: '' };
  }
  let fecharAberta = null;
  function montar() {
    const sel = document.getElementById('riConsultaProducao');
    if (!sel || sel.disabled || sel.dataset.riDrop) return;
    sel.dataset.riDrop = '1';
    const caixa = document.createElement('div');
    caixa.className = 'ri-prod-drop';
    const botao = document.createElement('button');
    botao.type = 'button';
    botao.className = 'ri-prod-botao';
    botao.id = 'riConsultaProducaoBotao';
    botao.setAttribute('aria-haspopup', 'listbox');
    botao.setAttribute('aria-expanded', 'false');
    botao.setAttribute('aria-label', 'Produção');
    const lista = document.createElement('ul');
    lista.className = 'ri-prod-lista';
    lista.setAttribute('role', 'listbox');
    lista.hidden = true;
    const texto = () => {
      const o = sel.options[sel.selectedIndex];
      botao.replaceChildren(document.createTextNode(o ? partes(o).numero : ''));
      const seta = document.createElement('span');
      seta.className = 'dmo-caret';
      seta.setAttribute('aria-hidden', 'true');
      botao.appendChild(seta);
    };
    const fechar = () => { lista.hidden = true; botao.setAttribute('aria-expanded', 'false'); if (fecharAberta === fechar) fecharAberta = null; };
    const abrir = () => { lista.hidden = false; botao.setAttribute('aria-expanded', 'true'); fecharAberta = fechar; (lista.querySelector('[aria-selected="true"]') || lista.firstElementChild)?.focus(); };
    const escolher = (i) => {
      fechar();
      botao.focus();
      if (i === sel.selectedIndex) return;
      sel.selectedIndex = i;
      texto();
      sel.dispatchEvent(new Event('change', { bubbles: true }));
    };
    Array.prototype.forEach.call(sel.options, (o, i) => {
      const { numero, resto } = partes(o);
      const li = document.createElement('li');
      li.setAttribute('role', 'option');
      li.tabIndex = -1;
      li.setAttribute('aria-selected', i === sel.selectedIndex ? 'true' : 'false');
      const n = document.createElement('span');
      n.className = 'ri-prod-numero';
      n.textContent = numero;
      li.appendChild(n);
      if (resto) {
        const r = document.createElement('span');
        r.className = 'ri-prod-resto';
        r.textContent = resto;
        li.appendChild(r);
      }
      li.addEventListener('click', () => escolher(i));
      li.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); escolher(i); }
        else if (e.key === 'ArrowDown') { e.preventDefault(); li.nextElementSibling?.focus(); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); li.previousElementSibling?.focus(); }
      });
      lista.appendChild(li);
    });
    botao.addEventListener('click', () => (lista.hidden ? abrir() : fechar()));
    botao.addEventListener('keydown', (e) => { if (e.key === 'ArrowDown' && lista.hidden) { e.preventDefault(); abrir(); } });
    sel.parentNode.insertBefore(caixa, sel);
    caixa.append(sel, botao, lista);
    sel.hidden = true;
    texto();
  }
  // Com a lista aberta, um clique fora só a fecha e não abre o que está por baixo.
  const fora = (e) => {
    if (!fecharAberta || e.target.closest?.('.ri-prod-drop')) return;
    e.preventDefault();
    e.stopPropagation();
    if (e.type === 'click') fecharAberta();
  };
  document.addEventListener('mousedown', fora, true);
  document.addEventListener('click', fora, true);
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && fecharAberta) { const f = fecharAberta; f(); document.getElementById('riConsultaProducaoBotao')?.focus(); } });
  // Carregar numa pessoa do resumo escolhe-a no «Quem» e aplica, como escolher no dropdown (os
  // filtros ficam na página e mostram-na); na escolhida, volta a «Todos».
  document.addEventListener('click', (e) => {
    const nome = e.target.closest?.('a[data-ri-pessoa]');
    const quem = document.getElementById('riConsultaPessoa');
    if (!nome || !quem || e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey) return;
    const valor = nome.getAttribute('data-ri-pessoa');
    if (valor && !Array.prototype.some.call(quem.options, (o) => o.value === valor)) return;
    e.preventDefault();
    quem.value = valor;
    quem.dispatchEvent(new Event('change', { bubbles: true }));
  });
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', montar); else montar();
  // A zona da Produção troca quando muda a referência (DmoTroca).
  document.addEventListener('dmo:troca', () => { fecharAberta = null; montar(); });
})();

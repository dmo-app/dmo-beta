// «Registar sem Job On» (Diogo, 22/10/2026): a BQ é uma só escolha, com o que
// a identifica (referência, lote, quantidade, máquinas, MF, última linha).
// Com JavaScript, o select fica escondido atrás de uma caixa onde se escreve
// para procurar; escolher uma opção põe o tool_id no select. Sem JavaScript,
// o select funciona sozinho (o servidor valida).
(() => {
  const ligar = () => {
    const select = document.querySelector('[data-bq-escolha]');
    if (!select || select.dataset.ligado) return;
    select.dataset.ligado = '1';
    const caixa = document.createElement('div');
    caixa.className = 'bq-escolha';
    const input = document.createElement('input');
    input.type = 'search';
    input.id = select.id + 'Procura';
    input.autocomplete = 'off';
    input.placeholder = 'Escreva a referência ou o lote';
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-expanded', 'false');
    input.setAttribute('aria-controls', select.id + 'Lista');
    const lista = document.createElement('ul');
    lista.id = select.id + 'Lista';
    lista.className = 'bq-escolha-lista';
    lista.setAttribute('role', 'listbox');
    lista.hidden = true;
    // «BQ C002 · Lote 18»: lote sem zeros à esquerda (só na apresentação).
    const curto = texto => {
      const [ref, lote = ''] = texto.split(' · ');
      return `BQ ${ref} · ${lote.replace(/^(Lote\s+)0+(?=\d)/, '$1')}`;
    };
    const opcoes = [...select.options].filter(o => o.value);
    let ativa = -1;
    const visiveis = () => [...lista.children].filter(li => !li.hidden);
    for (const option of opcoes) {
      const li = document.createElement('li');
      li.setAttribute('role', 'option');
      li.dataset.value = option.value;
      li.dataset.procura = option.dataset.procura || option.text.toLowerCase();
      const forte = document.createElement('strong');
      forte.textContent = curto(option.text);
      li.append(forte);
      // O resto que identifica a BQ (sem as máquinas: a linha não fica presa à ficha).
      const resto = option.text.split(' · ').slice(2).filter(p => !p.startsWith('Máq.'));
      if (resto.length) {
        const extra = document.createElement('span');
        extra.textContent = resto.join(' · ');
        li.append(extra);
      }
      lista.append(li);
    }
    const marcar = i => {
      const v = visiveis();
      v.forEach((li, n) => li.setAttribute('aria-selected', n === i ? 'true' : 'false'));
      ativa = i;
      if (v[i]) v[i].scrollIntoView({ block: 'nearest' });
    };
    const abrir = () => { lista.hidden = false; input.setAttribute('aria-expanded', 'true'); };
    const fechar = () => { lista.hidden = true; input.setAttribute('aria-expanded', 'false'); ativa = -1; };
    const filtrar = () => {
      const termos = input.value.toLowerCase().replace(/^bq\s+/, '').split(/\s+/).filter(Boolean);
      if (!termos.length) { fechar(); return; }
      for (const li of lista.children) li.hidden = !termos.every(t => li.dataset.procura.includes(t));
      marcar(-1);
      abrir();
    };
    const escolher = li => {
      select.value = li.dataset.value;
      input.value = curto(select.selectedOptions[0].text);
      select.dispatchEvent(new Event('change', { bubbles: true }));
      fechar();
    };
    input.addEventListener('input', () => { select.value = ''; filtrar(); });
    input.addEventListener('focus', () => { input.select(); if (!select.value && input.value) filtrar(); });
    input.addEventListener('keydown', e => {
      const v = visiveis();
      if (e.key === 'ArrowDown') { e.preventDefault(); abrir(); marcar(Math.min(ativa + 1, v.length - 1)); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); marcar(Math.max(ativa - 1, 0)); }
      else if (e.key === 'Enter' && !lista.hidden) { e.preventDefault(); const li = v[ativa] || (v.length === 1 ? v[0] : null); if (li) escolher(li); }
      else if (e.key === 'Escape' && !lista.hidden) { e.preventDefault(); e.stopPropagation(); fechar(); }
    });
    lista.addEventListener('mousedown', e => {
      const li = e.target.closest('li');
      if (li) { e.preventDefault(); escolher(li); }
    });
    input.addEventListener('blur', () => setTimeout(fechar, 0));
    select.classList.add('bq-escolha-select');
    select.tabIndex = -1;
    select.setAttribute('aria-hidden', 'true');
    select.before(caixa);
    caixa.append(input, lista, select);
    const label = document.querySelector(`label[for="${select.id}"]`);
    if (label) label.htmlFor = input.id;
    if (select.value) input.value = curto(select.selectedOptions[0].text);
    select.addEventListener('invalid', () => input.focus());
  };
  ligar();
  document.addEventListener('dmo:troca', ligar);
})();

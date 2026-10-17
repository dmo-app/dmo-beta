(() => {
  const type = document.getElementById('ferr-new-type');
  const quantity = document.getElementById('ferr-new-qty');
  const manual = document.getElementById('ferr-quantity-manual');
  const notice = document.getElementById('ferr-quantity-notice');
  const choices = document.getElementById('ferr-quantity-choices');
  if (!type || !quantity || !manual || !notice || !choices) return;
  const machines = quantity.form.querySelectorAll('input[name="machine_ids"], input[data-lot-machine]');
  let request = 0;
  // A returned draft or prefilled value is already an explicit choice.
  if (quantity.value !== '') manual.value = '1';
  quantity.addEventListener('input', () => { manual.value = '1'; });
  // Sem botão «Usar sugestão» (Diogo, 17/10/2026: «criar tool aplica sugestão
  // automaticamente, isto é redundante»): o total entra sozinho; o aviso
  // amarelo discreto só aparece quando falta um total ou eles diferem.
  function say(text, warn) {
    notice.textContent = text;
    notice.classList.toggle('dmo-aviso-discreto', !!warn && text !== '');
  }
  async function refresh() {
    const current = ++request;
    choices.replaceChildren();
    const query = new URLSearchParams({ handler: 'LotQuantity', type: type.value });
    machines.forEach(input => { if (input.checked) query.append('machine_ids', input.value); });
    try {
      const response = await fetch('/Ferramentas?' + query, { credentials: 'same-origin' });
      if (!response.ok) throw new Error('Quantity defaults unavailable');
      const result = await response.json();
      if (current !== request) return;
      const suggestion = result.quantity;
      const missing = result.machines.filter(m => m.quantity === null).map(m => m.code);
      const defined = result.machines.filter(m => m.quantity !== null);
      if (manual.value !== '1') {
        manual.value = '0';
        quantity.value = suggestion === null ? '' : String(suggestion);
      }
      if (missing.length) say('Sem total do lote: ' + missing.join(', ') + '.', true);
      else if (suggestion === null && defined.length) say('Totais diferentes: escolha um ou escreva a quantidade.', true);
      else say(manual.value === '1' && suggestion !== null && String(suggestion) !== quantity.value ? 'Valor manual mantido.' : '', false);
      if (manual.value === '1' && missing.length && suggestion !== null) notice.textContent += ' Valor manual mantido.';
      if (suggestion === null && defined.length) defined.forEach(m => {
        const button = document.createElement('button');
        button.type = 'button'; button.className = 'dmo-button';
        button.textContent = m.code + ' · ' + m.quantity;
        button.addEventListener('click', () => { quantity.value = String(m.quantity); manual.value = '1'; });
        choices.append(button);
      });
    } catch (_) {
      if (current === request) say('Não foi possível ler o total do lote. Confirme a quantidade.', true);
    }
  }
  type.addEventListener('change', refresh);
  machines.forEach(input => input.addEventListener('change', refresh));
  refresh();
})();

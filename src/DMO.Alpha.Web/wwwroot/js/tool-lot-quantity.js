(() => {
  const type = document.getElementById('ferr-new-type');
  const quantity = document.getElementById('ferr-new-qty');
  const manual = document.getElementById('ferr-quantity-manual');
  const notice = document.getElementById('ferr-quantity-notice');
  const choices = document.getElementById('ferr-quantity-choices');
  const useSuggestion = document.getElementById('ferr-use-quantity-suggestion');
  if (!type || !quantity || !manual || !notice || !choices || !useSuggestion) return;
  const machines = quantity.form.querySelectorAll('input[name="machine_ids"], input[data-lot-machine]');
  useSuggestion.hidden = false;
  let request = 0;
  let suggestion = null;
  // A returned draft or prefilled value is already an explicit choice.
  if (quantity.value !== '') manual.value = '1';
  function applySuggestion() {
    manual.value = '0';
    quantity.value = suggestion === null ? '' : String(suggestion);
  }
  quantity.addEventListener('input', () => { manual.value = '1'; });
  useSuggestion.addEventListener('click', applySuggestion);
  async function refresh() {
    const current = ++request;
    suggestion = null;
    useSuggestion.disabled = true;
    choices.replaceChildren();
    const query = new URLSearchParams({ handler: 'LotQuantity', type: type.value });
    machines.forEach(input => { if (input.checked) query.append('machine_ids', input.value); });
    try {
      const response = await fetch('/Ferramentas?' + query, { credentials: 'same-origin' });
      if (!response.ok) throw new Error('Quantity defaults unavailable');
      const result = await response.json();
      if (current !== request) return;
      suggestion = result.quantity;
      choices.replaceChildren();
      const descriptions = result.machines.map(m => m.code + ': ' + (m.quantity === null ? 'por definir' : m.quantity));
      notice.textContent = descriptions.length ? descriptions.join(' · ') + (suggestion === null ? '. Escolha ou introduza o total do lote.' : '. Quantidade sugerida.') : 'Escolha as máquinas para sugerir o total do lote.';
      if (manual.value !== '1') applySuggestion();
      else notice.textContent += ' Valor manual mantido.';
      useSuggestion.disabled = suggestion === null;
      if (suggestion === null) result.machines.filter(m => m.quantity !== null).forEach(m => {
        const button = document.createElement('button');
        button.type = 'button'; button.className = 'dmo-button';
        button.textContent = m.code + ' · ' + m.quantity;
        button.addEventListener('click', () => { quantity.value = String(m.quantity); manual.value = '1'; });
        choices.append(button);
      });
    } catch (_) {
      if (current === request) notice.textContent = 'Não foi possível atualizar a sugestão. Valor anterior mantido; confirme o total do lote.';
    }
  }
  type.addEventListener('change', refresh);
  machines.forEach(input => input.addEventListener('change', refresh));
  refresh();
})();

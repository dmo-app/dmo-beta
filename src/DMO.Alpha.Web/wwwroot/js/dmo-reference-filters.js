// Opções dependentes da referência nas consultas e na criação de Job On.
(() => {
  'use strict';
  const states = new WeakMap();
  let sequence = 0;
  const field = (form, name) => [...form.elements].find(e => e.name === name && e.type !== 'hidden');
  const controls = form => [...form.elements].filter(e => e.name && e.name !== form.dataset.dmoReferenceName
    && ['maquina', 'linha', 'machine_id', 'tipo', 'type', 'reparador', 'pessoa', 'trabalho', 'peca', 'fabrica', 'lot', 'lote', 'estado', 'local'].includes(e.name));
  function remember(state, element) {
    const saved = state.original.get(element);
    if (saved && (!element.options || (saved.options?.length === element.options.length
      && saved.options.every((o, i) => o.option === element.options[i])))) return;
    state.original.set(element, {
      disabled: element.disabled,
      options: element.options ? [...element.options].map(o => ({ option: o, hidden: o.hasAttribute('data-dmo-reference-excluded') ? false : o.hidden, disabled: o.hasAttribute('data-dmo-reference-excluded') ? false : o.disabled })) : null,
      suggest: element.getAttribute('data-dmo-suggest'), list: element.getAttribute('list')
    });
  }
  function restore(state) {
    for (const [el, before] of state.original) {
      el.disabled = before.disabled;
      before.options?.forEach(o => { o.option.hidden = o.hidden; o.option.disabled = o.disabled; });
      if (before.suggest !== null) el.setAttribute('data-dmo-suggest', before.suggest);
      if (before.list === null) el.removeAttribute('list'); else el.setAttribute('list', before.list);
    }
    state.lists.forEach(l => l.remove()); state.lists = [];
  }
  function status(form, message) {
    let note = form.querySelector('[data-dmo-reference-status]');
    if (!note && message) {
      note = document.createElement('p'); note.dataset.dmoReferenceStatus = ''; note.setAttribute('role', 'status');
      form.append(note);
    }
    if (note) { note.textContent = message; note.hidden = !message; }
  }
  async function update(form) {
    const state = states.get(form);
    const input = field(form, form.dataset.dmoReferenceName);
    if (!state || !input) return;
    const reference = input.value.trim();
    state.controller?.abort();
    const version = ++state.version;
    if (!reference) { restore(state); status(form, ''); state.reference = ''; return; }
    const controller = new AbortController(); state.controller = controller;
    const parentChanged = state.reference !== reference;
    const lot = field(form, 'lote') || field(form, 'lot');
    const load = async lotValue => {
      const query = new URLSearchParams({ scope: form.dataset.dmoReferenceScope, reference });
      if (lotValue) query.set('lot', lotValue);
      const response = await fetch('/OpcoesDaReferencia?' + query, { signal: controller.signal, credentials: 'same-origin' });
      if (!response.ok) throw new Error('options');
      return response.json();
    };
    form.setAttribute('data-dmo-reference-busy', 'true');
    try {
      let options = await load(parentChanged ? '' : lot?.value);
      if (version !== state.version || !form.isConnected || input.value.trim() !== reference) return;
      let changed = false;
      if (parentChanged && lot?.value) {
        const lots = options[lot.name];
        if (lots && !lots.some(v => v.replace(/^0+(?=\d)/, '') === lot.value.replace(/^0+(?=\d)/, ''))) { lot.value = ''; changed = true; }
        else if (lots) options = await load(lot.value);
      }
      if (version !== state.version || !form.isConnected || input.value.trim() !== reference) return;
      restore(state);
      for (const [name, allowed] of Object.entries(options)) {
        const el = field(form, name);
        if (!el) continue;
        remember(state, el);
        const valid = new Set(allowed.map(v => String(v).toUpperCase()));
        if (el.options) {
          for (const o of el.options) {
            if (!o.value) continue;
            const before = state.original.get(el).options.find(x => x.option === o);
            const outside = !valid.has(o.value.toUpperCase());
            o.hidden = before.hidden || outside; o.disabled = before.disabled || outside;
          }
          if (el.value && el.selectedOptions[0]?.disabled) { el.value = ''; changed = true; }
        } else {
          // Referência/lote e reparador continuam a aceitar escrita; as sugestões passam a ser contextuais.
          el.removeAttribute('data-dmo-suggest');
          const list = document.createElement('datalist'); list.id = 'dmo-reference-options-' + (++sequence);
          for (const value of allowed) { const o = document.createElement('option'); o.value = value; list.append(o); }
          form.append(list); state.lists.push(list); el.setAttribute('list', list.id);
          if (parentChanged && !['lot','lote'].includes(name) && el.value && document.activeElement !== el && !valid.has(el.value.toUpperCase())) {
            el.value = ''; changed = true;
          }
        }
      }
      state.reference = reference;
      status(form, '');
      // O pedido que a pesquisa já fez pode ter levado um critério da referência anterior.
      if (changed && form.method.toLowerCase() === 'get') form.requestSubmit();
    } catch (error) {
      if (controller.signal.aborted || version !== state.version || !form.isConnected) return;
      for (const el of controls(form)) {
        remember(state, el);
        if (el.options) { el.value = ''; el.disabled = true; }
      }
      status(form, 'Não foi possível atualizar as opções desta referência. Volte a escrever a referência para tentar de novo.');
    } finally {
      if (version === state.version) form.removeAttribute('data-dmo-reference-busy');
    }
  }
  function bind() {
    document.querySelectorAll('form[data-dmo-reference-scope]').forEach(form => {
      if (states.has(form) || field(form, form.dataset.dmoReferenceName)?.readOnly) return;
      const state = { original: new Map(), lists: [], reference: '', version: 0, controller: null, timer: null };
      states.set(form, state);
      controls(form).forEach(el => remember(state, el));
      const queue = event => {
        const ref = field(form, form.dataset.dmoReferenceName);
        if (![ref, field(form, 'lote'), field(form, 'lot')].includes(event.target)) return;
        // Invalida já a resposta anterior; a escrita rápida só faz um pedido novo.
        state.controller?.abort(); ++state.version;
        clearTimeout(state.timer); state.timer = setTimeout(() => update(form), 150);
      };
      form.addEventListener('input', queue);
      form.addEventListener('change', queue);
      // O Enter do Lote também deve aplicar os filtros quando o formulário não tem botão.
      form.addEventListener('keydown', event => {
        if (event.key === 'Enter' && ['lote', 'lot'].includes(event.target.name)) {
          event.preventDefault(); form.requestSubmit();
        }
      });
      update(form);
    });
  }
  bind();
  document.addEventListener('dmo:troca', () => {
    bind();
    document.querySelectorAll('form[data-dmo-reference-scope]').forEach(form => update(form));
  });
})();

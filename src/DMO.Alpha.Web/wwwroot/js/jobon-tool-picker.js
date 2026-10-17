// Os campos CM, MF e BQ do Job On procuram ao escrever (o Diogo, 16/10/2026:
// um dropdown com todas as ferramentas pesa, e quem cria o Job On sabe a
// referência). O servidor devolve no máximo 20 sugestões, filtradas pelos
// dados da folha: a máquina e a referência da produção e, na CM, a MF já
// escolhida. «Mostrar todas» procura no catálogo inteiro. A procura nunca
// muda a ferramenta já escolhida; a escolha é sempre um clique.
(() => {
  function init() {
    const tools = document.getElementById('lightTools');
    if (!tools) return; // a folha só existe quando se cria ou edita
    const machine = document.getElementById('lightMachine');
    const reference = document.getElementById('lightReference');
    const showAll = document.getElementById('lightShowAllTools');
    const status = document.getElementById('lightToolFilterStatus');
    if (!tools || !machine || !reference || !showAll || !status) return;
    const mf = tools.querySelector('select[data-facts-type="MF"]');

    function label(item) {
      return item.reference + ' · Lote ' + item.lot + ' · ' + (item.machineCodes || 'Máquinas por indicar');
    }

    function source(type) {
      return async term => {
        const query = new URLSearchParams({ handler: 'ToolCandidates', type, q: term, reference: reference.value.trim(), all: String(showAll.checked) });
        if (machine.value) query.set('machine_id', machine.value);
        if (type === 'CM' && mf && mf.value) query.set('mf_tool_id', mf.value);
        const response = await fetch('/JobOn?' + query, { credentials: 'same-origin' });
        if (!response.ok) throw new Error('Candidates unavailable');
        const page = await response.json();
        const items = page.items.map(item => ({ value: item.toolId, label: label(item) }));
        let note = page.filtro;
        if (page.total > items.length) note += ' · ' + items.length + ' de ' + page.total + '. Escreva para afinar.';
        return {
          items,
          note,
          empty: term
            ? 'Sem resultados para «' + term + '»' + (showAll.checked ? '.' : '. Pode marcar «Mostrar todas as ferramentas».')
            : (showAll.checked ? 'Sem ferramentas ' + type + ' no catálogo.' : 'Sem sugestões para esta máquina e referência. Escreva ou marque «Mostrar todas as ferramentas».')
        };
      };
    }

    tools.querySelectorAll('select[data-facts-type]').forEach(select => {
      select.dmoSource = source(select.dataset.factsType);
    });
    // O CARTÃO (o Diogo, 17/10/2026): carregar em CM abre «Associar» ou
    // «Criar». «Associar» mostra a procura; escolhida a ferramenta, a
    // procura fecha e o cartão mostra só a referência, o lote e os dados
    // técnicos. As máquinas ficam só nas linhas da procura.
    const filterBar = showAll.closest('.job-tool-filter-bar');
    function updateFilterBar() {
      if (filterBar) filterBar.hidden = !tools.querySelector('.is-associating');
    }
    tools.querySelectorAll('[data-tool-card]').forEach(card => {
      const type = card.dataset.toolCard;
      const select = card.querySelector('select[data-facts-type]');
      const menu = card.querySelector('.job-tool-menu');
      const search = card.querySelector('[data-tool-search]');
      const chosen = card.querySelector('[data-tool-chosen]');
      const title = card.querySelector('[data-tool-title]');
      const hint = card.querySelector('[data-tool-hint]');
      const associate = card.querySelector('[data-tool-associate]');
      const clear = card.querySelector('[data-tool-clear]');
      if (!select || !menu || !search || !chosen || !title || !associate || !clear) return;

      function render() {
        const has = select.value !== '';
        chosen.hidden = !has;
        search.hidden = !card.classList.contains('is-associating');
        if (hint) hint.textContent = has ? '' : 'Associar ou criar';
        associate.textContent = (has ? 'Trocar ' : 'Associar ') + type;
        clear.hidden = !has;
        updateFilterBar();
      }
      function stopSearch() {
        card.classList.remove('is-associating');
        render();
      }

      associate.addEventListener('click', () => {
        menu.open = false;
        card.classList.add('is-associating');
        render();
        const input = search.querySelector('input.dmo-suggest-combo') || select;
        input.focus();
      });
      clear.addEventListener('click', () => {
        menu.open = false;
        select.value = '';
        title.textContent = '';
        select.dispatchEvent(new Event('change', { bubbles: true }));
      });
      select.addEventListener('change', () => {
        const option = select.selectedOptions[0];
        if (select.value !== '' && option) {
          // «5447 · Lote 3 · B1 · C3» → «5447 · Lote 3».
          title.textContent = option.text.trim().split(' · ').slice(0, 2).join(' · ');
          card.classList.remove('is-associating');
        }
        render();
      });
      search.addEventListener('keydown', event => {
        if (event.key === 'Escape') stopSearch();
      });
      // Fechar o menu ao clicar fora dele.
      document.addEventListener('click', event => {
        if (menu.open && !menu.contains(event.target)) menu.open = false;
      });
      render();
    });

    showAll.addEventListener('change', () => {
      status.textContent = showAll.checked
        ? 'Catálogo completo: escreva a referência ou o lote.'
        : '';
    });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();

// «Registar sem Job On» (Diogo, 22/10/2026): o lote mostra só os lotes da BQ
// escolhida. Sem JavaScript, o lote mostra todos (o servidor valida).
(() => {
  const ligar = () => {
    const ref = document.querySelector('[data-bq-sem-jobon-ref]');
    const lote = document.querySelector('[data-bq-sem-jobon-lote]');
    if (!ref || !lote || ref.dataset.ligado) return;
    ref.dataset.ligado = '1';
    const filtrar = () => {
      for (const option of lote.options) {
        if (!option.value) continue;
        const mostra = option.dataset.ref === ref.value;
        option.hidden = !mostra;
        option.disabled = !mostra;
        if (!mostra && option.selected) lote.value = '';
      }
    };
    ref.addEventListener('change', filtrar);
    filtrar();
  };
  ligar();
  document.addEventListener('dmo:troca', ligar);
})();

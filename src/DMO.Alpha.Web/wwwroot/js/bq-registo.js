// Boquilhas → Registo com «atualizar só o que muda» (Diogo, 17/10/2026).
// Abrir outra boquilha no painel lateral troca só o registo (DmoTroca) e o
// registo fica à vista. Gravar um movimento não mexe no scroll. Sem JavaScript, os cliques abrem a página.
(() => {
  let abrir = false;
  document.addEventListener('dmo:list-open', (event) => {
    abrir = Boolean(event.target.closest && event.target.closest('#bqCurrentLines'));
  });
  document.addEventListener('dmo:troca', () => {
    if (!abrir) return;
    abrir = false;
    const registo = document.querySelector('#bqRegistoAberto .register-open');
    if (!registo) return;
    const topo = registo.getBoundingClientRect().top;
    if (topo < 0 || topo > window.innerHeight - 120) registo.scrollIntoView({ block: 'start' });
  });
})();

// A data e a hora em dois campos (Diogo, 28/10/2026: «muitas vezes só tenho de
// alterar a hora»): ao mudar um deles, o campo junto (occurred_at) recebe os dois.
// Delegado no documento, porque o registo é trocado sem recarregar a página.
(() => {
  const juntar = (alvoId) => {
    const alvo = document.getElementById(alvoId);
    const dia = document.querySelector(`[data-bq-dia="${alvoId}"]`);
    const hora = document.querySelector(`[data-bq-hora="${alvoId}"]`);
    if (!alvo || !dia || !hora || !dia.value || !hora.value) return;
    alvo.value = `${dia.value}T${hora.value.slice(0, 5)}`;
    alvo.dispatchEvent(new Event('input', { bubbles: true }));
    alvo.dispatchEvent(new Event('change', { bubbles: true }));
  };
  const aoMudar = (event) => {
    const campo = event.target.closest && event.target.closest('[data-bq-dia],[data-bq-hora]');
    if (campo) juntar(campo.dataset.bqDia || campo.dataset.bqHora);
  };
  document.addEventListener('input', aoMudar);
  document.addEventListener('change', aoMudar);
})();

// Boquilhas → Registo com «atualizar só o que muda» (Diogo, 17/10/2026).
// Abrir outra boquilha no painel lateral ou na procura troca só o registo
// (DmoTroca): a procura fecha, como numa página nova, e o registo fica à
// vista, com o foco no tipo do próximo movimento. Gravar um movimento não
// mexe na procura nem no scroll. Sem JavaScript, os cliques abrem a página.
(() => {
  let abrir = false;
  document.addEventListener('dmo:list-open', (event) => {
    abrir = Boolean(event.target.closest && event.target.closest('#bqProcura, #bqCurrentLines'));
  });
  document.addEventListener('dmo:troca', () => {
    if (!abrir) return;
    abrir = false;
    const registo = document.querySelector('#bqRegistoAberto .register-open');
    if (!registo) return;
    const procura = document.querySelector('details.bq-register-search');
    if (procura) procura.open = false;
    const topo = registo.getBoundingClientRect().top;
    if (topo < 0 || topo > window.innerHeight - 120) registo.scrollIntoView({ block: 'start' });
  });
})();

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

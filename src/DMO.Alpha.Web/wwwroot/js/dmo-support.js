// Contextual pages slide in beside the workspace. Main navigation never enters here.
// A marcação é a estrutura fixa dos painéis (<dmo-painel>, _DmoSupportPanel); o
// abrir/fechar é este, próprio (data-dmo-painel-proprio), por causa do iframe.
(() => {
  'use strict';
  const panel = document.getElementById('dmoSupport');
  if (!panel || window.frameElement?.classList.contains('dmo-support-frame')) return;
  const frame = panel.querySelector('iframe');
  const title = panel.querySelector('h2');
  const full = panel.querySelector('#dmoSupportFull');
  const confirm = panel.querySelector('.dmo-painel-aviso');
  const loading = panel.querySelector('.dmo-support-loading');
  const refresh = document.querySelector('.dmo-support-refresh');
  // «Abrir página» abre sempre o mesmo separador para a mesma página: um
  // segundo clique volta a esse separador em vez de abrir outro, sem fim
  // (Diogo, 27/10/2026: «posso spammar abrir a mesma pagina sem fim»).
  function separadorDe(href) {
    try { const u = new URL(href, location.href); u.searchParams.delete('painel'); return 'dmo:' + u.pathname + u.search; }
    catch { return '_blank'; }
  }
  function apontar(href) { full.href = href; full.target = separadorDe(href); }
  // A ficha da ferramenta abre bloqueada, como o Job On: só depois de «Editar»
  // os campos aceitam mudanças, para nada mudar por engano (Diogo, 27/10/2026).
  const bloqueadas = new Set(['/Ferramentas/Ficha']);
  function bloquear(doc, sim) {
    doc.documentElement.classList.toggle('dmo-bloqueado', sim);
    doc.querySelectorAll('form[method="post"] :is(input:not([type=hidden]),select,textarea,button), button[form]').forEach(campo => {
      if (sim) { if (!campo.disabled) { campo.disabled = true; campo.dataset.dmoBloqueado = ''; } }
      else if ('dmoBloqueado' in campo.dataset) { campo.disabled = false; delete campo.dataset.dmoBloqueado; }
      // Bloqueado, o campo perde a caixa: vazio, não se via nada (o Gargalo
      // da BQ, Diogo 27/10/2026). Fica com «—» até ao «Editar».
      if (sim && campo.matches('input') && !campo.value && !campo.placeholder) { campo.placeholder = '—'; campo.dataset.dmoBloqueadoVazio = ''; }
      else if (!sim && 'dmoBloqueadoVazio' in campo.dataset) { campo.removeAttribute('placeholder'); delete campo.dataset.dmoBloqueadoVazio; }
    });
    // O «Editar» também dentro da ficha, no cartão «Editar», onde fica o
    // «Guardar»: o do topo do painel sai do ecrã quando se desce.
    let dentro = doc.getElementById('dmoDesbloquear');
    if (sim && !dentro) {
      const acoes = doc.querySelector('#ferrEditar .dmo-card-topo-acoes');
      if (acoes) {
        dentro = doc.createElement('button');
        dentro.type = 'button'; dentro.id = 'dmoDesbloquear'; dentro.className = 'dmo-button'; dentro.textContent = 'Editar';
        dentro.addEventListener('click', desbloquear);
        acoes.prepend(dentro);
      }
    }
    if (!sim) dentro?.remove();
  }
  function desbloquear() {
    const doc = frame.contentDocument; if (!doc) return;
    bloquear(doc, false);
    doc.querySelector('#ferrEditar :is(input:not([type=hidden]),select)')?.focus();
  }
  let opener, dirty = false, operation = false, aberta = null;
  // «Voltar» no cabeçalho do painel (Diogo, 28/10/2026: «usar o voltar em vez do
  // carregar fora»): aparece quando se navegou dentro do painel e recua uma
  // página; o «Fechar» fecha. Clicar fora já não faz nada.
  let paginas = 0;
  const voltar = document.createElement('button');
  voltar.type = 'button'; voltar.className = 'dmo-button'; voltar.textContent = 'Voltar'; voltar.hidden = true;
  voltar.dataset.dmoPainelVoltar = '';
  full.before(voltar);
  voltar.addEventListener('click', () => {
    if (paginas < 2) return;
    if (dirty) { requestClose(); return; }
    paginas -= 2;
    try { frame.contentWindow.history.back(); } catch { /* fica */ }
  });
  const destinations = new Map([
    ['/Ferramentas/Ficha', 'Ficha da ferramenta'],
    // O título vem do h1 da página (diz qual é a ferramenta).
    ['/Ferramentas/Utilizacao', ''],
    ['/Controlo/Peso', 'Peso'],
    ['/Controlo/PesoComparar', 'Comparar peso'],
    ['/Controlo/Pegamentos', 'Pegamentos'],
    ['/Controlo/Puncoes', 'PU NNPB'],
    ['/Controlo/Approve', 'Revisão do controlo'],
    ['/ReparacaoInterna/Consultar', 'Consulta de reparação']
  ]);
  // Uma página que se abre a partir de outra dentro do painel (o Job On do aviso
  // do Peso, a ficha do duplo clique) dá o título ao painel: o painel dizia
  // «Peso» com o Job On à vista (Diogo, 27/10/2026). O Planeamento abre com a
  // largura quase toda.
  const outrasPaginas = new Map([['/JobOn', 'Job On'], ['/JobOn/Folha', 'Job On']]);
  const largas = new Set(['/JobOn']);
  // A folha do Job On no painel é só um resumo para consulta rápida, com «Abrir folha» (Diogo, 27/10/2026).
  const resumos = new Set(['/JobOn/Folha']);
  function acertarPainel(caminho, doc) {
    title.textContent = destinations.get(caminho) || outrasPaginas.get(caminho)
      || doc?.querySelector('main h1')?.textContent.trim() || title.textContent;
    panel.dataset.largura = largas.has(caminho) ? 'total' : resumos.has(caminho) ? 'media' : 'larga';
  }
  function close() {
    panel.close(); frame.removeAttribute('src'); dirty = false; aberta = null; paginas = 0; voltar.hidden = true;
    document.documentElement.classList.remove('dmo-support-open', 'dmo-painel-aberto');
    confirm.hidden = true;
    if (operation) refresh.hidden = false;
    devolverFoco();
  }
  // O foco volta para quem abriu o painel. Uma linha de tabela não é
  // focável: sem isto o foco caía no <body> e o teclado perdia o lugar.
  function devolverFoco() {
    opener?.focus?.({ preventScroll: true });
    if (document.activeElement && document.activeElement !== document.body) return;
    const lista = opener?.closest?.('[data-dmo-list]') || opener?.querySelector?.('[data-dmo-list]');
    if (lista) { lista.tabIndex = -1; lista.focus({ preventScroll: true }); return; }
    opener?.querySelector?.(':is(a,button,input,select,textarea,[tabindex])')?.focus?.({ preventScroll: true });
  }
  function requestClose() {
    if (!dirty) return close();
    confirm.hidden = false;
    confirm.querySelector('button').focus();
  }
  // Um painel de cada vez (Diogo, 27/10/2026: «consigo spammar sliding
  // windows»). Com o painel aberto, a mesma página não volta a abrir e outra
  // página troca a que lá está, no mesmo painel; com alterações por guardar, o
  // aviso aparece em vez da troca.
  const semPainel_ = href => { try { const u = new URL(href, location.href); u.searchParams.delete('painel'); return u.pathname + u.search; } catch { return href; } };
  function tryOpen(href, source) {
    if (source?.closest('.dmo-shell-header,nav')) return false;
    // Fora do painel: um contentor [data-dmo-sem-painel="<seletor>"] deixa os
    // links que casam com o seletor (vazio: todos) navegar como sempre. O
    // «Registar» do Resumo, para quem tem o Criar, abre a página de registo
    // (Diogo, 27/10/2026); quem só tem o Ver continua no painel.
    const semPainel = source?.closest?.('[data-dmo-sem-painel]');
    if (semPainel) {
      try { if (source.matches(semPainel.dataset.dmoSemPainel || '*')) return false; } catch { return false; }
    }
    let url;
    try { url = new URL(href, location.href); } catch { return false; }
    if (url.origin !== location.origin || !destinations.has(url.pathname)) return false;
    // Um link para a própria página (corrigir uma leitura nos Pegamentos) fica
    // nela: no painel, apagar lá deixava a linha na página por trás (Diogo,
    // 27/10/2026: «o x apaga a leitura mas não apaga a linha»).
    if (url.pathname === location.pathname) return false;
    if (panel.open) {
      if (semPainel_(url.href) === aberta) return true;
      if (dirty) { requestClose(); return true; }
    }
    // Um <dmo-painel> com alterações por guardar recusa fechar: fica ele, com o
    // aviso, e o painel de apoio não abre por cima (eram dois painéis abertos).
    if (!panel.open && window.DmoPainel?.fecharTodos?.() === false) return true;
    aberta = semPainel_(url.href);
    opener = source || document.activeElement;
    dirty = false; operation = false; confirm.hidden = true;
    acertarPainel(url.pathname);
    apontar(url.href);
    url.searchParams.set('painel', '1');
    loading.hidden = false;
    paginas = 0; voltar.hidden = true;
    frame.src = url.href;
    if (!panel.open) panel.showModal();
    // O foco não fica no «Abrir página» (parecia um botão apagado).
    frame.focus({ preventScroll: true });
    document.documentElement.classList.add('dmo-support-open', 'dmo-painel-aberto');
    return true;
  }
  frame.addEventListener('load', () => {
    if (!panel.open || !frame.hasAttribute('src')) return;
    loading.hidden = true;
    paginas += 1; voltar.hidden = paginas < 2;
    let doc;
    try { doc = frame.contentDocument; } catch { return; }
    if (!doc) return;
    doc.documentElement.dataset.dmoEmbedded = 'true';
    acertarPainel(frame.contentWindow.location.pathname, doc);
    const bloquear_ = bloqueadas.has(frame.contentWindow.location.pathname);
    if (bloquear_) bloquear(doc, true);
    dirty = false;
    apontar(frame.contentWindow.location.href.replace(/([?&])painel=1(&|$)/, (_, before, after) => after ? before : ''));
    doc.addEventListener('input', event => { if (event.target.closest('form[method="post"],form[method="POST"]')) dirty = true; });
    doc.addEventListener('change', event => { if (event.target.closest('form[method="post"],form[method="POST"]')) dirty = true; });
    doc.addEventListener('submit', event => { if (event.target.method.toLowerCase() === 'post') operation = true; });
    // Escape from inside the frame closes the outer drawer only if no local dialog is open.
    doc.addEventListener('keydown', event => {
      if (event.key === 'Escape' && !doc.querySelector('dialog[open],.dmo-modal-backdrop.open')) {event.preventDefault(); requestClose();}
    });
  });
  // Clicar fora não faz nada (Diogo, 28/10/2026: clicar fora abria sempre o que
  // estava por baixo e trazia problemas): o painel fecha no «Fechar» ou no Esc,
  // e recua no «Voltar».
  panel.addEventListener('cancel', event => {event.preventDefault(); requestClose();});
  panel.querySelector('[data-dmo-painel-fechar]').addEventListener('click', requestClose);
  panel.querySelector('[data-dmo-painel-continuar]').addEventListener('click', () => {confirm.hidden=true; frame.focus();});
  panel.querySelector('[data-dmo-painel-descartar]').addEventListener('click', close);
  refresh.querySelector('[data-support-refresh]').addEventListener('click', () => location.reload());
  refresh.querySelector('[data-support-dismiss]').addEventListener('click', () => {refresh.hidden=true;});
  document.addEventListener('click', event => {
    if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const link = event.target.closest('a[href]');
    if (!link || link.target || link.hasAttribute('download')) return;
    if (tryOpen(link.href, link)) {event.preventDefault(); event.stopImmediatePropagation();}
  }, true);
  window.DmoSupport = {tryOpen};
})();

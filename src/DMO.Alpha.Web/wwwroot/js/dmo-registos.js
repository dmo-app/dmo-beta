// Registos (Diogo, 27/10/2026): o campo Procurar sugere Job On, referências,
// ferramentas, operadores e reparadores; escolher uma sugestão aplica esse
// filtro. Clicar numa ferramenta abre o painel dela (com «Filtrar»); clicar
// numa produção ou num registo abre o painel do Job On (com «Opções»).
(function () {
  var form = document.getElementById('regProcura');
  var campo = document.getElementById('regTexto');
  var caixa = document.getElementById('regSugestoes');
  var tipo = document.getElementById('regTipo');
  var valor = document.getElementById('regValor');
  if (!form || !campo || !caixa) return;

  var pedido = 0, temporizador = null, ativa = -1, opcoes = [];

  function esconder() { caixa.hidden = true; caixa.innerHTML = ''; opcoes = []; ativa = -1; }

  function escolher(s) {
    campo.value = s.texto;
    tipo.value = s.tipo;
    valor.value = s.valor;
    esconder();
    form.submit();
  }

  function marcar(i) {
    opcoes.forEach(function (o, j) { o.el.classList.toggle('reg-sug-ativa', j === i); });
    ativa = i;
    if (opcoes[i]) opcoes[i].el.scrollIntoView({ block: 'nearest' });
  }

  function mostrar(lista) {
    caixa.innerHTML = '';
    opcoes = [];
    ativa = -1;
    if (!lista.length) { caixa.hidden = true; return; }
    var grupo = null;
    lista.forEach(function (s) {
      if (s.grupo !== grupo) {
        grupo = s.grupo;
        var g = document.createElement('div');
        g.className = 'reg-sug-grupo';
        g.textContent = grupo;
        caixa.appendChild(g);
      }
      var el = document.createElement('div');
      el.className = 'reg-sug';
      el.setAttribute('role', 'option');
      el.textContent = s.texto;
      if (s.detalhe) {
        var d = document.createElement('span');
        d.textContent = s.detalhe;
        el.appendChild(d);
      }
      el.addEventListener('mousedown', function (e) { e.preventDefault(); escolher(s); });
      caixa.appendChild(el);
      opcoes.push({ el: el, s: s });
    });
    caixa.hidden = false;
  }

  campo.addEventListener('input', function () {
    // Escrever de novo desfaz a escolha anterior: volta a ser texto livre.
    tipo.value = '';
    valor.value = '';
    clearTimeout(temporizador);
    var texto = campo.value.trim();
    if (texto.length < 2) { esconder(); return; }
    temporizador = setTimeout(function () {
      var meu = ++pedido;
      fetch('/Registos?handler=Sugestoes&q=' + encodeURIComponent(texto), { headers: { Accept: 'application/json' } })
        .then(function (r) { return r.ok ? r.json() : []; })
        .then(function (lista) { if (meu === pedido) mostrar(lista); })
        .catch(function () { });
    }, 200);
  });

  campo.addEventListener('keydown', function (e) {
    if (caixa.hidden || !opcoes.length) return;
    if (e.key === 'ArrowDown') { e.preventDefault(); marcar(Math.min(ativa + 1, opcoes.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); marcar(Math.max(ativa - 1, 0)); }
    else if (e.key === 'Enter' && ativa >= 0) { e.preventDefault(); escolher(opcoes[ativa].s); }
    else if (e.key === 'Escape') { esconder(); }
  });
  campo.addEventListener('blur', function () { setTimeout(esconder, 150); });

  // Os painéis: o HTML vem do servidor; o título e as ações vão para o topo do painel.
  function abrirPainel(id, url, linha) {
    var painel = document.getElementById(id);
    if (!painel || !window.DmoPainel) { return; }
    fetch(url, { headers: { Accept: 'text/html' } })
      .then(function (r) { if (!r.ok) throw new Error(r.status); return r.text(); })
      .then(function (html) {
        var corpo = painel.querySelector('[data-reg-corpo]');
        corpo.innerHTML = html;
        var raiz = corpo.querySelector('[data-reg-titulo]');
        painel.querySelector('.dmo-painel-titulo').textContent = raiz ? raiz.getAttribute('data-reg-titulo') : '';
        var acoes = painel.querySelector('[data-reg-acoes]');
        var modelo = corpo.querySelector('template[data-reg-acoes-html]');
        acoes.innerHTML = '';
        if (modelo) acoes.appendChild(modelo.content.cloneNode(true));
        document.querySelectorAll('tr.reg-escolhida').forEach(function (t) { t.classList.remove('reg-escolhida'); });
        if (linha) linha.classList.add('reg-escolhida');
        window.DmoPainel.abrir(id);
      })
      .catch(function () { });
  }

  function ferramenta(toolId, linha) {
    var aqui = location.pathname + location.search;
    abrirPainel('regPainelFerramenta', '/Registos?handler=Ferramenta&tool_id=' + encodeURIComponent(toolId) + '&voltar=' + encodeURIComponent(aqui), linha);
  }

  function jobOn(id, linha) {
    abrirPainel('regPainelJobOn', '/Registos?handler=JobOn&id=' + encodeURIComponent(id), linha);
  }

  document.addEventListener('click', function (e) {
    if (e.target.closest('a, summary, .dmo-options-list')) return;
    var f = e.target.closest('[data-reg-ferramenta]');
    if (f) { ferramenta(f.getAttribute('data-reg-ferramenta'), f.closest('tr')); return; }
    var tr = e.target.closest('#regResultados tbody tr');
    if (!tr) return;
    var j = tr.getAttribute('data-reg-jobon');
    var t = tr.getAttribute('data-reg-tool');
    if (j) jobOn(j, tr);
    else if (t) ferramenta(t, tr);
  });

  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Enter') return;
    var f = e.target.closest && e.target.closest('[data-reg-ferramenta]');
    if (f) { e.preventDefault(); ferramenta(f.getAttribute('data-reg-ferramenta'), f.closest('tr')); }
  });
})();

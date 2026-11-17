// O ENTER NA PROCURA DO JOB ON ENTRA NA PRODUÇÃO (Diogo, 10/11/2026: «ao
// carregar Enter com referência e número de produção deveria entrar»; «só
// me deixa criar novo»). Antes, o Enter recarregava a página com o texto
// escrito e não abria nada; com só o início da referência («3245») o
// dropdown das produções ficava vazio.
//
// Agora, no campo da referência ou no dropdown «Produção»:
// - com produções no dropdown, o Enter abre a escolhida (a mais recente vem
//   já escolhida), como escolher no dropdown;
// - com só o início de uma referência que é a única a começar assim, o
//   Enter completa-a («3245» → «3245C002»), o dropdown enche-se e fica com
//   o foco: outro Enter abre a produção escolhida.
// O Enter nunca cria um Job On novo; isso continua no «Criar Job On».
// Sem JavaScript, o Enter continua a pedir a página com a referência.
(function () {
  'use strict';

  var ESPERA_MAXIMA = 5000;

  function campo() { return document.getElementById('lightSearch'); }
  function alvo() { return document.getElementById('lightProcura'); }
  function dropdown() { return document.getElementById('lightProcuraProducoes'); }

  function aEsperar() {
    var zona = alvo();
    return !!(zona && zona.hasAttribute('data-dmo-procura-espera'));
  }

  // Espera que a procura ao escrever traga a lista do texto atual.
  function quandoPronto(depois) {
    var inicio = Date.now();
    (function ver() {
      if (!aEsperar()) { depois(); return; }
      if (Date.now() - inicio > ESPERA_MAXIMA) return;
      window.setTimeout(ver, 50);
    })();
  }

  function abrirEscolhida() {
    var select = dropdown();
    if (!select || select.disabled || !select.value || aEsperar()) return false;
    window.location.href = select.value;
    return true;
  }

  // A referência completa que começa pelo texto escrito, quando é só uma.
  function completar(texto, depois) {
    var url = '/Sugestoes?' + new URLSearchParams({ fonte: 'referencias', q: texto });
    fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(function (response) { return response.ok ? response.json() : []; })
      .then(function (itens) {
        var procurado = texto.toUpperCase();
        var refs = (itens || []).map(function (item) { return String(item.value || ''); });
        var iguais = refs.filter(function (ref) { return ref.toUpperCase() === procurado; });
        var comecam = refs.filter(function (ref) { return ref.toUpperCase().indexOf(procurado) === 0; });
        var escolhida = iguais.length === 1 ? iguais[0] : comecam.length === 1 ? comecam[0] : refs.length === 1 ? refs[0] : null;
        depois(escolhida);
      })
      .catch(function () { depois(null); });
  }

  function enterNoCampo(event) {
    var input = campo();
    if (!input || event.target !== input) return;
    event.preventDefault();
    var texto = input.value.trim();
    if (!texto) return;
    quandoPronto(function () {
      if (input.value.trim() !== texto) return;
      if (abrirEscolhida()) return;
      completar(texto, function (referencia) {
        if (!referencia || input.value.trim() !== texto) return;
        if (referencia !== input.value) {
          input.value = referencia;
          input.dispatchEvent(new Event('input', { bubbles: true }));
        }
        // A procura ao escrever espera 250 ms antes de pedir: dá-lhe tempo
        // para começar e depois espera pela lista.
        window.setTimeout(function () {
          quandoPronto(function () {
            var select = dropdown();
            if (input.value !== referencia || !select || select.disabled) return;
            select.focus();
          });
        }, 300);
      });
    });
  }

  // Ouvido no documento, depois do campo: se a lista das sugestões escolheu
  // uma referência com as setas e o Enter, ela já tratou do Enter.
  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Enter' || event.defaultPrevented || event.isComposing
      || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
    var target = event.target;
    if (!target || !target.id) return;
    if (target.id === 'lightSearch') { enterNoCampo(event); return; }
    if (target.id === 'lightProcuraProducoes') {
      event.preventDefault();
      abrirEscolhida();
    }
  });
})();

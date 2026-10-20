// PU NNPB, «Número da punção» (deep sweep 18/10/2026, B-pegamentos-2). Com a
// rede lenta, o campo ficava com o número enviado enquanto o POST esperava:
// «10» Enter e logo «11» Enter gravava «1011», e o que se escrevia sem Enter
// perdia-se quando a folha (#puFolha) trocava com a resposta.
// - Ao enviar, o número enviado fica selecionado no campo: o que se escreve a
//   seguir substitui-o, não se junta. O mesmo quando o DmoTroca volta a
//   escrever, no campo novo, um número que ainda espera (aria-busy).
// - O que se escreveu e não foi enviado volta ao campo novo depois da troca,
//   se ele vier vazio e sem outro envio à espera.
// Sem resposta (a ligação falhou e nada trocou), o campo fica como está.
(function () {
  'use strict';

  var ID = 'pu-numero';
  var porEnviar = '';
  var campoVisto = document.getElementById(ID);

  function selecionar(campo) {
    try {
      campo.setSelectionRange(0, campo.value.length);
    } catch (erro) {
      // Um campo sem seleção de texto: fica como está.
    }
  }

  document.addEventListener('submit', function (evento) {
    var form = evento.target;
    var campo = form && form.elements ? form.elements.namedItem('numero') : null;
    if (!campo || campo.id !== ID) return;
    porEnviar = '';
    selecionar(campo);
  });

  document.addEventListener('input', function (evento) {
    if (evento.target && evento.target.id === ID) porEnviar = evento.target.value;
  });

  document.addEventListener('dmo:troca', function () {
    var campo = document.getElementById(ID);
    if (!campo || campo === campoVisto) return;
    campoVisto = campo;
    if (campo.form && campo.form.getAttribute('aria-busy') === 'true') {
      selecionar(campo);
    } else if (!campo.value && porEnviar) {
      campo.value = porEnviar;
    }
  });
})();
